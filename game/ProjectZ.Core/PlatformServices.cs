using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ProjectZ.InGame.Controls;

namespace ProjectZ
{
    public interface IPlatformDisplayConfiguration
    {
        int PreferredBackBufferWidth { get; }
        int PreferredBackBufferHeight { get; }
    }

    public interface IPlatformFileSystem
    {
        bool PackagedAssetExists(string path);
        bool PackagedAssetDirectoryExists(string path);
        Stream OpenPackagedAsset(string path);
        string[] ListPackagedAssets(string directory);
    }

    public interface IUserDataPaths
    {
        bool DefaultMapThreadLoad { get; }
        bool ShouldCreateModsDirs { get; }
        string UserDataRoot { get; }
        string ModsRoot { get; }
        string InternalModsRoot { get; }
        string SaveDirectory { get; }
        string SettingsFilePath { get; }
        string AdvancedFilePath { get; }
        string AchievementsFilePath { get; }
    }

    public interface ISharedSaveService
    {
        bool IsSupported { get; }
        bool HasAccess { get; }
        string SharedSaveDirectory { get; }
        string SharedRootDirectory { get; }
        void RequestAccess();
        void CopyFile(string sourcePath, string destinationPath);
        bool FileExists(string path);
        void DeleteFile(string path);
        void EnsureDirectory(string path);
        DateTime GetLastWriteTimeUtc(string path);
    }

    public interface IPlatformInput
    {
        bool HasTouchInput { get; }
        IReadOnlyList<PlatformTouch> Touches { get; }
        Vector2 RightStick { get; }
        bool IsButtonDown(CButtons button);
        bool WasButtonPressed(CButtons button);
        bool WasButtonReleased(CButtons button);
        bool ConsumeSelectPressed();
        void BeginFrame();
        KeyboardState FilterKeyboardState(KeyboardState state) => state;

        // Reports whether this platform's mouse coordinates can be relative to a stale or
        // wrong window origin (e.g. macOS resizing a fullscreen window for its notch safe
        // area while cached window geometry lags). When true, offset is subtracted from raw
        // client coordinates before any further mapping.
        bool TryGetRawMouseClientOffset(Game game, out Point offset);
    }

    public interface ITextInputService
    {
        void SetEnabled(bool enabled);
        void OnGameActivated();
    }

    public interface IPlatformWindow
    {
        bool SupportsFullscreenConfiguration { get; }
        bool SupportsInactiveWindowInput { get; }
        bool ForceFullscreen { get; }
        bool VerticalFlipBlur { get; }
        void Exit(Game game) => game.Exit();
    }

    public interface IPlatformWindowLifecycle
    {
        void Initialize(Game1 game);
        void ApplySavedSettings(Game1 game);
        void Update(Game1 game);
        void ApplyInitialFullscreen(Game1 game);
        void PreparePresentationReset(Game1 game);
        void ToggleFullscreen(Game1 game);
    }

    public interface IGraphicsCapabilities
    {
        bool UsePresentationParametersForSize { get; }
        bool NeedsPreferredBackBufferSynchronization { get; }
        bool CanCreateGraphicsResourcesOnWorkerThread { get; }
        bool SupportsBlendFunctionMax { get; }
        bool UseAnisotropicFiltering { get; }
    }

    public interface IPlatformPresentation
    {
        int MinimumHeight { get; }
        bool UseCompactMenus { get; }
        bool UseFullWindowHud { get; }
        int DefaultSequenceScaleAmplify { get; }
    }

    public interface IFileDialogService
    {
        bool TryOpen(string extension, string defaultPath, out string path);
        bool TrySave(string extension, string defaultPath, out string path);
        IReadOnlyList<string> OpenMultiple(string extension);
    }

    public interface IPhotoPrintService
    {
        bool IsSupported { get; }
        IReadOnlyList<string> GetPrinters();
        string DefaultPrinter { get; }

        // Spools on a worker thread; onComplete fires off the game thread.
        void Print(string printerName, PhotoPrintJob job, Action<bool, string> onComplete);
    }

