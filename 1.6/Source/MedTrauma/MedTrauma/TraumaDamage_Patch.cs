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
    /// 
    /// 注意：补丁 ApplyDamageToPart 而不是 Apply，因为前者在设置 HitPart 之后才被调用
    /// </summary>
    public static class TraumaDamage_Patch
    {
        // 从 XML 获取配置
        private static TraumaConfigDef Config => DefDatabase<TraumaConfigDef>.GetNamed("DefaultTraumaConfig");

        // 补丁 DamageWorker_AddInjury.ApplyDamageToPart - 此时 HitPart 已经被正确设置
        [HarmonyPatch(typeof(DamageWorker_AddInjury), "ApplyDamageToPart")]
        public static class DamageWorker_AddInjury_ApplyDamageToPart_Patch
        {
            [HarmonyPostfix]
            public static void ApplyTraumaLogic(DamageInfo dinfo, Pawn pawn, DamageWorker.DamageResult result)
            {
                var config = Config;
                if (config == null) return;

                // 只对 Pawn 应用创伤逻辑
                if (pawn == null)
                    return;

                // 确保有命中部位（此时已经被 ApplyDamageToPart 设置）
                if (dinfo.HitPart == null)
                    return;

                var hitPart = dinfo.HitPart;
                var damageDef = dinfo.Def;
                var damageAmount = dinfo.Amount;
                var partDefName = hitPart.def.defName;

                // 1. 钝伤、枪伤 → 概率对对应分组的一个随机骨头造成一次钝伤
                if (damageDef == DamageDefOf.Blunt || damageDef == DamageDefOf.Bullet || damageDef == DamageDefOf.Bomb)
                {
                    ApplyBluntToBone(pawn, hitPart, damageAmount, dinfo, config);
                }

                // 2. 骨头受伤 → 对特定内脏造成刺伤（必定流血）
                if (IsBonePart(hitPart, config))
                {
                    ApplyBoneToOrgan(pawn, hitPart, damageAmount, dinfo, config);
                }

                // 3. 胸骨/肋骨/肺受伤 → 概率造成气胸
                ApplyPneumothorax(pawn, hitPart, partDefName, damageAmount, config);
            }
        }

        /// <summary>
        /// 判断是否是骨头部位
        /// </summary>
        static bool IsBonePart(BodyPartRecord part, TraumaConfigDef config)
        {
            if (part == null || part.def == null || config?.boneParts == null)
                return false;

            var defName = part.def.defName;
            return config.boneParts.Any(b => b.defName == defName);
        }

        /// <summary>
        /// 获取与命中部位同组的所有骨头部位
        /// </summary>
        static List<BodyPartRecord> GetBonePartsInGroups(BodyPartRecord hitPart, Pawn pawn, TraumaConfigDef config)
        {
            var boneParts = new List<BodyPartRecord>();

            if (pawn?.RaceProps?.body == null || hitPart == null || config?.boneParts == null)
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
                if (IsBonePart(part, config) && part.groups.Any(g => allRelevantGroups.Contains(g)))
                {
                    boneParts.Add(part);
                }
            }

            return boneParts;
        }

        /// <summary>
        /// 钝伤对随机骨头造成二次钝伤
        /// </summary>
        static void ApplyBluntToBone(Pawn pawn, BodyPartRecord hitPart, float damageAmount, DamageInfo originalDinfo, TraumaConfigDef config)
        {
            if (!Rand.Chance(config.bluntToBoneChance))
            {
                return;
            }

            var boneParts = GetBonePartsInGroups(hitPart, pawn, config);
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
                damageAmount * config.bluntSecondaryDamageRatio,
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
        static List<(string defName, float weight)> GetOrganTargetsForBone(string boneDefName, TraumaConfigDef config)
        {
            if (config?.boneOrganMappings == null)
                return new List<(string, float)>();

            var mapping = config.boneOrganMappings.FirstOrDefault(m => m.boneDefName == boneDefName);
            if (mapping?.organTargets == null)
                return new List<(string, float)>();

            return mapping.organTargets.Select(t => (t.defName, t.weight)).ToList();
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
        static void ApplyBoneToOrgan(Pawn pawn, BodyPartRecord hitPart, float damageAmount, DamageInfo originalDinfo, TraumaConfigDef config)
        {
            if (!Rand.Chance(config.boneToOrganChance))
            {
                return;
            }

            var organTargets = GetOrganTargetsForBone(hitPart.def.defName, config);

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
            var stabDamageAmount = damageAmount * config.stabDamageRatio;

            // 创建刺伤伤害（使用 Cut 以产生流血效果）
            var stabDamage = new DamageInfo(
                DamageDefOf.Cut,
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

            stabDamage.SetBodyRegion(BodyPartHeight.Undefined, BodyPartDepth.Outside);

            pawn.TakeDamage(stabDamage);
        }

        /// <summary>
        /// 应用气胸逻辑
        /// </summary>
        static void ApplyPneumothorax(Pawn pawn, BodyPartRecord hitPart, string partDefName, float damageAmount, TraumaConfigDef config)
        {
            if (config?.pneumothoraxTriggers == null)
                return;

            var trigger = config.pneumothoraxTriggers.FirstOrDefault(t => t.partDefName == partDefName);
            if (trigger == null)
                return;

            if (damageAmount < trigger.damageThreshold)
                return;

            if (trigger.chance < 1f && !Rand.Chance(trigger.chance))
                return;

            BodyPartRecord targetLung;
            if (partDefName == "Lung")
            {
                targetLung = hitPart;
            }
            else
            {
                var lungs = GetPartsByDefName(pawn, "Lung");
                if (lungs.Count == 0)
                    return;

                targetLung = lungs.RandomElement();
            }

            AddOrUpdatePneumothorax(pawn, targetLung, config);
        }

        /// <summary>
        /// 添加或更新气胸 Hediff
        /// </summary>
        static void AddOrUpdatePneumothorax(Pawn pawn, BodyPartRecord lung, TraumaConfigDef config)
        {
            var existingPneumothorax = pawn.health.hediffSet.hediffs
                .FirstOrDefault(h => h.def.defName == "Pneumothorax" && h.Part == lung);

            if (existingPneumothorax != null)
            {
                float newSeverity = Math.Min(existingPneumothorax.Severity + config.pneumothoraxMaxSeverityIncrease, 1f);
                existingPneumothorax.Severity = newSeverity;
            }
            else
            {
                var pneumothoraxDef = DefDatabase<HediffDef>.GetNamed("Pneumothorax");
                if (pneumothoraxDef != null)
                {
                    var pneumothorax = pawn.health.AddHediff(pneumothoraxDef, lung);
                    pneumothorax.Severity = config.pneumothoraxInitialSeverity;
                }
            }
        }
    }
}
