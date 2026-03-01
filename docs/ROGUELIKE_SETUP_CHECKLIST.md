# 肉鸽模式：让游戏跑起来 — 配置清单

按下面顺序做完，Play 后应能：进门 → 刷怪 → 清波次 → 弹卡三选一 → 进门下一段 → 最后 Boss 段清完触发胜利。

---

## 一、场景（MainScene）

肉鸽 Bootstrap 只在 **场景名为 `MainScene`** 时生效（见 `RoguelikeMainSceneBootstrap`）。当前主场景在 `Assets/FPS/Scenes/MainScene.unity`。

### 1. 场景里必须有且配置好的对象

| 对象 | 必须？ | 说明 |
|------|--------|------|
| **Player** | ✅ | Tag 必须是 `Player`（门触发器用）。带上 `Health`、`PlayerWeaponsManager`，并挂 **`RoguelikePlayerStats`**。 |
| **GameFlowManager** | ✅ | 负责胜利/失败后淡出与切场景。Inspector 里要拖上 **End Game Fade Canvas Group**（结束时的黑屏 CanvasGroup）。 |
| **ObjectiveManager** | ✅ | 保留即可，用于显示“当前关卡/波次”目标（`RoguelikeStageObjective` 会注册到这里）。 |
| **ObjectiveHUDManager** | 建议 | 没有的话看不到目标文字（关卡 N、当前波次 1/3 等）。需配好 Objective Panel、Primary/Secondary Objective Prefab。 |
| **RoguelikeCardSelectionUI** | ✅ | 卡牌三选一界面。见下方「卡牌 UI」小节。 |
| **RoguelikeCardManager** | ✅ | 发卡逻辑。必须配 **Affix Pool**，否则不会弹卡。其他引用可留空（会 FindObjectOfType）。 |
| **ObjPrefabManager** | 建议 | 敌人/特效池化；没有会降级为 Instantiate 并打一次 Warning。 |
| **NavMeshSurface** | 建议 | 若段内需要 AI 寻路，场景里放一个并让 Generator 引用；没有则刷怪器要把 Nav Mesh Sample Radius 设为 0 做验证。 |

### 2. 首段（第一段关卡）怎么来

- **方式 A（用 Bootstrap）**  
  - 在 MainScene 里**直接摆一个“第一段”的实例**（例如从 `Assets/FPS/Prefabs/Level/Rooms/Room_Small_T.prefab` 拖进场景）。  
  - 该物体上要有 **`LevelSegment`**，且 **Enter Point / Exit Point** 指好，**Entrance Door Gate / Exit Door Gate** 指好（见下「段 Prefab」）。  
  - 运行后 Bootstrap 会 `FindObjectOfType<LevelSegment>` 当作首段，并从 `Rooms/Room_Small_T、Room_Small_Y、Room_Medium` 加载普通段+Boss 段（仅 Editor，Build 里需自己配 Generator）。

- **方式 B（不用 Bootstrap，自己控）**  
  - 场景里先放一个空物体，挂 **`RoguelikeLevelGenerator`**，**不勾** Auto Start On Enable。  
  - Inspector：**Fixed First Segment In Scene** 拖场景里的首段；**Normal Segment Prefabs** 拖 1～多个普通段 Prefab；**Boss Segment Prefab** 拖 Boss 段；**Nav Mesh Surface** 有就拖。  
  - 用你自己的方式在合适时机调用 `StartRun()`（例如菜单按钮、Start 里延迟等）。

---

## 二、关卡段 Prefab（每个房间/段都要满足）

你项目里已有：  
- `Assets/FPS/Prefabs/Level/Rooms/`：Room_Small_T、Room_Small_Y、Room_Medium（Bootstrap 默认用这些）  
- `Assets/FPS/Prefabs/Level/Segments/`：Segment_01～05、Segment_Boss  

任选一套，但**每个“段”Prefab 根上**都要满足下面结构。

### 段根节点必备组件与引用

| 组件 | 说明 |
|------|------|
| **LevelSegment** | 根上必须有。**Enter Point** / **Exit Point** 指向两个子 Transform（用于和下一段对齐）。**Entrance Door Gate** / **Exit Door Gate** 指向两扇门的 `SegmentDoorGate`。**Is Fixed Start Segment** 只给“场景里摆的首段”勾选；**Is Boss Segment** 只给 Boss 段勾选（最后一段清完会触发胜利）。 |
| **SegmentDoorGate**（两处） | 入口门、出口门各一个。每个门：至少一个 **Trigger Collider**（用于检测玩家进出）；若干**非 Trigger** 的阻挡 Collider（Open/Close 会关/开）。 |
| **SegmentGateWaveBinder** | 必须挂在段根（或段内同一层级）。负责：进门关门+开波次；波次清完→弹卡→开门；Boss 段清完→触发胜利。不挂则不会刷怪、不会开门、不会胜利。 |
| **SegmentEnemySpawner** | 可挂在段内子物体。**Enemy Prefab** 必填（带 `EnemyController` 的 Prefab）；**Spawn Points** 至少 2～4 个 Transform；若没 NavMesh，把 **Nav Mesh Sample Radius** 设为 0 先跑通。 |
| **StageWaveController** | 可与 Spawner 同层级或由 `LevelSegment` 自动补。可调 **Min/Max Waves**、**Base Enemies Per Wave**、**Spawn Interval**。 |

