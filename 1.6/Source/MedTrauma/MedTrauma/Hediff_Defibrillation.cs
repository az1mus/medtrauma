using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MedTrauma
{
    /// <summary>
    /// 除颤并发症 - 限制 VF 的 severity 最大值
    /// </summary>
    public class HediffComp_Defibrillation : HediffComp
    {
        public HediffCompProperties_Defibrillation Props => (HediffCompProperties_Defibrillation)this.props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (Pawn?.health?.hediffSet == null)
                return;

            // 获取 VF Hediff Def
            var vfDef = DefDatabase<HediffDef>.GetNamedSilentFail("VF");
            if (vfDef == null)
                return;

            // 限制所有心脏部位 VF 的 severity 最大值
            var heartParts = Pawn.health.hediffSet.GetNotMissingParts()
                .Where(p => p.def.defName.Contains("Heart")).ToList();

            foreach (var part in heartParts)
            {
                var vf = Pawn.health.hediffSet.hediffs
                    .FirstOrDefault(h => h.def == vfDef && h.Part == part);

                if (vf != null && vf.Severity > Props.vfMaxSeverity)
                {
                    vf.Severity = Props.vfMaxSeverity;
                }
            }
        }
    }

    /// <summary>
    /// 除颤并发症属性
    /// </summary>
    public class HediffCompProperties_Defibrillation : HediffCompProperties
    {
        public float vfMaxSeverity;

        public HediffCompProperties_Defibrillation()
        {
            compClass = typeof(HediffComp_Defibrillation);
        }
    }

    /// <summary>
    /// 脉冲除颤手术 Worker
    /// </summary>
    public class RecipeWorker_PulseDefibrillation : RecipeWorker
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            if (pawn?.health?.hediffSet?.hediffs == null)
                yield break;

            // 查找所有有 VF Hediff 的心脏部位
            var vfDef = DefDatabase<HediffDef>.GetNamedSilentFail("VF");
            if (vfDef == null)
                yield break;

            foreach (var hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff.def == vfDef &&
                    hediff.Part != null &&
                    hediff.Part.def.defName.Contains("Heart") &&
                    hediff.Severity > 0.01f)
                {
                    yield return hediff.Part;
                }
            }
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            // 移除该部位的 VF Hediff
            var vfDef = DefDatabase<HediffDef>.GetNamedSilentFail("VF");
            if (vfDef != null)
            {
                var vf = pawn.health.hediffSet.hediffs
                    .FirstOrDefault(h => h.def == vfDef && h.Part == part);

                if (vf != null)
                {
                    pawn.health.RemoveHediff(vf);
                }
            }

            // 应用除颤 Hediff
            var defibDef = DefDatabase<HediffDef>.GetNamedSilentFail("Defibrillation");
            if (defibDef != null)
            {
                var defib = HediffMaker.MakeHediff(defibDef, pawn, part);
                pawn.health.AddHediff(defib, part);
            }
        }

        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            if (!(thing is Pawn pawn))
                return false;

            if (part == null)
                return false;

            // 检查指定部位是否有 VF 且 severity > 0.01
            var vfDef = DefDatabase<HediffDef>.GetNamedSilentFail("VF");
            if (vfDef == null)
                return false;

            return pawn.health?.hediffSet?.hediffs?.Any(h =>
                h.def == vfDef &&
                h.Part == part &&
                h.Severity > 0.01f) == true;
        }

        public override AcceptanceReport AvailableReport(Thing thing, BodyPartRecord part = null)
        {
            if (!(thing is Pawn pawn))
                return new AcceptanceReport("Not a pawn");

            // 检查是否有 VF
            var vfDef = DefDatabase<HediffDef>.GetNamedSilentFail("VF");
            if (vfDef == null)
                return new AcceptanceReport("VF def not found");

            bool hasVF = pawn.health?.hediffSet?.hediffs?.Any(h =>
                h.def == vfDef &&
                h.Visible) == true;

            if (!hasVF)
                return new AcceptanceReport("No ventricular fibrillation");

            return AcceptanceReport.WasAccepted;
        }
    }

    /// <summary>
    /// 人工 AED 手术 Worker
    /// </summary>
    public class RecipeWorker_ArtificialAED : RecipeWorker
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            if (pawn?.health?.hediffSet?.hediffs == null)
                yield break;

            // 查找所有有 VF Hediff 的心脏部位
            var vfDef = DefDatabase<HediffDef>.GetNamedSilentFail("VF");
            if (vfDef == null)
                yield break;

            foreach (var hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff.def == vfDef &&
                    hediff.Part != null &&
                    hediff.Part.def.defName.Contains("Heart") &&
                    hediff.Severity > 0.01f)
                {
                    yield return hediff.Part;
                }
            }
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            // 移除该部位的 VF Hediff
            var vfDef = DefDatabase<HediffDef>.GetNamedSilentFail("VF");
            if (vfDef != null)
            {
                var vf = pawn.health.hediffSet.hediffs
                    .FirstOrDefault(h => h.def == vfDef && h.Part == part);

                if (vf != null)
                {
                    pawn.health.RemoveHediff(vf);
                }
            }

            // 应用人工 AED Hediff
            var aedDef = DefDatabase<HediffDef>.GetNamedSilentFail("ArtificialAED");
            if (aedDef != null)
            {
                var aed = HediffMaker.MakeHediff(aedDef, pawn, part);
                pawn.health.AddHediff(aed, part);
            }
        }

        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            if (!(thing is Pawn pawn))
                return false;

            if (part == null)
                return false;

            // 检查指定部位是否有 VF 且 severity > 0.01
            var vfDef = DefDatabase<HediffDef>.GetNamedSilentFail("VF");
            if (vfDef == null)
                return false;

            return pawn.health?.hediffSet?.hediffs?.Any(h =>
                h.def == vfDef &&
                h.Part == part &&
                h.Severity > 0.01f) == true;
        }

        public override AcceptanceReport AvailableReport(Thing thing, BodyPartRecord part = null)
        {
            if (!(thing is Pawn pawn))
                return new AcceptanceReport("Not a pawn");

            // 检查是否有 VF
            var vfDef = DefDatabase<HediffDef>.GetNamedSilentFail("VF");
            if (vfDef == null)
                return new AcceptanceReport("VF def not found");

            bool hasVF = pawn.health?.hediffSet?.hediffs?.Any(h =>
                h.def == vfDef &&
                h.Visible) == true;

            if (!hasVF)
                return new AcceptanceReport("No ventricular fibrillation");

            return AcceptanceReport.WasAccepted;
        }
    }
}
