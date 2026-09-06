# Architecture

> 本文记录项目当前较稳定的职责边界、依赖方向和核心运行流  
> 最后核对的运行时代码基线：当前工作树（2026-09-06）

## 1. 当前架构概览

当前项目的核心运行时集中在 `Assets/Scripts/Player/`。玩家系统把 **Gameplay 状态、Motion 数据模拟、实际移动和动画表现** 分开，由 `PlayerSimulationDriver` 统一编排一帧。

```text
Unity Input System
        │
        ▼
PlayerInputReader ──► PlayerActionBuffer
        │                    │
        └────── interfaces ──┘
                 │
          PlayerInstaller
                 │
                 ▼
        PlayerSimulationDriver
                 │
        ┌────────┼───────────────┐
        ▼        ▼               ▼
 PlayerState   Motion         Ability facts
 Controller   Planner        Jump / Dodge
        │        │
        │        ▼
        │   PlayerMotionRuntime
        │   PlayerLocomotionPhaseRuntime
        │        │
        └──► PlayerGameplayIntent
                 │
                 ▼
        PlayerMotionComposer
                 │
                 ▼
          PlayerMotorCommand
                 │
                 ▼
             PlayerMotor
                 │
        CharacterController
                 │
                 ▼
          PlayerMotorResult
                 │
        ┌────────┴─────────┐
        ▼                  ▼
 LandingTracker      State facts / transitions
        │                  │
        └────────┬─────────┘
                 ▼
       committed Motion / Phase
                 │
                 ▼
    PlayerAnimationController
                 │
       PlayerAnimationSet
                 │
              Animancer
```

当前职责关系：

- **状态机决定玩家处于什么 Gameplay 状态**
- **Motion 系统决定特殊运动如何按数据演进**
- **Motor 是唯一实际执行 CharacterController 移动的模块**
- **Animation 消费已经产生的状态、Motion 与相位事实并表现为 Pose**
- **Editor 工具把动画离线加工为 Runtime 可消费的数据，Runtime 当前不反向依赖 Editor**

## 2. 每帧控制流

`PlayerSimulationDriver` 是当前玩家每帧唯一的高层编排入口。当前顺序为：

```text
Action Buffer / Motion 帧事件 / Dodge Cooldown 更新
    ↓
解析世界移动方向并更新地面移动意图
    ↓
写入上一帧 Motor / Motion 事实
    ↓
状态机 Pre-Tick 转换
    ↓
建立 PlayerGameplayIntent
    ↓
Motion Planner 解析状态转换
    ↓
状态 Tick 补充 Gameplay Intent
    ↓
Motion Planner 解析连续 Motion
    ↓
PlayerMotionRuntime 推进烘焙 Motion
    ↓
PlayerMotionComposer 合成最终 MotorCommand
    ↓
PlayerMotor 执行 CharacterController 移动
    ↓
生成 MotorResult + LandingSnapshot
    ↓
状态机 Post-Tick 转换
    ↓
处理 Post-Tick Motion 与落地表现语义
    ↓
必要时启动 Motion-backed Landing
    ↓
提交 Locomotion Phase
    ↓
AnimationController 消费最终事实并手动评估 Animancer
```

`HandleStateTransition` 位于状态 Tick 之前；`ResolveContinuousMotion` 位于状态 Tick 之后，因此连续 Motion 可以读取本帧状态补充后的 `PlayerGameplayIntent`。

`PlayerMotorResult` 在 Motor 执行后产生，再反馈给状态、落地检测、Foot Phase 和后续决策。

`PlayerAnimationController` 位于模拟之后，接收最终的 Gameplay State、Transition、Motion Snapshot、Phase Snapshot 和 Landing Presentation，当前不参与本帧移动裁决。

## 3. 输入与依赖注入

### Input

`PlayerInputReader` 负责 Unity Input System 边界：

- 持续输入通过 `IPlayerInputSource` 暴露
- Jump / Dodge 等离散操作写入 `IPlayerActionBuffer`
- `PlayerActionBuffer` 在输入回调时机与 Gameplay 模拟时机之间保存短时动作请求

当前上层 Gameplay 通过 `IPlayerInputSource` / `IPlayerActionBuffer` 与输入实现交互。

### Bootstrap

