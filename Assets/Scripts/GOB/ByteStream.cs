/* ByteStream.cs
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

using System;
using System.IO;
using System.Text;

/*! ByteStream is a replacement for BinaryReader, enabling reading
 * of streams using variable length integers and endianness */
public class ByteStream : IDisposable {

	public ByteStream(Stream baseStream) {
		_stream = baseStream;
		if (baseStream == null)
			throw new ArgumentException("null argument", "baseStream");
	}

	public void Skip(int numBytes) {
		long position = _stream.Position;
		position += numBytes;

		if ((position < 0) || (position > _stream.Length)) {
			throw new InvalidOperationException("Seek outside of stream bounds.");
		}

		_stream.Position = position;
	}

	public void SeekSet(long position) {
		if ((position < 0) || (position >= _stream.Length)) {
			throw new InvalidOperationException("Seek outside of stream bounds.");
		}

		_stream.Position = position;
	}

	public void SeekEnd(long position) {
		position += _stream.Length;
		SeekSet(position);
	}

	public long Position { get { return _stream.Position; } }

	public int ReadByte() {
		 int z = _stream.ReadByte();
		 if (z == -1)
			 throw new EndOfStreamException();
		 return z;
	}

	public byte[] Read(int count) {
		byte[] bytes = new byte[count];
		int z = _stream.Read(bytes, 0, count);
		if (z != count)
			throw new EndOfStreamException();
		return bytes;
	}

	public int Read(byte[] buffer, int offset, int count) {
		return _stream.Read(buffer, offset, count);
	}

	public char ReadChar() {
		return Convert.ToChar(ReadByte());
	}

	public string ReadString(int numChars) {
		return System.Text.Encoding.ASCII.GetString(Read(numChars));
	}

	public int ReadLittleShort16() {
		return ReadLittleInt32(2);
	}

	public int ReadBigShort16() {
		return ReadBigInt32(2);
	}

	public int ReadLittleUShort16() {
		return (int)ReadLittleUInt32(2);
	}

	public int ReadBigUShort16() {
		return (int)ReadBigUInt32(2);
	}

	public int ReadLittleInt32() {
		return ReadLittleInt32(4);
	}

	public int ReadBigInt32() {
		return ReadBigInt32(4);
	}

	public int ReadLittleInt32(int numBytes) {
		return BitConverter.ToInt32(SafeReadLittle(numBytes, 4, true), 0);
	}

	public int ReadBigInt32(int numBytes) {
		return BitConverter.ToInt32(SafeReadBig(numBytes, 4, true), 0);
	}

	public uint ReadLittleUInt32() {
		return ReadLittleUInt32(4);
	}

	public uint ReadBigUInt32() {
		return ReadBigUInt32(4);
	}

	public uint ReadLittleUInt32(int numBytes) {
		return BitConverter.ToUInt32(SafeReadLittle(numBytes, 4, false), 0);
	}

	public uint ReadBigUInt32(int numBytes) {
		return BitConverter.ToUInt32(SafeReadBig(numBytes, 4, false), 0);
	}

	public long ReadLittleInt64() {
		return ReadLittleInt64(8);
	}

	public long ReadLittleInt64(int numBytes) {
		return BitConverter.ToInt64(SafeReadLittle(numBytes, 8, true), 0);
	}

	public long ReadBigInt64() {
		return ReadBigInt64(8);
	}

	public long ReadBigInt64(int numBytes) {
		return BitConverter.ToInt64(SafeReadBig(numBytes, 8, true), 0);
	}

	public ulong ReadLittleUInt64() {
		return ReadLittleUInt64(8);
	}

	public ulong ReadLittleUInt64(int numBytes) {
		return BitConverter.ToUInt64(SafeReadLittle(numBytes, 8, false), 0);
	}

	public ulong ReadBigUInt64() {
		return ReadBigUInt64(8);
	}

