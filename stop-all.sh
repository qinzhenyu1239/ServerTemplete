#!/usr/bin/env bash
set -euo pipefail

# Stop the servers started by start-all.sh in this deployment directory.
BASE_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
mkdir -p "$BASE_DIR/.pids"
exec 9>"$BASE_DIR/.pids/start.lock"
flock -n 9 || { echo "Another start/stop operation is in progress." >&2; exit 1; }

failed=0
for role in Game Login ServerZone; do
    exe="$(readlink -f "$BASE_DIR/$role/GameServer" || true)"
    pid_file="$BASE_DIR/.pids/$role.pid"
    input_file="$BASE_DIR/.pids/$role.stdin"

    if [[ ! -f "$pid_file" ]]; then
        echo "[$role] No saved PID; skipped."
        continue
    fi
    pid="$(cat -- "$pid_file")"

    matches_server() {
        [[ "$pid" =~ ^[0-9]+$ ]] && [[ "$pid" -gt 1 ]] || return 1
        local actual
        actual="$(readlink "/proc/$pid/exe" 2>/dev/null || true)"
        # An executable replaced during deployment can have this Linux suffix.
        [[ -n "$actual" && "${actual% (deleted)}" == "$exe" ]]
    }

    if ! matches_server; then
        echo "[$role] Saved process is gone or no longer matches; skipped."
        rm -f -- "$pid_file"
        continue
    fi

    echo "[$role] Stopping PID=$pid ..."
    # A newline releases Console.ReadLine() and invokes the normal Shutdown().
    if [[ -p "$input_file" ]]; then
        timeout 2s bash -c 'printf "\n" > "$1"' _ "$input_file" 9>&- || true
    fi
    for ((attempt = 0; attempt < 15; attempt++)); do
        matches_server || break
        sleep 1
    done

    if matches_server; then
        echo "[$role] Shutdown timed out; sending SIGTERM."
        kill -TERM -- "$pid" 2>/dev/null || true
        for ((attempt = 0; attempt < 5; attempt++)); do
            matches_server || break
            sleep 1
        done
    fi

    if matches_server; then
        echo "[$role] Still running. Check logs/$role.log (PID=$pid)." >&2
        failed=1
    else
        rm -f -- "$pid_file"
        echo "[$role] Stopped."
    fi
done

exit "$failed"
