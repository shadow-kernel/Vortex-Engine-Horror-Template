# Vortex — Spieler-Charakter: EIN Rig, ZWEI Instanzen (CoD-Style, v2.7)

Der Spieler besteht aus **zwei Instanzen desselben Vollkörper-Prefabs** — beide fahren mit
**demselben Script** (`LocomotionController`), nur der `FirstPerson`-Haken unterscheidet sie:

| | **WCharakter (World-Body)** | **FP_Arms (FP-Viewmodel)** |
|---|---|---|
| Wer sieht es | **alle ANDEREN Kameras** (P-Debug-Cam, Killcams, andere Spieler) | **NUR deine Kamera** |
| RenderLayer (Meshes) | **2** (Third-Person only) | **1** (Viewmodel-Pass — eigenes FOV, kein Wand-Clipping) |
| Was | derselbe volle Charakter (`tp_character`) | **DASSELBE Prefab, zweite Instanz** — Kopf/Hals/Beine versteckt |
| `LocomotionController` | `FirstPerson = false` | `FirstPerson = true` |
| Pose | aufrecht an den Füßen, nur Yaw, Wirbelsäule zielt | kamera-fixiert am (versteckten) Kopf, pitcht als Einheit |
| Clips | volle Locomotion-State-Machine | `aim` (Idle) + `rifle_run` (Bewegung) — immer geschultert |

> **Neu in v2.7 — Vollkörper GEHT jetzt als FP-Viewmodel.** Früher galt: „ein Vollkörper an der
> Kamera blobbt, das Becken lässt sich nie ausblenden". Seit v2.7 kollabiert
> `SetBoneScaleOverride(bone, 0)` den Knochen **auf der GPU-Skinning-Palette** — das Limb
> verschwindet wirklich, während IK und Bone-Attachments weiterlaufen. Darum braucht es kein
> separates Arme-Mesh mehr: **FP = zweite Instanz des Weltkörpers** mit versteckten Bones.
> Beide Rigs lesen dieselben `PlayerRig`-Statics und spielen dieselben Clips → FP- und
> Third-Person-Animation sind **per Konstruktion identisch** (auch der Mag-Zug beim Nachladen).

### RenderLayer setzen (so sagst du's der Engine)
Es gibt **3 Layer**, gewählt im **Inspector → MeshRenderer → Layer**-Dropdown:
- **World (Layer 0)**: Welt-Pass, alle Cams → Level-Geometrie, NPCs, Showcase-Charaktere.
- **First-Person / viewmodel (Layer 1)**: nur deine Cam, eigener FOV, kein Wand-Clip/Schatten → das FP_Arms-Rig + FP-Waffeninstanz.
- **Third-Person only (Layer 2)**: „was ANDERE sehen" — im Editor normal sichtbar, **im Spiel für dich ausgeblendet** → WCharakter + 3P-Waffeninstanz (gegen den Doppel-Körper im FP).

Der Layer sitzt am **MeshRenderer, also pro Mesh** — beim Vollkörper-Prefab stellst du **alle
Mesh-Kinder** um (Surface + Joints). (In der `.ventity` steht dann `"renderLayer": 0/1/2`.)
Waffen brauchst du NICHT von Hand umzustellen — das `WeaponLoadout` erzwingt den Layer per
`Scene.SetRenderLayer` auf jede gespawnte Instanz (→ `WEAPONS_GUIDE.md`).

### So verhält sich Layer 1 im EDITOR
- Im normalen Build-Viewport sind Layer-1-Meshes **ausgeblendet** (sie gehören dem FP-Pass des Spiels). Im Scene Tree tragen sie einen lila **FP**-Chip (Layer 2 einen grünen **3P**-Chip).
- Zum **Platzieren**: Toolbar-Toggle **„FP"** (View-Options-Pill) → zeigt sie als normale Welt-Geometrie.
- Zum **Live-Testen wie im Spiel**: **View: FP Preview (In-Game)** im Viewport-Dropdown → rendert exakt den Spiel-Frame (FP-Pass mit eigenem FOV + Depth-Clear, Layer 2 versteckt), Freecam bleibt fliegbar. In Splitscreen-Layouts kann eine Pane **„Editor Camera (Free)"** daneben die freie Welt-Sicht zeigen.
- Das **Auge** im Scene Tree blendet wirklich aus (Entity + Kinder, nur Editor-Session, wird nicht gespeichert).

