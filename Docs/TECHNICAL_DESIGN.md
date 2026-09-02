# Technical Design — Live2DAction (Draft)

## 專案架構

```
Assets/_Project/
├── Game/
│   ├── Core/
│   ├── Input/
│   ├── Characters/
│   ├── Combat/
│   ├── AI/
│   ├── Camera/
│   ├── Skills/
│   ├── UI/
│   ├── Dialogue/
│   ├── Save/
│   ├── Audio/
│   ├── VFX/
│   └── SceneManagement/
└── Tests/
```

只建立當前 Phase 實際用到的資料夾，避免空殼結構（Phase 1 只需要 Core/Input/Characters/Combat/Camera）。

## 角色物件結構

```
PlayerRoot
├── CharacterController（Unity 內建，非 Rigidbody 驅動移動）
├── PlayerInput（實作共用 IInputCommand 介面）
├── PlayerMovement
├── PlayerCombat
├── PlayerHealth
├── PlayerTargeting
├── PlayerAnimation
├── PlayerAudio
├── Hurtbox
├── ModelRoot / 3DCharacterModel
├── WeaponRoot / Weapon
└── VFXRoot
```

敵人比照：`EnemyRoot`（NavMeshAgent／EnemyBrain／EnemyCombat／EnemyHealth／EnemyAnimation／Hurtbox／ModelRoot／VFXRoot）。

## 場景管理

垂直切片只有 1 個固定戰鬥場景 + 1 個主選單場景（Phase 3 起），不需要複雜場景管理系統；先用 Unity `SceneManager.LoadScene`，不提前建立通用場景管理框架（避免過度抽象）。

## 角色狀態機（草案）

`Idle → Move → Attack(1/2/3) → Dodge → Hit → Dead`，攻擊/受傷/死亡互斥，閃避需明確判斷是否可從當前狀態進入。

## 戰鬥狀態機／攻擊資料

`AttackData`（ScriptableObject）：AttackId、Animation、Damage、StartupTime、ActiveTime、RecoveryTime、HitboxShape、HitboxOffset、HitboxSize、Knockback、HitStun、MovementDuringAttack、ComboWindowStart/End、CanBeInterrupted、VFX、SFX、CameraImpulse、HitStopDuration。判定優先用 3D 範圍/掃掠檢測（`Physics.OverlapBox`/`CapsuleCast` 等），不依賴高速武器的單幀 `OnTriggerEnter`。

## 技能資料

`SkillData`（ScriptableObject）：SkillId、DisplayName、Description、Icon、Animation、Damage、Range、Cooldown、HitStun、Knockback、VFX、SFX。不寫死在 Player 腳本中。

## 傷害流程

`AttackDefinition → HitDetection → DamageInfo → IDamageable → Health → HitReaction → VFX/SFX/HitStop`。同一次 Active 視窗不重複命中同一目標（比照 `C:\Live2DFighter` 的作法）。

## 敵人 AI

NavMesh + 明確狀態機（Idle/Detect/Chase/Strafe/Attack/Cooldown/Hurt/Stagger/Dead）。近戰敵人接近後攻擊；遠程敵人保持距離、物件池管理投射物。

> 註：上面是 Phase 0 草案。實際做出來的武士 Boss 用 `BossStateMachine`（`Assets/_Project/Game/AI/Boss/`），**不使用 NavMesh**，狀態機也跟草案不同（Dormant/Alert/Idle/Approach/Attack/HitReaction/PostureBroken/LeapSlam/… 等）。節奏模型見下節。

## 武士 Boss：攻擊節奏模型（技能時長 vs 出招間隔）

武士打一次普通攻擊，時間由**兩套完全獨立的系統**相加而成。調節奏前先分清楚要動哪一套。

```
[進入 Attack state] ──── A. 技能本身時長 ──── [EndAttack()] ──── B. 出招間隔 ──── [下一次攻擊]
```

### A. 技能本身時長

= **動畫 clip 長度 ÷ 該 AnimatorState 的 `m_Speed`**。

FSM 進入 `Attack` state 後，`UpdateAttack()` 一路跑到 `AnimatorNormalizedTime() >= 0.98`（`IsAttackAnimationFinished`）才呼叫 `EndAttack()`。所以**整段動畫、包含它尾端的收招／定格 pose，都算在「技能時長」裡**——這段收招 pose 通常佔 clip 的 20–40%，是純粹的死時間但屬於 A 不屬於 B。

| 要調的東西 | 在哪裡 | 說明 |
|---|---|---|
| clip 播放速度 | `Wushi.controller` → 該 state 的 `m_Speed` | 1.0 = 原速。調高 → 整招變快，hit window（normalized）跟著等比縮短**實際秒數** |
| clip 原始長度 | `Wushi_Attack_*.fbx` import 的 `firstFrame`/`lastFrame` | 裁掉尾端 recovery 幀可縮短死時間 |
| 判定窗 / 追蹤切換 | `Wushi_Attack_*.asset` 的 `hitWindows[]`、`trackingDropNormalizedTime` | 都是 clip 內的 **normalized 位置（0–1）**，不是秒數，改 `m_Speed` 不需要跟著改 |
| 提早結束 Attack state | `BossStateMachine.IsAttackAnimationFinished` 的 `0.98` 門檻（**程式**） | 改小（如 0.85）可砍掉收招定格死時間——所有判定窗都在 0.75 前，安全，但屬於 code 改動 |

