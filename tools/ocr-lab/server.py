"""启动命令：.local/ocr-lab/venv/Scripts/python.exe tools/ocr-lab/server.py"""

import io
import json
import os
import traceback
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, urlsplit

from PIL import Image
from recognition import CONFIG, LAB, recognize


class OcrLabHandler(BaseHTTPRequestHandler):
    def send_bytes(self, status, payload, content_type):
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)

    def do_GET(self):
        path = urlsplit(self.path).path
        if path == "/":
            index = LAB / "index.html"
            if not index.is_file():
                self.send_bytes(404, b'{"error":"OCR lab UI is not installed."}', "application/json")
                return
            self.send_bytes(200, index.read_bytes(), "text/html; charset=utf-8")
        elif path == "/sample.png":
            sample = LAB.parent.parent / "output/playwright/mse-card-export.png"
            if not sample.is_file():
                self.send_bytes(404, b'{"error":"Local OCR sample is not installed."}', "application/json")
                return
            self.send_bytes(200, sample.read_bytes(), "image/png")
        elif path == "/health":
            self.send_bytes(200, b'{"status":"ready"}', "application/json")
        else:
            self.send_error(404)

    def do_POST(self):
        route = urlsplit(self.path)
        if route.path != "/recognize":
            self.send_error(404)
            return
        try:
            options = parse_qs(route.query)
            image_bytes = self.rfile.read(int(self.headers["Content-Length"]))
            with Image.open(io.BytesIO(image_bytes)) as image:
                recognized = recognize(image, options["model"][0], options["layout"][0])
            self.send_bytes(200, json.dumps(recognized, ensure_ascii=False).encode(),
                            "application/json; charset=utf-8")
        except (ValueError, FileNotFoundError) as error:
            self.send_bytes(400, json.dumps({"error": str(error)}, ensure_ascii=False).encode(),
                            "application/json; charset=utf-8")
        except Exception as error:
            traceback.print_exc()
            self.send_bytes(500, json.dumps({"error": str(error)}, ensure_ascii=False).encode(),
                            "application/json; charset=utf-8")


if __name__ == "__main__":
    host = os.environ.get("OCR_LAB_HOST", CONFIG["host"])
    port = int(os.environ.get("OCR_LAB_PORT", CONFIG["port"]))
    if not 1 <= port <= 65535:
        raise ValueError("OCR_LAB_PORT 必须在 1..65535 之间。")
    print(f'OCR Lab: http://{host}:{port}', flush=True)
    ThreadingHTTPServer((host, port), OcrLabHandler).serve_forever()
