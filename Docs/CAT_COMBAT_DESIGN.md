# 貓咪戰鬥機制設計文件（近戰連段 ＋ 蓄力重擊 ＋ 撲擊 ＋ 空中攻擊 ＋ 命中反饋 ＋ 敵貓）

> **狀態：全部 7 切片一次實作完成（2026-08-29 追加30），程式碼＋接線＋EditMode 已驗證，PlayMode ＋ 實機手感待確認**。切片 1（飛行 ＋ 衝刺）見 `CHANGELOG.md` 追加26–28。
>
> **2026-08-29 定案的操作/範圍決定**：蓄力重擊 = 按住左鍵 ≥ 門檻再放開（方案 A）；撲擊 = 地面移動中點左鍵（方案 A）；hitstop 只在貓附身時啟用（不碰玩家/Boss 戰）；命中反饋切片提前到 pose 之後。切片順序見第 7 節。
>
> **實作差異（vs 本文件原規劃）**：
> - 蓄力沒有動 `IInputCommand`（會強迫改十幾個 test stub）——改在 `PlayerInputProvider` 加 `AttackHeld`（非介面成員，player-only），`CatChargeAttack` 讀具體型別。
> - 撲擊沒有動 `CharacterMovement.Update`（風險太高）——改用既有的 `CharacterMovement.ApplyDash` 一次性前衝原語（`CheckpointGate`/`Updraft`/Boss `KnockbackReceiver` 都已在用）。
> - 蓄力/撲擊/連段續段全部走新的 `PlayerCombat.FeedAttackPressed()` / `TryStartOverrideAttack()` 外部輸入路徑；貓的 `PlayerCombat.inputSource` 留 null。
> - 通用擊退用新 `MeleeKnockback`（吃 `CharacterMovement` / `CharacterController` / 純 transform 三種目標），不是 Boss 那顆（只吃 `CharacterMovement`）。
>
> 本文件記錄**現況基準**、**每個設計決定的取捨**、**元件/欄位層級規格**、**分切片計畫**，讓實作時可直接照做。數值一律進 ScriptableObject（rule 7）；Live2D 視覺不參與戰鬥判定（rule 4）；玩家與 AI 共用 `IInputCommand`（rule 8）；每個切片要能在 `GreyboxTest.unity` 重現且有自動化測試（rule 6）。

---

## 0. 範圍（使用者 2026-08-29 探討定案）

貓要有**完整的近戰機制**，一次規劃、分切片交付：

| 項目 | 要不要 | 備註 |
| --- | --- | --- |
| 三段連段 | ✅ | 沿用玩家 `ComboAttackState` |
| 蓄力重擊 | ✅ | **新機制**，玩家目前沒有 |
| 撲擊 pounce（帶位移的攻擊） | ✅ | **新機制**，跟 dodge 分開 |
| 空中攻擊（飛行/滯空時揮爪） | ✅ | 用 `PlayerCombat.UseSphericalJudgment` |
| 攻擊時移動 | 自由移動（不鎖腳） | 跟玩家一致 |
| 攻擊朝向 | 身體正面 | 不給貓 lock-on |
| 命中反饋 | hitstop ＋ 螢幕震動 ＋ 擊退 ＋ 命中/揮空音效 | **全新通用層**，目前只有 Boss 專屬版 |
| 敵貓（AI 驅動） | ✅ | 沿用 `EnemyAI` |

---

## 1. 現況基準（動手前先釘清）

### 1.1 可直接複用（不重寫）

