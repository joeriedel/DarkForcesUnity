/* VOC.cs
 *
 * The MIT License (MIT)
 *
 * Copyright (c) 2013 Joseph Riedel
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

/*! Creative Voice File as used in Dark Forces */
public sealed class VOC : SoundAsset {

	public VOC(string name, byte[] data, object createArgs) : base(name, (createArgs == null) ? SoundType.Positional : ((SoundType)createArgs), Type.VOC) {
		DecodeVOC(data);
	}

	public override SoundInstance CreateInstance() {
		if (_clipInstance != null) {
			_clipInstance.Reference();
			return _clipInstance;
		}
		return new VOCStreamingSoundInstance(_blocks, _blockSamples, Internal);
	}

	public override bool AudioSourceShouldLoop { get { return _audioSourceShouldLoop; } }
	public override bool IsLooping { get { return _loop; } }

	protected override void OnDispose() {
		base.OnDispose();

		if (_audioClip != null) {
			Object.Destroy(_audioClip);
			_audioClip = null;
		}
	}

	private void DecodeVOC(byte[] data) {
		using (MemoryStream backing = new MemoryStream(data, false))
		using (ByteStream stream = new ByteStream(backing)) {

			VOCHeader.Read(stream);

			Rate rate = new Rate();
			Rate _8block = new Rate();
			List<Block> blocks = new List<Block>();

			uint loop = 1;
							
			while (!stream.EOS) {
				int blockType = stream.ReadByte();

				if (blockType == 0x0)
					break;
				if (blockType > 9) // terminate, bad VOC
					break;
				
				int blockSize = (int)stream.ReadLittleUInt32(3);

				long endOfBlock = stream.BaseStream.Position + blockSize;

				switch (blockType) {
					case 0x1: {
						if (blockSize < 2) {
							throw new InvalidDataException("VOC block type 0x1 has length < 2");
						}

						int freq = stream.ReadByte();

						if (freq == 256) {
							throw new InvalidDataException("VOC invalid frequency divisor");
						}

						int encoding = stream.ReadLittleInt32(1);

						Block block = new Block();
						block.rate.channels = 1;
						block.rate.bits = 8;
						block.rate.rate = VOCSampleRate(freq);
						block.loop = (int)loop;

						if (encoding != 0) { // 8 bit PCM only
							throw new InvalidDataException("VOC block type 0x1 decoder only supports 8 bit pcm");
						}

						if (blockSize > 2) {
							block.pcm = stream.Read(blockSize-2);
						}

						if (_8block.rate != 0) {
							// override freq info
							block.rate = _8block;
							_8block.rate = 0;
						}

						if (rate.rate == 0) {
							rate = block.rate;
						} else if (rate != block.rate) {
							throw new InvalidDataException("VOC blocks contain different rates");
						}

						blocks.Add(block);

					} break;

					case 0x9: { // new data block

						if (blockSize < 12) {
							throw new InvalidDataException("VOC block type 0x9 has length < 12");
						}

						Block block = new Block();
						block.rate.rate = (int)stream.ReadLittleUInt32();
						block.rate.bits = (int)stream.ReadByte();
						block.rate.channels = (int)stream.ReadByte();
						block.loop = (int)loop;

						int encoding = stream.ReadByte();
													
						if ((encoding != 0) && (encoding != 4)) { // 8/16 bit PCM only
							throw new InvalidDataException("VOC block type 0x1 decoder only supports 8 or 16 bit pcm");
						}

						if (blockSize > 2) {
							block.pcm = stream.Read(blockSize-2);
						}

						if (_8block.rate != 0) {
							// override freq info
							block.rate = _8block;
							_8block.rate = 0;
						}

						if (rate.rate == 0) {
							rate = block.rate;
						} else if (rate != block.rate) {
							throw new InvalidDataException("VOC blocks contain different rates");
						}

						blocks.Add(block);

					} break;

					case 0x3: { // silence

						if (blockSize != 3) {
							throw new InvalidDataException("VOC invalid block type 0x3");
						}

						if (rate.rate == 0) {
							throw new InvalidDataException("VOC silence block before rate.");
						}

						Block block = new Block();
						block.silence = (int)stream.ReadLittleUInt32(2) + 1;
						block.rate = rate;

						blocks.Add(block);

					} break;

					case 0x6: { // repeat

						if (blockSize != 2) {
							throw new InvalidDataException("VOC invalid block type 0x6");
						}

						loop = stream.ReadLittleUInt32(2);
						if (loop < 65535) { 
							// not a loop forever
							++loop;
						}

					} break;

					case 0x7: { // repeat-end
						loop = 1;
					} break;

					case 0x8: { // extra info

						if (blockSize != 4) {
							throw new InvalidDataException("VOC invalid block type 0x8");
						}

						int freq = (int)stream.ReadLittleUInt32(2);
						if (freq == 65536) {
							throw new InvalidDataException("VOC invalid frequency divisor");
						}

						int encoding = stream.ReadByte();
						if (encoding != 0) {
							throw new InvalidDataException("VOC block type 0x8 only supports 8 bit pcm");
						}

						_8block.channels = stream.ReadByte() + 1;
						_8block.bits = 8;
						_8block.rate = (256000000 / (65536 - freq));

					} break;

					default: {
						Debug.Log("VOC load: skipped unknown block-id " + blockType);
					} break;
				}

				if (endOfBlock >= stream.BaseStream.Length)
					break; // EOS
				stream.SeekSet(endOfBlock);
			}

			AssembleAudio(blocks);
		}
	}

