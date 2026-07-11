using Vortex;

// Melee: no ammo (derives from Weapon, not Firearm) — the default hitscan with a very short range
// IS a stab. Shows how a completely different weapon type drops into the same loadout.
public class Knife : Weapon
{
    public Knife()
    {
        WeaponName = "Knife";
        Damage = 55f;
        FireRate = 90f;        // ~0.66 s between swings
        Automatic = false;
        Range = 1.8f;
        FireSound = "Assets/Audio/gun_action.wav";

    }
}
