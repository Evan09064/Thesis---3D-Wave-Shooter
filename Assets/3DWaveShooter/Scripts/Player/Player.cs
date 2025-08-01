﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Contains data about the player.
/// </summary>
public class Player : MonoBehaviour
{
    [Header("Stats")]
    public PlayerState state;                       //Current state of the player.
    public float moveSpeed;                         //Player's move speed in units per second.
    public int money;                               //Player's current money.

    [Header("Weapon")]
    public WeaponScriptableObject startingWeapon;   //Weapon the player starts the game with.
    public Weapon curWeapon;                        //Player's currently equipped weapon.
    //public int maxWeapons;                          //Maximum number of weapons the player can hold.
    public List<Weapon> weapons = new List<Weapon>();//Player's inventory of weapons.
    private GameObject curWeaponObject;             //Player's current weapon game object visual.

    [Header("Bools")]
    public bool canMove;                            //Is the player able to move?
    public bool canAttack;                          //Is the player able to use their weapon/s?

    [Header("Swap Control")]
    public bool canSwap = true;    // whether the player is allowed to scroll‐switch weapons


    [Header("Components")]
    public GameObject weaponPos;                    //Position the player will hold their weapon at.
    public PlayerMovement movement;                 //Player's PlayerMovement component.
    public PlayerAttack attack;                     //Player's PlayerAttack component.
    public AudioSource audioSource;                 //Player's Audio Source component.
    public Animator anim;                           //Player's Animator component.
    public MeshSetter meshSetter;                   //Player's MeshSetter.cs component.
    public float currentWeaponEquipTime;
    
    private Coroutine _boostCoroutine = null;


    //Instance
    public static Player inst;                      //We create an instance (singelton) of the player so that it can be accessed from anywhere.
    void Awake () { inst = this; }

    void Start ()
    {
        //Get missing components.
        if(!movement) movement = GetComponent<PlayerMovement>();
        if(!attack) attack = GetComponent<PlayerAttack>();
        if(!audioSource) audioSource = GetComponent<AudioSource>();
        if(!anim) anim = transform.Find("PlayerModel").GetComponent<Animator>();
        if(!meshSetter) meshSetter = GetComponent<MeshSetter>();

        //Make sure to give the player their starting weapon.
        GiveWeapon(WeaponManager.GetWeapon(startingWeapon));
    }

    void Update ()
    {
        CheckInputs();
        // Accumulate active weapon time if the game is in progress.
        if(GameManager.inst.waveInProgress && GameManager.inst.gameIsActive && curWeapon != null && !string.IsNullOrEmpty(curWeapon.displayName))
        {
            // Update the usage time per frame.
            if(WeaponUsageStats.WeaponUsageTimes.ContainsKey(curWeapon.displayName))
                WeaponUsageStats.WeaponUsageTimes[curWeapon.displayName] += Time.deltaTime;
            else
                WeaponUsageStats.WeaponUsageTimes[curWeapon.displayName] = Time.deltaTime;
        }
    }

    //Checks for keyboard inputs.
    void CheckInputs ()
    {
        //Weapon change.
        if(Input.GetAxis("Mouse ScrollWheel") > 0)
            TryChangeWeapon(1);
        else if(Input.GetAxis("Mouse ScrollWheel") < 0)
            TryChangeWeapon(-1);
    }

    //Called when the player takes damage. From enemies or any other source in the world.
    public void TakeDamage(int damage)
    {
        // Update damage counters.
        PerformanceStats.OverallDamageTaken += damage;
        PerformanceStats.RoundDamageTaken += damage;

        //Instead of losing health, Player will lose 10 units of money 
        if (GameManager.inst.waveCount == 1 || GameManager.inst.waveCount == 2 || GameManager.inst.waveCount == 3)
        {
            money -= 10;
        }
        else if (GameManager.inst.waveCount == 4 || GameManager.inst.waveCount == 5)
        {
            money -= 30;
        }
        if (money < 0) money = 0;

        //Sound effect.
        AudioManager.inst.Play(audioSource, AudioManager.inst.playerImpactSFX[Random.Range(0, AudioManager.inst.playerImpactSFX.Length)]);

        // GameUI.inst.UpdateMoneyText();

        //Cam Shake
        CameraEffects.inst.Shake(0.1f, 0.1f, 10.0f);

        //Visual color change.
        StartCoroutine(DamageVisualFlash());
        
        // … your existing money‐loss, shake, flash …
        if (GameManager.inst.damageDisruptEnabled)
        {
            GameManager.inst.TriggerDamageDisrupt();
        }

    }


    //Adds to the player's current health.

    //Called for the player to receive a new weapon.
    public void GiveWeapon (Weapon weapon)
    {
        //Can the player hold more weapons? If not, return.
        /*if(weapons.Count >= maxWeapons)
            return;*/

        weapons.Add(weapon);

        if (!WeaponUsageStats.WeaponUsageCounts.ContainsKey(weapon.displayName))
        {
            WeaponUsageStats.WeaponUsageCounts[weapon.displayName] = 1;
        }



        //Is this weapon not already linked to a player weapon visual?
        if(!weapon.onPlayerVisual)
        {
            //Create the new visual.
            weapon.onPlayerVisual = Instantiate(weapon.visualPrefab, weaponPos.transform.position + weapon.offsets.positionOffset, weaponPos.transform.rotation, weaponPos.transform);
            weapon.onPlayerVisual.transform.localEulerAngles += weapon.offsets.rotationOffset;
            weapon.onPlayerVisual.SetActive(false);
        }

        //Finally equip the weapon.
        EquipWeapon(weapon);
    }

