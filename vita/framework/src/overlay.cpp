// On-screen performance overlay drawn with a tiny built-in 3x5 pixel font (no assets needed).
#include "gfx_internal.h"
#include <cstring>
#include <string>
#include <vector>

namespace fw::overlay {

bool enabled = false;
static std::vector<std::string> lines;

// Rows of 3 bits, MSB = left column.
struct Glyph {
    char c;
    uint8_t rows[5];
};
static const Glyph kFont[] = {
    {'0', {7, 5, 5, 5, 7}}, {'1', {2, 6, 2, 2, 7}}, {'2', {7, 1, 7, 4, 7}}, {'3', {7, 1, 7, 1, 7}},
    {'4', {5, 5, 7, 1, 1}}, {'5', {7, 4, 7, 1, 7}}, {'6', {7, 4, 7, 5, 7}}, {'7', {7, 1, 1, 1, 1}},
    {'8', {7, 5, 7, 5, 7}}, {'9', {7, 5, 7, 1, 7}}, {'A', {2, 5, 7, 5, 5}}, {'B', {6, 5, 6, 5, 6}},
    {'C', {3, 4, 4, 4, 3}}, {'D', {6, 5, 5, 5, 6}}, {'E', {7, 4, 6, 4, 7}}, {'F', {7, 4, 6, 4, 4}},
    {'G', {3, 4, 5, 5, 3}}, {'H', {5, 5, 7, 5, 5}}, {'I', {7, 2, 2, 2, 7}}, {'J', {1, 1, 1, 5, 2}},
    {'K', {5, 5, 6, 5, 5}}, {'L', {4, 4, 4, 4, 7}}, {'M', {5, 7, 7, 5, 5}}, {'N', {6, 5, 5, 5, 5}},
    {'O', {2, 5, 5, 5, 2}}, {'P', {6, 5, 6, 4, 4}}, {'Q', {2, 5, 5, 6, 3}}, {'R', {6, 5, 6, 5, 5}},
    {'S', {3, 4, 2, 1, 6}}, {'T', {7, 2, 2, 2, 2}}, {'U', {5, 5, 5, 5, 7}}, {'V', {5, 5, 5, 5, 2}},
    {'W', {5, 5, 7, 7, 5}}, {'X', {5, 5, 2, 5, 5}}, {'Y', {5, 5, 2, 2, 2}}, {'Z', {7, 1, 2, 4, 7}},
    {'.', {0, 0, 0, 0, 2}}, {':', {0, 2, 0, 2, 0}}, {'/', {1, 1, 2, 4, 4}}, {'-', {0, 0, 7, 0, 0}},
    {'%', {5, 1, 2, 4, 5}}, {'|', {2, 2, 2, 2, 2}},
};

void setLines(std::vector<std::string> l) { lines = std::move(l); }

void draw(int w, int h) {
    if (!enabled || lines.empty()) return;
    const int px = 3, advance = 4 * px, lineH = 7 * px, pad = 6;
    size_t maxLen = 0;
    for (auto& l : lines) maxLen = std::max(maxLen, l.size());
    std::vector<gfx::Vertex> v;
    auto quad = [&](float x, float y, float qw, float qh, uint32_t color) {
        v.push_back({x, y, color, 0, 0});
        v.push_back({x + qw, y, color, 1, 0});
        v.push_back({x, y + qh, color, 0, 1});
        v.push_back({x + qw, y + qh, color, 1, 1});
    };
    // Packed ABGR, premultiplied alpha.
    quad(0, 0, (float)(maxLen * advance + 2 * pad), (float)(lines.size() * lineH + 2 * pad), 0xB0000000u);
    for (size_t li = 0; li < lines.size(); li++) {
        float y0 = (float)(pad + li * lineH);
        for (size_t ci = 0; ci < lines[li].size(); ci++) {
            char c = lines[li][ci];
            if (c >= 'a' && c <= 'z') c -= 32;
            const Glyph* g = nullptr;
            for (auto& f : kFont)
                if (f.c == c) g = &f;
            if (!g) continue;
            float x0 = (float)(pad + ci * advance);
            for (int r = 0; r < 5; r++)
                for (int col = 0; col < 3; col++)
                    if (g->rows[r] & (4 >> col)) quad(x0 + col * px, y0 + r * px, (float)px, (float)px, 0xFF40FF40u);
        }
    }
    gfx::drawOverlay(v.data(), (int)(v.size() / 4), w, h);
}

}  // namespace fw::overlay

namespace fw {
void setPerfOverlay(bool on) { overlay::enabled = on; }
}  // namespace fw
