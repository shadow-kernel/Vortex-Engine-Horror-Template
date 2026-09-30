using Vortex;

// PROCEDURAL FIRST-PERSON VIEWMODEL (CoD-style) — a plain helper class owned by the LocomotionController of the
// FP_Arms rig (FirstPerson = true). It does NOT play first-person animation clips; instead it computes the weapon's
// pose in CAMERA space every frame (hip / ADS / sprint / reload poses, look sway, walk bob, breathing, recoil
// spring), pins the rig's (hidden) shoulders to a fixed camera-space anchor below the eye and IKs BOTH hands onto
// the weapon: the RIGHT hand takes the grip pose (the weapon is bone-attached to that hand, so arms + gun move as
// one), the LEFT hand is pulled onto the fore-grip (or along the magazine path during a reload). Two TwoBoneIk
// components on the rig entity (tips mixamorig:RightHand / mixamorig:LeftHand) provide the chains.
// Torso, shoulders, neck, head and legs are collapsed with Animation.SetBoneHidden so only the arms render.
//
// Everything is driven from PlayerRig (camera pose, speed, ADS, sprint, reload progress, per-shot kicks) and from
// the active Weapon's viewmodel fields (poses, sight point, grip points) — tune those on the weapon PREFAB.
public class ViewmodelRig
{
    public const string HandBone    = "mixamorig:RightHand";
    public const string SupportBone = "mixamorig:LeftHand";

    // ---- feel constants (per project; the per-weapon poses live on the Weapon prefab) ----
    public float SwayAmount   = 0.055f;  // degrees of weapon lag per pixel of mouse movement
    public float SwayMax      = 3.2f;
    public float SwaySpeed    = 11f;
    public float BobAmount    = 0.0075f; // metres at sprint speed
    public float BobFrequency = 1.75f;   // stride cycles per metre of travel (x2 for the vertical bounce)
    public float RecoilStiffness = 200f, RecoilDamping = 17f;   // spring that pulls the gun back after a kick

    // ---- arm geometry (CoD-style): the hidden SHOULDERS are pinned to a fixed camera-space anchor below and a touch
    // behind the eye, so the upper arms always enter the frame from the bottom corners and never cross the view. Both
    // hands are then IK'd onto the weapon (right: grip incl. rotation, left: fore-grip / mag path); when a grip is out
    // of reach the whole rig slides toward it instead of stretching an arm. ----
    public Vector3 ShoulderAnchor = new Vector3(0f, -0.21f, -0.03f);   // shoulder mid-point in camera space (m): X right, Y up, Z forward
    public float RigPitch      = 0f;      // extra pitch of the arms rig around the shoulders (deg; positive = shoulders roll forward)
    public float RigYaw        = 180f;    // the Mixamo rig's rest pose faces -Z in engine space: turn it to face along the camera
    public float ReachFraction = 0.96f;   // never extend an arm beyond this fraction of its length (keeps the elbows bent)
    public float ElbowPole      = 50f;     // support-arm pole angle (deg): swing the elbow around shoulder->hand (out of the view)
    public float AdsElbowPole   = 45f;     // … while aiming
    public float ElbowPoleRight = 40f;     // weapon-arm pole angle (deg)
    private int _lastPole = int.MinValue, _lastPoleR = int.MinValue;

    // ---- hand grips: the support hand is ORIENTED onto the fore-grip (palm against the handguard, fingers across
    // it, thumb forward). The FINGERS close through HandPose components on the rig entity (one per hand, edited in
    // the inspector) — no finger code here. ----
    public bool  OrientSupportHand = true;
    public bool  SupportPalmFlip = false;    // flip if the back of the hand faces the handguard on your rig
    private long _gripWeapon;                // weapon entity the auto-grip was computed for

    private long _rig;
    private bool _bonesHidden, _envApplied;
    private float _adsT, _sprintT, _reloadT, _bobBlend, _bobPhase, _time;
    private float _swayYaw, _swayPitch;
    private float _kBack, _kBackV, _kPitch, _kPitchV, _kYaw, _kYawV, _kRoll, _kRollV;
    private long _magEnt; private long _magWeapon; private Vector3 _magRest, _magRestRot;
    private bool _slapDone;
    private bool _debug = System.Environment.GetEnvironmentVariable("VM_DEBUG") == "1";
    private Vector3 _lf, _ln;   // support hand: finger direction + palm normal in HAND-BONE space (measured from the pose)
    private int _dbgFrame;

