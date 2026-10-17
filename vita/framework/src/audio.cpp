// Software mixer for SoundEffect / SoundEffectInstance / DynamicSoundEffectInstance.
#include "fw/all.h"
#include "platform.h"
#include <atomic>
#include <cmath>
#include <cstring>
#include <deque>
#include <mutex>
#include <vector>

using namespace ::System;

namespace fw::audio {

constexpr int kRate = 48000;
constexpr int kVoices = 48;

struct Stream {
    int rate = 44100, channels = 1;
    std::deque<std::vector<int16_t>> queue;
    size_t pos = 0;  // frame position in front buffer (fixed 16.16 via fpos)
    double fpos = 0;
    // After running dry, wait for 50 ms of data: playing each chunk the moment it arrives sounds like crackling.
    bool starved = true;
};

std::atomic<int> underruns{0};

struct Voice {
    std::atomic<bool> active{false};
    bool paused = false;
    bool looped = false;
    const int16_t* samples = nullptr;
    int frames = 0, channels = 1, rate = 44100;
    double pos = 0;
    float volume = 1, pitch = 0, pan = 0;
    Stream* stream = nullptr;
    uint32_t generation = 0;
};

static Voice voices[kVoices];
static float masterVolume = 1.0f;
static bool started = false;

static void mix(int16_t* out, int frames) {
    static std::vector<float> acc;
    acc.assign((size_t)frames * 2, 0.0f);
    for (auto& v : voices) {
        if (!v.active || v.paused) continue;
        float vol = v.volume * masterVolume;
        float lg = vol * (v.pan <= 0 ? 1.0f : 1.0f - v.pan), rg = vol * (v.pan >= 0 ? 1.0f : 1.0f + v.pan);
        if (v.stream) {
            Stream* s = v.stream;
            if (s->starved) {
                size_t queued = 0;
                for (auto& b : s->queue) queued += b.size() / s->channels;
                if (queued < (size_t)s->rate / 20) continue;
                s->starved = false;
            }
            double step = (double)s->rate / kRate;
            for (int i = 0; i < frames; i++) {
                if (s->queue.empty()) {
                    s->starved = true;
                    underruns++;
                    break;
                }
                auto& buf = s->queue.front();
                size_t nFrames = buf.size() / s->channels;
                size_t idx = (size_t)s->fpos;
                if (idx >= nFrames) {
                    s->fpos -= nFrames;
                    s->queue.pop_front();
                    i--;
                    continue;
                }
                float l = buf[idx * s->channels] / 32768.0f;
                float r = s->channels > 1 ? buf[idx * s->channels + 1] / 32768.0f : l;
                acc[i * 2] += l * lg;
                acc[i * 2 + 1] += r * rg;
                s->fpos += step;
            }
            continue;
        }
        double step = (double)v.rate / kRate * std::pow(2.0, v.pitch);
        for (int i = 0; i < frames; i++) {
            int idx = (int)v.pos;
            if (idx >= v.frames) {
                if (v.looped && v.frames > 0) {
                    v.pos -= v.frames;
                    idx = (int)v.pos;
                } else {
                    v.active = false;
                    break;
                }
            }
            float l = v.samples[idx * v.channels] / 32768.0f;
            float r = v.channels > 1 ? v.samples[idx * v.channels + 1] / 32768.0f : l;
            acc[i * 2] += l * lg;
            acc[i * 2 + 1] += r * rg;
            v.pos += step;
        }
    }
    for (size_t i = 0; i < acc.size(); i++) {
        float x = acc[i];
        x = x > 1 ? 1 : (x < -1 ? -1 : x);
        out[i] = (int16_t)(x * 32767.0f);
    }
}

static void ensureStarted() {
    if (started) return;
    started = true;
    plat::audioInit(kRate, mix);
}

int allocVoice() {
    ensureStarted();
    for (int i = 0; i < kVoices; i++)
        if (!voices[i].active) return i;
    return -1;
}

}  // namespace fw::audio

