#pragma once
#include "linq.h"
#include <atomic>
#include <cstdio>

namespace System {
using namespace ::System::Collections::Generic;
enum class MidpointRounding : int32_t;
enum class Environment__SpecialFolder : int32_t;
namespace Globalization { enum class NumberStyles : int32_t; }
namespace Text::RegularExpressions { enum class RegexOptions : int32_t; }

// ---------------------------------------------------------------- Math
struct Math {
    static constexpr double PI = 3.14159265358979323846;
    static constexpr double E = 2.7182818284590452354;
    static double Atan2(double y, double x) { return std::atan2(y, x); }
    static double Atan(double x) { return std::atan(x); }
    static double Asin(double x) { return std::asin(x); }
    static double Acos(double x) { return std::acos(x); }
    static double Ceiling(double a) { return std::ceil(a); }
    static double Floor(double a) { return std::floor(a); }
    static double Truncate(double a) { return std::trunc(a); }
    static double Cos(double a) { return std::cos(a); }
    static double Sin(double a) { return std::sin(a); }
    static double Tan(double a) { return std::tan(a); }
    static double Sqrt(double a) { return std::sqrt(a); }
    static double Pow(double a, double b) { return std::pow(a, b); }
    static double Exp(double a) { return std::exp(a); }
    static double Log(double a) { return std::log(a); }
    static double Log10(double a) { return std::log10(a); }
    static double Round(double a) { return std::nearbyint(a); }  // banker's rounding (default FE_TONEAREST)
    static double Round(double a, MidpointRounding mode);
    static double RoundM(double a, int32_t mode) { return mode == 1 ? std::round(a) : std::nearbyint(a); }
    static double Round(double a, int32_t digits, int);
    static double Round(double a, int32_t digits) { return Round(a, digits, 0); }
    static int32_t Abs(int32_t v) { return v < 0 ? -v : v; }
    static int64_t Abs(int64_t v) { return v < 0 ? -v : v; }
    static float Abs(float v) { return std::fabs(v); }
    static double Abs(double v) { return std::fabs(v); }
    template <class T>
    static T Max(T a, T b) {
        if constexpr (std::is_floating_point_v<T>) { if (std::isnan(a)) return a; }
        return a > b ? a : b;
    }
    template <class T>
    static T Min(T a, T b) {
        if constexpr (std::is_floating_point_v<T>) { if (std::isnan(a)) return a; }
        return a < b ? a : b;
    }
    template <class T>
    static T Clamp(T v, T lo, T hi) {
        if (lo > hi) ThrowArgument("min > max");
        return v < lo ? lo : (v > hi ? hi : v);
    }
    static int32_t Sign(int32_t v) { return (v > 0) - (v < 0); }
    static int32_t Sign(int64_t v) { return (v > 0) - (v < 0); }
    static int32_t Sign(float v) { if (std::isnan(v)) ThrowArithmetic(); return (v > 0) - (v < 0); }
    static int32_t Sign(double v) { if (std::isnan(v)) ThrowArithmetic(); return (v > 0) - (v < 0); }
    [[noreturn]] static void ThrowArithmetic();
};
struct MathF {
    static constexpr float PI = 3.14159265f;
    static constexpr float Tau = 6.28318531f;
    static constexpr float E = 2.71828183f;
    static float Abs(float x) { return std::fabs(x); }
    static float Asin(float x) { return std::asin(x); }
    static float Acos(float x) { return std::acos(x); }
    static float Atan(float x) { return std::atan(x); }
    static float Atan2(float y, float x) { return std::atan2(y, x); }
    static float Ceiling(float x) { return std::ceil(x); }
    static float Floor(float x) { return std::floor(x); }
    static float Cos(float x) { return std::cos(x); }
    static float Sin(float x) { return std::sin(x); }
    static float Tan(float x) { return std::tan(x); }
    static float Sqrt(float x) { return std::sqrt(x); }
    static float Pow(float x, float y) { return std::pow(x, y); }
    static float Exp(float x) { return std::exp(x); }
    static float Log(float x) { return std::log(x); }
    static float Round(float x) { return std::nearbyint(x); }
    static float Round(float x, MidpointRounding mode);
    static float RoundM(float x, int32_t mode) { return mode == 1 ? std::round(x) : std::nearbyint(x); }
    static float Truncate(float x) { return std::trunc(x); }
    static float IEEERemainder(float x, float y) { return std::remainder(x, y); }
    static float Max(float a, float b) { if (std::isnan(a)) return a; return a > b ? a : b; }
    static float Min(float a, float b) { if (std::isnan(a)) return a; return a < b ? a : b; }
    static int32_t Sign(float v) { if (std::isnan(v)) Math::ThrowArithmetic(); return (v > 0) - (v < 0); }
};

// ---------------------------------------------------------------- Primitive statics
struct IFormatProvider;
struct Boolean {
    static bool Parse(String* s);
    static bool TryParse(String* s, bool& r);
    static String* ToString(bool v) { return ToStr(v); }
};
struct Char {
    static constexpr char16_t MaxValue = 0xFFFF;
    static bool IsControl(char16_t c) { return c < 0x20 || (c >= 0x7F && c < 0xA0); }
    static bool IsDigit(char16_t c) { return c >= '0' && c <= '9'; }
    static bool IsLetter(char16_t c);
    static bool IsLetterOrDigit(char16_t c) { return IsLetter(c) || IsDigit(c); }
    static bool IsWhiteSpace(char16_t c);
    static bool IsUpper(char16_t c);
    static bool IsLower(char16_t c);
    static bool IsPunctuation(char16_t c);
    static bool IsSurrogate(char16_t c) { return c >= 0xD800 && c <= 0xDFFF; }
    static bool IsHighSurrogate(char16_t c) { return c >= 0xD800 && c <= 0xDBFF; }
    static char16_t ToUpper(char16_t c);
    static char16_t ToLower(char16_t c);
    static char16_t ToUpperInvariant(char16_t c) { return ToUpper(c); }
    static char16_t ToLowerInvariant(char16_t c) { return ToLower(c); }
    static String* ToString(char16_t c) { return ToStr(c); }
};
struct Byte {
    static constexpr uint8_t MaxValue = 255;
    static constexpr uint8_t MinValue = 0;
    static uint8_t Parse(String* s);
    static bool TryParse(String* s, uint8_t& r);
};
struct Int16 {
    static constexpr int16_t MaxValue = 32767;
    static constexpr int16_t MinValue = -32768;
    static bool TryParse(String* s, int16_t& r);
    static int16_t Parse(String* s);
};
struct Int32 {
    static constexpr int32_t MaxValue = 2147483647;
    static constexpr int32_t MinValue = -2147483647 - 1;
    static int32_t Parse(String* s);
    static int32_t Parse(String* s, IFormatProvider* p) { return Parse(s); }
    static int32_t Parse(String* s, Globalization::NumberStyles style, IFormatProvider* p) { return Parse(s); }
    static bool TryParse(String* s, int32_t& r);
    static bool TryParse(String* s, Globalization::NumberStyles style, IFormatProvider* p, int32_t& r) { return TryParse(s, r); }
    static int32_t CompareTo(int32_t a, int32_t b) { return a < b ? -1 : (a > b ? 1 : 0); }
    static String* ToString(int32_t v) { return ToStr(v); }
    static String* ToString(int32_t v, String* fmt) { return Fmt(v, fmt); }
    static String* ToString(int32_t v, IFormatProvider*) { return ToStr(v); }
};
struct UInt32 {
    static constexpr uint32_t MaxValue = 0xFFFFFFFFu;
    static constexpr uint32_t MinValue = 0;
};
struct Int64 {
    static constexpr int64_t MaxValue = INT64_MAX;
    static constexpr int64_t MinValue = INT64_MIN;
    static bool TryParse(String* s, int64_t& r);
    static int64_t Parse(String* s);
    static int32_t CompareTo(int64_t a, int64_t b) { return a < b ? -1 : (a > b ? 1 : 0); }
};
struct Single {
    static constexpr float MaxValue = 3.40282347e+38f;
    static constexpr float MinValue = -3.40282347e+38f;
    static constexpr float Epsilon = 1.401298E-45f;
    static constexpr float PositiveInfinity = std::numeric_limits<float>::infinity();
    static constexpr float NegativeInfinity = -std::numeric_limits<float>::infinity();
    static constexpr float NaN = std::numeric_limits<float>::quiet_NaN();
    static bool IsNaN(float f) { return std::isnan(f); }
    static bool IsInfinity(float f) { return std::isinf(f); }
    static bool IsFinite(float f) { return std::isfinite(f); }
    static float Parse(String* s);
    static float Parse(String* s, IFormatProvider* p) { return Parse(s); }
    static float Parse(String* s, Globalization::NumberStyles style, IFormatProvider* p) { return Parse(s); }
    static bool TryParse(String* s, float& r);
    static bool TryParse(String* s, Globalization::NumberStyles style, IFormatProvider* p, float& r) { return TryParse(s, r); }
    static int32_t CompareTo(float a, float b) {
        if (a < b) return -1;
        if (a > b) return 1;
        if (a == b) return 0;
        return std::isnan(a) ? (std::isnan(b) ? 0 : -1) : 1;
    }
    static String* ToString(float v) { return ToStr(v); }
    static String* ToString(float v, IFormatProvider*) { return ToStr(v); }
    static String* ToString(float v, String* fmt) { return Fmt(v, fmt); }
    static String* ToString(float v, String* fmt, IFormatProvider*) { return Fmt(v, fmt); }
};
struct Double {
    static constexpr double MaxValue = 1.7976931348623157E+308;
    static constexpr double MinValue = -1.7976931348623157E+308;
    static constexpr double PositiveInfinity = std::numeric_limits<double>::infinity();
    static constexpr double NaN = std::numeric_limits<double>::quiet_NaN();
    static bool IsNaN(double f) { return std::isnan(f); }
    static bool IsInfinity(double f) { return std::isinf(f); }
    static double Parse(String* s);
    static double Parse(String* s, IFormatProvider* p) { return Parse(s); }
    static bool TryParse(String* s, double& r);
    static bool TryParse(String* s, Globalization::NumberStyles style, IFormatProvider* p, double& r) { return TryParse(s, r); }
    static int32_t CompareTo(double a, double b) { return a < b ? -1 : (a > b ? 1 : 0); }
    static String* ToString(double v) { return ToStr(v); }
    static String* ToString(double v, IFormatProvider*) { return ToStr(v); }
    static String* ToString(double v, String* fmt) { return Fmt(v, fmt); }
};

// ---------------------------------------------------------------- Enum helpers
struct Enum {
    template <class E>
    static Array1<E>* GetValues() { return __EnumValues((E*)nullptr); }
    template <class E>
    static bool TryParse(String* s, E& r) {
        auto* vals = GetValues<E>();
        for (int32_t i = 0; i < vals->length; i++)
            if (String::op_Equality(ToStr(vals->data[i]), s)) { r = vals->data[i]; return true; }
        r = E{};
        return false;
    }
    template <class E>
    static E Parse(String* s) {
        E r;
        if (!TryParse<E>(s, r)) ThrowArgument("Enum.Parse");
        return r;
    }
    template <class E>
    static bool IsDefined(E v) {
        auto* vals = GetValues<E>();
        for (int32_t i = 0; i < vals->length; i++)
            if (vals->data[i] == v) return true;
        return false;
    }
};
String* EnumToStringImpl(int64_t v, const int64_t* values, const char16_t* const* names, int32_t n, bool flags);

// ---------------------------------------------------------------- Random (.NET legacy subtractive generator)
struct Random : Object {
    int32_t inext = 0, inextp = 0;
    int32_t seedArray[56] = {};
    void __init();
    void __init(int32_t seed);
    int32_t internalSample();
    double Sample() { return internalSample() * (1.0 / 2147483647); }
    virtual int32_t Next() { return internalSample(); }
    virtual int32_t Next(int32_t maxValue) {
        if (maxValue < 0) ThrowArgument("maxValue");
        return (int32_t)(Sample() * maxValue);
    }
    virtual int32_t Next(int32_t minValue, int32_t maxValue);
    virtual double NextDouble() { return Sample(); }
    float NextSingle() { return (float)Sample(); }
};

// ---------------------------------------------------------------- TimeSpan / DateTime
struct TimeSpan {
    int64_t ticks = 0;
    TimeSpan() = default;
    explicit TimeSpan(int64_t t) : ticks(t) {}
    TimeSpan(int32_t h, int32_t m, int32_t s) : ticks(((int64_t)h * 3600 + (int64_t)m * 60 + s) * 10000000LL) {}
    static TimeSpan FromSeconds(double s) { return TimeSpan((int64_t)(s * 10000000.0)); }
    static TimeSpan FromMilliseconds(double ms) { return TimeSpan((int64_t)(ms * 10000.0)); }
    static TimeSpan FromTicks(int64_t t) { return TimeSpan(t); }
    static TimeSpan get_Zero() { return TimeSpan(); }
    int64_t get_Ticks() const { return ticks; }
    double get_TotalSeconds() const { return ticks / 10000000.0; }
    double get_TotalMilliseconds() const { return ticks / 10000.0; }
    double get_TotalMinutes() const { return ticks / 600000000.0; }
    double get_TotalHours() const { return ticks / 36000000000.0; }
    int32_t get_Milliseconds() const { return (int32_t)((ticks / 10000) % 1000); }
    int32_t get_Seconds() const { return (int32_t)((ticks / 10000000) % 60); }
    int32_t get_Minutes() const { return (int32_t)((ticks / 600000000) % 60); }
    int32_t get_Hours() const { return (int32_t)((ticks / 36000000000LL) % 24); }
    int32_t get_Days() const { return (int32_t)(ticks / 864000000000LL); }
    TimeSpan operator+(TimeSpan o) const { return TimeSpan(ticks + o.ticks); }
    TimeSpan operator-(TimeSpan o) const { return TimeSpan(ticks - o.ticks); }
    bool operator==(TimeSpan o) const { return ticks == o.ticks; }
    bool operator!=(TimeSpan o) const { return ticks != o.ticks; }
    bool operator<(TimeSpan o) const { return ticks < o.ticks; }
    bool operator>(TimeSpan o) const { return ticks > o.ticks; }
    bool operator<=(TimeSpan o) const { return ticks <= o.ticks; }
    bool operator>=(TimeSpan o) const { return ticks >= o.ticks; }
    bool Equals(TimeSpan o) const { return ticks == o.ticks; }
    int32_t GetHashCode() const { return (int32_t)(ticks ^ (ticks >> 32)); }
    String* ToString();
};
struct DateTime {
    int64_t ticks = 0;  // since 0001-01-01, local time
    static DateTime get_Now();
    static DateTime get_UtcNow() { return get_Now(); }
    int32_t get_Year();
    int32_t get_Month();
    int32_t get_Day();
    int32_t get_Hour() { return (int32_t)((ticks / 36000000000LL) % 24); }
    int32_t get_Minute() { return (int32_t)((ticks / 600000000LL) % 60); }
    int32_t get_Second() { return (int32_t)((ticks / 10000000LL) % 60); }
    int64_t get_Ticks() { return ticks; }
    TimeSpan operator-(DateTime o) const { return TimeSpan(ticks - o.ticks); }
    bool operator<(DateTime o) const { return ticks < o.ticks; }
    bool operator>(DateTime o) const { return ticks > o.ticks; }
    bool operator<=(DateTime o) const { return ticks <= o.ticks; }
    bool operator>=(DateTime o) const { return ticks >= o.ticks; }
    bool operator==(DateTime o) const { return ticks == o.ticks; }
    bool operator!=(DateTime o) const { return ticks != o.ticks; }
    String* ToString();
    String* ToString(String* fmt);
    bool Equals(DateTime o) const { return ticks == o.ticks; }
    int32_t GetHashCode() const { return (int32_t)(ticks ^ (ticks >> 32)); }
};

// ---------------------------------------------------------------- HashCode
struct HashCode {
    uint32_t h = 17;
    template <class T>
    void Add(T v) { h = h * 31 + (uint32_t)Eq<T>::hash(v); }
    int32_t ToHashCode() { return (int32_t)h; }
    template <class A, class B>
    static int32_t Combine(A a, B b) { HashCode c; c.Add(a); c.Add(b); return c.ToHashCode(); }
    template <class A, class B, class C>
    static int32_t Combine(A a, B b, C cc) { HashCode c; c.Add(a); c.Add(b); c.Add(cc); return c.ToHashCode(); }
    template <class A, class B, class C, class D>
    static int32_t Combine(A a, B b, C cc, D d) { HashCode c; c.Add(a); c.Add(b); c.Add(cc); c.Add(d); return c.ToHashCode(); }
};

// ---------------------------------------------------------------- Convert
struct Convert {
    static bool ToBoolean(String* s) { return Boolean::Parse(s); }
    static int32_t ToInt32(bool v) { return v ? 1 : 0; }
    static int32_t ToInt32(String* s) { return s ? Int32::Parse(s) : 0; }
    static int32_t ToInt32(float v) { return (int32_t)std::nearbyint(v); }
    static int32_t ToInt32(double v) { return (int32_t)std::nearbyint(v); }
    static float ToSingle(String* s) { return Single::Parse(s); }
    static double ToDouble(String* s) { return Double::Parse(s); }
    static Object* ChangeType(Object* v, Type* t, IFormatProvider* p);
    static String* ToBase64String(Array1<uint8_t>* a);
    static Array1<uint8_t>* FromBase64String(String* s);
};

// ---------------------------------------------------------------- Environment / AppContext / Debug
struct Environment {
    static String* GetFolderPath(Environment__SpecialFolder folder);
    static String* GetEnvironmentVariable(String* name);
    static String* get_NewLine() { return STR(u"\n"); }
    static int32_t get_TickCount();
    static int64_t get_TickCount64();
    static int32_t get_ProcessorCount() { return 4; }
    static void Exit(int32_t code);
};
enum class Environment__SpecialFolder : int32_t {
    ApplicationData = 26,
    LocalApplicationData = 28,
    Personal = 5,
    MyDocuments = 5,
    UserProfile = 40,
};
inline String* __EnumToString(Environment__SpecialFolder v) { return ToStr((int32_t)v); }
struct AppContext {
    static String* get_BaseDirectory();
};
namespace Diagnostics {
struct Debug {
    static void WriteLine(String* s);
    static void WriteLine(String* s, String* cat) { WriteLine(s); }
    static void WriteLine(Object* o) { WriteLine(o ? o->ToString() : nullptr); }
    static void Write(String* s) { WriteLine(s); }
    static void Assert(bool c) {}
    static void Assert(bool c, String* m) {}
};
struct Stopwatch : Object {
    int64_t startTicks = 0, elapsed = 0;
    bool running = false;
    static int64_t nowTicks();
    static Stopwatch* StartNew() { auto* s = New<Stopwatch>(); s->Start(); return s; }
    void Start() { if (!running) { startTicks = nowTicks(); running = true; } }
    void Stop() { if (running) { elapsed += nowTicks() - startTicks; running = false; } }
    void Reset() { elapsed = 0; running = false; }
    void Restart() { elapsed = 0; startTicks = nowTicks(); running = true; }
    int64_t ticks() { return elapsed + (running ? nowTicks() - startTicks : 0); }
    TimeSpan get_Elapsed() { return TimeSpan(ticks()); }
    int64_t get_ElapsedMilliseconds() { return ticks() / 10000; }
    int64_t get_ElapsedTicks() { return ticks(); }
    bool get_IsRunning() { return running; }
    static constexpr int64_t Frequency = 10000000;
};
}  // namespace Diagnostics
struct Console {
    static void WriteLine(String* s) { Diagnostics::Debug::WriteLine(s); }
    static void WriteLine() { Diagnostics::Debug::WriteLine(String::Empty); }
    static void WriteLine(Object* o) { Diagnostics::Debug::WriteLine(o); }
    static void Write(String* s) { Diagnostics::Debug::WriteLine(s); }
};

// ---------------------------------------------------------------- Text
namespace Text {
struct StringBuilder : Object {
    List<char16_t>* buf = nullptr;
    void __init() { buf = New<List<char16_t>>(); }
    void __init(int32_t cap) { buf = New<List<char16_t>>(cap); }
    void __init(String* s) { __init(); Append(s); }
    StringBuilder* Append(char16_t c) { buf->Add(c); return this; }
    StringBuilder* Append(char16_t c, int32_t n) { for (int32_t i = 0; i < n; i++) buf->Add(c); return this; }
    StringBuilder* Append(String* s) {
        if (s)
            for (int32_t i = 0; i < s->length; i++) buf->Add(s->chars[i]);
        return this;
    }
    StringBuilder* Append(int32_t v) { return Append(ToStr(v)); }
    StringBuilder* Append(float v) { return Append(ToStr(v)); }
    StringBuilder* Append(double v) { return Append(ToStr(v)); }
    StringBuilder* Append(bool v) { return Append(ToStr(v)); }
    StringBuilder* Append(Object* o) { return Append(o ? o->ToString() : nullptr); }
    StringBuilder* AppendLine() { return Append(u'\n'); }
    StringBuilder* AppendLine(String* s) { Append(s); return Append(u'\n'); }
    StringBuilder* Insert(int32_t idx, String* s) {
        for (int32_t i = 0; s && i < s->length; i++) buf->Insert(idx + i, s->chars[i]);
        return this;
    }
    StringBuilder* Remove(int32_t idx, int32_t n) { buf->RemoveRange(idx, n); return this; }
    StringBuilder* Clear() { buf->Clear(); return this; }
    int32_t get_Length() { return buf->size; }
    void set_Length(int32_t n) {
        while (buf->size > n) buf->RemoveAt(buf->size - 1);
        while (buf->size < n) buf->Add(0);
    }
    char16_t get_Chars(int32_t i) { return buf->get_Item(i); }
    void set_Chars(int32_t i, char16_t c) { buf->set_Item(i, c); }
    String* ToString() override { return String::FromUtf16(buf->begin(), buf->size); }
};
struct Encoding : Object {
    static Encoding* get_UTF8();
    static Encoding* get_ASCII() { return get_UTF8(); }
    static Encoding* get_Default() { return get_UTF8(); }
    Array1<uint8_t>* GetBytes(String* s);
    String* GetString(Array1<uint8_t>* b);
    String* GetString(Array1<uint8_t>* b, int32_t idx, int32_t n);
};
namespace RegularExpressions {
struct Regex : Object {
    void* impl = nullptr;
    bool icase = false;
    void __init(String* pattern);
    void __init(String* pattern, RegexOptions options);
    bool IsMatch(String* input);
    static bool IsMatch(String* input, String* pattern);
};
enum class RegexOptions : int32_t { None = 0, IgnoreCase = 1, Multiline = 2, Compiled = 8, CultureInvariant = 512 };
inline String* __EnumToString(RegexOptions v) { return ToStr((int32_t)v); }
inline RegexOptions operator|(RegexOptions a, RegexOptions b) { return (RegexOptions)((int32_t)a | (int32_t)b); }
}  // namespace RegularExpressions
}  // namespace Text

// ---------------------------------------------------------------- Globalization
namespace Globalization {
struct CultureInfo : Object, IFormatProvider {
    static CultureInfo* get_InvariantCulture();
    static CultureInfo* get_CurrentCulture() { return get_InvariantCulture(); }
};
enum class NumberStyles : int32_t { None = 0, Integer = 7, Float = 167, Any = 511, AllowDecimalPoint = 32, AllowLeadingSign = 4 };
inline String* __EnumToString(NumberStyles v) { return ToStr((int32_t)v); }
inline NumberStyles operator|(NumberStyles a, NumberStyles b) { return (NumberStyles)((int32_t)a | (int32_t)b); }
struct TextElementEnumerator : Object {
    String* s = nullptr;
    int32_t index = -1, next = 0;
    bool MoveNext();
    int32_t get_ElementIndex() { return index; }
    String* GetTextElement();
    Object* get_Current() { return GetTextElement(); }
};
struct StringInfo : Object {
    static TextElementEnumerator* GetTextElementEnumerator(String* s) {
        auto* e = new TextElementEnumerator();
        e->s = s;
        return e;
    }
};
}  // namespace Globalization

enum class StringComparison : int32_t { CurrentCulture = 0, CurrentCultureIgnoreCase = 1, InvariantCulture = 2, InvariantCultureIgnoreCase = 3, Ordinal = 4, OrdinalIgnoreCase = 5 };
inline String* __EnumToString(StringComparison v) { return ToStr((int32_t)v); }
enum class StringSplitOptions : int32_t { None = 0, RemoveEmptyEntries = 1, TrimEntries = 2 };
inline String* __EnumToString(StringSplitOptions v) { return ToStr((int32_t)v); }
inline StringSplitOptions operator|(StringSplitOptions a, StringSplitOptions b) { return (StringSplitOptions)((int32_t)a | (int32_t)b); }
enum class MidpointRounding : int32_t { ToEven = 0, AwayFromZero = 1 };
inline String* __EnumToString(MidpointRounding v) { return ToStr((int32_t)v); }

struct StringComparer : Object, IEqualityComparer<String*>, IComparer<String*> {
    bool ignoreCase = false;
    static StringComparer* get_Ordinal();
    static StringComparer* get_OrdinalIgnoreCase();
    static StringComparer* get_InvariantCultureIgnoreCase() { return get_OrdinalIgnoreCase(); }
    bool Equals(String* a, String* b) override;
    int32_t GetHashCode(String* s) override;
    int32_t Compare(String* a, String* b) override;
    using Object::Equals;
    using Object::GetHashCode;
};

// ---------------------------------------------------------------- Threading
namespace Threading {
struct Monitor {
    static void Enter(Object* o);
    static void Exit(Object* o);
};
struct LockGuard {
    Object* o;
    explicit LockGuard(Object* obj) : o(obj) { Monitor::Enter(o); }
    ~LockGuard() { Monitor::Exit(o); }
};
struct Interlocked {
    static int32_t Increment(int32_t& v) { return __atomic_add_fetch(&v, 1, __ATOMIC_SEQ_CST); }
    static int32_t Decrement(int32_t& v) { return __atomic_sub_fetch(&v, 1, __ATOMIC_SEQ_CST); }
    static int32_t Exchange(int32_t& v, int32_t n) { return __atomic_exchange_n(&v, n, __ATOMIC_SEQ_CST); }
    static int32_t Add(int32_t& v, int32_t n) { return __atomic_add_fetch(&v, n, __ATOMIC_SEQ_CST); }
    static int32_t CompareExchange(int32_t& v, int32_t n, int32_t c) {
        __atomic_compare_exchange_n(&v, &c, n, false, __ATOMIC_SEQ_CST, __ATOMIC_SEQ_CST);
        return c;
    }
    template <class T>
    static T* Exchange(T*& v, T* n) { return __atomic_exchange_n(&v, n, __ATOMIC_SEQ_CST); }
};
struct Volatile {
    static int32_t Read(const int32_t& v) { return __atomic_load_n(&v, __ATOMIC_SEQ_CST); }
    static void Write(int32_t& v, int32_t n) { __atomic_store_n(&v, n, __ATOMIC_SEQ_CST); }
    static bool Read(const bool& v) { return __atomic_load_n(&v, __ATOMIC_SEQ_CST); }
    static void Write(bool& v, bool n) { __atomic_store_n(&v, n, __ATOMIC_SEQ_CST); }
};
using ThreadStart = Fn<void()>;
using WaitCallback = Fn<void(Object*)>;
struct Thread : Object {
    Fn<void()>* start = nullptr;
    Fn<void(Object*)>* startParam = nullptr;
    void* handle = nullptr;
    std::atomic<bool> alive{false};
    bool isBackground = false;
    String* name = nullptr;
    int32_t priority = 2;
    void __init(Fn<void()>* s) { start = s; }
    void __init(Fn<void(Object*)>* s) { startParam = s; }
    void Start();
    void Start(Object* param);
    void Join();
    bool Join(int32_t ms);
    bool get_IsAlive() { return alive.load(); }
    bool get_IsBackground() { return isBackground; }
    void set_IsBackground(bool b) { isBackground = b; }
    String* get_Name() { return name; }
    void set_Name(String* n) { name = n; }
    int32_t get_Priority() { return priority; }
    void set_Priority(int32_t p) { priority = p; }
    static void Sleep(int32_t ms);
    static Thread* get_CurrentThread();
    int32_t get_ManagedThreadId();
};
enum class ThreadPriority : int32_t { Lowest = 0, BelowNormal = 1, Normal = 2, AboveNormal = 3, Highest = 4 };
inline String* __EnumToString(ThreadPriority v) { return ToStr((int32_t)v); }
struct ThreadPool {
    static bool QueueUserWorkItem(Fn<void(Object*)>* cb);
};
struct CancellationToken {
    struct CancellationTokenSource* src = nullptr;
    bool get_IsCancellationRequested();
    void ThrowIfCancellationRequested();
    static CancellationToken get_None() { return CancellationToken{}; }
};
struct CancellationTokenSource : Object {
    std::atomic<bool> cancelled{false};
    void Cancel() { cancelled = true; }
    bool get_IsCancellationRequested() { return cancelled.load(); }
    CancellationToken get_Token() { return CancellationToken{this}; }
    void Dispose() {}
};
namespace Tasks {
struct Task : Object {
    static Task* Run(Fn<void()>* a);
    void Wait() {}
    bool get_IsCompleted() { return true; }
};
}  // namespace Tasks
}  // namespace Threading

namespace Collections::Concurrent {
template <class T>
struct ConcurrentQueue : Object {
    Queue<T>* q = nullptr;
    void __init() { q = New<Queue<T>>(); }
    void Enqueue(T v) { Threading::LockGuard g(this); q->Enqueue(v); }
    bool TryDequeue(T& v) { Threading::LockGuard g(this); return q->TryDequeue(v); }
    int32_t get_Count() { Threading::LockGuard g(this); return q->get_Count(); }
    bool get_IsEmpty() { return get_Count() == 0; }
    void Clear() { Threading::LockGuard g(this); q->Clear(); }
};
}  // namespace Collections::Concurrent

namespace Collections::ObjectModel {
template <class T>
struct Collection : Object, virtual IReadOnlyList<T> {
    List<T>* l = nullptr;
    void __init() { l = New<List<T>>(); }
    void Add(T v) { l->Add(v); }
    int32_t get_Count() override { return l->size; }
    T get_Item(int32_t i) override { return l->get_Item(i); }
    IEnumerator<T>* GetEnumerator() override { return l->GetEnumerator(); }
    T* begin() { return l->begin(); }
    T* end() { return l->end(); }
};
template <class T>
struct ReadOnlyCollection : Object, virtual IReadOnlyList<T> {
    List<T>* l = nullptr;
    void __init(List<T>* src) { l = src; }
    bool Contains(T v) { return l->Contains(v); }
    int32_t get_Count() override { return l->size; }
    T get_Item(int32_t i) override { return l->get_Item(i); }
    IEnumerator<T>* GetEnumerator() override { return l->GetEnumerator(); }
    T* begin() { return l->begin(); }
    T* end() { return l->end(); }
};
}  // namespace Collections::ObjectModel
template <class T>
inline Collections::ObjectModel::ReadOnlyCollection<T>& Each(Collections::ObjectModel::ReadOnlyCollection<T>* c) { return *nn(c); }

template <class T>
struct Span {
    T* p = nullptr;
    int32_t n = 0;
    Span() = default;
    Span(Array1<T>* a) : p(a ? a->data : nullptr), n(a ? a->length : 0) {}
    Span(T* ptr, int32_t len) : p(ptr), n(len) {}
    int32_t get_Length() const { return n; }
    T& get_Item(int32_t i) { if ((uint32_t)i >= (uint32_t)n) ThrowIndexOutOfRange(); return p[i]; }
    Array1<T>* ToArray() const {
        auto* a = Array1<T>::New(n);
        for (int32_t i = 0; i < n; i++) a->data[i] = p[i];
        return a;
    }
    T* begin() { return p; }
    T* end() { return p + n; }
};
namespace Runtime::InteropServices {
struct MemoryMarshal {
    template <class T>
    static Span<uint8_t> AsBytes(Span<T> s) { return Span<uint8_t>((uint8_t*)s.p, s.n * (int32_t)sizeof(T)); }
};
}  // namespace Runtime::InteropServices

struct Buffer {
    static void BlockCopy(Array* src, int32_t srcOffset, Array* dst, int32_t dstOffset, int32_t count) {
        std::memmove((uint8_t*)dst->__data() + dstOffset, (uint8_t*)src->__data() + srcOffset, count);
    }
};

namespace Runtime::CompilerServices {
using SwitchExpressionException = ::System::SwitchExpressionException;
template <class K, class V>
struct ConditionalWeakTable : Object {
    Dictionary<K, V>* d = nullptr;
    void __init() { d = New<Dictionary<K, V>>(); }
    bool TryGetValue(K k, V& v) { return d->TryGetValue(k, v); }
    void AddOrUpdate(K k, V v) { d->set_Item(k, v); }
    void Add(K k, V v) { d->Add(k, v); }
    bool Remove(K k) { return d->Remove(k); }
};
}  // namespace Runtime::CompilerServices

}  // namespace System
