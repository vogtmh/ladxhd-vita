#pragma once
// MonoGame API subset used by the game, implemented natively (SDL2+OpenGL on desktop, vitaGL on Vita).
#include "rt/all.h"

namespace Microsoft::Xna::Framework {
using ::System::String;
using ::System::Object;

struct Point;
struct Vector3;
struct Matrix;

// ---------------------------------------------------------------- math types
struct Vector2 {
    float X = 0, Y = 0;
    Vector2() = default;
    constexpr Vector2(float x, float y) : X(x), Y(y) {}
    explicit constexpr Vector2(float v) : X(v), Y(v) {}
    static Vector2 get_Zero() { return Vector2(); }
    static Vector2 get_One() { return Vector2(1, 1); }
    static Vector2 get_UnitX() { return Vector2(1, 0); }
    static Vector2 get_UnitY() { return Vector2(0, 1); }
    float Length() const { return std::sqrt(X * X + Y * Y); }
    float LengthSquared() const { return X * X + Y * Y; }
    void Normalize() { float l = 1.0f / std::sqrt(X * X + Y * Y); X *= l; Y *= l; }
    static Vector2 Normalize(Vector2 v) { v.Normalize(); return v; }
    static float Distance(Vector2 a, Vector2 b) { return (a - b).Length(); }
    static float DistanceSquared(Vector2 a, Vector2 b) { return (a - b).LengthSquared(); }
    static float Dot(Vector2 a, Vector2 b) { return a.X * b.X + a.Y * b.Y; }
    static Vector2 Lerp(Vector2 a, Vector2 b, float t) { return Vector2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t); }
    static Vector2 Transform(Vector2 p, const Matrix& m);
    static Vector2 Min(Vector2 a, Vector2 b) { return Vector2(a.X < b.X ? a.X : b.X, a.Y < b.Y ? a.Y : b.Y); }
    static Vector2 Max(Vector2 a, Vector2 b) { return Vector2(a.X > b.X ? a.X : b.X, a.Y > b.Y ? a.Y : b.Y); }
    static Vector2 Clamp(Vector2 v, Vector2 lo, Vector2 hi) { return Min(Max(v, lo), hi); }
    Point ToPoint() const;
    bool Equals(const Vector2& o) const { return X == o.X && Y == o.Y; }
    int32_t GetHashCode() const { return ::System::Eq<float>::hash(X) * 397 ^ ::System::Eq<float>::hash(Y); }
    String* ToString();
    Vector2 operator+(Vector2 o) const { return Vector2(X + o.X, Y + o.Y); }
    Vector2 operator-(Vector2 o) const { return Vector2(X - o.X, Y - o.Y); }
    Vector2 operator*(Vector2 o) const { return Vector2(X * o.X, Y * o.Y); }
    Vector2 operator/(Vector2 o) const { return Vector2(X / o.X, Y / o.Y); }
    Vector2 operator*(float s) const { return Vector2(X * s, Y * s); }
    Vector2 operator/(float s) const { float f = 1.0f / s; return Vector2(X * f, Y * f); }
    Vector2 operator-() const { return Vector2(-X, -Y); }
    Vector2& operator+=(Vector2 o) { X += o.X; Y += o.Y; return *this; }
    Vector2& operator-=(Vector2 o) { X -= o.X; Y -= o.Y; return *this; }
    Vector2& operator*=(float s) { X *= s; Y *= s; return *this; }
    Vector2& operator*=(Vector2 o) { X *= o.X; Y *= o.Y; return *this; }
    Vector2& operator/=(float s) { float f = 1.0f / s; X *= f; Y *= f; return *this; }
    bool operator==(Vector2 o) const { return X == o.X && Y == o.Y; }
    bool operator!=(Vector2 o) const { return !(*this == o); }
    static ::System::Type* __type();
};
inline Vector2 operator*(float s, Vector2 v) { return v * s; }

