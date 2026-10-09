using Vortex;

// A HIDING SPOT — a closet, a crawl space, the shadow under the stairs. Put this script on an entity with a Collider
// marked "Is Trigger": while the player stands inside, the monster's perception treats them as hidden — line of sight
// is broken even in the open, so the monster walks to the last place it saw them (the tree's Search branch) and gives
// up. Stepping out makes the player visible again. Hearing is NOT muted: a shot or sprinting inside the spot is heard.
public class HidingSpot : VortexBehaviour
{
    public string PlayerTag = "Player";
    public bool   LogEnterExit = true;

    public override void OnTriggerEnter(TriggerHit other)
    {
        if (!IsPlayer(other)) return;
        Perception.SetHidden(other.EntityId, true);
        if (LogEnterExit) Debug.Log("Hidden.");
    }

    public override void OnTriggerExit(TriggerHit other)
    {
        if (!IsPlayer(other)) return;
        Perception.SetHidden(other.EntityId, false);
        if (LogEnterExit) Debug.Log("Exposed.");
    }

    private bool IsPlayer(TriggerHit other) { return other.EntityId != 0 && other.Tag == PlayerTag; }
}