    public readonly struct PhotoPrintJob
    {
        public PhotoPrintJob(byte[] rgba, int width, int height, string jobName,
                             int copies = 1, bool grayscale = false, float widthInches = 4f)
        {
            Rgba = rgba;
            Width = width;
            Height = height;
            JobName = jobName;
            Copies = copies;
            Grayscale = grayscale;
            WidthInches = widthInches;
        }

        // Straight from Texture2D.GetData<Color>: 4 bytes per pixel, R,G,B,A order.
        public byte[] Rgba { get; }
        public int Width { get; }
        public int Height { get; }
        public string JobName { get; }
        public int Copies { get; }
        public bool Grayscale { get; }
        public float WidthInches { get; }
    }

    public readonly struct PlatformTouch
    {
        public PlatformTouch(int id, Vector2 position, PlatformTouchState state)
        {
            Id = id;
            Position = position;
            State = state;
        }

        public int Id { get; }
        public Vector2 Position { get; }
        public PlatformTouchState State { get; }
    }

    public enum PlatformTouchState
    {
        Pressed,
        Moved,
        Released,
        Invalid
    }

    public sealed class PlatformDisplayConfiguration : IPlatformDisplayConfiguration
    {
        public PlatformDisplayConfiguration(int preferredBackBufferWidth, int preferredBackBufferHeight)
        {
            PreferredBackBufferWidth = preferredBackBufferWidth;
            PreferredBackBufferHeight = preferredBackBufferHeight;
        }

        public int PreferredBackBufferWidth { get; }
        public int PreferredBackBufferHeight { get; }
    }

    public sealed class LocalPlatformFileSystem : IPlatformFileSystem
    {
        private readonly string _baseDirectory;

        public LocalPlatformFileSystem(string baseDirectory = null)
        {
            _baseDirectory = baseDirectory ?? AppContext.BaseDirectory;
        }

        public bool PackagedAssetExists(string path) => File.Exists(ToPath(path));

        public bool PackagedAssetDirectoryExists(string path) => Directory.Exists(ToPath(path));

        public Stream OpenPackagedAsset(string path) => File.Open(ToPath(path), FileMode.Open, FileAccess.Read, FileShare.Read);

        public string[] ListPackagedAssets(string directory)
        {
            var path = ToPath(directory);
            return Directory.Exists(path)
                ? Directory.GetFileSystemEntries(path)
                    .Select(Path.GetFileName)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .ToArray()
                : [];
        }

