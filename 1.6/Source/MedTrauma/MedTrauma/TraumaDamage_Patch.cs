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
    /// Mod 入口类
    /// </summary>
    public class MedTraumaMod : Mod
    {
        public MedTraumaMod(ModContentPack content) : base(content)
        {
            var harmony = new Harmony("com.Az1mus.MedTrauma");
            harmony.PatchAll();
        }

        public override string SettingsCategory() => "MedTrauma";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            // 预留设置界面
        }
    }

    /// <summary>
    /// 创伤伤害补丁：
    /// 1. 钝伤 → 概率对对应分组的一个随机骨头造成一次钝伤
    /// 2. 骨头受伤 → 对特定内脏造成刺伤（必定流血）
    /// 3. 胸骨/肋骨受伤 → 概率造成气胸
    /// </summary>
    public static class TraumaDamage_Patch
    {
        // 钝伤触发骨头二次伤害的概率
        private const float BluntToBoneChance = 0.3f;

        // 骨头受伤触发内脏刺伤的概率
        private const float BoneToOrganChance = 0.6f;

        // 刺伤伤害比例（相对于原始伤害）
        private const float StabDamageRatio = 0.4f;

        // 胸骨伤害触发气胸的阈值
        private const float SternumPneumothoraxThreshold = 5f;

        // 肋骨伤害触发气胸的阈值
        private const float RibPneumothoraxThreshold = 8f;

        // 肋骨触发气胸的概率
        private const float RibPneumothoraxChance = 0.4f;

        // 肺部伤害触发气胸的阈值
        private const float LungPneumothoraxThreshold = 5f;

        // 气胸初始 severity
        private const float PneumothoraxInitialSeverity = 0.05f;

        // 气胸最大 severity 增量
        private const float PneumothoraxMaxSeverityIncrease = 0.1f;

        // 补丁 DamageWorker_AddInjury.Apply 以捕获所有伤害处理
        [HarmonyPatch(typeof(DamageWorker_AddInjury), "Apply")]
        public static class DamageWorker_AddInjury_Apply_Patch
        {
            [HarmonyPostfix]
            public static void ApplyTraumaLogic(DamageInfo dinfo, Thing thing, DamageWorker.DamageResult __result)
            {
                // 只对 Pawn 应用创伤逻辑
                if (thing is not Pawn pawn)
                    return;

                // 确保有命中部位
                if (dinfo.HitPart == null)
                    return;

                var hitPart = dinfo.HitPart;
                var damageDef = dinfo.Def;
                var damageAmount = dinfo.Amount;
                var partDefName = hitPart.def.defName;

                // 1. 钝伤、枪伤 → 概率对对应分组的一个随机骨头造成一次钝伤
                if (damageDef == DamageDefOf.Blunt || damageDef == DamageDefOf.Bullet || damageDef == DamageDefOf.Bomb)
                {
                    ApplyBluntToBone(pawn, hitPart, damageAmount, dinfo);
                }

                // 2. 骨头受伤 → 对特定内脏造成刺伤（必定流血）
                if (IsBonePart(hitPart))
                {
                    ApplyBoneToOrgan(pawn, hitPart, damageAmount, dinfo);
                }

                // 3. 胸骨/肋骨/肺受伤 → 概率造成气胸
                if (partDefName == "Sternum")
                {
                    ApplySternumToPneumothorax(pawn, damageAmount);
                }
                else if (partDefName == "Ribcage")
                {
                    ApplyRibcageToPneumothorax(pawn, damageAmount);
                }
                else if (partDefName == "Lung")
                {
                    ApplyLungToPneumothorax(pawn, hitPart, damageAmount);
                }
            }
        }

        /// <summary>
        /// 判断是否是骨头部位
        /// </summary>
        static bool IsBonePart(BodyPartRecord part)
        {
            if (part == null || part.def == null)
                return false;

            var defName = part.def.defName;
            // 检查是否是骨头类部位（精确匹配原版 defName）
            return defName == "Clavicle" ||   // 锁骨
                   defName == "Ribcage" ||    // 肋骨
                   defName == "Sternum" ||    // 胸骨
                   defName == "Skull" ||      // 颅骨
                   defName == "Pelvis" ||     // 骨盆
                   defName == "Spine" ||      // 脊柱
                   defName == "Jaw" ||        // 下颌骨
                   defName == "Humerus" ||    // 肱骨
                   defName == "Radius" ||     // 桡骨
                   defName == "Femur" ||      // 股骨
                   defName == "Tibia";        // 胫骨
        }

        /// <summary>
        /// 获取与命中部位同组的所有骨头部位
        /// 逻辑：收集 hitPart 及其所有祖先的 groups，然后找这些 groups 中所有的骨头
        /// </summary>
        static List<BodyPartRecord> GetBonePartsInGroups(BodyPartRecord hitPart, Pawn pawn)
        {
            var boneParts = new List<BodyPartRecord>();

            if (pawn?.RaceProps?.body == null || hitPart == null)
                return boneParts;

            // 收集 hitPart 及其所有祖先的 groups
            var allRelevantGroups = new HashSet<BodyPartGroupDef>();
            var current = hitPart;
            while (current != null)
            {
                foreach (var g in current.groups)
                {
                    allRelevantGroups.Add(g);
                }
                current = current.parent;
            }

            var rootPart = pawn.RaceProps.body.corePart;
            var allParts = rootPart.GetPartAndAllChildParts().ToList();

            foreach (var part in allParts)
            {
                if (IsBonePart(part) && part.groups.Any(g => allRelevantGroups.Contains(g)))
                {
                    boneParts.Add(part);
                }
            }

            return boneParts;
        }

        /// <summary>
        /// 钝伤对随机骨头造成二次钝伤
        /// </summary>
        static void ApplyBluntToBone(Pawn pawn, BodyPartRecord hitPart, float damageAmount, DamageInfo originalDinfo)
        {
            if (!Rand.Chance(BluntToBoneChance))
            {
                return;
            }

            var boneParts = GetBonePartsInGroups(hitPart, pawn);
            if (boneParts.Count == 0)
            {
                return;
            }

            var availableBones = boneParts.Where(b => b != hitPart).ToList();
            if (availableBones.Count == 0)
            {
                return;
            }

            var targetBone = availableBones.RandomElement();

            var secondaryDamage = new DamageInfo(
                DamageDefOf.Blunt,
                damageAmount * 0.5f,
                originalDinfo.ArmorPenetrationInt,
                originalDinfo.Angle,
                originalDinfo.Instigator,
                targetBone,
                originalDinfo.Weapon,
                originalDinfo.Category,
                originalDinfo.IntendedTarget,
                originalDinfo.InstigatorGuilty
            );

            pawn.TakeDamage(secondaryDamage);
        }

        /// <summary>
        /// 根据受伤的骨头类型获取可能受伤的内脏列表
        /// </summary>
        static List<(string defName, float weight)> GetOrganTargetsForBone(string boneDefName)
        {
            var targets = new List<(string, float)>();

            switch (boneDefName)
            {
                case "Clavicle": // 锁骨 → 肺
                    targets.Add(("Lung", 1.0f));
                    targets.Add(("Heart", 0.5f));
                    break;

                case "Ribcage":  // 肋骨 → 肺、心脏、肝、肾
                    targets.Add(("Lung", 1.0f));
                    targets.Add(("Heart", 0.5f));
                    targets.Add(("Liver", 0.7f));
                    targets.Add(("Kidney", 0.5f));
                    break;

                case "Sternum":  // 胸骨 → 肺、心脏
                    targets.Add(("Lung", 1.0f));
                    targets.Add(("Heart", 0.5f));
                    break;

                case "Skull":    // 颅骨 → 大脑
                    targets.Add(("Brain", 1.0f));
                    break;

                default:
                    break;
            }

            return targets;
        }

        /// <summary>
        /// 获取指定部位类型的所有实例
        /// </summary>
        static List<BodyPartRecord> GetPartsByDefName(Pawn pawn, string defName)
        {
            var parts = new List<BodyPartRecord>();
            var allParts = pawn.RaceProps.body.corePart.GetPartAndAllChildParts();

            foreach (var part in allParts)
            {
                if (part.def.defName == defName)
                {
                    parts.Add(part);
                }
            }

            return parts;
        }

        /// <summary>
        /// 骨头受伤对特定内脏造成刺伤（必定流血）
        /// </summary>
        static void ApplyBoneToOrgan(Pawn pawn, BodyPartRecord hitPart, float damageAmount, DamageInfo originalDinfo)
        {

            if (!Rand.Chance(BoneToOrganChance))
            {
                return;
            }


            var organTargets = GetOrganTargetsForBone(hitPart.def.defName);

            if (organTargets.Count == 0)
            {
                return;
            }

            // 根据权重选择一个内脏
            var selectedTarget = organTargets.RandomElementByWeight(t => t.weight);

            var organParts = GetPartsByDefName(pawn, selectedTarget.defName);

            if (organParts.Count == 0)
            {
                return;
            }

            // 随机选择一个器官实例
            var targetOrgan = organParts.RandomElement();
            var stabDamageAmount = damageAmount * StabDamageRatio;


            // 创建刺伤伤害（使用 Cut 以产生流血效果）
            var stabDamage = new DamageInfo(
                DamageDefOf.Cut,  // 使用 Cut 造成流血
                stabDamageAmount,
                originalDinfo.ArmorPenetrationInt,
                originalDinfo.Angle,
                originalDinfo.Instigator,
                targetOrgan,
                originalDinfo.Weapon,
                originalDinfo.Category,
                originalDinfo.IntendedTarget,
                originalDinfo.InstigatorGuilty
            );

            // 确保伤害作用于外部以产生流血
            stabDamage.SetBodyRegion(BodyPartHeight.Undefined, BodyPartDepth.Outside);


            pawn.TakeDamage(stabDamage);
        }

        /// <summary>
        /// 胸骨受伤造成气胸
        /// </summary>
        static void ApplySternumToPneumothorax(Pawn pawn, float damageAmount)
        {
            if (damageAmount < SternumPneumothoraxThreshold)
                return;

            var lungs = GetPartsByDefName(pawn, "Lung");
            if (lungs.Count == 0)
                return;

            var targetLung = lungs.RandomElement();
            AddOrUpdatePneumothorax(pawn, targetLung);
        }

        /// <summary>
        /// 肋骨受伤概率造成气胸
        /// </summary>
        static void ApplyRibcageToPneumothorax(Pawn pawn, float damageAmount)
        {
            if (damageAmount < RibPneumothoraxThreshold)
                return;

            if (!Rand.Chance(RibPneumothoraxChance))
                return;

            var lungs = GetPartsByDefName(pawn, "Lung");
            if (lungs.Count == 0)
                return;

            var targetLung = lungs.RandomElement();
            AddOrUpdatePneumothorax(pawn, targetLung);
        }

        static void ApplyLungToPneumothorax(Pawn pawn, BodyPartRecord hitPart, float damageAmount)
        {
            if (damageAmount < LungPneumothoraxThreshold)
                return;

            var targetLung = hitPart;
            AddOrUpdatePneumothorax(pawn, targetLung);
        }

        /// <summary>
        /// 添加或更新气胸 Hediff
        /// </summary>
        static void AddOrUpdatePneumothorax(Pawn pawn, BodyPartRecord lung)
        {
            var existingPneumothorax = pawn.health.hediffSet.hediffs
                .FirstOrDefault(h => h.def.defName == "Pneumothorax" && h.Part == lung);

            if (existingPneumothorax != null)
            {
                // 已有气胸，增加 severity（最多 0.1）
                float newSeverity = Math.Min(existingPneumothorax.Severity + PneumothoraxMaxSeverityIncrease, 1f);
                existingPneumothorax.Severity = newSeverity;
            }
            else
            {
                // 创建新的气胸 Hediff
                var pneumothoraxDef = DefDatabase<HediffDef>.GetNamed("Pneumothorax");
                if (pneumothoraxDef != null)
                {
                    var pneumothorax = pawn.health.AddHediff(pneumothoraxDef, lung);
                    pneumothorax.Severity = PneumothoraxInitialSeverity;
                }
            }
        }
    }
}