`PlayerInstaller` 当前承担轻量装配职责：

- 把输入源与动作缓冲注入 `PlayerSimulationDriver`
- 把输入源注入 `PlayerCameraOrbitTarget`
- 把动作缓冲注入 `PlayerInputReader`

当前依赖链：

```text
Concrete Input / Action Buffer
        │
        ├──► IPlayerInputSource / IPlayerActionBuffer ──► Simulation
        └──► IPlayerInputSource ────────────────────────► Camera Orbit Target
```

## 4. Gameplay 状态层

### PlayerStateController

`PlayerStateController` 是**唯一 Gameplay State 切换裁决者**。

当前注册状态：

- `PlayerIdleState`
- `PlayerWalkState`
- `PlayerRunState`
- `PlayerFastRunState`
- `PlayerDodgeState`
- `PlayerAirState`
- `PlayerHardLandingState`

状态对象提出 `PlayerStateTransitionRequest`；Exit / 切换 / Enter 由 Controller 统一执行。

状态更新分为：

```text
EvaluateInputTransition
        ↓
       Tick
        ↓
EvaluateResultTransition
```

输入导致的转换和执行移动后才能确认的转换（例如落地）分阶段处理。

### PlayerContext

`PlayerContext` 是状态对象共享的稳定依赖与事实容器，目前包含：

- `PlayerJump` / `PlayerDodge`
- `IPlayerInputSource` / `IPlayerActionBuffer`
- `PlayerMovementConfig`
- `PlayerMotorResult`
- `PlayerMotionSnapshot`
- `PlayerLandingSnapshot`
- Walk / FastRun 等需要跨状态保存的移动意图状态
- 待应用的垂直冲量

### Motion Lock 与状态所有权

`PlayerMotionDefinition` 可以定义 `TransitionLockEndProgress`，由 `PlayerMotionRuntime` 通过 `PlayerMotionSnapshot.IsTransitionLocked` 暴露。

Motion Lock 是状态切换约束，不拥有状态切换权。

普通候选转换可以在承诺窗口内被 `PlayerStateController` 拒绝；初始化、跌落、落地、重落地等强制转换由状态层裁决。Motion 数据用于表达当前运动的普通打断窗口，Gameplay State 的切换权仍位于状态层。

## 5. Motion：数据驱动的特殊运动

`PlayerMotion` 是当前架构中的独立 Runtime 数据层之一。

### 数据链

```text
AnimationClip
    │  Editor Bake
    ▼
PlayerMotionProfile
    │
    ▼
PlayerMotionDefinition
    │
    ▼
PlayerMotionCatalog
    │
    ▼
PlayerMotionPlanner
    │
    ▼
PlayerMotionRuntime
    │
    ▼
PlayerMotionFrame / PlayerMotionSnapshot
```

### PlayerMotionProfile

`PlayerMotionProfile` 保存从动画离线烘焙得到的运动数据、Foot Motion Channel 与 Foot Plant Marker。Runtime 读取这些结果，不在 Gameplay 运行时重新分析 AnimationClip。

### PlayerMotionDefinition

`PlayerMotionDefinition` 给一份 Profile 增加运行语义，主要包括：

- 平移策略
- 旋转策略
- Basis 选择
- 运行时持续时间与位移倍率
- Entry Handoff（源地面循环 → Finite Motion）与 Exit Handoff（Finite Motion → 目标 Locomotion）的区间和权重曲线
- Transition Lock 承诺窗口
- 被打断后的退出策略
- 是否按 Foot Phase 选择左右脚 Profile
- 是否需要对应动画表现

Profile 描述动画中的运动数据，Definition 描述这些数据在 Gameplay 中的使用方式。

### PlayerMotionCatalog

`PlayerMotionCatalog` 是 Motion 语义索引：

```text
PlayerMotionId
    ↓
PlayerMotionDefinition
```

它同时保存 Walk / Run / FastRun 的 `PlayerLocomotionCycleDefinition`。

### PlayerMotionPlanner

`PlayerMotionPlanner` 位于 Simulation 层，把 **Gameplay Transition / Intent 转换为 Motion 选择**。

它负责：

