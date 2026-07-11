using Vortex;

// Shared player-rig state so the WORLD-SPACE weapon viewmodel (a top-level entity, because camera-child
// meshes don't render in the GameHost) can follow the camera each frame. Written by CoDMovement, read
// by the weapon system (Weapon/Firearm instances). Same script assembly => the static is shared.
public static class PlayerRig
{
    public static Vector3 EyePos;
    public static float Yaw, Pitch;
    public static bool Ads;
    public static bool Ready;
    public static float Speed;       // smoothed horizontal speed (for walk bob)
    public static bool Grounded;

    // ---- world-character extension (#178 additive aim-offset) ----
    // The body is now a REAL skinned actor standing on the ground (PlayerBody), not a self-teleporting
    // viewmodel. These let it stand at the feet, face where you look, pitch its upper body with the aim
    // (so chest+arms+weapon move as one and the gun stays in the hands), and pick a locomotion clip.
    public static Vector3 FootPos;                  // capsule feet world position = body root anchor
    public static float   BodyYaw;                  // body facing (deg) — follows camera yaw
    public static float   AimPitch;                 // clamped look pitch (deg) fed additively to the spine bones
    public static float   MoveForwardN, MoveRightN; // local move intent relative to facing (-1..1) for the blend
    public static bool    IsSprinting, IsCrouched, IsSliding, IsAirborne;

    // ---- orbit inspector (press P): the render camera detaches and circles a standing character so you can
    // inspect it from outside with mouse-orbit + wheel-zoom. FP body + FP gun hide themselves while active. ----
    public static bool    Inspect;

    // ---- weapon ACTION state: written by the active Weapon instance, read by LocomotionController
    // so the WORLD BODY reloads/fires when YOU do — this is what makes other cameras see you reload + shoot. ----
    public static bool  Firing;        // a shot went off THIS frame (pulse)
    public static bool  Reloading;     // reload in progress (level, true for the whole reload)

    // shared rigid-weapon offsets (legacy viewmodel tuning; kept for compatibility):
    public static float RecoilPitch, RecoilYaw;      // extra view kick added to the gun's rotation
    public static float RollAdd;                      // gun-only roll (deg) — reload tilt-in + wall port-arms
    public static float OffXAdd, OffYAdd, OffZAdd;    // ADS pull-in + walk bob + reload + wall added to every part's local offset

    // magazine-part animation shares (legacy; the weapon system animates the mag via Animation.Attach) —
    // is a SEPARATE viewmodel entity so it can physically drop out and slam back in.
    public static float MagOffX, MagOffY, MagOffZ;    // extra camera-local offset applied to the mag ONLY
    public static bool  MagHidden;                     // true mid-swap (old mag gone, before new mag is in)

    // viewmodel SWAY (CS:GO-style): the gun trails the look a touch then settles = smooth, not rigid.
    public static float SwayYaw, SwayPitch;           // deg, added to every viewmodel part's rotation
    public static float SwayX, SwayY;                 // m, camera-local positional lag

    // camera up = Forward x Right. Viewmodel parts MUST offset vertically along THIS, not world-up — world-up
    // makes the gun swing off-screen when you look steeply up/down. Keeps the weapon locked in front at any pitch.
    public static Vector3 ViewUp(Vector3 f, Vector3 r)
    {
        return new Vector3(f.Y * r.Z - f.Z * r.Y, f.Z * r.X - f.X * r.Z, f.X * r.Y - f.Y * r.X);
    }

    // per-shot CAMERA recoil velocity impulses queued by the weapon, consumed by CoDMovement's camera-recoil
    // spring — the VIEW kicks with every shot (not just the gun), so aim climbs like a real shooter.
    public static float CamKickPitch, CamKickYaw;

    // ---- player vitals (written by PlayerHealth, read by HudManager) ----
    public static float Health = 100f, MaxHealth = 100f;
    public static int   Medkits = 3;

    // ---- ammo (written by PlayerWeapon, read by HudManager) ----
    public static int   Ammo = 30, MagSize = 30;

    // ---- active weapon slot (see WeaponLoadout.ActiveSlot; kept for HUD compatibility) ----
    public static int   WeaponSlot = 0;
}

