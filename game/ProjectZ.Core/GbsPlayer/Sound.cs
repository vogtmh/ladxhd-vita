using System;
using Microsoft.Xna.Framework.Audio;

namespace GBSPlayer
{
    public class Sound
    {
        private readonly CDynamicEffectInstance _soundOutput;

        public bool WasStopped;
        public int PendingCount => _soundOutput.GetPendingBufferCount();
        public int BufferedCount => _soundOutput.GetBufferedChunkCount();

        public SoundState OutputState => _soundOutput.State;

        private int _endBufferCount;
        private int _bufferCount;

        private const int OutputRate = 44100;

        private byte[] _soundBuffer = new byte[(OutputRate / 100) * 2]; // 100 buffers per second
        private int _bufferIndex;

        // FF10-FF3F
        private byte[] _soundRegister = new byte[0x30];

        // frame sequencer
        private byte _frameSequencerCounter;
        private double _frameSeqAcc;

        // Square 1
        private byte _modeOneNumberOfSweepShifts;
        private bool _modeOneSweepSubtraction;
        private byte _modeOneSweepTime;
        private byte _modeOneSweepCounter;

        private byte _square1DutyPattern;

        private int _square1LengthLoad;
        private byte _square1LengthCounter;

        // envelope
        private byte _square1StartVolume;
        private bool _square1EnvelopeAddMode;
        private byte _square1EnvelopePeriod;
        private int _square1EnvelopeCounter;
        private int _square1Volume;

        private short _square1Frequency;
        private double _modeOneFreqCounter;
        private double _modeOneFreqTime;
        private byte _square1WaveCounter;

        private bool _modeOneCounterEnable;

        private bool _square1Running;
        private int _waveOne = 1;


        // Square 2
        private byte _square2DutyPattern;

        private byte _square2StartVolume;
        private bool _square2EnvelopeAddMode;
        private byte _square2EnvelopePeriod;
        private int _square2EnvelopeCounter;
        private int _square2Volume;

        private byte _square2LengthCounter;
        private int _modeTwoSoundLength;

        private short _square2Frequency;
        private double _modeTwoFreqTime;
        private double _modeTwoFreqCounter;
        private byte _square2WaveCounter;

        private bool _modeTwoCounterEnable;

        private bool _square2Running;
        private int _waveTwo = 1;


        // Wave
        private bool _waveDACpower;

        private byte _waveLengthCounter;
        private int _modeThreeSoundLength;

        private byte _waveVolumeCode;

        private short _modeThreeFrequency;

        private double _modeThreeFreqCounter;
        private double _modeThreeFreqTime;

        private bool _waveLengthEnable;

        private byte[] _waveNibbles = new byte[32];
        private int _waveIndex;

        private short _waveThree;

        // Noise
        private byte _noiseLengthLoad;
        private byte _noiseVolume;
        private byte _noiseInitVolume;
        private bool _noiseEnvelopeAddMode;
        private byte _noiseEnvelopePeriod;

        private byte _noiseClockShift;
        private bool _noiseWidthMode;
        private byte _noiseDivisorCode;
        private bool _noiseLengthEnable;

        private short _noiseLfsr;

        private double _noiseTimeSteps;

        private int _noiseEnvelopeCounter;

        private byte _noiseLengthCounter;

        // FF24
        private byte _soundOutputLevel;
        // FF25
        private byte _channelLeftRightControl;
        // FF26
        private byte _soundOnOff;

        private double _highPassFilterCapacitor;

        public int DebugCounter;

        public Sound()
        {
            _soundOutput = new CDynamicEffectInstance(OutputRate);
        }

        public void Init()
        {
            _frameSeqAcc = 0.0;
            _frameSequencerCounter = 0;

            _square1LengthCounter = 0;
            _square2LengthCounter = 0;
            _waveLengthCounter = 0;
            _noiseLengthCounter = 0;

            _modeOneFreqCounter = 0;
            _modeTwoFreqCounter = 0;
            _modeThreeFreqCounter = 0;

            _square1EnvelopeCounter = 0;
            _square2EnvelopeCounter = 0;
            _noiseEnvelopeCounter = 0;

            _highPassFilterCapacitor = 0;

            _bufferIndex = 0;
            _bufferCount = 0;
            _endBufferCount = 0;
            WasStopped = false;
        }