struct Vector3 {
    float X = 0, Y = 0, Z = 0;
    Vector3() = default;
    constexpr Vector3(float x, float y, float z) : X(x), Y(y), Z(z) {}
    constexpr Vector3(Vector2 v, float z) : X(v.X), Y(v.Y), Z(z) {}
    explicit constexpr Vector3(float v) : X(v), Y(v), Z(v) {}
    static Vector3 get_Zero() { return Vector3(); }
    static Vector3 get_One() { return Vector3(1, 1, 1); }
    float Length() const { return std::sqrt(X * X + Y * Y + Z * Z); }
    float LengthSquared() const { return X * X + Y * Y + Z * Z; }
    void Normalize() { float l = 1.0f / Length(); X *= l; Y *= l; Z *= l; }
    static Vector3 Lerp(Vector3 a, Vector3 b, float t) { return Vector3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t); }
    bool Equals(const Vector3& o) const { return X == o.X && Y == o.Y && Z == o.Z; }
    int32_t GetHashCode() const { return (::System::Eq<float>::hash(X) * 397 ^ ::System::Eq<float>::hash(Y)) * 397 ^ ::System::Eq<float>::hash(Z); }
    String* ToString();
    Vector3 operator+(Vector3 o) const { return Vector3(X + o.X, Y + o.Y, Z + o.Z); }
    Vector3 operator-(Vector3 o) const { return Vector3(X - o.X, Y - o.Y, Z - o.Z); }
    Vector3 operator*(float s) const { return Vector3(X * s, Y * s, Z * s); }
    Vector3 operator*(Vector3 o) const { return Vector3(X * o.X, Y * o.Y, Z * o.Z); }
    Vector3 operator/(float s) const { float f = 1.0f / s; return Vector3(X * f, Y * f, Z * f); }
    Vector3 operator-() const { return Vector3(-X, -Y, -Z); }
    Vector3& operator+=(Vector3 o) { X += o.X; Y += o.Y; Z += o.Z; return *this; }
    Vector3& operator-=(Vector3 o) { X -= o.X; Y -= o.Y; Z -= o.Z; return *this; }
    Vector3& operator*=(float s) { X *= s; Y *= s; Z *= s; return *this; }
    bool operator==(Vector3 o) const { return X == o.X && Y == o.Y && Z == o.Z; }
    bool operator!=(Vector3 o) const { return !(*this == o); }
    static ::System::Type* __type();
};
inline Vector3 operator*(float s, Vector3 v) { return v * s; }

struct Vector4 {
    float X = 0, Y = 0, Z = 0, W = 0;
    Vector4() = default;
    constexpr Vector4(float x, float y, float z, float w) : X(x), Y(y), Z(z), W(w) {}
    bool Equals(const Vector4& o) const { return X == o.X && Y == o.Y && Z == o.Z && W == o.W; }
    int32_t GetHashCode() const { return ::System::Eq<float>::hash(X) ^ ::System::Eq<float>::hash(Y) ^ ::System::Eq<float>::hash(Z) ^ ::System::Eq<float>::hash(W); }
    bool operator==(Vector4 o) const { return Equals(o); }
    bool operator!=(Vector4 o) const { return !Equals(o); }
    Vector4 operator*(float s) const { return Vector4(X * s, Y * s, Z * s, W * s); }
    String* ToString();
    static ::System::Type* __type();
};

struct Point {
    int32_t X = 0, Y = 0;
    Point() = default;
    constexpr Point(int32_t x, int32_t y) : X(x), Y(y) {}
    explicit constexpr Point(int32_t v) : X(v), Y(v) {}
    static Point get_Zero() { return Point(); }
    Vector2 ToVector2() const { return Vector2((float)X, (float)Y); }
    bool Equals(const Point& o) const { return X == o.X && Y == o.Y; }
    int32_t GetHashCode() const { return X * 397 ^ Y; }
    String* ToString();
    bool operator==(Point o) const { return X == o.X && Y == o.Y; }
    bool operator!=(Point o) const { return !(*this == o); }
    Point operator+(Point o) const { return Point(X + o.X, Y + o.Y); }
    Point operator-(Point o) const { return Point(X - o.X, Y - o.Y); }
    Point operator*(Point o) const { return Point(X * o.X, Y * o.Y); }
    Point operator/(Point o) const { return Point(X / o.X, Y / o.Y); }
    static ::System::Type* __type();
};
inline Point Vector2::ToPoint() const { return Point((int32_t)X, (int32_t)Y); }

