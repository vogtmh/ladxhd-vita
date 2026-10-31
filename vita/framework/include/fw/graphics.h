#pragma once
#include "all.h"

namespace Microsoft::Xna::Framework::Graphics {
using namespace ::Microsoft::Xna::Framework;
using ::System::Array1;

#define FW_ENUM_BASIC(E)                                                              \
    inline ::System::String* __EnumToString(E v) { return ::System::ToStr((int32_t)v); } \
    ::System::Array1<E>* __EnumValues(E*);                                            \
    ::System::Type* __EnumType(E*);
#define FW_ENUM_FLAGS(E, U)                                                                                   \
    inline constexpr E operator|(E a, E b) { return static_cast<E>(static_cast<U>(a) | static_cast<U>(b)); } \
    inline constexpr E operator&(E a, E b) { return static_cast<E>(static_cast<U>(a) & static_cast<U>(b)); } \
    inline constexpr E operator^(E a, E b) { return static_cast<E>(static_cast<U>(a) ^ static_cast<U>(b)); } \
    inline constexpr E operator~(E a) { return static_cast<E>(~static_cast<U>(a)); }                         \
    inline E& operator|=(E& a, E b) { return a = a | b; }                                                    \
    inline E& operator&=(E& a, E b) { return a = a & b; }                                                    \
    inline E& operator^=(E& a, E b) { return a = a ^ b; }

enum class Blend : int32_t { One, Zero, SourceColor, InverseSourceColor, SourceAlpha, InverseSourceAlpha, DestinationColor, InverseDestinationColor, DestinationAlpha, InverseDestinationAlpha, BlendFactor, InverseBlendFactor, SourceAlphaSaturation };
enum class BlendFunction : int32_t { Add, Subtract, ReverseSubtract, Min, Max };
enum class CullMode : int32_t { None, CullClockwiseFace, CullCounterClockwiseFace };
enum class DepthFormat : int32_t { None, Depth16, Depth24, Depth24Stencil8 };
enum class GraphicsProfile : int32_t { Reach, HiDef };
enum class PrimitiveType : int32_t { TriangleList, TriangleStrip, LineList, LineStrip, PointList };
enum class RenderTargetUsage : int32_t { DiscardContents, PreserveContents, PlatformContents };
enum class SpriteEffects : int32_t { None = 0, FlipHorizontally = 1, FlipVertically = 2 };
enum class SpriteSortMode : int32_t { Deferred, Immediate, Texture, BackToFront, FrontToBack };
enum class SurfaceFormat : int32_t { Color = 0 };
enum class VertexElementFormat : int32_t { Single, Vector2, Vector3, Vector4, Color, Byte4, Short2, Short4 };
enum class VertexElementUsage : int32_t { Position, Color, TextureCoordinate, Normal };
enum class TextureFilter : int32_t { Linear, Point, Anisotropic };
enum class TextureAddressMode : int32_t { Wrap, Clamp, Mirror };
FW_ENUM_BASIC(Blend)
FW_ENUM_BASIC(BlendFunction)
FW_ENUM_BASIC(CullMode)
FW_ENUM_BASIC(DepthFormat)
FW_ENUM_BASIC(GraphicsProfile)
FW_ENUM_BASIC(PrimitiveType)
FW_ENUM_BASIC(RenderTargetUsage)
FW_ENUM_BASIC(SpriteEffects)
FW_ENUM_FLAGS(SpriteEffects, int32_t)
FW_ENUM_BASIC(SpriteSortMode)
FW_ENUM_BASIC(SurfaceFormat)
FW_ENUM_BASIC(VertexElementFormat)
FW_ENUM_BASIC(VertexElementUsage)

struct GraphicsDevice;

struct GraphicsResource : Object, ::System::IDisposable {
    GraphicsDevice* device = nullptr;
    bool disposed = false;
    GraphicsDevice* get_GraphicsDevice() { return device; }
    bool get_IsDisposed() { return disposed; }
    void Dispose() override;
    virtual void releaseGpu() {}
};

struct Texture : GraphicsResource {
    uint32_t glTex = 0;
    void* be = nullptr;  // backend texture data (GXM)
    int32_t width = 0, height = 0;
    int32_t levelCount = 1;
    float texelW = 0, texelH = 0;  // MonoGame TexelWidth/Height (HD packs override them)
    int32_t samplerKey = -1;       // last filter/wrap applied to the GL texture
    // Encoded image kept until first use (Texture2D.FromStream decodes lazily).
    Array1<uint8_t>* pendingImage = nullptr;
    ::System::Fn<void(Array1<uint8_t>*)>* pendingProc = nullptr;
    int32_t get_LevelCount() { return levelCount; }
    void releaseGpu() override;
};

struct Texture2D : Texture {
    void __init(GraphicsDevice* gd, int32_t w, int32_t h);
    void __init(GraphicsDevice* gd, int32_t w, int32_t h, bool mipmap, SurfaceFormat fmt);
    int32_t get_Width() { return width; }
    int32_t get_Height() { return height; }
    Rectangle get_Bounds() { return Rectangle(0, 0, width, height); }
    static Texture2D* FromStream(GraphicsDevice* gd, ::System::IO::Stream* s);
    static Texture2D* FromStream(GraphicsDevice* gd, ::System::IO::Stream* s, ::System::Fn<void(Array1<uint8_t>*)>* colorProcessor);
    static Texture2D* FromFile(GraphicsDevice* gd, String* path);
    // RGBA8 pixel access.
    void setPixels(int32_t x, int32_t y, int32_t w, int32_t h, const void* rgba);
    void getPixels(int32_t x, int32_t y, int32_t w, int32_t h, void* rgba);
    template <class T>
    void SetData(Array1<T>* data) { setPixels(0, 0, width, height, data->data); }
    template <class T>
    void SetData(Array1<T>* data, int32_t start, int32_t count) { setPixels(0, 0, width, height, data->data + start); }
    template <class T>
    void SetData(int32_t level, ::System::Nullable<Rectangle> rect, Array1<T>* data, int32_t start, int32_t count) {
        Rectangle r = rect.hasValue ? rect.value : Rectangle(0, 0, width, height);
        setPixels(r.X, r.Y, r.Width, r.Height, data->data + start);
    }
    template <class T>
    void GetData(Array1<T>* data) { getPixels(0, 0, width, height, data->data); }
    template <class T>
    void GetData(int32_t level, ::System::Nullable<Rectangle> rect, Array1<T>* data, int32_t start, int32_t count) {
        Rectangle r = rect.hasValue ? rect.value : Rectangle(0, 0, width, height);
        getPixels(r.X, r.Y, r.Width, r.Height, data->data + start);
    }
    static ::System::Type* __type();
    ::System::Type* GetType() override { return __type(); }
};

struct RenderTarget2D : Texture2D {
    uint32_t fbo = 0;
    RenderTargetUsage usage = RenderTargetUsage::DiscardContents;
    bool clearPending = false;  // Clear() recorded but not yet executed on the GPU
    float clearColor[4] = {};
    void __init(GraphicsDevice* gd, int32_t w, int32_t h);
    void __init(GraphicsDevice* gd, int32_t w, int32_t h, bool mip, SurfaceFormat fmt, DepthFormat depth, int32_t msaa, RenderTargetUsage usage);
    void __init(GraphicsDevice* gd, int32_t w, int32_t h, bool mip, SurfaceFormat fmt, DepthFormat depth);
    void releaseGpu() override;
    static ::System::Type* __type();
    ::System::Type* GetType() override { return __type(); }
};

struct BlendState : GraphicsResource {
    Blend colorSrc = Blend::One, colorDst = Blend::Zero, alphaSrc = Blend::One, alphaDst = Blend::Zero;
    BlendFunction colorFn = BlendFunction::Add, alphaFn = BlendFunction::Add;
    void __init() {}
    Blend get_ColorSourceBlend() { return colorSrc; }
    void set_ColorSourceBlend(Blend b) { colorSrc = b; }
    Blend get_ColorDestinationBlend() { return colorDst; }
    void set_ColorDestinationBlend(Blend b) { colorDst = b; }
    Blend get_AlphaSourceBlend() { return alphaSrc; }
    void set_AlphaSourceBlend(Blend b) { alphaSrc = b; }
    Blend get_AlphaDestinationBlend() { return alphaDst; }
    void set_AlphaDestinationBlend(Blend b) { alphaDst = b; }
    BlendFunction get_ColorBlendFunction() { return colorFn; }
    void set_ColorBlendFunction(BlendFunction f) { colorFn = f; }
    BlendFunction get_AlphaBlendFunction() { return alphaFn; }
    void set_AlphaBlendFunction(BlendFunction f) { alphaFn = f; }
    static BlendState* AlphaBlend;
    static BlendState* NonPremultiplied;
    static BlendState* Opaque;
    static BlendState* Additive;
};

struct SamplerState : GraphicsResource {
    TextureFilter filter = TextureFilter::Linear;
    TextureAddressMode addressU = TextureAddressMode::Clamp, addressV = TextureAddressMode::Clamp;
    void __init() {}
    static SamplerState* PointClamp;
    static SamplerState* PointWrap;
    static SamplerState* LinearClamp;
    static SamplerState* LinearWrap;
    static SamplerState* AnisotropicClamp;
    static SamplerState* AnisotropicWrap;
};

struct DepthStencilState : GraphicsResource {
    bool depth = false;
    static DepthStencilState* Default;
    static DepthStencilState* None;
};

struct RasterizerState : GraphicsResource {
    CullMode cull = CullMode::CullCounterClockwiseFace;
    bool scissor = false;
    void __init() {}
    CullMode get_CullMode() { return cull; }
    void set_CullMode(CullMode c) { cull = c; }
    bool get_ScissorTestEnable() { return scissor; }
    void set_ScissorTestEnable(bool b) { scissor = b; }
    static RasterizerState* CullNone;
    static RasterizerState* CullCounterClockwise;
};

struct Viewport {
    int32_t x = 0, y = 0, width = 0, height = 0;
    Viewport() = default;
    Viewport(int32_t X, int32_t Y, int32_t w, int32_t h) : x(X), y(Y), width(w), height(h) {}
    int32_t get_X() const { return x; }
    int32_t get_Y() const { return y; }
    int32_t get_Width() const { return width; }
    int32_t get_Height() const { return height; }
    Rectangle get_Bounds() const { return Rectangle(x, y, width, height); }
    bool Equals(const Viewport& o) const { return x == o.x && y == o.y && width == o.width && height == o.height; }
    int32_t GetHashCode() const { return width ^ height; }
};

struct PresentationParameters : Object {
    int32_t backBufferWidth = 0, backBufferHeight = 0;
    int32_t get_BackBufferWidth() { return backBufferWidth; }
    int32_t get_BackBufferHeight() { return backBufferHeight; }
};

struct DisplayMode : Object {
    int32_t width = 0, height = 0;
    int32_t get_Width() { return width; }
    int32_t get_Height() { return height; }
};
struct GraphicsAdapter : Object {
    static GraphicsAdapter* get_DefaultAdapter();
    DisplayMode* get_CurrentDisplayMode();
};

struct SamplerStateCollection : Object {
    SamplerState* states[4] = {};
    SamplerState* get_Item(int32_t i) { return states[i & 3]; }
    void set_Item(int32_t i, SamplerState* s);
};
struct TextureCollection : Object {
    Texture* textures[4] = {};
    Texture* get_Item(int32_t i) { return textures[i & 3]; }
    void set_Item(int32_t i, Texture* t);
};

struct VertexElement {
    int32_t offset = 0;
    VertexElementFormat format{};
    VertexElementUsage usage{};
    int32_t usageIndex = 0;
    VertexElement() = default;
    VertexElement(int32_t o, VertexElementFormat f, VertexElementUsage u, int32_t i) : offset(o), format(f), usage(u), usageIndex(i) {}
    bool Equals(const VertexElement& o) const { return offset == o.offset && format == o.format && usage == o.usage; }
    int32_t GetHashCode() const { return offset; }
};
struct VertexDeclaration : Object {
    Array1<VertexElement>* elements = nullptr;
    void __init(Array1<VertexElement>* e) { elements = e; }
};
struct IVertexType {
    virtual ~IVertexType() = default;
    virtual VertexDeclaration* get_VertexDeclaration() = 0;
};

struct Effect;
struct EffectParameter : Object {
    Effect* effect = nullptr;
    String* name = nullptr;
    int32_t index = -1;  // uniform slot in the shader program
    float value[16] = {};
    int32_t count = 0;
    Texture* texture = nullptr;
    bool dirty = true;
    String* get_Name() { return name; }
    void SetValue(float v);
    void SetValue(int32_t v);
    void SetValue(bool v) { SetValue(v ? 1.0f : 0.0f); }
    void SetValue(Vector2 v);
    void SetValue(Vector3 v);
    void SetValue(Vector4 v);
    void SetValue(Matrix m);
    void SetValue(Texture* t);
    float GetValueSingle() { return value[0]; }
};
struct EffectParameterCollection : Object {
    ::System::Collections::Generic::List<EffectParameter*>* list = nullptr;
    EffectParameter* get_Item(String* name);
    EffectParameter* get_Item(int32_t i) { return list->get_Item(i); }
    int32_t get_Count() { return list->get_Count(); }
};
struct EffectPass : Object {
    Effect* effect = nullptr;
    void Apply();
};
struct EffectPassCollection : Object {
    EffectPass* pass = nullptr;
    EffectPass* get_Item(int32_t i) { return pass; }
    int32_t get_Count() { return 1; }
};
struct EffectTechnique : Object {
    EffectPassCollection* passes = nullptr;
    EffectPassCollection* get_Passes() { return passes; }
};
struct Effect : GraphicsResource {
    String* name = nullptr;
    void* program = nullptr;  // backend shader program
    EffectParameterCollection* parameters = nullptr;
    EffectTechnique* technique = nullptr;
    EffectParameterCollection* get_Parameters() { return parameters; }
    EffectTechnique* get_CurrentTechnique() { return technique; }
    void set_CurrentTechnique(EffectTechnique* t) { technique = t; }
    static Effect* Load(GraphicsDevice* gd, String* name);
    static ::System::Type* __type();
    ::System::Type* GetType() override { return __type(); }
};

struct SpriteBatch;
struct GraphicsDevice : Object {
    Viewport viewport;
    PresentationParameters* pp = nullptr;
    BlendState* blendState = nullptr;
    DepthStencilState* depthState = nullptr;
    RasterizerState* rasterizerState = nullptr;
    Rectangle scissor;
    SamplerStateCollection* samplers = nullptr;
    TextureCollection* textures = nullptr;
    RenderTarget2D* renderTarget = nullptr;
    ::System::Fn<void(Object*, ::System::EventArgs*)>* deviceReset = nullptr;
    Effect* activeEffect = nullptr;

