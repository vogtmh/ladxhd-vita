// Desktop OpenGL 3.2 core backend.
#include "gfx_internal.h"
#include "gl.h"
#include "platform.h"
#include <algorithm>
#include <cstdio>
#include <cstring>

using namespace ::System;

namespace fw::gfx {

static GLuint g_vao = 0, g_vbo = 0, g_ibo = 0;
static int g_iboQuads = 0;
static RenderTarget2D* g_bound = nullptr;  // target currently bound as GL framebuffer
static int g_backW = 0, g_backH = 0;
static GLint g_vp[4];  // current viewport in GL window coordinates

// Shaders are written in GLSL 1.10 style (attribute/varying/texture2D/gl_FragColor); the core profile gets these macros.
static const char* vsPrelude = "#version 150\n#define attribute in\n#define varying out\n";
static const char* fsPrelude =
    "#version 150\n#define varying in\n#define texture2D texture\nout vec4 fragColor_;\n#define gl_FragColor fragColor_\n";

static const char* spriteVS =
    "attribute vec2 a_pos;\n"
    "attribute vec4 a_color;\n"
    "attribute vec2 a_tex0;\n"
    "uniform mat4 u_matrix;\n"
    "uniform float u_flip;\n"
    "varying vec4 v_color;\n"
    "varying vec2 v_tex;\n"
    "void main() { vec4 p = u_matrix * vec4(a_pos, 0.0, 1.0); p.y *= u_flip; gl_Position = p; v_color = a_color; v_tex = a_tex0; }\n";
// v_fc reproduces gl_FragCoord (what MonoGame GL feeds HLSL SV_Position) without relying on it.
static const char* spriteFcVS =
    "attribute vec2 a_pos;\n"
    "attribute vec4 a_color;\n"
    "attribute vec2 a_tex0;\n"
    "uniform mat4 u_matrix;\n"
    "uniform float u_flip;\n"
    "uniform vec4 u_glvp;\n"
    "varying vec4 v_color;\n"
    "varying vec2 v_tex;\n"
    "varying vec2 v_fc;\n"
    "void main() {\n"
    "  vec4 p = u_matrix * vec4(a_pos, 0.0, 1.0); p.y *= u_flip; gl_Position = p;\n"
    "  v_color = a_color; v_tex = a_tex0;\n"
    "  v_fc = (p.xy / p.w * 0.5 + 0.5) * u_glvp.zw + u_glvp.xy;\n"
    "}\n";
static const char* spriteFS =
    "varying vec4 v_color;\n"
    "varying vec2 v_tex;\n"
    "uniform sampler2D s0;\n"
    "void main() { gl_FragColor = texture2D(s0, v_tex) * v_color; }\n";

struct GLProgram : Program {
    GLuint prog = 0;
    GLint uMatrix = -1, uFlip = -1, uGlvp = -1;
    std::vector<GLint> uniforms;  // per ParamDef
};

static GLuint compile(const char* vs, const char* fs, const char* name) {
    auto sh = [&](GLenum type, const char* src) {
        GLuint s = glCreateShader(type);
        std::string full = std::string(type == GL_VERTEX_SHADER ? vsPrelude : fsPrelude) + src;
        const char* p = full.c_str();
        glShaderSource(s, 1, &p, nullptr);
        glCompileShader(s);
        GLint ok = 0;
        glGetShaderiv(s, GL_COMPILE_STATUS, &ok);
        if (!ok) {
            char log[4096];
            glGetShaderInfoLog(s, sizeof log, nullptr, log);
            fw::plat::logf("shader %s (%s) failed:\n%s\n", name, type == GL_VERTEX_SHADER ? "vs" : "fs", log);
        }
        return s;
    };
    GLuint p = glCreateProgram();
    GLuint v = sh(GL_VERTEX_SHADER, vs), f = sh(GL_FRAGMENT_SHADER, fs);
    glAttachShader(p, v);
    glAttachShader(p, f);
    glBindAttribLocation(p, 0, "a_pos");
    glBindAttribLocation(p, 1, "a_color");
    glBindAttribLocation(p, 2, "a_tex0");
    glBindAttribLocation(p, 3, "a_tex1");
    glBindAttribLocation(p, 4, "a_tex2");
    glLinkProgram(p);
    GLint ok = 0;
    glGetProgramiv(p, GL_LINK_STATUS, &ok);
    if (!ok) {
        char log[4096];
        glGetProgramInfoLog(p, sizeof log, nullptr, log);
        fw::plat::logf("program %s link failed:\n%s\n", name, log);
    }
    glUseProgram(p);
    for (int i = 0; i < 4; i++) {
        char n[8];
        std::snprintf(n, sizeof n, "s%d", i);
        GLint loc = glGetUniformLocation(p, n);
        if (loc >= 0) glUniform1i(loc, i);
    }
    return p;
}

static GLProgram* makeProgram(const char* vs, const char* fs, const char* name, const EffectDef* def) {
    auto* p = new GLProgram();
    p->def = def;
    p->prog = compile(vs, fs, name);
    p->uMatrix = glGetUniformLocation(p->prog, "u_matrix");
    p->uFlip = glGetUniformLocation(p->prog, "u_flip");
    p->uGlvp = glGetUniformLocation(p->prog, "u_glvp");
    if (def)
        for (auto& pd : def->params) p->uniforms.push_back(pd.kind == ParamDef::Tex ? -1 : glGetUniformLocation(p->prog, pd.name));
    return p;
}

Program* beSpriteProgram() { return makeProgram(spriteVS, spriteFS, "sprite", nullptr); }
Program* beEffectProgram(const EffectDef* def) {
    const char* vs = def->vs ? def->vs : std::strstr(def->fs, "v_fc") ? spriteFcVS : spriteVS;
    return makeProgram(vs, def->fs, def->name, def);
}

static GLenum glBlend(Blend b) {
    switch (b) {
        case Blend::One: return GL_ONE;
        case Blend::Zero: return GL_ZERO;
        case Blend::SourceColor: return GL_SRC_COLOR;
        case Blend::InverseSourceColor: return GL_ONE_MINUS_SRC_COLOR;
        case Blend::SourceAlpha: return GL_SRC_ALPHA;
        case Blend::InverseSourceAlpha: return GL_ONE_MINUS_SRC_ALPHA;
        case Blend::DestinationColor: return GL_DST_COLOR;
        case Blend::InverseDestinationColor: return GL_ONE_MINUS_DST_COLOR;
        case Blend::DestinationAlpha: return GL_DST_ALPHA;
        case Blend::InverseDestinationAlpha: return GL_ONE_MINUS_DST_ALPHA;
        case Blend::BlendFactor: return GL_CONSTANT_COLOR;
        case Blend::InverseBlendFactor: return GL_ONE_MINUS_CONSTANT_COLOR;
        case Blend::SourceAlphaSaturation: return GL_SRC_ALPHA_SATURATE;
    }
    return GL_ONE;
}
static GLenum glFunc(BlendFunction f) {
    switch (f) {
        case BlendFunction::Add: return GL_FUNC_ADD;
        case BlendFunction::Subtract: return GL_FUNC_SUBTRACT;
        case BlendFunction::ReverseSubtract: return GL_FUNC_REVERSE_SUBTRACT;
        case BlendFunction::Min: return GL_MIN;
        case BlendFunction::Max: return GL_MAX;
    }
    return GL_FUNC_ADD;
}
static void applyBlend(BlendState* b) {
    if (!b) b = BlendState::AlphaBlend;
    if (b->colorSrc == Blend::One && b->colorDst == Blend::Zero && b->alphaSrc == Blend::One && b->alphaDst == Blend::Zero) {
        glDisable(GL_BLEND);
        return;
    }
    glEnable(GL_BLEND);
    glBlendFuncSeparate(glBlend(b->colorSrc), glBlend(b->colorDst), glBlend(b->alphaSrc), glBlend(b->alphaDst));
    glBlendEquationSeparate(glFunc(b->colorFn), glFunc(b->alphaFn));
}

static void bindTexture(int unit, Texture* t, SamplerState* s) {
    glActiveTexture(GL_TEXTURE0 + unit);
    glBindTexture(GL_TEXTURE_2D, t ? t->glTex : 0);
    if (!t) return;
    if (!s) s = SamplerState::LinearClamp;
    int32_t key = (s->filter == TextureFilter::Point ? 1 : 0) | (s->addressU == TextureAddressMode::Wrap ? 2 : 0) |
                  (s->addressV == TextureAddressMode::Wrap ? 4 : 0);
    if (t->samplerKey == key) return;
    t->samplerKey = key;
    GLint filter = (key & 1) ? GL_NEAREST : GL_LINEAR;
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, filter);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, filter);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, (key & 2) ? GL_REPEAT : GL_CLAMP_TO_EDGE);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, (key & 4) ? GL_REPEAT : GL_CLAMP_TO_EDGE);
}

