"""Read-only source scan. Classify Topaz by filename, including a merged PNG folder.

Writes the chosen catalog only; never edits, moves, or deletes source images.
Requires Pillow. Usage: python AssetCatalog.py --root PATH --output PATH
"""
from pathlib import Path
from datetime import datetime, timezone
import argparse
import hashlib
import json
import re
import unicodedata
from PIL import Image

TOPAZ = re.compile(r'\s+Topaz\s+Gigapixel\s+4x(?:\s+scale)?$', re.IGNORECASE)


def key(stem):
    return unicodedata.normalize('NFC', stem).casefold()


def catalog(root):
    entries = []
    # The legacy folder is transitional fallback; primary copies sort first.
    for folder in [root / 'png', root / 'Topaz Gigapixel 4x']:
        if not folder.exists():
            continue
        for path in sorted(folder.rglob('*')):
            if not path.is_file() or path.suffix.lower() != '.png':
                continue
            record = dict(path=str(path), name=path.name,
                          kind='topaz4x' if TOPAZ.search(path.stem) else 'original')
            try:
                # The user can still be saving/moving files while this snapshot runs.
                before = path.stat()
                digest = hashlib.sha256(path.read_bytes()).hexdigest()
                with Image.open(path) as im:
                    im.load()
                    record.update(size=list(im.size), mode=im.mode,
                                  alpha=im.getchannel('A').getextrema() if 'A' in im.getbands() else None)
                after = path.stat()
                record.update(sha256=digest, bytes=after.st_size, mtime=after.st_mtime,
                              status='ready' if (before.st_size, before.st_mtime_ns) == (after.st_size, after.st_mtime_ns) else 'changed-during-scan')
            except (OSError, ValueError, SyntaxError) as error:
                record.update(status='retry-later', error=str(error))
            entries.append(record)
    originals = {}
    seen = {}
    for record in entries:
        if record['kind'] == 'original' and record['status'] == 'ready':
            originals.setdefault(key(Path(record['name']).stem), record['path'])
        identity = (record['name'].casefold(), record.get('sha256'))
        if identity in seen:
            record['duplicateOf'] = seen[identity]
        elif record['status'] == 'ready':
            seen[identity] = record['path']
    for record in entries:
        if record['kind'] == 'topaz4x':
            record['original'] = originals.get(key(TOPAZ.sub('', Path(record['name']).stem)))
    unique = [r for r in entries if 'duplicateOf' not in r]
    return dict(scannedUtc=datetime.now(timezone.utc).isoformat(), root=str(root),
                primaryFolder=str(root / 'png'),
                counts={kind:sum(r['kind'] == kind for r in unique) for kind in ['original','topaz4x']},
                readable=sum(r['status'] == 'ready' for r in unique),
                matchedUpscales=sum(r['kind'] == 'topaz4x' and bool(r.get('original')) for r in unique),
                duplicateCopies=len(entries)-len(unique), entries=entries)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = catalog(args.root)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k != 'entries'}, ensure_ascii=False))
