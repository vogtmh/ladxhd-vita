#pragma once
#include "all.h"

namespace Microsoft::Xna::Framework::Audio {
using namespace ::Microsoft::Xna::Framework;

enum class SoundState : int32_t { Playing, Paused, Stopped };
enum class AudioChannels : int32_t { Mono = 1, Stereo = 2 };
inline ::System::String* __EnumToString(SoundState v) { return ::System::ToStr((int32_t)v); }
inline ::System::String* __EnumToString(AudioChannels v) { return ::System::ToStr((int32_t)v); }

struct SoundEffectInstance;
struct SoundEffect : Object, ::System::IDisposable {
    int16_t* samples = nullptr;  // interleaved PCM16 (malloc'd, freed never; effects live for the whole game)
    int32_t frames = 0;
    int32_t channels = 1;
    int32_t sampleRate = 44100;
    static SoundEffect* FromStream(::System::IO::Stream* s);
    SoundEffectInstance* CreateInstance();
    bool Play();
    bool Play(float volume, float pitch, float pan);
    ::System::TimeSpan get_Duration() { return ::System::TimeSpan::FromSeconds((double)frames / sampleRate); }
    void Dispose() override {}
    static float get_MasterVolume();
    static void set_MasterVolume(float v);
};

struct SoundEffectInstance : Object, ::System::IDisposable {
    SoundEffect* effect = nullptr;
    int32_t voice = -1;  // mixer voice id
    bool looped = false;
    float volume = 1, pitch = 0, pan = 0;
    SoundState state = SoundState::Stopped;
    virtual SoundState get_State();
    bool get_IsLooped() { return looped; }
    void set_IsLooped(bool v) { looped = v; }
    float get_Volume() { return volume; }
    virtual void set_Volume(float v);
    float get_Pitch() { return pitch; }
    void set_Pitch(float v);
    float get_Pan() { return pan; }
    void set_Pan(float v);
    virtual void Play();
    virtual void Pause();
    virtual void Resume();
    virtual void Stop();
    void Stop(bool immediate) { Stop(); }
    void Dispose() override;
    bool get_IsDisposed() { return false; }
};

struct DynamicSoundEffectInstance : SoundEffectInstance {
    int32_t rate = 44100;
    int32_t chans = 1;
    void* stream = nullptr;  // backend stream
    void __init(int32_t sampleRate, AudioChannels channels);
    int32_t get_PendingBufferCount();
    void SubmitBuffer(::System::Array1<uint8_t>* buffer);
    void SubmitBuffer(::System::Array1<uint8_t>* buffer, int32_t offset, int32_t count);
    SoundState get_State() override { return state; }
    void set_Volume(float v) override;
    void Play() override;
    void Pause() override;
    void Resume() override;
    void Stop() override;
    ::System::Fn<void(Object*, ::System::EventArgs*)>* bufferNeeded = nullptr;
    void add_BufferNeeded(::System::Fn<void(Object*, ::System::EventArgs*)>* h) { bufferNeeded = ::System::Delegate::Combine(bufferNeeded, h); }
    void remove_BufferNeeded(::System::Fn<void(Object*, ::System::EventArgs*)>* h) { bufferNeeded = ::System::Delegate::Remove(bufferNeeded, h); }
};

}  // namespace Microsoft::Xna::Framework::Audio