- 状态进入 / 退出 Motion 解析
- Dodge / Start / Stop / 180° Turn / Motion-backed Landing 等语义选择
- 根据 Foot Phase 选择对应 Foot Profile
- Stop Motion 启动时从 `PhaseSnapshot.Mode` 与 `MotorResult.HorizontalVelocity` 捕获有效的地面 Loop Entry Source
- 驱动 `PlayerMotionRuntime`
- 持有并提交 `PlayerLocomotionPhaseRuntime`

当前不负责：

- Animancer 播放
- AnimationClip 选择
- CharacterController 移动
- Gameplay State 的最终切换裁决

### PlayerMotionRuntime

`PlayerMotionRuntime` 演进当前已选择的 `PlayerMotionDefinition` / `PlayerMotionProfile`：

- 管理 Motion instance 与 progress
- 采样烘焙位移、Yaw
- 捕获并保留 Entry Source 的地面模式与平面速度
- 计算 Entry / Exit Handoff 进度、位移权重与 Translation Authority
- 暴露完成 / 取消 / Transition Lock 快照
- 产出本帧 `PlayerMotionFrame`

## 6. Gameplay Intent → 实际移动

跨层移动数据通过显式结构传递：

```text
PlayerGameplayIntent
        +
PlayerMotionFrame
        +
previous PlayerMotorResult
        ↓
PlayerMotionComposer
        ↓
PlayerMotorCommand
        ↓
PlayerMotor
        ↓
PlayerMotorResult
```

### PlayerMotionComposer

`PlayerMotionComposer` 是 Gameplay 常规移动与烘焙 Motion 之间的合成边界。

状态层表达移动意图，Motion 提供当前特殊运动的本帧贡献，Composer 生成最终 Motor 参数：

- Entry Source 速度、Authored Motion 位移与目标 Locomotion 预测速度使用统一三路权重合成
- Velocity Driven
- Displacement Driven
- Face Direction
- Yaw Delta
- 垂直冲量

Entry / Exit Handoff 的跨层数据流为：

```text
Stop Transition + PhaseSnapshot + MotorResult.HorizontalVelocity
                              │
                              ▼
                     PlayerMotionPlanner
                              │ PlayerMotionEntrySource
                              ▼
                     PlayerMotionRuntime
                       ├──► PlayerMotionFrame
                       │       └──► PlayerMotionComposer
                       └──► PlayerMotionSnapshot
                               ├──► PlayerLocomotionPhaseRuntime
                               └──► PlayerAnimationController
```

`PlayerMotionEntrySource` 只包含地面 Loop 模式与平面速度等 Simulation 数据；Planner、Runtime 和 Composer 不持有 Animancer State 或 AnimationClip。位移由 Entry Source 速度、Authored Motion 位移和目标 Locomotion 预测速度三路权重合成。

### PlayerMotor

`PlayerMotor` 是当前**唯一 CharacterController 执行器**。

它解释 `PlayerMotorCommand`，负责：

- 水平速度或直接位移
- 重力与垂直速度
- Jump 垂直冲量
- Ground Snap
- 实际 CharacterController.Move
- 旋转
- 输出真实 `PlayerMotorResult`

当前不包含 Dodge、Jump State、Stop Animation 或 Animancer 等高层语义。

## 7. Ground 与 Landing

### PlayerGroundProbe

`PlayerGroundProbe` 封装地面探测和 Ground Snap 所需事实，由 `PlayerMotor` 消费。

### PlayerLandingTracker

`PlayerLandingTracker` 跨帧记录一次空中生命周期，并在落地帧依据：

- 下落距离
- 落地冲击速度
- 空中前的地面移动模式
- 当前是否仍有移动意图
- 目标地面移动模式

生成一次性的 `PlayerLandingSnapshot`。

### Landing Presentation

落地事实产生后由 `PlayerSimulationDriver` 协调两条分支：

```text
MotorResult
    ↓
LandingTracker
    ↓
LandingSnapshot
    ├──► StateController / AirState ──► HardLanding 或普通地面状态
    │                                      │
    │                                      └──► HardLand 表现
    │                                             （存储在 Land4 资源槽）
    │
    └──► PlayerSimulationDriver
              │
              └──► PresentationResolver：选择普通或移动落地语义
                            │
             ┌──────────────┴────────────────┐
             ▼                               ▼
      普通表现 Edge                  Motion-backed Landing
      Land1 / Land2 / Land3          LandWalk / LandRun / LandRoll
             │                               │
             ▼                               ▼
     AnimationController                MotionPlanner
                                             │
                                             ▼
                                     AnimationController
```

