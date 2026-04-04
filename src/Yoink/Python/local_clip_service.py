import json
import os
import sys


MODEL_NAME = os.environ.get("YOINK_CLIP_MODEL", "ViT-B-32")
PRETRAINED_NAME = os.environ.get("YOINK_CLIP_PRETRAINED", "laion2b_s34b_b79k")


def emit(payload):
    sys.stdout.write(json.dumps(payload, ensure_ascii=False) + "\n")
    sys.stdout.flush()


def fail(message, code=1):
    emit({"ok": False, "error": message})
    return code


def load_runtime():
    try:
        import torch
        import open_clip
        from PIL import Image
    except Exception as exc:
        return None, fail(f"Missing CLIP dependencies: {exc}")

    device = "cuda" if torch.cuda.is_available() else "cpu"
    try:
        model, _, preprocess = open_clip.create_model_and_transforms(
            MODEL_NAME,
            pretrained=PRETRAINED_NAME,
            device=device,
        )
        tokenizer = open_clip.get_tokenizer(MODEL_NAME)
        model.eval()
        return {
            "torch": torch,
            "Image": Image,
            "model": model,
            "preprocess": preprocess,
            "tokenizer": tokenizer,
            "device": device,
            "model_key": f"{MODEL_NAME}/{PRETRAINED_NAME}",
        }, 0
    except Exception as exc:
        return None, fail(f"Failed to load CLIP model: {exc}")


def embed_text(runtime, text):
    torch = runtime["torch"]
    tokens = runtime["tokenizer"]([text]).to(runtime["device"])
    with torch.inference_mode():
        features = runtime["model"].encode_text(tokens)
        features = features / features.norm(dim=-1, keepdim=True)
    return features[0].float().cpu().tolist()


def embed_image(runtime, path):
    torch = runtime["torch"]
    image = runtime["Image"].open(path).convert("RGB")
    try:
        tensor = runtime["preprocess"](image).unsqueeze(0).to(runtime["device"])
        with torch.inference_mode():
            features = runtime["model"].encode_image(tensor)
            features = features / features.norm(dim=-1, keepdim=True)
        return features[0].float().cpu().tolist()
    finally:
        image.close()


def main():
    runtime, code = load_runtime()
    if runtime is None:
        return code

    emit({
        "ok": True,
        "device": runtime["device"],
        "model": MODEL_NAME,
        "pretrained": PRETRAINED_NAME,
        "modelKey": runtime["model_key"],
    })

    for raw in sys.stdin:
        raw = raw.strip()
        if not raw:
            continue

        try:
            request = json.loads(raw)
        except Exception as exc:
            emit({"id": None, "ok": False, "error": f"Invalid JSON: {exc}"})
            continue

        req_id = request.get("id")
        op = request.get("op")

        try:
            if op == "shutdown":
                emit({"id": req_id, "ok": True, "embedding": []})
                break

            if op == "text":
                text = request.get("text") or ""
                if not text.strip():
                    raise ValueError("Text was empty.")
                emit({"id": req_id, "ok": True, "embedding": embed_text(runtime, text)})
                continue

            if op == "image":
                path = request.get("path") or ""
                if not path.strip():
                    raise ValueError("Image path was empty.")
                emit({"id": req_id, "ok": True, "embedding": embed_image(runtime, path)})
                continue

            raise ValueError(f"Unknown operation: {op}")
        except Exception as exc:
            emit({"id": req_id, "ok": False, "error": str(exc)})


if __name__ == "__main__":
    raise SystemExit(main())
