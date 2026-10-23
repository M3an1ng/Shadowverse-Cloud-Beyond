"""Check maintained documentation links and immutable archive originals."""

from pathlib import Path
from urllib.parse import unquote, urlsplit
import hashlib
import json
import re
import sys


ROOT = Path(__file__).resolve().parents[1]
INDEXES = (
    'README.md', '归档清单.md', 'OpenSpec待核实清单.md', '验证记录.md',
)
LINK = re.compile(r'\[[^\]\n]*\]\((<[^>\n]+>|(?:[^()\s]|\([^()\n]*\))+)(?:\s+"[^"\n]*")?\)')


def prose(markdown):
    """Code examples are not document links or heading anchors."""
    lines = []
    fence = None
    for line in markdown.splitlines():
        marker = re.match(r'^\s{0,3}(`{3,}|~{3,})', line)
        if marker:
            token = marker.group(1)
            if fence is None:
                fence = token
            elif token[0] == fence[0] and len(token) >= len(fence):
                fence = None
            continue
        if fence is None:
            lines.append(line)
    return '\n'.join(lines)


def anchors(markdown):
    found = set()
    counts = {}
    for heading in re.findall(r'^\s{0,3}#{1,6}\s+(.+?)\s*#*$', prose(markdown), re.M):
        heading = re.sub(r'\[([^\]]+)\]\([^)]*\)', r'\1', heading)
        base = re.sub(r'[^\w\-\s]', '', heading.lower())
        base = re.sub(r'\s', '-', base)
        duplicate = counts.get(base, 0)
        counts[base] = duplicate + 1
        found.add(f'{base}-{duplicate}' if duplicate else base)
    return found


def local_links(markdown):
    text = re.sub(r'(`+).*?\1', '', prose(markdown))
    for match in LINK.finditer(text):
        target = match.group(1).strip('<>')
        if re.match(r'^[a-zA-Z][a-zA-Z0-9+.-]*:', target) or target.startswith('//'):
            continue
        yield target


def check_links(document):
    errors = []
    text = document.read_text(encoding='utf-8-sig')
    if '\ufffd' in text:
        errors.append(f'{document}: Unicode replacement character')
    for target in local_links(text):
        parsed = urlsplit(target)
        destination = (document.parent / unquote(parsed.path)).resolve() if parsed.path else document
        if not destination.exists():
            errors.append(f'{document.relative_to(ROOT)} -> missing {target}')
            continue
        if parsed.fragment and destination.suffix.lower() == '.md':
            heading = unquote(parsed.fragment)
            if heading not in anchors(destination.read_text(encoding='utf-8-sig')):
                errors.append(f'{document.relative_to(ROOT)} -> missing anchor {target}')
    return errors


def main():
    documents = {ROOT / 'README.md', ROOT / 'openspec/project.md'}
    documents.update(ROOT / 'docs' / name for name in INDEXES)
    documents.update((ROOT / 'docs').glob('[0-9][0-9]-*.md'))
    errors = []
    archived = 0
    manifests = sorted((ROOT / 'docs/archive').glob('*/manifest.json'))
    if not manifests:
        errors.append('No archive manifest found.')
    for manifest_path in manifests:
        manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
        documents.add(manifest_path.parent / 'INDEX.md')
        for item in manifest['files']:
            archive = (ROOT / item['archive']).resolve()
            original = (ROOT / item['original']).resolve()
            replacement = (ROOT / item['replacement']).resolve()
            archive.relative_to(manifest_path.parent.resolve())
            original.relative_to(ROOT)
            replacement.relative_to(ROOT)
            documents.add(original)
            if not replacement.is_file():
                errors.append(f'Missing replacement: {item["replacement"]}')
            if not archive.is_file():
                errors.append(f'Missing archived original: {item["archive"]}')
            elif hashlib.sha256(archive.read_bytes()).hexdigest() != item['sha256']:
                errors.append(f'Archive hash mismatch: {item["archive"]}')
            archived += 1
    for document in sorted(documents):
        if not document.is_file():
            errors.append(f'Missing maintained document: {document.relative_to(ROOT)}')
            continue
        errors.extend(check_links(document))
    for error in errors:
        print(error)
    print(f'Checked {len(documents)} maintained documents and {archived} archived originals; {len(errors)} errors.')
    return 1 if errors else 0


if __name__ == '__main__':
    sys.exit(main())