| 元件 | 職責 | 對貓的用法 |
| --- | --- | --- |
| `PlayerCombat` | 每幀在 Active 那一步用 `Physics.OverlapCapsule(attackOrigin → forward×Range, Radius)`（或 `UseSphericalJudgment` 時用 `OverlapSphere`）主動查詢命中 `IDamageable`。**主動查詢，攻擊端不需要任何 collider。** 曝露 `CurrentPhase` / `ComboIndex` / `PhaseProgress`。已 null-safe gate `stance.IsStaggered` / `health.IsDead` | 掛貓 root，`comboAttacks[3]` + `attackOrigin` 子物件 |
| `ComboAttackState`（純類別、已測） | `Startup → Active → Recovery` 三相；Active 只結算一次；Recovery 內 `attackPressed` 且在 `comboWindow` 內 → 推進下一段 | 不動 |
| `AttackData`（SO） | `damage` / `range` / `radius` / `startup·active·recovery·comboWindow` frames / `hitEffectOverride` / `alwaysSpawnHitEffect` | 建貓專屬 assets |
| `AttackResolver`（純） | 遍歷 candidates → `Health.ApplyDamage(DamageInfo)`，回傳每個命中點；支援 `damageMultiplier` | 需擴充 `DamageInfo.Direction`（見 4.3） |
| `HurtboxLink` → `Health` | 承傷端 collider → 扣血、觸發 `Damaged` / `Died` 事件 | 假人已有；貓要新加（見 5、切片 2-7） |
| `StancePoise` | 架式條 / 削韌 / stagger，`PlayerCombat` 已 gate | 貓可選 |
| `EnemyAI`（`IInputCommand` + `ICharacterSpeedSource`） | 自己驅動 `CharacterController`、面向 target、只餵 `AttackPressed` 給 `PlayerCombat`。與視覺完全無關 | 敵貓直接複用（見 5） |
| `AttackPoseUtility`（純） | `phase + progress → 揮擊角度` 的純函式範式 | `CatAttackPose` 照此範式，做多骨頭版 |

### 1.2 目前沒有、要新做

- **蓄力/重擊**：`ComboAttackState` 只有「連點推進」，`AttackData` 沒有 charge 欄位，`IInputCommand` 只有 `AttackPressed`（edge），沒有 held 信號。玩家的所謂「續力攻擊」= 連點續段，不是按住蓄力。
- **通用命中反饋**：hitstop / knockback / screenshake 只有 Boss 專屬（`BossHitbox` / `KnockbackReceiver` / `BossTuning`）。一般攻擊 `AttackResolver` 傳 `DamageInfo.Direction = Vector3.zero`，承傷端沒有位移反應。
- **音效 / 震動基礎設施**：全專案只有 `RangedWeapon` 有 `AudioSource`（槍聲）。沒有 `CameraShake`、沒有通用 SFX 播放器。
- **撲擊**：帶鎖定方向 + 速度窗的位移攻擊（概念上像 `DodgeState`，但會結算傷害）。
- **多骨頭 procedural attack pose**：`AttackPoseVisualizer` 是單骨頭、2026-08-12 已被真 Animator clip 取代並停用。貓無 Animator、無 clip。
- **貓的承傷**：貓現在沒有 `Health` / `HurtboxLink`。
- **貓 AI 的體型/攻擊距離校準**：`EnemyAI` 的預設值是照人形 076 調的。

### 1.3 貓的既有狀態（切片 1）

`Cat` root：`CharacterController` + `PlayerInputProvider` + `CharacterMovement`（飛行/衝刺，附身才 enable）+ `CatProceduralWalk`（程序化四足步態，只驅動 swing 034/042/018/023 + bend 032/040/016/021）+ `UltimateEnergy`（飛行體力）。`CameraPossessionSwitcher.catControl` = `[Cat 的 CharacterMovement]`。

---

## 2. 元件架構

### 2.1 貓（玩家附身）

```
Cat (root)
├─ CharacterController
├─ PlayerInputProvider            (已有；擴充：AttackHeld 見 3.2)
├─ CharacterMovement              (已有)
├─ CatProceduralWalk              (已有；擴充：SetAttackSuppression 見 3.8)
├─ UltimateEnergy                 (已有，飛行體力)
├─ PlayerCombat            ← 新。comboAttacks[3] + attackOrigin + stance(可選) + health(切片2-7)
├─ CatChargeAttack         ← 新。讀 AttackHeld，蓄力達標時把下一次 attack 導向重擊 AttackData
├─ CatPounce               ← 新。撲擊位移（DodgeState 式方向+速度窗）+ 觸發 PlayerCombat 的撲擊招
├─ CatAerialJudgment       ← 新。飛行/滯空時把 PlayerCombat.UseSphericalJudgment 設 true
├─ CatAttackPose           ← 新。多骨頭 procedural，讀 PlayerCombat 狀態，LateUpdate
├─ Health                  ← 新（切片 2-7）。貓被反擊
├─ StancePoise             ← 新（可選，切片 2-7）
└─ Visual/
   ├─ attackOrigin        ← 新 child Transform（嘴前 ~0.35m，貼地高度）
   ├─ Hurtbox (child)     ← 新（切片 2-7）：Collider(isTrigger) + HurtboxLink → Health
   └─ Bone_000 …          (glb 骨架)
```

