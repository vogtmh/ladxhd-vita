// Desktop platform layer: SDL2 window + OpenGL 3.2 core context, input, audio.
#include "platform.h"
#include <SDL.h>
#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <string>
#include <thread>

namespace fw::plat {

static SDL_Window* window = nullptr;
static SDL_GLContext glctx = nullptr;
static SDL_GameController* pads[4] = {};
static int wheel = 0;
static SDL_AudioDeviceID audioDev = 0;
static void (*mixFn)(int16_t*, int) = nullptr;

void preInit() {}
MemoryInfo memoryInfo() { return {}; }

bool init(int width, int height, const char* title) {
    if (SDL_Init(SDL_INIT_VIDEO | SDL_INIT_AUDIO | SDL_INIT_GAMECONTROLLER | SDL_INIT_TIMER) != 0) {
        logf("SDL_Init: %s\n", SDL_GetError());
        return false;
    }
    SDL_GL_SetAttribute(SDL_GL_CONTEXT_MAJOR_VERSION, 3);
    SDL_GL_SetAttribute(SDL_GL_CONTEXT_MINOR_VERSION, 2);
    SDL_GL_SetAttribute(SDL_GL_CONTEXT_PROFILE_MASK, SDL_GL_CONTEXT_PROFILE_CORE);
    SDL_GL_SetAttribute(SDL_GL_CONTEXT_FLAGS, SDL_GL_CONTEXT_FORWARD_COMPATIBLE_FLAG);
    SDL_GL_SetAttribute(SDL_GL_DOUBLEBUFFER, 1);
    window = SDL_CreateWindow(title, SDL_WINDOWPOS_CENTERED, SDL_WINDOWPOS_CENTERED, width, height, SDL_WINDOW_OPENGL | SDL_WINDOW_SHOWN);
    if (!window) {
        logf("SDL_CreateWindow: %s\n", SDL_GetError());
        return false;
    }
    glctx = SDL_GL_CreateContext(window);
    if (!glctx) {
        logf("SDL_GL_CreateContext: %s\n", SDL_GetError());
        return false;
    }
    SDL_GL_SetSwapInterval(1);
    for (int i = 0; i < SDL_NumJoysticks() && i < 4; i++)
        if (SDL_IsGameController(i)) pads[i] = SDL_GameControllerOpen(i);
    return true;
}

void shutdown() {
    if (audioDev) SDL_CloseAudioDevice(audioDev);
    SDL_Quit();
}

void swap() { SDL_GL_SwapWindow(window); }

bool pollEvents(bool& active) {
    SDL_Event e;
    bool running = true;
    while (SDL_PollEvent(&e)) {
        switch (e.type) {
            case SDL_QUIT: running = false; break;
            case SDL_MOUSEWHEEL: wheel += e.wheel.y * 120; break;
            case SDL_WINDOWEVENT:
                if (e.window.event == SDL_WINDOWEVENT_FOCUS_GAINED) active = true;
                if (e.window.event == SDL_WINDOWEVENT_FOCUS_LOST) active = false;
                break;
            case SDL_CONTROLLERDEVICEADDED:
                if (e.cdevice.which < 4 && !pads[e.cdevice.which]) pads[e.cdevice.which] = SDL_GameControllerOpen(e.cdevice.which);
                break;
        }
    }
    return running;
}

void drawableSize(int& w, int& h) { SDL_GL_GetDrawableSize(window, &w, &h); }
double timeSeconds() { return (double)SDL_GetPerformanceCounter() / (double)SDL_GetPerformanceFrequency(); }
void sleepMs(int ms) { SDL_Delay(ms); }

static int xnaKey(SDL_Scancode sc) {
    if (sc >= SDL_SCANCODE_A && sc <= SDL_SCANCODE_Z) return 65 + (sc - SDL_SCANCODE_A);
    if (sc >= SDL_SCANCODE_1 && sc <= SDL_SCANCODE_9) return 49 + (sc - SDL_SCANCODE_1);
    if (sc == SDL_SCANCODE_0) return 48;
    if (sc >= SDL_SCANCODE_F1 && sc <= SDL_SCANCODE_F12) return 112 + (sc - SDL_SCANCODE_F1);
    if (sc >= SDL_SCANCODE_KP_1 && sc <= SDL_SCANCODE_KP_9) return 97 + (sc - SDL_SCANCODE_KP_1);
    switch (sc) {
        case SDL_SCANCODE_KP_0: return 96;
        case SDL_SCANCODE_RETURN: return 13;
        case SDL_SCANCODE_ESCAPE: return 27;
        case SDL_SCANCODE_BACKSPACE: return 8;
        case SDL_SCANCODE_TAB: return 9;
        case SDL_SCANCODE_SPACE: return 32;
        case SDL_SCANCODE_LEFT: return 37;
        case SDL_SCANCODE_UP: return 38;
        case SDL_SCANCODE_RIGHT: return 39;
        case SDL_SCANCODE_DOWN: return 40;
        case SDL_SCANCODE_INSERT: return 45;
        case SDL_SCANCODE_DELETE: return 46;
        case SDL_SCANCODE_HOME: return 36;
        case SDL_SCANCODE_END: return 35;
        case SDL_SCANCODE_PAGEUP: return 33;
        case SDL_SCANCODE_PAGEDOWN: return 34;
        case SDL_SCANCODE_LSHIFT: return 160;
        case SDL_SCANCODE_RSHIFT: return 161;
        case SDL_SCANCODE_LCTRL: return 162;
        case SDL_SCANCODE_RCTRL: return 163;
        case SDL_SCANCODE_LALT: return 164;
        case SDL_SCANCODE_RALT: return 165;
        case SDL_SCANCODE_KP_PLUS: return 107;
        case SDL_SCANCODE_KP_MINUS: return 109;
        case SDL_SCANCODE_KP_PERIOD: return 110;
        case SDL_SCANCODE_COMMA: return 188;
        case SDL_SCANCODE_PERIOD: return 190;
        case SDL_SCANCODE_GRAVE: return 192;
        case SDL_SCANCODE_MINUS: return 189;
        case SDL_SCANCODE_EQUALS: return 187;
        default: return 0;
    }
}

void keyboard(uint32_t keys[8]) {
    int n = 0;
    const uint8_t* st = SDL_GetKeyboardState(&n);
    for (int i = 0; i < 8; i++) keys[i] = 0;
    for (int sc = 0; sc < n; sc++) {
        if (!st[sc]) continue;
        int k = xnaKey((SDL_Scancode)sc);
        if (k > 0 && k < 256) keys[k >> 5] |= 1u << (k & 31);
    }
}

void mouse(int& x, int& y, int& buttons, int& w) {
    uint32_t b = SDL_GetMouseState(&x, &y);
    buttons = (b & SDL_BUTTON_LMASK ? 1 : 0) | (b & SDL_BUTTON_MMASK ? 2 : 0) | (b & SDL_BUTTON_RMASK ? 4 : 0);
    w = wheel;
}

Pad gamepad(int index) {
    Pad p{};
    SDL_GameController* c = index >= 0 && index < 4 ? pads[index] : nullptr;
    if (!c || !SDL_GameControllerGetAttached(c)) return p;
    p.connected = true;
    auto btn = [&](SDL_GameControllerButton b, uint32_t bit) { if (SDL_GameControllerGetButton(c, b)) p.buttons |= bit; };
    btn(SDL_CONTROLLER_BUTTON_DPAD_UP, 1);
    btn(SDL_CONTROLLER_BUTTON_DPAD_DOWN, 2);
    btn(SDL_CONTROLLER_BUTTON_DPAD_LEFT, 4);
    btn(SDL_CONTROLLER_BUTTON_DPAD_RIGHT, 8);
    btn(SDL_CONTROLLER_BUTTON_START, 16);
    btn(SDL_CONTROLLER_BUTTON_BACK, 32);
    btn(SDL_CONTROLLER_BUTTON_LEFTSTICK, 64);
    btn(SDL_CONTROLLER_BUTTON_RIGHTSTICK, 128);
    btn(SDL_CONTROLLER_BUTTON_LEFTSHOULDER, 256);
    btn(SDL_CONTROLLER_BUTTON_RIGHTSHOULDER, 512);
    btn(SDL_CONTROLLER_BUTTON_GUIDE, 2048);
    btn(SDL_CONTROLLER_BUTTON_A, 4096);
    btn(SDL_CONTROLLER_BUTTON_B, 8192);
    btn(SDL_CONTROLLER_BUTTON_X, 16384);
    btn(SDL_CONTROLLER_BUTTON_Y, 32768);
    auto ax = [&](SDL_GameControllerAxis a) { return SDL_GameControllerGetAxis(c, a) / 32767.0f; };
    p.lx = ax(SDL_CONTROLLER_AXIS_LEFTX);
    p.ly = -ax(SDL_CONTROLLER_AXIS_LEFTY);
    p.rx = ax(SDL_CONTROLLER_AXIS_RIGHTX);
    p.ry = -ax(SDL_CONTROLLER_AXIS_RIGHTY);
    p.lt = ax(SDL_CONTROLLER_AXIS_TRIGGERLEFT);
    p.rt = ax(SDL_CONTROLLER_AXIS_TRIGGERRIGHT);
    return p;
}

// Desktop test runs are silent unless LADX_SOUND=1; the mixer still runs so audio timing stays the same.
static const bool audioMuted = !(std::getenv("LADX_SOUND") && std::getenv("LADX_SOUND")[0] == '1');

static void audioCallback(void*, uint8_t* stream, int len) {
    if (mixFn) mixFn((int16_t*)stream, len / 4);
    if (!mixFn || audioMuted) SDL_memset(stream, 0, len);
}

bool audioInit(int rate, void (*mix)(int16_t*, int)) {
    mixFn = mix;
    SDL_AudioSpec want{}, have{};
    want.freq = rate;
    want.format = AUDIO_S16SYS;
    want.channels = 2;
    want.samples = 1024;
    want.callback = audioCallback;
    audioDev = SDL_OpenAudioDevice(nullptr, 0, &want, &have, 0);
    if (!audioDev) return false;
    SDL_PauseAudioDevice(audioDev, 0);
    return true;
}
void audioLock() { if (audioDev) SDL_LockAudioDevice(audioDev); }
void audioUnlock() { if (audioDev) SDL_UnlockAudioDevice(audioDev); }

const char* contentRoot() {
    static std::string root = [] {
        if (const char* env = std::getenv("LADX_ROOT")) return std::string(env);
        char* base = SDL_GetBasePath();
        std::string r = base ? base : "./";
        SDL_free(base);
        return r + "game";
    }();
    return root.c_str();
}
const char* packFile() { return std::getenv("LADX_PAK"); }
const char* userRoot() {
    static std::string root = [] {
        char* p = SDL_GetPrefPath("ladxhd", "vita-port");
        std::string r = p ? p : "./";
        SDL_free(p);
        if (!r.empty() && r.back() == '/') r.pop_back();
        return r;
    }();
    return root.c_str();
}
void log(const char* msg) { std::fprintf(stderr, "%s\n", msg); }
void logf(const char* fmt, ...) {
    va_list ap;
    va_start(ap, fmt);
    std::vfprintf(stderr, fmt, ap);
    va_end(ap);
}
void logFlush() {}
void startThread(void (*fn)(void*), void* arg, void** handle) {
    auto* t = new std::thread(fn, arg);
    *handle = t;
}
void joinThread(void* handle) {
    auto* t = (std::thread*)handle;
    if (t->joinable()) t->join();
}
void heartbeat(const char*) {}
void keepAwake() {}

}  // namespace fw::plat
