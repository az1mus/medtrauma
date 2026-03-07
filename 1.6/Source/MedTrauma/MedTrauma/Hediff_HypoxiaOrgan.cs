using Verse;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;

namespace MedTrauma
{
    /// <summary>
    /// 器官低氧症并发症 - 根据血氧水平增长或恢复
    /// 应用于脑
    /// </summary>
    public class HediffComp_HypoxiaOrgan : HediffComp_SeverityPerDay
    {
        private const float THRESHOLD_TO_APPLY = 0.9f;
        private const float THRESHOLD_TO_RECOVER = 0.91f;

        public HediffCompProperties_HypoxiaOrgan Props => (HediffCompProperties_HypoxiaOrgan)this.props;

        public override float SeverityChangePerDay()
        {
            if (Pawn == null) return 0f;

            var state = PawnBleedingStateManager.GetState(Pawn);
            if (state == null) return 0f;

            if (state.bloodOxygen < THRESHOLD_TO_APPLY)
            {
                return Props.severityPerDay;
            }
            else if (state.bloodOxygen > THRESHOLD_TO_RECOVER)
            {
                return -Props.recoveryPerDay;
            }

            return 0f;
        }
    }

    /// <summary>
    /// 器官低氧症并发症属性
    /// </summary>
    public class HediffCompProperties_HypoxiaOrgan : HediffCompProperties_SeverityPerDay
    {
        public float recoveryPerDay;

        public HediffCompProperties_HypoxiaOrgan()
        {
            compClass = typeof(HediffComp_HypoxiaOrgan);
        }
    }

    /// <summary>
    /// 心室颤动并发症 - 根据血氧水平增长或恢复，severity 达到 1 时致死
    /// 应用于心脏
    /// </summary>
    public class HediffComp_VF : HediffComp_SeverityPerDay
    {
        private const float THRESHOLD_TO_APPLY = 0.2f;
        private const float THRESHOLD_TO_RECOVER = 0.6f;

        public HediffCompProperties_VF Props => (HediffCompProperties_VF)this.props;

        public override float SeverityChangePerDay()
        {
            if (Pawn == null) return 0f;

            var state = PawnBleedingStateManager.GetState(Pawn);
            if (state == null) return 0f;

            if (state.bloodOxygen < THRESHOLD_TO_APPLY)
            {
                return Props.severityPerDay;
            }
            else if (state.bloodOxygen > THRESHOLD_TO_RECOVER)
            {
                return -Props.recoveryPerDay;
            }

            return 0f;
        }
    }

    /// <summary>
    /// 心室颤动并发症属性
    /// </summary>
    public class HediffCompProperties_VF : HediffCompProperties_SeverityPerDay
    {
        public float recoveryPerDay;

        public HediffCompProperties_VF()
        {
            compClass = typeof(HediffComp_VF);
        }
    }
}
