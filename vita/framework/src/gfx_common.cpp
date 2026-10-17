// Backend-neutral graphics: device state, render-target bookkeeping, resources, SpriteBatch.
#include "gfx_internal.h"
#include "platform.h"
#include <algorithm>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <cstring>

using namespace ::System;

namespace fw::gfx {

static GraphicsDevice* g_device = nullptr;
static RenderTarget2D* g_target = nullptr;
static int g_backW = 0, g_backH = 0;
// Render targets are bound lazily: a target that is set and replaced without being drawn to costs
// nothing, which matters on the Vita where every switch is a separate GPU scene.
static bool g_targetDirty = false;
static bool g_pendingDiscardClear = false;
// Target the backend renders to; binding it again would only split the GPU scene.
static RenderTarget2D* g_boundTarget = nullptr;
static bool g_haveBound = false;
// Clears are executed when the target is first drawn to, so a target that is cleared and replaced
// (or discarded) before any draw costs no GPU scene.
static bool g_backClearPending = false;
static float g_backClearColor[4];
size_t textureBytes = 0;
int drawCalls = 0, targetSwitches = 0;
GpuStats gpuStats;
int debugMode = 0;
bool backBufferAsTarget = false;
void setBackBufferAsTarget(bool on) { backBufferAsTarget = on; }

namespace {
struct ScopedTime {
    double& acc;
    double t0;
    explicit ScopedTime(double& a) : acc(a), t0(fw::plat::timeSeconds()) {}
    ~ScopedTime() { acc += fw::plat::timeSeconds() - t0; }
};
}  // namespace

extern "C" long fw_frame_counter();
static bool rtLogActive() {
    static long frame = [] { const char* s = std::getenv("LADX_RTLOG"); return s ? std::atol(s) : -1L; }();
    return frame >= 0 && fw_frame_counter() == frame;
}

RenderTarget2D* currentTarget() { return g_target; }
int targetWidth() { return g_target ? g_target->width : g_backW; }
int targetHeight() { return g_target ? g_target->height : g_backH; }

Program* spriteProgram() {
    static Program* p = nullptr;
    if (!p) p = beSpriteProgram();
    return p;
}

Program* effectProgram(Effect* e) {
    if (e->program) return (Program*)e->program;
    char* nm = e->name ? e->name->ToUtf8() : (char*)"?";
    const EffectDef* def = findEffect(nm);
    Program* p = nullptr;
    if (def) p = beEffectProgram(def);
    if (!p) {
        fw::plat::logf("effect %s not available, using sprite shader\n", nm);
        p = spriteProgram();
    }
    e->program = p;
    return p;
}

static bool& clearPendingOf(RenderTarget2D* rt) { return rt ? rt->clearPending : g_backClearPending; }
static float* clearColorOf(RenderTarget2D* rt) { return rt ? rt->clearColor : g_backClearColor; }

static void bindNow(RenderTarget2D* rt) {
    if (g_haveBound && g_boundTarget == rt) return;
    targetSwitches++;
    beBindTarget(rt);
    g_boundTarget = rt;
    g_haveBound = true;
}

void ensureTarget() {
    if (!g_targetDirty) return;
    g_targetDirty = false;
    if (debugMode == 2) {
        g_pendingDiscardClear = false;
        clearPendingOf(g_target) = false;
        return;
    }
    ScopedTime st(gpuStats.bindSeconds);
    bindNow(g_target);
    Viewport v = g_device->viewport;
    beSetViewport(v.x, v.y, v.width, v.height);
    bool& pending = clearPendingOf(g_target);
    if (pending) {
        pending = false;
        g_pendingDiscardClear = false;
        const float* c = clearColorOf(g_target);
        beClear(c[0], c[1], c[2], c[3]);
    } else if (g_pendingDiscardClear) {
        g_pendingDiscardClear = false;
        // MonoGame clears DiscardContents targets (and the back buffer) with this purple.
        beClear(68 / 255.0f, 34 / 255.0f, 136 / 255.0f, 1.0f);
    }
}

// A render target about to be sampled must hold its deferred clear colour.
static void flushClear(Texture* t) {
    auto* rt = t ? dynamic_cast<RenderTarget2D*>(t) : nullptr;
    if (!rt || !rt->clearPending || (rt == g_target && g_targetDirty)) return;
    rt->clearPending = false;
    ScopedTime st(gpuStats.bindSeconds);
    bindNow(rt);
    beSetViewport(0, 0, rt->width, rt->height);
    beClear(rt->clearColor[0], rt->clearColor[1], rt->clearColor[2], rt->clearColor[3]);
    // Rebind the current target, keeping its contents.
    g_targetDirty = true;
}

// A target that was only cleared reads as its clear colour everywhere, so a 1x1 texture of that colour
// stands in for it (e.g. the light map in daylight) and the full-size clear never has to run.
static Texture* sampledTexture(Texture* t) {
    auto* rt = t ? dynamic_cast<RenderTarget2D*>(t) : nullptr;
    if (!rt || !rt->clearPending || (rt == g_target && g_targetDirty)) return t;
    auto byte = [](float v) { return (uint8_t)(std::clamp(v, 0.0f, 1.0f) * 255.0f + 0.5f); };
    const uint8_t px[4] = {byte(rt->clearColor[0]), byte(rt->clearColor[1]), byte(rt->clearColor[2]), byte(rt->clearColor[3])};
    uint32_t key;
    std::memcpy(&key, px, 4);
    // Static storage keeps the textures visible to the GC.
    static Texture2D* cache[16];
    static uint32_t keys[16];
    static int count = 0;
    for (int i = 0; i < count && i < 16; i++)
        if (keys[i] == key) return cache[i];
    auto* solid = New<Texture2D>(g_device, 1, 1);
    solid->setPixels(0, 0, 1, 1, px);
    keys[count % 16] = key;
    cache[count % 16] = solid;
    count++;
    return solid;
}

// Texture units 1-3 come from GraphicsDevice.Textures unless an effect texture parameter covers them.
static void resolveTextures(DrawState& ds, Effect* effect, Program* p) {
    for (int u = 1; u < 4; u++) {
        ds.tex[u] = g_device->textures->textures[u];
        ds.samp[u] = g_device->samplers->states[u];
    }
    if (!effect || !p->def) return;
    auto* list = effect->parameters->list;
    for (size_t i = 0; i < p->def->params.size(); i++) {
        const ParamDef& pd = p->def->params[i];
        if (pd.kind != ParamDef::Tex) continue;
        Texture* t = nullptr;
        for (int32_t k = 0; k < list->size; k++)
            if (list->items->data[k]->index == (int32_t)i) t = list->items->data[k]->texture;
        ds.tex[pd.texUnit] = t ? t : g_device->textures->textures[pd.texUnit];
        ds.samp[pd.texUnit] = pd.def[0] == 1 ? SamplerState::PointClamp : g_device->samplers->states[pd.texUnit];
    }
}

void drawSprites(const Vertex* verts, int quadCount, Program* p, const Matrix& m, Effect* effect, Texture* tex, SamplerState* s) {
    if (quadCount <= 0 || debugMode == 1) return;
    ScopedTime st(gpuStats.glSeconds);
    DrawState ds;
    ds.prog = p;
    ds.effect = effect;
    ds.matrix = m;
    ds.blend = g_device->blendState;
    ds.scissor = g_device->rasterizerState && g_device->rasterizerState->scissor;
    ds.scissorRect = g_device->scissor;
    ds.tex[0] = tex;
    ds.samp[0] = s;
    resolveTextures(ds, effect, p);
    for (Texture*& t : ds.tex) t = sampledTexture(t);
    ensureTarget();
    for (Texture* t : ds.tex) ensureLoaded(t);
    beDrawQuads(ds, verts, quadCount);
    drawCalls++;
}

void drawOverlay(const Vertex* verts, int quadCount, int w, int h) {
    if (g_target || !g_device) return;
    static Texture2D* white = nullptr;
    if (!white) {
        white = New<Texture2D>(g_device, 1, 1);
        const uint8_t px[4] = {255, 255, 255, 255};
        white->setPixels(0, 0, 1, 1, px);
    }
    ensureTarget();
    Viewport saved = g_device->viewport;
    beSetViewport(0, 0, w, h);
    DrawState ds;
    ds.prog = spriteProgram();
    ds.matrix = Matrix::CreateOrthographicOffCenter(0, (float)w, (float)h, 0, 0, -1);
    ds.blend = BlendState::AlphaBlend;
    ds.tex[0] = white;
    ds.samp[0] = SamplerState::PointClamp;
    beDrawQuads(ds, verts, quadCount);
    beSetViewport(saved.x, saved.y, saved.width, saved.height);
}

}  // namespace fw::gfx

