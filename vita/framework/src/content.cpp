// ContentManager (XNB reader), image/font loading.
#include "gfx_internal.h"
#include "platform.h"
#include <cstdio>
#include <cstring>
#include <string>
#include <unordered_map>
#include <vector>

#define STB_IMAGE_IMPLEMENTATION
#define STBI_ONLY_PNG
#define STBI_NO_STDIO
#include "stb/stb_image.h"

using namespace ::System;
using namespace ::Microsoft::Xna::Framework;
using namespace ::Microsoft::Xna::Framework::Graphics;

namespace fw {
LoadStats loadStats;
namespace gfx {
bool deferTextureCreation = false;
void zeroTransparentPixels(uint8_t* d, size_t n) {
    for (size_t i = 0; i + 3 < n; i += 4)
        if (d[i + 3] == 0) d[i] = d[i + 1] = d[i + 2] = 0;
}
void premultiplyAlpha(uint8_t* d, size_t n) {
    for (size_t i = 0; i + 3 < n; i += 4) {
        uint32_t a = d[i + 3];
        d[i] = (uint8_t)((d[i] * a + 127) / 255);
        d[i + 1] = (uint8_t)((d[i + 1] * a + 127) / 255);
        d[i + 2] = (uint8_t)((d[i + 2] * a + 127) / 255);
    }
}
void ensureLoaded(Texture* t) {
    if (!t || !t->pendingImage) return;
    auto* bytes = t->pendingImage;
    auto* proc = t->pendingProc;
    t->pendingImage = nullptr;
    t->pendingProc = nullptr;
    double t0 = plat::timeSeconds();
    int w, h, comp;
    uint8_t* px = stbi_load_from_memory(bytes->data, bytes->length, &w, &h, &comp, 4);
    beCreateTexture(t, t->width, t->height);
    if (!px || w != t->width || h != t->height) {
        if (px) stbi_image_free(px);
        plat::logf("could not decode deferred image %dx%d\n", t->width, t->height);
        return;
    }
    // Decoded in malloc memory: a block this large can fail in a full, fragmented GC heap.
    size_t n = (size_t)w * h * 4;
    if (proc == DefaultColorProcessors::ZeroTransparentPixels) {
        zeroTransparentPixels(px, n);
    } else if (proc == DefaultColorProcessors::PremultiplyAlpha) {
        premultiplyAlpha(px, n);
    } else if (proc) {
        auto* arr = Array1<uint8_t>::New(w * h * 4);
        std::memcpy(arr->data, px, n);
        proc->Invoke(arr);
        std::memcpy(px, arr->data, n);
    }
    beUpload(t, 0, 0, w, h, px);
    stbi_image_free(px);
    gpuStats.uploadSeconds += plat::timeSeconds() - t0;
}
}  // namespace gfx

struct Reader {
    const uint8_t* p;
    const uint8_t* end;
    uint8_t u8() { if (p >= end) throw NewEx<IO::InvalidDataException>(STR(u"XNB truncated")); return *p++; }
    uint32_t u32() { uint32_t v; std::memcpy(&v, need(4), 4); return v; }
    int32_t i32() { return (int32_t)u32(); }
    float f32() { float v; std::memcpy(&v, need(4), 4); return v; }
    const uint8_t* need(size_t n) {
        if ((size_t)(end - p) < n) throw NewEx<IO::InvalidDataException>(STR(u"XNB truncated"));
        const uint8_t* r = p;
        p += n;
        return r;
    }
    int32_t v7() {
        int32_t r = 0, shift = 0;
        uint8_t b;
        do {
            b = u8();
            r |= (b & 0x7F) << shift;
            shift += 7;
        } while (b & 0x80);
        return r;
    }
    std::string str() {
        int32_t n = v7();
        return std::string((const char*)need(n), n);
    }
    char16_t utf8char() {
        uint32_t c = u8();
        if (c < 0x80) return (char16_t)c;
        int extra = (c & 0xE0) == 0xC0 ? 1 : (c & 0xF0) == 0xE0 ? 2 : 3;
        c &= extra == 1 ? 0x1F : extra == 2 ? 0x0F : 0x07;
        for (int i = 0; i < extra; i++) c = (c << 6) | (u8() & 0x3F);
        return (char16_t)c;
    }
};

static std::vector<uint8_t> readFile(const std::string& path) {
    std::vector<uint8_t> v;
    // Through System.IO so files inside the mounted pack resolve too.
    auto* s = String::FromUtf8(path.c_str());
    double t0 = fw::plat::timeSeconds();
    if (!::System::IO::File::Exists(s)) return v;
    auto* a = ::System::IO::File::ReadAllBytes(s);
    v.assign(a->data, a->data + a->length);
    loadStats.readSeconds += fw::plat::timeSeconds() - t0;
    return v;
}

// DXT decoders for compressed font textures.
static void decodeColorBlock(const uint8_t* b, uint8_t out[16][4], bool dxt1) {
    uint16_t c0 = b[0] | (b[1] << 8), c1 = b[2] | (b[3] << 8);
    uint8_t pal[4][4];
    auto expand = [](uint16_t c, uint8_t* o) {
        o[0] = (uint8_t)(((c >> 11) & 31) * 255 / 31);
        o[1] = (uint8_t)(((c >> 5) & 63) * 255 / 63);
        o[2] = (uint8_t)((c & 31) * 255 / 31);
        o[3] = 255;
    };
    expand(c0, pal[0]);
    expand(c1, pal[1]);
    if (c0 > c1 || !dxt1) {
        for (int i = 0; i < 3; i++) {
            pal[2][i] = (uint8_t)((2 * pal[0][i] + pal[1][i]) / 3);
            pal[3][i] = (uint8_t)((pal[0][i] + 2 * pal[1][i]) / 3);
        }
        pal[2][3] = pal[3][3] = 255;
    } else {
        for (int i = 0; i < 3; i++) pal[2][i] = (uint8_t)((pal[0][i] + pal[1][i]) / 2);
        pal[2][3] = 255;
        pal[3][0] = pal[3][1] = pal[3][2] = pal[3][3] = 0;
    }
    uint32_t idx = b[4] | (b[5] << 8) | (b[6] << 16) | ((uint32_t)b[7] << 24);
    for (int i = 0; i < 16; i++) std::memcpy(out[i], pal[(idx >> (2 * i)) & 3], 4);
}
static std::vector<uint8_t> decodeDxt(const uint8_t* src, int w, int h, int fmt) {
    std::vector<uint8_t> out((size_t)w * h * 4);
    int bw = (w + 3) / 4, bh = (h + 3) / 4;
    int blockSize = fmt == 4 ? 8 : 16;
    for (int by = 0; by < bh; by++)
        for (int bx = 0; bx < bw; bx++) {
            const uint8_t* b = src + (size_t)(by * bw + bx) * blockSize;
            uint8_t px[16][4];
            if (fmt == 4) decodeColorBlock(b, px, true);
            else {
                decodeColorBlock(b + 8, px, false);
                if (fmt == 5) {
                    for (int i = 0; i < 16; i++) {
                        int a = (b[i / 2] >> ((i & 1) * 4)) & 15;
                        px[i][3] = (uint8_t)(a * 17);
                    }
                } else {
                    uint8_t a0 = b[0], a1 = b[1], al[8];
                    al[0] = a0;
                    al[1] = a1;
                    if (a0 > a1)
                        for (int i = 1; i < 7; i++) al[i + 1] = (uint8_t)(((7 - i) * a0 + i * a1) / 7);
                    else {
                        for (int i = 1; i < 5; i++) al[i + 1] = (uint8_t)(((5 - i) * a0 + i * a1) / 5);
                        al[6] = 0;
                        al[7] = 255;
                    }
                    uint64_t bits = 0;
                    for (int i = 0; i < 6; i++) bits |= (uint64_t)b[2 + i] << (8 * i);
                    for (int i = 0; i < 16; i++) px[i][3] = al[(bits >> (3 * i)) & 7];
                }
            }
            for (int i = 0; i < 16; i++) {
                int x = bx * 4 + (i & 3), y = by * 4 + (i >> 2);
                if (x < w && y < h) std::memcpy(&out[((size_t)y * w + x) * 4], px[i], 4);
            }
        }
    return out;
}

static Texture2D* readTexture(Reader& r, GraphicsDevice* gd) {
    int32_t fmt = r.i32();
    int32_t w = (int32_t)r.u32(), h = (int32_t)r.u32();
    int32_t levels = (int32_t)r.u32();
    auto* t = New<Texture2D>(gd, w, h);
    for (int32_t l = 0; l < levels; l++) {
        uint32_t size = r.u32();
        const uint8_t* data = r.need(size);
        if (l != 0) continue;
        if (fmt == 0) t->setPixels(0, 0, w, h, data);
        else if (fmt >= 4 && fmt <= 6) {
            auto px = decodeDxt(data, w, h, fmt);
            t->setPixels(0, 0, w, h, px.data());
        } else fw::plat::logf("unsupported texture format %d\n", fmt);
    }
    return t;
}

struct XnbFile {
    std::vector<uint8_t> bytes;
    std::vector<std::string> readers;
    Reader r{};
};
static bool openXnb(const std::string& path, XnbFile& x) {
    x.bytes = readFile(path);
    if (x.bytes.size() < 10 || std::memcmp(x.bytes.data(), "XNB", 3) != 0) return false;
    uint8_t flags = x.bytes[5];
    if (flags & 0xC0) {
        fw::plat::logf("compressed XNB not supported: %s\n", path.c_str());
        return false;
    }
    x.r = Reader{x.bytes.data() + 10, x.bytes.data() + x.bytes.size()};
    int32_t n = x.r.v7();
    for (int32_t i = 0; i < n; i++) {
        x.readers.push_back(x.r.str());
        x.r.i32();
    }
    x.r.v7();  // shared resources
    return true;
}

static SpriteFont* readSpriteFont(Reader& r, GraphicsDevice* gd) {
    auto* f = New<SpriteFont>();
    r.v7();
    f->texture = readTexture(r, gd);
    auto readRects = [&]() {
        r.v7();
        uint32_t n = r.u32();
        std::vector<Rectangle> v(n);
        for (auto& rc : v) {
            rc.X = r.i32();
            rc.Y = r.i32();
            rc.Width = r.i32();
            rc.Height = r.i32();
        }
        return v;
    };
    auto glyphs = readRects();
    auto crops = readRects();
    r.v7();
    uint32_t nc = r.u32();
    std::vector<char16_t> chars(nc);
    for (auto& c : chars) c = r.utf8char();
    f->lineSpacing = r.i32();
    f->spacing = r.f32();
    r.v7();
    uint32_t nk = r.u32();
    std::vector<Vector3> kern(nk);
    for (auto& k : kern) {
        k.X = r.f32();
        k.Y = r.f32();
        k.Z = r.f32();
    }
    if (r.u8()) f->defaultChar = Nullable<char16_t>(r.utf8char());
    f->glyphs = New<Collections::Generic::Dictionary<char16_t, SpriteFont__Glyph>>((int32_t)nc);
    auto* charList = New<Collections::Generic::List<char16_t>>();
    for (uint32_t i = 0; i < nc; i++) {
        SpriteFont__Glyph g;
        g.Character = chars[i];
        g.BoundsInTexture = glyphs[i];
        g.Cropping = crops[i];
        g.LeftSideBearing = kern[i].X;
        g.Width = kern[i].Y;
        g.RightSideBearing = kern[i].Z;
        g.WidthIncludingBearings = kern[i].X + kern[i].Y + kern[i].Z;
        f->glyphs->set_Item(chars[i], g);
        charList->Add(chars[i]);
    }
    f->characters = New<Collections::ObjectModel::ReadOnlyCollection<char16_t>>(charList);
    return f;
}

static Audio::SoundEffect* soundFromPcm(const uint8_t* fmt, uint32_t fmtSize, const uint8_t* data, uint32_t dataSize) {
    auto* s = New<Audio::SoundEffect>();
    uint16_t tag = fmt[0] | (fmt[1] << 8);
    s->channels = fmt[2] | (fmt[3] << 8);
    s->sampleRate = (int32_t)(fmt[4] | (fmt[5] << 8) | (fmt[6] << 16) | ((uint32_t)fmt[7] << 24));
    uint16_t bits = fmt[14] | (fmt[15] << 8);
    if (tag != 1 || (bits != 16 && bits != 8)) {
        fw::plat::logf("unsupported sound format tag=%d bits=%d\n", tag, bits);
        s->frames = 0;
        return s;
    }
    if (bits == 16) {
        s->frames = (int32_t)(dataSize / (2 * s->channels));
        s->samples = (int16_t*)std::malloc(dataSize);
        std::memcpy(s->samples, data, dataSize);
    } else {
        s->frames = (int32_t)(dataSize / s->channels);
        s->samples = (int16_t*)std::malloc((size_t)s->frames * s->channels * 2);
        for (uint32_t i = 0; i < dataSize; i++) s->samples[i] = (int16_t)((data[i] - 128) << 8);
    }
    return s;
}
static Audio::SoundEffect* readSoundEffect(Reader& r) {
    uint32_t fmtSize = r.u32();
    const uint8_t* fmt = r.need(fmtSize);
    uint32_t dataSize = r.u32();
    const uint8_t* data = r.need(dataSize);
    return soundFromPcm(fmt, fmtSize, data, dataSize);
}
Audio::SoundEffect* soundFromWav(const uint8_t* p, size_t n) {
    if (n < 12 || std::memcmp(p, "RIFF", 4) != 0 || std::memcmp(p + 8, "WAVE", 4) != 0) throw NewEx<IO::InvalidDataException>(STR(u"not a wav file"));
    const uint8_t* fmt = nullptr;
    uint32_t fmtSize = 0;
    size_t off = 12;
    while (off + 8 <= n) {
        uint32_t sz = p[off + 4] | (p[off + 5] << 8) | (p[off + 6] << 16) | ((uint32_t)p[off + 7] << 24);
        if (std::memcmp(p + off, "fmt ", 4) == 0) { fmt = p + off + 8; fmtSize = sz; }
        else if (std::memcmp(p + off, "data", 4) == 0 && fmt) return soundFromPcm(fmt, fmtSize, p + off + 8, (uint32_t)std::min<size_t>(sz, n - off - 8));
        off += 8 + sz + (sz & 1);
    }
    throw NewEx<IO::InvalidDataException>(STR(u"wav without data"));
}

}  // namespace fw

