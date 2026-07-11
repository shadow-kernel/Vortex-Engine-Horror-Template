using Vortex;

// The flashlight — attach to a SPOT-light entity that is a CHILD of the Player (the transform
// hierarchy makes it point where you look; no code needed for that). F toggles it, the battery
// drains while on and trickles back while off, low battery flickers like a dying bulb, and the
// spot light casts REAL shadows (Light component: Shadows = Soft).
public class FlashlightController : VortexBehaviour
{
    public float MaxBattery     = 100f;
    public float DrainPerSec    = 0.9f;    // ~110s of continuous light
    public float RechargePerSec = 0.35f;
    public float LowBatteryPct  = 22f;
    public float Intensity      = 40f;
    public bool  StartOn        = false;   // the cellar is lamp-lit; the flashlight starts OFF (F toggles).
                                           // NB: a spot light 0.2 m in front of the camera acts as a fill
                                           // light ON the gun -> point-blank inverse-square blows it white.

    private bool  _on = false;
    private float _battery;
    private bool  _fHeld;
    private float _flickerT;

    public override void Start()
    {
        _battery = MaxBattery;
        _on = StartOn;
        ApplyToLight();
    }

    public override void Update(float dt)
    {
        if (dt <= 0f) return;

        bool f = Input.GetKey("F") || Input.GetGamepadButtonDown("Y");
        if (f && !_fHeld) { _on = !_on; ApplyToLight(); }
        _fHeld = f;

        if (_on && _battery > 0f)
        {
            _battery -= DrainPerSec * dt;
            if (_battery <= 0f) { _battery = 0f; _on = false; ApplyToLight(); }
        }
        else if (!_on && _battery < MaxBattery)
        {
            _battery += RechargePerSec * dt;
            if (_battery > MaxBattery) _battery = MaxBattery;
        }

        _flickerT += dt;
        if (_on && _battery > 0f && _battery < LowBatteryPct) ApplyToLight();   // animate the dying bulb

        DrawBatteryHud();
    }

    private void ApplyToLight()
    {
        Light light = GetLight();
        if (light == null) return;
        if (!_on || _battery <= 0f) { light.Enabled = false; return; }
        light.Enabled = true;
        float intensity = Intensity;
        if (_battery < LowBatteryPct)
            intensity *= 0.5f + 0.5f * Flicker(_flickerT, 14f);
        light.Intensity = intensity;
    }

    private static float Flicker(float t, float speed)
    {
        float s = (float)System.Math.Sin(t * speed);
        float s2 = (float)System.Math.Sin(t * speed * 2.3f + 1.7f);
        float v = 0.5f + 0.35f * s + 0.15f * s2;
        return v < 0f ? 0f : (v > 1f ? 1f : v);
    }

    private void DrawBatteryHud()
    {
        float W = UI.Width, H = UI.Height;
        if (W < 10f) return;
        // coherent with the weapon HUD: dark ink panel + drop shadow + hairline + muted accents
        float pw = 190f, ph = 48f, px = 22f, py = H - ph - 22f;
        UI.Rect(px + 3f, py + 4f, pw, ph, Color.Rgba(0, 0, 0, 90), 12f);
        UI.Rect(px, py, pw, ph, Color.Rgba(11, 13, 18, 188), 12f);
        UI.Rect(px + 14f, py + 10f, pw - 28f, 1.5f, Color.Rgba(255, 255, 255, 26), 1f);

        float pct = _battery / MaxBattery; if (pct < 0f) pct = 0f;
        Color fill = _battery <= 0f ? Color.Rgba(240, 82, 82, 235)
                   : _battery < LowBatteryPct ? Color.Rgba(240, 150, 70, 235)
                   : Color.Rgba(122, 176, 150, 225);
        // label row
        UI.Text("FLASHLIGHT", px + 16f, py + 13f, 120f, 14f, 11.5f, Color.Rgba(150, 159, 174, 235), 0, 700);
        string state = _on ? (_battery <= 0f ? "DEPLETED" : "ON  ·  [F]") : "OFF  ·  [F]";
        Color sc = _battery <= 0f ? Color.Rgba(240, 82, 82, 235)
                 : _on ? Color.Rgba(122, 152, 190, 220) : Color.Rgba(120, 128, 140, 200);
        UI.Text(state, px + pw - 100f, py + 13f, 84f, 14f, 11.5f, sc, 2, 700);
        // battery bar
        float bx = px + 16f, by = py + ph - 15f, bw = pw - 32f, bh = 7f;
        UI.Rect(bx, by, bw, bh, Color.Rgba(38, 40, 48, 180), 3f);
        UI.Rect(bx, by, bw * pct, bh, fill, 3f);
    }
}
