# Static western prototype art

Generated using built-in imagegen, 2026-11-02. Reference: user supplied western sunset ruins. Original PNG pixels and transparency preserved; no image postprocessing. Each image generated separately. Backdrop and ground are opaque; other layers have alpha. Import uses Point filtering and uncompressed textures.

These sprites are arranged in a perspective 3D world. Ground is an XZ plane; scenery is at different Z distances; characters remain upright sprites. Shadows are soft procedural radial sprites on horizontal planes.

## Generation prompts

### Backdrop.png

Transparent: false

Production 2D game asset, richly detailed pixel art, chunky crisp pixel clusters, warm dusty ochre and burgundy western gothic ruins at sunset, consistent sunset rim light, no text, no UI, no watermarks. Wide 16:9 background layer only: pink orange sunset sky, sun near upper right, hazy distant ruined frontier fortress with broken stone towers and arches. Horizon at 62 percent height. Empty haze below horizon. No foreground objects, no characters. Atmospheric distant silhouettes. Designed as backmost parallax layer.

### Ground.png

Transparent: false

Production 2D game asset, richly detailed pixel art, chunky crisp pixel clusters, warm dusty ochre and burgundy western gothic ruins at sunset, consistent sunset rim light, no text, no UI, no watermarks. Wide 16:9 ground plate only for over the shoulder stationary shooter. Flat open ochre dirt courtyard, rough stone slabs, tiny dry grasses at margins, coherent ground plane perspective vanishing to center at top edge. NO sky, buildings, large rocks, characters, weapons. Ground fills entire image, near ground large details bottom, small details recede to distant top. Central clear combat lane.

### SideRuins.png

Transparent: true

Production 2D game asset, richly detailed pixel art, chunky crisp pixel clusters, warm dusty ochre and burgundy western gothic ruins at sunset, consistent sunset rim light, no text, no UI, no watermarks. Wide 16:9 transparent environmental overlay. Large broken stone arch and wooden post with glowing hanging lantern occupy far LEFT 25 percent. At far RIGHT 20 percent ruined stone column and two wooden crates. The middle 55 percent and all sky area are completely transparent. Feet of objects along bottom. Medium distance scenery layer. No floor, no background, no characters. True transparent alpha.

### Foreground.png

Transparent: true

Production 2D game asset, richly detailed pixel art, chunky crisp pixel clusters, warm dusty ochre and burgundy western gothic ruins at sunset, consistent sunset rim light, no text, no UI, no watermarks. Wide 16:9 transparent environmental overlay. Close up old wooden barrel, broken fence and small dry grass at bottom RIGHT corner only. A low wooden crate and broken sandstone blocks bottom LEFT corner only. Center 65 percent transparent and upper 70 percent transparent. Foreground parallax framing for shooting gallery, do not cover central combat lane. No background or floor or characters. True transparent alpha.

### Hero.png

Transparent: true

Production 2D game asset, richly detailed pixel art, chunky crisp pixel clusters, warm dusty ochre and burgundy western gothic ruins at sunset, consistent sunset rim light, no text, no UI, no watermarks. One full body cowboy gunslinger seen from behind, three-quarter over-left-shoulder view, broad black hat and ragged deep red cape blowing to left, dark coat and boots. Facing diagonally forward and RIGHT, right arm extended aiming into scene. Empty right hand ready to hold a separate weapon; NO gun drawn, NO weapon. Character standing, feet at bottom, head at top, whole silhouette within canvas. Large readable silhouette for lower-left foreground. Transparent background.

### Revolver.png

Transparent: true

Production 2D game asset, richly detailed pixel art, chunky crisp pixel clusters, warm dusty ochre and burgundy western gothic ruins at sunset, consistent sunset rim light, no text, no UI, no watermarks. Single steel six shot revolver sprite, SIDE PROFILE pointing RIGHT, black wood grip angled down left, brass cylinder details, small warm sunset glint. Horizontal barrel. Isolated silhouette only with transparent background. No hand, no character, no environment. Entire weapon fits canvas with small margin.

### Enemy.png

Transparent: true

Production 2D game asset, richly detailed pixel art, chunky crisp pixel clusters, warm dusty ochre and burgundy western gothic ruins at sunset, consistent sunset rim light, no text, no UI, no watermarks. One full body skeletal outlaw raider facing camera, standing leaning forward as if running toward viewer, red bandana, skull face, ragged dark leather vest, old brown boots, small rusty hatchet in one hand, clearly readable threatening silhouette. Both feet visible at bottom. No background, no floor, no shadow baked in, no UI or healthbar. Transparent background. Game enemy sprite not illustration scene.

### GroundTile.png

Additional built-in imagegen generation for the real XZ ground plane: seamless top-down ochre dirt and sandstone material, no perspective, no horizon, no objects; restrained pixel-art clusters and even warm lighting. Uses Repeat wrapping, mipmaps and Bilinear filtering on a 15x15 tiled ground material. Ground.png is retained as the initial painted-floor fallback; the 3D scene uses GroundTile.png.
