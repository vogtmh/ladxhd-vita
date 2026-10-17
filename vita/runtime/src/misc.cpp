// Runtime: Random, time, environment, threading, text, globalization, reflection helpers.
#include "rt/all.h"
#include <chrono>
#include <cstdio>
#include <cstdlib>
#include <ctime>
#include <mutex>
#include <regex>
#include <string>
#include <thread>

namespace System {

// ---------------------------------------------------------------- Random (Net5CompatSeedImpl)
void Random::__init() { __init((int32_t)(std::chrono::steady_clock::now().time_since_epoch().count() & 0x7FFFFFFF)); }
void Random::__init(int32_t seed) {
    int32_t subtraction = (seed == INT32_MIN) ? INT32_MAX : (seed < 0 ? -seed : seed);
    int32_t mj = 161803398 - subtraction;
    seedArray[55] = mj;
    int32_t mk = 1;
    int32_t ii = 0;
    for (int32_t i = 1; i < 55; i++) {
        if ((ii += 21) >= 55) ii -= 55;
        seedArray[ii] = mk;
        mk = mj - mk;
        if (mk < 0) mk += INT32_MAX;
        mj = seedArray[ii];
    }
    for (int32_t k = 1; k < 5; k++) {
        for (int32_t i = 1; i < 56; i++) {
            int32_t n = i + 30;
            if (n >= 55) n -= 55;
            seedArray[i] -= seedArray[1 + n];
            if (seedArray[i] < 0) seedArray[i] += INT32_MAX;
        }
    }
    inext = 0;
    inextp = 21;
}
int32_t Random::internalSample() {
    int32_t locINext = inext, locINextp = inextp;
    if (++locINext >= 56) locINext = 1;
    if (++locINextp >= 56) locINextp = 1;
    int32_t retVal = seedArray[locINext] - seedArray[locINextp];
    if (retVal == INT32_MAX) retVal--;
    if (retVal < 0) retVal += INT32_MAX;
    seedArray[locINext] = retVal;
    inext = locINext;
    inextp = locINextp;
    return retVal;
}
int32_t Random::Next(int32_t minValue, int32_t maxValue) {
    if (minValue > maxValue) ThrowArgument("minValue > maxValue");
    int64_t range = (int64_t)maxValue - minValue;
    if (range <= INT32_MAX) return (int32_t)(Sample() * range) + minValue;
    return (int32_t)((int64_t)(Sample() * (double)range) + minValue);
}

// ---------------------------------------------------------------- time
static int64_t nowTicksLocal() {
    using namespace std::chrono;
    auto now = system_clock::now();
    int64_t us = duration_cast<microseconds>(now.time_since_epoch()).count();
    std::time_t tt = system_clock::to_time_t(now);
    std::tm lt{};
#if defined(_WIN32)
    localtime_s(&lt, &tt);
#else
    localtime_r(&tt, &lt);
#endif
    int64_t offset = 0;
#if defined(__APPLE__) || defined(__linux__)
    offset = lt.tm_gmtoff;
#endif
    return 621355968000000000LL + us * 10 + offset * 10000000LL;
}
DateTime DateTime::get_Now() { DateTime d; d.ticks = nowTicksLocal(); return d; }
static void civil(int64_t ticks, int32_t& y, int32_t& m, int32_t& d) {
    int64_t days = ticks / 864000000000LL - 719162;  // days since 1970-01-01
    days += 719468;
    int64_t era = (days >= 0 ? days : days - 146096) / 146097;
    int64_t doe = days - era * 146097;
    int64_t yoe = (doe - doe / 1460 + doe / 36524 - doe / 146096) / 365;
    int64_t yy = yoe + era * 400;
    int64_t doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
    int64_t mp = (5 * doy + 2) / 153;
    d = (int32_t)(doy - (153 * mp + 2) / 5 + 1);
    m = (int32_t)(mp < 10 ? mp + 3 : mp - 9);
    y = (int32_t)(yy + (m <= 2));
}
int32_t DateTime::get_Year() { int32_t y, m, d; civil(ticks, y, m, d); return y; }
int32_t DateTime::get_Month() { int32_t y, m, d; civil(ticks, y, m, d); return m; }
int32_t DateTime::get_Day() { int32_t y, m, d; civil(ticks, y, m, d); return d; }
String* DateTime::ToString() {
    char b[64];
    std::snprintf(b, sizeof b, "%02d/%02d/%04d %02d:%02d:%02d", get_Month(), get_Day(), get_Year(), get_Hour(), get_Minute(), get_Second());
    return String::FromUtf8(b);
}
String* DateTime::ToString(String* fmt) {
    // yyyy MM dd HH mm ss tokens
    std::string f = fmt ? fmt->ToUtf8() : "";
    std::string out;
    for (size_t i = 0; i < f.size();) {
        auto take = [&](const char* tok) { size_t n = std::strlen(tok); if (f.compare(i, n, tok) == 0) { i += n; return true; } return false; };
        char b[16];
        if (take("yyyy")) { std::snprintf(b, sizeof b, "%04d", get_Year()); out += b; }
        else if (take("MM")) { std::snprintf(b, sizeof b, "%02d", get_Month()); out += b; }
        else if (take("dd")) { std::snprintf(b, sizeof b, "%02d", get_Day()); out += b; }
        else if (take("HH")) { std::snprintf(b, sizeof b, "%02d", get_Hour()); out += b; }
        else if (take("mm")) { std::snprintf(b, sizeof b, "%02d", get_Minute()); out += b; }
        else if (take("ss")) { std::snprintf(b, sizeof b, "%02d", get_Second()); out += b; }
        else out.push_back(f[i++]);
    }
    return String::FromUtf8(out.c_str());
}
String* TimeSpan::ToString() {
    char b[64];
    int64_t t = ticks < 0 ? -ticks : ticks;
    std::snprintf(b, sizeof b, "%s%02d:%02d:%02d", ticks < 0 ? "-" : "", (int)(t / 36000000000LL), (int)((t / 600000000LL) % 60), (int)((t / 10000000LL) % 60));
    return String::FromUtf8(b);
}
int64_t Diagnostics::Stopwatch::nowTicks() {
    return std::chrono::duration_cast<std::chrono::nanoseconds>(std::chrono::steady_clock::now().time_since_epoch()).count() / 100;
}
int32_t Environment::get_TickCount() { return (int32_t)(Diagnostics::Stopwatch::nowTicks() / 10000); }
int64_t Environment::get_TickCount64() { return Diagnostics::Stopwatch::nowTicks() / 10000; }

// ---------------------------------------------------------------- environment
String* (*IO::MapPath)(String*) = nullptr;
extern "C" const char* rt_platform_folder(int folder);
extern "C" const char* rt_base_directory();
extern "C" void rt_log(const char* s);
String* Environment::GetFolderPath(Environment__SpecialFolder f) { return String::FromUtf8(rt_platform_folder((int)f)); }
String* Environment::GetEnvironmentVariable(String* name) {
    const char* v = std::getenv(name->ToUtf8());
    return v ? String::FromUtf8(v) : nullptr;
}
void Environment::Exit(int32_t code) { std::exit(code); }
String* AppContext::get_BaseDirectory() { return String::FromUtf8(rt_base_directory()); }
void Diagnostics::Debug::WriteLine(String* s) { rt_log(s ? s->ToUtf8() : ""); }

// ---------------------------------------------------------------- misc
Object* Convert::ChangeType(Object* v, Type* t, IFormatProvider*) {
    if (!v || !t) return v;
    auto* s = dynamic_cast<String*>(v);
    if (t == TypeOf<String>()) return v->ToString();
    if (s) {
        if (t == TypeOf<int32_t>()) return box(Int32::Parse(s));
        if (t == TypeOf<float>()) return box(Single::Parse(s));
        if (t == TypeOf<double>()) return box(Double::Parse(s));
        if (t == TypeOf<bool>()) return box(Boolean::Parse(s));
        if (t == TypeOf<int64_t>()) return box(Int64::Parse(s));
        if (t == TypeOf<uint8_t>()) return box(Byte::Parse(s));
        if (t == TypeOf<int16_t>()) return box(Int16::Parse(s));
    }
    if (auto* b = dynamic_cast<BoxBase*>(v)) {
        double d;
        if (b->__tryGetF64(d)) {
            if (t == TypeOf<int32_t>()) return box((int32_t)d);
            if (t == TypeOf<float>()) return box((float)d);
            if (t == TypeOf<double>()) return box(d);
            if (t == TypeOf<bool>()) return box(d != 0);
        }
    }
    throw NewEx<InvalidCastException>(STR(u"Invalid cast in ChangeType"));
}
String* Convert::ToBase64String(Array1<uint8_t>* a) {
    static const char* tbl = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    std::string out;
    int32_t i = 0;
    for (; i + 2 < a->length; i += 3) {
        uint32_t v = (a->data[i] << 16) | (a->data[i + 1] << 8) | a->data[i + 2];
        out += tbl[v >> 18]; out += tbl[(v >> 12) & 63]; out += tbl[(v >> 6) & 63]; out += tbl[v & 63];
    }
    if (i < a->length) {
        uint32_t v = a->data[i] << 16;
        if (i + 1 < a->length) v |= a->data[i + 1] << 8;
        out += tbl[v >> 18]; out += tbl[(v >> 12) & 63];
        out += i + 1 < a->length ? tbl[(v >> 6) & 63] : '=';
        out += '=';
    }
    return String::FromUtf8(out.c_str());
}
Array1<uint8_t>* Convert::FromBase64String(String* s) {
    auto val = [](char16_t c) -> int { if (c >= 'A' && c <= 'Z') return c - 'A'; if (c >= 'a' && c <= 'z') return c - 'a' + 26; if (c >= '0' && c <= '9') return c - '0' + 52; if (c == '+') return 62; if (c == '/') return 63; return -1; };
    std::string out;
    uint32_t acc = 0;
    int bits = 0;
    for (int32_t i = 0; i < s->length; i++) {
        int v = val(s->chars[i]);
        if (v < 0) continue;
        acc = (acc << 6) | v;
        bits += 6;
        if (bits >= 8) { bits -= 8; out.push_back((char)((acc >> bits) & 0xFF)); }
    }
    auto* a = Array1<uint8_t>::New((int32_t)out.size());
    std::memcpy(a->data, out.data(), out.size());
    return a;
}

int32_t Collections::Generic::HashPrime(int32_t min) {
    static const int32_t primes[] = {3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353, 431, 521, 631, 761, 919, 1103, 1327, 1597, 1931, 2333, 2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591, 17519, 21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431, 90523, 108631, 130363, 156437, 187751, 225307, 270371, 324449, 389357, 467237, 560689, 672827, 807403, 968897, 1162687, 1395263, 1674319, 2009191, 2411033, 2893249, 3471899, 4166287, 4999559, 5999471, 7199369};
    for (int32_t p : primes)
        if (p >= min) return p;
    for (int32_t i = min | 1; i < INT32_MAX; i += 2) {
        bool prime = true;
        for (int32_t d = 3; (int64_t)d * d <= i; d += 2)
            if (i % d == 0) { prime = false; break; }
        if (prime) return i;
    }
    return min;
}

void Array::Clear(Array* a, int32_t index, int32_t len) {
    std::memset((uint8_t*)a->__data() + (size_t)index * a->__elemSize(), 0, (size_t)len * a->__elemSize());
}
void Array::Copy(Array* src, int32_t si, Array* dst, int32_t di, int32_t len) {
    if (len <= 0) return;
    if (si < 0 || di < 0 || si + len > src->length || di + len > dst->length) throw NewEx<ArgumentException>(STR(u"Array.Copy out of range"));
    int32_t es = src->__elemSize();
    std::memmove((uint8_t*)dst->__data() + (size_t)di * es, (uint8_t*)src->__data() + (size_t)si * es, (size_t)len * es);
}

// ---------------------------------------------------------------- text
Text::Encoding* Text::Encoding::get_UTF8() {
    static Encoding* e = []() { void* m = GC_malloc_uncollectable(sizeof(Encoding)); return new (m) Encoding(); }();
    return e;
}
Array1<uint8_t>* Text::Encoding::GetBytes(String* s) {
    char* u = s->ToUtf8();
    int32_t n = (int32_t)std::strlen(u);
    auto* a = Array1<uint8_t>::New(n);
    std::memcpy(a->data, u, n);
    return a;
}
String* Text::Encoding::GetString(Array1<uint8_t>* b) { return String::FromUtf8((const char*)b->data, b->length); }
String* Text::Encoding::GetString(Array1<uint8_t>* b, int32_t idx, int32_t n) { return String::FromUtf8((const char*)b->data + idx, n); }

void Text::RegularExpressions::Regex::__init(String* pattern) { __init(pattern, RegexOptions::None); }
void Text::RegularExpressions::Regex::__init(String* pattern, RegexOptions options) {
    auto flags = std::regex::ECMAScript;
    if ((int32_t)options & (int32_t)RegexOptions::IgnoreCase) flags |= std::regex::icase;
    impl = new std::regex(pattern->ToUtf8(), flags);  // leaked; regexes are static in the game
}
bool Text::RegularExpressions::Regex::IsMatch(String* input) { return std::regex_search(std::string(input->ToUtf8()), *(std::regex*)impl); }
bool Text::RegularExpressions::Regex::IsMatch(String* input, String* pattern) { return std::regex_search(std::string(input->ToUtf8()), std::regex(pattern->ToUtf8())); }

Globalization::CultureInfo* Globalization::CultureInfo::get_InvariantCulture() {
    static CultureInfo* c = []() { void* m = GC_malloc_uncollectable(sizeof(CultureInfo)); return new (m) CultureInfo(); }();
    return c;
}
bool Globalization::TextElementEnumerator::MoveNext() {
    index = next;
    if (index >= s->length) return false;
    next = index + 1;
    if (Char::IsHighSurrogate(s->chars[index]) && next < s->length) next++;
    // combining marks stay with their base character
    while (next < s->length && s->chars[next] >= 0x300 && s->chars[next] <= 0x36F) next++;
    return true;
}
String* Globalization::TextElementEnumerator::GetTextElement() { return s->Substring(index, next - index); }

StringComparer* StringComparer::get_Ordinal() {
    static StringComparer* c = []() { void* m = GC_malloc_uncollectable(sizeof(StringComparer)); return new (m) StringComparer(); }();
    return c;
}
StringComparer* StringComparer::get_OrdinalIgnoreCase() {
    static StringComparer* c = []() { void* m = GC_malloc_uncollectable(sizeof(StringComparer)); auto* r = new (m) StringComparer(); r->ignoreCase = true; return r; }();
    return c;
}
bool StringComparer::Equals(String* a, String* b) { return String::Equals(a, b, ignoreCase ? StringComparison::OrdinalIgnoreCase : StringComparison::Ordinal); }
int32_t StringComparer::GetHashCode(String* s) {
    if (!ignoreCase) return HashString(s);
    uint32_t h = 2166136261u;
    for (int32_t i = 0; i < s->length; i++) h = (h ^ Char::ToUpper(s->chars[i])) * 16777619u;
    return (int32_t)h;
}
int32_t StringComparer::Compare(String* a, String* b) {
    if (!ignoreCase) return String::CompareOrdinal(a, b);
    return String::CompareOrdinal(a ? a->ToUpper() : nullptr, b ? b->ToUpper() : nullptr);
}

// ---------------------------------------------------------------- threading
// One recursive mutex pool for `lock`; GC objects never move so the address is a stable key.
static std::recursive_mutex& lockFor(Object* o) {
    static std::recursive_mutex pool[64];
    return pool[((uintptr_t)o >> 4) & 63];
}
void Threading::Monitor::Enter(Object* o) { lockFor(o).lock(); }
void Threading::Monitor::Exit(Object* o) { lockFor(o).unlock(); }
// Depth of thread bodies currently running inline (see Thread::Start).
static int g_inlineThreadDepth = 0;
// A sleep inside an inline thread body (e.g. the 75 ms in map loading) would only stall the game loop.
void Threading::Thread::Sleep(int32_t ms) {
    if (g_inlineThreadDepth > 0) return;
    std::this_thread::sleep_for(std::chrono::milliseconds(ms));
}
extern "C" void rt_thread_start(void (*fn)(void*), void* arg, void** handle);
extern "C" void rt_thread_join(void* handle);
static void threadEntry(void* p) {
    auto* t = (Threading::Thread*)p;
    try {
        if (t->start) t->start->Invoke();
        else if (t->startParam) t->startParam->Invoke(nullptr);
    } catch (Exception* e) {
        rt_log("unhandled exception in thread");
    }
    t->alive = false;
}
// The GC is single-threaded (Vita has no thread suspension for it), so managed threads run to
// completion on the caller. Long-running loops (GbsPlayer) are pumped from the frame hook instead.
void Threading::Thread::Start() {
    alive = true;
    g_inlineThreadDepth++;
    threadEntry(this);
    g_inlineThreadDepth--;
}
void Threading::Thread::Start(Object*) { Start(); }
void Threading::Thread::Join() { if (handle) rt_thread_join(handle); }
bool Threading::Thread::Join(int32_t ms) {
    auto end = std::chrono::steady_clock::now() + std::chrono::milliseconds(ms);
    while (alive && std::chrono::steady_clock::now() < end) std::this_thread::sleep_for(std::chrono::milliseconds(1));
    return !alive;
}
Threading::Thread* Threading::Thread::get_CurrentThread() {
    static Thread* t = []() { void* m = GC_malloc_uncollectable(sizeof(Thread)); return new (m) Thread(); }();
    return t;
}
int32_t Threading::Thread::get_ManagedThreadId() { return 1; }
// Work items run synchronously: the GC is single-threaded on the Vita.
bool Threading::ThreadPool::QueueUserWorkItem(Fn<void(Object*)>* cb) {
    cb->Invoke(nullptr);
    return true;
}
Threading::Tasks::Task* Threading::Tasks::Task::Run(Fn<void()>* a) {
    a->Invoke();
    return New<Task>();
}
bool Threading::CancellationToken::get_IsCancellationRequested() { return src && src->cancelled.load(); }
void Threading::CancellationToken::ThrowIfCancellationRequested() {
    if (get_IsCancellationRequested()) throw NewEx<OperationCanceledException>(STR(u"Operation cancelled"));
}

// ---------------------------------------------------------------- reflection helpers
namespace Reflection {
ConstructorInfo* MakeCtor(std::initializer_list<ParameterInfo*> params, Object* (*invoke)(Array1<Object*>*)) {
    auto* c = new ConstructorInfo();
    c->params = Array1<ParameterInfo*>::Of(params);
    c->invoke = invoke;
    c->name = STR(u".ctor");
    return c;
}
ParameterInfo* MakeParam(const char16_t* name, Type* type, bool hasDefault, Object* def) {
    auto* p = new ParameterInfo();
    int32_t l = 0;
    while (name[l]) l++;
    p->name = String::FromUtf16(name, l);
    p->type = type;
    p->hasDefault = hasDefault;
    p->defaultValue = def;
    return p;
}
FieldInfo* MakeField(const char16_t* name, Type* type, bool isStatic, Object* (*get)(Object*), void (*set)(Object*, Object*)) {
    auto* f = new FieldInfo();
    int32_t l = 0;
    while (name[l]) l++;
    f->name = String::FromUtf16(name, l);
    f->type = type;
    f->isStatic = isStatic;
    f->get = get;
    f->set = set;
    return f;
}
Assembly* Assembly::GetExecutingAssembly() {
    static Assembly* a = []() { void* m = GC_malloc_uncollectable(sizeof(Assembly)); return new (m) Assembly(); }();
    return a;
}
extern "C" const char* rt_game_version();
template <>
AssemblyInformationalVersionAttribute* CustomAttributeExtensions::GetCustomAttribute<AssemblyInformationalVersionAttribute*>(Assembly*) {
    auto* a = new AssemblyInformationalVersionAttribute();
    a->version = String::FromUtf8(rt_game_version());
    return a;
}
}  // namespace Reflection

}  // namespace System
