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
//   - FP instance (RenderLayer 1): glued to the FP arms rig's hand. The arms rig itself is placed every frame
//     by the procedural ViewmodelRig so that hand + weapon land on the CoD-style camera-space pose defined by
//     the viewmodel fields below (hip / ADS / sprint / reload). This script is ACTIVE here: it reads the
//     fire/reload input, publishes PlayerRig.Firing/Reloading for the 3P body's animations and fires the
//     shot feedback (viewmodel kick, camera kick, muzzle flash, shell casing).
//   - 3P instance (RenderLayer 2): glued to the world body's hand — hidden for you, visible for every other
//     camera (debug cam, other players). Visual only (IsFpInstance stays false).
public abstract class Weapon : VortexBehaviour
{
    // ---- identity + combat stats (tune per weapon prefab in the Inspector) ----
    public string WeaponName = "Weapon";
    public float Damage = 20f;
    public float FireRate = 600f;      // rounds per minute
    public float Range = 120f;
    public float BulletImpulse = 6f;   // N·s handed to a hit physics prop (barrels, crates) — 0 = none
    public bool Automatic = true;
    public string FireSound = "Assets/Audio/gun_rifle.vsndc";   // recorded shots, random container (pitch/volume variation)

    // ---- grip: how the weapon sits on the hand bone (offset in metres, rotation in engine ZXY
    // degrees, in the NORMALIZED hand-bone frame). With AutoGrip (default) BOTH are COMPUTED at runtime by the
    // ViewmodelRig from the rig's real hand geometry and the intuitive weapon-space description below; the
    // authored values are only used when AutoGrip is off (or can be captured in the Socket Editor). ----
    public Vector3 GripOffset = new Vector3(0f, -0.02f, 0f);
    public Vector3 GripRotation = new Vector3(81.2f, -156.6f, 126.9f);
    // EDITOR SOCKETS (preferred): empty child entities of the weapon prefab named "Grip", "SupportGrip", "Sight",
    // "Muzzle", "Eject" and "MagWell" override the numeric fields below — place and rotate them in the viewport like
    // any entity. For the two hand sockets the socket's +Z axis is where the fingers point (wrist -> knuckles) and
    // its +Y axis is the palm normal (points INTO the weapon), the socket position is the wrist.
    // WEAPON space (X right, Y up, Z forward = muzzle; origin = the prefab's grip point):
    public bool    AutoGrip       = true;
    public Vector3 GripHandPos    = new Vector3(0.02f, -0.055f, -0.04f);   // wrist (hand bone) relative to the grip point
    public Vector3 GripFingerDir  = new Vector3(0.05f, 0.2f, 1f);          // wrist -> knuckles: forward, wrapping up around the grip
    public Vector3 GripPalmDir    = new Vector3(-1f, 0f, 0.15f);           // palm faces the grip (left, a touch forward)
    public Vector3 SupportFingerDir = new Vector3(1f, 0.15f, 0f);          // support hand: fingers across the handguard, curling up
    public Vector3 SupportPalmDir   = new Vector3(0f, 1f, 0f);             // palm up against the handguard

    // ---- first-person viewmodel (CAMERA space: X right, Y up, Z forward, metres / degrees). The weapon's
    // origin (its mesh origin, near the receiver) is placed here relative to the eye; the arms follow. ----
    public Vector3 HipPosition    = new Vector3(0.11f, -0.17f, 0.31f);
    public Vector3 HipRotation    = new Vector3(0f, -5f, 3f);
    public Vector3 SprintPosition = new Vector3(0.09f, -0.19f, 0.30f);
    public Vector3 SprintRotation = new Vector3(14f, -30f, 10f);
    public Vector3 ReloadShift    = new Vector3(-0.02f, -0.02f, 0f);       // added to the hip pose while reloading
    public Vector3 ReloadTilt     = new Vector3(10f, -14f, 28f);           // roll the mag well toward the camera
    public float   AdsDistance    = 0.30f;    // eye -> sight distance while aiming (the sight lands on the screen centre)
    public float   AdsSpeed       = 12f;      // ADS blend speed (1/s)
    public float   ViewmodelFov   = 68f;      // first-person layer FOV (the world FOV is the camera's)

