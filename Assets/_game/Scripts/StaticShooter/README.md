# Static western shooter

Open `Assets/_game/Scenes/Game.unity`, then Play.

- Mouse: manual aim (default).
- Left click or Space: fire the next cartridge.
- F: switch manual / automatic aim. Automatic aim falls back to cursor without enemies.
- R: restart the run.

The stage uses a perspective camera, an actual XZ ground plane and 3D positions.
Enemies advance toward the fixed player at (0, 0, -5). Their world size depends
only on the health/type; perspective provides distance scaling. World-space
sprites form separate scenery planes; a small camera translation produces
parallax. The cowboy and revolver are separate camera-facing foreground sprites.
Soft procedural radial shadows lie on XZ planes under actors and props.

Combat retains AmmoItem, profiles, status ticks, intrinsic/extra triggers,
wave progression, player health and the upgrade bandolier. In this stage bullets
use swept 3D sphere casts against child hitboxes; piercing, ricochet and split
retain the shared shot budget. Existing 2D code remains as a fallback for old
validation scenes; disabled 2D components on enemies are not used for this mode.
No new third-party dependencies.

Art is in `Art/StaticShooter`, with prompts and generation provenance in README.
`StaticShooterArt.asset` holds sprite/material references. The stage is a scene
object. `Tools > Prototype > Build static shooter scene` imports art and updates
that scene object without rebuilding the gameplay prefabs or bandolier.

Validation: `StaticShooterValidation.Run` (Unity Editor, batchmode with graphics).
It checks perspective, Z approach, ray/3D hitboxes, projectile damage, aim modes
and shadows, then saves `.utmp/Artifacts/static-shooter.png`.

First-pass limits: one rigid sprite per character (small rocking motion),
visual weapon rotation rather than skeletal arm animation, no cover geometry
or navigation around scenery. Scenery is visual; the combat field stays open.
