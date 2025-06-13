using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PickupSpawner : MonoBehaviour
{
    public GameObject[] pickups;            //Array of available pickups to spawn.
    public GameObject[] pickupSpawnPoints;  //Positions to spawn the pickups at.

    public float averageSpawnRate;          //Average amount of time between spawning.
    public float spawnRateRandomness;       //Random range applied to average spawn rate.
    public static PickupSpawner inst;

    void Awake()
    {
        inst = this;
    }

    [Header("Speed Boost")]
    public GameObject speedPickupPrefab;   // drag your SpeedBoostPickup.prefab here

    [Header("Damage-Boost Pickup")]
    public GameObject damageBoostPrefab;

    [Header("New‐Weapon Assist")]
    public GameObject[] allWeaponPickupPrefabs;

    [Header("Ammo Settings")]
    public GameObject ammoPickupPrefab;
    
    // tracks whether we've already dropped an ammo pack for this dry spell
    private bool _ammoSpawnedThisDry = false;

    void Update()
    {
        if (!GameManager.inst.waveInProgress)
            return;

        // … your non-ammo spawn logic here …

        // only drop ammo the moment the player first runs completely dry:
        if (Player.inst.AreAllGunsEmpty())
        {
            if (!_ammoSpawnedThisDry)
            {
                SpawnOneAmmoPickup();
                _ammoSpawnedThisDry = true;
            }
        }
        else
        {
            // once they pick up or reload ammo, allow another drop next time they go dry
            _ammoSpawnedThisDry = false;
        }
    }

    void SpawnOneAmmoPickup()
    {
        if (ammoPickupPrefab == null) return;
        // use your existing spawn-position logic here (bounds or spawn-points)
        int spawnPoint = Random.Range(0, pickupSpawnPoints.Length);
        Vector3 offset = new Vector3(Random.Range(-1.0f, 1.0f), 0, Random.Range(-1.0f, 1.0f));

        Instantiate(ammoPickupPrefab, pickupSpawnPoints[spawnPoint].transform.position + offset, Quaternion.identity);
    }

    /// <summary>Pick a jittered point around one of your spawn points.</summary>
    public Vector3 GetRandomSpawnPosition()
    {
        // pick a random spawn‐point index
        int idx = Random.Range(0, pickupSpawnPoints.Length);
        Vector3 basePos = pickupSpawnPoints[idx].transform.position;
        // small random offset so they don’t all pile up
        Vector3 offset = new Vector3(
            Random.Range(-2f, 2f),
            0f,
            Random.Range(-2f, 2f)
        );
        return basePos + offset;
    }

    /// <summary>Drop exactly one speed‐boost/damage-boost prefabs at world‐space pos.</summary>
    public void SpawnOneSpeedBoost(Vector3 pos)
    {
        if (speedPickupPrefab == null) return;
        Instantiate(speedPickupPrefab, pos, Quaternion.identity);
    }

    public void SpawnOneDamageBoost(Vector3 pos)
    {
        Pool.Spawn(damageBoostPrefab, pos, Quaternion.identity);
    }
    
    public void SpawnOneWeaponPickup(GameObject weaponPickupPrefab, Vector3 pos)
    {
        Pool.Spawn(weaponPickupPrefab, pos, Quaternion.identity);
    }


}
