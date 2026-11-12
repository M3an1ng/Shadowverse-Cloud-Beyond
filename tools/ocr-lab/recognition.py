"""本地混合 OCR 识别引擎（PP-OCRv5_mobile + Beyond 卡框/数字像素级模板匹配 + 闲置内存自动释放）。"""

import base64
import gc
import io
import json
import os
import re
import threading
import time
from pathlib import Path

import numpy as np
from PIL import Image
from rapidocr import EngineType, LangDet, LangRec, ModelType, OCRVersion, RapidOCR

LAB = Path(__file__).resolve().parent
ROOT = LAB.parents[1]
CONFIG = json.loads((LAB / "config.json").read_text(encoding="utf-8"))
MOBILE_MODELS = ("ch_PP-OCRv5_det_mobile.onnx", "ch_PP-OCRv5_rec_mobile.onnx")


def _model_directory():
    configured = os.environ.get("OCR_LAB_MODEL_DIR")
    candidates = [Path(configured).expanduser()] if configured else [LAB / "models", ROOT / ".local/ocr-lab/models"]
    for directory in candidates:
        if directory.is_dir():
            missing = [directory / name for name in MOBILE_MODELS if not (directory / name).is_file()]
            if missing:
                raise FileNotFoundError("OCR 必需模型缺失：" + ", ".join(map(str, missing)))
            return directory
    raise FileNotFoundError("OCR 模型目录不存在：" + ", ".join(map(str, candidates)))


def _mse_directory():
    candidates = [ROOT / "mse-beyond", ROOT / "public/mse-beyond"]
    for directory in candidates:
        if directory.is_dir():
            return directory
    raise FileNotFoundError("MSE 模板目录不存在：" + ", ".join(map(str, candidates)))


MODEL_DIR = _model_directory()
MSE_ASSETS = _mse_directory()
MODELS = {"mobile": ModelType.MOBILE, "server": ModelType.SERVER}

REGIONS = {
    "name": (70, 65, 760, 126),
    "class": (880, 38, 1030, 80),
    "tribe": (860, 76, 1030, 116),
    "cost": (90, 163, 175, 248),
    "attack": (94, 540, 179, 642),
    "health": (383, 540, 468, 642),
    "rules": (510, 150, 1160, 570),
    "artist": (515, 578, 1010, 615),
}
ART_RECT = (136, 230, 429, 593)
NUMBER_FIELDS = {"cost", "attack", "health"}

NUMBER_FONT_METRICS = {
    "imagePointSize": 60.0,
    "spacing": -15.0,
}

CLASS_MAP = {
    "中立": "Neutral",
    "精灵": "Forestcraft",
    "精靈": "Forestcraft",
    "皇家护卫": "Swordcraft",
    "皇家護衛": "Swordcraft",
    "皇家": "Swordcraft",
    "巫师": "Runecraft",
    "巫師": "Runecraft",
    "龙族": "Dragoncraft",
    "龍族": "Dragoncraft",
    "梦魇": "Abysscraft",
    "夢魘": "Abysscraft",
    "主教": "Havencraft",
    "复仇者": "Portalcraft",
    "復仇者": "Portalcraft",
    "超越者": "Portalcraft",
}

IDLE_UNLOAD_SECONDS = CONFIG.get("idleUnloadSeconds", 300)
_engines = {}
_last_used_at = 0.0
_engine_lock = threading.Lock()
_unload_timer = None
_templates_cache = None


def _schedule_idle_unload():
    global _unload_timer
    if _unload_timer is not None:
        _unload_timer.cancel()

    def _unload():
        with _engine_lock:
            if _engines and (time.monotonic() - _last_used_at) >= IDLE_UNLOAD_SECONDS - 1:
                _engines.clear()
                gc.collect()

    _unload_timer = threading.Timer(IDLE_UNLOAD_SECONDS, _unload)
    _unload_timer.daemon = True
    _unload_timer.start()


