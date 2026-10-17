using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ProjectZ.InGame.Things
{
    internal static class GameFS
    {
        private static IPlatformFileSystem PlatformFileSystem => Game1.PlatformFileSystem;

        public static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            return path.Replace('\\', '/');
        }

        private static bool IsPackagedAssetPath(string path)
        {
            path = NormalizePath(path).TrimStart('/');
            return path.Equals("Data", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("Data/", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("Content", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("Content/", StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(Game1.UserDataPaths.InternalModsRoot) &&
                 (path.Equals(Game1.UserDataPaths.InternalModsRoot, StringComparison.OrdinalIgnoreCase) ||
                  path.StartsWith(Game1.UserDataPaths.InternalModsRoot + "/", StringComparison.OrdinalIgnoreCase)));
        }

        private static bool IsRealFileSystemPath(string path) => !string.IsNullOrWhiteSpace(path) &&
            !IsPackagedAssetPath(path) && Path.IsPathRooted(path);

        public static string ToAssetPath(string path)
        {
            path = NormalizePath(path);
            if (string.IsNullOrEmpty(path) || !IsRealFileSystemPath(path))
                return path.TrimStart('/');

            foreach (var assetRoot in new[] { "/Data/", "/Content/" })
            {
                var index = path.IndexOf(assetRoot, StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                    return path[(index + 1)..];
            }

            return path;
        }

        public static string ReadAllText(string path)
        {
            using var stream = OpenRead(path);
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }

        public static string[] ReadAllLines(string path) => ReadAllText(path)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n');

        public static bool Exists(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            path = NormalizePath(path);
            return IsRealFileSystemPath(path)
                ? File.Exists(path)
                : PlatformFileSystem.PackagedAssetExists(ToAssetPath(path));
        }

        public static Stream OpenRead(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or empty.", nameof(path));

            path = NormalizePath(path);
            return IsRealFileSystemPath(path)
                ? File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)
                : PlatformFileSystem.OpenPackagedAsset(ToAssetPath(path));
        }

        public static Stream OpenReadAny(string path) => OpenRead(path);

        public static string[] List(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                return [];

            directory = NormalizePath(directory);
            if (IsRealFileSystemPath(directory))
            {
                return Directory.Exists(directory)
                    ? Directory.EnumerateFileSystemEntries(directory).Select(Path.GetFileName).Where(name => !string.IsNullOrEmpty(name)).ToArray()
                    : [];
            }

            return PlatformFileSystem.ListPackagedAssets(ToAssetPath(directory));
        }

        public static bool IsDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                return false;

            directory = NormalizePath(directory);
            return IsRealFileSystemPath(directory)
                ? Directory.Exists(directory)
                : PlatformFileSystem.PackagedAssetDirectoryExists(ToAssetPath(directory));
        }

        public static IEnumerable<string> EnumerateFiles(string directory, bool recursive, Func<string, bool> acceptFile, Func<string, bool> skipDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(directory))
                yield break;

            directory = NormalizePath(directory);
            if (IsRealFileSystemPath(directory))
            {
                foreach (var file in EnumerateRealFiles(directory, recursive, acceptFile, skipDirectory))
                    yield return file;
                yield break;
            }

            foreach (var file in EnumeratePackagedFiles(ToAssetPath(directory), recursive, acceptFile, skipDirectory))
                yield return file;
        }

        public static IEnumerable<string> EnumerateDirectories(string directory, bool recursive, Func<string, bool> acceptDirectory = null, Func<string, bool> skipDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(directory))
                yield break;

            directory = NormalizePath(directory);
            if (IsRealFileSystemPath(directory))
            {
                foreach (var subDirectory in EnumerateRealDirectories(directory, recursive, acceptDirectory, skipDirectory))
                    yield return subDirectory;
                yield break;
            }

            foreach (var subDirectory in EnumeratePackagedDirectories(ToAssetPath(directory), recursive, acceptDirectory, skipDirectory))
                yield return subDirectory;
        }

        private static IEnumerable<string> EnumerateRealFiles(string directory, bool recursive, Func<string, bool> acceptFile, Func<string, bool> skipDirectory)
        {
            if (!Directory.Exists(directory))
                yield break;

            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var name = Path.GetFileName(entry);
                if (Directory.Exists(entry))
                {
                    if (recursive && (skipDirectory == null || !skipDirectory(name)))
                        foreach (var file in EnumerateRealFiles(entry, true, acceptFile, skipDirectory))
                            yield return file;
                    continue;
                }

                if (acceptFile == null || acceptFile(name))
                    yield return NormalizePath(entry);
            }
        }

        private static IEnumerable<string> EnumerateRealDirectories(string directory, bool recursive, Func<string, bool> acceptDirectory, Func<string, bool> skipDirectory)
        {
            if (!Directory.Exists(directory))
                yield break;

            foreach (var subDirectory in Directory.EnumerateDirectories(directory))
            {
                var name = Path.GetFileName(subDirectory);
                if (skipDirectory != null && skipDirectory(name))
                    continue;
                if (acceptDirectory == null || acceptDirectory(name))
                    yield return NormalizePath(subDirectory);
                if (recursive)
                    foreach (var sub in EnumerateRealDirectories(subDirectory, true, acceptDirectory, skipDirectory))
                        yield return sub;
            }
        }

        private static IEnumerable<string> EnumeratePackagedFiles(string directory, bool recursive, Func<string, bool> acceptFile, Func<string, bool> skipDirectory)
        {
            foreach (var name in List(directory))
            {
                var path = string.IsNullOrEmpty(directory) ? name : $"{directory}/{name}";
                if (IsDirectory(path))
                {
                    if (recursive && (skipDirectory == null || !skipDirectory(name)))
                        foreach (var file in EnumeratePackagedFiles(path, true, acceptFile, skipDirectory))
                            yield return file;
                    continue;
                }

                if (acceptFile == null || acceptFile(name))
                    yield return path;
            }
        }

        private static IEnumerable<string> EnumeratePackagedDirectories(string directory, bool recursive, Func<string, bool> acceptDirectory, Func<string, bool> skipDirectory)
        {
            foreach (var name in List(directory))
            {
                var path = string.IsNullOrEmpty(directory) ? name : $"{directory}/{name}";
                if (!IsDirectory(path))
                    continue;
                if (skipDirectory != null && skipDirectory(name))
                    continue;
                if (acceptDirectory == null || acceptDirectory(name))
                    yield return path;
                if (recursive)
                    foreach (var subDirectory in EnumeratePackagedDirectories(path, true, acceptDirectory, skipDirectory))
                        yield return subDirectory;
            }
        }

        public static byte[] ReadAllBytes(Stream stream)
        {
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }

        public static byte[] ReadAllBytes(string path)
        {
            using var stream = OpenRead(path);
            return ReadAllBytes(stream);
        }
    }
}