// Call-of-Duty-feel first-person movement — 100% game-side, tweak freely.
// WASD move · mouse look · Shift sprint · double-tap-W tactical sprint · Ctrl/C crouch ·
// crouch-while-sprinting = SLIDE · Space jump · RMB aims (slows you + narrows FOV) · ESC pause (Q quits).
//
// Feel notes vs. the horror-starter Quake controller: higher ground accel + friction = snappy,
// grounded, "instant" CoD response (not floaty). Sprint punches the FOV out; ADS pulls it in.
// Landing dips the view; walking bobs it (CameraFX composes the view AND the weapon together, so
// the gun bobs/kicks with you). World FOV is owned HERE; the weapon owns the viewmodel FOV.
//
// Lives on the PLAYER entity. The camera is a child at local (0,0,0); the weapon viewmodel is a
// child of the camera, so it inherits look + rides every CameraFX impulse.
public class CoDMovement : VortexBehaviour
{
    // ---- speeds (m/s) ----
    public float WalkSpeed      = 4.6f;
    public float SprintSpeed    = 6.3f;
    public float TacSprintSpeed = 7.4f;
    public float CrouchSpeed    = 2.6f;
    public float AdsSpeed       = 3.1f;

    // ---- look ----
    public float MouseSens    = 0.09f;
    public float PadLookSpeed = 230f;

    // ---- jump / gravity ----
    public float JumpSpeed = 6.6f;
    public float Gravity   = 22f;

    // ---- stance ----
    public float CrouchDrop = 0.62f;   // eye drop when crouched
    public float StepHeight = 0.4f;    // auto-climb (curbs, low crates)

    // ---- FOV ----
    public float BaseFov      = 80f;
    public float SprintFovAdd = 9f;
    public float TacFovAdd    = 15f;
    public float AdsWorldFov  = 50f;   // scope zoom
    public float FovLerp      = 9f;

    // ---- accel / friction ---- (tuned for a smoother, more responsive L4D2-ish glide: quicker to top speed,
    // a touch more air control so mid-air steering doesn't feel stuck, slightly less grabby friction)
    public float GroundAccel = 19f;
    public float AirAccel    = 3.2f;
    public float Friction    = 8.5f;
    public float SlideFriction = 3.0f;

    // ---- slide ----
    public float SlideBoost = 3.4f;    // extra speed injected at slide start
    public float SlideTime  = 0.65f;
    public float SlideDrop  = 0.95f;   // eye drop during slide

    // capsule
    public float CapsuleRadius = 0.35f;
    public float CapsuleHeight = 1.85f;

    private float _standEyeY;
    private float _eyeCur, _fovCur;
    private float _vx, _vz, _vy;
    private bool  _grounded, _prevGrounded;
    private bool  _jumpHeld, _escHeld, _paused;
    private float _pitch, _yaw;
    private bool  _levelCapture, _forceAds, _lookDown;
    private float _wTapTimer;          // double-tap-W window
    private bool  _wHeld;
    private bool  _tacSprint;
    private bool  _sliding;
    private float _slideT;
    private float _rollCur;            // camera roll (strafe + slide lean)
    private float _camRecPitch, _camRecVel, _camRecYaw, _camRecYawVel;   // camera-recoil spring (view kick per shot)
    public  float CamRecoilStiff = 55f, CamRecoilDamp = 11f;   // softer spring -> the climb lingers + accumulates = clearly visible

    // R6-style Q/E lean toggle (peek around corners)
    private int   _leanTarget;        // -1 left · 0 centre · +1 right
    private float _leanCur;
    private bool  _qHeld, _eHeld;
    public  float LeanRoll = 14f, LeanOffset = 0.42f;
    // bunny-hop guard
    private float _jumpCd;
    public  float JumpCooldown = 0.32f, MaxHorizSpeed = 9.5f;
    private float _speedSmooth;        // smoothed horizontal speed (for bob)

    // ---- orbit inspector (P) ----
    private bool  _inspect, _insHeld, _fpHidden;
    private float _orbYaw, _orbPitch = 14f, _orbDist = 3.2f;
    private Vector3 _orbTarget;
    public  string InspectTargetName = "soldier";   // the standing character to circle
    public  float  OrbitSens = 0.22f, ZoomSens = 0.6f;