namespace Microsoft::Xna::Framework::Audio {
using namespace fw::audio;
namespace plat = fw::plat;

float SoundEffect::get_MasterVolume() { return masterVolume; }
void SoundEffect::set_MasterVolume(float v) { masterVolume = v; }

SoundEffectInstance* SoundEffect::CreateInstance() {
    auto* i = new SoundEffectInstance();
    i->effect = this;
    return i;
}
bool SoundEffect::Play() { return Play(1, 0, 0); }
bool SoundEffect::Play(float volume, float pitch, float pan) {
    if (!samples || frames == 0) return false;
    plat::audioLock();
    int id = allocVoice();
    if (id >= 0) {
        Voice& v = voices[id];
        v.samples = samples;
        v.frames = frames;
        v.channels = channels;
        v.rate = sampleRate;
        v.pos = 0;
        v.volume = volume;
        v.pitch = pitch;
        v.pan = pan;
        v.looped = false;
        v.paused = false;
        v.stream = nullptr;
        v.active = true;
    }
    plat::audioUnlock();
    return id >= 0;
}

static bool ownsVoice(SoundEffectInstance* s) { return s->voice >= 0 && voices[s->voice].active && voices[s->voice].generation == (uint32_t)(uintptr_t)s; }

SoundState SoundEffectInstance::get_State() {
    if (state != SoundState::Stopped && !ownsVoice(this)) state = SoundState::Stopped;
    return state;
}
void SoundEffectInstance::set_Volume(float v) {
    volume = v;
    if (ownsVoice(this)) voices[voice].volume = v;
}
void SoundEffectInstance::set_Pitch(float v) {
    pitch = v;
    if (ownsVoice(this)) voices[voice].pitch = v;
}
void SoundEffectInstance::set_Pan(float v) {
    pan = v;
    if (ownsVoice(this)) voices[voice].pan = v;
}
void SoundEffectInstance::Play() {
    if (get_State() == SoundState::Playing) return;
    if (state == SoundState::Paused) { Resume(); return; }
    if (!effect || !effect->samples) return;
    plat::audioLock();
    int id = allocVoice();
    if (id >= 0) {
        Voice& v = voices[id];
        v.samples = effect->samples;
        v.frames = effect->frames;
        v.channels = effect->channels;
        v.rate = effect->sampleRate;
        v.pos = 0;
        v.volume = volume;
        v.pitch = pitch;
        v.pan = pan;
        v.looped = looped;
        v.paused = false;
        v.stream = nullptr;
        v.generation = (uint32_t)(uintptr_t)this;
        v.active = true;
        voice = id;
        state = SoundState::Playing;
    }
    plat::audioUnlock();
}
void SoundEffectInstance::Pause() {
    if (ownsVoice(this)) voices[voice].paused = true;
    if (state == SoundState::Playing) state = SoundState::Paused;
}
void SoundEffectInstance::Resume() {
    if (ownsVoice(this)) voices[voice].paused = false;
    if (state == SoundState::Paused) state = SoundState::Playing;
}
void SoundEffectInstance::Stop() {
    plat::audioLock();
    if (ownsVoice(this)) voices[voice].active = false;
    plat::audioUnlock();
    state = SoundState::Stopped;
}
void SoundEffectInstance::Dispose() { Stop(); }

// ---------------------------------------------------------------- dynamic
void DynamicSoundEffectInstance::__init(int32_t sampleRate, AudioChannels ch) {
    rate = sampleRate;
    chans = (int32_t)ch;
    auto* s = new Stream();
    s->rate = rate;
    s->channels = chans;
    stream = s;
}
int32_t DynamicSoundEffectInstance::get_PendingBufferCount() {
    plat::audioLock();
    int32_t n = (int32_t)((Stream*)stream)->queue.size();
    plat::audioUnlock();
    return n;
}
void DynamicSoundEffectInstance::SubmitBuffer(Array1<uint8_t>* b) { SubmitBuffer(b, 0, b->length); }
void DynamicSoundEffectInstance::SubmitBuffer(Array1<uint8_t>* b, int32_t offset, int32_t count) {
    std::vector<int16_t> v(count / 2);
    std::memcpy(v.data(), b->data + offset, (size_t)(count / 2) * 2);
    plat::audioLock();
    ((Stream*)stream)->queue.push_back(std::move(v));
    plat::audioUnlock();
}
void DynamicSoundEffectInstance::set_Volume(float v) {
    volume = v;
    if (voice >= 0 && voices[voice].stream == stream) voices[voice].volume = v;
}
void DynamicSoundEffectInstance::Play() {
    if (state == SoundState::Playing) return;
    if (state == SoundState::Paused) { Resume(); return; }
    plat::audioLock();
    int id = allocVoice();
    if (id >= 0) {
        Voice& v = voices[id];
        v.stream = (Stream*)stream;
        v.volume = volume;
        v.pan = 0;
        v.paused = false;
        v.generation = (uint32_t)(uintptr_t)this;
        v.active = true;
        voice = id;
        state = SoundState::Playing;
    }
    plat::audioUnlock();
}
void DynamicSoundEffectInstance::Pause() {
    if (voice >= 0 && voices[voice].stream == stream) voices[voice].paused = true;
    if (state == SoundState::Playing) state = SoundState::Paused;
}
void DynamicSoundEffectInstance::Resume() {
    if (voice >= 0 && voices[voice].stream == stream) voices[voice].paused = false;
    if (state == SoundState::Paused) state = SoundState::Playing;
}
void DynamicSoundEffectInstance::Stop() {
    plat::audioLock();
    if (voice >= 0 && voices[voice].stream == stream) voices[voice].active = false;
    ((Stream*)stream)->queue.clear();
    ((Stream*)stream)->fpos = 0;
    ((Stream*)stream)->starved = true;
    plat::audioUnlock();
    voice = -1;
    state = SoundState::Stopped;
}

}  // namespace Microsoft::Xna::Framework::Audio
