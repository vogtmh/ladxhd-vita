#pragma once
#include "all.h"

namespace Microsoft::Xna::Framework::Content {
struct ContentManager : ::System::Object {
    ::System::String* rootDirectory = nullptr;
    ::Microsoft::Xna::Framework::Graphics::GraphicsDevice* device = nullptr;
    ::System::Collections::Generic::Dictionary<::System::String*, ::System::Object*>* cache = nullptr;
    void __init();
    ::System::String* get_RootDirectory() { return rootDirectory; }
    void set_RootDirectory(::System::String* s) { rootDirectory = s; }
    ::System::Object* loadAsset(::System::String* name, ::System::Type* type);
    template <class T>
    T Load(::System::String* name) { return ::System::cast<std::remove_pointer_t<T>>(loadAsset(name, ::System::TypeOf<std::remove_pointer_t<T>>())); }
    void Unload();
};
}  // namespace Microsoft::Xna::Framework::Content

namespace Microsoft::Xna::Framework {

struct GameTime : Object {
    ::System::TimeSpan elapsed, total;
    bool runningSlowly = false;
    void __init() {}
    ::System::TimeSpan get_ElapsedGameTime() { return elapsed; }
    ::System::TimeSpan get_TotalGameTime() { return total; }
    bool get_IsRunningSlowly() { return runningSlowly; }
};

struct Game;
struct IGameComponent {
    virtual ~IGameComponent() = default;
    virtual void Initialize() = 0;
};
struct GameComponent : Object, virtual IGameComponent {
    Game* game = nullptr;
    bool enabled = true;
    void __init(Game* g) { game = g; }
    void Initialize() override {}
    virtual void Update(GameTime* gameTime) {}
    Game* get_Game() { return game; }
    bool get_Enabled() { return enabled; }
    void set_Enabled(bool e) { enabled = e; }
    static ::System::Type* __type();
    ::System::Type* GetType() override { return __type(); }
};

struct GameComponentCollection : Object {
    ::System::Collections::Generic::List<IGameComponent*>* list = nullptr;
    void __init() { list = ::System::New<::System::Collections::Generic::List<IGameComponent*>>(); }
    void Add(IGameComponent* c) { list->Add(c); }
    bool Remove(IGameComponent* c) { return list->Remove(c); }
    int32_t get_Count() { return list->get_Count(); }
};

struct GameServiceContainer : Object {
    ::System::Collections::Generic::Dictionary<::System::Type*, Object*>* services = nullptr;
    void __init() { services = ::System::New<::System::Collections::Generic::Dictionary<::System::Type*, Object*>>(); }
    Object* GetService(::System::Type* t) {
        Object* o = nullptr;
        services->TryGetValue(t, o);
        return o;
    }
    void AddService(::System::Type* t, Object* o) { services->set_Item(t, o); }
    template <class T>
    T* GetService() { return ::System::as<T>(GetService(::System::TypeOf<T>())); }
};

struct GameWindow : Object {
    Rectangle bounds;
    ::System::String* title = nullptr;
    bool allowResizing = false;
    Rectangle get_ClientBounds() { return bounds; }
    ::System::String* get_Title() { return title; }
    void set_Title(::System::String* t) { title = t; }
    bool get_AllowUserResizing() { return allowResizing; }
    void set_AllowUserResizing(bool b) { allowResizing = b; }
    bool get_IsBorderless() { return true; }
    void set_IsBorderless(bool) {}
};

struct ExitingEventArgs : ::System::EventArgs {
    bool cancel = false;
    bool get_Cancel() { return cancel; }
    void set_Cancel(bool c) { cancel = c; }
};

struct GraphicsDeviceManager;
struct Game : Object, ::System::IDisposable {
    Content::ContentManager* content = nullptr;
    GameComponentCollection* components = nullptr;
    GameServiceContainer* services = nullptr;
    GameWindow* window = nullptr;
    GraphicsDeviceManager* graphicsManager = nullptr;
    bool isActive = true;
    bool fixedTimeStep = true;
    bool mouseVisible = false;
    bool exitRequested = false;
    ::System::TimeSpan targetElapsed = ::System::TimeSpan::FromTicks(166667);
    ::System::Fn<void(Object*, ::System::EventArgs*)>* activated = nullptr;
    ::System::Fn<void(Object*, ::System::EventArgs*)>* deactivated = nullptr;
    ::System::Fn<void(Object*, ExitingEventArgs*)>* exiting = nullptr;

