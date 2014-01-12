/* Asset.cs
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

public abstract class Asset {

	public enum Type {
		BLOB,
		VOC
	}

	public enum CacheMode {
		None,
		Globals
	}

	public Asset(string name, Type type) {
		_name = name;
		_type = type;
		_refCount = 1;
	}

	public int Reference() {
		return ++_refCount;
	}

	public int Release() {
		if (--_refCount == 0) {
			Dispose();
		}
		return _refCount;
	}

	public int Count { get { return _refCount; } }
	public string Name { get { return _name; } }
	public Type TypeOf { get { return _type; } } 
	public bool Cached { get { return _cached; } }

	public static void StaticInit(Game game) {
		s_game = game;
	}

	public static Type TypeForName(string name) {
		int period = name.LastIndexOf('.');
		if (period == -1)
			return Type.BLOB;
		int numChars = name.Length - period - 1;
		
		string ext = name.Substring(period+1, numChars).ToUpper();
		
		if (ext == "VOC")
			return Type.VOC;
		
		return Type.BLOB;
	}
	
	public static Asset Load(string name, CacheMode mode) {
		Asset asset = null;
		if (s_assets.TryGetValue(name, out asset))
			return asset;
		byte[] data = s_game.Files.Load(name);
		if (data != null) {
			asset = New(name, data, TypeForName(name));
		}

		if (asset != null) {
			asset._cached = (mode == CacheMode.None) ? false : true;
		}

		return asset;
	}

	public static Asset Load(GOBFile.File file) {
		byte[] data = file.Load();

		if (data != null) {
			return New(file.Name, data, TypeForName(file.Name));
		}

		return null;
	}

	public static Asset New(string name, byte[] data, Type type) {
		Asset asset = null;
		switch (type) {
		default:
			asset = new BLOB(name, data);
			break;
			
		}
		return asset;
	}

	private void Dispose() {
		if (_cached)
			Remove(this);
	}

	private static void Add(Asset asset) {
		s_assets.Add(asset.Name, asset);
	}

	private static void Remove(Asset asset) {
		s_assets.Remove(asset.Name);
	}

	private Type _type;
	private int _refCount;
	private string _name;
	public bool _cached;
	private static Game s_game;
	private static Dictionary<string, Asset> s_assets = new Dictionary<string, Asset>();
}
