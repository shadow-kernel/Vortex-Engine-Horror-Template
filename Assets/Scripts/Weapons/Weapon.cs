using Vortex;

// WEAPON BASE CLASS — ABSTRACT: never assign THIS file to a Script component (assign a concrete
// class file like Pistol.cs / AssaultRifle.cs / Knife.cs; the engine logs an error if you try).
//
// Inheritance: Weapon -> Firearm -> Pistol / AssaultRifle,  Weapon -> Knife.
// Public fields (INCLUDING inherited ones) show up in the Inspector on the weapon PREFAB and are
// serialized per prefab (#47): the CLASS defines the weapon type + behaviour, the PREFAB defines
// the concrete values ("Vityaz" = mesh + AssaultRifle script + tuned Damage/FireRate/grip fields).
//
// The WeaponLoadout spawns every weapon prefab TWICE and BONE-ATTACHES each copy to a hand:
//   - FP instance (RenderLayer 1): glued to the FP arms rig's hand -> it moves with EVERY arm
//     animation (walk sway, fire, reload) automatically. This script is ACTIVE here: it reads the
//     fire/reload input and publishes PlayerRig.Firing/Reloading for BOTH rigs' animations.
//   - 3P instance (RenderLayer 2): glued to the world body's hand — hidden for you, visible for
//     every other camera (debug cam, other players). Visual only (IsFpInstance stays false).
public abstract class Weapon : VortexBehaviour
{
    // ---- identity + combat stats (tune per weapon prefab in the Inspector) ----
    public string WeaponName = "Weapon";
    public float Damage = 20f;
    public float FireRate = 600f;      // rounds per minute
    public float Range = 120f;
    public bool Automatic = true;
    public string FireSound = "Assets/Audio/gun_crack.wav";

    // ---- grip: how the weapon sits on the hand bone (offset in metres, rotation in engine ZXY
    // degrees, in the NORMALIZED hand-bone frame). Computed for the tp_character rifle pose; tune
    // per weapon prefab (or visually via the Socket Editor) if a model's origin differs. ----
    public Vector3 GripOffset = new Vector3(0f, -0.02f, 0f);
    public Vector3 GripRotation = new Vector3(81.2f, -156.6f, 126.9f);

    // ---- runtime wiring (written by WeaponLoadout — leave alone in the Inspector) ----
    public bool IsFpInstance = false;
    public bool Equipped = false;

    protected float Cooldown;
    private bool _fireHeld;

    public override void Update(float dt)
    {
        if (!IsFpInstance || !Equipped) return;
        if (!PlayerRig.Ready || PlayerRig.Inspect) return;

        if (Cooldown > 0f) Cooldown -= dt;
        if (!Cursor.Locked) { PlayerRig.Firing = false; return; }   // paused / menu

        bool held = Input.GetKey("LButton");
        bool wants = Automatic ? held : (held && !_fireHeld);
        _fireHeld = held;

        bool shot = false;
        if (wants && Cooldown <= 0f && CanFire())
        {
            Cooldown = 60f / (FireRate < 1f ? 1f : FireRate);
            Fire();
            shot = true;
        }
        PlayerRig.Firing = shot;   // per-shot pulse -> both rigs' masked fire animation layer

        Tick(dt);
    }

    /// <summary>May the weapon fire right now? Firearms add ammo/reload gating.</summary>
    protected virtual bool CanFire() { return true; }

    /// <summary>Per-frame hook after the input handling (reload timers etc.).</summary>
    protected virtual void Tick(float dt) { }

    /// <summary>THE polymorphic action — default: fire sound + hitscan + camera kick + "damage"
    /// message. Override for melee arcs, projectiles, shotguns…</summary>
    public virtual void Fire()
    {
        if (FireSound != "") Audio.PlayOneShot2D(FireSound, 0.9f, 1f);
        float yawRad = PlayerRig.Yaw * 0.0174532925f;
        float pitchRad = PlayerRig.Pitch * 0.0174532925f;
        float cy = (float)System.Math.Cos(yawRad), sy = (float)System.Math.Sin(yawRad);
        float cp = (float)System.Math.Cos(pitchRad), sp = (float)System.Math.Sin(pitchRad);
        Vector3 dir = new Vector3(sy * cp, -sp, cy * cp);
        RaycastHit hit;
        if (Physics.Raycast(PlayerRig.EyePos, dir, Range, out hit))
            SendMessage(hit.EntityId, "damage", Damage);
        PlayerRig.CamKickPitch -= 0.8f;                                 // view kick per shot
        PlayerRig.CamKickYaw += (Cooldown * 1000f % 2f < 1f) ? 0.25f : -0.25f;
    }

    /// <summary>Called by the loadout on switch — override for draw/holster anims and sounds.</summary>
    public virtual void OnEquip() { }
    public virtual void OnHolster() { }
}