`CameraPossessionSwitcher.catControl` 追加：`PlayerCombat`、`CatChargeAttack`、`CatPounce`、`CatAerialJudgment`（附身才能揮爪；`CatAttackPose` 不進 catControl —— 沒附身時 `PlayerCombat.CurrentPhase == Idle`，pose 自然是零偏移）。

### 2.2 敵貓

```
EnemyCat (root)
├─ CharacterController
├─ EnemyAI                 (複用；target = Player，校準 moveSpeed / attackRange / 體型)
├─ PlayerCombat            (comboAttacks = [1 招]，同人形敵人慣例)
├─ CatProceduralWalk       (speedSource = EnemyAI —— 它已 implements ICharacterSpeedSource)
├─ CatAttackPose           (combatSource = PlayerCombat)
├─ Health + Died → 死亡處理
└─ Visual/ (Cat.glb) + attackOrigin + Hurtbox
```

敵貓**不吃**蓄力/撲擊/空中攻擊（同人形敵人只有一招的慣例）；那些是玩家操作層。

### 2.3 執行順序（都在 LateUpdate）

1. `CatProceduralWalk`（`[DefaultExecutionOrder]` 未設 = 0）→ 寫腿骨 swing/bend、body bob
2. `CatAttackPose`（`[DefaultExecutionOrder(20)]`）→ **讀** walk 已寫的 `localRotation`，在其上 `*=` 出招偏移（前掌全鏈 + 肩胸 hub Bone_011/012/013 + 脖子頭 Bone_026-028 + 尾 Bone_004-008）

兩者寫的骨頭有重疊（前掌 swing/bend bone），採「後者相乘疊加在前者結果上」策略，`CatAttackPose` 出招時透過 `CatProceduralWalk.SetAttackSuppression()` 把步態幅度壓下去（見 3.8），避免兩組旋轉打架。

---

## 3. 機制設計

### 3.1 三段連段 — 複用 `ComboAttackState`

- 左鍵（`AttackPressed`）點一下起手 `LightAttack1`（貓：前掌拍擊）；Recovery 的 combo window 內再點 → `CatSwipe2`（另一前掌 / 反手）→ `CatSwipe3`（雙掌下壓 + 前撲小位移 or 咬）。
- frame data、傷害、range/radius 全在 `Settings/Combat/Cat/CatSwipe{1,2,3}.asset`。起手值比玩家略快（貓體型小、動作俐落），range 比玩家短（前掌 reach）。**起點值待實機微調。**
- 第 3 段 `comboWindowFrames = 0`（連段收尾，同玩家 `LightAttack3`）。

### 3.2 蓄力重擊 — 新機制

**觸發（2026-08-29 定案：方案 A）**
- 按住左鍵 ≥ `chargeThresholdSeconds`（起點 0.35s）再放開 → 這一擊變重擊（取代普攻起手）。點放（短於門檻）→ 正常連段。
- 只在 `ComboAttackState` 為 Idle 時能起蓄力（連段中途不能突然變蓄力）。