    public override void Start()
    {
        Cursor.Locked = true;
        _standEyeY = Position.Y;
        _eyeCur = _standEyeY;
        Vector3 r = Rotation; _pitch = r.X; _yaw = r.Y;
        _levelCapture = System.Environment.GetEnvironmentVariable("VM_LEVEL") == "1";   // capture-only: lock a clean level view
        _forceAds     = System.Environment.GetEnvironmentVariable("VM_ADS") == "1";     // capture-only: force aim-down-sight
        _lookDown     = System.Environment.GetEnvironmentVariable("VM_DOWN") == "1";    // capture-only: look down to check body awareness
        if (System.Environment.GetEnvironmentVariable("VM_INSPECT") == "1") { _inspect = true; PlayerRig.Inspect = true; }  // capture-only: force orbit inspect
        string _itn = System.Environment.GetEnvironmentVariable("VM_INSPECT_TARGET");
        if (_itn != null && _itn != "") InspectTargetName = _itn;   // capture-only: orbit a specific character (e.g. WCharakter)
        _orbYaw = EnvF("VM_ORBYAW", _orbYaw); _orbPitch = EnvF("VM_ORBPITCH", _orbPitch); _orbDist = EnvF("VM_ORBDIST", _orbDist);
        _grounded = true;
        _fovCur = BaseFov;
        Camera.SetFieldOfView(_fovCur);
        Physics.SetCharacterOptions(StepHeight, 55f);
        CameraFX.SetSpring(150f, 20f);          // crisp view recovery
        Settings.SetVSync(false);               // uncap FPS
        if (System.Environment.GetEnvironmentVariable("VM_LOWRES") == "1") Settings.SetRenderScale(0.4f);   // FPS diagnostic: is it fill/pixel-bound?
        UserSettings.Fov = BaseFov;             // seed the shared settings the ESC menu edits
        UserSettings.MouseSensitivity = MouseSens;
    }

    public override void Update(float dt)
    {
        if (dt <= 0f) return;

        // ESC opens the unified settings menu. It FREES the mouse and routes input to the menu, but does
        // NOT show a hard "PAUSED" screen. The mouse is ALSO always freed when the window is not focused.
        bool esc = Input.GetKey("Escape") || Input.GetGamepadButtonDown("Start");
        if (esc && !_escHeld) EscMenu.HandleToggle();
        _escHeld = esc;

        Cursor.Locked = !EscMenu.IsOpen && Input.WindowFocused;

        if (EscMenu.IsOpen)
        {
            CameraFX.StopSway(0);
            EscMenu.Tick(dt);            // renders + (while focused) handles the sliders/toggles/tabs
            _vx = 0f; _vz = 0f;
            return;
        }
        if (!Input.WindowFocused) { _vx = 0f; _vz = 0f; UpdateFov(dt); return; }

        // NOTE: the old template P-orbit-inspector is GONE — flying + watching is now the ENGINE's built-in DEBUG
        // FREECAM (press P in editor play; hold RMB to drive the player). It works in any scene and is stripped from
        // shipped builds. VM_INSPECT=1 still forces the legacy capture path for internal screenshots only.
        if (_inspect) { InspectUpdate(dt); return; }

        if (Cursor.Locked) Move(dt);
        UpdateFov(dt);
    }

    private void ToggleInspect()
    {
        _inspect = !_inspect;
        PlayerRig.Inspect = _inspect;
        if (_inspect)
        {
            long s = Scene.Find(InspectTargetName);
            Vector3 p = s != 0 ? Scene.PositionOf(s) : new Vector3(Position.X, Position.Y - 1.6f, Position.Z);
            _orbTarget = new Vector3(p.X, p.Y + 1.0f, p.Z);   // aim at the character's chest
            _orbYaw = _yaw; _orbPitch = 12f; _orbDist = 3.2f;
        }
        else { SetFpVisible(true); _fpHidden = false; }   // restore the FP body/gun on exit
    }

    // Hide/show the first-person body + gun (skinned viewmodel-layer entities can't be hidden by moving them).
    private void SetFpVisible(bool v)
    {
        long b = Scene.Find("tp_character"); if (b != 0) Scene.SetActive(b, v);
        long w = Scene.Find("Weapon");       if (w != 0) Scene.SetActive(w, v);
    }

