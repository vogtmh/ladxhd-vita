#include <gc.h>
#define GC_malloc CheckedMalloc
#define GC_malloc_atomic CheckedMallocAtomic
#define GC_malloc_ignore_off_page CheckedMallocOffPage
#define GC_malloc_atomic_ignore_off_page CheckedMallocAtomicOffPage
#include "rt/all.h"
#undef GC_malloc
#undef GC_malloc_atomic
#undef GC_malloc_ignore_off_page
#undef GC_malloc_atomic_ignore_off_page
#include <cassert>
#include <cstdio>
#include <cstdlib>

static bool limitAllocations = false;
static size_t largestAllocation = 0;

static void checkAllocation(size_t bytes) {
    if (!limitAllocations) return;
    if (bytes > largestAllocation) largestAllocation = bytes;
    if (bytes >= 64 * 1024) {
        std::fprintf(stderr, "Contiguous allocation too large: %zu bytes\n", bytes);
        std::exit(2);
    }
}

extern "C" void* CheckedMalloc(size_t bytes) { checkAllocation(bytes); return GC_malloc(bytes); }
extern "C" void* CheckedMallocAtomic(size_t bytes) { checkAllocation(bytes); return GC_malloc_atomic(bytes); }
extern "C" void* CheckedMallocOffPage(size_t bytes) { checkAllocation(bytes); return GC_malloc_ignore_off_page(bytes); }
extern "C" void* CheckedMallocAtomicOffPage(size_t bytes) { checkAllocation(bytes); return GC_malloc_atomic_ignore_off_page(bytes); }
extern "C" void rt_log(const char* message) { std::puts(message); }
extern "C" void rt_log_flush() {}
extern "C" const char* rt_base_directory() { return "."; }
extern "C" const char* rt_platform_folder(int) { return "."; }
extern "C" const char* rt_game_version() { return "test"; }
extern "C" void rt_thread_join(void*) { std::abort(); }

using namespace System;
using namespace System::Collections::Generic;

struct CollisionComparer final : Object, IEqualityComparer<int32_t> {
    bool Equals(int32_t left, int32_t right) override { return left == right; }
    int32_t GetHashCode(int32_t key) override { return key % 7; }
};

struct Payload final : Object {
    int32_t marker = 0;
    void __init(int32_t value) { marker = value; }
};

__attribute__((noinline)) static void throwInner() { auto* d = New<Dictionary<int32_t, int32_t>>(); d->get_Item(42); }
__attribute__((noinline)) static void throwOuter() {
    try {
        throwInner();
    } catch (Exception* e) {
        throw NewEx<Exception>(STR(u"outer"), e);
    }
}

static void checkExceptionTrace() {
    try {
        throwOuter();
        assert(false);
    } catch (Exception* e) {
        assert(e->traceDepth >= 3);
        assert(e->inner && e->inner->traceDepth >= 4);
        rt::LogException("Test", e);
    }
}

int main() {
    GC_INIT();
    checkExceptionTrace();
    GC_set_max_heap_size(16 * 1024 * 1024);
    limitAllocations = true;
    auto* dictionary = New<Dictionary<int32_t, int32_t>>();
    for (int32_t key = 0; key < 50000; ++key) dictionary->Add(key, key * 3);
    GC_gcollect();
    for (int32_t key = 0; key < 50000; ++key) assert(dictionary->get_Item(key) == key * 3);
    for (int32_t key = 0; key < 50000; key += 2) assert(dictionary->Remove(key));
    for (int32_t key = 0; key < 50000; key += 2) assert(dictionary->TryAdd(key, key * 5));
    int32_t visited = 0;
    for (auto pair : *dictionary) {
        assert(pair.value == pair.key * (pair.key % 2 == 0 ? 5 : 3));
        ++visited;
    }
    assert(visited == 50000);
    dictionary->Clear();
    assert(dictionary->get_Count() == 0);
    for (int32_t key = 0; key < 50000; ++key) assert(!dictionary->ContainsKey(key));
    for (int32_t key = 0; key < 50000; ++key) dictionary->Add(key, key);
    auto* collisions = New<Dictionary<int32_t, int32_t>>((IEqualityComparer<int32_t>*)New<CollisionComparer>());
    for (int32_t key = 0; key < 2000; ++key) collisions->Add(key, key);
    for (int32_t key = 0; key < 2000; key += 3) assert(collisions->Remove(key));
    for (int32_t key = 0; key < 2000; ++key) {
        assert(collisions->ContainsKey(key) == (key % 3 != 0));
        collisions->set_Item(key, key * 2);
        assert(!collisions->TryAdd(key, -1));
    }
    int32_t keyCount = 0;
    auto* enumerator = collisions->GetEnumerator();
    while (enumerator->MoveNext()) {
        auto pair = enumerator->get_Current();
        assert(pair.value == pair.key * 2);
        ++keyCount;
    }
    assert(keyCount == 2000);
    assert(collisions->get_Keys()->get_Count() == 2000);
    for (auto value : *collisions->get_Values()) assert(value % 2 == 0);
    auto* objects = New<HashSet<Payload*>>();
    for (int32_t index = 0; index < 40000; ++index) objects->Add(New<Payload>(index + 1));
    GC_gcollect();
    assert(objects->get_Count() == 40000);
    int64_t total = 0;
    for (auto* object : *objects) {
        assert(objects->Contains(object));
        assert(object->marker > 0 && object->marker <= 40000);
        total += object->marker;
    }
    assert(total == int64_t(40000) * 40001 / 2);
    objects->Clear();
    for (int32_t index = 0; index < 40000; ++index) assert(objects->d->entries->data[index].key == nullptr);
    auto* reserved = New<Dictionary<int32_t, int32_t>>(40000);
    reserved->Add(39999, 42);
    GC_gcollect();
    assert(reserved->get_Item(39999) == 42);
    std::printf("Collection checks passed; largest allocation: %zu bytes\n", largestAllocation);
}