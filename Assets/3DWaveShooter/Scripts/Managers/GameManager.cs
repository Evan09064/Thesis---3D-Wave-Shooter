using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages general game states and times.
/// </summary>
public class GameManager : MonoBehaviour
{
    public LevelDataScriptableObject level; //Level data file that relates to this scene.
    public int waveCountdownTime = 5;       //How long is the countdown before wave starts?
    public float curWaveTime;

    public float roundAccuracy; //Measuring player accuracy each round
    public float overallAccuracy;//Measuring player accuracy of the entire game
    public int curWave;
    public int waveCount = 1;
    public bool gameIsActive = false;
    public float prevDistanceTraveled = 0f;
    public float prevTimeMoving = 0f;
    public float prevTimeIdle = 0f;
    private int prevWeaponSwitchCount = 0;
    private const float PI_CUT      = -0.1727f;
    private const float MVI_CUT     =  2.2081f;
    private const float DPM_CUT     =  6.1254f;
    private const float SWITCH_CUT  = 2f;



    [Header("Bools")]
    public bool waveInProgress;

    [Header("Wave Start Conditions")]

    [Header("Playstyle Countermeasures")]
    /// <summary>Prefab of a low‐cover crate to help Low‐Skill, High‐Move players.</summary>
    public GameObject rockCoverPrefab;
    public float     minSpawnDistance = 2f;
    public float     maxSpawnDistance = 5f;
    public float     checkRadius      = 1f;    // radius of sphere to test overlaps
    public float     rockScaleFactor  = 0.5f;  // shrink prefab by 50%

    
    public bool refillAmmoOnNewWave;

    //Instance
    public static GameManager inst;

    public PerformanceDataOverall overallData = new PerformanceDataOverall();

    private List<PerformanceDataPerWave> calibrationWaves = new List<PerformanceDataPerWave>();

    private PerformanceDataPerWave BuildWaveData(int waveNum) {
    // pull out all your currentDistance/currentTimeMoving/etc logic
    float currentDistance   = Player.inst.movement.totalDistanceTraveled;
    float currentTimeMoving = Player.inst.movement.totalTimeMoving;
    float currentTimeIdle   = Player.inst.movement.totalTimeIdle;
    float waveTime          = PerformanceStats.CurrentWaveCompletionTime;
    float waveDistance      = currentDistance - prevDistanceTraveled;
    float waveTimeMoving    = currentTimeMoving - prevTimeMoving;
    float waveTimeIdle      = currentTimeIdle   - prevTimeIdle;
    float waveAvgSpeed      = waveTimeMoving > 0f
                              ? waveDistance / waveTimeMoving
                              : 0f;
    float wavePathEff       = Player.inst.movement.PathEfficiency();
    int currentSwitchCount = WeaponUsageStats.WeaponSwitchCount;
    int waveSwitchCount    = currentSwitchCount - prevWeaponSwitchCount;

    // update the “prev” trackers
    prevDistanceTraveled = currentDistance;
    prevTimeMoving      = currentTimeMoving;
    prevTimeIdle        = currentTimeIdle;
    prevWeaponSwitchCount = currentSwitchCount;

    // build and return
    return new PerformanceDataPerWave {
        waveNumber         = waveNum,
        waveAccuracy       = PerformanceStats.RoundAccuracy,
        waveTime           = waveTime,
        waveDamageTaken    = PerformanceStats.RoundDamageTaken,
        waveSpeed          = waveAvgSpeed,
        waveDistanceTraveled = waveDistance,
        waveTimeIdle       = waveTimeIdle,
        waveTimeMoving     = waveTimeMoving,
        wavePathEfficiency = wavePathEff,
        waveWeaponSwitches = waveSwitchCount
    };
}



    void Awake ()
    {
        inst = this;
    }

    void Start ()
    {
        StartGame();
    }

    void Update ()
    {
        if(waveInProgress)
            curWaveTime += Time.deltaTime;

        if(Input.GetKeyDown(KeyCode.Escape))
            SceneManager.LoadScene(0);

        if (Input.GetKeyDown(KeyCode.C))
        {
        Debug.Log("[DEBUG] Spawning test cover crates");
        StartCoroutine(SpawnRockCover(4, 0.5f));
        }
    }