def get_engine(model):
    global _last_used_at
    if model not in MODELS:
        raise ValueError("不支持的模型，请选择 mobile 或 server。")
    recognition_model = MODEL_DIR / f"ch_PP-OCRv5_rec_{model}.onnx"
    if not recognition_model.is_file():
        raise FileNotFoundError(f"未安装 {model} 识别模型：{recognition_model}。生产包仅提供 mobile 模型。")
    _last_used_at = time.monotonic()
    _schedule_idle_unload()
    if model not in _engines:
        _engines[model] = RapidOCR(params={
            "Det.engine_type": EngineType.ONNXRUNTIME,
            "Det.lang_type": LangDet.CH,
            "Det.model_type": ModelType.MOBILE,
            "Det.ocr_version": OCRVersion.PPOCRV5,
            "Det.model_path": str(MODEL_DIR / "ch_PP-OCRv5_det_mobile.onnx"),
            "Det.limit_type": "max",
            "Det.limit_side_len": 1280,
            "Rec.engine_type": EngineType.ONNXRUNTIME,
            "Rec.lang_type": LangRec.CH,
            "Rec.model_type": MODELS[model],
            "Rec.ocr_version": OCRVersion.PPOCRV5,
            "Rec.model_path": str(recognition_model),
            "Global.use_cls": False,
            "Global.text_score": 0.0,
            "EngineConfig.onnxruntime.intra_op_num_threads": CONFIG.get("cpuThreads", 2),
            "EngineConfig.onnxruntime.inter_op_num_threads": 1,
        })
    return _engines[model]


def _render_number_template(digits_sprites, value, width, height, metrics):
    """按 renderer.js 的 drawNumbers 精确合成 0~30 的 RGBA 模板"""
    s_val = str(value)
    sprites = [digits_sprites[ch] for ch in s_val]
    symbol_scale = 28.0 / metrics["imagePointSize"]
    spacing = metrics["spacing"]
    widths = [sp.width * symbol_scale for sp in sprites]
    heights = [sp.height * symbol_scale for sp in sprites]
    total = sum(widths) + spacing * (len(sprites) - 1)
    scale = min(1.0, width / total) if total > 0 else 1.0

    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    x = (width - total * scale) / 2.0
    for i, sp in enumerate(sprites):
        w = max(1, int(round(widths[i] * scale)))
        h = max(1, int(round(heights[i] * scale)))
        y = int(round((height - heights[i] * scale) / 2.0))
        ix = int(round(x))
        resized = sp.resize((w, h), Image.Resampling.BILINEAR)
        canvas.paste(resized, (ix, y), resized)
        x += (widths[i] + spacing) * scale

    arr = np.asarray(canvas, dtype=np.float32)
    rgb = arr[:, :, :3]
    alpha = arr[:, :, 3] / 255.0
    mask = alpha > 0.35
    return rgb, alpha, mask


def _ensure_templates():
    global _templates_cache
    if _templates_cache is not None:
        return _templates_cache

    number_templates = {"cost": [], "stat": []}
    style_dir = MSE_ASSETS / "numbers" / "shadowverse-symd"
    required = [style_dir / f"{d}.png" for d in range(10)]
    required.append(MSE_ASSETS / "card_mask.png")
    required.extend(MSE_ASSETS / f"card_{kind}_{rarity}.png"
                    for kind in ("minion", "spell", "amulet")
                    for rarity in ("bronze", "silver", "gold", "legend", "special"))
    missing = [path for path in required if not path.is_file()]
    if missing:
        raise FileNotFoundError("MSE 必需模板缺失：" + ", ".join(map(str, missing)))
    digit_sprites = {
        str(d): Image.open(style_dir / f"{d}.png").convert("RGBA")
        for d in range(10)
    }
    for val in range(31):
        rgb_c, alpha_c, mask_c = _render_number_template(digit_sprites, val, 85, 85, NUMBER_FONT_METRICS)
        number_templates["cost"].append((str(val), "shadowverse-symd2", rgb_c, alpha_c, mask_c))
        rgb_s, alpha_s, mask_s = _render_number_template(digit_sprites, val, 85, 102, NUMBER_FONT_METRICS)
        number_templates["stat"].append((str(val), "shadowverse-symd2", rgb_s, alpha_s, mask_s))

    frame_templates = []
    type_map = {"minion": "Follower", "spell": "Spell", "amulet": "Amulet"}
    rarity_map = {
        "bronze": ("Bronze", False),
        "silver": ("Silver", False),
        "gold": ("Gold", False),
        "legend": ("Legend", False),
        "special": ("Legend", True),
    }
    mask_path = MSE_ASSETS / "card_mask.png"
    m_img = Image.open(mask_path).convert("RGBA").resize((391, 513), Image.Resampling.BILINEAR)
    mask_alpha = np.asarray(m_img, dtype=np.float32)[:, :, 0] / 255.0

    for f_type, card_type in type_map.items():
        for f_rarity, (card_rarity, is_alt) in rarity_map.items():
            f_path = MSE_ASSETS / f"card_{f_type}_{f_rarity}.png"
            f_img = Image.open(f_path).convert("RGBA").resize((391, 513), Image.Resampling.BILINEAR)
            f_arr = np.asarray(f_img, dtype=np.float32).copy()
            f_arr[:, :, 3] = f_arr[:, :, 3] * mask_alpha
            # 右侧卡框类型纹章区 (x: 355..388, y: 120..360) + 底部稀有度边框区 (x: 110..280, y: 430..495)
            patch_type = f_arr[120:360, 355:388, :]
            patch_rarity = f_arr[430:495, 110:280, :]
            frame_templates.append((card_type, card_rarity, is_alt, patch_type, patch_rarity))

    _templates_cache = {
        "numbers": number_templates,
        "frames": frame_templates,
    }
    return _templates_cache


