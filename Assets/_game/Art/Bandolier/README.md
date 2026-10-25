# Патронташ — отдельная сцена просмотра

Откройте `Assets/_game/Scenes/BandolierPreview.unity` и нажмите Play. Кнопки: повторить, раскрыть, свернуть; ползунок позволяет остановить анимацию в любой точке. Пробел повторяет раскатывание.

## Слои

- `LeatherBase.png` — плоская кожа без патронов и передних петель.
- `Cartridge.png` — целый обычный патрон, используется шесть раз.
- `LeatherLoop.png` — передняя часть кожаной петли, используется шесть раз поверх патронов.
- `BandolierCurl.shader` — спиральное раскатывание, обратная сторона и затенение изгиба.
- `*Mesh.asset` — отдельные сетки основы, петли и жёсткого патрона; `Materials/` — их материалы и проекция контактной тени.

Оригинальные PNG скопированы без изменения пикселей. Прозрачные поля исключаются UV-прямоугольниками сеток при сборке сцены. Кожа и петли изгибаются одним отображением координат. Патроны появляются после выхода из рулона, остаются жёсткими и слегка подпрыгивают при ударе. Под каждым — смещённая чёрная копия исходного изображения для контактной тени. Игровая сцена и экран наград не изменены.

Настройки длительности, радиуса, прогресса и слоёв находятся на `BandolierPreview`. Команда `Tools > Prototype > Create Bandolier Preview Scene` пересобирает только сцену просмотра и её собственные материалы/сетки; сохраните текущую сцену перед использованием команды.

## Происхождение изображений

Сгенерировано встроенным инструментом imagegen по запросу пользователя; это экспериментальная графика для прототипа. Режим: built-in, прозрачный фон. Использованные промпты:

### LeatherBase.png

Generate a production game sprite asset, not a mockup. Single isolated fully flat rectangular leather sheet for a six-slot cartridge roll/bandolier, long horizontal wide composition approximately 3.2:1. Strict orthographic directly top-down view. Transparent background with real alpha. Rich warm chestnut brown aged leather, honey ochre double stitched border, softly rounded slightly irregular worn corners, subtle restrained grain, scuffs and shallow flat wrinkles, dark pixel outline and crisp chunky pixel-art clusters in the visual style of a polished 2D dungeon roguelite. The interior is completely empty uninterrupted leather, because six cartridges and their retaining loops will be layered separately by the game engine. Entire sheet lies FLAT: NO rolled edges, NO curl, NO bulging, NO perspective, NO shadows outside the silhouette. No loops, no pockets, no cartridges, no bullets, no buckles, no straps, no holes, no numbers, no text, no surrounding scene, no background, no decorations in the middle. Warm highlight from upper left consistent throughout. Object fills most of image width with a small transparent margin. Match the warm pixel-art leather bandolier concept discussed, but this is ONLY its underlying leather base layer. Must be a clean reusable flat texture suitable for deforming on a segmented mesh.

### Cartridge.png

Production 2D game sprite, ONE ordinary brass revolver cartridge isolated on a genuinely transparent background. Complete cartridge, fully visible including the middle casing that a separate leather loop will later overlap. Strictly upright vertical orientation with rounded bullet tip pointing up and flat ridged cartridge base below, centered, narrow tall silhouette approximately width:height = 1:3.4. Orthographic straight-on top-down inventory-item sprite, NO perspective lean, no isometric angle. Premium crisp chunky pixel-art style for a warm 2D dungeon revolver roguelite, dark brown outline, stepped pixel highlights. Smooth rounded dull golden copper projectile tip, rich golden brass casing, strong restrained highlight on upper left, darker right edge, raised brass rim at bottom. Classic stylized revolver ammunition, not a modern pointed rifle bullet. No leather, no strap, no loop, no special elemental colors, no symbols, no text, no numbers, no floor, no background, no cast shadow outside silhouette. This will be repeated six times over an empty chestnut leather bandolier base and under six independent leather retaining loops. The cartridge should fill most of the image height with a small transparent margin. One single object only, no variants or sprite sheet.

### LeatherLoop.png

Generate ONE isolated front leather retaining loop sprite for a cartridge bandolier in a warm polished pixel-art dungeon game. Genuinely transparent background. This is a layer that will be placed OVER the middle of a separate upright cartridge: the sprite must depict ONLY the opaque FRONT of one leather loop, no cartridge visible at all, no backing sheet. Single vertical rectangular cuff approximately width:height = 1:1.35, with slightly rounded shoulders, very gently bowed stitched upper and lower hems, and dark narrow side seams. Warm medium chestnut/caramel brown leather, matching a worn honey-stitched leather cartridge roll. Two narrow ochre stitched hems running across top and bottom, medium-brown subtly grained body, restrained highlight toward upper-left, darker side edges so it suggests wrapping around a cylindrical object. Crisp chunky pixel-art clusters, dark brown outline. Strict orthographic front view, straight symmetric upright shape. The cuff surface is SOLID opaque leather across its entire middle because it will occlude a cartridge beneath. Absolutely NO bullet, brass casing, projectile, ammunition tip, cylinder, metallic hardware, labels, numbers, symbols, backgrounds, surrounding floor, multiple loops, holes punched through the center, rolled-up sheet or whole bandolier. ONLY ONE front leather cuff. Object centered and fills most image height with transparent margin. Not a photograph, not a smooth realistic 3D render. Keep it compatible with the generated plain chestnut bandolier base and plain brass cartridge sprite.


Новые варианты находятся в Cartridges/ (девять отдельных PNG без петель, точные промпты в Cartridges/README.md). В тестовой сцене показаны Plain, Needle, Lava, Ice, Ricochet, Fan. Команда Tools > Prototype > Apply Cartridge Art To Bandolier Preview меняет только графику патронов и теней в существующей сцене, сохраняя пользовательские петли.
