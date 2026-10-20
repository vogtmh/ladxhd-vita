#pragma once
#include <cstddef>
#include <cstdint>

namespace fw::overlay {
struct Sample {
    double fps = 0;
    double targetFps = 60;
    double updateMs = 0;
    double drawMs = 0;
    size_t memoryUsed = 0;
    size_t memoryBudget = 0;
    size_t gcHeap = 0;
    size_t systemFree = 0;
    size_t graphicsFree = 0;
    int draws = 0;
    int targets = 0;
    bool heapOnly = false;
    bool freeKnown = false;
};
bool active();
int getMode();
void setMode(int value);
void cycle();
void setEnabled(bool on);
void record(const Sample& sample);
void draw(int width, int height);
}