	private void AssembleAudio(List<Block> blocks) {

		if (blocks.Count < 1) {
			throw new InvalidDataException("Zero block in VOC file.");
		}

		bool streamingAudio = false;

		if ((blocks.Count > 1) || (blocks[0].loop != 65535)) {
			foreach(Block block in blocks) {
				if (block.loop > 4) {
					// more than 4 needs to stream
					streamingAudio = true;
					break;
				}
			}
		}

		_loop = false;
		_audioSourceShouldLoop = false;

		if (streamingAudio) {
			AssembleStreamingAudio(blocks);
		} else {
			AssembleAudioClip(blocks);
		}
	}

	private void AssembleStreamingAudio(List<Block> blocks) {
		Rate rate = blocks[0].rate;

		_blockSamples = 0;

		foreach (Block block in blocks) {
			_loop = _loop || (block.loop == 65535);

			int numSamples = block.pcm.Length / ((rate.bits/8) * rate.channels);

			if (rate.bits == 8) {
				block.fpcm = WriteAudio8(rate, numSamples, block.pcm);
			} else {
				block.fpcm = WriteAudio16(rate, numSamples, block.pcm);
			}

			_blockSamples += numSamples;
		}

		_blocks = blocks;

		if (_loop) {
			// looping forever sound, tell unity it's a really long sound (24 hours)
			_blockSamples = rate.rate*60*60*24;
		}
	}

	private void AssembleAudioClip(List<Block> blocks) {
		Rate rate = blocks[0].rate;

		if (blocks[0].loop == 65535) {
			_audioSourceShouldLoop = blocks.Count == 1; // one block with infinite looping blocks, have unity just loop the sound.
		}

		using (MemoryStream pcm = new MemoryStream()) {
			foreach (Block block in blocks) {
				WriteBlock(block, pcm);
			}
			AudioClip audioClip = CreateAudioClip(rate, pcm.ToArray());
			_clipInstance = new AudioClipSoundInstance(audioClip, Internal);
		}
	}

	private AudioClip CreateAudioClip(Rate rate, byte[] pcm) {
		int bytesPerSample = rate.bits / 8;
		int numSamples = pcm.Length / (bytesPerSample * rate.channels);

		AudioClip clip = AudioClip.Create(Name, numSamples, rate.channels, rate.rate, false);

		float[] samples;
		if (rate.bits == 8) {
			samples = WriteAudio8(rate, numSamples, pcm);
		} else {
			samples = WriteAudio16(rate, numSamples, pcm);
		}

		clip.SetData(samples, 0);
		return clip;
	}

