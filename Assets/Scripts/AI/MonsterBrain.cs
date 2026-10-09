using Vortex;

// THE MONSTER'S BODY — the half of the monster that is not the behaviour tree.
//
// The decisions live in Assets/AI/Monster.vbt (open it in the Behavior Tree editor: Asset Browser ▸ AI ▸ Monster):
// chase on sight, investigate noises, search the last known spot, otherwise patrol the MonsterPatrol route. The tree's
// built-in tasks drive the Nav Agent and read the AI Perception; every "SendMessage" node in the tree lands here as
// OnMessage("bt.<state>"). This script turns those states into what the player sees and hears:
//   * the locomotion clip follows the agent's actual speed (idle / walk / run, crossfaded)
//   * the chase starts the tension loop, losing the player fades it out again
//   * "bt.catch" hurts the player (PlayerHealth listens to "damage")
// Everything is a project script — the engine ticks the tree and moves the agent, the game decides what that means.
public class MonsterBrain : VortexBehaviour
{
    public string TreePath   = "Assets/AI/Monster.vbt";
    public string IdleClip   = "idle";
    public string WalkClip   = "walk";
    public string RunClip    = "run";
    public float  RunSpeed   = 3.2f;     // agent speed above which the run clip plays
    public float  Fade       = 0.2f;
    public string ChaseMusic = "Assets/Audio/cellar_ambience.wav";   // the tension loop (swap for your chase track)
    public float  CatchDamage = 35f;
    public float  CatchCooldown = 1.5f;

    private string _clip = "";
    private string _state = "";
    private float _sinceCatch = 99f;
    private bool _music;

    public override void Start()
    {
        // the prefab carries a Behavior Tree component with RunOnStart; a monster spawned without one starts its tree here
        if (!BehaviorTree.IsRunning(EntityId)) BehaviorTree.Run(EntityId, TreePath);
        PlayClip(IdleClip);
    }

    public override void Update(float dt)
    {
        _sinceCatch += dt;
        // locomotion from the agent's real velocity: the tree says where, the feet say how fast
        Vector3 v = Navigation.Velocity(EntityId);
        float speed = (float)System.Math.Sqrt(v.X * v.X + v.Z * v.Z);
        string want = speed < 0.25f ? IdleClip : (speed < RunSpeed ? WalkClip : RunClip);
        if (want != _clip) PlayClip(want);
    }

    private void PlayClip(string clip)
    {
        _clip = clip;
        PlayAnimation(clip, Fade);
    }

    // ---- the tree talks: SendMessage nodes in Monster.vbt ----
    public override void OnMessage(string message, object arg)
    {
        switch (message)
        {
            case "bt.chase":
                SetState("chase");
                if (!_music) { _music = true; Audio.Music.CrossFade(ChaseMusic, 1.2f); }
                break;
            case "bt.catch":
                if (_sinceCatch >= CatchCooldown)
                {
                    _sinceCatch = 0f;
                    long target = BlackboardTarget();
                    if (target != 0) SendMessage(target, "damage", CatchDamage);
                    Debug.Log("The monster caught you.");
                }
                break;
            case "bt.investigate": SetState("investigate"); StopMusic(); break;
            case "bt.search":      SetState("search");      StopMusic(); break;
            case "bt.patrol":      SetState("patrol");      StopMusic(); break;
        }
    }

    private void StopMusic()
    {
        if (!_music) return;
        _music = false;
        Audio.Music.Stop(2.5f);
    }

    private void SetState(string s)
    {
        if (s == _state) return;
        _state = s;
        Debug.Log("Monster: " + s);
    }

    private long BlackboardTarget()
    {
        var bb = BehaviorTree.BlackboardOf(EntityId);
        return bb != null ? bb.GetEntity("Target") : 0;
    }

    public override void OnDestroy() { StopMusic(); }
}
