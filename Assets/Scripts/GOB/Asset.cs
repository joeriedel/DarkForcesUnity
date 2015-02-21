﻿/* Asset.cs
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
using System.Collections.Generic;

public abstract class Asset : System.IDisposable {

	public enum Type {
		BLOB,
		BM,
		LEV,
		PAL,
		VOC
	}

	public enum CacheMode {
		Uncached,
		Cached
	}

	public Asset(string name, Type type) {
		_name = name;
		_type = type;
		_refCount = 1;
	}

	public void Dispose() {
		RuntimeCheck.Assert(_refCount > 0, "RefCount error!");

		if (--_refCount == 0) {
			if (!_bIsCached) {
				Dispose(true);
				System.GC.SuppressFinalize(this);
			}
		}
	}

	protected virtual void Dispose(bool bIsDisposing) {
		if (bIsDisposing) {
			if (_bIsCached) {
				_bIsCached = false;
				Remove(this);
			} else {
				OnDispose();
			}
		}
	}

	protected virtual void OnDispose() { }

	public void Reference() {
		++_refCount;
	}
	
	public string Name { get { return _name; } }
	public Type TypeOf { get { return _type; } } 
	public bool Cached { get { return _bIsCached; } }
	public int RefCount { get { return _refCount; } }

	public static void StaticInit(Game game) {
		s_game = game;
	}

	public static Type TypeForName(string name) {
		int period = name.LastIndexOf('.');
		if (period == -1)
			return Type.BLOB;
		int numChars = name.Length - period - 1;
		
		string ext = name.Substring(period+1, numChars).ToUpper();

		if (ext == "BM") {
			return Type.BM;
		} else if (ext == "LEV") {
			return Type.LEV;
		} else if (ext == "PAL") {
			return Type.PAL;
		} else if (ext == "VOC") {
			return Type.VOC;
		}

		return Type.BLOB;
	}

	public static Asset LoadCached(string name) {
		return Load(name, CacheMode.Cached, null);
	}

	public static Asset LoadCached(string name, object createArgs) {
		return Load(name, CacheMode.Cached, createArgs);
	}

	public static Asset LoadUncached(string name) {
		return Load(name, CacheMode.Uncached, null);
	}

	public static Asset LoadUncached(string name, object createArgs) {
		return Load(name, CacheMode.Uncached, createArgs);
	}

	public static Asset Load(string name, CacheMode mode, object createArgs) {
		Asset asset = null;
		if ((mode == CacheMode.Cached) && s_assets.TryGetValue(name, out asset)) {
			asset.Reference();
			return asset;
		}
		byte[] data = s_game.Files.Load(name);
		if (data != null) {
			asset = New(name, data, TypeForName(name), createArgs);
		}

		if (asset != null) {
			asset._bIsCached = (mode == CacheMode.Cached);
			if (asset._bIsCached) {
				s_assets[name] = asset;
			}
		}

		return asset;
	}

	public static Asset Load(GOBFile.File file, object createArgs) {
		byte[] data = file.Load();

		if (data != null) {
			return New(file.Name, data, TypeForName(file.Name), createArgs);
		}

		return null;
	}

	public static Asset New(string name, byte[] data, Type type, object createArgs) {
		Asset asset = null;
		//try {
			switch (type) {
				case Type.BM:
					asset = new BM(name, data, createArgs);
					break;
				case Type.LEV:
					asset = new LEV(name, data, createArgs);
					break;
				case Type.PAL:
					asset = new PAL(name, data, createArgs);
					break;
				case Type.VOC:
					asset = new VOC(name, data, createArgs);
					break;
				default:
					asset = new BLOB(name, data);
					break;
			}
		//} catch (System.Exception e) {
		//	Debug.LogError("ERROR LOADING '" + name + "': '" + e.Message + "' @ " + e.StackTrace);
		//	asset = null;
		//}
		return asset;
	}

	public static void ClearCache() {
		Dictionary<string, Asset> assets = s_assets;
		s_assets = null;

		if (assets != null) {
			foreach (Asset asset in assets.Values) {
				asset.Dispose();
			}
		}

		s_assets = new Dictionary<string, Asset>();
	}

	public static void PurgeCache() {
		List<string> purgeList = new List<string>();

		foreach (Asset asset in s_assets.Values) {
			if (asset._bIsCached && (asset.RefCount == 0)) {
				purgeList.Add(asset.Name);
				asset.Dispose(true);
			}
		}

		foreach (string name in purgeList) {
			s_assets.Remove(name);
		}
	}

	private static void Add(Asset asset) {
		s_assets.Add(asset.Name, asset);
	}

	private static void Remove(Asset asset) {
		if (s_assets != null) {
			s_assets.Remove(asset.Name);
		}
	}

	private Type _type;
	private int _refCount;
	private string _name;
	private bool _bIsCached;
	private static Game s_game;
	private static Dictionary<string, Asset> s_assets = new Dictionary<string, Asset>();
}
