using UnityEngine;

public static class PerformanceStats {
    // Shooting metrics.
    public static int OverallShotsFired = 0;
    public static int OverallShotsHit = 0;
    public static int RoundShotsFired = 0;
    public static int RoundShotsHit = 0;

    // Wave timing metrics.
    public static float OverallWaveTime = 0f;
    public static int CompletedWaves = 0;
    public static float CurrentWaveStartTime = 0f;
    public static float CurrentWaveCompletionTime = 0f;

    // New damage metrics.
    public static int OverallDamageTaken = 0;
    public static int RoundDamageTaken = 0;
    public static bool shotgunHitCountedThisClick = false;

    // Shooting accuracy properties.
    public static float OverallAccuracy
    {
        get { return OverallShotsFired > 0 ? (float)OverallShotsHit / OverallShotsFired : 0f; }
    }
    public static float RoundAccuracy {
        get { return RoundShotsFired > 0 ? (float)RoundShotsHit / RoundShotsFired : 0f; }
    }

    // Average wave completion time.
    public static float AverageWaveCompletionTime {
        get { return CompletedWaves > 0 ? OverallWaveTime / CompletedWaves : 0f; }
    }

    // Resets round-specific counters (shots and damage) and records the current time as the wave start.
    public static void ResetRoundStats() {
        RoundShotsFired = 0;
        RoundShotsHit = 0;
        RoundDamageTaken = 0;
        CurrentWaveStartTime = Time.time;
    }

    // Call this when a wave ends to record its completion time.
    public static void EndWave() {
        CurrentWaveCompletionTime = Time.time - CurrentWaveStartTime;
        OverallWaveTime += CurrentWaveCompletionTime;
        CompletedWaves++;
    }
}