	public ulong ReadBigUInt64(int numBytes) {
		return BitConverter.ToUInt64(SafeReadBig(numBytes, 8, false), 0);
	}

	public float ReadLittleSingle() {
		return ReadLittleSingle(4);
	}

	public float ReadBigSingle() {
		return ReadBigSingle(4);
	}

	public float ReadLittleSingle(int numBytes) {
		return BitConverter.ToSingle(SafeReadLittle(numBytes, 4, false), 0);
	}

	public float ReadBigSingle(int numBytes) {
		return BitConverter.ToSingle(SafeReadBig(numBytes, 4, false), 0);
	}

	public double ReadLittleDouble() {
		return ReadLittleDouble(8);
	}

	public double ReadBigDouble() {
		return ReadBigDouble(8);
	}

	public double ReadLittleDouble(int numBytes) {
		return BitConverter.ToDouble(SafeReadLittle(numBytes, 8, false), 0);
	}

	public double ReadBigDouble(int numBytes) {
		return BitConverter.ToDouble(SafeReadBig(numBytes, 8, false), 0);
	}

	public void Write(byte[] bytes) {
		_stream.Write(bytes, 0, bytes.Length);
	}

	public void Write(byte[] buffer, int offset, int count) {
		_stream.Write(buffer, offset, count);
	}

	public void WriteLittle(int x) {
		WriteLittle(x, 4);
	}

	public void WriteLittle(int x, int numBytes) {
		WriteLittle(GetBytes(x, numBytes));
	}

	public void WriteBig(int x) {
		WriteBig(x, 4);
	}

	public void WriteBig(int x, int numBytes) {
		WriteBig(GetBytes(x, numBytes));
	}

	public void WriteLittle(uint x) {
		WriteLittle(x, 4);
	}

	public void WriteLittle(uint x, int numBytes) {
		WriteLittle(GetBytes(x, numBytes));
	}

	public void WriteBig(uint x) {
		WriteBig(x, 4);
	}

	public void WriteBig(uint x, int numBytes) {
		WriteBig(GetBytes(x, numBytes));
	}

	public void WriteLittle(long x) {
		WriteLittle(x, 8);
	}

	public void WriteLittle(long x, int numBytes) {
		WriteLittle(GetBytes(x, numBytes));
	}

	public void WriteBig(long x) {
		WriteBig(x, 8);
	}

	public void WriteBig(long x, int numBytes) {
		WriteBig(GetBytes(x, numBytes));
	}

	public void WriteLittle(ulong x) {
		WriteLittle(x, 8);
	}

	public void WriteLittle(ulong x, int numBytes) {
		WriteLittle(GetBytes(x, numBytes));
	}

	public void WriteBig(ulong x) {
		WriteBig(x, 8);
	}

	public void WriteBig(ulong x, int numBytes) {
		WriteBig(GetBytes(x, numBytes));
	}

	public void WriteLittle(float x) {
		WriteLittle(x, 4);
	}

	public void WriteLittle(float x, int numBytes) {
		WriteLittle(Trim(BitConverter.GetBytes(x), numBytes));
	}

	public void WriteBig(float x) {
		WriteBig(x, 4);
	}

	public void WriteBig(float x, int numBytes) {
		WriteBig(Trim(BitConverter.GetBytes(x), numBytes));
	}

	public void WriteLittle(double x) {
		WriteLittle(x, 8);
	}

	public void WriteLittle(double x, int numBytes) {
		WriteLittle(Trim(BitConverter.GetBytes(x), numBytes));
	}

	public void WriteBig(double x) {
		WriteBig(x, 8);
	}

	public void WriteBig(double x, int numBytes) {
		WriteBig(Trim(BitConverter.GetBytes(x), numBytes));
	}

	public Stream BaseStream { get { return _stream; } }
	public bool EOS { get { return _stream.Position >= _stream.Length; } }

