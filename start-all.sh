#!/usr/bin/env bash
set -euo pipefail

# Place this script beside the Game, Login and ServerZone directories.
BASE_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
mkdir -p "$BASE_DIR/logs" "$BASE_DIR/.pids"

# Prevent simultaneous invocations; children must not inherit this lock.
exec 9>"$BASE_DIR/.pids/start.lock"
flock -n 9 || { echo "Another startup is in progress." >&2; exit 1; }

failed=0
for role in ServerZone Login Game; do
    dir="$BASE_DIR/$role"
    exe="$dir/GameServer"
    pid_file="$BASE_DIR/.pids/$role.pid"
    input_file="$BASE_DIR/.pids/$role.stdin"
    log_file="$BASE_DIR/logs/$role.log"

    if [[ ! -f "$exe" ]]; then
        echo "[$role] Missing executable: $exe" >&2
        failed=1
        continue
    fi

    if [[ -f "$pid_file" ]]; then
        pid="$(cat -- "$pid_file")"
        if [[ "$pid" =~ ^[0-9]+$ ]] && kill -0 "$pid" 2>/dev/null &&
            [[ "$(readlink -f "/proc/$pid/exe" 2>/dev/null || true)" == "$exe" ]]; then
            echo "[$role] Already running (PID=$pid)."
            continue
        fi
    fi

    if ! chmod u+x "$exe"; then
        echo "[$role] Cannot grant execute permission." >&2
        failed=1
        continue
    fi

    # Main waits on Console.ReadLine(). An open read/write FIFO prevents EOF
    # without a separate helper process. This relies on Linux FIFO semantics.
    if [[ ! -p "$input_file" ]]; then
        mkfifo -- "$input_file"
    fi

    # Each server needs its own working directory for config and data files.
    (
        cd -- "$dir"
        exec nohup "$exe" 8<>"$input_file" <&8 8>&- >>"$log_file" 2>&1 9>&-
    ) &
    pid=$!
    printf '%s\n' "$pid" >"$pid_file"
    sleep 2

    if kill -0 "$pid" 2>/dev/null; then
        echo "[$role] Process started (PID=$pid). Log: $log_file"
    else
        echo "[$role] Process exited. Recent log:" >&2
        tail -n 20 -- "$log_file" >&2
        rm -f -- "$pid_file"
        failed=1
    fi
done

# A live process does not guarantee that its ports and dependencies are ready.
exit "$failed"
