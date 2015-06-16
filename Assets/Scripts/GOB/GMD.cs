/* GMD.cs
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

/*! Dark Forces Music. */
public sealed class GMD : Asset {

	public GMD(string name, byte[] data, object createArgs) : base(name, Type.GMD) {
		using (MemoryStream backing = new MemoryStream(data, false))
		using (ByteStream stream = new ByteStream(backing)) {
			ExtractMIDI(stream);
		}
	}

	public byte[] MidiData { get { return _midiData; } }

	void ExtractMIDI(ByteStream stream) {
		if (stream.ReadString(4) != "MIDI") {
			throw new InvalidDataException("Not a GMD file!");
		}
		stream.Skip(4);
		
		string type;
		while ((type = GetChunkType(stream)) != null) {
			if (type == "MThd") {
				long size = stream.Length - stream.Position;
				_midiData = stream.Read((int)size);
				return;
			}

			if (!SkipChunk(stream)) {
				break;
			}
		}

		throw new InvalidDataException("Could not locate midi track header!");
	}

	bool SkipChunk(ByteStream stream) {
		stream.Skip(4);
		int length = stream.ReadBigInt32();
		stream.Skip(length);
		return !stream.EOS;
	}

	string GetChunkType(ByteStream stream) {
		long pos = stream.Position;
		string type = stream.ReadString(4);
		stream.SeekSet(pos);
		return type;
	}

	byte[] _midiData;
}