struct Rectangle {
    int32_t X = 0, Y = 0, Width = 0, Height = 0;
    Rectangle() = default;
    constexpr Rectangle(int32_t x, int32_t y, int32_t w, int32_t h) : X(x), Y(y), Width(w), Height(h) {}
    constexpr Rectangle(Point loc, Point size) : X(loc.X), Y(loc.Y), Width(size.X), Height(size.Y) {}
    static Rectangle get_Empty() { return Rectangle(); }
    int32_t get_Left() const { return X; }
    int32_t get_Right() const { return X + Width; }
    int32_t get_Top() const { return Y; }
    int32_t get_Bottom() const { return Y + Height; }
    Point get_Center() const { return Point(X + Width / 2, Y + Height / 2); }
    Point get_Location() const { return Point(X, Y); }
    void set_Location(Point p) { X = p.X; Y = p.Y; }
    Point get_Size() const { return Point(Width, Height); }
    void set_Size(Point p) { Width = p.X; Height = p.Y; }
    bool get_IsEmpty() const { return Width == 0 && Height == 0 && X == 0 && Y == 0; }
    bool Contains(int32_t x, int32_t y) const { return X <= x && x < X + Width && Y <= y && y < Y + Height; }
    bool Contains(float x, float y) const { return X <= x && x < X + Width && Y <= y && y < Y + Height; }
    bool Contains(Point p) const { return Contains(p.X, p.Y); }
    bool Contains(Vector2 p) const { return Contains(p.X, p.Y); }
    bool Contains(Rectangle r) const { return X <= r.X && r.X + r.Width <= X + Width && Y <= r.Y && r.Y + r.Height <= Y + Height; }
    bool Intersects(Rectangle r) const { return r.X < X + Width && X < r.X + r.Width && r.Y < Y + Height && Y < r.Y + r.Height; }
    void Offset(Point p) { X += p.X; Y += p.Y; }
    void Offset(int32_t x, int32_t y) { X += x; Y += y; }
    void Offset(float x, float y) { X += (int32_t)x; Y += (int32_t)y; }
    void Offset(Vector2 v) { X += (int32_t)v.X; Y += (int32_t)v.Y; }
    void Inflate(int32_t h, int32_t v) { X -= h; Y -= v; Width += h * 2; Height += v * 2; }
    void Inflate(float h, float v) { Inflate((int32_t)h, (int32_t)v); }
    static Rectangle Intersect(Rectangle a, Rectangle b);
    static Rectangle Union(Rectangle a, Rectangle b);
    bool Equals(const Rectangle& o) const { return X == o.X && Y == o.Y && Width == o.Width && Height == o.Height; }
    int32_t GetHashCode() const { return ((X * 397 ^ Y) * 397 ^ Width) * 397 ^ Height; }
    String* ToString();
    bool operator==(Rectangle o) const { return Equals(o); }
    bool operator!=(Rectangle o) const { return !Equals(o); }
    static ::System::Type* __type();
};

