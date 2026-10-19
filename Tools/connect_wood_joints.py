from pathlib import Path
import re
import uuid

root = Path(__file__).resolve().parents[1]
script = root / 'Assets/_game/Scripts/WoodFloorJoints.cs'
meta = Path(str(script) + '.meta')
value = re.search(r'^guid: (\w+)', meta.read_text(), re.M)[1] if meta.exists() else uuid.uuid4().hex
meta.write_text('fileFormatVersion: 2\nguid: ' + value + '\n')
prefab = root / 'Assets/_game/Prefabs/PrototypeDungeon.prefab'
text = prefab.read_text()
if 'm_Name: Staggered board joints' in text:
    print('Joint mesh is already connected.')
    raise SystemExit(0)
parts = re.split(r'(?=^--- !u!)', text, flags=re.M)
renderer = next(b for b in parts if b.startswith('--- !u!212'))
renderer = re.sub(r'^--- !u!212 &\d+\nSpriteRenderer:', '--- !u!23 &3000000003\nMeshRenderer:', renderer, flags=re.M)
renderer = re.sub(r'  m_GameObject:.*', '  m_GameObject: {fileID: 3000000000}', renderer)
renderer = re.sub(r'  m_SortingOrder:.*', '  m_SortingOrder: -19', renderer)
renderer = renderer[:renderer.index('  m_Sprite:')]
for i, block in enumerate(parts):
    if block.startswith('--- !u!4 &2000000001\n'):
        parts[i] = block.replace('  m_Father:', '  - {fileID: 3000000001}\n  m_Father:')
text = ''.join(parts) + '''--- !u!1 &3000000000
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 3000000001}
  - component: {fileID: 3000000002}
  - component: {fileID: 3000000003}
  - component: {fileID: 3000000004}
  m_Layer: 0
  m_Name: Staggered board joints
  m_TagString: Untagged
  m_IsActive: 1
--- !u!4 &3000000001
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 3000000000}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 1
  m_Children: []
  m_Father: {fileID: 2000000001}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!33 &3000000002
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 3000000000}
  m_Mesh: {fileID: 0}
''' + renderer + f'''--- !u!114 &3000000004
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 3000000000}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {value}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  _boardLength: 3
  _rowShift: 1
  _jointColor: {{r: 0.7058824, g: 0.3607843, b: 0.1411765, a: 1}}
'''
prefab.write_text(text)
print('Connected a single joint mesh: each board row is shifted by one third of a board length.')
