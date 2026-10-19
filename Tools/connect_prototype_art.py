"""Connect author sprites to serialized Unity assets without changing gameplay."""
from pathlib import Path
import re
import uuid

ROOT = Path(__file__).resolve().parents[1]
FRAMES = ROOT / 'Assets/ThirdParty/PrototypeArt/0x72-dungeon-tileset-ii/0x72_DungeonTilesetII_v1.7/frames'
PARTICLES = ROOT / 'Assets/ThirdParty/PrototypeArt/kenney-particle-pack/PNG (Transparent)'
HEADER = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'

def guid(path):
    meta = Path(str(path) + '.meta')
    if meta.exists():
        return re.search(r'^guid: (\w+)', meta.read_text(), re.M).group(1)
    value = uuid.uuid4().hex
    meta.write_text('fileFormatVersion: 2\nguid: ' + value + '\n')
    return value

def sprite(path, ppu=16, pixel=True):
    value = guid(path)
    # Selected source PNGs use one sprite each with the stable single-sprite ID.
    Path(str(path) + '.meta').write_text(f'''fileFormatVersion: 2
guid: {value}
TextureImporter:
  serializedVersion: 12
  internalIDToNameTable: []
  externalObjects: {{}}
  mipmaps:
    enableMipMap: 0
  isReadable: 0
  textureSettings:
    serializedVersion: 2
    filterMode: {0 if pixel else 1}
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  maxTextureSize: 2048
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: {ppu}
  alphaIsTransparency: 1
  textureType: 8
  textureShape: 1
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    overridden: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: {uuid.uuid4().hex}
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
  userData: 
  assetBundleName: 
  assetBundleVariant: 
''')
    return f'{{fileID: 21300000, guid: {value}, type: 3}}'

def blocks(text):
    return re.split(r'(?=^--- !u!)', text, flags=re.M)

def change_block(text, identity, action):
    result = []
    matched = False
    for block in blocks(text):
        if re.match(r'--- !u!\d+ &' + str(identity) + r'\b', block):
            block = action(block)
            matched = True
        result.append(block)
    assert matched, identity
    return ''.join(result)

def replace_field(block, key, value):
    return re.sub(r'^  ' + re.escape(key) + r':.*$', '  ' + key + ': ' + value, block, flags=re.M)

def animation(go, ident, refs, movement=False, mouse=False):
    script = guid(ROOT / 'Assets/_game/Scripts/PrototypeSpriteAnimation.cs')
    return f'''--- !u!114 &{ident}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  _frames:
''' + ''.join('  - ' + ref + '\n' for ref in refs) + f'''  _framesPerSecond: 8
  _faceMovement: {int(movement)}
  _faceMouse: {int(mouse)}
'''

scene_path = ROOT / 'Assets/_game/Scenes/Game.unity'
scene = scene_path.read_text()
assert 'Prototype Dungeon' not in scene, 'Art is already connected'
enemy_path = ROOT / 'Assets/_game/Prefabs/Enemy.prefab'
enemy = enemy_path.read_text()
renderer_template = next(b for b in blocks(enemy) if b.startswith('--- !u!212'))

def renderer(ident, go, ref, order, color='{r: 1, g: 1, b: 1, a: 1}'):
    block = re.sub(r'&\d+', '&' + str(ident), renderer_template, count=1)
    for key, val in [('m_GameObject', f'{{fileID: {go}}}'), ('m_Sprite', ref), ('m_Color', color), ('m_SortingOrder', str(order))]:
        block = replace_field(block, key, val)
    return block

hero_refs = [sprite(FRAMES / f'knight_m_idle_anim_f{i}.png') for i in range(4)]
enemy_refs = [sprite(FRAMES / f'tiny_zombie_run_anim_f{i}.png') for i in range(4)]
scene = change_block(scene, 106797641, lambda b: renderer(106797641, 106797639, hero_refs[0], 10))
scene = change_block(scene, 106797639, lambda b: b.replace('  m_Layer:', '  - component: {fileID: 1900000001}\n  m_Layer:', 1))
scene += animation(106797639, 1900000001, hero_refs, mouse=True)
enemy = change_block(enemy, 3550511817217055350, lambda b: renderer(3550511817217055350, 3637406703804334611, enemy_refs[0], 10))
enemy = change_block(enemy, 3637406703804334611, lambda b: b.replace('  m_Layer:', '  - component: {fileID: 1900000002}\n  m_Layer:', 1))
enemy = change_block(enemy, 8751852636754551746, lambda b: replace_field(b, 'm_LocalScale', '{x: 1, y: 1, z: 1}'))
# Match the visible body while retaining the existing collision component.
enemy = change_block(enemy, 5978370555814383450, lambda b: replace_field(b, 'm_Size', '{x: 0.6, y: 0.7}'))
enemy += animation(3637406703804334611, 1900000002, enemy_refs, movement=True)
enemy_path.write_text(enemy)

# Reusable arena prefab: baked tiles visible immediately in the scene editor.
arena_path = ROOT / 'Assets/_game/Prefabs/PrototypeDungeon.prefab'
arena_guid = guid(arena_path)
objects = []
children = []
counter = 2000000000