static int boundHeight() { return g_bound ? g_bound->height : g_backH; }

static void applyScissor(const DrawState& ds) {
    if (!ds.scissor) {
        glDisable(GL_SCISSOR_TEST);
        return;
    }
    glEnable(GL_SCISSOR_TEST);
    const Rectangle& r = ds.scissorRect;
    int y = g_bound ? r.Y : boundHeight() - (r.Y + r.Height);
    glScissor(r.X, y, std::max(0, r.Width), std::max(0, r.Height));
}

// Common state for both draw paths: program, uniforms, blend, scissor, textures.
static void applyDrawState(const DrawState& ds) {
    auto* p = static_cast<GLProgram*>(ds.prog);
    glUseProgram(p->prog);
    if (p->uMatrix >= 0) glUniformMatrix4fv(p->uMatrix, 1, GL_FALSE, &ds.matrix.M11);
    // Render targets are drawn with clip-space Y flipped (like MonoGame's posFixup) so texture row 0 is the top.
    if (p->uFlip >= 0) glUniform1f(p->uFlip, g_bound ? -1.0f : 1.0f);
    if (p->uGlvp >= 0) {
        // Top-left origin on the back buffer: mirror the bottom-left viewport rows.
        if (!g_bound && backBufferAsTarget)
            glUniform4f(p->uGlvp, (float)g_vp[0], (float)(boundHeight() - g_vp[1]), (float)g_vp[2], -(float)g_vp[3]);
        else
            glUniform4f(p->uGlvp, (float)g_vp[0], (float)g_vp[1], (float)g_vp[2], (float)g_vp[3]);
    }
    if (p->def && ds.effect) {
        auto* list = ds.effect->parameters->list;
        for (int32_t i = 0; i < list->size; i++) {
            EffectParameter* ep = list->items->data[i];
            if (ep->index < 0 || ep->index >= (int)p->def->params.size()) continue;
            GLint loc = p->uniforms[ep->index];
            if (loc < 0) continue;
            switch (p->def->params[ep->index].kind) {
                case ParamDef::Float:
                case ParamDef::Int: glUniform1f(loc, ep->value[0]); break;
                case ParamDef::Vec2: glUniform2fv(loc, 1, ep->value); break;
                case ParamDef::Vec3: glUniform3fv(loc, 1, ep->value); break;
                case ParamDef::Vec4: glUniform4fv(loc, 1, ep->value); break;
                case ParamDef::Mat4: glUniformMatrix4fv(loc, 1, GL_FALSE, ep->value); break;
                case ParamDef::Tex: break;
            }
        }
    }
    applyBlend(ds.blend);
    applyScissor(ds);
    for (int u = 0; u < 4; u++)
        if (u == 0 || ds.tex[u]) bindTexture(u, ds.tex[u], ds.samp[u]);
}

