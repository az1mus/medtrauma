using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace MedTrauma
{
    /// <summary>
    /// 气胸 Hediff - 带有进度条，影响肺效率
    /// 效率 = 70% - (progress * 0.7%)，progress 范围 0-100
    /// </summary>
    public class Hediff_Pneumothorax : HediffWithComps
    {
        // 获取当前进度 (0-100)
        public float Progress => Severity;

        /// <summary>
        /// 计算当前效率 (70 - progress * 0.7)
        /// </summary>
        public float CalculateEfficiency()
        {
            return Mathf.Max(0, 70f - Progress * 0.7f);
        }

        public override string TipStringExtra
        {
            get
            {
                string tip = base.TipStringExtra;
                float efficiency = CalculateEfficiency();
                tip += $"\n{"MedTrauma_PneumothoraxEfficiency".Translate()}: {efficiency:F1}%";
                tip += $"\n{"MedTrauma_PneumothoraxProgress".Translate()}: {Progress:F1}/100";
                return tip;
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
                    // 计算效率：70 - progress * 0.7
                    float progress = pneumothorax.Severity;
                    float efficiency = Mathf.Max(0, 70f - progress * 0.7f);

                    // 将效率转换为乘数
                    __result = __result * (efficiency / 100f);
                }
            }
        }

        /// <summary>
        /// 补丁手术成功后的逻辑：减少器官 1 点耐久
        /// </summary>
        [HarmonyPatch(typeof(HealthCardUtility), "DoSurgery")]
        public static class HealthCardUtility_DoSurgery_Patch
        {
            [HarmonyPostfix]
            public static void ApplyPneumothoraxNeedle(Pawn patient, RecipeDef recipe, BodyPartRecord part)
            {
                // 只处理气胸针穿刺手术
                if (recipe.defName != "PneumothoraxNeedleProcedure")
                    return;

                // 减少器官 1 点耐久 - 通过增加 injury severity 实现
                // 使用 Crush 伤害类型，severity 1 点约等于 1 点耐久
                var injuryDef = DefDatabase<HediffDef>.GetNamed("Crush");
                if (injuryDef == null)
                    return;

                var existingInjury = patient.health.hediffSet.hediffs
                    .FirstOrDefault(h => h.Part == part && h.def == injuryDef);

                if (existingInjury != null)
                {
                    existingInjury.Severity += 1f;
                }
                else
                {
                    var injury = HediffMaker.MakeHediff(injuryDef, patient, part);
                    injury.Severity = 1f;
                    patient.health.AddHediff(injury);
                }
            }
        }

        /// <summary>
        /// 控制手术显示条件 - 只有未损毁且存在气胸的单侧肺才显示
        /// </summary>
        [HarmonyPatch(typeof(HealthCardUtility), "GetSurgeryOptions")]
        public static class HealthCardUtility_GetSurgeryOptions_Patch
        {
            [HarmonyPostfix]
            public static void FilterPneumothoraxSurgery(Pawn pawn, ref IEnumerable<RecipeDef> __result)
            {
                var surgeryDef = DefDatabase<RecipeDef>.GetNamed("PneumothoraxNeedleProcedure");
                if (surgeryDef == null)
                    return;

                var result = __result.ToList();

                // 检查是否有符合条件的肺（未损毁且存在气胸）
                bool hasValidLung = false;
                if (pawn.health?.hediffSet?.hediffs != null)
                {
                    // 遍历所有肺部位
                    foreach (var lungPart in pawn.RaceProps.body.AllParts.Where(p => p.def.defName == "Lung"))
                    {
                        // 检查该部位是否缺失
                        if (pawn.health.hediffSet.PartIsMissing(lungPart))
                            continue;

                        // 检查该部位是否有气胸
                        var hasPneumothorax = pawn.health.hediffSet.hediffs
                            .Any(h => h.def.defName == "Pneumothorax" && h.Part == lungPart);

                        if (hasPneumothorax)
                        {
                            hasValidLung = true;
                            break;
                        }
                    }
                }

                if (!hasValidLung)
                {
                    result.RemoveAll(r => r.defName == "PneumothoraxNeedleProcedure");
                }

                __result = result;
            }
        }
    }
}
