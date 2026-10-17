#pragma once
// Internal graphics interface: backend-neutral code (gfx_common.cpp, effects.cpp, content.cpp) and the
// backend that talks to the GPU (gfx_gl.cpp on desktop, gfx_gxm.cpp on the Vita).
#include "fw/all.h"
#include <string>
#include <vector>

namespace fw::gfx {
using namespace ::Microsoft::Xna::Framework;
using namespace ::Microsoft::Xna::Framework::Graphics;

struct ParamDef {
    const char* name;
    enum Kind { Float, Int, Vec2, Vec3, Vec4, Mat4, Tex } kind;
    int texUnit;   // for Tex
    float def[4];  // Tex: def[0] == 1 forces point filtering (sampler_state Filter = POINT)
};
struct EffectDef {
    const char* name;
    const char* vs;  // GLSL; null = sprite vertex shader
    const char* fs;  // GLSL
    std::vector<ParamDef> params;
};
const EffectDef* findEffect(const char* name);

// Compiled shader program; each backend derives its own.
struct Program {
    const EffectDef* def = nullptr;
    virtual ~Program() = default;
};

struct Vertex {
    float x, y;
    uint32_t color;
    float u, v;
};

// Everything one draw needs, resolved by the backend-neutral code.
struct DrawState {
    Program* prog = nullptr;
    Effect* effect = nullptr;  // supplies parameter values when prog->def is set
    Matrix matrix;
    BlendState* blend = nullptr;
    bool scissor = false;
    Rectangle scissorRect;  // XNA coordinates in the bound target
    Texture* tex[4] = {};
    SamplerState* samp[4] = {};
};

// ---- backend
void beInit(int backW, int backH);
void beBeginFrame();
void bePresent();
// Time the game should advance this frame, given the measured wall-clock time.
double beFrameDelta(double measured);
void beCreateTexture(Texture* t, int w, int h);
void beReleaseTexture(Texture* t);
void beUpload(Texture* t, int x, int y, int w, int h, const void* rgba);
void beDownload(Texture* t, int x, int y, int w, int h, void* rgba);
void beCreateTarget(RenderTarget2D* rt);
void beReleaseTarget(RenderTarget2D* rt);
void beBindTarget(RenderTarget2D* rt);  // null = back buffer
void beSetViewport(int x, int y, int w, int h);  // XNA coordinates inside the bound target
void beClear(float r, float g, float b, float a);  // whole target, ignores scissor and viewport
void beDrawQuads(const DrawState& ds, const Vertex* v, int quads);
void beDrawIndexed(const DrawState& ds, int stride, const void* verts, int nverts, const int16_t* idx, int prims,
                   const VertexDeclaration* decl);
Program* beSpriteProgram();
Program* beEffectProgram(const EffectDef* def);
// Debug screenshot of the back buffer (desktop only).
bool beReadBackBuffer(int w, int h, uint8_t* rgbaTopDown);

// ---- backend-neutral helpers
// Decodes and uploads a lazily loaded image (Texture2D.FromStream) before it is drawn or accessed.
void ensureLoaded(Texture* t);
// Set while constructing a texture whose GPU storage is created later by ensureLoaded.
extern bool deferTextureCreation;
Program* spriteProgram();
Program* effectProgram(Effect* e);
RenderTarget2D* currentTarget();
int targetWidth();
int targetHeight();
void ensureTarget();
void drawSprites(const Vertex* verts, int quadCount, Program* p, const Matrix& m, Effect* effect, Texture* tex, SamplerState* s);
// Untextured quads on the back buffer in pixel coordinates (performance overlay).
void drawOverlay(const Vertex* verts, int quadCount, int w, int h);

extern size_t textureBytes;
extern int drawCalls, targetSwitches;
struct GpuStats {
    double glSeconds = 0, bindSeconds = 0, uploadSeconds = 0;
    int uploads = 0;
    size_t uploadBytes = 0;
};
extern GpuStats gpuStats;
// 0 = normal, 1 = skip all draws (measures game-side CPU), 2 = skip render-target binds.
extern int debugMode;
extern bool backBufferAsTarget;
}  // namespace fw::gfx

namespace fw {
struct LoadStats {
    int images = 0, assets = 0;
    double imageSeconds = 0, assetSeconds = 0, readSeconds = 0;
};
extern LoadStats loadStats;
}  // namespace fw
