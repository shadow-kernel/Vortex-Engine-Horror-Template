using Vortex;

// Left-4-Dead-2-style survivor HUD: a health bar + number, player name, and med-kit count in the bottom-LEFT,
// an ammo panel bottom-RIGHT (mag / magazine size from the weapon system), plus a simple minimap with a
// heading arrow and nearby-entity blips (tags "Enemy"/"Door"). Reads everything from PlayerRig. One entity.
public class HudManager : VortexBehaviour
{
    public string PlayerName = "Survivor";
    public float  MapRange = 30f;    // world metres shown across the minimap
    public float  MapSize  = 236f;   // minimap size in px (bigger)

    private float _t;

    public override void Update(float dt)
    {
        float W = UI.Width, H = UI.Height; if (W < 10f) return;
        _t += dt;
        DrawVitals(W, H);
        DrawAmmo(W, H);
        DrawMiniMap(W, H);
    }

    // Ammo panel bottom-right: big mag count + magazine size, red pulse when empty, amber when low.
    private void DrawAmmo(float W, float H)
    {
        float pw = 170f, ph = 80f, px = W - pw - 22f, py = H - ph - 22f;
        UI.Rect(px + 3f, py + 4f, pw, ph, Color.Rgba(0, 0, 0, 90), 13f);
        UI.Rect(px, py, pw, ph, Color.Rgba(11, 13, 18, 190), 13f);
        UI.Rect(px + 15f, py + 11f, pw - 30f, 1.5f, Color.Rgba(255, 255, 255, 26), 1f);
        UI.Text("AMMO", px + 16f, py + 11f, 80f, 14f, 11f, Color.Rgba(150, 159, 174, 235), 0, 700);

        int mag = PlayerRig.Ammo, size = PlayerRig.MagSize;
        Color ac = mag > size / 4 ? Color.Rgba(236, 239, 244, 250)
                 : mag > 0        ? Color.Rgba(240, 170, 70, 245)
                 :                  Color.Rgba(240, 70, 70, 245);
        if (mag <= 0) ac = ac.WithAlpha(0.55f + 0.45f * (float)System.Math.Abs(System.Math.Sin(_t * 6.0)));
        UI.Text(mag.ToString(), px + 14f, py + 26f, 86f, 40f, 34f, ac, 2, 800);
        UI.Text("/ " + size, px + 104f, py + 40f, 56f, 22f, 16f, Color.Rgba(150, 159, 174, 235), 0, 700);
        UI.Text("[R] RELOAD", px + 16f, py + ph - 20f, 110f, 14f, 10.5f, Color.Rgba(150, 159, 174, 200), 0, 700);
    }

