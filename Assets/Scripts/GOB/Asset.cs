/* Asset.cs
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
using System.Collections.Generic;

public abstract class Asset : System.IDisposable {

	Type _type;
	int _refCount;
	string _name;
	bool _bIsCached;
	static Dictionary<string, Asset> cachedAssets = new Dictionary<string, Asset>();

	public enum Type {
		BLOB,
		BM,
		CMP,
		GMD,
		FME,
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
		RuntimeCheck.Assert(_refCount >= 0, "RefCount error! (" + name + ")");

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

	public void AddRef() {
		++_refCount;
	}
	
	public string name { get { return _name; } }
	public Type typeOf { get { return _type; } } 
	public bool cached { get { return _bIsCached; } }
	public int refCount { get { return _refCount; } }

	public static Type TypeForName(string name) {
		int period = name.LastIndexOf('.');
		if (period == -1)
			return Type.BLOB;
		int numChars = name.Length - period - 1;
		
		string ext = name.Substring(period+1, numChars).ToUpper();

		if (ext == "BM") {
			return Type.BM;
		}  else if (ext == "CMP") {
			return Type.CMP;
		} else if (ext == "GMD") {
			return Type.GMD;
		} else if (ext == "LEV") {
			return Type.LEV;
		} else if (ext == "FME") {
			return Type.FME;
		} else if (ext == "PAL") {
			return Type.PAL;
		} else if (ext == "VOC") {
			return Type.VOC;
		}

		return Type.BLOB;
	}

	public static T LoadCached<T>(string name) where T : Asset {
		return Load<T>(name, CacheMode.Cached, null);
	}

	public static T LoadCached<T>(string name, object createArgs) where T : Asset {
		return Load<T>(name, CacheMode.Cached, createArgs);
	}

	public static T LoadUncached<T>(string name) where T : Asset {
		return Load<T>(name, CacheMode.Uncached, null);
	}

	public static T LoadUncached<T>(string name, object createArgs) where T : Asset {
		return Load<T>(name, CacheMode.Uncached, createArgs);
	}

	public static T Load<T>(string name, CacheMode mode, object createArgs) where T : Asset {
		
		Asset asset = null;
		if ((mode == CacheMode.Cached) && cachedAssets.TryGetValue(name, out asset)) {
			asset.AddRef();
		} else {
			byte[] data = Game.instance.files.Load(name);
			if (data != null) {
				asset = New(name, data, TypeForName(name), createArgs);
			}

			if (asset != null) {
				asset._bIsCached = (mode == CacheMode.Cached);
				if (asset._bIsCached) {
					cachedAssets[name] = asset;
				}
			} else {
				throw new System.IO.FileNotFoundException("'" + name + "' was not found in any open GOB file.");
			}
		}

		T t = asset as T;
		if (t == null) {
			throw new System.InvalidCastException("'" + name + "' is not a " + typeof(T).FullName);
		}

		return t;
	}

	public static Asset Load(GOBFile.File file, object createArgs) {
		byte[] data = file.Load();

		if (data != null) {
			return New(file.name, data, TypeForName(file.name), createArgs);
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
				case Type.CMP:
					asset = new CMP(name, data, createArgs);
					break;
				case Type.GMD:
					asset = new GMD(name, data, createArgs);
					break;
				case Type.LEV:
					asset = new LEV(name, data, createArgs);
					break;
				case Type.PAL:
					asset = new PAL(name, data, createArgs);
					break;
				case Type.FME:
					asset = new FME(name, data, createArgs);
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
		Dictionary<string, Asset> assets = cachedAssets;
		cachedAssets = null;

		if (assets != null) {
			foreach (Asset asset in assets.Values) {
				asset.Dispose();
			}
		}

		cachedAssets = new Dictionary<string, Asset>();
	}

	public static void PurgeCache() {
		List<string> purgeList = new List<string>();

		foreach (Asset asset in cachedAssets.Values) {
			if (asset._bIsCached && (asset.refCount == 0)) {
				asset._bIsCached = false;
				purgeList.Add(asset.name);
				asset.Dispose(true);
			}
		}

		foreach (string name in purgeList) {
			cachedAssets.Remove(name);
		}
	}

	private static void Add(Asset asset) {
		cachedAssets.Add(asset.name, asset);
	}

	private static void Remove(Asset asset) {
		if (cachedAssets != null) {
			cachedAssets.Remove(asset.name);
		}
	}
}