### In-Game Debug-Freecam (dich selbst in Third Person sehen)
Während du spielst (▶ Play im Viewport ODER standalone `--project`): **Taste `P`** → die Kamera löst
sich vom Spieler und du fliegst frei umher, um dich **von außen** anzusehen. Nur im Editor/Debug —
aus einem exportierten Release ist das rausgestrippt.
- **WASD/QE** fliegen, **Shift** schneller, **Maus** schauen. Nochmal **`P`** = zurück ins Spiel.
- **CAPS LOCK** schaltet um, WER die Eingaben bekommt — **gleich im Viewport-Play UND standalone**:
  - **CAPS AN** = ALLE Eingaben (Maus + Tasten, inkl. RMB=ADS) steuern den **SPIELER**, die Cam
    friert ein → du spielst normal und schaust dir von einem festen Punkt aus zu (Laufen, Feuern,
    Nachladen samt Mag-Zug in Third Person prüfen).
  - **CAPS AUS** = du fliegst die Freecam, der Spieler steht.
- Beim Rausfliegen wird **sauber wie für andere Kameras** gerendert: dein **World-Body (Layer 2)**
  wird sichtbar, dein **FP-Viewmodel (Layer 1)** verschwindet — also weder unsichtbarer Körper noch
  schwebende Arme. Genau so verifizierst du, dass Charakter + Waffe im Third-Person sauber sitzen
  und animieren.
- Ein kleiner Hinweis „P = free camera" steht während des Spielens unten links im Bild.

---

> **Körper kippt beim Runterschauen?** Wenn dein World-Charakter ein **Kind der Kamera** ist, erbt er
> deren Pitch und kippt beim Hoch-/Runterschauen mit. RICHTIG: der Körper bleibt **aufrecht** (nur Yaw),
> das Zielen nach oben/unten macht die **Wirbelsäule**. Der `LocomotionController` erledigt das
> automatisch (`FollowPlayer`=an, `FirstPerson`=aus): jeden Frame `SetWorldPose(FootPos, yaw-only)` +
> additive Spine-Rotation aus `PlayerRig.AimPitch` (verteilt auf `SpineBones`).
> Falls der Körper in die falsche Richtung lehnt: `SpineAimSign` auf `-1`. Schaut der Körper von der
> Blickrichtung WEG (Mixamo-Rigs stehen im Engine-Raum mit dem Gesicht nach −Z): `BodyYawOffset` (Default
> `180`). Ein Showcase-NPC (steht fest): `FollowPlayer`=aus.


## Blaupausen: nicht alles neu machen

Im Template liegen zwei fertige Prefabs, die du in JEDE Szene (auch in eigenen Projekten — Ordner `Assets/Prefabs`
rüberkopieren) ziehen kannst:

| Prefab | Was drin ist | Wofür |
|---|---|---|
| `Assets/Prefabs/Player.ventity` | Kamera + `CoDMovement`, `Flashlight`, `Feet` (Schritte), `Interactor`, **`WCharakter`** (Third-Person-Körper: Animator, `LocomotionController`, Two-Bone-IK), **`FP_Arms`** (First-Person-Arme: Animator, `LocomotionController` FirstPerson=an, zwei Two-Bone-IK-Ketten, zwei `HandPose`-Komponenten, `fp_arms`-Material), **`Loadout`** (`WeaponLoadout` mit Vityaz + MP5) | Der komplette Spieler. In eine neue Szene ziehen, auf Augenhöhe (1.7 m über dem Boden) stellen — fertig. |
| `Assets/Prefabs/Character_Soldier.ventity` | Skinned Soldat (`soldier.glb`) + Animator (alle Locomotion-Clips) + `LocomotionController` (FollowPlayer=aus → steht als NPC) + Two-Bone-IK + `HandPose` links/rechts | Blaupause für jeden weiteren Charakter/NPC: Prefab duplizieren, Mesh tauschen, fertig. |

### Neuen Charakter in 5 Schritten (Mixamo-Rig)
1. Modell (`.glb`/`.fbx`, Mixamo-Skelett `mixamorig:`) nach `Assets/Models/<Name>/` importieren; die Clips liegen als
   `.vanim` daneben (der Importer extrahiert sie).
