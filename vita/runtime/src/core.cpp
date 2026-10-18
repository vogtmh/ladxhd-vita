// Runtime core: GC init, String, Type, exceptions, number formatting and parsing.
#include "rt/all.h"
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <cwctype>
#include <string>
#if defined(__vita__)
#include <malloc.h>
#endif

extern "C" {
void GC_init(void);
void GC_gcollect(void);
void GC_set_max_heap_size(size_t);
void GC_expand_hp(size_t);
size_t GC_get_total_bytes(void);
size_t GC_get_heap_size(void);
size_t GC_get_free_bytes(void);
void GC_set_disable_automatic_collection(int);
void GC_set_max_retries(unsigned long);
unsigned long GC_get_full_gc_total_time(void);
void GC_set_oom_fn(void* (*)(size_t));
void rt_log(const char*);
}

// Collection policy. A full collection costs 0.5-1 s on the Vita, so automatic collections are off and the game
// collects while the screen is black during a map transition. Boehm still collects by itself when the heap can't
// grow any further (max_retries).
namespace {
size_t g_heapLimit = 160u << 20;
bool g_enforceLimit = false;
size_t heapInUse() { return GC_get_heap_size() - GC_get_free_bytes(); }
void* outOfMemory(size_t bytes) {
    char msg[200];
    int n = std::snprintf(msg, sizeof msg, "out of memory: %u KB requested, heap %u MB, free blocks %u MB",
                          (unsigned)(bytes >> 10), (unsigned)(GC_get_heap_size() >> 20),
                          (unsigned)(GC_get_free_bytes() >> 20));
#if defined(__vita__)
    struct mallinfo mi = mallinfo();
    std::snprintf(msg + n, sizeof msg - n, ", malloc arena %u MB, used %u MB", (unsigned)(mi.arena >> 20),
                  (unsigned)(mi.uordblks >> 20));
#else
    (void)n;
#endif
    rt_log(msg);
    return nullptr;
}
}  // namespace

namespace rt {
void Init() { GC_init(); }
void ConfigureCollector(size_t heapLimit, bool enforceLimit) {
    g_heapLimit = heapLimit;
    g_enforceLimit = enforceLimit;
    if (enforceLimit) GC_set_max_heap_size(heapLimit);
    GC_set_disable_automatic_collection(1);
    GC_set_max_retries(3);
    GC_set_oom_fn(outOfMemory);
}
void* AllocFailed(size_t n, bool atomic) {
    // Free memory can be too fragmented for a large block even after collecting; the newlib heap has room left.
    static bool grown = false;
    if (g_enforceLimit && !grown) {
        grown = true;
        GC_set_max_heap_size(g_heapLimit + (16u << 20));
        rt_log("gc: heap limit raised by 16 MB");
        if (void* p = GcAlloc(n, atomic)) return p;
    }
    rt_log("out of memory, giving up");
    __builtin_trap();
}
void Collect() {
    size_t before = heapInUse();
    unsigned long t0 = GC_get_full_gc_total_time();
    GC_gcollect();
    char msg[160];
    // The Vita printf has no %zu.
    std::snprintf(msg, sizeof msg, "gc: %lu ms, heap %u MB, in use %u -> %u MB", GC_get_full_gc_total_time() - t0,
                  (unsigned)(GC_get_heap_size() >> 20), (unsigned)(before >> 20), (unsigned)(heapInUse() >> 20));
    rt_log(msg);
}
// GC.Collect(generation): 2 always collects, 1 collects above 40% of the heap limit (a cheap moment: little is
// live), 0 only above 60%.
void CollectGeneration(int32_t generation) {
    size_t threshold = generation >= 2 ? 0 : g_heapLimit / 10 * (generation == 1 ? 4 : 6);
    if (heapInUse() > threshold) Collect();
}
// GC.TryStartNoGCRegion(bytes): called before each map load; makes room up front so the load doesn't hit the heap
// limit.
bool BeginNoGC(int64_t bytes) {
    if (heapInUse() + (size_t)bytes > g_heapLimit) Collect();
    return true;
}
int64_t AllocatedBytes() { return (int64_t)GC_get_total_bytes(); }
}  // namespace rt

