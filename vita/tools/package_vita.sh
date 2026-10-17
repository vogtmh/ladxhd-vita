#!/bin/zsh
# Builds the Vita eboot, stages it in build/vita/vpk and builds the importer (../importer), which embeds the stage.
# If LADX_GAME points to a v1.0.0 (or v1.1.4+ patched) game folder, the importer also writes build/vita/ladxhd.vpk.
set -e
cd "${0:A:h}/.."
: ${VITASDK:=$HOME/.local/share/celtic-vitasdk}
export VITASDK PATH="$VITASDK/bin:$HOME/.cargo/bin:$PATH"
: ${LADX_GAME:="$PWD/../local/Links Awakening DX HD v1.0.0"}
version=$(sed -n 's:.*<GameVersion>\(.*\)</GameVersion>.*:\1:p' ../game/Directory.Build.props)
v=(${(s:.:)version})
app_ver=$(printf "%02d.%d%d" $v[1] $v[2] $v[3])

cmake -S . -B build/vita -DVITA_BUILD=ON -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_CXX_FLAGS_RELEASE="-O2 -DNDEBUG" -DCMAKE_C_FLAGS_RELEASE="-O2 -DNDEBUG" >/dev/null
cmake --build build/vita -j10

stage=build/vita/vpk
rm -rf "$stage"
mkdir -p "$stage/sce_sys"
cp build/vita/eboot.bin "$stage/"
# LiveArea artwork and template are added by the importer.
# Precompiled shaders copied back from a device (ux0:data/ladxhd/gxp) skip the on-device compiler.
[[ -d port/vita/gxp ]] && cp -R port/vita/gxp "$stage/gxp"
vita-mksfoex -s TITLE_ID=LADX00001 -s APP_VER=$app_ver -d ATTRIBUTE2=12 "Link's Awakening DX HD" "$stage/sce_sys/param.sfo"
find "$stage" -name .DS_Store -delete

cargo build --release --manifest-path ../importer/Cargo.toml
if [[ -d "$LADX_GAME" ]]; then
  ../importer/target/release/ladxhd-vita-importer "$LADX_GAME" -o build/vita/ladxhd.vpk
  ls -la build/vita/ladxhd.vpk
fi