说明：  
- **ObstacleGenerator** 可选；若段上有 **SegmentObstacleGenerator**，会按槽位随机障碍。  
- 门触发器检测的是 **Tag = Player**，所以玩家根节点 Tag 必须是 `Player`。

---

## 三、卡牌系统（三选一要能弹出来）

### 1. 创建 Affix 池（必做）

- 菜单：**Create → FPS → Roguelike → Card Affix Pool**，得到 `RoguelikeAffixPoolSO`。  
- 在 Project 里选中该资产，Inspector 里 **All Affixes** 列表至少加 1 个 **Roguelike Affix Definition**（见下）。

### 2. 创建若干 Affix 定义

- 菜单：**Create → FPS → Roguelike → Card Affix Definition**。  
- 每个 Affix 建议填：**Id**、**Display Name**、**Description Template**（可用 `{value}`、`{weapon}`），**Target**（Player 或 Weapon），**Stat Id**（如 `Player_AttackMul`、`Weapon_DamageMul`、`Weapon_AdditionalProjectiles`），**Value Range**、**Modifier Kind**。  
- 若 **Target = Weapon**，**Allowed Shoot Types** 别限制太死，否则可能没有候选卡（当前武器不匹配）。

### 3. 场景里的卡牌 UI

- **RoguelikeCardSelectionUI**：Inspector 里 **Menu Root**（弹卡时的面板）、**Card Container**（卡牌生成父节点）、**Card Prefab**（`RoguelikeCardView` 的 Prefab）必须都拖好，缺一就会报错且不显示。  
- **RoguelikeCardManager**：**Affix Pool** 拖上面建的 `RoguelikeAffixPoolSO`；**Reward Option Count** 默认 3。**Selection UI** / **Player Stats** / **Weapons Manager** 可留空，代码会自动查找。

---

## 四、胜利/失败与结束画面

- **GameFlowManager** 已改为只监听 **PlayerDeathEvent**（失败）和 **GameOverEvent**（胜利/失败）。  
- Boss 段波次清完后会自动发 **GameOverEvent(Win=true)** 并显示“打败最终Boss，成功通关！”。  
- 请在 GameFlowManager 上配置：  
  - **End Game Fade Canvas Group**：结束时的黑屏。  
  - **Win Scene Name** / **Lose Scene Name**：结束后要加载的场景名。  
  - **Win Game Message**：可填通用胜利文案；Boss 通关时会用上面的固定句覆盖显示。

---

## 五、快速自检（跑不起来时先看这里）

| 现象 | 检查项 |
|------|--------|
| 进门后不刷怪 | 该段是否有 **SegmentGateWaveBinder**？Player 的 **Tag 是否为 Player**？入口门是否有 **Trigger Collider**？ |
| 怪刷不出或门永远不开 | **SegmentEnemySpawner** 的 **Enemy Prefab** 是否填了？**Spawn Points** 是否至少 2 个？若没有 NavMesh，**Nav Mesh Sample Radius** 是否先设为 0？ |
| 清完怪不弹卡 | **RoguelikeCardManager** 的 **Affix Pool** 是否拖了池子？池子里是否至少有一条 **Roguelike Affix Definition**？ |
| 弹卡报错 / 不显示 | **RoguelikeCardSelectionUI** 的 **Menu Root / Card Container / Card Prefab** 是否全拖好？ |
| 清完 Boss 段没胜利 | 该段的 **LevelSegment.Is Boss Segment** 是否在 Prefab 或运行时为 true？场景里是否有 **GameFlowManager** 且 **End Game Fade Canvas Group** 已拖？ |
| 没有目标/波次文字 | 场景是否有 **ObjectiveManager** + **ObjectiveHUDManager**，且 HUD 的 Panel/Prefab 已配置？（目标由 `RoguelikeStageObjective` 动态创建并注册） |

---

## 六、推荐的最小测试流程

1. 打开 **MainScene**。  
2. 确认有 **Player**（Tag=Player）、**GameFlowManager**（End Game Fade 已拖）、**RoguelikeCardManager**（Affix Pool 已拖）、**RoguelikeCardSelectionUI**（Menu Root / Card Container / Card Prefab 已拖）。  
3. 从 **Rooms/Room_Small_T** 拖一个到场景当首段，在根上配好 **LevelSegment**（Enter/Exit、两扇门）、**SegmentGateWaveBinder**、**SegmentEnemySpawner**（Enemy Prefab + Spawn Points）、门用 **SegmentDoorGate**。  
4. 若用 Bootstrap：确保场景名是 **MainScene**，Play 后应自动生成 Generator 并用首段 + Room_Small_T/Y、Room_Medium 跑。  
5. 进入门内 → 应刷怪；清完当前段所有波次 → 应弹三选一卡；选卡后出口门开，进下一段。最后一格若是 Boss 段（Is Boss Segment = true），清完波次应触发“打败最终Boss，成功通关！”并进入胜利流程。

按上述配置完成后，游戏即可按肉鸽流程跑起来；若某一步不符合表格里的“必须”项，对应现象可对照第五节排查。
