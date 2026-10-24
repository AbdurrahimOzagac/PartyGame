#!/usr/bin/env bash
# End-to-end test: a dedicated game server + 3 self-playing clients over WebSocket.
#
#   e2e_local.sh [basic|leave-during-load] <desktop game binary>
#   E2E_REMOTE=<ssh-host> E2E_SERVER_DIR=Builds/PiServer e2e_local.sh basic <desktop game binary>
#   e2e_local.sh rooms <desktop game binary>     the room service starts the server; joins go through its HTTP API
#   e2e_local.sh drop <desktop game binary>      rooms + Cem's connection drops 8 s into round 1; it must rejoin (token)
#   e2e_local.sh drop-all <desktop game binary>  rooms + everyone drops at once (a tunnel blip); all rejoin, the session goes on
#
# Local: the same executable runs as server (-party-server) and clients (-autopilot).
# Remote: the server build folder is copied to E2E_REMOTE (over SSH) and run there; the clients here reach it
# through an SSH port forward (private, nothing is opened to the internet). Timers run at 1/4 speed.
# Exit 0 = the session reached its final with consistent scores (e2e_check.py). Logs stay in $E2E_OUT.
set -u
SCENARIO=${1:-basic}
BIN=${2:-${SHOOTERPARTY_BIN:-}}
[ -x "$BIN" ] || { echo "usage: $0 [basic|leave-during-load|rooms|drop|drop-all] <game binary>"; exit 2; }
ROOMS=; case "$SCENARIO" in rooms|drop|drop-all) ROOMS=1 ;; esac
HERE=$(cd "$(dirname "$0")" && pwd)
OUT=${E2E_OUT:-$(mktemp -d -t shooterparty-e2e)}; mkdir -p "$OUT"
REMOTE=${E2E_REMOTE:-}
PORT=${E2E_PORT:-$([ -n "$REMOTE" ] && echo 7801 || echo 7701)}   # remote: stay off the live rooms' 7701/7702
REMOTE_DIR=${E2E_REMOTE_DIR:-shooterparty_e2e}
SECRET=$(python3 -c 'import secrets; print(secrets.token_hex(32))')
ROOM=BCDFG
NAMES=(Ann Bea Cem)
PIDS=()
SERVER_ARGS="-batchmode -nographics -logFile - -party-server -slot 1 -port $PORT -wsPath /g/1 -room $ROOM -minPlayers 3 -timeScale 0.25"

cleanup() {
  for p in ${PIDS[@]+"${PIDS[@]}"}; do kill "$p" 2>/dev/null; done
  [ -n "$REMOTE" ] && ssh -o BatchMode=yes "$REMOTE" "pkill -f 'ShooterPartyServer -batchmode.*-port $PORT ' || true"
}
trap cleanup EXIT
echo "logs: $OUT"

