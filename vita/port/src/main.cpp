// Entry point: GC/static init, platform services, Game1.Run().
#include "types.h"
#include "../../framework/src/platform.h"
#include <cstdlib>
#include <string>

void __RunStaticInit();
extern "C" int GC_expand_hp(size_t);
extern "C" void GC_start_performance_measurement(void);
namespace fw {
extern void (*frameHook)();
}

using namespace ::System;
using namespace ::ProjectZ;

// GbsPlayer's update thread is replaced by one pump per game update.
static void frameHook() {
    if (Game1::GbsPlayer) Game1::GbsPlayer->UpdateThread();
}

extern "C" const char* rt_game_version() { return LADX_GAME_VERSION; }

#if !defined(__vita__)
#include <csignal>
#include <execinfo.h>
#include <unistd.h>
static void crashHandler(int sig) {
    void* frames[64];
    int n = backtrace(frames, 64);
    fw::plat::logf("*** signal %d\n", sig);
    backtrace_symbols_fd(frames, n, 2);
    _exit(128 + sig);
}
#endif

static String* MapPathIdentity(String* p) { return p; }

int main(int argc, char** argv) {
    fw::plat::preInit();
    fw::plat::logf("LADXHD Vita release %s (game %s)\n", LADX_RELEASE_VERSION, rt_game_version());
#if !defined(__vita__)
    std::signal(SIGSEGV, crashHandler);
    std::signal(SIGBUS, crashHandler);
    std::signal(SIGABRT, crashHandler);
#endif
    rt::Init();
    GC_start_performance_measurement();
#if defined(__vita__)
    // Start with a large heap so loading doesn't run dozens of collections while growing it.
    GC_expand_hp(64 * 1024 * 1024);
    // Keep 32 MB of the 256 MB newlib heap for malloc (image decoding, GXM shader patcher); the GC collects
    // harder instead of growing past this.
    rt::ConfigureCollector(224 * 1024 * 1024, true);
#else
    rt::ConfigureCollector(160 * 1024 * 1024, false);
#endif
    if (const char* pak = fw::plat::packFile())
        if (!rt::MountPack(pak, fw::plat::contentRoot())) fw::plat::logf("could not mount %s\n", pak);
    IO::MapPath = MapPathIdentity;
    EventArgs::Empty = New<EventArgs>();
    __RunStaticInit();
    fw::frameHook = ::frameHook;

    bool loadSave = false;
    int32_t slot = 0;
    for (int i = 1; i < argc; i++)
        if (std::string(argv[i]) == "loadSave") {
            loadSave = true;
            if (i + 1 < argc) slot = std::atoi(argv[++i]);
        }

    try {
        auto* game = New<Game1>(false, loadSave, slot);
        auto* svc = game->get_Services();
        auto* root = String::FromUtf8(fw::plat::contentRoot());
        auto* user = String::FromUtf8(fw::plat::userRoot());
        svc->AddService(TypeOf<IPlatformDisplayConfiguration>(), New<PlatformDisplayConfiguration>(960, 544));
        svc->AddService(TypeOf<IPlatformFileSystem>(), New<LocalPlatformFileSystem>(root));
        svc->AddService(TypeOf<IUserDataPaths>(), New<LocalUserDataPaths>(user));
        svc->AddService(TypeOf<ISharedSaveService>(), New<UnavailableSharedSaveService>());
        svc->AddService(TypeOf<IPlatformInput>(), New<NullPlatformInput>());
        svc->AddService(TypeOf<ITextInputService>(), New<NullTextInputService>());
        svc->AddService(TypeOf<IPlatformWindow>(), New<DefaultPlatformWindow>(false, false, true));
        svc->AddService(TypeOf<IGraphicsCapabilities>(), New<GraphicsCapabilities>(true, false, true, false, false));
        svc->AddService(TypeOf<IPlatformPresentation>(), New<PlatformPresentation>(240, true, true, 1));
        svc->AddService(TypeOf<IFileDialogService>(), New<UnavailableFileDialogService>());
        svc->AddService(TypeOf<IPhotoPrintService>(), New<UnavailablePhotoPrintService>());
        game->Run();
    } catch (Exception* e) {
        auto* m = e->get_Message();
        fw::plat::logf("Unhandled exception %s: %s\n", e->GetType()->fullName->ToUtf8(), m ? m->ToUtf8() : "");
        return 1;
    }
    return 0;
}
