# Vortex — Waffen-System (Klassen + Vererbung + EIN Loadout-Objekt)

Strikte Trennung: die **Engine/der Editor** kennen keine "Waffe" — sie liefern nur generische
Mechanismen (Script-Komponente, Feld-Serialisierung inkl. **Listen**, Prefabs, `Scene.Instantiate`,
`Scene.SetRenderLayer`, `Animation.Attach`). Das komplette Waffen-System lebt im **Game-Projekt**
unter `Assets/Scripts/Weapons/`.

## Die Klassen-Hierarchie (C#, Vererbung)

```
Weapon (abstrakt)             Stats: WeaponName, Damage, FireRate, Range, Automatic, FireSound
│                             + Hand-Grip (GripOffset/GripRotation — Sitz auf dem Hand-Bone)
│                             + VIEWMODEL (v2.8): Hip/Sprint/Reload-Posen im Kamera-Raum, AdsDistance, ViewmodelFov,
│                               Punkte auf der Waffe (SightOffset, SupportHandOffset, MagWellOffset, MuzzleOffset,
│                               EjectOffset) + Schuss-Feedback (KickBack/KickUp/KickSide, CamKick*, Flash*, ShellPrefab)
│                             virtuelle Methoden: Fire(), CanFire(), Tick(dt), OnEquip(), OnHolster()
├── Firearm (abstrakt)        + MagazineSize, ReserveAmmo, ReloadTime, Dry-/MagOut-/Reload-/ActionSound; Ammo-Logik, R = Reload
│   │                         + SICHTBARER Mag-Wechsel: FP = prozeduraler Magazin-Pfad im ViewmodelRig (Hand + Mag),
│   │                           3P = das "Mag"-Kind hängt mid-reload an der LINKEN Hand des Körpers (MagOutAt, MagInAt)
│   │                         + PlayerRig.ReloadProgress (0..1) + Sound-Cues bei 15 % / 72 % / 86 %
│   ├── Pistol                semi-auto Defaults (im Konstruktor)
│   └── AssaultRifle          full-auto Defaults
└── Knife                     melee = kurzer Hitscan, kein Magazin
```

**Regeln (Engine-Fakten):**
- **Eine Datei pro konkreter Klasse**, Dateiname == Klassenname (`Pistol.cs` → `class Pistol`).
  Basisklassen (`Weapon.cs`, `Firearm.cs`) liegen einfach daneben — NIE einer Entity zuweisen
  (abstrakt = läuft nicht; der Editor loggt das als Fehler in die Console).
- **Geerbte public-Felder erscheinen im Inspector** und werden pro Prefab serialisiert — `Damage`
  aus `Weapon` ist an einem `Pistol`-Prefab ganz normal editierbar.
- Scripts sind **C#5** (kein `$"..."`, kein `?.`, kein inline `out var`).

## Wie die Waffe am Charakter hängt (2 Rigs, 1 Prefab)

`WeaponLoadout` spawnt jedes Waffen-Prefab **ZWEIMAL** und klebt beide Kopien per
`Animation.Attach` an den `mixamorig:RightHand`-Bone:

- **FP-Instanz** (RenderLayer 1) → an der Hand des **FP_Arms**-Rigs. Sie reitet dadurch auf JEDER
  Arm-Animation mit (Gehen, Feuern, Nachladen). Ihr Weapon-Script ist das AKTIVE (Input + Ammo).
- **3P-Instanz** (RenderLayer 2) → an der Hand des **WCharakter**-Weltkörpers. Für dich unsichtbar,
  für jede andere Kamera / die P-Debug-Cam / (später) andere Spieler am Körper sichtbar.

Der Sitz auf der Hand kommt aus `GripOffset` (Meter, normalisierter Bone-Frame) + `GripRotation`
(Euler ZXY) — die Defaults passen für den Vityaz am tp_character-Rig; pro Waffen-Prefab im
Inspector nachtunen (oder visuell über den Socket Editor ermitteln).

Die **Stützhand** greift automatisch an den Vordergriff: die `TwoBoneIk`-Komponente auf beiden
Rigs (AutoGrip an) hält die linke Hand relativ zur Waffenhand — durch jede Animation. Beim
Nachladen gibt `LocomotionController` die IK frei (`SetIkWeight(..., 0)`) und die animierte Hand
zieht das sichtbare Magazin (Mag-Follow in `Firearm`).