    // ---- points on the weapon (WEAPON-local metres): sight aim point, support-hand grip, mag well, muzzle,
    // ejection port. Defaults fit vm_vityaz_body.glb; read them off your model's bounds for a new gun. ----
    public Vector3 SightOffset       = new Vector3(0.002f, 0.128f, 0.02f);
    public Vector3 SupportHandOffset = new Vector3(-0.065f, -0.045f, 0.17f);   // wrist of the support hand: left of and below the handguard (the fingers wrap its right side)
    public Vector3 MagWellOffset     = new Vector3(0f, -0.005f, 0.10f);
    public Vector3 MuzzleOffset      = new Vector3(0f, 0.055f, 0.31f);
    public Vector3 EjectOffset       = new Vector3(0.025f, 0.07f, 0.04f);
    public string  MagChildName      = "Mag";          // weapon-prefab child carrying the magazine mesh ("" = none)

    // ---- accuracy: hip-fire cone (deg) that widens with movement and sustained fire, collapses while aiming ----
    public float HipSpread   = 2.4f;
    public float AdsSpread   = 0.25f;
    public float MoveSpread  = 1.8f;   // added at full move speed
    public float ShotSpread  = 0.7f;   // added per shot (decays)
    public float SpreadRecovery = 6f;  // 1/s

    // ---- shot feedback ----
    public string FireLayerSound = "Assets/Audio/gun_sub.wav";   // low-end punch layer under FireSound ("" = none)
    public string FireTailSound  = "";                           // room tail ("" = none; the recordings carry their own)
    public string AdsSound       = "Assets/Audio/ads_tick.wav";  // aim in/out click ("" = none)
    public float KickBack  = 0.55f;    // viewmodel recoil: backward velocity impulse (m/s)
    public float KickUp    = 42f;      // muzzle climb impulse (deg/s)
    public float KickSide  = 14f;      // alternating yaw/roll wobble (deg/s)
    public float CamKickPitch = 0.8f;  // camera recoil per shot (deg)
    public float CamKickYaw   = 0.25f;
    public float FlashIntensity = 30f; // muzzle-flash light pulse (prefab child "MuzzleFlash")
    public float FlashTime = 0.045f;
    public string FlashChildName = "MuzzleFlash";
    public string FlashMeshChildName = "Flash";
    public string ShellPrefab = "Assets/Prefabs/Shell.ventity";

    // ---- runtime wiring (written by WeaponLoadout — leave alone in the Inspector) ----
    public bool IsFpInstance = false;
    public bool Equipped = false;

    protected float Cooldown;
    private bool _fireHeld, _adsPrev;
    private int _shotCounter;
    private float _flashLeft, _spreadKick;
    private bool _fxSearched;
    private Light _flashLight;
    private long _flashMesh;
    private Vector3 _flashScale = Vector3.One;
    private System.Random _rng = new System.Random(1337);

    /// <summary>The cone the next shot scatters in (degrees, half-angle) — what the crosshair shows.</summary>
    public float CurrentSpread
    {
        get
        {
            float moveN = PlayerRig.Speed / 6.3f; if (moveN > 1f) moveN = 1f;
            float baseS = PlayerRig.Ads ? AdsSpread : HipSpread + MoveSpread * moveN;
            return baseS + _spreadKick * (PlayerRig.Ads ? 0.25f : 1f);
        }
    }

    /// <summary>Read the editor-placed socket children (if present) into the grip / sight / muzzle fields.</summary>
    public void ResolveSockets()
    {
        if (_socketsResolved) return;
        _socketsResolved = true;
        long[] kids = Scene.Children(EntityId);
        for (int i = 0; kids != null && i < kids.Length; i++)
        {
            string n = Scene.NameOf(kids[i]);
            Vector3 p = Scene.PositionOf(kids[i]);
            Quaternion q = Quaternion.FromEuler(Scene.RotationOf(kids[i]));
            if (n == "Grip")             { GripHandPos = p; GripFingerDir = q.Forward; GripPalmDir = q.Up; }
            else if (n == "SupportGrip") { SupportHandOffset = p; SupportFingerDir = q.Forward; SupportPalmDir = q.Up; }
            else if (n == "Sight")       SightOffset = p;
            else if (n == "Muzzle")      MuzzleOffset = p;
            else if (n == "Eject")       EjectOffset = p;
            else if (n == "MagWell")     MagWellOffset = p;
        }
    }
    private bool _socketsResolved;

