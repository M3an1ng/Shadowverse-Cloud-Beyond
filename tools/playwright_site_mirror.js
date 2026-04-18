#!/usr/bin/env node
"use strict";

const fs = require("node:fs/promises");
const path = require("node:path");
const crypto = require("node:crypto");
const { chromium } = require("playwright");

const CAPTURE_TYPES = new Set([
  "document",
  "stylesheet",
  "script",
  "image",
  "font",
  "media",
  "xhr",
  "fetch",
]);

function printHelp() {
  console.log(`
Playwright site mirror (for local learning only).

Usage:
  node tools/playwright_site_mirror.js --base-url <url> [options]

Options:
  --base-url <url>         Base URL, e.g. https://shadowverse-wb.com
  --paths <p1,p2,...>      Paths or full URLs, default: /
  --paths-file <file>      One path/URL per line
  --out <dir>              Output dir, default: data/scrape-playwright/<domain>
  --delay-ms <num>         Delay after navigation, default: 1200
  --timeout-ms <num>       Navigation timeout, default: 30000
  --wait-until <state>     load | domcontentloaded | networkidle, default: networkidle
  --headed                 Run browser in headed mode
  --include-external       Download cross-origin assets too
  --save-screenshot        Save screenshot for each page
  --user-agent <ua>        Custom user agent
  --help                   Show this help

Examples:
  node tools/playwright_site_mirror.js --base-url https://shadowverse-wb.com --paths /,/ja/about/
  node tools/playwright_site_mirror.js --base-url https://shadowverse-wb.com --paths-file tools/mirror-paths.txt --save-screenshot
`);
}

function parseArgs(argv) {
  const options = {
    baseUrl: "",
    paths: ["/"],
    pathsFile: "",
    outDir: "",
    delayMs: 1200,
    timeoutMs: 30000,
    waitUntil: "networkidle",
    headed: false,
    includeExternal: false,
    saveScreenshot: false,
    userAgent: "",
    help: false,
  };
  const positional = [];

  for (let i = 0; i < argv.length; i += 1) {
    const arg = argv[i];
    const take = () => {
      if (i + 1 >= argv.length) {
        throw new Error(`Missing value for ${arg}`);
      }
      i += 1;
      return argv[i];
    };

    if (arg === "--help" || arg === "-h") {
      options.help = true;
    } else if (arg === "--base-url") {
      options.baseUrl = take().trim();
    } else if (arg === "--paths") {
      const raw = take();
      options.paths = raw
        .split(",")
        .map((x) => x.trim())
        .filter(Boolean);
    } else if (arg === "--paths-file") {
      options.pathsFile = take().trim();
    } else if (arg === "--out") {
      options.outDir = take().trim();
    } else if (arg === "--delay-ms") {
      options.delayMs = Number.parseInt(take(), 10);
    } else if (arg === "--timeout-ms") {
      options.timeoutMs = Number.parseInt(take(), 10);
    } else if (arg === "--wait-until") {
      options.waitUntil = take().trim();
    } else if (arg === "--headed") {
      options.headed = true;
    } else if (arg === "--include-external") {
      options.includeExternal = true;
    } else if (arg === "--save-screenshot") {
      options.saveScreenshot = true;
    } else if (arg === "--user-agent") {
      options.userAgent = take();
    } else if (arg.startsWith("-")) {
      throw new Error(`Unknown argument: ${arg}`);
    } else {
      positional.push(arg);
    }
  }

  if (!options.baseUrl && isHttpUrl(process.env.npm_config_base_url)) {
    options.baseUrl = process.env.npm_config_base_url;
  }
  if (
    options.paths.length === 1 &&
    options.paths[0] === "/" &&
    hasUsableValue(process.env.npm_config_paths)
  ) {
    options.paths = process.env.npm_config_paths
      .split(",")
      .map((x) => x.trim())
      .filter(Boolean);
  }
  if (!options.outDir && hasUsableValue(process.env.npm_config_out)) {
    options.outDir = process.env.npm_config_out;
  }
  if (options.delayMs === 1200 && hasUsableValue(process.env.npm_config_delay_ms)) {
    const n = Number.parseInt(process.env.npm_config_delay_ms, 10);
    if (Number.isFinite(n)) options.delayMs = n;
  }
  if (options.timeoutMs === 30000 && hasUsableValue(process.env.npm_config_timeout_ms)) {
    const n = Number.parseInt(process.env.npm_config_timeout_ms, 10);
    if (Number.isFinite(n)) options.timeoutMs = n;
  }
  if (options.waitUntil === "networkidle" && hasUsableValue(process.env.npm_config_wait_until)) {
    options.waitUntil = process.env.npm_config_wait_until;
  }

  // Some npm/powershell combinations strip option names and keep values only.
  if (!options.baseUrl && positional[0]) options.baseUrl = positional[0];
  if (positional[1]) {
    options.paths = positional[1]
      .split(",")
      .map((x) => x.trim())
      .filter(Boolean);
  }
  if (!options.outDir && positional[2]) options.outDir = positional[2];
  if (options.delayMs === 1200 && positional[3]) {
    const n = Number.parseInt(positional[3], 10);
    if (Number.isFinite(n)) options.delayMs = n;
  }
  if (options.timeoutMs === 30000 && positional[4]) {
    const n = Number.parseInt(positional[4], 10);
    if (Number.isFinite(n)) options.timeoutMs = n;
  }

  if (!options.help && !options.baseUrl) {
    throw new Error("--base-url is required");
  }

  if (!["load", "domcontentloaded", "networkidle"].includes(options.waitUntil)) {
    throw new Error("--wait-until must be one of: load | domcontentloaded | networkidle");
  }
  if (!Number.isFinite(options.delayMs) || options.delayMs < 0) {
    throw new Error("--delay-ms must be >= 0");
  }
  if (!Number.isFinite(options.timeoutMs) || options.timeoutMs <= 0) {
    throw new Error("--timeout-ms must be > 0");
  }

  return options;
}

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function shortHash(text) {
  return crypto.createHash("sha1").update(text).digest("hex").slice(0, 8);
}