    void __init();
    Viewport get_Viewport() { return viewport; }
    void set_Viewport(Viewport v);
    PresentationParameters* get_PresentationParameters() { return pp; }
    BlendState* get_BlendState() { return blendState; }
    void set_BlendState(BlendState* b);
    DepthStencilState* get_DepthStencilState() { return depthState; }
    void set_DepthStencilState(DepthStencilState* d) { depthState = d; }
    RasterizerState* get_RasterizerState() { return rasterizerState; }
    void set_RasterizerState(RasterizerState* r);
    Rectangle get_ScissorRectangle() { return scissor; }
    void set_ScissorRectangle(Rectangle r);
    SamplerStateCollection* get_SamplerStates() { return samplers; }
    TextureCollection* get_Textures() { return textures; }
    void Clear(Color c);
    void SetRenderTarget(RenderTarget2D* rt);
    ::System::Array1<RenderTarget2D*>* GetRenderTargets();
    void add_DeviceReset(::System::Fn<void(Object*, ::System::EventArgs*)>* h) { deviceReset = ::System::Delegate::Combine(deviceReset, h); }
    void remove_DeviceReset(::System::Fn<void(Object*, ::System::EventArgs*)>* h) { deviceReset = ::System::Delegate::Remove(deviceReset, h); }
    void drawIndexed(int32_t stride, const void* vertices, int32_t numVertices, const int16_t* indices, int32_t primitiveCount, const VertexDeclaration* decl);
    template <class T>
    void DrawUserIndexedPrimitives(PrimitiveType type, Array1<T>* vertexData, int32_t vertexOffset, int32_t numVertices, Array1<int16_t>* indexData, int32_t indexOffset, int32_t primitiveCount) {
        T proto{};
        drawIndexed((int32_t)sizeof(T), vertexData->data + vertexOffset, numVertices, indexData->data + indexOffset, primitiveCount, proto.get_VertexDeclaration());
    }
    template <class T>
    void DrawUserPrimitives(PrimitiveType type, Array1<T>* vertexData, int32_t vertexOffset, int32_t primitiveCount);
};

struct SpriteFont__Glyph {
    char16_t Character = 0;
    Rectangle BoundsInTexture;
    Rectangle Cropping;
    float LeftSideBearing = 0, RightSideBearing = 0, Width = 0, WidthIncludingBearings = 0;
    bool Equals(const SpriteFont__Glyph& o) const { return Character == o.Character; }
    int32_t GetHashCode() const { return Character; }
};

struct SpriteFont : Object {
    Texture2D* texture = nullptr;
    ::System::Collections::Generic::Dictionary<char16_t, SpriteFont__Glyph>* glyphs = nullptr;
    ::System::Collections::ObjectModel::ReadOnlyCollection<char16_t>* characters = nullptr;
    int32_t lineSpacing = 0;
    float spacing = 0;
    ::System::Nullable<char16_t> defaultChar;
    Texture2D* get_Texture() { return texture; }
    int32_t get_LineSpacing() { return lineSpacing; }
    void set_LineSpacing(int32_t v) { lineSpacing = v; }
    float get_Spacing() { return spacing; }
    void set_Spacing(float v) { spacing = v; }
    ::System::Nullable<char16_t> get_DefaultCharacter() { return defaultChar; }
    void set_DefaultCharacter(::System::Nullable<char16_t> c) { defaultChar = c; }
    ::System::Collections::ObjectModel::ReadOnlyCollection<char16_t>* get_Characters() { return characters; }
    ::System::Collections::Generic::Dictionary<char16_t, SpriteFont__Glyph>* GetGlyphs() { return glyphs; }
    Vector2 MeasureString(String* text);
    static SpriteFont* Load(GraphicsDevice* gd, String* path);
    static ::System::Type* __type();
    ::System::Type* GetType() override { return __type(); }
};

struct SpriteBatch : GraphicsResource {
    struct Item {
        Texture* tex;
        float x[4], y[4], u[4], v[4];
        uint32_t color;
    };
    bool begun = false;
    SpriteSortMode sortMode{};
    BlendState* blend = nullptr;
    SamplerState* sampler = nullptr;
    DepthStencilState* depth = nullptr;
    RasterizerState* raster = nullptr;
    Effect* effect = nullptr;
    Matrix transform;
    void* items = nullptr;  // backend-owned vector<Item>
    int32_t itemCount = 0;