**實作**
- `IInputCommand` 加 `bool AttackHeld { get; }`（`leftShiftKey`… 不對，是 `Mouse.leftButton.isPressed`）—— held 信號，與既有 `AttackPressed`（edge）並存。
- 新元件 `CatChargeAttack`：在 `PlayerCombat` 之前執行；量 `AttackHeld` 連續按住時間；`ComboAttackState` 仍是 Idle 且放開時已達門檻 → 把「這一次起手」導向重擊 `AttackData`（`CatHeavy.asset`：高傷、慢 startup、大 range/radius、`alwaysSpawnHitEffect = true`、擊退加成）。
- 作法選擇：`PlayerCombat.comboAttacks` 保持 3 段；`CatChargeAttack` 透過一個新的 `PlayerCombat.TryStartOverrideAttack(AttackData)` 公開方法插入單發重擊（重擊結束回 Idle，不接連段）。這樣 `ComboAttackState` 核心不動，只加一個「一次性覆寫招」入口。
- 蓄滿回饋：`CatAttackPose` 讀 `CatChargeAttack.ChargeNormalized`（0–1）做「壓低身體、後腿蓄力、尾巴豎起」的預備姿態；蓄滿後 pose 微顫。特效/音效在切片 2-4。

### 3.3 撲擊 pounce — 新機制

**觸發（2026-08-29 定案：方案 A；追加30 收緊）**
- **跑**（`CurrentHorizontalSpeed ≥ moveSpeed × pounceMinSpeedFraction`，預設 0.7）＋ 按方向鍵 ＋ `IsGrounded` ＋ `ComboAttackState` Idle ＋ 不在 cooldown 時按左鍵 → 撲擊。其餘（站著、貼牆、剛起步加速中、放鍵滑行、空中、連段中）→ 普通揮爪／蓄力。
- 規則抽成 pure `CatPounce.ShouldPounce(...)`，`CatCombatTests` 鎖。
- 追加30 前的版本只看「有按方向鍵」→ 站著微調位置手還在鍵上就誤觸（使用者回報「有時普通攻擊也會衝刺」）。同時把 `PlayerInputProvider` 改 `[DefaultExecutionOrder(-100)]`，`CatPounce` 不再讀到晚一幀的 `MoveInput`。
- 代價：跑動中的第一下攻擊仍一定是撲擊。使用者已接受此取捨。
- 撲擊「按住左鍵」時：先判定移動 → 撲擊；蓄力判定在撲擊之後才輪到（撲擊 Idle gate 已排除連段/撲擊進行中）。實作時 `CatPounce` 執行序在 `CatChargeAttack` 之前，撲擊觸發就消費掉這次 `AttackPressed`。

**實作**
- 新元件 `CatPounce`：概念同 `DodgeState` —— 起手鎖定方向（`CameraRelativeDirection(MoveInput)` 或身體正前）+ 速度窗（`pounceDistance / pounceDurationSeconds`），期間 `CharacterMovement` 讓出水平控制（新增一個像 dodge 分支的 `_pounceState` gate，或 `CatPounce` 直接寫 `CharacterController.Move` —— 傾向前者，跟 dodge 同機制）。
- 撲擊落點觸發 `PlayerCombat` 的撲擊招 `CatPounce.asset`（前撲抓擊，range 中等、傷害中高、擊退強）。時序：位移窗前段 = 撲出（Startup），中段 = 抓擊判定（Active），後段 = 落地（Recovery）。
- 數值：`Settings/Combat/Cat/CatPounce.asset` + `CatPounce` 元件上的 `pounceDistance` / `pounceDurationSeconds`（進 SO 或元件序列化欄位，rule 7 —— 位移數值放元件 serialized field，同 `CatProceduralWalk` 慣例）。
- 空中不能撲擊（`IsGrounded` gate）；撲擊 cooldown 避免連續平移。

### 3.4 空中攻擊 — `UseSphericalJudgment` hook

- 新元件 `CatAerialJudgment`：`Update` 裡 `playerCombat.UseSphericalJudgment = movement.IsFlying || !movement.IsGrounded;`
- 效果：飛行/跳躍/滯空時，`PlayerCombat.ResolveActiveHit` 改用 `OverlapSphere(attackOrigin, Range + Radius)`，不受身體 pitch/朝向影響 —— 貓在空中隨便對著目標揮爪都打得到（玩家的 aerial combat 已是這套邏輯）。
- 地面時自動回到方向性 capsule 判定（`PlayerCombat` 那行本來就每幀重算）。
- 連段/蓄力/撲擊在空中的可用性：連段可用（球判）；蓄力可用；撲擊不可用（3.3 的 `IsGrounded` gate）。

