/* Files.cs
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
using System.Collections;
using System.Collections.Generic;

public sealed class Files : System.IDisposable {

	public Files() {
		_assetPath = Application.dataPath;
		_basePath = _assetPath + "/..";
		_darkPath = _basePath + "/DARK/";
		Debug.Log ("Starting files at '" + _basePath + "'...");
	}

	public bool Initialize() {
		if (OpenDarkGOB("DARK.GOB") == null)
			return false;
		if (OpenDarkGOB("SOUNDS.GOB") == null)
			return false;
		if (OpenDarkGOB("SPRITES.GOB") == null)
			return false;
		if (OpenDarkGOB("TEXTURES.GOB") == null)
			return false;
		return true;
	}

	public string AssetPath {
		get { return _assetPath; }
	}

	public string BasePath {
		get { return _basePath; }
	}

	public string DarkPath {
		get { return _darkPath; }
	}

	public byte[] Load(string name) {
		for (int i = _gobs.Count - 1; i >= 0; --i) {
			GOBFile gob = _gobs[i];
			GOBFile.File file = gob.Find(name);
			if (file != null) {
				return file.Load();
			}
		}

		return null;
	}

	private GOBFile OpenDarkGOB(string name) {
		return OpenGOB(DarkPath + name);
	}

	private GOBFile OpenUserGOB(string name) {
		return OpenGOB(BasePath + name);
	}

	private GOBFile OpenGOB(string name) {
		GOBFile gob = GOBFile.Open(name);
		if (gob != null) {
			Debug.Log("Opened " + name);
			_gobs.Add(gob);
		} else {
			Debug.LogError("Failed to open " + name);
		}
		return gob;
	}

	public void Dispose() {
		foreach (var gob in _gobs) {
			gob.Dispose();
		}
		_gobs = null;
		System.GC.SuppressFinalize(this);
	}

	public List<GOBFile> GOBs {
		get { return _gobs; }
	}

	private string _assetPath;
	private string _basePath;
	private string _darkPath;
	private List<GOBFile> _gobs = new List<GOBFile>();
}
