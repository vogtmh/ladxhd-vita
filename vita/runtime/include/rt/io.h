#pragma once
#include "misc.h"

namespace System::IO {

enum class FileMode : int32_t { CreateNew = 1, Create = 2, Open = 3, OpenOrCreate = 4, Truncate = 5, Append = 6 };
enum class FileAccess : int32_t { Read = 1, Write = 2, ReadWrite = 3 };
enum class FileShare : int32_t { None = 0, Read = 1, Write = 2, ReadWrite = 3, Delete = 4 };
enum class SeekOrigin : int32_t { Begin = 0, Current = 1, End = 2 };
inline String* __EnumToString(FileMode v) { return ToStr((int32_t)v); }
inline String* __EnumToString(FileAccess v) { return ToStr((int32_t)v); }
inline String* __EnumToString(FileShare v) { return ToStr((int32_t)v); }
inline String* __EnumToString(SeekOrigin v) { return ToStr((int32_t)v); }

struct Stream : Object, IDisposable {
    virtual int32_t Read(Array1<uint8_t>* buf, int32_t off, int32_t n) = 0;
    virtual void Write(Array1<uint8_t>* buf, int32_t off, int32_t n) { ThrowInvalidOperation("stream not writable"); }
    virtual int32_t ReadByte() {
        uint8_t b;
        return readRaw(&b, 1) == 1 ? b : -1;
    }
    virtual void WriteByte(uint8_t b) { writeRaw(&b, 1); }
    virtual int64_t Seek(int64_t off, SeekOrigin origin) = 0;
    virtual int64_t get_Length() = 0;
    virtual int64_t get_Position() = 0;
    virtual void set_Position(int64_t p) { Seek(p, SeekOrigin::Begin); }
    virtual bool get_CanRead() { return true; }
    virtual bool get_CanWrite() { return false; }
    virtual bool get_CanSeek() { return true; }
    virtual void Flush() {}
    virtual void Close() {}
    void Dispose() override { Close(); }
    virtual int32_t readRaw(uint8_t* dst, int32_t n) = 0;
    virtual void writeRaw(const uint8_t* src, int32_t n) { ThrowInvalidOperation("stream not writable"); }
    void CopyTo(Stream* dst);
    Array1<uint8_t>* ReadAllRemaining();
};

struct MemoryStream : Stream {
    Array1<uint8_t>* buf = nullptr;
    int32_t len = 0, pos = 0;
    bool writable = true;
    void __init() { buf = Array1<uint8_t>::New(256); }
    void __init(int32_t cap) { buf = Array1<uint8_t>::New(cap > 0 ? cap : 16); }
    void __init(Array1<uint8_t>* b) { buf = b; len = b->length; writable = false; }
    void __init(Array1<uint8_t>* b, bool w) { buf = b; len = b->length; writable = w; }
    int32_t Read(Array1<uint8_t>* dst, int32_t off, int32_t n) override { return readRaw(dst->data + off, n); }
    void Write(Array1<uint8_t>* src, int32_t off, int32_t n) override { writeRaw(src->data + off, n); }
    int32_t readRaw(uint8_t* dst, int32_t n) override;
    void writeRaw(const uint8_t* src, int32_t n) override;
    int64_t Seek(int64_t off, SeekOrigin origin) override;
    int64_t get_Length() override { return len; }
    int64_t get_Position() override { return pos; }
    bool get_CanWrite() override { return writable; }
    Array1<uint8_t>* ToArray();
    Array1<uint8_t>* GetBuffer() { return buf; }
};

struct FileStream : Stream {
    void* fp = nullptr;
    bool canWrite = false;
    String* path = nullptr;
    int64_t winStart = -1, winLen = 0, winPos = 0;  // window into the mounted pack file
    uint8_t* winMem = nullptr;                      // inflated contents of a compressed pack entry
    void __init(String* path, FileMode mode);
    void __init(String* path, FileMode mode, FileAccess access);
    void __init(String* path, FileMode mode, FileAccess access, FileShare share);
    int32_t Read(Array1<uint8_t>* dst, int32_t off, int32_t n) override { return readRaw(dst->data + off, n); }
    void Write(Array1<uint8_t>* src, int32_t off, int32_t n) override { writeRaw(src->data + off, n); }
    int32_t readRaw(uint8_t* dst, int32_t n) override;
    void writeRaw(const uint8_t* src, int32_t n) override;
    int64_t Seek(int64_t off, SeekOrigin origin) override;
    int64_t get_Length() override;
    int64_t get_Position() override;
    bool get_CanWrite() override { return canWrite; }
    void Flush() override;
    void Close() override;
};

struct TextReader : Object, IDisposable {
    virtual String* ReadLine() = 0;
    virtual String* ReadToEnd() = 0;
    virtual int32_t Read() = 0;
    virtual int32_t Peek() = 0;
    virtual void Close() {}
    void Dispose() override { Close(); }
};
struct StreamReader : TextReader {
    Stream* s = nullptr;
    String* text = nullptr;  // decoded eagerly
    int32_t pos = 0;
    void __init(Stream* stream);
    void __init(Stream* stream, Text::Encoding* enc) { __init(stream); }
    void __init(Stream* stream, Text::Encoding* enc, bool detect) { __init(stream); }
    void __init(Stream* stream, bool detect) { __init(stream); }
    void __init(String* path);
    void __init(String* path, Text::Encoding* enc) { __init(path); }
    String* ReadLine() override;
    String* ReadToEnd() override;
    int32_t Read() override { return pos < text->length ? text->chars[pos++] : -1; }
    int32_t Peek() override { return pos < text->length ? text->chars[pos] : -1; }
    bool get_EndOfStream() { return pos >= text->length; }
    Stream* get_BaseStream() { return s; }
    void Close() override;
};
struct StringReader : TextReader {
    String* text = nullptr;
    int32_t pos = 0;
    void __init(String* s) { text = s; }
    String* ReadLine() override;
    String* ReadToEnd() override;
    int32_t Read() override { return pos < text->length ? text->chars[pos++] : -1; }
    int32_t Peek() override { return pos < text->length ? text->chars[pos] : -1; }
};

struct TextWriter : Object, IDisposable {
    virtual void Write(String* s) = 0;
    void Write(char16_t c) { Write(String::FromChar(c, 1)); }
    void Write(int32_t v) { Write(ToStr(v)); }
    void Write(float v) { Write(ToStr(v)); }
    void Write(bool v) { Write(ToStr(v)); }
    void WriteLine() { Write(STR(u"\n")); }
    void WriteLine(String* s) { if (s) Write(s); Write(STR(u"\n")); }
    void WriteLine(int32_t v) { WriteLine(ToStr(v)); }
    void WriteLine(float v) { WriteLine(ToStr(v)); }
    void WriteLine(bool v) { WriteLine(ToStr(v)); }
    void WriteLine(char16_t c) { WriteLine(String::FromChar(c, 1)); }
    void WriteLine(Object* o) { WriteLine(o ? o->ToString() : nullptr); }
    virtual void Flush() {}
    virtual void Close() {}
    void Dispose() override { Close(); }
};
struct StreamWriter : TextWriter {
    Stream* s = nullptr;
    bool ownsStream = false;
    void __init(Stream* stream);
    void __init(Stream* stream, Text::Encoding* enc) { __init(stream); }
    void __init(Stream* stream, Text::Encoding* enc, bool detect) { __init(stream); }
    void __init(Stream* stream, bool detect) { __init(stream); }
    void __init(String* path);
    void __init(String* path, bool append);
    void __init(String* path, bool append, Text::Encoding* enc) { __init(path, append); }
    using TextWriter::Write;
    void Write(String* str) override;
    void Flush() override;
    void Close() override;
};
struct StringWriter : TextWriter {
    Text::StringBuilder* sb = nullptr;
    void __init() { sb = New<Text::StringBuilder>(); }
    using TextWriter::Write;
    void Write(String* str) override { sb->Append(str); }
    String* ToString() override { return sb->ToString(); }
};

struct BinaryReader : Object, IDisposable {
    Stream* s = nullptr;
    void __init(Stream* st) { s = st; }
    void __init(Stream* st, Text::Encoding*) { s = st; }
    uint8_t ReadByte();
    int8_t ReadSByte() { return (int8_t)ReadByte(); }
    bool ReadBoolean() { return ReadByte() != 0; }
    int16_t ReadInt16();
    uint16_t ReadUInt16() { return (uint16_t)ReadInt16(); }
    int32_t ReadInt32();
    uint32_t ReadUInt32() { return (uint32_t)ReadInt32(); }
    int64_t ReadInt64();
    float ReadSingle();
    double ReadDouble();
    String* ReadString();
    Array1<uint8_t>* ReadBytes(int32_t n);
    char16_t ReadChar();
    Stream* get_BaseStream() { return s; }
    void Close() { if (s) s->Close(); }
    void Dispose() override { Close(); }
};
struct BinaryWriter : Object, IDisposable {
    Stream* s = nullptr;
    void __init(Stream* st) { s = st; }
    void __init(Stream* st, Text::Encoding*) { s = st; }
    void Write(uint8_t v) { s->writeRaw(&v, 1); }
    void Write(bool v) { Write((uint8_t)(v ? 1 : 0)); }
    void Write(int16_t v) { s->writeRaw((uint8_t*)&v, 2); }
    void Write(uint16_t v) { s->writeRaw((uint8_t*)&v, 2); }
    void Write(int32_t v) { s->writeRaw((uint8_t*)&v, 4); }
    void Write(uint32_t v) { s->writeRaw((uint8_t*)&v, 4); }
    void Write(int64_t v) { s->writeRaw((uint8_t*)&v, 8); }
    void Write(float v) { s->writeRaw((uint8_t*)&v, 4); }
    void Write(double v) { s->writeRaw((uint8_t*)&v, 8); }
    void Write(String* str);
    void Write(Array1<uint8_t>* b) { s->writeRaw(b->data, b->length); }
    void Close() { if (s) s->Close(); }
    void Dispose() override { Close(); }
};

struct DirectoryInfo : Object {
    String* path = nullptr;
    String* get_FullName() { return path; }
    bool get_Exists();
};
struct FileInfo : Object {
    String* path = nullptr;
    void __init(String* p) { path = p; }
    bool get_Exists();
    int64_t get_Length();
    String* get_Name();
    String* get_FullName() { return path; }
};

struct File {
    static bool Exists(String* path);
    static FileStream* Open(String* path, FileMode mode) { return New<FileStream>(path, mode); }
    static FileStream* Open(String* path, FileMode mode, FileAccess a) { return New<FileStream>(path, mode, a); }
    static FileStream* Open(String* path, FileMode mode, FileAccess a, FileShare s) { return New<FileStream>(path, mode, a, s); }
    static FileStream* OpenRead(String* path) { return New<FileStream>(path, FileMode::Open, FileAccess::Read); }
    static FileStream* Create(String* path) { return New<FileStream>(path, FileMode::Create, FileAccess::Write); }
    static FileStream* OpenWrite(String* path) { return New<FileStream>(path, FileMode::OpenOrCreate, FileAccess::Write); }
    static StreamReader* OpenText(String* path) { return New<StreamReader>(path); }
    static StreamWriter* CreateText(String* path) { return New<StreamWriter>(path); }
    static void Copy(String* src, String* dst, bool overwrite);
    static void Copy(String* src, String* dst) { Copy(src, dst, false); }
    static void Delete(String* path);
    static void Move(String* src, String* dst);
    static String* ReadAllText(String* path);
    static String* ReadAllText(String* path, Text::Encoding*) { return ReadAllText(path); }
    static Array1<String*>* ReadAllLines(String* path);
    static Array1<uint8_t>* ReadAllBytes(String* path);
    static void WriteAllText(String* path, String* text);
    static void WriteAllText(String* path, String* text, Text::Encoding*) { WriteAllText(path, text); }
    static void WriteAllBytes(String* path, Array1<uint8_t>* b);
    static void WriteAllLines(String* path, Array1<String*>* lines);
    static DateTime GetLastWriteTime(String* path);
};
struct Directory {
    static bool Exists(String* path);
    static DirectoryInfo* CreateDirectory(String* path);
    static Array1<String*>* GetFiles(String* path);
    static Array1<String*>* GetFiles(String* path, String* pattern);
    static Array1<String*>* GetDirectories(String* path);
    static Array1<String*>* GetFileSystemEntries(String* path);
    static IEnumerable<String*>* EnumerateFiles(String* path) { return AsEnumerable(GetFiles(path)); }
    static IEnumerable<String*>* EnumerateFiles(String* path, String* pattern) { return AsEnumerable(GetFiles(path, pattern)); }
    static IEnumerable<String*>* EnumerateDirectories(String* path) { return AsEnumerable(GetDirectories(path)); }
    static IEnumerable<String*>* EnumerateFileSystemEntries(String* path) { return AsEnumerable(GetFileSystemEntries(path)); }
    static void Delete(String* path);
    static void Delete(String* path, bool recursive);
};
struct Path {
    static constexpr char16_t DirectorySeparatorChar = u'/';
    static constexpr char16_t AltDirectorySeparatorChar = u'/';
    static constexpr char16_t PathSeparator = u':';
    static String* Combine(String* a, String* b);
    static String* Combine(String* a, String* b, String* c) { return Combine(Combine(a, b), c); }
    static String* Combine(String* a, String* b, String* c, String* d) { return Combine(Combine(Combine(a, b), c), d); }
    static String* GetFullPath(String* p) { return p; }
    static bool IsPathRooted(String* p);
    static String* ChangeExtension(String* p, String* ext);
    static String* GetDirectoryName(String* p);
    static String* GetExtension(String* p);
    static String* GetFileName(String* p);
    static String* GetFileNameWithoutExtension(String* p);
    static bool HasExtension(String* p) { auto* e = GetExtension(p); return e && e->length > 0; }
    static String* GetTempPath();
    static Array1<char16_t>* GetInvalidFileNameChars();
};

using InvalidDataException = ::System::InvalidDataException;
using IOException = ::System::IOException;
using FileNotFoundException = ::System::FileNotFoundException;
using DirectoryNotFoundException = ::System::DirectoryNotFoundException;

// Platform hook: maps game paths to real paths (e.g. Vita app0:/ux0:).
extern String* (*MapPath)(String* path);

}  // namespace System::IO