    //Called when the game starts.
    public void StartGame ()
    {
        PerformanceStats.OverallShotsFired = 0;
        PerformanceStats.OverallShotsHit = 0;
        PerformanceStats.RoundShotsFired = 0;
        PerformanceStats.RoundShotsHit = 0;
        PerformanceStats.CompletedWaves = 0;
        PerformanceStats.OverallDamageTaken = 0;
        PerformanceStats.RoundDamageTaken = 0;
        PerformanceStats.OverallWaveTime = 0f;
        Player.inst.movement.totalDistanceTraveled = 0f;
        Player.inst.movement.totalTimeMoving = 0f;
        Player.inst.movement.totalTimeIdle = 0f;
        Player.inst.isDead = false;
        WeaponUsageStats.WeaponSwitchCount = 0;
        WeaponUsageStats.ResetWeaponUsage();
        prevWeaponSwitchCount = 0;

        
    
        // Reset previous wave tracking variables:
        prevDistanceTraveled = 0f;
        prevTimeMoving = 0f;
        prevTimeIdle = 0f;
        SetNextWave();
    }

    //Called to start spawning the next wave.
    public void SetNextWave ()
    {
        ShopUI.inst.ToggleShop(false);
        curWaveTime = 0.0f;
        curWave = EnemySpawner.inst.nextWaveIndex + 1;
        Player.inst.canMove = false;

        GameUI.inst.StartCoroutine("SetWaveCountdownText", waveCountdownTime);
        Invoke("StartNextWave", waveCountdownTime + 1);
    }

    //Called after countdown is done from the method above.
    void StartNextWave ()
    {
        Player.inst.currentWeaponEquipTime = Time.time;
        gameIsActive = true;
        waveInProgress = true;

        //Same with ammo for all weapons.
        if(refillAmmoOnNewWave)
            foreach(Weapon weapon in Player.inst.weapons)
                Player.inst.RefillAmmo(weapon.id);

        PerformanceStats.ResetRoundStats();
        EnemySpawner.inst.SetNewWave();
        Player.inst.canMove = true;

        if (waveCount == 3)
        {
            EvaluatePlaystyleAndConfigure();
        }

    }

    //Called when the wave is over.
    public void EndWave ()
    {
        waveInProgress = false;
        PerformanceStats.EndWave();   // finishes timing + increments CompletedWaves

        // 1) build the wave object
        var waveData = BuildWaveData(waveCount);
        overallData.waveMetrics.Add(waveData);

        // 2) award completion bonus etc...
        if (waveCount == 1)    Player.inst.AddMoney(50);

        // 3) calibration logic:
        if (waveCount == 1 || waveCount == 2) 
        {
            calibrationWaves.Add(waveData);
        }
        //Was this the last wave? Then we win!
        if(EnemySpawner.inst.nextWaveIndex == EnemySpawner.inst.waves.Length)
            WinGame();
        //Otherwise enable the next wave button which triggers the next wave.
        else
        {
            GameUI.inst.nextWaveButton.SetActive(true);
            GameUI.inst.openShopButton.SetActive(true);
            waveCount++;
        }
    }

