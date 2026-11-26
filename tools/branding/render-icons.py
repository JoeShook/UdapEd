"""Render the UdapEd raster icons from the hand-authored SVG sources.

Sources (edit these, then re-run this script):
  Shared/wwwroot/favicon.svg                    -> favicon.ico (16/32/48), favicon.png (32)
  Shared/wwwroot/images/brand/udaped-icon.svg   -> icon-192.png, icon-512.png, logo_uri PNG (600)
  udaped-icon.svg with square corners           -> apple-touch-icon.png (180; iOS rounds it itself)
  UdapEdAppMaui/Resources/Splash/splash.svg     -> UdapEdAppMaui/Platforms/Windows/Assets/splash.png
  (the MAUI app icon itself is built from Resources/AppIcon/*.svg by the MAUI resizetizer)

Outputs are written to both Shared/wwwroot (served as _content/UdapEd.Shared/...) and
Client/wwwroot (served at the site root by the WASM client and the Server host).

Rasterising uses headless Microsoft Edge (no cairo needed on Windows) and Pillow for
downscaling and the .ico container.

Usage (from the repo root):
  python tools/branding/render-icons.py
"""
from __future__ import annotations

import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

from PIL import Image

REPO = Path(__file__).resolve().parents[2]
SHARED = REPO / "Shared" / "wwwroot"
CLIENT = REPO / "Client" / "wwwroot"

EDGE_CANDIDATES = [
    os.environ.get("EDGE_PATH", ""),
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
]


def find_edge() -> str:
    for candidate in EDGE_CANDIDATES:
        if candidate and Path(candidate).exists():
            return candidate
    sys.exit("Microsoft Edge not found; set EDGE_PATH.")


def render(edge: str, svg_text: str, size: int, work: Path) -> Image.Image:
    """Render SVG markup to a transparent RGBA image of size x size."""
    svg_file = work / f"src-{size}.svg"
    svg_file.write_text(svg_text, encoding="utf-8")
    html = work / f"page-{size}.html"
    html.write_text(
        "<!doctype html><html><head><style>html,body{margin:0;background:transparent;overflow:hidden}"
        f"img{{display:block;width:{size}px;height:{size}px}}</style></head>"
        f'<body><img src="{svg_file.name}"></body></html>',
        encoding="utf-8",
    )
    out = work / f"out-{size}.png"
    subprocess.run(
        [
            edge,
            "--headless=new",
            "--disable-gpu",
            "--hide-scrollbars",
            "--force-device-scale-factor=1",
            f"--user-data-dir={work / 'edge-profile'}",
            "--default-background-color=00000000",
            f"--window-size={size},{size}",
            f"--screenshot={out}",
            html.as_uri(),
        ],
        check=True,
        capture_output=True,
    )
    img = Image.open(out).convert("RGBA")
    return img.crop((0, 0, size, size))


def save_png(img: Image.Image, size: int, *targets: Path) -> None:
    resized = img if img.size == (size, size) else img.resize((size, size), Image.LANCZOS)
    for target in targets:
        target.parent.mkdir(parents=True, exist_ok=True)
        resized.save(target, optimize=True)
        print(f"wrote {target.relative_to(REPO)} ({size}x{size})")


def main() -> None:
    edge = find_edge()
    favicon_svg = (SHARED / "favicon.svg").read_text(encoding="utf-8")
    icon_svg = (SHARED / "images" / "brand" / "udaped-icon.svg").read_text(encoding="utf-8")
    square_svg = icon_svg.replace('rx="14"', 'rx="0"')

    splash_svg = (REPO / "UdapEdAppMaui" / "Resources" / "Splash" / "splash.svg").read_text(encoding="utf-8")

    with tempfile.TemporaryDirectory() as tmp:
        work = Path(tmp)
        fav_big = render(edge, favicon_svg, 256, work)
        icon_big = render(edge, icon_svg, 1024, work)
        square_big = render(edge, square_svg, 1024, work)
        splash_mark = render(edge, splash_svg, 320, work)

    # MAUI Windows static splash fallback (see UdapEdAppMaui.csproj): mark centred on ink.
    splash = Image.new("RGBA", (1240, 600), "#152A38")
    splash.alpha_composite(splash_mark, ((1240 - 320) // 2, (600 - 320) // 2))
    splash_target = REPO / "UdapEdAppMaui" / "Platforms" / "Windows" / "Assets" / "splash.png"
    splash.save(splash_target, optimize=True)
    print(f"wrote {splash_target.relative_to(REPO)} (1240x600)")

    for root in (SHARED, CLIENT):
        save_png(fav_big, 32, root / "favicon.png")
        save_png(icon_big, 192, root / "icon-192.png")
        save_png(icon_big, 512, root / "icon-512.png")
        save_png(square_big.convert("RGB"), 180, root / "apple-touch-icon.png")
        save_png(icon_big, 600, root / "images" / "UdapEdLogobyDesigner.png")

        ico = root / "favicon.ico"
        fav_big.save(ico, sizes=[(16, 16), (32, 32), (48, 48)])
        print(f"wrote {ico.relative_to(REPO)} (16/32/48)")

    shutil.copyfile(SHARED / "favicon.svg", CLIENT / "favicon.svg")
    print("copied favicon.svg to Client/wwwroot")


if __name__ == "__main__":
    main()