namespace System::Reflection {
enum class BindingFlags : int32_t { Default = 0, Instance = 4, Static = 8, Public = 16, NonPublic = 32 };
inline String* __EnumToString(BindingFlags v) { return ToStr((int32_t)v); }
inline BindingFlags operator|(BindingFlags a, BindingFlags b) { return (BindingFlags)((int32_t)a | (int32_t)b); }
struct MemberInfo : Object {
    String* name = nullptr;
    String* get_Name() { return name; }
};
struct ParameterInfo : Object {
    String* name = nullptr;
    Type* type = nullptr;
    bool hasDefault = false;
    Object* defaultValue = nullptr;
    String* get_Name() { return name; }
    Type* get_ParameterType() { return type; }
    bool get_HasDefaultValue() { return hasDefault; }
    Object* get_DefaultValue() { return defaultValue; }
};
struct MethodBase : MemberInfo {
    Array1<ParameterInfo*>* params = nullptr;
    Array1<ParameterInfo*>* GetParameters() { return params; }
};
struct ConstructorInfo : MethodBase {
    Object* (*invoke)(Array1<Object*>* args) = nullptr;
    Object* Invoke(Array1<Object*>* args) { return invoke(args); }
};
struct FieldInfo : MemberInfo {
    Type* type = nullptr;
    bool isStatic = false;
    Object* (*get)(Object* target) = nullptr;
    void (*set)(Object* target, Object* value) = nullptr;
    Type* get_FieldType() { return type; }
    bool get_IsStatic() { return isStatic; }
    Object* GetValue(Object* target) { return get(target); }
    void SetValue(Object* target, Object* value) { set(target, value); }
};
struct AssemblyInformationalVersionAttribute : Object {
    String* version = nullptr;
    String* get_InformationalVersion() { return version; }
};
struct Assembly : Object {
    static Assembly* GetExecutingAssembly();
    static Assembly* GetEntryAssembly() { return GetExecutingAssembly(); }
};
struct CustomAttributeExtensions {
    template <class T>
    static T GetCustomAttribute(Assembly*);
};
template <>
AssemblyInformationalVersionAttribute* CustomAttributeExtensions::GetCustomAttribute<AssemblyInformationalVersionAttribute*>(Assembly*);

// Helpers used by generated reflection tables.
ConstructorInfo* MakeCtor(std::initializer_list<ParameterInfo*> params, Object* (*invoke)(Array1<Object*>*));
ParameterInfo* MakeParam(const char16_t* name, Type* type, bool hasDefault, Object* def);
FieldInfo* MakeField(const char16_t* name, Type* type, bool isStatic, Object* (*get)(Object*), void (*set)(Object*, Object*));
}  // namespace System::Reflection