    //Called when all waves have been killed off.
    public void WinGame ()
    {
        gameIsActive = false;
        waveInProgress = false;
        roundAccuracy = PerformanceStats.OverallAccuracy;

        // Update current weapon usage before ending.
    
        // Log weapon usage time.
        foreach(var entry in WeaponUsageStats.WeaponUsageTimes)

        //Logging all performance metrics
        overallData.overallAccuracy = PerformanceStats.OverallAccuracy;
        overallData.totalRoundTime = PerformanceStats.OverallWaveTime;
        overallData.averageRoundTime = PerformanceStats.AverageWaveCompletionTime;
        overallData.averageSpeed = Player.inst.movement.AverageSpeed();
        overallData.completedWaves = PerformanceStats.CompletedWaves;
        overallData.overallDamageTaken = PerformanceStats.OverallDamageTaken;
        overallData.averageSpeed = Player.inst.movement.AverageSpeed();
        overallData.totalDistanceTraveled = Player.inst.movement.totalDistanceTraveled;
        overallData.totalTimeMoving = Player.inst.movement.totalTimeMoving;
        overallData.totalTimeIdle = Player.inst.movement.totalTimeIdle;
        overallData.overallWeaponSwitches = WeaponUsageStats.WeaponSwitchCount;
        overallData.pathEfficiency = Player.inst.movement.PathEfficiency();
        if(WeaponUsageStats.WeaponUsageCounts.ContainsKey("Pistol"))
            overallData.pistolUses = WeaponUsageStats.WeaponUsageCounts["Pistol"];
        if(WeaponUsageStats.WeaponUsageCounts.ContainsKey("Shotgun"))
            overallData.shotgunUses = WeaponUsageStats.WeaponUsageCounts["Shotgun"];
        if(WeaponUsageStats.WeaponUsageCounts.ContainsKey("SMG"))
            overallData.SMGUses = WeaponUsageStats.WeaponUsageCounts["SMG"];
        if(WeaponUsageStats.WeaponUsageCounts.ContainsKey("RocketLauncher"))
            overallData.rocketLauncherUses = WeaponUsageStats.WeaponUsageCounts["RocketLauncher"];
        if(WeaponUsageStats.WeaponUsageCounts.ContainsKey("FlameThrower"))
            overallData.flameThrowerUses = WeaponUsageStats.WeaponUsageCounts["FlameThrower"];
        if(WeaponUsageStats.WeaponUsageTimes.ContainsKey("Pistol"))
            overallData.pistolUseTime = WeaponUsageStats.WeaponUsageTimes["Pistol"];
        if(WeaponUsageStats.WeaponUsageTimes.ContainsKey("Shotgun"))
            overallData.shotgunUseTime = WeaponUsageStats.WeaponUsageTimes["Shotgun"];
        if(WeaponUsageStats.WeaponUsageTimes.ContainsKey("SMG"))
            overallData.SMGUseTime = WeaponUsageStats.WeaponUsageTimes["SMG"];
        if(WeaponUsageStats.WeaponUsageTimes.ContainsKey("RocketLauncher"))
            overallData.rocketLauncherUseTime = WeaponUsageStats.WeaponUsageTimes["RocketLauncher"];
        if(WeaponUsageStats.WeaponUsageTimes.ContainsKey("FlameThrower"))
            overallData.flameThrowerUseTime = WeaponUsageStats.WeaponUsageTimes["FlameThrower"];

        string json = JsonUtility.ToJson(overallData, true);

        string filePath = System.IO.Path.Combine(Application.persistentDataPath, "PerformanceData.json");
        System.IO.File.WriteAllText(filePath, json);
        Debug.Log("Performance data saved to: " + filePath);

        // Reset weapon usage stats.
        WeaponUsageStats.ResetWeaponUsage();
        //Set the win status to player prefs.
        PlayerPrefs.SetInt("LevelCompleted_" + level.sceneName, 1);
        GameUI.inst.SetEndGameText(true);
        Invoke("ReturnToMenu", 5.0f);
    }

