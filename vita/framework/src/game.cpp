// Game loop, GraphicsDeviceManager, input.
#include "fw/all.h"
#include "gfx_internal.h"
#include "platform.h"
#include <atomic>
extern "C" size_t GC_get_heap_size(void);
extern "C" size_t GC_get_free_bytes(void);
extern "C" size_t GC_get_gc_no(void);
extern "C" size_t GC_get_total_bytes(void);
extern "C" unsigned long GC_get_full_gc_total_time(void);
#include <cstdio>
#include <cstring>
#include <string>
#include <strings.h>

using namespace ::System;
using namespace ::Microsoft::Xna::Framework;
using namespace ::Microsoft::Xna::Framework::Graphics;

namespace Microsoft::Xna::Framework::Graphics {
void fwSetBackBuffer(GraphicsDevice* gd, int w, int h);
}
#define STB_IMAGE_WRITE_IMPLEMENTATION
#include "stb/stb_image_write.h"
#include <cstdlib>
#include <vector>
namespace fw {
// Debug automation: LADX_SHOTS="frame,frame,..." writes PNGs to LADX_SHOT_DIR;
// LADX_INPUT="frame:key:frames,..." holds XNA key codes for scripted runs; LADX_QUIT=frame exits.
static std::vector<long> shotFrames;
struct ScriptKey { long frame, key, len; };
static std::vector<ScriptKey> scriptKeys;
static long quitFrame = -1;
static bool memStat = false;
static bool perfStat = false;
struct Stats {
    double start = 0, update = 0, hook = 0, draw = 0, swap = 0;
    int frames = 0, updates = 0;
    size_t gcStart = 0;
};
static Stats stats;
static void reportStats();
static double processStart = 0;
static const double bootStart = plat::timeSeconds();
// Logs single updates/draws above 25 ms (LADX_HITCH_MS) with what they spent time on (perf mode only).
struct HitchProbe {
    double t0 = 0, reads = 0, assets = 0, decode = 0;
    size_t gcs = 0, allocated = 0;
    unsigned long gcMs = 0;
    void start() {
        t0 = plat::timeSeconds();
        reads = loadStats.readSeconds;
        assets = loadStats.assetSeconds;
        decode = gfx::gpuStats.uploadSeconds;
        gcs = GC_get_gc_no();
        gcMs = GC_get_full_gc_total_time();
        allocated = GC_get_total_bytes();
    }
    void end(const char* what, long frame) {
        double ms = (plat::timeSeconds() - t0) * 1000.0;
        static const double limit = [] { const char* s = std::getenv("LADX_HITCH_MS"); return s ? std::atof(s) : 25.0; }();
        if (ms < limit) return;
        plat::logf("hitch %ld: %s %.0f ms (file reads %.0f, content %.0f, image decode %.0f, gc %u = %lu ms, allocated %u KB)\n", frame,
                   what, ms, (loadStats.readSeconds - reads) * 1000.0, (loadStats.assetSeconds - assets) * 1000.0,
                   (gfx::gpuStats.uploadSeconds - decode) * 1000.0, (unsigned)(GC_get_gc_no() - gcs),
                   GC_get_full_gc_total_time() - gcMs, (unsigned)((GC_get_total_bytes() - allocated) / 1024));
    }
};
namespace overlay {
extern bool enabled;
void setLines(std::vector<std::string> l);
void draw(int w, int h);
}  // namespace overlay
namespace audio {
extern std::atomic<int> underruns;
}  // namespace audio
long frameCounter = 0;
static void initAutomation() {
    if (const char* s = std::getenv("LADX_SHOTS"))
        for (const char* p = s; *p;) {
            shotFrames.push_back(std::strtol(p, (char**)&p, 10));
            if (*p == ',') p++;
            else break;
        }
    if (const char* s = std::getenv("LADX_INPUT"))
        for (const char* p = s; *p;) {
            ScriptKey k{};
            k.frame = std::strtol(p, (char**)&p, 10);
            if (*p == ':') p++;
            k.key = std::strtol(p, (char**)&p, 10);
            k.len = 6;
            if (*p == ':') { p++; k.len = std::strtol(p, (char**)&p, 10); }
            scriptKeys.push_back(k);
            if (*p == ',') p++;
            else break;
        }
    if (const char* s = std::getenv("LADX_QUIT")) quitFrame = std::atol(s);
    memStat = std::getenv("LADX_MEMSTAT") != nullptr;
#if defined(FW_VITA)
    perfStat = true;
#else
    perfStat = std::getenv("LADX_PERF") != nullptr;
#endif
    overlay::enabled = perfStat;
    stats.start = plat::timeSeconds();
}
// Select+L+R on the pad or F3 on the keyboard toggles the overlay; Select+Triangle (F4) cycles gfx debug modes.
static void pollOverlayToggle() {
    static bool was = false, wasMode = false;
    uint32_t keys[8];
    plat::keyboard(keys);
    auto pad = plat::gamepad(0);
    const uint32_t combo = 32 | 256 | 512, modeCombo = 32 | 32768;
    bool now = (pad.connected && (pad.buttons & combo) == combo) || ((keys[114 >> 5] >> (114 & 31)) & 1);
    if (now && !was) overlay::enabled = !overlay::enabled;
    was = now;
    bool mode = (pad.connected && (pad.buttons & modeCombo) == modeCombo) || ((keys[115 >> 5] >> (115 & 31)) & 1);
    if (mode && !wasMode) gfx::debugMode = (gfx::debugMode + 1) % 4;
    wasMode = mode;
}
void reportStats() {
    double now = plat::timeSeconds();
    double ms = 1000.0 / stats.frames;
    double fps = stats.frames / (now - stats.start);
    int gcs = (int)(GC_get_gc_no() - stats.gcStart);
    auto& g = gfx::gpuStats;
    static const char* modeNames[] = {"NORMAL", "NO GL DRAWS", "NO RT BINDS", "GPU SCENES"};
    char l1[96], l2[96], l3[96], l4[96];
    std::snprintf(l1, sizeof l1, "FPS %.1f  FRAME %.1fMS  UPD/FRAME %.1f", fps, 1000.0 / fps, (double)stats.updates / stats.frames);
    std::snprintf(l2, sizeof l2, "UPDATE %.1f  MUSIC %.1f  DRAW %.1f  SWAP %.1f", stats.update * ms, stats.hook * ms,
                  stats.draw * ms, stats.swap * ms);
    std::snprintf(l3, sizeof l3, "GL %.1f  BIND %.1f  UPLOAD %.1f %d/%uKB", g.glSeconds * ms, g.bindSeconds * ms,
                  g.uploadSeconds * ms, g.uploads / stats.frames, (unsigned)(g.uploadBytes / stats.frames / 1024));
    std::snprintf(l4, sizeof l4, "GC %d  HEAP %uMB  DRAWS %d  RT %d  MODE %s", gcs, (unsigned)(GC_get_heap_size() >> 20),
                  gfx::drawCalls / stats.frames, gfx::targetSwitches / stats.frames, modeNames[gfx::debugMode]);
    overlay::setLines({l1, l2, l3, l4});
    plat::logf("perf: %s | %s | %s | %s\n", l1, l2, l3, l4);
    if (int n = audio::underruns.exchange(0)) plat::logf("audio: music ran dry %d times\n", n);
    stats = Stats{};
    stats.start = now;
    stats.gcStart = GC_get_gc_no();
    gfx::drawCalls = gfx::targetSwitches = 0;
    g = gfx::GpuStats{};
}
void scriptedKeys(uint32_t keys[8]) {
    for (auto& k : scriptKeys)
        if (frameCounter >= k.frame && frameCounter < k.frame + k.len) keys[k.key >> 5] |= 1u << (k.key & 31);
}
static void maybeShot(int w, int h) {
    for (long f : shotFrames)
        if (f == frameCounter) {
            std::vector<uint8_t> flip((size_t)w * h * 4);
            if (!fw::gfx::beReadBackBuffer(w, h, flip.data())) return;
            const char* dir = std::getenv("LADX_SHOT_DIR");
            char path[512];
            std::snprintf(path, sizeof path, "%s/shot_%05ld.png", dir ? dir : "/tmp", frameCounter);
            stbi_write_png(path, w, h, 4, flip.data(), w * 4);
            fw::plat::logf("wrote %s\n", path);
        }
}
void (*frameHook)() = nullptr;
GraphicsDevice* g_gd = nullptr;
}  // namespace fw
namespace Microsoft::Xna::Framework::Content {
GraphicsDevice* fwDevice() { return fw::g_gd; }
}