**目前值（2026-08-28，playtested）：** SwordJudgment `m_Speed` 1.35（2.44s）、SpartanKick 1.4（0.90s）、OverheadSlam 1.4（1.81s）、DoubleCombo 1.4（2.02s）。LeapSlam 維持 1.0（它的飛空高度 arc 綁 `normalizedTime`，加速會弄壞落地）。

### B. 出招間隔

`EndAttack()` 之後才開始算，一路到下一次 `BeginAttack()`：

| 成分 | 欄位（`Wushi_Tuning.asset` 除非另註） | 目前值（2026-08-28） | 觸發條件 |
|---|---|---|---|
| 全域休息 | `globalRestPhase1/2 Min/Max Seconds` | P1 0.05–0.15 / P2 0.03–0.08 | 每次普通攻擊結束後 |
| 大招額外休息 | `majorAttackExtraRest Min/Max Seconds` | 0.1–0.3 | 只有 `isMajorAttack: 1` 的招（SwordJudgment、OverheadSlam） |
| 決策間隔 | `decisionIntervalPhase1/2 Min/Max Seconds` | P1 0.05–0.12 / P2 0.03–0.08 | `UpdateIdle` 每次挑下一招前的等待（`RollDecisionInterval`） |
| 接近時間 | 由 `walkSpeed`(P1) / `runSpeed`(P2)、`approachDecelerationDistance` 決定 | walk 5.5 / run 7.5 / decel 0.35 | **只有玩家跑出攻擊距離**時才需要走過去 |
| 定身緩衝 | `attackReadinessBuffer Min/Max Seconds` | 0.05–0.12 | 從 `Approach` 停下、面向玩家、再進 `Idle` 前 |
| 等 cooldown | 各 `Wushi_Attack_*.asset` 的 `cooldownSeconds` | Sword 1.0 / Spartan 0.5 / Overhead 1.1 / Double 1.0 | **只有池裡每一招都在 cooldown / 不可用**時 `PickAttack()` 回 null，boss 站著等 |
| 可連續次數 | 各 asset 的 `maxConsecutiveUses`（配 `disallowImmediateRepeat`） | 大招 1、SpartanKick/DoubleCombo 2 | 同一招連續使用達上限後，該招暫時被排除 |

`AttackReadinessDistance()` = 池裡**最小的** `maxDistance`（目前 SpartanKick 的 1.7）——boss approach 會停在這個距離，保證停下時至少有一招在距離內可用。

### ⚠️ 死欄位：`startupSeconds` / `recoverySeconds`

`Wushi_Attack_*.asset` 裡有 `startupSeconds` 和 `recoverySeconds` 兩個欄位，**`BossStateMachine` 執行階段完全不讀它們**（只有 `BossAttackDefinition.EditorConfigure` 這個設定工具會寫入）。改它們對武士的節奏**沒有任何效果**。武士攻擊的實際時序 100% 由「動畫 clip ＋ AnimatorState `m_Speed` ＋ `hitWindows` 的 normalized 位置」決定。

（玩家的 `AttackData` 和舊的 `EnemyUltimateAbility` 是**另一套**系統，那邊的 startup/active/recovery frames 才真的驅動時序——不要混淆。）

### 快速對照：想改什麼 → 動哪裡

| 想要 | 動 A（技能時長） | 動 B（出招間隔） |
|---|---|---|
| Boss 出招更頻繁 | — | ↓ `globalRest*`、`majorAttackExtraRest*`、`decisionInterval*`、各 `cooldownSeconds` |
| 單招揮得更快 | ↑ 該 state `m_Speed`（副作用：判定窗實際秒數變短、易揮空） | — |
| Boss 追玩家更快 | — | ↑ `walkSpeed`（P1）/ `runSpeed`（P2） |
| 砍掉收招定格的呆時間 | 裁 clip 尾幀，或改 `IsAttackAnimationFinished` 的 0.98 門檻（code） | — |
| Boss 太兇想收斂 | ↓ 相關 state `m_Speed` 回 1.0 | ↑ `globalRestPhase1*` 和各 `cooldownSeconds` |

CLAUDE.md 把這些標記為「手動調校值是權威」——非經使用者明確要求不要改。變更歷史見 `CHANGELOG.md`（2026-08-28 追加9/10/11）。

## 輸入系統

玩家與 AI 共用 `IInputCommand` 介面（比照 `C:\Live2DFighter` 既有模式），`PlayerInput` 與未來的 `EnemyBrain`/AI 決策都輸出同一介面，戰鬥/移動邏輯不需要知道輸入來源。

## 存檔系統

Unity 持久化資料目錄（`Application.persistentDataPath`），不寫進 `Assets/`；版本化格式、處理檔案不存在/損壞、提供刪除確認。Phase 4（Alpha）才需要。

## UI 架構

垂直切片最低需求：啟動畫面、主選單、玩家血量、技能冷卻、Boss 血量、暫停選單、勝利/失敗畫面。事件驅動（`HealthComponent` 等元件觸發事件，UI 訂閱更新），不直接輪詢。

## 物件池

投射物與重複 VFX 使用物件池，避免逐幀 `Instantiate`/`Destroy`。

## 測試方式

EditMode 測試覆蓋資料邏輯（傷害計算、狀態轉換、命中判定），PlayMode 測試覆蓋跨影格流程（命中→扣血→UI），比照 `C:\Live2DFighter` 已驗證有效的分層方式。

## Build 流程

Windows Standalone，Development Build 先跑 Profiler 檢查，Release 前確認無 Missing Reference/粉紅材質/Console 錯誤。詳見 `BUILD_RELEASE_GUIDE.md`（Phase 3 起補完）。
