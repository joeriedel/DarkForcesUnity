/* SoundAssets.cs
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

public interface ISoundAssetInternal {
	void AddInstance(SoundInstance instance);
	void RemoveInstance(SoundInstance instance);
	SoundAsset Sound { get; }
}

public enum SoundType {
	Positional,
	Stereo
}

public abstract class SoundAsset : Asset {

	public SoundAsset(string name, SoundType soundType, Type assetType) : base(name, assetType) { 
		_soundType = soundType;
	}

	public abstract SoundInstance CreateInstance();
	public abstract bool AudioSourceShouldLoop { get; }
	public abstract bool IsLooping { get; }
	public SoundType SoundType { get { return _soundType; } }

	protected ISoundAssetInternal Internal { 
		get {
			if (_internal == null) {
				_internal = new SoundRefInternal(this);
			}
			return _internal; 
		} 
	}

	private class SoundRefInternal : ISoundAssetInternal {

		public SoundRefInternal(SoundAsset sound) {
			_sound = sound;
		}

		public void AddInstance(SoundInstance instance) {
			if (_instances == null) {
				_instances = new List<SoundInstance>();
			}
			_instances.Add(instance);
			_sound.Reference();
		}

		public void RemoveInstance(SoundInstance instance) {
			if (_instances != null) {
				if (_instances.Remove(instance)) {
					_sound.Dispose();
				}
			}
		}

		public SoundAsset Sound { get { return _sound; } }

		private List<SoundInstance> _instances;
		private SoundAsset _sound;
	}

	SoundRefInternal _internal;
	SoundType _soundType;
}

public abstract class SoundInstance : System.IDisposable {

	public SoundInstance(ISoundAssetInternal sound) {
		_internal = sound;
		_refCount = 1;
		_internal.AddInstance(this);
	}

	public abstract AudioClip AudioClip { get; }

	public void Dispose() {
		if (--_refCount == 0) {
			Dispose(true);
			System.GC.SuppressFinalize(this);
		}
	}

	protected virtual void Dispose(bool bIsDisposing) {
		if (bIsDisposing) {
			_internal.RemoveInstance(this);
		}
	}

	public virtual void AttachToAudioSource(AudioSource source) {
		source.loop = Sound.AudioSourceShouldLoop;
		source.clip = AudioClip;
	}

	public SoundAsset Sound { get { return _internal.Sound; } }
	public int RefCount { get { return _refCount; } }

	public void Reference() {
		++_refCount;
	}

	private ISoundAssetInternal _internal;
	private int _refCount;
}

public sealed class AudioClipSoundInstance : SoundInstance {

	public AudioClipSoundInstance(AudioClip clip, ISoundAssetInternal sound) : base(sound) {
		_clip = clip;
	}

	public override AudioClip AudioClip {
		get {
			return _clip;
		}
	}

	private AudioClip _clip;
}