set -e
cd "$(dirname "$0")"
node render2.mjs slides.html out2
node pack2.js out2 raw2.pptx
/private/tmp/claude-501/-Users-ezelbayraktar-Documents-Development-PartyGame/5a9d9fb7-082b-4e07-a4ed-bd5fd9a5d069/scratchpad/venv/bin/python -I round.py raw2.pptx rounded.pptx
/private/tmp/claude-501/-Users-ezelbayraktar-Documents-Development-PartyGame/5a9d9fb7-082b-4e07-a4ed-bd5fd9a5d069/scratchpad/venv/bin/python -I animate.py rounded.pptx ShooterParty_v10.pptx
/private/tmp/claude-501/-Users-ezelbayraktar-Documents-Development-PartyGame/5a9d9fb7-082b-4e07-a4ed-bd5fd9a5d069/scratchpad/venv/bin/python /Users/ezelbayraktar/.claude/skills/synced/8b886348-9c9c-49b7-9173-1192562a186d_31a6ea3d-af1d-4570-895d-4c064195fc7c/pptx/scripts/office/validate.py ShooterParty_v10.pptx | tail -1
node flat.mjs >/dev/null
/private/tmp/claude-501/-Users-ezelbayraktar-Documents-Development-PartyGame/5a9d9fb7-082b-4e07-a4ed-bd5fd9a5d069/scratchpad/venv/bin/python -I -c "from PIL import Image;import glob;ims=[Image.open(f).convert('RGB') for f in sorted(glob.glob('out_flat/s*.jpg'))];ims[0].save('ShooterParty_v10.pdf',save_all=True,append_images=ims[1:],resolution=216,quality=90)"
