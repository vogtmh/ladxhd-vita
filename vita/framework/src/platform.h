#pragma once
// Platform layer used by the MonoGame shim (SDL2 on desktop, native Vita APIs on the console).
#include <cstdint>
#include <cstddef>

namespace fw::plat {
// Called first in main(), before the GC starts.
void preInit();
bool init(int width, int height, const char* title);
void shutdown();
void swap();
// Returns false when the app should quit.
bool pollEvents(bool& active);
void drawableSize(int& w, int& h);
double timeSeconds();
struct MemoryInfo {
    size_t used = 0, budget = 0;
    size_t systemFree = 0, graphicsFree = 0;
    bool heapOnly = false, freeKnown = false;
};
MemoryInfo memoryInfo();
void sleepMs(int ms);
// XNA key codes, 256 bits.
void keyboard(uint32_t keys[8]);
void mouse(int& x, int& y, int& buttons, int& wheel);
struct Pad {
    bool connected;
    uint32_t buttons;  // XNA Buttons bits
    float lx, ly, rx, ry, lt, rt;
};
Pad gamepad(int index);
// Audio: callback fills interleaved stereo int16 frames at `rate`.
bool audioInit(int rate, void (*mix)(int16_t* out, int frames));
void audioLock();
void audioUnlock();
const char* contentRoot();  // folder containing Content/ and Data/
const char* packFile();     // optional pack served at contentRoot() (null = plain files)
const char* userRoot();     // writable folder for saves/settings
bool diagnosticsEnabled();
void log(const char* msg);
void logf(const char* fmt, ...) __attribute__((format(printf, 1, 2)));
void startThread(void (*fn)(void*), void* arg, void** handle);
void joinThread(void* handle);
// Marks game-loop progress; the Vita build runs a watchdog on it.
void heartbeat(const char* phase);
}  // namespace fw::plat