    // Circle the target character: mouse orbits, wheel (or W/S) zooms. Camera looks at the target the whole time.
    private void InspectUpdate(float dt)
    {
        Cursor.Locked = true;
        if (!_fpHidden) { SetFpVisible(false); _fpHidden = true; }   // hide FP body/gun once, on entering inspect
        // re-acquire the target each frame in case it settled/moved
        long s = Scene.Find(InspectTargetName);
        if (s != 0) { Vector3 p = Scene.PositionOf(s); _orbTarget = new Vector3(p.X, p.Y + 1.0f, p.Z); }

        if (!_levelCapture) { _orbYaw += Input.MouseDeltaX * OrbitSens; _orbPitch += Input.MouseDeltaY * OrbitSens; }
        if (_orbPitch > 85f) _orbPitch = 85f; else if (_orbPitch < -85f) _orbPitch = -85f;
        _orbDist  -= Input.ScrollDelta * ZoomSens;
        if (Input.GetKey("W")) _orbDist -= 3f * dt;
        if (Input.GetKey("S")) _orbDist += 3f * dt;
        if (_orbDist < 0.5f) _orbDist = 0.5f; else if (_orbDist > 14f) _orbDist = 14f;

        double yr = _orbYaw * System.Math.PI / 180.0, pr = _orbPitch * System.Math.PI / 180.0;
        float cx = (float)(System.Math.Cos(pr) * System.Math.Sin(yr));
        float cy = (float)System.Math.Sin(pr);
        float cz = (float)(System.Math.Cos(pr) * System.Math.Cos(yr));
        Position = new Vector3(_orbTarget.X + cx * _orbDist, _orbTarget.Y + cy * _orbDist, _orbTarget.Z + cz * _orbDist);
        Rotation = new Vector3(_orbPitch, _orbYaw + 180f, 0f);   // face back toward the target

        Camera.SetFieldOfView(70f);
        // keep the rig readable so nothing NaNs; consumers gate on PlayerRig.Inspect to hide the FP body/gun
        PlayerRig.EyePos = Position; PlayerRig.Yaw = _orbYaw + 180f; PlayerRig.Pitch = _orbPitch;
        PlayerRig.Ready = true;

        // ---- PREVIEW the body's animation states on the standing character while you orbit it. LocomotionController
        // reads these, so you SEE the world body walk/run/aim/reload/fire (and that the gun stays glued to the hand). ----
        float pvSpeed = 0f, pvFwd = 0f; bool pvAds = false;
        if (Input.GetKey("D1")) { pvSpeed = 3f; pvFwd = 1f; }        // 1 = walk
        else if (Input.GetKey("D2")) { pvSpeed = 6f; pvFwd = 1f; }   // 2 = run
        if (Input.GetKey("D3")) pvAds = true;                        // 3 = aim
        PlayerRig.Speed = pvSpeed; PlayerRig.MoveForwardN = pvFwd; PlayerRig.MoveRightN = 0f;
        PlayerRig.Ads = pvAds; PlayerRig.IsAirborne = false;
        PlayerRig.Reloading = Input.GetKey("R");                     // hold R = reload
        PlayerRig.Firing    = Input.GetKey("F");                     // hold F = fire punch

        // NOTE: weapon-socket PLACEMENT is authored in the editor's Socket Editor now (not the game). This orbit
        // view is a read-only DEBUG viewer to sanity-check the body's animations — it does not tune anything.
        float W = UI.Width;
        if (W > 10f)
        {
            UI.Text("ANIMATION PREVIEW  (debug view — placement is done in the editor's Socket Editor)",
                    16f, 16f, W - 32f, 24f, 15f, Color.Rgba(120, 235, 150, 255), 0, 800);
            UI.Text("hold 1 walk · 2 run · 3 aim · R reload · F fire     orbit: mouse   zoom: wheel   exit: P",
                    16f, 42f, W - 32f, 20f, 12.5f, Color.Rgba(205, 210, 220, 220), 0, 600);
        }
    }

