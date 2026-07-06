using Vortex;

// The v2.7 weapon recipe — a complete pistol in ONE project script, zero engine gameplay code.
// Attach to a "Pistol" entity parented under the player CAMERA whose Mesh Renderer has the
// "First-Person (viewmodel)" flag: it renders with its own FOV and never clips through walls.
//
// One left-click fires everything in the same frame:
//   hitscan (Physics.Raycast where you look) -> "damage" message to whatever you hit
//   recoil     CameraFX.Kick (view kicks up, spring-damper pulls it back)
//   muzzle     the pistol's own point light pulses for a few hundredths of a second
//   shell      a pooled casing prefab pops out with an arc (ShellCasing.cs despawns it)
//   bang       one-shot audio (drop clip paths into the fields below; empty = silent)
// R reloads (sprint cancels it), the HUD shows mag/reserve bottom-right.
public class WeaponPistol : VortexBehaviour
{
    public int   MagazineSize   = 8;
    public int   ReserveAmmo    = 24;
    public float FireInterval   = 0.34f;
    public float ReloadTime     = 1.6f;
    public float Damage         = 25f;
    public float Range          = 60f;
    public float KickPitch      = 1.7f;    // view kick per shot (degrees up)
    public float KickBack       = 0.04f;   // view kick backwards (meters)
    public float MuzzleFlashTime = 0.045f;
    public string ShellPrefab   = "Assets/Prefabs/Shell.ventity";
    public string FireSound     = "";      // e.g. Assets/Audio/pistol_shot.vsndc (random container = variations)
    public string DrySound      = "";
    public string ReloadSound   = "";

    private int   _mag, _reserve;
    private float _cooldown, _reloadT, _muzzleT;
    private bool  _reloading, _mHeld, _rHeld;
    private int   _shotIndex;
    private Light _muzzle;

    public override void Start()
    {
        _mag = MagazineSize;
        _reserve = ReserveAmmo;
        _muzzle = GetLight();                     // point light ON THE PISTOL entity = the flash
        if (_muzzle != null) _muzzle.Enabled = false;
        CameraFX.Sway(0, 0.0025f, 0.22f, 0.35f);  // breathing — the horror staple
    }

    public override void Update(float dt)
    {
        if (!Cursor.Locked) return;               // paused

        if (_cooldown > 0f) _cooldown -= dt;

        // Muzzle flash off after its pulse.
        if (_muzzleT > 0f)
        {
            _muzzleT -= dt;
            if (_muzzleT <= 0f && _muzzle != null) _muzzle.Enabled = false;
        }

        // Reload: R starts it, sprinting CANCELS it (horror rule: running for your life > topping up).
        bool r = Input.GetKey("R");
        if (r && !_rHeld && !_reloading && _mag < MagazineSize && _reserve > 0) StartReload();
        _rHeld = r;
        if (_reloading)
        {
            if (Input.GetKey("LeftShift")) { _reloading = false; }   // interrupted
            else
            {
                _reloadT -= dt;
                if (_reloadT <= 0f) FinishReload();
            }
        }

        // Fire on click edge.
        bool m = UI.MouseDown;
        if (m && !_mHeld && !_reloading && _cooldown <= 0f)
        {
            if (_mag > 0) Fire();
            else
            {
                _cooldown = 0.2f;
                if (DrySound != "") Audio.PlayOneShot(DrySound, Position);
                if (_reserve > 0) StartReload();   // auto-reload on empty click
            }
        }
        _mHeld = m;

        DrawHud();
    }

    private void Fire()
    {
        _mag = _mag - 1;
        _cooldown = FireInterval;
        _shotIndex = _shotIndex + 1;

        // Muzzle flash: the pistol's own light pulses (lite VFX — GPU particles are the v3.3 upgrade).
        if (_muzzle != null) { _muzzle.Enabled = true; _muzzleT = MuzzleFlashTime; }

        // Recoil: view kicks up-and-back, alternating slight yaw — spring-damper recovers.
        float yawJitter = (_shotIndex % 2 == 0) ? 0.35f : -0.35f;
        CameraFX.Kick(new Vector3(-KickPitch, yawJitter, 0f), new Vector3(0f, 0f, -KickBack));

        // Hitscan from the muzzle: this entity sits at the camera, Forward is the look direction.
        RaycastHit hit;
        if (Physics.Raycast(Position, Forward, Range, out hit))
        {
            SendMessage(hit.EntityId, "damage", Damage);
            Debug.DrawSphere(hit.Point, 0.06f, 1f, 0.6f, 0.1f, 0.25f);  // impact placeholder
        }

        // Shell casing: pooled prefab with its own arc + despawn script.
        if (ShellPrefab != "")
        {
            long shell = Scene.Instantiate(ShellPrefab,
                new Vector3(Position.X + Right.X * 0.12f, Position.Y - 0.05f, Position.Z + Right.Z * 0.12f));
            if (shell != 0) SendMessage(shell, "eject", null);
        }

        if (FireSound != "") Audio.PlayOneShot(FireSound, Position);
    }

    private void StartReload()
    {
        _reloading = true;
        _reloadT = ReloadTime;
        if (ReloadSound != "") Audio.PlayOneShot(ReloadSound, Position);
        // Rigged setups pair this with Animation.PlaySynced(character reload + weapon reload) —
        // see the docs' weapon recipe. The starter pistol is a viewmodel prop, so a timer carries it.
    }

    private void FinishReload()
    {
        _reloading = false;
        int need = MagazineSize - _mag;
        int take = need < _reserve ? need : _reserve;
        _mag = _mag + take;
        _reserve = _reserve - take;
    }

    private void DrawHud()
    {
        float W = UI.Width, H = UI.Height;
        if (W < 10f) return;
        UI.Rect(W - 150f, H - 64f, 130f, 44f, Color.Rgba(10, 10, 14, 170), 8f);
        UI.Text(_mag + " / " + _reserve, W - 140f, H - 56f, 110f, 26f, 19f,
            _mag == 0 ? Color.Rgba(255, 90, 90, 240) : Color.Rgba(235, 235, 240, 240), 0, 700);
        if (_reloading)
        {
            float f = 1f - (_reloadT / ReloadTime);
            UI.Rect(W - 150f, H - 74f, 130f, 5f, Color.Rgba(60, 60, 70, 200), 2f);
            UI.Rect(W - 150f, H - 74f, 130f * f, 5f, Color.Rgba(235, 200, 90, 230), 2f);
        }
    }
}
