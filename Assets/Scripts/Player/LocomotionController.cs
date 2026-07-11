using Vortex;

// UNIFIED CHARACTER RIG DRIVER — drives ONE skinned tp_character rig from the shared PlayerRig state.
// The SAME script runs on BOTH rigs of the player:
//   * 3P world body   (FirstPerson = false, meshes on RenderLayer 2): stands upright at the feet, yaw-only,
//     spine bends to the aim. This is what OTHER cameras / players see you doing.
//   * FP arms viewmodel (FirstPerson = true, meshes on RenderLayer 1): the same rig, camera-locked, with the
//     head/neck/legs hidden so only the arms + weapon show (CoD-style). Pitches with the view as one unit.
// Because both read the same PlayerRig and play the SAME clips, the weapon stays gripped by BOTH hands and the
// reload (hand pulls the mag) is IDENTICAL in first- and third-person — no separate animation content.
//
// The weapon is glued to mixamorig:RightHand by a BoneAttachment socket; a TwoBoneIk keeps the LEFT hand on the
// fore-grip (autoGrip). During reload the support-hand IK is released so the animated hand can reach the mag well.
public class LocomotionController : VortexBehaviour
{
    const string A = "Assets/Models/Character/animations/";
    public float RunSpeed = 4.2f;
    public float Fade     = 0.18f;
    public string UpperMask = "mixamorig:Spine1+";

    // ---- placement ----
    public bool  FollowPlayer = true;    // false = a static showcase NPC (don't drive it)
    public bool  FirstPerson  = false;   // true = FP arms viewmodel (camera-locked, non-arm bones hidden)

    // ---- 3P: upright at the feet, spine bends to aim ----
    public float  SpineAimGain  = 1f;
    public float  SpineAimSign  = 1f;
    public string[] SpineBones  = new string[] { "mixamorig:Spine", "mixamorig:Spine1", "mixamorig:Spine2" };

    // ---- FP: camera-locked placement (metres, camera-local). Tuned live via VM_FP_* env during dev. ----
    public float FpEyeHeight = 1.58f;    // camera sits at the rig's (hidden) head — arms/weapon read naturally
    public float FpOffFwd    = 0.0f;     // forward from the eye
    public float FpOffRight  = -0.06f;   // right (slightly left so the shouldered gun reads centred-right)
    public float FpOffUp     = 0.05f;    // extra vertical trim (gun into the lower third, CoD-style)
    public float FpPitchSign = 1f;       // flip if the arms pitch the wrong way

    // ---- FP ADS: shift the WHOLE rig so the weapon's sight line meets the camera centre (CoD-style).
    // Blended in/out smoothly; tune with VM_FP_ADS* env captures. ----
    public float AdsShiftRight = -0.095f;
    public float AdsShiftUp    = 0.10f;
    public float AdsShiftFwd   = 0.0f;
    public float AdsBlendSpeed = 10f;
    private float _adsBlend;
    // bones collapsed to nothing in FP so ONLY the arms + weapon render (the hips/spine stay, positioned behind the eye)
    public string[] HideBones = new string[] { "mixamorig:Head", "mixamorig:HeadTop_End", "mixamorig:Neck",
                                               "mixamorig:LeftUpLeg", "mixamorig:RightUpLeg" };

    // ---- reload: release the support-hand IK so the animated hand reaches the mag well ----
    public string IkTipBone = "mixamorig:LeftHand";
    public float  ReloadTime = 2.4f;

    private bool   _demo, _fpHidden;
    private string _base = "";
    private string _wantPending = "";
    private float  _wantTimer;
    private string _pendingLoop;
    private float  _transT;
    private bool   _firing, _reloading, _reloadPrev, _dead;
    private float  _fireT, _reloadT, _demoT;

    public override void Start()
    {
        _demo = System.Environment.GetEnvironmentVariable("VM_ANIMDEMO") == "1";
        if (FirstPerson)
        {
            // live dev tuning of the FP viewmodel placement
            FpEyeHeight = EnvF("VM_FP_EYEH", FpEyeHeight);
            FpOffFwd    = EnvF("VM_FP_FWD",  FpOffFwd);
            FpOffRight  = EnvF("VM_FP_RIGHT",FpOffRight);
            FpOffUp     = EnvF("VM_FP_UP",   FpOffUp);
            FpPitchSign = EnvF("VM_FP_PSIGN",FpPitchSign);
            AdsShiftRight = EnvF("VM_FP_ADSR", AdsShiftRight);
            AdsShiftUp    = EnvF("VM_FP_ADSU", AdsShiftUp);
            AdsShiftFwd   = EnvF("VM_FP_ADSF", AdsShiftFwd);
        }
        if (FirstPerson) { PlayAnimation(A + "aim.vanim", 0f); _base = "aim"; }
        else { PlayAnimation(A + "rifle_idle.vanim", 0f); _base = "rifle_idle"; }
    }

