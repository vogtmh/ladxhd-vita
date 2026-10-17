#!/bin/zsh
# Run the desktop build under lldb for up to N seconds (default 30) and print the crash backtrace.
# Game data: LADX_ROOT, else build/desktop/game (importer --tree output).
cd "${0:A:h}/.."
lldb --batch -o 'process launch' -o 'bt 40' -o 'kill' -o 'quit' -- ./build/desktop/ladxhd "$@" > /tmp/lldb.log 2>&1 &
PID=$!
for i in $(seq 1 ${DEBUG_SECONDS:-30}); do
  kill -0 $PID 2>/dev/null || break
  sleep 1
done
kill $PID 2>/dev/null
pkill -f build/desktop/ladxhd 2>/dev/null
grep -v '^\s*$' /tmp/lldb.log | grep -E 'stop reason|frame #|Unhandled|error|exception' | cut -c1-240 | head -50
