// Runtime: streams, files, directories, paths.
#include "rt/all.h"
#include <algorithm>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>
#include <unordered_map>
#include <vector>
#include <sys/stat.h>
#include <dirent.h>
#include <fcntl.h>
#include <unistd.h>
#include <zlib.h>

namespace {
// Read-only pack: thousands of small files install slowly on the Vita, so game data ships as one file.
struct PackEntry {
    uint64_t off, size, stored;  // stored < size: zlib stream
};
struct PackChild {
    std::string name;
    bool isDir;
};
struct Pack {
    std::string file, prefix;  // prefix lowercased, without trailing '/'
    int fd = -1;               // kept open; entries are read with lseek+read (no stdio buffering)
    std::unordered_map<std::string, PackEntry> files;  // lowercased relative path
    std::unordered_map<std::string, std::vector<PackChild>> dirs;  // lowercased dir -> children
};
Pack* g_pack = nullptr;

std::string lower(std::string s) {
    for (auto& c : s) c = (char)std::tolower((unsigned char)c);
    return s;
}
std::string parentOf(const std::string& p) {
    auto i = p.rfind('/');
    return i == std::string::npos ? std::string() : p.substr(0, i);
}
// True if `n` lies below the pack prefix; `rel` receives the normalized, lowercased relative path.
bool packRel(const std::string& n, std::string& rel) {
    if (!g_pack) return false;
    std::string l = lower(n);
    const std::string& pre = g_pack->prefix;
    if (l.compare(0, pre.size(), pre) != 0 || (l.size() > pre.size() && l[pre.size()] != '/')) return false;
    std::vector<std::string> parts;
    size_t i = pre.size();
    while (i < l.size()) {
        size_t j = l.find('/', i + 1);
        if (j == std::string::npos) j = l.size();
        std::string seg = l.substr(i + 1, j - i - 1);
        if (seg == "..") {
            if (!parts.empty()) parts.pop_back();
        } else if (!seg.empty() && seg != ".") parts.push_back(seg);
        i = j;
    }
    rel.clear();
    for (auto& s : parts) rel += (rel.empty() ? "" : "/") + s;
    return true;
}
const PackEntry* packFile(const std::string& n, bool* under = nullptr) {
    std::string rel;
    bool u = packRel(n, rel);
    if (under) *under = u;
    if (!u) return nullptr;
    auto it = g_pack->files.find(rel);
    return it == g_pack->files.end() ? nullptr : &it->second;
}
int32_t packRead(int64_t offset, uint8_t* dst, int32_t n) {
    if (!g_pack || g_pack->fd < 0 || lseek(g_pack->fd, (off_t)offset, SEEK_SET) < 0) return 0;
    int32_t done = 0;
    while (done < n) {
        ssize_t r = read(g_pack->fd, dst + done, (size_t)(n - done));
        if (r <= 0) break;
        done += (int32_t)r;
    }
    return done;
}
}  // namespace

namespace rt {
bool MountPack(const char* packFile, const char* prefix) {
    FILE* f = std::fopen(packFile, "rb");
    if (!f) return false;
    char magic[8];
    uint32_t counts[2];
    if (std::fread(magic, 1, 8, f) != 8 || std::memcmp(magic, "LADXPAK2", 8) != 0 || std::fread(counts, 4, 2, f) != 2) {
        std::fclose(f);
        return false;
    }
    auto* p = new Pack();
    p->file = packFile;
    p->prefix = lower(prefix);
    while (!p->prefix.empty() && p->prefix.back() == '/') p->prefix.pop_back();
    auto readName = [&]() {
        uint16_t len = 0;
        std::fread(&len, 2, 1, f);
        std::string s(len, '\0');
        std::fread(s.data(), 1, len, f);
        return s;
    };
    p->dirs[""];
    for (uint32_t i = 0; i < counts[0]; i++) {
        std::string d = readName();
        p->dirs[lower(d)];
        p->dirs[lower(parentOf(d))].push_back({d.substr(d.rfind('/') + 1), true});
    }
    for (uint32_t i = 0; i < counts[1]; i++) {
        std::string name = readName();
        PackEntry e;
        std::fread(&e.off, 8, 1, f);
        std::fread(&e.size, 8, 1, f);
        std::fread(&e.stored, 8, 1, f);
        p->files[lower(name)] = e;
        p->dirs[lower(parentOf(name))].push_back({name.substr(name.rfind('/') + 1), false});
    }
    std::fclose(f);
    p->fd = open(packFile, O_RDONLY);
    g_pack = p;
    return p->fd >= 0;
}
}  // namespace rt

