using System;
using System.Collections.Generic;
using CSharpSynth.Midi;
using CSharpSynth.Synthesis;
using CSharpSynth.Banks;
using UnityEngine;

namespace CSharpSynth.Sequencer
{
    public class MidiSequencer
    {
        //--Variables
        private MidiFile _MidiFile;
        private StreamSynthesizer synth;
        private int[] currentPrograms;
        private List<byte> blockList;
        private double PitchWheelSemitoneRange = 2.0;
        private bool playing = false;
        private bool looping = false;
        private MidiSequencerEvent seqEvt;
        private int sampleTime;
        private int eventIndex;
		private int trackNumber;
		private MidiTrack.Section trackSection;
        //--Events
        public delegate void NoteOnEventHandler(int channel, int note, int velocity);
        public event NoteOnEventHandler NoteOnEvent;
        public delegate void NoteOffEventHandler(int channel, int note);
        public event NoteOffEventHandler NoteOffEvent;
        //--Public Properties
        public bool isPlaying
        {
            get { return playing; }
        }
        public int SampleTime
        {
            get { return sampleTime; }
        }
        public int EndSampleTime
        {
			get { return (_MidiFile != null) ? (int)_MidiFile.Tracks[trackNumber].TotalTime : 0; }
        }
        public TimeSpan EndTime
        {
            get { return new TimeSpan(0, 0, (int)SynthHelper.getTimeFromSample(synth.SampleRate, (int)_MidiFile.Tracks[0].TotalTime)); }
        }
        public TimeSpan Time
        {
            get { return new TimeSpan(0, 0, (int)SynthHelper.getTimeFromSample(synth.SampleRate, sampleTime)); }
            set { SetTime(value); }
        }
		public double PitchWheelRange
        {
            get { return PitchWheelSemitoneRange; }
            set { PitchWheelSemitoneRange = value; }
        }
		public int EventIndex {
			get { return eventIndex; }
		}
		public int TrackNumber {
			get { return trackNumber; }
			set {
				if ((trackNumber != value) && (value >= 0) && (value < NumTracks)) {
					eventIndex = 0;
					sampleTime = 0;
					trackNumber = value;
					//Array.Clear(currentPrograms, 0, currentPrograms.Length);
					ResetControllers();
					_MidiFile.BeatsPerMinute = 120;
					SeekToFirstNote();
					synth.setAllChannelSlowPitchBend(0f);
					synth.NoteOffAll(true);
				}
			}
		}
		public int NumTracks {
			get { return (_MidiFile != null) ? _MidiFile.Tracks.Length : 0; }
		}
		public void SwitchSection(int trackIndex, MidiTrack.Section section, float offset) {
			if ((trackIndex != trackNumber) || (trackSection != section)) {
				trackNumber = trackIndex;
				eventIndex = section.EventIndex;
				sampleTime = (int)section.StartSample;
				trackSection = section;

				SeekToFirstNote();
				SilentProcess(SynthHelper.getSampleFromTime(synth.SampleRate, (_MidiFile.MicroSecondsPerQuarterNote / 1000000f) * offset * 4));
				synth.NoteOffAll(false);
			}
		}
		
        //--Public Methods
        public MidiSequencer(StreamSynthesizer synth)
        {
            currentPrograms = new int[16]; //16 channels
            this.synth = synth;
            this.synth.setSequencer(this);
            blockList = new List<byte>();
            seqEvt = new MidiSequencerEvent();
        }
        public string getProgramName(int channel)
        {
            if (channel == 9)
                return synth.SoundBank.getInstrument(currentPrograms[channel], true).Name;
            else
                return synth.SoundBank.getInstrument(currentPrograms[channel], false).Name;
        }
        public int getProgramIndex(int channel)
        {
            return currentPrograms[channel];
        }
        public void setProgram(int channel, int program)
        {
            currentPrograms[channel] = program;
        }
        public bool Looping
        {
            get { return looping; }
            set { looping = value; }
        }
		
