#pragma once
#include "all.h"

namespace MonoGame::Extended {
struct SizeF {
    float Width = 0, Height = 0;
    SizeF() = default;
    SizeF(float w, float h) : Width(w), Height(h) {}
    operator ::Microsoft::Xna::Framework::Vector2() const { return ::Microsoft::Xna::Framework::Vector2(Width, Height); }
    float get_Width() const { return Width; }
    float get_Height() const { return Height; }
    bool Equals(const SizeF& o) const { return Width == o.Width && Height == o.Height; }
    int32_t GetHashCode() const { return (int32_t)Width ^ (int32_t)Height; }
};
}  // namespace MonoGame::Extended

namespace MonoGame::Extended::BitmapFonts {
using ::Microsoft::Xna::Framework::Vector2;
using ::Microsoft::Xna::Framework::Color;
using ::Microsoft::Xna::Framework::Rectangle;

struct BitmapFont : ::System::Object {
    struct Glyph {
        int32_t x, y, w, h, xoff, yoff, xadvance, page;
    };
    ::System::Array1<::Microsoft::Xna::Framework::Graphics::Texture2D*>* pages = nullptr;
    void* glyphs = nullptr;  // std::unordered_map<int, Glyph>*
    int32_t lineHeight = 0;
    int32_t get_LineHeight() { return lineHeight; }
    ::MonoGame::Extended::SizeF MeasureString(::System::String* text);
    static BitmapFont* Load(::Microsoft::Xna::Framework::Graphics::GraphicsDevice* gd, ::System::String* fntPath);
    const Glyph* find(int32_t cp) const;
    static ::System::Type* __type();
    ::System::Type* GetType() override { return __type(); }
};

struct BitmapFontExtensions {
    static void DrawString(::Microsoft::Xna::Framework::Graphics::SpriteBatch* sb, BitmapFont* font, ::System::String* text, Vector2 pos, Color color,
                           float rotation, Vector2 origin, Vector2 scale, ::Microsoft::Xna::Framework::Graphics::SpriteEffects effect, float layerDepth,
                           ::System::Nullable<Rectangle> clip);
    static void DrawString(::Microsoft::Xna::Framework::Graphics::SpriteBatch* sb, BitmapFont* font, ::System::String* text, Vector2 pos, Color color,
                           ::System::Nullable<Rectangle> clip);
};
}  // namespace MonoGame::Extended::BitmapFonts

namespace NVorbis::Contracts {
struct ITagData {
    virtual ~ITagData() = default;
    virtual ::System::String* GetTagSingle(::System::String* key, bool concatenate) = 0;
};
}  // namespace NVorbis::Contracts

namespace NVorbis {
// Ogg music replacement (mods); decoded with stb_vorbis.
struct VorbisReader : ::System::Object, ::System::IDisposable, virtual Contracts::ITagData {
    void* dec = nullptr;
    ::System::Array1<uint8_t>* data = nullptr;
    int32_t channels = 0, sampleRate = 0;
    int64_t position = 0;
    void __init(::System::IO::Stream* s, bool closeOnDispose);
    int32_t get_Channels() { return channels; }
    int32_t get_SampleRate() { return sampleRate; }
    int64_t get_SamplePosition() { return position; }
    void set_SamplePosition(int64_t p) { SeekTo(p, ::System::IO::SeekOrigin::Begin); }
    int64_t get_TotalSamples();
    int32_t ReadSamples(::System::Array1<float>* buffer, int32_t offset, int32_t count);
    void SeekTo(int64_t pos, ::System::IO::SeekOrigin origin);
    Contracts::ITagData* get_Tags() { return this; }
    ::System::String* GetTagSingle(::System::String* key, bool concatenate) override;
    void Dispose() override;
};
}  // namespace NVorbis