    void __init(GraphicsDevice* gd);
    void Begin(SpriteSortMode sortMode, BlendState* blendState, SamplerState* samplerState, DepthStencilState* depthStencilState, RasterizerState* rasterizerState, Effect* effect, ::System::Nullable<Matrix> transformMatrix);
    void End();
    void Draw(Texture2D* tex, Rectangle dest, Color color);
    void Draw(Texture2D* tex, Rectangle dest, ::System::Nullable<Rectangle> src, Color color);
    void Draw(Texture2D* tex, Rectangle dest, ::System::Nullable<Rectangle> src, Color color, float rotation, Vector2 origin, SpriteEffects effects, float layerDepth);
    void Draw(Texture2D* tex, Vector2 pos, Color color);
    void Draw(Texture2D* tex, Vector2 pos, ::System::Nullable<Rectangle> src, Color color);
    void Draw(Texture2D* tex, Vector2 pos, ::System::Nullable<Rectangle> src, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth);
    void Draw(Texture2D* tex, Vector2 pos, ::System::Nullable<Rectangle> src, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth);
    void DrawString(SpriteFont* font, String* text, Vector2 pos, Color color);
    void DrawString(SpriteFont* font, String* text, Vector2 pos, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth);
    void DrawString(SpriteFont* font, String* text, Vector2 pos, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth);
    // internal
    void push(Texture* tex, float dx, float dy, float dw, float dh, float sx, float sy, float sw, float sh, Color color, float rotation, float ox, float oy, SpriteEffects fx);
    void flush();
};

struct DefaultColorProcessors {
    static ::System::Fn<void(Array1<uint8_t>*)>* ZeroTransparentPixels;
    static ::System::Fn<void(Array1<uint8_t>*)>* PremultiplyAlpha;
};

}  // namespace Microsoft::Xna::Framework::Graphics