struct Color {
    uint32_t packed = 0;  // ABGR like MonoGame (R in the low byte)
    Color() = default;
    constexpr Color(uint8_t r, uint8_t g, uint8_t b, uint8_t a) : packed((uint32_t)r | ((uint32_t)g << 8) | ((uint32_t)b << 16) | ((uint32_t)a << 24)) {}
    Color(int32_t r, int32_t g, int32_t b) : Color(clampB(r), clampB(g), clampB(b), (uint8_t)255) {}
    Color(int32_t r, int32_t g, int32_t b, int32_t a) : Color(clampB(r), clampB(g), clampB(b), clampB(a)) {}
    Color(float r, float g, float b) : Color(clampF(r), clampF(g), clampF(b), (uint8_t)255) {}
    Color(float r, float g, float b, float a) : Color(clampF(r), clampF(g), clampF(b), clampF(a)) {}
    Color(Color c, float alpha) : Color(c.get_R(), c.get_G(), c.get_B(), clampF(alpha)) {}
    Color(Color c, int32_t alpha) : Color(c.get_R(), c.get_G(), c.get_B(), clampB(alpha)) {}
    explicit Color(Vector4 v) : Color(v.X, v.Y, v.Z, v.W) {}
    explicit Color(Vector3 v) : Color(v.X, v.Y, v.Z) {}
    static uint8_t clampB(int32_t v) { return (uint8_t)(v < 0 ? 0 : (v > 255 ? 255 : v)); }
    static uint8_t clampF(float v) { float f = v * 255.0f; return (uint8_t)(f < 0 ? 0 : (f > 255 ? 255 : (int32_t)(f + 0.5f))); }
    uint8_t get_R() const { return packed & 0xFF; }
    uint8_t get_G() const { return (packed >> 8) & 0xFF; }
    uint8_t get_B() const { return (packed >> 16) & 0xFF; }
    uint8_t get_A() const { return (packed >> 24) & 0xFF; }
    void set_R(uint8_t v) { packed = (packed & 0xFFFFFF00u) | v; }
    void set_G(uint8_t v) { packed = (packed & 0xFFFF00FFu) | ((uint32_t)v << 8); }
    void set_B(uint8_t v) { packed = (packed & 0xFF00FFFFu) | ((uint32_t)v << 16); }
    void set_A(uint8_t v) { packed = (packed & 0x00FFFFFFu) | ((uint32_t)v << 24); }
    uint32_t get_PackedValue() const { return packed; }
    void set_PackedValue(uint32_t v) { packed = v; }
    Vector4 ToVector4() const { return Vector4(get_R() / 255.0f, get_G() / 255.0f, get_B() / 255.0f, get_A() / 255.0f); }
    Vector3 ToVector3() const { return Vector3(get_R() / 255.0f, get_G() / 255.0f, get_B() / 255.0f); }
    static Color Lerp(Color a, Color b, float t);
    static Color FromNonPremultiplied(int32_t r, int32_t g, int32_t b, int32_t a) { return Color(r * a / 255, g * a / 255, b * a / 255, a); }
    Color operator*(float s) const;
    Color& operator*=(float s) { return *this = *this * s; }
    bool operator==(Color o) const { return packed == o.packed; }
    bool operator!=(Color o) const { return packed != o.packed; }
    bool Equals(const Color& o) const { return packed == o.packed; }
    int32_t GetHashCode() const { return (int32_t)packed; }
    String* ToString();
    static ::System::Type* __type();
#define FW_COLOR(N, R, G, B, A) static Color get_##N() { return Color((uint8_t)R, (uint8_t)G, (uint8_t)B, (uint8_t)A); }
    FW_COLOR(Transparent, 0, 0, 0, 0)
    FW_COLOR(AliceBlue, 240, 248, 255, 255)
    FW_COLOR(Aqua, 0, 255, 255, 255)
    FW_COLOR(Black, 0, 0, 0, 255)
    FW_COLOR(Blue, 0, 0, 255, 255)
    FW_COLOR(CadetBlue, 95, 158, 160, 255)
    FW_COLOR(CornflowerBlue, 100, 149, 237, 255)
    FW_COLOR(DarkBlue, 0, 0, 139, 255)
    FW_COLOR(DarkGray, 169, 169, 169, 255)
    FW_COLOR(DarkRed, 139, 0, 0, 255)
    FW_COLOR(DeepPink, 255, 20, 147, 255)
    FW_COLOR(DodgerBlue, 30, 144, 255, 255)
    FW_COLOR(Gray, 128, 128, 128, 255)
    FW_COLOR(Green, 0, 128, 0, 255)
    FW_COLOR(GreenYellow, 173, 255, 47, 255)
    FW_COLOR(HotPink, 255, 105, 180, 255)
    FW_COLOR(LightBlue, 173, 216, 230, 255)
    FW_COLOR(LightGray, 211, 211, 211, 255)
    FW_COLOR(Lime, 0, 255, 0, 255)
    FW_COLOR(Magenta, 255, 0, 255, 255)
    FW_COLOR(Orange, 255, 165, 0, 255)
    FW_COLOR(OrangeRed, 255, 69, 0, 255)
    FW_COLOR(Pink, 255, 192, 203, 255)
    FW_COLOR(Purple, 128, 0, 128, 255)
    FW_COLOR(Red, 255, 0, 0, 255)
    FW_COLOR(Sienna, 160, 82, 45, 255)
    FW_COLOR(White, 255, 255, 255, 255)
    FW_COLOR(Yellow, 255, 255, 0, 255)
    FW_COLOR(YellowGreen, 154, 205, 50, 255)
#undef FW_COLOR
};
inline Color operator*(float s, Color c) { return c * s; }