    public void Start(long rig) { _rig = rig; }

    public void HideBodyBones(string[] hideSubtrees, string[] hideSelf)
    {
        if (_bonesHidden) return;
        _bonesHidden = true;
        for (int i = 0; hideSubtrees != null && i < hideSubtrees.Length; i++)
            if (hideSubtrees[i] != null && hideSubtrees[i] != "") Animation.SetBoneHidden(_rig, hideSubtrees[i], true, true);
        for (int i = 0; hideSelf != null && i < hideSelf.Length; i++)
            if (hideSelf[i] != null && hideSelf[i] != "") Animation.SetBoneHidden(_rig, hideSelf[i], true, false);
    }

    /// <summary>Park the rig out of sight (no weapon, inspect mode).</summary>
    public void Park()
    {
        Scene.SetWorldPose(_rig, new Vector3(0f, -1000f, 0f), Vector3.Zero);
        Animation.ClearIkTarget(_rig, SupportBone);
        Animation.ClearIkTarget(_rig, HandBone);
    }

    public void LateUpdate(float dt)
    {
        Weapon w = PlayerRig.ActiveWeapon;
        if (w == null || !PlayerRig.Ready || PlayerRig.FpWeaponEntity == 0) { Park(); return; }
        if (dt > 0.1f) dt = 0.1f;
        _time += dt;
        if (!_envApplied) { _envApplied = true; w.ResolveSockets(); ApplyEnvOverrides(w); }

        // ---------------- state blends ----------------
        float reloadP = PlayerRig.ReloadProgress;
        bool reloading = reloadP >= 0f;
        bool ads = PlayerRig.Ads && !reloading;
        bool sprint = (PlayerRig.IsSprinting && PlayerRig.Speed > 3.0f || PlayerRig.Mantling) && !ads && !reloading;   // gun down while sprinting / climbing
        _adsT    = Approach(_adsT,    ads ? 1f : 0f,       w.AdsSpeed * dt);
        _sprintT = Approach(_sprintT, sprint ? 1f : 0f,    8f * dt);
        _reloadT = Approach(_reloadT, reloading ? 1f : 0f, 9f * dt);
        float a = Smooth(_adsT), sp = Smooth(_sprintT) * (1f - a), rl = Smooth(_reloadT) * (1f - a);
        int pole = (int)System.Math.Round(ElbowPole + (AdsElbowPole - ElbowPole) * a);
        if (pole != _lastPole) { _lastPole = pole; Animation.SetIkPoleAngle(_rig, SupportBone, pole); }
        int poleR = (int)System.Math.Round(ElbowPoleRight);
        if (poleR != _lastPoleR) { _lastPoleR = poleR; Animation.SetIkPoleAngle(_rig, HandBone, poleR); }

        // ---------------- base pose (camera space: X right, Y up, Z forward) ----------------
        Vector3 hipP = w.HipPosition;                       Quaternion hipQ = Quaternion.FromEuler(w.HipRotation);
        Vector3 adsP = new Vector3(-w.SightOffset.X, -w.SightOffset.Y, w.AdsDistance - w.SightOffset.Z);
        Vector3 sprP = w.SprintPosition;                    Quaternion sprQ = Quaternion.FromEuler(w.SprintRotation);
        Vector3 relP = hipP + w.ReloadShift;                Quaternion relQ = hipQ * Quaternion.FromEuler(w.ReloadTilt);
        Vector3 p = Vector3.Lerp(hipP, adsP, a);            Quaternion q = Quaternion.Slerp(hipQ, Quaternion.Identity, a);
        p = Vector3.Lerp(p, sprP, sp);                      q = Quaternion.Slerp(q, sprQ, sp);
        p = Vector3.Lerp(p, relP, rl);                      q = Quaternion.Slerp(q, relQ, rl);
        // weapon swap: the gun drops out of the bottom of the frame and the next one rises back in
        float lower = Smooth(PlayerRig.SwitchLower);
        if (lower > 0.0005f)
        {
            p = p + new Vector3(0.03f, -0.30f, -0.05f) * lower;
            q = Quaternion.Slerp(q, q * Quaternion.FromEuler(40f, -8f, 24f), lower);
        }

        // ---------------- look sway (the gun lags the view a touch, then settles) ----------------
        float swayScale = 1f - 0.75f * a;
        float ty = Clamp(-Input.MouseDeltaX * SwayAmount, -SwayMax, SwayMax) * swayScale;
        float tp = Clamp(-Input.MouseDeltaY * SwayAmount, -SwayMax, SwayMax) * swayScale;
        _swayYaw   = Approach(_swayYaw, ty, SwaySpeed * dt);
        _swayPitch = Approach(_swayPitch, tp, SwaySpeed * dt);
        float sx = _swayYaw * 0.0022f, sy = -_swayPitch * 0.0022f;

        // ---------------- walk bob + breathing ----------------
        float speed = PlayerRig.Speed;
        float moveN = speed / 6.3f; if (moveN > 1f) moveN = 1f;
        _bobBlend = Approach(_bobBlend, PlayerRig.Grounded ? moveN : 0f, 6f * dt);
        _bobPhase += speed * BobFrequency * dt * 3.14159f;
        float amp = BobAmount * _bobBlend * (1f - 0.7f * a) * (1f + 0.6f * sp);
        float bx = (float)System.Math.Sin(_bobPhase) * amp;
        float by = (float)System.Math.Sin(_bobPhase * 2f) * amp * 0.55f - amp * 0.25f;
        float bRoll  = (float)System.Math.Sin(_bobPhase) * 1.1f * _bobBlend * (1f - a);
        float bPitch = (float)System.Math.Sin(_bobPhase * 2f) * 0.5f * _bobBlend * (1f - a);
        float brY = (float)System.Math.Sin(_time * 1.3f) * 0.0012f * (1f - 0.5f * a);
        float brPitch = (float)System.Math.Sin(_time * 1.1f) * 0.15f * (1f - 0.6f * a);

        // ---------------- recoil spring (per-shot velocity impulses from Weapon.Fire) ----------------
        _kBackV  += PlayerRig.WeaponKickBack;  PlayerRig.WeaponKickBack = 0f;
        _kPitchV -= PlayerRig.WeaponKickPitch; PlayerRig.WeaponKickPitch = 0f;   // muzzle UP = negative pitch
        _kYawV   += PlayerRig.WeaponKickYaw;   PlayerRig.WeaponKickYaw = 0f;
        _kRollV  += PlayerRig.WeaponKickRoll;  PlayerRig.WeaponKickRoll = 0f;
        Spring(ref _kBack, ref _kBackV, dt); Spring(ref _kPitch, ref _kPitchV, dt);
        Spring(ref _kYaw, ref _kYawV, dt);   Spring(ref _kRoll, ref _kRollV, dt);

        // ---------------- compose the weapon pose in world space ----------------
        Vector3 localP = p + new Vector3(sx + bx, sy + by + brY, -_kBack);
        Quaternion localQ = q * Quaternion.FromEuler(_swayPitch + bPitch + brPitch + _kPitch, _swayYaw + _kYaw, bRoll + _kRoll);
        Quaternion camQ = Quaternion.FromEuler(PlayerRig.Pitch, PlayerRig.Yaw, PlayerRig.Roll);
        Vector3 e = PlayerRig.EyePos;
        Vector3 wp = e + camQ.Rotate(localP);
        Quaternion wq = camQ * localQ;

        // ---------------- hand targets ----------------
        // weapon = hand * Grip  =>  hand = weapon * Grip^-1  (the weapon is bone-attached to the right hand)
        Quaternion gq = Quaternion.FromEuler(w.GripRotation);
        Quaternion tq = wq * gq.Inverse;
        Vector3 tp2 = wp - tq.Rotate(w.GripOffset);
        Vector3 handLocal = w.SupportHandOffset;
        Vector3 magOff = Vector3.Zero;
        if (reloading) ReloadPath(w, reloadP, ref handLocal, ref magOff);
        else _slapDone = false;
        Vector3 supportTarget = wp + wq.Rotate(handLocal);

        // ---------------- place the rig: shoulders on the anchor, then slide until both grips are in reach ----------------
        Vector3 r0p, r0e;
        if (!Scene.TryGetWorldPose(_rig, out r0p, out r0e)) return;
        Quaternion r0i = Quaternion.FromEuler(r0e).Inverse;
        Vector3 lsW = Animation.BonePosition(_rig, "mixamorig:LeftArm"),     rsW = Animation.BonePosition(_rig, "mixamorig:RightArm");
        Vector3 lfW = Animation.BonePosition(_rig, "mixamorig:LeftForeArm"), rfW = Animation.BonePosition(_rig, "mixamorig:RightForeArm");
        Vector3 lhW = Animation.BonePosition(_rig, SupportBone),             rhW = Animation.BonePosition(_rig, HandBone);
        float reachL = ((lfW - lsW).Length + (lhW - lfW).Length) * ReachFraction;
        float reachR = ((rfW - rsW).Length + (rhW - rfW).Length) * ReachFraction;
        Vector3 lsL = r0i.Rotate(lsW - r0p), rsL = r0i.Rotate(rsW - r0p);   // shoulder joints in rig space (pose only)
        Vector3 midL = (lsL + rsL) * 0.5f;
        Quaternion r1q = camQ * Quaternion.FromEuler(RigPitch, RigYaw, 0f);
        Vector3 r1p = e + camQ.Rotate(ShoulderAnchor) - r1q.Rotate(midL);
        Vector3 lsN = r1p + r1q.Rotate(lsL), rsN = r1p + r1q.Rotate(rsL);
        if (reachL > 0.05f && reachR > 0.05f)
        {
            for (int it = 0; it < 4; it++)
            {
                Vector3 dR = tp2 - rsN, dL = supportTarget - lsN;
                float distR = dR.Length, distL = dL.Length;
                float defR = distR - reachR, defL = distL - reachL;
                if (defR <= 0.0005f && defL <= 0.0005f) break;
                Vector3 shift = (defR >= defL) ? dR * (defR / distR) : dL * (defL / distL);
                r1p = r1p + shift; lsN = lsN + shift; rsN = rsN + shift;
            }
        }
        Scene.SetWorldPose(_rig, r1p, r1q.ToEuler());

        // ---------------- auto-grip: derive the weapon-hand socket from the rig's real hand geometry ----------------
        if (w.AutoGrip && _gripWeapon != PlayerRig.FpWeaponEntity)
        {
            Vector3 rf, rn;
            if (TryGetHandFrame(HandBone, "mixamorig:RightHandMiddle1", "mixamorig:RightHandIndex1", "mixamorig:RightHandPinky1", false, out rf, out rn))
            {
                _gripWeapon = PlayerRig.FpWeaponEntity;
                Vector3 fws = w.GripFingerDir.Normalized;
                Vector3 nws = Ortho(w.GripPalmDir, fws);
                Quaternion handInWeapon = Quaternion.LookRotation(fws, nws) * Quaternion.LookRotation(rf, rn).Inverse;
                Quaternion gripQ = handInWeapon.Inverse;                 // weapon = hand * grip
                w.GripOffset = gripQ.Rotate(-w.GripHandPos);             // 0 = handPos + hand.Rotate(gripOffset)
                w.GripRotation = gripQ.ToEuler();
                if (WeaponLoadout.Instance != null) WeaponLoadout.Instance.Reattach(w);
                else Animation.Attach(PlayerRig.FpWeaponEntity, _rig, HandBone, w.GripOffset, w.GripRotation);
                if (_debug) Debug.Log("[VM] auto-grip: hand frame f=" + rf + " n=" + rn + " -> GripOffset=" + w.GripOffset + " GripRotation=" + w.GripRotation);
                gq = Quaternion.FromEuler(w.GripRotation); tq = wq * gq.Inverse; tp2 = wp - tq.Rotate(w.GripOffset);
            }
        }

        // ---------------- both hands onto the weapon ----------------
        Animation.SetIkTarget(_rig, HandBone, tp2, tq.ToEuler());
        if (OrientSupportHand && !reloading && TryGetHandFrame(SupportBone, "mixamorig:LeftHandMiddle1", "mixamorig:LeftHandIndex1", "mixamorig:LeftHandPinky1", true, out _lf, out _ln))
        {
            // desired frame in weapon space: fingers across the handguard (curling up), palm against the handguard
            Vector3 fw = wq.Rotate(w.SupportFingerDir.Normalized);
            Vector3 nw = wq.Rotate(Ortho(w.SupportPalmDir, w.SupportFingerDir.Normalized));
            Quaternion want = Quaternion.LookRotation(fw, nw) * Quaternion.LookRotation(_lf, SupportPalmFlip ? -_ln : _ln).Inverse;
            Animation.SetIkTarget(_rig, SupportBone, supportTarget, want.ToEuler());
        }
        else Animation.SetIkTarget(_rig, SupportBone, supportTarget);
        DriveMag(w, magOff);

        if (_debug && (++_dbgFrame % 90) == 0)
        {
            Quaternion ci = camQ.Inverse;
            Vector3 gp, ge; Scene.TryGetWorldPose(PlayerRig.FpWeaponEntity, out gp, out ge);
            Debug.Log("[VM] eye=" + e + " yaw=" + PlayerRig.Yaw + " pitch=" + PlayerRig.Pitch + " weaponWant=" + wp + " weaponIs=" + gp
                + " | reachL=" + reachL + " reachR=" + reachR + " rig=" + r1p
                + " | cam-space: RArm=" + ci.Rotate(rsN - e) + " RFore=" + ci.Rotate(Animation.BonePosition(_rig, "mixamorig:RightForeArm") - e) + " RHand=" + ci.Rotate(Animation.BonePosition(_rig, HandBone) - e) + " gripWant=" + ci.Rotate(tp2 - e)
                + " | LArm=" + ci.Rotate(lsN - e) + " LFore=" + ci.Rotate(Animation.BonePosition(_rig, "mixamorig:LeftForeArm") - e) + " LHand=" + ci.Rotate(Animation.BonePosition(_rig, SupportBone) - e) + " supportWant=" + ci.Rotate(supportTarget - e)
                + " | LHand frame local f=" + _lf + " n=" + _ln);
            Vector3 rf, rn;
            if (TryGetHandFrame(HandBone, "mixamorig:RightHandMiddle1", "mixamorig:RightHandIndex1", "mixamorig:RightHandPinky1", false, out rf, out rn))
            {
                Vector3 hp, he; Animation.TryGetBoneTransform(_rig, HandBone, out hp, out he); Quaternion hq = Quaternion.FromEuler(he);
                Quaternion wi = wq.Inverse;
                Debug.Log("[VM] RHand frame local f=" + rf + " n=" + rn + " | in WEAPON space: fingers=" + wi.Rotate(hq.Rotate(rf)) + " palm=" + wi.Rotate(hq.Rotate(rn))
                    + " | LHand in weapon space: fingers=" + wi.Rotate(Quaternion.FromEuler(GetRot(SupportBone)).Rotate(_lf)) + " palm=" + wi.Rotate(Quaternion.FromEuler(GetRot(SupportBone)).Rotate(_ln)));
            }
        }

        Camera.SetViewmodelFieldOfView(w.ViewmodelFov);
    }