namespace System {

[[noreturn]] void ThrowNullReference() { throw NewEx<NullReferenceException>(STR(u"Object reference not set to an instance of an object.")); }
[[noreturn]] void ThrowIndexOutOfRange() { throw NewEx<IndexOutOfRangeException>(STR(u"Index was outside the bounds of the array.")); }
[[noreturn]] void ThrowInvalidCast() { throw NewEx<InvalidCastException>(STR(u"Specified cast is not valid.")); }
[[noreturn]] void ThrowKeyNotFound() { throw NewEx<KeyNotFoundException>(STR(u"The given key was not present in the dictionary.")); }
[[noreturn]] void ThrowInvalidOperation(const char* msg) { throw NewEx<InvalidOperationException>(String::FromUtf8(msg)); }
[[noreturn]] void ThrowArgument(const char* msg) { throw NewEx<ArgumentException>(String::FromUtf8(msg)); }
[[noreturn]] void ThrowSwitch() { throw NewEx<SwitchExpressionException>(STR(u"Non-exhaustive switch expression.")); }
[[noreturn]] void Math::ThrowArithmetic() { throw NewEx<InvalidOperationException>(STR(u"Function does not accept floating point Not-a-Number values.")); }

void TraceException(Exception* e) {
    static int enabled = -1;
    if (enabled < 0) enabled = std::getenv("LADX_TRACE_EX") ? 1 : 0;
    if (!enabled) return;
    std::fprintf(stderr, "[exception] %s: %s\n", e->GetType()->fullName->ToUtf8(), e->message ? e->message->ToUtf8() : "");
}

// ---------------------------------------------------------------- Object
String* Object::ToString() { return GetType()->fullName; }
Type* Object::__type() {
    static Type* t = Type::Create(u"Object", u"System.Object", nullptr);
    return t;
}
Type* Object::GetType() { return __type(); }
EventArgs* EventArgs::Empty = nullptr;
Type* EventArgs::__type() {
    static Type* t = Type::Create(u"EventArgs", u"System.EventArgs", Object::__type());
    return t;
}
Type* Exception::__type() {
    static Type* t = Type::Create(u"Exception", u"System.Exception", Object::__type());
    return t;
}
#define RT_EX_TYPE(N, B) \
    Type* N::__type() { static Type* t = Type::Create(u## #N, u"System." #N, B::__type()); return t; }
RT_EX_TYPE(SystemException, Exception)
RT_EX_TYPE(ArgumentException, SystemException)
RT_EX_TYPE(ArgumentNullException, ArgumentException)
RT_EX_TYPE(ArgumentOutOfRangeException, ArgumentException)
RT_EX_TYPE(InvalidOperationException, SystemException)
RT_EX_TYPE(NullReferenceException, SystemException)
RT_EX_TYPE(IndexOutOfRangeException, SystemException)
RT_EX_TYPE(InvalidCastException, SystemException)
RT_EX_TYPE(NotImplementedException, SystemException)
RT_EX_TYPE(NotSupportedException, SystemException)
RT_EX_TYPE(PlatformNotSupportedException, NotSupportedException)
RT_EX_TYPE(FormatException, SystemException)
RT_EX_TYPE(OverflowException, SystemException)
RT_EX_TYPE(OperationCanceledException, SystemException)
RT_EX_TYPE(MissingMemberException, SystemException)
RT_EX_TYPE(KeyNotFoundException, SystemException)
RT_EX_TYPE(IOException, SystemException)
RT_EX_TYPE(FileNotFoundException, IOException)
RT_EX_TYPE(DirectoryNotFoundException, IOException)
RT_EX_TYPE(InvalidDataException, SystemException)
RT_EX_TYPE(UnauthorizedAccessException, SystemException)
RT_EX_TYPE(SwitchExpressionException, InvalidOperationException)

// ---------------------------------------------------------------- Type
Type* Type::Create(const char16_t* n, const char16_t* fn, Type* base) {
    void* mem = GC_malloc_uncollectable(sizeof(Type));
    auto* t = new (mem) Type();
    auto len = [](const char16_t* s) { int32_t l = 0; while (s[l]) l++; return l; };
    t->name = String::Literal(n, len(n));
    t->fullName = String::Literal(fn, len(fn));
    t->baseType = base;
    return t;
}
Type* Type::GetType() {
    static Type* t = Create(u"Type", u"System.Type", Object::__type());
    return t;
}
Type* PrimitiveType(const char16_t* name, int32_t code) {
    std::u16string full = u"System.";
    full += name;
    void* mem = GC_malloc_uncollectable((full.size() + 1) * 2);
    std::memcpy(mem, full.c_str(), (full.size() + 1) * 2);
    Type* t = Type::Create(name, (const char16_t*)mem, nullptr);
    t->isValueType = true;
    t->primitive = code;
    return t;
}
Type* TypeOfImpl<String>::get() {
    static Type* t = Type::Create(u"String", u"System.String", Object::__type());
    return t;
}
Type* String::GetType() { return TypeOf<String>(); }
Type* EnumTypeCreate(const char16_t* name, const char16_t* fullName, Object* (*boxer)(int64_t)) {
    Type* t = Type::Create(name, fullName, nullptr);
    t->isValueType = true;
    t->isEnum = true;
    t->primitive = 9;
    return t;
}
bool Type::IsSubclassOf(Type* t) {
    for (Type* b = baseType; b; b = b->baseType)
        if (b == t) return true;
    return false;
}
bool Type::IsAssignableFrom(Type* t) {
    if (!t) return false;
    if (t == this) return true;
    if (isInterface) return false;  // interface checks need an instance
    return t->IsSubclassOf(this);
}
bool Type::IsInstanceOfType(Object* o) { return o && (isInstance ? isInstance(o) : IsAssignableFrom(o->GetType())); }
void Type::ensureReflected() {
    if (reflected) return;
    reflected = true;
    if (reflect) reflect(this);
}
Array1<Reflection::ConstructorInfo*>* Type::GetConstructors() {
    ensureReflected();
    return ctors ? (Array1<Reflection::ConstructorInfo*>*)ctors : Array1<Reflection::ConstructorInfo*>::New(0);
}
Reflection::FieldInfo* Type::GetField(String* n) { return GetField(n, Reflection::BindingFlags::Public | Reflection::BindingFlags::Instance | Reflection::BindingFlags::Static); }
Reflection::FieldInfo* Type::GetField(String* n, Reflection::BindingFlags flags) {
    for (Type* t = this; t; t = t->baseType) {
        t->ensureReflected();
        auto* fs = (Array1<Reflection::FieldInfo*>*)t->fields;
        if (!fs) continue;
        bool wantStatic = ((int32_t)flags & (int32_t)Reflection::BindingFlags::Static) != 0;
        bool wantInstance = ((int32_t)flags & (int32_t)Reflection::BindingFlags::Instance) != 0;
        for (auto* f : *fs)
            if (String::op_Equality(f->name, n) && (f->isStatic ? wantStatic : wantInstance)) return f;
    }
    return nullptr;
}
Array1<Reflection::FieldInfo*>* Type::GetFields() { return GetFields(Reflection::BindingFlags::Public | Reflection::BindingFlags::Instance | Reflection::BindingFlags::Static); }
Array1<Reflection::FieldInfo*>* Type::GetFields(Reflection::BindingFlags flags) {
    auto* l = New<List<Reflection::FieldInfo*>>();
    bool wantStatic = ((int32_t)flags & (int32_t)Reflection::BindingFlags::Static) != 0;
    bool wantInstance = ((int32_t)flags & (int32_t)Reflection::BindingFlags::Instance) != 0;
    ensureReflected();
    if (auto* fs = (Array1<Reflection::FieldInfo*>*)fields)
        for (auto* f : *fs)
            if (f->isStatic ? wantStatic : wantInstance) l->Add(f);
    return l->ToArray();
}

// ---------------------------------------------------------------- String
String* String::Empty = String::Literal(u"", 0);

String* String::Alloc(int32_t len) {
    void* mem = gc_alloc_atomic(sizeof(String) + (size_t)(len + 1) * 2);
    auto* s = new (mem) String();
    s->length = len;
    s->chars = (char16_t*)((uint8_t*)mem + sizeof(String));
    return s;
}
String* String::FromUtf16(const char16_t* p, int32_t len) {
    auto* s = Alloc(len);
    if (len) std::memcpy(s->chars, p, (size_t)len * 2);
    return s;
}
String* String::Literal(const char16_t* p, int32_t len) {
    void* mem = GC_malloc_uncollectable(sizeof(String) + (size_t)(len + 1) * 2);
    std::memset(mem, 0, sizeof(String) + (size_t)(len + 1) * 2);
    auto* s = new (mem) String();
    s->length = len;
    s->chars = (char16_t*)((uint8_t*)mem + sizeof(String));
    if (len) std::memcpy(s->chars, p, (size_t)len * 2);
    return s;
}
String* String::FromUtf8(const char* p) { return p ? FromUtf8(p, std::strlen(p)) : nullptr; }
String* String::FromUtf8(const char* p, size_t n) {
    std::u16string out;
    out.reserve(n);
    size_t i = 0;
    if (n >= 3 && (uint8_t)p[0] == 0xEF && (uint8_t)p[1] == 0xBB && (uint8_t)p[2] == 0xBF) i = 3;
    while (i < n) {
        uint32_t c = (uint8_t)p[i];
        uint32_t cp;
        int extra;
        if (c < 0x80) { cp = c; extra = 0; }
        else if ((c & 0xE0) == 0xC0) { cp = c & 0x1F; extra = 1; }
        else if ((c & 0xF0) == 0xE0) { cp = c & 0x0F; extra = 2; }
        else if ((c & 0xF8) == 0xF0) { cp = c & 0x07; extra = 3; }
        else { cp = 0xFFFD; extra = 0; }
        i++;
        for (int k = 0; k < extra; k++) {
            if (i >= n || ((uint8_t)p[i] & 0xC0) != 0x80) { cp = 0xFFFD; break; }
            cp = (cp << 6) | ((uint8_t)p[i] & 0x3F);
            i++;
        }
        if (cp >= 0x10000) {
            cp -= 0x10000;
            out.push_back((char16_t)(0xD800 + (cp >> 10)));
            out.push_back((char16_t)(0xDC00 + (cp & 0x3FF)));
        } else out.push_back((char16_t)cp);
    }
    return FromUtf16(out.data(), (int32_t)out.size());
}
char* String::ToUtf8() const {
    std::string out;
    out.reserve(length);
    for (int32_t i = 0; i < length; i++) {
        uint32_t c = chars[i];
        if (c >= 0xD800 && c <= 0xDBFF && i + 1 < length && chars[i + 1] >= 0xDC00 && chars[i + 1] <= 0xDFFF) {
            c = 0x10000 + ((c - 0xD800) << 10) + (chars[i + 1] - 0xDC00);
            i++;
        }
        if (c < 0x80) out.push_back((char)c);
        else if (c < 0x800) { out.push_back((char)(0xC0 | (c >> 6))); out.push_back((char)(0x80 | (c & 0x3F))); }
        else if (c < 0x10000) { out.push_back((char)(0xE0 | (c >> 12))); out.push_back((char)(0x80 | ((c >> 6) & 0x3F))); out.push_back((char)(0x80 | (c & 0x3F))); }
        else { out.push_back((char)(0xF0 | (c >> 18))); out.push_back((char)(0x80 | ((c >> 12) & 0x3F))); out.push_back((char)(0x80 | ((c >> 6) & 0x3F))); out.push_back((char)(0x80 | (c & 0x3F))); }
    }
    char* r = (char*)gc_alloc_atomic(out.size() + 1);
    std::memcpy(r, out.c_str(), out.size() + 1);
    return r;
}

static char16_t lowerC(char16_t c) { return (char16_t)(c < 0x80 ? (c >= 'A' && c <= 'Z' ? c + 32 : c) : std::towlower(c)); }
static char16_t upperC(char16_t c) { return (char16_t)(c < 0x80 ? (c >= 'a' && c <= 'z' ? c - 32 : c) : std::towupper(c)); }
static bool isWs(char16_t c) { return c == ' ' || (c >= 9 && c <= 13) || c == 0x85 || c == 0xA0 || c == 0x2028 || c == 0x2029 || c == 0x3000 || (c >= 0x2000 && c <= 0x200A) || c == 0x1680 || c == 0x202F || c == 0x205F; }
static bool eqChars(const char16_t* a, const char16_t* b, int32_t n, bool ic) {
    if (!ic) return std::memcmp(a, b, (size_t)n * 2) == 0;
    for (int32_t i = 0; i < n; i++)
        if (upperC(a[i]) != upperC(b[i])) return false;
    return true;
}
static bool ignoreCase(StringComparison c) { return c == StringComparison::OrdinalIgnoreCase || c == StringComparison::CurrentCultureIgnoreCase || c == StringComparison::InvariantCultureIgnoreCase; }

bool String::op_Equality(String* a, String* b) {
    if (a == b) return true;
    if (!a || !b || a->length != b->length) return false;
    return std::memcmp(a->chars, b->chars, (size_t)a->length * 2) == 0;
}
bool String::Equals(Object* o) { return op_Equality(this, dynamic_cast<String*>(o)); }
bool String::Equals(String* o) { return op_Equality(this, o); }
bool String::Equals(String* o, StringComparison c) { return Equals(this, o, c); }
bool String::Equals(String* a, String* b, StringComparison c) {
    if (a == b) return true;
    if (!a || !b || a->length != b->length) return false;
    return eqChars(a->chars, b->chars, a->length, ignoreCase(c));
}
int32_t HashString(String* s) {
    uint32_t h = 2166136261u;
    for (int32_t i = 0; i < s->length; i++) h = (h ^ s->chars[i]) * 16777619u;
    return (int32_t)h;
}
int32_t String::GetHashCode() { return HashString(this); }
bool String::IsNullOrWhiteSpace(String* s) {
    if (!s) return true;
    for (int32_t i = 0; i < s->length; i++)
        if (!isWs(s->chars[i])) return false;
    return true;
}
int32_t String::CompareOrdinal(String* a, String* b) {
    if (a == b) return 0;
    if (!a) return -1;
    if (!b) return 1;
    int32_t n = a->length < b->length ? a->length : b->length;
    for (int32_t i = 0; i < n; i++)
        if (a->chars[i] != b->chars[i]) return a->chars[i] < b->chars[i] ? -1 : 1;
    return a->length < b->length ? -1 : (a->length > b->length ? 1 : 0);
}
// Culture-aware compare approximated: case-insensitive first, then ordinal.
int32_t String::Compare(String* a, String* b) {
    if (a == b) return 0;
    if (!a) return -1;
    if (!b) return 1;
    int32_t n = a->length < b->length ? a->length : b->length;
    for (int32_t i = 0; i < n; i++) {
        char16_t x = lowerC(a->chars[i]), y = lowerC(b->chars[i]);
        if (x != y) return x < y ? -1 : 1;
    }
    if (a->length != b->length) return a->length < b->length ? -1 : 1;
    return -CompareOrdinal(a, b);
}
bool String::Contains(char16_t c) { return IndexOf(c) >= 0; }
bool String::Contains(String* s) { return IndexOf(s) >= 0; }
bool String::StartsWith(String* s) { return StartsWith(s, StringComparison::Ordinal); }
bool String::StartsWith(String* s, StringComparison c) { nn(s); return s->length <= length && eqChars(chars, s->chars, s->length, ignoreCase(c)); }
bool String::EndsWith(String* s) { return EndsWith(s, StringComparison::Ordinal); }
bool String::EndsWith(String* s, StringComparison c) { nn(s); return s->length <= length && eqChars(chars + length - s->length, s->chars, s->length, ignoreCase(c)); }
int32_t String::IndexOf(char16_t c) { return IndexOf(c, 0); }
int32_t String::IndexOf(char16_t c, int32_t start) {
    for (int32_t i = start; i < length; i++)
        if (chars[i] == c) return i;
    return -1;
}
int32_t String::IndexOf(String* s) { return IndexOf(s, 0, StringComparison::Ordinal); }
int32_t String::IndexOf(String* s, StringComparison c) { return IndexOf(s, 0, c); }
int32_t String::IndexOf(String* s, int32_t start, StringComparison c) {
    nn(s);
    bool ic = ignoreCase(c);
    for (int32_t i = start; i + s->length <= length; i++)
        if (eqChars(chars + i, s->chars, s->length, ic)) return i;
    return -1;
}
int32_t String::LastIndexOf(char16_t c) {
    for (int32_t i = length - 1; i >= 0; i--)
        if (chars[i] == c) return i;
    return -1;
}
String* String::Substring(int32_t start) { return Substring(start, length - start); }
String* String::Substring(int32_t start, int32_t len) {
    if (start < 0 || len < 0 || start + len > length) throw NewEx<ArgumentOutOfRangeException>(STR(u"Substring index out of range"));
    if (start == 0 && len == length) return this;
    return FromUtf16(chars + start, len);
}
String* String::Insert(int32_t start, String* v) {
    auto* r = Alloc(length + v->length);
    std::memcpy(r->chars, chars, (size_t)start * 2);
    std::memcpy(r->chars + start, v->chars, (size_t)v->length * 2);
    std::memcpy(r->chars + start + v->length, chars + start, (size_t)(length - start) * 2);
    return r;
}
String* String::Remove(int32_t start) { return Substring(0, start); }
String* String::Remove(int32_t start, int32_t count) {
    auto* r = Alloc(length - count);
    std::memcpy(r->chars, chars, (size_t)start * 2);
    std::memcpy(r->chars + start, chars + start + count, (size_t)(length - start - count) * 2);
    return r;
}
String* String::Replace(char16_t a, char16_t b) {
    auto* r = FromUtf16(chars, length);
    for (int32_t i = 0; i < length; i++)
        if (r->chars[i] == a) r->chars[i] = b;
    return r;
}
String* String::Replace(String* a, String* b) { return Replace(a, b, StringComparison::Ordinal); }
String* String::Replace(String* a, String* b, StringComparison c) {
    nn(a);
    if (a->length == 0) ThrowArgument("oldValue empty");
    bool ic = ignoreCase(c);
    std::u16string out;
    int32_t i = 0;
    bool changed = false;
    while (i < length) {
        if (i + a->length <= length && eqChars(chars + i, a->chars, a->length, ic)) {
            if (b) out.append(b->chars, b->length);
            i += a->length;
            changed = true;
        } else out.push_back(chars[i++]);
    }
    return changed ? FromUtf16(out.data(), (int32_t)out.size()) : this;
}
String* String::ToLower() {
    auto* r = FromUtf16(chars, length);
    for (int32_t i = 0; i < length; i++) r->chars[i] = lowerC(r->chars[i]);
    return r;
}
String* String::ToUpper() {
    auto* r = FromUtf16(chars, length);
    for (int32_t i = 0; i < length; i++) r->chars[i] = upperC(r->chars[i]);
    return r;
}
static String* trimImpl(String* s, bool start, bool end, const char16_t* set, int32_t n) {
    auto match = [&](char16_t c) {
        if (!set) return isWs(c);
        for (int32_t i = 0; i < n; i++)
            if (set[i] == c) return true;
        return false;
    };
    int32_t a = 0, b = s->length;
    if (start) while (a < b && match(s->chars[a])) a++;
    if (end) while (b > a && match(s->chars[b - 1])) b--;
    return (a == 0 && b == s->length) ? s : String::FromUtf16(s->chars + a, b - a);
}
String* String::Trim() { return trimImpl(this, true, true, nullptr, 0); }
String* String::TrimEnd() { return trimImpl(this, false, true, nullptr, 0); }
String* String::TrimStart() { return trimImpl(this, true, false, nullptr, 0); }
String* String::TrimStart(char16_t c) { return trimImpl(this, true, false, &c, 1); }
String* String::TrimEnd(char16_t c) { return trimImpl(this, false, true, &c, 1); }
String* String::Trim(char16_t c) { return trimImpl(this, true, true, &c, 1); }
String* String::TrimStart(Array1<char16_t>* cs) { return cs && cs->length ? trimImpl(this, true, false, cs->data, cs->length) : TrimStart(); }
String* String::TrimEnd(Array1<char16_t>* cs) { return cs && cs->length ? trimImpl(this, false, true, cs->data, cs->length) : TrimEnd(); }
String* String::Trim(Array1<char16_t>* cs) { return cs && cs->length ? trimImpl(this, true, true, cs->data, cs->length) : Trim(); }
String* String::PadLeft(int32_t n) { return PadLeft(n, u' '); }
String* String::PadLeft(int32_t n, char16_t c) {
    if (n <= length) return this;
    auto* r = Alloc(n);
    for (int32_t i = 0; i < n - length; i++) r->chars[i] = c;
    std::memcpy(r->chars + n - length, chars, (size_t)length * 2);
    return r;
}
String* String::PadRight(int32_t n) { return PadRight(n, u' '); }
String* String::PadRight(int32_t n, char16_t c) {
    if (n <= length) return this;
    auto* r = Alloc(n);
    std::memcpy(r->chars, chars, (size_t)length * 2);
    for (int32_t i = length; i < n; i++) r->chars[i] = c;
    return r;
}
Array1<char16_t>* String::ToCharArray() {
    auto* a = Array1<char16_t>::New(length);
    if (length) std::memcpy(a->data, chars, (size_t)length * 2);
    return a;
}
static Array1<String*>* splitImpl(String* s, const char16_t* seps, int32_t nseps, Array1<String*>* strSeps, int32_t count, StringSplitOptions opt) {
    auto* parts = New<List<String*>>();
    bool removeEmpty = ((int32_t)opt & 1) != 0;
    bool trim = ((int32_t)opt & 2) != 0;
    auto add = [&](int32_t a, int32_t b) {
        String* p = String::FromUtf16(s->chars + a, b - a);
        if (trim) p = p->Trim();
        if (removeEmpty && p->length == 0) return;
        parts->Add(p);
    };
    int32_t start = 0;
    int32_t i = 0;
    while (i < s->length) {
        if (count > 0 && parts->size >= count - 1) break;
        int32_t matchLen = 0;
        if (strSeps) {
            for (auto* sep : *strSeps)
                if (sep && sep->length && i + sep->length <= s->length && std::memcmp(s->chars + i, sep->chars, (size_t)sep->length * 2) == 0) { matchLen = sep->length; break; }
        } else if (nseps == 0) {
            if (isWs(s->chars[i])) matchLen = 1;
        } else {
            for (int32_t k = 0; k < nseps; k++)
                if (s->chars[i] == seps[k]) { matchLen = 1; break; }
        }
        if (matchLen) {
            add(start, i);
            i += matchLen;
            start = i;
        } else i++;
    }
    add(start, s->length);
    return parts->ToArray();
}
Array1<String*>* String::Split(char16_t sep, StringSplitOptions o) { return splitImpl(this, &sep, 1, nullptr, 0, o); }
Array1<String*>* String::Split(char16_t sep, int32_t count, StringSplitOptions o) { return splitImpl(this, &sep, 1, nullptr, count, o); }
Array1<String*>* String::Split(Array1<char16_t>* seps, int32_t count) { return splitImpl(this, seps ? seps->data : nullptr, seps ? seps->length : 0, nullptr, count, StringSplitOptions::None); }
Array1<String*>* String::Split(Array1<char16_t>* seps, int32_t count, StringSplitOptions o) { return splitImpl(this, seps ? seps->data : nullptr, seps ? seps->length : 0, nullptr, count, o); }
Array1<String*>* String::Split(Array1<char16_t>* seps) { return splitImpl(this, seps ? seps->data : nullptr, seps ? seps->length : 0, nullptr, 0, StringSplitOptions::None); }
Array1<String*>* String::Split(String* sep, StringSplitOptions o) {
    if (!sep || sep->length == 0) return splitImpl(this, nullptr, 0, nullptr, 0, o);
    return splitImpl(this, nullptr, 0, Array1<String*>::Of({sep}), 0, o);
}
Array1<String*>* String::Split(Array1<String*>* seps, StringSplitOptions o) { return splitImpl(this, nullptr, 0, seps, 0, o); }

String* String::Concat(String* a, String* b) {
    if (!a) a = Empty;
    if (!b) b = Empty;
    if (a->length == 0) return b;
    if (b->length == 0) return a;
    auto* r = Alloc(a->length + b->length);
    std::memcpy(r->chars, a->chars, (size_t)a->length * 2);
    std::memcpy(r->chars + a->length, b->chars, (size_t)b->length * 2);
    return r;
}
String* String::Concat(String* a, String* b, String* c) { return ConcatN({a, b, c}); }
String* String::Concat(String* a, String* b, String* c, String* d) { return ConcatN({a, b, c, d}); }
String* String::ConcatN(std::initializer_list<String*> parts) {
    int32_t n = 0;
    for (auto* p : parts) if (p) n += p->length;
    auto* r = Alloc(n);
    int32_t k = 0;
    for (auto* p : parts)
        if (p) { std::memcpy(r->chars + k, p->chars, (size_t)p->length * 2); k += p->length; }
    return r;
}
String* String::Join(String* sep, Array1<String*>* values) {
    std::u16string out;
    for (int32_t i = 0; i < values->length; i++) {
        if (i && sep) out.append(sep->chars, sep->length);
        if (values->data[i]) out.append(values->data[i]->chars, values->data[i]->length);
    }
    return FromUtf16(out.data(), (int32_t)out.size());
}
String* String::Join(String* sep, Object* values) {
    auto* e = dynamic_cast<IEnumerable<String*>*>(values);
    if (!e) return Empty;
    std::u16string out;
    bool first = true;
    for (auto* v : Each(e)) {
        if (!first && sep) out.append(sep->chars, sep->length);
        first = false;
        if (v) out.append(v->chars, v->length);
    }
    return FromUtf16(out.data(), (int32_t)out.size());
}
String* String::FromChar(char16_t c, int32_t count) {
    auto* r = Alloc(count);
    for (int32_t i = 0; i < count; i++) r->chars[i] = c;
    return r;
}
String* String::FromChars(Array1<char16_t>* cs) { return FromUtf16(cs->data, cs->length); }
String* String::Format(String* fmt, Array1<Object*>* args) {
    std::u16string out;
    for (int32_t i = 0; i < fmt->length; i++) {
        char16_t c = fmt->chars[i];
        if (c == '{' && i + 1 < fmt->length && fmt->chars[i + 1] == '{') { out.push_back('{'); i++; continue; }
        if (c == '}' && i + 1 < fmt->length && fmt->chars[i + 1] == '}') { out.push_back('}'); i++; continue; }
        if (c == '{') {
            int32_t j = i + 1, idx = 0;
            while (j < fmt->length && fmt->chars[j] >= '0' && fmt->chars[j] <= '9') idx = idx * 10 + (fmt->chars[j++] - '0');
            int32_t end = j;
            while (end < fmt->length && fmt->chars[end] != '}') end++;
            Object* a = args && idx < args->length ? args->data[idx] : nullptr;
            String* s = a ? a->ToString() : Empty;
            if (s) out.append(s->chars, s->length);
            i = end;
            continue;
        }
        out.push_back(c);
    }
    return FromUtf16(out.data(), (int32_t)out.size());
}
String* String::Format(String* fmt, Object* a) { return Format(fmt, Array1<Object*>::Of({a})); }
String* String::Format(String* fmt, Object* a, Object* b) { return Format(fmt, Array1<Object*>::Of({a, b})); }
String* String::Format(String* fmt, Object* a, Object* b, Object* c) { return Format(fmt, Array1<Object*>::Of({a, b, c})); }

// ---------------------------------------------------------------- ToStr / formatting
String* ToStr(bool v) { return v ? STR(u"True") : STR(u"False"); }
String* ToStr(char16_t v) { return String::FromUtf16(&v, 1); }
String* ToStr(int32_t v) { char b[16]; std::snprintf(b, sizeof b, "%d", v); return String::FromUtf8(b); }
String* ToStr(uint32_t v) { char b[16]; std::snprintf(b, sizeof b, "%u", v); return String::FromUtf8(b); }
String* ToStr(int64_t v) { char b[32]; std::snprintf(b, sizeof b, "%lld", (long long)v); return String::FromUtf8(b); }
String* ToStr(uint64_t v) { char b[32]; std::snprintf(b, sizeof b, "%llu", (unsigned long long)v); return String::FromUtf8(b); }

// Shortest round-trip representation like .NET Core ("R").
static String* realToStr(double v, int maxDigits, bool isFloat) {
    if (std::isnan(v)) return STR(u"NaN");
    if (std::isinf(v)) return v > 0 ? STR(u"∞") : STR(u"-∞");
    char buf[64];
    for (int p = 1; p <= maxDigits; p++) {
        std::snprintf(buf, sizeof buf, "%.*g", p, v);
        if (isFloat ? (float)std::strtod(buf, nullptr) == (float)v : std::strtod(buf, nullptr) == v) break;
    }
    // .NET style exponent: 1E+20, 1E-05
    std::string s = buf;
    auto e = s.find('e');
    if (e != std::string::npos) {
        std::string mant = s.substr(0, e);
        std::string ex = s.substr(e + 1);
        char sign = ex[0];
        std::string digits = ex.substr(1);
        while (digits.size() > 2 && digits[0] == '0') digits.erase(0, 1);
        // .NET uses fixed notation up to 1e15 for double / 1e7 for float, else E notation.
        s = mant + "E" + sign + digits;
    }
    return String::FromUtf8(s.c_str());
}
String* ToStr(float v) { return realToStr(v, 9, true); }
String* ToStr(double v) { return realToStr(v, 17, false); }

// Custom/standard numeric format subset: F/N/D/X/P/E, and 0/#/. patterns.
static String* formatNumber(double v, bool isInt, int64_t iv, const char16_t* f, int32_t n) {
    char buf[128];
    if (n == 0) return isInt ? ToStr(iv) : ToStr(v);
    char16_t c0 = f[0];
    int prec = -1;
    if (n > 1) {
        prec = 0;
        for (int32_t i = 1; i < n && f[i] >= '0' && f[i] <= '9'; i++) prec = prec * 10 + (f[i] - '0');
    }
    bool allDigits = true;
    for (int32_t i = 1; i < n; i++) if (f[i] < '0' || f[i] > '9') allDigits = false;
    if (allDigits && (c0 == 'F' || c0 == 'f')) {
        std::snprintf(buf, sizeof buf, "%.*f", prec < 0 ? 2 : prec, isInt ? (double)iv : v);
        return String::FromUtf8(buf);
    }
    if (allDigits && (c0 == 'N' || c0 == 'n')) {
        std::snprintf(buf, sizeof buf, "%.*f", prec < 0 ? 2 : prec, isInt ? (double)iv : v);
        std::string s = buf, out;
        auto dot = s.find('.');
        std::string ip = s.substr(0, dot), fp = dot == std::string::npos ? "" : s.substr(dot);
        bool neg = !ip.empty() && ip[0] == '-';
        if (neg) ip.erase(0, 1);
        for (size_t i = 0; i < ip.size(); i++) {
            if (i && (ip.size() - i) % 3 == 0) out.push_back(',');
            out.push_back(ip[i]);
        }
        return String::FromUtf8(((neg ? "-" : "") + out + fp).c_str());
    }
    if (allDigits && (c0 == 'D' || c0 == 'd')) {
        std::snprintf(buf, sizeof buf, "%0*lld", prec < 0 ? 1 : prec, (long long)(isInt ? iv : (int64_t)v));
        return String::FromUtf8(buf);
    }
    if (allDigits && (c0 == 'X' || c0 == 'x')) {
        std::snprintf(buf, sizeof buf, c0 == 'X' ? "%0*llX" : "%0*llx", prec < 0 ? 1 : prec, (unsigned long long)(isInt ? iv : (int64_t)v));
        return String::FromUtf8(buf);
    }
    if (allDigits && (c0 == 'P' || c0 == 'p')) {
        std::snprintf(buf, sizeof buf, "%.*f %%", prec < 0 ? 2 : prec, (isInt ? (double)iv : v) * 100.0);
        return String::FromUtf8(buf);
    }
    if (allDigits && (c0 == 'G' || c0 == 'g')) return isInt ? ToStr(iv) : ToStr(v);
    // Custom pattern: count zeros/hashes before and after '.'
    int intZeros = 0, fracZeros = 0, fracHashes = 0;
    bool afterDot = false, percent = false;
    std::string prefix, suffix;
    bool seenDigit = false;
    for (int32_t i = 0; i < n; i++) {
        char16_t c = f[i];
        if (c == '.') afterDot = true;
        else if (c == '0') { seenDigit = true; if (afterDot) fracZeros++; else intZeros++; }
        else if (c == '#') { seenDigit = true; if (afterDot) fracHashes++; }
        else if (c == '%') percent = true;
        else if (c == ',') {}
        else if (!seenDigit) prefix.push_back((char)c);
        else suffix.push_back((char)c);
    }
    double x = isInt ? (double)iv : v;
    if (percent) x *= 100;
    int decimals = fracZeros + fracHashes;
    std::snprintf(buf, sizeof buf, "%.*f", decimals, x);
    std::string s = buf;
    if (fracHashes > 0) {
        auto dot = s.find('.');
        if (dot != std::string::npos) {
            size_t minLen = dot + 1 + fracZeros;
            while (s.size() > minLen && s.back() == '0') s.pop_back();
            if (s.back() == '.') s.pop_back();
        }
    }
    bool neg = s[0] == '-';
    if (neg) s.erase(0, 1);
    auto dot = s.find('.');
    size_t ilen = dot == std::string::npos ? s.size() : dot;
    if ((int)ilen < intZeros) s.insert(0, intZeros - ilen, '0');
    if (intZeros == 0 && s.rfind("0", 0) == 0 && s.size() > 1 && s[1] == '.') s.erase(0, 1);
    if (neg && std::strtod(s.c_str(), nullptr) != 0) s.insert(0, "-");
    return String::FromUtf8((prefix + s + (percent ? "%" : "") + suffix).c_str());
}
String* Format(double v, const char16_t* fmt, int32_t n) { return formatNumber(v, false, 0, fmt, n); }
String* FormatInt(int64_t v, const char16_t* fmt, int32_t n) { return formatNumber(0, true, v, fmt, n); }
String* PadAlign(String* s, int32_t align) {
    if (!s) s = String::Empty;
    return align >= 0 ? s->PadLeft(align) : s->PadRight(-align);
}

String* EnumToStringImpl(int64_t v, const int64_t* values, const char16_t* const* names, int32_t n, bool flags) {
    auto lit = [](const char16_t* s) { int32_t l = 0; while (s[l]) l++; return String::FromUtf16(s, l); };
    for (int32_t i = 0; i < n; i++)
        if (values[i] == v) return lit(names[i]);
    if (flags && v != 0) {
        std::u16string out;
        int64_t rest = v;
        for (int32_t i = n - 1; i >= 0; i--) {
            if (values[i] != 0 && (rest & values[i]) == values[i]) {
                if (!out.empty()) out = u", " + out;
                std::u16string nm;
                for (const char16_t* p = names[i]; *p; p++) nm.push_back(*p);
                out = nm + out;
                rest &= ~values[i];
            }
        }
        if (rest == 0 && !out.empty()) return String::FromUtf16(out.data(), (int32_t)out.size());
    }
    return ToStr(v);
}

// ---------------------------------------------------------------- Parsing
static std::string toAscii(String* s) {
    std::string r;
    if (!s) return r;
    for (int32_t i = 0; i < s->length; i++) r.push_back(s->chars[i] < 128 ? (char)s->chars[i] : '?');
    return r;
}
static std::string trimmed(String* s) {
    std::string r = toAscii(s);
    size_t a = r.find_first_not_of(" \t\r\n"), b = r.find_last_not_of(" \t\r\n");
    return a == std::string::npos ? std::string() : r.substr(a, b - a + 1);
}
static bool parseI64(String* s, int64_t& out) {
    std::string t = trimmed(s);
    if (t.empty()) return false;
    char* end = nullptr;
    errno = 0;
    long long v = std::strtoll(t.c_str(), &end, 10);
    if (*end != 0 || errno) return false;
    out = v;
    return true;
}
static bool parseF64(String* s, double& out) {
    std::string t = trimmed(s);
    if (t.empty()) return false;
    if (t == "NaN") { out = NAN; return true; }
    if (t == "Infinity" || t == "∞") { out = INFINITY; return true; }
    if (t == "-Infinity") { out = -INFINITY; return true; }
    char* end = nullptr;
    double v = std::strtod(t.c_str(), &end);
    if (*end != 0) return false;
    out = v;
    return true;
}
[[noreturn]] static void throwFormat() { throw NewEx<FormatException>(STR(u"Input string was not in a correct format.")); }
bool Boolean::TryParse(String* s, bool& r) {
    std::string t = trimmed(s);
    for (auto& c : t) c = (char)std::tolower(c);
    if (t == "true") { r = true; return true; }
    if (t == "false") { r = false; return true; }
    r = false;
    return false;
}
bool Boolean::Parse(String* s) { bool r; if (!TryParse(s, r)) throwFormat(); return r; }
bool Int32::TryParse(String* s, int32_t& r) { int64_t v; if (!parseI64(s, v) || v < INT32_MIN || v > INT32_MAX) { r = 0; return false; } r = (int32_t)v; return true; }
int32_t Int32::Parse(String* s) { int32_t r; if (!TryParse(s, r)) throwFormat(); return r; }
bool Int16::TryParse(String* s, int16_t& r) { int64_t v; if (!parseI64(s, v) || v < -32768 || v > 32767) { r = 0; return false; } r = (int16_t)v; return true; }
int16_t Int16::Parse(String* s) { int16_t r; if (!TryParse(s, r)) throwFormat(); return r; }
bool Byte::TryParse(String* s, uint8_t& r) { int64_t v; if (!parseI64(s, v) || v < 0 || v > 255) { r = 0; return false; } r = (uint8_t)v; return true; }
uint8_t Byte::Parse(String* s) { uint8_t r; if (!TryParse(s, r)) throwFormat(); return r; }
bool Int64::TryParse(String* s, int64_t& r) { if (!parseI64(s, r)) { r = 0; return false; } return true; }
int64_t Int64::Parse(String* s) { int64_t r; if (!TryParse(s, r)) throwFormat(); return r; }
bool Single::TryParse(String* s, float& r) { double v; if (!parseF64(s, v)) { r = 0; return false; } r = (float)v; return true; }
float Single::Parse(String* s) { float r; if (!TryParse(s, r)) throwFormat(); return r; }
bool Double::TryParse(String* s, double& r) { if (!parseF64(s, r)) { r = 0; return false; } return true; }
double Double::Parse(String* s) { double r; if (!TryParse(s, r)) throwFormat(); return r; }

double Math::Round(double a, int32_t digits, int) {
    double p = std::pow(10.0, digits);
    return std::nearbyint(a * p) / p;
}
double Math::Round(double a, MidpointRounding mode) { return mode == MidpointRounding::AwayFromZero ? std::round(a) : std::nearbyint(a); }
float MathF::Round(float a, MidpointRounding mode) { return mode == MidpointRounding::AwayFromZero ? std::round(a) : std::nearbyint(a); }

bool Char::IsLetter(char16_t c) { return c < 128 ? ((c | 32) >= 'a' && (c | 32) <= 'z') : std::iswalpha(c); }
bool Char::IsWhiteSpace(char16_t c) { return isWs(c); }
bool Char::IsUpper(char16_t c) { return c < 128 ? (c >= 'A' && c <= 'Z') : std::iswupper(c); }
bool Char::IsLower(char16_t c) { return c < 128 ? (c >= 'a' && c <= 'z') : std::iswlower(c); }
bool Char::IsPunctuation(char16_t c) { return c < 128 ? std::ispunct(c) && c != '$' && c != '+' && c != '<' && c != '=' && c != '>' && c != '^' && c != '`' && c != '|' && c != '~' : false; }
char16_t Char::ToUpper(char16_t c) { return upperC(c); }
char16_t Char::ToLower(char16_t c) { return lowerC(c); }

}  // namespace System
