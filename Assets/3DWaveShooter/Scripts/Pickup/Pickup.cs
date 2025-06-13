using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gives the player something when they collide with the pickup. One time use.
/// </summary>
public class Pickup : MonoBehaviour
{
    public PickupType type;                         //Type of pickup.

    [Header("Health")]
    public int healthToGive;                        //Health given to the player upon pickup.

    [Header("Ammo")]
    public int ammoToGive;                          //How much ammo is given to the player?
    public bool spreadAmmoAcrossAllWeapons;         //Is the ammo spread out across all weapons?

    [Header("Weapon")]
    public WeaponScriptableObject baseWeapon;       //Base weapon scriptable object.
    public Weapon weaponToGive;                     //Weapon given upon pickup.

    private float creationTime;                     //Used to make sure the player doesn't instantly pick their weapon up again after dropping it.
    
    [Header("Speed Boost")]
    [Tooltip("How much to multiply the player's speed by.")]
    public float speedMultiplier = 1.60f;
    [Tooltip("How long the speed boost lasts (seconds).")]
    public float boostDuration   = 5f;

    [Header("Damage Boost")]
    [Tooltip("Multiplier to apply to player damage")]
    public float damageMultiplier = 1.75f;
    [Tooltip("How long the buff lasts (seconds)")]
    public float duration = 10f;

    void OnEnable ()
    {
        creationTime = Time.time;
    }

    void Start ()
    {
        if(baseWeapon != null)
            weaponToGive = WeaponManager.GetWeapon(baseWeapon);
    }

    //Sets the weapon to give.
    //Called when the player drops a weapon.
    public void SetWeapon (Weapon weapon)
    {
        weaponToGive = weapon;
    }

    void OnTriggerEnter(Collider col)
    {
        if (Time.time - creationTime < 0.2f) return;

        if (col.CompareTag("Player"))
        {
            switch (type)
            {
                case PickupType.Ammo:
                    if (spreadAmmoAcrossAllWeapons)
                    {
                        int perWeapon = Mathf.FloorToInt((float)ammoToGive / Player.inst.weapons.Count);
                        foreach (var w in Player.inst.weapons)
                            Player.inst.GiveAmmo(w.id, perWeapon);
                    }
                    else
                    {
                        Player.inst.GiveAmmo(Player.inst.curWeapon.id, ammoToGive);
                    }
                    Destroy(gameObject);
                    break;

                case PickupType.Weapon:
                    Player.inst.GiveWeapon(weaponToGive);
                    Destroy(gameObject);
                    break;

                case PickupType.SpeedBoost:
                    // Apply the speed boost on the player
                    Player.inst.ApplySpeedBoost(speedMultiplier, boostDuration);
                    Destroy(gameObject);
                    break;

                case PickupType.DamageBoost:
                    var atk = col.GetComponent<PlayerAttack>();
                    if (atk != null)
                    {
                        atk.ApplyDamageBuff(damageMultiplier, duration);
                    }
                    Destroy(gameObject);
                    break;

            }
        }
    }    
}

public enum PickupType
{
    Health,
    Weapon,
    Ammo,
    SpeedBoost,
    DamageBoost

}