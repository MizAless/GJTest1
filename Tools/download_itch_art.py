import http.cookiejar
import json
import re
import urllib.parse
import urllib.request
from download_prototype_art import save_pack, ARCHIVES

page = 'https://0x72.itch.io/dungeontileset-ii'
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
opener.addheaders = [('User-Agent', 'Mozilla/5.0')]
markup = opener.open(page + '/purchase', timeout=40).read().decode()
token = re.search(r'name="csrf_token" value="([^"]+)"', markup).group(1)
body = urllib.parse.urlencode({'csrf_token': token}).encode()
reply = json.loads(opener.open(page + '/download_url', data=body, timeout=40).read())
download_page = reply['url']
markup = opener.open(download_page, timeout=40).read().decode()
# Inspect public file identifiers; never log temporary access URLs.
uploads = re.findall(r'data-upload_id="(\d+)"', markup)
print('Upload identifiers:', uploads, flush=True)
for upload in uploads:
    endpoint = page + '/file/' + upload
    reply = json.loads(opener.open(endpoint, data=body, timeout=40).read())
    url = reply.get('url')
    if not url:
        continue
    try:
        record = save_pack('0x72-dungeon-tileset-ii', page, url, 'CC0 1.0')
        # Temporary download credentials do not belong in project metadata.
        record['download'] = page + '/purchase'
        from download_prototype_art import ART
        (ART / record['name'] / 'SOURCE.json').write_text(json.dumps(record, indent=2), encoding='utf-8')
        manifest = ARCHIVES / 'manifest.json'
        records = json.loads(manifest.read_text(encoding='utf-8'))
        records.append(record)
        manifest.write_text(json.dumps(records, indent=2), encoding='utf-8')
        break
    except Exception as error:
        print('File is not the ZIP pack:', type(error).__name__, flush=True)
else:
    raise RuntimeError('No ZIP downloaded')
