#!/usr/bin/env python3
import argparse
import gzip
import hashlib
import json
import os
import re
import sys
import time
from html.parser import HTMLParser
from urllib.error import HTTPError, URLError
from urllib.parse import urljoin, urlparse, urlunparse
from urllib.request import Request, urlopen
from urllib import robotparser

CSS_URL_RE = re.compile(r"url\(\s*([\"']?)(.*?)\1\s*\)", re.IGNORECASE)
CSS_IMPORT_RE = re.compile(r"@import\s+(?:url\()?['\"]?([^'\")]+)", re.IGNORECASE)

IMG_EXTS = {".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg", ".bmp", ".ico", ".avif"}
FONT_EXTS = {".woff", ".woff2", ".ttf", ".otf", ".eot"}


class AssetHTMLParser(HTMLParser):
    def __init__(self):
        super().__init__()
        self.css_links = []
        self.images = []
        self.inline_styles = []
        self.inline_style_blocks = []
        self.base_href = None
        self._in_style = False

    def handle_starttag(self, tag, attrs):
        attr_map = {k.lower(): v for k, v in attrs if v is not None}
        if tag.lower() == "base" and not self.base_href:
            self.base_href = attr_map.get("href")
        if tag.lower() == "link":
            rel = (attr_map.get("rel") or "").lower()
            href = attr_map.get("href")
            if "stylesheet" in rel.split():
                if href:
                    self.css_links.append(href)
            if any(r in rel for r in ["icon", "apple-touch-icon", "shortcut icon"]):
                if href:
                    self.images.append(href)
            if rel == "preload" and attr_map.get("as", "").lower() == "image":
                if href:
                    self.images.append(href)
        if tag.lower() == "img":
            src = attr_map.get("src")
            if src:
                self.images.append(src)
            srcset = attr_map.get("srcset")
            if srcset:
                self.images.extend(parse_srcset(srcset))
        if tag.lower() == "source":
            srcset = attr_map.get("srcset")
            if srcset:
                self.images.extend(parse_srcset(srcset))
            src = attr_map.get("src")
            if src:
                self.images.append(src)
        if tag.lower() == "meta":
            prop = (attr_map.get("property") or attr_map.get("name") or "").lower()
            if prop in {"og:image", "twitter:image", "msapplication-tileimage"}:
                content = attr_map.get("content")
                if content:
                    self.images.append(content)
        style_attr = attr_map.get("style")
        if style_attr:
            self.inline_styles.append(style_attr)
        if tag.lower() == "style":
            self._in_style = True

    def handle_endtag(self, tag):
        if tag.lower() == "style":
            self._in_style = False

    def handle_data(self, data):
        if self._in_style and data.strip():
            self.inline_style_blocks.append(data)


def parse_srcset(srcset):
    urls = []
    for part in srcset.split(","):
        piece = part.strip()
        if not piece:
            continue
        url = piece.split()[0]
        if url:
            urls.append(url)
    return urls


def fetch_bytes(url, timeout, user_agent):
    req = Request(url, headers={"User-Agent": user_agent, "Accept": "*/*"})
    with urlopen(req, timeout=timeout) as resp:
        data = resp.read()
        encoding = (resp.headers.get("Content-Encoding") or "").lower()
        if encoding == "gzip":
            data = gzip.decompress(data)
        return data, resp.headers, resp.geturl()


def decode_bytes(data, headers):
    content_type = headers.get("Content-Type") or ""
    match = re.search(r"charset=([^\s;]+)", content_type, re.IGNORECASE)
    charset = match.group(1) if match else "utf-8"
    try:
        return data.decode(charset, errors="replace")
    except LookupError:
        return data.decode("utf-8", errors="replace")


def short_hash(text):
    return hashlib.sha1(text.encode("utf-8")).hexdigest()[:8]


def safe_filename(url, default_ext):
    parsed = urlparse(url)
    base = os.path.basename(parsed.path.rstrip("/"))
    if not base:
        base = "index"
    name, ext = os.path.splitext(base)
    if not ext:
        ext = default_ext
        name = f"{name}_{short_hash(url)}"
    elif parsed.query:
        name = f"{name}_{short_hash(url)}"
    return f"{name}{ext}"


