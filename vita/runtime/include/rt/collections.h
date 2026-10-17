#pragma once
#include "core.h"
#include <algorithm>

namespace System {

template <class T>
struct Predicate_ {};  // placeholder (Predicate<T> maps to Fn<bool(T)>)

namespace Collections {
namespace Generic {
template <class T>
struct IEnumerator {
    virtual ~IEnumerator() = default;
    virtual bool MoveNext() = 0;
    virtual T get_Current() = 0;
};
}  // namespace Generic
// Non-generic enumeration yields boxed elements.
struct IEnumerable {
    virtual ~IEnumerable() = default;
    virtual Generic::IEnumerator<Object*>* __GetObjEnumerator() = 0;
};
namespace Generic {

template <class T>
struct BoxingEnumerator final : Object, IEnumerator<Object*> {
    IEnumerator<T>* inner = nullptr;
    bool MoveNext() override { return inner->MoveNext(); }
    Object* get_Current() override { return box(inner->get_Current()); }
};

template <class T>
struct IEnumerable : virtual ::System::Collections::IEnumerable {
    virtual IEnumerator<T>* GetEnumerator() = 0;
    IEnumerator<Object*>* __GetObjEnumerator() override {
        auto* b = new BoxingEnumerator<T>();
        b->inner = GetEnumerator();
        return b;
    }
};
template <class T>
struct IReadOnlyCollection : virtual IEnumerable<T> {
    virtual int32_t get_Count() = 0;
};
template <class T>
struct IReadOnlyList : virtual IReadOnlyCollection<T> {
    virtual T get_Item(int32_t i) = 0;
};
template <class T>
struct ICollection : virtual IEnumerable<T> {
    virtual int32_t get_Count() = 0;
};
template <class T>
struct IList : virtual ICollection<T> {};
template <class T>
struct IComparer {
    virtual ~IComparer() = default;
    virtual int32_t Compare(T a, T b) = 0;
};
template <class T>
struct IEqualityComparer {
    virtual ~IEqualityComparer() = default;
    virtual bool Equals(T a, T b) = 0;
    virtual int32_t GetHashCode(T a) = 0;
};

// Enumerator over a contiguous range, keeps its owner alive.
template <class T>
struct PtrEnumerator final : Object, IEnumerator<T> {
    Object* owner = nullptr;
    T* p = nullptr;
    T* e = nullptr;
    bool started = false;
    bool MoveNext() override {
        if (!started) started = true;
        else ++p;
        return p < e;
    }
    T get_Current() override { return *p; }
};

template <class T>
struct List;
template <class T>
List<T>* ToListImpl(IEnumerable<T>* e);

// ---------------------------------------------------------------- List<T>
template <class T>
struct List : Object, virtual IReadOnlyList<T>, virtual IList<T> {
    Array1<T>* items = nullptr;
    int32_t size = 0;

    void __init() { items = nullptr; size = 0; }
    void __init(int32_t capacity) { items = capacity > 0 ? Array1<T>::New(capacity) : nullptr; }
    void __init(IEnumerable<T>* src) { AddRange(src); }

