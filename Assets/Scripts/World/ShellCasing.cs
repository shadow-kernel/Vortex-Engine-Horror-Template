using Vortex;

// Ejected pistol casing (spawned by WeaponPistol via Scene.Instantiate). Pops out with a small
// deterministic arc, falls under gravity, rests briefly, then despawns — a lightweight pool by
// lifetime, not a real physics body (a 2 cm prop doesn't need collide-and-slide).
public class ShellCasing : VortexBehaviour
{
    public float LifeTime = 2.5f;

    private float _age;
    private float _vx, _vy, _vz;
    private bool _ejected;

    public override void OnMessage(string message, object arg)
    {
        if (message != "eject") return;
        _ejected = true;
        // Deterministic per-shell variation from the entity id — no RNG, replay-stable.
        float v = (EntityId % 5) * 0.12f;
        _vx = 0.9f + v;
        _vy = 1.6f + v * 0.5f;
        _vz = 0.25f - v * 0.3f;
    }

    public override void Update(float dt)
    {
        _age = _age + dt;
        if (_age >= LifeTime) { Scene.Destroy(EntityId); return; }
        if (!_ejected) return;

        _vy = _vy - 9.8f * dt;
        Vector3 p = Position;
        p.X = p.X + _vx * dt;
        p.Y = p.Y + _vy * dt;
        p.Z = p.Z + _vz * dt;
        if (p.Y < 0.02f) { p.Y = 0.02f; _vx = 0f; _vy = 0f; _vz = 0f; }   // rest on the floor
        Position = p;
    }
}
