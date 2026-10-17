# Link's Awakening DX HD for PS Vita

A native PS Vita port of **LADXHD Updated** (game version 2.0.9).

Original project: <https://gitlab.com/bighead.0/ladxhd_updated>
([releases](https://gitlab.com/bighead.0/ladxhd_updated/-/releases),
[manual](https://gitlab.com/bighead.0/ladxhd_updated/-/blob/main/MANUAL.md),
[changelog](https://gitlab.com/bighead.0/ladxhd_updated/-/blob/main/CHANGELOG.md)).
Please report bugs in the game itself there, and Vita-specific problems here.

**This repository does not include any copyrighted game assets.** You need your own copy of the game.

## Installing

### What you need

- A PS Vita with HENkaku/Ensō and [VitaShell](https://github.com/TheOfficialFloW/VitaShell),
  with unsafe homebrew enabled in HENkaku settings.
- One of these copies of Link's Awakening DX HD on your computer:
  - the original **v1.0.0** release, or
  - a **Windows, Linux or macOS** copy already patched to **v1.1.4 or newer**
    with the upstream patcher. It must still have its `Data/Backup` folder,
    because the patcher keeps the original files there.
  
  Android copies don't work because they only contain the APK.

### Creating the VPK

1. Download the importer for your system from the releases page and unpack it:
   - `macos-universal.zip`: an app for Apple silicon and Intel Macs (macOS 11 or newer). The app isn't
     notarized, so the first time, right-click it, choose **Open** and confirm.
   - `windows-x86_64.zip` / `windows-arm64.zip`: Windows 10 or newer.
   - `linux-x86_64.tar.gz` / `linux-arm64.tar.gz`: glibc 2.28 or newer, X11 or Wayland.
2. Start **LADXHD Vita Importer**.
3. Click **Browse…** and select the game folder. You can select any of these:
   - the folder containing `Content` and `Data`,
   - a macOS `Link's Awakening DX HD.app`,
   - a release download that has one subfolder per variant (DirectX, OpenGL, …).
4. Click **Create VPK…** and choose where to save it.

The importer checks every original file against the v1.0.0 hashes. It then patches the files to v2.0.9
and writes a VPK containing the game and its data, about 25 MB. Your game folder is never modified. If files
are missing or modified, the importer lists them instead of creating a broken VPK.

The importer also works from the command line:

```
ladxhd-vita-importer "<game folder>" -o LADXHD-Vita-v2.0.9.vpk
```

### Artwork

The home screen pictures (bubble icon, LiveArea background and start button) are not part of this
repository. The importer gets them when it creates the VPK:

1. By default it downloads chosen images from [SteamGridDB](https://www.steamgriddb.com/game/5439861):
   an icon and a logo by Orion1189, and a hero image by SeeDborg (mirrored). Downloads are cached.
2. Without internet, it builds the pictures from the game's own intro graphics in your copy.
3. The importer window shows all three pictures and lets you change them:
   - **Image file…** uses a picture from your computer.
   - **Place** puts the start button on the right (default) or in the centre. The Vita has no
     other positions. The background is moved so the egg is not covered; **Position** changes this.

On the command line, use `--bubble`, `--background` and `--startup` with an image file or an
`https://` image link. `--button centre|right` sets the start button and
`--bg-position 0..1` the background crop (0 = left edge, 1 = right edge).

### Installing on the Vita

1. Copy the VPK to the Vita, for example to `ux0:` via USB or FTP in VitaShell.
2. Select it in VitaShell and confirm the installation.
3. Start the game from the LiveArea. The bubble is called *Link's Awakening DX HD*, title ID `LADX00001`.

To update, install a newer VPK over the old one. Saves and settings are kept.

### Notes

- Chinese is not available on the Vita: its two font textures (about 12 MB) would be loaded at every start,
  whatever language is selected. All other languages are available.
- Saves, settings and a log file (`log.txt`) are stored in `ux0:data/ladxhd/`.
- *Settings → Video → Performance Overlay* shows frame times on screen.

## Repository layout

| Folder      | Contents |
|-------------|----------|
| `game/`     | Upstream game code (`ProjectZ.Core` from v2.0.9, unmodified). |
| `vita/`     | The port: C#-to-C++ transpiler, runtime (Boehm GC), MonoGame subset, Vita/SDL platform code, port patches. |
| `importer/` | Rust/egui app that turns the original release into the VPK. `resources/` holds the upstream v2.0.9 patches and the hashes of the v1.0.0 files. |
| `local/`    | Ignored by git. Put your game copy and other local data here. |

## Building

Requirements:

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- CMake
- SDL2 (desktop build only)
- [VitaSDK](https://vitasdk.org) with vitaGL (`vdpm install vitaGL`)
- [Rust](https://rustup.rs)

`package_vita.sh` looks for VitaSDK in `$VITASDK` (default `~/.local/share/celtic-vitasdk`).

```sh
# 1. Translate the game code to C++ (repeat after changing game/ or vita/port/patches)
dotnet restore game/ProjectZ.Core                # NuGet references used by the transpiler
cd vita/transpiler && dotnet build -c Release && dotnet bin/Release/net9.0/Cs2Cpp.dll gen && cd ..

# 2. Desktop build for fast testing (game data comes from the importer)
cmake -S . -B build/desktop && cmake --build build/desktop -j
../importer/target/release/ladxhd-vita-importer "<game folder>" --tree build/desktop/game
./build/desktop/ladxhd

# 3. Vita build: eboot, then the importer (which embeds it).
#    With LADX_GAME set it also writes build/vita/ladxhd.vpk
#    (default: local/Links Awakening DX HD v1.0.0).
LADX_GAME="<game folder>" ./tools/package_vita.sh
```

The importer binary ends up in `importer/target/release/ladxhd-vita-importer`. It needs the Vita build
from step 3 at compile time; without it, it can only write game files (`--tree`).

`importer/release.sh` builds the release archives for macOS, Windows and Linux (x86_64 and arm64) into
`dist/<version>/`. It runs on macOS and needs the Vita build from step 3, the Rust targets
(`rustup target add x86_64-apple-darwin x86_64-pc-windows-gnu aarch64-pc-windows-gnullvm
aarch64-unknown-linux-gnu x86_64-unknown-linux-gnu`), [zig](https://ziglang.org) and
[cargo-zigbuild](https://github.com/rust-cross/cargo-zigbuild).

Changes to the game code go into `vita/port/patches/*.txt` (applied by the transpiler) or
`vita/port/bodies/` (C++ replacements for single methods), never into `game/`.

## Updating to a new upstream release

1. Replace `game/ProjectZ.Core` with the version from the new upstream release.
   Update `GameVersion` in `game/Directory.Build.props`.
2. Run `importer/update_resources.sh <upstream checkout> <v1.0.0 folder>` to refresh the patches and hashes.
3. Regenerate, build and test as above.

## Credits

The game is the work of the [LADXHD Updated](https://gitlab.com/bighead.0/ladxhd_updated) project and its
contributors. This repository only adds the Vita port and the importer.

## License

The [MIT license](LICENSE) covers only the code written for this port: `vita/` (except `vita/third_party/`),
`importer/` (except `importer/resources/`) and the build scripts.

It does not apply to:

- `game/`: code from LADXHD Updated, which is published without a license file.
- `importer/resources/`: patches, icons and achievement images from LADXHD Updated.
  Its v1.0.0 hash list (`v100.md5`) contains only file hashes.
- `vita/third_party/`: Boehm GC and stb, each under its own license.
- The game itself: Link's Awakening is © Nintendo. No game assets are included; you need your own copy.
- SteamGridDB images belong to their uploaders and rights holders. They are downloaded by the importer
  on your computer and are not stored in this repository.
