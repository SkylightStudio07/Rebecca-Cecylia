from pathlib import Path
import sys

import numpy as np
from PIL import Image
from scipy import ndimage


def extract_panel(source: Path, destination: Path) -> None:
    rgb = np.asarray(Image.open(source).convert("RGB"))
    channel_spread = rgb.max(axis=2) - rgb.min(axis=2)

    # 이미지 모델이 구운 회백색 체크무늬는 화면 가장자리와 연결된 무채색 밝은 영역이다.
    checker_candidate = (rgb.min(axis=2) >= 232) & (channel_spread <= 7)
    checker_labels, _ = ndimage.label(checker_candidate)
    edge_labels = np.unique(
        np.concatenate(
            [
                checker_labels[0, :],
                checker_labels[-1, :],
                checker_labels[:, 0],
                checker_labels[:, -1],
            ]
        )
    )
    background = np.isin(checker_labels, edge_labels[edge_labels != 0])

    foreground_labels, foreground_count = ndimage.label(~background)
    if foreground_count == 0:
        raise RuntimeError(f"패널 윤곽을 찾지 못했습니다: {source}")

    areas = np.bincount(foreground_labels.ravel())
    areas[0] = 0
    panel = foreground_labels == areas.argmax()
    panel = ndimage.binary_fill_holes(panel)

    # 패널은 각 스캔라인에서 하나의 긴 실루엣이다. 생성 모델이 외곽에 남긴 밝은 잔점을 제거한다.
    cleaned_panel = np.zeros_like(panel)
    minimum_run = max(8, int(panel.shape[1] * 0.08))
    for row_index, row in enumerate(panel):
        run_labels, run_count = ndimage.label(row)
        if run_count == 0:
            continue
        run_areas = np.bincount(run_labels)
        run_areas[0] = 0
        longest_run = run_areas.argmax()
        if run_areas[longest_run] >= minimum_run:
            cleaned_panel[row_index] = run_labels == longest_run
    panel = ndimage.binary_fill_holes(cleaned_panel)

    y, x = np.where(panel)
    padding = 24
    left = max(0, int(x.min()) - padding)
    right = min(rgb.shape[1], int(x.max()) + padding + 1)
    top = max(0, int(y.min()) - padding)
    bottom = min(rgb.shape[0], int(y.max()) + padding + 1)

    cropped_rgb = rgb[top:bottom, left:right].copy()
    cropped_alpha = (panel[top:bottom, left:right] * 255).astype(np.uint8)
    cropped_rgb[cropped_alpha == 0] = 0
    rgba = np.dstack((cropped_rgb, cropped_alpha))

    destination.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(rgba, "RGBA").save(destination)


def create_hover(source: Path, destination: Path) -> None:
    rgba = np.asarray(Image.open(source).convert("RGBA")).copy()
    rgb = rgba[:, :, :3]
    alpha = rgba[:, :, 3]
    lightness = rgb.mean(axis=2)
    channel_spread = rgb.max(axis=2) - rgb.min(axis=2)

    # 검은 구조물과 흰 외곽선 사이의 넓은 종이 면만 선택해 원근 윤곽을 완전히 보존한다.
    face_candidate = (alpha > 0) & (lightness >= 145) & (channel_spread <= 34)
    face_labels, face_count = ndimage.label(face_candidate)
    if face_count == 0:
        raise RuntimeError(f"패널 전면을 찾지 못했습니다: {source}")
    areas = np.bincount(face_labels.ravel())
    areas[0] = 0
    face = face_labels == areas.argmax()
    face = ndimage.binary_fill_holes(face)

    texture = np.clip((lightness - 155.0) / 100.0, 0.0, 1.0)
    blue_dark = np.array([4.0, 92.0, 198.0])
    blue_light = np.array([10.0, 132.0, 242.0])
    blue = blue_dark + texture[:, :, None] * (blue_light - blue_dark)
    rgb[face] = blue[face].astype(np.uint8)

    destination.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(rgba, "RGBA").save(destination)


if __name__ == "__main__":
    if len(sys.argv) == 3:
        extract_panel(Path(sys.argv[1]), Path(sys.argv[2]))
    elif len(sys.argv) == 4 and sys.argv[1] == "--hover":
        create_hover(Path(sys.argv[2]), Path(sys.argv[3]))
    else:
        raise SystemExit(
            "usage: process_generated_panels.py SOURCE DESTINATION\n"
            "   or: process_generated_panels.py --hover NORMAL DESTINATION"
        )