    // Reload choreography (progress 0..1 over Firearm.ReloadTime): grab the mag, pull it out, drop it out of
    // view, bring the fresh one up, seat it, slap it home, back to the fore-grip. Positions are weapon-local.
    private void ReloadPath(Weapon w, float p, ref Vector3 hand, ref Vector3 mag)
    {
        Vector3 support = w.SupportHandOffset;
        Vector3 grab = w.MagWellOffset + new Vector3(0f, -0.085f, 0f);       // hand around the magazine body
        Vector3 outOff = new Vector3(0f, -0.17f, -0.02f);                     // clear of the well
        Vector3 downOff = new Vector3(-0.06f, -0.42f, -0.12f);                // below the frame
        if (p < 0.15f)      { hand = Vector3.Lerp(support, grab, Smooth(p / 0.15f)); }
        else if (p < 0.32f) { float t = Smooth((p - 0.15f) / 0.17f); mag = outOff * t; hand = grab + mag; }
        else if (p < 0.50f) { float t = Smooth((p - 0.32f) / 0.18f); mag = Vector3.Lerp(outOff, downOff, t); hand = grab + mag; }
        else if (p < 0.70f) { float t = Smooth((p - 0.50f) / 0.20f); mag = Vector3.Lerp(downOff, outOff, t); hand = grab + mag; }
        else if (p < 0.80f) { float t = Smooth((p - 0.70f) / 0.10f); mag = outOff * (1f - t); hand = grab + mag; }
        else if (p < 0.90f)
        {
            float t = (p - 0.80f) / 0.10f;
            float slap = (float)System.Math.Sin(t * 3.14159f) * 0.035f;
            hand = grab + new Vector3(0f, -0.04f + slap, 0f);
            if (!_slapDone && t > 0.45f) { _slapDone = true; _kPitchV -= 22f; _kBackV += 0.25f; }   // the slam
        }
        else { hand = Vector3.Lerp(grab, support, Smooth((p - 0.90f) / 0.10f)); }
    }

