using System;
using System.IO;
using System.Threading;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Audio
{
    public class MusicPlayer
    {
        private DynamicSoundEffectInstance _instance;
        private CancellationTokenSource _cts;
        private Thread _streamThread;

        private int _currentTrack = -1;
        private string _currentPath;
        private float _volume = 1f;
        private float _volumeMultiplier = 1f;
        private float _playbackSpeed = 1f;
        private float _gain = 1f;
        private readonly object _lock = new object();

        private double _stopTime = 0;
        private bool _wasStopped = false;

        public bool WasStopped => _wasStopped;
        public bool IsPlaying => _instance?.State == SoundState.Playing;
        public int CurrentTrack => _currentTrack;

        public void SetGain(float gain)
        {
            _gain = Math.Max(0f, gain);
        }

        public void Play(string path, int trackId)
        {
            if (_currentPath == path && IsPlaying) return;

            Stop();

            _currentPath = path;
            _currentTrack = trackId;
            _cts = new CancellationTokenSource();
            _streamThread = new Thread(() => StreamLoop(path, _cts.Token))
            {
                IsBackground = true,
                Name = "MusicStream"
            };
            _streamThread.Start();
        }

        public void Stop()
        {
            _cts?.Cancel();
            _streamThread?.Join(500);
            _streamThread = null;

            lock (_lock)
            {
                _instance?.Stop();
                _instance?.Dispose();
                _instance = null;
            }
            _currentPath = null;
            _currentTrack = -1;
            _wasStopped = false;
            _stopTime = 0;
        }

        public void SetStopTime(float seconds)
        {
            _stopTime = seconds;
            _wasStopped = false;
        }

        public void Pause()
        {
            lock (_lock)
            {
                if (_instance?.State == SoundState.Playing)
                    _instance.Pause();
            }
        }

        public void Resume()
        {
            lock (_lock)
            {
                if (_instance?.State == SoundState.Paused)
                    _instance.Resume();
            }
        }

        public void SetVolume(float volume)
        {
            _volume = volume;
            ApplyVolume();
        }

        public void SetVolumeMultiplier(float mult)
        {
            _volumeMultiplier = mult;
            ApplyVolume();
        }

        public float GetVolumeMultiplier()
        {
            return _volumeMultiplier;
        }

        public void SetPlaybackSpeed(float speed)
        {
            if (speed <= 0f) speed = 1f;
            _playbackSpeed = speed;
        }

        private void ApplyVolume()
        {
            lock (_lock)
            {
                if (_instance != null)
                    _instance.Volume = MathHelper.Clamp(_volume * _volumeMultiplier, 0f, 1f);
            }
        }

        private static float GetGain(NVorbis.VorbisReader vorbis)
        {
            // Try to pull the volume from OGG tags.
            var tags = vorbis.Tags;

            if (tags != null)
            {
                var volumeTag = tags.GetTagSingle("VOLUME");
                if (volumeTag != null && float.TryParse(volumeTag.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float volume))
                    return volume / 100.0f;
            }
            // Search for a global "volume.txt" file to set the volume of all songs.
            var gainFile = Path.Combine(Values.PathMusicMods, "volume.txt");
            if (GameFS.Exists(gainFile) && float.TryParse(GameFS.ReadAllText(gainFile).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float globalGain))
                return globalGain / 100.0f;

            return 1.0f;
        }

        private void StreamLoop(string path, CancellationToken ct)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".ogg")
                StreamLoopOgg(path, ct);
        }

        private struct LoopPoints
        {
            public long StartSample;
            public long EndSample;
        }

        private static LoopPoints GetLoopPoints(NVorbis.VorbisReader vorbis, string audioPath)
        {
             // Use -1 as default to play to end.
            long start = 0;
            long end = -1;

            // Try OGG metadata first
            var tags = vorbis.Tags;
            if (tags != null)
            {
                if (long.TryParse(tags.GetTagSingle("LOOPSTART"), out long startSample))
                    start = startSample;

                if (long.TryParse(tags.GetTagSingle("LOOPLENGTH"), out long length))
                    end = start + length;
                else if (long.TryParse(tags.GetTagSingle("LOOPEND"), out long endSample))
                    end = endSample;

                if (start > 0)
                    return new LoopPoints { StartSample = start, EndSample = end };
            }

            // Fall back to .loop file
            var loopFile = Path.ChangeExtension(audioPath, ".loop");
            if (GameFS.Exists(loopFile))
            {
                var lines = GameFS.ReadAllLines(loopFile);
                if (lines.Length >= 1 && double.TryParse(lines[0].Trim(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double startSeconds))
                    start = (long)(startSeconds * vorbis.SampleRate);

                if (lines.Length >= 2 && double.TryParse(lines[1].Trim(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double endSeconds))
                    end = (long)(endSeconds * vorbis.SampleRate);
            }

            return new LoopPoints { StartSample = start, EndSample = end };
        }

        private void StreamLoopOgg(string path, CancellationToken ct)
        {
            try
            {
                using var fs = new MemoryStream(GameFS.ReadAllBytes(path));
                using var vorbis = new NVorbis.VorbisReader(fs, closeOnDispose: false);

                int sampleRate = vorbis.SampleRate;
                int channels = vorbis.Channels;
                var audioChannels = channels == 1 ? AudioChannels.Mono : AudioChannels.Stereo;

                var loop = GetLoopPoints(vorbis, path);
                var gain = GetGain(vorbis) * _gain;

                lock (_lock)
                {
                    _instance?.Dispose();
                    _instance = new DynamicSoundEffectInstance((int)(sampleRate * _playbackSpeed), audioChannels);
                    _instance.Volume = MathHelper.Clamp(_volume * _volumeMultiplier, 0f, 1f);
                    _instance.Play();
                }
                const int samplesPerChunk = 4096;
                var floatBuf = new float[samplesPerChunk * channels];
                var byteBuf = new byte[floatBuf.Length * 2];
                var elapsed = 0.0;

                while (!ct.IsCancellationRequested)
                {
                    while (!ct.IsCancellationRequested && _instance.PendingBufferCount < 3)
                    {
                        // Check stop time
                        if (_stopTime > 0 && elapsed >= _stopTime)
                        {
                            _wasStopped = true;
                            _cts.Cancel();
                            return;
                        }
                        // If we have a loop end point, check if we're about to pass it
                        int samplesToRead = floatBuf.Length;
                        if (loop.EndSample >= 0)
                        {
                            long samplesRemaining = loop.EndSample - vorbis.SamplePosition;
                            if (samplesRemaining <= 0)
                            {
                                vorbis.SeekTo(loop.StartSample);
                                break;
                            }
                            samplesToRead = (int)Math.Min(samplesToRead, samplesRemaining * channels);
                        }

                        int read = vorbis.ReadSamples(floatBuf, 0, samplesToRead);
                        if (read == 0)
                        {
                            vorbis.SeekTo(loop.StartSample);
                            break;
                        }

                        for (int i = 0; i < read; i++)
                        {
                            short s = (short)MathHelper.Clamp(floatBuf[i] * gain * 32767f, short.MinValue, short.MaxValue);
                            byteBuf[i * 2]     = (byte)(s & 0xFF);
                            byteBuf[i * 2 + 1] = (byte)(s >> 8);
                        }

                        lock (_lock)
                            _instance?.SubmitBuffer(byteBuf, 0, read * 2);

                        elapsed += (double)read / channels / sampleRate;
                    }
                    Thread.Sleep(10);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MusicPlayer OGG stream error: {ex.Message}");
            }
        }
    }
}