        public bool IsPlaying()
        {
            return _soundOutput.State == SoundState.Playing;
        }

        public bool FinishedPlaying()
        {
            return BufferedCount == 0;
        }

        public void SetStopTime(float length)
        {
            _endBufferCount = (int)(length * OutputRate);
        }

        public void Pump()
        {
            _soundOutput.Pump();
        }

        public void Play()
        {
            _soundOutput.Play();
        }

        public void Pause()
        {
            _soundOutput.Pause();
        }

        public void Resume()
        {
            _soundOutput.Resume();
        }

        public void Stop()
        {
            WasStopped = false;
            _bufferCount = 0;
            _bufferIndex = 0;
            _soundOutput.ClearQueuedPcm();
            _soundOutput.Stop();
        }

        public void SetVolume(float volume)
        {
            _soundOutput.SetVolume(volume);
        }

        public void AddCurrentBuffer()
        {
            if (_bufferIndex <= 0)
            {
                _bufferIndex = 0;
                return;
            }
            var copy = new byte[_bufferIndex];
            Buffer.BlockCopy(_soundBuffer, 0, copy, 0, _bufferIndex);

            _soundOutput.SubmitBuffer(copy, 0, copy.Length);
            _bufferIndex = 0;
        }

        private static byte GetDutyPattern(byte duty)
        {
            return duty switch
            {
                0 => 0b00000001, // 12.5%
                1 => 0b10000001, // 25%
                2 => 0b10000111, // 50%
                3 => 0b01111110, // 75%
                _ => 0b00000001
            };
        }