    //Called when the player dies.
    public void LoseGame ()
    {
        gameIsActive = false;
        waveInProgress = false;
        //CaptureFinalWaveMetrics();
        roundAccuracy = PerformanceStats.OverallAccuracy;
        overallData.overallAccuracy = PerformanceStats.OverallAccuracy;
        overallData.totalRoundTime = PerformanceStats.OverallWaveTime;
        overallData.averageRoundTime = PerformanceStats.AverageWaveCompletionTime;
        overallData.averageSpeed = Player.inst.movement.AverageSpeed();
        overallData.completedWaves = PerformanceStats.CompletedWaves;
        overallData.overallDamageTaken = PerformanceStats.OverallDamageTaken;
        overallData.averageSpeed = Player.inst.movement.AverageSpeed();
        overallData.totalDistanceTraveled = Player.inst.movement.totalDistanceTraveled;
        overallData.totalTimeMoving = Player.inst.movement.totalTimeMoving;
        overallData.totalTimeIdle = Player.inst.movement.totalTimeIdle;
        overallData.overallWeaponSwitches = WeaponUsageStats.WeaponSwitchCount;
        overallData.pathEfficiency = Player.inst.movement.PathEfficiency();
        if(WeaponUsageStats.WeaponUsageCounts.ContainsKey("Pistol"))
            overallData.pistolUses = WeaponUsageStats.WeaponUsageCounts["Pistol"];
        if(WeaponUsageStats.WeaponUsageCounts.ContainsKey("Shotgun"))
            overallData.shotgunUses = WeaponUsageStats.WeaponUsageCounts["Shotgun"];
        if(WeaponUsageStats.WeaponUsageCounts.ContainsKey("SMG"))
            overallData.SMGUses = WeaponUsageStats.WeaponUsageCounts["SMG"];
        if(WeaponUsageStats.WeaponUsageCounts.ContainsKey("RocketLauncher"))
            overallData.rocketLauncherUses = WeaponUsageStats.WeaponUsageCounts["RocketLauncher"];
        if(WeaponUsageStats.WeaponUsageCounts.ContainsKey("FlameThrower"))
            overallData.flameThrowerUses = WeaponUsageStats.WeaponUsageCounts["FlameThrower"];
        if(WeaponUsageStats.WeaponUsageTimes.ContainsKey("Pistol"))
            overallData.pistolUseTime = WeaponUsageStats.WeaponUsageTimes["Pistol"];
        if(WeaponUsageStats.WeaponUsageTimes.ContainsKey("Shotgun"))
            overallData.shotgunUseTime = WeaponUsageStats.WeaponUsageTimes["Shotgun"];
        if(WeaponUsageStats.WeaponUsageTimes.ContainsKey("SMG"))
            overallData.SMGUseTime = WeaponUsageStats.WeaponUsageTimes["SMG"];
        if(WeaponUsageStats.WeaponUsageTimes.ContainsKey("RocketLauncher"))
            overallData.rocketLauncherUseTime = WeaponUsageStats.WeaponUsageTimes["RocketLauncher"];
        if(WeaponUsageStats.WeaponUsageTimes.ContainsKey("FlameThrower"))
            overallData.flameThrowerUseTime = WeaponUsageStats.WeaponUsageTimes["FlameThrower"];

        string json = JsonUtility.ToJson(overallData, true);

        string filePath = System.IO.Path.Combine(Application.persistentDataPath, "PerformanceData.json");
        System.IO.File.WriteAllText(filePath, json);
        Debug.Log("Performance data saved to: " + filePath);


        // Reset weapon usage stats.
        WeaponUsageStats.ResetWeaponUsage();
        
        GameUI.inst.SetEndGameText(false);
        Invoke("ReturnToMenu", 5.0f);
    }

    //Loads the menu scene.
    void ReturnToMenu ()
    {
        SceneManager.LoadScene(0);
    }