    /// <summary>Measure a hand's local frame from its finger joints: f = wrist -> middle-finger knuckle, n = PALM normal
    /// (both in the hand bone's own space, so they are constant for a rig regardless of the pose). n comes from
    /// f x (index - pinky), which points out of the palm for a right hand and out of the back for a left hand.</summary>
    private bool TryGetHandFrame(string hand, string middle1, string index1, string pinky1, bool leftHand, out Vector3 f, out Vector3 n)
    {
        f = Vector3.Forward; n = Vector3.Up;
        Vector3 hp, he;
        if (!Animation.TryGetBoneTransform(_rig, hand, out hp, out he)) return false;
        Quaternion hi = Quaternion.FromEuler(he).Inverse;
        Vector3 fl = hi.Rotate(Animation.BonePosition(_rig, middle1) - hp);
        Vector3 sl = hi.Rotate(Animation.BonePosition(_rig, index1) - Animation.BonePosition(_rig, pinky1));
        if (fl.Length < 1e-4f || sl.Length < 1e-4f) return false;
        f = fl.Normalized;
        Vector3 nl = Vector3.Cross(f, sl.Normalized);
        if (nl.Length < 1e-4f) return false;
        n = nl.Normalized;
        if (leftHand) n = -n;
        return true;
    }

    /// <summary>Part of v orthogonal to the unit vector f, normalized.</summary>
    private static Vector3 Ortho(Vector3 v, Vector3 f)
    {
        Vector3 o = v - f * Vector3.Dot(v, f);
        return o.Length < 1e-5f ? Vector3.Up : o.Normalized;
    }

