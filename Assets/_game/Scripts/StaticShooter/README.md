# Static western shooter

Open `Assets/_game/Scenes/Game.unity`, then Play.

- Auto aim is enabled by default. Press F for manual mouse aiming.
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

## Combat presentation prefabs

`Prefabs/StaticShooter/Gunslinger.prefab`: body sprite and editable HandSocket.
`Revolver.prefab`: local +Z is the barrel axis, Muzzle is the shot origin;
a narrow block and two side sprites replace a full 3D model. Visual recoil is
separate from the aiming root. Move HandSocket to adjust the grip. The stage
instantiates the weapon under that socket and aims its root in world space.

`ProjectileVisual.prefab`: camera-facing bright core and glow, colored trail and
status sparks. Diameter and Minimum Pixels control visibility at distance;
Trail time/width and particle settings are editable in the prefab. Visual size
does not increase the collision radius. Speed is 1.4 times the 2D baseline,
reduced from the old 3x speed. The trail survives impact for its configured time.

HP backgrounds and fills have distinct render orders, a small depth gap and
camera-facing orientation. Actual HP fraction drives only fill width, with a
fixed left edge. Hit/death particles and explosions orient to the perspective
camera and render above enemy sprites; existing color-over-lifetime settings
are preserved.

Create initial prefabs: CombatReadabilityAuthoring.Build (also called by the
stage builder). Existing prefabs are retained so Inspector tuning survives.
Validation: CombatPresentationValidation.Run checks health fractions, anchored
fill, sorting, socket parenting and actual 3D muzzle alignment, then runs the
stage's real projectile-hit test and saves a camera render with three colors.

## Physical blood and muzzle flash

`BloodHit3D.prefab` and `BloodDeath3D.prefab` in Prefabs/StaticShooter are independent,
editable ParticleSystem effects for the perspective stage. Their square meshes
spread through XYZ with world gravity, collide with GroundSurface through the
particle physics plane solver, and settle as flat red squares. PhysicalBlood
keeps landed particles still and above the floor until their authored lifetime
and color fade end. No Rigidbody is created per droplet; the floor does not need
a collider that could intercept the aiming ray. Existing 2D blood remains as a
fallback in scenes without StaticShooterStage.

Edit count, speed, gravity, lifetime and color on the blood prefabs. BloodSquares
is double sided and depth tested, so droplets are occluded by actual world depth.
The plane reference is supplied on spawn; it cannot be previewed against the
scene floor from an isolated Prefab Stage. Enemy cleanup follows blood lifetime
instead of deleting the effect after one or two seconds.

Revolver.prefab keeps the muzzle anchor at Z=0.58, just beyond the support barrel.
Its small flash emitter is another 0.035 forward, emits three 0.09–0.15-unit
particles and lasts 25–50 ms. Local simulation keeps the short flash attached to
the weapon. PhysicalBloodAuthoring.Build explicitly updates these assets;
ordinary scene startup does not overwrite particle settings.

PhysicalBloodValidation.Run checks actual hit/death prefab bindings, XYZ spread,
gravity, plane contact, landed orientation/lifetime and muzzle emission. It saves
muzzle-flash.png, blood-airborne.png and blood-landed.png into .utmp/Artifacts in
the validation project. StaticShooterValidation.RunSavedScene remains available
for the existing cursor/projectile regression without rebuilding assets.