function sanitizeSegment(text) {
  return text.replace(/[<>:"/\\|?*\x00-\x1F]/g, "_");
}

function hasUsableValue(value) {
  if (typeof value !== "string") return false;
  const v = value.trim();
  return v !== "" && v !== "true" && v !== "false";
}

function isHttpUrl(value) {
  if (!hasUsableValue(value)) return false;
  return /^https?:\/\//i.test(value.trim());
}

function toFsPath(outDir, relPath) {
  return path.join(outDir, ...relPath.split("/"));
}

function normalizeUrl(rawUrl) {
  const u = new URL(rawUrl);
  u.hash = "";
  return u.toString();
}

function isSkippableUrl(raw) {
  const v = (raw || "").trim().toLowerCase();
  return (
    !v ||
    v.startsWith("#") ||
    v.startsWith("data:") ||
    v.startsWith("javascript:") ||
    v.startsWith("mailto:") ||
    v.startsWith("tel:")
  );
}

function extFromContentType(contentType) {
  const c = (contentType || "").toLowerCase();
  if (c.includes("text/css")) return ".css";
  if (c.includes("javascript")) return ".js";
  if (c.includes("application/json")) return ".json";
  if (c.includes("image/png")) return ".png";
  if (c.includes("image/jpeg")) return ".jpg";
  if (c.includes("image/webp")) return ".webp";
  if (c.includes("image/svg+xml")) return ".svg";
  if (c.includes("image/gif")) return ".gif";
  if (c.includes("image/x-icon")) return ".ico";
  if (c.includes("font/woff2")) return ".woff2";
  if (c.includes("font/woff")) return ".woff";
  if (c.includes("font/ttf")) return ".ttf";
  if (c.includes("font/otf")) return ".otf";
  if (c.includes("video/mp4")) return ".mp4";
  return "";
}

function buildAssetRelPath(assetUrl, contentType) {
  const u = new URL(assetUrl);
  let filePath = u.pathname || "/";
  if (filePath.endsWith("/")) {
    filePath += "index";
  }
  filePath = filePath.replace(/^\/+/, "");
  if (!filePath) {
    filePath = "index";
  }

  const ext = path.posix.extname(filePath);
  if (!ext) {
    const guessed = extFromContentType(contentType) || ".bin";
    filePath += guessed;
  }

  if (u.search) {
    const dir = path.posix.dirname(filePath);
    const oldExt = path.posix.extname(filePath);
    const name = path.posix.basename(filePath, oldExt);
    filePath = path.posix.join(dir, `${name}_${shortHash(u.search)}${oldExt}`);
  }

  return path.posix.join("assets", sanitizeSegment(u.hostname), filePath);
}

function buildPageRelPath(pageUrl) {
  const u = new URL(pageUrl);
  let filePath = u.pathname || "/";

  if (filePath === "/") {
    filePath = "/index";
  } else if (filePath.endsWith("/")) {
    filePath += "index";
  } else if (!path.posix.extname(filePath)) {
    filePath += "/index";
  }

  filePath = filePath.replace(/^\/+/, "");
  let relPath = path.posix.join("pages", sanitizeSegment(u.hostname), filePath);
  if (!path.posix.extname(relPath)) {
    relPath += ".html";
  }

  if (u.search) {
    const dir = path.posix.dirname(relPath);
    const ext = path.posix.extname(relPath);
    const name = path.posix.basename(relPath, ext);
    relPath = path.posix.join(dir, `${name}_${shortHash(u.search)}${ext}`);
  }
  return relPath;
}

function relativePath(fromFileRelPath, toFileRelPath) {
  let rel = path.posix.relative(path.posix.dirname(fromFileRelPath), toFileRelPath);
  if (!rel.startsWith(".")) {
    rel = `./${rel}`;
  }
  return rel;
}

function resolvePageUrls(baseUrl, inputPaths) {
  const result = [];
  const seen = new Set();
  for (const raw of inputPaths) {
    if (!raw) continue;
    const abs = normalizeUrl(new URL(raw, baseUrl).toString());
    if (!seen.has(abs)) {
      seen.add(abs);
      result.push(abs);
    }
  }
  return result;
}

async function loadPathsFile(filePath) {
  const content = await fs.readFile(filePath, "utf8");
  return content
    .split(/\r?\n/g)
    .map((line) => line.trim())
    .filter((line) => line && !line.startsWith("#"));
}

async function writeBuffer(outDir, relPath, data) {
  const filePath = toFsPath(outDir, relPath);
  await fs.mkdir(path.dirname(filePath), { recursive: true });
  await fs.writeFile(filePath, data);
}

async function writeText(outDir, relPath, text) {
  const filePath = toFsPath(outDir, relPath);
  await fs.mkdir(path.dirname(filePath), { recursive: true });
  await fs.writeFile(filePath, text, "utf8");
}

function mapEntriesForPage(pageRelPath, assetIndex) {
  const entries = [];
  for (const [assetUrl, asset] of assetIndex.entries()) {
    entries.push([assetUrl, relativePath(pageRelPath, asset.relPath)]);
  }
  return entries;
}

async function rewritePageDomUrls(page, entries) {
  await page.evaluate((mapEntries) => {
    const assetMap = new Map(mapEntries);
    const URL_ATTRS = [
      "src",
      "href",
      "poster",
      "data-src",
      "data-href",
      "data-bg",
      "data-background",
    ];
    const META_IMG_KEYS = new Set(["og:image", "twitter:image", "msapplication-tileimage"]);
    const STYLE_URL_RE = /url\(\s*(['"]?)([^"')]+)\1\s*\)/gi;

    const isSkip = (raw) => {
      const val = (raw || "").trim().toLowerCase();
      return (
        !val ||
        val.startsWith("#") ||
        val.startsWith("data:") ||
        val.startsWith("javascript:") ||
        val.startsWith("mailto:") ||
        val.startsWith("tel:")
      );
    };

    const normalize = (raw) => {
      const u = new URL(raw, document.baseURI);
      u.hash = "";
      return u.toString();
    };

    const rewriteOne = (raw) => {
      if (isSkip(raw)) return raw;
      try {
        const abs = normalize(raw);
        return assetMap.get(abs) || raw;
      } catch {
        return raw;
      }
    };

    const rewriteSrcset = (raw) => {
      if (!raw) return raw;
      return raw
        .split(",")
        .map((part) => {
          const item = part.trim();
          if (!item) return item;
          const [urlPart, descriptor = ""] = item.split(/\s+/, 2);
          const next = rewriteOne(urlPart);
          return descriptor ? `${next} ${descriptor}` : next;
        })
        .join(", ");
    };

    const rewriteCssText = (cssText) =>
      cssText.replace(STYLE_URL_RE, (all, quote, raw) => {
        const next = rewriteOne(raw);
        if (next === raw) return all;
        const q = quote || '"';
        return `url(${q}${next}${q})`;
      });

    const nodes = document.querySelectorAll("*");
    nodes.forEach((el) => {
      URL_ATTRS.forEach((attr) => {
        if (!el.hasAttribute(attr)) return;
        const oldVal = el.getAttribute(attr);
        const newVal = rewriteOne(oldVal);
        if (newVal !== oldVal) {
          el.setAttribute(attr, newVal);
        }
      });

      if (el.hasAttribute("srcset")) {
        const oldVal = el.getAttribute("srcset");
        const newVal = rewriteSrcset(oldVal);
        if (newVal !== oldVal) {
          el.setAttribute("srcset", newVal);
        }
      }

      if (el.hasAttribute("style")) {
        const oldVal = el.getAttribute("style");
        const newVal = rewriteCssText(oldVal);
        if (newVal !== oldVal) {
          el.setAttribute("style", newVal);
        }
      }

      if (el.tagName === "META") {
        const key =
          (el.getAttribute("property") || el.getAttribute("name") || "").trim().toLowerCase();
        if (META_IMG_KEYS.has(key) && el.hasAttribute("content")) {
          const oldVal = el.getAttribute("content");
          const newVal = rewriteOne(oldVal);
          if (newVal !== oldVal) {
            el.setAttribute("content", newVal);
          }
        }
      }
    });

    document.querySelectorAll("style").forEach((styleNode) => {
      if (!styleNode.textContent) return;
      styleNode.textContent = rewriteCssText(styleNode.textContent);
    });
  }, entries);
}

async function rewriteCssAssetUrls(outDir, assetIndex) {
  const cssUrlRe = /url\(\s*(['"]?)([^"')]+)\1\s*\)/gi;
  const cssImportRe = /@import\s+(?:url\(\s*)?(['"]?)([^"')\s;]+)\1\s*\)?/gi;

  for (const [assetUrl, meta] of assetIndex.entries()) {
    const ext = path.posix.extname(meta.relPath).toLowerCase();
    if (ext !== ".css" && !meta.contentType.toLowerCase().includes("text/css")) {
      continue;
    }

    const cssPath = toFsPath(outDir, meta.relPath);
    let css;
    try {
      css = await fs.readFile(cssPath, "utf8");
    } catch {
      continue;
    }

    let changed = false;
    const rewriteRaw = (raw) => {
      if (isSkippableUrl(raw)) return raw;
      let absUrl;
      try {
        absUrl = normalizeUrl(new URL(raw, assetUrl).toString());
      } catch {
        return raw;
      }
      const target = assetIndex.get(absUrl);
      if (!target) return raw;
      changed = true;
      return relativePath(meta.relPath, target.relPath);
    };

    const next1 = css.replace(cssUrlRe, (all, quote, raw) => {
      const next = rewriteRaw(raw);
      if (next === raw) return all;
      const q = quote || '"';
      return `url(${q}${next}${q})`;
    });

    const next2 = next1.replace(cssImportRe, (all, quote, raw) => {
      const next = rewriteRaw(raw);
      if (next === raw) return all;
      const q = quote || '"';
      return `@import ${q}${next}${q}`;
    });

    if (changed) {
      await fs.writeFile(cssPath, next2, "utf8");
    }
  }
}