### 3.5 攻擊時移動 — 自由（不鎖）

- `PlayerCombat` 本來就不碰 `CharacterMovement`，維持現狀 —— 貓揮爪時腳可以繼續走（同玩家）。
- 唯一例外：撲擊期間（3.3）水平控制讓給 `CatPounce`，這是撲擊的本質不是「鎖腳」。

### 3.6 朝向 — 身體正面

- 不給貓 `TargetLockController` / lock-on。
- `attackOrigin` 是 `Visual` 下的子物件，朝向跟著貓身體正面（`CharacterMovement` 已讓角色 `SmoothDampAngle` 轉向移動方向）。
- 判定 capsule 從 `attackOrigin.forward` 打出去 —— 貓面向哪打哪。空中則球判無視朝向（3.4）。

### 3.7 `CatAttackPose` — 多骨頭 procedural pose

- **輸入**：`PlayerCombat.CurrentPhase`（Idle/Startup/Active/Recovery）、`PhaseProgress`（0–1）、`ComboIndex`（0/1/2 或重擊/撲擊的特殊 index）、`CatChargeAttack.ChargeNormalized`。
- **純函式**（EditMode 可測，同 `AttackPoseUtility`）：`ComputePoseWeights(phase, progress) → (windUp, strike, recover)` 三個 0–1 權重。
- **骨頭群組**（每段一組目標角度，`CaptureRest` 為基準，`Quaternion.Slerp` 混合）：
  - 前掌鏈：`Bone_034…029`（左）、`Bone_042…037`（右）—— swipe 用單掌，撲擊用雙掌
  - 肩胸 hub：`Bone_013 → 012 → 011` —— 前傾/下壓
  - 脖子頭：`Bone_026 → 027 → 028` —— 低頭瞄準 / 咬擊
  - 尾：`Bone_004…008` —— 平衡擺動（蓄力時豎起）
- 每段的目標角度組是序列化的 `struct`（`CatProceduralWalk.Leg[]` 的相同做法），實機調。
- LateUpdate、`[DefaultExecutionOrder(20)]`（見 2.3）。

### 3.8 `CatProceduralWalk` 抑制 hook

- 加 `public void SetAttackSuppression(float t)`（0 = 正常步態，1 = 完全抑制擺幅）。
- `CatAttackPose` 每幀呼叫：出招（`phase != Idle`）時 suppression 拉到 `attackWalkSuppression`（起點 0.7），Idle 時回 0，用 `MoveTowards` 緩動。
- `CatProceduralWalk.LateUpdate` 內把 `_gaitBlend` 或最終角度乘 `(1 - _attackSuppression)`。
- 純函式化其中的 clamp/lerp 供測試。

---

## 4. 命中反饋層（全新通用層）

> 設計原則：做成**通用元件**，貓先用，玩家/Boss 之後可選接。切片 2-6 必須跑既有 Boss 戰 PlayMode 測試確認不迴歸。

### 4.1 Hitstop（命中頓幀）

- 新 `HitStopController`（場景單一物件，靜態存取 `HitStopController.Request(seconds)`）。
- 實作：命中當幀起，`Time.timeScale = hitStopScale`（起點 0.05，非 0，避免完全凍結造成的問題）持續 `hitStopSeconds`（起點 0.06 ≈ 3–4 frame），用 `unscaledDeltaTime` 計時還原。
- **範圍（2026-08-29 定案：只在貓附身時啟用）**：`HitStopController` 只接受來自「貓附身狀態」的請求 —— `CameraPossessionSwitcher.Current == Cat` 才作用。玩家角色、Boss 戰、其他 AI 完全不受影響，切片不動任何共用戰鬥碼的 timeScale 行為。等於 hitstop 是「貓專屬手感」。
- 附身切回玩家時，`HitStopController` 立即把 `Time.timeScale` 還原成 1（防止切換當幀卡在慢動作）。
- 數值進 `Settings/Combat/HitStopTuning.asset`（每種命中類型一組：普攻 / 重擊 / 撲擊 / 被打）。

