# Vortex — Waffen-System (Klassen + Vererbung + EIN Loadout-Objekt)

Strikte Trennung: die **Engine/der Editor** kennen keine "Waffe" — sie liefern nur generische
Mechanismen (Script-Komponente, Feld-Serialisierung inkl. **Listen**, Prefabs, `Scene.Instantiate`,
`Scene.SetRenderLayer`, `Animation.Attach`). Das komplette Waffen-System lebt im **Game-Projekt**
unter `Assets/Scripts/Weapons/`.

## Die Klassen-Hierarchie (C#, Vererbung)

```
Weapon (abstrakt)             Stats: WeaponName, Damage, FireRate, Range, Automatic, FireSound
│                             + Hand-Grip (GripOffset/GripRotation — Sitz auf dem Hand-Bone)
│                             virtuelle Methoden: Fire(), CanFire(), Tick(dt), OnEquip(), OnHolster()
├── Firearm (abstrakt)        + MagazineSize, ReserveAmmo, ReloadTime, Dry-/ReloadSound; Ammo-Logik, R = Reload
│   │                         + SICHTBARER Mag-Wechsel: das "Mag"-Kind des Prefabs hängt mid-reload an der
│   │                           LINKEN Hand des Rigs (MagChildName, MagOutAt, MagInAt)
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

## Eine neue Waffe anlegen (z.B. "Deagle")

1. OPTIONAL neue Klasse: `Assets/Scripts/Weapons/Deagle.cs` mit `class Deagle : Firearm` und
   Konstruktor-Defaults — ODER einfach die `Pistol`-Klasse wiederverwenden.
2. **Prefab bauen**: Entity mit dem Waffen-MESH + **Script-Komponente** (`Pistol.cs`/`Deagle.cs`)
   + optional ein Kind **`Mag`** mit dem Magazin-Mesh (für den sichtbaren Reload)
   → als `.ventity` speichern (z.B. `Assets/Prefabs/Weapon_Deagle.ventity`).
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
- Console-Fehler, wenn eine Script-Komponente auf eine abstrakte/umbenannte Klasse zeigt.
