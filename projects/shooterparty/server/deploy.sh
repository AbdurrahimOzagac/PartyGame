#!/usr/bin/env bash
# Deploys ShooterParty's server side to a Linux ARM64 host (the Pi) over SSH. See Assets/Ezel/ShooterParty/Docs/SERVER_OPS.md.
#
#   deploy.sh <ssh-host> [server-build-dir]   install or update: room service + Unity game server build
#   deploy.sh <ssh-host> --tunnel             once, after `cloudflared tunnel login` on the host: create the
#                                             partyshooter tunnel and its DNS name, install config + unit, start it,
#                                             then delete ~/.cloudflared/cert.pem (only the tunnel credential stays)
#   deploy.sh <ssh-host> --status             what the room service sees right now
#
# The deploying user needs passwordless sudo on the host. The ticket secret is made there and never leaves it.
# An update refuses to restart while rooms are running (players would be dropped); FORCE=1 overrides.
set -euo pipefail
HOST=${1:?usage: deploy.sh <ssh-host> [server-build-dir | --tunnel | --status]}
ARG=${2:-}
HERE=$(cd "$(dirname "$0")" && pwd)
ROOT=$(cd "$HERE/../../.." && pwd)
PUBLIC=partyshooter.ezellm.com
APP=/mnt/apps/partyshooter
STAGE=partyshooter-stage            # in the deploying user's home on the host
remote() { ssh -o BatchMode=yes "$HOST" "$@"; }
push() { rsync -az --delete -e "ssh -o BatchMode=yes" "$@"; }

install_service() {
  local build=${1:-$ROOT/Builds/PiServer}
  [ -x "$build/ShooterPartyServer" ] || { echo "no server build in $build (Unity: ShooterParty > Build > Pi Server (Linux Arm64); the first try per editor launch can fail on the sysroot, build again)"; exit 2; }
  echo "staging on $HOST"
  remote "mkdir -p $STAGE"
  push --exclude '*_DoNotShip' --exclude '*_BackUpThisFolder_ButDontShipItWithYourGame' "$build/" "$HOST:$STAGE/server/"
  push --exclude 'test_*.py' --exclude 'fake_game_server.py' --exclude 'make_*.py' --exclude '__pycache__/' \
       --include '*.py' --exclude '*' "$HERE/rooms/" "$HOST:$STAGE/rooms/"
  push "$HERE/systemd/" "$HOST:$STAGE/systemd/"
  push "$HERE/partyshooter-status" "$HOST:$STAGE/partyshooter-status"
  remote "sudo FORCE=${FORCE:-} APP=$APP STAGE=\$HOME/$STAGE bash -s" <<'REMOTE'
set -euo pipefail
running=$(curl -s --max-time 2 -H 'X-ShooterParty-Version: 1' http://127.0.0.1:7700/api/v1/status \
  | python3 -c 'import json,sys; print(json.load(sys.stdin).get("rooms", 0))' 2>/dev/null || echo 0)
if [ "$running" != 0 ] && [ -z "$FORCE" ]; then echo "$running room(s) running: not restarting (FORCE=1 to drop them)"; exit 4; fi
id partyshooter >/dev/null 2>&1 || useradd --system --home-dir "$APP/home" --no-create-home --shell /usr/sbin/nologin partyshooter
install -d -o root -g root -m 755 "$APP" "$APP/server" "$APP/rooms"
install -d -o partyshooter -g partyshooter -m 700 "$APP/home"
install -d -o root -g partyshooter -m 750 /etc/partyshooter
if [ ! -s /etc/partyshooter/secret ]; then (umask 077; openssl rand -hex 32 > /etc/partyshooter/secret); echo "made a new ticket secret"; fi
chown root:partyshooter /etc/partyshooter/secret; chmod 0640 /etc/partyshooter/secret
# code is root-owned and read-only to the service: a compromised game server cannot rewrite itself
rsync -a --delete --chown=root:root --chmod=D755,F644 "$STAGE/server/" "$APP/server/"
chmod 755 "$APP/server/ShooterPartyServer"
rsync -a --delete --chown=root:root --chmod=D755,F644 "$STAGE/rooms/" "$APP/rooms/"
install -m 644 "$STAGE/systemd/partyshooter-rooms.service" /etc/systemd/system/partyshooter-rooms.service
install -m 755 "$STAGE/partyshooter-status" /usr/local/bin/partyshooter-status
systemctl daemon-reload
systemctl enable --quiet partyshooter-rooms
systemctl restart partyshooter-rooms
for _ in 1 2 3 4 5 6 7 8 9 10; do curl -s --max-time 1 -o /dev/null http://127.0.0.1:7700/ && break; sleep 0.5; done
echo -n "partyshooter-rooms: "; systemctl is-active partyshooter-rooms
curl -s -H 'X-ShooterParty-Version: 1' http://127.0.0.1:7700/api/v1/status; echo
REMOTE
}

install_tunnel() {
  remote "mkdir -p $STAGE"
  push "$HERE/cloudflared/partyshooter.yml.example" "$HERE/systemd/cloudflared-partyshooter.service" "$HOST:$STAGE/"
  remote "PUBLIC=$PUBLIC STAGE=\$HOME/$STAGE bash -s" <<'REMOTE'
set -euo pipefail
[ -s ~/.cloudflared/cert.pem ] || { echo "first run on the host: cloudflared tunnel login (pick the zone in the browser)"; exit 3; }
tid() { cloudflared tunnel list -o json 2>/dev/null | python3 -c 'import json,sys; print(next((t["id"] for t in json.load(sys.stdin) or [] if t["name"] == "partyshooter"), ""))'; }
ID=$(tid)
[ -n "$ID" ] || { cloudflared tunnel create partyshooter; ID=$(tid); }
[ -n "$ID" ] || { echo "could not create the partyshooter tunnel"; exit 5; }
cloudflared tunnel route dns partyshooter "$PUBLIC" || echo "(DNS route already there?)"
sudo install -d -m 755 /etc/cloudflared
if [ -s ~/.cloudflared/"$ID".json ]; then sudo install -m 600 -o root -g root ~/.cloudflared/"$ID".json /etc/cloudflared/partyshooter.json; fi
[ -s /etc/cloudflared/partyshooter.json ] || sudo test -s /etc/cloudflared/partyshooter.json || { echo "no tunnel credential"; exit 6; }
sed "s/TUNNEL_ID/$ID/" "$STAGE/partyshooter.yml.example" | sudo tee /etc/cloudflared/partyshooter.yml >/dev/null
sudo chmod 644 /etc/cloudflared/partyshooter.yml
sudo install -m 644 "$STAGE/cloudflared-partyshooter.service" /etc/systemd/system/cloudflared-partyshooter.service
sudo systemctl daemon-reload
sudo systemctl enable --quiet --now cloudflared-partyshooter
sudo systemctl restart cloudflared-partyshooter
# the account certificate can create tunnels for the whole zone: it does not stay on a game server
rm -f ~/.cloudflared/cert.pem ~/.cloudflared/"$ID".json
echo -n "cloudflared-partyshooter: "; systemctl is-active cloudflared-partyshooter
REMOTE
}

case "$ARG" in
  --tunnel) install_tunnel ;;
  --status) remote partyshooter-status ;;
  *) install_service "$ARG" ;;
esac
