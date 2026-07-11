using Vortex;

// Ejected 9x19 casing. Flies out the ejection port with the world velocity the weapon hands it (arg),
// tumbles in the air, bounces off the floor a couple of times with a metallic tink, then ROLLS to a stop
// and lies on the ground for a while before it fades out. Not a full rigid body — a lightweight, replay-
// stable approximation that reads exactly like brass hitting a concrete cellar floor.
public class ShellCasing : VortexBehaviour
{
    public float LifeTime = 30f;      // stay on the ground a long time so you can see the brass pile up
    public string TinkSound = "Assets/Audio/shell_drop.wav";

    private float _age;
    private Vector3 _vel;     // linear m/s
    private Vector3 _ang;     // angular deg/s (air tumble)
    private float _floorY = 0.01f;
    private bool  _ejected, _grounded;
    private int   _bounces;
    private System.Random _rng;

    public override void OnMessage(string message, object arg)
    {
        if (message != "eject") return;
        _ejected = true;
        _rng = new System.Random(unchecked((int)(EntityId * 2654435761) ^ 0x5bd1e995));

        // base velocity comes from the weapon (ejection port -> up/right/slightly back, in WORLD space)
        if (arg is Vector3) _vel = (Vector3)arg;
        else _vel = new Vector3(1.8f, 1.6f, 0f);
        // small deterministic scatter so no two casings fly identically
        _vel.X += (float)(_rng.NextDouble() - 0.5) * 0.7f;
        _vel.Y += (float)(_rng.NextDouble() - 0.2) * 0.5f;
        _vel.Z += (float)(_rng.NextDouble() - 0.5) * 0.7f;
        _ang = new Vector3((float)(_rng.NextDouble() * 2 - 1) * 1100f,
                           (float)(_rng.NextDouble() * 2 - 1) * 900f,
                           (float)(_rng.NextDouble() * 2 - 1) * 1400f);

        // find the floor directly beneath the spawn (handles props / uneven ground)
        RaycastHit hit;
        if (Physics.Raycast(Position, new Vector3(0f, -1f, 0f), 4f, out hit)) _floorY = hit.Point.Y + 0.008f;
        else _floorY = Position.Y - 1.4f;
    }

    public override void Update(float dt)
    {
        _age += dt;
        if (_age >= LifeTime) { Scene.Destroy(EntityId); return; }
        if (!_ejected || dt <= 0f) return;

        Vector3 p = Position;
        Vector3 rot = Rotation;

        if (!_grounded)
        {
            _vel.Y -= 9.8f * dt;
            p.X += _vel.X * dt; p.Y += _vel.Y * dt; p.Z += _vel.Z * dt;
            rot.X += _ang.X * dt; rot.Y += _ang.Y * dt; rot.Z += _ang.Z * dt;

            if (p.Y <= _floorY)
            {
                p.Y = _floorY;
                float impact = -_vel.Y;
                if (_vel.Y < -0.5f && _bounces < 4)
                {
                    _vel.Y = -_vel.Y * 0.36f;              // bounce back up (energy loss)
                    _vel.X *= 0.6f; _vel.Z *= 0.6f;        // horizontal scrub
                    _ang.X *= 0.45f; _ang.Y *= 0.45f; _ang.Z *= 0.45f;
                    _bounces++;
                    PlayTink(p, System.Math.Min(0.8f, 0.35f + impact * 0.12f));   // metallic clink on EACH bounce, louder on harder hits
                }
                else
                {
                    _vel.Y = 0f; _grounded = true;         // settle into rolling
                    PlayTink(p, 0.3f);
                }
            }
        }
        else
        {
            // rolling on the floor: horizontal drift with friction, spin decays; then lie flat and rest
            float sp = (float)System.Math.Sqrt(_vel.X * _vel.X + _vel.Z * _vel.Z);
            if (sp > 0.03f)
            {
                p.X += _vel.X * dt; p.Z += _vel.Z * dt;
                float fr = System.Math.Min(1f, 2.2f * dt);
                _vel.X -= _vel.X * fr; _vel.Z -= _vel.Z * fr;
                rot.X += sp * 900f * dt;                    // rolling tumble ~ proportional to speed
            }
            else
            {
                _vel.X = _vel.Z = 0f;
                // ease pitch/roll toward flat-on-side (keep yaw), so it lies naturally
                rot.X += (90f - rot.X) * System.Math.Min(1f, 6f * dt);
                rot.Z += (0f - rot.Z) * System.Math.Min(1f, 6f * dt);
            }
            p.Y = _floorY;
        }

        Position = p; Rotation = rot;
    }

    private void PlayTink(Vector3 p, float vol) { if (TinkSound != "") Audio.PlayOneShot(TinkSound, p, vol); }
}