    private void Move(float dt)
    {
        // ---------------- look ----------------
        float dLookX = Input.MouseDeltaX * UserSettings.MouseSensitivity;
        float dLookY = Input.MouseDeltaY * UserSettings.MouseSensitivity;
        _yaw   += dLookX;
        _pitch += dLookY;
        if (_levelCapture) { _pitch = _lookDown ? 42f : 0f; _yaw = 0f; dLookX = 0f; dLookY = 0f; }   // capture-only: ignore RDP mouse drift, hold a level (or look-down) view

        // viewmodel sway (CS:GO feel): the gun trails the look, then eases back to rest when still.
        float swMul = PlayerRig.Ads ? 0.25f : 1f;   // aiming steadies the weapon
        float swYawT   = System.Math.Max(-5f, System.Math.Min(5f, -dLookX * 0.8f)) * swMul;
        float swPitchT = System.Math.Max(-5f, System.Math.Min(5f, -dLookY * 0.8f)) * swMul;
        PlayerRig.SwayYaw   += (swYawT   - PlayerRig.SwayYaw)   * System.Math.Min(1f, 10f * dt);
        PlayerRig.SwayPitch += (swPitchT - PlayerRig.SwayPitch) * System.Math.Min(1f, 10f * dt);
        PlayerRig.SwayX = -PlayerRig.SwayYaw   * 0.004f;
        PlayerRig.SwayY =  PlayerRig.SwayPitch * 0.004f;
        _yaw   += Input.RightStickX * PadLookSpeed * dt;
        _pitch -= Input.RightStickY * PadLookSpeed * dt;
        if (float.IsNaN(_yaw)   || float.IsInfinity(_yaw))   _yaw = 0f;
        if (float.IsNaN(_pitch) || float.IsInfinity(_pitch)) _pitch = 0f;
        if (_pitch > 89f) _pitch = 89f; else if (_pitch < -89f) _pitch = -89f;
        _yaw %= 360f;

        // ---------------- inputs / stance ----------------
        bool ads    = Input.GetKey("RButton") || Input.LeftTrigger > 0.5f || _forceAds;
        bool crouch = Input.GetKey("LeftCtrl") || Input.GetKey("C") || Input.GetGamepadButton("B");
        bool wKey   = Input.GetKey("W");
        bool fwdHeld = wKey || Input.LeftStickY > 0.3f;

        // double-tap W -> tactical sprint latch
        if (_wTapTimer > 0f) _wTapTimer -= dt;
        if (wKey && !_wHeld) { if (_wTapTimer > 0f) _tacSprint = true; _wTapTimer = 0.28f; }
        _wHeld = wKey;

        bool sprintKey = Input.GetKey("LeftShift") || Input.GetGamepadButton("LeftStick");
        bool sprint = sprintKey && fwdHeld && !ads && !crouch && _grounded && _leanTarget == 0;
        if (!sprint) _tacSprint = false;               // dropping sprint clears tac latch
        bool tac = sprint && _tacSprint;

        // ---------------- Q / E lean (R6-style toggle) ----------------
        bool qKey = Input.GetKey("Q"); bool eKey = Input.GetKey("E");
        if (qKey && !_qHeld) _leanTarget = (_leanTarget == -1) ? 0 : -1;   // Q -> lean left / re-press to centre
        if (eKey && !_eHeld) _leanTarget = (_leanTarget ==  1) ? 0 :  1;   // E -> lean right / re-press to centre
        _qHeld = qKey; _eHeld = eKey;
        if (sprint || _sliding) _leanTarget = 0;                            // can't lean while sprinting/sliding
        _leanCur += (_leanTarget - _leanCur) * System.Math.Min(1f, 11f * dt);

        // ---------------- wish direction ----------------
        double yawRad = _yaw * System.Math.PI / 180.0;
        float fX = (float)System.Math.Sin(yawRad), fZ = (float)System.Math.Cos(yawRad);
        float rX = (float)System.Math.Cos(yawRad), rZ = (float)-System.Math.Sin(yawRad);
        float dx = 0f, dz = 0f;
        if (wKey)              { dx += fX; dz += fZ; }
        if (Input.GetKey("S")) { dx -= fX; dz -= fZ; }
        if (Input.GetKey("D")) { dx += rX; dz += rZ; }
        if (Input.GetKey("A")) { dx -= rX; dz -= rZ; }
        dx += fX * Input.LeftStickY + rX * Input.LeftStickX;
        dz += fZ * Input.LeftStickY + rZ * Input.LeftStickX;
        float wl = (float)System.Math.Sqrt(dx * dx + dz * dz);
        float strafe = 0f;
        float wishX = 0f, wishZ = 0f;
        if (wl > 0.001f) { wishX = dx / wl; wishZ = dz / wl; strafe = (rX * wishX + rZ * wishZ); }

        // ---------------- slide start ----------------
        bool slideKey = crouch;
        if (!_sliding && slideKey && (sprint || tac) && _speedSmooth > SprintSpeed * 0.7f)
        {
            _sliding = true; _slideT = SlideTime;
            _vx += wishX * SlideBoost; _vz += wishZ * SlideBoost;
        }
        if (_sliding)
        {
            _slideT -= dt;
            if (_slideT <= 0f || (!slideKey)) _sliding = false;
        }

        float maxSpeed = _sliding ? TacSprintSpeed
                       : ads    ? AdsSpeed
                       : crouch ? CrouchSpeed
                       : tac    ? TacSprintSpeed
                       : sprint ? SprintSpeed
                       :          WalkSpeed;

        // ---------------- accelerate ----------------
        if (_grounded)
        {
            ApplyFriction(_sliding ? SlideFriction : Friction, dt);
            if (!_sliding) Accelerate(wishX, wishZ, maxSpeed, GroundAccel, dt);
            else           Accelerate(wishX, wishZ, maxSpeed, GroundAccel * 0.25f, dt);
        }
        else Accelerate(wishX, wishZ, maxSpeed, AirAccel * (crouch ? 1f : 0.7f), dt);   // less air control -> harder to air-strafe
        if (float.IsNaN(_vx)) _vx = 0f; if (float.IsNaN(_vz)) _vz = 0f; if (float.IsNaN(_vy)) _vy = 0f;

        // bunny-hop guard: hard-cap absolute horizontal speed so chained air-strafe jumps can't build unlimited speed
        float hsp = (float)System.Math.Sqrt(_vx * _vx + _vz * _vz);
        if (hsp > MaxHorizSpeed) { float s = MaxHorizSpeed / hsp; _vx *= s; _vz *= s; }

        // ---------------- jump / gravity ----------------
        bool jump = Input.GetKey("Space") || Input.GetGamepadButton("A");
        if (_jumpCd > 0f) _jumpCd -= dt;
        if (_grounded && jump && !_jumpHeld && !_sliding && _jumpCd <= 0f) { _vy = JumpSpeed; _grounded = false; _jumpCd = JumpCooldown; }
        else if (_grounded && _vy < 0f) _vy = 0f;
        _vy -= Gravity * dt;
        _jumpHeld = jump;

        // ---------------- stance eye height ----------------
        float targetEye = _standEyeY - (_sliding ? SlideDrop : crouch ? CrouchDrop : 0f);
        _eyeCur += (targetEye - _eyeCur) * System.Math.Min(1f, 12f * dt);

        // ---------------- move through collision ----------------
        Vector3 cam = Position;
        Vector3 feet = new Vector3(cam.X, cam.Y - _eyeCur, cam.Z);
        Vector3 disp = new Vector3(_vx * dt, _vy * dt, _vz * dt);
        feet = Physics.MoveCharacter(feet, CapsuleRadius, CapsuleHeight, disp, EntityId);
        _grounded = Physics.Grounded;
        if (_grounded && _vy < 0f)
        {
            // landing dip scaled by impact speed
            if (!_prevGrounded)
            {
                float impact = -_vy;
                if (impact > 3f) CameraFX.Kick(new Vector3(-System.Math.Min(3.5f, impact * 0.35f), 0f, 0f),
                                               new Vector3(0f, -System.Math.Min(0.05f, impact * 0.006f), 0f));
            }
            _vy = 0f;
        }
        // R6 lean: shift the eye sideways along Right so you peek past a corner (roll added below)
        Vector3 lr = Right;
        Position = new Vector3(feet.X + lr.X * _leanCur * LeanOffset, feet.Y + _eyeCur, feet.Z + lr.Z * _leanCur * LeanOffset);
        _prevGrounded = _grounded;

        // ---------------- camera roll (strafe lean + slide + Q/E lean) ----------------
        float rollTarget = -strafe * (_sliding ? 6.5f : 1.6f) + _leanCur * LeanRoll;
        _rollCur += (rollTarget - _rollCur) * System.Math.Min(1f, 8f * dt);

        // ---- camera recoil: consume the weapon's per-shot impulses, spring back to rest → the VIEW kicks with fire ----
        _camRecVel += PlayerRig.CamKickPitch; PlayerRig.CamKickPitch = 0f;
        _camRecYawVel += PlayerRig.CamKickYaw; PlayerRig.CamKickYaw = 0f;
        _camRecVel += (-CamRecoilStiff * _camRecPitch - CamRecoilDamp * _camRecVel) * dt; _camRecPitch += _camRecVel * dt;
        _camRecYawVel += (-CamRecoilStiff * _camRecYaw - CamRecoilDamp * _camRecYawVel) * dt; _camRecYaw += _camRecYawVel * dt;

        Rotation = new Vector3(_pitch + _camRecPitch, _yaw + _camRecYaw, _rollCur);

        // ---------------- view bob (speed-scaled) ----------------
        float hSpeed = (float)System.Math.Sqrt(_vx * _vx + _vz * _vz);
        _speedSmooth += (hSpeed - _speedSmooth) * System.Math.Min(1f, 10f * dt);
        float moveN = System.Math.Min(1f, _speedSmooth / SprintSpeed);
        if (_grounded && !ads)
        {
            // gentler view bob than before (less roll/nausea) — the weapon no longer jitters, so the bob reads clean
            float amp  = 0.005f + 0.016f * moveN;
            float rot  = 0.08f  + 0.38f  * moveN;
            float freq = 1.4f   + 3.4f   * moveN;
            CameraFX.Sway(0, amp, rot, freq);
        }
        else if (ads)   CameraFX.Sway(0, 0.0016f, 0.06f, 0.7f);   // steadied breathing
        else            CameraFX.Sway(0, 0.004f, 0.12f, 0.5f);    // airborne

        // remember stance for FOV
        _wantSprintFov = sprint; _wantTacFov = tac; _wantAdsFov = ads;

        // publish rig state for the world-space weapon viewmodel
        PlayerRig.EyePos = Position;
        // publish the RECOILED view so the gun (and its sight) ride the camera kick — sight stays on the reticle
        PlayerRig.Yaw = _yaw + _camRecYaw; PlayerRig.Pitch = _pitch + _camRecPitch;
        PlayerRig.Ads = ads; PlayerRig.Speed = _speedSmooth;
        PlayerRig.Grounded = _grounded; PlayerRig.Ready = true;

        // ---- world-character state (#178): feet anchor, body facing, aim pitch, locomotion intent ----
        PlayerRig.FootPos = new Vector3(feet.X, feet.Y, feet.Z);   // ground contact (feet already resolved this frame)
        PlayerRig.BodyYaw = _yaw;                                  // body turns with the look (turn-in-place is a later refinement)
        PlayerRig.AimPitch = _pitch + _camRecPitch;                // includes recoil so the muzzle climbs with fire
        PlayerRig.MoveForwardN = wishX * fX + wishZ * fZ;          // +1 = running the way you face, -1 = backpedal
        PlayerRig.MoveRightN   = wishX * rX + wishZ * rZ;          // +1 = strafe right
        PlayerRig.IsSprinting = sprint || tac; PlayerRig.IsCrouched = crouch;
        PlayerRig.IsSliding = _sliding; PlayerRig.IsAirborne = !_grounded;
    }