	private float[] WriteAudio8(Rate rate, int numSamples, byte[] pcm) {
		using (MemoryStream memStream = new MemoryStream(pcm, false))
		using (ByteStream stream = new ByteStream(memStream)) {
			float[] floats = new float[numSamples*rate.channels];
			for (int i = 0; i < numSamples*rate.channels; ++i) {
				float b = (float)stream.ReadByte(); // read sample
				float n = ((b / 255.0f) - 0.5f) * 2.0f;
				floats[i] = n;
			}
			return floats;
		}
	}

	private float[] WriteAudio16(Rate rate, int numSamples, byte[] pcm) {
		using (MemoryStream memStream = new MemoryStream(pcm, false))
		using (ByteStream stream = new ByteStream(memStream)) {
			float[] floats = new float[numSamples*rate.channels];
			for (int i = 0; i < numSamples*rate.channels; ++i) {
				float b = (float)stream.ReadLittleInt32(2); // read sample
				float n = (((b + 32768.0f) / 65535.0f) - 0.5f) * 2.0f;
				floats[i] = n;
			}
			return floats;
		}
	}

	private void WriteBlock(Block block, Stream pcm) {
		for (int i = 0; i < block.loop; ++i) {
			pcm.Write(block.pcm, 0, block.pcm.Length);
		}
	}

	private void WriteSilence(Rate rate, int numSamples, Stream pcm) {
		int bytesPerSample = rate.bits / 8;
		int bytesToWrite = bytesPerSample * numSamples * rate.channels;

		for (int i = 0; i < bytesToWrite; ++i) {
			pcm.WriteByte(0);
		}
	}

	private static int VOCSampleRate(int rate) {
		if ((rate == 0xa5) || (rate == 0xa6))
			return 11025;
		if ((rate == 0xd2) || (rate == 0xd3))
			return 22050;
		return 1000000 / (256 - rate);
	}

	public struct Rate : System.IEquatable<Rate> {
		public int channels;
		public int rate;
		public int bits;

		public override bool Equals(object obj) {
			if (!(obj is Rate))
				return false;

			Rate other = (Rate)obj;

			return Equals(other);
		}

		public override int GetHashCode() {
			return base.GetHashCode();
		}

		public bool Equals(Rate other) {
			return (channels == other.channels) &&
				(rate == other.rate) &&
				(bits == other.bits);
		}

		public static bool operator == (Rate a, Rate b) {
			return a.Equals(b);
		}

		public static bool operator != (Rate a, Rate b) {
			return !a.Equals(b);
		}
	}

	public class Block {
		public byte[] pcm;
		public float[] fpcm;
		public Rate rate;
		public int loop;
		public int silence;
	}

	private struct VOCHeader {
		public uint major;
		public uint minor;
		
		public static VOCHeader Read(ByteStream stream) {
			long position = stream.BaseStream.Position;

			string ident = System.Text.Encoding.ASCII.GetString(stream.Read(19));
			if (ident != "Creative Voice File") {
				throw new InvalidDataException();
			}

			stream.BaseStream.Position += 1;
			int headerSize = stream.ReadLittleInt32(2);

			uint version = stream.ReadLittleUInt32(2);
			ushort magic = (ushort)stream.ReadLittleUInt32(2);

			if (magic != (~version + 0x1234u)) { // bogus header.
				throw new InvalidDataException();
			}

			VOCHeader header;
			header.major = (version>>8)&0xffu;
			header.minor = version&0xffu;

			stream.BaseStream.Position = position + headerSize;

			return header;
		}
	};

	private AudioClip _audioClip;
	private List<Block> _blocks;
	private int _blockSamples;
	private bool _audioSourceShouldLoop;
	private bool _loop;
	private AudioClipSoundInstance _clipInstance;
}

public sealed class VOCStreamingSoundInstance : SoundInstance {

	public VOCStreamingSoundInstance(List<VOC.Block> blocks, int totalSamples, ISoundAssetInternal sound) : base(sound) {
		_position = 0;
		_startOfBlock = 0;
		_blockIdx = 0;
		_blockLoop = 0;
		_blocks = blocks;
		_rate = blocks[0].rate;
		_clip = CreateStreamingAudio(totalSamples);
		SeekAudio();
	}