`PlayerLandingPresentationResolver` 只消费 `PlayerLandingSnapshot`；`PlayerSimulationDriver` 负责结合已发生的 `PlayerStateTransition` 判断是否产生表现，以及是否启动 Motion-backed Landing。

落地检测、落地 Gameplay 状态、落地 Motion 和落地 Clip 分属不同职责。

## 8. Locomotion Phase 与脚步语义

Foot Phase 是 **Simulation Fact**，不是 AnimationController 内部状态。

当前数据流：

```text
PlayerMotorResult
    +
Current LocomotionMode
    +
Active Motion Snapshot
        ↓
PlayerLocomotionPhaseRuntime
        ↓
PlayerLocomotionPhaseSnapshot
        ├──► PlayerMotionDefinition.ResolveEntryFoot
        │      选择对应的左右脚 Motion Profile
        │
        └──► PlayerAnimationController
               用于选择 Loop 变体与手动采样 NormalizedTime
```

当前相位关系：

- Phase 的推进依据实际运动结果与 Motion 状态
- Entry Handoff Active 时，即使 Gameplay 已切换到 Idle，仍保留当前 Source Loop 的 Profile、VariantFoot 与 NormalizedPhase，并用本帧实际平面位移推进
- Entry Handoff 完成后按当前 Locomotion 状态正常关闭或重新建立 Loop；Exit Handoff 不改变该 Phase 所有权
- AnimationController 不生产 Phase
- Motion Definition 消费 Phase 选择左右脚版本
- AnimationController 把同一份 Phase Fact 转换为 Pose

## 9. Animation 表现层

### PlayerAnimationSet

`PlayerAnimationSet` 是运行语义与具体 Animancer `ClipTransition` 的资源映射边界。

它负责：

- `PlayerMotionDefinition + selected PlayerMotionProfile → Motion ClipTransition`
- `PlayerLocomotionMode + PlayerFoot → Loop ClipTransition`
- Jump / Landing 等 Presentation Cue → ClipTransition
- 校验 Catalog、Definition、Profile 与 Animation Binding 的一致性

### PlayerAnimationController

`PlayerAnimationController` 将 Gameplay、Motion 和 Simulation Phase Fact 表现为 Pose。

当前 Animancer Graph 使用 Manual Update。

两类时间来源：

- **Boundary Motion**：按 `PlayerMotionSnapshot.Progress` 手动采样
- **Ground Loop**：按 `PlayerLocomotionPhaseSnapshot.NormalizedTime` 手动采样

Controller 使用 `GetOrCreateState` 建立手动播放状态，取消 Animancer Fade，并在新 Motion 开始时停止自身未持有的活动 State。Motion 播放期间 Source Loop、Boundary Motion 与 Target Loop 的权重统一由 Controller 写入，不再与 Animancer FadeGroup 同时竞争控制权。

存在有效 Motion Entry Source 时，Controller 从当前 `stableLoopState` 转移并拥有 `entrySourceLoopState`，按 Phase Snapshot 继续采样源 Loop，并使用 Motion Definition 的 Entry 区间计算姿态权重。不存在有效 Entry Source 时，如果当前稳定 Loop 存在，则使用 `ClipTransition.FadeDuration / Motion Duration` 形成回退 Entry Pose 区间；区间结束、Motion 取消或被替换时清理 Source State。

Exit Handoff 期间，Controller 使用 `exitPoseWeight` 将 Boundary Pose 移交到目标 Locomotion Loop。Entry 与 Exit 区间重叠时，三路姿态权重统一组合：

```text
Source Loop    = 1 - EntryTargetWeight
Boundary Pose  = EntryTargetWeight × (1 - ExitTargetWeight)
Target Loop    = EntryTargetWeight × ExitTargetWeight
```

位移侧对应由 `PlayerMotionComposer` 使用 Entry Translation Weight 与 Exit Translation Authority 组合 Entry Source、Authored Motion 和目标 Locomotion 三路位移。

## 10. Editor 烘焙与预览工具链

运行时数据由 `Assets/Tools/AnimationPreview/Editor/` 下的 Editor 工具生成和检查。

