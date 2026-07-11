using Vortex;

// Shared game settings that gameplay scripts read (menu writes them live).
public static class UserSettings
{
    public static float MouseSensitivity = 0.09f;
    public static float Brightness       = 1.0f;   // multiplies HorrorAtmosphere exposure
    public static float MasterVolume     = 0.9f;
    public static float SfxVolume        = 1.0f;
    public static float MusicVolume      = 0.8f;
    public static float Fov              = 80f;
    public static float RenderScale      = 1.0f;
    public static bool  VSync            = false;
    public static bool  Fullscreen       = false;
    public static int   ResIndex         = 2;   // 1280/1600/1920/2560
    public static int   DlssMode         = 0;   // 0 Off · 1 Quality · 2 Balanced · 3 Performance · 4 Ultra
    public static int   FrameGen         = 0;   // 0 Off · 1 x2 · 2 x3 · 3 x4
}

// A single, unified, custom immediate-mode settings overlay (no .vui). Tabs: General / Graphics / Display /
// Audio / Controls. ESC toggles it: it FREES THE MOUSE and routes input to the menu but does NOT show a
// "PAUSED" screen. When the window is not focused OR the menu is open, the cursor is always released.
// Driven by CoDMovement: EscMenu.HandleToggle() on ESC edge, EscMenu.Tick() every frame.
public static class EscMenu
{
    public static bool IsOpen;

    // palette
    static readonly Color Scrim   = Color.Rgba(6, 7, 10, 224);
    static readonly Color Panel   = Color.Rgba(15, 17, 23, 240);
    static readonly Color Rail     = Color.Rgba(11, 12, 17, 245);
    static readonly Color Card     = Color.Rgba(22, 25, 32, 235);
    static readonly Color Hair     = Color.Rgba(255, 255, 255, 22);
    static readonly Color TextHi   = Color.Rgba(238, 241, 246, 250);
    static readonly Color TextMid  = Color.Rgba(176, 184, 198, 240);
    static readonly Color TextLo   = Color.Rgba(120, 128, 142, 220);
    static readonly Color Accent   = Color.Rgba(232, 178, 74, 255);
    static readonly Color AccentDim= Color.Rgba(232, 178, 74, 40);
    static readonly Color Track    = Color.Rgba(44, 48, 58, 230);
    static readonly Color Danger   = Color.Rgba(226, 74, 74, 255);

    static readonly string[] Tabs = { "GENERAL", "GRAPHICS", "DISPLAY", "AUDIO", "CONTROLS" };
    static int _tab;
    static int _view;              // 0 = pause menu (Resume/Options/Quit) · 1 = settings
    static float _appear;          // 0..1 open animation
    static bool _md, _mdPrev, _pressed;
    static string _dragId;         // active slider id
    static readonly int[] ResW = { 1280, 1600, 1920, 2560 };
    static readonly int[] ResH = { 720, 900, 1080, 1440 };

    public static void HandleToggle()
    {
        if (!IsOpen) { IsOpen = true; _view = 0; _appear = 0f; }
        else if (_view == 1) _view = 0;   // ESC in Options backs out to the pause menu
        else IsOpen = false;              // ESC on the pause menu resumes
    }
    public static void Resume() { IsOpen = false; _view = 0; }
    public static void Close() { Resume(); }

    static bool Hit(float x, float y, float w, float h)
    {
        float mx = UI.MouseX, my = UI.MouseY;
        return mx >= x && mx <= x + w && my >= y && my <= y + h;
    }
    static bool Clicked(float x, float y, float w, float h) { return _pressed && Hit(x, y, w, h); }

