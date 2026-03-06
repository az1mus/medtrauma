using System.Collections.Generic;
using Verse;

namespace MedTrauma
{
    /// <summary>
    /// 创伤配置 Def - 所有数据从 XML 加载
    /// </summary>
    public class TraumaConfigDef : Def
    {
        public float bluntToBoneChance;
        public float boneToOrganChance;
        public float stabDamageRatio;
        public float bluntSecondaryDamageRatio;
        public float sternumPneumothoraxThreshold;
        public float ribcagePneumothoraxThreshold;
        public float ribcagePneumothoraxChance;
        public float lungPneumothoraxThreshold;
        public float pneumothoraxInitialSeverity;
        public float pneumothoraxMaxSeverityIncrease;
        public List<BonePartDef> boneParts;
        public List<BoneOrganMapping> boneOrganMappings;
        public List<PneumothoraxTriggerDef> pneumothoraxTriggers;
    }

    public class BonePartDef
    {
        public string defName;
    }

    public class BoneOrganMapping
    {
        public string boneDefName;
        public List<OrganTargetDef> organTargets;
    }

    public class OrganTargetDef
    {
        public string defName;
        public float weight = 1f;
    }

    public class PneumothoraxTriggerDef
    {
        public string partDefName;
        public float damageThreshold;
        public float chance = 1f;
    }
}
