import json
from download_prototype_art import kenney, ARCHIVES

record = kenney('roguelike-indoors')
manifest = ARCHIVES / 'manifest.json'
records = json.loads(manifest.read_text(encoding='utf-8'))
records = [r for r in records if r['name'] != record['name']]
records.append(record)
manifest.write_text(json.dumps(records, indent=2), encoding='utf-8')
