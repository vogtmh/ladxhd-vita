#pragma once
// Core of the C# runtime subset for transpiled code: GC objects, strings, arrays, boxing, delegates.
#include <cstdint>
#include <cstddef>
#include <cstring>
#include <cmath>
#include <new>
#include <type_traits>
#include <utility>
#include <limits>

extern "C" {
void* GC_malloc(size_t);
void* GC_malloc_atomic(size_t);
void* GC_malloc_ignore_off_page(size_t);
void* GC_malloc_atomic_ignore_off_page(size_t);
void* GC_malloc_uncollectable(size_t);
}

namespace rt {
// Arrays are referenced through their start, so a large one only needs a first page without false references.
inline void* GcAlloc(size_t n, bool atomic) {
    if (n >= 64 * 1024) return atomic ? GC_malloc_atomic_ignore_off_page(n) : GC_malloc_ignore_off_page(n);
    return atomic ? GC_malloc_atomic(n) : GC_malloc(n);
}
// Grows the heap past its limit once; aborts with a log entry if that doesn't help.
void* AllocFailed(size_t n, bool atomic);
}  // namespace rt

namespace System {

struct Type;
struct String;
struct Object;
enum class StringComparison : int32_t;
template <class T>
struct Array1;
namespace Reflection {
struct FieldInfo;
struct ConstructorInfo;
enum class BindingFlags : int32_t;
}  // namespace Reflection
enum class StringSplitOptions : int32_t;

inline void* gc_alloc(size_t n) {
    void* p = rt::GcAlloc(n, false);
    return p ? p : rt::AllocFailed(n, false);
}
inline void* gc_alloc_atomic(size_t n) {
    void* p = rt::GcAlloc(n, true);
    if (!p) p = rt::AllocFailed(n, true);
    std::memset(p, 0, n);
    return p;
}

[[noreturn]] void ThrowNullReference();
[[noreturn]] void ThrowIndexOutOfRange();
[[noreturn]] void ThrowInvalidCast();
[[noreturn]] void ThrowKeyNotFound();
[[noreturn]] void ThrowInvalidOperation(const char* msg);
[[noreturn]] void ThrowArgument(const char* msg);

struct Object {
    static void* operator new(size_t n) { return gc_alloc(n); }
    static void* operator new(size_t, void* p) { return p; }
    static void operator delete(void*) {}
    virtual ~Object() = default;
    void __init() {}
    virtual String* ToString();
    virtual bool Equals(Object* o) { return this == o; }
    virtual int32_t GetHashCode() { return (int32_t)(((uintptr_t)this >> 3) * 2654435761u); }
    virtual Type* GetType();
    static Type* __type();
    static bool ReferenceEquals(Object* a, Object* b) { return a == b; }
    static bool Equals(Object* a, Object* b) { return a == b || (a && b && a->Equals(b)); }
};

// Interfaces are polymorphic but do not derive Object; convert with AsObject.
template <class T>
inline Object* AsObject(T* p) {
    if constexpr (std::is_base_of_v<Object, T>) return p;
    else return dynamic_cast<Object*>(p);
}

template <class T, class U>
inline T* cast(U* p) {
    if (!p) return nullptr;
    if constexpr (std::is_base_of_v<T, U>) return p;
    else {
        T* r = dynamic_cast<T*>(p);
        if (!r) ThrowInvalidCast();
        return r;
    }
}
template <class T, class U>
inline T* as(U* p) {
    if constexpr (std::is_base_of_v<T, U>) return p;
    else return p ? dynamic_cast<T*>(p) : nullptr;
}
template <class T, class U>
inline bool is(U* p) { return as<T>(p) != nullptr; }

template <class T>
inline T* nn(T* p) { if (!p) ThrowNullReference(); return p; }

// ---------------------------------------------------------------- String
struct String final : Object {
    int32_t length = 0;
    char16_t* chars = nullptr;  // points into this allocation (atomic, never scanned)

    static void* operator new(size_t, void* p) { return p; }
    static String* Alloc(int32_t len);
    static String* FromUtf16(const char16_t* s, int32_t len);
    static String* FromUtf8(const char* s);
    static String* FromUtf8(const char* s, size_t len);
    static String* Literal(const char16_t* s, int32_t len);
    char* ToUtf8() const;  // GC-allocated, NUL terminated

    static String* Empty;

    int32_t get_Length() const { return length; }
    char16_t get_Chars(int32_t i) const { if ((uint32_t)i >= (uint32_t)length) ThrowIndexOutOfRange(); return chars[i]; }

    String* ToString() override { return this; }
    bool Equals(Object* o) override;
    int32_t GetHashCode() override;
    Type* GetType() override;

