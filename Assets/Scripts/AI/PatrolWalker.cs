using Vortex;

// PATROL WALKER — the smallest possible route follower, no behaviour tree involved: the entity's Nav Agent walks a
// Patrol Path waypoint by waypoint and pauses at each for the path's wait time. Put it on an entity with a Nav Agent;
// name the route in PathName (an entity with a Patrol Path component whose children are the waypoints) or leave it
// empty to use a Patrol Path on this entity. Loop / ping-pong / once come from the path's Mode.
public class PatrolWalker : VortexBehaviour
{
    public string PathName  = "MonsterPatrol";
    public float  Acceptance = 0.6f;    // "arrived" within this distance of the waypoint
    public float  ExtraWait  = 0f;      // added to the path's wait time at every stop

    private long _path;
    private int _index = -1, _dir = 1;
    private float _wait;
    private bool _done;

    public override void Start()
    {
        _path = PathName != "" ? Scene.Find(PathName) : EntityId;
        if (_path == 0 || PatrolPath.Count(_path) == 0) { Debug.Log("PatrolWalker: no route '" + PathName + "'"); _done = true; return; }
        _index = PatrolPath.Closest(_path, WorldPosition);
        Navigation.SetDestination(EntityId, PatrolPath.GetWaypoint(_path, _index));
    }

    public override void Update(float dt)
    {
        if (_done) return;
        if (_wait > 0f)
        {
            _wait -= dt;
            if (_wait > 0f) return;
            _index = PatrolPath.Next(_path, _index, ref _dir);
            if (_index < 0) { _done = true; return; }   // a "once" route ends at its last waypoint
            Navigation.SetDestination(EntityId, PatrolPath.GetWaypoint(_path, _index));
            return;
        }
        Vector3 wp = PatrolPath.GetWaypoint(_path, _index);
        Vector3 me = WorldPosition;
        float dx = wp.X - me.X, dz = wp.Z - me.Z;
        if (dx * dx + dz * dz <= Acceptance * Acceptance || Navigation.HasArrived(EntityId))
            _wait = PatrolPath.WaitTime(_path) + ExtraWait + 0.01f;
    }
}
