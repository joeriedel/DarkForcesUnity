using UnityEngine;
using System.Collections.Generic;
using CSharpSynth.Sequencer;
using CSharpSynth.Synthesis;
using CSharpSynth.Midi;

class iMUSEMidiSequencer : MidiSequencer {
	public iMUSEMidiSequencer(iMUSE iMuse, StreamSynthesizer synth) : base(synth) {
		_iMuse = iMuse;
		Looping = false;
	}

	public override bool ProcessSysExEvent(MidiEvent midiEvent) {
		if (midiEvent.midiSysExEvent == MidiHelper.MidiSysExEvent.iMUSE) {
			return _iMuse.HandleSysExEvent(midiEvent);
		}
		return base.ProcessSysExEvent(midiEvent);
	}

	public override bool HandleEndOfTrack(StreamSynthesizer synth) {
		return _iMuse.HandleEndOfTrack();
	}

	private iMUSE _iMuse;
}

[RequireComponent(typeof(AudioSource))]
public class iMUSE : MonoBehaviour {

	private class MusicSequences {
		public int FirstTrack;
		public int NumTracks;
		public List<MidiTrack.Section[]> TrackSections = new List<MidiTrack.Section[]>();
	}

	private iMUSEMidiSequencer _midiSequencer;
	private StreamSynthesizer _midiStreamSynthesizer;
	private MusicSequences _stalkSequences;
	private MusicSequences _fightSequences;
	private float[] _sampleBuffer;
	private int _currentTrack;
	private MidiTrack.Section _currentSection;
	private float _targetPitch;
	private float _currentPitch;
	private float _startPitch;
	private float _pitchSpeed;
	private float _pitchTime;
	private bool _bIsPlayingLevelMusic;
	private bool _bSwitchToFight;
	private float _transitionTimeout = 0f;
	private const float SlowPitchSpeed = 0f;
	private List<string>[] _sysExQueue =  new List<string>[] { new List<string>(), new List<string>() };
	private int _sysExQueueIndex;
	private float[] stalkReturnTimes = new float[4];
	private static int _nextRand;
	private static float[] _randomTable;

	// inspector
	public int CurrentTrack;
	public float CurrentTrackTime;
	public float TotalTrackTime;
	public float Offset;

	void Awake() {
		InitRandomTable();

		int dspBufferCount;
		int dspBufferLength;

		AudioSettings.GetDSPBufferSize(out dspBufferLength, out dspBufferCount);
		_midiStreamSynthesizer = new StreamSynthesizer(44100, 2, dspBufferLength, 40);
		_sampleBuffer = new float[_midiStreamSynthesizer.BufferSize];

		_midiStreamSynthesizer.LoadBank("GM Bank/gm");
		_midiSequencer = new iMUSEMidiSequencer(this, _midiStreamSynthesizer);
	}

	void InitRandomTable() {
		if (_randomTable == null) {
			_randomTable = new float[1024];
			for (int i = 0; i < _randomTable.Length; ++i) {
				_randomTable[i] = Random.value;
			}
		}
	}

	float NextRand() {
		float r = _randomTable[_nextRand++];
		if (_nextRand >= _randomTable.Length) {
			_nextRand = 0;
		}
		return r;
	}

	int RandRange(int min, int max) {
		return Mathf.Min(min + Mathf.FloorToInt((max-min)*NextRand()), max-1);
	}

	float RandRange(float min, float max) {
		return Mathf.Lerp(min, max, NextRand());
	}

	public void PlayLevelMusic(GMD stalk, GMD fight) {
		_bIsPlayingLevelMusic = true;

		MidiFile stalkMidi = new MidiFile(stalk.MidiData);

		_stalkSequences = new MusicSequences();
		_stalkSequences.FirstTrack = 0;
		_stalkSequences.NumTracks = stalkMidi.Tracks.Length;
		
		foreach (var Track in stalkMidi.Tracks) {
			_stalkSequences.TrackSections.Add(Track.Sections);
		}

		if (fight != null) {
			MidiFile fightMidi = new MidiFile(fight.MidiData);

			_fightSequences = new MusicSequences();
			_fightSequences.FirstTrack = _stalkSequences.NumTracks;
			_fightSequences.NumTracks = fightMidi.Tracks.Length;

			foreach (var Track in fightMidi.Tracks) {
				_fightSequences.TrackSections.Add(Track.Sections);
			}

			// merge
			stalkMidi.AddTracks(fightMidi);
		}

		PitchTo(0f, 0f);

		lock (this) {
			_midiSequencer.LoadMidi(stalkMidi, false);
			_currentTrack = 0;
			_currentSection = _stalkSequences.TrackSections[0][0];
			_midiSequencer.Play();
			_midiSequencer.SwitchSection(_currentTrack, _currentSection, 0f);
		}
	}

