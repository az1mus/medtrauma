using RimWorld;
using Verse;
using System;
using System.Linq;
using UnityEngine;

namespace MedTrauma
{
    /// <summary>
    /// 血氧容量计算器 - Breathing 和 BloodPumping 共同影响血氧水平
    /// </summary>
    public class PawnCapacityWorker_BloodOxygen : PawnCapacityWorker
    {
        private static readonly string[] _organs = { "Brain", "Liver", "Heart", "Stomach" };

        public override float CalculateCapacityLevel(HediffSet diffSet, System.Collections.Generic.List<PawnCapacityUtility.CapacityImpactor> impactors = null)
        {
            Pawn pawn = diffSet.pawn;

            float breathing = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Breathing);
            float pumping = pawn.health.capacities.GetLevel(PawnCapacityDefOf.BloodPumping);

            float bloodOxygen = (float)(Math.Log(breathing * pumping + 1) / Math.Log(2)) * 1.2f;

            // 确保器官有 HypoxiaOrgan Hediff
            EnsureHypoxiaOrganHediffs(pawn, bloodOxygen);

            return bloodOxygen;
        }

        static void EnsureHypoxiaOrganHediffs(Pawn pawn, float bloodOxygen)
        {
            if (pawn.RaceProps?.body == null || pawn.health?.hediffSet == null)
                return;

            HediffDef hypoxiaDef = MedTraumaDefDatabase.HypoxiaOrgan;
            if (hypoxiaDef == null)
                return;

            float severity = Mathf.Clamp(1f - bloodOxygen, 0f, 1f);

            foreach (var organ in _organs)
            {
                var parts = pawn.RaceProps.body.corePart.GetPartAndAllChildParts()
                    .Where(p => p.def.defName == organ).ToList();

                foreach (var part in parts)
                {
                    var hypoxia = pawn.health.hediffSet.hediffs
                        .FirstOrDefault(h => h.def == hypoxiaDef && h.Part == part);

                    if (hypoxia == null)
                    {
                        hypoxia = HediffMaker.MakeHediff(hypoxiaDef, pawn, part);
                        hypoxia.Severity = severity;
                        pawn.health.AddHediff(hypoxia, part);
                    }
                    else
                    {
                        hypoxia.Severity = severity;
                    }
                }
            }
        }

        public override bool CanHaveCapacity(BodyDef body)
        {
            return body.HasPartWithTag(BodyPartTagDefOf.BreathingSource) ||
                   body.HasPartWithTag(BodyPartTagDefOf.BloodPumpingSource);
        }
    }
}
