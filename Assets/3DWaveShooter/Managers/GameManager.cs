using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;        
using System.Linq;
using URandom = UnityEngine.Random;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;


/// <summary>
/// Manages general game states and times.
/// </summary>
public class GameManager : MonoBehaviour
{
    public LevelDataScriptableObject level; 
    public int waveCountdownTime = 5;    
    public float curWaveTime;
    public float overallAccuracy;
    public int curWave;
    public int waveCount = 1;
    public bool gameIsActive = false;
    public float prevDistanceTraveled = 0f;
    public float prevTimeMoving = 0f;
    public float prevTimeIdle = 0f;
    private int prevWeaponSwitchCount = 0;
    private Dictionary<string,int>   prevUsageCounts = new Dictionary<string,int>();
    private Dictionary<string,float> prevUsageTimes  = new Dictionary<string,float>();
    private const float PI_CUT = 0f;
    private const float MVI_CUT = 2.2081f;
    private const float DPM_CUT = 6.1254f;
    private const float SWITCH_CUT = 2f;

    // at the top of GameManager
    private Coroutine speedBoostRoutine;
    private Coroutine damagePickupRoutine;
    private Coroutine switchLockRoutine;
    private Coroutine _dartRoutine;
    private Coroutine _damageDisruptRoutine;



    [Header("Bools")]
    public bool waveInProgress;
    private bool swapBonusEnabled;

    [Header("Wave Start Conditions")]

    [Header("Playstyle Countermeasures")]
    /// <summary>Prefab of a low‐cover crate to help Low‐Skill, High‐Move players.</summary>
    public GameObject rockCoverPrefab;
    public float minSpawnDistance = 2f;
    public float maxSpawnDistance = 5f;
    public float checkRadius = 1f;   
    public float rockScaleFactor = 0.6f; 

    [Tooltip("Which layers to treat as obstacles when placing cover.")]
    public LayerMask obstacleMask;          
    [Tooltip("Smaller test sphere radius to cut down physics work.")]
    private Collider[] _overlapResults = new Collider[8]; 
    public bool refillAmmoOnNewWave;

    [Header("Dart Disruptor (reuse existing enemy)")]

    [Tooltip("Which enemy type to turn into a 'dart'")]
    public GameObject dartEnemyType;          
    [Tooltip("Multiplier applied to moveSpeed for dart enemies")]
    public float dartSpeedMultiplier = 2.0f;
    [Tooltip("Color to tint dart enemies so they stand out")]
    public Color dartColor = Color.green;

    [Tooltip("Which enemy prefab to replace when disrupting")]
    public GameObject fastEnemyPrefab;         

    [HideInInspector]
    public bool dartReplaceEnabled = false;     
    private List<GameObject> _activeDarts = new List<GameObject>();
    public static GameManager inst;

    public PerformanceDataOverall overallData = new PerformanceDataOverall();

    // waves 1 & 2 → initial baseline
    private List<PerformanceDataPerWave> calibrationWavesInitial = new List<PerformanceDataPerWave>();

    // waves 3 & 4 → for re-evaluate before wave 5
    private List<PerformanceDataPerWave> calibrationWavesSecondary = new List<PerformanceDataPerWave>();


    private List<GameObject> spawnedRocks = new List<GameObject>();

    [Header("Damage Disruptor")]

    [Tooltip("If true, taking damage slows the player + shows blood effect")]
    public bool damageDisruptEnabled = false;
    public float damageSlowMultiplier = 0.60f;   // half speed
    public float damageSlowDuration = 3f;     // in seconds

    [Header("Damage-Assist: Spawn Rate Reduction")]

    [Tooltip("Multiplier to increase the spawn interval (e.g. 1.2 = +20%)")]
    public float spawnRateMultiplier = 1.75f;

    [Header("Mini-Tank Disrupt")]

    [Tooltip("Prefab for the heavy mini-tank")]
    public GameObject miniTankPrefab;

    [Header("Switch Disruptor")]

    [Tooltip("How long to lock out swapping (seconds)")]
    public float switchLockDuration = 9f;
    [Tooltip("Message to display when swap is locked")]
    public string switchLockMessage = "Weapon Swapping Disabled Temporarily!";

    public string damageTakenMessage = "Avoid damage!";
    public float messageDisplayTime = 4f;

    // When true, every swap triggers an 8 s lock.
    private bool perSwapLockEnabled = false;

    // The weapon to debuff for the whole wave.
    public string debuffedWeaponName = null;