    private void DrawVitals(float W, float H)
    {
        float pw = 252f, ph = 80f, px = 22f, py = H - ph - 22f;
        UI.Rect(px + 3f, py + 4f, pw, ph, Color.Rgba(0, 0, 0, 90), 13f);            // drop shadow
        UI.Rect(px, py, pw, ph, Color.Rgba(11, 13, 18, 190), 13f);                 // body
        UI.Rect(px + 15f, py + 11f, pw - 30f, 1.5f, Color.Rgba(255, 255, 255, 26), 1f);

        UI.Text(PlayerName, px + 16f, py + 11f, pw - 32f, 18f, 14f, Color.Rgba(236, 239, 244, 250), 0, 800);

        float hp = PlayerRig.Health;
        float max = PlayerRig.MaxHealth < 1f ? 100f : PlayerRig.MaxHealth;
        float frac = max > 0f ? hp / max : 0f;
        if (frac < 0f) frac = 0f; else if (frac > 1f) frac = 1f;

        float bx = px + 16f, by = py + 38f, bw = pw - 92f, bh = 16f;
        Color hcol = frac > 0.5f ? Color.Rgba(90, 200, 110, 240)
                   : frac > 0.25f ? Color.Rgba(240, 170, 70, 245)
                   :                 Color.Rgba(240, 70, 70, 245);
        if (frac <= 0.25f) hcol = hcol.WithAlpha(0.55f + 0.45f * (float)System.Math.Abs(System.Math.Sin(_t * 6.0)));
        UI.Rect(bx - 1f, by - 1f, bw + 2f, bh + 2f, Color.Rgba(0, 0, 0, 150), 5f);
        UI.Rect(bx, by, bw, bh, Color.Rgba(40, 42, 50, 210), 4f);
        UI.Rect(bx, by, bw * frac, bh, hcol, 4f);
        UI.Text(((int)hp).ToString(), bx + bw + 8f, by - 5f, 62f, 26f, 21f, Color.Rgba(236, 239, 244, 250), 0, 800);

        int kits = PlayerRig.Medkits;
        UI.Text("MED-KITS", px + 16f, py + ph - 20f, 80f, 14f, 11f, Color.Rgba(150, 159, 174, 235), 0, 700);
        Color kc = kits > 0 ? Color.Rgba(120, 210, 150, 235) : Color.Rgba(150, 90, 90, 200);
        UI.Text("x " + kits + "   [H]", px + pw - 100f, py + ph - 21f, 88f, 14f, 12f, kc, 2, 800);
    }

    private void DrawMiniMap(float W, float H)
    {
        float ms = MapSize, mx = 18f, my = 18f;   // TOP-LEFT
        UI.Rect(mx - 2f, my - 2f, ms + 4f, ms + 4f, Color.Rgba(0, 0, 0, 120), 8f);
        UI.Rect(mx, my, ms, ms, Color.Rgba(11, 13, 18, 200), 8f);
        UI.Rect(mx + 12f, my + 10f, ms - 24f, 1.2f, Color.Rgba(255, 255, 255, 22), 1f);
        UI.Text("AREA", mx + 12f, my + 7f, 80f, 14f, 10.5f, Color.Rgba(150, 159, 174, 220), 0, 700);

        float cx = mx + ms * 0.5f, cy = my + ms * 0.5f;
        float scale = (ms * 0.5f - 12f) / MapRange;
        Vector3 p = PlayerRig.EyePos;
        double yawRad = PlayerRig.Yaw * System.Math.PI / 180.0;

        DrawBlips("Enemy", Color.Rgba(240, 80, 80, 240), p, yawRad, cx, cy, scale);
        DrawBlips("Door",  Color.Rgba(120, 170, 240, 220), p, yawRad, cx, cy, scale);

        // player heading arrow (points up = forward)
        Color pc = Color.Rgba(90, 220, 130, 250);
        UI.Line(cx, cy - 7f, cx - 5f, cy + 5f, pc, 2.4f);
        UI.Line(cx, cy - 7f, cx + 5f, cy + 5f, pc, 2.4f);
        UI.Line(cx - 5f, cy + 5f, cx + 5f, cy + 5f, pc, 2.4f);
    }

    private void DrawBlips(string tag, Color col, Vector3 p, double yawRad, float cx, float cy, float scale)
    {
        long[] ents = Scene.FindByTag(tag);
        if (ents == null) return;
        float s = (float)System.Math.Sin(yawRad), c = (float)System.Math.Cos(yawRad);
        for (int i = 0; i < ents.Length; i++)
        {
            Vector3 ep = Scene.PositionOf(ents[i]);
            float dx = ep.X - p.X, dz = ep.Z - p.Z;
            if (dx * dx + dz * dz > MapRange * MapRange) continue;
            float relRight   = dx * c - dz * s;   // camera-local right
            float relForward = dx * s + dz * c;   // camera-local forward
            float bx = cx + relRight * scale;
            float by = cy - relForward * scale;   // forward -> up on the map
            UI.Rect(bx - 2.2f, by - 2.2f, 4.4f, 4.4f, col, 2.2f);
        }
    }
}