        public bool LoadMidi(MidiFile midi, bool UnloadUnusedInstruments)
        {
            if (playing == true)
                return false;
            _MidiFile = midi;
            if (_MidiFile.SequencerReady == false)
            {
                try
                {
                    //Convert delta time to sample time
                    eventIndex = 0;
					sampleTime = 0;
					trackNumber = 1;

					for (int i = 0; i < _MidiFile.Tracks.Length; ++i) {
						uint lastSample = 0;
						for (int x = 0; x < _MidiFile.Tracks[i].MidiEvents.Length; x++) {
							_MidiFile.Tracks[i].MidiEvents[x].deltaTime = lastSample + (uint)DeltaTimetoSamples(_MidiFile.Tracks[i].MidiEvents[x].deltaTime);
							lastSample = _MidiFile.Tracks[i].MidiEvents[x].deltaTime;
							//Update tempo
							if (_MidiFile.Tracks[i].MidiEvents[x].midiMetaEvent == MidiHelper.MidiMetaEvent.Tempo) {
								_MidiFile.BeatsPerMinute = MidiHelper.MicroSecondsPerMinute / System.Convert.ToUInt32(_MidiFile.Tracks[i].MidiEvents[x].Parameters[0]);
							}
						}
						foreach (MidiTrack.Section Section in _MidiFile.Tracks[i].Sections) {
							Section.StartSample = _MidiFile.Tracks[i].MidiEvents[Section.EventIndex].deltaTime;
							Section.EndSample = (ulong)DeltaTimetoSamples((uint)Section.EndSample);
						}

						//Set total time to proper value
						_MidiFile.Tracks[i].TotalTime = _MidiFile.Tracks[i].MidiEvents[_MidiFile.Tracks[i].MidiEvents.Length-1].deltaTime;
					}

                    //reset tempo
                    _MidiFile.BeatsPerMinute = 120;
                    //mark midi as ready for sequencing
                    _MidiFile.SequencerReady = true;
                }
                catch (Exception ex)
                {
                    //UnitySynth
                    Debug.Log("Error Loading Midi:\n" + ex.Message);
                    return false;
                }
            }
            blockList.Clear();
            if (UnloadUnusedInstruments == true)
            {
                if (synth.SoundBank == null)
                {//If there is no bank warn the developer =)
					Debug.LogError("No Soundbank loaded !");
                }
                else
                {
                    string bankStr = synth.SoundBank.BankPath;
                    //Remove old bank being used by synth
                    synth.UnloadBank();
                    //Add the bank and switch to it with the synth
                    BankManager.addBank(new InstrumentBank(synth.SampleRate, bankStr, _MidiFile.Tracks[0].Programs, _MidiFile.Tracks[0].DrumPrograms));
                    synth.SwitchBank(BankManager.Count - 1);
                }
            }
            return true;
        }
        public bool LoadMidi(string file, bool UnloadUnusedInstruments)
        {
            if (playing == true)
                return false;
            MidiFile mf = null;
            try
            {
                mf = new MidiFile(file);
            }
            catch (Exception ex)
            {
                //UnitySynth
                Debug.LogError("Error Loading Midi:\n" + ex.Message);
                return false;
            }
            return LoadMidi(mf, UnloadUnusedInstruments);
        }
        public void Play()
        {
            if (playing == true)
                return;
            //Clear the current programs for the channels.
            Array.Clear(currentPrograms, 0, currentPrograms.Length);
            //Clear vol, pan, and tune
            ResetControllers();
            //set bpm
            _MidiFile.BeatsPerMinute = 120;
            //Let the synth know that the sequencer is ready.
            playing = true;
        }
        public void Stop(bool immediate)
        {
			sampleTime = 0;
			eventIndex = 0;
            playing = false;
            if (immediate)
                synth.NoteOffAll(true);
            else
                synth.NoteOffAll(false);
        }
		public void Pause(bool immediate) 
		{
			playing = false;
			if (immediate)
				synth.NoteOffAll(true);
			else
				synth.NoteOffAll(false);
		}
        public bool isChannelMuted(int channel)
        {
            if (blockList.Contains((byte)channel))
                return true;
            return false;
        }
        public void MuteChannel(int channel)
        {
            if (channel > -1 && channel < 16)
                if (blockList.Contains((byte)channel) == false)
                    blockList.Add((byte)channel);
        }
        public void UnMuteChannel(int channel)
        {
            if (channel > -1 && channel < 16)
                blockList.Remove((byte)channel);
        }
        public void MuteAllChannels()
        {
            for (int x = 0; x < 16; x++)
                blockList.Add((byte)x);
        }
        public void UnMuteAllChannels()
        {
            blockList.Clear();
        }
        public void ResetControllers()
        {
            //Reset Pan Positions back to 0.0f
            Array.Clear(synth.PanPositions, 0, synth.PanPositions.Length);
            //Set Tuning Positions back to 0.0f
            Array.Clear(synth.TunePositions, 0, synth.TunePositions.Length);
            //Reset Vol Positions back to 1.00f
            for (int x = 0; x < synth.VolPositions.Length; x++)
                synth.VolPositions[x] = 1.00f;
        }
        public MidiSequencerEvent Process(int frame)
        {
            seqEvt.Events.Clear();
            //stop or loop
			if ((sampleTime >= (int)_MidiFile.Tracks[trackNumber].TotalTime)/* || ((trackSection != null) && (sampleTime >= (int)trackSection.EndSample))*/)
            {
                sampleTime = 0;
				eventIndex = 0;
				trackSection = null;

                if (looping == true)
                {
                    //Clear the current programs for the channels.
                    Array.Clear(currentPrograms, 0, currentPrograms.Length);
                    //Clear vol, pan, and tune
                    ResetControllers();
                    //set bpm
                    _MidiFile.BeatsPerMinute = 120;
				} else if (HandleEndOfTrack(synth)) {
					//Clear vol, pan, and tune
					ResetControllers();
					//set bpm
					_MidiFile.BeatsPerMinute = 120;

				} else {
					playing = false;
					synth.NoteOffAll(true);
					return null;
				}
            }
			while (eventIndex < _MidiFile.Tracks[trackNumber].EventCount && _MidiFile.Tracks[trackNumber].MidiEvents[eventIndex].deltaTime < (sampleTime + frame))
            {
				seqEvt.Events.Add(_MidiFile.Tracks[trackNumber].MidiEvents[eventIndex]);
                eventIndex++;
            }
            return seqEvt;
        }
		public virtual bool HandleEndOfTrack(StreamSynthesizer synth) { return false; }
        public void IncrementSampleCounter(int amount)
        {
            sampleTime = sampleTime + amount;
        }