### 4.2 螢幕震動

- 新 `CameraShake` 元件，掛 `Main Camera` 與 `CatCamera` 兩顆。
- `CameraShake.Shake(amplitude, seconds)`；用 `unscaledDeltaTime`（跟 hitstop 疊加時才對）；衰減 noise 位移疊在 `ThirdPersonCameraController` 算出的位置上（在其之後、`[DefaultExecutionOrder]` 更大的 LateUpdate，或直接在 controller 尾端 hook）。
- 命中類型不同幅度（重擊/撲擊 > 普攻）；被打也震。
- 數值進 SO。

### 4.3 擊退

- `AttackResolver.ResolveHits` 目前傳 `DamageInfo.Direction = Vector3.zero` → 改成傳 `(target - origin)` 水平單位向量。
- `AttackData` 加 `knockbackSpeed` / `knockbackSeconds`（frame 制，可為 0 = 不擊退，維持既有招不變）。
- 承傷端：新通用 `KnockbackReceiver`（參考 Boss 的 `KnockbackReceiver` / `IKnockbackReceiver`，抽成通用版），`Health.Damaged` 事件觸發，對有 `CharacterController` 的目標套線性衰減位移窗。
- 假人加 `KnockbackReceiver`（切片 2-6）；貓被打也吃（切片 2-7）。

### 4.4 命中特效

- 沿用 `PlayerCombat.hitEffectPrefab` + `AttackData.HitEffectOverride` + `AlwaysSpawnHitEffect`。
- 貓：普攻用共享火花；重擊/撲擊各給 override（爪痕 / 撲擊衝擊波），`alwaysSpawnHitEffect = true`（揮空也出）。
- 沿用既有 `Attack3SlashEffectSetup` 的 renderer alignment 慣例（世界對齊、跟攻擊者朝向）。

### 4.5 音效

- 新 `CombatSfx` 元件（或複用一個簡單 `AudioSource` pool）：`PlayerCombat` 命中 / 揮空 事件 → 播對應 clip。
- clip 掛在 `AttackData` 上（`swingClip` / `hitClip`）或 `CombatSfx` 的對照表。
- 原創音效（rule 1）—— 佔位可用 Unity 內建或程序生成，正式音效另議。
- 被打音、擊退落地音。

---

## 5. 敵貓 AI ~~（已取消，2026-08-29）~~

> **使用者看到實作後決定不要敵貓**（「場上仍然有兩隻貓 請排除 → 兩隻敵貓都刪」）。`EnemyCat*` 與 `EnemyCatSetup.cs` / `CatEnemySwipe.asset` 已刪。戰鬥標靶改用場景現有的 `TrainingDummy` / `Enemy`。`CatAttackPose` / `CatProceduralWalk` / `MeleeKnockback` 本身保留（玩家貓在用）。以下為原規劃記錄。



- 複用 `EnemyAI`（chase-and-attack，`IInputCommand` + `ICharacterSpeedSource`，與視覺無關）。
- `CatProceduralWalk.speedSource` 指向 `EnemyAI`（已 implements `ICharacterSpeedSource`）。
- `CatAttackPose.combatSource` 指向敵貓的 `PlayerCombat`。
- 校準：`EnemyAI.moveSpeed`（貓 0.45 縮放、地面速度 3，比人形快）、`attackRange`（用 `PlayerCombat.MaxAttackReach` 自動同步，同 `EnemyAttackRangeSync` 慣例）、體型（`CharacterController` height/radius 照貓）。
- `Health` + `RespawnController`（追加30，5s in-place，掛 `GameManager`，同全場景其他角色）→ 死亡 5 秒後原地滿血復活。沒有倒下動畫（貓無 Animator）——只是消失再出現。
- **玩家貓也一樣**：`CatCharacterSetup` 給 `Cat` 掛 `RespawnController`（5s）。附身貓死掉時 `CameraPossessionSwitcher`（新 `catHealth` 欄位）自動 `FocusPlayer()` 交還控制，復活後按 C 重新附身。
- ~~Greybox 敵貓~~：已取消（見第 5 節）。戰鬥標靶用場景現有的 `TrainingDummy`（靜止假人）/ `Enemy`（076 AI）。
- `alwaysFaceTarget` 這類 076/077 專屬 flag 留 false（貓是普通敵人，感知到才轉身）。