    public static void Tick(float dt)
    {
        if (!IsOpen) return;
        _mdPrev = _md; _md = UI.MouseDown; _pressed = _md && !_mdPrev;
        if (!_md) _dragId = null;
        _appear += (1f - _appear) * System.Math.Min(1f, 14f * dt);

        float W = UI.Width, H = UI.Height; if (W < 10f) return;
        float ease = _appear * _appear * (3f - 2f * _appear);

        // scrim (game stays visible faintly behind — mouse is freed, but no hard "PAUSED" freeze)
        UI.Rect(0, 0, W, H, Color.Rgba(6, 7, 10, (int)(210 * ease)), 0f);

        if (_view == 0) DrawPauseMenu(W, H, ease);
        else            DrawSettings(W, H, ease);
        ApplyAll();
    }

    // ---- view 0: standard pause menu ----
    static void DrawPauseMenu(float W, float H, float ease)
    {
        float cx = W * 0.5f, top = H * 0.30f - (1f - ease) * 24f;
        UI.Text("PAUSED", 0f, top, W, 56f, 46f, TextHi, 1, 800);
        UI.Text("the night waits.", 0f, top + 58f, W, 24f, 14f, TextLo, 1, 400);
        float bw = 340f, bh = 58f, gap = 14f, bx = cx - bw / 2f, by = top + 118f;
        if (MenuButton(bx, by, bw, bh, "RESUME", true)) Resume();
        if (MenuButton(bx, by + (bh + gap), bw, bh, "OPTIONS", false)) _view = 1;
        if (MenuButton(bx, by + 2f * (bh + gap), bw, bh, "QUIT GAME", false)) Application.Quit();
        UI.Text("ESC to resume", 0f, by + 3f * (bh + gap) + 10f, W, 20f, 12.5f, TextLo, 1, 500);
    }
    static bool MenuButton(float x, float y, float w, float h, string label, bool primary)
    {
        bool hov = Hit(x, y, w, h);
        UI.Rect(x + 2f, y + 4f, w, h, Color.Rgba(0, 0, 0, 100), 8f);
        Color bg = primary ? (hov ? Accent : Color.Rgba(52, 41, 18, 240)) : (hov ? Color.Rgba(38, 42, 52, 245) : Color.Rgba(22, 25, 32, 235));
        UI.Rect(x, y, w, h, bg, 8f);
        if (!(primary && hov)) UI.Rect(x, y + 10f, 3f, h - 20f, primary ? Accent : Color.Rgba(255, 255, 255, 30), 2f);
        Color fg = primary && hov ? Color.Rgba(16, 16, 20, 255) : TextHi;
        UI.Text(label, x, y + h / 2f - 13f, w, 26f, 18f, fg, 1, 700);
        return Clicked(x, y, w, h);
    }

    // ---- view 1: unified settings ----
    static void DrawSettings(float W, float H, float ease)
    {
        float pw = System.Math.Min(1120f, W - 120f), ph = System.Math.Min(680f, H - 120f);
        float px = (W - pw) / 2f, py = (H - ph) / 2f - (1f - ease) * 24f;
        Shadowed(px, py, pw, ph, Panel, 16f);
        UI.Rect(px + 24f, py + 74f, pw - 48f, 1.5f, Hair, 1f);
        UI.Text("SETTINGS", px + 30f, py + 26f, 400f, 34f, 26f, TextHi, 0, 800);
        UI.Text("ESC to go back", px + pw - 180f, py + 34f, 160f, 20f, 12.5f, TextLo, 2, 600);

        float railX = px + 24f, railY = py + 96f, railW = 210f;
        UI.Rect(railX, railY, railW, ph - 156f, Rail, 10f);
        for (int i = 0; i < Tabs.Length; i++)
        {
            float ty = railY + 14f + i * 52f, tw = railW - 20f, th = 42f, tx = railX + 10f;
            bool hov = Hit(tx, ty, tw, th);
            if (i == _tab) UI.Rect(tx, ty, tw, th, AccentDim, 8f);
            else if (hov) UI.Rect(tx, ty, tw, th, Color.Rgba(255, 255, 255, 14), 8f);
            if (i == _tab) UI.Rect(tx, ty + 8f, 3f, th - 16f, Accent, 2f);
            UI.Text(Tabs[i], tx + 16f, ty + 12f, tw - 20f, 20f, 14f, i == _tab ? TextHi : TextMid, 0, i == _tab ? 700 : 600);
            if (Clicked(tx, ty, tw, th)) _tab = i;
        }

        float cx = railX + railW + 28f, cy = railY + 6f, cw = px + pw - 30f - cx;
        float y = cy;
        if (_tab == 0)      y = TabGeneral(cx, y, cw);
        else if (_tab == 1) y = TabGraphics(cx, y, cw);
        else if (_tab == 2) y = TabDisplay(cx, y, cw);
        else if (_tab == 3) y = TabAudio(cx, y, cw);
        else                y = TabControls(cx, y, cw);

        float by = py + ph - 52f;
        if (Button(px + 30f, by, 150f, 38f, "< BACK", Accent, TextHi)) _view = 0;
    }

