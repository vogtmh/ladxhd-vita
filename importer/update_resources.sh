#!/bin/zsh
# Refreshes resources/ for a new upstream release: patches and extra files from an upstream ladxhd_updated
# checkout, plus the hash list of the original v1.0.0 files (no game files are copied).
set -e
cd "${0:A:h}"
[[ $# -eq 2 ]] || { echo "usage: $0 <upstream ladxhd_updated folder> <v1.0.0 folder>"; exit 1; }
res="${1:A}/ladxhd_patcher_source_code/Resources"
v100="${2:A}"
[[ -f $res/patches_allplatforms.7z ]] || { echo "No patcher resources in $1"; exit 1; }
[[ $(md5 -q "$v100/Link's Awakening DX HD.exe") == f4adfba864b852908705ea6a18a48f18 ]] || { echo "$2 is not the original v1.0.0"; exit 1; }

out=resources
rm -rf $out
mkdir -p $out/patches $out/extra/Data/Icon $out/extra/Data/Achievements
# Data patches plus the compiled OpenGL XNB patches; the executable patch isn't needed.
bsdtar -xf $res/patches_allplatforms.7z -C $out/patches
bsdtar -xf $res/patches_macos_arm64_gl.7z -C $out/patches --include '*.xnb.vcdiff'
# Chinese isn't supported on the Vita.
for f in $out/patches/*(N); do
  [[ ${f:t:l} =~ '(^|_)chn[._]' ]] && rm $f
done
cp $res/file_targets_xnb.txt $out/file_targets.txt
# v1.0.0 dungeon3_1.map(.data): patched installs delete them, the patcher restores them from these.
cp $res/d3map $res/d3mapdata $out/
# Files the patcher adds that don't come from v1.0.0.
cp $res/Icon.{ico,bmp,png,svg} $out/extra/Data/Icon/
bsdtar -xf $res/achievements.7z -C $out/extra/Data/Achievements
(cd "$v100" && find Content Data -type f ! -name .DS_Store -print0 | sort -z | xargs -0 md5 -r) > $out/v100.md5
echo "$(ls $out/patches | wc -l | tr -d ' ') patches, $(wc -l < $out/v100.md5 | tr -d ' ') original files"
