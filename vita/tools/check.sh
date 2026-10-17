#!/bin/zsh
# Regenerate C++ from the game and syntax-check all generated files; prints an error summary.
set -e
cd "${0:A:h}/.."
~/.dotnet/dotnet transpiler/bin/Release/net9.0/Cs2Cpp.dll gen
cd build/gen
ls *.cpp | xargs -P 10 -I{} sh -c 'clang++ -std=gnu++20 -fsyntax-only -ferror-limit=0 -Wno-everything -I../../runtime/include -I../../framework/include -I. -I../../third_party/bdwgc/include {} > {}.err 2>&1 || true'
cat *.err | grep -E 'error:' | sed -E 's/^[^ ]+:[0-9]+:[0-9]+: //' | sort | uniq -c | sort -rn > ../errors_summary.txt
echo "errors: $(cat *.err | grep -c 'error:')"
head -${1:-40} ../errors_summary.txt