        public byte this[int index]
        {
            get
            {
                // Square 1
                if (index == 0xFF10)
                    return _soundRegister[index - 0xFF10];
                if (index == 0xFF11)
                    return (byte)(_soundRegister[index - 0xFF10] & 0xC0);
                if (index == 0xFF12)
                    return _soundRegister[index - 0xFF10];
                if (index == 0xFF13)
                    return 0;
                if (index == 0xFF14)
                    return _soundRegister[index - 0xFF10];

                // Square 2
                if (index == 0xFF15) // not used
                    return 0;
                if (index == 0xFF16)
                    return (byte)(_soundRegister[index - 0xFF10] & 0xC0);
                if (index == 0xFF17)
                    return _soundRegister[index - 0xFF10];
                if (index == 0xFF18)
                    return 0;
                if (index == 0xFF19)
                    return _soundRegister[index - 0xFF10];

                // Wave
                if (index == 0xFF1A)
                    return (byte)(_waveDACpower ? 0x80 : 0x00);
                if (index == 0xFF1B)
                    return 0;
                if (index == 0xFF1C)
                    return (byte)(_waveVolumeCode << 5);
                if (index == 0xFF1D)
                    return 0;
                if (index == 0xFF1E)
                    return (byte)(_waveLengthEnable ? 0x40 : 0x00);

                // Noise
                if (0xFF1F <= index && index <= 0xFF23)
                {

                }

                // Stuff
                if (index == 0xFF24)
                    return _soundOutputLevel;
                if (index == 0xFF25)
                    return _channelLeftRightControl;
                if (index == 0xFF26)
                    return _soundOnOff;

                return _soundRegister[index - 0xFF10];
            }
            set
            {
                // Square 1
                if (index == 0xFF10)
                {
                    _soundRegister[index - 0xFF10] = (byte)(value & 0x7F);

                    _modeOneNumberOfSweepShifts = (byte)(value & 0x07);
                    _modeOneSweepSubtraction = (value & 0x08) == 0x08;
                    _modeOneSweepTime = (byte)((value & 0x70) >> 4);

                    _modeOneSweepCounter = 0;
                }
                else if (index == 0xFF11)
                {
                    _soundRegister[index - 0xFF10] = (byte)(value & 0xC7);

                    // Sound Length = (64-t1) * (1/256) sec
                    _square1LengthLoad = 64 - (byte)(value & 0x3F); // was 0x07 before?

                    var waveDuty = (byte)(value >> 6);
                    _square1DutyPattern = GetDutyPattern(waveDuty);
                }
                else if (index == 0xFF12)
                {
                    _soundRegister[index - 0xFF10] = value;

                    _square1StartVolume = (byte)(value >> 4);
                    _square1EnvelopeAddMode = (value & 0x08) == 0x08;
                    _square1EnvelopePeriod = (byte)(value & 0x07);
                }
                else if (index == 0xFF13)
                {
                    _soundRegister[index - 0xFF10] = value;

                    _square1Frequency = (short)((_square1Frequency & 0x700) + value); // this was 0xF00 before
                    UpdateSquare1FrequencyTime();
                }
                else if (index == 0xFF14)
                {
                    _soundRegister[index - 0xFF10] = (byte)(value & 0x40);

                    _square1Frequency = (short)(((value & 0x07) << 8) | (_square1Frequency & 0xFF));
                    UpdateSquare1FrequencyTime();

                    _modeOneCounterEnable = (value & 0x40) == 0x40;

                    if ((value & 0x80) == 0x80)
                    {
                        _square1Running = true;
                        _soundOnOff |= 0x01;

                        _square1LengthCounter = 0;

                        // Reset waveform phase/timer
                        _modeOneFreqCounter = 0;
                        _square1WaveCounter = 0;
                        _waveOne = ((_square1DutyPattern >> (7 - _square1WaveCounter)) & 0x01) != 0 ? 1 : 0;

                        // Reset envelope
                        _square1Volume = _square1StartVolume;
                        _square1EnvelopeCounter = _square1EnvelopePeriod;

                        // Reset sweep timer
                        _modeOneSweepCounter = 0;
                    }
                }

                // Square 2
                else if (index == 0xFF15) { } // not used
                else if (index == 0xFF16)
                {
                    _soundRegister[index - 0xFF10] = (byte)(value & 0xC7);

                    // Sound Length = (64-t1) * (1/256) sec
                    _modeTwoSoundLength = 64 - (byte)(value & 0x3F);

                    // 12.5, 25, 50, 75
                    var waveDuty = (byte)(value >> 6);
                    _square2DutyPattern = GetDutyPattern(waveDuty);
                }
                else if (index == 0xFF17)
                {
                    _soundRegister[index - 0xFF10] = value;

                    _square2StartVolume = (byte)(value >> 4);
                    _square2EnvelopeAddMode = (value & 0x08) == 0x08;
                    _square2EnvelopePeriod = (byte)(value & 0x07);
                }
                else if (index == 0xFF18)
                {
                    _soundRegister[index - 0xFF10] = value;

                    _square2Frequency = (short)((_square2Frequency & 0x700) + value); // this was 0xF00 before
                    UpdateSquare2FrequencyTime();
                }
                else if (index == 0xFF19)
                {
                    _soundRegister[index - 0xFF10] = (byte)(value & 0x40);

                    _square2Frequency = (short)(((value & 0x07) << 8) + (_square2Frequency & 0xFF));
                    UpdateSquare2FrequencyTime();

                    _modeTwoCounterEnable = (value & 0x40) == 0x40;

                    if ((value & 0x80) == 0x80)
                    {
                        _square2Running = true;
                        _soundOnOff |= 0x02;

                        _square2LengthCounter = 0;

                        // Reset waveform phase/timer
                        _modeTwoFreqCounter = 0;
                        _square2WaveCounter = 0;
                        _waveTwo = ((_square2DutyPattern >> (7 - _square2WaveCounter)) & 0x01) != 0 ? 1 : 0;

                        // Reset envelope
                        _square2Volume = _square2StartVolume;
                        _square2EnvelopeCounter = _square2EnvelopePeriod;
                    }
                }

                // Wave
                else if (index == 0xFF1A)
                {
                    _waveDACpower = (value & 0x80) == 0x80;
                    // When the sound OFF flag(bit 7 of NR30) is reset to "0", cancellation of the OFF mode must be performed by setting the sound OFF flag to a "1".This is performed by Sound 3.
                }
                else if (index == 0xFF1B)
                {
                    // (256-t1) * (1/256) sec
                    _modeThreeSoundLength = 256 - value;
                }
                else if (index == 0xFF1C)
                {
                    _waveVolumeCode = (byte)((value >> 5) & 0x03);
                    _waveThree = _waveVolumeCode switch
                    {
                        1 => _waveNibbles[_waveIndex],
                        2 => (short)(_waveNibbles[_waveIndex] >> 1),
                        3 => (short)(_waveNibbles[_waveIndex] >> 2),
                        _ => (short)0
                    };
                }
                else if (index == 0xFF1D)
                {
                    _modeThreeFrequency = (short)((_modeThreeFrequency & 0x700) + value);
                    UpdateWaveFrequencyTime();
                }
                else if (index == 0xFF1E)
                {
                    _modeThreeFrequency = (short)((_modeThreeFrequency & 0xFF) + ((value & 0x07) << 8));
                    UpdateWaveFrequencyTime();

                    bool trigger = (value & 0x80) == 0x80; // local, no need for a field
                    _waveLengthEnable = (value & 0x40) == 0x40;

                    if (_waveDACpower && trigger)
                    {
                        _soundOnOff |= 0x04;
                        _waveLengthCounter = 0;
                        _modeThreeFreqCounter = 0;
                        _waveIndex = 31;
                        _waveThree = 0;
                    }
                }

                // Noise
                else if (index == 0xFF1F) { } // not used
                else if (index == 0xFF20)
                {
                    _noiseLengthLoad = (byte)(64 - (value & 0x3F));
                }
                else if (index == 0xFF21)
                {
                    _noiseInitVolume = (byte)(value >> 4);
                    _noiseEnvelopeAddMode = (value & 0x08) == 0x08;
                    _noiseEnvelopePeriod = (byte)(value & 0x07);
                }
                else if (index == 0xFF22)
                {
                    _noiseClockShift = (byte)(value >> 4);
                    _noiseWidthMode = (value & 0x08) == 0x08;
                    _noiseDivisorCode = (byte)(value & 0x07);
                }
                else if (index == 0xFF23)
                {
                    bool noiseTrigger = (value & 0x80) == 0x80;
                    _noiseLengthEnable = (value & 0x40) == 0x40;

                    // turn channel on/off
                    if (noiseTrigger)
                    {
                        _soundOnOff |= 0x08;

                        _noiseLengthCounter = 0;

                        _noiseVolume = _noiseInitVolume;
                        _noiseEnvelopeCounter = _noiseEnvelopePeriod;

                        // Reset noise timer/LFSR
                        _noiseTimeSteps = 0;

                        if (_noiseWidthMode)
                            _noiseLfsr = 0x7F;
                        else
                            _noiseLfsr = 0x7FFF;
                    }
                    else
                    {
                        _soundOnOff &= 0xF7;
                    }
                }
                else if (index == 0xFF24)
                {
                    _soundOutputLevel = value;
                }
                else if (index == 0xFF25)
                    _channelLeftRightControl = value;
                // NR52
                else if (index == 0xFF26)
                    _soundOnOff = value;

                // wave data
                else if (0xFF30 <= index && index <= 0xFF3F)
                {
                    _soundRegister[index - 0xFF10] = value;

                    _waveNibbles[(index - 0xFF30) * 2] = (byte)(value >> 4);
                    _waveNibbles[(index - 0xFF30) * 2 + 1] = (byte)(value & 0x0F);
                }
            }
        }

