using System.Collections.Generic;
using UnityEngine;

public static class WeaponUsageStats {
    // Dictionary to store the usage count for each weapon.
    // You can use the weapon's displayName or any unique identifier.
    public static Dictionary<string, int> WeaponUsageCounts = new Dictionary<string, int>();

    // New dictionary for tracking weapon usage time (in seconds).
    public static Dictionary<string, float> WeaponUsageTimes = new Dictionary<string, float>();

    // Call this method whenever a weapon is used.
    public static void RecordWeaponUsage(string weaponName) {
        if (WeaponUsageCounts.ContainsKey(weaponName)) {
            WeaponUsageCounts[weaponName]++;
        } else {
            WeaponUsageCounts[weaponName] = 1;
        }
    }

    // Optional: Call this at the start of a new round to reset per-round stats.
    public static void ResetWeaponUsage() {
        WeaponUsageCounts.Clear();
        WeaponUsageTimes.Clear();
    }
}