2. `Character_Soldier.ventity` im Asset-Browser duplizieren → `Character_<Name>.ventity`, Instanz in die Szene ziehen.
3. Bei den Kind-Entities `Meshes/*` im **MeshRenderer** den `Mesh Path` auf das neue Modell setzen (`…/<Name>.glb#submesh0`
   usw.; ein Kind pro Submesh). Skalierung des Rigs bleibt `0.01` (Mixamo = cm).
4. **Animator**: Clips zeigen auf `Assets/Models/<Name>/animations/…` — gleiche Clip-Namen behalten (`rifle_idle`,
   `walk`, `run`, `aim`, `fire`, `rifle_reload`, …), dann muss am `LocomotionController` nichts geändert werden.
5. Prüfen: **Two-Bone-IK** (Tip `mixamorig:LeftHand`, Target `mixamorig:RightHand`, Auto-Grip an) und die beiden
   **`HandPose`** (Side Left/Right) — beide arbeiten mit Knochennamen, bei einem Mixamo-Rig also unverändert. Bei einem
   fremden Rig: `BonePrefix`/`BoneFormat` bzw. Tip/Target anpassen.
   Soll der Charakter der SPIELER sein: im `Player`-Prefab die Kinder `WCharakter` und `FP_Arms` durch das neue Prefab
   ersetzen (FP_Arms bekommt `FirstPerson`=an, MeshRenderer-Layer 1, Material `fp_arms.vmat`).

### HandPose — Finger im Inspector statt Code
`Inspector ▸ Add Component ▸ Animation ▸ Hand Pose` auf dem Entity mit dem **Animator**. Felder:
* **Hand** (Left/Right) — wählt die Knochen (`mixamorig:LeftHandIndex1` …).
* **Index / Middle / Ring / Pinky / Thumb** — Beugung der drei Gelenke in Grad (x = Knöchel, y = Mittelgelenk, z = Endgelenk).
  Faust ≈ 85/95/50, Abzugsfinger ≈ 25/40/20, offen = 0/0/0. **Spread** spreizt die Finger, **Weight** blendet die
  ganze Pose (0 = nur Animation).
* **Bone prefix / Bone format / Curl axis / Curl sign** — nur für fremde Rigs (Mixamo: `mixamorig:`, Achse X, Vorzeichen −1).
Die Pose wirkt **additiv** auf jede Animation und ist sofort im Editor-Viewport sichtbar (Bind-Pose + Griff), im Spiel
über alle Clips hinweg. Mehrere HandPose-Komponenten pro Entity sind erlaubt (eine je Hand). Per Script setzt man
`Animation.SetBoneAdditiveRotation` — die Komponente ersetzt genau das für die Finger.

### Waffe im Griff: Sockets am Waffen-Prefab
Wo die Hände greifen, steht nicht im Code, sondern an der Waffe: Kind-Entities `Grip` (Waffenhand), `SupportGrip`
(Stützhand), `Sight`, `Muzzle`, `Eject`, `MagWell` — im Viewport verschieben/drehen (→ `WEAPONS_GUIDE.md`).

## Teil 1 — Die Szenen-Struktur (genau so shipped `Demo.vscene`)

```
Player                  (Camera + CoDMovement — die Kamera ist das Player-Entity selbst, top-level)
├── WCharakter          (Vollkörper-Prefab, Scale 0.01, Meshes Layer 2, LocomotionController FirstPerson=aus, TwoBoneIk)
├── FP_Arms             (DASSELBE Prefab nochmal, Meshes Layer 1, LocomotionController FirstPerson=AN, TwoBoneIk)
└── Loadout             (leeres Entity + WeaponLoadout-Script → spawnt jede Waffe 2x, siehe WEAPONS_GUIDE.md)
```

### WCharakter (was andere sehen)
1. **Charakter-Prefab** in die Szene (dein Mixamo/CC-Charakter, im Template `tp_character`).
   - Alle **MeshRenderer** → `renderLayer = 2` (Third-Person only). Nur ein Showcase/NPC, der
     woanders steht, bleibt Layer `0`.
   - Falls in cm authored (Mixamo): Root-Scale auf `0.01` (→ ~1,8 m).