def match_card_frame(image):
    """通过卡框特征区域匹配卡牌类型（Follower/Spell/Amulet）与稀有度"""
    templates = _ensure_templates()["frames"]

    # 卡框绘制在整图 (81, 131)，尺寸 (391, 513)
    img_arr = np.asarray(image, dtype=np.float32)
    crop_type = img_arr[131 + 120:131 + 360, 81 + 355:81 + 388, :3]
    crop_rarity = img_arr[131 + 430:131 + 495, 81 + 110:81 + 280, :3]

    best_score = float("inf")
    best_match = ("Follower", "Bronze", False)

    for card_type, card_rarity, is_alt, p_type, p_rarity in templates:
        mask_t = p_type[:, :, 3] > 200
        mask_r = p_rarity[:, :, 3] > 200
        # 避开中间职业宝石 (268-81=187, 572-131=441, w=25, h=26) -> 在 patch_rarity 中对应 x: 77..102, y: 11..37
        mask_r_clean = mask_r.copy()
        mask_r_clean[8:42, 72:108] = False

        diff_t = np.abs(crop_type - p_type[:, :, :3]).mean(axis=2)
        diff_r = np.abs(crop_rarity - p_rarity[:, :, :3]).mean(axis=2)
        mae_t = float(diff_t[mask_t].mean()) if mask_t.any() else 255.0
        mae_r = float(diff_r[mask_r_clean].mean()) if mask_r_clean.any() else 255.0
        score = mae_t * 0.6 + mae_r * 0.4
        if score < best_score:
            best_score = score
            best_match = (card_type, card_rarity, is_alt)

    return best_match


def match_number_region(crop_img, kind="cost"):
    """使用 Alpha 加权像素误差匹配 0~30 的卡面艺术数字"""
    templates = _ensure_templates()["numbers"][kind]

    target = np.asarray(crop_img.convert("RGB"), dtype=np.float32)
    best_val = None
    best_style = None
    best_mae = float("inf")

    for val_str, style, tpl_rgb, tpl_alpha, tpl_mask in templates:
        if not tpl_mask.any():
            continue
        diff = np.abs(target - tpl_rgb).mean(axis=2)
        fg_mae = float((diff[tpl_mask] * tpl_alpha[tpl_mask]).sum() / tpl_alpha[tpl_mask].sum())
        if fg_mae < best_mae:
            best_mae = fg_mae
            best_val = val_str
            best_style = style

    confidence = max(0.0, min(0.9999, round(1.0 - best_mae / 128.0, 4)))
    return best_val, confidence, best_style


def read_region(engine, image, region, offset):
    output = engine(image, use_det=region not in NUMBER_FIELDS,
                    use_cls=False, use_rec=True)
    lines = []
    if output.txts is None:
        return lines
    for index, (text, confidence) in enumerate(zip(output.txts, output.scores)):
        box = None
        if getattr(output, "boxes", None) is not None:
            box = [[round(float(x) + offset[0], 1), round(float(y) + offset[1], 1)]
                   for x, y in output.boxes[index]]
        lines.append({
            "region": region,
            "text": text,
            "confidence": round(float(confidence), 4),
            "box": box
        })
    return lines