    // ---------------- tabs ----------------
    static float TabGeneral(float x, float y, float w)
    {
        UserSettings.Fov = Slider("fov", x, y, w, "Field of View", UserSettings.Fov, 60f, 110f, "0"); y += 66f;
        UserSettings.MouseSensitivity = Slider("sens", x, y, w, "Mouse Sensitivity", UserSettings.MouseSensitivity, 0.02f, 0.30f, "0.00"); y += 66f;
        UserSettings.Brightness = Slider("bri", x, y, w, "Brightness", UserSettings.Brightness, 0.5f, 1.8f, "0.00"); y += 66f;
        return y;
    }
    static float TabGraphics(float x, float y, float w)
    {
        UserSettings.RenderScale = Slider("rs", x, y, w, "Render Scale", UserSettings.RenderScale, 0.5f, 1.0f, "%"); y += 66f;
        if (Settings.DlssSupported)
        {
            UserSettings.DlssMode = Stepper("dlss", x, y, w, "DLSS 4", UserSettings.DlssMode, new string[]{ "Off", "Quality", "Balanced", "Performance", "Ultra Perf" }); y += 58f;
            UserSettings.FrameGen = Stepper("fg", x, y, w, "Frame Generation", UserSettings.FrameGen, new string[]{ "Off", "x2", "x3", "x4" }); y += 58f;
        }
        else { UI.Text("DLSS 4 — no compatible GPU detected", x, y + 8f, w, 20f, 13f, TextLo, 0, 500); y += 44f; }
        UserSettings.VSync = Toggle("vs", x, y, w, "V-Sync", UserSettings.VSync); y += 54f;
        UI.Text("Real FPS  " + Settings.CurrentFps, x, y + 6f, w, 20f, 13f, TextMid, 0, 600); y += 40f;
        return y;
    }
    static float TabDisplay(float x, float y, float w)
    {
        UserSettings.ResIndex = Stepper("res", x, y, w, "Resolution", UserSettings.ResIndex, new string[]{ "1280 x 720", "1600 x 900", "1920 x 1080", "2560 x 1440" }); y += 58f;
        UserSettings.Fullscreen = Toggle("fs", x, y, w, "Fullscreen", UserSettings.Fullscreen); y += 54f;
        UserSettings.VSync = Toggle("vs2", x, y, w, "V-Sync", UserSettings.VSync); y += 54f;
        return y;
    }
    static float TabAudio(float x, float y, float w)
    {
        UserSettings.MasterVolume = Slider("mv", x, y, w, "Master Volume", UserSettings.MasterVolume, 0f, 1f, "%"); y += 66f;
        UserSettings.SfxVolume = Slider("sfx", x, y, w, "Effects Volume", UserSettings.SfxVolume, 0f, 1f, "%"); y += 66f;
        UserSettings.MusicVolume = Slider("mus", x, y, w, "Music Volume", UserSettings.MusicVolume, 0f, 1f, "%"); y += 66f;
        return y;
    }
    static float TabControls(float x, float y, float w)
    {
        UserSettings.MouseSensitivity = Slider("sens2", x, y, w, "Mouse Sensitivity", UserSettings.MouseSensitivity, 0.02f, 0.30f, "0.00"); y += 70f;
        string[,] binds = {
            {"Move","W A S D"},{"Sprint","Shift  ·  W W = tac"},{"Crouch / Slide","Ctrl / C"},{"Jump","Space"},
            {"Fire","LMB"},{"Aim (ADS)","RMB"},{"Reload","R"},{"Fire Mode","V"},{"Flashlight","F"},{"Interact","E"},{"Menu","ESC"}
        };
        for (int i = 0; i < binds.GetLength(0); i++)
        {
            float ry = y + i * 30f;
            UI.Text(binds[i, 0], x, ry, w * 0.5f, 22f, 14f, TextMid, 0, 600);
            UI.Text(binds[i, 1], x + w * 0.5f, ry, w * 0.5f, 22f, 14f, TextHi, 2, 600);
        }
        return y + binds.GetLength(0) * 30f;
    }

