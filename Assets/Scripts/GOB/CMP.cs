/* CMP.cs
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

/*! Dark Forces colormap file. */
public sealed class CMP : Asset {

	public CMP(string name, byte[] data, object createArgs)
		: base(name, Type.PAL) {
		if (data.Length != 8320) {
			throw new InvalidDataException("Dark forces colormap files are 8320 bytes long!");
		}

		colorMap = data;

		texture = new Texture2D(256, 33, TextureFormat.Alpha8, false, false);
		texture.anisoLevel = 0;
		texture.filterMode = FilterMode.Point;
		texture.wrapMode = TextureWrapMode.Clamp;

		Color32[] colors = new Color32[256*33];

		int ofs;
		for (int i = 0; i < 32; ++i) {
			ofs = 256*i;

			for (int j = 0; j < 256; ++j) {
				byte c = colorMap[ofs+j];
				colors[ofs+j] = new Color32(c, c, c, c);
			}
		}

		ofs = 256*32;
		for (int i = 0; i < 128; ++i) {
			byte c = colorMap[ofs+i];
			colors[ofs+i] = new Color32(c, c, c, c);
		}
		ofs = 256*32+128;
		for (int i = 0; i < 128; ++i) {
			colors[ofs+i] = new Color32(0, 0, 0, 0);
		}

		texture.SetPixels32(colors);
		texture.Apply();
	}

	protected override void OnDispose() {
		base.OnDispose();
		if (Application.isPlaying) {
			Object.Destroy(texture);
		} else {
			Object.DestroyImmediate(texture);
		}
	}

	public byte[] colorMap {
		get;
		private set;
	}

	public Texture2D texture {
		get;
		private set;
	}
	
}
