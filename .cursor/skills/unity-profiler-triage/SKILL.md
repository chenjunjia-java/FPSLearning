---
name: unity-profiler-triage
description: Triages Unity performance issues using Profiler/Memory tools with a zero-GC mindset. Use when the user mentions 卡顿, 掉帧, 性能, Profiler, GC Alloc, CPU ms, spikes, 或需要定位热路径。
---

# Unity Profiler 性能排查（Triage）

## 目标
- **锁定根因**：CPU/GPU/GC/加载/渲染/脚本热路径中的主要瓶颈。
- **给出可验证结论**：明确“哪里慢、为什么慢、改完会快多少、如何验证”。

## 工作流（按顺序执行）
1. **复现条件**：场景/步骤/设备/帧率目标/是否 Development Build。
2. **采样策略**（先粗后细）：
   - 先用 Profiler（非 Deep Profile）抓 300-1000 帧，定位主要模块占比。
   - 只在需要时对局部/短时间启用 Deep Profile（成本高、易扰动结论）。
3. **锁定维度**：
   - **CPU 高**：看 `CPU Usage` 的 Timeline，先找主线程长帧，再看 Worker 线程并行情况。
   - **GC Alloc/GC Spike**：看 `GC Alloc` 列与 `GC.Collect`，找每帧分配源头。
   - **GPU 高**：GPU Profiler + Frame Debugger，看 overdraw、材质切换、阴影、后处理。
   - **加载卡顿**：看 `Loading` / `Asset Loading`，区分 IO、反序列化、Instantiate、Shader 预热。
4. **提出假设 → 验证**：每次只改一个变量（开关/代码路径），再次抓同样窗口对比指标。

## 关键检查清单（高命中）
- **脚本热路径**：
  - `Update`/`LateUpdate`/`FixedUpdate` 是否可事件化或 tick 化（节流/分帧）。
  - 是否存在 `GetComponent`/`Find`/`Camera.main`/`GetComponentInChildren` 频繁调用。
  - 是否使用 LINQ/闭包捕获/字符串拼接/装箱导致 GC Alloc。
- **物理与 AI**：
  - `Physics.Raycast/Overlap*` 频率、层级 mask、非分配 API（`NonAlloc`）使用情况。
  - NavMesh/寻路是否做了分帧与缓存，避免每帧全量重算。
- **渲染**：
  - 大量 SetPass/材质实例化（MaterialPropertyBlock 优先）。
  - UI rebuild（Canvas 拆分、避免频繁 Layout）。
- **Instantiate/Destroy**：
  - 战斗中大量生成销毁 → 对象池 + 预热 + 上限。

## 输出格式（你对用户的最终交付）
- **结论**：Top 3 瓶颈（按 CPU ms / GPU ms / GC / 内存）与证据（Profiler 截图/采样窗口）。
- **根因**：指到具体脚本/函数/系统与触发条件。
- **改动建议**：按收益/风险排序，给出“最小改动先验证”的路径。
- **验证方式**：明确要对比的指标（平均/95 分位/峰值、GC Alloc/帧、DrawCall、内存常驻）。

## 常用 Unity 工具（优先级）
- Profiler（CPU Usage / Rendering / Memory / GC）
- Frame Debugger（渲染管线排查）
- Memory Profiler（快照对比）
- Build：Development Build + Autoconnect Profiler（真机必做）
