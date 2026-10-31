"""Fetch the free Cyrillic extension of Daniel Linssen's m6x11 from its author."""
import http.cookiejar
import json
import re
import urllib.parse
import urllib.request
from pathlib import Path

PAGE = "https://nattogames.itch.io/m6x11cyrillic"
DESTINATION = Path(__file__).resolve().parents[1] / "Assets/_game/Resources/Fonts"


def download_font():
    opener = urllib.request.build_opener(
        urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar())
    )
    opener.addheaders = [("User-Agent", "Mozilla/5.0")]
    markup = opener.open(PAGE, timeout=40).read().decode()
    token_match = re.search(r'name="csrf_token" value="([^"]+)"', markup)
    if token_match is None:
        token_match = re.search(r'name="csrf_token" content="([^"]+)"', markup)
    if token_match is None:
        raise RuntimeError("The free-download page did not provide a CSRF field")
    token = token_match.group(1)
    body = urllib.parse.urlencode({"csrf_token": token}).encode()
    uploads = re.findall(r'data-upload_id="(\d+)"', markup)
    if not uploads:
        reply = json.loads(opener.open(PAGE + "/download_url", data=body, timeout=40).read())
        markup = opener.open(reply["url"], timeout=40).read().decode()
        uploads = re.findall(r'data-upload_id="(\d+)"', markup)
    for upload in dict.fromkeys(uploads):
        reply = json.loads(opener.open(PAGE + "/file/" + upload, data=body, timeout=40).read())
        if "url" not in reply:
            continue
        data = opener.open(reply["url"], timeout=40).read()
        if data[:4] not in (b"\x00\x01\x00\x00", b"OTTO"):
            continue
        DESTINATION.mkdir(parents=True, exist_ok=True)
        destination = DESTINATION / "m6x11cyrillic.ttf"
        destination.write_bytes(data)
        print(f"Saved {destination.name}: {len(data)} bytes")
        return
    raise RuntimeError("No downloadable TTF found on the author's page")


if __name__ == "__main__":
    download_font()
