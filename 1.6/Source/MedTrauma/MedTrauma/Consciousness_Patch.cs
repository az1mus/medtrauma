using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace MedTrauma
{
    /// <summary>
    /// 补丁 Consciousness 计算：
    /// 移除原版 Breathing 和 BloodPumping 对 Consciousness 的影响
    /// Consciousness 现在只受 Pain、BloodFiltration 和 ConsciousnessSource 影响
    /// </summary>
    [HarmonyPatch(typeof(PawnCapacityWorker_Consciousness), "CalculateCapacityLevel")]
    public static class Consciousness_Patch
    {
        [HarmonyPostfix]
        static void RecalculateConsciousness(HediffSet diffSet, ref float __result)
        {
            Pawn pawn = diffSet.pawn;

            // 基于 ConsciousnessSource 重新计算
            float consciousness = PawnCapacityUtility.CalculateTagEfficiency(
                diffSet, BodyPartTagDefOf.ConsciousnessSource);

            // 应用 Pain 影响
            float painFactor = Mathf.Clamp(GenMath.LerpDouble(0.1f, 1f, 0f, 0.4f, diffSet.PainTotal), 0f, 0.4f);
            if (painFactor >= 0.01f)
                consciousness -= painFactor;

            // 应用 BloodFiltration 影响（10% 权重）
            float bloodFiltration = pawn.health.capacities.GetLevel(PawnCapacityDefOf.BloodFiltration);
            consciousness = Mathf.Lerp(consciousness, consciousness * Mathf.Min(bloodFiltration, 1f), 0.1f);

            __result = Mathf.Max(consciousness, 0f);
        }
    }
}