struct Matrix {
    float M11 = 0, M12 = 0, M13 = 0, M14 = 0;
    float M21 = 0, M22 = 0, M23 = 0, M24 = 0;
    float M31 = 0, M32 = 0, M33 = 0, M34 = 0;
    float M41 = 0, M42 = 0, M43 = 0, M44 = 0;
    static Matrix get_Identity() {
        Matrix m;
        m.M11 = m.M22 = m.M33 = m.M44 = 1;
        return m;
    }
    static Matrix CreateScale(float s) { return CreateScale(s, s, s); }
    static Matrix CreateScale(float x, float y, float z) {
        Matrix m = get_Identity();
        m.M11 = x;
        m.M22 = y;
        m.M33 = z;
        return m;
    }
    static Matrix CreateScale(Vector3 v) { return CreateScale(v.X, v.Y, v.Z); }
    static Matrix CreateTranslation(float x, float y, float z) {
        Matrix m = get_Identity();
        m.M41 = x;
        m.M42 = y;
        m.M43 = z;
        return m;
    }
    static Matrix CreateTranslation(Vector3 v) { return CreateTranslation(v.X, v.Y, v.Z); }
    static Matrix CreateRotationZ(float r);
    static Matrix CreateOrthographicOffCenter(float l, float r, float b, float t, float n, float f);
    static Matrix Invert(Matrix m);
    Matrix operator*(const Matrix& o) const;
    bool operator==(const Matrix& o) const { return std::memcmp(this, &o, sizeof(Matrix)) == 0; }
    bool operator!=(const Matrix& o) const { return !(*this == o); }
    bool Equals(const Matrix& o) const { return *this == o; }
    int32_t GetHashCode() const { return ::System::Eq<float>::hash(M11) ^ ::System::Eq<float>::hash(M41); }
    static ::System::Type* __type();
};

struct MathHelper {
    static constexpr float Pi = 3.14159265f;
    static constexpr float TwoPi = 6.28318531f;
    static constexpr float PiOver2 = 1.57079633f;
    static constexpr float PiOver4 = 0.785398163f;
    static float Clamp(float v, float lo, float hi) { v = v > hi ? hi : v; return v < lo ? lo : v; }
    static int32_t Clamp(int32_t v, int32_t lo, int32_t hi) { v = v > hi ? hi : v; return v < lo ? lo : v; }
    static float Lerp(float a, float b, float t) { return a + (b - a) * t; }
    static float Max(float a, float b) { return a > b ? a : b; }
    static float Min(float a, float b) { return a < b ? a : b; }
    static int32_t Max(int32_t a, int32_t b) { return a > b ? a : b; }
    static int32_t Min(int32_t a, int32_t b) { return a < b ? a : b; }
    static float ToDegrees(float r) { return r * 57.2957795f; }
    static float ToRadians(float d) { return d * 0.0174532925f; }
    static float Distance(float a, float b) { return std::fabs(a - b); }
    static float WrapAngle(float a);
};

}  // namespace Microsoft::Xna::Framework

#include "graphics.h"
#include "audio.h"
#include "input.h"
#include "game.h"
#include "extended.h"