    /// <summary>
/// Returns true if _every_ weapon has 0 in-clip AND 0 in reserve.
/// </summary>
    /// <summary>
/// Returns true if _every_ weapon has no rounds in the clip AND no reserve ammo.
/// </summary>
    public bool AreAllGunsEmpty()
    {
        foreach (var w in weapons)
        {
            // w.curAmmoInClip == bullets currently loaded
            // w.curAmmo       == bullets in reserve
            if (w.curAmmoInClip > 0 || w.curAmmo > 0)
                return false;
        }
        return true;
    }



    //Tries to equip a weapon in arsenal.
    //dir is the direction of change (1 = next, -1 = last).
    void TryChangeWeapon (int dir)
    {
        if (!GameManager.inst.waveInProgress || !canSwap)
            return;

        int nextIndex = weapons.IndexOf(curWeapon) + dir;

        if(nextIndex < 0)
            EquipWeapon(weapons[weapons.Count - 1]);
        else if(nextIndex >= weapons.Count)
            EquipWeapon(weapons[0]);
        else
            EquipWeapon(weapons[nextIndex]);

        GameUI.inst.UpdateEquippedWeaponIcons();
    }

    //Equips the requested weapon, changing values that are needed and visuals.
    public void EquipWeapon (Weapon weapon)
    {
        if(curWeapon == weapon)
        {
            return;
        }
        
        Weapon prevWeapon = curWeapon;
        curWeapon = weapon;

       
        WeaponUsageStats.RecordWeaponUsage(weapon.displayName);
        WeaponUsageStats.RecordWeaponSwitchCount();
        GameManager.inst.OnWeaponSwapped(weapon.displayName);
 

        //Disable previous weapon visual, and enable the new one.
        if(prevWeapon != null)
            if(prevWeapon.onPlayerVisual != null)
                prevWeapon.onPlayerVisual.SetActive(false);

        curWeapon.onPlayerVisual.SetActive(true);

        GameUI.inst.UpdateEquippedWeaponIcons();
    }

    //Drops a weapon on the ground.
    public void DropWeapon (Weapon weaponToDrop)
    {
        //Does the player have the weapon? If not, return.
        if(weapons.Find(x => x.displayName == weaponToDrop.displayName) == null)
            return;

        //Spawn the weapon pickup.
        GameObject droppedWeapon = Instantiate(weaponToDrop.droppedPickup, transform.position, Quaternion.identity, null);
        droppedWeapon.transform.position = new Vector3(transform.position.x, 0, transform.position.z);
        droppedWeapon.GetComponent<Pickup>().SetWeapon(weaponToDrop);

        //Remove the weapon from the player.
        weapons.Remove(weaponToDrop);

        GameUI.inst.UpdateEquippedWeaponIcons();
    }

    //Adds money to the player's total.
    public void AddMoney (int amount)
    {
        money += amount;
    }

    //Removes money from the player's total.
    public void RemoveMoney (int amount)
    {
        money -= amount;
    }

    //Returns a player's weapon based on the id.
    public Weapon GetWeapon (int weaponId)
    {
        return weapons.Find(x => x.id == weaponId);
    }

    //Gives an amount of ammo to a certain weapon.
    public void GiveAmmo (int weaponId, int ammoAmount)
    {
        Weapon weapon = GetWeapon(weaponId);

        if(weapon == null)
            return;

        weapon.curAmmo += ammoAmount;
    }

    //Refills the ammo of the requested ammo.
    public void RefillAmmo (int weaponId)
    {
        Weapon weapon = GetWeapon(weaponId);

        if(weapon == null)
            return;

        weapon.curAmmo = weapon.totalAmmo - weapon.clipSize;
        weapon.curAmmoInClip = weapon.clipSize;
    }

    public void ApplySpeedBoost(float multiplier, float duration = 10f)
    {
        // 1) First-time boost: actually increase the speed
        if (_boostCoroutine == null)
        {
            movement.speedMultiplier *= multiplier;
        }
        // 2) If there is already a boost running, stop its timer
        else
        {
            StopCoroutine(_boostCoroutine);
        }
        // 3) Start (or restart) the timer
        _boostCoroutine = StartCoroutine(SpeedBoostCoroutine(multiplier, duration));
    }

    private IEnumerator SpeedBoostCoroutine(float multiplier, float duration)
    {
        yield return new WaitForSeconds(duration);

        // Revert the speed and clear the coroutine handle
        movement.speedMultiplier /= multiplier;
        _boostCoroutine = null;
    }



    //Visually flashes the player red when damaged.
    IEnumerator DamageVisualFlash ()
    {
        yield return new WaitForEndOfFrame();
    }
}

public enum PlayerState
{
    Idle,
    Moving
}