    public override void Update(float dt)
    {
        if (!IsFpInstance || !Equipped) return;
        ResolveSockets();
        FlashTick(dt);
        if (_spreadKick > 0f) { _spreadKick -= SpreadRecovery * dt * (0.5f + _spreadKick); if (_spreadKick < 0f) _spreadKick = 0f; }
        if (!PlayerRig.Ready || PlayerRig.Inspect) return;
        PlayerRig.CurrentSpread = CurrentSpread;

        if (Cooldown > 0f) Cooldown -= dt;
        if (!Cursor.Locked) { PlayerRig.Firing = false; return; }   // paused / menu

        if (PlayerRig.Ads != _adsPrev) { _adsPrev = PlayerRig.Ads; if (AdsSound != "") Audio.PlayOneShot2D(AdsSound, 0.35f, PlayerRig.Ads ? 1f : 0.92f); }
        if (PlayerRig.Switching) { PlayerRig.Firing = false; _fireHeld = Input.GetKey("LButton"); return; }   // hands busy with the swap

        bool held = Input.GetKey("LButton");
        PlayerRig.FireHeld = held;                 // tells the movement to drop out of a sprint
        bool wants = Automatic ? held : (held && !_fireHeld);
        _fireHeld = held;

        // no run-and-gun: while sprinting (and for the sprint-out moment after it) or climbing, the gun is down
        bool gunDown = PlayerRig.IsSprinting || PlayerRig.SprintOut > 0f || PlayerRig.Mantling;
        bool shot = false;
        if (wants && !gunDown && Cooldown <= 0f && CanFire())
        {
            Cooldown = 60f / (FireRate < 1f ? 1f : FireRate);
            Fire();
            shot = true;
        }
        PlayerRig.Firing = shot;   // per-shot pulse -> the 3P body's masked fire animation layer

        Tick(dt);
    }

    /// <summary>May the weapon fire right now? Firearms add ammo/reload gating.</summary>
    protected virtual bool CanFire() { return true; }

    /// <summary>Per-frame hook after the input handling (reload timers etc.).</summary>
    protected virtual void Tick(float dt) { }

    /// <summary>THE polymorphic action — default: fire sound + hitscan + shot feedback + "damage"
    /// message. Override for melee arcs, projectiles, shotguns…</summary>
    public virtual void Fire()
    {
        // layered shot: crack + low-end layer + room tail, slightly detuned so no two shots sound identical
        float pitch = 0.96f + 0.08f * (float)_rng.NextDouble();
        if (FireSound != "") Audio.PlayOneShot2D(FireSound, 0.9f, pitch);
        if (FireLayerSound != "") Audio.PlayOneShot2D(FireLayerSound, 0.55f, pitch * 0.98f);
        if (FireTailSound != "") Audio.PlayOneShot2D(FireTailSound, 0.45f, 0.94f + 0.1f * (float)_rng.NextDouble());

        // scatter inside the current cone (uniform over the disc)
        float spread = CurrentSpread * 0.0174532925f;
        float rr = spread * (float)System.Math.Sqrt(_rng.NextDouble()), ph = (float)(_rng.NextDouble() * 6.2831853);
        float yawRad = PlayerRig.Yaw * 0.0174532925f + rr * (float)System.Math.Cos(ph);
        float pitchRad = PlayerRig.Pitch * 0.0174532925f + rr * (float)System.Math.Sin(ph);
        _spreadKick += ShotSpread;
        float cy = (float)System.Math.Cos(yawRad), sy = (float)System.Math.Sin(yawRad);
        float cp = (float)System.Math.Cos(pitchRad), sp = (float)System.Math.Sin(pitchRad);
        Vector3 dir = new Vector3(sy * cp, -sp, cy * cp);
        RaycastHit hit;
        if (Physics.Raycast(PlayerRig.EyePos, dir, Range, out hit))
        {
            SendMessage(hit.EntityId, "damage", Damage);
            // physics props (Rigidbody): the bullet shoves them — impulse along the shot at the hit point (Physics v2)
            if (BulletImpulse > 0f && Physics.HasRigidbody(hit.EntityId)) Physics.AddImpulseAtPoint(hit.EntityId, dir * BulletImpulse, hit.Point);
            string tag = Scene.TagOf(hit.EntityId);
            if (tag == "Enemy" || tag == "Monster" || Scene.NameOf(hit.EntityId).IndexOf("Monster") >= 0)
            {
                PlayerRig.HitMarkerT = 0.16f; PlayerRig.HitMarkerKill = false;
            }
        }
        FireFeedback();
    }