WICHTIG: alte `BoneAttachment`-Sockets mit `SocketPrefabPath` am selben Hand-Bone entfernen/leeren
— sonst hängen zwei Waffen in der Hand.

## Viewmodel-Felder (CoD-Style First-Person, v2.8)

Die First-Person-Darstellung ist **prozedural** (`Assets/Scripts/Player/ViewmodelRig.cs`, läuft auf dem
`FP_Arms`-Rig): die Waffe bekommt jeden Frame eine Pose im **Kamera-Raum** (X rechts, Y hoch, Z vorwärts,
Meter/Grad), die Schultern des Arm-Rigs werden unter dem Auge verankert und **beide Hände gehen per IK an die
Waffe** (rechte Hand: Griff inkl. Rotation, linke Hand: Vordergriff). Alle Posen stehen **am Waffen-Prefab**
(geerbte `Weapon`-Felder, im Inspector editierbar):

| Feld | Bedeutung | Vityaz-Default |
|---|---|---|
| `HipPosition` / `HipRotation` | Waffen-Ursprung relativ zum Auge im Hüftanschlag | `(0.11, -0.17, 0.31)` / `(0, -5, 3)` |
| `SprintPosition` / `SprintRotation` | Pose beim Sprinten (Waffe gesenkt, gekippt) | `(0.09, -0.19, 0.30)` / `(14, -30, 10)` |
| `ReloadShift` / `ReloadTilt` | Verschiebung + Kippung (Rolle zur Kamera) während des Nachladens | `(-0.02, -0.02, 0)` / `(10, -14, 28)` |
| `AdsDistance`, `AdsSpeed` | Abstand Auge→Visier beim Zielen (Visierpunkt landet exakt in der Bildmitte), Blendtempo | `0.30`, `12` |
| `ViewmodelFov` | FOV des First-Person-Passes (Welt-FOV bleibt die Kamera) | `68` |
| `SightOffset` | Zielpunkt des Visiers, **waffenlokal** | `(0.002, 0.128, 0.02)` |
| `SupportHandOffset` | Handgelenk der Stützhand unter dem Handschutz, waffenlokal | `(-0.015, -0.035, 0.17)` |
| `SupportFingerDir` / `SupportPalmDir` | Wohin die Finger der Stützhand zeigen (quer über den Handschutz, nach oben einrollend) und wohin die Handfläche zeigt (gegen den Handschutz) — waffenlokal | `(1, 0.4, 0)` / `(-0.3, 1, 0)` |
| `AutoGrip` | **an**: `GripOffset`/`GripRotation` werden zur Laufzeit aus der echten Handgeometrie des Rigs + den drei Feldern darunter berechnet (gilt für FP- UND 3P-Kopie); aus: die authored Werte gelten | `true` |
| `GripHandPos` | Handgelenk der Waffenhand relativ zum Griffpunkt (Prefab-Ursprung), waffenlokal | `(0.02, -0.055, -0.04)` |
| `GripFingerDir` / `GripPalmDir` | Richtung Handgelenk→Fingerknöchel (nach vorn, um den Griff nach oben) und Handflächen-Normale (gegen den Griff = nach links) | `(0.05, 0.2, 1)` / `(-1, 0, 0.15)` |
| `MagWellOffset` | Magazinschacht (Start des Reload-Pfads), waffenlokal | `(0, -0.005, 0.10)` |
| `MuzzleOffset` / `EjectOffset` | Mündung (Flash) und Auswurffenster (Hülsen), waffenlokal | `(0, 0.055, 0.31)` / `(0.025, 0.07, 0.04)` |
| `KickBack` / `KickUp` / `KickSide` | Rückstoß-Impulse auf die Waffe (Feder im ViewmodelRig) | `0.55 m/s`, `42 °/s`, `14 °/s` |
| `CamKickPitch` / `CamKickYaw` | Kamera-Rückstoß pro Schuss | `0.8°`, `0.25°` |
| `FlashIntensity`, `FlashTime`, `FlashChildName`, `FlashMeshChildName` | Mündungsfeuer: Punktlicht-Kind `MuzzleFlash` + Emissive-Mesh-Kind `Flash` im Prefab | `30`, `0.045 s` |
| `ShellPrefab` | Hülsen-Prefab (bekommt per `SendMessage("eject", velocity)` seinen Auswurf) | `Assets/Prefabs/Shell.ventity` |
| `BulletImpulse` | Impuls (N·s) auf getroffene Physik-Props (Rigidbody) am Trefferpunkt — 0 = aus | `6` |