namespace Microsoft::Xna::Framework::Graphics {
using namespace fw::gfx;

BlendState* BlendState::AlphaBlend = nullptr;
BlendState* BlendState::NonPremultiplied = nullptr;
BlendState* BlendState::Opaque = nullptr;
BlendState* BlendState::Additive = nullptr;
SamplerState* SamplerState::PointClamp = nullptr;
SamplerState* SamplerState::PointWrap = nullptr;
SamplerState* SamplerState::LinearClamp = nullptr;
SamplerState* SamplerState::LinearWrap = nullptr;
SamplerState* SamplerState::AnisotropicClamp = nullptr;
SamplerState* SamplerState::AnisotropicWrap = nullptr;
DepthStencilState* DepthStencilState::Default = nullptr;
DepthStencilState* DepthStencilState::None = nullptr;
RasterizerState* RasterizerState::CullNone = nullptr;
RasterizerState* RasterizerState::CullCounterClockwise = nullptr;
Fn<void(Array1<uint8_t>*)>* DefaultColorProcessors::ZeroTransparentPixels = nullptr;
Fn<void(Array1<uint8_t>*)>* DefaultColorProcessors::PremultiplyAlpha = nullptr;

template <class T>
static T* keep(T* p) {
    static std::vector<Object*>* roots = nullptr;
    // static-storage array so the GC sees these objects
    static Object* storage[64];
    static int n = 0;
    storage[n++] = p;
    (void)roots;
    return p;
}

static void initStates() {
    auto blend = [](Blend cs, Blend cd, Blend as, Blend ad) {
        auto* b = keep(New<BlendState>());
        b->colorSrc = cs;
        b->colorDst = cd;
        b->alphaSrc = as;
        b->alphaDst = ad;
        return b;
    };
    BlendState::AlphaBlend = blend(Blend::One, Blend::InverseSourceAlpha, Blend::One, Blend::InverseSourceAlpha);
    BlendState::NonPremultiplied = blend(Blend::SourceAlpha, Blend::InverseSourceAlpha, Blend::SourceAlpha, Blend::InverseSourceAlpha);
    BlendState::Opaque = blend(Blend::One, Blend::Zero, Blend::One, Blend::Zero);
    BlendState::Additive = blend(Blend::SourceAlpha, Blend::One, Blend::SourceAlpha, Blend::One);
    auto sampler = [](TextureFilter f, TextureAddressMode m) {
        auto* s = keep(New<SamplerState>());
        s->filter = f;
        s->addressU = s->addressV = m;
        return s;
    };
    SamplerState::PointClamp = sampler(TextureFilter::Point, TextureAddressMode::Clamp);
    SamplerState::PointWrap = sampler(TextureFilter::Point, TextureAddressMode::Wrap);
    SamplerState::LinearClamp = sampler(TextureFilter::Linear, TextureAddressMode::Clamp);
    SamplerState::LinearWrap = sampler(TextureFilter::Linear, TextureAddressMode::Wrap);
    SamplerState::AnisotropicClamp = sampler(TextureFilter::Linear, TextureAddressMode::Clamp);
    SamplerState::AnisotropicWrap = sampler(TextureFilter::Linear, TextureAddressMode::Wrap);
    DepthStencilState::Default = keep(new DepthStencilState());
    DepthStencilState::Default->depth = true;
    DepthStencilState::None = keep(new DepthStencilState());
    RasterizerState::CullNone = keep(New<RasterizerState>());
    RasterizerState::CullNone->cull = CullMode::None;
    RasterizerState::CullCounterClockwise = keep(New<RasterizerState>());
    DefaultColorProcessors::ZeroTransparentPixels = keep(Fn<void(Array1<uint8_t>*)>::Make([](Array1<uint8_t>* d) {
        for (int32_t i = 0; i + 3 < d->length; i += 4)
            if (d->data[i + 3] == 0) d->data[i] = d->data[i + 1] = d->data[i + 2] = 0;
    }));
    DefaultColorProcessors::PremultiplyAlpha = keep(Fn<void(Array1<uint8_t>*)>::Make([](Array1<uint8_t>* d) {
        for (int32_t i = 0; i + 3 < d->length; i += 4) {
            uint32_t a = d->data[i + 3];
            d->data[i] = (uint8_t)((d->data[i] * a + 127) / 255);
            d->data[i + 1] = (uint8_t)((d->data[i + 1] * a + 127) / 255);
            d->data[i + 2] = (uint8_t)((d->data[i + 2] * a + 127) / 255);
        }
    }));
}

// ---------------------------------------------------------------- resources
void GraphicsResource::Dispose() {
    if (disposed) return;
    disposed = true;
    releaseGpu();
}
void Texture::releaseGpu() {
    pendingImage = nullptr;
    beReleaseTexture(this);
}
static void initTextureFields(Texture2D* t, GraphicsDevice* gd, int32_t w, int32_t h) {
    t->device = gd;
    t->width = w;
    t->height = h;
    t->texelW = 1.0f / w;
    t->texelH = 1.0f / h;
}
void Texture2D::__init(GraphicsDevice* gd, int32_t w, int32_t h) {
    initTextureFields(this, gd, w, h);
    if (!deferTextureCreation) beCreateTexture(this, w, h);
}
void Texture2D::__init(GraphicsDevice* gd, int32_t w, int32_t h, bool, SurfaceFormat) { __init(gd, w, h); }
void Texture2D::setPixels(int32_t x, int32_t y, int32_t w, int32_t h, const void* rgba) {
    ensureLoaded(this);
    flushClear(this);
    ScopedTime st(gpuStats.uploadSeconds);
    gpuStats.uploads++;
    gpuStats.uploadBytes += (size_t)w * h * 4;
    beUpload(this, x, y, w, h, rgba);
}
void Texture2D::getPixels(int32_t x, int32_t y, int32_t w, int32_t h, void* rgba) {
    ensureLoaded(this);
    if (this == g_target) ensureTarget();
    else flushClear(this);
    beDownload(this, x, y, w, h, rgba);
}
Type* Texture2D::__type() {
    static Type* t = Type::Create(u"Texture2D", u"Microsoft.Xna.Framework.Graphics.Texture2D", Object::__type());
    return t;
}

static bool rtCreateLog() {
    static bool on = std::getenv("LADX_RTCREATE") != nullptr;
    return on;
}
static void createTarget(RenderTarget2D* rt, GraphicsDevice* gd, int32_t w, int32_t h, RenderTargetUsage usage) {
    initTextureFields(rt, gd, w, h);
    rt->usage = usage;
    if (rtCreateLog()) fw::plat::logf("[frame %ld] create rt %p %dx%d\n", fw_frame_counter(), (void*)rt, w, h);
    beCreateTarget(rt);
}
void RenderTarget2D::__init(GraphicsDevice* gd, int32_t w, int32_t h) { createTarget(this, gd, w, h, RenderTargetUsage::DiscardContents); }
void RenderTarget2D::__init(GraphicsDevice* gd, int32_t w, int32_t h, bool, SurfaceFormat, DepthFormat, int32_t, RenderTargetUsage u) { createTarget(this, gd, w, h, u); }
void RenderTarget2D::__init(GraphicsDevice* gd, int32_t w, int32_t h, bool, SurfaceFormat, DepthFormat) { createTarget(this, gd, w, h, RenderTargetUsage::DiscardContents); }
void RenderTarget2D::releaseGpu() {
    if (rtCreateLog()) fw::plat::logf("[frame %ld] release rt %p %dx%d\n", fw_frame_counter(), (void*)this, width, height);
    // The address may be reused by a new target, which then must really be bound.
    if (g_haveBound && g_boundTarget == this) g_haveBound = false;
    clearPending = false;
    beReleaseTarget(this);
}
Type* RenderTarget2D::__type() {
    static Type* t = Type::Create(u"RenderTarget2D", u"Microsoft.Xna.Framework.Graphics.RenderTarget2D", Texture2D::__type());
    return t;
}

// ---------------------------------------------------------------- device
void GraphicsDevice::__init() {
    g_device = this;
    initStates();
    pp = New<PresentationParameters>();
    samplers = New<SamplerStateCollection>();
    textures = New<TextureCollection>();
    blendState = BlendState::AlphaBlend;
    depthState = DepthStencilState::None;
    rasterizerState = RasterizerState::CullCounterClockwise;
    for (auto& s : samplers->states) s = SamplerState::LinearClamp;
}
void fwSetBackBuffer(GraphicsDevice* gd, int w, int h) {
    static bool started = false;
    if (!started) {
        started = true;
        beInit(w, h);
    }
    g_backW = w;
    g_backH = h;
    gd->pp->backBufferWidth = w;
    gd->pp->backBufferHeight = h;
    if (!g_target) {
        gd->viewport = Viewport(0, 0, w, h);
        if (!g_targetDirty) beSetViewport(0, 0, w, h);
    }
}
void GraphicsDevice::set_Viewport(Viewport v) {
    viewport = v;
    if (!g_targetDirty) beSetViewport(v.x, v.y, v.width, v.height);
}
void GraphicsDevice::set_BlendState(BlendState* b) { blendState = b; }
void GraphicsDevice::set_RasterizerState(RasterizerState* r) { rasterizerState = r; }
void GraphicsDevice::set_ScissorRectangle(Rectangle r) {
    Rectangle full(0, 0, targetWidth(), targetHeight());
    scissor = Rectangle::Intersect(r, full);
}
void SamplerStateCollection::set_Item(int32_t i, SamplerState* s) { states[i & 3] = s; }
void TextureCollection::set_Item(int32_t i, Texture* t) { textures[i & 3] = t; }
void GraphicsDevice::Clear(Color c) {
    // A full clear makes the pending discard clear redundant.
    g_pendingDiscardClear = false;
    float rgba[4] = {c.get_R() / 255.0f, c.get_G() / 255.0f, c.get_B() / 255.0f, c.get_A() / 255.0f};
    if (g_targetDirty) {
        clearPendingOf(g_target) = true;
        std::memcpy(clearColorOf(g_target), rgba, sizeof rgba);
        return;
    }
    beClear(rgba[0], rgba[1], rgba[2], rgba[3]);
}
void GraphicsDevice::SetRenderTarget(RenderTarget2D* rt) {
    if (rtLogActive())
        fw::plat::logf("  [draws %d] -> SetRenderTarget %p %dx%d %s\n", drawCalls, (void*)rt, rt ? rt->width : g_backW,
                       rt ? rt->height : g_backH, rt && rt->usage != RenderTargetUsage::DiscardContents ? "preserve" : "discard");
    g_target = rt;
    renderTarget = rt;
    int w = targetWidth(), h = targetHeight();
    viewport = Viewport(0, 0, w, h);
    scissor = Rectangle(0, 0, w, h);
    g_targetDirty = true;
    g_pendingDiscardClear = !rt || rt->usage == RenderTargetUsage::DiscardContents;
    // Binding discards the contents, so an earlier clear that was never executed is moot.
    if (g_pendingDiscardClear) clearPendingOf(rt) = false;
}
Array1<RenderTarget2D*>* GraphicsDevice::GetRenderTargets() { return g_target ? Array1<RenderTarget2D*>::Of({g_target}) : Array1<RenderTarget2D*>::New(0); }

void GraphicsDevice::drawIndexed(int32_t stride, const void* vertices, int32_t numVertices, const int16_t* indices, int32_t primitiveCount, const VertexDeclaration* decl) {
    Effect* e = activeEffect;
    Program* p = e ? effectProgram(e) : spriteProgram();
    if (debugMode == 1) return;
    ScopedTime st(gpuStats.glSeconds);
    DrawState ds;
    ds.prog = p;
    ds.effect = e;
    ds.matrix = Matrix::get_Identity();
    ds.blend = blendState;
    ds.scissor = rasterizerState && rasterizerState->scissor;
    ds.scissorRect = scissor;
    ds.tex[0] = textures->textures[0];
    ds.samp[0] = samplers->states[0];
    resolveTextures(ds, e, p);
    for (Texture*& t : ds.tex) t = sampledTexture(t);
    ensureTarget();
    for (Texture* t : ds.tex) ensureLoaded(t);
    beDrawIndexed(ds, stride, vertices, numVertices, indices, primitiveCount, decl);
    drawCalls++;
}

GraphicsAdapter* GraphicsAdapter::get_DefaultAdapter() {
    static GraphicsAdapter* a = [] { void* m = GC_malloc_uncollectable(sizeof(GraphicsAdapter)); return new (m) GraphicsAdapter(); }();
    return a;
}
DisplayMode* GraphicsAdapter::get_CurrentDisplayMode() {
    auto* d = New<DisplayMode>();
    d->width = g_backW;
    d->height = g_backH;
    return d;
}

// ---------------------------------------------------------------- SpriteBatch
struct BatchItem {
    Texture* tex;
    Vertex v[4];
};
static std::vector<BatchItem>& itemsOf(SpriteBatch* sb) {
    if (!sb->items) sb->items = new std::vector<BatchItem>();
    return *(std::vector<BatchItem>*)sb->items;
}
// Textures referenced by pending items must stay alive until flush; they are also referenced by game objects.

void SpriteBatch::__init(GraphicsDevice* gd) { device = gd; }
void SpriteBatch::Begin(SpriteSortMode mode, BlendState* b, SamplerState* s, DepthStencilState* d, RasterizerState* r, Effect* e, Nullable<Matrix> m) {
    if (begun) ThrowInvalidOperation("Begin cannot be called again until End has been successfully called.");
    begun = true;
    sortMode = mode;
    blend = b ? b : BlendState::AlphaBlend;
    sampler = s ? s : SamplerState::LinearClamp;
    depth = d ? d : DepthStencilState::None;
    raster = r ? r : RasterizerState::CullCounterClockwise;
    effect = e;
    transform = m.hasValue ? m.value : Matrix::get_Identity();
    itemsOf(this).clear();
}
void SpriteBatch::End() {
    if (!begun) ThrowInvalidOperation("Begin must be called before calling End.");
    flush();
    begun = false;
}
void SpriteBatch::flush() {
    auto& items = itemsOf(this);
    if (items.empty()) return;
    device->blendState = blend;
    device->rasterizerState = raster;
    device->samplers->states[0] = sampler;
    Viewport vp = device->viewport;
    Matrix proj = Matrix::CreateOrthographicOffCenter(0, (float)vp.width, (float)vp.height, 0, 0, -1);
    Matrix m = transform * proj;
    Program* p = effect ? effectProgram(effect) : spriteProgram();
    static std::vector<Vertex> verts;
    size_t start = 0;
    while (start < items.size()) {
        Texture* tex = items[start].tex;
        size_t end = start;
        verts.clear();
        while (end < items.size() && items[end].tex == tex) {
            verts.insert(verts.end(), items[end].v, items[end].v + 4);
            end++;
        }
        drawSprites(verts.data(), (int)(end - start), p, m, effect, tex, sampler);
        start = end;
    }
    items.clear();
}

void SpriteBatch::push(Texture* tex, float dx, float dy, float dw, float dh, float sx, float sy, float sw, float sh, Color color, float rotation, float ox, float oy, SpriteEffects fx) {
    if (!tex) ThrowArgument("texture");
    float tw = tex->texelW, th = tex->texelH;
    float u0 = sx * tw, v0 = sy * th, u1 = (sx + sw) * tw, v1 = (sy + sh) * th;
    if (((int32_t)fx & (int32_t)SpriteEffects::FlipVertically) != 0) std::swap(v0, v1);
    if (((int32_t)fx & (int32_t)SpriteEffects::FlipHorizontally) != 0) std::swap(u0, u1);
    BatchItem it;
    it.tex = tex;
    uint32_t c = color.packed;
    if (rotation == 0) {
        float x = dx - ox, y = dy - oy;
        it.v[0] = {x, y, c, u0, v0};
        it.v[1] = {x + dw, y, c, u1, v0};
        it.v[2] = {x, y + dh, c, u0, v1};
        it.v[3] = {x + dw, y + dh, c, u1, v1};
    } else {
        float sn = std::sin(rotation), cs = std::cos(rotation);
        float x = dx, y = dy, ddx = -ox, ddy = -oy;
        it.v[0] = {x + ddx * cs - ddy * sn, y + ddx * sn + ddy * cs, c, u0, v0};
        it.v[1] = {x + (ddx + dw) * cs - ddy * sn, y + (ddx + dw) * sn + ddy * cs, c, u1, v0};
        it.v[2] = {x + ddx * cs - (ddy + dh) * sn, y + ddx * sn + (ddy + dh) * cs, c, u0, v1};
        it.v[3] = {x + (ddx + dw) * cs - (ddy + dh) * sn, y + (ddx + dw) * sn + (ddy + dh) * cs, c, u1, v1};
    }
    itemsOf(this).push_back(it);
    if (sortMode == SpriteSortMode::Immediate) flush();
}

void SpriteBatch::Draw(Texture2D* t, Rectangle d, Color c) { Draw(t, d, Nullable<Rectangle>(), c, 0, Vector2(), SpriteEffects::None, 0); }
void SpriteBatch::Draw(Texture2D* t, Rectangle d, Nullable<Rectangle> s, Color c) { Draw(t, d, s, c, 0, Vector2(), SpriteEffects::None, 0); }
void SpriteBatch::Draw(Texture2D* t, Rectangle d, Nullable<Rectangle> src, Color c, float rot, Vector2 origin, SpriteEffects fx, float depth) {
    nn(t);
    Rectangle s = src.hasValue ? src.value : Rectangle(0, 0, t->width, t->height);
    float ox, oy;
    if (src.hasValue) {
        ox = s.Width != 0 ? origin.X * d.Width / (float)s.Width : origin.X * d.Width * t->texelW;
        oy = s.Height != 0 ? origin.Y * d.Height / (float)s.Height : origin.Y * d.Height * t->texelH;
    } else {
        ox = origin.X * d.Width * t->texelW;
        oy = origin.Y * d.Height * t->texelH;
    }
    push(t, (float)d.X, (float)d.Y, (float)d.Width, (float)d.Height, (float)s.X, (float)s.Y, (float)s.Width, (float)s.Height, c, rot, ox, oy, fx);
}
void SpriteBatch::Draw(Texture2D* t, Vector2 p, Color c) { Draw(t, p, Nullable<Rectangle>(), c, 0, Vector2(), Vector2(1, 1), SpriteEffects::None, 0); }
void SpriteBatch::Draw(Texture2D* t, Vector2 p, Nullable<Rectangle> s, Color c) { Draw(t, p, s, c, 0, Vector2(), Vector2(1, 1), SpriteEffects::None, 0); }
void SpriteBatch::Draw(Texture2D* t, Vector2 p, Nullable<Rectangle> src, Color c, float rot, Vector2 origin, Vector2 scale, SpriteEffects fx, float depth) {
    nn(t);
    Rectangle s = src.hasValue ? src.value : Rectangle(0, 0, t->width, t->height);
    push(t, p.X, p.Y, s.Width * scale.X, s.Height * scale.Y, (float)s.X, (float)s.Y, (float)s.Width, (float)s.Height, c, rot, origin.X * scale.X, origin.Y * scale.Y, fx);
}
void SpriteBatch::Draw(Texture2D* t, Vector2 p, Nullable<Rectangle> src, Color c, float rot, Vector2 origin, float scale, SpriteEffects fx, float depth) {
    Draw(t, p, src, c, rot, origin, Vector2(scale, scale), fx, depth);
}

static const SpriteFont__Glyph* glyphFor(SpriteFont* f, char16_t ch) {
    int32_t idx = f->glyphs->findEntry(ch);
    if (idx < 0) {
        if (!f->defaultChar.hasValue) return nullptr;
        idx = f->glyphs->findEntry(f->defaultChar.value);
        if (idx < 0) return nullptr;
    }
    return &f->glyphs->entries->data[idx].value;
}
Vector2 SpriteFont::MeasureString(String* text) {
    if (!text || text->length == 0) return Vector2();
    float width = 0, finalLineHeight = (float)lineSpacing;
    Vector2 offset;
    bool first = true;
    for (int32_t i = 0; i < text->length; i++) {
        char16_t c = text->chars[i];
        if (c == '\r') continue;
        if (c == '\n') {
            finalLineHeight = (float)lineSpacing;
            offset.X = 0;
            offset.Y += lineSpacing;
            first = true;
            continue;
        }
        const SpriteFont__Glyph* g = glyphFor(this, c);
        if (!g) continue;
        if (first) {
            offset.X = std::max(g->LeftSideBearing, 0.0f);
            first = false;
        } else offset.X += spacing + g->LeftSideBearing;
        offset.X += g->Width;
        float proposed = offset.X + std::max(g->RightSideBearing, 0.0f);
        if (proposed > width) width = proposed;
        offset.X += g->RightSideBearing;
        if (g->Cropping.Height > finalLineHeight) finalLineHeight = (float)g->Cropping.Height;
    }
    return Vector2(width, offset.Y + finalLineHeight);
}
Type* SpriteFont::__type() {
    static Type* t = Type::Create(u"SpriteFont", u"Microsoft.Xna.Framework.Graphics.SpriteFont", Object::__type());
    return t;
}
void SpriteBatch::DrawString(SpriteFont* f, String* text, Vector2 pos, Color color) { DrawString(f, text, pos, color, 0, Vector2(), Vector2(1, 1), SpriteEffects::None, 0); }
void SpriteBatch::DrawString(SpriteFont* f, String* text, Vector2 pos, Color color, float rot, Vector2 origin, float scale, SpriteEffects fx, float depth) {
    DrawString(f, text, pos, color, rot, origin, Vector2(scale, scale), fx, depth);
}
void SpriteBatch::DrawString(SpriteFont* f, String* text, Vector2 pos, Color color, float rot, Vector2 origin, Vector2 scale, SpriteEffects fx, float depth) {
    nn(f);
    if (!text) return;
    bool fv = ((int32_t)fx & 2) != 0, fh = ((int32_t)fx & 1) != 0;
    Vector2 flipAdj;
    if (fv || fh) {
        Vector2 size = f->MeasureString(text);
        if (fh) { origin.X *= -1; flipAdj.X = -size.X; }
        if (fv) { origin.Y *= -1; flipAdj.Y = f->lineSpacing - size.Y; }
    }
    Matrix m = Matrix::get_Identity();
    float cs = 1, sn = 0;
    if (rot == 0) {
        m.M11 = fh ? -scale.X : scale.X;
        m.M22 = fv ? -scale.Y : scale.Y;
        m.M41 = ((flipAdj.X - origin.X) * m.M11) + pos.X;
        m.M42 = ((flipAdj.Y - origin.Y) * m.M22) + pos.Y;
    } else {
        cs = std::cos(rot);
        sn = std::sin(rot);
        m.M11 = (fh ? -scale.X : scale.X) * cs;
        m.M12 = (fh ? -scale.X : scale.X) * sn;
        m.M21 = (fv ? -scale.Y : scale.Y) * -sn;
        m.M22 = (fv ? -scale.Y : scale.Y) * cs;
        m.M41 = (((flipAdj.X - origin.X) * m.M11) + (flipAdj.Y - origin.Y) * m.M21) + pos.X;
        m.M42 = (((flipAdj.X - origin.X) * m.M12) + (flipAdj.Y - origin.Y) * m.M22) + pos.Y;
    }
    Vector2 offset;
    bool first = true;
    for (int32_t i = 0; i < text->length; i++) {
        char16_t c = text->chars[i];
        if (c == '\r') continue;
        if (c == '\n') {
            offset.X = 0;
            offset.Y += f->lineSpacing;
            first = true;
            continue;
        }
        const SpriteFont__Glyph* g = glyphFor(f, c);
        if (!g) continue;
        if (first) {
            offset.X = std::max(g->LeftSideBearing, 0.0f);
            first = false;
        } else offset.X += f->spacing + g->LeftSideBearing;
        Vector2 p = offset;
        if (fh) p.X += g->BoundsInTexture.Width;
        p.X += g->Cropping.X;
        if (fv) p.Y += g->BoundsInTexture.Height - f->lineSpacing;
        p.Y += g->Cropping.Y;
        p = Vector2::Transform(p, m);
        Rectangle b = g->BoundsInTexture;
        push(f->texture, p.X, p.Y, b.Width * scale.X, b.Height * scale.Y, (float)b.X, (float)b.Y, (float)b.Width, (float)b.Height, color, rot, 0, 0, fx);
        offset.X += g->Width + g->RightSideBearing;
    }
}

}  // namespace Microsoft::Xna::Framework::Graphics
