// MonoGame math types, type descriptors and enum tables.
#include "fw/all.h"
#include <cstdarg>
#include <cstdio>

using namespace ::System;

namespace Microsoft::Xna::Framework {

static String* fmt(const char* f, ...) __attribute__((format(printf, 1, 2)));
static String* fmt(const char* f, ...) {
    char buf[256];
    va_list ap;
    va_start(ap, f);
    std::vsnprintf(buf, sizeof buf, f, ap);
    va_end(ap);
    return String::FromUtf8(buf);
}

Vector2 Vector2::Transform(Vector2 p, const Matrix& m) {
    return Vector2(p.X * m.M11 + p.Y * m.M21 + m.M41, p.X * m.M12 + p.Y * m.M22 + m.M42);
}
String* Vector2::ToString() { return String::ConcatN({STR(u"{X:"), ToStr(X), STR(u" Y:"), ToStr(Y), STR(u"}")}); }
String* Vector3::ToString() { return String::ConcatN({STR(u"{X:"), ToStr(X), STR(u" Y:"), ToStr(Y), STR(u" Z:"), ToStr(Z), STR(u"}")}); }
String* Vector4::ToString() { return String::ConcatN({STR(u"{X:"), ToStr(X), STR(u" Y:"), ToStr(Y), STR(u" Z:"), ToStr(Z), STR(u" W:"), ToStr(W), STR(u"}")}); }
String* Point::ToString() { return fmt("{X:%d Y:%d}", X, Y); }
String* Rectangle::ToString() { return fmt("{X:%d Y:%d Width:%d Height:%d}", X, Y, Width, Height); }
String* Color::ToString() { return fmt("{R:%d G:%d B:%d A:%d}", get_R(), get_G(), get_B(), get_A()); }

Rectangle Rectangle::Intersect(Rectangle a, Rectangle b) {
    if (!a.Intersects(b)) return Rectangle();
    int32_t right = std::min(a.X + a.Width, b.X + b.Width);
    int32_t left = std::max(a.X, b.X);
    int32_t top = std::max(a.Y, b.Y);
    int32_t bottom = std::min(a.Y + a.Height, b.Y + b.Height);
    return Rectangle(left, top, right - left, bottom - top);
}
Rectangle Rectangle::Union(Rectangle a, Rectangle b) {
    int32_t x = std::min(a.X, b.X), y = std::min(a.Y, b.Y);
    return Rectangle(x, y, std::max(a.get_Right(), b.get_Right()) - x, std::max(a.get_Bottom(), b.get_Bottom()) - y);
}

Color Color::Lerp(Color a, Color b, float t) {
    t = MathHelper::Clamp(t, 0.0f, 1.0f);
    auto l = [&](int32_t x, int32_t y) { return (uint8_t)(int32_t)(x + (y - x) * t); };
    return Color(l(a.get_R(), b.get_R()), l(a.get_G(), b.get_G()), l(a.get_B(), b.get_B()), l(a.get_A(), b.get_A()));
}
Color Color::operator*(float s) const {
    auto m = [&](uint8_t c) -> uint8_t {
        int32_t v = (int32_t)(c * s);
        return (uint8_t)(v < 0 ? 0 : (v > 255 ? 255 : v));
    };
    return Color(m(get_R()), m(get_G()), m(get_B()), m(get_A()));
}

Matrix Matrix::operator*(const Matrix& b) const {
    const Matrix& a = *this;
    Matrix r;
    r.M11 = a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31 + a.M14 * b.M41;
    r.M12 = a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32 + a.M14 * b.M42;
    r.M13 = a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33 + a.M14 * b.M43;
    r.M14 = a.M11 * b.M14 + a.M12 * b.M24 + a.M13 * b.M34 + a.M14 * b.M44;
    r.M21 = a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31 + a.M24 * b.M41;
    r.M22 = a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32 + a.M24 * b.M42;
    r.M23 = a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33 + a.M24 * b.M43;
    r.M24 = a.M21 * b.M14 + a.M22 * b.M24 + a.M23 * b.M34 + a.M24 * b.M44;
    r.M31 = a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31 + a.M34 * b.M41;
    r.M32 = a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32 + a.M34 * b.M42;
    r.M33 = a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33 + a.M34 * b.M43;
    r.M34 = a.M31 * b.M14 + a.M32 * b.M24 + a.M33 * b.M34 + a.M34 * b.M44;
    r.M41 = a.M41 * b.M11 + a.M42 * b.M21 + a.M43 * b.M31 + a.M44 * b.M41;
    r.M42 = a.M41 * b.M12 + a.M42 * b.M22 + a.M43 * b.M32 + a.M44 * b.M42;
    r.M43 = a.M41 * b.M13 + a.M42 * b.M23 + a.M43 * b.M33 + a.M44 * b.M43;
    r.M44 = a.M41 * b.M14 + a.M42 * b.M24 + a.M43 * b.M34 + a.M44 * b.M44;
    return r;
}
Matrix Matrix::CreateRotationZ(float rad) {
    Matrix m = get_Identity();
    float c = std::cos(rad), s = std::sin(rad);
    m.M11 = c;
    m.M12 = s;
    m.M21 = -s;
    m.M22 = c;
    return m;
}
Matrix Matrix::CreateOrthographicOffCenter(float l, float r, float b, float t, float n, float f) {
    Matrix m;
    m.M11 = 2.0f / (r - l);
    m.M22 = 2.0f / (t - b);
    m.M33 = 1.0f / (n - f);
    m.M41 = (l + r) / (l - r);
    m.M42 = (t + b) / (b - t);
    m.M43 = n / (n - f);
    m.M44 = 1.0f;
    return m;
}
Matrix Matrix::Invert(Matrix m) {
    // General 4x4 inverse (cofactor expansion).
    float a[16] = {m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44};
    float inv[16];
    inv[0] = a[5] * a[10] * a[15] - a[5] * a[11] * a[14] - a[9] * a[6] * a[15] + a[9] * a[7] * a[14] + a[13] * a[6] * a[11] - a[13] * a[7] * a[10];
    inv[4] = -a[4] * a[10] * a[15] + a[4] * a[11] * a[14] + a[8] * a[6] * a[15] - a[8] * a[7] * a[14] - a[12] * a[6] * a[11] + a[12] * a[7] * a[10];
    inv[8] = a[4] * a[9] * a[15] - a[4] * a[11] * a[13] - a[8] * a[5] * a[15] + a[8] * a[7] * a[13] + a[12] * a[5] * a[11] - a[12] * a[7] * a[9];
    inv[12] = -a[4] * a[9] * a[14] + a[4] * a[10] * a[13] + a[8] * a[5] * a[14] - a[8] * a[6] * a[13] - a[12] * a[5] * a[10] + a[12] * a[6] * a[9];
    inv[1] = -a[1] * a[10] * a[15] + a[1] * a[11] * a[14] + a[9] * a[2] * a[15] - a[9] * a[3] * a[14] - a[13] * a[2] * a[11] + a[13] * a[3] * a[10];
    inv[5] = a[0] * a[10] * a[15] - a[0] * a[11] * a[14] - a[8] * a[2] * a[15] + a[8] * a[3] * a[14] + a[12] * a[2] * a[11] - a[12] * a[3] * a[10];
    inv[9] = -a[0] * a[9] * a[15] + a[0] * a[11] * a[13] + a[8] * a[1] * a[15] - a[8] * a[3] * a[13] - a[12] * a[1] * a[11] + a[12] * a[3] * a[9];
    inv[13] = a[0] * a[9] * a[14] - a[0] * a[10] * a[13] - a[8] * a[1] * a[14] + a[8] * a[2] * a[13] + a[12] * a[1] * a[10] - a[12] * a[2] * a[9];
    inv[2] = a[1] * a[6] * a[15] - a[1] * a[7] * a[14] - a[5] * a[2] * a[15] + a[5] * a[3] * a[14] + a[13] * a[2] * a[7] - a[13] * a[3] * a[6];
    inv[6] = -a[0] * a[6] * a[15] + a[0] * a[7] * a[14] + a[4] * a[2] * a[15] - a[4] * a[3] * a[14] - a[12] * a[2] * a[7] + a[12] * a[3] * a[6];
    inv[10] = a[0] * a[5] * a[15] - a[0] * a[7] * a[13] - a[4] * a[1] * a[15] + a[4] * a[3] * a[13] + a[12] * a[1] * a[7] - a[12] * a[3] * a[5];
    inv[14] = -a[0] * a[5] * a[14] + a[0] * a[6] * a[13] + a[4] * a[1] * a[14] - a[4] * a[2] * a[13] - a[12] * a[1] * a[6] + a[12] * a[2] * a[5];
    inv[3] = -a[1] * a[6] * a[11] + a[1] * a[7] * a[10] + a[5] * a[2] * a[11] - a[5] * a[3] * a[10] - a[9] * a[2] * a[7] + a[9] * a[3] * a[6];
    inv[7] = a[0] * a[6] * a[11] - a[0] * a[7] * a[10] - a[4] * a[2] * a[11] + a[4] * a[3] * a[10] + a[8] * a[2] * a[7] - a[8] * a[3] * a[6];
    inv[11] = -a[0] * a[5] * a[11] + a[0] * a[7] * a[9] + a[4] * a[1] * a[11] - a[4] * a[3] * a[9] - a[8] * a[1] * a[7] + a[8] * a[3] * a[5];
    inv[15] = a[0] * a[5] * a[10] - a[0] * a[6] * a[9] - a[4] * a[1] * a[10] + a[4] * a[2] * a[9] + a[8] * a[1] * a[6] - a[8] * a[2] * a[5];
    float det = a[0] * inv[0] + a[1] * inv[4] + a[2] * inv[8] + a[3] * inv[12];
    Matrix r;
    if (det == 0) return r;
    det = 1.0f / det;
    float* o = &r.M11;
    for (int i = 0; i < 16; i++) o[i] = inv[i] * det;
    return r;
}
float MathHelper::WrapAngle(float a) {
    if (a > -Pi && a <= Pi) return a;
    a = std::remainder(a, TwoPi);
    if (a <= -Pi) return a + TwoPi;
    if (a > Pi) return a - TwoPi;
    return a;
}

#define FW_STRUCT_TYPE(T, N)                                                          \
    Type* T::__type() {                                                               \
        static Type* t = [] { auto* x = Type::Create(u## #T, N, nullptr); x->isValueType = true; return x; }(); \
        return t;                                                                     \
    }
FW_STRUCT_TYPE(Vector2, u"Microsoft.Xna.Framework.Vector2")
FW_STRUCT_TYPE(Vector3, u"Microsoft.Xna.Framework.Vector3")
FW_STRUCT_TYPE(Vector4, u"Microsoft.Xna.Framework.Vector4")
FW_STRUCT_TYPE(Point, u"Microsoft.Xna.Framework.Point")
FW_STRUCT_TYPE(Rectangle, u"Microsoft.Xna.Framework.Rectangle")
FW_STRUCT_TYPE(Color, u"Microsoft.Xna.Framework.Color")
FW_STRUCT_TYPE(Matrix, u"Microsoft.Xna.Framework.Matrix")

}  // namespace Microsoft::Xna::Framework

// ---------------------------------------------------------------- enum tables
namespace Microsoft::Xna::Framework::Input {
#define FW_NAME(N, V) u## #N,
#define FW_VAL(N, V) V,
#define FW_ENUMV(N, V) Keys::N,
static const int64_t keyVals[] = {FW_KEYS(FW_VAL)};
static const char16_t* const keyNames[] = {FW_KEYS(FW_NAME)};
String* __EnumToString(Keys v) { return EnumToStringImpl((int64_t)v, keyVals, keyNames, (int32_t)(sizeof keyVals / sizeof keyVals[0]), false); }
Array1<Keys>* __EnumValues(Keys*) { return Array1<Keys>::Of({FW_KEYS(FW_ENUMV)}); }
Type* __EnumType(Keys*) { static Type* t = EnumTypeCreate(u"Keys", u"Microsoft.Xna.Framework.Input.Keys", EnumBoxer<Keys>()); return t; }
#undef FW_ENUMV
#define FW_ENUMV(N, V) Buttons::N,
static const int64_t btnVals[] = {FW_BUTTONS(FW_VAL)};
static const char16_t* const btnNames[] = {FW_BUTTONS(FW_NAME)};
String* __EnumToString(Buttons v) { return EnumToStringImpl((int64_t)v, btnVals, btnNames, (int32_t)(sizeof btnVals / sizeof btnVals[0]), true); }
Array1<Buttons>* __EnumValues(Buttons*) { return Array1<Buttons>::Of({FW_BUTTONS(FW_ENUMV)}); }
Type* __EnumType(Buttons*) { static Type* t = EnumTypeCreate(u"Buttons", u"Microsoft.Xna.Framework.Input.Buttons", EnumBoxer<Buttons>()); return t; }
}  // namespace Microsoft::Xna::Framework::Input

namespace Microsoft::Xna::Framework::Graphics {
#define FW_ENUM_IMPL(E)                                                                        \
    Array1<E>* __EnumValues(E*) { return Array1<E>::New(0); }                                  \
    Type* __EnumType(E*) { static Type* t = EnumTypeCreate(u## #E, u"Microsoft.Xna.Framework.Graphics." #E, EnumBoxer<E>()); return t; }
FW_ENUM_IMPL(Blend)
FW_ENUM_IMPL(BlendFunction)
FW_ENUM_IMPL(CullMode)
FW_ENUM_IMPL(DepthFormat)
FW_ENUM_IMPL(GraphicsProfile)
FW_ENUM_IMPL(PrimitiveType)
FW_ENUM_IMPL(RenderTargetUsage)
FW_ENUM_IMPL(SpriteEffects)
FW_ENUM_IMPL(SpriteSortMode)
FW_ENUM_IMPL(SurfaceFormat)
FW_ENUM_IMPL(VertexElementFormat)
FW_ENUM_IMPL(VertexElementUsage)
}  // namespace Microsoft::Xna::Framework::Graphics
