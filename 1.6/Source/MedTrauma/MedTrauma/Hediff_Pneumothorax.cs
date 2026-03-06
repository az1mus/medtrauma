using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MedTrauma
{
    /// <summary>
    /// 气胸 Hediff - 带有进度条，影响肺效率
    /// 肺效率 = 70% - (progress * 0.7%)，progress 范围 0-100
    /// </summary>
    public class Hediff_Pneumothorax : HediffWithComps
    {
        public float Progress => Severity * 100;

        /// <summary>
        /// 计算当前效率 (70 - progress * 0.7)
        /// </summary>
        public float CalculateEfficiency()
        {
            return Mathf.Max(0, 70f - Progress * 0.7f);
        }

        public override string Label
        {
            get
            {
                string severityLabel = Severity > 0.7f ? "MedTrauma_PneumothoraxSevere".Translate() : "";
                if (Severity > 0.7f)
                {
                    return $"{base.Label} ({severityLabel}) ({Progress:F1}%)";
                }
                return $"{base.Label} ({Progress:F1}%)";
            }
        }

        public override string TipStringExtra
        {
            get
            {
                return base.TipStringExtra;
            }
        }
    }

    /// <summary>
    /// 气胸并发症 - 支持被气胸针穿刺暂停
    /// </summary>
    public class HediffComp_PneumothoraxProgress : HediffComp_SeverityPerDay
    {
        public override float SeverityChangePerDay()
        {
            // 检查是否有气胸针穿刺 hediff 来暂停进度增长
            if (Pawn?.health?.hediffSet?.hediffs != null)
            {
                var hasNeedle = Pawn.health.hediffSet.hediffs
                    .Any(h => h.def.defName == "PneumothoraxNeedle" && h.Part == this.parent.Part);
                
                if (hasNeedle)
                {
                    // 有针穿刺，暂停进度增长
                    return 0f;
                }
            }
            
            return base.SeverityChangePerDay();
        }
    }

    /// <summary>
    /// 气胸并发症属性
    /// </summary>
    public class HediffCompProperties_PneumothoraxProgress : HediffCompProperties_SeverityPerDay
    {
        public HediffCompProperties_PneumothoraxProgress()
        {
            compClass = typeof(HediffComp_PneumothoraxProgress);
        }
    }

    /// <summary>
    /// 主补丁类
    /// </summary>
    [StaticConstructorOnStartup]
    public static class MedTraumaPatches
    {
        static MedTraumaPatches()
        {
            var harmony = new Harmony("com.medtrauma.main");
            harmony.PatchAll();
        }

        /// <summary>
        /// 补丁 CalculatePartEfficiency 来应用气胸的效率惩罚
        /// </summary>
        [HarmonyPatch(typeof(PawnCapacityUtility), "CalculatePartEfficiency")]
        public static class PawnCapacityUtility_CalculatePartEfficiency_Patch
        {
            [HarmonyPostfix]
            public static void ApplyPneumothoraxEfficiency(HediffSet diffSet, BodyPartRecord part, ref float __result)
            {
                // 只处理肺部位
                if (part.def.defName != "Lung")
                    return;

                // 查找该部位的气胸 hediff
                var pneumothorax = diffSet.hediffs
                    .FirstOrDefault(h => h.def.defName == "Pneumothorax" && h.Part == part);

                if (pneumothorax != null)
                {
                    // 检查是否有气胸针穿刺 hediff，如果有则不产生效率影响
                    var pneumothoraxNeedle = diffSet.hediffs
                        .FirstOrDefault(h => h.def.defName == "PneumothoraxNeedle" && h.Part == part);

                    if (pneumothoraxNeedle != null)
                    {
                        // 存在气胸针穿刺，不对肺产生效率影响，直接返回
                        return;
                    }

                    // 计算效率：70 - progress * 0.7
                    float progress = pneumothorax.Severity * 100;
                    float efficiency = Mathf.Max(0, 70f - progress * 0.7f);

                    // 将效率转换为乘数
                    __result = __result * (efficiency / 100f);
                }
            }
        }


        /// <summary>
        /// 补丁 RecipeWorker.GetPartsToApplyOn 来为气胸针穿刺手术返回有气胸 Hediff 的肺部
        /// 这样手术才会显示在医疗卡片中
        /// </summary>
        [HarmonyPatch(typeof(RecipeWorker), "GetPartsToApplyOn")]
        public static class RecipeWorker_GetPartsToApplyOn_Patch
        {
            [HarmonyPostfix]
            public static void AddPneumothoraxParts(RecipeDef recipe, Pawn pawn, ref IEnumerable<BodyPartRecord> __result)
            {
                // 仅处理气胸针穿刺手术
                if (recipe.defName != "PneumothoraxNeedleProcedure")
                    return;

                if (pawn?.health?.hediffSet?.hediffs == null)
                    return;

                // 查找所有有气胸 Hediff 的肺部
                var pneumothoraxParts = new List<BodyPartRecord>();
                foreach (var hediff in pawn.health.hediffSet.hediffs)
                {
                    if (hediff.def.defName == "Pneumothorax" &&
                        hediff.Part != null &&
                        hediff.Part.def.defName == "Lung" &&
                        hediff.Visible)
                    {
                        pneumothoraxParts.Add(hediff.Part);
                    }
                }

                __result = pneumothoraxParts;
            }
        }

        /// <summary>
        /// 补丁 RecipeWorker.AvailableOnNow 来为气胸针穿刺手术检查是否有气胸
        /// </summary>
        [HarmonyPatch(typeof(RecipeWorker), "AvailableOnNow")]
        public static class RecipeWorker_AvailableOnNow_Patch
        {
            [HarmonyPostfix]
            public static void CheckPneumothoraxAvailability(RecipeWorker __instance, Thing thing, BodyPartRecord part, ref bool __result)
            {
                // 仅处理气胸针穿刺手术
                if (__instance.recipe.defName != "PneumothoraxNeedleProcedure")
                    return;

                if (!(thing is Pawn pawn))
                {
                    __result = false;
                    return;
                }

                if (part == null)
                {
                    __result = false;
                    return;
                }

                // 检查指定部位是否有气胸
                __result = pawn.health?.hediffSet?.hediffs?.Any(h =>
                    h.def.defName == "Pneumothorax" &&
                    h.Part == part &&
                    h.Visible) == true;
            }
        }

        /// <summary>
        /// 补丁 RecipeWorker.AvailableReport 来为气胸针穿刺手术检查是否有气胸
        /// AvailableReport 在 GetPartsToApplyOn 之前被调用，如果不通过检查会阻止手术显示
        /// </summary>
        [HarmonyPatch(typeof(RecipeWorker), "AvailableReport")]
        public static class RecipeWorker_AvailableReport_Patch
        {
            [HarmonyPostfix]
            public static void CheckPneumothoraxReport(RecipeWorker __instance, Thing thing, BodyPartRecord part, ref AcceptanceReport __result)
            {
                // 仅处理气胸针穿刺手术
                if (__instance.recipe.defName != "PneumothoraxNeedleProcedure")
                    return;

                if (!(thing is Pawn pawn))
                {
                    __result = new AcceptanceReport("Not a pawn");
                    return;
                }

                // 检查是否有气胸（不需要 part，因为 AvailableReport 可能在没有 part 的情况下调用）
                bool hasPneumothorax = pawn.health?.hediffSet?.hediffs?.Any(h =>
                    h.def.defName == "Pneumothorax" &&
                    h.Visible) == true;

                if (!hasPneumothorax)
                {
                    __result = new AcceptanceReport("No pneumothorax");
                }
                else
                {
                    __result = AcceptanceReport.WasAccepted;
                }
            }
        }
        
        /// <summary>
        /// 补丁 PawnCapacityWorker_Breathing 来在气胸严重时对呼吸容量产生额外影响
        /// </summary>
        [HarmonyPatch(typeof(PawnCapacityWorker_Breathing), "CalculateCapacityLevel")]
        public static class PawnCapacityWorker_Breathing_CalculateCapacityLevel_Patch
        {
            [HarmonyPostfix]
            public static void ApplyPneumothoraxBreathingPenalty(HediffSet diffSet, ref float __result)
            {
                // 检查pawn是否有气胸，且严重度大于0.7
                if (diffSet?.pawn?.health?.hediffSet?.hediffs != null)
                {
                    bool hasSeverePneumothorax = diffSet.pawn.health.hediffSet.hediffs
                        .Any(h => h.def.defName == "Pneumothorax" && 
                                  h.Severity > 0.7f &&
                                  // 确保气胸不在被针穿刺治疗的状态
                                  !diffSet.pawn.health.hediffSet.hediffs.Any(n => n.def.defName == "PneumothoraxNeedle" && n.Part == h.Part));

                    if (hasSeverePneumothorax)
                    {
                        // 对呼吸容量乘以0.6
                        __result *= 0.6f;
                    }
                }
            }
        }
    }
}