extern "C" const char* rt_platform_folder(int folder) { return fw::plat::userRoot(); }
extern "C" const char* rt_base_directory() {
    static std::string s = std::string(fw::plat::contentRoot()) + "/";
    return s.c_str();
}
extern "C" void rt_log(const char* s) { fw::plat::log(s); }
extern "C" void rt_thread_start(void (*fn)(void*), void* arg, void** handle) { fw::plat::startThread(fn, arg, handle); }
extern "C" void rt_thread_join(void* handle) { fw::plat::joinThread(handle); }
extern "C" long fw_frame_counter() { return fw::frameCounter; }

namespace Microsoft::Xna::Framework {

void Game::__init() {
    content = New<Content::ContentManager>();
    components = New<GameComponentCollection>();
    services = New<GameServiceContainer>();
    window = New<GameWindow>();
}
Type* Game::__type() {
    static Type* t = Type::Create(u"Game", u"Microsoft.Xna.Framework.Game", Object::__type());
    return t;
}
Type* GameComponent::__type() {
    static Type* t = Type::Create(u"GameComponent", u"Microsoft.Xna.Framework.GameComponent", Object::__type());
    return t;
}
GraphicsDevice* Game::get_GraphicsDevice() { return graphicsManager ? graphicsManager->device : nullptr; }
void Game::Initialize() {
    for (int32_t i = 0; i < components->list->size; i++) components->list->items->data[i]->Initialize();
    LoadContent();
}
void Game::Update(GameTime* gt) {
    for (int32_t i = 0; i < components->list->size; i++)
        if (auto* c = dynamic_cast<GameComponent*>(components->list->items->data[i]))
            if (c->enabled) c->Update(gt);
}

void Game::Run() {
    fw::processStart = fw::plat::timeSeconds();
    // Fixed Vita resolution on every platform so desktop runs match the console.
    int w = 960, h = 544;
    if (!fw::plat::init(w, h, "Link's Awakening DX HD")) return;
    int dw, dh;
    fw::plat::drawableSize(dw, dh);
    auto* gd = New<GraphicsDevice>();
    fw::g_gd = gd;
    if (graphicsManager) graphicsManager->device = gd;
    fwSetBackBuffer(gd, dw, dh);
    window->bounds = Rectangle(0, 0, dw, dh);
    content->device = gd;
    fw::initAutomation();
    Initialize();
    auto* gt = New<GameTime>();
    double last = fw::plat::timeSeconds();
    double accumulator = 0;
    int64_t totalTicks = 0;
    while (!exitRequested) {
        bool active = isActive;
        if (!fw::plat::pollEvents(active)) break;
        if (active != isActive) {
            isActive = active;
            auto* h2 = active ? activated : deactivated;
            if (h2) h2->Invoke(this, EventArgs::Empty);
        }
        fw::plat::drawableSize(dw, dh);
        if (dw != gd->pp->backBufferWidth || dh != gd->pp->backBufferHeight) {
            fwSetBackBuffer(gd, dw, dh);
            window->bounds = Rectangle(0, 0, dw, dh);
        }
        double now = fw::plat::timeSeconds();
        double dt = fw::gfx::beFrameDelta(now - last);
        last = now;
        if (dt > 0.25) dt = 0.25;
        int64_t target = targetElapsed.ticks;
        if (fixedTimeStep) {
            accumulator += dt;
            double step = target / 1.0e7;
            int updates = 0;
            // Absorb vsync jitter so a 60 Hz display runs exactly one update per frame.
            if (accumulator < step * 0.9) {
                fw::plat::sleepMs(1);
                continue;
            }
            while (accumulator >= step * 0.9 && updates < 4) {
                accumulator -= step;
                if (accumulator < 0) accumulator = 0;
                totalTicks += target;
                gt->elapsed = TimeSpan(target);
                gt->total = TimeSpan(totalTicks);
                fw::HitchProbe probe;
                if (fw::perfStat) probe.start();
                fw::plat::heartbeat("update");
                double t0 = fw::plat::timeSeconds();
                Update(gt);
                double t1 = fw::plat::timeSeconds();
                if (fw::perfStat) probe.end("update", fw::frameCounter);
                fw::plat::heartbeat("music");
                if (fw::frameHook) fw::frameHook();
                fw::stats.update += t1 - t0;
                fw::stats.hook += fw::plat::timeSeconds() - t1;
                fw::stats.updates++;
                updates++;
            }
            gt->runningSlowly = updates > 1;
        } else {
            int64_t ticks = (int64_t)(dt * 1.0e7);
            totalTicks += ticks;
            gt->elapsed = TimeSpan(ticks);
            gt->total = TimeSpan(totalTicks);
            fw::HitchProbe probe;
            if (fw::perfStat) probe.start();
            fw::plat::heartbeat("update");
            double t0 = fw::plat::timeSeconds();
            Update(gt);
            double t1 = fw::plat::timeSeconds();
            if (fw::perfStat) probe.end("update", fw::frameCounter);
            fw::plat::heartbeat("music");
            if (fw::frameHook) fw::frameHook();
            fw::stats.update += t1 - t0;
            fw::stats.hook += fw::plat::timeSeconds() - t1;
            fw::stats.updates++;
        }
        if (exitRequested) break;
        double td = fw::plat::timeSeconds();
        fw::HitchProbe drawProbe;
        fw::plat::heartbeat("waiting for gpu (frame begin)");
        fw::gfx::beBeginFrame();
        fw::plat::heartbeat("draw");
        if (fw::perfStat) drawProbe.start();
        Draw(gt);
        fw::gfx::ensureTarget();
        if (fw::perfStat) drawProbe.end("draw", fw::frameCounter);
        fw::stats.draw += fw::plat::timeSeconds() - td;
        fw::pollOverlayToggle();
        fw::overlay::draw(dw, dh);
        fw::maybeShot(dw, dh);
        fw::frameCounter++;
        if (fw::memStat && fw::frameCounter % 100 == 0) {
            static size_t lastTotal = 0;
            size_t total = GC_get_total_bytes();
            char msg[200];
            std::snprintf(msg, sizeof msg, "mem %ld: gc heap %zu KB, free %zu KB, allocated %zu KB, textures %zu KB",
                          fw::frameCounter, GC_get_heap_size() / 1024, GC_get_free_bytes() / 1024, (total - lastTotal) / 1024,
                          fw::gfx::textureBytes / 1024);
            lastTotal = total;
            fw::plat::log(msg);
        }
        if (fw::frameCounter == fw::quitFrame) break;
        double ts = fw::plat::timeSeconds();
        fw::plat::heartbeat("present");
        fw::gfx::bePresent();
        fw::stats.swap += fw::plat::timeSeconds() - ts;
        fw::stats.frames++;
        if (fw::perfStat && fw::frameCounter == 1) {
            char msg[320];
            std::snprintf(msg, sizeof msg,
                          "startup: first frame after %.1f s, Game.Run after %.1f s (images %d in %.1f s, content assets %d in "
                          "%.1f s; file reads %.1f s, texture uploads %.1f s, gc %u = %lu ms)",
                          fw::plat::timeSeconds() - fw::bootStart, fw::processStart - fw::bootStart, fw::loadStats.images,
                          fw::loadStats.imageSeconds, fw::loadStats.assets, fw::loadStats.assetSeconds,
                          fw::loadStats.readSeconds, fw::gfx::gpuStats.uploadSeconds, (unsigned)GC_get_gc_no(),
                          GC_get_full_gc_total_time());
            fw::plat::log(msg);
        }
        if (fw::perfStat && fw::stats.frames == 30) fw::reportStats();
    }
    if (exiting) exiting->Invoke(this, New<ExitingEventArgs>());
    fw::plat::shutdown();
}

void GraphicsDeviceManager::__init(Game* g) {
    game = g;
    g->graphicsManager = this;
}
void GraphicsDeviceManager::ApplyChanges() {}

}  // namespace Microsoft::Xna::Framework

