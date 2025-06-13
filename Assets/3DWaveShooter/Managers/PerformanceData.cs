using System;
using System.Collections.Generic;

[Serializable]
public struct WeaponTime
{
    public string weaponName;
    public float  secondsEquipped;
}

[System.Serializable]
public class PerformanceDataPerWave
{
    public int waveNumber;
    public float waveAccuracy;
    public float waveTime;
    public int waveDamageTaken;
    public float waveSpeed;
    public float waveDistanceTraveled;
    public float waveTimeIdle;
    public float waveTimeMoving;
    public int waveWeaponSwitches;
    public WeaponTime[]  waveWeaponUsageTimes;

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
    public int overallWeaponSwitches;
    public WeaponTime[]  totalWeaponUsageTimes;


    public List<PerformanceDataPerWave> waveMetrics = new List<PerformanceDataPerWave>();


}
