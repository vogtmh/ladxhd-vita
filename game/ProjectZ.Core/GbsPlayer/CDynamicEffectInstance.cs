using System;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Xna.Framework.Audio;

namespace GBSPlayer
{
    public sealed class CDynamicEffectInstance : IDisposable
    {
        private readonly DynamicSoundEffectInstance _instance;

        private readonly ConcurrentQueue<PcmChunk> _pcmQueue = new();

        private volatile SoundState _cachedState;
        private volatile int _cachedPending;
        private int _queuedChunkCount;

        // command flags (cross-thread)
        private int _cmdPlay;
        private int _cmdPause;
        private int _cmdResume;
        private int _cmdStop;
        private int _cmdDispose;

        // latest volume requested (nullable pattern without locks)
        private int _hasVolume;
        private float _volume;

        private int _isDisposed;

        public SoundState State => _cachedState;

        public CDynamicEffectInstance(int sampleRate)
        {
            _instance = new DynamicSoundEffectInstance(sampleRate, AudioChannels.Mono);
            _cachedPending = 0;
            _cachedState = SoundState.Stopped;
        }

        public int GetPendingBufferCount() => _cachedPending;

        public void Play()
        {
            Volatile.Write(ref _cmdPlay, 1);
        }

        public void Pause()
        {
            Volatile.Write(ref _cmdPause, 1);
        }

        public void Resume()
        {
            Volatile.Write(ref _cmdResume, 1);
        }

        public void Stop()
        {
            Volatile.Write(ref _cmdStop, 1);
        }

        public void SetVolume(float volume)
        {
            _volume = volume;
            Volatile.Write(ref _hasVolume, 1);
        }

        public void SubmitBuffer(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (count <= 0) return; // never submit empty
            if ((uint)offset > (uint)buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (offset + count > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));

            if (Volatile.Read(ref _isDisposed) == 1)
                return;

            _pcmQueue.Enqueue(new PcmChunk(buffer, offset, count));
            Interlocked.Increment(ref _queuedChunkCount);
        }

        public void Dispose()
        {
            // allow Dispose from any thread; actual disposal happens in Pump()
            Volatile.Write(ref _cmdDispose, 1);
        }

        public void Pump(int maxBuffersPerFrame = 6, int maxPending = 12)
        {
            if (Volatile.Read(ref _isDisposed) == 1)
                return;

            // Dispose request wins
            if (Interlocked.Exchange(ref _cmdDispose, 0) == 1)
            {
                try { _instance.Stop(); } catch { }
                DrainPcmQueue();
                _instance.Dispose();
                Volatile.Write(ref _isDisposed, 1);
                _cachedPending = 0;
                _cachedState = SoundState.Stopped;
                return;
            }

            // Stop (clears queued buffers in MonoGame/OpenAL)
            if (Interlocked.Exchange(ref _cmdStop, 0) == 1)
            {
                _instance.Stop();
                DrainPcmQueue();
                _cachedPending = 0;
                _cachedState = SoundState.Stopped;
            }

            // Pause
            if (Interlocked.Exchange(ref _cmdPause, 0) == 1)
            {
                _instance.Pause();
            }

            // Resume
            if (Interlocked.Exchange(ref _cmdResume, 0) == 1)
            {
                if (_instance.State == SoundState.Paused)
                    _instance.Resume();
            }

            // Volume
            if (Interlocked.Exchange(ref _hasVolume, 0) == 1)
            {
                _instance.Volume = _volume;
            }

            Interlocked.Exchange(ref _cmdPlay, 0);

            // Submit PCM buffers first
            int submitted = 0;
            while (submitted < maxBuffersPerFrame &&
                   _instance.PendingBufferCount < maxPending &&
                   _pcmQueue.TryDequeue(out var chunk))
            {
                Interlocked.Decrement(ref _queuedChunkCount);
                _instance.SubmitBuffer(chunk.Buffer, chunk.Offset, chunk.Count);
                submitted++;
            }

            if (_instance.State != SoundState.Playing && _instance.PendingBufferCount > 0)
                _instance.Play();

            _cachedPending = _instance.PendingBufferCount;
            _cachedState   = _instance.State;
        }

        private void DrainPcmQueue()
        {
            while (_pcmQueue.TryDequeue(out _))
            {
                if (Interlocked.Decrement(ref _queuedChunkCount) < 0)
                    Volatile.Write(ref _queuedChunkCount, 0);
            }
            Volatile.Write(ref _queuedChunkCount, 0);
        }

        public int GetBufferedChunkCount()
        {
            return Volatile.Read(ref _queuedChunkCount) + _cachedPending;
        }

        public void ClearQueuedPcm()
        {
            DrainPcmQueue();
        }

        private readonly struct PcmChunk
        {
            public readonly byte[] Buffer;
            public readonly int Offset;
            public readonly int Count;

            public PcmChunk(byte[] buffer, int offset, int count)
            {
                Buffer = buffer;
                Offset = offset;
                Count = count;
            }
        }
    }
}