def split_rule_sections(lines):
    rule_lines = [line for line in lines if line["region"] == "rules" and line["text"].strip()]
    rule_lines.sort(key=lambda line: min(point[1] for point in line["box"]) if line["box"] else 0)
    sections = []
    previous_bottom = None
    previous_height = None
    styles = {
        "进化时": "进化", "進化時": "进化",
        "超进化时": "超进化", "超進化時": "超进化",
        "纹章": "纹章", "紋章": "纹章", "信仰": "信仰",
    }
    for line in rule_lines:
        text = line["text"].strip()
        lead = re.match(r"^(?:【)?(超进化时|超進化時|进化时|進化時|纹章|紋章|信仰)", text)
        style = styles[lead.group(1)] if lead else "普通"
        separated = False
        if line["box"]:
            top = min(point[1] for point in line["box"])
            bottom = max(point[1] for point in line["box"])
            line_height = bottom - top
            if previous_bottom is not None:
                separated = top - previous_bottom > (line_height + previous_height) / 2 * 0.6
            previous_bottom = bottom
            previous_height = line_height
        else:
            previous_bottom = None
            previous_height = None

        # 超过编辑器的五段上限后保留文案，继续合入末段。
        if not sections or ((lead or separated) and len(sections) < 5):
            sections.append({"text": text, "style": style})
        else:
            sections[-1]["text"] += "\n" + text
    return sections


def recognize(image, model="mobile", layout="beyond"):
    started_at = time.perf_counter()
    with _engine_lock:
        engine = get_engine(model)
        image = image.convert("RGB")

        if layout == "whole":
            lines = read_region(engine, image, "whole", (0, 0))
            return {
                "model": model,
                "layout": layout,
                "width": image.width,
                "height": image.height,
                "recognitionSeconds": round(time.perf_counter() - started_at, 3),
                "lines": lines,
            }

        if layout != "beyond":
            raise ValueError("不支持的识别范围。")

        if image.size != (1269, 715):
            image = image.resize((1269, 715), Image.Resampling.BICUBIC)

        card_type, card_rarity, is_alt = match_card_frame(image)
        lines = []

        for field, rect in REGIONS.items():
            if field in NUMBER_FIELDS:
                if field in {"attack", "health"} and card_type != "Follower":
                    continue
                kind = "cost" if field == "cost" else "stat"
                val_str, conf, _ = match_number_region(
                    image.crop(rect),
                    kind=kind
                )
                if val_str is not None and conf >= 0.70:
                    lines.append({
                        "region": field,
                        "text": val_str,
                        "confidence": conf,
                        "box": [
                            [float(rect[0]), float(rect[1])],
                            [float(rect[2]), float(rect[1])],
                            [float(rect[2]), float(rect[3])],
                            [float(rect[0]), float(rect[3])],
                        ]
                    })
                    continue

            lines.extend(read_region(engine, image.crop(rect), field, rect[:2]))

        art_crop = image.crop(ART_RECT)
        buf = io.BytesIO()
        art_crop.save(buf, format="WEBP", quality=92)
        cropped_art_data_url = "data:image/webp;base64," + base64.b64encode(buf.getvalue()).decode("ascii")

        def join_region(r_name, sep=""):
            return sep.join(item["text"].strip() for item in lines if item["region"] == r_name and item["text"].strip())

        raw_class = join_region("class", "")
        mapped_class = CLASS_MAP.get(raw_class, "Neutral")
        raw_tribe = join_region("tribe", "")
        tribes = [t.strip() for t in raw_tribe.replace("•", ",").replace("·", ",").replace("、", ",").split(",") if t.strip() and t.strip() != "-"]

        def parse_int_region(r_name, default_val=None):
            txt = join_region(r_name, "")
            return int(txt) if txt.isdigit() else default_val

        artist = join_region("artist", "")
        for prefix in ("繪師：", "画师：", "绘师：", "繪師:", "画师:", "绘师:"):
            artist = artist.replace(prefix, "")

        return {
            "model": model,
            "layout": layout,
            "width": image.width,
            "height": image.height,
            "recognitionSeconds": round(time.perf_counter() - started_at, 3),
            "name": join_region("name", ""),
            "class": mapped_class,
            "classLabel": raw_class,
            "type": card_type,
            "rarity": card_rarity,
            "alternateArt": is_alt,
            "numberStyle": "shadowverse-symd2",
            "presentation": {"numberStyle": "shadowverse-symd2"},
            "cost": parse_int_region("cost", 0),
            "attack": parse_int_region("attack", 0) if card_type == "Follower" else None,
            "health": parse_int_region("health", 1) if card_type == "Follower" else None,
            "tribe": tribes,
            "rulesText": join_region("rules", "\n"),
            "sections": split_rule_sections(lines),
            "artist": artist.strip(),
            "croppedArtDataUrl": cropped_art_data_url,
            "lines": lines,
        }