static void ensureBuffers(int quads) {
    if (!g_vao) {
        glGenVertexArrays(1, &g_vao);
        glGenBuffers(1, &g_vbo);
        glGenBuffers(1, &g_ibo);
    }
    glBindVertexArray(g_vao);
    glBindBuffer(GL_ARRAY_BUFFER, g_vbo);
    glBindBuffer(GL_ELEMENT_ARRAY_BUFFER, g_ibo);
    if (quads > g_iboQuads) {
        int n = std::max(quads, 2048);
        std::vector<uint16_t> idx(n * 6);
        for (int i = 0; i < n; i++) {
            uint16_t b = (uint16_t)(i * 4);
            idx[i * 6 + 0] = b;
            idx[i * 6 + 1] = b + 1;
            idx[i * 6 + 2] = b + 2;
            idx[i * 6 + 3] = b + 1;
            idx[i * 6 + 4] = b + 3;
            idx[i * 6 + 5] = b + 2;
        }
        glBufferData(GL_ELEMENT_ARRAY_BUFFER, idx.size() * 2, idx.data(), GL_STATIC_DRAW);
        g_iboQuads = n;
    }
}

void beDrawQuads(const DrawState& ds, const Vertex* verts, int quadCount) {
    applyDrawState(ds);
    ensureBuffers(quadCount);
    glBufferData(GL_ARRAY_BUFFER, (GLsizeiptr)quadCount * 4 * sizeof(Vertex), verts, GL_STREAM_DRAW);
    glEnableVertexAttribArray(0);
    glEnableVertexAttribArray(1);
    glEnableVertexAttribArray(2);
    glDisableVertexAttribArray(3);
    glDisableVertexAttribArray(4);
    // uint16 indices: split very large batches; rebasing the attribute pointers avoids glDrawElementsBaseVertex
    int done = 0;
    while (done < quadCount) {
        int n = std::min(quadCount - done, 16000);
        const char* base = (const char*)((size_t)done * 4 * sizeof(Vertex));
        glVertexAttribPointer(0, 2, GL_FLOAT, GL_FALSE, sizeof(Vertex), base + 0);
        glVertexAttribPointer(1, 4, GL_UNSIGNED_BYTE, GL_TRUE, sizeof(Vertex), base + 8);
        glVertexAttribPointer(2, 2, GL_FLOAT, GL_FALSE, sizeof(Vertex), base + 12);
        glDrawElements(GL_TRIANGLES, n * 6, GL_UNSIGNED_SHORT, nullptr);
        done += n;
    }
}

