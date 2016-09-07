/* BM.cs
 *
 * The MIT License (MIT)
 *
 * Copyright (c) 2015 Joseph Riedel
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
*/

using UnityEngine;
using System.IO;
using System.Collections.Generic;

/*! Dark Forces bitmap. */
public sealed class BM : Asset {

	public class CreateArgs {
		public bool bMipmap = true;
		public int anisoLevel = 9;
		public FilterMode filterMode = FilterMode.Trilinear;
		public TextureWrapMode wrapMode = TextureWrapMode.Repeat;
		public TextureFormat textureFormat = TextureFormat.ARGB32;
		public PAL pal;
	}

	public class Frame : System.IDisposable {
		public Frame(Texture2D texture, bool bIsTransparent) {
			_texture = texture;
			_bIsTransparent = bIsTransparent;
			_wRecip = 1.0f / texture.width;
			_hRecip = 1.0f / texture.height;
		}

		public void Dispose() {
			if (_texture != null) {
				if (Application.isPlaying) {
					Object.Destroy(_texture);
				} else {
					Object.DestroyImmediate(_texture);
				}
				_texture = null;
			}
		}

		public Texture2D Texture { get { return _texture; } }
		public bool bIsTraparent { get { return _bIsTransparent; } }
		public float WRecip { get { return _wRecip; } }
		public float HRecip { get { return _hRecip; } }

		private Texture2D _texture;
		private bool _bIsTransparent;
		private float _wRecip;
		private float _hRecip;
	}

	public BM(string name, byte[] data, object createArgs) : base(name, Type.BM) {
		CreateArgs args = createArgs as CreateArgs;
		if (args == null) {
			throw new System.ArgumentException("BM requires BM.CreateArgs.");
		}

		using (MemoryStream backing = new MemoryStream(data, false))
		using (ByteStream stream = new ByteStream(backing)) {
			ParseBitmap(stream, args);
		}
	}

	public List<Frame> Frames { get { return _frames; } }
	public int FPS { get { return _fps; } }

	protected override void OnDispose() {
		base.OnDispose();
		foreach (var frame in _frames) {
			frame.Dispose();
		}
		_frames = null;
	}

	private void ParseBitmap(ByteStream stream, CreateArgs createArgs) {
		if ((stream.ReadString(3) != "BM ") || (stream.ReadByte() != 0x1e)){
			throw new InvalidDataException("Not a BM file.");
		}

		Header header = ReadHeader(stream, EHeaderType.FileHeader);
		DebugCheck.Assert(stream.Position == 32);

		if ((header.h == 1) && (header.h != 1)) {
			// multiple bitmaps in this file.
			_fps = stream.ReadByte();
			stream.Skip(1);

			long baseOfs = stream.Position;

			int[] offsets = new int[header.iy];
			for (int i = 0; i < offsets.Length; ++i) {
				offsets[i] = stream.ReadLittleInt32();
			}

			for (int i = 0; i < offsets.Length; ++i) {
				stream.SeekSet(offsets[i] + baseOfs);
				Header subHeader = ReadHeader(stream, EHeaderType.SubHeader);
				Frame frame = ReadColumns(stream, subHeader, null, createArgs);
				_frames.Add(frame);
			}
		} else {
			int[] columnOffsets = null;

			if (header.compressed != 0) {
				// read column offsets.
				stream.SeekSet(header.dataSize);
				columnOffsets = new int[header.w];

				for (int i = 0; i < columnOffsets.Length; ++i) {
					columnOffsets[i] = stream.ReadLittleInt32() + 32;
				}
			}

			Frame frame = ReadColumns(stream, header, columnOffsets, createArgs);
			_frames.Add(frame);
		}
	}

	public static Frame ReadColumns(ByteStream stream, Header header, int[] columnOffsets, CreateArgs createArgs) {

		try {
			Texture2D texture = new Texture2D(header.w, header.h, createArgs.textureFormat, createArgs.bMipmap, false);

			texture.anisoLevel = createArgs.anisoLevel;
			texture.filterMode = createArgs.filterMode;
			texture.wrapMode = createArgs.wrapMode;

			Color32[] pixels = new Color32[header.w*header.h];
			byte[] column = new byte[header.h];
			bool bIsTransparent = (header.transparent & 0x8) != 0;

			for (int x = 0; x < header.w; ++x) {
				if (columnOffsets != null) {
					stream.SeekSet(columnOffsets[x]);
				}

				DecodeColumn(stream, column, header.compressed);

				if (createArgs.textureFormat == TextureFormat.Alpha8) {
					int pixelOfs = x;

					for (int y = 0; y < header.h; ++y, pixelOfs += header.w) {
						byte color = column[y];
						pixels[pixelOfs] = new Color32(color, color, color, color);
					}

				} else {
					int pixelOfs = x;

					for (int y = 0; y < header.h; ++y, pixelOfs += header.w) {
						byte color = column[y];

						if (bIsTransparent && (color == 0)) {
							pixels[pixelOfs] = PAL.transparent;
						} else {
							pixels[pixelOfs] = createArgs.pal.colors[color];
						}
					}
				}
			}

			texture.SetPixels32(pixels);
			texture.Apply();

			return new Frame(texture, bIsTransparent);
		} catch (UnityException e) {
			throw e;
		}
	}

	private Header ReadHeader(ByteStream stream, EHeaderType headerType) {
		Header header = new Header();

		if (headerType == EHeaderType.FileHeader) {
			header.w = stream.ReadLittleShort16();
			header.h = stream.ReadLittleShort16();
			stream.Skip(2);
			header.iy = stream.ReadLittleShort16();
			header.transparent = stream.ReadByte();
			stream.Skip(1);
			header.compressed = stream.ReadLittleShort16();
			header.dataSize = stream.ReadLittleInt32();
			stream.Skip(12);
		} else {
			header.w = stream.ReadLittleShort16();
			header.h = stream.ReadLittleShort16();
			stream.Skip(20);
			header.transparent = stream.ReadByte();
			stream.Skip(3);
		}

		return header;
	}

	private static void DecodeColumn(ByteStream stream, byte[] columnOut, int compressed) {
		if (compressed == 0) {
			// uncompressed.
			for (int y = 0; y < columnOut.Length; ++y) {
				columnOut[y] = (byte)stream.ReadByte();	
			}
		} else if (compressed == 1) {
			// rle1
			for (int y = 0; y < columnOut.Length; ) {
				int code = stream.ReadByte();
				if (code > 128) {
					byte color = (byte)stream.ReadByte();
					int repeat = code & 0x7f;
					while (repeat-- > 0) {
						columnOut[y++] = color;
					}
				} else {
					while (code-- > 0) {
						columnOut[y++] = (byte)stream.ReadByte();
					}
				}
			}
		} else if (compressed == 2) {
			// rle2 (transparent coding)
			for (int y = 0; y < columnOut.Length; ) {
				int code = stream.ReadByte();
				if (code > 128) {
					int skip = code & 0x7f;
					while (skip-- > 0) {
						columnOut[y++] = 0;
					}
				} else {
					while (code-- > 0) {
						columnOut[y++] = (byte)stream.ReadByte();
					}
				}
			}
		}
	}

	private enum EHeaderType {
		FileHeader,
		SubHeader
	}

	public struct Header {
		public int w;
		public int h;
		public int iy;
		public int transparent;
		public int compressed;
		public int dataSize;
	}

	private List<Frame> _frames = new List<Frame>();
	private int _fps;
}