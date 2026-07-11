using Vortex;

// FIREARM — abstract mid-layer: everything with a magazine (ammo, dry click, reload with timer,
// and the VISIBLE mag pull: during the reload the "Mag" child of the weapon is bone-attached to the
// rig's LEFT hand, so the hand physically pulls the magazine out and slams the new one in — on BOTH
// the FP arms and the 3P world body (each weapon instance animates its own mag on its own rig).
public abstract class Firearm : Weapon
{
    public int MagazineSize = 30;
    public int ReserveAmmo = 90;
    public float ReloadTime = 2.4f;
    public string DrySound = "Assets/Audio/dry_click.wav";
    public string ReloadSound = "Assets/Audio/mag_in.wav";

    // ---- visible mag pull ----
    public string MagChildName = "Mag";       // weapon-prefab child carrying the magazine mesh ("" = none)
    public float MagOutAt = 0.18f;            // reload progress when the hand grabs the mag (0..1)
    public float MagInAt  = 0.70f;            // progress when the new mag is seated back in the gun
    public Vector3 MagHandOffset = new Vector3(0f, 0.06f, 0f);       // mag-in-left-hand placement (m, bone frame)
    public Vector3 MagHandRotation = new Vector3(81.2f, -156.6f, 126.9f);

    // set by WeaponLoadout: the rig THIS instance hangs on (FP arms or 3P body)
    public long RigEntityId = 0;

    protected int Mag = -1;
    protected int Reserve = -1;
    protected float ReloadLeft;
    private bool _reloadHeld;

    // mag-follow state (per instance, driven by the SHARED PlayerRig.Reloading level)
    private long _magEnt;
    private bool _magSearched, _magOnHand;
    private float _reloadAnimT = -1f;         // -1 = not reloading (3P instances track the level themselves)
    private Vector3 _magLocalPos, _magLocalRot;

    public int MagCount { get { EnsureAmmo(); return Mag; } }
    public int ReserveCount { get { EnsureAmmo(); return Reserve; } }
    public bool IsReloading { get { return ReloadLeft > 0f; } }

    private void EnsureAmmo()
    {
        if (Mag < 0) { Mag = MagazineSize; Reserve = ReserveAmmo; }
    }

    protected override bool CanFire()
    {
        EnsureAmmo();
        if (ReloadLeft > 0f) return false;
        if (Mag <= 0)
        {
            if (DrySound != "") Audio.PlayOneShot2D(DrySound, 0.7f, 1f);
            Cooldown = 0.25f;
            return false;
        }
        return true;
    }

    public override void Fire()
    {
        Mag -= 1;
        PlayerRig.Ammo = Mag; PlayerRig.MagSize = MagazineSize;
        base.Fire();
    }

    protected override void Tick(float dt)
    {
        EnsureAmmo();
        PlayerRig.Ammo = Mag; PlayerRig.MagSize = MagazineSize;

        if (ReloadLeft > 0f)
        {
            ReloadLeft -= dt;
            PlayerRig.Reloading = ReloadLeft > 0f;   // level -> both rigs' reload animation layer
            if (ReloadLeft <= 0f)
            {
                int need = MagazineSize - Mag;
                int take = need < Reserve ? need : Reserve;
                Mag += take;
                Reserve -= take;
                PlayerRig.Ammo = Mag;
            }
            return;
        }

        bool r = Input.GetKey("R");
        if (r && !_reloadHeld && Mag < MagazineSize && Reserve > 0)
        {
            ReloadLeft = ReloadTime;
            PlayerRig.Reloading = true;
            if (ReloadSound != "") Audio.PlayOneShot2D(ReloadSound, 0.8f, 1f);
        }
        _reloadHeld = r;
    }

    // ---- visible mag pull: runs on EVERY instance (FP + 3P), keyed off the shared Reloading level ----
    public override void LateUpdate(float dt)
    {
        if (MagChildName == "" || RigEntityId == 0) return;
        if (!_magSearched)
        {
            _magSearched = true;
            long[] kids = Scene.Children(EntityId);
            for (int i = 0; kids != null && i < kids.Length; i++)
                if (Scene.NameOf(kids[i]) == MagChildName) { _magEnt = kids[i]; break; }
            if (_magEnt != 0)
            {
                _magLocalPos = Scene.PositionOf(_magEnt);      // local rest pose under the weapon
                _magLocalRot = Scene.RotationOf(_magEnt);
            }
        }
        if (_magEnt == 0) return;

        if (PlayerRig.Reloading)
        {
            if (_reloadAnimT < 0f) _reloadAnimT = 0f; else _reloadAnimT += dt;
            float p = ReloadTime > 0.01f ? _reloadAnimT / ReloadTime : 1f;
            bool onHand = p >= MagOutAt && p < MagInAt;
            if (onHand && !_magOnHand)
            {
                Animation.Attach(_magEnt, RigEntityId, "mixamorig:LeftHand", MagHandOffset, MagHandRotation);
                _magOnHand = true;
            }
            else if (!onHand && _magOnHand && p >= MagInAt)
            {
                ReleaseMag();
            }
        }
        else
        {
            _reloadAnimT = -1f;
            if (_magOnHand) ReleaseMag();
        }
    }

    private void ReleaseMag()
    {
        Animation.Detach(_magEnt, false);
        Scene.SetPositionOf(_magEnt, _magLocalPos);            // snap back into the mag well
        Scene.SetRotationOf(_magEnt, _magLocalRot);
        _magOnHand = false;
    }

    public override void OnHolster()
    {
        ReloadLeft = 0f;
        PlayerRig.Reloading = false;
        if (_magOnHand) ReleaseMag();
    }
}