void beDrawIndexed(const DrawState& ds, int stride, const void* vertices, int numVertices, const int16_t* indices, int primitiveCount,
                   const VertexDeclaration* decl) {
    applyDrawState(ds);
    static GLuint vao = 0, vbo = 0, ibo = 0;
    if (!vao) {
        glGenVertexArrays(1, &vao);
        glGenBuffers(1, &vbo);
        glGenBuffers(1, &ibo);
    }
    glBindVertexArray(vao);
    glBindBuffer(GL_ARRAY_BUFFER, vbo);
    glBufferData(GL_ARRAY_BUFFER, (GLsizeiptr)stride * numVertices, vertices, GL_STREAM_DRAW);
    glBindBuffer(GL_ELEMENT_ARRAY_BUFFER, ibo);
    glBufferData(GL_ELEMENT_ARRAY_BUFFER, (GLsizeiptr)primitiveCount * 3 * 2, indices, GL_STREAM_DRAW);
    for (int i = 0; i < 5; i++) glDisableVertexAttribArray(i);
    for (auto& el : *decl->elements) {
        int loc = -1, comps = 2;
        GLenum type = GL_FLOAT;
        GLboolean norm = GL_FALSE;
        if (el.usage == VertexElementUsage::Position) loc = 0;
        else if (el.usage == VertexElementUsage::Color) loc = 1;
        else if (el.usage == VertexElementUsage::TextureCoordinate) loc = 2 + el.usageIndex;
        switch (el.format) {
            case VertexElementFormat::Single: comps = 1; break;
            case VertexElementFormat::Vector2: comps = 2; break;
            case VertexElementFormat::Vector3: comps = 3; break;
            case VertexElementFormat::Vector4: comps = 4; break;
            case VertexElementFormat::Color: comps = 4; type = GL_UNSIGNED_BYTE; norm = GL_TRUE; break;
            default: break;
        }
        if (loc < 0 || loc > 4) continue;
        glEnableVertexAttribArray(loc);
        glVertexAttribPointer(loc, comps, type, norm, stride, (const char*)nullptr + el.offset);
    }
    glDrawElements(GL_TRIANGLES, primitiveCount * 3, GL_UNSIGNED_SHORT, nullptr);
}

void beInit(int backW, int backH) {
    g_backW = backW;
    g_backH = backH;
    glDisable(GL_CULL_FACE);
    glDisable(GL_DEPTH_TEST);
}
void beBeginFrame() {}
void bePresent() { fw::plat::swap(); }
double beFrameDelta(double measured) { return measured; }

