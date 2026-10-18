set -e
cd "$(dirname "$0")"
node render.mjs slides.html out
node pack.js out raw.pptx
/private/tmp/claude-501/-Users-ezelbayraktar-Documents-Development-PartyGame/9274d9c7-7631-4e33-aefc-30a38befa928/scratchpad/venv/bin/python -I animate.py raw.pptx IceRumble_v7_edit.pptx
/private/tmp/claude-501/-Users-ezelbayraktar-Documents-Development-PartyGame/9274d9c7-7631-4e33-aefc-30a38befa928/scratchpad/venv/bin/python /Users/ezelbayraktar/.claude/skills/synced/8b886348-9c9c-49b7-9173-1192562a186d_31a6ea3d-af1d-4570-895d-4c064195fc7c/pptx/scripts/office/validate.py IceRumble_v7_edit.pptx | tail -2