---

## 6. 操作對照表（貓，附身時）

| 操作 | 鍵 | 效果 |
| --- | --- | --- |
| 移動 | WASD | 相機相對，自動轉向 |
| 普攻連段 | 點左鍵 ×3 | CatSwipe1 → 2 → 3（Recovery 內續段） |
| 蓄力重擊 | 按住左鍵 ≥0.35s 放開（方案 A） | CatHeavy（高傷、擊退強、揮空也有特效） |
| 撲擊 | 移動中點左鍵（方案 A） | 前撲位移 + 抓擊 |
| 空中攻擊 | 飛行/滯空時點左鍵 | 連段（球形判定，無視朝向） |
| 飛行 / 下降 / boost / 衝刺 | Ctrl / Shift 按住 / Q / 點 Shift | 切片 1，不變 |
| 視角切換 | C | 切片 1，不變 |

---

## 7. 分切片交付計畫

> 每個切片 = 一個可測試功能（rule 9），可在 `GreyboxTest` 重現，有 EditMode/PlayMode 測試。

> 順序 2026-08-29 定案：命中反饋（2-4）提前到 pose 之後，蓄力/撲擊順延。
> **狀態（追加30）：2-1 ~ 2-7 全部實作完成**，程式碼 + Editor 接線 + EditMode 測試已驗；每格「測試」欄的 PlayMode 部分尚未實跑（Play mode 失焦凍結），實機手感待調。

| 切片 | 內容 | 主要新增 | 測試 |
| --- | --- | --- | --- |
| **2-1** 骨架 + 單招 | `CatCombatSetup` 選單：`PlayerCombat` 掛貓、`attackOrigin` 子物件、`CatSwipe1.asset`、`CatAerialJudgment`、`catControl` 接線。貓打假人扣血。無 pose | `CatCombatSetup.cs`、1 asset、`CatAerialJudgment.cs` | PlayMode：附身貓 → 左鍵 → 假人 `Health` 下降；空中 → 球判命中 |
| **2-2** 三段連段 | `CatSwipe2/3.asset`、combo window 調校 | 2 assets | EditMode：`ComboAttackState`（已測）；PlayMode：三段都命中、combo window 續段 |
| **2-3** `CatAttackPose` | 多骨頭 procedural、`CatProceduralWalk.SetAttackSuppression` hook、三段各一姿態 | `CatAttackPose.cs`、`CatProceduralWalk` 改 | EditMode：`ComputePoseWeights` / suppression 純函式；Greybox 目視 |
| **2-4** 命中反饋（提前） | `HitStopController`（**只在貓附身時作用**，見 4.1）、`CameraShake`（掛 `CatCamera`）、`DamageInfo.Direction` + 通用 `KnockbackReceiver`、`CombatSfx`、假人加 `KnockbackReceiver`、命中/揮空 VFX·SFX | 多個新元件 + SO | PlayMode：附身貓命中 → timeScale dip / 假人被推 / `CatCamera` 震；**回歸：切回玩家 timeScale 立即回 1、Boss 戰 PlayMode 測試全綠、`AttackResolver` 既有測試全綠** |
| **2-5** 蓄力重擊 | `IInputCommand.AttackHeld`、`PlayerInputProvider` 擴充、`CatChargeAttack.cs`、`PlayerCombat.TryStartOverrideAttack`、`CatHeavy.asset`、蓄力 pose | `CatChargeAttack.cs` 等 | EditMode：蓄力計時 / 門檻純函式；PlayMode：按住放開 → 重擊命中、點放 → 普攻 |
| **2-6** 撲擊 pounce | `CatPounce.cs`、`CharacterMovement` 撲擊 gate、`CatPounce.asset` | `CatPounce.cs`、`CharacterMovement` 改 | EditMode：撲擊位移窗數學（同 `DodgeState` 測法）；PlayMode：撲擊命中 + 位移 + cooldown；**回歸：全套 movement 測試** |
| **2-7** 貓承傷（~~+ 敵貓~~） | 貓 `Health` + `MeleeKnockback` + 5s `RespawnController`。**敵貓部分已取消**（見第 5 節）。附身貓死 → `CameraPossessionSwitcher` 自動 `FocusPlayer()` | — | PlayMode：玩家貓被打扣血、死後 5s 復活 |