        private string ToPath(string path)
        {
            var relativePath = path.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(_baseDirectory, relativePath));
        }
    }

    public sealed class LocalUserDataPaths : IUserDataPaths
    {
        public bool DefaultMapThreadLoad => true;
        public bool ShouldCreateModsDirs => true;
        public LocalUserDataPaths(string userDataRoot = null)
        {
            var workingDirectory = AppContext.BaseDirectory;
            var portable = File.Exists(Path.Combine(workingDirectory, "portable.txt"));
            UserDataRoot = userDataRoot ?? (portable
                ? workingDirectory
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Zelda_LA"));
            ModsRoot = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPIMAGE"))
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Zelda_LA", "Mods")
                : Path.Combine(workingDirectory, "Mods");
        }
        public string UserDataRoot { get; }
        public string ModsRoot { get; }
        public string InternalModsRoot => null;
        public string SaveDirectory => Path.Combine(UserDataRoot, "SaveFiles");
        public string SettingsFilePath => Path.Combine(UserDataRoot, "settings");
        public string AdvancedFilePath => Path.Combine(UserDataRoot, "advanced");
        public string AchievementsFilePath => Path.Combine(UserDataRoot, "achievements");
    }

    public sealed class UnavailableSharedSaveService : ISharedSaveService
    {
        public bool IsSupported => false;
        public bool HasAccess => false;
        public string SharedSaveDirectory => null;
        public string SharedRootDirectory => null;
        public void RequestAccess() { }
        public void CopyFile(string sourcePath, string destinationPath) => throw new PlatformNotSupportedException();
        public bool FileExists(string path) => false;
        public void DeleteFile(string path) => throw new PlatformNotSupportedException();
        public void EnsureDirectory(string path) => throw new PlatformNotSupportedException();
        public DateTime GetLastWriteTimeUtc(string path) => throw new PlatformNotSupportedException();
    }

    public sealed class NullPlatformInput : IPlatformInput
    {
        public bool HasTouchInput => false;
        public IReadOnlyList<PlatformTouch> Touches => [];
        public Vector2 RightStick => Vector2.Zero;
        public bool IsButtonDown(CButtons button) => false;
        public bool WasButtonPressed(CButtons button) => false;
        public bool WasButtonReleased(CButtons button) => false;
        public bool ConsumeSelectPressed() => false;
        public void BeginFrame() { }
        public bool TryGetRawMouseClientOffset(Game game, out Point offset)
        {
            offset = Point.Zero;
            return false;
        }
    }

    public sealed class NullTextInputService : ITextInputService
    {
        public void SetEnabled(bool enabled) { }
        public void OnGameActivated() { }
    }

    public sealed class DefaultPlatformWindow : IPlatformWindow
    {
        public DefaultPlatformWindow(bool supportsFullscreenConfiguration, bool supportsInactiveWindowInput, bool forceFullscreen = false)
        {
            SupportsFullscreenConfiguration = supportsFullscreenConfiguration;
            SupportsInactiveWindowInput = supportsInactiveWindowInput;
            ForceFullscreen = forceFullscreen;
        }

        public bool SupportsFullscreenConfiguration { get; }
        public bool SupportsInactiveWindowInput { get; }
        public bool ForceFullscreen { get; }
        public bool VerticalFlipBlur { get; }
    }

    public sealed class GraphicsCapabilities : IGraphicsCapabilities
    {
        public GraphicsCapabilities(bool usePresentationParametersForSize, bool canCreateGraphicsResourcesOnWorkerThread, bool supportsBlendFunctionMax, bool useAnisotropicFiltering, bool needsPreferredBackBufferSynchronization = true)
        {
            UsePresentationParametersForSize = usePresentationParametersForSize;
            NeedsPreferredBackBufferSynchronization = needsPreferredBackBufferSynchronization;
            CanCreateGraphicsResourcesOnWorkerThread = canCreateGraphicsResourcesOnWorkerThread;
            SupportsBlendFunctionMax = supportsBlendFunctionMax;
            UseAnisotropicFiltering = useAnisotropicFiltering;
        }

        public bool UsePresentationParametersForSize { get; }
        public bool NeedsPreferredBackBufferSynchronization { get; }
        public bool CanCreateGraphicsResourcesOnWorkerThread { get; }
        public bool SupportsBlendFunctionMax { get; }
        public bool UseAnisotropicFiltering { get; }
    }

    public sealed class PlatformPresentation : IPlatformPresentation
    {
        public PlatformPresentation(int minimumHeight, bool useCompactMenus, bool useFullWindowHud, int defaultSequenceScaleAmplify)
        {
            MinimumHeight = minimumHeight;
            UseCompactMenus = useCompactMenus;
            UseFullWindowHud = useFullWindowHud;
            DefaultSequenceScaleAmplify = defaultSequenceScaleAmplify;
        }

        public int MinimumHeight { get; }
        public bool UseCompactMenus { get; }
        public bool UseFullWindowHud { get; }
        public int DefaultSequenceScaleAmplify { get; }
    }

    public sealed class UnavailableFileDialogService : IFileDialogService
    {
        public bool TryOpen(string extension, string defaultPath, out string path)
        {
            path = null;
            return false;
        }

        public bool TrySave(string extension, string defaultPath, out string path)
        {
            path = null;
            return false;
        }

        public IReadOnlyList<string> OpenMultiple(string extension) => [];
    }

    public sealed class UnavailablePhotoPrintService : IPhotoPrintService
    {
        public bool IsSupported => false;
        public IReadOnlyList<string> GetPrinters() => [];
        public string DefaultPrinter => null;
        public void Print(string printerName, PhotoPrintJob job, Action<bool, string> onComplete)
            => onComplete?.Invoke(false, "Printing is not supported on this platform.");
    }
}
