using Vortex;

// Full-auto rifle. Class defaults here; per-prefab values in the Inspector.
public class AssaultRifle : Firearm
{
    public AssaultRifle()
    {
        WeaponName = "Assault Rifle";
        Damage = 24f;
        FireRate = 640f;
        Automatic = true;
        Range = 140f;
        MagazineSize = 30;
        ReserveAmmo = 90;
        ReloadTime = 2.4f;   // matches the rifle_reload clip length
    }
}