**Punkte von einem neuen Modell ablesen:** die glb-Bounds der Einzelteile (Handschutz, Visier, Magazin,
Lauf) liegen im Modell-Raum der Waffe — Vorwärts = +Z, oben = +Y. Beim Vityaz: Handschutz z 0.12–0.24,
Holo-Visier y 0.107–0.147, Magazin z 0.06–0.14, Laufende z 0.28. Erst `SightOffset` setzen (ADS muss die
Bildmitte treffen), dann `SupportHandOffset`, dann die Hüft-Pose per `VM_HIP` live tunen
(→ `CHARACTER_SETUP_GUIDE.md`, „Live-Tuning“). Den Griff selbst prüft man am besten von außen: der Player mit
`VM_DBGCAM="x,y,z,yaw,pitch" VM_DBGCAM_FP=1` rendert die First-Person-Arme + Waffe aus einer freien Kamera
(Nahaufnahme der Hände), `VM_GRIPPOS/VM_GRIPF/VM_GRIPPALM/VM_SUPF/VM_SUPPALM="x,y,z"` überschreiben die
Griff-Felder live.

**Sockets statt Zahlen (empfohlen):** leere Kind-Entities am Waffen-Prefab mit den Namen `Grip`, `SupportGrip`,
`Sight`, `Muzzle`, `Eject` und `MagWell` überschreiben die Felder oben — im Viewport platzieren/drehen wie jede
Entity. Bei den beiden Hand-Sockets zeigt die **+Z-Achse** des Sockets dorthin, wo die Finger zeigen (Handgelenk →
Knöchel), die **+Y-Achse** ist die Handflächen-Normale (zeigt IN die Waffe), die Position ist das Handgelenk. Die
mitgelieferten Prefabs (Vityaz, MP5) haben alle sechs Sockets.

**Finger — `HandPose`-Komponente (Editor, kein Code):** auf dem Rig-Entity (`FP_Arms` bzw. `WCharakter`) liegt pro
Hand eine **`HandPose`**-Komponente: `Side` (Left/Right), pro Finger die Beugung der drei Gelenke in Grad
(`Index`/`Middle`/`Ring`/`Pinky`/`Thumb` als x/y/z = Gelenk 1/2/3), `Spread` (Spreizen), `Weight`, dazu
`BonePrefix`/`BoneFormat` und `CurlAxis`/`CurlSign` für fremde Rigs (Mixamo: Achse X, Vorzeichen −1). Die
Werte wirken additiv auf jede Animation und sind **live im Editor-Viewport** sichtbar (Bind-Pose + Griff). Die
Waffenhand hat einen weniger gebeugten Zeigefinger (am Abzug), die Stützhand schließt sich um den Handschutz.

**Schuss-Feedback** (`Weapon.FireFeedback`, wird von `Fire()` aufgerufen): Viewmodel-Feder + Kamera-Kick,
Mündungsfeuer (Licht-Puls + kurzes Emissive-Mesh, beides Kinder des Prefabs — fehlen sie, passiert einfach
nichts), Hülse aus dem Auswurffenster (`Shell.ventity`, fliegt/springt/rollt per `ShellCasing`).

**Reload-Choreografie** (`ViewmodelRig.ReloadPath`, Fortschritt 0..1 über `ReloadTime`): 0–15 % Hand zum
Magazin · 15–32 % Magazin raus · 32–50 % nach unten aus dem Bild · 50–70 % frisches Magazin hoch · 70–80 %
einsetzen · 80–90 % draufschlagen (kleiner Ruck auf die Waffe) · 90–100 % zurück an den Handschutz. Sounds
kommen aus `Firearm` (MagOutSound 15 %, ReloadSound 72 %, ActionSound 86 %). Der 3P-Körper spielt derweil
den `rifle_reload`-Clip und zieht das Magazin per Bone-Attach.

## Eine neue Waffe anlegen (z.B. "Deagle")

1. OPTIONAL neue Klasse: `Assets/Scripts/Weapons/Deagle.cs` mit `class Deagle : Firearm` und
   Konstruktor-Defaults — ODER einfach die `Pistol`-Klasse wiederverwenden.