    // ---------------- widgets ----------------
    static float Slider(string id, float x, float y, float w, string label, float val, float lo, float hi, string fmt)
    {
        float t = (val - lo) / (hi - lo); if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
        float trackX = x, trackY = y + 34f, trackW = w, trackH = 6f;
        // label + value
        string vs = fmt == "%" ? ((int)System.Math.Round((double)(t * 100f)) + "%")
                  : fmt == "0" ? ((int)System.Math.Round((double)val)).ToString()
                  : val.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        UI.Text(label, x, y, w - 70f, 22f, 14.5f, TextHi, 0, 600);
        UI.Text(vs, x + w - 70f, y, 70f, 22f, 14.5f, Accent, 2, 700);
        // track + fill + knob
        UI.Rect(trackX, trackY, trackW, trackH, Track, 3f);
        UI.Rect(trackX, trackY, trackW * t, trackH, Accent, 3f);
        float kx = trackX + trackW * t;
        bool hov = Hit(trackX - 6f, trackY - 12f, trackW + 12f, 30f);
        if (_pressed && hov) _dragId = id;
        UI.Rect(kx - 7f, trackY - 5f, 14f, 16f, hov || _dragId == id ? TextHi : Color.Rgba(220, 224, 232, 255), 4f);
        if (_dragId == id && _md)
        {
            float nt = (UI.MouseX - trackX) / trackW; if (nt < 0f) nt = 0f; else if (nt > 1f) nt = 1f;
            val = lo + nt * (hi - lo);
        }
        return val;
    }
    static bool Toggle(string id, float x, float y, float w, string label, bool on)
    {
        UI.Text(label, x, y + 4f, w - 70f, 24f, 14.5f, TextHi, 0, 600);
        float tw = 52f, th = 26f, tx = x + w - tw, ty = y + 2f;
        UI.Rect(tx, ty, tw, th, on ? Accent : Track, 13f);
        UI.Rect(on ? tx + tw - 24f : tx + 2f, ty + 2f, 22f, 22f, on ? Color.Rgba(20, 20, 24, 255) : Color.Rgba(180, 186, 196, 255), 11f);
        if (Clicked(tx - 4f, ty - 4f, tw + 8f, th + 8f)) on = !on;
        return on;
    }
    static int Stepper(string id, float x, float y, float w, string label, int idx, string[] opts)
    {
        UI.Text(label, x, y + 6f, w * 0.5f, 24f, 14.5f, TextHi, 0, 600);
        float bw = 34f, bh = 34f, val_w = w * 0.5f - bw * 2f - 8f;
        float rx = x + w - bw, lx = x + w - val_w - bw * 2f - 8f, vx = lx + bw + 4f;
        // left
        bool lh = Hit(lx, y, bw, bh); UI.Rect(lx, y, bw, bh, lh ? Color.Rgba(255,255,255,18) : Card, 6f);
        UI.Text("<", lx, y + 6f, bw, 22f, 18f, idx > 0 ? TextHi : TextLo, 1, 700);
        if (Clicked(lx, y, bw, bh) && idx > 0) idx--;
        // value
        UI.Rect(vx, y, val_w, bh, Card, 6f);
        UI.Text(opts[idx < 0 ? 0 : (idx >= opts.Length ? opts.Length - 1 : idx)], vx, y + 6f, val_w, 22f, 14f, Accent, 1, 700);
        // right
        bool rh = Hit(rx, y, bw, bh); UI.Rect(rx, y, bw, bh, rh ? Color.Rgba(255,255,255,18) : Card, 6f);
        UI.Text(">", rx, y + 6f, bw, 22f, 18f, idx < opts.Length - 1 ? TextHi : TextLo, 1, 700);
        if (Clicked(rx, y, bw, bh) && idx < opts.Length - 1) idx++;
        return idx;
    }
    static bool Button(float x, float y, float w, float h, string label, Color tint, Color fg)
    {
        bool hov = Hit(x, y, w, h);
        UI.Rect(x + 2f, y + 3f, w, h, Color.Rgba(0, 0, 0, 90), 8f);
        UI.Rect(x, y, w, h, hov ? tint : Color.Rgba(30, 33, 41, 235), 8f);
        UI.Text(label, x, y + h / 2f - 11f, w, 22f, 15f, hov ? Color.Rgba(16, 16, 20, 255) : fg, 1, 700);
        return Clicked(x, y, w, h);
    }
    static void Shadowed(float x, float y, float w, float h, Color c, float r)
    {
        UI.Rect(x + 5f, y + 8f, w, h, Color.Rgba(0, 0, 0, 120), r);
        UI.Rect(x, y, w, h, c, r);
    }