    [Header("Switch Helper")]
    public float swapBonusDamageMultiplier = 1.75f;
    public float swapBonusDuration = 10f;           


    [Header("Anti-Social (Spread-Out) Settings")]
    
    [Tooltip("When GameManager.inst.spreadEnemiesEnabled == true, enemies will push away from each other.")]
    public bool spreadEnemiesEnabled = false;
    public float separationRadius = 2.5f;       
    [Tooltip("Enemies closer than this distance will be pushed apart.")]
    public float desiredSeparation = 2.5f;         // ideal minimum distance between enemies
    [Tooltip("Strength of the repulsion force.")]
    public float antiSocialCoefficient = 1.0f;     // multiplier for repulsion
    private HashSet<string> swapBonusGrantedFor = new HashSet<string>();
    private bool isPaused = false;

    [Header("Pause")]
    public Text pauseGameText;

    [Header("Survey Popup")]
    [Tooltip("Reference to the SurveyPopupController in the scene")]
    public SurveyPopupController surveyPopup;

    [Tooltip("Qualtrics URL for the pre-game consent survey")]
    public string preGameSurveyURL;

    [Tooltip("Qualtrics URL for the mid-game wave survey")]
    public string midGameSurveyURL;

    [Tooltip("Qualtrics URL for the post-game final survey")]
    public string postGameSurveyURL;
    private string sessionID;

    


    private PerformanceDataPerWave BuildWaveData(int waveNum)
    {
        float currentDistance = Player.inst.movement.totalDistanceTraveled;
        float currentTimeMoving = Player.inst.movement.totalTimeMoving;
        float currentTimeIdle = Player.inst.movement.totalTimeIdle;
        float waveTime = PerformanceStats.CurrentWaveCompletionTime;
        float waveDistance = currentDistance - prevDistanceTraveled;
        float waveTimeMoving = currentTimeMoving - prevTimeMoving;
        float waveTimeIdle = currentTimeIdle - prevTimeIdle;
        float waveAvgSpeed = waveTimeMoving > 0f
                                  ? waveDistance / waveTimeMoving
                                  : 0f;
        int currentSwitchCount = WeaponUsageStats.WeaponSwitchCount;
        int waveSwitchCount = currentSwitchCount - prevWeaponSwitchCount;

        var waveUsageCounts = new Dictionary<string, int>();
        foreach (var kv in WeaponUsageStats.WeaponUsageCounts)
            waveUsageCounts[kv.Key] = kv.Value - (prevUsageCounts.ContainsKey(kv.Key) ? prevUsageCounts[kv.Key] : 0);

        var waveUsageTimes = new Dictionary<string, float>();
        foreach (var kv in WeaponUsageStats.WeaponUsageTimes)
            waveUsageTimes[kv.Key] = kv.Value - (prevUsageTimes.ContainsKey(kv.Key) ? prevUsageTimes[kv.Key] : 0f);

        var timeArray = waveUsageTimes
            .Select(kv => new WeaponTime { weaponName = kv.Key, secondsEquipped = kv.Value })
            .ToArray();

        prevDistanceTraveled = currentDistance;
        prevTimeMoving = currentTimeMoving;
        prevTimeIdle = currentTimeIdle;
        prevWeaponSwitchCount = currentSwitchCount;
        prevUsageCounts = new Dictionary<string, int>(WeaponUsageStats.WeaponUsageCounts);
        prevUsageTimes = new Dictionary<string, float>(WeaponUsageStats.WeaponUsageTimes);

        return new PerformanceDataPerWave
        {
            waveNumber = waveNum,
            waveAccuracy = PerformanceStats.RoundAccuracy,
            waveTime = waveTime,
            waveDamageTaken = PerformanceStats.RoundDamageTaken,
            waveSpeed = waveAvgSpeed,
            waveDistanceTraveled = waveDistance,
            waveTimeIdle = waveTimeIdle,
            waveTimeMoving = waveTimeMoving,
            waveWeaponSwitches = waveSwitchCount,
            waveWeaponUsageTimes = timeArray
        };
    }



    void Awake()
    {
        inst = this;
        // Generate a unique session ID
        sessionID = Guid.NewGuid().ToString();
    }

    void Start()
    {
        surveyPopup.surveyURL = $"{preGameSurveyURL}?userID={sessionID}&phase=pre";
        surveyPopup.isFinalSurvey = false; 
        surveyPopup.Show();
    }