2. **Prefab bauen**: Entity mit dem Waffen-MESH + **Script-Komponente** (`Pistol.cs`/`Deagle.cs`)
   + optional ein Kind **`Mag`** mit dem Magazin-Mesh (für den sichtbaren Reload)
   + optional die Kinder **`MuzzleFlash`** (Punktlicht, Intensität 0, an der Mündung) und **`Flash`**
     (kleine Emissive-Kugel an der Mündung, inaktiv) für das Mündungsfeuer
   → als `.ventity` speichern (z.B. `Assets/Prefabs/Weapon_Deagle.ventity`).
   > `Weapon_MP5.ventity` ist noch das alte starre Viewmodel-Modell **mit eingebauten Armen** (`vm_mp5.glb`) —
   > für das Bone-Attach-System ein reines Waffen-Mesh nehmen (wie `vm_vityaz_body.glb`).
3. Im **Prefab-Inspector** die Stats tunen: Damage 60, MagazineSize 7, FireSound, `GripOffset`/
   `GripRotation` falls das Mesh anders sitzt. Fertig — die Werte gehören zum Prefab.

## Der Spieler bekommt EIN Waffen-Objekt

1. Unter `Player` ein leeres Kind-Entity **`Loadout`** anlegen.
2. **Add Component → Script → `WeaponLoadout.cs`**.
3. Im Inspector bei **Weapon Prefabs**: `+ Add` pro Slot, dann **`…`** und das `.ventity` picken
   (Slot-Reihenfolge = Tasten 1, 2, 3 …).
4. `Fp Entity` = "FP_Arms", `Body Entity` = "WCharakter", `Hand Bone` = `mixamorig:RightHand`
   (Template-Defaults — nur ändern, wenn deine Rigs anders heißen).

## Wechseln & Steuern aus C# (voll flexibel)

```csharp
WeaponLoadout lo = Scene.GetBehaviour<WeaponLoadout>(Scene.Find("Loadout"));
lo.Equip("Vityaz");            // per Name (WeaponName ODER Klassenname)
lo.Equip(2);                   // per Slot
lo.EquipNext();                // durchschalten
Weapon w = lo.ActiveWeapon;    // POLYMORPH: Basisklassen-Referenz
w.Fire();                      // ruft die Klasse des aktiven Slots (Pistol/Rifle/Knife)
if (w is Firearm) { int ammo = ((Firearm)w).MagCount; }   // HUD
```

Eingebaut: Tasten **1..9** + **Mausrad** wechseln; **LMB** feuert (auto/semi je Klasse),
**R** lädt nach. `PlayerRig.Firing/Reloading` werden gepulst → BEIDE Rigs spielen ihre
Fire-/Reload-Layer synchron; der HUD liest `PlayerRig.Ammo`/`PlayerRig.MagSize`.

## Editor-Mechanismen dahinter (generisch)

- **Listen-Felder**: `public string[]`/`int[]`/`float[]`… erscheinen als Liste (+ Add / ✕) im
  Inspector und serialisieren in Szene UND Prefab.
- **`…`-Browse** an jedem String-Feld: Asset picken → projekt-relativer Pfad.
- **`Scene.SetRenderLayer(entity, layer)`**: Render-Layer (0/1/2) rekursiv per Script setzen.
- **`Animation.Attach/Detach`**: Entity zur Laufzeit an einen Bone heften/lösen.
- **`Animation.SetIkTarget(entity, tipBone, worldPos[, worldRotEuler])` / `ClearIkTarget`**: eine `TwoBoneIk`-Kette
  auf ein WELT-Ziel ziehen (Stützhand am Magazin, Hand an der Türklinke) — `SetIkPoleAngle` dreht den Ellbogen.
- **`Animation.SetBoneHidden(entity, bone, hidden, includeDescendants)`**: Knochen ausblenden — mit
  `includeDescendants=false` nur den Knochen selbst (Torso weg, Arme bleiben).
- **`Scene.TryGetWorldPose` / `SetWorldPose`, `Scene.ScaleOf` / `SetScaleOf`, `Quaternion`** (FromEuler/ToEuler/
  Rotate/`*`/Slerp/LookRotation, exakt die Engine-Konvention): die Mathe für kameragebundene Posen ohne Matrix-Code.
- Console-Fehler, wenn eine Script-Komponente auf eine abstrakte/umbenannte Klasse zeigt.