    int32_t get_Count() override { return size; }
    int32_t get_Capacity() { return items ? items->length : 0; }
    T get_Item(int32_t i) override {
        if ((uint32_t)i >= (uint32_t)size) ThrowIndexOutOfRange();
        return items->data[i];
    }
    void set_Item(int32_t i, T v) {
        if ((uint32_t)i >= (uint32_t)size) ThrowIndexOutOfRange();
        items->data[i] = v;
    }
    T& ref(int32_t i) {
        if ((uint32_t)i >= (uint32_t)size) ThrowIndexOutOfRange();
        return items->data[i];
    }
    void grow(int32_t min) {
        int32_t cap = items ? items->length : 0;
        int32_t n = cap == 0 ? 4 : cap * 2;
        if (n < min) n = min;
        auto* a = Array1<T>::New(n);
        for (int32_t i = 0; i < size; i++) a->data[i] = items->data[i];
        items = a;
    }
    void Add(T v) {
        if (!items || size == items->length) grow(size + 1);
        items->data[size++] = v;
    }
    void AddRange(IEnumerable<T>* src);
    void Insert(int32_t idx, T v) {
        if ((uint32_t)idx > (uint32_t)size) ThrowIndexOutOfRange();
        if (!items || size == items->length) grow(size + 1);
        for (int32_t i = size; i > idx; i--) items->data[i] = items->data[i - 1];
        items->data[idx] = v;
        size++;
    }
    void Clear() {
        if (items)
            for (int32_t i = 0; i < size; i++) items->data[i] = T{};
        size = 0;
    }
    int32_t IndexOf(T v) {
        for (int32_t i = 0; i < size; i++)
            if (Eq<T>::eq(items->data[i], v)) return i;
        return -1;
    }
    int32_t LastIndexOf(T v) {
        for (int32_t i = size - 1; i >= 0; i--)
            if (Eq<T>::eq(items->data[i], v)) return i;
        return -1;
    }
    bool Contains(T v) { return IndexOf(v) >= 0; }
    bool Remove(T v) {
        int32_t i = IndexOf(v);
        if (i < 0) return false;
        RemoveAt(i);
        return true;
    }
    void RemoveAt(int32_t idx) {
        if ((uint32_t)idx >= (uint32_t)size) ThrowIndexOutOfRange();
        size--;
        for (int32_t i = idx; i < size; i++) items->data[i] = items->data[i + 1];
        items->data[size] = T{};
    }
    void RemoveRange(int32_t idx, int32_t count) {
        if (idx < 0 || count < 0 || idx + count > size) ThrowArgument("RemoveRange");
        for (int32_t i = idx; i + count < size; i++) items->data[i] = items->data[i + count];
        for (int32_t i = size - count; i < size; i++) items->data[i] = T{};
        size -= count;
    }
    int32_t RemoveAll(Fn<bool(T)>* match) {
        int32_t free = 0;
        while (free < size && !match->Invoke(items->data[free])) free++;
        if (free >= size) return 0;
        int32_t cur = free + 1;
        while (cur < size) {
            while (cur < size && match->Invoke(items->data[cur])) cur++;
            if (cur < size) items->data[free++] = items->data[cur++];
        }
        for (int32_t i = free; i < size; i++) items->data[i] = T{};
        int32_t removed = size - free;
        size = free;
        return removed;
    }
    T Find(Fn<bool(T)>* match) {
        for (int32_t i = 0; i < size; i++)
            if (match->Invoke(items->data[i])) return items->data[i];
        return T{};
    }
    int32_t FindIndex(Fn<bool(T)>* match) {
        for (int32_t i = 0; i < size; i++)
            if (match->Invoke(items->data[i])) return i;
        return -1;
    }
    bool Exists(Fn<bool(T)>* match) { return FindIndex(match) >= 0; }
    void ForEach(Fn<void(T)>* action) {
        for (int32_t i = 0; i < size; i++) action->Invoke(items->data[i]);
    }
    void Reverse() {
        for (int32_t i = 0, j = size - 1; i < j; i++, j--) std::swap(items->data[i], items->data[j]);
    }
    void Sort();
    void Sort(Fn<int32_t(T, T)>* comparison);
    void Sort(IComparer<T>* comparer);
    Array1<T>* ToArray() {
        auto* a = Array1<T>::New(size);
        for (int32_t i = 0; i < size; i++) a->data[i] = items->data[i];
        return a;
    }
    IEnumerator<T>* GetEnumerator() override {
        auto* e = new PtrEnumerator<T>();
        e->owner = this;
        e->p = items ? items->data : nullptr;
        e->e = items ? items->data + size : nullptr;
        return e;
    }
    T* begin() { return items ? items->data : nullptr; }
    T* end() { return items ? items->data + size : nullptr; }
    static Type* __type();
    Type* GetType() override { return __type(); }
};

template <class T>
Type* List<T>::__type() {
    static Type* t = Type::Create(u"List`1", u"System.Collections.Generic.List`1", Object::__type());
    return t;
}

// .NET ArraySortHelper introsort, so equal elements end up in the same order as on .NET.
template <class T, class C>
struct IntroSorter {
    T* keys;
    C cmp;
    void swapIfGreater(int32_t i, int32_t j) {
        if (i != j && cmp(keys[i], keys[j]) > 0) std::swap(keys[i], keys[j]);
    }
    void swap(int32_t i, int32_t j) { std::swap(keys[i], keys[j]); }
    void insertionSort(int32_t lo, int32_t hi) {
        for (int32_t i = lo; i < hi; i++) {
            int32_t j = i;
            T t = keys[i + 1];
            while (j >= lo && cmp(t, keys[j]) < 0) {
                keys[j + 1] = keys[j];
                j--;
            }
            keys[j + 1] = t;
        }
    }
    void downHeap(int32_t i, int32_t n, int32_t lo) {
        T d = keys[lo + i - 1];
        while (i <= n / 2) {
            int32_t child = 2 * i;
            if (child < n && cmp(keys[lo + child - 1], keys[lo + child]) < 0) child++;
            if (!(cmp(d, keys[lo + child - 1]) < 0)) break;
            keys[lo + i - 1] = keys[lo + child - 1];
            i = child;
        }
        keys[lo + i - 1] = d;
    }
    void heapsort(int32_t lo, int32_t hi) {
        int32_t n = hi - lo + 1;
        for (int32_t i = n / 2; i >= 1; i--) downHeap(i, n, lo);
        for (int32_t i = n; i > 1; i--) {
            swap(lo, lo + i - 1);
            downHeap(1, i - 1, lo);
        }
    }
    int32_t pickPivotAndPartition(int32_t lo, int32_t hi) {
        int32_t mid = lo + ((hi - lo) >> 1);
        swapIfGreater(lo, mid);
        swapIfGreater(lo, hi);
        swapIfGreater(mid, hi);
        T pivot = keys[mid];
        swap(mid, hi - 1);
        int32_t left = lo, right = hi - 1;
        while (left < right) {
            while (cmp(keys[++left], pivot) < 0) {}
            while (cmp(pivot, keys[--right]) < 0) {}
            if (left >= right) break;
            swap(left, right);
        }
        if (left != hi - 1) swap(left, hi - 1);
        return left;
    }
    void introSort(int32_t lo, int32_t hi, int32_t depthLimit) {
        while (hi > lo) {
            int32_t partitionSize = hi - lo + 1;
            if (partitionSize <= 16) {
                if (partitionSize == 2) { swapIfGreater(lo, hi); return; }
                if (partitionSize == 3) { swapIfGreater(lo, hi - 1); swapIfGreater(lo, hi); swapIfGreater(hi - 1, hi); return; }
                insertionSort(lo, hi);
                return;
            }
            if (depthLimit == 0) { heapsort(lo, hi); return; }
            depthLimit--;
            int32_t p = pickPivotAndPartition(lo, hi);
            introSort(p + 1, hi, depthLimit);
            hi = p - 1;
        }
    }
    void sort(int32_t n) {
        if (n < 2) return;
        int32_t log2 = 0;
        for (uint32_t v = (uint32_t)n; v > 1; v >>= 1) log2++;
        introSort(0, n - 1, 2 * (log2 + 1));
    }
};
template <class T, class C>
inline void IntroSort(T* keys, int32_t n, C cmp) {
    IntroSorter<T, C> s{keys, cmp};
    s.sort(n);
}

template <class T>
void List<T>::Sort() {
    if (size > 1) IntroSort(items->data, size, [](const T& a, const T& b) { return Cmp<T>::cmp(a, b); });
}
template <class T>
void List<T>::Sort(Fn<int32_t(T, T)>* comparison) {
    if (size > 1) IntroSort(items->data, size, [comparison](const T& a, const T& b) { return comparison->Invoke(a, b); });
}
template <class T>
void List<T>::Sort(IComparer<T>* c) {
    if (!c) { Sort(); return; }
    if (size > 1) IntroSort(items->data, size, [c](const T& a, const T& b) { return c->Compare(a, b); });
}

// ---------------------------------------------------------------- KeyValuePair
template <class K, class V>
struct KeyValuePair {
    K key{};
    V value{};
    K get_Key() const { return key; }
    V get_Value() const { return value; }
    bool Equals(const KeyValuePair& o) const { return Eq<K>::eq(key, o.key) && Eq<V>::eq(value, o.value); }
    int32_t GetHashCode() const { return Eq<K>::hash(key); }
};

// ---------------------------------------------------------------- Dictionary (same slot reuse / order as .NET)
template <class K, class V>
struct Dictionary;

template <class K, class V>
struct Dictionary__KeyCollection final : Object, virtual IReadOnlyCollection<K>, virtual ICollection<K> {
    Dictionary<K, V>* d = nullptr;
    int32_t get_Count() override;
    IEnumerator<K>* GetEnumerator() override;
    struct It {
        Dictionary<K, V>* d;
        int32_t i;
        void skip();
        K operator*() const;
        It& operator++() { i++; skip(); return *this; }
        bool operator!=(const It& o) const { return i != o.i; }
    };
    It begin();
    It end();
    Array1<K>* ToArray();
    bool Contains(K k);
};
template <class K, class V>
struct Dictionary__ValueCollection final : Object, virtual IReadOnlyCollection<V>, virtual ICollection<V> {
    Dictionary<K, V>* d = nullptr;
    int32_t get_Count() override;
    IEnumerator<V>* GetEnumerator() override;
    struct It {
        Dictionary<K, V>* d;
        int32_t i;
        void skip();
        V& operator*() const;
        It& operator++() { i++; skip(); return *this; }
        bool operator!=(const It& o) const { return i != o.i; }
    };
    It begin();
    It end();
};

template <class K, class V>
struct Dictionary : Object, virtual IReadOnlyCollection<KeyValuePair<K, V>>, virtual ICollection<KeyValuePair<K, V>> {
    struct Entry {
        uint32_t hashCode;
        int32_t next;  // -1 end of chain, < -1 free list encoding
        K key;
        V value;
    };
    Array1<int32_t>* buckets = nullptr;
    Array1<Entry>* entries = nullptr;
    int32_t count = 0;
    int32_t freeList = -1;
    int32_t freeCount = 0;
    IEqualityComparer<K>* comparer = nullptr;