2. **Animator**-Komponente drauf. Die Locomotion-Clips liegen unter `Assets/Models/Character/animations/`.
3. **`LocomotionController`**-Script drauf (`FirstPerson` = aus). Es liest `PlayerRig` (Speed /
   MoveForwardN/RightN / Ads / Firing / Reloading / IsAirborne / Health) und spielt die volle State-Machine:
   idle · walk · run · walk_back · run_back · strafe_l/r · aim · jump · jump_back · **death** + Start/Stop-Übergänge, fire/reload als maskierte Oberkörper-Overlays (`UpperMask` = `mixamorig:Spine1+`). **Überlagerungsfrei** (immer 1 Basis-Clip + max. 1 Overlay), mit Hysterese gegen W+A+D-Zucken.
   - Der Körper feuert/lädt automatisch synchron mit dir (das aktive Waffen-Script pulst `PlayerRig.Firing/Reloading`).
4. **`TwoBoneIk`**-Komponente drauf (→ Abschnitt „Beide Hände an der Waffe").

### FP_Arms (was DU siehst) — dasselbe Prefab, zweite Instanz, PROZEDURAL (v2.8)
1. **Dasselbe Charakter-Prefab NOCHMAL** in die Szene (Name `FP_Arms`), Scale wieder `0.01`.
   - Alle **MeshRenderer** → `renderLayer = 1` (First-Person/viewmodel).
2. **Animator** + **`LocomotionController`** + **`TwoBoneIk`** wie beim WCharakter — nur:
   **`FirstPerson` = AN**. Damit übernimmt der `ViewmodelRig`-Helfer (`Assets/Scripts/Player/ViewmodelRig.cs`)
   das Rig komplett — **CoD-Style, ohne eigene First-Person-Animationen**:
   - **Die Waffe bekommt jeden Frame eine Pose im KAMERA-Raum** (Hüfte / ADS / Sprint / Reload aus den
     Viewmodel-Feldern des Waffen-Prefabs, dazu Look-Sway, Geh-Bob, Atmen, Rückstoß-Feder).
   - **Schulter-Anker statt Animation**: die (versteckten) Schultern des Rigs werden jeden Frame auf einen festen
     Punkt im Kamera-Raum gepinnt — `ViewmodelRig.ShoulderAnchor` (Default `(0, -0.21, -0.03)` m: unter und
     leicht hinter dem Auge). Dadurch kommen die Oberarme immer von unten aus den Bildecken ins Bild und hängen
     nie „in der Kamera". Reicht ein Griff nicht in Armlänge (`ReachFraction`, Default 0.96), rutscht das ganze
     Rig zum Griff, statt einen Arm zu strecken.
   - **Beide Hände per IK auf der Waffe**: die RECHTE Hand bekommt Position **und** Rotation des Griffs
     (Waffe = Hand × Griff ⇒ Hand = Waffe × Griff⁻¹; die Waffe hängt per Bone-Attach an der Hand → Arme + Waffe
     bewegen sich als Einheit), die LINKE Hand wird auf den Vordergriff gezogen (`Animation.SetIkTarget`) und
     läuft beim Nachladen den Magazin-Pfad ab (greifen, ziehen, fallen lassen, einsetzen, draufschlagen).
     Dafür braucht `FP_Arms` **ZWEI `TwoBoneIk`-Komponenten**: Tip `mixamorig:LeftHand` (Target
     `mixamorig:RightHand`, AutoGrip an) und Tip `mixamorig:RightHand` (Target = derselbe Knochen, AutoGrip
     aus, `ApplyTipRotation` an).
   - **Ellbogen aus dem Bild drehen**: `ElbowPole` (Stützarm, Default 50°), `AdsElbowPole` (beim Zielen, 45°) und
     `ElbowPoleRight` (Waffenarm, 40°) drehen die Ellbogen um die Achse Schulter→Hand nach unten/außen.
   - **Torso, Schultern, Hals/Kopf und Beine werden ausgeblendet** (`Animation.SetBoneHidden`): `HideBones`
     (ganze Teilbäume: Neck, LeftUpLeg, RightUpLeg) + `HideBonesSelf` (nur der Knochen selbst, Kinder bleiben:
     Hips, Spine, Spine1, Spine2, LeftShoulder, RightShoulder) → es rendern nur die Arme + die Waffe.
   - **Arm-Optik**: dem Arm-Mesh (`FP_Arms/Meshes/Beta_Surface`) ist das Material `Assets/Materials/fp_arms.vmat`
     zugewiesen (dunkler Stoff) — importierte Modelle ignorieren die Entity-Farbe, ein `.vmat` am MeshRenderer
     ist der saubere Weg für Handschuhe/Ärmel. `FpRigScale` (Default `0.8`) skaliert das Rig (Armlänge vs.
     Waffenabstand); `HideMeshChildren` blendet Hilfs-Meshes des Rigs aus (`Beta_Joints`).
   - Es läuft nur EIN Haltungs-Clip (`FpHoldClip` = `aim`) für Finger-/Ellbogenpose; Feuern/Nachladen sind
     prozedural. Die Felder `FpEyeHeight/FpOff*/AdsShift*` aus v2.7 gibt es nicht mehr — die Posen stehen
     jetzt **am Waffen-Prefab** (→ `WEAPONS_GUIDE.md`, Abschnitt „Viewmodel-Felder“).

> **Live-Tuning beim Entwickeln** (nur Umgebungsvariablen, nichts wird gespeichert): `VM_HIP="x,y,z,pitch,yaw,roll"`,
> `VM_SPRINT=...`, `VM_RELOAD="sx,sy,sz,pitch,yaw,roll"`, `VM_SUPPORT="x,y,z"`, `VM_ADS=Distanz`, `VM_FOV=Grad`,
> `VM_ANCHOR="x,y,z"`, `VM_RIGPITCH=Grad`, `VM_REACH=0..1`, `VM_POLE=Grad`, `VM_POLE_ADS=Grad`, `VM_POLE_R=Grad`,
> `VM_SCALE=Faktor`, `VM_CLIP=aim`, `VM_DEBUG=1` (loggt Posen, Reichweiten und Gelenkpositionen im Kamera-Raum).
> Werte, die passen, dann in die Prefab-/Script-Felder übernehmen.
> Skriptbar testen: `Vortex.Player --project=<Pfad> --scene=Yard --input=script.txt --capture-dir=<Ordner>`
> (Zeilen wie `2.0 capture idle.bmp`, `2.5 hold LButton 0.4`, `3.5 tap R`, `7.0 press RButton`).

### Loadout (EIN Waffen-Objekt)
Leeres Kind-Entity `Loadout` + **`WeaponLoadout`**-Script. Es spawnt jedes Waffen-Prefab aus der
`Weapon Prefabs`-Liste **ZWEIMAL** und klebt beide Kopien per `Animation.Attach` an den
`mixamorig:RightHand`-Bone: die **FP-Instanz** (Layer 1) an die Hand von `FP_Arms` (ihr
Weapon-Script ist das aktive — Input + Ammo; ihre Viewmodel-Felder steuern den `ViewmodelRig`
über `PlayerRig.ActiveWeapon`), die **3P-Instanz** (Layer 2) an die Hand von
`WCharakter`. Der Sitz kommt aus `GripOffset`/`GripRotation` am Waffen-Prefab.
**Alles Weitere — Klassen, neue Waffe anlegen, Wechseln per Taste/Code, sichtbarer Mag-Zug —
steht in `WEAPONS_GUIDE.md`.**

> **WICHTIG (Migration von <v2.7):** die Waffe hängt NICHT mehr per `BoneAttachment`-Socket am
> Rig. Alte Sockets mit `SocketPrefabPath` am Hand-Bone **entfernen/leeren** — sonst hängen zwei
> Waffen in der Hand. Auch **keine manuell platzierte Waffe** als Kind liegen lassen (schwebt sonst
> als Leftover, oft auf Kopfhöhe).

---

## Beide Hände an der Waffe — Two-Bone IK (Engine #179)

Die Waffe klebt an der RECHTEN Hand — die LINKE würde stur den Locomotion-Clip spielen und nie
„greifen". Dafür sitzt auf **BEIDEN Rigs** eine **`TwoBoneIk`**-Komponente (im Template fertig
verdrahtet):

