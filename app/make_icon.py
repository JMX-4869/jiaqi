import os
from PIL import Image, ImageDraw

os.makedirs("res/mipmap-xxhdpi", exist_ok=True)
S = 192
im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(im)

# 底：近黑圆角块
d.rounded_rectangle([0, 0, S - 1, S - 1], radius=44, fill=(14, 17, 22, 255))

# 日历本：白色的圆角矩形
x0, y0, x1, y1 = 40, 52, S - 40, S - 44
d.rounded_rectangle([x0, y0, x1, y1], radius=18, fill=(230, 237, 243, 255))

# 顶部横条（装订处）盖一块青色
d.rounded_rectangle([x0, y0, x1, y0 + 30], radius=18, fill=(76, 194, 255, 255))
d.rectangle([x0, y0 + 18, x1, y0 + 30], fill=(76, 194, 255, 255))

# 两个挂环
d.rounded_rectangle([x0 + 26, y0 - 14, x0 + 40, y0 + 6], radius=7, fill=(230, 237, 243, 255))
d.rounded_rectangle([x1 - 40, y0 - 14, x1 - 26, y0 + 6], radius=7, fill=(230, 237, 243, 255))

# 日历上的日期点：三行，最后一行空一格（像正在放假的空白）
dot = 13
gap = 6
cx, cy = x0 + 22, y0 + 50
for r in range(3):
    for c in range(4):
        if r == 2 and c == 3:
            continue  # 空出来那一格 = 假期
        px = cx + c * (dot + gap * 3)
        py = cy + r * (dot + gap * 3)
        col = (76, 194, 255, 255) if (r == 1 and c == 2) else (150, 163, 178, 255)
        d.rounded_rectangle([px, py, px + dot, py + dot], radius=4, fill=col)

im.save("res/mipmap-xxhdpi/ic_launcher.png")
print("icon ok", im.size)
