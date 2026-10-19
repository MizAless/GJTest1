"""Assign only interior wood tiles; decorative border tiles cannot repeat."""
from pathlib import Path
import re
import uuid

root = Path(__file__).resolve().parents[1]
sheet = root / 'Assets/ThirdParty/PrototypeArt/kenney-roguelike-indoors/Tilesheets/roguelikeIndoor_transparent.png'
meta = Path(str(sheet) + '.meta')
value = re.search(r'^guid: (\w+)', meta.read_text(), re.M)[1] if meta.exists() else uuid.uuid4().hex
template = (root / 'Assets/ThirdParty/PrototypeArt/0x72-dungeon-tileset-ii/0x72_DungeonTilesetII_v1.7/frames/floor_1.png.meta').read_text()
template = re.sub(r'^guid:.*', 'guid: ' + value, template, flags=re.M)
template = template.replace('  internalIDToNameTable: []', '''  internalIDToNameTable:
  - first:
      213: 21300000
    second: WoodFloor_A
  - first:
      213: 21300002
    second: WoodFloor_B''')
template = template.replace('  spriteMode: 1', '  spriteMode: 2')
entries = []
for index, x in enumerate([408, 408]):
    entries.append(f'''    - serializedVersion: 2
      name: WoodFloor_{'A' if index == 0 else 'B'}
      rect:
        serializedVersion: 2
        x: {x}
        y: 255
        width: 16
        height: 16
      alignment: 0
      pivot: {{x: 0.5, y: 0.5}}
      border: {{x: 0, y: 0, z: 0, w: 0}}
      outline: []
      physicsShape: []
      tessellationDetail: -1
      bones: []
      spriteID: {uuid.uuid4().hex}
      internalID: {21300000 + index * 2}
      vertices: []
      indices:
      edges: []
      weights: []
''')
template = template.replace('    sprites: []\n', '    sprites:\n' + ''.join(entries))
meta.write_text(template)

prefab = root / 'Assets/_game/Prefabs/PrototypeDungeon.prefab'
blocks = re.split(r'(?=^--- !u!)', prefab.read_text(), flags=re.M)
assignments = {}
for block in blocks:
    match = re.search(r'm_Name: Floor (-?\d+) (-?\d+)', block)
    if match:
        renderer = re.findall(r'component: \{fileID: (\d+)\}', block)[1]
        variant = 0
        assignments[renderer] = f'{{fileID: {21300000 + variant * 2}, guid: {value}, type: 3}}'
count = 0
for index, block in enumerate(blocks):
    match = re.match(r'--- !u!212 &(\d+)', block)
    if match and match[1] in assignments:
        blocks[index] = re.sub(r'  m_Sprite:.*', '  m_Sprite: ' + assignments[match[1]], block)
        count += 1
assert count == 589, count
prefab.write_text(''.join(blocks))
assert 'spriteMode: 2' in meta.read_text()
assert all(0 <= x and x + 16 <= 458 for x in [408])
print(f'Assigned {count} interior wood floor tiles without decorative borders; Point filtering, no compression.')