namespace System {
struct EffectPassRange {
    ::Microsoft::Xna::Framework::Graphics::EffectPass* p;
    ::Microsoft::Xna::Framework::Graphics::EffectPass** begin() { return &p; }
    ::Microsoft::Xna::Framework::Graphics::EffectPass** end() { return &p + 1; }
};
inline EffectPassRange Each(::Microsoft::Xna::Framework::Graphics::EffectPassCollection* c) { return EffectPassRange{nn(c)->pass}; }
}  // namespace System

namespace fw::gfx {
// While on, shaders see the back buffer like a render target (top-left screen position), because the frame that
// would have gone through the game's main render targets is drawn straight to the screen.
void setBackBufferAsTarget(bool on);
}  // namespace fw::gfx

namespace fw {
void setPerfOverlay(bool on);
int getPerfOverlayMode();
void setPerfOverlayMode(int value);
// Stress test: forces hitch/perf log lines on and reports memory counters (see memoryStat in game.cpp).
void setStressMode(bool on);
// Vita: waits for the GPU after every scene and logs "gpu scenes:" once a second (slows the game down).
void setGpuSceneTiming(bool on);
int64_t memoryStat(int what);
// Map transitions: frame-time misses during the next `frames` frames don't count against 60 fps.
void holdPacing(int frames);
}  // namespace fw