namespace Microsoft::Xna::Framework::Content {

void ContentManager::__init() { cache = New<::System::Collections::Generic::Dictionary<String*, Object*>>(); }

extern GraphicsDevice* fwDevice();

Object* ContentManager::loadAsset(String* name, Type* type) {
    Object* cached = nullptr;
    String* key = String::Concat(name, type->fullName);
    if (cache->TryGetValue(key, cached)) return cached;
    std::string n = name->ToUtf8();
    for (auto& c : n)
        if (c == '\\') c = '/';
    std::string root = fw::plat::contentRoot();
    std::string dir = rootDirectory ? std::string(rootDirectory->ToUtf8()) : "Content";
    std::string path = root + "/" + dir + "/" + n;
    Object* result = nullptr;
    double t0 = fw::plat::timeSeconds();
    if (type == Effect::__type()) {
        result = Effect::Load(fwDevice(), name);
    } else if (type == ::MonoGame::Extended::BitmapFonts::BitmapFont::__type()) {
        result = ::MonoGame::Extended::BitmapFonts::BitmapFont::Load(fwDevice(), String::FromUtf8((path + ".fnt").c_str()));
    } else {
        fw::XnbFile x;
        if (!fw::openXnb(path + ".xnb", x)) throw NewEx<IO::FileNotFoundException>(String::Concat(STR(u"Content not found: "), name));
        int32_t id = x.r.v7();
        const std::string& reader = id > 0 && id <= (int32_t)x.readers.size() ? x.readers[id - 1] : std::string();
        if (reader.find("Texture2DReader") != std::string::npos) result = fw::readTexture(x.r, fwDevice());
        else if (reader.find("SpriteFontReader") != std::string::npos) result = fw::readSpriteFont(x.r, fwDevice());
        else if (reader.find("SoundEffectReader") != std::string::npos) result = fw::readSoundEffect(x.r);
        else throw NewEx<NotSupportedException>(String::FromUtf8(("unsupported content reader " + reader).c_str()));
    }
    fw::loadStats.assets++;
    double dt = fw::plat::timeSeconds() - t0;
    fw::loadStats.assetSeconds += dt;
    if (dt > 0.05) fw::plat::logf("slow asset %s: %.0f ms\n", n.c_str(), dt * 1000.0);
    cache->set_Item(key, result);
    return result;
}
void ContentManager::Unload() { cache->Clear(); }

}  // namespace Microsoft::Xna::Framework::Content

