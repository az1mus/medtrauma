using Verse;
using RimWorld;
using UnityEngine;

namespace MedTrauma
{
    /// <summary>
    /// 低血氧症器官影响组件 - 根据血氧水平动态更新器官 Hediff 的 severity
    /// 每秒更新一次，血氧越低 severity 越高
    /// </summary>
    public class HediffComp_HypoxiaOrgan : HediffComp
    {
        public HediffCompProperties_HypoxiaOrgan Props => (HediffCompProperties_HypoxiaOrgan)props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            // 每 60 tick（1 秒）更新一次
            if (Pawn.IsHashIntervalTick(60))
                UpdateSeverity();
        }

        void UpdateSeverity()
        {
            if (Pawn.health?.capacities == null)
                return;

            PawnCapacityDef bloodOxygenDef = MedTraumaDefDatabase.BloodOxygen;
            if (bloodOxygenDef == null)
                return;

            float bloodOxygen = Pawn.health.capacities.GetLevel(bloodOxygenDef);

            // 血氧越低，severity 越高（1 - bloodOxygen）
            float targetSeverity = Mathf.Clamp(1f - bloodOxygen, 0f, 1f);

            // 直接设置 severity
            parent.Severity = targetSeverity;
        }
    }

    public class HediffCompProperties_HypoxiaOrgan : HediffCompProperties
    {
        public HediffCompProperties_HypoxiaOrgan()
        {
            compClass = typeof(HediffComp_HypoxiaOrgan);
        }
    }
}
