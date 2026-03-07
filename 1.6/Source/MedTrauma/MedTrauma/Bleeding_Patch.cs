using HarmonyLib;
using Verse;

namespace MedTrauma
{
    /// <summary>
    /// 全局失血速率 2 倍补丁
    /// 修改 HediffSet.CalculateBleedRate 的返回值
    /// </summary>
    [HarmonyPatch(typeof(HediffSet), "CalculateBleedRate", MethodType.Normal)]
    public static class HediffSet_CalculateBleedRate_BleedMultiplier_Patch
    {
        [HarmonyPostfix]
        static void MultiplyBleedRate(ref float __result)
        {
            __result *= 2f;  // 2 倍失血速率
        }
    }
}