---

## 8. 測試策略

- **純邏輯優先**：連段（`ComboAttackState` 已測）、蓄力計時、撲擊位移、pose 權重、hitstop 計時、擊退衰減 —— 全部抽純函式/純類別，EditMode 測。
- **PlayMode**：每個切片一個「附身貓打假人 → 預期效果」的整合測試，跑真 Update 迴圈（同 `CatFlightAndDashTests` / `DodgeMovementTests` 的 reasoning）。
- **回歸**：切片 2-6 動到共用的 `AttackResolver` / `DamageInfo` / 可能全域 `Time.timeScale`，必跑既有 `CombatPlayModeTests` / `EnemyAttacksPlayerTests` / Boss 相關 PlayMode 測試。
- **已知環境限制**：MCP 下 PlayMode 測試偶爾 wedge（見 `KNOWN_ISSUES.md`），必要時 `RequestScriptReload` 清、或請使用者實跑。
- 「一定要人眼確認」：pose 觀感、每招 frame data 手感、hitstop/震動強度、撲擊距離、敵貓難度。

## 9. 對專案規則的符合性

- **rule 4**（Live2D 不參與判定）：貓是 glb 不是 Live2D；判定是 `PlayerCombat` 的 `OverlapCapsule`/`OverlapSphere` 幾何查詢，pose 只是視覺。✅
- **rule 6**（每功能要測試 / 可在 GreyboxTest 重現）：見第 7、8 節。✅
- **rule 7**（數值進 SO）：frame data / 傷害 / range → `AttackData`；hitstop / 震動 / 擊退 → 專屬 Tuning SO；撲擊位移數值 → 元件 serialized field（同 `CatProceduralWalk` 既有慣例）。✅
- **rule 8**（玩家與 AI 共用輸入）：敵貓走 `EnemyAI : IInputCommand` → 同一個 `PlayerCombat`。✅
- **rule 9**（一次一個可測試功能）：切片 2-1 ~ 2-7。✅

## 10. 風險與待人眼確認

- **glb 骨頭方向未知**：Meshy generic auto-rig，`CatAttackPose` 的每軸旋轉方向只能實機調（同 `CatProceduralWalk` 踩過的坑）。
- **pose 疊 walk 的視覺打架**：靠執行順序 + suppression hook 緩解，仍需目視。
- **全域 hitstop 的副作用**：若採全域 `Time.timeScale`，Boss 戰、其他 AI、飛行中的角色都會被頓 —— 切片 2-6 要專門回歸測試（見問題 3）。
- **`CharacterMovement` 加撲擊 gate**：這支檔案已經很複雜（飛行/dodge/slide/gravity 交織），加一個新 gate 有迴歸風險，切片 2-5 要跑全套 movement 測試。
- **敵貓難度**：`EnemyAI` 數值照人形調的，貓體型/速度不同，需校準。
- **音效原創性**（rule 1）：佔位音效可用，正式音效要原創，另議。

---

## 已定案（2026-08-29）

1. 蓄力重擊觸發：**方案 A**（按住左鍵 ≥0.35s 放開）
2. 撲擊觸發：**方案 A**（地面移動中點左鍵；移動中無法原地揮爪，已接受）
3. hitstop 範圍：**只在貓附身時啟用**（不碰玩家/Boss 戰的 timeScale）
4. 切片順序：命中反饋**提前為 2-4**，蓄力/撲擊順延為 2-5 / 2-6

## 仍待決（不擋開工，切片 2-7 前再定）

- 敵貓死亡表現：倒下 pose + 淡出，還是直接消失？
- 蓄力/撲擊/命中反饋的實機手感數值（frame data、hitstop 長度、撲擊距離、震動強度、敵貓難度）—— 一律實機微調。
