"""Download free art from author pages; preserve archives and licenses."""
import concurrent.futures
import hashlib
import html
import io
import json
from pathlib import Path
import re
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
ARCHIVES = ROOT / 'ArtDownloads'
ART = ROOT / 'Assets' / 'ThirdParty' / 'PrototypeArt'
PACKS = ['top-down-shooter', 'tiny-dungeon', 'tiny-battle', 'particle-pack', 'ui-pack']

def fetch(url):
    req = urllib.request.Request(url, headers={'User-Agent': 'PrototypeArtDownloader/1.0'})
    return urllib.request.urlopen(req, timeout=60).read()

def save_pack(name, page, url, license_name):
    data = fetch(url)
    archive = ARCHIVES / (name + '.zip')
    archive.write_bytes(data)
    folder = ART / name
    folder.mkdir(parents=True, exist_ok=True)
    count = 0
    with zipfile.ZipFile(io.BytesIO(data)) as z:
        bad = z.testzip()
        if bad:
            raise ValueError('Corrupt archive: ' + bad)
        for entry in z.infolist():
            dest = (folder / entry.filename).resolve()
            if not dest.is_relative_to(folder.resolve()):
                raise ValueError('Unsafe archive path')
            # Import art/documentation only, never third-party executable code.
            if entry.is_dir() or '__MACOSX' in Path(entry.filename).parts or dest.name.startswith('._') or dest.suffix.lower() not in {'.png', '.jpg', '.jpeg', '.svg', '.xml', '.txt', '.md', '.pdf', '.json', '.ttf', '.otf'}:
                continue
            dest.parent.mkdir(parents=True, exist_ok=True)
            dest.write_bytes(z.read(entry))
            count += 1
    public_url = page + '/purchase' if '.itch.io/' in page else url
    record = dict(name=name, source=page, download=public_url, license=license_name,
                  bytes=len(data), sha256=hashlib.sha256(data).hexdigest(), imported_files=count)
    (folder / 'SOURCE.json').write_text(json.dumps(record, indent=2), encoding='utf-8')
    print(json.dumps(record), flush=True)
    return record

def kenney(name):
    page = 'https://kenney.nl/assets/' + name
    markup = fetch(page).decode()
    links = re.findall(r'href=[\"\']([^\"\']+\.zip)[\"\']', markup)
    if not links:
        raise ValueError('Download not found: ' + page)
    return save_pack('kenney-' + name, page, html.unescape(links[0]), 'CC0 1.0')

if __name__ == '__main__':
    ARCHIVES.mkdir(exist_ok=True)
    ART.mkdir(parents=True, exist_ok=True)
    results = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=5) as pool:
        futures = {pool.submit(kenney, name): name for name in PACKS}
        for future in concurrent.futures.as_completed(futures):
            try:
                results.append(future.result())
            except Exception as error:
                print('FAILED ' + futures[future] + ': ' + str(error), flush=True)
    (ARCHIVES / 'manifest.json').write_text(json.dumps(results, indent=2), encoding='utf-8')
