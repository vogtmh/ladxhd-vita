#include "gfx_internal.h"
#include "overlay.h"
#include <algorithm>
#include <array>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <vector>

namespace fw::overlay {

static int mode = 0;
static int lastMode = 1;
static Sample current;
static std::array<Sample, 60> history;
static size_t historyCount = 0;
static size_t historyNext = 0;
static std::vector<gfx::Vertex> vertices;
static constexpr uint32_t white = 0xFFF4F7F4u;
static constexpr uint32_t muted = 0xFFCFD3CAu;
static constexpr uint32_t green = 0xFFA6E691u;
static constexpr uint32_t orange = 0xFF8AC1FFu;
static constexpr uint32_t red = 0xFF8080FFu;

bool active() { return mode != 0; }
int getMode() { return mode; }
void setMode(int value) {
    mode = value >= 0 && value <= 3 ? value : 0;
    if (mode) lastMode = mode;
}
void cycle() {
    mode = (mode + 1) % 4;
    if (mode) lastMode = mode;
}
void setEnabled(bool on) {
    if (on && !mode) mode = lastMode;
    if (!on) mode = 0;
}
void record(const Sample& sample) {
    current = sample;
    history[historyNext] = sample;
    historyNext = (historyNext + 1) % history.size();
    historyCount = std::min(historyCount + 1, history.size());
}
static uint32_t fpsColor(const Sample& sample) {
    double ratio = sample.fps / std::max(1.0, sample.targetFps);
    return ratio >= 0.95 ? green : ratio >= 0.75 ? orange : red;
}
static uint32_t memoryColor(const Sample& sample) {
    if (!sample.memoryBudget) return muted;
    double ratio = (double)sample.memoryUsed / sample.memoryBudget;
    return ratio < 0.8 ? green : ratio < 0.9 ? orange : red;
}
static void quad(float left, float top, float width, float height, uint32_t color, float slant = 0) {
    vertices.push_back({left + slant, top, color, 0, 0});
    vertices.push_back({left + width + slant, top, color, 1, 0});
    vertices.push_back({left, top + height, color, 0, 1});
    vertices.push_back({left + width, top + height, color, 1, 1});
}

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

static float text(float left, const char* value, float scale, uint32_t color) {
    for (; *value; value++, left += 4 * scale) {
        for (const auto& glyph : kFont) {
            if (glyph.c != *value) continue;
            for (int row = 0; row < 5; row++)
                for (int column = 0; column < 3; column++)
                    if (glyph.rows[row] & (4 >> column))
                        quad(left + column * scale, 19 - 2.5f * scale + row * scale, scale, scale, color);
            break;
        }
    }
    return left;
}
static float textWidth(const char* value, float scale) { return std::strlen(value) * 4 * scale - scale; }
static void fpsGraph(float left, float width) {
    quad(left, 7, width, 24, 0xFF26302Bu);
    float step = width / history.size();
    for (size_t index = 0; index < historyCount; index++) {
        const auto& sample = history[(historyNext + history.size() - historyCount + index) % history.size()];
        float height = 22 * std::clamp(sample.fps / std::max(1.0, sample.targetFps), 0.0, 1.0);
        quad(left + (history.size() - historyCount + index) * step, 30 - height, std::max(1.0f, step), std::max(1.0f, height), fpsColor(sample));
    }
}
static void memoryBar(float left, float width) {
    int filled = current.memoryBudget ? (int)std::lround(10.0 * current.memoryUsed / current.memoryBudget) : 0;
    float step = width / 10;
    for (int index = 0; index < 10; index++) {
        float start = left + index * step;
        uint32_t color = index < filled ? memoryColor(current) : muted;
        if (index < filled) quad(start, 10, step - 4, 18, color, 4);
        else {
            quad(start + 4, 10, step - 4, 1, color);
            quad(start, 27, step - 4, 1, color);
            quad(start, 10, 1, 18, color, 4);
            quad(start + step - 5, 10, 1, 18, color, 4);
        }
    }
}
void draw(int width, int height) {
    if (!active() || width <= 0) return;
    vertices.clear();
    char fps[32], ram[64], detail[128];
    std::snprintf(fps, sizeof fps, "%.0f FPS", current.fps);
    if (current.memoryBudget)
        std::snprintf(ram, sizeof ram, "%s %u/%u MB", current.heapOnly ? "RAM HEAP" : "RAM", (unsigned)(current.memoryUsed >> 20), (unsigned)(current.memoryBudget >> 20));
    else std::snprintf(ram, sizeof ram, "RAM N/A");
    if (current.freeKnown)
        std::snprintf(detail, sizeof detail, "UPD %.1f DRAW %.1f GC %uMB FREE %uMB VRAM %uMB", current.updateMs, current.drawMs, (unsigned)(current.gcHeap >> 20), (unsigned)(current.systemFree >> 20), (unsigned)(current.graphicsFree >> 20));
    else
        std::snprintf(detail, sizeof detail, "UPD %.1f  DRAW %.1f  GC %uMB  CALLS %d  RT %d", current.updateMs, current.drawMs, (unsigned)(current.gcHeap >> 20), current.draws, current.targets);
    float scale = mode == 3 ? 2 : 3;
    float graphWidth = 142, barWidth = 150, gap = 12, detailScale = 2;
    if (mode == 3) {
        graphWidth = 90;
        barWidth = 110;
        // 940 = 960 minus the 10 px padding on each side; six gaps and two 1 px dividers.
        float slack = 940 - (textWidth(fps, scale) + textWidth(ram, 2) + textWidth(detail, 2) + graphWidth + barWidth + 2 + 6 * gap);
        if (slack < 0) detailScale = std::max(1.0f, 2 + slack / (4 * std::strlen(detail) - 1));
        else {
            float graphExtra = std::min(slack / 2, 142 - graphWidth);
            float barExtra = std::min(slack / 2, 150 - barWidth);
            graphWidth += graphExtra;
            barWidth += barExtra;
            gap += (slack - graphExtra - barExtra) / 6;
        }
    }
    // Width is fixed up once the content is laid out.
    quad(0, 0, 960, 38, 0xDC12160Fu);
    float right = text(10, fps, scale, mode == 1 ? white : fpsColor(current)) - scale;
    if (mode > 1) {
        float left = right + gap;
        fpsGraph(left, graphWidth);
        left += graphWidth + gap;
        quad(left, 9, 1, 20, 0xFF46514Bu);
        left += 1 + gap;
        left = text(left, ram, 2, memoryColor(current)) - 2 + gap;
        memoryBar(left, barWidth);
        right = left + barWidth;
        if (mode == 3) {
            left = right + gap;
            quad(left, 9, 1, 20, 0xFF46514Bu);
            left += 1 + gap;
            text(left, detail, detailScale, muted);
        }
    }
    if (mode != 3) vertices[1].x = vertices[3].x = right + 10;
    float originX = (960 - vertices[1].x) / 2;
    float originY = height * 960.0f / width - 38;
    float screenScale = width / 960.0f;
    for (auto& vertex : vertices) {
        vertex.x = (vertex.x + originX) * screenScale;
        vertex.y = (vertex.y + originY) * screenScale;
    }
    gfx::drawOverlay(vertices.data(), (int)(vertices.size() / 4), width, height);
}

}  // namespace fw::overlay

namespace fw {
void setPerfOverlay(bool on) { overlay::setEnabled(on); }
int getPerfOverlayMode() { return overlay::getMode(); }
void setPerfOverlayMode(int value) { overlay::setMode(value); }
}  // namespace fw
