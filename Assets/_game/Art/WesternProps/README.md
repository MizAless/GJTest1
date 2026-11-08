# Western environment props

Ten final PNG sprites generated with built-in imagegen on 2026-11-08, using the user supplied western ruins image as aesthetic reference. Source pixels and alpha are unchanged. The three -v2 assets replace rejected first attempts; rejected art is not included in Assets. Point filtering, alpha-based ground pivots, upright Y-axis rotation and soft XZ shadows provide an editable perspective scene.

The game instantiates `Prefabs/StaticShooter/WesternScenery.prefab` from `StaticShooterArt.WesternSceneryPrefab`. Its 54 objects occupy several depth planes: four houses, lantern post, two banners, three fences, two platforms, three barrels, three boulders, 24 grass clumps and 12 pebble groups. Scenery has no colliders. Central combat lane remains open. Existing background/frame art is retained.

Use `Tools > Prototype > Build western scenery` to import and bind assets while keeping manual prefab placements. `Rebuild western scenery layout` explicitly restores the initial layout. Change object transforms and billboard/shadow settings in the prefab. `WesternSceneryValidation.Run` tests references, object counts, no shot blockers and depth parallax, then the existing perspective-stage movement/hit test.

## Final generation prompts

### Boulder.png

Standalone 2D billboard game sprite for a perspective 3D western gothic shooting gallery. Match reference aesthetic: restrained chunky pixel art, visible square pixel clusters, muted dusty ochre stone, weathered dark brown wood, burgundy accents, soft warm late-afternoon light from upper right. Front three-quarter view, slightly see top surfaces as camera at human shoulder height. Genuine transparent background, no ground plate, no background scene, no text, no interface, no cast shadow baked outside object. Entire object fits within frame with a small transparent margin. One large broken sandstone boulder with layered chipped surfaces and a few small fragments at its feet. Broad irregular angular silhouette.

### Barrel.png

Standalone 2D billboard game sprite for a perspective 3D western gothic shooting gallery. Match reference aesthetic: restrained chunky pixel art, visible square pixel clusters, muted dusty ochre stone, weathered dark brown wood, burgundy accents, soft warm late-afternoon light from upper right. Front three-quarter view, slightly see top surfaces as camera at human shoulder height. Genuine transparent background, no ground plate, no background scene, no text, no interface, no cast shadow baked outside object. Entire object fits within frame with a small transparent margin. One old wooden frontier barrel with dark iron hoops, slightly damaged staves. Upright standing barrel.

### LanternPost.png

Standalone 2D billboard game sprite for a perspective 3D western gothic shooting gallery. Match reference aesthetic: restrained chunky pixel art, visible square pixel clusters, muted dusty ochre stone, weathered dark brown wood, burgundy accents, soft warm late-afternoon light from upper right. Front three-quarter view, slightly see top surfaces as camera at human shoulder height. Genuine transparent background, no ground plate, no background scene, no text, no interface, no cast shadow baked outside object. Entire object fits within frame with a small transparent margin. One tall rough wooden post with short crossbar projecting right at top, hanging amber glowing oil lantern on chain. Freestanding outdoor western lantern post. No structures behind it.

### Saloon.png

Standalone 2D billboard game sprite for a perspective 3D western gothic shooting gallery. Match reference aesthetic: restrained chunky pixel art, visible square pixel clusters, muted dusty ochre stone, weathered dark brown wood, burgundy accents, soft warm late-afternoon light from upper right. Front three-quarter view, slightly see top surfaces as camera at human shoulder height. Genuine transparent background, no ground plate, no background scene, no text, no interface, no cast shadow baked outside object. Entire object fits within frame with a small transparent margin. One two-story weathered wooden western saloon facade, broad false-front roof, narrow second floor balcony, dark door and windows, porch on timber posts. No written lettering. Whole building, feet on common baseline.

### FrontierHouse.png

Standalone 2D billboard game sprite for a perspective 3D western gothic shooting gallery. Match reference aesthetic: restrained chunky pixel art, visible square pixel clusters, muted dusty ochre stone, weathered dark brown wood, burgundy accents, soft warm late-afternoon light from upper right. Front three-quarter view, slightly see top surfaces as camera at human shoulder height. Genuine transparent background, no ground plate, no background scene, no text, no interface, no cast shadow baked outside object. Entire object fits within frame with a small transparent margin. One small weathered western frontier wooden storehouse, pitched dusty roof, porch on two posts, dark windows, simple steps and blank signboard without lettering. Whole building, feet on common baseline.

### Fence.png

Standalone 2D billboard game sprite for a perspective 3D western gothic shooting gallery. Match reference aesthetic: restrained chunky pixel art, visible square pixel clusters, muted dusty ochre stone, weathered dark brown wood, burgundy accents, soft warm late-afternoon light from upper right. Front three-quarter view, slightly see top surfaces as camera at human shoulder height. Genuine transparent background, no ground plate, no background scene, no text, no interface, no cast shadow baked outside object. Entire object fits within frame with a small transparent margin. One standalone short length of old western split rail fence with three posts and two horizontal beams, irregular broken ends. Front view with very slight perspective. Open gaps between rails genuinely transparent.

### Banner.png

Standalone 2D billboard game sprite for a perspective 3D western gothic shooting gallery. Match reference aesthetic: restrained chunky pixel art, visible square pixel clusters, muted dusty ochre stone, weathered dark brown wood, burgundy accents, soft warm late-afternoon light from upper right. Front three-quarter view, slightly see top surfaces as camera at human shoulder height. Genuine transparent background, no ground plate, no background scene, no text, no interface, no cast shadow baked outside object. Entire object fits within frame with a small transparent margin. One tall freestanding wooden standard pole with torn burgundy pennant hanging from crossbar, faded simple gold diamond emblem, no lettering. Dark wooden base stand.

### DryGrass-v2.png

A single small clump of dry prairie GRASS ONLY. 12 irregular ochre grass blades sprouting from one tiny base, a few bent stalks and seed heads. Entire subject is botanical dried grass. Isolated transparent background. Chunky pixel art, muted brown and ochre, warm evening light from upper right. No wood, no signs, no boards, no targets, no buildings, no symbols, no text. Three-quarter front view, whole clump visible with a small margin. 2D environment prop sprite.

### Pebbles-v2.png

Only five small irregular natural sandstone PEBBLES resting beside each other. Squat flat cluster of ochre and dusty grey stones, each a distinct random chipped shape. Isolated transparent background, no ground plane. Chunky pixel art with muted warm evening lighting. No wood, no structures, no targets, no circles painted on stones, no symbols, no text. View from slightly above. Wide low silhouette, entire cluster visible with a small margin. 2D environment prop sprite.

### TimberPlatform-v2.png

A plain low wooden porch platform ONLY, wide rectangular deck of weathered brown planks, thick timber legs and two short steps in front. Completely empty flat top, nothing standing on the deck. Isolated transparent background. Chunky pixel art, muted brown and dusty ochre colors, warm evening light from upper right. View from front and slightly above showing plank top, front and right edge. No boards standing vertically, no wall, no backdrop, no targets, no signs, no symbols, no red circles, no building. Whole platform fits in frame.