namespace Microsoft::Xna::Framework::Graphics {

Texture2D* Texture2D::FromStream(GraphicsDevice* gd, IO::Stream* s) { return FromStream(gd, s, DefaultColorProcessors::ZeroTransparentPixels); }
Texture2D* Texture2D::FromStream(GraphicsDevice* gd, IO::Stream* s, Fn<void(Array1<uint8_t>*)>* proc) {
    double t0 = fw::plat::timeSeconds();
    auto* bytes = s->ReadAllRemaining();
    fw::loadStats.readSeconds += fw::plat::timeSeconds() - t0;
    int w, h, comp;
    if (!stbi_info_from_memory(bytes->data, bytes->length, &w, &h, &comp)) throw NewEx<IO::InvalidDataException>(STR(u"Could not decode image"));
    // The game loads every image (all languages, photo albums, ...) at startup; decode on first use instead.
    fw::gfx::deferTextureCreation = true;
    auto* t = New<Texture2D>(gd, w, h);
    fw::gfx::deferTextureCreation = false;
    t->pendingImage = bytes;
    t->pendingProc = proc;
    fw::loadStats.images++;
    fw::loadStats.imageSeconds += fw::plat::timeSeconds() - t0;
    return t;
}
Texture2D* Texture2D::FromFile(GraphicsDevice* gd, String* path) {
    auto* fs = IO::File::OpenRead(path);
    auto* t = FromStream(gd, fs);
    fs->Close();
    return t;
}

}  // namespace Microsoft::Xna::Framework::Graphics

