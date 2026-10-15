using Vortex;

// Player vitals + med-kits (L4D2-style). Receives "damage"/"heal" messages, slow passive regen after a lull,
// and heals on the med-kit key [H] when you carry one. Publishes to PlayerRig so the HUD can read it.
// At zero health you die: the controls stop (PlayerRig.Dead), the enemies lose sight of you, the screen fades to
// "YOU DIED", and after RespawnDelay seconds you are back at the scene's start point with full health and med-kits —
// unseen and unhurt for SpawnProtection seconds, because the enemies come looking where they last saw you.
// Lives on the Player entity, next to CoDMovement: every "damage" sent to the player (a bot's shot, a zombie's bite, the
// monster's catch) lands here.
public class PlayerHealth : VortexBehaviour
{
    public float MaxHealth   = 100f;
    public int   StartMedkits = 3;
    public float MedkitHeal  = 60f;
    public float RegenPerSec = 2.5f;   // gentle passive regen up to a cap
    public float RegenCap    = 100f;
    public float RegenDelay  = 5f;     // seconds after damage before regen resumes
    public string HealSound  = "Assets/Audio/ads_tick.wav";
    public float RespawnDelay = 3f;    // seconds of "YOU DIED" before the respawn
    public float SpawnProtection = 2f; // seconds after the respawn before the enemies can see and hurt you again

    private float _hp, _sinceDamage;
    private int   _medkits;
    private bool  _healHeld;
    private float _deadT = -1f;        // seconds since the death, -1 while alive
    private float _fadeIn;             // the screen clearing again after the respawn
    private float _protectT;           // spawn protection left
    private const float FadeTime = 0.6f;

    public override void Start()
    {
        _hp = MaxHealth; _medkits = StartMedkits;
        PlayerRig.Dead = false;
        Publish();
    }

    public override void Update(float dt)
    {
        if (_deadT >= 0f)
        {
            _deadT += dt;
            if (_deadT >= RespawnDelay) Respawn();
            return;
        }
        if (_fadeIn > 0f) _fadeIn -= dt;
        if (_protectT > 0f) { _protectT -= dt; if (_protectT <= 0f) Perception.SetHidden(EntityId, false); }

        _sinceDamage += dt;
        if (_hp > 0f && _hp < RegenCap && _sinceDamage > RegenDelay)
            _hp = System.Math.Min(RegenCap, _hp + RegenPerSec * dt);

        // med-kit heal on [H] (edge-triggered) when you have one and aren't full
        bool heal = Input.GetKey("H");
        if (heal && !_healHeld && _medkits > 0 && _hp < MaxHealth)
        {
            _hp = System.Math.Min(MaxHealth, _hp + MedkitHeal);
            _medkits--;
            if (HealSound != "") Audio.PlayOneShot2D(HealSound, 0.55f, 1.2f);
        }
        _healHeld = heal;

        Publish();
    }

    // the death screen: drawn in LateUpdate, so it lies over the HUD (HudManager draws in Update)
    public override void LateUpdate(float dt)
    {
        float W = UI.Width, H = UI.Height;
        if (W < 10f || EscMenu.IsOpen) return;
        if (_deadT >= 0f)
        {
            float k = System.Math.Min(1f, _deadT / FadeTime);
            UI.Rect(0f, 0f, W, H, Color.Rgba(20, 0, 0, (int)(220 * k)));
            UI.Text("YOU DIED", 0f, H * 0.4f, W, 60f, 46f, Color.Rgba(230, 60, 50, (int)(250 * k)), 1, 800);
            int left = (int)System.Math.Ceiling(RespawnDelay - _deadT);
            UI.Text("Respawning in " + left, 0f, H * 0.4f + 66f, W, 24f, 15f, Color.Rgba(215, 218, 224, (int)(220 * k)), 1, 600);
        }
        else if (_fadeIn > 0f) UI.Rect(0f, 0f, W, H, Color.Rgba(20, 0, 0, (int)(220 * _fadeIn / FadeTime)));
    }

    public override void OnMessage(string message, object arg)
    {
        if (_deadT >= 0f || _protectT > 0f) return;   // nothing hurts the dead, or the just respawned
        if (message == "damage" && arg is float)
        {
            _hp = System.Math.Max(0f, _hp - (float)arg);
            _sinceDamage = 0f;
            if (_hp <= 0f) Die();
        }
        else if (message == "heal" && arg is float)
        {
            _hp = System.Math.Min(MaxHealth, _hp + (float)arg);
        }
    }

    private void Die()
    {
        _deadT = 0f;
        PlayerRig.Dead = true;                  // CoDMovement and the weapons stop taking input
        Perception.SetHidden(EntityId, true);   // the enemies lose sight of you: they search, then go back to patrolling
        Publish();
    }

    private void Respawn()
    {
        _deadT = -1f; _fadeIn = FadeTime; _protectT = SpawnProtection;
        _hp = MaxHealth; _medkits = StartMedkits; _sinceDamage = 0f;
        PlayerRig.Dead = false;
        if (_protectT <= 0f) Perception.SetHidden(EntityId, false);   // else when the protection runs out
        SendMessage(EntityId, "respawn");       // CoDMovement puts you back at the scene's start point
        Publish();
    }

    private void Publish()
    {
        PlayerRig.MaxHealth = MaxHealth;
        PlayerRig.Health = _hp;
        PlayerRig.Medkits = _medkits;
    }
}