    void __init() {}
    void __init(int32_t capacity) { if (capacity > 0) initialize(capacity); }
    void __init(IEqualityComparer<K>* c) { comparer = c; }
    void __init(int32_t capacity, IEqualityComparer<K>* c) { comparer = c; if (capacity > 0) initialize(capacity); }
    void __init(Dictionary* other) {
        if (other) {
            comparer = other->comparer;
            for (auto kv : *other) Add(kv.key, kv.value);
        }
    }
    void __init(Dictionary* other, IEqualityComparer<K>* c) {
        comparer = c;
        if (other)
            for (auto kv : *other) Add(kv.key, kv.value);
    }

    uint32_t hashOf(const K& k) {
        if constexpr (std::is_pointer_v<K>) if (!k) ThrowArgument("key is null");
        return (uint32_t)(comparer ? comparer->GetHashCode(k) : Eq<K>::hash(k));
    }
    bool keyEq(const K& a, const K& b) { return comparer ? comparer->Equals(a, b) : Eq<K>::eq(a, b); }
    static int32_t prime(int32_t min);
    void initialize(int32_t cap) {
        int32_t size = prime(cap);
        buckets = Array1<int32_t>::New(size);
        entries = Array1<Entry>::New(size);
        freeList = -1;
    }
    void resize() {
        int32_t newSize = prime(count * 2);
        auto* nb = Array1<int32_t>::New(newSize);
        auto* ne = Array1<Entry>::New(newSize);
        for (int32_t i = 0; i < count; i++) ne->data[i] = entries->data[i];
        for (int32_t i = 0; i < count; i++) {
            if (ne->data[i].next >= -1) {
                uint32_t b = ne->data[i].hashCode % (uint32_t)newSize;
                ne->data[i].next = nb->data[b] - 1;
                nb->data[b] = i + 1;
            }
        }
        buckets = nb;
        entries = ne;
    }
    int32_t findEntry(const K& key) {
        if (!buckets) return -1;
        uint32_t h = hashOf(key);
        int32_t i = buckets->data[h % (uint32_t)buckets->length] - 1;
        while ((uint32_t)i < (uint32_t)entries->length) {
            Entry& e = entries->data[i];
            if (e.hashCode == h && keyEq(e.key, key)) return i;
            i = e.next;
        }
        return -1;
    }
    // behavior: 0 = throw on existing, 1 = overwrite, 2 = keep (TryAdd)
    bool insert(const K& key, const V& value, int behavior) {
        if (!buckets) initialize(0);
        uint32_t h = hashOf(key);
        int32_t* bucket = &buckets->data[h % (uint32_t)buckets->length];
        int32_t i = *bucket - 1;
        while ((uint32_t)i < (uint32_t)entries->length) {
            Entry& e = entries->data[i];
            if (e.hashCode == h && keyEq(e.key, key)) {
                if (behavior == 1) { e.value = value; return true; }
                if (behavior == 0) ThrowArgument("An item with the same key has already been added.");
                return false;
            }
            i = e.next;
        }
        int32_t index;
        if (freeCount > 0) {
            index = freeList;
            freeList = -3 - entries->data[freeList].next;
            freeCount--;
        } else {
            if (count == entries->length) {
                resize();
                bucket = &buckets->data[h % (uint32_t)buckets->length];
            }
            index = count;
            count++;
        }
        Entry& e = entries->data[index];
        e.hashCode = h;
        e.next = *bucket - 1;
        e.key = key;
        e.value = value;
        *bucket = index + 1;
        return true;
    }

