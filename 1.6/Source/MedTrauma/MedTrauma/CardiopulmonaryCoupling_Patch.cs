using HarmonyLib;
using RimWorld;
using Verse;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MedTrauma
{
    /// <summary>
    /// 心肺耦合模型 Harmony 补丁
    /// 使用固定每 10 tick 更新一次心肺耦合状态
    /// </summary>
    public static class CardiopulmonaryCoupling_Patch
    {
        // 低血氧阈值
        private const float HYPOXIA_ORGAN_THRESHOLD = 0.9f; // 器官低血氧应用阈值
        private const float VF_THRESHOLD = 0.2f; // 心室颤动应用阈值

        /// <summary>
        /// 获取 Pawn 的 BleedingState
        /// </summary>
        private static BleedingState GetBleedingState(Pawn pawn)
        {
            if (pawn == null) return null;
            return PawnBleedingStateManager.GetState(pawn);
        }

        /// <summary>
        /// 获取 Pawn 的 BloodLoss 严重程度
        /// </summary>
        private static float GetBloodLossSeverity(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return 0f;

            var bloodLossDef = DefDatabase<HediffDef>.GetNamedSilentFail("BloodLoss");
            if (bloodLossDef == null) return 0f;

            var bloodLoss = pawn.health.hediffSet.GetFirstHediffOfDef(bloodLossDef);
            if (bloodLoss == null) return 0f;

            return bloodLoss.Severity;
        }

        /// <summary>
        /// 获取 Pawn 的 Breathing 效率
        /// </summary>
        private static float GetBreathingEfficiency(Pawn pawn)
        {
            if (pawn?.health?.capacities == null) return 1f;

            var breathingDef = DefDatabase<PawnCapacityDef>.GetNamedSilentFail("Breathing");
            if (breathingDef == null) return 1f;

            return pawn.health.capacities.GetLevel(breathingDef);
        }

        /// <summary>
        /// 更新心肺耦合状态 - 固定每 10 tick 调用一次
        /// 使用 HashInterval 分散不同 Pawn 的更新时间，避免性能峰值
        /// </summary>
        private static void TryUpdateCardiopulmonaryState(Pawn pawn)
        {
            if (pawn == null) return;

            // 每 10 tick 更新一次，使用 pawn 的 hash 分散更新时间
            if (!IsHashIntervalTick(pawn, 10))
                return;

            var state = GetBleedingState(pawn);
            if (state == null) return;

            float bloodLossSeverity = GetBloodLossSeverity(pawn);
            float breathingEfficiency = GetBreathingEfficiency(pawn);

            state.UpdateState(bloodLossSeverity, breathingEfficiency);

            // 检查并应用低血氧 Hediff
            CheckAndApplyHypoxiaHediffs(pawn, state);
        }

        /// <summary>
        /// 检查并应用低血氧导致的 Hediff
        /// HediffComp 负责 severity 的增减和移除，这里只负责添加
        /// </summary>
        private static void CheckAndApplyHypoxiaHediffs(Pawn pawn, BleedingState state)
        {
            if (pawn?.health?.hediffSet == null) return;

            float bloodOxygen = state.bloodOxygen;

            // 获取 HediffDef
            var hypoxiaOrganDef = DefDatabase<HediffDef>.GetNamedSilentFail("Hypoxia");
            var vfDef = DefDatabase<HediffDef>.GetNamedSilentFail("VF");

            // ==================== 大脑缺氧 ====================
            if (bloodOxygen < HYPOXIA_ORGAN_THRESHOLD && hypoxiaOrganDef != null)
            {
                // 获取目标器官部位 - 使用 defName 查找
                var allParts = pawn.health.hediffSet.GetNotMissingParts();
                var brainParts = allParts.Where(p => p.def.defName == "Brain").ToList();
                ApplyHypoxiaToParts(pawn, hypoxiaOrganDef, brainParts);
            }

            // ==================== 心室颤动（心脏） ====================
            if (bloodOxygen < VF_THRESHOLD && vfDef != null)
            {
                // 获取心脏部位
                var heartParts = pawn.health.hediffSet.GetNotMissingParts()
                    .Where(p => p.def.defName.Contains("Heart")).ToList();
                ApplyVFToParts(pawn, vfDef, heartParts);
            }
        }

        /// <summary>
        /// 对指定部位应用低血氧 Hediff
        /// </summary>
        private static void ApplyHypoxiaToParts(Pawn pawn, HediffDef hypoxiaDef, List<BodyPartRecord> parts)
        {
            foreach (var part in parts)
            {
                var existingHypoxia = pawn.health.hediffSet.hediffs
                    .FirstOrDefault(h => h.def == hypoxiaDef && h.Part == part);

                if (existingHypoxia == null)
                {
                    var hypoxia = HediffMaker.MakeHediff(hypoxiaDef, pawn, part);
                    pawn.health.AddHediff(hypoxia, part);
                }
            }
        }

        /// <summary>
        /// 对指定部位应用 VF Hediff
        /// </summary>
        private static void ApplyVFToParts(Pawn pawn, HediffDef vfDef, List<BodyPartRecord> parts)
        {
            foreach (var part in parts)
            {
                var existingVF = pawn.health.hediffSet.hediffs
                    .FirstOrDefault(h => h.def == vfDef && h.Part == part);

                if (existingVF == null)
                {
                    var vf = HediffMaker.MakeHediff(vfDef, pawn, part);
                    pawn.health.AddHediff(vf, part);
                }
            }
        }

        /// <summary>
        /// 判断是否是 HashIntervalTick
        /// 参考原版 Gen.IsHashIntervalTick 实现
        /// </summary>
        private static bool IsHashIntervalTick(Pawn pawn, int interval)
        {
            return (Find.TickManager.TicksGame + pawn.thingIDNumber.HashOffset()) % interval == 0;
        }

        // ==================== Pawn.Tick 补丁 - 主要更新入口 ====================

        /// <summary>
        /// 补丁 Pawn.Tick - 在原版 Tick 之后每 10 tick 更新一次心肺耦合状态
        /// </summary>
        [HarmonyPatch(typeof(Pawn), "Tick")]
        public static class Pawn_Tick_Patch
        {
            [HarmonyPostfix]
            static void UpdateStateOnTick(Pawn __instance)
            {
                TryUpdateCardiopulmonaryState(__instance);
            }
        }

        // ==================== Heart Efficiency 补丁 ====================

        /// <summary>
        /// 补丁 PawnCapacityUtility.CalculateTagEfficiency - 计算 BloodPumpingSource 时应用 heart efficiency factor
        /// 原版 BloodPumping capacity 通过 CalculateTagEfficiency(BloodPumpingSource) 计算心脏效率
        /// postfix: __result *= Heart efficiency factor，并确保最小值 0.01
        /// </summary>
        [HarmonyPatch(typeof(PawnCapacityUtility), "CalculateTagEfficiency")]
        public static class PawnCapacityUtility_CalculateTagEfficiency_Patch
        {
            [HarmonyPostfix]
            static void ApplyHeartEfficiencyFactor(HediffSet diffSet, BodyPartTagDef tag, ref float __result)
            {
                // 只对 BloodPumpingSource 标签应用心脏效率因子
                if (tag != BodyPartTagDefOf.BloodPumpingSource || diffSet?.pawn == null) return;

                var state = GetBleedingState(diffSet.pawn);
                if (state != null)
                {
                    __result *= state.heartEfficiencyFactor;
                    __result = Mathf.Clamp01(__result);
                    __result = Mathf.Max(0.01f, __result);  // 确保最小值 0.01
                }
            }
        }

        // ==================== Blood Oxygen 补丁 ====================

        /// <summary>
        /// 补丁 BloodOxygen 容量计算 - postfix: __result = bloodoxygen
        /// 注意：由于 BloodOxygen 是自定义容量，需要拦截其 Worker 的计算
        /// </summary>
        [HarmonyPatch(typeof(PawnCapacityUtility), "CalculateCapacityLevel")]
        public static class PawnCapacityUtility_CalculateCapacityLevel_Patch
        {
            [HarmonyPostfix]
            static void ApplyBloodOxygenOverride(HediffSet diffSet, PawnCapacityDef capacity, ref float __result)
            {
                if (diffSet?.pawn == null || capacity == null) return;

                // 检查是否是 BloodOxygen 容量
                if (capacity.defName == "BloodOxygen")
                {
                    var state = GetBleedingState(diffSet.pawn);
                    if (state != null)
                    {
                        __result = state.bloodOxygen;
                        __result = Mathf.Clamp01(__result);
                    }
                }
            }
        }

        // ==================== Consciousness 补丁 ====================

        /// <summary>
        /// 补丁 Consciousness 容量计算 - postfix: __result /= ((0.8+0.2*bloodpumping)*(0.8+0.2*breathing))
        /// 移除原版对 BloodPumping 和 Breathing 的依赖，让 Consciousness 只受大脑健康影响
        /// </summary>
        [HarmonyPatch(typeof(PawnCapacityWorker_Consciousness), "CalculateCapacityLevel")]
        public static class PawnCapacityWorker_Consciousness_CalculateCapacityLevel_Patch
        {
            [HarmonyPostfix]
            static void RemoveBloodPumpingBreathingDependency(HediffSet diffSet, ref float __result)
            {
                if (diffSet?.pawn == null) return;

                var state = GetBleedingState(diffSet.pawn);
                if (state == null) return;

                // 获取 BloodPumping 和 Breathing 效率
                float bloodPumping = GetBloodPumpingEfficiency(diffSet.pawn);
                float breathing = GetBreathingEfficiency(diffSet.pawn);

                // 移除原版的依赖：__result /= ((0.8+0.2*bloodpumping)*(0.8+0.2*breathing))
                float divisor = (0.8f + 0.2f * bloodPumping) * (0.8f + 0.2f * breathing);
                if (divisor > 0.01f)
                {
                    __result /= divisor;
                    __result = Mathf.Clamp01(__result);
                }
            }
        }

        /// <summary>
        /// 获取 Pawn 的 BloodPumping 效率
        /// </summary>
        private static float GetBloodPumpingEfficiency(Pawn pawn)
        {
            if (pawn?.health?.capacities == null) return 1f;

            var bloodPumpingDef = DefDatabase<PawnCapacityDef>.GetNamedSilentFail("BloodPumping");
            if (bloodPumpingDef == null) return 1f;

            return pawn.health.capacities.GetLevel(bloodPumpingDef);
        }
    }
}
