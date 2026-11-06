from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "book" / "assets" / "prod-config-guard.png"
FONT_PATH = Path(r"C:\Windows\Fonts\msyh.ttc")
WIDTH, HEIGHT = 1600, 820

image = Image.new("RGB", (WIDTH, HEIGHT), "#f7f9fc")
draw = ImageDraw.Draw(image)
font = ImageFont.truetype(str(FONT_PATH), 29)
small = ImageFont.truetype(str(FONT_PATH), 24)
title = ImageFont.truetype(str(FONT_PATH), 42)
draw.text((70, 45), "生产配置合并前门禁", font=title, fill="#172b4d")
draw.text((70, 105), "先拒绝确定不安全的期望状态，再用 Canary 验证实际运行行为", font=small, fill="#42526e")

boxes = {
    "change": (60, 330, 330, 500, "#e8f1ff", "#175cd3"),
    "parse": (400, 330, 690, 500, "#f0f3f8", "#64748b"),
    "policy": (765, 330, 1080, 500, "#fff5d6", "#b54708"),
    "pass": (1180, 195, 1535, 335, "#e7f8ef", "#16803c"),
    "block": (1180, 495, 1535, 645, "#feeceb", "#b42318"),
}
labels = {
    "change": ["Pull request", "生产 values 路径"],
    "parse": ["安全解析 YAML", "拒绝坏格式 / 重复键"],
    "policy": ["校验策略", "errorRate、latencyMs", "只能为数值 0 或缺省"],
    "pass": ["PASS · 退出码 0", "作为必需合并状态"],
    "block": ["BLOCK · 退出码非零", "修复后重新提交"],
}
for key, (x1, y1, x2, y2, fill, outline) in boxes.items():
    draw.rounded_rectangle((x1, y1, x2, y2), radius=22, fill=fill, outline=outline, width=4)
    lines = labels[key]
    total_height = len(lines) * 44
    y = y1 + ((y2 - y1) - total_height) // 2
    for index, line in enumerate(lines):
        active_font = font if index == 0 else small
        bbox = draw.textbbox((0, 0), line, font=active_font)
        draw.text(((x1 + x2 - (bbox[2] - bbox[0])) / 2, y), line, font=active_font, fill="#172b4d" if index == 0 else "#344054")
        y += 44

def arrow(start: tuple[int, int], end: tuple[int, int], color: str = "#526581") -> None:
    draw.line((start, end), fill=color, width=6)
    ex, ey = end
    draw.polygon([(ex, ey), (ex - 18, ey - 12), (ex - 18, ey + 12)], fill=color)

arrow((330, 415), (400, 415))
arrow((690, 415), (765, 415))
arrow((1080, 390), (1180, 280), "#16803c")
arrow((1080, 445), (1180, 560), "#b42318")
draw.text((1090, 295), "规则通过", font=small, fill="#16803c")
draw.text((1080, 590), "违规或解析失败", font=small, fill="#b42318")
draw.text((65, 720), "CI 只检查指定生产路径；开发环境可以保留故障注入。Canary 负责部署后的运行时验证。", font=small, fill="#42526e")
OUT.parent.mkdir(parents=True, exist_ok=True)
image.save(OUT, format="PNG", optimize=True)
print(f"wrote {OUT.relative_to(ROOT)} ({WIDTH}x{HEIGHT})")