    void __init();
    virtual void Initialize();
    virtual void LoadContent() {}
    virtual void UnloadContent() {}
    virtual void Update(GameTime* gameTime);
    virtual void Draw(GameTime* gameTime) {}
    virtual void BeginRun() {}
    virtual void EndRun() {}
    void Run();
    void Exit() { exitRequested = true; }
    void Dispose() override {}
    Content::ContentManager* get_Content() { return content; }
    GameComponentCollection* get_Components() { return components; }
    GameServiceContainer* get_Services() { return services; }
    GameWindow* get_Window() { return window; }
    bool get_IsActive() { return isActive; }
    bool get_IsFixedTimeStep() { return fixedTimeStep; }
    void set_IsFixedTimeStep(bool b) { fixedTimeStep = b; }
    bool get_IsMouseVisible() { return mouseVisible; }
    void set_IsMouseVisible(bool b) { mouseVisible = b; }
    ::System::TimeSpan get_TargetElapsedTime() { return targetElapsed; }
    void set_TargetElapsedTime(::System::TimeSpan t) { targetElapsed = t; }
    ::Microsoft::Xna::Framework::Graphics::GraphicsDevice* get_GraphicsDevice();
    void add_Activated(::System::Fn<void(Object*, ::System::EventArgs*)>* h) { activated = ::System::Delegate::Combine(activated, h); }
    void remove_Activated(::System::Fn<void(Object*, ::System::EventArgs*)>* h) { activated = ::System::Delegate::Remove(activated, h); }
    void add_Deactivated(::System::Fn<void(Object*, ::System::EventArgs*)>* h) { deactivated = ::System::Delegate::Combine(deactivated, h); }
    void remove_Deactivated(::System::Fn<void(Object*, ::System::EventArgs*)>* h) { deactivated = ::System::Delegate::Remove(deactivated, h); }
    void add_Exiting(::System::Fn<void(Object*, ExitingEventArgs*)>* h) { exiting = ::System::Delegate::Combine(exiting, h); }
    void remove_Exiting(::System::Fn<void(Object*, ExitingEventArgs*)>* h) { exiting = ::System::Delegate::Remove(exiting, h); }
    static ::System::Type* __type();
    ::System::Type* GetType() override { return __type(); }
};

struct GraphicsDeviceManager : Object {
    Game* game = nullptr;
    ::Microsoft::Xna::Framework::Graphics::GraphicsDevice* device = nullptr;
    int32_t prefW = 800, prefH = 480;
    bool vsync = true;
    bool fullScreen = false;
    ::Microsoft::Xna::Framework::Graphics::GraphicsProfile profile{};
    void __init(Game* g);
    ::Microsoft::Xna::Framework::Graphics::GraphicsDevice* get_GraphicsDevice() { return device; }
    int32_t get_PreferredBackBufferWidth() { return prefW; }
    void set_PreferredBackBufferWidth(int32_t w) { prefW = w; }
    int32_t get_PreferredBackBufferHeight() { return prefH; }
    void set_PreferredBackBufferHeight(int32_t h) { prefH = h; }
    ::Microsoft::Xna::Framework::Graphics::GraphicsProfile get_GraphicsProfile() { return profile; }
    void set_GraphicsProfile(::Microsoft::Xna::Framework::Graphics::GraphicsProfile p) { profile = p; }
    bool get_SynchronizeWithVerticalRetrace() { return vsync; }
    void set_SynchronizeWithVerticalRetrace(bool b) { vsync = b; }
    bool get_IsFullScreen() { return fullScreen; }
    void set_IsFullScreen(bool b) { fullScreen = b; }
    bool get_HardwareModeSwitch() { return false; }
    void set_HardwareModeSwitch(bool) {}
    void ApplyChanges();
    void ToggleFullScreen() {}
};

}  // namespace Microsoft::Xna::Framework
