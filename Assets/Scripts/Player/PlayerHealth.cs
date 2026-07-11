using Vortex;

// Player vitals + med-kits (L4D2-style). Receives "damage"/"heal" messages, slow passive regen after a lull,
// and heals on the med-kit key [H] when you carry one. Publishes to PlayerRig so the HUD can read it.
// Attach to the Player entity.
public class PlayerHealth : VortexBehaviour
{
    public float MaxHealth   = 100f;
    public int   StartMedkits = 3;
    public float MedkitHeal  = 60f;
    public float RegenPerSec = 2.5f;   // gentle passive regen up to a cap
    public float RegenCap    = 100f;
    public float RegenDelay  = 5f;     // seconds after damage before regen resumes
    public string HealSound  = "Assets/Audio/ads_tick.wav";

    private float _hp, _sinceDamage;
    private int   _medkits;
    private bool  _healHeld;

    public override void Start()
    {
        _hp = MaxHealth; _medkits = StartMedkits;
        Publish();
    }

    public override void Update(float dt)
    {
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

    public override void OnMessage(string message, object arg)
    {
        if (message == "damage" && arg is float)
        {
            _hp = System.Math.Max(0f, _hp - (float)arg);
            _sinceDamage = 0f;
        }
        else if (message == "heal" && arg is float)
        {
            _hp = System.Math.Min(MaxHealth, _hp + (float)arg);
        }
    }

    private void Publish()
    {
        PlayerRig.MaxHealth = MaxHealth;
        PlayerRig.Health = _hp;
        PlayerRig.Medkits = _medkits;
    }
}
