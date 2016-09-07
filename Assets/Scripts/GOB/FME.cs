/* FME.cs
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

public struct FMEHeader {
	public Vector2 Shift;
	public bool Flipped;
}

/*! Dark forces sprite frame */
public class FME : Asset {

	public FME(string name, byte[] data, object createArgs) : base(name, Type.FME) {
		BM.CreateArgs args = createArgs as BM.CreateArgs;
		if (args == null) {
			throw new System.ArgumentException("FME requires BM.CreateArgs.");
		}

		using (MemoryStream backing = new MemoryStream(data, false))
		using (ByteStream stream = new ByteStream(backing)) {
			_frame = ReadHeader1(stream, args, out _header);
		}
	}

	public static BM.Frame ReadHeader1(ByteStream stream, BM.CreateArgs createArgs, out FMEHeader header) {
		header = new FMEHeader();
		header.Shift = new Vector2();
		header.Shift.x = (float)stream.ReadLittleInt32();
		header.Shift.y = (float)stream.ReadLittleInt32();
		header.Flipped = stream.ReadLittleInt32() != 0;

		int header2Ofs = stream.ReadLittleInt32();
		stream.Skip(16 + (header2Ofs-32));

		return ReadHeader2(stream, createArgs);
	}
	
	public static BM.Frame ReadHeader2(ByteStream stream, BM.CreateArgs createArgs) {
		int headerStart = (int)stream.Position;

		BM.Header header = new BM.Header();
		header.w = stream.ReadLittleInt32();
		header.h = stream.ReadLittleInt32();
		header.compressed = stream.ReadLittleInt32();
		header.dataSize = stream.ReadLittleInt32();
		header.transparent = 0x8;

		stream.Skip(8);

		int[] columnOffsets = null;

		if (header.compressed != 0) {
			header.compressed = 2;

			columnOffsets = new int[header.w];

			for (int i = 0; i < columnOffsets.Length; ++i) {
				columnOffsets[i] = stream.ReadLittleInt32() + headerStart;
			}
		}

		return BM.ReadColumns(stream, header, columnOffsets, createArgs);
	}

	protected override void OnDispose() {
		base.OnDispose();
		_frame.Dispose();
		_frame = null;
	}

	public FMEHeader header { get { return _header; } }

	public BM.Frame frame { get { return _frame; } }

	private FMEHeader _header;
	private BM.Frame _frame;
}
