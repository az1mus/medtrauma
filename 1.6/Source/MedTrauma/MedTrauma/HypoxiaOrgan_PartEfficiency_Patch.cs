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
    /// 补丁：低血氧症器官效率系数计算
    /// 替代 partEfficiencyOffset，使用乘法系数：partEfficiency *= factor
    /// 血氧越低，系数越小（效率越低）
    /// </summary>
    [HarmonyPatch(typeof(PawnCapacityUtility), "CalculatePartEfficiency")]
    public static class HypoxiaOrgan_PartEfficiency_Patch
    {
        /// <summary>
        /// 后置补丁：在原始计算后应用血氧系数
        /// </summary>
        [HarmonyPostfix]
        static void ApplyHypoxiaFactor(HediffSet diffSet, BodyPartRecord part, ref float __result)
        {
            if (diffSet?.pawn?.health?.hediffSet == null || part == null)
                return;

            var pawn = diffSet.pawn;

            if (part?.def?.defName == null)
                return;

            // 获取该部位上的 HypoxiaOrgan Hediff
            var hypoxiaOrgan = diffSet.hediffs
                .FirstOrDefault(h => h.def == MedTraumaDefDatabase.HypoxiaOrgan && h.Part == part);

            if (hypoxiaOrgan == null || hypoxiaOrgan.CurStage == null)
                return;

            // 根据 severity 计算系数
            float severity = hypoxiaOrgan.Severity;

            // 如果已有 VF，直接设置心脏效率为 0.01f
            if (part.def.defName == "Heart")
            {
                HediffDef vfDef = MedTraumaDefDatabase.VF;
                if (vfDef != null)
                {
                    var vf = diffSet.hediffs.FirstOrDefault(h => h.def == vfDef && h.Part == part);
                    if (vf != null)
                    {
                        __result = 0.01f;
                        return;
                    }
                }

                if (severity > 0.98f)
                {
                    // 诱发室颤
                    AddVF(pawn, part);
                    __result = 0.01f; // 心脏
                    return;
                }

                return;
            }

            // 线性系数：factor = 1 - severity * 0.7
            // 最大 severity=1 时，factor=0.3（至少保留 30% 效率）
            float factor = severity > 0.7f ? 0.3f : Mathf.Lerp(1f, 0.3f, Mathf.Max(severity, 0f) / 0.7f);

            // 应用系数乘法
            __result *= factor;
        }

        /// <summary>
        /// 添加或更新室颤 Hediff
        /// </summary>
        static void AddVF(Pawn pawn, BodyPartRecord heart)
        {
            // 检查 Pawn 是否完全初始化，避免在生成过程中触发异常
            if (pawn?.mindState == null || pawn.health?.hediffSet == null)
                return;

            // 避免在生成过程中触发状态变化检查
            if (pawn.SpawnedOrAnyParentSpawned == false && pawn.Faction == null)
                return;

            HediffDef vfDef = MedTraumaDefDatabase.VF;
            if (vfDef == null)
            {
                Log.Error("[MedTrauma] VF HediffDef not found! Cannot add ventricular fibrillation.");
                return;
            }

            var existingVF = pawn.health.hediffSet.hediffs
                .FirstOrDefault(h => h.def == vfDef && h.Part == heart);

            if (existingVF != null)
            {
                return; // 已存在，不重复添加
            }

            // 添加永久性的室颤 Hediff
            var vf = HediffMaker.MakeHediff(vfDef, pawn, heart);
            vf.Severity = 1f;
            pawn.health.AddHediff(vf, heart);
        }
    }
}
