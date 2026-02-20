#!/usr/bin/env node
"use strict";

const fs = require("fs");
const path = require("path");
const { execSync } = require("child_process");

const TEXT_EXTENSIONS = new Set([
    ".js",
    ".jsx",
    ".ts",
    ".tsx",
    ".css",
    ".scss",
    ".html",
    ".json",
    ".md",
    ".cs",
    ".yml",
    ".yaml",
    ".xml",
    ".toml",
    ".txt",
]);

const SKIP_DIRS = new Set([
    ".git",
    "node_modules",
    "dist",
    "build",
    "tmp_build_out",
    "Server.Tests/artifacts",
    "Server/publish",
]);

const SKIP_PATH_PREFIXES = [
    "assets/",
    "artifacts/",
    "augment-unpacked/",
    "dist/",
    "build/",
    "node_modules/",
    "tmp_",
    ".tmp_",
    "Server.Tests/artifacts/",
    "Server/publish/",
];

const SKIP_EXACT_FILES = new Set([
    "webview.js",
]);

// Strong corruption markers:
// - U+FFFD replacement char
// - Private Use Area glyphs (common in mojibake after bad transcoding)
const REPLACEMENT_CHAR_REGEX = /\uFFFD/u;
const PRIVATE_USE_REGEX = /[\uE000-\uF8FF]/u;

// Common mojibake punctuation/symbols seen in UTF-8/GBK corruption chains.
const MOJIBAKE_PUNCT_REGEX = /[\u951B\u9286\u9225\u951F\u95BF]/u;

// A set of uncommon CJK compatibility glyphs frequently produced by wrong decoding.
// Using Unicode escapes avoids embedding visible garbled literals in source.
const MOJIBAKE_GLYPH_REGEX = /[\u5A13\u93C4\u9359\u7F02\u6FDE\u95BA\u940E\u935A\u93B4\u9428\u95BF\u951B\u9286\u9225\u951F]/gu;
const MOJIBAKE_GLYPH_MIN_COUNT = 3;

function isTextFile(filePath) {
    const ext = path.extname(filePath).toLowerCase();
    return TEXT_EXTENSIONS.has(ext);
}

function normalizePath(filePath) {
    return filePath.replace(/\\/g, "/");
}

function shouldSkipFile(filePath) {
    const normalized = normalizePath(filePath);
    if (SKIP_EXACT_FILES.has(normalized)) return true;
    return SKIP_PATH_PREFIXES.some(prefix => normalized.startsWith(prefix));
}

function isLikelyBinary(buffer) {
    const checkLength = Math.min(buffer.length, 2048);
    for (let i = 0; i < checkLength; i += 1) {
        if (buffer[i] === 0x00) {
            return true;
        }
    }
    return false;
}

function listProjectFiles(rootDir) {
    const queue = [rootDir];
    const files = [];

    while (queue.length > 0) {
        const current = queue.pop();
        const entries = fs.readdirSync(current, { withFileTypes: true });
        for (const entry of entries) {
            const fullPath = path.join(current, entry.name);
            const relPath = path.relative(rootDir, fullPath).replace(/\\/g, "/");

            if (entry.isDirectory()) {
                if (SKIP_DIRS.has(relPath) || SKIP_DIRS.has(entry.name)) continue;
                queue.push(fullPath);
                continue;
            }

            if (!entry.isFile()) continue;
            if (!isTextFile(relPath)) continue;
            if (shouldSkipFile(relPath)) continue;
            files.push(relPath);
        }
    }

    return files;
}

function listStagedFiles() {
    const output = execSync("git diff --cached --name-only --diff-filter=ACMR", {
        stdio: ["ignore", "pipe", "pipe"],
        encoding: "utf8",
    });
    return output
        .split(/\r?\n/)
        .map(line => line.trim())
        .filter(Boolean)
        .filter(isTextFile)
        .filter(filePath => !shouldSkipFile(filePath));
}

function listTrackedFiles() {
    const output = execSync("git ls-files", {
        stdio: ["ignore", "pipe", "pipe"],
        encoding: "utf8",
    });
    return output
        .split(/\r?\n/)
        .map(line => line.trim())
        .filter(Boolean)
        .filter(isTextFile)
        .filter(filePath => !shouldSkipFile(filePath));
}

function looksLikeMojibake(line) {
    if (!line) return false;
    if (REPLACEMENT_CHAR_REGEX.test(line)) return true;
    if (PRIVATE_USE_REGEX.test(line)) return true;
    if (MOJIBAKE_PUNCT_REGEX.test(line)) return true;

    const glyphMatches = line.match(MOJIBAKE_GLYPH_REGEX);
    return Array.isArray(glyphMatches) && glyphMatches.length >= MOJIBAKE_GLYPH_MIN_COUNT;
}

function inspectFile(filePath) {
    const absPath = path.resolve(filePath);
    if (!fs.existsSync(absPath)) {
        return [];
    }

    const buffer = fs.readFileSync(absPath);
    if (isLikelyBinary(buffer)) {
        return [];
    }

    const text = buffer.toString("utf8");
    const lines = text.split(/\r?\n/);
    const findings = [];

    for (let i = 0; i < lines.length; i += 1) {
        const line = lines[i];
        if (!looksLikeMojibake(line)) continue;
        findings.push({
            line: i + 1,
            content: line.trim(),
        });
    }

    return findings;
}

function dedupe(values) {
    return Array.from(new Set(values));
}

function main() {
    const args = process.argv.slice(2);
    let files = [];

    if (args.includes("--staged")) {
        try {
            files = listStagedFiles();
        } catch (error) {
            console.error("[encoding-check] Failed to list staged files.");
            console.error(error.message);
            process.exit(1);
        }
    } else if (args.includes("--all")) {
        files = listProjectFiles(process.cwd());
    } else if (args.length > 0) {
        files = args
            .map(normalizePath)
            .filter(isTextFile)
            .filter(filePath => !shouldSkipFile(filePath));
    } else {
        try {
            files = listTrackedFiles();
        } catch {
            files = listProjectFiles(process.cwd());
        }
    }

    files = dedupe(files);

    const problems = [];
    for (const filePath of files) {
        const findings = inspectFile(filePath);
        if (findings.length === 0) continue;
        problems.push({ filePath, findings });
    }

    if (problems.length === 0) {
        console.log(`[encoding-check] OK. checked ${files.length} file(s).`);
        return;
    }

    console.error("[encoding-check] Found suspicious encoding artifacts:");
    for (const issue of problems) {
        for (const finding of issue.findings.slice(0, 8)) {
            console.error(`  - ${issue.filePath}:${finding.line}: ${finding.content}`);
        }
        if (issue.findings.length > 8) {
            console.error(`  - ${issue.filePath}: ... and ${issue.findings.length - 8} more`);
        }
    }

    console.error("[encoding-check] Commit blocked. Please fix mojibake/replacement characters first.");
    process.exit(1);
}

main();
