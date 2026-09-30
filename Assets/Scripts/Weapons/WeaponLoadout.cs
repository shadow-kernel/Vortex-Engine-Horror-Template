using Vortex;

// THE one weapon object on the player. Put this on a single entity (e.g. "Loadout" under Player)
// and fill WeaponPrefabs in the Inspector (+ Add, then "…" to pick each .ventity). At play start it
// spawns every weapon TWICE and BONE-ATTACHES each copy to a hand:
//   - FP instance (RenderLayer 1): glued to the FP arms rig's hand — it rides every arm animation
//     (walk sway, fire, reload) automatically. Its Weapon script is the ACTIVE one (input + ammo).
//   - 3P instance (RenderLayer 2): glued to the world body's hand — hidden for you, visible for
//     every other camera / player / the P debug cam.
// Switch with the 1..9 keys / mouse wheel, or from ANY script:
//   WeaponLoadout lo = Scene.GetBehaviour<WeaponLoadout>(Scene.Find("Loadout"));
//   lo.Equip("Vityaz");  lo.Equip(2);  lo.EquipNext();  Weapon w = lo.ActiveWeapon;
public class WeaponLoadout : VortexBehaviour
{
    // The whole arsenal as prefab paths — edited as a LIST in the Inspector.
    public string[] WeaponPrefabs = new string[0];
    public int StartSlot = 0;
    public string FpEntity = "FP_Arms";             // FP arms rig (RenderLayer-1 viewmodel)
    public string BodyEntity = "WCharakter";        // 3P world body; "" = no body copy
    public string HandBone = "mixamorig:RightHand";
    public string SwitchSound = "Assets/Audio/gun_action.wav";

    public float HolsterTime = 0.22f;               // the current weapon drops out of the frame …
    public float DrawTime = 0.32f;                  // … and the next one rises into it

    public static WeaponLoadout Instance;   // the player's loadout (set in Start)
    private long[] _fp;        // FP weapon instances (layer 1, attached to the FP arms hand)
    private long[] _tp;        // 3P weapon instances (layer 2, attached to the body hand)
    private Weapon[] _fpW;     // behaviour of each FP instance (polymorphic access)
    private int _slot = -1;
    private bool[] _numHeld;
    private float _wheelCd;
    private int _pendingSlot = -1;                  // swap in progress: target slot
    private float _switchT;                          // seconds into the swap
    private bool _swapped;                           // the entity swap of the current transition happened

    public int ActiveSlot { get { return _slot; } }
    public Weapon ActiveWeapon
    {
        get { return (_slot >= 0 && _fpW != null && _slot < _fpW.Length) ? _fpW[_slot] : null; }
    }

    public override void Start()
    {
        Instance = this;
        int n = WeaponPrefabs != null ? WeaponPrefabs.Length : 0;
        _fp = new long[n];
        _tp = new long[n];
        _fpW = new Weapon[n];
        _numHeld = new bool[10];
        long fpRig = (FpEntity != null && FpEntity != "") ? Scene.Find(FpEntity) : 0;
        long body = (BodyEntity != null && BodyEntity != "") ? Scene.Find(BodyEntity) : 0;
        if (fpRig == 0) Debug.LogWarning("WeaponLoadout: FP rig '" + FpEntity + "' not found — no first-person weapon.");
        if (body == 0) Debug.LogWarning("WeaponLoadout: body '" + BodyEntity + "' not found — no third-person weapon.");

        for (int i = 0; i < n; i++)
        {
            string prefab = WeaponPrefabs[i];
            if (prefab == null || prefab == "") continue;

            if (fpRig != 0) _fp[i] = SpawnOn(prefab, fpRig, 1, true, i);
            if (body != 0) _tp[i] = SpawnOn(prefab, body, 2, false, i);
        }
        // first weapon: no holster phase, just the draw (the gun rises into the frame at spawn)
        if (n > 0 && StartSlot >= 0 && StartSlot < n) { ActivateSlot(StartSlot); _pendingSlot = -1; _switchT = HolsterTime; _swapped = true; PlayerRig.Switching = true; PlayerRig.SwitchLower = 1f; }
    }

    /// <summary>Spawn one weapon copy, force its render layer, glue it to the rig's hand bone with the
    /// prefab's grip fields, and wire its Weapon behaviour.</summary>
    private long SpawnOn(string prefab, long rig, int layer, bool isFp, int slot)
    {
        long e = Scene.Instantiate(prefab, new Vector3(0f, -1000f, 0f), 0f);
        if (e == 0) { Debug.LogError("WeaponLoadout: could not instantiate '" + prefab + "'"); return 0; }
        Scene.SetRenderLayer(e, layer);
        Weapon w = Scene.GetBehaviour<Weapon>(e);
        Vector3 gripPos = Vector3.Zero, gripRot = Vector3.Zero;
        if (w != null)
        {
            gripPos = w.GripOffset; gripRot = w.GripRotation;
            w.IsFpInstance = isFp; w.Equipped = false;
            Firearm f = w as Firearm;
            if (f != null) f.RigEntityId = rig;                 // mag-pull targets THIS rig's left hand
            if (isFp) _fpW[slot] = w;
        }
        Animation.Attach(e, rig, HandBone, gripPos, gripRot);
        Scene.SetActive(e, false);
        return e;
    }

