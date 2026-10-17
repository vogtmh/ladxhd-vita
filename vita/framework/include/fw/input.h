#pragma once
#include "all.h"

namespace Microsoft::Xna::Framework::Input {
using namespace ::Microsoft::Xna::Framework;

#define FW_KEYS(MM) MM(None, 0) MM(Back, 8) MM(Tab, 9) MM(Enter, 13) MM(Pause, 19) MM(CapsLock, 20) MM(Kana, 21) MM(Kanji, 25) MM(Escape, 27) \
    MM(ImeConvert, 28) MM(ImeNoConvert, 29) MM(Space, 32) MM(PageUp, 33) MM(PageDown, 34) MM(End, 35) MM(Home, 36) MM(Left, 37) MM(Up, 38)    \
    MM(Right, 39) MM(Down, 40) MM(Select, 41) MM(Print, 42) MM(Execute, 43) MM(PrintScreen, 44) MM(Insert, 45) MM(Delete, 46) MM(Help, 47)   \
    MM(D0, 48) MM(D1, 49) MM(D2, 50) MM(D3, 51) MM(D4, 52) MM(D5, 53) MM(D6, 54) MM(D7, 55) MM(D8, 56) MM(D9, 57) MM(A, 65) MM(B, 66) MM(C, 67)   \
    MM(D, 68) MM(E, 69) MM(F, 70) MM(G, 71) MM(H, 72) MM(I, 73) MM(J, 74) MM(K, 75) MM(L, 76) MM(M, 77) MM(N, 78) MM(O, 79) MM(P, 80) MM(Q, 81)   \
    MM(R, 82) MM(S, 83) MM(T, 84) MM(U, 85) MM(V, 86) MM(W, 87) MM(X, 88) MM(Y, 89) MM(Z, 90) MM(LeftWindows, 91) MM(RightWindows, 92)         \
    MM(Apps, 93) MM(Sleep, 95) MM(NumPad0, 96) MM(NumPad1, 97) MM(NumPad2, 98) MM(NumPad3, 99) MM(NumPad4, 100) MM(NumPad5, 101)             \
    MM(NumPad6, 102) MM(NumPad7, 103) MM(NumPad8, 104) MM(NumPad9, 105) MM(Multiply, 106) MM(Add, 107) MM(Separator, 108) MM(Subtract, 109) \
    MM(Decimal, 110) MM(Divide, 111) MM(F1, 112) MM(F2, 113) MM(F3, 114) MM(F4, 115) MM(F5, 116) MM(F6, 117) MM(F7, 118) MM(F8, 119)          \
    MM(F9, 120) MM(F10, 121) MM(F11, 122) MM(F12, 123) MM(F13, 124) MM(F14, 125) MM(F15, 126) MM(F16, 127) MM(F17, 128) MM(F18, 129)          \
    MM(F19, 130) MM(F20, 131) MM(F21, 132) MM(F22, 133) MM(F23, 134) MM(F24, 135) MM(NumLock, 144) MM(Scroll, 145) MM(LeftShift, 160)        \
    MM(RightShift, 161) MM(LeftControl, 162) MM(RightControl, 163) MM(LeftAlt, 164) MM(RightAlt, 165) MM(BrowserBack, 166)                 \
    MM(BrowserForward, 167) MM(BrowserRefresh, 168) MM(BrowserStop, 169) MM(BrowserSearch, 170) MM(BrowserFavorites, 171)                \
    MM(BrowserHome, 172) MM(VolumeMute, 173) MM(VolumeDown, 174) MM(VolumeUp, 175) MM(MediaNextTrack, 176) MM(MediaPreviousTrack, 177)    \
    MM(MediaStop, 178) MM(MediaPlayPause, 179) MM(LaunchMail, 180) MM(SelectMedia, 181) MM(LaunchApplication1, 182)                      \
    MM(LaunchApplication2, 183) MM(OemSemicolon, 186) MM(OemPlus, 187) MM(OemComma, 188) MM(OemMinus, 189) MM(OemPeriod, 190)             \
    MM(OemQuestion, 191) MM(OemTilde, 192) MM(ChatPadGreen, 202) MM(ChatPadOrange, 203) MM(OemOpenBrackets, 219) MM(OemPipe, 220)          \
    MM(OemCloseBrackets, 221) MM(OemQuotes, 222) MM(Oem8, 223) MM(OemBackslash, 226) MM(ProcessKey, 229) MM(OemCopy, 242) MM(OemAuto, 243) \
    MM(OemEnlW, 244) MM(Attn, 246) MM(Crsel, 247) MM(Exsel, 248) MM(EraseEof, 249) MM(Play, 250) MM(Zoom, 251) MM(Pa1, 253) MM(OemClear, 254)

#define FW_ENUM_MEMBER(N, V) N = V,
enum class Keys : int32_t { FW_KEYS(FW_ENUM_MEMBER) };

#define FW_BUTTONS(MM) MM(None, 0) MM(DPadUp, 1) MM(DPadDown, 2) MM(DPadLeft, 4) MM(DPadRight, 8) MM(Start, 16) MM(Back, 32) MM(LeftStick, 64) \
    MM(RightStick, 128) MM(LeftShoulder, 256) MM(RightShoulder, 512) MM(BigButton, 2048) MM(A, 4096) MM(B, 8192) MM(X, 16384) MM(Y, 32768) \
    MM(LeftThumbstickLeft, 2097152) MM(RightTrigger, 4194304) MM(LeftTrigger, 8388608) MM(RightThumbstickUp, 16777216)                  \
    MM(RightThumbstickDown, 33554432) MM(RightThumbstickRight, 67108864) MM(RightThumbstickLeft, 134217728)                            \
    MM(LeftThumbstickUp, 268435456) MM(LeftThumbstickDown, 536870912) MM(LeftThumbstickRight, 1073741824)
enum class Buttons : int32_t { FW_BUTTONS(FW_ENUM_MEMBER) };
#undef FW_ENUM_MEMBER

::System::String* __EnumToString(Keys v);
::System::Array1<Keys>* __EnumValues(Keys*);
::System::Type* __EnumType(Keys*);
::System::String* __EnumToString(Buttons v);
::System::Array1<Buttons>* __EnumValues(Buttons*);
::System::Type* __EnumType(Buttons*);
inline constexpr Buttons operator|(Buttons a, Buttons b) { return static_cast<Buttons>(static_cast<int32_t>(a) | static_cast<int32_t>(b)); }
inline constexpr Buttons operator&(Buttons a, Buttons b) { return static_cast<Buttons>(static_cast<int32_t>(a) & static_cast<int32_t>(b)); }
inline constexpr Buttons operator~(Buttons a) { return static_cast<Buttons>(~static_cast<int32_t>(a)); }
inline Buttons& operator|=(Buttons& a, Buttons b) { return a = a | b; }
inline Buttons& operator&=(Buttons& a, Buttons b) { return a = a & b; }

enum class ButtonState : int32_t { Released = 0, Pressed = 1 };
inline ::System::String* __EnumToString(ButtonState v) { return v == ButtonState::Pressed ? STR(u"Pressed") : STR(u"Released"); }

struct KeyboardState {
    uint32_t keys[8] = {};
    bool IsKeyDown(Keys k) const { int32_t i = (int32_t)k; return i >= 0 && i < 256 && (keys[i >> 5] >> (i & 31)) & 1; }
    bool IsKeyUp(Keys k) const { return !IsKeyDown(k); }
    void set(Keys k) { int32_t i = (int32_t)k; if (i > 0 && i < 256) keys[i >> 5] |= 1u << (i & 31); }
    ::System::Array1<Keys>* GetPressedKeys() const;
    bool Equals(const KeyboardState& o) const { return std::memcmp(keys, o.keys, sizeof(keys)) == 0; }
    int32_t GetHashCode() const { return (int32_t)keys[0]; }
};
struct Keyboard {
    static KeyboardState GetState();
};

struct MouseState {
    int32_t x = 0, y = 0, wheel = 0;
    ButtonState left{}, middle{}, right{};
    int32_t get_X() const { return x; }
    int32_t get_Y() const { return y; }
    Point get_Position() const { return Point(x, y); }
    int32_t get_ScrollWheelValue() const { return wheel; }
    ButtonState get_LeftButton() const { return left; }
    ButtonState get_MiddleButton() const { return middle; }
    ButtonState get_RightButton() const { return right; }
    bool Equals(const MouseState& o) const { return x == o.x && y == o.y && wheel == o.wheel && left == o.left && right == o.right; }
    int32_t GetHashCode() const { return x ^ y; }
};
struct Mouse {
    static MouseState GetState();
    static void SetPosition(int32_t x, int32_t y) {}
};

struct GamePadButtons {
    Buttons buttons{};
    GamePadButtons() = default;
    explicit GamePadButtons(Buttons b) : buttons(b) {}
    bool Equals(const GamePadButtons& o) const { return buttons == o.buttons; }
    int32_t GetHashCode() const { return (int32_t)buttons; }
};
struct GamePadDPad {
    ButtonState up{}, down{}, left{}, right{};
    GamePadDPad() = default;
    GamePadDPad(ButtonState u, ButtonState d, ButtonState l, ButtonState r) : up(u), down(d), left(l), right(r) {}
    ButtonState get_Up() const { return up; }
    ButtonState get_Down() const { return down; }
    ButtonState get_Left() const { return left; }
    ButtonState get_Right() const { return right; }
    bool Equals(const GamePadDPad& o) const { return up == o.up && down == o.down && left == o.left && right == o.right; }
    int32_t GetHashCode() const { return (int32_t)up; }
};
struct GamePadThumbSticks {
    Vector2 left, right;
    GamePadThumbSticks() = default;
    GamePadThumbSticks(Vector2 l, Vector2 r) : left(l), right(r) {}
    Vector2 get_Left() const { return left; }
    Vector2 get_Right() const { return right; }
    bool Equals(const GamePadThumbSticks& o) const { return left == o.left && right == o.right; }
    int32_t GetHashCode() const { return left.GetHashCode(); }
};
struct GamePadTriggers {
    float left = 0, right = 0;
    GamePadTriggers() = default;
    GamePadTriggers(float l, float r) : left(l), right(r) {}
    float get_Left() const { return left; }
    float get_Right() const { return right; }
    bool Equals(const GamePadTriggers& o) const { return left == o.left && right == o.right; }
    int32_t GetHashCode() const { return (int32_t)left; }
};
struct GamePadState {
    GamePadThumbSticks thumbSticks;
    GamePadTriggers triggers;
    GamePadButtons buttons;
    GamePadDPad dpad;
    bool connected = false;
    GamePadState() = default;
    GamePadState(GamePadThumbSticks t, GamePadTriggers tr, GamePadButtons b, GamePadDPad d) : thumbSticks(t), triggers(tr), buttons(b), dpad(d), connected(true) {}
    bool IsButtonDown(Buttons b) const;
    bool IsButtonUp(Buttons b) const { return !IsButtonDown(b); }
    GamePadDPad get_DPad() const { return dpad; }
    GamePadThumbSticks get_ThumbSticks() const { return thumbSticks; }
    GamePadTriggers get_Triggers() const { return triggers; }
    GamePadButtons get_Buttons() const { return buttons; }
    bool get_IsConnected() const { return connected; }
    bool Equals(const GamePadState& o) const { return buttons.buttons == o.buttons.buttons && thumbSticks.Equals(o.thumbSticks); }
    int32_t GetHashCode() const { return (int32_t)buttons.buttons; }
};
struct GamePad {
    static GamePadState GetState(int32_t index);
    static bool SetVibration(int32_t index, float l, float r) { return false; }
};

}  // namespace Microsoft::Xna::Framework::Input