	public override AudioClip AudioClip {
		get {
			return _clip;
		}
	}

	protected override void Dispose(bool bIsDisposing) {
		base.Dispose(bIsDisposing);

		if (bIsDisposing) {
			if (_clip) {
				Object.Destroy(_clip);
			}
		}
	}

	private AudioClip CreateStreamingAudio(int totalSamples) {
		return AudioClip.Create(
			Sound.Name, 
			totalSamples, 
			_rate.channels, 
			_rate.rate,
			true, 
			OnAudioRead, 
			OnAudioSetPosition
		);
	}

	private void OnAudioRead(float[] data) {
		int samplesToRead = data.Length / _rate.channels;
		int sampleOffset = 0;

		while (samplesToRead > 0) {
			for (;_blockIdx < _blocks.Count; ++_blockIdx) {
				VOC.Block block = _blocks[_blockIdx];

				int numBlockSamples = block.fpcm.Length / _rate.channels;

				if (block.loop == 65535) {
					// continuously fill buffer until output is full or we are triggered
					while (samplesToRead > 0) {
						if (_position >= _startOfBlock) {
							int blockOffset = _position - _startOfBlock;
							if (blockOffset < numBlockSamples) {
								int samplesToCopy = System.Math.Min(samplesToRead, (numBlockSamples-blockOffset));
								System.Array.Copy(block.fpcm, blockOffset, data, sampleOffset, samplesToCopy);
								samplesToRead -= samplesToCopy;
								sampleOffset += samplesToCopy;
								_position += samplesToCopy;

								if (samplesToRead < 1) {
									// didn't eat buffer?
									if ((blockOffset+samplesToCopy) < numBlockSamples) {
										return;
									}
								}
							}
						}

						_startOfBlock += numBlockSamples;
					}

					return;
				} else {
					for (;_blockLoop < block.loop; ++_blockLoop) {

						if (_position >= _startOfBlock) {
							int blockOffset = _position - _startOfBlock;
							if (blockOffset < numBlockSamples) {
								int samplesToCopy = System.Math.Min(samplesToRead, (numBlockSamples-blockOffset));
								System.Array.Copy(block.fpcm, blockOffset, data, sampleOffset, samplesToCopy);
								samplesToRead -= samplesToCopy;
								sampleOffset += samplesToCopy;
								_position += samplesToCopy;

								if (samplesToRead < 1) {
									// no more samples needed.
									if ((blockOffset+samplesToCopy) == numBlockSamples) {
										// ate buffer.
										++_blockIdx;
										_blockLoop = 0;
										_startOfBlock += numBlockSamples;
									}

									return;
								}
							}
						}
											
						_startOfBlock += numBlockSamples;
					}
				}
			}

			_blockIdx = 0;
			_blockLoop = 0;
			_position = 0;
			_startOfBlock = 0;
		}
	}

	private void OnAudioSetPosition(int position) {
		if (_position != position) {
			_position = position;
			SeekAudio();
		}
	}

	private void SeekAudio() {
		// find the block that matches this position.
		_startOfBlock = 0;

		for (_blockIdx = 0; _blockIdx < _blocks.Count; ++_blockIdx) {
			VOC.Block block = _blocks[_blockIdx];

			if (block.loop == 65535) {
				_blockLoop = 0;
				break; // endless looping block
			} else {
				int blockSamples = block.fpcm.Length / block.rate.channels;
				int numSamples = 0;
				for (_blockLoop = 0; _blockLoop < block.loop; ++_blockLoop) {

					numSamples += blockSamples;

					if (_startOfBlock+numSamples > _position) {
						return;
					}

					_startOfBlock += blockSamples;
				}
			}
		}
	}

	private AudioClip _clip;
	private VOC.Rate _rate;
	private List<VOC.Block> _blocks;
	private int _position;
	private int _startOfBlock;
	private int _blockIdx;
	private int _blockLoop;
}