1. **Tip Bone** = `mixamorig:LeftHand` (die greifende Hand), **Target Bone** = `mixamorig:RightHand`
   (die Waffenhand). Ellbogen/Schulter (LeftForeArm/LeftArm) findet die Engine selbst.
2. **Auto-Grip = an** (Default): die IK **erfasst automatisch** den natürlichen Griff aus der
   Hold-Animation (wo die linke Hand relativ zur Waffenhand sitzt) und hält ihn durch JEDE Animation
   (idle/walk/run/aim) — weil das Target skelett-intern an der rechten Hand hängt, wandert es mit
   jedem Clip mit. Die Offset-Felder sind nur Feintuning obendrauf.
3. **Offset von Hand tunen** (nur wenn nötig): **Capture From Current Pose** (Charakter in eine
   Rifle-Pose bringen → Button → Offset gebacken) ODER Zahlen tippen — der **Editor-Viewport zeigt
   die IK-Pose live**. Einheiten = **Modell-Einheiten** (Mixamo = cm!).
4. **Weight** = 1. **Pole Angle** nur anfassen, wenn der Ellbogen komisch knickt (dreht die
   Beuge-Ebene um die Schulter→Ziel-Achse; 0 = natürliche Beuge der Animation).
5. **Nachladen**: der `LocomotionController` gibt die IK **automatisch** frei —
   `SetIkWeight("mixamorig:LeftHand", 0)` beim Reload-Start, zurück auf `1` am Ende
   (`ReloadTime`, Default 2.4 s). Die animierte Hand greift derweil das **sichtbare Magazin**
   (Mag-Follow in `Firearm`, → `WEAPONS_GUIDE.md`). In eigenen Scripts geht dasselbe per
   `Animation.SetIkWeight(chr, "mixamorig:LeftHand", 0f);`.

