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
    /// 气胸移除手术 Worker
    /// </summary>
    public class RecipeWorker_PneumothoraxRemoval : RecipeWorker
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            if (pawn?.health?.hediffSet?.hediffs == null)
                yield break;

            // 查找所有有气胸 Hediff 的肺部
            foreach (var hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff.def.defName == "Pneumothorax" &&
                    hediff.Part != null &&
                    hediff.Part.def.defName == "Lung" &&
                    hediff.Visible)
                {
                    yield return hediff.Part;
                }
            }
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            // 移除该部位的气胸 Hediff
            var pneumothorax = pawn.health.hediffSet.hediffs
                .FirstOrDefault(h => h.def.defName == "Pneumothorax" && h.Part == part);

            if (pneumothorax != null)
            {
                pawn.health.RemoveHediff(pneumothorax);
            }

            // 消耗医药
            if (ingredients != null)
            {
                foreach (var ingredient in ingredients)
                {
                    ingredient.Destroy();
                }
            }
        }

        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            if (!(thing is Pawn pawn))
                return false;

            if (part == null)
                return false;

            // 检查指定部位是否有气胸
            return pawn.health?.hediffSet?.hediffs?.Any(h =>
                h.def.defName == "Pneumothorax" &&
                h.Part == part &&
                h.Visible) == true;
        }

        public override AcceptanceReport AvailableReport(Thing thing, BodyPartRecord part = null)
        {
            if (!(thing is Pawn pawn))
                return new AcceptanceReport("Not a pawn");

            // 检查是否有气胸
            bool hasPneumothorax = pawn.health?.hediffSet?.hediffs?.Any(h =>
                h.def.defName == "Pneumothorax" &&
                h.Visible) == true;

            if (!hasPneumothorax)
                return new AcceptanceReport("No pneumothorax");

            return AcceptanceReport.WasAccepted;
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
        /// 补丁 RecipeWorker.GetPartsToApplyOn 来为气胸手术返回有气胸 Hediff 的肺部
        /// 这样手术才会显示在医疗卡片中
        /// </summary>
        [HarmonyPatch(typeof(RecipeWorker), "GetPartsToApplyOn")]
        public static class RecipeWorker_GetPartsToApplyOn_Patch
        {
            [HarmonyPostfix]
            public static void AddPneumothoraxParts(RecipeDef recipe, Pawn pawn, ref IEnumerable<BodyPartRecord> __result)
            {
                // 仅处理气胸手术
                if (recipe.defName != "PneumothoraxNeedleProcedure" && recipe.defName != "PneumothoraxRemovalSurgery")
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
        /// 补丁 RecipeWorker.AvailableOnNow 来为气胸手术检查是否有气胸
        /// </summary>
        [HarmonyPatch(typeof(RecipeWorker), "AvailableOnNow")]
        public static class RecipeWorker_AvailableOnNow_Patch
        {
            [HarmonyPostfix]
            public static void CheckPneumothoraxAvailability(RecipeWorker __instance, Thing thing, BodyPartRecord part, ref bool __result)
            {
                // 仅处理气胸手术
                if (__instance.recipe.defName != "PneumothoraxNeedleProcedure" && __instance.recipe.defName != "PneumothoraxRemovalSurgery")
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
        /// 补丁 RecipeWorker.AvailableReport 来为气胸手术检查是否有气胸
        /// AvailableReport 在 GetPartsToApplyOn 之前被调用，如果不通过检查会阻止手术显示
        /// </summary>
        [HarmonyPatch(typeof(RecipeWorker), "AvailableReport")]
        public static class RecipeWorker_AvailableReport_Patch
        {
            [HarmonyPostfix]
            public static void CheckPneumothoraxReport(RecipeWorker __instance, Thing thing, BodyPartRecord part, ref AcceptanceReport __result)
            {
                // 仅处理气胸手术
                if (__instance.recipe.defName != "PneumothoraxNeedleProcedure" && __instance.recipe.defName != "PneumothoraxRemovalSurgery")
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
    }
}
