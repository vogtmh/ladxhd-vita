#pragma once
#include "collections.h"

namespace System::Linq {
using namespace ::System::Collections::Generic;
template <class T>
using IOrderedEnumerable = IEnumerable<T>;

struct Enumerable {
    template <class T>
    static List<T>* ToList(IEnumerable<T>* src) {
        auto* l = New<List<T>>();
        l->AddRange(nn(src));
        return l;
    }
    template <class T>
    static Array1<T>* ToArray(IEnumerable<T>* src) { return ToList(src)->ToArray(); }
    template <class A, class B>
    static IEnumerable<B>* Select(IEnumerable<A>* src, Fn<B(A)>* f) {
        auto* l = New<List<B>>();
        for (auto v : Each(nn(src))) l->Add(f->Invoke(v));
        return l;
    }
    template <class A, class B>
    static IEnumerable<B>* Select(IEnumerable<A>* src, Fn<B(A, int32_t)>* f) {
        auto* l = New<List<B>>();
        int32_t i = 0;
        for (auto v : Each(nn(src))) l->Add(f->Invoke(v, i++));
        return l;
    }
    template <class A, class B>
    static IEnumerable<B>* SelectMany(IEnumerable<A>* src, Fn<IEnumerable<B>*(A)>* f) {
        auto* l = New<List<B>>();
        for (auto v : Each(nn(src))) l->AddRange(f->Invoke(v));
        return l;
    }
    template <class T>
    static IEnumerable<T>* Where(IEnumerable<T>* src, Fn<bool(T)>* f) {
        auto* l = New<List<T>>();
        for (auto v : Each(nn(src)))
            if (f->Invoke(v)) l->Add(v);
        return l;
    }
    template <class T>
    static IEnumerable<T>* Concat(IEnumerable<T>* a, IEnumerable<T>* b) {
        auto* l = New<List<T>>();
        l->AddRange(nn(a));
        l->AddRange(nn(b));
        return l;
    }
    template <class R>
    static IEnumerable<R>* OfType(::System::Collections::IEnumerable* src) {
        auto* l = New<List<R>>();
        auto* e = dynamic_cast<IEnumerable<Object*>*>(src);
        if (e) {
            for (auto v : Each(e))
                if (auto* r = as<std::remove_pointer_t<R>>(v)) l->Add(r);
        }
        return l;
    }
    template <class R, class T>
    static IEnumerable<R>* OfType(IEnumerable<T>* src) {
        auto* l = New<List<R>>();
        for (auto v : Each(nn(src)))
            if (auto* r = as<std::remove_pointer_t<R>>(v)) l->Add(r);
        return l;
    }
    template <class R, class T>
    static IEnumerable<R>* Cast(IEnumerable<T>* src) {
        auto* l = New<List<R>>();
        for (auto v : Each(nn(src))) l->Add(cast<std::remove_pointer_t<R>>(v));
        return l;
    }
    template <class T, class K>
    static IOrderedEnumerable<T>* OrderBy(IEnumerable<T>* src, Fn<K(T)>* key, IComparer<K>* cmp = nullptr) {
        auto* l = ToList(src);
        std::stable_sort(l->begin(), l->end(), [&](const T& a, const T& b) {
            K ka = key->Invoke(a), kb = key->Invoke(b);
            return (cmp ? cmp->Compare(ka, kb) : Cmp<K>::cmp(ka, kb)) < 0;
        });
        return l;
    }
    template <class T, class K>
    static IOrderedEnumerable<T>* OrderByDescending(IEnumerable<T>* src, Fn<K(T)>* key, IComparer<K>* cmp = nullptr) {
        auto* l = ToList(src);
        std::stable_sort(l->begin(), l->end(), [&](const T& a, const T& b) {
            K ka = key->Invoke(a), kb = key->Invoke(b);
            return (cmp ? cmp->Compare(ka, kb) : Cmp<K>::cmp(ka, kb)) > 0;
        });
        return l;
    }
    template <class T>
    static T FirstOrDefault(IEnumerable<T>* src) {
        for (auto v : Each(nn(src))) return v;
        return T{};
    }
    template <class T>
    static T FirstOrDefault(IEnumerable<T>* src, Fn<bool(T)>* f) {
        for (auto v : Each(nn(src)))
            if (f->Invoke(v)) return v;
        return T{};
    }
    template <class T>
    static T First(IEnumerable<T>* src) {
        for (auto v : Each(nn(src))) return v;
        ThrowInvalidOperation("Sequence contains no elements");
    }
    template <class T>
    static T First(IEnumerable<T>* src, Fn<bool(T)>* f) {
        for (auto v : Each(nn(src)))
            if (f->Invoke(v)) return v;
        ThrowInvalidOperation("Sequence contains no matching element");
    }
    template <class T>
    static T Last(IEnumerable<T>* src) {
        auto* l = ToList(src);
        if (!l->size) ThrowInvalidOperation("Sequence contains no elements");
        return l->items->data[l->size - 1];
    }
    template <class T>
    static T LastOrDefault(IEnumerable<T>* src) {
        auto* l = ToList(src);
        return l->size ? l->items->data[l->size - 1] : T{};
    }
    template <class T>
    static bool Contains(IEnumerable<T>* src, T v) {
        for (auto x : Each(nn(src)))
            if (Eq<T>::eq(x, v)) return true;
        return false;
    }
    template <class T>
    static bool Any(IEnumerable<T>* src) {
        for (auto v : Each(nn(src))) { (void)v; return true; }
        return false;
    }
    template <class T>
    static bool Any(IEnumerable<T>* src, Fn<bool(T)>* f) {
        for (auto v : Each(nn(src)))
            if (f->Invoke(v)) return true;
        return false;
    }
    template <class T>
    static bool All(IEnumerable<T>* src, Fn<bool(T)>* f) {
        for (auto v : Each(nn(src)))
            if (!f->Invoke(v)) return false;
        return true;
    }
    template <class T>
    static int32_t Count(IEnumerable<T>* src) {
        if (auto* c = dynamic_cast<IReadOnlyCollection<T>*>(src)) return c->get_Count();
        int32_t n = 0;
        for (auto v : Each(nn(src))) { (void)v; n++; }
        return n;
    }
    template <class T>
    static int32_t Count(IEnumerable<T>* src, Fn<bool(T)>* f) {
        int32_t n = 0;
        for (auto v : Each(nn(src)))
            if (f->Invoke(v)) n++;
        return n;
    }
    static double Average(IEnumerable<double>* src) {
        double s = 0;
        int32_t n = 0;
        for (auto v : Each(nn(src))) { s += v; n++; }
        if (!n) ThrowInvalidOperation("Sequence contains no elements");
        return s / n;
    }
    template <class T>
    static T Sum(IEnumerable<T>* src) {
        T s{};
        for (auto v : Each(nn(src))) s += v;
        return s;
    }
    template <class T>
    static T Max(IEnumerable<T>* src) {
        bool any = false;
        T m{};
        for (auto v : Each(nn(src)))
            if (!any || v > m) { m = v; any = true; }
        return m;
    }
    template <class T>
    static T Min(IEnumerable<T>* src) {
        bool any = false;
        T m{};
        for (auto v : Each(nn(src)))
            if (!any || v < m) { m = v; any = true; }
        return m;
    }
    template <class T, class R>
    static R Max(IEnumerable<T>* src, Fn<R(T)>* f) { return Max(Select(src, f)); }
    template <class T, class R>
    static R Min(IEnumerable<T>* src, Fn<R(T)>* f) { return Min(Select(src, f)); }
    template <class T, class R>
    static R Sum(IEnumerable<T>* src, Fn<R(T)>* f) { return Sum(Select(src, f)); }
    template <class T>
    static IEnumerable<T>* Distinct(IEnumerable<T>* src) {
        auto* seen = New<HashSet<T>>();
        auto* l = New<List<T>>();
        for (auto v : Each(nn(src)))
            if (seen->Add(v)) l->Add(v);
        return l;
    }
    template <class T>
    static IEnumerable<T>* Reverse(IEnumerable<T>* src) {
        auto* l = ToList(src);
        l->Reverse();
        return l;
    }
    template <class T>
    static IEnumerable<T>* Skip(IEnumerable<T>* src, int32_t n) {
        auto* l = New<List<T>>();
        int32_t i = 0;
        for (auto v : Each(nn(src)))
            if (i++ >= n) l->Add(v);
        return l;
    }
    template <class T>
    static IEnumerable<T>* Take(IEnumerable<T>* src, int32_t n) {
        auto* l = New<List<T>>();
        int32_t i = 0;
        for (auto v : Each(nn(src))) {
            if (i++ >= n) break;
            l->Add(v);
        }
        return l;
    }
    template <class T>
    static T ElementAt(IEnumerable<T>* src, int32_t idx) { return ToList(src)->get_Item(idx); }
    static IEnumerable<int32_t>* Range(int32_t start, int32_t count) {
        auto* l = New<List<int32_t>>();
        for (int32_t i = 0; i < count; i++) l->Add(start + i);
        return l;
    }
    template <class T>
    static IEnumerable<T>* Empty() { return New<List<T>>(); }
};

}  // namespace System::Linq