if [ -n "$ROOMS" ]; then
  RPORT=$((PORT + 2000)); mkdir -p "$OUT/rooms"; printf '%s' "$SECRET" > "$OUT/secret"
  python3 "$HERE/rooms/room_service.py" --server-cmd "'$BIN' -timeScale 0.25" --secret-file "$OUT/secret" \
    --listen 127.0.0.1:$RPORT --status-listen 127.0.0.1:$((RPORT + 1)) --slot-base-port $PORT \
    --ws-url-template "ws://127.0.0.1:{port}/g/{slot}" --maintenance-file "$OUT/maintenance" --server-log-dir "$OUT/rooms" \
    > "$OUT/rooms.log" 2>&1 &
  PIDS+=($!)
  sleep 1
  api() { curl -s -H 'X-ShooterParty-Version: 1' -H 'Content-Type: application/json' -X POST -d "$2" "http://127.0.0.1:$RPORT/api/v1$1"; }
  field() { python3 -c "import json,sys; print(json.load(sys.stdin).get('$1',''))"; }
  ROOMS_URL="http://127.0.0.1:$RPORT/api/v1"
  server_out() { cat "$OUT"/rooms/*.log > "$OUT/server.out" 2>/dev/null; }
  server_alive() { ls "$OUT"/rooms/*.log >/dev/null 2>&1 && ! grep -q server_exited "$OUT/rooms.log"; }
elif [ -n "$REMOTE" ]; then
  [ -d "${E2E_SERVER_DIR:-}" ] || { echo "set E2E_SERVER_DIR to the Pi server build folder"; exit 2; }
  rsync -az --delete --exclude '*_BackUpThisFolder_ButDontShipItWithYourGame' --exclude '*_DoNotShip' \
    -e "ssh -o BatchMode=yes" "$E2E_SERVER_DIR/" "$REMOTE:$REMOTE_DIR/"
  ssh -o BatchMode=yes "$REMOTE" "cd $REMOTE_DIR && chmod +x ShooterPartyServer && rm -f server.out && \
    (PARTY_TICKET_SECRET=$SECRET nohup ./ShooterPartyServer $SERVER_ARGS > server.out 2>&1 &)"
  LPORT=$((PORT + 10000))
  ssh -o BatchMode=yes -o ExitOnForwardFailure=yes -N -L "$LPORT:127.0.0.1:$PORT" "$REMOTE" & PIDS+=($!)
  server_out() { ssh -o BatchMode=yes "$REMOTE" "cat $REMOTE_DIR/server.out" > "$OUT/server.out"; }
  PEAK_KB=0
  server_alive() {   # also samples the server's memory (RSS) on the remote
    local kb; kb=$(ssh -o BatchMode=yes "$REMOTE" "ps -o rss= -p \$(pgrep -f 'ShooterPartyServer -batchmode.*-port $PORT ' | head -1) 2>/dev/null") || return 1
    kb=${kb//[^0-9]/}; [ -n "$kb" ] || return 1
    [ "$kb" -gt "$PEAK_KB" ] && PEAK_KB=$kb
    return 0
  }
  URL="ws://127.0.0.1:$LPORT/g/1"
else
  # -logFile - : Unity's log and the @@ protocol lines both go to stdout (with a log file Unity swallows stdout)
  PARTY_TICKET_SECRET=$SECRET "$BIN" $SERVER_ARGS > "$OUT/server.out" 2>&1 &
  SERVER=$!; PIDS+=($SERVER)
  server_out() { :; }
  server_alive() { kill -0 $SERVER 2>/dev/null; }
  URL="ws://127.0.0.1:$PORT/g/1"
fi

if [ -n "$ROOMS" ]; then
  # the game's own client creates the room; the others join with the code it got
  first=(); [ "$SCENARIO" = drop-all ] && first=(-bridge -dropAt 8)
  "$BIN" -batchmode -nographics -logFile "$OUT/client0.log" -autopilot -roomService "$ROOMS_URL" -create -name Ann \
    ${first[@]+"${first[@]}"} > /dev/null 2>&1 &
  PIDS+=($!)
  for _ in $(seq 60); do CODE=$(sed -n 's/.*\[Autopilot\] room \([A-Z]*\).*/\1/p' "$OUT/client0.log" 2>/dev/null | head -1); [ -n "$CODE" ] && break; sleep 1; done
  [ -n "$CODE" ] || { echo "the first client never got a room"; grep Autopilot "$OUT/client0.log"; exit 1; }
  echo "  room $CODE"
  for i in 1 2; do
    extra=()
    [ "$SCENARIO" = drop ] && [ "$i" = 2 ] && extra=(-bridge -dropAt 8)   # plain ws through the bridge, so it can cut it
    [ "$SCENARIO" = drop-all ] && extra=(-bridge -dropAt 8)
    "$BIN" -batchmode -nographics -logFile "$OUT/client$i.log" -autopilot -roomService "$ROOMS_URL" -code "$CODE" -name "${NAMES[$i]}" \
      ${extra[@]+"${extra[@]}"} > /dev/null 2>&1 &
    PIDS+=($!); sleep 2
  done
fi

for _ in $(seq 60); do server_out; grep -q '^@@READY' "$OUT/server.out" && break; sleep 1; done
grep -q '^@@READY' "$OUT/server.out" || { echo "server never got ready"; tail -20 "$OUT/server.out"; exit 1; }

for i in 0 1 2; do
  [ -n "$ROOMS" ] && break
  extra=()
  [ "$i" = 0 ] && lead=--leader || lead=
  case "$SCENARIO" in
    leave-during-load) [ "$i" = 2 ] && extra=(-leaveAt Loading) ;;
  esac
  ticket=$(python3 "$HERE/rooms/make_ticket.py" --secret-hex "$SECRET" --room $ROOM --pid "p$i" --name "${NAMES[$i]}" $lead --ttl 120)
  "$BIN" -batchmode -nographics -logFile "$OUT/client$i.log" -autopilot \
    -join "$URL" -ticket "$ticket" -name "${NAMES[$i]}" ${extra[@]+"${extra[@]}"} > /dev/null 2>&1 &
  PIDS+=($!)
  sleep 2   # the first to join leads the lobby
done

for _ in $(seq 120); do server_out; grep -q '^@@END' "$OUT/server.out" && break; server_alive || break; sleep 3; done
server_out
grep '^@@STATE' "$OUT/server.out" | sed 's/^/  /'
grep '^@@STATS' "$OUT/server.out" | tail -3 | sed 's/^/  /'
[ -n "$REMOTE" ] && echo "  server peak RSS on $REMOTE: $(( PEAK_KB / 1024 )) MB" 
check=(); case "$SCENARIO" in drop|drop-all) check=(--expect-rejoin) ;; esac
python3 "$HERE/e2e_check.py" "$OUT/server.out" --humans 3 --rounds 2 ${check[@]+"${check[@]}"}
RC=$?
if [ "$SCENARIO" = drop ]; then
  grep -h "\[Autopilot\] \(dropping\|disconnected\|join flow Rejoining\|rejoined\)" "$OUT/client2.log" | sed 's/^/  Cem: /'
  grep -q "\[Autopilot\] rejoined room" "$OUT/client2.log" || { echo "FAIL: Cem never rejoined"; RC=1; }
fi
if [ "$SCENARIO" = drop-all ]; then
  for i in 0 1 2; do grep -q "\[Autopilot\] rejoined room" "$OUT/client$i.log" || { echo "FAIL: ${NAMES[$i]} never rejoined"; RC=1; }; done
fi
grep -h -i -E "exception|error" "$OUT"/client*.log "$OUT/server.out" | grep -v -E "Licensing|LogAssemblyErrors|IntegrityCheck|errors=0|0 errors" | sort | uniq -c | head -10
exit $RC