当前数据方向：

```text
AnimationClip / Preview Config
        ↓
Animation Preview / Baker / Foot Plant Detector
        ↓
PlayerMotionProfile
        ↓
Definitions / Catalog / AnimationSet validation
        ↓
Runtime
```

`Project.AnimationPreview.Editor.asmdef` 是 Editor-only，并依赖 `Project.PlayerMotion.Runtime`。

当前依赖方向：

```text
Editor Tooling ──► PlayerMotion Runtime
PlayerMotion Runtime -X-> Editor Tooling
```

## 11. 程序集边界

当前已存在的关键 asmdef：

```text
Project.PlayerMotion.Runtime
    references: none

Project.PlayerLanding.Runtime
    └──► Project.PlayerMotion.Runtime

Project.AnimationPreview.Editor
    └──► Project.PlayerMotion.Runtime
    Editor only
```

`PlayerMotion` 数据类型承担底层运行时契约职责。

需要注意：Input、State、Simulation、Ability、Animation 等多数玩家模块当前没有各自独立的 asmdef，主要编译在默认 `Assembly-CSharp`；`PlayerAnimationSetEditor` 等对应 Editor 代码编译在默认 `Assembly-CSharp-Editor`。因此这些层之间的职责边界主要由设计和显式数据契约维护，只有 Motion、Landing 与 Animation Preview Editor 的部分依赖由 asmdef 强制约束。

测试程序集：

```text
Project.PlayerMotion.Tests
    ├──► Project.PlayerMotion.Runtime
    └──► Project.PlayerLanding.Runtime

Project.AnimationPreview.Editor.Tests
    ├──► Project.AnimationPreview.Editor
    └──► Project.PlayerMotion.Runtime
```

测试程序集只验证稳定的行为契约：Motion/Phase/Landing 的纯运行时演进、Definition/Profile/Composer 数值约束、必要的默认资产合法性，以及 Editor 采样、检测和批量烘焙的原子性。测试不通过反射锁定默认程序集的私有结构，也不参与玩家运行时数据流。

动画 State 所有权、场景衔接、卡顿、滑步和输入手感由人工场景检查与 `PlayerAnimationSetEditor` 资产校验承担；测试程序集编译成功也不等同于 Unity EditMode 测试已经实际执行。

## 12. 配置与资产 Source of Truth

核心运行配置位于：

```text
Assets/Settings/Player/Motion/
├── DefaultPlayerMovementConfig.asset
├── DefaultPlayerMotionCatalog.asset
├── DefaultPlayerAnimationSet.asset
├── Definitions/
├── Profiles/
└── FootCalibration/
```

职责分别是：

- `PlayerMovementConfig`：常规 Locomotion、Motor Physics、Landing 等 Gameplay/Physics 参数
- `PlayerMotionCatalog`：MotionId → Definition 与 Locomotion Cycle 索引
- `PlayerMotionDefinition`：Motion 运行策略
- `PlayerMotionProfile`：烘焙运动 / Foot Motion Channel / Foot Marker 数据
- `PlayerAnimationSet`：运行语义 → 具体动画资源
- `PlayerFootCalibration`：Foot Marker / 烘焙相关角色校准数据

`Assets/Prefabs/Player.prefab` 是当前 Player 组件装配和序列化引用的重要 Source of Truth。

当前默认 Catalog 的 19 个 Motion Definition 均配置 Entry / Exit Handoff：Entry 通常为 `0→0.2`，`WalkToIdle` 为 `0→0.12`，Exit 统一为 `0.7→1`。有有效地面 Loop Source 时 Entry 同时驱动位移与姿态移交；没有有效 Entry Source 时动画层使用 Clip FadeDuration 形成回退姿态混合。

## 13. 当前依赖关系

```text
Input Implementation
    ↓ interfaces
Simulation / State / Camera

State
    ↓ intent / transition semantics
Motion Planner

Motion Data / Runtime
    ↓ frame / snapshot
Composer / Simulation / Presentation

Composer
    ↓ command
Motor
    ↓ result
State / Landing / Phase

Simulation Facts
    ↓
Animation Presentation

Editor Tooling
    ↓
Runtime Data
```

具体文件位置记录在 `Docs/CodeMap.md`。