    int32_t get_Count() override { return count - freeCount; }
    V get_Item(K key) {
        int32_t i = findEntry(key);
        if (i < 0) ThrowKeyNotFound();
        return entries->data[i].value;
    }
    void set_Item(K key, V value) { insert(key, value, 1); }
    V& ref(K key) {
        int32_t i = findEntry(key);
        if (i < 0) ThrowKeyNotFound();
        return entries->data[i].value;
    }
    void Add(K key, V value) { insert(key, value, 0); }
    bool TryAdd(K key, V value) { return insert(key, value, 2); }
    bool ContainsKey(K key) { return findEntry(key) >= 0; }
    bool ContainsValue(V value) {
        for (auto kv : *this)
            if (Eq<V>::eq(kv.value, value)) return true;
        return false;
    }
    bool TryGetValue(K key, V& value) {
        int32_t i = findEntry(key);
        if (i >= 0) { value = entries->data[i].value; return true; }
        value = V{};
        return false;
    }
    V GetValueOrDefault(K key) {
        int32_t i = findEntry(key);
        return i >= 0 ? entries->data[i].value : V{};
    }
    bool Remove(K key) {
        if (!buckets) return false;
        uint32_t h = hashOf(key);
        uint32_t b = h % (uint32_t)buckets->length;
        int32_t last = -1;
        int32_t i = buckets->data[b] - 1;
        while (i >= 0) {
            Entry& e = entries->data[i];
            if (e.hashCode == h && keyEq(e.key, key)) {
                if (last < 0) buckets->data[b] = e.next + 1;
                else entries->data[last].next = e.next;
                e.next = -3 - freeList;
                e.key = K{};
                e.value = V{};
                freeList = i;
                freeCount++;
                return true;
            }
            last = i;
            i = e.next;
        }
        return false;
    }
    bool Remove(K key, V& value) {
        int32_t i = findEntry(key);
        if (i < 0) { value = V{}; return false; }
        value = entries->data[i].value;
        return Remove(key);
    }
    void Clear() {
        if (count > 0) {
            for (int32_t i = 0; i < buckets->length; i++) buckets->data[i] = 0;
            for (int32_t i = 0; i < count; i++) entries->data[i] = Entry{};
            count = 0;
            freeList = -1;
            freeCount = 0;
        }
    }
    Dictionary__KeyCollection<K, V>* get_Keys() {
        auto* c = new Dictionary__KeyCollection<K, V>();
        c->d = this;
        return c;
    }
    Dictionary__ValueCollection<K, V>* get_Values() {
        auto* c = new Dictionary__ValueCollection<K, V>();
        c->d = this;
        return c;
    }