def tile(name, ref, x, y, order=-20, scale=1):
    global counter
    counter += 3
    go, tr, sr = counter, counter + 1, counter + 2
    children.append(tr)
    objects.append(f'''--- !u!1 &{go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {tr}}}
  - component: {{fileID: {sr}}}
  m_Layer: 0
  m_Name: {name}
  m_TagString: Untagged
  m_IsActive: 1
--- !u!4 &{tr}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: {x}, y: {y}, z: 0}}
  m_LocalScale: {{x: {scale}, y: {scale}, z: 1}}
  m_ConstrainProportionsScale: 1
  m_Children: []
  m_Father: {{fileID: 2000000001}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
''' + renderer(sr, go, ref, order))

floors = [sprite(FRAMES / f'floor_{i}.png') for i in range(1, 5)]
wall = sprite(FRAMES / 'wall_mid.png')
for y in range(-9, 10):
    for x in range(-15, 16):
        variant = 0 if (x * 31 + y * 17) % 13 > 1 else (x * 17 + y * 13) % 4
        tile(f'Floor {x} {y}', floors[variant], x, y)
for x in range(-15, 16):
    tile('North wall', wall, x, 9, -10)
    tile('South wall', wall, x, -9, -10)
for y in range(-8, 9):
    for x in (-15, 15):
        tile('Side wall', wall, x, y, -10)
for x, y, name in [(-7, 3, 'crate'), (7, -3, 'crate'), (-9, -4, 'column'), (9, 4, 'column'), (-10, 5, 'chest_full_open_anim_f0')]:
    tile(name, sprite(FRAMES / (name + '.png')), x, y, -5)
arena = HEADER + '''--- !u!1 &2000000000
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 2000000001}
  m_Layer: 0
  m_Name: Prototype Dungeon
  m_TagString: Untagged
  m_IsActive: 1
--- !u!4 &2000000001
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 2000000000}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 0.85, y: 0.85, z: 1}
  m_ConstrainProportionsScale: 1
  m_Children:
''' + ''.join(f'  - {{fileID: {tr}}}\n' for tr in children) + '''  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
''' + ''.join(objects)
arena_path.write_text(arena)
scene += f'''--- !u!1001 &1900000010
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {{fileID: 0}}
    m_Modifications: []
    m_RemovedComponents: []
    m_RemovedGameObjects: []
    m_AddedGameObjects: []
    m_AddedComponents: []
  m_SourcePrefab: {{fileID: 100100000, guid: {arena_guid}, type: 3}}
--- !u!4 &1900000011 stripped
Transform:
  m_CorrespondingSourceObject: {{fileID: 2000000001, guid: {arena_guid}, type: 3}}
  m_PrefabInstance: {{fileID: 1900000010}}
  m_PrefabAsset: {{fileID: 0}}
'''
scene = change_block(scene, 9223372036854775807, lambda b: b + '  - {fileID: 1900000011}\n')
scene = change_block(scene, 519420031, lambda b: replace_field(replace_field(b, 'orthographic size', '8'), 'm_BackGroundColor', '{r: 0.06, g: 0.05, b: 0.09, a: 1}'))
scene_path.write_text(scene)

bullet_path = ROOT / 'Assets/_game/Prefabs/Bullet.prefab'
bullet = bullet_path.read_text()
bullet_ref = sprite(PARTICLES / 'spark_01.png', 512, False)
bullet = change_block(bullet, 1340394372576399792, lambda b: renderer(1340394372576399792, 7051222404024946552, bullet_ref, 20, '{r: 1, g: 0.8, b: 0.25, a: 1}'))
bullet_path.write_text(bullet)

ui_path = ROOT / 'Assets/_game/Prefabs/BulletUITemplate.prefab'
ui = ui_path.read_text()
ui_ref = sprite(FRAMES / 'flask_yellow.png')
ui = re.sub(r'  m_Sprite:.*', '  m_Sprite: ' + ui_ref, ui)
ui = ui.replace('  m_PreserveAspect: 0', '  m_PreserveAspect: 1').replace('  m_RaycastTarget: 1', '  m_RaycastTarget: 0')
ui_path.write_text(ui)

# Sprite shader is available in the built-in pipeline and supports particles.
materials = ROOT / 'Assets/_game/Art/Materials'
materials.mkdir(parents=True, exist_ok=True)
def material(name, texture):
    path = materials / (name + '.mat')
    value = guid(path)
    tex = guid(PARTICLES / texture)
    sprite(PARTICLES / texture, 512, False)
    path.write_text(HEADER + f'''--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_Shader: {{fileID: 10755, guid: 0000000000000000f000000000000000, type: 0}}
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {{}}
  disabledShaderPasses: []
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _MainTex:
        m_Texture: {{fileID: 2800000, guid: {tex}, type: 3}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    m_Ints: []
    m_Floats: []
    m_Colors:
    - _Color: {{r: 1, g: 1, b: 1, a: 1}}
''')
    return f'{{fileID: 2100000, guid: {value}, type: 2}}'

for prefab, texture in [('ShootVFX', 'muzzle_01.png'), ('OnDeadVFX', 'spark_04.png'), ('Explosion', 'smoke_01.png')]:
    ref = material(prefab, texture)
    path = ROOT / 'Assets/_game/Prefabs' / (prefab + '.prefab')
    text = path.read_text()
    text = re.sub(r'(  m_Materials:\n)  - .*', lambda m: m.group(1) + '  - ' + ref, text)
    text = re.sub(r'  m_SortingOrder: 0', '  m_SortingOrder: 30', text)
    path.write_text(text)
print('Connected hero/enemy animation, arena prefab, bullet, ammo UI and 3 VFX materials.')