namespace fw { void scriptedKeys(uint32_t keys[8]); }
namespace Microsoft::Xna::Framework::Input {
KeyboardState Keyboard::GetState() {
    KeyboardState s;
    fw::plat::keyboard(s.keys);
    fw::scriptedKeys(s.keys);
    return s;
}
Array1<Keys>* KeyboardState::GetPressedKeys() const {
    auto* l = New<Collections::Generic::List<Keys>>();
    for (int i = 1; i < 256; i++)
        if ((keys[i >> 5] >> (i & 31)) & 1) l->Add((Keys)i);
    return l->ToArray();
}
MouseState Mouse::GetState() {
    MouseState m;
    int b = 0;
    fw::plat::mouse(m.x, m.y, b, m.wheel);
    m.left = (b & 1) ? ButtonState::Pressed : ButtonState::Released;
    m.middle = (b & 2) ? ButtonState::Pressed : ButtonState::Released;
    m.right = (b & 4) ? ButtonState::Pressed : ButtonState::Released;
    return m;
}
GamePadState GamePad::GetState(int32_t index) {
    auto p = fw::plat::gamepad(index);
    GamePadState s;
    if (!p.connected) return s;
    uint32_t b = p.buttons;
    const float dead = 0.25f;
    auto dz = [&](float v) { return std::fabs(v) < dead ? 0.0f : v; };
    Vector2 l(dz(p.lx), dz(p.ly)), r(dz(p.rx), dz(p.ry));
    if (p.lt > 0.5f) b |= (uint32_t)Buttons::LeftTrigger;
    if (p.rt > 0.5f) b |= (uint32_t)Buttons::RightTrigger;
    if (l.X < -0.5f) b |= (uint32_t)Buttons::LeftThumbstickLeft;
    if (l.X > 0.5f) b |= (uint32_t)Buttons::LeftThumbstickRight;
    if (l.Y > 0.5f) b |= (uint32_t)Buttons::LeftThumbstickUp;
    if (l.Y < -0.5f) b |= (uint32_t)Buttons::LeftThumbstickDown;
    if (r.X < -0.5f) b |= (uint32_t)Buttons::RightThumbstickLeft;
    if (r.X > 0.5f) b |= (uint32_t)Buttons::RightThumbstickRight;
    if (r.Y > 0.5f) b |= (uint32_t)Buttons::RightThumbstickUp;
    if (r.Y < -0.5f) b |= (uint32_t)Buttons::RightThumbstickDown;
    auto bs = [&](uint32_t bit) { return (b & bit) ? ButtonState::Pressed : ButtonState::Released; };
    s = GamePadState(GamePadThumbSticks(l, r), GamePadTriggers(p.lt, p.rt), GamePadButtons((Buttons)b), GamePadDPad(bs(1), bs(2), bs(4), bs(8)));
    return s;
}
bool GamePadState::IsButtonDown(Buttons b) const { return ((int32_t)buttons.buttons & (int32_t)b) == (int32_t)b && (int32_t)b != 0; }
}  // namespace Microsoft::Xna::Framework::Input