    public override void Update(float dt)
    {
        float speed = 0f, fwd = 0f, right = 0f; bool ads = false, airborne = false, fireBtn = false, reloadBtn = false, dead = false;
        if (_demo) DemoInputs(dt, ref speed, ref fwd, ref right, ref ads, ref airborne, ref fireBtn, ref reloadBtn);
        else
        {
            if (!PlayerRig.Ready) return;
            speed = PlayerRig.Speed; fwd = PlayerRig.MoveForwardN; right = PlayerRig.MoveRightN;
            ads = PlayerRig.Ads; airborne = PlayerRig.IsAirborne;
            fireBtn = PlayerRig.Firing; reloadBtn = PlayerRig.Reloading;
            dead = PlayerRig.Health <= 0f;
        }

        if (dead)
        {
            if (!_dead) { _dead = true; _firing = false; _reloading = false; StopAnimationLayer(1); StopAnimationLayer(2);
                          PlayAnimation(A + "death.vanim", 0.2f); _base = "death"; _pendingLoop = null; _transT = 0f; }
            return;
        }
        if (_dead) { _dead = false; PlayAnimation(A + "rifle_idle.vanim", Fade); _base = "rifle_idle"; }

        if (_transT > 0f)
        {
            _transT -= dt;
            if (_transT <= 0f && _pendingLoop != null) { PlayAnimation(A + _pendingLoop + ".vanim", Fade); _base = _pendingLoop; _pendingLoop = null; }
            Overlays(fireBtn, reloadBtn, dt);
            return;
        }

        bool wasRun = _base == "run" || _base == "run_back";
        float runCut = wasRun ? RunSpeed - 0.6f : RunSpeed + 0.6f;
        string want;
        if (FirstPerson)
        {
            // FP viewmodel: the weapon must ALWAYS be shouldered and IN FRAME (CoD-style) — the 3P
            // low-ready/locomotion poses hang the arms below the camera. aim = shouldered idle;
            // rifle_run adds the movement sway while staying shouldered.
            if (speed < 0.25f || ads)  want = "aim";
            else                        want = "rifle_run";
        }
        else if (airborne)                                  want = fwd < -0.3f ? "jump_back" : "jump";
        else if (speed < 0.25f)                             want = ads ? "aim" : "rifle_idle";
        else if (System.Math.Abs(right) > System.Math.Abs(fwd) + 0.35f) want = right > 0f ? "strafe_r" : "strafe_l";
        else if (fwd < -0.3f)                               want = speed > runCut ? "run_back" : "walk_back";
        else                                                want = speed > runCut ? "run" : "walk";

        if (want != _base)
        {
            if (want != _wantPending) { _wantPending = want; _wantTimer = 0f; }
            _wantTimer += dt;
            float dwell = (want == "rifle_idle" || want == "aim" || _base == "rifle_idle" || _base == "aim") ? 0.05f : 0.14f;
            if (_wantTimer >= dwell) { SetBase(want); _wantTimer = 0f; }
        }
        else { _wantPending = want; _wantTimer = 0f; }

        Overlays(fireBtn, reloadBtn, dt);
    }