    public override void Update(float dt)
    {
        if (!PlayerRig.Ready || PlayerRig.Inspect) return;
        int n = _fp != null ? _fp.Length : 0;
        if (n == 0) return;

        // swap timeline: holster (lower) -> swap entities -> draw (raise)
        if (PlayerRig.Switching)
        {
            _switchT += dt;
            if (_switchT < HolsterTime) PlayerRig.SwitchLower = Smooth(_switchT / HolsterTime);
            else
            {
                if (!_swapped) { _swapped = true; ActivateSlot(_pendingSlot); _pendingSlot = -1; }
                float t = (_switchT - HolsterTime) / (DrawTime > 0.01f ? DrawTime : 0.01f);
                PlayerRig.SwitchLower = 1f - Smooth(t);
                if (t >= 1f) { PlayerRig.Switching = false; PlayerRig.SwitchLower = 0f; }
            }
        }

        // number keys 1..9
        for (int i = 0; i < n && i < 9; i++)
        {
            bool k = Input.GetKey("D" + (i + 1));
            if (k && !_numHeld[i]) Equip(i);
            _numHeld[i] = k;
        }

        // mouse wheel cycles
        if (_wheelCd > 0f) _wheelCd -= dt;
        float wheel = Input.ScrollDelta;
        if (wheel != 0f && _wheelCd <= 0f && n > 1)
        {
            _wheelCd = 0.2f;
            int dir = wheel < 0f ? 1 : (n - 1);
            Equip((_slot + dir) % n);
        }
    }

    /// <summary>Equip by slot index (no-op on same slot / out of range / while a swap is running): the current
    /// weapon is lowered, then the entities swap and the new one rises (HolsterTime + DrawTime).</summary>
    public void Equip(int slot)
    {
        int n = _fp != null ? _fp.Length : 0;
        if (slot < 0 || slot >= n || slot == _slot || PlayerRig.Switching) return;
        _pendingSlot = slot; _switchT = 0f; _swapped = false;
        PlayerRig.Switching = true; PlayerRig.SwitchLower = 0f;
        if (SwitchSound != null && SwitchSound != "") Audio.PlayOneShot2D(SwitchSound, 0.6f, 1.1f);
    }

    private static float Smooth(float t) { if (t < 0f) t = 0f; else if (t > 1f) t = 1f; return t * t * (3f - 2f * t); }

    /// <summary>Re-glue both copies of a weapon with (new) grip values — the ViewmodelRig calls this after computing
    /// the auto-grip from the rig's hand geometry, so the third-person copy holds the gun the same way.</summary>
    public void Reattach(Weapon w)
    {
        if (w == null || _fpW == null) return;
        for (int i = 0; i < _fpW.Length; i++)
        {
            if (_fpW[i] != w) continue;
            long fpRig = Scene.Find(FpEntity), body = BodyEntity != "" ? Scene.Find(BodyEntity) : 0;
            if (_fp[i] != 0 && fpRig != 0) Animation.Attach(_fp[i], fpRig, HandBone, w.GripOffset, w.GripRotation);
            if (_tp != null && _tp[i] != 0 && body != 0) Animation.Attach(_tp[i], body, HandBone, w.GripOffset, w.GripRotation);
            Firearm f = w as Firearm; if (f != null) f.RigEntityId = fpRig;
        }
    }

    /// <summary>The instantaneous part of a swap: holster the old instances, activate the new ones.</summary>
    private void ActivateSlot(int slot)
    {
        int n = _fp != null ? _fp.Length : 0;
        if (slot < 0 || slot >= n || slot == _slot) return;

        if (_slot >= 0 && _slot < n)
        {
            if (_fpW[_slot] != null) { _fpW[_slot].Equipped = false; _fpW[_slot].OnHolster(); }
            if (_fp[_slot] != 0) Scene.SetActive(_fp[_slot], false);
            if (_tp[_slot] != 0) Scene.SetActive(_tp[_slot], false);
            PlayerRig.ActiveWeapon = null; PlayerRig.FpWeaponEntity = 0;
        }

        _slot = slot;
        if (_fp[slot] != 0)
        {
            Scene.SetActive(_fp[slot], true);
            if (_fpW[slot] != null) { _fpW[slot].Equipped = true; _fpW[slot].OnEquip(); }
        }
        if (_tp[slot] != 0) Scene.SetActive(_tp[slot], true);
        // the procedural viewmodel (ViewmodelRig on the FP arms) follows the active weapon's poses
        PlayerRig.ActiveWeapon = _fpW[slot];
        PlayerRig.FpWeaponEntity = _fp[slot];
        PlayerRig.WeaponSlot = slot;
    }

    /// <summary>Equip by WeaponName or class name ("Pistol"). False if not found.</summary>
    public bool Equip(string weaponName)
    {
        if (weaponName == null || _fpW == null) return false;
        for (int i = 0; i < _fpW.Length; i++)
        {
            Weapon w = _fpW[i];
            if (w == null) continue;
            if (string.Equals(w.WeaponName, weaponName, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(w.GetType().Name, weaponName, System.StringComparison.OrdinalIgnoreCase))
            {
                Equip(i);
                return true;
            }
        }
        return false;
    }

    public void EquipNext()
    {
        int n = _fp != null ? _fp.Length : 0;
        if (n > 0) Equip((_slot + 1) % n);
    }
}