    /// <summary>Viewmodel + camera recoil, muzzle flash and shell casing for one shot.</summary>
    protected void FireFeedback()
    {
        float side = ((_shotCounter++) & 1) == 0 ? 1f : -1f;
        float r = (float)_rng.NextDouble();
        PlayerRig.WeaponKickBack  += KickBack * (0.85f + 0.3f * r);
        PlayerRig.WeaponKickPitch += KickUp * (0.85f + 0.3f * r);
        PlayerRig.WeaponKickYaw   += side * KickSide * (0.5f + 0.5f * r);
        PlayerRig.WeaponKickRoll  += side * KickSide * 0.8f;
        PlayerRig.CamKickPitch -= CamKickPitch;                    // view kick per shot
        PlayerRig.CamKickYaw   += side * CamKickYaw;

        FindEffectChildren();
        if (_flashLight != null) _flashLight.Intensity = FlashIntensity * (0.8f + 0.4f * r);
        if (_flashMesh != 0)
        {
            // no two flashes alike: random size + roll around the barrel
            float fs = 0.7f + 0.6f * (float)_rng.NextDouble();
            Scene.SetScaleOf(_flashMesh, new Vector3(_flashScale.X * fs, _flashScale.Y * fs, _flashScale.Z * (0.8f + 0.5f * (float)_rng.NextDouble())));
            Scene.SetRotationOf(_flashMesh, new Vector3(0f, 0f, (float)(_rng.NextDouble() * 360.0)));
            Scene.SetActive(_flashMesh, true);
        }
        _flashLeft = FlashTime;

        Vector3 wp, we;
        if (ShellPrefab != "" && Scene.TryGetWorldPose(EntityId, out wp, out we))
        {
            Quaternion q = Quaternion.FromEuler(we);
            long shell = Scene.Instantiate(ShellPrefab, wp + q.Rotate(EjectOffset), 0f);
            if (shell != 0) SendMessage(shell, "eject", q.Rotate(new Vector3(2.4f, 1.6f, -0.4f)));
        }
    }

    private void FindEffectChildren()
    {
        if (_fxSearched) return;
        _fxSearched = true;
        long[] kids = Scene.Children(EntityId);
        for (int i = 0; kids != null && i < kids.Length; i++)
        {
            string n = Scene.NameOf(kids[i]);
            if (n == FlashChildName) _flashLight = Scene.GetLight(kids[i]);
            else if (n == FlashMeshChildName) { _flashMesh = kids[i]; _flashScale = Scene.ScaleOf(kids[i]); }
        }
        if (_flashLight != null) _flashLight.Intensity = 0f;
    }

    private void FlashTick(float dt)
    {
        if (_flashLeft <= 0f) return;
        _flashLeft -= dt;
        if (_flashLeft <= 0f)
        {
            if (_flashLight != null) _flashLight.Intensity = 0f;
            if (_flashMesh != 0) Scene.SetActive(_flashMesh, false);
        }
    }

    /// <summary>Called by the loadout on switch — override for draw/holster anims and sounds.</summary>
    public virtual void OnEquip()
    {
        // Scene.SetActive(weapon, true) re-activates the children too: park the flash mesh until the next shot.
        FindEffectChildren();
        if (_flashMesh != 0) Scene.SetActive(_flashMesh, false);
        if (_flashLight != null) _flashLight.Intensity = 0f;
    }
    public virtual void OnHolster()
    {
        _flashLeft = 0f;
        if (_flashLight != null) _flashLight.Intensity = 0f;
        if (_flashMesh != 0) Scene.SetActive(_flashMesh, false);
    }
}
