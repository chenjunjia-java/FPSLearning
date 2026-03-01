---
name: unity-resource-lifecycle-audit
description: Audits Unity runtime resource lifetime (load/hold/release), pools, async cancellation, and event subscriptions to prevent leaks and spikes. Use when the user mentions 内存泄漏, 资源释放, Addressables, 对象池, Instantiate/Destroy, 场景切换残留, 野回调。
---

# Unity 资源生命周期审计（Load/Hold/Release）

## 目标
- **把资源变成可控资产**：每个资源/实例都有明确所有权与释放点。
- **消灭泄漏与残留**：场景切换后内存可回落、订阅可解除、异步可取消、池对象状态可重置。

## 审计流程（按顺序）
1. **列资产清单**：哪些是“常驻”（跨场景）、哪些是“关卡内”（可释放）、哪些是“临时”（帧/秒级）。
2. **定义所有权**：为每类资产写出：
   - 创建者（谁 Load/Instantiate）
   - 持有者（谁存引用/缓存）
   - 释放者（谁 Release/ReturnToPool/Destroy）
   - 释放时机（何时：OnDisable/OnDestroy/关卡卸载/系统 Shutdown）
3. **抓证据**：
   - Memory Profiler：切场景前后各抓一份快照，对比常驻增长项。
   - Profiler：看 `Asset Loading`/`GC.Collect`/`UnloadUnusedAssets` 等峰值来源。
4. **落治理点**（最小改动优先）：
   - 成对释放、解除订阅、取消异步、池重置。
   - 把“隐式全局引用”（static/单例缓存/事件）收敛到可控模块。

## 高危点清单（优先排查）
- **事件/委托泄漏**：`+=` 未 `-=`；`UnityEvent.AddListener` 未 `RemoveListener`；匿名 lambda 无法解绑。
- **静态引用**：static 字段/缓存持有 `MonoBehaviour/GameObject/Texture` 等导致场景无法卸载。
- **协程/异步野回调**：对象销毁后回调仍触发；未取消/未判空/未检查对象存活。
- **对象池不重置**：返回池时未清状态（特效、Animator、粒子、NavMeshAgent、刚体、订阅、计时器）。
- **重复加载**：同 key/地址多次加载但句柄未复用，导致引用计数增长无法回落。

## Addressables（如项目使用）
- **句柄管理**：任何 `LoadAssetAsync/InstantiateAsync` 的句柄必须被持有并在对称时机 `Release`。
- **实例释放**：Addressables 实例用 `Addressables.ReleaseInstance`（而不是直接 Destroy）。
- **并发合并**：同一 key 的并发请求要去重/共享（避免 N 次加载同资源）。

## 对象池要求（最低标准）
- **Reset 入口**：提供 `OnSpawn/OnDespawn`（或等价）统一重置。
- **边界清理**：`OnDisable/OnDestroy` 里确保解除订阅/停止协程/取消异步，避免池化对象“退回池仍活着”。
- **容量策略**：设置上限与回收策略（超限销毁/延迟回收），避免池无限长。

## 输出格式（交付给用户）
- **问题列表**：每条包含“证据（快照/Profiler）+ 根因（引用链/持有者）+ 修复点（具体释放/解绑/取消位置）”。
- **治理方案**：按风险/收益排序的改动列表，并说明如何回归验证（切场景 10 次内存是否回落、峰值是否下降）。
