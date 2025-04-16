using System;
using System.Collections.Generic;

[System.Serializable]
public class PerformanceDataPerWave {
    public int waveNumber;
    public float waveAccuracy;
    public float waveTime;
    public int waveDamageTaken;
    public float waveSpeed;
    public float waveDistanceTraveled;
    public float waveTimeIdle;
    public float waveTimeMoving;
    public float wavePathEfficiency;
}

[System.Serializable]
public class PerformanceDataOverall {
    public float overallAccuracy;
    public float totalRoundTime;
    public float averageRoundTime;
    public int completedWaves;
    public int overallDamageTaken;
    public float averageSpeed;
    public float totalDistanceTraveled;
    public float totalTimeIdle;
    public float totalTimeMoving;
    public float pathEfficiency;
    public int pistolUses;
    public int shotgunUses;
    public int SMGUses;
    public int rocketLauncherUses;
    public int flameThrowerUses;
    public float pistolUseTime;
    public float shotgunUseTime;
    public float SMGUseTime;
    public float rocketLauncherUseTime;
    public float flameThrowerUseTime;

    public List<PerformanceDataPerWave> waveMetrics = new List<PerformanceDataPerWave>();



}
