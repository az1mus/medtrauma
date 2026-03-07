using RimWorld;
using Verse;

namespace MedTrauma
{
    /// <summary>
    /// Mod Def 数据库辅助类
    /// </summary>
    public static class MedTraumaDefDatabase
    {
        private static PawnCapacityDef _bloodOxygen;
        private static HediffDef _hypoxiaOrgan;
        private static HediffDef _vf;

        /// <summary>
        /// 血氧容量 Def
        /// </summary>
        public static PawnCapacityDef BloodOxygen
        {
            get
            {
                if (_bloodOxygen == null)
                {
                    _bloodOxygen = DefDatabase<PawnCapacityDef>.GetNamed("BloodOxygen");
                }
                return _bloodOxygen;
            }
        }

        /// <summary>
        /// 器官低氧症 Hediff Def
        /// </summary>
        public static HediffDef HypoxiaOrgan
        {
            get
            {
                if (_hypoxiaOrgan == null)
                {
                    _hypoxiaOrgan = DefDatabase<HediffDef>.GetNamed("HypoxiaOrgan");
                }
                return _hypoxiaOrgan;
            }
        }

        /// <summary>
        /// 心室颤动 Hediff Def
        /// </summary>
        public static HediffDef VF
        {
            get
            {
                if (_vf == null)
                {
                    _vf = DefDatabase<HediffDef>.GetNamed("VF");
                }
                return _vf;
            }
        }
    }
}