		public virtual bool ProcessSysExEvent(MidiEvent midiEvent) { return false; }

        public bool ProcessMidiEvent(MidiEvent midiEvent)
        {
            if (midiEvent.midiChannelEvent != MidiHelper.MidiChannelEvent.None)
            {
                switch (midiEvent.midiChannelEvent)
                {
                    case MidiHelper.MidiChannelEvent.Program_Change:
                        if (midiEvent.channel != 9)
                        {
                            if (midiEvent.parameter1 < synth.SoundBank.InstrumentCount)
                                currentPrograms[midiEvent.channel] = midiEvent.parameter1;
                        }
                        else //its the drum channel
                        {
                            if (midiEvent.parameter1 < synth.SoundBank.DrumCount)
                                currentPrograms[midiEvent.channel] = midiEvent.parameter1;
                        }
                        break;
                    case MidiHelper.MidiChannelEvent.Note_On:
                        if (blockList.Contains(midiEvent.channel))
                            return false;
                        if (this.NoteOnEvent != null)
                            this.NoteOnEvent(midiEvent.channel, midiEvent.parameter1, midiEvent.parameter2);
                        synth.NoteOn(midiEvent.channel, midiEvent.parameter1, midiEvent.parameter2, currentPrograms[midiEvent.channel]);
                        break;
                    case MidiHelper.MidiChannelEvent.Note_Off:
                        if (this.NoteOffEvent != null)
                            this.NoteOffEvent(midiEvent.channel, midiEvent.parameter1);
                        synth.NoteOff(midiEvent.channel, midiEvent.parameter1);
                        break;
                    case MidiHelper.MidiChannelEvent.Pitch_Bend:
                        //Store PitchBend as the # of semitones higher or lower
                        synth.TunePositions[midiEvent.channel] = (double)midiEvent.Parameters[1] * PitchWheelSemitoneRange;
                        break;
                    case MidiHelper.MidiChannelEvent.Controller:
                        switch (midiEvent.GetControllerType())
                        {
                            case MidiHelper.ControllerType.AllNotesOff:
                                synth.NoteOffAll(true);
                                break;
                            case MidiHelper.ControllerType.MainVolume:
                                synth.VolPositions[midiEvent.channel] = midiEvent.parameter2 / 127.0f;
                                break;
                            case MidiHelper.ControllerType.Pan:
                                synth.PanPositions[midiEvent.channel] = (midiEvent.parameter2 - 64) == 63 ? 1.00f : (midiEvent.parameter2 - 64) / 64.0f;
                                break;
                            case MidiHelper.ControllerType.ResetControllers:
                                ResetControllers();
                                break;
                            default:
                                break;
                        }
                        break;
                    default:
                        break;
                }
            }
            else if (midiEvent.midiMetaEvent != MidiHelper.MidiMetaEvent.None)
            {
                switch (midiEvent.midiMetaEvent)
                {
                    case MidiHelper.MidiMetaEvent.Tempo:
                        _MidiFile.BeatsPerMinute = MidiHelper.MicroSecondsPerMinute / System.Convert.ToUInt32(midiEvent.Parameters[0]);
                        break;
                    default:
                        break;
                }
			} else if (midiEvent.midiSysExEvent != MidiHelper.MidiSysExEvent.None) {
				return ProcessSysExEvent(midiEvent);
			}

			return false;
        }
        public void Dispose()
        {
            Stop(true);
            //Set anything that may become a circular reference to null...
            synth = null;
            _MidiFile = null;
            seqEvt = null;
        }
        //--Private Methods
        private int DeltaTimetoSamples(uint DeltaTime)
        {
            return SynthHelper.getSampleFromTime(synth.SampleRate, (DeltaTime * (60.0f / (((int)_MidiFile.BeatsPerMinute) * _MidiFile.MidiHeader.DeltaTiming))));
        }
        private void SetTime(TimeSpan time)
        {
            int _stime = SynthHelper.getSampleFromTime(synth.SampleRate, (float)time.TotalSeconds);
            if (_stime > sampleTime)
            {
                SilentProcess(_stime - sampleTime);
            }
            else if (_stime < sampleTime)
            {//we have to restart the midi to make sure we get the right temp, instrument, etc
                synth.Stop();
                Array.Clear(currentPrograms, 0, currentPrograms.Length);
                ResetControllers();
                _MidiFile.BeatsPerMinute = 120;
                SilentProcess(_stime);
            }
        }
        private void SilentProcess(int amount)
        {
			while (eventIndex < _MidiFile.Tracks[trackNumber].EventCount && _MidiFile.Tracks[trackNumber].MidiEvents[eventIndex].deltaTime < (sampleTime + amount))
            {
				if (_MidiFile.Tracks[trackNumber].MidiEvents[eventIndex].midiChannelEvent != MidiHelper.MidiChannelEvent.Note_On)
					ProcessMidiEvent(_MidiFile.Tracks[trackNumber].MidiEvents[eventIndex]);               
                eventIndex++;
            }
            sampleTime = sampleTime + amount;
        }

		public void SeekToFirstNote() {
			if (eventIndex > 0) {
				sampleTime = (int)_MidiFile.Tracks[trackNumber].MidiEvents[eventIndex-1].deltaTime;
			} else {
				sampleTime = 0;
			}

			while (eventIndex < _MidiFile.Tracks[trackNumber].EventCount) {
				MidiEvent e = _MidiFile.Tracks[trackNumber].MidiEvents[eventIndex];

				sampleTime = (int)_MidiFile.Tracks[trackNumber].MidiEvents[eventIndex].deltaTime;

				if (e.midiChannelEvent == MidiHelper.MidiChannelEvent.Note_On) {
					return;
				} else {
					ProcessMidiEvent(_MidiFile.Tracks[trackNumber].MidiEvents[eventIndex]);
				}
				
				eventIndex++;
			}
		}
    }
}