Weil FP_Arms und WCharakter identisch verdrahtet sind, greift die Stützhand in BEIDEN Ansichten —
mit der **P-Debug-Cam + CAPS** von vorne prüfen.

**First-Person-Zusatz (FP_Arms)**: dort gibt es eine **zweite** `TwoBoneIk`-Komponente mit Tip **UND** Target
`mixamorig:RightHand` (AutoGrip aus, ApplyTipRotation an). Der `ViewmodelRig` setzt jeden Frame Welt-Ziele für
beide Ketten: die Waffenhand bekommt den **Auto-Grip** (Griff-Sitz aus der Handgeometrie + `GripHandPos`/
`GripFingerDir`/`GripPalmDir` des Waffen-Prefabs, gilt danach auch für die 3P-Kopie), die Stützhand wird mit
`SupportHandOffset`/`SupportFingerDir`/`SupportPalmDir` (oder die Sockets `Grip`/`SupportGrip` am Waffen-Prefab)
unter den Handschutz gedreht; die Finger beider Hände schließen sich über **`HandPose`-Komponenten** auf dem Rig
(pro Hand eine, Beugung pro Fingergelenk im Inspector, → `WEAPONS_GUIDE.md`). Von außen prüfen:
`VM_DBGCAM="x,y,z,yaw,pitch" VM_DBGCAM_FP=1` rendert die FP-Arme + Waffe aus einer freien Kamera.

---

## BoneAttachment / Socket-Editor — für ALLES ANDERE am Knochen

Der generische Engine-Socket bleibt das Werkzeug für **Nicht-Waffen-Attachments**: Laterne an der
Hüfte, Hut auf dem Kopf, Rucksack, oder die Waffe eines **Showcase-NPCs**, der kein `WeaponLoadout`
fährt:

1. Ein **leeres Kind-Entity** unter dem Charakter anlegen (z.B. `hat_socket`).
2. **Add Component → Bone Attachment**: **Pick Prefab…** → dein `.ventity`, **Bone** wählen,
   **Open Socket Editor…** → Live-3D. Klick in die Ansicht, dann `←→` X · `PgUp/PgDn` Y · `↑↓` Z
   bewegen, `I/K J/L U/O` drehen (oder exakte Werte tippen) → **Save** → Prefab speichern.
3. **Render Layer des Sockets** (Dropdown in der Komponente): erzwingt den Layer auf ALLE Meshes
   des gespawnten Prefabs. **Muss zum Körper passen** — Attachment an einem Layer-2-Charakter →
   auch **Third-Person only**, sonst schwebt es sichtbar in der Luft. „Keep prefab" = wie authored.