	public void Dispose() {
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	public virtual void Dispose(bool bIsDisposing) {
		if (bIsDisposing) {
			_stream.Dispose();
		}
	}

	private void WriteLittle(byte[] bytes) {
		SwapLittle(bytes);
		_stream.Write(bytes, 0, bytes.Length);
	}

	private void WriteBig(byte[] bytes) {
		SwapBig(bytes);
		_stream.Write(bytes, 0, bytes.Length);
	}

	private void SafeWriteLittle(byte[] bytes) {
		SwapLittle(bytes);
		_stream.Write(bytes, 0, bytes.Length);
	}

	private void SafeWriteBig(byte[] bytes) {
		SwapBig(bytes);
		_stream.Write(bytes, 0, bytes.Length);
	}

	private byte[] SafeReadLittle(int numBytes, int maxLen, bool sign) {
		if ((numBytes < 1) || (numBytes > maxLen))
			throw new ArgumentException("Invalid byte count for data-type");
		byte[] bytes = new byte[maxLen];
		int z = _stream.Read(bytes, 0, numBytes);
		if (z < numBytes)
			throw new EndOfStreamException();
		SwapLittle(bytes);
		if (sign) {
			DecodeSignBit(bytes, numBytes);
		}
		return bytes;
	}

	private byte[] SafeReadBig(int numBytes, int maxLen, bool sign) {
		if ((numBytes < 1) || (numBytes > maxLen))
			throw new ArgumentException("Invalid byte count for data-type");
		byte[] bytes = new byte[maxLen];
		int z = _stream.Read(bytes, maxLen-numBytes, numBytes);
		if (z < numBytes)
			throw new EndOfStreamException();
		SwapBig(bytes);
		if (sign) {
			DecodeSignBit(bytes, numBytes);
		}
		return bytes;
	}

	private void DecodeSignBit(byte[] bytes, int len) {
		int srcSignBit = BitConverter.IsLittleEndian ? (len-1) : (bytes.Length-len);
		int dstSignBit = BitConverter.IsLittleEndian ? (bytes.Length-1) : 0;

		bool sign = (bytes[srcSignBit] & SignBit) != 0;
		if (sign) {
			bytes[srcSignBit] &= SignBit;
			bytes[dstSignBit] |= SignBit;
		}
	}

	private static byte[] Trim(byte[] bytes, int count) {
		if (bytes.Length < count)
			throw new ArgumentException("Invalid size for data-type specified.");
		if (bytes.Length > count) {
			byte[] dst = new byte[count];
			Array.Copy(bytes, dst, count);
			bytes = dst;
		}
		return bytes;
	}

	private static byte[] GetBytes(int x, int numBytes) {
		byte[] bytes = Trim(BitConverter.GetBytes(Math.Abs(x)), numBytes);
		if (x < 0) {
			// set sign in correct bit
			int signBit = BitConverter.IsLittleEndian ? (numBytes-1) : 0;
			bytes[signBit] |= SignBit;
		}
		return bytes;
	}

	private static byte[] GetBytes(uint x, int numBytes) {
		return Trim(BitConverter.GetBytes(x), numBytes);
	}

	private static byte[] GetBytes(long x, int numBytes) {
		byte[] bytes = Trim(BitConverter.GetBytes(Math.Abs(x)), numBytes);
		if (x < 0) {
			// set sign in correct bit
			int signBit = BitConverter.IsLittleEndian ? (numBytes-1) : 0;
			bytes[signBit] |= SignBit;
		}
		return bytes;
	}

	private static byte[] GetBytes(ulong x, int numBytes) {
		return Trim(BitConverter.GetBytes(x), numBytes);
	}

	private static void SwapLittle(byte[] bytes) {
		if (!BitConverter.IsLittleEndian)
			Array.Reverse(bytes);
	}

	private static void SwapBig(byte[] bytes) {
		if (BitConverter.IsLittleEndian)
			Array.Reverse(bytes);
	}

	private Stream _stream;
	private const byte SignBit = 128;
}
