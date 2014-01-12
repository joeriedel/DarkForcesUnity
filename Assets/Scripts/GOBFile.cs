/* GOBFile.cs
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
using System.Collections;
using System.Collections.Generic;
using System.IO;

public sealed class GOBFile : System.IDisposable {

	private GOBFile() {
	}

	public static GOBFile Open(string path) {
		Debug.Log("GOBFile.Open(" + path + ")");
		FileStream fs = null;
		try { fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); } catch {}
		if (fs == null) {
			Debug.LogError("OpenRead(\"" + path + "\") failed.");
			return null;
		}

		BinaryReader br = new BinaryReader(fs);

		try {
			byte[] id = new byte[4];
			if (br.Read(id, 0, 4) != 4) {
				Debug.LogError("Failed to read GOB header in file " + path + ".");
				fs.Close();
				return null;
			}

			if ((id[0] != 71) || (id[1] != 79) || (id[2] != 66) || (id[3] != 0xA)) {
				fs.Close();
				Debug.LogError("\"" + path + "\" is not a GOB file.");
				return null;
			}

			int dirOfs = br.ReadInt32();

			br.BaseStream.Position = (long)dirOfs;

			int numFiles = br.ReadInt32();

			GOBFile gob = new GOBFile();
			byte[] name = new byte[13];
			for (int i = 0; i < numFiles; ++i) {
				File file = File.Read(br, name);
				gob._dir.Add(file.Name, file);
			}

			gob._file = br;
			return gob;
		} catch  {
			using (br) {}
		}

		return null;
	}

	public File Find(string name) {
		File file;
		_dir.TryGetValue(name, out file);
		return file;
	}

	public void Dispose() {
		if (_file != null) {
			using (_file) {}
			_file = null;
		}

		_dir = null;
	}

	public class File {

		private File(BinaryReader br, int ofs, int len, string name) {
			_ofs = ofs;
			_len = len;
			_name = name;
			_br = br;
		}

		public static File Read(BinaryReader br, byte[] nameBuf) {
			int ofs = br.ReadInt32();
			int len = br.ReadInt32();
			br.Read(nameBuf, 0, 13);
			string name = System.Text.Encoding.ASCII.GetString(nameBuf);
			return new File(br, ofs, len, name);
		}

		public byte[] Load() {
			byte[] data = new byte[_len];
			_br.Read(data, 0, _len);
			return data;
		}

		public int Ofs { get { return _ofs; } }
		public int Len { get { return _len; } }
		public string Name { get { return _name; } }

		private int _ofs;
		private int _len;
		private string _name;
		private BinaryReader _br;
	}

	private BinaryReader _file;
	private Dictionary<string, File> _dir = new Dictionary<string, File>();
}
