// PS Vita platform layer: SceCtrl input, SceAudioOut, ux0: save data (graphics live in gfx_gxm.cpp).
#include "platform.h"
#include <psp2/apputil.h>
#include <psp2/appmgr.h>
#include <psp2/audioout.h>
#include <psp2/ctrl.h>
#include <psp2/io/fcntl.h>
#include <psp2/io/stat.h>
#include <psp2/kernel/modulemgr.h>
#include <psp2/kernel/processmgr.h>
#include <psp2/kernel/sysmem.h>
#include <psp2/kernel/threadmgr.h>
#include <psp2/power.h>
#include <atomic>
#include <cstdarg>
#include <cstdio>
#include <cstring>
#include <string>
#include <algorithm>
#include <malloc.h>
#include <unistd.h>

extern "C" {
int sceUserMainThreadStackSize = 8 * 1024 * 1024;
// newlib heap backs malloc and the GC heap (via sbrk); GPU memory comes from separate memblocks.
// 192 MB left 127 MB of user memory unallocated at the first frame (device log 2026-10-30); 256 MB keeps ~60 MB free.
unsigned int _newlib_heap_size_user = 256 * 1024 * 1024;
// bdwgc's NOSYS/ARM32 configuration reads the main stack bottom from here.
void* __stack_base__ = nullptr;
void GC_init(void);
size_t GC_get_free_bytes(void);
void GC_add_roots(void* low, void* high_plus_1);
void GC_set_abort_func(void (*fn)(const char*));
void GC_set_warn_proc(void (*fn)(char*, unsigned long));
}

