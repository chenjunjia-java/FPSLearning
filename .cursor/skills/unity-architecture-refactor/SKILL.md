---
name: unity-architecture-refactor
description: Refactors Unity gameplay code into clean modules with explicit ownership, lifecycle symmetry, and testable core logic. Use when the user mentions 架构, 重构, 解耦, Manager 太臃肿, 模块化, 依赖方向, asmdef, 事件总线, 可测试性。
---

# Unity 架构重构（模块化/解耦/可测试）

## 目标
- **把“场景脚本堆逻辑”变成模块**：边界清晰、依赖单向、可替换。
- **让生命周期可推理**：初始化/运行/暂停/卸载都有唯一入口与收敛点。

## 重构流程（推荐顺序）
1. **识别职责**：把目标脚本按职责拆成 2-4 类（输入、规则、状态、表现、数据访问）。
2. **提炼核心域逻辑**：
   - 把纯计算/状态机/规则移动到非 `MonoBehaviour`（POCO/service）。
   - Unity 相关（Transform/Animator/Particle/Audio）只留在适配层。
3. **定义模块 API**：
   - 用接口或 Facade 暴露“必须能力”；隐藏内部细节。
   - 依赖通过显式初始化注入（`Initialize(...)` / 构造注入在非 MB 中）。
4. **建立生命周期入口**：
   - 模块提供 `Start/Stop`（或 `Enable/Disable`）与 `Dispose`（如需要）。
   - 所有订阅/协程/异步在 `Stop/Dispose` 内收敛。
5. **渐进式迁移**：
   - 先用适配器包住旧代码（保持行为不变），再逐步替换内部实现。
   - 每步都有可回滚点与回归验证路径。

## 反模式清单（看到就拆）
- “万能 Manager”：一个类负责生成、计时、AI、UI、音效、配置加载……难测试、难优化。
- “全局事件总线滥用”：到处广播/订阅导致依赖不可见、泄漏频发。
- “静态持有场景对象”：导致场景无法卸载、状态污染。

## 产出物（你对用户的交付）
- **模块边界图**：列出模块、公开 API、依赖方向（文字即可）。
- **迁移计划**：按风险从低到高分批次落地（每批次包含回归点）。
- **验证清单**：功能回归 + 性能回归（Profiler 指标、GC Alloc、加载耗时）。

## 约束
- 尽量不引入重型框架；优先用最小抽象（接口/组合）解决耦合与生命周期问题。