	void Update() {
		_transitionTimeout = Mathf.Max(_transitionTimeout - Time.deltaTime, 0f);
		
		UpdatePitch();
		ProcessSysExQueue();

		CurrentTrack = _midiSequencer.TrackNumber;
		CurrentTrackTime = SynthHelper.getTimeFromSample(_midiStreamSynthesizer.SampleRate, _midiSequencer.SampleTime);
		TotalTrackTime = SynthHelper.getTimeFromSample(_midiStreamSynthesizer.SampleRate, _midiSequencer.EndSampleTime);
	}

	private void PitchTo(float target, float time) {
		if (time <= 0f) {
			_currentPitch = _targetPitch;
			//_midiStreamSynthesizer.setGlobalPitchBend(target);
		} else {
			_targetPitch = target;
			_startPitch = _currentPitch;
			_pitchSpeed = time;
			_pitchTime = 0f;
		}
	}

	private void UpdatePitch() {
		if (_currentPitch != _targetPitch) {
			if (_pitchTime >= _pitchSpeed) {
				_currentPitch = _targetPitch;
			} else {
				_pitchTime += Time.deltaTime;
				_currentPitch = Mathf.Lerp(_startPitch, _targetPitch, _pitchTime / _pitchSpeed);
			}
			//_midiStreamSynthesizer.setGlobalPitchBend(_currentPitch);
		}
	}

	void ProcessSysExQueue() {

		int index;

		lock (_sysExQueue) {
			index = _sysExQueueIndex;
			_sysExQueueIndex = (_sysExQueueIndex + 1) & 1;
		}

		foreach (string msg in _sysExQueue[index]) {
			ProcessSysExEvent(msg);
		}

		_sysExQueue[index].Clear();
		
	}

	void ProcessSysExEvent(string msg) {
		Debug.Log("SysEx: " + msg);

		if (msg.Contains("from fight ")) {
			msg = msg.Substring("from fight ".Length);

			string[] times = msg.Split(',');
			int index = 0;
			foreach (var s in times) {
				stalkReturnTimes[index] = float.Parse(s);
				++index;
				if (index == stalkReturnTimes.Length) {
					break;
				}
			}

			if (index > 0) {
				while (index < stalkReturnTimes.Length) {
					stalkReturnTimes[index] = stalkReturnTimes[index-1];
					++index;
				}
			} else {
				for (int i = 0; i < stalkReturnTimes.Length; ++i) {
					stalkReturnTimes[i] = 0f;
				}
			}
		}

		/*if (msg == "to A") {
			_midiStreamSynthesizer.setAllChannelPitchBend(-1.5f);
			_midiStreamSynthesizer.NoteOffAll(false);
		} else if (msg == "to Aslow") {
			_midiStreamSynthesizer.setAllChannelSlowPitchBend(-1.5f);
			_midiStreamSynthesizer.NoteOffAll(false);
		} else if (msg == "to B") {
			_midiStreamSynthesizer.setAllChannelPitchBend(-0.5f);
			_midiStreamSynthesizer.NoteOffAll(false);
		} else if (msg == "to Bslow") {
			_midiStreamSynthesizer.setAllChannelSlowPitchBend(-0.5f);
			_midiStreamSynthesizer.NoteOffAll(false);
		} else if (msg == "to C") {
			_midiStreamSynthesizer.setAllChannelPitchBend(0f);
			_midiStreamSynthesizer.NoteOffAll(false);
		} else if (msg == "to Cslow") {
			_midiStreamSynthesizer.setAllChannelSlowPitchBend(0f);
			_midiStreamSynthesizer.NoteOffAll(false);
		} else if (msg == "to D") {
			_midiStreamSynthesizer.setAllChannelPitchBend(-10f);
			_midiStreamSynthesizer.NoteOffAll(false);
		} else if (msg == "to Dslow") {
			_midiStreamSynthesizer.setAllChannelSlowPitchBend(-10f);
			_midiStreamSynthesizer.NoteOffAll(false);
		}*/

		if (IsPlayingStalkTrack) {
			if (SwitchToFight) {
				// take a transition to fight?
				if (msg == "to B") {
					TransitionToFight(1, false);
				} else if (msg == "to Bslow") {
					TransitionToFight(1, true);
				} else if (msg == "to C") {
					TransitionToFight(2, false);
				} else if (msg == "to Cslow") {
					TransitionToFight(2, true);
				} else if (msg == "to D") {
					TransitionToFight(3, false);
				} else if (msg == "to Dslow") {
					TransitionToFight(3, true);
				}
			}
		} else {
			if (msg == "to A") {
				NotifyBranchFightTrack(0, false);
			} else if (msg == "to Aslow") {
				NotifyBranchFightTrack(0, true);
			} else if (msg == "to B") {
				NotifyBranchFightTrack(1, false);
			} else if (msg == "to Bslow") {
				NotifyBranchFightTrack(1, true);
			} else if (msg == "to C") {
				NotifyBranchFightTrack(2, false);
			} else if (msg == "to Cslow") {
				NotifyBranchFightTrack(2, true);
			} else if (msg == "to D") {
				NotifyBranchFightTrack(3, false);
			} else if (msg == "to Dslow") {
				NotifyBranchFightTrack(3, true);
			}
		}

		if (msg == "start new") {
			handleStartNew();
		}
	}

