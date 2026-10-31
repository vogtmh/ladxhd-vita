// PS Vita backend on raw sceGxm (same approach as the Mighty No. 9 port): own display/context setup, Cg shaders
// compiled once with libshacccg and cached as GXP, vertex data written straight into GPU-mapped per-frame rings,
// and one GXM scene per render-target switch.
#include "gfx_internal.h"
#include "platform.h"
#include <psp2/display.h>
#include <psp2/gxm.h>
#include <psp2/io/stat.h>
#include <psp2/kernel/modulemgr.h>
#include <psp2/kernel/sysmem.h>
#include <psp2/kernel/threadmgr.h>
#include <psp2/kernel/processmgr.h>
#include <psp2/shacccg.h>
#include <algorithm>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <map>
#include <mutex>
#include <string>
#include <vector>

using namespace ::System;

namespace fw::gfx {

#include "effects_cg.inc"

namespace {

constexpr int kW = 960, kH = 544, kStride = 960, kBuffers = 3;
constexpr size_t kRingSize = 8 * 1024 * 1024;  // vertex/index data per frame half
constexpr int kMaxQuadsPerDraw = 16384;        // 16-bit indices

// The GXM context and shader patcher are shared with the loading-spinner thread. The main thread holds
// g_gpuMutex for a whole frame (beBeginFrame..bePresent) and around any other GPU call.
std::recursive_mutex g_gpuMutex;
std::mutex g_memMutex;
using GpuLock = std::lock_guard<std::recursive_mutex>;

// ---------------------------------------------------------------- GPU memory
void* mapBlock(SceKernelMemBlockType type, size_t size, SceGxmMemoryAttribFlags attr, SceUID* uid) {
    size_t align = type == SCE_KERNEL_MEMBLOCK_TYPE_USER_CDRAM_RW ? 256 * 1024 : 4096;
    size = (size + align - 1) & ~(align - 1);
    *uid = sceKernelAllocMemBlock("ladx_gpu", type, size, nullptr);
    if (*uid < 0) return nullptr;
    void* base = nullptr;
    sceKernelGetMemBlockBase(*uid, &base);
    if (sceGxmMapMemory(base, size, attr) < 0) {
        sceKernelFreeMemBlock(*uid);
        return nullptr;
    }
    return base;
}
void* mapUsse(size_t size, bool fragment, unsigned* offset) {
    size = (size + 4095) & ~(size_t)4095;
    SceUID uid = sceKernelAllocMemBlock("ladx_usse", SCE_KERNEL_MEMBLOCK_TYPE_USER_RW_UNCACHE, size, nullptr);
    if (uid < 0) return nullptr;
    void* base = nullptr;
    sceKernelGetMemBlockBase(uid, &base);
    int r = fragment ? sceGxmMapFragmentUsseMemory(base, size, offset) : sceGxmMapVertexUsseMemory(base, size, offset);
    return r < 0 ? nullptr : base;
}

// Textures and render targets are sub-allocated from large blocks: CDRAM first, then uncached main RAM.
struct Pool {
    uint8_t* base;
    size_t size;
    std::map<size_t, size_t> free;  // offset -> length
};
std::vector<Pool> g_pools;
std::map<uint8_t*, size_t> g_allocs;
struct PendingFree {
    void* ptr;
    uint64_t frame;
};
std::vector<PendingFree> g_pendingFrees;
uint64_t g_frame = 0;

bool addPool(size_t minSize) {
    const SceKernelMemBlockType types[2] = {SCE_KERNEL_MEMBLOCK_TYPE_USER_CDRAM_RW, SCE_KERNEL_MEMBLOCK_TYPE_USER_RW_UNCACHE};
    for (auto type : types)
        for (size_t want : {std::max(minSize, (size_t)32 << 20), minSize}) {
            SceUID uid;
            void* base = mapBlock(type, want, (SceGxmMemoryAttribFlags)(SCE_GXM_MEMORY_ATTRIB_READ | SCE_GXM_MEMORY_ATTRIB_WRITE), &uid);
            if (!base) continue;
            SceKernelMemBlockInfo info{};
            info.size = sizeof info;
            sceKernelGetMemBlockInfoByAddr(base, &info);
            Pool p{(uint8_t*)base, info.mappedSize ? info.mappedSize : want, {}};
            p.free[0] = p.size;
            g_pools.push_back(p);
            fw::plat::logf("gpu pool %u KB (%s)\n", (unsigned)(p.size / 1024), type == types[0] ? "cdram" : "main");
            return true;
        }
    return false;
}
void* gpuAlloc(size_t size, size_t align = 256) {
    std::lock_guard<std::mutex> lk(g_memMutex);
    size = (size + 255) & ~(size_t)255;
    for (int attempt = 0; attempt < 2; attempt++) {
        for (auto& p : g_pools)
            for (auto it = p.free.begin(); it != p.free.end(); ++it) {
                size_t off = it->first, len = it->second;
                size_t aoff = (off + align - 1) & ~(align - 1);
                if (aoff + size > off + len) continue;
                p.free.erase(it);
                if (aoff > off) p.free[off] = aoff - off;
                if (aoff + size < off + len) p.free[aoff + size] = off + len - (aoff + size);
                g_allocs[p.base + aoff] = size;
                return p.base + aoff;
            }
        if (!addPool(size + align)) break;
    }
    fw::plat::logf("gpu alloc of %u KB failed\n", (unsigned)(size / 1024));
    return nullptr;
}
void gpuFreeNow(void* ptr) {
    std::lock_guard<std::mutex> lk(g_memMutex);
    auto a = g_allocs.find((uint8_t*)ptr);
    if (a == g_allocs.end()) return;
    size_t size = a->second;
    g_allocs.erase(a);
    for (auto& p : g_pools) {
        if ((uint8_t*)ptr < p.base || (uint8_t*)ptr >= p.base + p.size) continue;
        size_t off = (uint8_t*)ptr - p.base;
        auto next = p.free.lower_bound(off);
        if (next != p.free.end() && next->first == off + size) {
            size += next->second;
            next = p.free.erase(next);
        }
        if (next != p.free.begin()) {
            auto prev = std::prev(next);
            if (prev->first + prev->second == off) {
                prev->second += size;
                return;
            }
        }
        p.free[off] = size;
        return;
    }
}
// The GPU may still read the memory for the next two frames.
void gpuFree(void* ptr) {
    std::lock_guard<std::mutex> lk(g_memMutex);
    if (ptr) g_pendingFrees.push_back({ptr, g_frame});
}

// ---------------------------------------------------------------- display / context
struct DisplayData {
    void* address;
    int vblanks;  // refreshes the frame stays on screen
};
// Frame pacing: every frame is shown for the same number of refreshes and the game advances by exactly that time.
// Showing ~50 fps frames for one or two refreshes at random made scrolling shake by a few pixels.
// Starts at 60: the intro fits it, and starting at 30 showed its first seconds at 30 fps until the first try.
int g_swapInterval = 1;
SceGxmContext* g_ctx = nullptr;
SceGxmShaderPatcher* g_patcher = nullptr;
SceGxmRenderTarget* g_displayRT = nullptr;
void* g_dispMem[kBuffers];
SceGxmColorSurface g_dispSurf[kBuffers];
SceGxmSyncObject* g_dispSync[kBuffers];
int g_front = 0, g_back = 1;
volatile unsigned int* g_notify = nullptr;
SceGxmNotification g_frameDone[2];
uint8_t* g_ring[2] = {};
size_t g_ringPos = 0;
uint16_t* g_quadIdx = nullptr;

void displayCallback(const void* data) {
    auto* d = (const DisplayData*)data;
    SceDisplayFrameBuf fb{};
    fb.size = sizeof fb;
    fb.base = d->address;
    fb.pitch = kStride;
    fb.pixelformat = SCE_DISPLAY_PIXELFORMAT_A8B8G8R8;
    fb.width = kW;
    fb.height = kH;
    sceDisplaySetFrameBuf(&fb, SCE_DISPLAY_SETBUF_NEXTFRAME);
    sceDisplayWaitVblankStartMulti(d->vblanks);
}
void* patcherAlloc(void*, SceSize size) { return std::malloc(size); }
void patcherFree(void*, void* p) { std::free(p); }

void* ringAlloc(size_t n) {
    n = (n + 15) & ~(size_t)15;
    if (g_ringPos + n <= kRingSize) {
        void* p = g_ring[g_frame & 1] + g_ringPos;
        g_ringPos += n;
        return p;
    }
    // Overflow: a one-off block that is released once the GPU is done with this frame.
    void* p = gpuAlloc(n, 16);
    gpuFree(p);
    return p;
}

// ---------------------------------------------------------------- shaders
SceShaccCgSourceFile g_source;
bool g_shaccLoaded = false, g_shaccFailed = false;
SceShaccCgSourceFile* openSource(const char*, const SceShaccCgSourceLocation*, const SceShaccCgCompileOptions*, const char**) {
    return &g_source;
}
void* shaccAlloc(unsigned int size) { return std::malloc(size); }

SceGxmProgram* loadGxp(const char* path) {
    FILE* f = std::fopen(path, "rb");
    if (!f) return nullptr;
    std::fseek(f, 0, SEEK_END);
    long size = std::ftell(f);
    std::fseek(f, 0, SEEK_SET);
    auto* p = size > 0 ? (SceGxmProgram*)std::malloc((size_t)size) : nullptr;
    // The compiler's output size can exceed the program's own size (padding), so only require it to fit.
    if (!p || std::fread(p, 1, (size_t)size, f) != (size_t)size || sceGxmProgramCheck(p) != 0 ||
        sceGxmProgramGetSize(p) > (unsigned)size) {
        fw::plat::logf("gxp %s rejected (%ld bytes)\n", path, size);
        std::free(p);
        p = nullptr;
    }
    std::fclose(f);
    return p;
}

// Bundled GXP (app0:gxp) first, then the ux0 cache, then the runtime compiler.
SceGxmProgram* compileCg(const char* name, const char* src, bool fragment) {
    uint64_t h = 1469598103934665603ULL;
    for (const char* s = src; *s; s++) h = (h ^ (uint8_t)*s) * 1099511628211ULL;
    char file[96], path[160];
    std::snprintf(file, sizeof file, "%s_%016llx.gxp", name, (unsigned long long)h);
    std::snprintf(path, sizeof path, "app0:gxp/%s", file);
    if (SceGxmProgram* p = loadGxp(path)) return p;
    std::snprintf(path, sizeof path, "ux0:data/ladxhd/gxp/%s", file);
    if (SceGxmProgram* p = loadGxp(path)) return p;
    if (!g_shaccLoaded && !g_shaccFailed) {
        static const char* const modules[] = {"ur0:data/libshacccg.suprx", "ur0:data/external/libshacccg.suprx",
                                              "ux0:data/ladxhd/libshacccg.suprx"};
        SceUID mod = -1;
        for (const char* m : modules)
            if ((mod = sceKernelLoadStartModule(m, 0, nullptr, 0, nullptr, nullptr)) >= 0) break;
        if (mod < 0) {
            g_shaccFailed = true;
            fw::plat::logf("libshacccg.suprx not found (ur0:data/); shaders cannot be compiled\n");
            return nullptr;
        }
        sceShaccCgSetDefaultAllocator(shaccAlloc, std::free);
        sceIoMkdir("ux0:data/ladxhd/gxp", 0777);
        g_shaccLoaded = true;
    }
    if (!g_shaccLoaded) return nullptr;
    double t0 = fw::plat::timeSeconds();
    g_source = SceShaccCgSourceFile{name, src, (SceUInt32)std::strlen(src)};
    SceShaccCgCallbackList callbacks;
    sceShaccCgInitializeCallbackList(&callbacks, SCE_SHACCCG_TRIVIAL);
    callbacks.openFile = openSource;
    SceShaccCgCompileOptions options;
    sceShaccCgInitializeCompileOptions(&options);
    options.mainSourceFile = name;
    options.targetProfile = fragment ? SCE_SHACCCG_PROFILE_FP : SCE_SHACCCG_PROFILE_VP;
    options.entryFunctionName = "main";
    options.useFx = 1;
    options.optimizationLevel = 3;
    options.useFastmath = 1;
    options.warningLevel = 1;
    const SceShaccCgCompileOutput* out = sceShaccCgCompileProgram(&options, &callbacks, 0);
    SceGxmProgram* p = nullptr;
    if (out && out->programData && out->programSize) {
        p = (SceGxmProgram*)std::malloc(out->programSize);
        std::memcpy(p, out->programData, out->programSize);
        if (FILE* f = std::fopen(path, "wb")) {
            std::fwrite(p, 1, out->programSize, f);
            std::fclose(f);
        } else {
            fw::plat::logf("could not write shader cache %s\n", path);
        }
        fw::plat::logf("compiled shader %s in %.0f ms\n", name, (fw::plat::timeSeconds() - t0) * 1000.0);
    } else {
        fw::plat::logf("shader %s failed:\n", name);
        for (int i = 0; out && i < out->diagnosticCount; i++) fw::plat::logf("  %s\n", out->diagnostics[i].message);
    }
    if (out) sceShaccCgDestroyCompileOutput(out);
    return p;
}

struct Registered {
    SceGxmProgram* gxp = nullptr;
    SceGxmShaderPatcherId id = nullptr;
};
std::map<std::string, Registered> g_registered;
Registered registerShader(const char* name, const char* src, bool fragment) {
    std::string key = std::string(fragment ? "f:" : "v:") + name;
    auto it = g_registered.find(key);
    if (it != g_registered.end()) return it->second;
    Registered r;
    r.gxp = compileCg(name, src, fragment);
    if (r.gxp && sceGxmShaderPatcherRegisterProgram(g_patcher, r.gxp, &r.id) < 0) r.gxp = nullptr;
    g_registered[key] = r;
    return r;
}

struct GxmProgram : Program {
    const SceGxmProgram* vsGxp = nullptr;
    const SceGxmProgram* fsGxp = nullptr;
    SceGxmShaderPatcherId vsId = nullptr, fsId = nullptr;
    SceGxmVertexProgram* vpQuad = nullptr;
    std::vector<std::pair<std::string, SceGxmVertexProgram*>> vpDecl;
    std::vector<std::pair<uint32_t, SceGxmFragmentProgram*>> fps;
    const SceGxmProgramParameter *uMatrix = nullptr, *uGlvp = nullptr, *uFcsign = nullptr;
    std::vector<const SceGxmProgramParameter*> vsParams, fsParams;  // per ParamDef
    bool vsUniforms = false, fsUniforms = false;
};

struct AttrSpec {
    const char* name;
    uint16_t offset;
    SceGxmAttributeFormat format;
    uint8_t comps;
};
SceGxmVertexProgram* createVertexProgram(GxmProgram* p, const AttrSpec* specs, int n, int stride) {
    SceGxmVertexAttribute attrs[8];
    int count = 0;
    for (int i = 0; i < n && count < 8; i++) {
        const SceGxmProgramParameter* param = sceGxmProgramFindParameterByName(p->vsGxp, specs[i].name);
        if (!param) continue;
        attrs[count].streamIndex = 0;
        attrs[count].offset = specs[i].offset;
        attrs[count].format = specs[i].format;
        attrs[count].componentCount = specs[i].comps;
        attrs[count].regIndex = (unsigned short)sceGxmProgramParameterGetResourceIndex(param);
        count++;
    }
    SceGxmVertexStream stream;
    stream.stride = (unsigned short)stride;
    stream.indexSource = SCE_GXM_INDEX_SOURCE_INDEX_16BIT;
    SceGxmVertexProgram* vp = nullptr;
    int r = sceGxmShaderPatcherCreateVertexProgram(g_patcher, p->vsId, attrs, count, &stream, 1, &vp);
    if (r < 0) fw::plat::logf("vertex program failed: 0x%08X\n", (unsigned)r);
    return vp;
}

SceGxmBlendFactor gxmFactor(Blend b) {
    switch (b) {
        case Blend::One: return SCE_GXM_BLEND_FACTOR_ONE;
        case Blend::Zero: return SCE_GXM_BLEND_FACTOR_ZERO;
        case Blend::SourceColor: return SCE_GXM_BLEND_FACTOR_SRC_COLOR;
        case Blend::InverseSourceColor: return SCE_GXM_BLEND_FACTOR_ONE_MINUS_SRC_COLOR;
        case Blend::SourceAlpha: return SCE_GXM_BLEND_FACTOR_SRC_ALPHA;
        case Blend::InverseSourceAlpha: return SCE_GXM_BLEND_FACTOR_ONE_MINUS_SRC_ALPHA;
        case Blend::DestinationColor: return SCE_GXM_BLEND_FACTOR_DST_COLOR;
        case Blend::InverseDestinationColor: return SCE_GXM_BLEND_FACTOR_ONE_MINUS_DST_COLOR;
        case Blend::DestinationAlpha: return SCE_GXM_BLEND_FACTOR_DST_ALPHA;
        case Blend::InverseDestinationAlpha: return SCE_GXM_BLEND_FACTOR_ONE_MINUS_DST_ALPHA;
        case Blend::SourceAlphaSaturation: return SCE_GXM_BLEND_FACTOR_SRC_ALPHA_SATURATE;
        default: return SCE_GXM_BLEND_FACTOR_ONE;  // constant blend factors do not exist on GXM
    }
}
SceGxmBlendFunc gxmFunc(BlendFunction f) {
    switch (f) {
        case BlendFunction::Subtract: return SCE_GXM_BLEND_FUNC_SUBTRACT;
        case BlendFunction::ReverseSubtract: return SCE_GXM_BLEND_FUNC_REVERSE_SUBTRACT;
        case BlendFunction::Min: return SCE_GXM_BLEND_FUNC_MIN;
        case BlendFunction::Max: return SCE_GXM_BLEND_FUNC_MAX;
        default: return SCE_GXM_BLEND_FUNC_ADD;
    }
}
bool isOpaque(const BlendState* b) {
    return b->colorSrc == Blend::One && b->colorDst == Blend::Zero && b->alphaSrc == Blend::One && b->alphaDst == Blend::Zero;
}
SceGxmFragmentProgram* fragmentProgram(GxmProgram* p, const BlendState* b) {
    if (!b) b = BlendState::AlphaBlend;
    bool opaque = isOpaque(b);
    uint32_t key = opaque ? 0xFFFFFFFFu
                          : ((uint32_t)b->colorSrc | (uint32_t)b->colorDst << 4 | (uint32_t)b->alphaSrc << 8 | (uint32_t)b->alphaDst << 12 |
                             (uint32_t)b->colorFn << 16 | (uint32_t)b->alphaFn << 20);
    for (auto& f : p->fps)
        if (f.first == key) return f.second;
    SceGxmBlendInfo bi{};
    bi.colorMask = SCE_GXM_COLOR_MASK_ALL;
    bi.colorFunc = gxmFunc(b->colorFn);
    bi.alphaFunc = gxmFunc(b->alphaFn);
    bi.colorSrc = gxmFactor(b->colorSrc);
    bi.colorDst = gxmFactor(b->colorDst);
    bi.alphaSrc = gxmFactor(b->alphaSrc);
    bi.alphaDst = gxmFactor(b->alphaDst);
    SceGxmFragmentProgram* fp = nullptr;
    int r = sceGxmShaderPatcherCreateFragmentProgram(g_patcher, p->fsId, SCE_GXM_OUTPUT_REGISTER_FORMAT_UCHAR4,
                                                     SCE_GXM_MULTISAMPLE_NONE, opaque ? nullptr : &bi, p->vsGxp, &fp);
    if (r < 0) fw::plat::logf("fragment program failed: 0x%08X\n", (unsigned)r);
    p->fps.push_back({key, fp});
    return fp;
}

const AttrSpec kQuadAttrs[] = {{"a_pos", 0, SCE_GXM_ATTRIBUTE_FORMAT_F32, 2},
                               {"a_color", 8, SCE_GXM_ATTRIBUTE_FORMAT_U8N, 4},
                               {"a_tex0", 12, SCE_GXM_ATTRIBUTE_FORMAT_F32, 2}};

GxmProgram* makeProgram(const char* vsName, const char* vsSrc, const char* fsName, const char* fsSrc, const EffectDef* def,
                        bool quadLayout = true) {
    Registered vs = registerShader(vsName, vsSrc, false), fs = registerShader(fsName, fsSrc, true);
    if (!vs.gxp || !fs.gxp) return nullptr;
    auto* p = new GxmProgram();
    p->def = def;
    p->vsGxp = vs.gxp;
    p->fsGxp = fs.gxp;
    p->vsId = vs.id;
    p->fsId = fs.id;
    p->uMatrix = sceGxmProgramFindParameterByName(vs.gxp, "u_matrix");
    p->uGlvp = sceGxmProgramFindParameterByName(vs.gxp, "u_glvp");
    p->uFcsign = sceGxmProgramFindParameterByName(vs.gxp, "u_fcsign");
    p->vsUniforms = sceGxmProgramGetDefaultUniformBufferSize(vs.gxp) > 0;
    p->fsUniforms = sceGxmProgramGetDefaultUniformBufferSize(fs.gxp) > 0;
    if (def)
        for (auto& pd : def->params) {
            bool tex = pd.kind == ParamDef::Tex;
            p->vsParams.push_back(tex ? nullptr : sceGxmProgramFindParameterByName(vs.gxp, pd.name));
            p->fsParams.push_back(tex ? nullptr : sceGxmProgramFindParameterByName(fs.gxp, pd.name));
        }
    // Custom vertex shaders (shadows) take extra attributes and are only drawn through drawIndexed.
    if (quadLayout) p->vpQuad = createVertexProgram(p, kQuadAttrs, 3, sizeof(Vertex));
    return p;
}

// ---------------------------------------------------------------- textures
struct GxmTex {
    uint8_t* mem = nullptr;
    int stride = 0;  // pixels
    size_t bytes = 0;
    SceGxmTexture tex;
    SceGxmColorSurface surf;
    SceGxmRenderTarget* rt = nullptr;
};
GxmTex* texOf(Texture* t) { return t ? (GxmTex*)t->be : nullptr; }
GxmTex g_white;

// ---------------------------------------------------------------- scene state
RenderTarget2D* g_cur = nullptr;  // null = back buffer
bool g_sceneOpen = false;
int g_vp[4] = {0, 0, kW, kH};
const SceGxmVertexProgram* g_lastVp = nullptr;
const SceGxmFragmentProgram* g_lastFp = nullptr;
bool g_regionFull = true;
struct SceneTime {
    int w, h;
    float ms;
};
std::vector<SceneTime> g_sceneTimes;
// Scene trace of the last frames ("WxH:draws:programs"), logged when the GPU stops finishing frames.
std::string g_frameTrace, g_frameTraces[3];
int g_sceneDraws = 0;
std::string g_scenePrograms;
void traceProgram(const GxmProgram* p) {
    const char* n = p->def ? p->def->name : "sprite";
    if (g_scenePrograms.size() < 160 && g_scenePrograms.find(n) == std::string::npos) {
        g_scenePrograms += ':';
        g_scenePrograms += n;
    }
}
bool finiteMatrix(const Matrix& m) {
    const float* f = &m.M11;
    for (int i = 0; i < 16; i++)
        if (!std::isfinite(f[i])) return false;
    return true;
}
// Non-finite or absurdly large positions are dropped: they are a known way to hang the tiler.
bool saneXY(float x, float y) { return std::isfinite(x) && std::isfinite(y) && std::fabs(x) < 1.0e7f && std::fabs(y) < 1.0e7f; }
bool g_badMatrixLogged = false, g_badVertexLogged = false, g_badIndexLogged = false;
void logOnce(bool& done, const char* msg) {
    if (done) return;
    done = true;
    fw::plat::logf("%s\n", msg);
}

int curWidth() { return g_cur ? g_cur->width : kW; }
int curHeight() { return g_cur ? g_cur->height : kH; }

void applyViewport() {
    sceGxmSetViewport(g_ctx, g_vp[0] + g_vp[2] * 0.5f, g_vp[2] * 0.5f, g_vp[1] + g_vp[3] * 0.5f, -g_vp[3] * 0.5f, 0.5f, 0.5f);
}
void setRegionFull() {
    sceGxmSetRegionClip(g_ctx, SCE_GXM_REGION_CLIP_OUTSIDE, 0, 0, curWidth() - 1, curHeight() - 1);
    g_regionFull = true;
}

void beginScene() {
    if (g_cur) {
        GxmTex* g = texOf(g_cur);
        sceGxmBeginScene(g_ctx, 0, g->rt, nullptr, nullptr, nullptr, &g->surf, nullptr);
    } else {
        sceGxmBeginScene(g_ctx, 0, g_displayRT, nullptr, nullptr, g_dispSync[g_back], &g_dispSurf[g_back], nullptr);
    }
    g_sceneOpen = true;
    sceGxmSetCullMode(g_ctx, SCE_GXM_CULL_NONE);
    sceGxmSetFrontDepthFunc(g_ctx, SCE_GXM_DEPTH_FUNC_ALWAYS);
    sceGxmSetBackDepthFunc(g_ctx, SCE_GXM_DEPTH_FUNC_ALWAYS);
    sceGxmSetFrontDepthWriteEnable(g_ctx, SCE_GXM_DEPTH_WRITE_DISABLED);
    sceGxmSetBackDepthWriteEnable(g_ctx, SCE_GXM_DEPTH_WRITE_DISABLED);
    applyViewport();
    setRegionFull();
    g_lastVp = nullptr;
    g_lastFp = nullptr;
}
void endScene(const SceGxmNotification* fragmentNotify = nullptr) {
    if (!g_sceneOpen) return;
    sceGxmEndScene(g_ctx, nullptr, fragmentNotify);
    g_sceneOpen = false;
    char buf[48];
    std::snprintf(buf, sizeof buf, " %dx%d:%d", curWidth(), curHeight(), g_sceneDraws);
    g_frameTrace += buf;
    g_frameTrace += g_scenePrograms;
    g_sceneDraws = 0;
    g_scenePrograms.clear();
    if (debugMode == 3) {
        // Serialises CPU and GPU, so only the relative cost per scene is meaningful.
        double t0 = fw::plat::timeSeconds();
        sceGxmFinish(g_ctx);
        g_sceneTimes.push_back({curWidth(), curHeight(), (float)((fw::plat::timeSeconds() - t0) * 1000.0)});
    }
}
void ensureScene() {
    if (!g_sceneOpen) beginScene();
}

void bindPrograms(SceGxmVertexProgram* vp, SceGxmFragmentProgram* fp) {
    if (vp != g_lastVp) {
        sceGxmSetVertexProgram(g_ctx, vp);
        g_lastVp = vp;
    }
    if (fp != g_lastFp) {
        sceGxmSetFragmentProgram(g_ctx, fp);
        g_lastFp = fp;
    }
}

int paramComponents(ParamDef::Kind k) {
    switch (k) {
        case ParamDef::Vec2: return 2;
        case ParamDef::Vec3: return 3;
        case ParamDef::Vec4: return 4;
        case ParamDef::Mat4: return 16;
        default: return 1;
    }
}

void setUniforms(GxmProgram* p, const DrawState& ds) {
    void* vu = nullptr;
    void* fu = nullptr;
    if (p->vsUniforms) {
        sceGxmReserveVertexDefaultUniformBuffer(g_ctx, &vu);
        if (p->uMatrix) sceGxmSetUniformDataF(vu, p->uMatrix, 0, 16, &ds.matrix.M11);
        if (p->uGlvp) {
            // gl_FragCoord-style viewport: origin bottom-left on the back buffer, top-left in render targets (MonoGame GL).
            bool rtLike = g_cur || backBufferAsTarget;
            float glvp[4] = {(float)g_vp[0], (float)(rtLike ? g_vp[1] : curHeight() - (g_vp[1] + g_vp[3])), (float)g_vp[2], (float)g_vp[3]};
            sceGxmSetUniformDataF(vu, p->uGlvp, 0, 4, glvp);
        }
        if (p->uFcsign) {
            float s = g_cur || backBufferAsTarget ? -1.0f : 1.0f;
            sceGxmSetUniformDataF(vu, p->uFcsign, 0, 1, &s);
        }
    }
    if (p->fsUniforms) sceGxmReserveFragmentDefaultUniformBuffer(g_ctx, &fu);
    if (p->def && ds.effect) {
        auto* list = ds.effect->parameters->list;
        for (int32_t i = 0; i < list->size; i++) {
            EffectParameter* ep = list->items->data[i];
            if (ep->index < 0 || ep->index >= (int)p->def->params.size()) continue;
            int n = paramComponents(p->def->params[ep->index].kind);
            if (vu && p->vsParams[ep->index]) sceGxmSetUniformDataF(vu, p->vsParams[ep->index], 0, n, ep->value);
            if (fu && p->fsParams[ep->index]) sceGxmSetUniformDataF(fu, p->fsParams[ep->index], 0, n, ep->value);
        }
    }
}

void setTextures(const DrawState& ds) {
    for (int u = 0; u < 4; u++) {
        GxmTex* g = texOf(ds.tex[u]);
        SceGxmTexture t = g ? g->tex : g_white.tex;
        SamplerState* s = ds.samp[u] ? ds.samp[u] : SamplerState::LinearClamp;
        SceGxmTextureFilter filter = s->filter == TextureFilter::Point ? SCE_GXM_TEXTURE_FILTER_POINT : SCE_GXM_TEXTURE_FILTER_LINEAR;
        sceGxmTextureSetMinFilter(&t, filter);
        sceGxmTextureSetMagFilter(&t, filter);
        auto addr = [](TextureAddressMode m) {
            return m == TextureAddressMode::Wrap ? SCE_GXM_TEXTURE_ADDR_REPEAT
                   : m == TextureAddressMode::Mirror ? SCE_GXM_TEXTURE_ADDR_MIRROR
                                                     : SCE_GXM_TEXTURE_ADDR_CLAMP;
        };
        if (sceGxmTextureSetUAddrMode(&t, addr(s->addressU)) < 0) sceGxmTextureSetUAddrMode(&t, SCE_GXM_TEXTURE_ADDR_CLAMP);
        if (sceGxmTextureSetVAddrMode(&t, addr(s->addressV)) < 0) sceGxmTextureSetVAddrMode(&t, SCE_GXM_TEXTURE_ADDR_CLAMP);
        sceGxmSetFragmentTexture(g_ctx, u, &t);
    }
}

void setScissor(const DrawState& ds) {
    if (!ds.scissor) {
        if (!g_regionFull) setRegionFull();
        return;
    }
    const Rectangle& r = ds.scissorRect;
    // Region clip coordinates outside the surface can hang the GPU, so clamp them.
    int x0 = std::max(0, r.X), y0 = std::max(0, r.Y);
    int x1 = std::min(curWidth(), r.X + r.Width) - 1, y1 = std::min(curHeight(), r.Y + r.Height) - 1;
    if (x1 < x0 || y1 < y0) sceGxmSetRegionClip(g_ctx, SCE_GXM_REGION_CLIP_ALL, 0, 0, 0, 0);
    else sceGxmSetRegionClip(g_ctx, SCE_GXM_REGION_CLIP_OUTSIDE, x0, y0, x1, y1);
    g_regionFull = false;
}

void drawQuadsRaw(const Vertex* v, int quads) {
    g_sceneDraws++;
    size_t bytes = (size_t)quads * 4 * sizeof(Vertex);
    auto* dst = (uint8_t*)ringAlloc(bytes);
    if (!dst) return;
    std::memcpy(dst, v, bytes);
    for (int done = 0; done < quads;) {
        int n = std::min(quads - done, kMaxQuadsPerDraw);
        sceGxmSetVertexStream(g_ctx, 0, dst + (size_t)done * 4 * sizeof(Vertex));
        sceGxmDraw(g_ctx, SCE_GXM_PRIMITIVE_TRIANGLES, SCE_GXM_INDEX_FORMAT_U16, g_quadIdx, n * 6);
        done += n;
    }
}

GxmProgram* g_sprite = nullptr;
struct PendingTarget {
    SceGxmRenderTarget* rt;
    uint32_t size;  // width << 16 | height
    uint64_t frame;
};
std::vector<PendingTarget> g_pendingTargets;
// Released render-target objects are reused for new targets of the same size. Destroying them at runtime
// failed with SCE_GXM_ERROR_DRIVER and hung the GPU (and the whole system) right after.
std::multimap<uint32_t, SceGxmRenderTarget*> g_freeTargets;

void frameBegin() {
    g_frame++;
    int half = (int)(g_frame & 1);
    // The ring half and deferred frees from two frames ago are reusable once the GPU signalled that frame.
    double waitStart = fw::plat::timeSeconds();
    bool reported = false;
    while (g_notify[half] == 0) {
        sceKernelDelayThread(100);
        if (!reported && fw::plat::timeSeconds() - waitStart > 2.0) {
            reported = true;
            fw::plat::logf("gpu: frame %u still not finished after 2 s (GPU hang?)\n", (unsigned)(g_frame - 2));
            for (uint64_t f = g_frame >= 3 ? g_frame - 3 : 0; f < g_frame; f++)
                fw::plat::logf("  frame %u scenes:%s\n", (unsigned)f, g_frameTraces[f % 3].c_str());
        }
    }
    g_ringPos = 0;
    std::vector<void*> ready;
    {
        std::lock_guard<std::mutex> lk(g_memMutex);
        size_t keep = 0;
        for (auto& f : g_pendingFrees) {
            if (f.frame + 2 <= g_frame) ready.push_back(f.ptr);
            else g_pendingFrees[keep++] = f;
        }
        g_pendingFrees.resize(keep);
    }
    for (void* p : ready) gpuFreeNow(p);
    size_t keep = 0;
    for (auto& t : g_pendingTargets) {
        if (t.frame + 2 <= g_frame) g_freeTargets.insert({t.size, t.rt});
        else g_pendingTargets[keep++] = t;
    }
    g_pendingTargets.resize(keep);
}

void framePresent() {
    ensureScene();
    int half = (int)(g_frame & 1);
    g_notify[half] = 0;
    endScene(&g_frameDone[half]);
    g_frameTraces[g_frame % 3] = std::move(g_frameTrace);
    g_frameTrace.clear();
    if (debugMode == 3 && fw::plat::diagnosticsEnabled() && g_frame % 60 == 0) {
        std::string line = "gpu scenes:";
        float total = 0;
        char buf[48];
        for (auto& s : g_sceneTimes) {
            std::snprintf(buf, sizeof buf, " %dx%d=%.2f", s.w, s.h, s.ms);
            line += buf;
            total += s.ms;
        }
        fw::plat::logf("%s | total %.2f ms\n", line.c_str(), total);
    }
    g_sceneTimes.clear();
    DisplayData d{g_dispMem[g_back], g_swapInterval};
    sceGxmDisplayQueueAddEntry(g_dispSync[g_front], g_dispSync[g_back], &d);
    g_front = g_back;
    g_back = (g_back + 1) % kBuffers;
}

// ---------------------------------------------------------------- loading spinner
// When the main thread has not presented for a while (startup, map loads), this thread redraws the last
// game frame with an animated spinner in the lower right corner.
constexpr double kSpinnerDelay = 0.25;
volatile double g_lastPresent = 0;
double g_gpuWait = 0;  // time beBeginFrame spent waiting for the GPU this frame
volatile bool g_gamePresented = false;
volatile bool g_spinnerRun = false;
constexpr int kMaxSpinnerFrames = 64;
SceGxmTexture g_spinnerTex[kMaxSpinnerFrames];
int g_spinnerW = 0, g_spinnerH = 0, g_spinnerFrames = 0, g_spinnerMs = 50;
uint8_t* g_snapshot = nullptr;  // copy of the last game frame shown behind the spinner
SceGxmTexture g_snapshotTex;
bool g_spinnerActive = false;

bool loadSpinner() {
    FILE* f = std::fopen("app0:ui/spinner.bin", "rb");
    if (!f) return false;
    char magic[4];
    uint16_t hdr[4];
    bool ok = std::fread(magic, 1, 4, f) == 4 && std::memcmp(magic, "SPN1", 4) == 0 && std::fread(hdr, 2, 4, f) == 4;
    uint8_t* mem = nullptr;
    if (ok) {
        size_t bytes = (size_t)hdr[0] * hdr[1] * hdr[2] * 4;
        mem = (uint8_t*)gpuAlloc(bytes, 256);
        ok = mem && std::fread(mem, 1, bytes, f) == bytes;
    }
    std::fclose(f);
    if (!ok) return false;
    g_spinnerW = hdr[0];
    g_spinnerH = hdr[1];
    g_spinnerFrames = std::min<int>(hdr[2], kMaxSpinnerFrames);
    g_spinnerMs = hdr[3] ? hdr[3] : 50;
    // One texture per frame: all frames stacked in one texture exceed the GPU's 4096 px size limit.
    const size_t frameBytes = (size_t)g_spinnerW * g_spinnerH * 4;
    for (int i = 0; i < g_spinnerFrames; i++) {
        if (sceGxmTextureInitLinear(&g_spinnerTex[i], mem + i * frameBytes, SCE_GXM_TEXTURE_FORMAT_U8U8U8U8_ABGR,
                                    g_spinnerW, g_spinnerH, 0) < 0) {
            gpuFree(mem);
            return false;
        }
        sceGxmTextureSetMinFilter(&g_spinnerTex[i], SCE_GXM_TEXTURE_FILTER_POINT);
        sceGxmTextureSetMagFilter(&g_spinnerTex[i], SCE_GXM_TEXTURE_FILTER_POINT);
    }
    g_snapshot = (uint8_t*)gpuAlloc((size_t)4 * kStride * kH, 256);
    if (!g_snapshot) return false;
    std::memset(g_snapshot, 0, (size_t)4 * kStride * kH);
    sceGxmTextureInitLinear(&g_snapshotTex, g_snapshot, SCE_GXM_TEXTURE_FORMAT_U8U8U8U8_ABGR, kW, kH, 0);
    sceGxmTextureSetMinFilter(&g_snapshotTex, SCE_GXM_TEXTURE_FILTER_POINT);
    sceGxmTextureSetMagFilter(&g_snapshotTex, SCE_GXM_TEXTURE_FILTER_POINT);
    return true;
}

void drawTexturedQuad(const SceGxmTexture* tex, float x, float y, float w, float h, float v0, float v1, bool blend) {
    static const float ortho[16] = {2.0f / kW, 0, 0, 0, 0, -2.0f / kH, 0, 0, 0, 0, 1, 0, -1, 1, 0, 1};
    SceGxmFragmentProgram* fp = fragmentProgram(g_sprite, blend ? BlendState::AlphaBlend : BlendState::Opaque);
    if (!fp) return;
    bindPrograms(g_sprite->vpQuad, fp);
    void* vu = nullptr;
    sceGxmReserveVertexDefaultUniformBuffer(g_ctx, &vu);
    sceGxmSetUniformDataF(vu, g_sprite->uMatrix, 0, 16, ortho);
    sceGxmSetFragmentTexture(g_ctx, 0, tex);
    const uint32_t c = 0xFFFFFFFFu;
    const Vertex q[4] = {{x, y, c, 0, v0}, {x + w, y, c, 1, v0}, {x, y + h, c, 0, v1}, {x + w, y + h, c, 1, v1}};
    drawQuadsRaw(q, 1);
}

void spinnerFrame(double now) {
    if (!g_spinnerActive) {
        // The front buffer holds the last finished frame; keep a copy so the spinner never draws over itself.
        if (g_gamePresented) std::memcpy(g_snapshot, g_dispMem[g_front], (size_t)4 * kStride * kH);
        g_spinnerActive = true;
    }
    RenderTarget2D* savedCur = g_cur;
    int savedVp[4] = {g_vp[0], g_vp[1], g_vp[2], g_vp[3]};
    endScene();
    frameBegin();
    g_cur = nullptr;
    g_vp[0] = g_vp[1] = 0;
    g_vp[2] = kW;
    g_vp[3] = kH;
    beginScene();
    drawTexturedQuad(&g_snapshotTex, 0, 0, kW, kH, 0, 1, false);
    int frame = (int)(now * 1000.0 / g_spinnerMs) % g_spinnerFrames;
    // Drawn 1:1 (the importer renders it at the UI scale) where the game shows its save icon: 16 px * scale 4.
    const float sw = g_spinnerW, sh = g_spinnerH, pad = 64.0f;
    drawTexturedQuad(&g_spinnerTex[frame], kW - sw - pad, kH - sh - pad, sw, sh, 0, 1, true);
    framePresent();
    g_cur = savedCur;
    std::memcpy(g_vp, savedVp, sizeof savedVp);
}

int spinnerMain(SceSize, void*) {
    // Startup only: once the game has presented smoothly for a second, in-game loading shows just its save icon.
    double smoothSince = 0;
    while (g_spinnerRun) {
        sceKernelDelayThread(16 * 1000);
        double now = fw::plat::timeSeconds();
        if (g_gamePresented && now - g_lastPresent < kSpinnerDelay) {
            g_spinnerActive = false;
            if (smoothSince == 0) smoothSince = now;
            if (now - smoothSince > 1.0) break;
            continue;
        }
        smoothSince = 0;
        std::unique_lock<std::recursive_mutex> lk(g_gpuMutex, std::try_to_lock);
        if (!lk.owns_lock() || (g_gamePresented && fw::plat::timeSeconds() - g_lastPresent < kSpinnerDelay)) continue;
        spinnerFrame(now);
    }
    gpuFree(sceGxmTextureGetData(&g_spinnerTex[0]));
    gpuFree(g_snapshot);
    return 0;
}

void startSpinner() {
    if (!g_sprite || !g_sprite->uMatrix || !loadSpinner()) {
        fw::plat::logf("loading spinner unavailable\n");
        return;
    }
    fragmentProgram(g_sprite, BlendState::AlphaBlend);
    fragmentProgram(g_sprite, BlendState::Opaque);
    g_spinnerRun = true;
    SceUID th = sceKernelCreateThread("ladx_spinner", spinnerMain, 0x10000100, 0x10000, 0, SCE_KERNEL_CPU_MASK_USER_1, nullptr);
    if (th >= 0) sceKernelStartThread(th, 0, nullptr);
}

}  // namespace

// ---------------------------------------------------------------- backend API
void beInit(int, int) {
    SceGxmInitializeParams ip{};
    ip.flags = 0;
    ip.displayQueueMaxPendingCount = kBuffers - 1;
    ip.displayQueueCallback = displayCallback;
    ip.displayQueueCallbackDataSize = sizeof(DisplayData);
    ip.parameterBufferSize = SCE_GXM_DEFAULT_PARAMETER_BUFFER_SIZE;
    int r = sceGxmInitialize(&ip);
    if (r < 0) fw::plat::logf("sceGxmInitialize failed: 0x%08X\n", (unsigned)r);

    SceUID uid;
    const auto R = SCE_GXM_MEMORY_ATTRIB_READ;
    const auto RW = (SceGxmMemoryAttribFlags)(SCE_GXM_MEMORY_ATTRIB_READ | SCE_GXM_MEMORY_ATTRIB_WRITE);
    void* vdm = mapBlock(SCE_KERNEL_MEMBLOCK_TYPE_USER_RW_UNCACHE, SCE_GXM_DEFAULT_VDM_RING_BUFFER_SIZE, R, &uid);
    void* vring = mapBlock(SCE_KERNEL_MEMBLOCK_TYPE_USER_RW_UNCACHE, 2 * SCE_GXM_DEFAULT_VERTEX_RING_BUFFER_SIZE, R, &uid);
    void* fring = mapBlock(SCE_KERNEL_MEMBLOCK_TYPE_USER_RW_UNCACHE, 2 * SCE_GXM_DEFAULT_FRAGMENT_RING_BUFFER_SIZE, R, &uid);
    unsigned fusseOffset = 0;
    void* fusse = mapUsse(SCE_GXM_DEFAULT_FRAGMENT_USSE_RING_BUFFER_SIZE, true, &fusseOffset);
    SceGxmContextParams cp{};
    cp.hostMem = std::malloc(SCE_GXM_MINIMUM_CONTEXT_HOST_MEM_SIZE);
    cp.hostMemSize = SCE_GXM_MINIMUM_CONTEXT_HOST_MEM_SIZE;
    cp.vdmRingBufferMem = vdm;
    cp.vdmRingBufferMemSize = SCE_GXM_DEFAULT_VDM_RING_BUFFER_SIZE;
    cp.vertexRingBufferMem = vring;
    cp.vertexRingBufferMemSize = 2 * SCE_GXM_DEFAULT_VERTEX_RING_BUFFER_SIZE;
    cp.fragmentRingBufferMem = fring;
    cp.fragmentRingBufferMemSize = 2 * SCE_GXM_DEFAULT_FRAGMENT_RING_BUFFER_SIZE;
    cp.fragmentUsseRingBufferMem = fusse;
    cp.fragmentUsseRingBufferMemSize = SCE_GXM_DEFAULT_FRAGMENT_USSE_RING_BUFFER_SIZE;
    cp.fragmentUsseRingBufferOffset = fusseOffset;
    r = sceGxmCreateContext(&cp, &g_ctx);
    if (r < 0) fw::plat::logf("sceGxmCreateContext failed: 0x%08X\n", (unsigned)r);

    SceGxmRenderTargetParams rp{};
    rp.flags = 0;
    rp.width = kW;
    rp.height = kH;
    rp.scenesPerFrame = 8;
    rp.multisampleMode = SCE_GXM_MULTISAMPLE_NONE;
    rp.multisampleLocations = 0;
    rp.driverMemBlock = -1;
    sceGxmCreateRenderTarget(&rp, &g_displayRT);
    for (int i = 0; i < kBuffers; i++) {
        g_dispMem[i] = mapBlock(SCE_KERNEL_MEMBLOCK_TYPE_USER_CDRAM_RW, (size_t)4 * kStride * kH, RW, &uid);
        std::memset(g_dispMem[i], 0, (size_t)4 * kStride * kH);
        sceGxmColorSurfaceInit(&g_dispSurf[i], SCE_GXM_COLOR_FORMAT_A8B8G8R8, SCE_GXM_COLOR_SURFACE_LINEAR,
                               SCE_GXM_COLOR_SURFACE_SCALE_NONE, SCE_GXM_OUTPUT_REGISTER_SIZE_32BIT, kW, kH, kStride, g_dispMem[i]);
        sceGxmSyncObjectCreate(&g_dispSync[i]);
    }

    const size_t patcherSize = 256 * 1024, usseSize = 256 * 1024;
    unsigned vusseOffset = 0, fpusseOffset = 0;
    void* patcherBuf = mapBlock(SCE_KERNEL_MEMBLOCK_TYPE_USER_RW_UNCACHE, patcherSize, R, &uid);
    void* vusse = mapUsse(usseSize, false, &vusseOffset);
    void* fpusse = mapUsse(usseSize, true, &fpusseOffset);
    SceGxmShaderPatcherParams pp{};
    pp.hostAllocCallback = patcherAlloc;
    pp.hostFreeCallback = patcherFree;
    pp.bufferMem = patcherBuf;
    pp.bufferMemSize = patcherSize;
    pp.vertexUsseMem = vusse;
    pp.vertexUsseMemSize = usseSize;
    pp.vertexUsseOffset = vusseOffset;
    pp.fragmentUsseMem = fpusse;
    pp.fragmentUsseMemSize = usseSize;
    pp.fragmentUsseOffset = fpusseOffset;
    r = sceGxmShaderPatcherCreate(&pp, &g_patcher);
    if (r < 0) fw::plat::logf("sceGxmShaderPatcherCreate failed: 0x%08X\n", (unsigned)r);

    g_notify = sceGxmGetNotificationRegion();
    for (int k = 0; k < 2; k++) {
        g_frameDone[k].address = g_notify + k;
        g_frameDone[k].value = 1;
        g_notify[k] = 1;
        g_ring[k] = (uint8_t*)mapBlock(SCE_KERNEL_MEMBLOCK_TYPE_USER_RW_UNCACHE, kRingSize, R, &uid);
    }
    g_quadIdx = (uint16_t*)gpuAlloc((size_t)kMaxQuadsPerDraw * 6 * 2, 16);
    for (int i = 0; i < kMaxQuadsPerDraw; i++) {
        uint16_t b = (uint16_t)(i * 4);
        uint16_t* q = g_quadIdx + i * 6;
        q[0] = b;
        q[1] = b + 1;
        q[2] = b + 2;
        q[3] = b + 1;
        q[4] = b + 3;
        q[5] = b + 2;
    }
    g_white.mem = (uint8_t*)gpuAlloc(8 * 8 * 4, 256);
    std::memset(g_white.mem, 0xFF, 8 * 8 * 4);
    sceGxmTextureInitLinear(&g_white.tex, g_white.mem, SCE_GXM_TEXTURE_FORMAT_U8U8U8U8_ABGR, 8, 8, 0);
    beSpriteProgram();
    startSpinner();
}

void beBeginFrame() {
    g_gpuMutex.lock();  // released in bePresent
    double t0 = fw::plat::timeSeconds();
    frameBegin();
    g_gpuWait += fw::plat::timeSeconds() - t0;
}

// 60 fps while frames really make every refresh, otherwise an even 30 fps. At 30 fps the waits come from the
// pacing itself, so whether 60 fits can only be found out by trying it; failed tries back off up to 30 s.
int g_lastVblanks = 1;  // refreshes between the last two frames
int g_pacingHold = 120;  // frames left in which misses are ignored (map transitions; startup decodes and targets)
static void updateSwapInterval(double workMs) {
    static int window = 0, missed = 0, goodWindows = 0, countdown = 90, retryFrames = 90;
    int before = g_swapInterval;
    if (g_pacingHold > 0) {
        // Loads, collections and first draws of a new map say nothing about whether the map itself fits 60 fps;
        // earlier failed tries were for another scene, so the next try comes soon.
        g_pacingHold--;
        window = missed = 0;
        retryFrames = 90;
        countdown = std::min(countdown, 30);
        return;
    }
    if (g_swapInterval == 1) {
        // Only steady 2-3 refresh frames mean overload; single long frames are hitches.
        missed += g_lastVblanks == 2 || g_lastVblanks == 3;
        if (++window == 60) {
            if (missed >= 4) {
                g_swapInterval = 2;
                countdown = retryFrames;
                retryFrames = std::min(retryFrames * 2, 900);
                goodWindows = 0;
            } else if (++goodWindows == 10) {
                retryFrames = 90;
            }
            window = missed = 0;
        }
    } else if (workMs < 20.0 && --countdown <= 0) {
        g_swapInterval = 1;
        window = missed = 0;
    }
    if (g_swapInterval != before && fw::plat::diagnosticsEnabled())
        fw::plat::logf("pacing: %d fps\n", 60 / g_swapInterval);
}

void bePresent() {
    static double lastPresentEnd = 0;
    double start = fw::plat::timeSeconds();
    if (lastPresentEnd > 0) updateSwapInterval((start - lastPresentEnd - g_gpuWait) * 1000.0);
    g_gpuWait = 0;
    framePresent();
    g_lastPresent = lastPresentEnd = fw::plat::timeSeconds();
    g_gamePresented = true;
    g_gpuMutex.unlock();
}

// Whole refreshes since the previous frame, so game time matches how long frames are actually shown.
double beFrameDelta(double) {
    static int last = -1;
    int now = (int)sceDisplayGetVcount();
    int n = last < 0 ? g_swapInterval : now - last;
    last = now;
    g_lastVblanks = n < 1 ? 1 : n;
    return (n < 1 ? 1 : n) / 60.0;
}

void beCreateTexture(Texture* t, int w, int h) {
    auto* g = new GxmTex();
    g->stride = (w + 7) & ~7;
    g->bytes = (size_t)g->stride * h * 4;
    g->mem = (uint8_t*)gpuAlloc(g->bytes, 256);
    if (g->mem) {
        std::memset(g->mem, 0, g->bytes);
        sceGxmTextureInitLinear(&g->tex, g->mem, SCE_GXM_TEXTURE_FORMAT_U8U8U8U8_ABGR, w, h, 0);
    } else {
        g->tex = g_white.tex;
    }
    t->be = g;
    textureBytes += g->bytes;
}
void beReleaseTexture(Texture* t) {
    GpuLock lock(g_gpuMutex);
    GxmTex* g = texOf(t);
    if (!g) return;
    if (g_cur == t) g_cur = nullptr;
    textureBytes -= g->bytes;
    gpuFree(g->mem);
    // Queued scenes may still use the render-target object, so it is destroyed with the same delay as the memory.
    if (g->rt) g_pendingTargets.push_back({g->rt, (uint32_t)t->width << 16 | (uint32_t)t->height, g_frame});
    delete g;
    t->be = nullptr;
}
void beUpload(Texture* t, int x, int y, int w, int h, const void* rgba) {
    GxmTex* g = texOf(t);
    if (!g || !g->mem) return;
    auto* src = (const uint8_t*)rgba;
    if (x == 0 && w == g->stride) {
        std::memcpy(g->mem + (size_t)y * g->stride * 4, src, (size_t)w * h * 4);
        return;
    }
    for (int row = 0; row < h; row++)
        std::memcpy(g->mem + ((size_t)(y + row) * g->stride + x) * 4, src + (size_t)row * w * 4, (size_t)w * 4);
}
void beDownload(Texture* t, int x, int y, int w, int h, void* rgba) {
    GpuLock lock(g_gpuMutex);
    GxmTex* g = texOf(t);
    if (!g || !g->mem) return;
    endScene();
    sceGxmFinish(g_ctx);
    auto* dst = (uint8_t*)rgba;
    for (int row = 0; row < h; row++)
        std::memcpy(dst + (size_t)row * w * 4, g->mem + ((size_t)(y + row) * g->stride + x) * 4, (size_t)w * 4);
}
void beCreateTarget(RenderTarget2D* rt) {
    GpuLock lock(g_gpuMutex);
    double t0 = fw::plat::timeSeconds();
    beCreateTexture(rt, rt->width, rt->height);
    GxmTex* g = texOf(rt);
    if (!g->mem) return;
    sceGxmColorSurfaceInit(&g->surf, SCE_GXM_COLOR_FORMAT_A8B8G8R8, SCE_GXM_COLOR_SURFACE_LINEAR, SCE_GXM_COLOR_SURFACE_SCALE_NONE,
                           SCE_GXM_OUTPUT_REGISTER_SIZE_32BIT, rt->width, rt->height, g->stride, g->mem);
    uint32_t key = (uint32_t)rt->width << 16 | (uint32_t)rt->height;
    auto it = g_freeTargets.find(key);
    if (it != g_freeTargets.end()) {
        g->rt = it->second;
        g_freeTargets.erase(it);
        return;
    }
    SceGxmRenderTargetParams rp{};
    rp.flags = 0;
    rp.width = (uint16_t)rt->width;
    rp.height = (uint16_t)rt->height;
    rp.scenesPerFrame = 8;
    rp.multisampleMode = SCE_GXM_MULTISAMPLE_NONE;
    rp.multisampleLocations = 0;
    rp.driverMemBlock = -1;
    int r = sceGxmCreateRenderTarget(&rp, &g->rt);
    if (r < 0) fw::plat::logf("render target %dx%d failed: 0x%08X\n", rt->width, rt->height, (unsigned)r);
    if (fw::plat::diagnosticsEnabled()) {
        unsigned driverMem = 0;
        sceGxmGetRenderTargetMemSize(&rp, &driverMem);
        fw::plat::logf("gxm render target %dx%d: new, %u KB driver memory (%u free for reuse), %.1f ms\n", rt->width,
                       rt->height, driverMem / 1024, (unsigned)g_freeTargets.size(), (fw::plat::timeSeconds() - t0) * 1000.0);
    }
}
void beReleaseTarget(RenderTarget2D* rt) { beReleaseTexture(rt); }

void beBindTarget(RenderTarget2D* rt) {
    GpuLock lock(g_gpuMutex);
    endScene();
    g_cur = rt && texOf(rt) && texOf(rt)->rt ? rt : nullptr;
    g_vp[0] = g_vp[1] = 0;
    g_vp[2] = curWidth();
    g_vp[3] = curHeight();
    beginScene();
}
void beSetViewport(int x, int y, int w, int h) {
    GpuLock lock(g_gpuMutex);
    g_vp[0] = x;
    g_vp[1] = y;
    g_vp[2] = w;
    g_vp[3] = h;
    if (g_sceneOpen) applyViewport();
}
void beClear(float r, float g, float b, float a) {
    GpuLock lock(g_gpuMutex);
    // The first clear can happen before any sprite was drawn, so make sure the program exists.
    auto* sprite = static_cast<GxmProgram*>(beSpriteProgram());
    if (!sprite || !sprite->vpQuad) return;
    SceGxmFragmentProgram* fp = fragmentProgram(sprite, BlendState::Opaque);
    if (!fp) return;
    ensureScene();
    int saved[4] = {g_vp[0], g_vp[1], g_vp[2], g_vp[3]};
    g_vp[0] = g_vp[1] = 0;
    g_vp[2] = curWidth();
    g_vp[3] = curHeight();
    applyViewport();
    if (!g_regionFull) setRegionFull();
    auto byte = [](float v) { return (uint32_t)(std::clamp(v, 0.0f, 1.0f) * 255.0f + 0.5f); };
    uint32_t c = byte(r) | byte(g) << 8 | byte(b) << 16 | byte(a) << 24;
    const Vertex quad[4] = {{-1, 1, c, 0, 0}, {1, 1, c, 1, 0}, {-1, -1, c, 0, 1}, {1, -1, c, 1, 1}};
    DrawState ds;
    ds.prog = sprite;
    ds.matrix = Matrix::get_Identity();
    ds.blend = BlendState::Opaque;
    bindPrograms(sprite->vpQuad, fp);
    setUniforms(sprite, ds);
    setTextures(ds);
    drawQuadsRaw(quad, 1);
    std::memcpy(g_vp, saved, sizeof saved);
    applyViewport();
}

void beDrawQuads(const DrawState& ds, const Vertex* v, int quads) {
    GpuLock lock(g_gpuMutex);
    auto* p = static_cast<GxmProgram*>(ds.prog);
    if (!p || !p->vpQuad) return;
    if (!finiteMatrix(ds.matrix)) return logOnce(g_badMatrixLogged, "gxm: dropped draw with a non-finite matrix");
    for (int i = 0; i < quads * 4; i++)
        if (!saneXY(v[i].x, v[i].y)) return logOnce(g_badVertexLogged, "gxm: dropped draw with invalid vertex positions");
    ensureScene();
    SceGxmFragmentProgram* fp = fragmentProgram(p, ds.blend);
    if (!fp) return;
    traceProgram(p);
    bindPrograms(p->vpQuad, fp);
    setUniforms(p, ds);
    setTextures(ds);
    setScissor(ds);
    drawQuadsRaw(v, quads);
}

void beDrawIndexed(const DrawState& ds, int stride, const void* verts, int nverts, const int16_t* idx, int prims,
                   const VertexDeclaration* decl) {
    GpuLock lock(g_gpuMutex);
    auto* p = static_cast<GxmProgram*>(ds.prog);
    if (!p) return;
    for (int i = 0; i < prims * 3; i++)
        if (idx[i] < 0 || idx[i] >= nverts) return logOnce(g_badIndexLogged, "gxm: dropped indexed draw with out-of-range indices");
    ensureScene();
    std::string key = std::to_string(stride);
    AttrSpec specs[8];
    int n = 0;
    static const char* const texNames[] = {"a_tex0", "a_tex1", "a_tex2", "a_tex3"};
    for (auto& el : *decl->elements) {
        if (n >= 8) break;
        AttrSpec s{nullptr, (uint16_t)el.offset, SCE_GXM_ATTRIBUTE_FORMAT_F32, 2};
        if (el.usage == VertexElementUsage::Position) s.name = "a_pos";
        else if (el.usage == VertexElementUsage::Color) s.name = "a_color";
        else if (el.usage == VertexElementUsage::TextureCoordinate && el.usageIndex < 4) s.name = texNames[el.usageIndex];
        if (!s.name) continue;
        switch (el.format) {
            case VertexElementFormat::Single: s.comps = 1; break;
            case VertexElementFormat::Vector3: s.comps = 3; break;
            case VertexElementFormat::Vector4: s.comps = 4; break;
            case VertexElementFormat::Color: s.format = SCE_GXM_ATTRIBUTE_FORMAT_U8N; s.comps = 4; break;
            default: break;
        }
        specs[n++] = s;
        key += "|" + std::string(s.name) + ":" + std::to_string(s.offset) + ":" + std::to_string(s.comps) + ":" + std::to_string((int)s.format);
    }
    SceGxmVertexProgram* vp = nullptr;
    for (auto& e : p->vpDecl)
        if (e.first == key) vp = e.second;
    if (!vp) {
        vp = createVertexProgram(p, specs, n, stride);
        p->vpDecl.push_back({key, vp});
    }
    SceGxmFragmentProgram* fp = fragmentProgram(p, ds.blend);
    if (!vp || !fp) return;
    for (int a = 0; a < n; a++) {
        const AttrSpec& s = specs[a];
        if (std::strcmp(s.name, "a_pos") != 0 || s.format != SCE_GXM_ATTRIBUTE_FORMAT_F32 || s.comps < 2) continue;
        for (int i = 0; i < nverts; i++) {
            float xy[2];
            std::memcpy(xy, (const uint8_t*)verts + (size_t)i * stride + s.offset, sizeof xy);
            if (!saneXY(xy[0], xy[1])) return logOnce(g_badVertexLogged, "gxm: dropped draw with invalid vertex positions");
        }
    }
    traceProgram(p);
    g_sceneDraws++;
    bindPrograms(vp, fp);
    setUniforms(p, ds);
    setTextures(ds);
    setScissor(ds);
    auto* vdst = (uint8_t*)ringAlloc((size_t)stride * nverts);
    auto* idst = (uint8_t*)ringAlloc((size_t)prims * 3 * 2);
    if (!vdst || !idst) return;
    std::memcpy(vdst, verts, (size_t)stride * nverts);
    std::memcpy(idst, idx, (size_t)prims * 3 * 2);
    sceGxmSetVertexStream(g_ctx, 0, vdst);
    sceGxmDraw(g_ctx, SCE_GXM_PRIMITIVE_TRIANGLES, SCE_GXM_INDEX_FORMAT_U16, idst, prims * 3);
}

Program* beSpriteProgram() {
    GpuLock lock(g_gpuMutex);
    static bool tried = false;
    if (!g_sprite && !tried) {
        tried = true;
        g_sprite = makeProgram("sprite_v", kCgSpriteVS, "sprite_f", kCgSpriteFS, nullptr);
        if (!g_sprite) fw::plat::logf("sprite shader unavailable: nothing can be drawn\n");
    }
    return g_sprite;
}
Program* beEffectProgram(const EffectDef* def) {
    GpuLock lock(g_gpuMutex);
    const CgEffect* cg = nullptr;
    for (auto& e : kCgEffects)
        if (std::strcmp(e.name, def->name) == 0) cg = &e;
    if (!cg) return nullptr;
    const char* base = std::strrchr(def->name, '/');
    std::string fsName = base ? base + 1 : def->name;
    if (cg->vs) return makeProgram((fsName + "_v").c_str(), cg->vs, fsName.c_str(), cg->fs, def, false);
    bool fc = std::strstr(cg->fs, "v_fc") != nullptr;
    return makeProgram(fc ? "sprite_fc_v" : "sprite_v", fc ? kCgSpriteFcVS : kCgSpriteVS, fsName.c_str(), cg->fs, def);
}

bool beReadBackBuffer(int, int, uint8_t*) { return false; }

}  // namespace fw::gfx

namespace fw {
void holdPacing(int frames) { gfx::g_pacingHold = std::max(gfx::g_pacingHold, frames); }
}  // namespace fw