    bool Equals(String* o);
    bool Equals(String* o, StringComparison comparison);
    static bool Equals(String* a, String* b, StringComparison comparison);
    static bool op_Equality(String* a, String* b);
    static bool op_Inequality(String* a, String* b) { return !op_Equality(a, b); }
    static bool IsNullOrEmpty(String* s) { return !s || s->length == 0; }
    static bool IsNullOrWhiteSpace(String* s);
    static int32_t CompareOrdinal(String* a, String* b);
    static int32_t Compare(String* a, String* b);
    int32_t CompareTo(String* b) { return Compare(this, b); }

    bool Contains(char16_t c);
    bool Contains(String* s);
    bool StartsWith(String* s);
    bool StartsWith(String* s, StringComparison comparison);
    bool EndsWith(String* s);
    bool EndsWith(String* s, StringComparison comparison);
    int32_t IndexOf(char16_t c);
    int32_t IndexOf(char16_t c, int32_t start);
    int32_t IndexOf(String* s);
    int32_t IndexOf(String* s, StringComparison comparison);
    int32_t IndexOf(String* s, int32_t start, StringComparison comparison);
    int32_t LastIndexOf(char16_t c);
    String* Insert(int32_t start, String* v);
    String* Remove(int32_t start);
    String* Remove(int32_t start, int32_t count);
    String* Replace(char16_t a, char16_t b);
    String* Replace(String* a, String* b);
    String* Replace(String* a, String* b, StringComparison comparison);
    String* Substring(int32_t start);
    String* Substring(int32_t start, int32_t len);
    String* ToLower();
    String* ToUpper();
    String* ToLowerInvariant() { return ToLower(); }
    String* ToUpperInvariant() { return ToUpper(); }
    String* Trim();
    String* TrimEnd();
    String* TrimStart();
    String* TrimStart(char16_t c);
    String* TrimStart(Array1<char16_t>* chars);
    String* TrimEnd(Array1<char16_t>* chars);
    String* Trim(Array1<char16_t>* chars);
    String* Trim(char16_t c);
    String* TrimEnd(char16_t c);
    String* PadLeft(int32_t n);
    String* PadLeft(int32_t n, char16_t c);
    String* PadRight(int32_t n);
    String* PadRight(int32_t n, char16_t c);
    Array1<char16_t>* ToCharArray();
    Array1<String*>* Split(char16_t sep, StringSplitOptions options);
    Array1<String*>* Split(char16_t sep, int32_t count, StringSplitOptions options);
    Array1<String*>* Split(Array1<char16_t>* seps, int32_t count);
    Array1<String*>* Split(Array1<char16_t>* seps, int32_t count, StringSplitOptions options);
    Array1<String*>* Split(Array1<char16_t>* seps);
    Array1<String*>* Split(String* sep, StringSplitOptions options);
    Array1<String*>* Split(Array1<String*>* seps, StringSplitOptions options);

    static String* Concat(String* a, String* b);
    static String* Concat(String* a, String* b, String* c);
    static String* Concat(String* a, String* b, String* c, String* d);
    static String* ConcatN(std::initializer_list<String*> parts);
    static String* Join(String* sep, Array1<String*>* values);
    static String* Join(String* sep, Object* values);  // IEnumerable<string>
    static String* FromChars(Array1<char16_t>* chars);
    static String* Format(String* fmt, Array1<Object*>* args);
    static String* Format(String* fmt, Object* a);
    static String* Format(String* fmt, Object* a, Object* b);
    static String* Format(String* fmt, Object* a, Object* b, Object* c);
    static String* FromChar(char16_t c, int32_t count);
};

inline bool str_eq(String* a, String* b) { return String::op_Equality(a, b); }

// Cached literal: one String per call site.
#define STR(lit) ([]() -> ::System::String* { static ::System::String* s_ = ::System::String::Literal(lit, (int32_t)(sizeof(lit) / sizeof(char16_t) - 1)); return s_; }())

// ---------------------------------------------------------------- Type
struct Type final : Object {
    String* name = nullptr;
    String* fullName = nullptr;
    Type* baseType = nullptr;
    bool isValueType = false;
    bool isEnum = false;
    bool isInterface = false;
    int32_t primitive = 0;  // TypeCode-like for primitives/enums
    void (*reflect)(Type*) = nullptr;
    bool reflected = false;
    struct Array* ctors = nullptr;
    struct Array* fields = nullptr;
    struct Array* enumValues = nullptr;  // boxed
    struct Array* enumNames = nullptr;
    Type* underlying = nullptr;
    bool (*isInstance)(Object*) = nullptr;

