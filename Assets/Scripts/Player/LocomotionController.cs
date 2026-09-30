using Vortex;

// UNIFIED CHARACTER RIG DRIVER — drives ONE skinned tp_character rig from the shared PlayerRig state.
// The SAME script runs on BOTH rigs of the player:
//   * 3P world body   (FirstPerson = false, meshes on RenderLayer 2): stands upright at the feet, yaw-only,
//     spine bends to the aim, plays the full locomotion state machine + masked fire/reload overlays. This is
//     what OTHER cameras / players see you doing.
//   * FP arms viewmodel (FirstPerson = true, meshes on RenderLayer 1): the same rig, but driven PROCEDURALLY by
//     the ViewmodelRig helper (CoD-style): the weapon gets a camera-space pose (hip / ADS / sprint / reload,
//     sway, bob, recoil), the rig is placed so the animated right hand sits on the grip, the left hand is IK'd
//     onto the fore-grip / along the magazine path, and torso/legs/head are hidden so only the arms render.
//     It plays one shouldered hold clip (aim) for the finger/elbow pose; no first-person animation content needed.
//
// The weapon is glued to mixamorig:RightHand by the WeaponLoadout (bone attach); a TwoBoneIk component on each
// rig keeps the LEFT hand on the fore-grip (3P: auto-grip from the clip, released during the reload clip;
// FP: world-space IK target from the ViewmodelRig).
public class LocomotionController : VortexBehaviour
{
    const string A = "Assets/Models/Character/animations/";
    public float RunSpeed = 4.2f;
    public float Fade     = 0.18f;
    public string UpperMask = "mixamorig:Spine1+";

    // ---- placement ----
    public bool  FollowPlayer = true;    // false = a static showcase NPC (don't drive it)
    public bool  FirstPerson  = false;   // true = FP arms viewmodel (procedural, see ViewmodelRig.cs)

    // ---- 3P: upright at the feet, spine bends to aim ----
    public float  BodyYawOffset = 180f;  // the Mixamo rig's rest pose faces -Z in engine space: turn it to face where you look
    public float  SpineAimGain  = 1f;
    public float  SpineAimSign  = 1f;
    public string[] SpineBones  = new string[] { "mixamorig:Spine", "mixamorig:Spine1", "mixamorig:Spine2" };

    // ---- FP: the hold clip and the bones stripped away so only the arms + weapon render ----
    public string   FpHoldClip = "aim";
    public float    FpRigScale = 0.8f;   // extra scale on the FP rig (arm length vs. weapon distance; see ViewmodelRig.ShoulderAnchor)
    // whole limbs (bone + descendants): neck+head, both legs
    public string[] HideBones = new string[] { "mixamorig:Neck", "mixamorig:LeftUpLeg", "mixamorig:RightUpLeg" };
    // single bones (descendants stay): pelvis, spine, shoulders -> the arms hanging off them keep rendering
    public string[] HideBonesSelf = new string[] { "mixamorig:Hips", "mixamorig:Spine", "mixamorig:Spine1", "mixamorig:Spine2",
                                                   "mixamorig:LeftShoulder", "mixamorig:RightShoulder" };

    // ---- FP mesh look: hide helper meshes (the Beta rig's joint spheres) and tint the arm surface like gloves/sleeves ----
    public string[] HideMeshChildren = new string[] { "Beta_Joints" };
    public bool     TintArms = true;
    public Vector3  ArmTint = new Vector3(0.13f, 0.12f, 0.11f);

    // ---- reload (3P): release the support-hand IK so the animated hand reaches the mag well ----
    public string IkTipBone = "mixamorig:LeftHand";
    public float  ReloadTime = 2.4f;

    private bool   _demo;
    private string _base = "";
    private string _wantPending = "";
    private float  _wantTimer;
    private string _pendingLoop;
    private float  _transT;
    private bool   _firing, _reloading, _reloadPrev, _dead;
    private float  _fireT, _reloadT, _demoT;
    private ViewmodelRig _vm;
    private bool   _fpBonesHidden;

