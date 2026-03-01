---
name: unity-code-review
description: Reviews Unity C# code for performance, lifetime safety, architecture boundaries, and coding standards. Use when the user asks for code review, PR review, 脚本规范, 性能优化, GC, 生命周期, 资源管理, 对象池, 架构建议。
---

# Unity C# 代码评审（性能/生命周期/架构）

## 评审输出格式（统一）
- 🔴 **必须修**：会导致崩溃/泄漏/严重性能问题/逻辑错误
- 🟡 **建议改**：可维护性/中度性能/边界不清
- 🟢 **可选**：风格优化/小收益改进

## Checklist（逐条对照）
### 性能与 GC
- `Update*` 热路径是否 **零分配**（无 LINQ/无装箱/无字符串拼接/无隐式捕获）。
- 是否存在 **频繁 Instantiate/Destroy**（战斗/循环中）且无池化/上限。
- 是否有 **频繁 GetComponent/Find/Camera.main** 未缓存。
- 物理 API 是否优先 NonAlloc，且层级 mask 正确、频率可控。

### 生命周期与资源安全
- 事件/委托订阅是否在对称回调中解绑（`OnEnable/OnDisable` 或 `Start/OnDestroy`）。
- 协程/异步是否可取消，且在销毁/禁用时收敛，避免野回调。
- 是否存在 static 缓存持有场景对象/资源，导致场景无法卸载。
- Addressables（如有）句柄是否被持有并对称 `Release/ReleaseInstance`。

### 架构边界
- 纯逻辑是否可从 `MonoBehaviour` 中提取为可测试模块（service/状态机/POCO）。
- 是否出现“万能 Manager”与跨模块乱引用；是否能用更小的模块与显式依赖替代。
- 数据/配置是否用 `ScriptableObject` 或既定数据层，而不是散落常量。

### 代码规范
- 默认 `private` + `[SerializeField]` 暴露 Inspector；避免 public 可变字段。
- 类型/文件命名一致；命名空间与模块目录一致（至少模块级）。
- 日志是否可控（避免热路径 `Debug.Log` 泛滥）。

## 评审步骤（工作流）
1. 先确认改动范围与运行时路径（是否热路径、是否跨场景常驻）。
2. 先找 🔴：泄漏/野回调/每帧分配/无限增长。
3. 再给 🟡：解耦、模块边界、可测试性、可读性。
4. 最后给 🟢：风格与小收益优化，并给出“如何验证”的建议（Profiler 指标/回归点）。