    static Type* Create(const char16_t* name, const char16_t* fullName, Type* baseType);
    String* get_Name() { return name; }
    String* get_FullName() { return fullName; }
    Type* get_BaseType() { return baseType; }
    bool get_IsValueType() { return isValueType; }
    bool get_IsEnum() { return isEnum; }
    bool IsSubclassOf(Type* t);
    bool IsAssignableFrom(Type* t);
    bool IsInstanceOfType(Object* o);
    String* ToString() override { return fullName; }
    Type* GetType() override;
    Array1<Reflection::ConstructorInfo*>* GetConstructors();
    Reflection::FieldInfo* GetField(String* name);
    Reflection::FieldInfo* GetField(String* name, Reflection::BindingFlags flags);
    Array1<Reflection::FieldInfo*>* GetFields(Reflection::BindingFlags flags);
    Array1<Reflection::FieldInfo*>* GetFields();
    void ensureReflected();
    static bool op_Equality(Type* a, Type* b) { return a == b; }
    static bool op_Inequality(Type* a, Type* b) { return a != b; }
};

template <class T, class = void>
struct TypeOfImpl {
    static Type* get() {
        if constexpr (requires { T::__type(); }) return T::__type();
        else return Object::__type();
    }
};
Type* PrimitiveType(const char16_t* name, int32_t code);
#define RT_PRIM_TYPE(T, N, C) \
    template <> struct TypeOfImpl<T> { static Type* get() { static Type* t = PrimitiveType(N, C); return t; } };
RT_PRIM_TYPE(bool, u"Boolean", 3)
RT_PRIM_TYPE(char16_t, u"Char", 4)
RT_PRIM_TYPE(int8_t, u"SByte", 5)
RT_PRIM_TYPE(uint8_t, u"Byte", 6)
RT_PRIM_TYPE(int16_t, u"Int16", 7)
RT_PRIM_TYPE(uint16_t, u"UInt16", 8)
RT_PRIM_TYPE(int32_t, u"Int32", 9)
RT_PRIM_TYPE(uint32_t, u"UInt32", 10)
RT_PRIM_TYPE(int64_t, u"Int64", 11)
RT_PRIM_TYPE(uint64_t, u"UInt64", 12)
RT_PRIM_TYPE(float, u"Single", 13)
RT_PRIM_TYPE(double, u"Double", 14)
#undef RT_PRIM_TYPE
template <> struct TypeOfImpl<String> { static Type* get(); };
template <class T>
struct TypeOfImpl<T, std::enable_if_t<std::is_enum_v<T>>> { static Type* get() { return __EnumType((T*)nullptr); } };
Type* EnumTypeCreate(const char16_t* name, const char16_t* fullName, Object* (*boxer)(int64_t));
template <class E>
struct EnumBoxerT { static Object* box(int64_t v); };
template <class E>
inline auto EnumBoxer() { return &EnumBoxerT<E>::box; }
template <> struct TypeOfImpl<Object> { static Type* get() { return Object::__type(); } };
template <class T>
inline Type* TypeOf() { return TypeOfImpl<T>::get(); }

// ---------------------------------------------------------------- ToString helpers
String* ToStr(bool v);
String* ToStr(char16_t v);
String* ToStr(int32_t v);
String* ToStr(uint32_t v);
String* ToStr(int64_t v);
String* ToStr(uint64_t v);
inline String* ToStr(int16_t v) { return ToStr((int32_t)v); }
inline String* ToStr(uint16_t v) { return ToStr((int32_t)v); }
inline String* ToStr(int8_t v) { return ToStr((int32_t)v); }
inline String* ToStr(uint8_t v) { return ToStr((int32_t)v); }
String* ToStr(float v);
String* ToStr(double v);
inline String* ToStr(String* s) { return s; }
template <class T>
inline String* ToStr(T* p) {
    Object* o = AsObject(p);
    return o ? o->ToString() : nullptr;
}
// Enums get a generated overload of __EnumName in their namespace, found by ADL.
template <class T>
    requires std::is_enum_v<T>
inline String* ToStr(T v) { return __EnumToString(v); }
template <class T>
    requires(std::is_class_v<T> && requires(T& t) { t.ToString(); })
inline String* ToStr(const T& v) { return const_cast<T&>(v).ToString(); }
// For string concatenation: null becomes "".
template <class T>
inline String* ToStrC(const T& v) {
    String* s = ToStr(v);
    return s ? s : String::Empty;
}
String* Format(double v, const char16_t* fmt, int32_t fmtLen);
String* FormatInt(int64_t v, const char16_t* fmt, int32_t fmtLen);
template <class T>
inline String* Fmt(T v, String* fmt) {
    if constexpr (std::is_floating_point_v<T>) return Format((double)v, fmt->chars, fmt->length);
    else if constexpr (std::is_integral_v<T>) return FormatInt((int64_t)v, fmt->chars, fmt->length);
    else if constexpr (std::is_enum_v<T>) return FormatInt((int64_t)v, fmt->chars, fmt->length);
    else return ToStr(v);
}
String* PadAlign(String* s, int32_t align);

// ---------------------------------------------------------------- Boxing
struct BoxBase : Object {
    virtual bool __tryGetI64(int64_t&) { return false; }
    virtual bool __tryGetF64(double&) { return false; }
};
template <class T>
struct Boxed final : BoxBase {
    T value;
    String* ToString() override {
        if constexpr (requires { ToStr(value); }) return ToStr(value);
        else return String::Empty;
    }
    bool Equals(Object* o) override;
    int32_t GetHashCode() override;
    Type* GetType() override { return TypeOf<T>(); }
    bool __tryGetI64(int64_t& r) override {
        if constexpr (std::is_integral_v<T> || std::is_enum_v<T>) { r = (int64_t)value; return true; }
        else return false;
    }
    bool __tryGetF64(double& r) override {
        if constexpr (std::is_arithmetic_v<T>) { r = (double)value; return true; }
        else return false;
    }
};
// Boxes of common small ints are shared (boxes are immutable).
Object* BoxInt32(int32_t v);
template <class T>
inline Object* box(const T& v) {
    if constexpr (std::is_pointer_v<T>) return AsObject(v);
    else if constexpr (std::is_same_v<T, int32_t>) return BoxInt32(v);
    else {
        auto* b = new Boxed<T>();
        b->value = v;
        return b;
    }
}
template <class T>
inline T unbox(Object* o) {
    if (!o) ThrowNullReference();
    if (auto* b = dynamic_cast<Boxed<T>*>(o)) return b->value;
    if constexpr (std::is_integral_v<T> || std::is_enum_v<T>) {
        int64_t v;
        if (auto* bb = dynamic_cast<BoxBase*>(o); bb && bb->__tryGetI64(v)) return (T)v;
    }
    ThrowInvalidCast();
}
template <class T>
inline bool isBoxed(Object* o) { return o && dynamic_cast<Boxed<T>*>(o) != nullptr; }

template <class E>
Object* EnumBoxerT<E>::box(int64_t v) { return ::System::box((E)v); }

// ---------------------------------------------------------------- Nullable
template <class T>
struct Nullable {
    T value{};
    bool hasValue = false;
    Nullable() = default;
    Nullable(std::nullptr_t) {}
    Nullable(const T& v) : value(v), hasValue(true) {}
    bool get_HasValue() const { return hasValue; }
    const T& get_Value() const { if (!hasValue) ThrowInvalidOperation("Nullable object must have a value."); return value; }
    T GetValueOrDefault() const { return hasValue ? value : T{}; }
    T GetValueOrDefault(const T& d) const { return hasValue ? value : d; }
    bool operator==(const Nullable& o) const { return hasValue == o.hasValue && (!hasValue || value == o.value); }
    bool operator!=(const Nullable& o) const { return !(*this == o); }
    String* ToString() { return hasValue ? ToStr(value) : String::Empty; }
    int32_t GetHashCode();
};
template <class T>
inline Object* box(const Nullable<T>& v) { return v.hasValue ? box(v.value) : nullptr; }

// ---------------------------------------------------------------- Equality / hashing traits
int32_t HashString(String* s);
template <class T, class = void>
struct Eq {
    static bool eq(const T& a, const T& b) {
        if constexpr (std::is_arithmetic_v<T> || std::is_enum_v<T>) return a == b;
        else if constexpr (requires(T& x) { x.Equals(b); }) return const_cast<T&>(a).Equals(b);
        else return std::memcmp(&a, &b, sizeof(T)) == 0;
    }
    static int32_t hash(const T& a) {
        if constexpr (std::is_integral_v<T> || std::is_enum_v<T>) return (int32_t)((int64_t)a ^ ((int64_t)a >> 32));
        else if constexpr (std::is_same_v<T, float>) { float f = a == 0.0f ? 0.0f : a; int32_t i; std::memcpy(&i, &f, 4); return i; }
        else if constexpr (std::is_same_v<T, double>) { double d = a == 0.0 ? 0.0 : a; int64_t i; std::memcpy(&i, &d, 8); return (int32_t)(i ^ (i >> 32)); }
        else if constexpr (requires(T& x) { x.GetHashCode(); }) return const_cast<T&>(a).GetHashCode();
        else return 0;
    }
};
template <class T>
struct Eq<T*, void> {
    static bool eq(T* a, T* b) {
        if (a == b) return true;
        if (!a || !b) return false;
        if constexpr (std::is_same_v<T, String>) return String::op_Equality(a, b);
        else return AsObject(a)->Equals(AsObject(b));
    }
    static int32_t hash(T* a) {
        if (!a) return 0;
        if constexpr (std::is_same_v<T, String>) return HashString(a);
        else return AsObject(a)->GetHashCode();
    }
};
template <class T>
bool Boxed<T>::Equals(Object* o) {
    auto* b = dynamic_cast<Boxed<T>*>(o);
    return b && Eq<T>::eq(value, b->value);
}
template <class T>
int32_t Boxed<T>::GetHashCode() { return Eq<T>::hash(value); }
template <class T>
int32_t Nullable<T>::GetHashCode() { return hasValue ? Eq<T>::hash(value) : 0; }

// Comparison for sorting (Comparer<T>.Default).
template <class T, class = void>
struct Cmp {
    static int32_t cmp(const T& a, const T& b) {
        if constexpr (std::is_arithmetic_v<T> || std::is_enum_v<T>) return a < b ? -1 : (b < a ? 1 : 0);
        else if constexpr (requires(T& x) { x.CompareTo(b); }) return const_cast<T&>(a).CompareTo(b);
        else return 0;
    }
};
template <class T>
struct Cmp<T*, void> {
    static int32_t cmp(T* a, T* b) {
        if (a == b) return 0;
        if (!a) return -1;
        if (!b) return 1;
        if constexpr (std::is_same_v<T, String>) return String::Compare(a, b);
        else return a->CompareTo(b);
    }
};

// ---------------------------------------------------------------- Arrays
struct Array : Object {
    int32_t length = 0;
    int32_t get_Length() const { return length; }
    virtual void* __data() = 0;
    virtual int32_t __elemSize() const = 0;
    virtual int32_t GetLength(int32_t dim) { return length; }
    virtual Object* __getBoxed(int32_t i) = 0;
    virtual Array* Clone() = 0;
    static void Clear(Array* a, int32_t index, int32_t len);
    static void Copy(Array* src, int32_t si, Array* dst, int32_t di, int32_t len);
    static void Copy(Array* src, Array* dst, int32_t len) { Copy(src, 0, dst, 0, len); }
    template <class T>
    static int32_t IndexOf(struct Array1<T>* a, T v);
    template <class T>
    static void Resize(struct Array1<T>*& a, int32_t n);
    template <class T>
    static void Fill(struct Array1<T>* a, T v);
    template <class T>
    static void Sort(struct Array1<T>* a);
    template <class T>
    static void Reverse(struct Array1<T>* a);
};

template <class T>
constexpr bool is_atomic_v = std::is_arithmetic_v<T> || std::is_enum_v<T>;

template <class T>
struct Array1 final : Array {
    T* data = nullptr;

