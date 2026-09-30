using Vortex;

// FIREARM — abstract mid-layer: everything with a magazine (ammo, dry click, reload with timer and sound
// cues, and the VISIBLE mag change).
//   FP instance: the procedural ViewmodelRig reads PlayerRig.ReloadProgress and moves the support hand and
//                the "Mag" child along the reload path (grab, pull, drop, insert, slap) — see ViewmodelRig.cs.
//   3P instance: during the body's rifle_reload clip the "Mag" child is bone-attached to the LEFT hand between
//                MagOutAt and MagInAt, so other cameras see the hand pull the magazine.
public abstract class Firearm : Weapon
{
    public int MagazineSize = 30;
    public int ReserveAmmo = 90;
    public float ReloadTime = 2.4f;
    public string DrySound = "Assets/Audio/dry_click.wav";
    public string MagOutSound = "Assets/Audio/mag_out_1.wav";  // reload progress 0.15: mag leaves the well
    public string ReloadSound = "Assets/Audio/mag_in.vsndc";    // progress 0.72: fresh mag seated (random container)
    public string ActionSound = "Assets/Audio/bolt_rack.wav";  // progress 0.86: charging handle

    // ---- 3P visible mag pull (bone-attached to the body's left hand) ----
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
    private int _cueStage;                    // reload sound cues fired so far (0..3)

    // 3P mag-follow state (driven by the SHARED PlayerRig.Reloading level)
    private long _magEnt;
    private bool _magSearched, _magOnHand;
    private float _reloadAnimT = -1f;
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
            float p = ReloadTime > 0.01f ? 1f - ReloadLeft / ReloadTime : 1f;
            if (p > 1f) p = 1f;
            PlayerRig.Reloading = ReloadLeft > 0f;   // level -> the 3P body's reload animation layer
            PlayerRig.ReloadProgress = ReloadLeft > 0f ? p : -1f;
            if (_cueStage == 0 && p >= 0.15f) { _cueStage = 1; if (MagOutSound != "") Audio.PlayOneShot2D(MagOutSound, 0.8f, 1f); }
            if (_cueStage == 1 && p >= 0.72f) { _cueStage = 2; if (ReloadSound != "") Audio.PlayOneShot2D(ReloadSound, 0.85f, 1f); }
            if (_cueStage == 2 && p >= 0.86f) { _cueStage = 3; if (ActionSound != "") Audio.PlayOneShot2D(ActionSound, 0.7f, 1.05f); }
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
            _cueStage = 0;
            PlayerRig.Reloading = true;
            PlayerRig.ReloadProgress = 0f;
        }
        _reloadHeld = r;
    }

    // ---- 3P visible mag pull: bone-attach the mag to the body's left hand for the middle of the reload.
    // The FP instance's mag is moved by the ViewmodelRig instead. ----
    public override void LateUpdate(float dt)
    {
        if (IsFpInstance || MagChildName == "" || RigEntityId == 0) return;
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
        base.OnHolster();
        ReloadLeft = 0f;
        PlayerRig.Reloading = false;
        PlayerRig.ReloadProgress = -1f;
        if (_magOnHand) ReleaseMag();
    }
}
