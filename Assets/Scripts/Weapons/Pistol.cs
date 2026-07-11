using Vortex;

// Semi-automatic sidearm. The constructor sets the CLASS defaults; every weapon PREFAB using this
// class can re-tune the fields in the Inspector (a "Deagle" prefab = Pistol class + Damage 60).
public class Pistol : Firearm
{
    public Pistol()
    {
        WeaponName = "Pistol";
        Damage = 34f;
        FireRate = 320f;
        Automatic = false;
        Range = 60f;
        MagazineSize = 12;
        ReserveAmmo = 48;
        ReloadTime = 1.6f;

    }
}