4. **Play** → die Engine (`ExpandSocketPrefabs`) instanziiert jedes `SocketPrefabPath`-Prefab und
   attached es an seinen Bone. Läuft nach der Animation.

> **Attachment folgt nicht dem Knochen / hängt am Kopf?** Der Socket findet den Charakter über den
> **Vorfahren mit Animator**. Das Socket-Entity MUSS also **unter dem animierten Charakter** hängen
> (nicht daneben unter Player!) — ODER du setzt in der Bone-Attachment-Komponente ein **Target**
> (die Charakter-Entity). Sonst wird das Prefab gespawnt, aber nie an den Knochen getrieben (die
> Engine warnt in der Console).

---

## Die Scripts/APIs (alles engine-seitig fertig)

- **`PlayerRig`** (statisch, in `CoDMovement.cs`): die Brücke. `CoDMovement` schreibt Kamera/Bewegung, das aktive Waffen-Script schreibt `Firing/Reloading/Ammo`. Beide `LocomotionController` + `WeaponLoadout` lesen es. → FP + 3P bleiben synchron ohne Kopplung.
- **`LocomotionController`**: DER Rig-Treiber (beide Instanzen, `FirstPerson` unterscheidet).
- **`WeaponLoadout` / `Weapon` / `Firearm`**: das Waffen-System im Projekt (→ `WEAPONS_GUIDE.md`).
- **`TwoBoneIk`** (Engine-Komponente): Stützhand-IK mit Auto-Grip; `Animation.SetIkWeight` zum Freigeben.
- **`BoneAttachment`** (Engine-Komponente): der generische Socket. Felder: Bone, Offset (Pos/Rot/Scale, Bone-lokal), `SocketPrefabPath`, Socket-Render-Layer. Editor-Buttons: Pick Prefab, Open Socket Editor, Snap/Capture.
- **`Animation.SetBoneScaleOverride(id, bone, scale)`**: Knochen kollabieren (0) — versteckt das Limb auf der GPU-Palette, IK/Sockets laufen weiter (so versteckt FP_Arms Kopf/Beine).
- **`Animation.Attach/Detach`**: Entity zur Laufzeit an einen Bone heften/lösen (so hängen die Waffen an den Händen).
- **`Scene.SetRenderLayer(entity, layer)`**: Render-Layer (0/1/2) rekursiv per Script setzen.
- **Socket Editor**: Rechtsklick auf ein Modell/Prefab → „Socket Editor…", ODER der Button in der Bone-Attachment-Komponente. Vorschau + dieselben Nudge-Controls; **Editor-Platzierung == Spiel-Platzierung** (identische Engine-Mathematik).

## Wichtige Regeln (damit nichts crasht/buggt)
- FP-Rig-Meshes IMMER `renderLayer = 1`, eigener World-Body `renderLayer = 2` (Third-Person only), Welt/NPCs `renderLayer = 0`.
- Die Haupt-Kamera (`Player`) muss **top-level** bleiben — die Rigs sind KINDER von Player, nie umgekehrt.
- Layer-1-Meshes sind im Build-Viewport unsichtbar — das ist Absicht. Platzieren mit dem **FP**-Toolbar-Toggle, live testen mit **View: FP Preview (In-Game)**.
- Layer 2 wirft (noch) keinen Schatten, wenn er versteckt ist: der Schatten-Pass liest dieselbe Submit-Queue, und Skinned Meshes casten in v1 ohnehin nicht.
- Alte `BoneAttachment`-Waffen-Sockets am Hand-Bone entfernen — das `WeaponLoadout` spawnt die Waffen selbst (sonst Doppel-Waffe).
- Datei-Dialoge im Editor: die Engine nutzt intern den STA-`FilePicker` — kein roher `OpenFileDialog` (crasht sonst die Engine).
- Skripte sind **C#5** (kein `$"..."`, kein `?.`, kein inline `out var`); `float.TryParse` mit `CultureInfo.InvariantCulture`.
- Ein Compile-Fehler in IRGENDEINEM Script killt alle → Fehler landen in der **Game Console** (Play → Console-Panel), vorm Testen prüfen.