    private void EvaluatePlaystyleAndConfigure() 
    {
        var w1 = calibrationWaves[0];
        var w2 = calibrationWaves[1];

        // AGGREGATE for PI (totals or averages)
        overallData.totalRoundTime        = w1.waveTime + w2.waveTime;
        overallData.averageRoundTime      = (w1.waveTime + w2.waveTime) / 2f;
        overallData.overallAccuracy       = (w1.waveAccuracy + w2.waveAccuracy) / 2f;
        overallData.totalDistanceTraveled = w1.waveDistanceTraveled + w2.waveDistanceTraveled;

        float PI = ComputeCompositeSkill();
        Debug.Log("PI:" + PI);

        // SECONDARY METRICS
        float totalMoving = w1.waveTimeMoving + w2.waveTimeMoving;
        float totalIdle   = w1.waveTimeIdle   + w2.waveTimeIdle;
        float moveIdleRatio   = totalIdle > 0f ? totalMoving / totalIdle : 0f;
        Debug.Log("MvI" + moveIdleRatio);

        float totalDamage = w1.waveDamageTaken + w2.waveDamageTaken;
        float totalTime   = w1.waveTime + w2.waveTime;
        float damagePerMin = totalTime > 0f ? totalDamage / totalTime * 60f : 0f;
        Debug.Log("DPM" + damagePerMin);

        int totalSwitches = w1.waveWeaponSwitches + w2.waveWeaponSwitches;
        float switchRate  = totalTime > 0f ? totalSwitches / totalTime * 60f : totalSwitches;
        Debug.Log("SR" + switchRate);

          // HIGH / LOW FLAGS
        bool highSkill   = PI            >= PI_CUT;
        bool highMove    = moveIdleRatio >= MVI_CUT;
        bool highDamage  = damagePerMin  >= DPM_CUT;
        bool highSwitch  = switchRate    >= SWITCH_CUT;

        // CONFIGURE YOUR INTERVENTIONS
        ConfigureMoveIdleIntervention(highSkill, highMove);
        ConfigureDamageIntervention  (highSkill, highDamage);
        ConfigureSwitchIntervention  (highSkill, highSwitch);


        
    
}

private float ComputeCompositeSkill()
{
    // PCA-derived weights (sum to 1)
    const float wRT   = 0.315f;  // totalRoundTime
    const float wAcc  = 0.062f;  // overallAccuracy
    const float wDist = 0.308f;  // totalDistanceTraveled
    const float wART  = 0.315f;  // averageRoundTime

    // Pilot means & stddevs (first-two-wave aggregates)
    const float MEAN_RT   = 77.4362f, STD_RT   = 25.0749f;
    const float MEAN_ACC  = 0.7958f, STD_ACC  =  0.0790f;
    const float MEAN_DIST = 205.7390f, STD_DIST = 55.5616f;
    const float MEAN_ART  = 38.7181f, STD_ART  =  12.5374f;

    // Grab the four aggregated metrics from overallData
    float rt   = overallData.totalRoundTime;
    float acc  = overallData.overallAccuracy;
    float dist = overallData.totalDistanceTraveled;
    float art  = overallData.averageRoundTime;

    // Compute z-scores
    float zRT   = (rt   - MEAN_RT)   / STD_RT;
    float zAcc  = (acc  - MEAN_ACC)  / STD_ACC;
    float zDist = (dist - MEAN_DIST) / STD_DIST;
    float zART  = (art  - MEAN_ART)  / STD_ART;

    // Invert the “lower is better” metrics, sum with weights
    return -wRT  * zRT
         + wAcc  * zAcc
         + wDist * zDist
         - wART * zART;
}

#region --- Intervention Stubs ---
/// <summary>
/// Quadrant I: High Skill × High Move  → Disrupt (spread enemies)
/// Quadrant II: Low Skill × High Move  → Assist (spawn cover)
/// Quadrant III: Low Skill × Low Move  → Assist (spawn speed‐boost)
/// Quadrant IV: High Skill × Low Move  → Disrupt (dart enemies)
/// </summary>
private void ConfigureMoveIdleIntervention(bool highSkill, bool highMove)
{
    if (highMove && highSkill)
    {
        Debug.Log("moving + high skill quadrant hit");
        StartCoroutine(SpawnCoverSequence());
    }
    else if (highMove && !highSkill)
    {
        
        Debug.Log("moving + low skill quadrant hit");
        // Assist: spawn cover for a low‐skill mover
        StartCoroutine(SpawnCoverSequence());
    }
    else if (!highMove && !highSkill) 
    {
        Debug.Log("Idle + low skill quadrant hit");
        StartCoroutine(SpawnCoverSequence());
    }
    else
    {
        Debug.Log("Idle + high skill quadrant hit");
        StartCoroutine(SpawnCoverSequence());
    } 
    
     // (!highMove && highSkill)
}

/// <summary>
/// Quadrant I: High Skill × High Damage → Disrupt (slow on hit)
/// Quadrant II: Low Skill × High Damage → Assist (reduce spawn rate)
/// Quadrant III: Low Skill × Low Damage → Assist (damage buff pickups)
/// Quadrant IV: High Skill × Low Damage → Disrupt (spawn mini‐tank)
/// </summary>
private void ConfigureDamageIntervention(bool highSkill, bool highDamage)
{
    if (highDamage && highSkill) 
    {
        Debug.Log("High DT + high skill quadrant hit");
    }
    else if (highDamage && !highSkill)
    {
        Debug.Log("High DT + low skill quadrant hit");
    }  // slower spawns
    else if (!highDamage && !highSkill)
    {
        Debug.Log("low DT + low skill quadrant hit");
    }
    else Debug.Log("low DT + high skill quadrant hit");
}

/// <summary>
/// Quadrant I: High Skill × High Switch → Disrupt (temporary lockout)
/// Quadrant II: Low Skill × High Switch → Assist (swap bonus)
/// Quadrant III: Low Skill × Low Switch → Assist (free ammo)
/// Quadrant IV: High Skill × Low Switch → Disrupt (limit ammo)
/// </summary>
private void ConfigureSwitchIntervention(bool highSkill, bool highSwitch)
{
    if (highSwitch && highSkill) 
    {
        Debug.Log("High SW + high skill quadrant hit");
    }
    else if (highSwitch && !highSkill)
    {
        Debug.Log("High SW + low skill quadrant hit");
    }
    else if (!highSwitch && !highSkill)
    {
        Debug.Log("Low SW + low skill quadrant hit");
    }
    else Debug.Log("Low SW + high skill quadrant hit");
    
}
#endregion

/// <summary>
/// Spawns a small cluster of cover crates around the player, one every `interval` seconds.
/// </summary>
private IEnumerator SpawnRockCover(int count, float delay = 2.0f)
{
    Debug.Log($"[CoverSpawn] rockCoverPrefab is {(rockCoverPrefab==null?"NULL":"OK")}");
    if (rockCoverPrefab == null) yield break;

    Vector3 playerPos = Player.inst.transform.position;
    int spawned = 0, attempts = 0;

    while (spawned < count && attempts < count * 5)
    {
        attempts++;
        Debug.Log($"[CoverSpawn] Attempt #{attempts}");

        // 1) pick a random point in a donut
        float ang  = Random.Range(0f, 2*Mathf.PI);
        float dist = Random.Range(minSpawnDistance, maxSpawnDistance);
        Vector3 candidate = playerPos + new Vector3(
            Mathf.Cos(ang)*dist,
            0f,
            Mathf.Sin(ang)*dist
        );
        Debug.Log($"  Candidate XZ: {candidate.x:F2}, {candidate.z:F2}");

        // 2) raycast down to ground
        if (!Physics.Raycast(candidate + Vector3.up*10f,
                             Vector3.down,
                             out RaycastHit hit, 
                             20f))
        {
            Debug.Log("   ✗ No ground below");
            continue;
        }
        candidate.y = hit.point.y;
        Debug.Log($"   ✓ Ground at Y={candidate.y:F2}");

        // 3) overlap‐sphere to see if anything’s in the way
        Vector3 overlapCenter = candidate + Vector3.up * (checkRadius * 0.5f);
        Collider[] hits = Physics.OverlapSphere(overlapCenter, checkRadius);
        bool blocked = false;
        foreach (var col in hits)
        {
            // ignore triggers entirely
            if (col.isTrigger) 
            {
                continue;
            }

            // ignore the ground by tag
            if (col.gameObject.CompareTag("Ground"))
            {
                Debug.Log("ground");
                continue;
            }

            // otherwise it’s a real blocker (player, wall, enemy, etc.)
            blocked = true;
            Debug.Log("real blocker");
            break;
        }
        if (blocked)
        {
            Debug.Log("No luck");
            continue;
        }
            

        // 4) success! spawn and scale
        var rock = Instantiate(rockCoverPrefab, candidate, Quaternion.identity);
        rock.transform.localScale *= rockScaleFactor;
        Debug.Log("   → Spawned rock cover here");
        spawned++;

        yield return new WaitForSeconds(delay);
    }

    if (spawned == 0)
        Debug.LogWarning("[CoverSpawn] Couldn’t place any cover rocks.");
    else
        Debug.Log($"[CoverSpawn] Spawned {spawned}/{count} rocks after {attempts} tries");
}

/// <summary>
/// 1) Spawn 4 rocks at t=0 (one every 0.5 s),  
/// 2) then wait 15 s and spawn 2 rocks (one every 0.5 s),  
/// 3) repeat that 2-rock refresh three times.
/// </summary>
private IEnumerator SpawnCoverSequence()
{
    // 1) initial burst of 4
    yield return SpawnRockCover(4, 2.0f);

    // 2) three refreshes
    for (int refresh = 0; refresh < 3; refresh++)
    {
        yield return new WaitForSeconds(15f);
        yield return SpawnRockCover(2, 0.5f);
    }
}




}