namespace System::IO {

static std::string native(String* p) {
    if (!p) throw NewEx<ArgumentNullException>(STR(u"path"));
    String* m = MapPath ? MapPath(p) : p;
    std::string s = m->ToUtf8();
    for (auto& c : s)
        if (c == '\\') c = '/';
    return s;
}

// ---------------------------------------------------------------- Stream
void Stream::CopyTo(Stream* dst) {
    uint8_t buf[16384];
    int32_t n;
    while ((n = readRaw(buf, sizeof buf)) > 0) dst->writeRaw(buf, n);
}
Array1<uint8_t>* Stream::ReadAllRemaining() {
    std::vector<uint8_t> v;
    uint8_t buf[16384];
    int32_t n;
    while ((n = readRaw(buf, sizeof buf)) > 0) v.insert(v.end(), buf, buf + n);
    auto* a = Array1<uint8_t>::New((int32_t)v.size());
    if (!v.empty()) std::memcpy(a->data, v.data(), v.size());
    return a;
}

int32_t MemoryStream::readRaw(uint8_t* dst, int32_t n) {
    int32_t avail = len - pos;
    if (n > avail) n = avail;
    if (n <= 0) return 0;
    std::memcpy(dst, buf->data + pos, n);
    pos += n;
    return n;
}
void MemoryStream::writeRaw(const uint8_t* src, int32_t n) {
    if (!writable) ThrowInvalidOperation("MemoryStream not writable");
    if (pos + n > buf->length) {
        int32_t cap = buf->length * 2;
        if (cap < pos + n) cap = pos + n;
        auto* nb = Array1<uint8_t>::New(cap);
        std::memcpy(nb->data, buf->data, len);
        buf = nb;
    }
    std::memcpy(buf->data + pos, src, n);
    pos += n;
    if (pos > len) len = pos;
}
int64_t MemoryStream::Seek(int64_t off, SeekOrigin origin) {
    int64_t p = origin == SeekOrigin::Begin ? off : origin == SeekOrigin::Current ? pos + off : len + off;
    if (p < 0) p = 0;
    pos = (int32_t)p;
    return pos;
}
Array1<uint8_t>* MemoryStream::ToArray() {
    auto* a = Array1<uint8_t>::New(len);
    if (len) std::memcpy(a->data, buf->data, len);
    return a;
}

// ---------------------------------------------------------------- FileStream
void FileStream::__init(String* p, FileMode mode) { __init(p, mode, mode == FileMode::Open ? FileAccess::Read : FileAccess::ReadWrite, FileShare::Read); }
void FileStream::__init(String* p, FileMode mode, FileAccess access) { __init(p, mode, access, FileShare::Read); }
void FileStream::__init(String* p, FileMode mode, FileAccess access, FileShare) {
    path = p;
    std::string n = native(p);
    const char* m = "rb";
    canWrite = access != FileAccess::Read;
    bool underPack = false;
    if (const PackEntry* e = packFile(n, &underPack)) {
        if (canWrite || (mode != FileMode::Open && mode != FileMode::OpenOrCreate))
            throw NewEx<IOException>(String::Concat(STR(u"Read-only file: "), p));
        winStart = (int64_t)e->off;
        winLen = (int64_t)e->size;
        winPos = 0;
        if (e->stored < e->size) {
            auto* packed = (uint8_t*)std::malloc((size_t)e->stored);
            winMem = (uint8_t*)gc_alloc_atomic((size_t)e->size);
            uLongf outLen = (uLongf)e->size;
            bool ok = packed && packRead(winStart, packed, (int32_t)e->stored) == (int32_t)e->stored &&
                      uncompress(winMem, &outLen, packed, (uLong)e->stored) == Z_OK && outLen == e->size;
            std::free(packed);
            if (!ok) throw NewEx<IOException>(String::Concat(STR(u"Corrupt pack entry: "), p));
        }
        return;
    }
    if (underPack && !canWrite) throw NewEx<FileNotFoundException>(String::Concat(STR(u"Could not open file: "), p));
    struct stat st;
    bool exists = stat(n.c_str(), &st) == 0;
    switch (mode) {
        case FileMode::Create:
        case FileMode::Truncate: m = access == FileAccess::Write ? "wb" : "w+b"; break;
        case FileMode::CreateNew:
            if (exists) throw NewEx<IOException>(String::Concat(STR(u"File exists: "), p));
            m = "w+b";
            break;
        case FileMode::Open: m = access == FileAccess::Read ? "rb" : "r+b"; break;
        case FileMode::OpenOrCreate: m = exists ? (access == FileAccess::Read ? "rb" : "r+b") : "w+b"; break;
        case FileMode::Append: m = "ab"; break;
    }
    fp = std::fopen(n.c_str(), m);
    if (!fp) throw NewEx<FileNotFoundException>(String::Concat(STR(u"Could not open file: "), p));
    std::setvbuf((FILE*)fp, nullptr, _IOFBF, 64 * 1024);
}
int32_t FileStream::readRaw(uint8_t* dst, int32_t n) {
    if (winStart >= 0) {
        if (n > winLen - winPos) n = (int32_t)(winLen - winPos);
        if (n <= 0) return 0;
        if (winMem) {
            std::memcpy(dst, winMem + winPos, n);
            winPos += n;
            return n;
        }
        int32_t got = packRead(winStart + winPos, dst, n);
        winPos += got;
        return got;
    }
    if (!fp) return 0;
    return (int32_t)std::fread(dst, 1, n, (FILE*)fp);
}
void FileStream::writeRaw(const uint8_t* src, int32_t n) { if (fp && winStart < 0) std::fwrite(src, 1, n, (FILE*)fp); }
int64_t FileStream::Seek(int64_t off, SeekOrigin origin) {
    if (winStart >= 0) {
        int64_t p = origin == SeekOrigin::Begin ? off : origin == SeekOrigin::Current ? winPos + off : winLen + off;
        winPos = std::clamp<int64_t>(p, 0, winLen);
        return winPos;
    }
    std::fseek((FILE*)fp, (long)off, origin == SeekOrigin::Begin ? SEEK_SET : origin == SeekOrigin::Current ? SEEK_CUR : SEEK_END);
    return std::ftell((FILE*)fp);
}
int64_t FileStream::get_Length() {
    if (winStart >= 0) return winLen;
    long cur = std::ftell((FILE*)fp);
    std::fseek((FILE*)fp, 0, SEEK_END);
    long end = std::ftell((FILE*)fp);
    std::fseek((FILE*)fp, cur, SEEK_SET);
    return end;
}
int64_t FileStream::get_Position() { return winStart >= 0 ? winPos : std::ftell((FILE*)fp); }
void FileStream::Flush() { if (fp) std::fflush((FILE*)fp); }
void FileStream::Close() {
    if (fp) std::fclose((FILE*)fp);
    fp = nullptr;
}

// ---------------------------------------------------------------- readers/writers
void StreamReader::__init(Stream* stream) {
    s = stream;
    auto* bytes = stream->ReadAllRemaining();
    text = String::FromUtf8((const char*)bytes->data, bytes->length);
}
void StreamReader::__init(String* path) { __init(New<FileStream>(path, FileMode::Open, FileAccess::Read)); s->Close(); }
static String* readLineFrom(String* text, int32_t& pos) {
    if (pos >= text->length) return nullptr;
    int32_t start = pos;
    while (pos < text->length && text->chars[pos] != '\n' && text->chars[pos] != '\r') pos++;
    String* line = String::FromUtf16(text->chars + start, pos - start);
    if (pos < text->length && text->chars[pos] == '\r') pos++;
    if (pos < text->length && text->chars[pos] == '\n') pos++;
    return line;
}
String* StreamReader::ReadLine() { return readLineFrom(text, pos); }
String* StreamReader::ReadToEnd() {
    String* r = text->Substring(pos);
    pos = text->length;
    return r;
}
void StreamReader::Close() { if (s) s->Close(); }
String* StringReader::ReadLine() { return readLineFrom(text, pos); }
String* StringReader::ReadToEnd() {
    String* r = text->Substring(pos);
    pos = text->length;
    return r;
}

void StreamWriter::__init(Stream* stream) { s = stream; }
void StreamWriter::__init(String* path) { __init(path, false); }
void StreamWriter::__init(String* path, bool append) {
    s = New<FileStream>(path, append ? FileMode::Append : FileMode::Create, FileAccess::Write);
    ownsStream = true;
}
void StreamWriter::Write(String* str) {
    if (!str) return;
    char* u = str->ToUtf8();
    s->writeRaw((const uint8_t*)u, (int32_t)std::strlen(u));
}
void StreamWriter::Flush() { s->Flush(); }
void StreamWriter::Close() { if (s) { s->Flush(); s->Close(); } }

uint8_t BinaryReader::ReadByte() {
    uint8_t b;
    if (s->readRaw(&b, 1) != 1) throw NewEx<IOException>(STR(u"End of stream"));
    return b;
}
int16_t BinaryReader::ReadInt16() { int16_t v; s->readRaw((uint8_t*)&v, 2); return v; }
int32_t BinaryReader::ReadInt32() { int32_t v; s->readRaw((uint8_t*)&v, 4); return v; }
int64_t BinaryReader::ReadInt64() { int64_t v; s->readRaw((uint8_t*)&v, 8); return v; }
float BinaryReader::ReadSingle() { float v; s->readRaw((uint8_t*)&v, 4); return v; }
double BinaryReader::ReadDouble() { double v; s->readRaw((uint8_t*)&v, 8); return v; }
char16_t BinaryReader::ReadChar() { return (char16_t)ReadByte(); }
String* BinaryReader::ReadString() {
    int32_t len = 0, shift = 0;
    uint8_t b;
    do {
        b = ReadByte();
        len |= (b & 0x7F) << shift;
        shift += 7;
    } while (b & 0x80);
    std::string buf(len, '\0');
    s->readRaw((uint8_t*)buf.data(), len);
    return String::FromUtf8(buf.data(), len);
}
Array1<uint8_t>* BinaryReader::ReadBytes(int32_t n) {
    auto* a = Array1<uint8_t>::New(n);
    int32_t got = s->readRaw(a->data, n);
    if (got < n) Array::Resize(a, got);
    return a;
}
void BinaryWriter::Write(String* str) {
    char* u = str->ToUtf8();
    uint32_t len = (uint32_t)std::strlen(u);
    do {
        uint8_t b = len & 0x7F;
        len >>= 7;
        if (len) b |= 0x80;
        s->writeRaw(&b, 1);
    } while (len);
    s->writeRaw((const uint8_t*)u, (int32_t)std::strlen(u));
}

// ---------------------------------------------------------------- File / Directory
bool File::Exists(String* p) {
    if (!p) return false;
    std::string n = native(p);
    bool under = false;
    if (packFile(n, &under)) return true;
    if (under) return false;
    struct stat st;
    return stat(n.c_str(), &st) == 0 && S_ISREG(st.st_mode);
}
void File::Copy(String* src, String* dst, bool overwrite) {
    if (!overwrite && Exists(dst)) throw NewEx<IOException>(STR(u"Destination exists"));
    WriteAllBytes(dst, ReadAllBytes(src));
}
void File::Delete(String* p) { std::remove(native(p).c_str()); }
void File::Move(String* src, String* dst) {
    if (std::rename(native(src).c_str(), native(dst).c_str()) != 0) {
        Copy(src, dst, true);
        Delete(src);
    }
}
Array1<uint8_t>* File::ReadAllBytes(String* p) {
    auto* fs = New<FileStream>(p, FileMode::Open, FileAccess::Read);
    int64_t len = fs->get_Length();
    auto* a = Array1<uint8_t>::New((int32_t)len);
    int32_t got = len > 0 ? fs->readRaw(a->data, (int32_t)len) : 0;
    fs->Close();
    if (got < len) Array::Resize(a, got);
    return a;
}
String* File::ReadAllText(String* p) {
    auto* b = ReadAllBytes(p);
    return String::FromUtf8((const char*)b->data, b->length);
}
Array1<String*>* File::ReadAllLines(String* p) {
    auto* text = ReadAllText(p);
    auto* l = New<List<String*>>();
    int32_t pos = 0;
    while (String* line = readLineFrom(text, pos)) l->Add(line);
    return l->ToArray();
}
void File::WriteAllBytes(String* p, Array1<uint8_t>* b) {
    auto* fs = New<FileStream>(p, FileMode::Create, FileAccess::Write);
    fs->writeRaw(b->data, b->length);
    fs->Close();
}
void File::WriteAllText(String* p, String* text) {
    auto* fs = New<FileStream>(p, FileMode::Create, FileAccess::Write);
    char* u = text ? text->ToUtf8() : (char*)"";
    fs->writeRaw((const uint8_t*)u, (int32_t)std::strlen(u));
    fs->Close();
}
void File::WriteAllLines(String* p, Array1<String*>* lines) {
    auto* w = New<StreamWriter>(p);
    for (auto* l : *lines) w->WriteLine(l);
    w->Close();
}
DateTime File::GetLastWriteTime(String* p) {
    struct stat st;
    DateTime d;
    std::string n = native(p), rel;
    if (packRel(n, rel)) return d;
    if (stat(n.c_str(), &st) == 0) d.ticks = 621355968000000000LL + (int64_t)st.st_mtime * 10000000LL;
    return d;
}
bool FileInfo::get_Exists() { return File::Exists(path); }
int64_t FileInfo::get_Length() {
    std::string n = native(path);
    bool under = false;
    if (const PackEntry* e = packFile(n, &under)) return (int64_t)e->size;
    if (under) return 0;
    struct stat st;
    return stat(n.c_str(), &st) == 0 ? st.st_size : 0;
}
String* FileInfo::get_Name() { return Path::GetFileName(path); }
bool DirectoryInfo::get_Exists() { return Directory::Exists(path); }

bool Directory::Exists(String* p) {
    if (!p) return false;
    std::string n = native(p), rel;
    if (packRel(n, rel)) return g_pack->dirs.count(rel) != 0;
    struct stat st;
    return stat(n.c_str(), &st) == 0 && S_ISDIR(st.st_mode);
}
DirectoryInfo* Directory::CreateDirectory(String* p) {
    std::string n = native(p);
    for (size_t i = 1; i <= n.size(); i++) {
        if (i == n.size() || n[i] == '/') {
            std::string sub = n.substr(0, i);
            if (!sub.empty() && sub.back() != ':') mkdir(sub.c_str(), 0777);
        }
    }
    auto* d = New<DirectoryInfo>();
    d->path = p;
    return d;
}
static bool wildcard(const char* pat, const char* s) {
    if (*pat == 0) return *s == 0;
    if (*pat == '*') return wildcard(pat + 1, s) || (*s && wildcard(pat, s + 1));
    if (*s && (*pat == '?' || std::tolower(*pat) == std::tolower(*s))) return wildcard(pat + 1, s + 1);
    return false;
}
static Array1<String*>* listDir(String* p, int kind, String* pattern) {
    auto* l = New<List<String*>>();
    std::string n = native(p);
    std::string pat = pattern ? pattern->ToUtf8() : "*";
    std::vector<std::string> names;
    std::string rel;
    if (packRel(n, rel)) {
        auto it = g_pack->dirs.find(rel);
        if (it == g_pack->dirs.end()) throw NewEx<DirectoryNotFoundException>(String::Concat(STR(u"Directory not found: "), p));
        for (auto& c : it->second) {
            if (kind == 1 && c.isDir) continue;
            if (kind == 2 && !c.isDir) continue;
            if (wildcard(pat.c_str(), c.name.c_str())) names.push_back(c.name);
        }
        std::sort(names.begin(), names.end());
        for (auto& name : names) l->Add(Path::Combine(p, String::FromUtf8(name.c_str())));
        return l->ToArray();
    }
    DIR* d = opendir(n.c_str());
    if (!d) throw NewEx<DirectoryNotFoundException>(String::Concat(STR(u"Directory not found: "), p));
    while (dirent* e = readdir(d)) {
        std::string name = e->d_name;
        if (name == "." || name == "..") continue;
        std::string full = n + (n.empty() || n.back() == '/' ? "" : "/") + name;
        struct stat st;
        if (stat(full.c_str(), &st) != 0) continue;
        bool isDir = S_ISDIR(st.st_mode);
        if (kind == 1 && isDir) continue;
        if (kind == 2 && !isDir) continue;
        if (!wildcard(pat.c_str(), name.c_str())) continue;
        names.push_back(name);
    }
    closedir(d);
    std::sort(names.begin(), names.end());
    for (auto& name : names) l->Add(Path::Combine(p, String::FromUtf8(name.c_str())));
    return l->ToArray();
}
Array1<String*>* Directory::GetFiles(String* p) { return listDir(p, 1, nullptr); }
Array1<String*>* Directory::GetFiles(String* p, String* pattern) { return listDir(p, 1, pattern); }
Array1<String*>* Directory::GetDirectories(String* p) { return listDir(p, 2, nullptr); }
Array1<String*>* Directory::GetFileSystemEntries(String* p) { return listDir(p, 0, nullptr); }
void Directory::Delete(String* p) { rmdir(native(p).c_str()); }
void Directory::Delete(String* p, bool recursive) {
    if (recursive) {
        for (auto* f : *GetFiles(p)) File::Delete(f);
        for (auto* d : *GetDirectories(p)) Delete(d, true);
    }
    Delete(p);
}

// ---------------------------------------------------------------- Path
static bool isSep(char16_t c) { return c == '/' || c == '\\'; }
String* Path::Combine(String* a, String* b) {
    nn(a);
    nn(b);
    if (b->length == 0) return a;
    if (a->length == 0 || IsPathRooted(b)) return b;
    if (isSep(a->chars[a->length - 1])) return String::Concat(a, b);
    return String::ConcatN({a, STR(u"/"), b});
}
bool Path::IsPathRooted(String* p) {
    if (!p || p->length == 0) return false;
    if (isSep(p->chars[0])) return true;
    for (int32_t i = 0; i < p->length && i < 8; i++)
        if (p->chars[i] == ':') return true;  // ux0:, app0:, C:
    return false;
}
static int32_t lastSep(String* p) {
    for (int32_t i = p->length - 1; i >= 0; i--)
        if (isSep(p->chars[i])) return i;
    return -1;
}
String* Path::GetFileName(String* p) {
    if (!p) return nullptr;
    int32_t i = lastSep(p);
    return i < 0 ? p : p->Substring(i + 1);
}
String* Path::GetDirectoryName(String* p) {
    if (!p) return nullptr;
    int32_t i = lastSep(p);
    return i < 0 ? String::Empty : p->Substring(0, i);
}
String* Path::GetExtension(String* p) {
    if (!p) return nullptr;
    int32_t s = lastSep(p);
    for (int32_t i = p->length - 1; i > s; i--)
        if (p->chars[i] == '.') return i == p->length - 1 ? String::Empty : p->Substring(i);
    return String::Empty;
}
String* Path::GetFileNameWithoutExtension(String* p) {
    String* f = GetFileName(p);
    if (!f) return nullptr;
    int32_t i = f->LastIndexOf(u'.');
    return i < 0 ? f : f->Substring(0, i);
}
String* Path::ChangeExtension(String* p, String* ext) {
    if (!p) return nullptr;
    int32_t s = lastSep(p);
    int32_t dot = -1;
    for (int32_t i = p->length - 1; i > s; i--)
        if (p->chars[i] == '.') { dot = i; break; }
    String* base = dot < 0 ? p : p->Substring(0, dot);
    if (!ext) return base;
    if (ext->length && ext->chars[0] != '.') return String::ConcatN({base, STR(u"."), ext});
    return String::Concat(base, ext);
}
String* Path::GetTempPath() { return STR(u"/tmp/"); }
Array1<char16_t>* Path::GetInvalidFileNameChars() { return Array1<char16_t>::Of({u'/', u'\\', u':', u'*', u'?', u'"', u'<', u'>', u'|', 0}); }

}  // namespace System::IO