    public override void Start()
    {
        _demo = System.Environment.GetEnvironmentVariable("VM_ANIMDEMO") == "1";
        if (FirstPerson)
        {
            string envClip = System.Environment.GetEnvironmentVariable("VM_CLIP");      // dev tuning hooks
            if (envClip != null && envClip != "") FpHoldClip = envClip;
            string envScale = System.Environment.GetEnvironmentVariable("VM_SCALE");
            float sc;
            if (envScale != null && float.TryParse(envScale, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out sc) && sc > 0.01f) FpRigScale = sc;
            if (FpRigScale > 0.01f && System.Math.Abs(FpRigScale - 1f) > 0.001f) Scale = new Vector3(Scale.X * FpRigScale, Scale.Y * FpRigScale, Scale.Z * FpRigScale);
            string envHide = System.Environment.GetEnvironmentVariable("VM_HIDESELF");
            if (envHide != null && envHide != "") HideBonesSelf = envHide.Split(',');
            string envJoints = System.Environment.GetEnvironmentVariable("VM_HIDEJOINTS");
            if (envJoints == "1") HideMeshChildren = new string[] { "Beta_Joints" };
            string envColor = System.Environment.GetEnvironmentVariable("VM_ARMCOLOR");
            if (envColor != null && envColor != "") { string[] c = envColor.Split(','); float cr, cg, cb; if (c.Length >= 3 && float.TryParse(c[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out cr) && float.TryParse(c[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out cg) && float.TryParse(c[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out cb)) { ArmTint = new Vector3(cr, cg, cb); TintArms = true; } }
            ApplyFpMeshLook();
            _vm = new ViewmodelRig();
            _vm.Start(EntityId);
            PlayAnimation(A + FpHoldClip + ".vanim", 0f); _base = FpHoldClip;
        }
        else { PlayAnimation(A + "rifle_idle.vanim", 0f); _base = "rifle_idle"; }
    }

    public override void Update(float dt)
    {
        if (FirstPerson) return;   // the FP arms are posed procedurally in LateUpdate (no state machine)

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
        if (airborne)                                       want = fwd < -0.3f ? "jump_back" : "jump";
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

    // Placed AFTER Update so the camera pitch is final. FP: hand the rig to the procedural viewmodel.
    // 3P: stand upright at the feet (yaw only) and bend the spine to the aim.
    public override void LateUpdate(float dt)
    {
        if (_demo || !FollowPlayer || !PlayerRig.Ready) return;

        if (FirstPerson)
        {
            if (!_fpBonesHidden) { _fpBonesHidden = true; _vm.HideBodyBones(HideBones, HideBonesSelf); }
            if (PlayerRig.Inspect) { _vm.Park(); return; }
            _vm.LateUpdate(dt);
            return;
        }

        if (PlayerRig.Inspect) return;   // 3P body: leave where it is

        // ---- 3P: upright at the feet, yaw only; spine bends to the aim ----
        SetWorldPose(PlayerRig.FootPos, new Vector3(0f, PlayerRig.BodyYaw + BodyYawOffset, 0f));
        int n = SpineBones != null ? SpineBones.Length : 0;
        float per = n > 0 ? (PlayerRig.AimPitch * SpineAimGain * SpineAimSign / n) : 0f;
        for (int i = 0; i < n; i++)
            if (SpineBones[i] != null && SpineBones[i] != "")
                SetBoneAdditiveRotation(SpineBones[i], new Vector3(per, 0f, 0f));
    }

    // Walk the rig's mesh children: deactivate helper meshes, tint the remaining surfaces.
    private void ApplyFpMeshLook()
    {
        long[] kids = Scene.Children(EntityId);
        for (int i = 0; kids != null && i < kids.Length; i++) ApplyFpMeshLookTo(kids[i]);
    }
    private void ApplyFpMeshLookTo(long e)
    {
        string n = Scene.NameOf(e);
        bool hide = false;
        for (int i = 0; HideMeshChildren != null && i < HideMeshChildren.Length; i++) if (HideMeshChildren[i] == n) hide = true;
        if (hide) { Scene.SetActive(e, false); return; }
        if (TintArms) Scene.SetColorOf(e, ArmTint.X, ArmTint.Y, ArmTint.Z);
        long[] kids = Scene.Children(e);
        for (int i = 0; kids != null && i < kids.Length; i++) ApplyFpMeshLookTo(kids[i]);
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
}