    static Array1* New(int32_t n) {
        if (n < 0) ThrowArgument("negative array size");
        auto* a = new Array1();
        a->length = n;
        if (n) a->data = (T*)(is_atomic_v<T> ? gc_alloc_atomic(sizeof(T) * n) : gc_alloc(sizeof(T) * n));
        return a;
    }
    static Array1* Of(std::initializer_list<T> il) {
        auto* a = New((int32_t)il.size());
        int32_t i = 0;
        for (auto& v : il) a->data[i++] = v;
        return a;
    }
    T& at(int32_t i) {
        if ((uint32_t)i >= (uint32_t)length) ThrowIndexOutOfRange();
        return data[i];
    }
    T& operator[](int32_t i) { return at(i); }
    T* begin() { return data; }
    T* end() { return data + length; }
    void* __data() override { return data; }
    int32_t __elemSize() const override { return (int32_t)sizeof(T); }
    Object* __getBoxed(int32_t i) override { return box(at(i)); }
    Array1* Clone() override {
        auto* a = New(length);
        for (int32_t i = 0; i < length; i++) a->data[i] = data[i];
        return a;
    }
};

template <class T>
struct Array2 final : Array {
    T* data = nullptr;
    int32_t d0 = 0, d1 = 0;
    static Array2* New(int32_t a, int32_t b) {
        auto* r = new Array2();
        r->d0 = a;
        r->d1 = b;
        r->length = a * b;
        if (r->length) r->data = (T*)(is_atomic_v<T> ? gc_alloc_atomic(sizeof(T) * r->length) : gc_alloc(sizeof(T) * r->length));
        return r;
    }
    T& at(int32_t i, int32_t j) {
        if ((uint32_t)i >= (uint32_t)d0 || (uint32_t)j >= (uint32_t)d1) ThrowIndexOutOfRange();
        return data[i * d1 + j];
    }
    int32_t GetLength(int32_t dim) override { return dim == 0 ? d0 : d1; }
    T* begin() { return data; }
    T* end() { return data + length; }
    void* __data() override { return data; }
    int32_t __elemSize() const override { return (int32_t)sizeof(T); }
    Object* __getBoxed(int32_t i) override { return box(data[i]); }
    Array2* Clone() override {
        auto* a = New(d0, d1);
        for (int32_t i = 0; i < length; i++) a->data[i] = data[i];
        return a;
    }
};

template <class T>
struct Array3 final : Array {
    T* data = nullptr;
    int32_t d0 = 0, d1 = 0, d2 = 0;
    static Array3* New(int32_t a, int32_t b, int32_t c) {
        auto* r = new Array3();
        r->d0 = a;
        r->d1 = b;
        r->d2 = c;
        r->length = a * b * c;
        if (r->length) r->data = (T*)(is_atomic_v<T> ? gc_alloc_atomic(sizeof(T) * r->length) : gc_alloc(sizeof(T) * r->length));
        return r;
    }
    T& at(int32_t i, int32_t j, int32_t k) {
        if ((uint32_t)i >= (uint32_t)d0 || (uint32_t)j >= (uint32_t)d1 || (uint32_t)k >= (uint32_t)d2) ThrowIndexOutOfRange();
        return data[(i * d1 + j) * d2 + k];
    }
    int32_t GetLength(int32_t dim) override { return dim == 0 ? d0 : (dim == 1 ? d1 : d2); }
    T* begin() { return data; }
    T* end() { return data + length; }
    void* __data() override { return data; }
    int32_t __elemSize() const override { return (int32_t)sizeof(T); }
    Object* __getBoxed(int32_t i) override { return box(data[i]); }
    Array3* Clone() override {
        auto* a = New(d0, d1, d2);
        for (int32_t i = 0; i < length; i++) a->data[i] = data[i];
        return a;
    }
};

template <class T>
int32_t Array::IndexOf(Array1<T>* a, T v) {
    for (int32_t i = 0; i < a->length; i++)
        if (Eq<T>::eq(a->data[i], v)) return i;
    return -1;
}
template <class T>
void Array::Resize(Array1<T>*& a, int32_t n) {
    auto* r = Array1<T>::New(n);
    if (a)
        for (int32_t i = 0; i < n && i < a->length; i++) r->data[i] = a->data[i];
    a = r;
}
template <class T>
void Array::Fill(Array1<T>* a, T v) {
    for (int32_t i = 0; i < a->length; i++) a->data[i] = v;
}
template <class T>
void Array::Reverse(Array1<T>* a) {
    for (int32_t i = 0, j = a->length - 1; i < j; i++, j--) std::swap(a->data[i], a->data[j]);
}

// ---------------------------------------------------------------- Delegates
template <class Sig>
struct Fn;
template <class R, class... A>
struct Fn<R(A...)> final : Object {
    struct Target : Object {
        void* key1 = nullptr;
        const char* key2 = nullptr;
        virtual R call(A... a) = 0;
    };
    template <class L>
    struct Impl final : Target {
        L fn;
        explicit Impl(L&& l) : fn(std::move(l)) {}
        R call(A... a) override { return fn(a...); }
    };
    Target* single = nullptr;
    Array1<Target*>* multi = nullptr;