// ---------------------------------------------------------------- NVorbis (stb_vorbis)
#define STB_VORBIS_HEADER_ONLY
#include "stb/stb_vorbis.c"
namespace NVorbis {
void VorbisReader::__init(IO::Stream* s, bool) {
    data = s->ReadAllRemaining();
    int err = 0;
    dec = stb_vorbis_open_memory(data->data, data->length, &err, nullptr);
    if (!dec) throw NewEx<IO::InvalidDataException>(STR(u"invalid ogg"));
    auto info = stb_vorbis_get_info((stb_vorbis*)dec);
    channels = info.channels;
    sampleRate = (int32_t)info.sample_rate;
}
int64_t VorbisReader::get_TotalSamples() { return dec ? stb_vorbis_stream_length_in_samples((stb_vorbis*)dec) : 0; }
int32_t VorbisReader::ReadSamples(Array1<float>* buffer, int32_t offset, int32_t count) {
    if (!dec) return 0;
    int got = stb_vorbis_get_samples_float_interleaved((stb_vorbis*)dec, channels, buffer->data + offset, count);
    position += got;
    return got * channels;
}
void VorbisReader::SeekTo(int64_t pos, IO::SeekOrigin) {
    if (dec) stb_vorbis_seek((stb_vorbis*)dec, (unsigned)pos);
    position = pos;
}
String* VorbisReader::GetTagSingle(String* key, bool) {
    if (!dec) return String::Empty;
    auto c = stb_vorbis_get_comment((stb_vorbis*)dec);
    std::string k = key->ToUtf8();
    for (int i = 0; i < c.comment_list_length; i++) {
        std::string e = c.comment_list[i];
        auto eq = e.find('=');
        if (eq == std::string::npos) continue;
        std::string ek = e.substr(0, eq);
        if (strcasecmp(ek.c_str(), k.c_str()) == 0) return String::FromUtf8(e.c_str() + eq + 1);
    }
    return String::Empty;
}
void VorbisReader::Dispose() {
    if (dec) stb_vorbis_close((stb_vorbis*)dec);
    dec = nullptr;
}
}  // namespace NVorbis