        private void UpdateSquare1FrequencyTime()
        {
            _modeOneFreqTime = OutputRate / (4194304.0 / (4 * (2048 - _square1Frequency)));
        }

        private void UpdateSquare2FrequencyTime()
        {
            _modeTwoFreqTime = OutputRate / (4194304.0 / (4 * (2048 - _square2Frequency)));
        }

        private void UpdateWaveFrequencyTime()
        {
            _modeThreeFreqTime = OutputRate / (4194304.0 / (2 * (2048 - _modeThreeFrequency)));
        }

        // gets called 44100 (outputRate) times a second
        public void UpdateBuffer()
        {
            DebugCounter++;

            // Advance frame sequencer with fractional timing (exact 512 Hz)
            _frameSeqAcc += 512.0 / OutputRate;

            // Tick as many times as needed (normally 0 or 1, occasionally more if you ever change rates)
            while (_frameSeqAcc >= 1.0)
            {
                _frameSeqAcc -= 1.0;

                // 256Hz Length Ctr Clock
                if (_frameSequencerCounter % 2 == 0)
                {
                    _square1LengthCounter++;
                    _square2LengthCounter++;
                    _waveLengthCounter++;
                    _noiseLengthCounter++;

                    if (_square1LengthCounter >= _square1LengthLoad && _modeOneCounterEnable)
                        _soundOnOff &= 0xFE;

                    if (_square2LengthCounter >= _modeTwoSoundLength && _modeTwoCounterEnable)
                        _soundOnOff &= 0xFD;

                    if (_waveLengthCounter >= _modeThreeSoundLength && _waveLengthEnable)
                    {
                        _soundOnOff &= 0xFB;
                    }

                    if (_noiseLengthCounter >= _noiseLengthLoad && _noiseLengthEnable)
                        _soundOnOff &= 0xF7;
                }

                // 64Hz Volume Envelope Clock
                if (_frameSequencerCounter == 7)
                {
                    if (_square1EnvelopePeriod != 0)
                    {                   
                        _square1EnvelopeCounter--;

                        if (_square1EnvelopeCounter <= 0 && ((!_square1EnvelopeAddMode && _square1Volume > 0) || (_square1EnvelopeAddMode && _square1Volume < 15)))
                        {
                            _square1EnvelopeCounter = _square1EnvelopePeriod;
                            _square1Volume += _square1EnvelopeAddMode ? 1 : -1;
                        }
                    }
                    if (_square2EnvelopePeriod != 0)
                    {                   
                        _square2EnvelopeCounter--;

                        if (_square2EnvelopeCounter <= 0 && ((!_square2EnvelopeAddMode && _square2Volume > 0) || (_square2EnvelopeAddMode && _square2Volume < 15)))
                        {
                            _square2EnvelopeCounter = _square2EnvelopePeriod;
                            _square2Volume += _square2EnvelopeAddMode ? 1 : -1;
                        }
                    }
                    if (_noiseEnvelopePeriod != 0)
                    {                   
                        _noiseEnvelopeCounter--;

                        if (_noiseEnvelopeCounter <= 0 && ((!_noiseEnvelopeAddMode && _noiseVolume > 0) || (_noiseEnvelopeAddMode && _noiseVolume < 15)))
                        {
                            _noiseEnvelopeCounter = _noiseEnvelopePeriod;
                            _noiseVolume += (byte)(_noiseEnvelopeAddMode ? 1 : -1);
                        }
                    }
                }

                // 128Hz Sweep Clock
                if (_frameSequencerCounter == 2 || _frameSequencerCounter == 6)
                {
                    _modeOneSweepCounter++;

                    if (_modeOneSweepTime != 0 && _modeOneNumberOfSweepShifts != 0 && _modeOneSweepCounter >= _modeOneSweepTime)
                    {
                        _modeOneSweepCounter = 0;
                        var newFreq = (short)(_square1Frequency + (_modeOneSweepSubtraction ? -1 : 1) * (_square1Frequency >> _modeOneNumberOfSweepShifts));

                        if ((newFreq & 0x7FF) != newFreq)
                            _soundOnOff &= 0xFE;
                        else if (newFreq > 0)
                            _square1Frequency = newFreq;

                        _soundRegister[0xFF13 - 0xFF10] = (byte)(_square1Frequency & 0xFF);
                        _soundRegister[0xFF14 - 0xFF10] = (byte)((_soundRegister[0xFF14 - 0xFF10] & 0xF8) + (_square1Frequency >> 8));

                        UpdateSquare1FrequencyTime();
                    }
                }

                _frameSequencerCounter++;
                if (_frameSequencerCounter >= 8)
                    _frameSequencerCounter = 0;
            }
            

            // Square 1
            short channel0 = 0;
            {
                if (_square1Running)
                {
                    _modeOneFreqCounter++;

                    if (_modeOneFreqCounter >= _modeOneFreqTime)
                    {
                        _modeOneFreqCounter -= _modeOneFreqTime;

                        // update wave
                        _waveOne = ((_square1DutyPattern >> (7 - _square1WaveCounter)) & 0x01) != 0 ? 1 : 0;
                        _square1WaveCounter = (byte)((_square1WaveCounter + 1) % 8);
                    }
                }

                // value between 0-15
                channel0 = (short)(_waveOne * _square1Volume);

                // Sound 1 ON Flag  
                if ((_soundOnOff & 0x01) == 0x00)
                    channel0 = 0;
            }

            // Square 2
            short channel1 = 0;
            {
                if (_square2Running)
                {
                    _modeTwoFreqCounter++;

                    if (_modeTwoFreqCounter >= _modeTwoFreqTime)
                    {
                        _modeTwoFreqCounter -= _modeTwoFreqTime;

                        // update wave
                        _waveTwo = ((_square2DutyPattern >> (7 - _square2WaveCounter)) & 0x01) != 0 ? 1 : 0;
                        _square2WaveCounter = (byte)((_square2WaveCounter + 1) % 8);
                    }
                }

                channel1 = (short)(_waveTwo * _square2Volume);

                // Sound 2 ON Flag  
                if ((_soundOnOff & 0x02) == 0x00)
                    channel1 = 0;
            }

            // Wave
            double channel2 = 0;
            {
                if ((_soundOnOff & 0x04) != 0 && _waveDACpower)
                {
                    _modeThreeFreqCounter++;
                    if (_modeThreeFreqCounter >= _modeThreeFreqTime)
                    {
                        _modeThreeFreqCounter -= _modeThreeFreqTime;
                        _waveIndex = (_waveIndex + 1) % 32;

                        _waveThree = _waveVolumeCode switch
                        {
                            0 => 0,
                            1 => _waveNibbles[_waveIndex],           // 100% volume, no shift
                            2 => (short)(_waveNibbles[_waveIndex] >> 1), // 50%
                            3 => (short)(_waveNibbles[_waveIndex] >> 2), // 25%
                            _ => 0
                        };
                    }
                }
                channel2 = _waveVolumeCode == 0 ? 0 : _waveThree;
            }

            // Noise Channel
            double channel3 = 0;
            {
                channel3 = 0;
                if ((_soundOnOff & 0x08) != 0x00)
                {
                    channel3 = (double)((~_noiseLfsr & 0x01) * _noiseVolume);

                    var s = _noiseClockShift;
                    var r = _noiseDivisorCode == 0 ? 0.5 : _noiseDivisorCode;
                    var divisor = (262144 / r / Math.Pow(2, s));
                    var freq = 4194304 / divisor;

                    var stepSize = 4194304 / OutputRate;
                    _noiseTimeSteps += stepSize;

                    var stepCount = (int)(_noiseTimeSteps / freq);
                    if (stepCount > 1)
                        channel3 /= (double)stepCount;

                    while (_noiseTimeSteps >= freq)
                    {
                        _noiseTimeSteps -= freq;

                        var lsb = (short)((_noiseLfsr & 0x01) ^ ((_noiseLfsr >> 1) & 0x01));

                        _noiseLfsr >>= 1;

                        // Set bit 14 to the new XOR bit
                        _noiseLfsr = (short)((_noiseLfsr & ~(1 << 14)) | (lsb << 14));

                        // In 7-bit mode, bit 6 must also be replaced with that same XOR bit
                        if (_noiseWidthMode)
                            _noiseLfsr = (short)((_noiseLfsr & ~(1 << 6)) | (lsb << 6));

                        if (_noiseTimeSteps > freq)
                            channel3 += (double)((~_noiseLfsr & 0x01) * _noiseVolume) / (double)stepCount;
                    }
                }
            }
            double ch1 = channel0 / 15.0;
            double ch2 = channel1 / 15.0;
            double ch3 = channel2 / 15.0;
            double ch4 = channel3 / 15.0;

            // NR51: route channels to left / right
            double left = 0.0;
            double right = 0.0;

            if ((_channelLeftRightControl & 0x10) != 0) left += ch1;
            if ((_channelLeftRightControl & 0x20) != 0) left += ch2;
            if ((_channelLeftRightControl & 0x40) != 0) left += ch3;
            if ((_channelLeftRightControl & 0x80) != 0) left += ch4;

            if ((_channelLeftRightControl & 0x01) != 0) right += ch1;
            if ((_channelLeftRightControl & 0x02) != 0) right += ch2;
            if ((_channelLeftRightControl & 0x04) != 0) right += ch3;
            if ((_channelLeftRightControl & 0x08) != 0) right += ch4;

            // Average active channels per side so one side doesn't get louder
            int leftCount = 0;
            int rightCount = 0;

            if ((_channelLeftRightControl & 0x10) != 0) leftCount++;
            if ((_channelLeftRightControl & 0x20) != 0) leftCount++;
            if ((_channelLeftRightControl & 0x40) != 0) leftCount++;
            if ((_channelLeftRightControl & 0x80) != 0) leftCount++;

            if ((_channelLeftRightControl & 0x01) != 0) rightCount++;
            if ((_channelLeftRightControl & 0x02) != 0) rightCount++;
            if ((_channelLeftRightControl & 0x04) != 0) rightCount++;
            if ((_channelLeftRightControl & 0x08) != 0) rightCount++;

            if (leftCount > 0)
                left /= leftCount;
            if (rightCount > 0)
                right /= rightCount;

            // NR50: per-side master volume
            double leftVolume = (((_soundOutputLevel >> 4) & 0x07) + 1) / 8.0;
            double rightVolume = ((_soundOutputLevel & 0x07) + 1) / 8.0;

            left *= leftVolume;
            right *= rightVolume;

            // Mono output: average left and right
            double mixerOutput = (left + right) * 0.5;

            if ((_soundOnOff & 0x80) != 0x80)
                mixerOutput = 0;

            bool anyDacEnabled = (_soundOnOff & 0x80) != 0 && _channelLeftRightControl != 0;

            var output = HighPass(mixerOutput, anyDacEnabled);
            var byteOutput = (short)(output * short.MaxValue);

            _soundBuffer[_bufferIndex++] = (byte)(byteOutput & 0xFF);
            _soundBuffer[_bufferIndex++] = (byte)(byteOutput >> 8);

            // this can happen when _bufferIndex get reset while in the process of setting the _soundBuffer
            if (_bufferIndex % 2 != 0)
                _bufferIndex = 0;

            _bufferCount++;
            if (_endBufferCount > 0 && _bufferCount > _endBufferCount)
                WasStopped = true;

            if (_bufferIndex >= _soundBuffer.Length || (WasStopped && _bufferIndex > 0))
                AddCurrentBuffer();
        }

        private double HighPass(double input, bool dacsEnabled)
        {
            if (!dacsEnabled)
                return 0.0;

            var output = input - _highPassFilterCapacitor;

            _highPassFilterCapacitor = input - output * 0.996013308910; // for 44100hz

            return output;
        }
    }
}
