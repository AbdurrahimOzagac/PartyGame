import sys, os
from PIL import Image, ImageDraw, ImageFont
d, out = sys.argv[1], sys.argv[2]
rows = [("Mevcut", "1  Mevcut yüz"), ("Hap", "2  Hap gözler"), ("Iri", "3  İri gözler"), ("Kasli", "4  Kaşlı, kararlı"), ("Mutlu", "5  Mutlu ^ ^")]
cols = [("Klasik", "A  Klasik, krem dudak"), ("Tombul", "B  Kısa tombul"), ("Bel", "C  Belli, pembe ağız"), ("Halkali", "D  Krem bantlı")]
F = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf", 30); F2 = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf", 26)
w, h = Image.open(os.path.join(d, "Mevcut_Klasik.png")).size; L, T = 270, 64
img = Image.new("RGB", (L + w * len(cols), T + h * len(rows)), (250, 250, 252)); dr = ImageDraw.Draw(img)
for c, (_, t) in enumerate(cols): dr.text((L + c * w + 16, 16), t, fill=(30, 30, 40), font=F2)
for r, (k, t) in enumerate(rows):
    dr.text((16, T + r * h + h // 2 - 18), t, fill=(30, 30, 40), font=F)
    for c, (tk, _) in enumerate(cols):
        img.paste(Image.open(os.path.join(d, f"{k}_{tk}.png")).convert("RGB"), (L + c * w, T + r * h))
img.save(out); print(out, img.size)