    private bool _wantSprintFov, _wantTacFov, _wantAdsFov;

    private void UpdateFov(float dt)
    {
        float baseFov = UserSettings.Fov;   // the ESC-menu FOV slider owns the base; sprint/ADS add on top
        float target = baseFov;
        if (_wantAdsFov)      target = AdsWorldFov;
        else if (_wantTacFov) target = baseFov + TacFovAdd;
        else if (_wantSprintFov) target = baseFov + SprintFovAdd;
        _fovCur += (target - _fovCur) * System.Math.Min(1f, FovLerp * dt);
        Camera.SetFieldOfView(_fovCur);
    }

    private void Accelerate(float wishX, float wishZ, float wishSpeed, float accel, float dt)
    {
        float current = _vx * wishX + _vz * wishZ;
        float add = wishSpeed - current;
        if (add <= 0f) return;
        float accelSpeed = accel * wishSpeed * dt;
        if (accelSpeed > add) accelSpeed = add;
        _vx += wishX * accelSpeed;
        _vz += wishZ * accelSpeed;
    }

    private void ApplyFriction(float friction, float dt)
    {
        float speed = (float)System.Math.Sqrt(_vx * _vx + _vz * _vz);
        if (speed < 0.0001f) { _vx = 0f; _vz = 0f; return; }
        float drop = speed * friction * dt;
        float newSpeed = speed - drop;
        if (newSpeed < 0f) newSpeed = 0f;
        float scale = newSpeed / speed;
        _vx *= scale; _vz *= scale;
    }

    private static float EnvF(string k, float d)
    {
        string v = System.Environment.GetEnvironmentVariable(k);
        float f;
        return v != null && float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f) ? f : d;
    }

}