	public bool HandleSysExEvent(MidiEvent midiEvent) {
		int index;
		lock (_sysExQueue) {
			index = _sysExQueueIndex;
		}

		_sysExQueue[index].Add(midiEvent.Parameters[0] as string);
				
		return false;
	}

	void TransitionToFight(int track, bool slow) {
		Debug.Log("Fight transition");
		ToFighTrack(0, false);
	}

	void ToStalkTrack(int track, bool slow) {
		_currentTrack = _stalkSequences.FirstTrack + track;
		_currentSection = _stalkSequences.TrackSections[track][0];
		lock (this) {
			float offset = 0f;
			if (track == 0) {
				offset = stalkReturnTimes[RandRange(0, stalkReturnTimes.Length)]+Offset;
			}
			_midiSequencer.SwitchSection(_currentTrack, _currentSection, offset);
		}
	}

	void ToFighTrack(int track, bool slow) {
		_currentTrack = _fightSequences.FirstTrack + track;

		MidiTrack.Section[] sections = _fightSequences.TrackSections[track];

		_currentSection = sections[RandRange(0, sections.Length)];
		lock (this) {
			_midiSequencer.SwitchSection(_currentTrack, _currentSection, 0f);
		}
	}

	void NotifyBranchFightTrack(int track, bool slow) {
		if (!SwitchToFight && (_transitionTimeout <= 0f)) {
			Debug.Log("Fight branch: " + track);
			ToFighTrack(track, slow);
		}
	}

	void ResetTransitionTimeout() {
		_transitionTimeout = 5f;// Random.Range(25f, 60f);
		Debug.Log("transitionTimeout : " + _transitionTimeout);
	}

	void handleStartNew() {
		if (_bIsPlayingLevelMusic) {
			_midiStreamSynthesizer.setAllChannelSlowPitchBend(0f);
			_midiStreamSynthesizer.NoteOffAll(false);

			if (SwitchToFight) {
				ToFighTrack(0, false);
			} else {
				ToStalkTrack(0, false);
			}
		}
	}

	// OnGUI is called for rendering and handling
	// GUI events.
	void OnGUI() {
		// Make a background box
		GUILayout.BeginArea(new Rect(Screen.width / 2 - 75, Screen.height / 2 - 50, 150, 300));

		if (GUILayout.Button(_bSwitchToFight ? "Stalk" : "Fight")) {
			_bSwitchToFight = !_bSwitchToFight;

			if (!_bSwitchToFight) {
				ResetTransitionTimeout();
			}
		}

		// End the Groups and Area	
		GUILayout.EndArea();
	}

	public bool SwitchToFight {
		get { return _bSwitchToFight; }
		set { _bSwitchToFight = value; }
	}

	public int TrackNumber {
		get { return _currentTrack; }
		set { _currentTrack = value; }
	}

	public bool HandleEndOfTrack() {
		handleStartNew();
		return true;
	}

	public bool IsPlayingStalkTrack {
		get { return _currentTrack < _stalkSequences.NumTracks; }
	}
	
	private void OnAudioFilterRead(float[] data, int channels) {
		lock (this) {
			_midiStreamSynthesizer.GetNext(_sampleBuffer);
		}
		System.Array.Copy(_sampleBuffer, data, data.Length);
	}
}