void beBindTarget(RenderTarget2D* rt) {
    g_bound = rt;
    glBindFramebuffer(GL_FRAMEBUFFER, rt ? rt->fbo : 0);
}
void beSetViewport(int x, int y, int w, int h) {
    g_vp[0] = x;
    g_vp[1] = g_bound ? y : boundHeight() - (y + h);
    g_vp[2] = w;
    g_vp[3] = h;
    glViewport(g_vp[0], g_vp[1], g_vp[2], g_vp[3]);
}
void beClear(float r, float g, float b, float a) {
    glDisable(GL_SCISSOR_TEST);
    glClearColor(r, g, b, a);
    glClear(GL_COLOR_BUFFER_BIT);
}

void beCreateTexture(Texture* t, int w, int h) {
    textureBytes += (size_t)w * h * 4;
    glGenTextures(1, &t->glTex);
    glActiveTexture(GL_TEXTURE0);
    glBindTexture(GL_TEXTURE_2D, t->glTex);
    glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, w, h, 0, GL_RGBA, GL_UNSIGNED_BYTE, nullptr);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
    t->samplerKey = -1;
}
void beReleaseTexture(Texture* t) {
    if (t->glTex) {
        glDeleteTextures(1, &t->glTex);
        textureBytes -= (size_t)t->width * t->height * 4;
    }
    t->glTex = 0;
}
void beUpload(Texture* t, int x, int y, int w, int h, const void* rgba) {
    glActiveTexture(GL_TEXTURE0);
    glBindTexture(GL_TEXTURE_2D, t->glTex);
    glPixelStorei(GL_UNPACK_ALIGNMENT, 1);
    glTexSubImage2D(GL_TEXTURE_2D, 0, x, y, w, h, GL_RGBA, GL_UNSIGNED_BYTE, rgba);
}
void beDownload(Texture* t, int x, int y, int w, int h, void* rgba) {
    GLuint fbo;
    GLint prev;
    glGetIntegerv(GL_FRAMEBUFFER_BINDING, &prev);
    glGenFramebuffers(1, &fbo);
    glBindFramebuffer(GL_FRAMEBUFFER, fbo);
    glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, t->glTex, 0);
    glPixelStorei(GL_PACK_ALIGNMENT, 1);
    glReadPixels(x, y, w, h, GL_RGBA, GL_UNSIGNED_BYTE, rgba);
    glBindFramebuffer(GL_FRAMEBUFFER, prev);
    glDeleteFramebuffers(1, &fbo);
}
void beCreateTarget(RenderTarget2D* rt) {
    beCreateTexture(rt, rt->width, rt->height);
    glGenFramebuffers(1, &rt->fbo);
    GLint prev;
    glGetIntegerv(GL_FRAMEBUFFER_BINDING, &prev);
    glBindFramebuffer(GL_FRAMEBUFFER, rt->fbo);
    glFramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_2D, rt->glTex, 0);
    glClearColor(0, 0, 0, 0);
    glClear(GL_COLOR_BUFFER_BIT);
    glBindFramebuffer(GL_FRAMEBUFFER, prev);
}
void beReleaseTarget(RenderTarget2D* rt) {
    if (rt->fbo) glDeleteFramebuffers(1, &rt->fbo);
    rt->fbo = 0;
    beReleaseTexture(rt);
}

bool beReadBackBuffer(int w, int h, uint8_t* rgbaTopDown) {
    std::vector<uint8_t> px((size_t)w * h * 4);
    glBindFramebuffer(GL_FRAMEBUFFER, 0);
    glPixelStorei(GL_PACK_ALIGNMENT, 1);
    glReadPixels(0, 0, w, h, GL_RGBA, GL_UNSIGNED_BYTE, px.data());
    for (int y = 0; y < h; y++) std::memcpy(rgbaTopDown + (size_t)y * w * 4, &px[(size_t)(h - 1 - y) * w * 4], (size_t)w * 4);
    glBindFramebuffer(GL_FRAMEBUFFER, g_bound ? g_bound->fbo : 0);
    return true;
}

}  // namespace fw::gfx
