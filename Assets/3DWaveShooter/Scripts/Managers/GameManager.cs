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
// Optionally, you can store other metrics if needed.



    [Header("Bools")]
    public bool waveInProgress;

    [Header("Wave Start Conditions")]
    public bool refillHealthOnNewWave;
    public bool refillAmmoOnNewWave;

    //Instance
    public static GameManager inst;

    public PerformanceDataOverall overallData = new PerformanceDataOverall();


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
        //If we refill health, do it.
        if(refillHealthOnNewWave)
            Player.inst.curHp = Player.inst.maxHp;

        //Same with ammo for all weapons.
        if(refillAmmoOnNewWave)
            foreach(Weapon weapon in Player.inst.weapons)
                Player.inst.RefillAmmo(weapon.id);

        PerformanceStats.ResetRoundStats();
        EnemySpawner.inst.SetNewWave();
        Player.inst.canMove = true;
    }

    private void CaptureFinalWaveMetrics() {
    
    PerformanceStats.EndWave();
    // Calculate per-wave metrics:
    float currentDistance = Player.inst.movement.totalDistanceTraveled;
    float currentTimeMoving = Player.inst.movement.totalTimeMoving;
    float currentTimeIdle = Player.inst.movement.totalTimeIdle;
    float waveTime = PerformanceStats.CurrentWaveCompletionTime;

    float waveDistance = currentDistance - prevDistanceTraveled;
    float waveTimeMoving = currentTimeMoving - prevTimeMoving;
    float waveTimeIdle = currentTimeIdle - prevTimeIdle;
    float waveAverageSpeed = waveTimeMoving > 0 ? waveDistance / waveTimeMoving : 0f;
    float wavePathEfficiency = Player.inst.movement.PathEfficiency();

    // Create a PerformanceDataPerWave object and add it to the overall list:
    PerformanceDataPerWave finalWaveData = new PerformanceDataPerWave();
    finalWaveData.waveNumber = waveCount;  
    finalWaveData.waveAccuracy = PerformanceStats.RoundAccuracy;
    finalWaveData.waveTime = waveTime;
    finalWaveData.waveDamageTaken = PerformanceStats.RoundDamageTaken;
    finalWaveData.waveSpeed = waveAverageSpeed;
    finalWaveData.waveDistanceTraveled = waveDistance;
    finalWaveData.waveTimeIdle = waveTimeIdle;
    finalWaveData.waveTimeMoving = waveTimeMoving;
    finalWaveData.wavePathEfficiency = wavePathEfficiency;

    overallData.waveMetrics.Add(finalWaveData);
}


    //Called when the wave is over.
    public void EndWave ()
    {
        waveInProgress = false;

        // Calculate per-wave metrics:
        float currentDistance = Player.inst.movement.totalDistanceTraveled;
        float currentTimeMoving = Player.inst.movement.totalTimeMoving;
        float currentTimeIdle = Player.inst.movement.totalTimeIdle;

        float waveDistance = currentDistance - prevDistanceTraveled;
        float waveTimeMoving = currentTimeMoving - prevTimeMoving;
        float waveTimeIdle = currentTimeIdle - prevTimeIdle;
        float waveAverageSpeed = waveTimeMoving > 0 ? waveDistance / waveTimeMoving : 0f;
        float wavePathEfficiency = Player.inst.movement.PathEfficiency(); // Assuming this method calculates using the wave start position set at round start.

        // Log per-wave metrics:
        Debug.Log("Wave Distance Traveled: " + waveDistance + "Unity units/meters");
        Debug.Log("Wave Average Speed (while moving): " + waveAverageSpeed + "Unity units/meters per second");
        Debug.Log("Wave Path Efficiency: " + wavePathEfficiency);
        Debug.Log("Wave Time Moving: " + waveTimeMoving + " seconds; Wave Time Idle: " + waveTimeIdle + " seconds");

        prevDistanceTraveled = currentDistance;
        prevTimeMoving = currentTimeMoving;
        prevTimeIdle = currentTimeIdle;

        roundAccuracy = PerformanceStats.RoundAccuracy;
        PerformanceStats.EndWave();

        PerformanceDataPerWave waveData = new PerformanceDataPerWave();
        waveData.waveNumber = waveCount;  // current wave number
        waveData.waveAccuracy = PerformanceStats.RoundAccuracy;
        waveData.waveTime = PerformanceStats.CurrentWaveCompletionTime;
        waveData.waveDamageTaken = PerformanceStats.RoundDamageTaken;
        waveData.waveSpeed = waveAverageSpeed;
        waveData.waveDistanceTraveled = waveDistance;
        waveData.waveTimeIdle = waveTimeIdle;
        waveData.waveTimeMoving = waveTimeMoving;
        waveData.wavePathEfficiency = wavePathEfficiency;

        overallData.waveMetrics.Add(waveData);

        Debug.Log("Wave Accuracy: " + PerformanceStats.RoundAccuracy);
        Debug.Log("Wave Completed in: " + PerformanceStats.CurrentWaveCompletionTime + " Seconds");
        Debug.Log("Damage taken this wave: " + PerformanceStats.RoundDamageTaken + " HP's");

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
        Debug.Log("____________GAME OVER___________");
        Debug.Log("Game ended. Accuracy: " + PerformanceStats.OverallAccuracy);
        Debug.Log("Overall time: " + PerformanceStats.OverallWaveTime);
        Debug.Log("Average time per wave: " + PerformanceStats.AverageWaveCompletionTime);
        Debug.Log("Overall Damage Taken: " + PerformanceStats.OverallDamageTaken);
        Debug.Log("Average Speed (while moving): " + Player.inst.movement.AverageSpeed());
        Debug.Log("Total Distance Traveled: " + Player.inst.movement.totalDistanceTraveled);
        Debug.Log("Path Efficiency: " + Player.inst.movement.PathEfficiency());
        Debug.Log("Time Moving: " + Player.inst.movement.totalTimeMoving + " seconds; Time Idle: " + Player.inst.movement.totalTimeIdle + " seconds");

        foreach(var entry in WeaponUsageStats.WeaponUsageCounts)
        {
            Debug.Log("Weapon: " + entry.Key + " used " + entry.Value + " times.");
        }

        // Update current weapon usage before ending.
    
        // Log weapon usage time.
        foreach(var entry in WeaponUsageStats.WeaponUsageTimes)
        {
            Debug.Log("Weapon: " + entry.Key + " used for " + entry.Value + " seconds.");
        }

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
        CaptureFinalWaveMetrics();
        roundAccuracy = PerformanceStats.OverallAccuracy;
        Debug.Log("____________GAME OVER___________");
        Debug.Log("Game ended. Accuracy: " + PerformanceStats.OverallAccuracy);
        Debug.Log("Overall time: " + PerformanceStats.OverallWaveTime);
        Debug.Log("Average time per wave: " + PerformanceStats.AverageWaveCompletionTime);
        Debug.Log("Overall Damage Taken: " + PerformanceStats.OverallDamageTaken);
        Debug.Log("Average Speed (while moving): " + Player.inst.movement.AverageSpeed());
        Debug.Log("Total Distance Traveled: " + Player.inst.movement.totalDistanceTraveled);
        Debug.Log("Path Efficiency: " + Player.inst.movement.PathEfficiency());
        Debug.Log("Time Moving: " + Player.inst.movement.totalTimeMoving + " seconds; Time Idle: " + Player.inst.movement.totalTimeIdle + " seconds");

        foreach(var entry in WeaponUsageStats.WeaponUsageCounts)
        {
            Debug.Log("Weapon: " + entry.Key + " used " + entry.Value + " times.");
        }

        // Update current weapon usage before ending.
    
        // Log weapon usage time.
        foreach(var entry in WeaponUsageStats.WeaponUsageTimes)
        {
            Debug.Log("Weapon: " + entry.Key + " used for " + entry.Value + " seconds.");
        }
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
}