using Vortex;

// Drop on any Point/Spot light entity to make its bulb live. Two moods:
//   Mode 0 "bulb"      – mostly steady with occasional nervous flickers + rare blackout blinks.
//   Mode 1 "generator" – low steady hum-pulse (a working machine), never fully dark.
// Uses the engine's Light.Flicker noise so every bulb desyncs from its neighbours.
public class LightFlicker : VortexBehaviour
{
    public int   Mode          = 0;      // 0 bulb, 1 generator
    public float BaseIntensity = 3.2f;
    public float FlickerAmount = 0.35f;  // 0..1 depth of the wobble
    public float Speed         = 11f;
    public float DropoutChance = 0.35f;  // bulb-mode: blackout blinks per ~second
    public float Seed          = 0f;

    private Light _light;
    private float _t;
    private float _blackout;              // seconds of a current blackout blink
    private System.Random _rng;

    public override void Start()
    {
        _light = GetLight();
        _rng = new System.Random((int)(Seed * 1000f) + (int)(EntityId & 0xffff));
        if (_light != null) BaseIntensity = _light.Intensity > 0.01f ? _light.Intensity : BaseIntensity;
    }

    public override void Update(float dt)
    {
        if (_light == null) return;
        _t += dt;

        if (Mode == 1)
        {
            // generator: gentle 2-tone pulse, always lit
            float pulse = 0.85f + 0.15f * (float)System.Math.Sin(_t * 6.0);
            float n = Light.Flicker(_t + Seed, Speed * 0.5f);
            _light.Intensity = BaseIntensity * (pulse * (1f - FlickerAmount * 0.4f) + FlickerAmount * 0.4f * n);
            return;
        }

        // bulb: steady-ish noise, plus rare hard blackout blinks
        if (_blackout > 0f)
        {
            _blackout -= dt;
            _light.Intensity = BaseIntensity * 0.04f;
            return;
        }
        if (_rng.NextDouble() < DropoutChance * dt) { _blackout = 0.03f + (float)_rng.NextDouble() * 0.09f; return; }

        float f = Light.Flicker(_t + Seed, Speed);
        _light.Intensity = BaseIntensity * (1f - FlickerAmount + FlickerAmount * f);
    }
}
