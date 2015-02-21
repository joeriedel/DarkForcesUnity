/* PAL.cs
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

/*! Dark Forces palette file. */
public sealed class PAL : Asset {

	public PAL(string name, byte[] data, object createArgs) : base(name, Type.PAL) {
		if (data.Length != 768) {
			throw new InvalidDataException("Dark forces palette files are 768 bytes long!");
		}

		_colors = new Color32[256];

		int palOfs = 0;
		for (int i = 0; i < _colors.Length; ++i, palOfs += 3) {
			_colors[i] = new Color32(ExpandPalByte(data[palOfs]), ExpandPalByte(data[palOfs+1]), ExpandPalByte(data[palOfs+2]), 255);
		}
	}

	static PAL() {
		_transparentColor = new Color32(0, 255, 255, 0);
	}

	static byte ExpandPalByte(byte c) {
		return (byte)(c/63f*255f+0.5f);
	}

	public static Color32 Transparent { get { return _transparentColor; } }

	public Color32[] Colors { get { return _colors; } }

	private Color32[] _colors;
	private static Color32 _transparentColor;
}