   void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            if (waveInProgress == true)
            {
                TogglePause();
            }
        }

        if (!isPaused && waveInProgress)
        {
            curWaveTime += Time.deltaTime;
        }
    }

    private void TogglePause()
    {
      isPaused = !isPaused;

     
      Time.timeScale = isPaused ? 0f : 1f;

      // show or hide pause UI
      pauseGameText.gameObject.SetActive(isPaused);

      Player.inst.canMove = !isPaused;
      Player.inst.canSwap = !isPaused;  
      Player.inst.canAttack = !isPaused;
      GameUI.inst.openShopButton.SetActive(false);
    }

    //Called when the game starts.
    public void StartGame()
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
        WeaponUsageStats.WeaponSwitchCount = 0;
        WeaponUsageStats.ResetWeaponUsage();
        prevWeaponSwitchCount = 0;
        calibrationWavesInitial.Clear();
        calibrationWavesSecondary.Clear();
        overallData.waveMetrics.Clear();
        prevUsageCounts.Clear();
        prevUsageTimes.Clear();
        prevDistanceTraveled = 0f;
        prevTimeMoving = 0f;
        prevTimeIdle = 0f;
        SetNextWave();
        GameUI.inst.openShopButton.SetActive(false);
    }

    //Called to start spawning the next wave.
    public void SetNextWave()
    {
        ShopUI.inst.
        ToggleShop(false);
        GameUI.inst.openShopButton.SetActive(false);
        curWaveTime = 0.0f;
        curWave = EnemySpawner.inst.nextWaveIndex + 1;
        Player.inst.canMove = false;
        Player.inst.canAttack = false;
        Player.inst.canSwap = false;

        GameUI.inst.StartCoroutine("SetWaveCountdownText", waveCountdownTime);
        Invoke("StartNextWave", waveCountdownTime + 1);
    }

    //Called after countdown is done from the method above.
    void StartNextWave()
    {
        Player.inst.currentWeaponEquipTime = Time.time;
        gameIsActive = true;
        waveInProgress = true;
        GameUI.inst.openShopButton.SetActive(false);

        //Same with ammo for all weapons.
        if (refillAmmoOnNewWave)
            foreach (Weapon weapon in Player.inst.weapons)
                Player.inst.RefillAmmo(weapon.id);

        PerformanceStats.ResetRoundStats();
        swapBonusGrantedFor.Clear();
        EnemySpawner.inst.SetNewWave();
        Player.inst.canMove = true;
        Player.inst.canSwap = true;
        Player.inst.canAttack = true;

        // wave 3: first evaluation on waves 1–2
        if (waveCount == 3 || waveCount == 4)
        {
            EvaluatePlaystyleAndConfigure(calibrationWavesInitial);
        }
        // wave 5: re-evaluate on waves 3–4
        else if (waveCount == 5)
        {
            EvaluatePlaystyleAndConfigure(calibrationWavesSecondary);
        }


    }

    //Called when the wave is over.
    public void EndWave()
    {
        waveInProgress = false;
        PerformanceStats.EndWave();   // finishes timing + increments CompletedWaves
        CleanupCoverRocks();

        var allPickups = FindObjectsByType<Pickup>(FindObjectsSortMode.None);
        foreach (var pickup in allPickups)
            Destroy(pickup.gameObject);

        // 1) build the wave object
        var waveData = BuildWaveData(waveCount);
        overallData.waveMetrics.Add(waveData);

        // 2) award completion bonus etc...
        if (waveCount == 1) Player.inst.AddMoney(50);
        if (waveCount == 3) Player.inst.AddMoney(50);


        //  ─── collect waves 1 & 2 ───────────────────────────
        if (waveCount == 1 || waveCount == 2)
        {
            calibrationWavesInitial.Add(waveData);
        }
        //  ─── collect waves 3 & 4 ───────────────────────────
        else if (waveCount == 3 || waveCount == 4)
        {
            calibrationWavesSecondary.Add(waveData);
        }

        // build the survey URL with both userID and waveNumber
        bool isFinal = EnemySpawner.inst.nextWaveIndex == EnemySpawner.inst.waves.Length;
        surveyPopup.surveyURL = $"{midGameSurveyURL}?userID={sessionID}&waveNumber={waveCount}&phase=mid";
        surveyPopup.isFinalSurvey = false; 
        surveyPopup.Show();
    }

    public void OnSurveyContinue()
    {
        int maxWaves = EnemySpawner.inst.waves.Length;

        if (waveCount < maxWaves)
        {
            waveCount++;
            GameUI.inst.nextWaveButton.SetActive(true);
            GameUI.inst.openShopButton.SetActive(true);
        }
        else
        {
            surveyPopup.surveyURL = $"{postGameSurveyURL}?userID={sessionID}&phase=post";
            surveyPopup.isFinalSurvey = true;
            surveyPopup.Show();
        }
    }

    public void OnFinalSurveyContinue()
    {
        // After final survey, return to menu or next step
        WinGame();
    }
    
    IEnumerator SendMetricsToServer(string json)
    {
        var url = "https://tested-mercury-akubra.glitch.me/collect-metrics";
        using var req = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        req.uploadHandler   = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success)
            Debug.LogError($"Metrics POST failed: {req.error}");
        else
            Debug.Log("Metrics successfully sent!");
    }

    //Called when all waves have been killed off.
    public void WinGame()
    {
        gameIsActive = false;
        waveInProgress = false;
        GameUI.inst.openShopButton.SetActive(false);

        //Logging all performance metrics
        overallData.sessionID = sessionID;
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
        overallData.totalWeaponUsageTimes = WeaponUsageStats.WeaponUsageTimes
            .Select(kv => new WeaponTime { weaponName = kv.Key, secondsEquipped = kv.Value })
            .ToArray();


        string json = JsonUtility.ToJson(overallData, true);

        string filePath = System.IO.Path.Combine(Application.persistentDataPath, "PerformanceData.json");
        System.IO.File.WriteAllText(filePath, json);
        Debug.Log("Performance data saved to: " + filePath);

        StartCoroutine(SendMetricsToServer(json));

        // Reset weapon usage stats.
        WeaponUsageStats.ResetWeaponUsage();
        //Set the win status to player prefs.
        PlayerPrefs.SetInt("LevelCompleted_" + level.sceneName, 1);
        GameUI.inst.SetEndGameText(true);
        Invoke("ReturnToMenu", 5.0f);
    }
    
    //Loads the menu scene.
    void ReturnToMenu()
    {
        SceneManager.LoadScene(0);
    }

    private void EvaluatePlaystyleAndConfigure(List<PerformanceDataPerWave> window)
    {
        var w1 = window[0];
        var w2 = window[1];

        // 1) Composite Skill (PI) on those two waves
        float PI = ComputeCompositeSkill(w1, w2);
        bool highSkill = PI >= PI_CUT;

        // 2) Secondary metrics on the same two waves
        float totalMoving   = w1.waveTimeMoving   + w2.waveTimeMoving;
        float totalIdle     = w1.waveTimeIdle     + w2.waveTimeIdle;
        float totalDamage   = w1.waveDamageTaken  + w2.waveDamageTaken;
        float totalTime     = w1.waveTime         + w2.waveTime;
        int   totalSwitches = w1.waveWeaponSwitches + w2.waveWeaponSwitches;

        float moveIdleRatio = totalIdle > 0f ? totalMoving / totalIdle : 0f;
        float damagePerMin  = totalTime > 0f ? totalDamage / totalTime * 60f : 0f;
        float switchRate    = totalTime > 0f ? totalSwitches / totalTime * 60f : totalSwitches;

        bool highMove   = moveIdleRatio >= MVI_CUT;
        bool highDamage = damagePerMin  >= DPM_CUT;
        bool highSwitch = switchRate    > SWITCH_CUT;

        ConfigureMoveIdleIntervention(highSkill, highMove);
        ConfigureDamageIntervention(highSkill, highDamage);
        ConfigureSwitchIntervention(highSkill, highSwitch);
    }

    private float ComputeCompositeSkill(PerformanceDataPerWave w1, PerformanceDataPerWave w2)
    {
        // Domain-derived weights (sum to 1)
        const float wRT = 0.25f;  // totalRoundTime
        const float wAcc = 0.30f;  // overallAccuracy
        const float wDist = 0.20f;  // totalDistanceTraveled
        const float wART = 0.25f;  // averageRoundTime

        // Pilot means & stddevs (first-two-wave aggregates)
        const float MEAN_RT = 77.4362f, STD_RT = 25.0749f;
        const float MEAN_ACC = 0.7958f, STD_ACC = 0.0790f;
        const float MEAN_DIST = 205.7390f, STD_DIST = 55.5616f;
        const float MEAN_ART = 38.7181f, STD_ART = 12.5374f;

        // 1) aggregate two‐wave window
        float totalRT   = w1.waveTime + w2.waveTime;
        float avgRT     = totalRT / 2f;
        float totalAcc  = w1.waveAccuracy + w2.waveAccuracy;
        float avgAcc    = totalAcc  / 2f;
        float totalDist = w1.waveDistanceTraveled + w2.waveDistanceTraveled;
        float avgArt    = avgRT;
        
        // 2) compute z-scores (use means/stddevs on the same scale)
        float zRT   = (avgRT  - MEAN_RT)   / STD_RT;
        float zAcc  = (avgAcc - MEAN_ACC)  / STD_ACC;
        float zDist = (totalDist/2f - MEAN_DIST) / STD_DIST;
        float zART  = (avgArt - MEAN_ART)   / STD_ART;

        // 3) composite index
        return -wRT * zRT
            + wAcc * zAcc
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
        // 1) Tear down whatever was running before
        if (speedBoostRoutine != null)
        {
            StopCoroutine(speedBoostRoutine);
            speedBoostRoutine = null;
        }
        CleanupDarts();
        CleanupCoverRocks();
        spreadEnemiesEnabled = false;

        // 2) Dispatch exactly one intervention
        if (highMove && highSkill)
        {
            Debug.Log("Quadrant I: High-Skill + High-Move → Disrupt: Pairwise spread");
            spreadEnemiesEnabled = true;
        }
        else if (highMove && !highSkill)
        {
            Debug.Log("Quadrant II: Low-Skill + High-Move → Assist: Spawn cover");
            StartCoroutine(SpawnCoverSequence());
        }
        else if (!highMove && !highSkill)
        {
            Debug.Log("Quadrant III: Low-Skill + Low-Move → Assist: Speed-boost pickups");
            speedBoostRoutine = StartCoroutine(SpeedBoostSpawner());
        }
        else // (!highMove && highSkill)
        {
            Debug.Log("Quadrant IV: High-Skill + Low-Move → Disrupt: Dart enemies");
            EnableDartReplace();
        }
    }

    /// <summary>
    /// Quadrant I: High Skill × High Damage → Disrupt (slow on hit)
    /// Quadrant II: Low Skill × High Damage → Assist (reduce spawn rate)
    /// Quadrant III: Low Skill × Low Damage → Assist (damage buff pickups)
    /// Quadrant IV: High Skill × Low Damage → Disrupt (spawn mini‐tank)
    /// </summary>
    private void ConfigureDamageIntervention(bool highSkill, bool highDamage)
    {
        // ensure any running effect is stopped & speed reset
        damageDisruptEnabled = false;
        if (_damageDisruptRoutine != null)
        {
            StopCoroutine(_damageDisruptRoutine);
            _damageDisruptRoutine = null;
            Player.inst.movement.speedMultiplier = 1f;
        }
        // stop any damage‐pickup spawner
        if (damagePickupRoutine != null)
        {
            StopCoroutine(damagePickupRoutine);
            damagePickupRoutine = null;
        }

        if (highDamage && highSkill)
        {
            damageDisruptEnabled = true;
            Debug.Log("Enabling damage disrupt");
        }
        else if (highDamage && !highSkill)
        {
            ApplySpawnRateReduction();
            Debug.Log("High DT + low skill quadrant hit");
        }  // slower spawns
        else if (!highDamage && !highSkill)
        {
            Debug.Log("low DT + low skill quadrant hit");
            damagePickupRoutine = StartCoroutine(DamagePickupSpawner());
        }
        else
        {
            ScheduleMiniTank();
            Debug.Log("low DT + high skill quadrant hit");
        }

    }

    /// <summary>
    /// Quadrant I: High Skill × High Switch → Disrupt (temporary lockout)
    /// Quadrant II: Low Skill × High Switch → Assist (swap bonus)
    /// Quadrant III: Low Skill × Low Switch → Assist (free ammo)
    /// Quadrant IV: High Skill × Low Switch → Disrupt (limit ammo)
    /// </summary>
    private void ConfigureSwitchIntervention(bool highSkill, bool highSwitch)
    {
        // 1) Tear down everything
        if (switchLockRoutine != null)
        {
            StopCoroutine(switchLockRoutine);
            switchLockRoutine = null;
            Player.inst.canSwap = true;
        }
        swapBonusEnabled = false;        
        perSwapLockEnabled   = false;
        debuffedWeaponName   = null;
        // 2) Dispatch exactly one branch
        if (highSwitch && highSkill)
        {
            Debug.Log("Quadrant I: High-Skill + High-Switch → Disrupt: Lock swapping");
            perSwapLockEnabled = true;
        }
        else if (highSwitch && !highSkill)
        {
            Debug.Log("Quadrant II: Low-Skill + High-Switch → Assist: Swap Bonus");
            swapBonusEnabled = true;
        }
        else if (!highSwitch && !highSkill)
        {
            Debug.Log("Quadrant III: Low-Skill + Low-Switch → Assist: Gift new weapon");
            SpawnOneNewWeaponPickup();
        }
        else // (!highSwitch && highSkill)
        {
            // 1) Pick by total seconds equipped, descending
            string mostUsed = WeaponUsageStats.WeaponUsageTimes
                .OrderByDescending(kvp => kvp.Value)
                .Select(kvp => kvp.Key)
                .FirstOrDefault();

            // 2) Fallback to current weapon if somehow empty
            if (string.IsNullOrEmpty(mostUsed) && Player.inst.curWeapon != null)
            {
                mostUsed = Player.inst.curWeapon.displayName;
            }

            // 3) Guard against truly empty
            if (string.IsNullOrEmpty(mostUsed))
            {
                Debug.LogWarning("No equip‐time data available; skipping switch penalty.");
                return;
            }

            // 2) Permanently halve its magazine capacity for this wave:
            var pw = Player.inst.weapons
                .FirstOrDefault(w => w.displayName.Equals(mostUsed, StringComparison.OrdinalIgnoreCase));

            // halve the clipSize
            pw.clipSize      = Mathf.Max(1, pw.clipSize / 2);

            // then adjust the current ammo to not exceed the new capacity
            pw.curAmmoInClip = Mathf.Min(pw.curAmmoInClip, pw.clipSize);


            // 3) Remember which weapon to debuff for the rest of the wave:
            debuffedWeaponName = mostUsed;

            GameUI.inst.ShowTemporaryMessage(
                $"'{mostUsed}' is DEBUFFED this wave! (–25% dmg, clip halved)",
                messageDisplayTime
            );
            Debug.Log($"Debuffing {mostUsed} for the whole wave");
        }
    }

    #endregion

    /// <summary>
    /// Spawns a small cluster of cover crates around the player, one every `interval` seconds.
    /// </summary>
    private IEnumerator SpawnRockCover(int count, float delay = 2.0f)
    {
        Debug.Log($"[CoverSpawn] rockCoverPrefab is {(rockCoverPrefab == null ? "NULL" : "OK")}");
        if (rockCoverPrefab == null) yield break;

        Vector3 playerPos = Player.inst.transform.position;
        int spawned = 0, attempts = 0;

        while (spawned < count && attempts < count * 5)
        {
            attempts++;
            Debug.Log($"[CoverSpawn] Attempt #{attempts}");

            // 1) pick a random point in a donut
            float ang = UnityEngine.Random.Range(0f, 2 * Mathf.PI);
            float dist = UnityEngine.Random.Range(minSpawnDistance, maxSpawnDistance);
            Vector3 candidate = playerPos + new Vector3(
                Mathf.Cos(ang) * dist,
                0f,
                Mathf.Sin(ang) * dist
            );
            Debug.Log($"  Candidate XZ: {candidate.x:F2}, {candidate.z:F2}");

            candidate.y = 0f;


            // 2) non-alloc overlap on only obstacleMask
            int hitCount = Physics.OverlapSphereNonAlloc(
                candidate + Vector3.up * (checkRadius * 0.5f),
                checkRadius,
                _overlapResults,
                obstacleMask
            );
            bool blocked = false;
            for (int i = 0; i < hitCount; i++)
            {
                var col = _overlapResults[i];
                if (col.isTrigger) continue;
                if (col.CompareTag("Ground")) continue;
                blocked = true;
                break;
            }
            if (blocked) continue;


            // 3) success! spawn and scale
            var rock = Instantiate(rockCoverPrefab, candidate, Quaternion.identity);
            rock.transform.localScale *= rockScaleFactor;
            spawnedRocks.Add(rock);
            Debug.Log("   → Spawned rock cover here");
            spawned++;

            yield return new WaitForSeconds(delay);
        }

        if (spawned == 0)
            Debug.LogWarning("[CoverSpawn] Couldn’t place any cover rocks.");
        else
            Debug.Log($"[CoverSpawn] Spawned {spawned}/{count} rocks after {attempts} tries");
    }
    private IEnumerator SpawnCoverSequence()
    {
        // 1) initial burst of 2
        yield return SpawnRockCover(2, 2.0f);

        // 2) five refreshes
        for (int refresh = 0; refresh < 4; refresh++)
        {
            yield return new WaitForSeconds(15f);
            yield return SpawnRockCover(2, 0.5f);
        }
    }

    private void CleanupCoverRocks()
    {
        foreach (var rock in spawnedRocks)
        {
            if (rock != null)
                Destroy(rock);
        }
        spawnedRocks.Clear();
    }

    private IEnumerator SpeedBoostSpawner()
    {
        // immediately drop one, then wait 20s between each
        while (true)
        {
            // pick a random spot from the spawner helper:
            Vector3 pos = PickupSpawner.inst.GetRandomSpawnPosition();
            PickupSpawner.inst.SpawnOneSpeedBoost(pos);

            yield return new WaitForSeconds(20f);
        }
    }

    private IEnumerator DamagePickupSpawner()
    {
        while (true)
        {
            // pick a random valid spawn location
            Vector3 pos = PickupSpawner.inst.GetRandomSpawnPosition();
            // delegate actual spawn to PickupSpawner
            PickupSpawner.inst.SpawnOneDamageBoost(pos);
            yield return new WaitForSeconds(20f);
        }
    }

    public void EnemySpeedIncrease()
    {
        // kill any old routine & darts
        CleanupDarts();

        // spawn one right now, then every 20s
        SpawnDart();
        _dartRoutine = StartCoroutine(DartSpawnLoop());
    }

    private IEnumerator DartSpawnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(20f);
            SpawnDart();
        }
    }

    private void SpawnDart()
    {
        // pick a random spawner point from your EnemySpawner
        var sp = EnemySpawner.inst.spawnPoints;
        Vector3 basePos = sp[UnityEngine.Random.Range(0, sp.Length)].transform.position;
        Vector3 jitter = new Vector3(UnityEngine.Random.Range(-1f, 1f), 0, UnityEngine.Random.Range(-1f, 1f));
        Vector3 spawnPos = basePos + jitter;

        // pull one instance from the pool, initialize exactly like a normal enemy
        GameObject go = Pool.Spawn(dartEnemyType, spawnPos, Quaternion.identity);
        var e = go.GetComponent<Enemy>();
        e.Initialize();

        // now buff its moveSpeed
        e.moveSpeed *= dartSpeedMultiplier;

        // tint its mesh so players see "this one is special"
        var renderers = go.GetComponentsInChildren<MeshRenderer>();
        foreach (var mr in renderers)
        {
            // iterate all materials on that renderer
            for (int i = 0; i < mr.materials.Length; i++)
            {
                var mat = mr.materials[i];
                if (mat.HasProperty("_Color"))
                    mat.color = dartColor;
            }
        }

        // ensure the spawner knows about this extra enemy so waves don't auto-end
        EnemySpawner.inst.remainingEnemies++;
        _activeDarts.Add(go);
    }

    public void EnableDartReplace()
    {
        CleanupDarts();                        
        dartReplaceEnabled = true;
    }
    public void CleanupDarts()
    {
        dartReplaceEnabled = false;  
        if (_dartRoutine != null)
            StopCoroutine(_dartRoutine);
        _dartRoutine = null;

        foreach (var d in _activeDarts)
            if (d != null) Destroy(d);
        _activeDarts.Clear();
    }

    public void TriggerDamageDisrupt()
    {
        // restart if already running
        if (_damageDisruptRoutine != null)
            StopCoroutine(_damageDisruptRoutine);
        _damageDisruptRoutine = StartCoroutine(DamageDisruptCoroutine());
    }

    private IEnumerator DamageDisruptCoroutine()
    {
        // 1) slow the player
        Player.inst.movement.speedMultiplier = damageSlowMultiplier;
        // 2) flash blood UI
        DamageUIEffect.inst.FlashBlood();

        GameUI.inst.ShowTemporaryMessage(damageTakenMessage, messageDisplayTime);


        // wait
        yield return new WaitForSeconds(damageSlowDuration);

        // restore
        Player.inst.movement.speedMultiplier = 1f;
        _damageDisruptRoutine = null;
    }

    private void ApplySpawnRateReduction()
    {
        var spawner = EnemySpawner.inst;
        // capture the original so we can restore later
        spawner.curWave.enemySpawnRate *= spawnRateMultiplier;
        Debug.Log($"Spawn interval increased to {spawner.curWave.enemySpawnRate:F2}s (×{spawnRateMultiplier})");
    }

    private void ScheduleMiniTank()
    {
        var spawner = EnemySpawner.inst;
        // estimate half of the total “spawn duration”:
        float halfDelay = spawner.enemiesToSpawn.Count * spawner.curWave.enemySpawnRate * 0.5f;
        StartCoroutine(SpawnMiniTankAfterDelay(halfDelay));
    }

    private IEnumerator SpawnMiniTankAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // pick a random spawn‐point to drop in the mini-tank
        var pts = EnemySpawner.inst.spawnPoints;
        var basePos = pts[UnityEngine.Random.Range(0, pts.Length)].transform.position;
        var spawnPos = basePos + new Vector3(UnityEngine.Random.Range(-1f, 1f), 0, UnityEngine.Random.Range(-1f, 1f));
        var go = Pool.Spawn(miniTankPrefab, spawnPos, Quaternion.identity);
        var e = go.GetComponent<Enemy>();
        e.Initialize();

        EnemySpawner.inst.remainingEnemies++;
        Debug.Log("Mini-Tank spawned to disrupt high-skill player");
    }

    private IEnumerator SwapLockCoroutine()
    {
        // 1) Disable swapping
        Player.inst.canSwap = false;

        GameUI.inst.ShowTemporaryMessage(switchLockMessage, messageDisplayTime);

        // 2) Wait out the lock period
        yield return new WaitForSeconds(switchLockDuration);

        // 3) Re‐enable swapping
        Player.inst.canSwap = true;
        switchLockRoutine = null;
    }

    public void OnWeaponSwapped(string weaponName)
    {
        if (swapBonusEnabled && !swapBonusGrantedFor.Contains(weaponName))
        {
            swapBonusGrantedFor.Add(weaponName);
            Player.inst.attack.ApplyDamageBuff(swapBonusDamageMultiplier, swapBonusDuration);
            GameUI.inst.ShowTemporaryMessage($"+{(int)((swapBonusDamageMultiplier - 1f) * 100)}% temporary damage buff granted for this weapon!", swapBonusDuration);
            Debug.Log($"Swap Bonus granted for {weaponName}");
        }

         if (perSwapLockEnabled)
        {
            // immediately lock them out for switchLockDuration
            if (switchLockRoutine != null) StopCoroutine(switchLockRoutine);
            switchLockRoutine = StartCoroutine(SwapLockCoroutine());
        }
    }

    public void WeaponSwitchPenalty()
    {
        // 1) Halve the clip so they have to reload immediately
        var weapon = Player.inst.curWeapon;
        weapon.curAmmoInClip = Mathf.Max(1, weapon.curAmmoInClip / 2);

        // 2) Nerf their damage to 80% for 5 seconds
        var atk = Player.inst.attack;
        atk.ApplyDamageBuff(0.8f, 1000f);

        // 3) Give feedback
        GameUI.inst.ShowTemporaryMessage(
            "Weapon Overuse! Damage −20% and clip halved for the wave",
            3f
        );
    }

    private void SpawnOneNewWeaponPickup()
    {
        // 1) build set of owned weapon names
        var ownedNames = new HashSet<string>(
            Player.inst.weapons.Select(w => w.displayName),
            StringComparer.OrdinalIgnoreCase
        );

        // 2) grab all the weapon‐pickup prefabs from your spawner
        var allPrefabs = PickupSpawner.inst.allWeaponPickupPrefabs;
        var candidates = new List<GameObject>();

        foreach (var prefab in allPrefabs)
        {
            var pc = prefab.GetComponent<Pickup>();
            // only consider Weapon‐type pickups
            if (pc == null || pc.type != PickupType.Weapon)
                continue;

            string giveName = pc.baseWeapon != null
                ? pc.baseWeapon.displayName
                : pc.weaponToGive.displayName;

            if (!ownedNames.Contains(giveName))
                candidates.Add(prefab);
        }

        if (candidates.Count == 0)
        {
            Debug.Log("All weapons owned; no new pickup spawned.");
            return;
        }

        var pick = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        Vector3 pos = PickupSpawner.inst.GetRandomSpawnPosition();
        PickupSpawner.inst.SpawnOneWeaponPickup(pick, pos);

        Debug.Log($"Spawned new weapon pickup: {pick.name}");
    }

}