def normalize_url(base_url, raw_url):
    if not raw_url:
        return None
    raw_url = raw_url.strip()
    if not raw_url or raw_url.startswith("data:") or raw_url.startswith("javascript:"):
        return None
    if raw_url.startswith("//"):
        parsed_base = urlparse(base_url)
        raw_url = f"{parsed_base.scheme}:{raw_url}"
    abs_url = urljoin(base_url, raw_url)
    parsed = urlparse(abs_url)
    normalized = urlunparse((parsed.scheme, parsed.netloc, parsed.path, parsed.params, parsed.query, ""))
    return normalized


def is_same_origin(base_url, target_url):
    base = urlparse(base_url)
    target = urlparse(target_url)
    return base.scheme == target.scheme and base.netloc == target.netloc


def choose_dir(url, dirs):
    ext = os.path.splitext(urlparse(url).path)[1].lower()
    if ext in IMG_EXTS:
        return dirs["images"]
    if ext in FONT_EXTS:
        return dirs["fonts"]
    if ext == ".css":
        return dirs["css"]
    if ext == ".js":
        return dirs["js"]
    return dirs["assets"]


def save_bytes(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(data)


def ensure_dirs(root):
    dirs = {
        "root": root,
        "html": os.path.join(root, "html"),
        "css": os.path.join(root, "css"),
        "js": os.path.join(root, "js"),
        "images": os.path.join(root, "images"),
        "fonts": os.path.join(root, "fonts"),
        "assets": os.path.join(root, "assets"),
        "inline": os.path.join(root, "inline"),
    }
    for path in dirs.values():
        os.makedirs(path, exist_ok=True)
    return dirs


def extract_css_urls(css_text):
    urls = [m.group(2).strip() for m in CSS_URL_RE.finditer(css_text)]
    return [u for u in urls if u]


def extract_css_imports(css_text):
    imports = [m.group(1).strip() for m in CSS_IMPORT_RE.finditer(css_text)]
    return [u for u in imports if u]


def check_robots(base_url, target_url, user_agent, timeout):
    parsed = urlparse(base_url)
    robots_url = f"{parsed.scheme}://{parsed.netloc}/robots.txt"
    rp = robotparser.RobotFileParser()
    try:
        data, headers, _ = fetch_bytes(robots_url, timeout, user_agent)
    except HTTPError as err:
        return False, f"robots.txt blocked with HTTP {err.code}"
    except URLError as err:
        return False, f"robots.txt unreachable: {err.reason}"
    except Exception as err:
        return False, f"robots.txt error: {err}"
    text = decode_bytes(data, headers)
    rp.parse(text.splitlines())
    allowed = rp.can_fetch(user_agent, target_url)
    if not allowed:
        return False, "robots.txt disallows this path"
    return True, ""


def download_url(url, dest_dir, default_ext, timeout, user_agent, seen, manifest, errors):
    if url in seen:
        return None
    seen.add(url)
    try:
        data, headers, final_url = fetch_bytes(url, timeout, user_agent)
    except (HTTPError, URLError) as err:
        errors.append({"url": url, "error": str(err)})
        return None
    except Exception as err:
        errors.append({"url": url, "error": str(err)})
        return None
    filename = safe_filename(final_url, default_ext)
    path = os.path.join(dest_dir, filename)
    save_bytes(path, data)
    manifest.append({"url": final_url, "path": path})
    return path, data, headers, final_url


def main():
    parser = argparse.ArgumentParser(description="Scrape public static assets (CSS/images/fonts) from a page.")
    parser.add_argument("--url", required=True, help="Target page URL.")
    parser.add_argument("--out", help="Output directory (default: data/scrape/<domain>).")
    parser.add_argument("--allow-external", action="store_true", help="Allow downloading external (cross-origin) assets.")
    parser.add_argument("--ignore-robots", action="store_true", help="Ignore robots.txt (use only if you have permission).")
    parser.add_argument("--delay", type=float, default=0.1, help="Delay between requests in seconds.")
    parser.add_argument("--timeout", type=float, default=15.0, help="Request timeout in seconds.")
    parser.add_argument("--user-agent", default="Mozilla/5.0 (AssetScraper/1.0)", help="User-Agent header.")
    args = parser.parse_args()

    parsed = urlparse(args.url)
    if not parsed.scheme or not parsed.netloc:
        print("Invalid URL. Example: https://example.com/path", file=sys.stderr)
        return 2

    domain = parsed.netloc.replace(":", "_")
    out_root = args.out or os.path.join("data", "scrape", domain)
    dirs = ensure_dirs(out_root)

    if not args.ignore_robots:
        ok, reason = check_robots(args.url, args.url, args.user_agent, args.timeout)
        if not ok:
            print(f"Robots check failed: {reason}", file=sys.stderr)
            print("Use --ignore-robots only if you have permission.", file=sys.stderr)
            return 2

    errors = []
    seen = set()
    manifest = {
        "page_url": args.url,
        "saved_html": None,
        "css": [],
        "images": [],
        "fonts": [],
        "assets": [],
        "inline_css": [],
        "skipped_external": [],
        "errors": errors,
    }

    html_data, html_headers, final_url = fetch_bytes(args.url, args.timeout, args.user_agent)
    html_path = os.path.join(dirs["html"], "page.html")
    save_bytes(html_path, html_data)
    manifest["saved_html"] = html_path

    html_text = decode_bytes(html_data, html_headers)
    parser = AssetHTMLParser()
    parser.feed(html_text)

    base_url = final_url
    if parser.base_href:
        base_url = urljoin(final_url, parser.base_href)

    def should_fetch(target_url):
        if not target_url:
            return False
        if args.allow_external:
            return True
        if is_same_origin(base_url, target_url):
            return True
        manifest["skipped_external"].append(target_url)
        return False

    css_queue = []
    for raw in parser.css_links:
        resolved = normalize_url(base_url, raw)
        if resolved and should_fetch(resolved):
            css_queue.append(resolved)

    for raw in parser.images:
        resolved = normalize_url(base_url, raw)
        if resolved and should_fetch(resolved):
            dest = choose_dir(resolved, dirs)
            item = download_url(resolved, dest, ".bin", args.timeout, args.user_agent, seen, manifest["images"], errors)
            time.sleep(args.delay)

    for snippet in parser.inline_styles:
        for raw in extract_css_urls(snippet):
            resolved = normalize_url(base_url, raw)
            if resolved and should_fetch(resolved):
                dest = choose_dir(resolved, dirs)
                download_url(resolved, dest, ".bin", args.timeout, args.user_agent, seen, manifest["assets"], errors)
                time.sleep(args.delay)

    inline_index = 0
    for block in parser.inline_style_blocks:
        inline_index += 1
        inline_path = os.path.join(dirs["inline"], f"inline-{inline_index}.css")
        with open(inline_path, "w", encoding="utf-8") as f:
            f.write(block)
        manifest["inline_css"].append({"path": inline_path})
        for raw in extract_css_urls(block):
            resolved = normalize_url(base_url, raw)
            if resolved and should_fetch(resolved):
                dest = choose_dir(resolved, dirs)
                download_url(resolved, dest, ".bin", args.timeout, args.user_agent, seen, manifest["assets"], errors)
                time.sleep(args.delay)

    css_seen = set()
    while css_queue:
        css_url = css_queue.pop(0)
        if css_url in css_seen:
            continue
        css_seen.add(css_url)
        result = download_url(css_url, dirs["css"], ".css", args.timeout, args.user_agent, seen, manifest["css"], errors)
        time.sleep(args.delay)
        if not result:
            continue
        _, css_data, css_headers, final_css_url = result
        css_text = decode_bytes(css_data, css_headers)
        for raw in extract_css_imports(css_text):
            resolved = normalize_url(final_css_url, raw)
            if resolved and should_fetch(resolved):
                css_queue.append(resolved)
        for raw in extract_css_urls(css_text):
            resolved = normalize_url(final_css_url, raw)
            if resolved and should_fetch(resolved):
                dest = choose_dir(resolved, dirs)
                group = "assets"
                ext = os.path.splitext(urlparse(resolved).path)[1].lower()
                if ext in IMG_EXTS:
                    group = "images"
                elif ext in FONT_EXTS:
                    group = "fonts"
                download_url(resolved, dest, ".bin", args.timeout, args.user_agent, seen, manifest[group], errors)
                time.sleep(args.delay)

    manifest_path = os.path.join(out_root, "manifest.json")
    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=True, indent=2)

    print("Done.")
    print(f"HTML: {manifest['saved_html']}")
    print(f"CSS files: {len(manifest['css'])}")
    print(f"Images: {len(manifest['images'])}")
    print(f"Fonts: {len(manifest['fonts'])}")
    print(f"Other assets: {len(manifest['assets'])}")
    print(f"Inline CSS blocks: {len(manifest['inline_css'])}")
    print(f"Manifest: {manifest_path}")
    if errors:
        print(f"Errors: {len(errors)} (see manifest.json)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