    template <class L>
    static Fn* Make(L l) {
        auto* f = new Fn();
        f->single = new Impl<L>(std::move(l));
        return f;
    }
    // Method-group conversion: equality is (target, method name).
    template <class L>
    static Fn* Bind(void* target, const char* method, L l) {
        auto* f = Make(std::move(l));
        f->single->key1 = target;
        f->single->key2 = method;
        return f;
    }
    R Invoke(A... a) {
        if (single) return single->call(a...);
        if constexpr (std::is_void_v<R>) {
            for (int32_t i = 0; i < multi->length; i++) multi->data[i]->call(a...);
        } else {
            R r{};
            for (int32_t i = 0; i < multi->length; i++) r = multi->data[i]->call(a...);
            return r;
        }
    }
    R operator()(A... a) { return Invoke(a...); }
    int32_t count() const { return single ? 1 : (multi ? multi->length : 0); }
    Target* get(int32_t i) const { return single ? single : multi->data[i]; }
};

struct Delegate {
    template <class F>
    static F* Combine(F* a, F* b) {
        if (!a) return b;
        if (!b) return a;
        auto* f = new F();
        int32_t n = a->count() + b->count();
        f->multi = Array1<typename F::Target*>::New(n);
        int32_t k = 0;
        for (int32_t i = 0; i < a->count(); i++) f->multi->data[k++] = a->get(i);
        for (int32_t i = 0; i < b->count(); i++) f->multi->data[k++] = b->get(i);
        return f;
    }
    template <class F>
    static bool Same(typename F::Target* x, typename F::Target* y) {
        return x == y || (x->key2 && y->key2 && x->key1 == y->key1 && std::strcmp(x->key2, y->key2) == 0);
    }
    template <class F>
    static F* Remove(F* a, F* b) {
        if (!a || !b) return a;
        if (b->count() != 1) return a;
        auto* t = b->get(0);
        for (int32_t i = a->count() - 1; i >= 0; i--) {
            if (!Same<F>(a->get(i), t)) continue;
            if (a->count() == 1) return nullptr;
            auto* f = new F();
            if (a->count() == 2) {
                f->single = a->get(1 - i);
                return f;
            }
            f->multi = Array1<typename F::Target*>::New(a->count() - 1);
            int32_t k = 0;
            for (int32_t j = 0; j < a->count(); j++)
                if (j != i) f->multi->data[k++] = a->get(j);
            return f;
        }
        return a;
    }
};

// ---------------------------------------------------------------- ValueTuple
template <class... T>
struct ValueTuple;
template <class A, class B>
struct ValueTuple<A, B> {
    A Item1{};
    B Item2{};
    bool operator==(const ValueTuple& o) const { return Eq<A>::eq(Item1, o.Item1) && Eq<B>::eq(Item2, o.Item2); }
    bool operator!=(const ValueTuple& o) const { return !(*this == o); }
    bool Equals(const ValueTuple& o) const { return *this == o; }
    int32_t GetHashCode() const { return Eq<A>::hash(Item1) * 31 + Eq<B>::hash(Item2); }
    String* ToString() { return String::ConcatN({STR(u"("), ToStrC(Item1), STR(u", "), ToStrC(Item2), STR(u")")}); }
};
template <class A, class B, class C>
struct ValueTuple<A, B, C> {
    A Item1{};
    B Item2{};
    C Item3{};
    bool operator==(const ValueTuple& o) const { return Eq<A>::eq(Item1, o.Item1) && Eq<B>::eq(Item2, o.Item2) && Eq<C>::eq(Item3, o.Item3); }
    bool operator!=(const ValueTuple& o) const { return !(*this == o); }
    bool Equals(const ValueTuple& o) const { return *this == o; }
    int32_t GetHashCode() const { return (Eq<A>::hash(Item1) * 31 + Eq<B>::hash(Item2)) * 31 + Eq<C>::hash(Item3); }
};
template <class A, class B, class C, class D>
struct ValueTuple<A, B, C, D> {
    A Item1{};
    B Item2{};
    C Item3{};
    D Item4{};
    bool operator==(const ValueTuple& o) const {
        return Eq<A>::eq(Item1, o.Item1) && Eq<B>::eq(Item2, o.Item2) && Eq<C>::eq(Item3, o.Item3) && Eq<D>::eq(Item4, o.Item4);
    }
    bool operator!=(const ValueTuple& o) const { return !(*this == o); }
    bool Equals(const ValueTuple& o) const { return *this == o; }
    int32_t GetHashCode() const { return ((Eq<A>::hash(Item1) * 31 + Eq<B>::hash(Item2)) * 31 + Eq<C>::hash(Item3)) * 31 + Eq<D>::hash(Item4); }
};

// ---------------------------------------------------------------- Exceptions
void TraceException(struct Exception* e);
struct Exception : Object {
    String* message = nullptr;
    Exception* inner = nullptr;
    // Return addresses captured when the exception was created (logged if it is never caught).
    void* trace[20] = {};
    int32_t traceDepth = 0;
    void __init() { TraceException(this); }
    void __init(String* m) { message = m; TraceException(this); }
    void __init(String* m, Exception* in) { message = m; inner = in; TraceException(this); }
    void __init(String* m, String* param) { message = m; TraceException(this); }
    virtual String* get_Message() { return message ? message : STR(u"Exception"); }
    Exception* get_InnerException() { return inner; }
    String* ToString() override { return get_Message(); }
    static Type* __type();
    Type* GetType() override { return __type(); }
};
#define RT_EXCEPTION(N, B)                                         \
    struct N : B {                                                 \
        static Type* __type();                                     \
        Type* GetType() override { return __type(); }              \
    };
RT_EXCEPTION(SystemException, Exception)
RT_EXCEPTION(ArgumentException, SystemException)
RT_EXCEPTION(ArgumentNullException, ArgumentException)
RT_EXCEPTION(ArgumentOutOfRangeException, ArgumentException)
RT_EXCEPTION(InvalidOperationException, SystemException)
RT_EXCEPTION(NullReferenceException, SystemException)
RT_EXCEPTION(IndexOutOfRangeException, SystemException)
RT_EXCEPTION(InvalidCastException, SystemException)
RT_EXCEPTION(NotImplementedException, SystemException)
RT_EXCEPTION(NotSupportedException, SystemException)
RT_EXCEPTION(PlatformNotSupportedException, NotSupportedException)
RT_EXCEPTION(FormatException, SystemException)
RT_EXCEPTION(OverflowException, SystemException)
RT_EXCEPTION(OperationCanceledException, SystemException)
RT_EXCEPTION(MissingMemberException, SystemException)
RT_EXCEPTION(KeyNotFoundException, SystemException)
RT_EXCEPTION(IOException, SystemException)
RT_EXCEPTION(FileNotFoundException, IOException)
RT_EXCEPTION(DirectoryNotFoundException, IOException)
RT_EXCEPTION(InvalidDataException, SystemException)
RT_EXCEPTION(UnauthorizedAccessException, SystemException)
RT_EXCEPTION(SwitchExpressionException, InvalidOperationException)

// Throw with the static type E* so `catch (Base*)` handlers match by pointer conversion.
template <class E>
[[noreturn]] inline void Throw(E* e) { throw e; }
template <class E, class... A>
inline E* NewEx(A... a) {
    auto* e = new E();
    e->__init(a...);
    return e;
}

// ---------------------------------------------------------------- misc helpers
template <class T, class... A>
inline T* New(A&&... a) {
    T* p = new T();
    p->__init(std::forward<A>(a)...);
    return p;
}

// Scope guard for finally / using.
template <class F>
struct Finally {
    F f;
    explicit Finally(F&& fn) : f(std::move(fn)) {}
    ~Finally() noexcept(false) { f(); }
};
template <class F>
Finally(F) -> Finally<F>;

// Shared cell for lambda-captured variables that are mutated.
template <class T>
struct Ref final : Object {
    T v{};
};
template <class T>
inline Ref<T>* MakeRef(T v) { auto* r = new Ref<T>(); r->v = v; return r; }
template <class T>
inline Ref<T>* MakeRef() { return new Ref<T>(); }

[[noreturn]] void ThrowSwitch();
template <class T, class E>
[[noreturn]] inline T ThrowExpr(E* e) { throw e; }

template <class T>
inline Array2<T>* MakeArray2(int32_t rows, int32_t cols, std::initializer_list<T> il) {
    auto* a = Array2<T>::New(rows, cols);
    int32_t i = 0;
    for (auto& v : il) a->data[i++] = v;
    return a;
}

// Lifted comparison/arithmetic operators for Nullable<T>.
template <class T> inline bool operator<(const Nullable<T>& a, const Nullable<T>& b) { return a.hasValue && b.hasValue && a.value < b.value; }
template <class T> inline bool operator>(const Nullable<T>& a, const Nullable<T>& b) { return a.hasValue && b.hasValue && a.value > b.value; }
template <class T> inline bool operator<=(const Nullable<T>& a, const Nullable<T>& b) { return a.hasValue && b.hasValue && a.value <= b.value; }
template <class T> inline bool operator>=(const Nullable<T>& a, const Nullable<T>& b) { return a.hasValue && b.hasValue && a.value >= b.value; }
template <class T> inline Nullable<T> operator+(const Nullable<T>& a, const Nullable<T>& b) { return a.hasValue && b.hasValue ? Nullable<T>(a.value + b.value) : Nullable<T>(); }
template <class T> inline Nullable<T> operator-(const Nullable<T>& a, const Nullable<T>& b) { return a.hasValue && b.hasValue ? Nullable<T>(a.value - b.value) : Nullable<T>(); }
template <class T> inline Nullable<T> operator*(const Nullable<T>& a, const Nullable<T>& b) { return a.hasValue && b.hasValue ? Nullable<T>(a.value * b.value) : Nullable<T>(); }
template <class T> inline Nullable<T> operator/(const Nullable<T>& a, const Nullable<T>& b) { return a.hasValue && b.hasValue ? Nullable<T>(a.value / b.value) : Nullable<T>(); }

struct EventArgs : Object {
    static EventArgs* Empty;
    static Type* __type();
};

struct IDisposable {
    virtual ~IDisposable() = default;
    virtual void Dispose() = 0;
};
struct IComparable {
    virtual ~IComparable() = default;
    virtual int32_t CompareTo(Object* other) = 0;
};
template <class T>
struct IComparableT {
    virtual ~IComparableT() = default;
    virtual int32_t CompareTo(T other) = 0;
};
template <class T>
struct IEquatable {
    virtual ~IEquatable() = default;
    virtual bool Equals(T other) = 0;
};
struct IFormatProvider {
    virtual ~IFormatProvider() = default;
};

}  // namespace System

// Static reference roots that must stay alive (static storage is scanned by the GC).
namespace rt {
void Init();
// Serves read-only files below `prefix` from a pack built by tools/make_pak.py.
bool MountPack(const char* packFile, const char* prefix);
void Collect();
void CollectGeneration(int32_t generation);
bool BeginNoGC(int64_t bytes);
void ConfigureCollector(size_t heapLimit, bool enforceLimit, size_t collectAbove);
int64_t AllocatedBytes();
// Saves are written by a background thread (temp file + rename); other disk accesses wait for it.
void QueueFileWrite(System::String* path, System::String* text);
void WaitForFileWrites();
// Logs type, message, creation stack and inner exceptions.
void LogException(const char* what, System::Exception* e);
}