namespace fw::plat {

MemoryInfo memoryInfo() {
    MemoryInfo result;
    SceKernelFreeMemorySizeInfo free{};
    free.size = sizeof free;
    if (sceKernelGetFreeMemorySize(&free) >= 0) {
        result.systemFree = std::max(0, free.size_user);
        result.graphicsFree = std::max(0, free.size_cdram);
        result.freeKnown = true;
    }
    SceAppMgrBudgetInfo budget{};
    budget.size = sizeof budget;
    if (sceAppMgrGetBudgetInfo(&budget) >= 0 && budget.total_user_rw_mem) {
        result.budget = budget.total_user_rw_mem;
        result.used = result.budget - std::min(result.budget, (size_t)budget.free_user_rw);
        return result;
    }
    void* end = sbrk(0);
    SceKernelMemBlockInfo block{};
    block.size = sizeof block;
    if (end != (void*)-1 && sceKernelGetMemBlockInfoByAddr((char*)end - 1, &block) >= 0) {
        size_t committed = (uintptr_t)end - (uintptr_t)block.mappedBase;
        size_t reusable = mallinfo().fordblks + GC_get_free_bytes();
        result.budget = block.mappedSize;
        result.used = std::min(result.budget, committed - std::min(committed, reusable));
        result.heapOnly = true;
    }
    return result;
}

static SceUID audioThread = -1;
static SceUID audioMutex = -1;
static int audioPort = -1;
static volatile bool audioRunning = false;
static void (*mixFn)(int16_t*, int) = nullptr;
constexpr int kAudioFrames = 1024;
static SceUID logFd = -1;
static std::atomic<bool> logDirty{false};
static void gcAbort(const char* msg) {
    if (logFd >= 0 && msg) {
        sceIoWrite(logFd, "GC abort: ", 10);
        sceIoWrite(logFd, msg, std::strlen(msg));
        sceIoWrite(logFd, "\n", 1);
        sceIoSyncByFd(logFd, 0);
    }
}
static void gcWarn(char* msg, unsigned long arg) { logf(msg, arg); }

// Watchdog: if the game loop stops advancing for 8 s, log where it was and crash on purpose so the system writes
// a core dump with every thread's stack (a silent freeze leaves no trace otherwise).
static volatile unsigned g_beat = 0;
static const char* volatile g_phase = "";
static int watchdogMain(SceSize, void*) {
    unsigned last = g_beat;
    int stuck = 0;
    for (;;) {
        sceKernelDelayThread(1000 * 1000);
        // A system hang needs a forced power-off, which loses unsynced writes.
        if (logFd >= 0 && logDirty.exchange(false)) sceIoSyncByFd(logFd, 0);
        unsigned b = g_beat;
        if (b != last) {
            last = b;
            stuck = 0;
            continue;
        }
        if (++stuck == 8) {
            logf("watchdog: game loop stuck for 8 s in '%s' (heartbeat %u), forcing a crash dump\n", g_phase, b);
            sceIoSyncByFd(logFd, 0);
            sceKernelDelayThread(200 * 1000);
            *(volatile int*)nullptr = 0;
        }
    }
    return 0;
}
void heartbeat(const char* phase) {
    static bool started = false;
    if (!started) {
        started = true;
        SceUID th = sceKernelCreateThread("ladx_watchdog", watchdogMain, 0x10000100, 0x4000, 0, 0, nullptr);
        if (th >= 0) sceKernelStartThread(th, 0, nullptr);
    }
    g_phase = phase;
    g_beat = g_beat + 1;
}

void preInit() {
    static bool done = false;
    if (done) return;
    done = true;
    SceKernelThreadInfo info{};
    info.size = sizeof info;
    sceKernelGetThreadInfo(sceKernelGetThreadId(), &info);
    __stack_base__ = (char*)info.stack + info.stackSize;
    sceIoMkdir("ux0:data/ladxhd", 0777);
    logFd = sceIoOpen("ux0:data/ladxhd/log.txt", SCE_O_WRONLY | SCE_O_CREAT | SCE_O_TRUNC, 0777);
    GC_set_abort_func(gcAbort);
    GC_set_warn_proc(gcWarn);
    GC_init();
    // The linker's `end` symbol sits exactly at the segment end and is not relocated reliably by the Vita loader,
    // so the GC's static roots come from the loaded module's writable segment instead.
    SceKernelModuleInfo mod{};
    mod.size = sizeof mod;
    if (sceKernelGetModuleInfo(sceKernelGetModuleIdByAddr((void*)&__stack_base__), &mod) >= 0)
        for (auto& s : mod.segments)
            if (s.memsz && (s.perms & 2)) GC_add_roots(s.vaddr, (char*)s.vaddr + s.memsz);
}
// Static constructors may allocate from the GC before main(), so the stack base must be known first.
__attribute__((constructor(101))) static void earlyInit() { preInit(); }

bool init(int width, int height, const char*) {
    scePowerSetArmClockFrequency(444);
    scePowerSetBusClockFrequency(222);
    scePowerSetGpuClockFrequency(222);
    scePowerSetGpuXbarClockFrequency(166);
    sceCtrlSetSamplingMode(SCE_CTRL_MODE_ANALOG_WIDE);
    // The Ext2 read functions used for input have their own mode; without it the sticks always read centered.
    sceCtrlSetSamplingModeExt(SCE_CTRL_MODE_ANALOG_WIDE);
    SceAppUtilInitParam ip{};
    SceAppUtilBootParam bp{};
    sceAppUtilInit(&ip, &bp);
    return true;
}

void shutdown() {
    audioRunning = false;
    if (audioThread >= 0) sceKernelWaitThreadEnd(audioThread, nullptr, nullptr);
    if (logFd >= 0) sceIoSyncByFd(logFd, 0);
    sceKernelExitProcess(0);
}

// Presentation goes through the GXM display queue (gfx_gxm.cpp).
void swap() {}

bool pollEvents(bool& active) {
    active = true;
    return true;
}

void drawableSize(int& w, int& h) {
    w = 960;
    h = 544;
}
double timeSeconds() { return (double)sceKernelGetProcessTimeWide() / 1.0e6; }
void sleepMs(int ms) { sceKernelDelayThread((SceUInt)ms * 1000); }

void keyboard(uint32_t keys[8]) {
    for (int i = 0; i < 8; i++) keys[i] = 0;
}

void mouse(int& x, int& y, int& buttons, int& w) {
    x = y = buttons = w = 0;
}

Pad gamepad(int index) {
    Pad p{};
    if (index != 0) return p;
    SceCtrlData c{};
    sceCtrlPeekBufferPositiveExt2(0, &c, 1);
    p.connected = true;
    auto btn = [&](uint32_t vita, uint32_t bit) { if (c.buttons & vita) p.buttons |= bit; };
    btn(SCE_CTRL_UP, 1);
    btn(SCE_CTRL_DOWN, 2);
    btn(SCE_CTRL_LEFT, 4);
    btn(SCE_CTRL_RIGHT, 8);
    btn(SCE_CTRL_START, 16);
    btn(SCE_CTRL_SELECT, 32);
    btn(SCE_CTRL_L3, 64);
    btn(SCE_CTRL_R3, 128);
    // The Ext2 read reports the L/R buttons as L1/R1; LTRIGGER/RTRIGGER are L2/R2 there.
    btn(SCE_CTRL_L1, 256);
    btn(SCE_CTRL_R1, 512);
    // Positional (Xbox) layout; the game's "swap buttons" option flips confirm/cancel.
    btn(SCE_CTRL_CROSS, 4096);
    btn(SCE_CTRL_CIRCLE, 8192);
    btn(SCE_CTRL_SQUARE, 16384);
    btn(SCE_CTRL_TRIANGLE, 32768);
    auto axis = [](uint8_t v) { return ((int)v - 128) / 127.0f; };
    p.lx = axis(c.lx);
    p.ly = -axis(c.ly);
    p.rx = axis(c.rx);
    p.ry = -axis(c.ry);
    // L2/R2 only exist on PS TV controllers; the rear touchpad is deliberately ignored.
    p.lt = (c.buttons & SCE_CTRL_L2) ? 1.0f : 0.0f;
    p.rt = (c.buttons & SCE_CTRL_R2) ? 1.0f : 0.0f;
    return p;
}

static int audioMain(SceSize, void*) {
    static int16_t buf[2][kAudioFrames * 2];
    int cur = 0;
    while (audioRunning) {
        sceKernelLockMutex(audioMutex, 1, nullptr);
        if (mixFn) mixFn(buf[cur], kAudioFrames);
        else std::memset(buf[cur], 0, sizeof buf[cur]);
        sceKernelUnlockMutex(audioMutex, 1);
        sceAudioOutOutput(audioPort, buf[cur]);
        cur ^= 1;
    }
    return 0;
}

bool audioInit(int rate, void (*mix)(int16_t*, int)) {
    mixFn = mix;
    audioMutex = sceKernelCreateMutex("ladx_audio", SCE_KERNEL_MUTEX_ATTR_RECURSIVE, 0, nullptr);
    audioPort = sceAudioOutOpenPort(SCE_AUDIO_OUT_PORT_TYPE_BGM, kAudioFrames, rate, SCE_AUDIO_OUT_MODE_STEREO);
    if (audioPort < 0) return false;
    audioRunning = true;
    // Above the main thread's default priority (0x10000100) so mixing never starves behind a slow frame.
    audioThread = sceKernelCreateThread("ladx_audio", audioMain, 0x10000100 - 20, 0x10000, 0, 0, nullptr);
    if (audioThread < 0) return false;
    sceKernelStartThread(audioThread, 0, nullptr);
    return true;
}
void audioLock() { if (audioMutex >= 0) sceKernelLockMutex(audioMutex, 1, nullptr); }
void audioUnlock() { if (audioMutex >= 0) sceKernelUnlockMutex(audioMutex, 1); }

const char* contentRoot() { return "app0:game"; }
const char* packFile() { return "app0:game.pak"; }
const char* userRoot() { return "ux0:data/ladxhd"; }

void log(const char* msg) {
    logf("%s\n", msg);
}
// Written with sceIo directly: stdio buffers are lost when the app is closed from the PS button.
void logf(const char* fmt, ...) {
    if (logFd < 0) return;
    char buf[1024];
    va_list ap;
    va_start(ap, fmt);
    int n = std::vsnprintf(buf, sizeof buf, fmt, ap);
    va_end(ap);
    if (n > 0 && sceIoWrite(logFd, buf, n < (int)sizeof buf ? n : (int)sizeof buf - 1) > 0)
        logDirty.store(true);
}

struct ThreadStart {
    void (*fn)(void*);
    void* arg;
};
static int threadMain(SceSize, void* argp) {
    auto* s = *(ThreadStart**)argp;
    s->fn(s->arg);
    delete s;
    return 0;
}
void startThread(void (*fn)(void*), void* arg, void** handle) {
    auto* s = new ThreadStart{fn, arg};
    SceUID id = sceKernelCreateThread("ladx_worker", threadMain, 0x10000100, 0x40000, 0, 0, nullptr);
    sceKernelStartThread(id, sizeof s, &s);
    *handle = (void*)(intptr_t)id;
}
void joinThread(void* handle) { sceKernelWaitThreadEnd((SceUID)(intptr_t)handle, nullptr, nullptr); }

}  // namespace fw::plat