    // ---------------- apply to engine (only on change) ----------------
    static float _aFov = -1, _aRs = -1, _aMv = -1, _aSfx = -1, _aMus = -1;
    static bool _aVs, _aFs, _aInit;
    static int _aRes = -1, _aDlss = -1, _aFg = -1;
    static void ApplyAll()
    {
        if (!_aInit) { _aInit = true; _aVs = !UserSettings.VSync; _aFs = !UserSettings.Fullscreen; }
        if (Chg(ref _aFov, UserSettings.Fov, 0.1f)) Settings.SetFieldOfView(UserSettings.Fov);
        if (Chg(ref _aRs, UserSettings.RenderScale, 0.005f)) Settings.SetRenderScale(UserSettings.RenderScale);
        if (Chg(ref _aMv, UserSettings.MasterVolume, 0.005f)) Audio.SetBusVolume("Master", UserSettings.MasterVolume);
        if (Chg(ref _aSfx, UserSettings.SfxVolume, 0.005f)) Audio.SetBusVolume("SFX", UserSettings.SfxVolume);
        if (Chg(ref _aMus, UserSettings.MusicVolume, 0.005f)) Audio.SetBusVolume("Music", UserSettings.MusicVolume);
        if (_aVs != UserSettings.VSync) { _aVs = UserSettings.VSync; Settings.SetVSync(UserSettings.VSync); }
        if (_aFs != UserSettings.Fullscreen) { _aFs = UserSettings.Fullscreen; Settings.SetFullscreen(UserSettings.Fullscreen); }
        if (_aRes != UserSettings.ResIndex) { _aRes = UserSettings.ResIndex; Settings.SetResolution(ResW[UserSettings.ResIndex], ResH[UserSettings.ResIndex]); }
        if (_aDlss != UserSettings.DlssMode) { _aDlss = UserSettings.DlssMode; Settings.SetDlssMode(UserSettings.DlssMode); }
        if (_aFg != UserSettings.FrameGen) { _aFg = UserSettings.FrameGen; Settings.SetFrameGenMode(UserSettings.FrameGen); }
    }
    static bool Chg(ref float last, float cur, float eps) { if (System.Math.Abs(cur - last) > eps) { last = cur; return true; } return false; }
}
