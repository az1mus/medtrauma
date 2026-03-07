using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MedTrauma
{
    /// <summary>
    /// Pawn 的失血状态组件 - 存储心肺耦合模型的状态
    /// </summary>
    public class BleedingState : IExposable
    {
        /// <summary>
        /// 心脏效率因子 (Heart efficiency factor)
        /// 范围：0.0 - 1.0
        /// </summary>
        public float heartEfficiencyFactor = 1.0f;

        /// <summary>
        /// 血氧容量值 (Blood oxygen capacity)
        /// 范围：0.0 - 1.0
        /// </summary>
        public float bloodOxygen = 1.0f;

        /// <summary>
        /// 上一刻的心脏功能状态 (用于耦合计算)
        /// </summary>
        private float prevHeartFunction = 1.0f;

        /// <summary>
        /// 上一刻的肺功能状态 (用于耦合计算)
        /// </summary>
        private float prevLungFunction = 1.0f;

        /// <summary>
        /// 模型参数 - Hill 系数
        /// </summary>
        private const float HillCoefficientHeart = 1.5f;
        private const float HillCoefficientLung = 1.5f; 

        /// <summary>
        /// 模型参数 - 缺氧阈值
        /// </summary>
        private const float KHeart = 0.15f;
        private const float KLung = 0.02f;

        /// <summary>
        /// 模型参数 - 受损反馈系数
        /// </summary>
        private const float Alpha = 0.2f; // 心脏对肺
        private const float Beta = 0.3f; // 肺对心脏

        public BleedingState()
        {
        }

        /// <summary>
        /// 更新心肺耦合模型状态
        /// </summary>
        /// <param name="bloodLossSeverity">失血严重程度 (0-1, 1 表示完全失血)</param>
        /// <param name="lungEfficiency">肺效率 (直接使用 breathing capacity，0-1)</param>
        public void UpdateState(float bloodLossSeverity, float lungEfficiency)
        {
            // B(t) = 1 - 失血量
            float currentB = 1.0f - bloodLossSeverity;
            currentB = Mathf.Clamp(currentB, 0.0f, 1.0f);

            // --- 更新心脏功能 H(t) ---
            // 逻辑：受当前氧供的缺氧抑制 + 受肺效率的阻力影响
            // Hill 函数模拟缺氧导致的收缩力下降
            float hypoxiaEffectHeart = CalculateHillFunction(bloodOxygen, HillCoefficientHeart, KHeart);
            float lungBurdenEffect = 1.0f - Alpha * (1.0f - lungEfficiency);

            float currentHeartFunction = hypoxiaEffectHeart * lungBurdenEffect;
            currentHeartFunction = Mathf.Clamp(currentHeartFunction, 0.01f, 1.0f);

            float hypoxiaEffectLung = CalculateHillFunction(bloodOxygen, HillCoefficientLung, KLung);
            float heartBurdenEffect = 1.0f - Beta * (1.0f - prevHeartFunction);

            float currentLungFunction = hypoxiaEffectLung * heartBurdenEffect;
            currentLungFunction = Mathf.Clamp(currentLungFunction, 0.01f, 1.0f);

            // --- 计算当前氧供 ---
            // O = B * H * L 
            float currentO = currentB * Mathf.Min(currentHeartFunction * 1.2f, 1f) * currentLungFunction;
            currentO = Mathf.Clamp(currentO, 0.01f, 1.0f);

            // --- 更新状态 ---
            heartEfficiencyFactor = currentHeartFunction;
            bloodOxygen = currentO;

            // 更新"上一刻"状态，供下一次循环使用
            prevHeartFunction = currentHeartFunction;
            // prevLungFunction = lungEfficiency;
        }

        /// <summary>
        /// Hill 函数 - 模拟缺氧对器官功能的影响
        /// </summary>
        /// <param name="oxygen">氧供水平</param>
        /// <param name="n">Hill 系数 (陡峭度)</param>
        /// <param name="K">阈值参数</param>
        /// <returns>器官功能效率 (0-1)</returns>
        private float CalculateHillFunction(float oxygen, float n, float K)
        {
            if (K <= 0f) return 1.0f;
            float oxygenN = Mathf.Pow(oxygen, n);
            float KN = Mathf.Pow(K, n);
            return oxygenN / (oxygenN + KN);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref heartEfficiencyFactor, "heartEfficiencyFactor", 1.0f);
            Scribe_Values.Look(ref bloodOxygen, "bloodOxygen", 1.0f);
            Scribe_Values.Look(ref prevHeartFunction, "prevHeartFunction", 1.0f);
            Scribe_Values.Look(ref prevLungFunction, "prevLungFunction", 1.0f);
        }
    }

    /// <summary>
    /// Pawn 状态管理器 - 使用静态字典存储每个 Pawn 的 BleedingState
    /// </summary>
    public static class PawnBleedingStateManager
    {
        private static readonly Dictionary<Pawn, BleedingState> _states = new Dictionary<Pawn, BleedingState>();

        /// <summary>
        /// 获取或创建 Pawn 的 BleedingState
        /// </summary>
        public static BleedingState GetState(Pawn pawn)
        {
            if (pawn == null) return null;

            if (!_states.ContainsKey(pawn))
            {
                _states[pawn] = new BleedingState();
            }
            return _states[pawn];
        }

        /// <summary>
        /// 移除 Pawn 的状态（当 Pawn 被移除时调用）
        /// </summary>
        public static void RemoveState(Pawn pawn)
        {
            if (pawn != null && _states.ContainsKey(pawn))
            {
                _states.Remove(pawn);
            }
        }
    }
}
