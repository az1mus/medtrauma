# Harmony Mod 编写指南

> 基于 [Harmony 官方文档](https://harmony.pardeike.net/) 整理的 RimWorld 模组开发最小必要知识

---

## 目录

1. [基础概念](#基础概念)
2. [补丁方法定义方式](#补丁方法定义方式)
3. [Prefix 前置补丁](#prefix-前置补丁)
4. [Postfix 后置补丁](#postfix-后置补丁)
5. [特殊参数注入](#特殊参数注入)
6. [Transpiler 转译器](#transpiler-转译器)
7. [Finalizer 终结器](#finalizer-终结器)
8. [最佳实践](#最佳实践)

---

## 基础概念

### Patch 执行顺序

```
Prefix → Original → Postfix → Finalizer
```

### 基本规则

- 所有补丁方法必须是 **`static`** 的
- 使用 `[HarmonyPatch]` 属性指定目标类型和方法
- 类必须至少有一个 `[HarmonyPatch]` 属性才能让 Harmony 找到它

### 基本模板

```csharp
using HarmonyLib;
using RimWorld;
using Verse;

[HarmonyPatch(typeof(TargetClass), "TargetMethod")]
public static class TargetClass_TargetMethod_Patch
{
    [HarmonyPrefix]
    static void MyPrefix() { }

    [HarmonyPostfix]
    static void MyPostfix() { }
}
```

---

## 补丁方法定义方式

### 方式一：标准名称（无需属性）

```csharp
[HarmonyPatch(typeof(TargetClass), "TargetMethod")]
class Patch
{
    static void Prefix() { }           // 自动识别为前置补丁
    static void Postfix() { }          // 自动识别为后置补丁
    static IEnumerable<CodeInstruction> Transpiler() { }
    static Exception Finalizer(Exception ex) { }
}
```

### 方式二：属性标注（推荐，可自定义名称）

```csharp
[HarmonyPatch(typeof(TargetClass), "TargetMethod")]
class Patch
{
    [HarmonyPrefix]
    static void CheckCondition() { }   // 自定义名称，语义更清晰

    [HarmonyPostfix]
    static void LogResult() { }        // 自定义名称，表达用途

    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> ModifyIL(
        IEnumerable<CodeInstruction> instructions) { }

    [HarmonyFinalizer]
    static Exception HandleException(Exception ex) { }
}
```

### 两种方式对比

| 特性 | 标准名称 | 属性方式 |
|------|---------|---------|
| 方法名 | 固定（Prefix/Postfix 等） | 自定义，可表达用途 |
| 可读性 | 名称即类型 | 需查看属性 |
| 灵活性 | 较低 | 更高 |
| 推荐场景 | 简单补丁 | 复杂项目、需清晰语义 |

---

## Prefix 前置补丁

### 方法签名规则

- 使用 `[HarmonyPrefix]` 属性标注
- 必须是 `static` 方法
- 返回值：
  - `void`：正常执行后续补丁和原方法
  - `bool`：返回 `false` **跳过**后续 Prefix 和原方法；返回 `true` 继续执行

### 参数访问和修改

```csharp
public class OriginalCode
{
    public void Test(int counter, string name) { }
}

[HarmonyPatch(typeof(OriginalCode), nameof(OriginalCode.Test))]
class Patch
{
    [HarmonyPrefix]
    static void ModifyParameters(int counter, ref string name)
    {
        Log.Message($"counter = {counter}");
        name = "修改后的值";  // 通过 ref 修改参数
    }
}
```

### 返回值控制（跳过原方法）

```csharp
public class OriginalCode
{
    public string GetName() => name;
}

[HarmonyPatch(typeof(OriginalCode), nameof(OriginalCode.GetName))]
class Patch
{
    [HarmonyPrefix]
    static bool ReplaceMethod(ref string __result)
    {
        __result = "直接返回的值";  // 设置返回值
        return false;               // 跳过原方法
    }
}
```

---

## Postfix 后置补丁

### 方法签名规则

- 使用 `[HarmonyPostfix]` 属性标注
- 返回值通常为 `void`，但可使用**透传 Postfix**返回与原方法相同类型
- **始终执行**（无论 Prefix 或原方法如何）

### 返回值访问和修改

```csharp
public class OriginalCode
{
    public string GetName() => "David";
}

[HarmonyPatch(typeof(OriginalCode), nameof(OriginalCode.GetName))]
class Patch
{
    [HarmonyPostfix]
    static void ModifyResult(ref string __result)
    {
        if (__result == "foo")
            __result = "bar";
    }
}
```

### 透传 Postfix（Pass Through）

适用于无法使用 `ref` 的类型（如 `IEnumerable<T>`）：

```csharp
[HarmonyPatch(typeof(OriginalCode), nameof(OriginalCode.GetNumbers))]
class Patch
{
    [HarmonyPostfix]
    static IEnumerable<int> ProcessNumbers(IEnumerable<int> values)
    {
        yield return 0;
        foreach (var value in values)
            yield return value * 10;
        yield return 99;
    }
}
```

---

## 特殊参数注入

### 参数注入规则

| 参数名 | 用途 | 类型要求 |
|--------|------|----------|
| `__instance` | 访问原方法实例（this） | 原方法的类类型 |
| `__result` | 访问/修改返回值 | 与原方法返回类型匹配 |
| `__state` | Prefix↔Postfix 传递状态 | 任意类型 |
| `___字段名` | 访问私有字段 | 与字段类型相同 |
| `__args` | 访问所有参数数组 | `object[]` |
| `__originalMethod` | 获取原方法信息 | `MethodBase` |
| `__runOriginal` | 判断原方法是否执行 | `bool`（只读） |
| 原参数名 | 访问原方法参数 | 与原方法参数名和类型一致 |

### `__instance` - 访问实例

```csharp
public class Player
{
    private int health = 100;
    public int GetHealth() => health;
}

[HarmonyPatch(typeof(Player), nameof(Player.GetHealth))]
class Patch
{
    [HarmonyPostfix]
    static void CheckHealth(Player __instance, ref int __result)
    {
        if (__instance.health < 10)
            __result = 0;  // 濒死时显示 0
    }
}
```

### `__state` - 状态传递

**注意**：Prefix 和 Postfix 必须在**同一个类**中

```csharp
[HarmonyPatch(typeof(Calculator), nameof(Calculator.Divide))]
class Patch
{
    [HarmonyPrefix]
    static void StartTimer(int b, out Stopwatch __state)
    {
        __state = Stopwatch.StartNew();
    }

    [HarmonyPostfix]
    static void EndTimer(Stopwatch __state)
    {
        __state.Stop();
        Log.Message($"耗时：{__state.ElapsedMilliseconds}ms");
    }
}
```

### `___字段名` - 访问私有字段

```csharp
public class Enemy
{
    private int damage = 50;
}

[HarmonyPatch(typeof(Enemy), nameof(Enemy.Attack))]
class Patch
{
    // 读取私有字段
    [HarmonyPrefix]
    static void ReadField(int ___damage) { }
    
    // 修改私有字段（需 ref）
    [HarmonyPrefix]
    static void ModifyField(ref int ___damage)
    {
        ___damage = 100;
    }
}
```

### `__args` - 参数数组

```csharp
[HarmonyPrefix]
static void AccessAllArgs(object[] __args)
{
    var firstArg = __args[0];      // 读取
    __args[1] = "新值";            // 修改（无需 ref）
}
```

---

## Transpiler 转译器

### 基本用法

修改原方法的 IL 代码（高级用法）：

```csharp
[HarmonyPatch(typeof(TargetClass), "TargetMethod")]
class Patch
{
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> ModifyIL(
        IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instr in instructions)
        {
            // 修改 IL 指令
            yield return instr;
        }
    }
}
```

### Transpiler 特殊参数

| 类型 | 说明 |
|------|------|
| `IEnumerable<CodeInstruction>` | **必需**，IL 指令序列 |
| `ILGenerator` | 当前 IL 代码生成器 |
| `MethodBase` | 原方法信息 |

---

## Finalizer 终结器

### 用途

- 保证执行的清理代码
- 异常处理、抑制或转换
- **唯一对异常免疫的补丁类型**

### 用法

```csharp
[HarmonyPatch(typeof(TargetClass), "TargetMethod")]
class Patch
{
    [HarmonyFinalizer]
    static Exception HandleException(Exception __exception)
    {
        if (__exception != null)
        {
            Log.Error($"补丁异常：{__exception}");
            // 返回 null 抑制异常，返回 __exception 继续抛出
            return null;
        }
        return null;
    }
}
```

---

## 最佳实践

### 1. 优先级选择

| 需求 | 推荐方式 |
|------|----------|
| 修改参数/前置逻辑 | **[HarmonyPrefix]** |
| 修改返回值/后置逻辑 | **[HarmonyPostfix]** |
| 完全替换方法 | Prefix + 返回 `false` |
| 精细控制 IL | **[HarmonyTranspiler]**（慎用） |
| 异常处理 | **[HarmonyFinalizer]** |

### 2. 命名规范

```csharp
// 类名：OriginalClass_Method_Patch
// 方法名：表达用途的动词短语

[HarmonyPatch(typeof(Hediff), "get_CurPart")]
class Hediff_CurPart_Patch
{
    [HarmonyPrefix]
    static void ValidateBodyPart() { }

    [HarmonyPostfix]
    static void LogPartChange() { }
}

[HarmonyPatch(typeof(Pawn), "TakeDamage")]
class Pawn_TakeDamage_Patch
{
    [HarmonyPrefix]
    static void RecordDamageSource() { }

    [HarmonyPostfix]
    static void ApplyDamageModifier() { }
}
```

### 3. 兼容性建议

- ✅ **优先使用 Postfix**：兼容性最佳
- ✅ **使用属性方式**：方法名可表达用途，便于维护
- ✅ **避免跳过原方法**：除非必要
- ✅ **使用 __state 传递状态**：不要使用静态变量存储临时数据
- ⚠️ **慎用 Transpiler**：维护成本高，游戏更新易失效

### 4. 完整示例

```csharp
using HarmonyLib;
using RimWorld;
using Verse;

/// <summary>
/// 补丁：修改 Pawn 受到伤害时的逻辑
/// </summary>
[HarmonyPatch(typeof(Pawn), "TakeDamage")]
public static class Pawn_TakeDamage_Patch
{
    // 保存原始伤害值
    [HarmonyPrefix]
    static void RecordOriginalDamage(DamageInfo dinfo, out float __state)
    {
        __state = dinfo.Amount;
        Log.Message($"受到攻击：{dinfo.Def.defName}");
    }

    // 修改最终伤害
    [HarmonyPostfix]
    static void ApplyDamageModifier(Pawn __instance, DamageInfo dinfo, 
                                    float __state, ref DamageWorker.DamageResult __result)
    {
        // 确保最小伤害
        if (__result.totalDamageDealt < 1)
            __result.totalDamageDealt = 1;
        
        Log.Message($"实际伤害：{__result.totalDamageDealt}");
    }

    // 异常处理
    [HarmonyFinalizer]
    static Exception HandleException(Exception __exception)
    {
        if (__exception != null)
        {
            Log.Error($"TakeDamage 补丁异常：{__exception}");
            return null;  // 抑制异常
        }
        return null;
    }
}
```

---

## 参考链接

- [Harmony 官方文档](https://harmony.pardeike.net/)
- [补丁方法定义](https://harmony.pardeike.net/articles/patching.html)
- [Prefix 补丁详解](https://harmony.pardeike.net/articles/patching-prefix.html)
- [Postfix 补丁详解](https://harmony.pardeike.net/articles/patching-postfix.html)
- [代码注入详解](https://harmony.pardeike.net/articles/patching-injections.html)