    // Placed AFTER Update so the camera pitch is final. FP: lock to the camera and pitch as one unit, hiding the
    // non-arm bones. 3P: stand upright at the feet (yaw only) and bend the spine to the aim.
    public override void LateUpdate(float dt)
    {
        if (_demo || !FollowPlayer || !PlayerRig.Ready) return;

        if (PlayerRig.Inspect)
        {
            if (FirstPerson) SetWorldPose(new Vector3(0f, -1000f, 0f), Vector3.Zero);  // hide the FP arms during orbit-inspect
            return;                                                                     // 3P body: leave where it is
        }

        if (FirstPerson)
        {
            // hide head/neck/legs ONCE so only the arms + weapon render
            if (!_fpHidden)
            {
                int hn = HideBones != null ? HideBones.Length : 0;
                for (int i = 0; i < hn; i++) if (HideBones[i] != null && HideBones[i] != "") SetBoneScaleOverride(HideBones[i], 0f);
                _fpHidden = true;
            }

            float yawR = PlayerRig.Yaw * 0.0174532925f;
            float pitR = PlayerRig.Pitch * 0.0174532925f;
            float cy = (float)System.Math.Cos(yawR), sy = (float)System.Math.Sin(yawR);
            float cp = (float)System.Math.Cos(pitR), sp = (float)System.Math.Sin(pitR);
            Vector3 f = new Vector3(sy * cp, -sp, cy * cp);
            Vector3 r = new Vector3(cy, 0f, -sy);
            Vector3 u = PlayerRig.ViewUp(f, r);
            Vector3 e = PlayerRig.EyePos;

            // ADS: shift the whole rig so the sight line meets the camera centre (smoothly blended)
            float adsT = PlayerRig.Ads ? 1f : 0f;
            _adsBlend += (adsT - _adsBlend) * System.Math.Min(1f, AdsBlendSpeed * dt);
            float offR = FpOffRight + AdsShiftRight * _adsBlend;
            float offU = FpOffUp + AdsShiftUp * _adsBlend;
            float offF = FpOffFwd + AdsShiftFwd * _adsBlend;

            Vector3 pos = new Vector3(
                e.X - u.X * FpEyeHeight + f.X * offF + r.X * offR + u.X * offU,
                e.Y - u.Y * FpEyeHeight + f.Y * offF + r.Y * offR + u.Y * offU,
                e.Z - u.Z * FpEyeHeight + f.Z * offF + r.Z * offR + u.Z * offU);
            SetWorldPose(pos, new Vector3(PlayerRig.Pitch * FpPitchSign, PlayerRig.Yaw, 0f));
            return;
        }

        // ---- 3P: upright at the feet, yaw only; spine bends to the aim ----
        SetWorldPose(PlayerRig.FootPos, new Vector3(0f, PlayerRig.BodyYaw, 0f));
        int n = SpineBones != null ? SpineBones.Length : 0;
        float per = n > 0 ? (PlayerRig.AimPitch * SpineAimGain * SpineAimSign / n) : 0f;
        for (int i = 0; i < n; i++)
            if (SpineBones[i] != null && SpineBones[i] != "")
                SetBoneAdditiveRotation(SpineBones[i], new Vector3(per, 0f, 0f));
    }

    private void Overlays(bool fireBtn, bool reloadBtn, float dt)
    {
        if (fireBtn && !_reloading) { _firing = true; _fireT = 0.28f; PlayAnimationLayered(A + "fire.vanim", 1, UpperMask, 1f, 0.04f); }
        if (_firing) { _fireT -= dt; if (_fireT <= 0f) { _firing = false; StopAnimationLayer(1); } }

        if (reloadBtn && !_reloadPrev && !_reloading)
        {
            _reloading = true; _reloadT = ReloadTime; _firing = false; StopAnimationLayer(1);
            PlayAnimationLayered(A + "rifle_reload.vanim", 2, UpperMask, 1f, 0.15f);
            SetIkWeight(IkTipBone, 0f);    // release the support hand so it can reach for the mag
        }
        _reloadPrev = reloadBtn;
        if (_reloading)
        {
            _reloadT -= dt;
            if (_reloadT <= 0f) { _reloading = false; StopAnimationLayer(2); SetIkWeight(IkTipBone, 1f); }  // support hand back on the fore-grip
        }
    }

    private void SetBase(string clip)
    {
        if (clip == _base) return;
        string trans = TransitionClip(_base, clip);
        if (trans != null) { PlayAnimation(A + trans + ".vanim", Fade); _base = clip; _pendingLoop = clip; _transT = 0.33f; return; }
        PlayAnimation(A + clip + ".vanim", Fade); _base = clip; _pendingLoop = null; _transT = 0f;
    }

    private static string TransitionClip(string from, string to)
    {
        bool fromIdle = from == "rifle_idle" || from == "aim" || from == "";
        bool toIdle   = to == "rifle_idle"   || to == "aim";
        if (fromIdle && (to == "walk"      || to == "run"))      return "walk_start";
        if (fromIdle && (to == "walk_back" || to == "run_back")) return "walk_back_start";
        if (toIdle   && (from == "walk"      || from == "run"))      return "walk_stop";
        if (toIdle   && (from == "walk_back" || from == "run_back")) return "walk_back_stop";
        return null;
    }

    private void DemoInputs(float dt, ref float speed, ref float fwd, ref float right,
                            ref bool ads, ref bool airborne, ref bool fire, ref bool reload)
    {
        _demoT += dt;
        const float step = 2.6f; const int n = 9;
        int s = (int)(_demoT / step) % n;
        float within = _demoT - (int)(_demoT / step) * step;
        bool edge = within < 0.08f;
        switch (s)
        {
            case 0: break;
            case 1: speed = 3f; fwd = 1f; break;
            case 2: speed = 6f; fwd = 1f; break;
            case 3: speed = 3f; right = 1f; break;
            case 4: speed = 3f; right = -1f; break;
            case 5: speed = 3f; fwd = -1f; break;
            case 6: ads = true; break;
            case 7: ads = true; fire = edge; break;
            case 8: reload = edge; break;
        }
    }

    private static float EnvF(string k, float d)
    {
        string v = System.Environment.GetEnvironmentVariable(k);
        float f;
        return v != null && float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f) ? f : d;
    }
}