    private Vector3 GetRot(string bone) { Vector3 p, r; Animation.TryGetBoneTransform(_rig, bone, out p, out r); return r; }

    private void DriveMag(Weapon w, Vector3 magOff)
    {
        long weapon = PlayerRig.FpWeaponEntity;
        if (_magWeapon != weapon)
        {
            _magWeapon = weapon; _magEnt = 0;
            long[] kids = Scene.Children(weapon);
            for (int i = 0; kids != null && i < kids.Length; i++)
                if (Scene.NameOf(kids[i]) == w.MagChildName) { _magEnt = kids[i]; _magRest = Scene.PositionOf(_magEnt); _magRestRot = Scene.RotationOf(_magEnt); break; }
        }
        if (_magEnt == 0) return;
        Scene.SetPositionOf(_magEnt, _magRest + magOff);
        Scene.SetRotationOf(_magEnt, _magRestRot);
    }

    // Live tuning during development: VM_HIP="x,y,z,pitch,yaw,roll" VM_SPRINT=... VM_RELOAD="sx,sy,sz,tp,ty,tr"
    // VM_SUPPORT="x,y,z" VM_ADS=distance VM_FOV=deg VM_ANCHOR="x,y,z" VM_RIGPITCH=deg VM_REACH=0..1 VM_POLE=deg
    // VM_POLE_ADS=deg VM_POLE_R=deg (see the README's tuning section).
    private void ApplyEnvOverrides(Weapon w)
    {
        float[] v;
        if (EnvVec("VM_HIP", out v) && v.Length >= 6) { w.HipPosition = new Vector3(v[0], v[1], v[2]); w.HipRotation = new Vector3(v[3], v[4], v[5]); }
        if (EnvVec("VM_SPRINT", out v) && v.Length >= 6) { w.SprintPosition = new Vector3(v[0], v[1], v[2]); w.SprintRotation = new Vector3(v[3], v[4], v[5]); }
        if (EnvVec("VM_RELOAD", out v) && v.Length >= 6) { w.ReloadShift = new Vector3(v[0], v[1], v[2]); w.ReloadTilt = new Vector3(v[3], v[4], v[5]); }
        if (EnvVec("VM_SUPPORT", out v) && v.Length >= 3) w.SupportHandOffset = new Vector3(v[0], v[1], v[2]);
        if (EnvVec("VM_ADS", out v) && v.Length >= 1) w.AdsDistance = v[0];
        if (EnvVec("VM_FOV", out v) && v.Length >= 1) w.ViewmodelFov = v[0];
        if (EnvVec("VM_ANCHOR", out v) && v.Length >= 3) ShoulderAnchor = new Vector3(v[0], v[1], v[2]);
        if (EnvVec("VM_RIGPITCH", out v) && v.Length >= 1) RigPitch = v[0];
        if (EnvVec("VM_RIGYAW", out v) && v.Length >= 1) RigYaw = v[0];
        if (EnvVec("VM_REACH", out v) && v.Length >= 1) ReachFraction = v[0];
        if (EnvVec("VM_POLE", out v) && v.Length >= 1) ElbowPole = v[0];
        if (EnvVec("VM_POLE_ADS", out v) && v.Length >= 1) AdsElbowPole = v[0];
        if (EnvVec("VM_POLE_R", out v) && v.Length >= 1) ElbowPoleRight = v[0];
        if (EnvVec("VM_ORIENT", out v) && v.Length >= 1) OrientSupportHand = v[0] > 0.5f;
        if (EnvVec("VM_GRIPPOS", out v) && v.Length >= 3) w.GripHandPos = new Vector3(v[0], v[1], v[2]);
        if (EnvVec("VM_GRIPF", out v) && v.Length >= 3) w.GripFingerDir = new Vector3(v[0], v[1], v[2]);
        if (EnvVec("VM_GRIPPALM", out v) && v.Length >= 3) w.GripPalmDir = new Vector3(v[0], v[1], v[2]);
        if (EnvVec("VM_SUPF", out v) && v.Length >= 3) w.SupportFingerDir = new Vector3(v[0], v[1], v[2]);
        if (EnvVec("VM_SUPPALM", out v) && v.Length >= 3) w.SupportPalmDir = new Vector3(v[0], v[1], v[2]);
        if (EnvVec("VM_AUTOGRIP", out v) && v.Length >= 1) w.AutoGrip = v[0] > 0.5f;
        if (EnvVec("VM_PALMFLIP", out v) && v.Length >= 1) SupportPalmFlip = v[0] > 0.5f;

    }

    private static bool EnvVec(string name, out float[] values)
    {
        values = null;
        string s = System.Environment.GetEnvironmentVariable(name);
        if (s == null || s == "") return false;
        string[] parts = s.Split(',');
        values = new float[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            if (!float.TryParse(parts[i].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out values[i])) return false;
        return true;
    }

    private void Spring(ref float x, ref float v, float dt)
    {
        v += (-RecoilStiffness * x - RecoilDamping * v) * dt;
        x += v * dt;
    }

    private static float Approach(float cur, float target, float k) { if (k > 1f) k = 1f; return cur + (target - cur) * k; }
    private static float Smooth(float t) { if (t < 0f) t = 0f; else if (t > 1f) t = 1f; return t * t * (3f - 2f * t); }
    private static float Clamp(float v, float lo, float hi) { return v < lo ? lo : (v > hi ? hi : v); }
}
