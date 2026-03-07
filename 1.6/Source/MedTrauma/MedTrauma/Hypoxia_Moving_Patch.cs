using HarmonyLib;
using RimWorld;
using Verse;
using System.Collections.Generic;
using UnityEngine;

namespace MedTrauma
{
    /// <summary>
    /// 低血氧症（Hypoxia）影响补丁：
    /// BloodOxygen 对 Moving 有 50% 的影响权重
    /// </summary>
    [HarmonyPatch(typeof(PawnCapacityWorker_Moving), "CalculateCapacityLevel")]
    public static class Hypoxia_Moving_Patch
    {
        [HarmonyPostfix]
        static void ApplyHypoxiaToMoving(HediffSet diffSet,
            List<PawnCapacityUtility.CapacityImpactor> impactors, ref float __result)
        {
            PawnCapacityDef bloodOxygenDef = MedTraumaDefDatabase.BloodOxygen;
            if (bloodOxygenDef == null)
                return;

            float bloodOxygen = diffSet.pawn.health.capacities.GetLevel(bloodOxygenDef);

            // BloodOxygen 对 Moving 有 50% 的影响权重
            // 即：result = result * (1 - 0.5) + result * bloodOxygen * 0.5
            // 简化：result = result * (0.5 + 0.5 * bloodOxygen)
            float hypoxiaFactor = 0.5f + 0.5f * bloodOxygen;

            if (hypoxiaFactor < 1f)
            {
                __result *= hypoxiaFactor;
            }
        }
    }
}
