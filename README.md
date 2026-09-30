<div align="center">

<img src="preview.png" alt="Vortex Horror Starter" width="640"/>

# Vortex Engine — Horror Starter Template

### A ready-to-play first-person horror foundation for the **[Vortex Engine](https://github.com/shadow-kernel/Vortex-Engine)**.

<br/>

[![Vortex Engine](https://img.shields.io/badge/POWERED%20BY-Vortex%20Engine-6C5CE7?style=for-the-badge)](https://github.com/shadow-kernel/Vortex-Engine)
[![License](https://img.shields.io/badge/LICENSE-MIT-3DA639?style=for-the-badge&logo=opensourceinitiative&logoColor=white)](LICENSE)
[![Free](https://img.shields.io/badge/100%25%20FREE-incl.%20commercial-00B894?style=for-the-badge)](#-license)

<br/>

**A fenced-off compound at night. Street lamps humming. Something moving between the containers.**

This is the horror template offered by Vortex Engine's **Create Project** dialog — press Play and you stand at
the gate of the **Yard**: an open 120 × 120 m industrial compound (warehouses, shipping containers, a checkpoint,
a storage yard, 30+ CC0 props) under a dim moon, with a CoD-style two-weapon loadout in your hands. The original
brick **Cellar** ships as a second scene. Everything gameplay is a plain project script you can read, strip down
or grow into your own game.

</div>

---

## 🎮 Controls

| Input | Action |
|-------|--------|
| **WASD** + mouse | move + look — CoD-feel grounded acceleration |
| **Shift** | sprint (double-tap **W** = tactical sprint, punches the FOV out) — no run-and-gun: pulling the trigger drops the sprint, the gun comes up first (sprint-out) |
| **Ctrl / C** | crouch — hold it while sprinting to **slide** |
| **Space** | jump (view dips on landing; jumping into chest-high cover auto-mantles onto it) |
| **LMB** | fire (full-auto) · **V** cycles AUTO / SEMI / BURST |
| **RMB** / **Left Alt** | aim down sights (ADS — narrows spread, tames recoil, zooms; mouse sensitivity scales with the FOV like CoD). Trackpad? Use Left Alt, or switch **ESC ▸ Aim: Toggle** (press once to aim, again to lower) |
| **1 / 2** · wheel | switch weapon (Vityaz · MP5) — holster / draw timeline |
| **Q / E** | lean left / right (R6-style toggle: the camera shifts and rolls, the feet stay put) |
| **R** | reload |
| **F** | weapon flashlight on/off (battery drains) |
| **E** | interact — open a sliding bunker door |
| **ESC** | pause (Q quits) |

Gamepad works out of the box (left stick move, right stick look, RT fire, LT aim, X reload).

---

## 🧟 What's inside

Every feature is a small, readable **project script** under `Assets/Scripts/` — no gameplay code hides in the engine:

| Script | What it does |
|--------|--------------|
| `Player/CoDMovement` | CoD-feel grounded movement: accel/friction, sprint + tactical sprint (FOV kick), crouch, slide, jump + auto-mantle, landing dip, speed-scaled view bob, camera recoil spring, ADS sensitivity scaling, Q/E lean. Publishes the rig pose to `PlayerRig`. **Drives the camera entity directly** (the main camera must be the top-level entity the movement moves). |
| `Player/LocomotionController` | ONE script for both player rigs: the third-person body (`WCharakter`, locomotion state machine, spine aim, masked fire/reload clips, support-hand IK) and the first-person arms (`FP_Arms`, `FirstPerson = true`) which hand themselves to… |
| `Player/ViewmodelRig` | …the **procedural CoD-style viewmodel**: camera-space weapon poses (hip / ADS / sprint / reload), look sway, walk bob, breathing, recoil spring; pins the (hidden) shoulders below the eye so the arms enter from the bottom corners, IKs BOTH hands onto the weapon (auto-grip computed from the rig's hand geometry: palm on the grip, fingers wrapped, support hand cupping the handguard), hides torso/head/legs. No first-person animation clips needed. |
| `Weapons/Weapon` · `Firearm` · `AssaultRifle` · `Pistol` · `Knife` · `WeaponLoadout` | The weapon system (classes + inheritance, one loadout object spawning an FP and a 3P copy of each weapon prefab): fire/ADS/reload logic, ammo, per-prefab viewmodel poses + grip points, shot feedback (viewmodel + camera kick, muzzle flash light + mesh, ejected shells), reload choreography with sound cues. See `WEAPONS_GUIDE.md`. |
| `Player/FlashlightController` | F-toggle weapon light, battery drain/recharge, flicker, HUD bar — casts **real shadows** |
| `Player/Interactor` / `Player/FootstepAudio` | `[E]` interaction; material-driven footsteps (floor material's footstep clip) |
| `World/SlidingDoor` | Coroutine slide + `Physics.RefreshCollider` so the doorway really opens |
| `World/LightFlicker` | Per-bulb nervous flicker + blackout blinks; generator hum-pulse mode |
| `World/HorrorAtmosphere` | Sets ambient light + starts the looping cellar ambience |

> **Note:** the first-person view is the SAME skinned character as the world body (a second instance, render
> layer 1) with the torso hidden, holding a real imported weapon model (`Assets/Models/Viewmodel/vm_vityaz_body.glb`
> + magazine) that is bone-attached to the hand. Everything you see in first person is computed by
> `ViewmodelRig` from the weapon prefab's fields — swap in your own gun by building a prefab with the mesh, a
> `Mag` child and the `AssaultRifle`/`Pistol` script, then set its sight / grip points (`WEAPONS_GUIDE.md`).
> `CHARACTER_SETUP_GUIDE.md` explains the two-rig player and the live-tuning hooks.

The **look** — fog, vignette, film grain, bloom — is authored in the editor's **Environment panel** and saved with
the scene: no script needed, and the effects apply to the game camera only (the build viewport stays clean).

### Scenes

| Scene | What it is |
|-------|------------|
| `Yard` (start scene) | Open night compound: perimeter wall + gate, entry road with a guard house and a barrier checkpoint, three lanes of cover (containers, road barriers, crates, barrels), a loading dock, warehouse A (brick, open front) and warehouse B (concrete, roller doors) with interiors, an outdoor storage yard and a dirt strip with rocks and dead trees. Lit by a dim blue moon (soft shadows), warm street lamps, cold security spots and interior lights, with fog / bloom / vignette / grain. Props are CC0 glTF models from [Poly Haven](https://polyhaven.com) (`Assets/Models/Props`, each with a box collider); ground and walls use CC0 PBR texture sets (`Assets/Textures`, materials in `Assets/Materials`). |
| `Demo` | The original closed brick cellar (generator hall, side stores, sliding doors, flickering bulbs). |

Both scenes contain the same **Player** rig (camera + movement, `FP_Arms`, `WCharakter`, `Loadout`), so anything
you change on the player works in both — copy the `Player` entity into your own scene to start from it.

### Physics

The Yard's barrels, crates, jerrycans, propane tanks, tyres, boxes and bags are **rigid bodies** (Collider +
Rigidbody, masses 2–26 kg) simulated by the engine's Jolt-based physics: shoot them (`Weapon.BulletImpulse`, N·s
per hit), shove them by walking into them, stack them. Everything is authored on the entities — no physics code in
the template beyond the one impulse line in `Weapon.cs`. See `Managed/README.md` ▸ Physics for the components and
the script API (`Physics.AddImpulse`, `OverlapSphere`, triggers, kinematic doors).

> The player rig follows Vortex's one-behaviour-per-entity rule: the player's scripts live on child
> entities (**Camera / Flashlight / Feet / Hands**).

---

## 🚀 Using it

Pick **Horror Starter** in Vortex Engine's *Create Project* dialog — this repository ships with the engine
as a git submodule and appears there automatically. Open `NewProjectScripts.sln` for full IntelliSense on
the gameplay scripts.

---

## 📄 License

MIT — 100% free, including commercial use. See [LICENSE](LICENSE).
The level is engine primitives; the first-person weapon (`vm_mp5.glb`) is a procedurally-built CC0 model,
and the footstep/gunshot/ambience assets are generated (procedural audio + solid-colour textures). Everything
ships CC0 with the template — no external downloads required.