async function run() {
  let options;
  try {
    options = parseArgs(process.argv.slice(2));
  } catch (err) {
    console.error(`Argument error: ${err.message}`);
    printHelp();
    process.exitCode = 2;
    return;
  }

  if (options.help) {
    printHelp();
    return;
  }

  const baseUrl = normalizeUrl(options.baseUrl);
  const baseOrigin = new URL(baseUrl).origin;
  const domain = sanitizeSegment(new URL(baseUrl).hostname);
  const outDir = path.resolve(options.outDir || path.join("data", "scrape-playwright", domain));

  const allInputs = [...options.paths];
  if (options.pathsFile) {
    const fromFile = await loadPathsFile(path.resolve(options.pathsFile));
    allInputs.push(...fromFile);
  }
  const pageUrls = resolvePageUrls(baseUrl, allInputs);
  if (pageUrls.length === 0) {
    console.error("No valid paths to mirror.");
    process.exitCode = 2;
    return;
  }

  await fs.mkdir(outDir, { recursive: true });
  const startedAt = new Date().toISOString();

  console.log(`Output dir: ${outDir}`);
  console.log(`Pages: ${pageUrls.length}`);
  console.log(`Mode: ${options.headed ? "headed" : "headless"}`);
  console.log("Reminder: only use this for local learning and respect target website terms.");

  let browser;
  try {
    browser = await chromium.launch({ headless: !options.headed });
  } catch (err) {
    const message = String(err);
    if (message.includes("Executable doesn't exist")) {
      console.error("Playwright browser is not installed.");
      console.error("Run: npx playwright install chromium");
      process.exitCode = 1;
      return;
    }
    throw err;
  }
  const context = await browser.newContext({
    viewport: { width: 1440, height: 900 },
    userAgent: options.userAgent || undefined,
  });
  const page = await context.newPage();

  const errors = [];
  const pages = [];
  const assetIndex = new Map();
  const responseTasks = new Set();

  const saveAssetFromResponse = async (response) => {
    const request = response.request();
    if (request.method() !== "GET") return;
    if (!CAPTURE_TYPES.has(request.resourceType())) return;
    if (response.status() >= 400) return;

    const absUrl = normalizeUrl(response.url());
    if (!options.includeExternal && new URL(absUrl).origin !== baseOrigin) return;
    if (assetIndex.has(absUrl)) return;

    let body;
    try {
      body = await response.body();
    } catch (err) {
      errors.push({ type: "asset", url: absUrl, message: `read body failed: ${String(err)}` });
      return;
    }

    const headers = response.headers();
    const contentType = headers["content-type"] || "";
    const relPath = buildAssetRelPath(absUrl, contentType);
    await writeBuffer(outDir, relPath, body);

    assetIndex.set(absUrl, {
      relPath,
      status: response.status(),
      resourceType: request.resourceType(),
      contentType,
      size: body.length,
    });
  };

  page.on("response", (response) => {
    const task = saveAssetFromResponse(response)
      .catch((err) => {
        errors.push({
          type: "asset",
          url: response.url(),
          message: String(err),
        });
      })
      .finally(() => responseTasks.delete(task));
    responseTasks.add(task);
  });

  for (let i = 0; i < pageUrls.length; i += 1) {
    const pageUrl = pageUrls[i];
    console.log(`[${i + 1}/${pageUrls.length}] ${pageUrl}`);

    try {
      await page.goto(pageUrl, {
        waitUntil: options.waitUntil,
        timeout: options.timeoutMs,
      });

      if (options.delayMs > 0) {
        await sleep(options.delayMs);
      }

      await Promise.all(Array.from(responseTasks));

      const pageRelPath = buildPageRelPath(pageUrl);
      const mapEntries = mapEntriesForPage(pageRelPath, assetIndex);
      await rewritePageDomUrls(page, mapEntries);

      const html = await page.content();
      await writeText(outDir, pageRelPath, html);

      let screenshotRelPath = "";
      if (options.saveScreenshot) {
        const ext = path.posix.extname(pageRelPath);
        screenshotRelPath = `${pageRelPath.slice(0, -ext.length)}.png`;
        await writeBuffer(outDir, screenshotRelPath, await page.screenshot({ fullPage: true }));
      }

      pages.push({
        sourceUrl: pageUrl,
        htmlPath: pageRelPath,
        screenshotPath: screenshotRelPath || null,
      });
    } catch (err) {
      errors.push({
        type: "page",
        url: pageUrl,
        message: String(err),
      });
      console.error(`  Failed: ${err.message || err}`);
    }
  }

  await Promise.all(Array.from(responseTasks));
  await rewriteCssAssetUrls(outDir, assetIndex);

  const manifest = {
    createdAt: startedAt,
    finishedAt: new Date().toISOString(),
    baseUrl,
    baseOrigin,
    includeExternal: options.includeExternal,
    pages,
    assets: Array.from(assetIndex.entries()).map(([url, meta]) => ({
      url,
      ...meta,
    })),
    errors,
  };

  await writeText(outDir, "manifest.json", JSON.stringify(manifest, null, 2));
  await context.close();
  await browser.close();

  console.log("");
  console.log("Mirror complete.");
  console.log(`Saved pages: ${pages.length}`);
  console.log(`Saved assets: ${assetIndex.size}`);
  console.log(`Errors: ${errors.length}`);
  console.log(`Manifest: ${path.join(outDir, "manifest.json")}`);
}

run().catch((err) => {
  console.error(err);
  process.exitCode = 1;
});