namespace Microsoft::Xna::Framework::Audio {
SoundEffect* SoundEffect::FromStream(IO::Stream* s) {
    auto* bytes = s->ReadAllRemaining();
    return fw::soundFromWav(bytes->data, bytes->length);
}
}  // namespace Microsoft::Xna::Framework::Audio

// ---------------------------------------------------------------- MonoGame.Extended BitmapFont (BMFont text format)
namespace MonoGame::Extended::BitmapFonts {
using GlyphMap = std::unordered_map<int32_t, BitmapFont::Glyph>;

BitmapFont* BitmapFont::Load(GraphicsDevice* gd, String* fntPath) {
    auto* f = New<BitmapFont>();
    auto* map = new GlyphMap();
    f->glyphs = map;
    std::string path = fntPath->ToUtf8();
    auto bytes = fw::readFile(path);
    std::string text(bytes.begin(), bytes.end());
    std::string dir = path.substr(0, path.find_last_of('/') + 1);
    std::vector<std::string> pageFiles;
    size_t pos = 0;
    auto getInt = [](const std::string& line, const char* key) {
        std::string k = std::string(" ") + key + "=";
        auto i = line.find(k);
        return i == std::string::npos ? 0 : std::atoi(line.c_str() + i + k.size());
    };
    while (pos < text.size()) {
        size_t e = text.find('\n', pos);
        if (e == std::string::npos) e = text.size();
        std::string line = text.substr(pos, e - pos);
        pos = e + 1;
        if (line.rfind("common ", 0) == 0) f->lineHeight = getInt(line, "lineHeight");
        else if (line.rfind("page ", 0) == 0) {
            auto i = line.find("file=\"");
            if (i != std::string::npos) {
                auto j = line.find('"', i + 6);
                pageFiles.push_back(line.substr(i + 6, j - i - 6));
            }
        } else if (line.rfind("char ", 0) == 0) {
            Glyph g{getInt(line, "x"), getInt(line, "y"), getInt(line, "width"), getInt(line, "height"), getInt(line, "xoffset"), getInt(line, "yoffset"), getInt(line, "xadvance"), getInt(line, "page")};
            (*map)[getInt(line, "id")] = g;
        }
    }
    f->pages = Array1<Texture2D*>::New((int32_t)pageFiles.size());
    for (size_t i = 0; i < pageFiles.size(); i++) {
        try {
            f->pages->data[i] = Texture2D::FromFile(gd, String::FromUtf8((dir + pageFiles[i]).c_str()));
        } catch (Exception*) {
            fw::plat::logf("bitmap font page missing: %s\n", pageFiles[i].c_str());
        }
    }
    return f;
}
const BitmapFont::Glyph* BitmapFont::find(int32_t cp) const {
    auto* m = (GlyphMap*)glyphs;
    if (!m) return nullptr;
    auto it = m->find(cp);
    return it == m->end() ? nullptr : &it->second;
}
SizeF BitmapFont::MeasureString(String* text) {
    if (!text || text->length == 0) return SizeF();
    float w = 0, x = 0;
    int lines = 1;
    for (int32_t i = 0; i < text->length; i++) {
        char16_t c = text->chars[i];
        if (c == '\n') { lines++; x = 0; continue; }
        const Glyph* g = find(c);
        if (!g) continue;
        x += g->xadvance;
        if (x > w) w = x;
    }
    return SizeF(w, (float)(lines * lineHeight));
}
Type* BitmapFont::__type() {
    static Type* t = Type::Create(u"BitmapFont", u"MonoGame.Extended.BitmapFonts.BitmapFont", Object::__type());
    return t;
}
void BitmapFontExtensions::DrawString(SpriteBatch* sb, BitmapFont* font, String* text, Vector2 pos, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effect, float depth, Nullable<Rectangle> clip) {
    if (!text || !font) return;
    float x = 0, y = 0;
    for (int32_t i = 0; i < text->length; i++) {
        char16_t c = text->chars[i];
        if (c == '\n') { x = 0; y += font->lineHeight; continue; }
        const BitmapFont::Glyph* g = font->find(c);
        if (!g) continue;
        Texture2D* page = g->page < font->pages->length ? font->pages->data[g->page] : nullptr;
        if (page && g->w > 0 && g->h > 0) {
            float px = pos.X + (x + g->xoff - origin.X) * scale.X;
            float py = pos.Y + (y + g->yoff - origin.Y) * scale.Y;
            sb->push(page, px, py, g->w * scale.X, g->h * scale.Y, (float)g->x, (float)g->y, (float)g->w, (float)g->h, color, 0, 0, 0, effect);
        }
        x += g->xadvance;
    }
}
void BitmapFontExtensions::DrawString(SpriteBatch* sb, BitmapFont* font, String* text, Vector2 pos, Color color, Nullable<Rectangle> clip) {
    DrawString(sb, font, text, pos, color, 0, Vector2(), Vector2(1, 1), SpriteEffects::None, 0, clip);
}
}  // namespace MonoGame::Extended::BitmapFonts
