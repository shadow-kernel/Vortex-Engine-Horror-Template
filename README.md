<div align="center">

<img src="preview.png" alt="Vortex Horror Starter" width="640"/>

# Vortex Engine — Horror Starter Template

### A ready-to-play first-person horror foundation for the **[Vortex Engine](https://github.com/shadow-kernel/Vortex-Engine)**.

<br/>

[![Vortex Engine](https://img.shields.io/badge/POWERED%20BY-Vortex%20Engine-6C5CE7?style=for-the-badge)](https://github.com/shadow-kernel/Vortex-Engine)
[![License](https://img.shields.io/badge/LICENSE-MIT-3DA639?style=for-the-badge&logo=opensourceinitiative&logoColor=white)](LICENSE)
[![Free](https://img.shields.io/badge/100%25%20FREE-incl.%20commercial-00B894?style=for-the-badge)](#-license)

<br/>

**A dark bunker. A dying flashlight. Something behind the door.**

This is the horror template offered by Vortex Engine's **Create Project** dialog — press Play and you are
inside a pitch-black room with a shadow-casting flashlight, an interactable bunker door and a scripted
jump scare. Everything gameplay is a plain project script you can read, strip down or grow into your own game.

</div>

---

## 🎮 Controls

| Input | Action |
|-------|--------|
| **WASD** + mouse | move + look — CoD-feel grounded acceleration |
| **Shift** | sprint (double-tap **W** = tactical sprint, punches the FOV out) |
| **Ctrl / C** | crouch — hold it while sprinting to **slide** |
| **Space** | jump (view dips on landing) |
| **LMB** | fire (full-auto) · **V** cycles AUTO / SEMI / BURST |
| **RMB** | aim down sights (ADS — narrows spread, tames recoil, zooms) |
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
| `Player/CoDMovement` | CoD-feel grounded movement: accel/friction, sprint + tactical sprint (FOV kick), crouch, slide, jump, landing dip, speed-scaled view bob. Publishes the rig pose to `PlayerRig`. **Drives the camera entity directly** (the main camera must be the top-level entity the movement moves, or the view renders from the floor). |
| `Player/CoDWeapon` | Full-auto/semi/burst fire, ADS, climbing recoil pattern (`CameraFX.Kick`), muzzle-flash light pulse, hitscan + hitmarker, shell ejection, reload, ammo HUD, positional gunshot audio. **Drives the imported weapon model** (`Assets/Models/Viewmodel/vm_mp5.glb`) to follow the camera each frame |
| `Player/ViewmodelPart` | Reusable "rigid part follows the camera via `PlayerRig`" helper (self-moving top-level meshes render in the GameHost; camera-child meshes don't). The stock viewmodel is a single model, but you can pin extra props (a torch, a tablet) to the view with this |
| `Player/FlashlightController` | F-toggle weapon light, battery drain/recharge, flicker, HUD bar — casts **real shadows** |
| `Player/Interactor` / `Player/FootstepAudio` | `[E]` interaction; material-driven footsteps (floor material's footstep clip) |
| `World/SlidingDoor` | Coroutine slide + `Physics.RefreshCollider` so the doorway really opens |
| `World/LightFlicker` | Per-bulb nervous flicker + blackout blinks; generator hum-pulse mode |
| `World/HorrorAtmosphere` | Sets ambient light + starts the looping cellar ambience |

> **Note:** the first-person weapon is a real imported 3D model — an MP5-style SMG with gloved arms and
> hands (`Assets/Models/Viewmodel/vm_mp5.glb`), lit by the scene and canted CoD-style. It's a top-level
> entity the `CoDWeapon` script drives to follow the camera each frame (camera-child meshes don't render
> in the GameHost; self-moving top-level ones do). Fog, bloom, SSAO, vignette and grain are authored in
> the scene. Swap in your own gun by dropping a different `.glb` on the `Weapon` entity and nudging the
> `Off*` fields — models often import tiny, so scale to a ~0.5 m barrel.

The **look** — fog, vignette, film grain — is authored in the editor's **Environment panel** and saved with
the scene: no script needed, and the effects apply to the game camera only (the build viewport stays clean).

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
