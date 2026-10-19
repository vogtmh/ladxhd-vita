#!/bin/zsh
# Builds the importer for macOS (universal app), Linux and Windows (x86_64 + arm64) into dist/<release>/.
# Usage: release.sh [release name, e.g. 2.0.9c; defaults to vita/RELEASE_VERSION]
# Builds and stages the Vita executable first; needs the rustup targets and zig + cargo-zigbuild.
set -e
here=${0:A:h}
cd $here
export PATH="$HOME/.cargo/bin:$PATH"
release=${1:-$(< ../vita/RELEASE_VERSION)}
../vita/tools/package_vita.sh "$release"
name=ladxhd-vita-importer
dist=${here:h}/dist/$release
rm -rf $dist
mkdir -p $dist

# macOS: one app bundle for Apple silicon and Intel.
cargo build --release --target aarch64-apple-darwin
cargo build --release --target x86_64-apple-darwin
stage=$(mktemp -d)
app="$stage/LADXHD Vita Importer.app"
mkdir -p "$app/Contents/MacOS"
lipo -create target/{aarch64,x86_64}-apple-darwin/release/$name -output "$app/Contents/MacOS/$name"
cat > "$app/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleExecutable</key><string>$name</string>
  <key>CFBundleIdentifier</key><string>io.github.vogtmh.ladxhd-vita-importer</string>
  <key>CFBundleName</key><string>LADXHD Vita Importer</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>$release</string>
  <key>CFBundleVersion</key><string>$release</string>
  <key>LSMinimumSystemVersion</key><string>11.0</string>
  <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
EOF
codesign --force --sign - "$app"
ditto -c -k --keepParent "$app" $dist/$name-$release-macos-universal.zip
rm -rf $stage

# Linux: glibc 2.28 or newer (Debian 10, Ubuntu 18.10, RHEL 8).
for arch in x86_64 aarch64; do
  cargo zigbuild --release --target $arch-unknown-linux-gnu.2.28
  tar -czf $dist/$name-$release-linux-${arch/aarch64/arm64}.tar.gz -C target/$arch-unknown-linux-gnu/release $name
done

# Windows 10 or newer. x86_64 uses MinGW (brew install mingw-w64); there's no arm64 MinGW, so zig builds that.
CARGO_TARGET_X86_64_PC_WINDOWS_GNU_LINKER=x86_64-w64-mingw32-gcc cargo build --release --target x86_64-pc-windows-gnu
cargo zigbuild --release --target aarch64-pc-windows-gnullvm
for target in x86_64-pc-windows-gnu aarch64-pc-windows-gnullvm; do
  arch=${target%%-*}
  (cd target/$target/release && zip -q $dist/$name-$release-windows-${arch/aarch64/arm64}.zip $name.exe)
done

(cd $dist && shasum -a 256 *.zip *.tar.gz > SHA256SUMS.txt)
ls -lh $dist
