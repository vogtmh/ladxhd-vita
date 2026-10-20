#include "../../framework/src/gfx_internal.h"
#include "../../framework/src/overlay.h"
#include <algorithm>
#include <cassert>
#include <cmath>
#include <cstdio>
#include <vector>

static std::vector<fw::gfx::Vertex> captured;
static constexpr uint32_t white = 0xFFF4F7F4u;
static constexpr uint32_t green = 0xFFA6E691u;
static constexpr uint32_t orange = 0xFF8AC1FFu;
static constexpr uint32_t red = 0xFF8080FFu;

namespace fw::gfx {
void drawOverlay(const Vertex* vertices, int quads, int, int) {
    captured.assign(vertices, vertices + quads * 4);
}
}

static bool hasColor(uint32_t color) {
    return std::any_of(captured.begin(), captured.end(), [=](const auto& vertex) { return vertex.color == color; });
}

static void render(int width = 960) {
    captured.clear();
    int height = width * 544 / 960;
    fw::overlay::draw(width, height);
    for (const auto& vertex : captured) {
        assert(std::isfinite(vertex.x) && std::isfinite(vertex.y));
        assert(vertex.x >= 0 && vertex.x <= width);
        assert(vertex.y >= height - width * 38.0f / 960 - 0.01f && vertex.y <= height + 0.01f);
    }
}

static bool centered(int width = 960) {
    return captured[1].x < width && std::fabs(captured[0].x + captured[1].x - width) < 0.01f;
}

static float contentRight() {
    float right = 0;
    for (size_t index = 4; index < captured.size(); index++) right = std::max(right, captured[index].x);
    return right;
}

int main() {
    using namespace fw::overlay;
    Sample sample;
    sample.fps = 60;
    sample.memoryUsed = 142u << 20;
    sample.memoryBudget = 192u << 20;
    sample.gcHeap = 64u << 20;
    sample.updateMs = 3.2;
    sample.drawMs = 5.1;
    sample.draws = 182;
    sample.targets = 8;
    record(sample);
    render();
    assert(captured.empty());
    cycle();
    render();
    assert(active() && hasColor(white) && !hasColor(green));
    assert(centered());
    cycle();
    render();
    assert(hasColor(green));
    assert(centered());
    render(480);
    assert(centered(480));
    render();
    sample.fps = 48;
    sample.memoryUsed = 166u << 20;
    record(sample);
    render();
    assert(hasColor(orange));
    sample.fps = 27;
    sample.memoryUsed = 184u << 20;
    record(sample);
    render();
    assert(hasColor(red));
    assert(hasColor(green) && hasColor(orange));
    sample.fps = 30;
    sample.targetFps = 30;
    sample.memoryUsed = 142u << 20;
    for (int index = 0; index < 75; index++) record(sample);
    render();
    assert(hasColor(green) && !hasColor(red) && !hasColor(orange));
    int filled = 0, outlined = 0;
    for (size_t index = 0; index < captured.size(); index += 4) {
        const auto& top = captured[index];
        const auto& bottom = captured[index + 2];
        if (top.y == 516 && bottom.y == 534 && top.x > bottom.x) {
            if (captured[index + 1].x - top.x > 1) filled++;
            else outlined++;
        }
    }
    assert(filled == 7 && outlined == 6);
    cycle();
    render();
    assert(captured[1].x == 960);
    assert(contentRight() > 945 && contentRight() <= 950.01f);
    sample.heapOnly = true;
    sample.freeKnown = true;
    sample.systemFree = 83u << 20;
    sample.graphicsFree = 12u << 20;
    record(sample);
    render();
    assert(captured[1].x == 960);
    assert(contentRight() > 945 && contentRight() <= 950.01f);
    render(480);
    assert(captured[1].x == 480);
    setEnabled(false);
    render();
    assert(captured.empty());
    setEnabled(true);
    render();
    assert(captured[1].x == 960);
    cycle();
    assert(!active());
    cycle();
    sample.fps = 5;
    record(sample);
    render();
    assert(hasColor(white) && !hasColor(red));
    cycle();
    sample.memoryBudget = 0;
    record(sample);
    render();
    sample.memoryBudget = 192u << 20;
    sample.memoryUsed = 240u << 20;
    record(sample);
    render();
    assert(hasColor(red));
    for (int value = 0; value <= 3; value++) {
        setMode(value);
        assert(getMode() == value);
        assert(active() == (value != 0));
        render();
        if (!value) assert(captured.empty());
    }
    setMode(-1);
    assert(getMode() == 0);
    setMode(99);
    assert(getMode() == 0);
    setMode(2);
    cycle();
    assert(getMode() == 3);
    setEnabled(false);
    setEnabled(true);
    assert(getMode() == 3);
    std::puts("overlay: modes, colors, history, memory segments, and bounds passed");
}