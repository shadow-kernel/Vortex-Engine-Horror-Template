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
| **WASD** + mouse | move + look — Shift sprint, Ctrl/C crouch, Space jump |
| **F** | flashlight on/off (the battery drains while it burns) |
| **E** | interact — open the bunker door |
| **ESC** | pause (Q quits) |
| **F9** | in-game dev console |

Gamepad works out of the box (left stick move, right stick look, Y flashlight, X interact).

---

## 🧟 What's inside

Every feature is a small, readable **project script** under `Assets/Scripts/` — no gameplay code hides in the engine:

| Script | What it does |
|--------|--------------|
| `Player/PlayerControllerFP` | Quake-feel first-person movement through the engine's collide-and-slide, pause overlay, HUD |
| `Player/FlashlightController` | F-toggle, battery drain/recharge, dying-bulb flicker, HUD bar — the spot light casts **real shadows** |
| `Player/Interactor` | Raycast + `[E]` prompt; sends `"interact"` to anything tagged **Interactable** |
| `Player/FootstepAudio` | Material-driven footsteps: assign a step sound to a floor **material** in the Material Editor and every floor using it just works |
| `World/SlidingDoor` | Coroutine slide + `Physics.RefreshCollider` so the doorway really opens |
| `World/JumpScareTrigger` | Walk past the door: post-FX panic ramp, then the stalker spawns **behind you** |
| `World/MonsterStalker` | Drifts toward you, looms, despawns |
| `World/HorrorAtmosphere` | Crushes the ambient light |

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
No third-party assets: all geometry is engine primitives; footstep sounds are yours to assign.