    struct It {
        Dictionary* d;
        int32_t i;
        void skip() { while (i < d->count && d->entries->data[i].next < -1) i++; }
        KeyValuePair<K, V> operator*() const { auto& e = d->entries->data[i]; return KeyValuePair<K, V>{e.key, e.value}; }
        It& operator++() { i++; skip(); return *this; }
        bool operator!=(const It& o) const { return i != o.i; }
    };
    It begin() { It it{this, 0}; it.skip(); return it; }
    It end() { return It{this, count}; }

    struct Enumerator final : Object, IEnumerator<KeyValuePair<K, V>> {
        Dictionary* d;
        int32_t i = -1;
        bool MoveNext() override {
            i++;
            while (i < d->count && d->entries->data[i].next < -1) i++;
            return i < d->count;
        }
        KeyValuePair<K, V> get_Current() override { auto& e = d->entries->data[i]; return KeyValuePair<K, V>{e.key, e.value}; }
    };
    IEnumerator<KeyValuePair<K, V>>* GetEnumerator() override {
        auto* e = new Enumerator();
        e->d = this;
        return e;
    }
    static Type* __type();
    Type* GetType() override { return __type(); }
};

int32_t HashPrime(int32_t min);
template <class K, class V>
Type* Dictionary<K, V>::__type() {
    static Type* t = Type::Create(u"Dictionary`2", u"System.Collections.Generic.Dictionary`2", Object::__type());
    return t;
}
template <class K, class V>
int32_t Dictionary<K, V>::prime(int32_t min) { return HashPrime(min); }

template <class K, class V>
int32_t Dictionary__KeyCollection<K, V>::get_Count() { return d->get_Count(); }
template <class K, class V>
void Dictionary__KeyCollection<K, V>::It::skip() { while (i < d->count && d->entries->data[i].next < -1) i++; }
template <class K, class V>
K Dictionary__KeyCollection<K, V>::It::operator*() const { return d->entries->data[i].key; }
template <class K, class V>
typename Dictionary__KeyCollection<K, V>::It Dictionary__KeyCollection<K, V>::begin() { It it{d, 0}; it.skip(); return it; }
template <class K, class V>
typename Dictionary__KeyCollection<K, V>::It Dictionary__KeyCollection<K, V>::end() { return It{d, d->count}; }
template <class K, class V>
Array1<K>* Dictionary__KeyCollection<K, V>::ToArray() {
    auto* a = Array1<K>::New(d->get_Count());
    int32_t n = 0;
    for (auto k : *this) a->data[n++] = k;
    return a;
}
template <class K, class V>
bool Dictionary__KeyCollection<K, V>::Contains(K k) { return d->ContainsKey(k); }

template <class T, class Range>
struct RangeEnumerator final : Object, IEnumerator<T> {
    Object* owner = nullptr;
    typename Range::It it;
    typename Range::It e;
    bool started = false;
    bool MoveNext() override {
        if (!started) started = true;
        else ++it;
        return it != e;
    }
    T get_Current() override { return *it; }
};
template <class K, class V>
IEnumerator<K>* Dictionary__KeyCollection<K, V>::GetEnumerator() {
    auto* r = new RangeEnumerator<K, Dictionary__KeyCollection<K, V>>();
    r->owner = this;
    r->it = begin();
    r->e = end();
    return r;
}
template <class K, class V>
int32_t Dictionary__ValueCollection<K, V>::get_Count() { return d->get_Count(); }
template <class K, class V>
void Dictionary__ValueCollection<K, V>::It::skip() { while (i < d->count && d->entries->data[i].next < -1) i++; }
template <class K, class V>
V& Dictionary__ValueCollection<K, V>::It::operator*() const { return d->entries->data[i].value; }
template <class K, class V>
typename Dictionary__ValueCollection<K, V>::It Dictionary__ValueCollection<K, V>::begin() { It it{d, 0}; it.skip(); return it; }
template <class K, class V>
typename Dictionary__ValueCollection<K, V>::It Dictionary__ValueCollection<K, V>::end() { return It{d, d->count}; }
template <class K, class V>
IEnumerator<V>* Dictionary__ValueCollection<K, V>::GetEnumerator() {
    auto* r = new RangeEnumerator<V, Dictionary__ValueCollection<K, V>>();
    r->owner = this;
    r->it = begin();
    r->e = end();
    return r;
}

// ---------------------------------------------------------------- HashSet
template <class T>
struct HashSet : Object, virtual IReadOnlyCollection<T>, virtual ICollection<T> {
    Dictionary<T, bool>* d = nullptr;
    void __init() { d = New<Dictionary<T, bool>>(); }
    void __init(IEqualityComparer<T>* c) { d = New<Dictionary<T, bool>>(c); }
    void __init(IEnumerable<T>* src) { __init(); UnionWith(src); }
    void __init(IEnumerable<T>* src, IEqualityComparer<T>* c) { __init(c); UnionWith(src); }
    int32_t get_Count() override { return d->get_Count(); }
    bool Add(T v) { return d->TryAdd(v, true); }
    bool Contains(T v) { return d->ContainsKey(v); }
    bool Remove(T v) { return d->Remove(v); }
    void Clear() { d->Clear(); }
    void UnionWith(IEnumerable<T>* src);
    struct It {
        typename Dictionary<T, bool>::It it;
        T operator*() const { return (*it).key; }
        It& operator++() { ++it; return *this; }
        bool operator!=(const It& o) const { return it != o.it; }
    };
    It begin() { return It{d->begin()}; }
    It end() { return It{d->end()}; }
    IEnumerator<T>* GetEnumerator() override {
        auto* r = new RangeEnumerator<T, HashSet<T>>();
        r->owner = this;
        r->it = begin();
        r->e = end();
        return r;
    }
    Array1<T>* ToArray() {
        auto* a = Array1<T>::New(get_Count());
        int32_t n = 0;
        for (auto v : *this) a->data[n++] = v;
        return a;
    }
};

// ---------------------------------------------------------------- Queue / Stack
template <class T>
struct Queue : Object, virtual IReadOnlyCollection<T> {
    Array1<T>* arr = nullptr;
    int32_t head = 0, tail = 0, size = 0;
    void __init() {}
    void __init(int32_t cap) { if (cap > 0) arr = Array1<T>::New(cap); }
    int32_t get_Count() override { return size; }
    void grow() {
        int32_t cap = arr ? arr->length : 0;
        int32_t n = cap < 4 ? 4 : cap * 2;
        auto* a = Array1<T>::New(n);
        for (int32_t i = 0; i < size; i++) a->data[i] = arr->data[(head + i) % cap];
        arr = a;
        head = 0;
        tail = size;
    }
    void Enqueue(T v) {
        if (!arr || size == arr->length) grow();
        arr->data[tail] = v;
        tail = (tail + 1) % arr->length;
        size++;
    }
    T Dequeue() {
        if (size == 0) ThrowInvalidOperation("Queue empty.");
        T v = arr->data[head];
        arr->data[head] = T{};
        head = (head + 1) % arr->length;
        size--;
        return v;
    }
    bool TryDequeue(T& v) {
        if (size == 0) { v = T{}; return false; }
        v = Dequeue();
        return true;
    }
    T Peek() {
        if (size == 0) ThrowInvalidOperation("Queue empty.");
        return arr->data[head];
    }
    bool TryPeek(T& v) {
        if (size == 0) { v = T{}; return false; }
        v = arr->data[head];
        return true;
    }
    void Clear() {
        for (int32_t i = 0; i < size; i++) arr->data[(head + i) % arr->length] = T{};
        head = tail = size = 0;
    }
    bool Contains(T v) {
        for (int32_t i = 0; i < size; i++)
            if (Eq<T>::eq(arr->data[(head + i) % arr->length], v)) return true;
        return false;
    }
    Array1<T>* ToArray() {
        auto* a = Array1<T>::New(size);
        for (int32_t i = 0; i < size; i++) a->data[i] = arr->data[(head + i) % arr->length];
        return a;
    }
    IEnumerator<T>* GetEnumerator() override {
        auto* a = ToArray();
        auto* e = new PtrEnumerator<T>();
        e->owner = a;
        e->p = a->data;
        e->e = a->data + a->length;
        return e;
    }
};

template <class T>
struct Stack : Object, virtual IReadOnlyCollection<T> {
    List<T>* l = nullptr;
    void __init() { l = New<List<T>>(); }
    int32_t get_Count() override { return l->size; }
    void Push(T v) { l->Add(v); }
    T Pop() {
        if (l->size == 0) ThrowInvalidOperation("Stack empty.");
        T v = l->items->data[l->size - 1];
        l->RemoveAt(l->size - 1);
        return v;
    }
    T Peek() {
        if (l->size == 0) ThrowInvalidOperation("Stack empty.");
        return l->items->data[l->size - 1];
    }
    bool TryPop(T& v) {
        if (l->size == 0) { v = T{}; return false; }
        v = Pop();
        return true;
    }
    bool TryPeek(T& v) {
        if (l->size == 0) { v = T{}; return false; }
        v = Peek();
        return true;
    }
    void Clear() { l->Clear(); }
    bool Contains(T v) { return l->Contains(v); }
    // Enumerates top to bottom like .NET.
    IEnumerator<T>* GetEnumerator() override {
        auto* a = l->ToArray();
        Array::Reverse(a);
        auto* e = new PtrEnumerator<T>();
        e->owner = a;
        e->p = a->data;
        e->e = a->data + a->length;
        return e;
    }
};

// ---------------------------------------------------------------- generic enumeration helpers
template <class T>
struct EnumRange {
    IEnumerator<T>* e;
    struct It {
        IEnumerator<T>* e;
        bool done;
        T operator*() const { return e->get_Current(); }
        It& operator++() { done = !e->MoveNext(); return *this; }
        bool operator!=(const It& o) const { return done != o.done; }
    };
    It begin() { return It{e, e ? !e->MoveNext() : true}; }
    It end() { return It{e, true}; }
};

}  // namespace Generic
}  // namespace Collections

template <class T>
void Array::Sort(Array1<T>* a) {
    if (a->length > 1) Collections::Generic::IntroSort(a->data, a->length, [](const T& x, const T& y) { return Cmp<T>::cmp(x, y); });
}

// foreach support: pick the fastest range for the static type.
template <class T>
inline T* ArrBegin(Array1<T>* a) { return a->data; }
template <class T>
inline Array1<T>& Each(Array1<T>* a) { return *nn(a); }
template <class T>
inline Array2<T>& Each(Array2<T>* a) { return *nn(a); }
template <class T>
inline Collections::Generic::List<T>& Each(Collections::Generic::List<T>* l) { return *nn(l); }
template <class K, class V>
inline Collections::Generic::Dictionary<K, V>& Each(Collections::Generic::Dictionary<K, V>* d) { return *nn(d); }
template <class K, class V>
inline Collections::Generic::Dictionary__KeyCollection<K, V>& Each(Collections::Generic::Dictionary__KeyCollection<K, V>* d) { return *nn(d); }
template <class K, class V>
inline Collections::Generic::Dictionary__ValueCollection<K, V>& Each(Collections::Generic::Dictionary__ValueCollection<K, V>* d) { return *nn(d); }
template <class T>
inline Collections::Generic::HashSet<T>& Each(Collections::Generic::HashSet<T>* d) { return *nn(d); }
template <class T>
inline Collections::Generic::EnumRange<T> Each(Collections::Generic::IEnumerable<T>* e) { return Collections::Generic::EnumRange<T>{nn(e)->GetEnumerator()}; }
inline Collections::Generic::EnumRange<Object*> Each(Collections::IEnumerable* e) { return Collections::Generic::EnumRange<Object*>{nn(e)->__GetObjEnumerator()}; }
struct StrRange {
    String* s;
    const char16_t* begin() { return s->chars; }
    const char16_t* end() { return s->chars + s->length; }
};
inline StrRange Each(String* s) { return StrRange{nn(s)}; }

namespace Collections::Generic {
template <class T>
void List<T>::AddRange(IEnumerable<T>* src) {
    if (auto* l = dynamic_cast<List<T>*>(src)) {
        int32_t n = l->size;
        if (!items || size + n > items->length) grow(size + n);
        for (int32_t i = 0; i < n; i++) items->data[size + i] = l->items->data[i];
        size += n;
        return;
    }
    for (auto v : Each(src)) Add(v);
}
template <class T>
void HashSet<T>::UnionWith(IEnumerable<T>* src) {
    for (auto v : Each(src)) Add(v);
}
}  // namespace Collections::Generic

// Wrap arrays as IEnumerable<T> when passed to APIs taking IEnumerable.
template <class T>
struct ArrayEnumerable final : Object, virtual Collections::Generic::IReadOnlyList<T> {
    Array1<T>* a = nullptr;
    int32_t get_Count() override { return a->length; }
    T get_Item(int32_t i) override { return a->at(i); }
    Collections::Generic::IEnumerator<T>* GetEnumerator() override {
        auto* e = new Collections::Generic::PtrEnumerator<T>();
        e->owner = a;
        e->p = a->data;
        e->e = a->data + a->length;
        return e;
    }
};
template <class T>
inline Collections::Generic::IEnumerable<T>* AsEnumerable(Array1<T>* a) {
    if (!a) return nullptr;
    auto* r = new ArrayEnumerable<T>();
    r->a = a;
    return r;
}
struct StringEnumerable final : Object, virtual Collections::Generic::IEnumerable<char16_t> {
    String* s = nullptr;
    Collections::Generic::IEnumerator<char16_t>* GetEnumerator() override {
        auto* e = new Collections::Generic::PtrEnumerator<char16_t>();
        e->owner = s;
        e->p = s->chars;
        e->e = s->chars + s->length;
        return e;
    }
};

}  // namespace System
