# 戰鬥系統現況快照 — 玩家 vs 武士（給 AI 分析用）

> 產出日期：2026-09-01 / 對應 CHANGELOG「追加94 續 8」之後的狀態。
> 場景：`Assets/_Project/Scenes/GreyboxTest.unity`（垂直切片驗證場景）。
> 本文件目的：把「玩家」「武士（Samurai Boss）」兩個 GameObject 及其相關戰鬥機制的**當前實作細節**攤平，方便丟給另一個 AI 做設計/平衡/程式分析。文中所有數值都是**場景序列化值 / 資產實際值**，不是腳本預設值（兩者常常不一致——見 §12）。
>
> ⚠️ **2026-09-02 更新**：這份快照是「續 8」時的狀態，之後續 9–34 依 `Docs/WUSHI_COMBAT_ENGINEERING_SPEC.md`
> 做了大量改造，本文多處已過時。**已知過時的關鍵點**：
> - 武士 gameplay root `localScale` 已 4→1（續 33，做法 A）——可見比例不變、但 `transform.position` / CC /
>   hurtbox 世界尺寸現在是可讀公尺數；文中提到「4× 縮放」的座標推導要重算。
> - `DeflectReaction` 每 hit-window（續 12）、一般格擋架勢改每招 `PoiseDamage`（續 11）、武士 2 個
>   Deathblow 生命節點 + 永久死亡（續 19）、共享特殊冷卻 7s（續 20）、彈反窗 0.20s + 反連按（續 5）。
> - 每招 hit window nt 值續 8 之後又動過數次（續 22/25/26/27），以 `Wushi_Attack_*.asset` 現值為準，
>   或跑 `Tools/Live2DAction/[9] 武士 Attack Timing Report` 選單看即時推導。
> - 規格逐項進度見 `WUSHI_COMBAT_ENGINEERING_SPEC.md` 開頭的「實作進度」表。

---

## 1. 名詞對照

| 中文 | 英文 / 型別 | 說明 |
| --- | --- | --- |
| 玩家 | `Player` GameObject | 可操作角色，武士刀近戰 + 究極技 + 斬殺 + 右鍵防禦/彈反 |
| 武士 | `武士` GameObject（劇情/UI 內稱 Samurai Boss） | 切片 2 的頭目，`BossStateMachine` 驅動 |
| 架勢 / 架式條 / posture | `StancePoise` | 魂系「架勢條」，滿格→僵直（stagger），可被斬殺 |
| 硬直 / 僵直 | stagger | `StancePoise.IsStaggered == true`，開一段可被處刑的空檔 |
| 斬殺 / 處刑 | `ExecutionAbility`（玩家按 F） | 對僵直目標的處決 |
| 彈反 / 完美格擋 | Parry（`BladeClashResult.Parried`） | 隻狼式 deflect，按下防禦後 0.2s 窗口內被擊中 |
| 一般格擋 | Guard（`BladeClashResult.Guarded`） | 防禦鍵按住、但不在彈反窗口 |
| 掃掠判定 | swept cast in `BossHitbox` | 每個 FixedUpdate 從上一幀刀姿到這一幀刀姿做 Box/Sphere/CapsuleCast，避免快刀穿透 |
| hit window / 有效幀 | `BossHitWindow`（normalized 0–1） | 攻擊動畫中「刀真的能造成傷害」的區間；用 clip normalized time 表示 |

---

## 2. 傷害管線（所有攻擊共用）

```
攻擊方 → DamageInfo → IDamageable.ApplyDamage → Health.ApplyDamage
                                                   │
                                                   ├─ if IsDead || IsInvulnerable → return（完全無效）
                                                   ├─ 依序套用 IIncomingDamageModifier[]（GetComponents，跳過 disabled Behaviour）
                                                   │     └─ PlayerGuard.ModifyIncoming（見 §7）
                                                   ├─ CurrentHealth -= damageInfo.Amount
                                                   ├─ Damaged?.Invoke(damageInfo)  ← StancePoise 監聽這個累積架勢
                                                   └─ if CurrentHealth <= 0 → IsDead = true; Died?.Invoke()
```

### `DamageInfo`（`Core/DamageInfo.cs`，readonly struct）
| 欄位 | 型別 | 意義 |
| --- | --- | --- |
| `Amount` | float | 血量傷害 |
| `Point` | Vector3 | 命中世界座標（特效用） |
| `Direction` | Vector3 | **從攻擊方指向被擊方**的方向（水平），格擋正面判定用 |
| `Source` | GameObject | 攻擊方（通常是 boss root） |
| `ExplicitPoiseAmount` | `float?` | 非 null 時，架勢傷害與血量傷害脫鉤（目前只有 `BossHitbox` 會傳）；null 時架勢 = `Amount × stanceGainMultiplier` |

### `Health`（`Core/Health.cs`）
- `maxHealth`：玩家 **500**、武士 **1000**（場景值）。
- `IsDead` / `CurrentHealth`（get）、`event Action<DamageInfo> Damaged`、`event Action Died`。
- `SetInvulnerable(object source, bool)`：多來源引用計數式無敵（`StancePoise` 起身無敵窗口用）。
- `deferDeactivationToDeathAnimation`：玩家 = true（等死亡動畫播完才 SetActive(false)）。
- `HurtboxLink`（子物件 `PlayerHurtbox` 上）實作 `IDamageable`，把命中轉發到 root 的 `Health`。

### `IIncomingDamageModifier`（`Core/IIncomingDamageModifier.cs`）
```csharp
DamageInfo ModifyIncoming(DamageInfo incoming);
```
掛在跟 `Health` 同一個 GameObject 上，在扣血前依序改寫 `DamageInfo`。目前唯一實作者：`PlayerGuard`。

---

## 3. 架勢系統 `StancePoise`（`Combat/StancePoise.cs`）

跟 `Health` 分離的獨立元件，只掛在需要斬殺機制的角色上（玩家、武士、屁孩王、Enemy…）。

| 欄位 | 玩家（場景） | 武士（場景） | 腳本預設 | 意義 |
| --- | --- | --- | --- | --- |
| `maxStance` | **100** | **60** | 60 | 架勢上限 |
| `staggerDurationSeconds` | **1.2**（追加94 續 11：guard-break 用，原 6） | 6 | 6 | 僵直窗口長度（沒被處刑就自己站起來） |
| `regenDelaySeconds` | **1.5** | **3** | 1.5 | 幾秒沒受擊後架勢開始回復 |
| `regenPerSecond` | **20** | **8** | 20 | 架勢回復速度 |
| `stanceGainMultiplier` | **0.5** | **0.2** | 0.2 | 受擊架勢增益 = `Amount × 此值`（僅當 `ExplicitPoiseAmount` 為 null） |
| `postStaggerGraceSeconds` | **1.5** | 1.5 | 1.5 | 起身後真實無敵窗口（血量 + 架勢都免疫，`Health.SetInvulnerable`） |

行為要點：
- `Health.Damaged` → `OnDamaged` → `gain = info.ExplicitPoiseAmount ?? info.Amount * stanceGainMultiplier` → `ApplyStanceGain`。
- `ApplyStanceGain`：`_currentStance` 達 `maxStance` → **同一幀** `IsStaggered = true` 且 `_currentStance = 0`（架勢條瞬間清空）。
- `AddPostureDamage(float)`：**繞過 Health/DamageInfo 管線**直接加架勢（不扣血）。彈反系統就是走這條——玩家彈反時對 boss 呼叫 `bossStance.AddPostureDamage(parryBossPoiseDamage=14)`。
- `EndStagger()`：`IsStaggered=false`，架勢歸 0，開 `postStaggerGraceSeconds` 無敵。由 `ExecutionAbility`（處刑落刀）或僵直逾時呼叫。
- `RestoreStanceFractionAfterRecovery(float)`：起身後恢復到某個比例（武士起身恢復約 20%，`tuning.PostureRestoreOnRecover = 0.2`）。

---

## 4. 玩家 GameObject（`Player`）

### 4.1 階層（戰鬥相關子物件）
| 子物件 | Layer | Collider | 元件 |
| --- | --- | --- | --- |
| `Visual` | Default | — | `Animator`（`NewAnimator.controller`，Player5「lacrimosa」模型，**與 中立者1 / 守望者 共用**） |
| `PlayerHurtbox` | **PlayerHurtbox**(3) | CapsuleCollider trigger | `HurtboxLink`（轉發到 root Health） |
| `GuardVolume` | **PlayerGuardWeapon**(6) | CapsuleCollider trigger（預設 disabled） | `PlayerGuardVolume`（見 §7.3） |
| `ClashFeedback` | Default | — | `AudioSource` + `PlayerClashFeedback`；子物件 `GuardSparks` / `ParrySparks`（ParticleSystem，程序生成 additive）。防禦/彈反刀刃碰撞聲 = `KatanaClash.mp3` |
| `AttackSfx` | Default | — | `AudioSource`（3D，min/max 3/45）+ `PlayerAttackSfx`（訂閱 `PlayerCombat.Hit`，每次左鍵揮刀播 `KatanaSwing.mp3`，揮空也播，pitch 0.92–1.10）。追加94 續 10 |
| `BackGreatswordDecor` | Default | — | `MeshBoundsFixer`（背後裝飾大劍，純外觀，Genshin Wolf's Gravestone，DoNotShip） |
| `ReadyFlameAura` | Default | — | ParticleSystem + AudioSource（究極就緒火焰光環） |
| 右手掛點 `.../Rhand_Weapon2/WolfsGravestone` | — | — | `BloodKatana.glb`（實際持用的武士刀，約 80× 骨骼縮放，`MeshBoundsFixer`，子物件 `BladeMesh`）。掛點**名字叫 WolfsGravestone 但拿的是血刀**；R 究極會擲出它 |

> 註：掛點骨骼縮放 ~80×，設定 `localPosition` 會被放大甩飛，必須用世界座標放置。katana 授權未確認；背後大劍為 DoNotShip。

### 4.2 根物件元件（場景序列化值）
| 元件 | 關鍵值 |
| --- | --- |
| `CharacterController` | height 1, radius 0.4, slopeLimit 65, **stepOffset 0（刻意，見 §12）**, skinWidth 0.08, center (0,0,0) |
| `CharacterMovement` | moveSpeed 2, walkSpeed 0.9, gravity -20, jumpSpeed 7；`ExternalSpeedMultiplier`（public，`PlayerGuard` 防禦時設為 0.35） |
| `PlayerCombat` | attackOrigin = Player root；hitEffectPrefab = HitEffect；comboAttacks = LightAttack1–4（各 Range 0.5 / Radius 0.5）；`CurrentActiveAttack` + `AttackOrigin`（public props） |
| `Health` | maxHealth 500, `deferDeactivationToDeathAnimation` true |
| `StancePoise` | maxStance 100 / **stagger 1.2s（guard-break，追加94 續 11，原 6）** / regenDelay 1.5 / regen 20 / gainMult 0.5 / grace 1.5 |
| `TargetLockController` | maxLockRange 15, maxLockAngleDegrees 60, breakRange 20 |
| `UltimateAbility`（R） | damage 500, spinCount 5, castVfxPrefab = SwordOrbitSkillVFX（實際已改火焰柱 PlayerUltimateAura，見記憶） |
| `ExecutionAbility`（F） | executionRange 2.5, executionAnimationSeconds 1.5, `executeTriggerName = "Execute"`, executionDamagePercentOfCurrentHealth 0.5（對僵直目標當前血量的 50%）；會 snap 面向受害者 |
| `KnockbackReceiver` | launchUpwardSpeed 7, instantDisplacementFraction 0.15 |
| `BossTeamMember` | team = Player |
| `PlayerGuard`（右鍵防禦/彈反） | 見 §7.1 全表 |
| `PlayerGuardVisualizer` | 藍色扇形提示（追加93），range 2, height 1.1，依 `guard.CurrentDefense` 變色（Parry 白 / Guard 藍 / None 隱藏） |
| `PlayerGuardAnimatorLink` | 每幀 `SetBool("IsGuarding", guard.IsBlocking)`；`Parried`/`Guarded` 事件且非按住狀態時 `SetTrigger("GuardParry")` |
| `SekiroDeflectDebug` | F9 開關的 debug overlay（Gizmo + OnGUI，見 §7.5） |
| `UltimateEnergy` ×2 | (100, regen 5 每 3s) 與 (500, regen 30 每 1s) |
| 其他 | `CharacterAnimatorLink` / `CharacterAttackAnimationLink` / `StaggerAnimationLink` / `DeathAnimationLink` / `HealthRegeneration`（閒置 10s 後 2/s） / `UltimateReadyAura` / `UltimateActivationBurst` / `UltimateAttackAnimationSwap` |

### 4.3 玩家 Animator（`NewAnimator.controller`，layer 0）
參數：`Speed, Jump, Fly, Aim, H, V, Grounded, Attack1-4, Execute, Staggered, Dead, IdleSword, WalkSword, RunSword, AttackComboSword, ExecuteThrust, IsGuarding, GuardParry`

| State | speed | motion | 備註 |
| --- | --- | --- | --- |
| Idle / Locomotion / Fall / Jump | 1 / 1 / 0.7 / 1.5 | NewIdle / Locomotion / NewFall / NewJump | |
| Attack1–4 | 3.4 / 3.2 / 2.1 / 2.3 | CrossPunch / HookPunch / Uppercut / MmaKick | 舊拳擊連段（近戰改刀後多半 inert） |
| Execute | 1 | FlyingKick | F 斬殺（舊，經 ExecutionAbility.executeTriggerName 已改用獨立 trigger） |
| Staggered / Dead | 1 / 1 | KneelingDown / Dying | |
| IdleSword / WalkSword / RunSword / AttackComboSword | 1 | KBS_* | 武士刀 locomotion / 連段（retarget 中，`TC_Sword_Free_Pack`） |
| ExecuteThrust | 1.35 | ContinuousThrust | 追加87 加、追加89 停用（動作是旋轉突刺，打空 + 位移） |
| **Guard** | 1 | **GuardHold**（`Guard.fbx` 子clip，frames 7–15，loop） | 雙手舉刀持續姿勢；`AnyState→Guard [IsGuarding]` dur 0.14 |
| **GuardParry** | 2.4 | **Guard**（`Guard.fbx` 完整，frames 1–61） | 一次性揮手；`AnyState→GuardParry [GuardParry trigger]` dur 0.03 |

`Guard.fbx`：Meshy `Meshy_AI_Parkside_Portrait_biped` 動畫，62 幀 / 2.03s，Humanoid + lockRootPositionXZ + lockRootRotation。授權：Meshy 付費方案、可商用（見 `ASSET_LICENSES.md`）。

---

## 5. 武士 GameObject（`武士`）

### 5.1 Transform / 根元件（場景值）
- pos (0, 0.6, 11)（「本地」地圖北端，離出生點）、rot (0,180,0)、**scale 4**、layer Default。
- `Animator`：WushiAvatar + `Wushi.controller`，**`applyRootMotion = false`**（重要，見 §11 位移問題）。
- `Health`：maxHealth **1000**。
- `StancePoise`：maxStance 60 / regenDelay 3 / regen 8 / gainMult 0.2。
- `BossTeamMember`：team = 武士。
- `CharacterController`：height 0.969, radius 0.113, slopeLimit 45, center (0, 0.46, 0)。
- `BossStateMachine`：見 §9 / §10。
- `LockOnTarget`：aimPoint = ChestAimPoint, cameraDistanceMultiplier 2.2, cameraFrameBias 0.9, `useDuelCamera` true, `duelTargetHeight` 4.1。
- `TooCloseRangeIndicator`、`UltimateEnergy`(100)、`NavPathFollower`（追加71，agent-less 繞障礙）、`AudioSource`（KatanaDraw）、`CinemachineImpulseSource`（DefaultVelocity (0,-0.4,0.15)）、`BossSignalReceiver` + `SignalReceiver`（開場過場 追加92）。

### 5.2 子物件 / hitbox
| 子物件 | Layer | Collider | 世界尺寸 | 備註 |
| --- | --- | --- | --- | --- |
| `BladeHitbox`（`.../RightHand/KatanaSocket/KatanaRoot/KatanaMesh/BladeHitbox`） | **BossWeapon**(7) | CapsuleCollider trigger，r 0.1 / h 0.52 / dir X | lossyScale ~3.2 → 世界半徑 ~0.32、長 ~1.66 | 沿刀身的判定；`BossHitboxPart.Weapon` |
| `RightFootHitbox`（`.../RightFoot/RightFootHitbox`） | Default | SphereCollider trigger，r 14（骨骼縮放極小 → 世界半徑很小） | | `BossHitboxPart.RightFoot`，SpartanKick 用 |
| `LandingAOEHitbox`（根下） | Default | SphereCollider trigger，r 0.75，lossyScale 4 → 世界 r 3 | | LeapSlam / DiveAttack 落地衝擊波 |
| `BodyHurtbox` | （PlayerHurtbox/敵對層） | | | 身體受擊區 |
| `ChestAimPoint` | | | | 鎖定/攝影機 aim |

### 5.3 `BossHitbox`（`Combat/Boss/BossHitbox.cs`）——掃掠判定 + 一次性去重
- Trigger collider，預設 disabled，kinematic Rigidbody（`ContinuousSpeculative`）。
- `Activate(attack, window)` / `Deactivate()`；`IsActive`、`ActiveWindowPart`（`BossHitboxPart?`）。
- **每 FixedUpdate 掃掠**：從 `_previousPosition` 到目前 `transform.position` 做 BoxCast/SphereCast/CapsuleCast（mask `~0`），修正快刀 trigger 穿透（tunneling）。`LastSweepFrom/LastSweepTo/HasSwept` 供 debug 疊圖。
- **一次性命中**：`_hitTargetsThisActivation`（HashSet<Transform>，key = 目標 root），**每次 window 啟用期間、對每個目標只結算一次**。這就是使用者要的「AttackInstanceId / 同一次攻擊同一目標只命中一次」——不需要另外的 id 欄位，window 的一次 Activate→Deactivate 就是那個 instance。
- `BossHitboxPart` enum：`LeftHand=0, RightHand=1, LeftFoot=2, RightFoot=3, Body=4, LandingAOE=5, Weapon=6`。
- `BossHitWindow.deflectReaction`（追加94 續 12，`DeflectReaction` enum）：`Recoil=0`（預設）/ `ContinueCombo=1` / `CancelAttack=2` — 見 §9.5。
- **血量傷害計算** `ComputeHealthDamage`：`HealthDamageIsPercentOfTargetMax` → `baseHealthDamage%` × 目標 `Health.MaxHealth`（例如 5 → 5%）；否則直接 `baseHealthDamage`。× `window.damageMultiplier`。架勢傷害 = `attack.BasePoiseDamage × window.damageMultiplier`（透過 `DamageInfo.ExplicitPoiseAmount` 傳）。

---

## 6. `Wushi_Tuning`（`Settings/Combat/Boss/Wushi_Tuning.asset`）關鍵值

| 欄位 | 值 | 意義 |
| --- | --- | --- |
| `alertRange` | 6 | 進入戰鬥距離 |
| `phaseThreshold` | 0.5 | 血量 50% 進入 Phase 2 |
| `walkSpeed` / `runSpeed` / `unsteadyWalkSpeed` | 5.5 / 7.5 / 1.2 | |
| `rotationSpeedDegrees` | 520 | 轉向速度 |
| `globalRestPhase1/2` Min-Max | 0.05–0.15 / 0.03–0.08 | 攻擊之間全域休息 |
| `majorAttackExtraRest` Min-Max | 0.1–0.3 | 大招後額外休息 |
| `attackRotationRecoverySeconds` | 6 | 招式輪替偏壓恢復窗口（LRU 軟性避免同招被孤立） |
| `attackRotationRecentFactor` | 0.15 | 剛用過的招權重降到此比例 |
| `decisionIntervalSeconds` / Phase1 / Phase2 | 0.2 / 0.05–0.12 / 0.03–0.08 | 決策間隔 |
| `postureUnsteadyEnterFraction` / `exitFraction` | 0.75 / 0.6 | 架勢達 75% 進入不穩步態，掉回 60% 退出 |
| `postureBreakDurationMin/Max` | 3 / 3 | PostureBroken（僵直）持續 3s |
| `postureRegenDelaySeconds` / `postureRegenPerSecond` | 2 / 5 | FSM 內部架勢回復（與 StancePoise 場景值不同，見 §12） |
| `postureKneelNormalizedTime` | 0.5 | 跪姿 clip 到 0.5 normalized 定住 |
| `postureRestoreOnRecover` | 0.2 | 起身恢復 20% 架勢 |
| `reviveDelaySeconds` / `standUpSeconds` / `permanentDeath` | 5 / 1.8 / false | 死後 5s → 1.8s 起身 → 復活 |
| `vanishTriggerSeconds` | **999999** | 消失/瞬移 cycle **實質關閉** |
| `breakdanceTriggerSeconds` | 15 | 每累積 15s 戰鬥時間觸發 Breakdance |
| `leapSlamTriggerSeconds` | 20 | 每累積 20s 戰鬥時間觸發 LeapSlam |
| `leapSlamWindupSeconds` | 1 | 飛天前蹲下前搖 1s |
| `leapSlamExtraHeight` | 30 | 跳躍高度 |
| `tooCloseDistance` / `tooCloseDurationSeconds` | 1.6 / 2 | 玩家貼身 2s → 強制 SpartanKick |
| `dodgeCounterChancePhase1/2` | 0.15 / 0.25 | 閃避反擊機率 |
| `dodgeIframeStartNormalized` / `End` | 0.15 / 0.35 | 閃避無敵幀 |
| `sprintMinDistance` / `Max` / `cooldown` | 4 / 8 / 6 | 衝刺接近 |
| `ultimate*` | （見資產） | 必殺技（距離 2–5m，前搖 1.5–2s）——`normalAttackPool` 之外的獨立系統 |

> 註：`periodicSlamIntervalSeconds`（OverheadSlam 每 30s）是 `BossStateMachine` 上的**序列化欄位**，不在 tuning 裡。

---

## 7. 隻狼式彈反系統（追加94，2026-09-01）

設計來源（使用者引用的資料拆解）：隻狼標準彈反窗口約 **12 幀 @60 = 0.2s**；按下防禦後敵人攻擊落在此窗口內 → 彈反，之後仍可一般格擋；**防連按**：短時間反覆放開再按，窗口逐步縮短最差到 0，成功彈反則恢復。

**設計原則：擴充既有系統，不重做玩家 HP / 傷害系統。** 玩家血量、架勢、`DamageInfo` 管線完全沿用 §2/§3。

### 7.1 `PlayerGuard`（`Combat/PlayerGuard.cs`）
`class PlayerGuard : MonoBehaviour, IIncomingDamageModifier, IBladeClashReceiver`
`enum DefenseState { None, Guard, Parry }`

視窗完全由**「防禦鍵按下的那一瞬間（press edge）」以來的時間**驅動，跟任何動畫幀無關：
- press edge → `_guardStartTime = Time.time`，彈反窗口開啟 `EffectiveParryWindow` 秒。
- 在 `[0, EffectiveParryWindow]` → `CurrentDefense == Parry`。
- 按住、超過窗口 → `CurrentDefense == Guard`。
- 放開 / 僵直 / 死亡 → `None`，全關。
- **按住不會重開彈反窗口，只有新的 press edge 會。**（隻狼 tap-to-deflect：快點一下也能彈反，不必一直按住。）

| 欄位 | 場景值 | 意義 |
| --- | --- | --- |
| `guardArcDegrees` | 120 | 正面格擋錐角（`Dot(forward, toBoss) >= cos(60°)`） |
| `parryWindowDuration` | **0.2** | 基礎彈反窗口（隻狼 12 幀）。實際窗口 = 此值 × anti-mash scale |
| `tapGuardWindowSeconds` | 0.55 | press edge 後即使放開，這段時間內仍算「格擋」（不吃乾淨命中），容錯 |
| `mashResetSeconds` | 0.35 | 兩次 press 間隔小於此 → 判定為連按 |
| `mashShrinkPerTap` | 0.4 | 每次連按讓 `_parryScale` 減 0.4 |
| `mashRecoverPerSecond` | 1.2 | 沒連按時 `_parryScale` 每秒回復 1.2 |
| `minMashScale` | 0 | scale 下限（0 = 純隻狼，硬連按會完全失去彈反窗口） |
| `restoreScaleOnParry` | true | 成功彈反 → `_parryScale` 直接回 1 |
| `clashCooldownSeconds` | 0.06 | 兩次已結算 clash 的最短間隔（避免一次刮擦爆一串火花/音效；夠小讓多段攻擊每段都登記） |
| `blockedDamageMultiplier` | 0.15 | **非刀刃**正面軟格擋後仍穿透的血量傷害比例（踢擊用；boss 刀刃走 clash path，不吃這個） |
| `poiseMultiplier` | 0.2 | 軟格擋仍造成的架勢 = `Amount × 此值`（保持 = `stanceGainMultiplier`） |
| `guardChipDamage` | 0 | 一般格擋的削血 |
| `guardPoiseMultiplier` | 1 | **追加94 續 11（spec 項目 6）**：一般格擋玩家吃的架勢 = 該招 `info.PoiseDamage` × 此值（DoubleCombo/ChargeCut 12、SpartanKick 14、SwordJudgment/OverheadSlam 22）。1 = 全額該招 poise |
| `guardPlayerPoiseDamage` | 6 | **降級為 fallback**：只在 clash 沒帶該招 poise 時用（真實 boss 攻擊一定有帶，幾乎用不到） |
| `guardHitStopSeconds` / `Scale` | 0.05 / 0.4 | 一般格擋 hitstop（輕微） |
| `guardShakeAmplitude` / `Seconds` | **0** / 0.1 | 一般格擋不震鏡（避免每次格擋都在抖） |
| `parryPlayerPoiseDamage` | **0** | 完美彈反玩家吃的架勢（≈0） |
| `parryBossPoiseDamage` | **14** | 完美彈反對 boss 造成的架勢（`bossStance.AddPostureDamage(14)`） |
| `parryHitStopSeconds` / `Scale` | 0.1 / 0.15 | 彈反 hitstop（比格擋更重） |
| `parryShakeAmplitude` / `Seconds` | 0.06 / 0.16 | 彈反震鏡 |
| `blockedSpeedMultiplier` | 0.35 | 舉刀時地面移動速度乘數（寫入 `CharacterMovement.ExternalSpeedMultiplier`） |
| `useProceduralPose` | **false** | 程序 2 骨舉刀姿勢已關閉（改由 Animator 的 Guard/GuardParry state 驅動，避免打架） |
| `swordArmBone` / `upperArmBone` | Bip001-R-Forearm / Bip001-R-UpperArm | （目前不用） |
| `cameraShake` | **場景中為 null** | Awake 時自動抓 `Camera.main` 上的 `CameraShake` |

公開 API：
```csharp
float ParryWindowScale        // _parryScale，0..1，anti-mash 縮放
float EffectiveParryWindow     // parryWindowDuration * _parryScale
bool  IsBlocking               // 按住 + 沒死 + 沒僵直 + 沒被 CancelDefenseAction 壓制
bool  InParryWindow            // CanDefend && EffectiveParryWindow>0.001 && 在窗口內
bool  InTapGuardWindow         // press edge 後 max(parryWindowDuration, tapGuardWindowSeconds) 內
bool  DefenseActionActive      // 追加94 續 13：唯一「防禦動作中」訊號 = CanDefend && !suppressed && (IsBlocking || InTapGuardWindow)
DefenseState CurrentDefense    // = DefenseStateCode(InParryWindow, DefenseActionActive)：InParryWindow→Parry / DefenseActionActive→Guard / None
void  CancelDefenseAction()    // 追加94 續 13：按鍵還按著也強制結束防禦（Execution/Ultimate/死亡/僵直），放開時自動解除
float GuardArcDegrees
event Action<Vector3> Parried  // 帶接觸點
event Action<Vector3> Guarded
event Action<DamageInfo> Blocked
float LastBlockTime, LastParryTime
```
> 追加94 續 13：`PlayerGuardVolume` / `PlayerGuardAnimatorLink`（`IsGuarding` bool）/ 移動減速 / 姿勢 / `PlayerGuardVisualizer` / `SekiroDeflectDebug` **全部**讀 `DefenseActionActive` / `CurrentDefense`，不再各自組 `IsBlocking + InTapGuardWindow`（修「隱形格擋」）。`AnyState→Guard` 轉場 0.14→0.05s。

`Update()`：`_parryScale` 朝 1 `MoveTowards`；press edge 時若距上次 press < `mashResetSeconds` 則 `_parryScale = max(minMashScale, _parryScale - mashShrinkPerTap)`，然後 `_lastPressTime = _guardStartTime = Time.time`。死亡/僵直清 `_guardStartTime`（**放開不清**）。

### 7.2 兩條命中路徑

**路徑 1（clash）— boss 掃掠刀第一個穿過玩家 GuardVolume：**
`BossHitbox.SweepCheck` → `TryResolveBladeClash` → `PlayerGuard.TryResolveClash(in BladeClashInfo)`：
```
frontal = IsFrontalBlock(player.forward, info.AttackDirectionFlat, guardArcDegrees)
result  = BladeClashUtility.Classify(frontal, InParryWindow, IsBlocking || InTapGuardWindow)
         // Classify: 非正面→None；在彈反窗口→Parried；按住/tap窗口→Guarded；否則→None
None     → return（讓身體命中照常結算）
冷卻未到 → return result（算已處理，不再產生回饋）
Parried  → restoreScaleOnParry ? _parryScale=1
         + stance.AddPostureDamage(parryPlayerPoiseDamage=0)
         + bossStance.AddPostureDamage(parryBossPoiseDamage=14)
         + BossStateMachine.NotifyParried()   ← 觸發 boss recoil（HitReaction 路徑，不擊飛）
         + HitStopController.Request(0.1, 0.15) + cameraShake.Shake(0.06, 0.16)
         + Parried?.Invoke(contactPoint)
Guarded  → guardChipDamage(0) + stance.AddPostureDamage(GuardPoiseGain(info.PoiseDamage, guardPoiseMultiplier=1, fallback=6))
         + HitStopController.Request(0.05, 0.4) + shake(0,0.1)
         + Guarded?.Invoke(contactPoint)
```
可 clash 的部位（`IsClashablePart`）：`Weapon, LeftHand, RightHand, LeftFoot, RightFoot`。**踢擊也能彈反**（使用者要求）。`Body`、`LandingAOE` 不可 clash。

**spec C（繞過防禦）：** `TryResolveBladeClash` 掃 sweep buffer，找最近的 active `PlayerGuardVolume`（依 `RaycastHit.distance`）與最近的 body（`IDamageable`）。若 `bodyDist + 0.05 < volumeDist` → 刀在 guard 之前先碰到身體 → return false → 走身體命中（全額傷害）。平手時 guard 贏（防守有利）。

**路徑 2（非 clash）— 其他打到玩家 Health 的東西（或繞過 guard 到身體的刀）：**
`PlayerGuard.ModifyIncoming(DamageInfo)`：非 `IsBlocking` 或非正面 → 原樣通過；`WasBossWeaponStrike(incoming)`（source 底下有 active 且 `ActiveWindowPart==Weapon` 的 BossHitbox）→ **原樣通過（刀刃到身體 = 全額，spec C）**；否則 → 軟格擋血量 ×0.15 + 觸發 `Blocked` + 架勢 = `GuardPoiseGain(incoming.ExplicitPoiseAmount ?? Amount×0.2, guardPoiseMultiplier, fallback)`（追加94 續 11：踢擊用該招 poise，例 SpartanKick 14）。

### 7.3 `PlayerGuardVolume`（`Combat/PlayerGuardVolume.cs`）
`[RequireComponent(CapsuleCollider)] [DefaultExecutionOrder(-40)]`（在 BossHitbox 掃掠前先擺好位置）

**不是刀身形狀**——是一個**以玩家為錨、朝正前上方張開、略偏持刀手**的「防禦覆蓋膠囊」。原因（見 §11）：4× 武士的刀從 y≈1.5–3.5 的柱狀空間往下砍，而 Meshy 攻擊 clip「起點在 root 後方約 3 units」，刀身級細碰撞完全接不到。

| 欄位 | 場景值 | 意義 |
| --- | --- | --- |
| `nearHeight` | 0.9 | 近端離玩家腳的高度（夠低擋貼身/踢擊） |
| `backReach` | 0.35 | 近端在玩家胸口後方多少（擋貼身攻擊） |
| `reach` | 1.5 | 向前延伸（世界公尺） |
| `farHeight` | 3.4 | 遠端升到多高（夠高擋 4× boss） |
| `handLean` | 0.35 | 朝持刀手偏多少 |
| `radius` | 0.45 | 膠囊半徑 |
| `rotateWeapon` / `bladeRise` / `poseBlendSpeed` | true / 1.1 / 12 | 防禦時把可見武士刀轉成刀尖朝前上（純外觀） |
| `drawGizmo` | true | |

`ShouldBeActive => guard != null && (guard.IsBlocking || guard.InTapGuardWindow)` → 控制 CapsuleCollider `enabled`。
`RecomputeAndPlace()`（FixedUpdate + LateUpdate 都呼叫）：從玩家位置沿 forward ± 建 `BladeRoot`（`basePos.y + nearHeight`）/ `BladeTip`（`+ farHeight`），朝 `weaponMount` 偏 `handLean`，把膠囊沿該線段擺好（`direction=2`, `LookRotation`）。
公開：`BladeRoot`, `BladeTip`（= 規格書的刀根/刀尖，供 boss sweep + debug）、`Radius`、`Active`。

### 7.4 `BladeClash.cs`（`Combat/BladeClash.cs`）——共用詞彙（無 MonoBehaviour，純邏輯 + 單元可測）
```csharp
enum BladeClashResult { None, Guarded, Parried }
readonly struct BladeClashInfo { GameObject Attacker; float HealthDamage; float PoiseDamage;
                                 Vector3 ContactPoint; Vector3 AttackDirectionFlat; }
interface IBladeClashReceiver { BladeClashResult TryResolveClash(in BladeClashInfo info); }
static class BladeClashUtility {
  Classify(bool isFrontal, bool withinParryWindow, bool guardHeld)     // 見 §7.2
  WithinParryWindow(float now, float guardStartTime, float dur)         // elapsed ∈ [0, dur]
  ClashCooldownElapsed(float now, float lastClashTime, float cd)
}
```

### 7.5 回饋 / debug
- `PlayerClashFeedback`：訂閱 `Guarded`/`Parried`（帶接觸點）。冷卻 0.1s；把對應 `ParticleSystem`（`GuardSparks`/`ParrySparks`，world sim）移到接觸點 `Play(true)`；`AudioSource` 移到接觸點、隨機 pitch（guard 0.90–1.02 / parry 1.03–1.14）、`PlayOneShot`。兩個 clip 都接 `KatanaClash.mp3`。
- `SekiroDeflectDebug`：F9 開關。記錄 `_lastOutcome`（Parry/Guard/Hit）。`OnDrawGizmos`：GuardVolume `BladeRoot→BladeTip` 線 + wire sphere + 正面錐；每個 active `BossHitbox` 的 `LastSweepFrom→LastSweepTo`（紅線）+ 最後命中球。`OnGUI`：`[Sekiro] Defense: {state}  ParryWin: {ms}ms (x{scale})  Last: {outcome} ({age}s ago)  (F9)`（scale < 0.95 / 0.5 轉黃 / 紅）。
- 三個 layer（`TagManager.asset`）：slot 3 `PlayerHurtbox`、slot 6 `PlayerGuardWeapon`、slot 7 `BossWeapon`。

### 7.6 anti-mash 驗證（session 內實測）
- 間隔開 → 200ms 窗口
- 連按 ×4 → 200 / 120 / 45 / 0 ms
- 成功彈反 → 立即恢復 1.0（→ 200ms）

---

## 8. `HitStopController` / `CameraShake` / `KnockbackReceiver`

- `HitStopController.Request(seconds, timeScale)`：靜態，把 `Time.timeScale` 壓低一小段再還原。**已知風險**：Editor 失焦時 Play Mode 幀凍結，`HitStop` 可能把 `timeScale` 卡在 0.05 不還原（見記憶 [[unity-play-mode-frozen-when-unfocused]]）。
- `CameraShake.Shake(amplitude, seconds)`：掛在主攝影機。`PlayerGuard` 場景中未指定 → Awake 抓 `Camera.main`。
- `KnockbackReceiver`：`launchUpwardSpeed 7`、`instantDisplacementFraction 0.15`；boss 攻擊的 `knockbackForce` 走這裡（`launchesTarget` 皆 0，只有水平推）。

### 8.1 玩家戰鬥音效
| 元件 | 觸發 | clip | 位置 |
| --- | --- | --- | --- |
| `PlayerAttackSfx`（`Player/AttackSfx`） | `PlayerCombat.Hit`（每個左鍵 combo 段落，揮空也播） | `KatanaSwing.mp3`（0.76s，`9月1日.mp3`） | 3D，blade 高度，pitch 0.92–1.10 |
| `PlayerClashFeedback`（`Player/ClashFeedback`） | `PlayerGuard.Guarded` / `Parried` | `KatanaClash.mp3`（1.23s） | 刀刃接觸點，guard pitch 0.90–1.02 / parry 1.03–1.14 |
| `PlayerGuardClashSfx`（腳本存在、目前場景未用） | `PlayerGuard.Blocked`（僅 boss 刀刃 window） | `KatanaClash.mp3` | — |

> 歷史：追加85 `KatanaClash.mp3` 原本是**左鍵每段**攻擊聲（`PlayerMeleeSfx`）；2026-09-01 使用者要求移到右鍵防禦（刀刃對刀刃），左鍵改用 `KatanaSwing.mp3`。

---

## 9. `BossStateMachine` — 狀態與優先序

`enum BossState`（`AI/Boss/BossState.cs`）：`Dormant, Alert, Idle, Approach, GateWatch, ReturnHome, Attack, DodgeCounter, Breakdance, LeapSlamWindup, LeapSlam, UltimateReposition, UltimatePrepare, UltimateAttack, Vanishing, DiveAttack, HitReaction, PostureBroken, Victory, Dead, GettingUp`

### 9.1 每幀優先序 cascade（`Update()`，`else if` 串接）
```
1. (Dead/GettingUp/Vanishing 等終端/腳本 beat 自己處理)
2. Victory                          — 終端，什麼都不能搶
3. TryEnterPostureBroken()          — 架勢滿 → 僵直（最高優先）
4. TryEnterHitReaction()            — 受擊硬直 / NotifyParried() 的 recoil
5. TryLeashReset()                  — 距離脫離 / GateWatch → ReturnHome
6. TryContinuePhaseTransitionVisual()
7. TryContinueCommittedSpecialAttack()
8. TryEnterUltimate()
9. TryEnterUltimateReposition()
10. TryEnterVanish()                — vanishTriggerSeconds=999999 → 實質關閉
11. TryEnterDodgeCounter()
12. TryEnterBreakdance()            — 每 15s 戰鬥時間
13. TryEnterLeapSlam()              — 每 20s 戰鬥時間（先 LeapSlamWindup 1s）
14. TryEnterPeriodicSlam()          — 每 30s → OverheadSlam（追加94）
15. TryEnterTooCloseKick()          — 貼身 1.6m 2s → SpartanKick
（都沒有 → 正常 Approach / Attack 流程）
```

### 9.2 一般攻擊選擇 `PickAttack` / `PickAttackFiltered`
從 `normalAttackPool` 依「距離 ∈ [MinDistance, MaxDistance] + 角度 ≤ MaxAngleDegrees + 冷卻已過 + 未達 MaxConsecutiveUses + Phase 權重 > 0」篩選，再乘上輪替 LRU 軟偏壓（`AttackRotationRecoverySeconds` 內用過 → 權重壓到 `AttackRotationRecentFactor` 再線性恢復），加權隨機。
`BeginAttack(attack)`：設 `_currentAttack`、開冷卻、若已在 Attack 先彈回 Idle 一幀、`ChangeState(Attack)`。

### 9.3 攻擊執行 `UpdateAttack` → `UpdateHitWindows(attack, normalized)`
- `normalized = AnimatorNormalizedTime()`（boss clip 自己的 normalized time，從 Attack state 進入起算；state 用 `CrossFadeInFixedTime(clipStateName, 0.08f)`）。
- **`BossAttackDefinition.startupSeconds` / `recoverySeconds` 在 `BossStateMachine` 中沒有被消費**——純描述性 metadata。真正的命中時機只由 `hitWindows` 的 normalized 區間 + clip 長度 + Animator state speed 決定。
- 追蹤：`normalized < TrackingDropNormalizedTime` 時用 `StartupTracking`，之後用 `LateTracking`；接近 phase 內若 `HorizontalDistance > MaxDistance*0.85` 允許小幅 creep（`WalkSpeed*0.5`）把後退的玩家拉回。
- `UpdateHitWindows`：對每個實體 hitbox 聚合「這一幀有沒有任何 window 宣告它 active」，再一次性 `Activate`/`Deactivate`（修正兩個 window 共用同一 hitbox 時互相關閉的 bug）。

### 9.4 週期性技能計時器（獨立於一般 pool）
| 技能 | 計時來源 | 觸發旗標 | 進入函式 |
| --- | --- | --- | --- |
| Breakdance | `_breakdanceTimeAccumulated` ≥ `tuning.BreakdanceTriggerSeconds`(15) | `_breakdancePending` | `TryEnterBreakdance()` |
| LeapSlam | `_leapSlamTimeAccumulated` ≥ `tuning.LeapSlamTriggerSeconds`(20) | `_leapSlamPending` | `TryEnterLeapSlam()` → `LeapSlamWindup`(1s) → `LeapSlam` |
| **PeriodicSlam** | `_periodicSlamTimeAccumulated` ≥ `periodicSlamIntervalSeconds`(30，序列化欄位) | `_periodicSlamPending` | `TryEnterPeriodicSlam()` → `BeginAttack(periodicSlamAttack = Wushi_Attack_OverheadSlam)` |

三個計時器都在 `Disengage` / `ResetVanishCycle` 附近 / `CancelAllPending()` 歸零。

### 9.5 對外整合 API
- `ForceEngage()`：Dormant/ReturnHome/GateWatch → `_hasEngaged=true; ChangeState(Alert)`。開場過場用。
- `NotifyParried()` / `NotifyParried(DeflectReaction)`（追加94 續 12）：玩家彈反時 `PlayerGuard` 傳入該 hit-window 的 `deflectReaction`：
  - `Recoil`（預設、= 所有未設定的 window）→ `_forcedHitReactionPending = true`（重用 HitReaction recoil，不擊飛）。
  - `ContinueCombo` → 不動 FSM，這招後續 window 照播（架勢傷害仍由 `PlayerGuard` 加；爆架勢時 `TryEnterPostureBroken()` 下一幀接手）。
  - `CancelAttack` → `CancelAttackInProgress()` + Attack→Idle。
  - 目前只有 `Wushi_Attack_SwordJudgment` / `Wushi_Attack_DoubleCombo` 的**第 1 window** 設為 ContinueCombo。
- `RequestBeHitFlyUp()` / `NotifyPlayerDied()` 等既有 API。

---

## 10. 武士每招 hit-window / 傷害 / 反應時間表

> `normalAttackPool` = **[SwordJudgment, SpartanKick, DoubleCombo, ChargeCut]**（OverheadSlam 追加94 已移出 pool，改為 periodic）。
> `tooCloseAttack` = SpartanKick。`leapSlamAttack` = Wushi_Attack_LeapSlam。`periodicSlamAttack` = Wushi_Attack_OverheadSlam @ 30s。

**反應時間換算（近似）：**
`t_接觸 ≈ crossfade(0.08s) + startNormalized × clipLength ÷ animatorStateSpeed`
`有效幀持續 = (endNormalized − startNormalized) × clipLength ÷ animatorStateSpeed`
「有效幀持續」= **傷害時間窗**（`BossHitbox` active 的時間），不是整段揮刀視覺時間。同一 window 對玩家只結算 1 次（§5.3）。

| 招式 | 大招 | clip 長 | state speed | hitWindow(s) (normalized) | 部位 | ≈t_接觸 / 窗口長 | 血量傷害 | 架勢傷害 | knockback | maxDist / angle | 冷卻 | 可彈反 | measured |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| **SwordJudgment** | ✔ | 3.30s | 1.00 | `0.175–0.225`；`0.61–0.66` | Weapon | ≈0.66s / 165ms；≈2.09s / 165ms | 32（固定） | 22 | 4 | 3.5 / 65° | 1.0s | ✔ | ✔✔ |
| **DoubleCombo** | ✗ | 2.833s | 1.40 | `0.24–0.32`；`0.61–0.68` | Weapon | ≈0.57s / 116ms；≈1.31s / 101ms | 5%×2（各擊 5%×max = 25） | 12 | 3 | 2.5 / 60° | 1.0s | ✔ | ✔✔ |
| **ChargeCut** | ✗ | 2.033s | 1.15 | `0.21–0.28` | Weapon | ≈0.45s / 124ms | 5%（25） | 12 | 3.5 | 3.0 / 60° | 1.0s | ✔ | ✔ |
| **SpartanKick** | ✗ | 1.267s | 1.40 | `0.58–0.75` | **RightFoot** | ≈0.61s / 154ms | 5%（25） | 14 | 4.5 | 1.7 / 55° | 0.5s | ✔（腳可 clash） | ✗（placeholder timing） |
| **OverheadSlam**（periodic 30s） | ✔ | 2.533s | 1.40 | `0.56–0.64` | Weapon | ≈0.99s / 145ms | 28（固定） | 22 | 4.5 | 3.2 / 60° | 1.1s | ✔ | ✔✔ |
| LeapSlam（每 20s，非 pool） | — | 3.033s | 1.00 | 整段下落（wide window） | RightFoot + LandingAOE(r3) | 落地前後 | — | — | — | — | — | 腳可 clash / AOE 不可 | — |

> **彈反反應**（追加94 續 12，`hitWindow.deflectReaction`）：SwordJudgment / DoubleCombo 的**第 1 window = ContinueCombo**（彈第一刀不中斷連段，第二刀照發），第 2 window + 其餘所有招 = **Recoil**（彈反 → Boss 短後震）。爆架勢一律 PostureBroken 優先。
> **SwordJudgment** 有 `derivedAttack`（衍生追擊）wired，deriveWindow 0.7–0.95，Phase1/2 機率 0.6 / 0.85，deriveCooldown 3s。
> SwordJudgment / OverheadSlam 的 clip 長度取自原始 FBX（"Scene" take），實際 controller 可能有 sub-clip 裁切，秒數為近似。
> 每招 designNotes（資產內）記錄了完整離線量測歷程——都用 `AnimationMode.SampleAnimationClip` 追 `BladeHitbox` 相對 root/hips 的位置與速度，把 window 對準「刀真的在玩家高度且在動」的相位。追加94 把所有 window 收緊到 0.10–0.20s 並修掉「刀還沒落下玩家就受傷/被擊退」的舊 bug（舊 window 開在刀還在頭頂/身後時）。

### 10.1 Wushi.controller 各 state speed
| state | speed |
| --- | --- |
| Wushi_SwordJudgment | 1.0（追加94 從 1.35 降） |
| Wushi_ChargeCut | 1.15（追加94 從 1.3 降；使用者要求慢一點好反應） |
| Wushi_SpartanKick / Wushi_DoubleCombo / Wushi_OverheadSlam | 1.4 |
| Wushi_LeapSlam / Wushi_PostureKneel / Wushi_DeathFallForward | 1.0 |
| Wushi_ContinuousThrust | 1.25（追加89 停用，clip 還在 disk） |

`Wushi.controller` 沒有 AnyState transition；每個 state 進場用 `animator.CrossFadeInFixedTime(clipStateName, 0.08f, 0, 0f)`。

---

## 11. 已知限制 / 硬約束（分析時務必納入）

1. **刀身級精確 Guard Collider 目前做不到。** 已量測確認：4× placeholder 武士的刀在 y≈1.5–3.5 柱狀空間揮動，且 Meshy 攻擊 clip「起點在 root 後方約 3 units、邊走邊揮」，配 `applyRootMotion=false` → 可見刀身落後 boss transform，攻擊即使對身體也只是勉強連上。所以 `PlayerGuardVolume` 只能是寬鬆覆蓋膠囊 + 純外觀的武器朝向旋轉。真正刀身級防禦需要：正常尺寸 boss + 對齊玩家高度重新製作的攻擊 clip。
2. **Meshy clip root drift。** `AnimationMode.SampleAnimationClip` 在 Meshy FBX 上會漂移 rig（root motion / hip 位移烤進骨骼），離線 X/Z 量測不可靠。可靠技巧：量 `bladeHb.position - hipsBone.position`（blade-rel-Hips，root-drift-invariant）。
3. **武士 `applyRootMotion = false`。** 有位移的招（DoubleCombo 全連段、ChargeCut lunge、ContinuousThrust）都無法真正移動 boss，第二段/衝刺段的刀會落在靜止 boss 前方 6–8m，只打得到 melee 範圍外的人。要用完整位移需 `useRootMotion=1` + 「LeapSlam/Breakdance 打斷攻擊時清 `_currentAttack`」的 root-motion trap fix（CHANGELOG 已標記）。
4. **Editor 失焦時 Play Mode 幀凍結。** `Time.deltaTime/time/frameCount` 不前進、`FixedUpdate/Update` callback 不跑、`Time.timeScale` 可能被 HitStop 卡在 0.05。Boss FSM 攻擊計時器永不觸發。測試需用反射直接呼叫 `bsm.Update()` / `BossHitbox.FixedUpdate()` / `UpdateHitWindows(attack, nt)` 並手動 `SampleAnimationClip` 到每個 nt。結論「trigger/collision 壞了」前先檢查 `Time.frameCount`。
5. **`NewAnimator.controller` 三方共用**（Player + 中立者1 + 守望者）。改任何既有 state 會同時影響三者。玩家專屬新增只能加新 state + 新 trigger（Guard/GuardParry/ExecuteThrust 都是這樣加的）。
6. **StancePoise 場景值 ≠ 腳本預設 ≠ tuning 值**（見 §12）——手調權威，不要「修正」。
7. `vanishTriggerSeconds = 999999` → 武士的消失/瞬移 cycle 實質關閉（分析 boss 節奏時忽略 Vanishing/DiveAttack）。
8. 儲存 `GreyboxTest.unity` 會重新序列化約 25k 行內嵌 Live2D mesh（既有 churn，非本次改動）。

---

## 12. 「手調值是權威」清單（不要改回程式碼預設）

| 位置 | 場景/資產值 | 腳本/其他預設 |
| --- | --- | --- |
| `CharacterController.stepOffset`（玩家） | **0**（刻意，真 bug fix；用平滑斜坡 + slopeLimit 取代爬階） | — |
| `ThirdPersonCameraController.distance` / `targetOffset` | 使用者反覆 play-test 手調 | 註解可能過時 |
| `StancePoise`（玩家） | maxStance 100 / regen 20 / gainMult 0.5 / **staggerDuration 1.2**（guard-break） | 腳本 60 / — / 0.2 / 6 |
| `StancePoise`（武士） | maxStance 60 / regenDelay 3 / regen 8 | 腳本 60 / 1.5 / 20 |
| `Wushi_Tuning.postureRegenDelay/PerSecond` | 2 / 5（FSM 內部路徑） | 與 StancePoise 元件值不同，兩套並存 |
| `PlayerGuard.*` | §7.1 全表（追加94 由 `PlayerDeflectSetup` menu 寫入 + 使用者微調） | 腳本預設多半不同（parryWindowDuration 腳本也是 0.2） |

---

## 13. 相關檔案索引

| 檔案 | 內容 |
| --- | --- |
| `Assets/_Project/Game/Combat/BladeClash.cs` | 彈反共用詞彙（enum / struct / interface / util） |
| `Assets/_Project/Game/Combat/PlayerGuard.cs` | 玩家防禦/彈反核心（`IIncomingDamageModifier` + `IBladeClashReceiver`） |
| `Assets/_Project/Game/Combat/PlayerGuardVolume.cs` | 防禦覆蓋膠囊 |
| `Assets/_Project/Game/Combat/PlayerGuardUtility.cs` | 正面判定 / 減傷 / pose 純函式 |
| `Assets/_Project/Game/Combat/PlayerClashFeedback.cs` | 火花 + 金屬音效 |
| `Assets/_Project/Game/Combat/SekiroDeflectDebug.cs` | F9 debug overlay |
| `Assets/_Project/Game/Combat/PlayerGuardAnimatorLink.cs` | IsGuarding / GuardParry 參數橋接 |
| `Assets/_Project/Game/Combat/PlayerGuardVisualizer.cs` | 藍色扇形防禦提示（追加93） |
| `Assets/_Project/Game/Combat/StancePoise.cs` | 架勢條 |
| `Assets/_Project/Game/Combat/Boss/BossHitbox.cs` | 掃掠判定 + clash routing + 一次性去重 |
| `Assets/_Project/Game/Combat/Boss/BossAttackDefinition.cs` | 攻擊 ScriptableObject 型別 |
| `Assets/_Project/Game/AI/Boss/BossStateMachine.cs` | Boss FSM（21 state、cascade、攻擊選擇、週期計時器） |
| `Assets/_Project/Game/AI/Boss/BossState.cs` | BossState enum |
| `Assets/_Project/Game/Core/DamageInfo.cs` / `Health.cs` / `IDamageable.cs` / `IIncomingDamageModifier.cs` / `HurtboxLink.cs` | 傷害管線 |
| `Assets/_Project/Settings/Combat/Boss/Wushi_Tuning.asset` | Boss 調校 |
| `Assets/_Project/Settings/Combat/Boss/Wushi_Attack_*.asset` | 每招數據（SwordJudgment / DoubleCombo / ChargeCut / SpartanKick / OverheadSlam / LeapSlam / ContinuousThrust） |
| `Assets/Editor/Bootstrap/PlayerDeflectSetup.cs` | menu「Tools/Live2DAction/Wire Sekiro Deflect Into GreyboxTest」（可重跑，寫入所有彈反 wiring + 數值） |
| `Assets/_Project/Characters/Placeholder/CombatAnimations/Meshy/Guard.fbx` | 防禦動畫（Meshy，可商用） |
| `Docs/CAT_COMBAT_DESIGN.md` | 貓咪近戰設計（切片 2，另一條線） |
| `Docs/BOSS_INTRO_EXPLORATION.md` §9 | 武士開場過場 |
| `Docs/KNOWN_ISSUES.md` / `Docs/CHANGELOG.md` | 已知問題 / 變更紀錄（追加92–94） |

---

## 14. 待辦（尚未完成，分析時可視為「設計意圖但未實作」）

- ChargeCut 蓄力預兆 VFX / 蓄力音效 / 刀身發光（使用者要求）。
- 對焦 Editor 的 Play + F9 實測彈反迴圈：確認每個 window 期間紅色 sweep 是否貼著玩家；SwordJudgment 首擊 ≈0.66s 是否偏慢（偏慢則 state speed 設 1.1）。
- DoubleCombo 是否放慢（目前 state speed 1.4，待使用者確認）。
- 一般格擋「被震退」反應 clip（spec B，需另一個動畫）。
- 玩家武士刀真正的 swept **攻擊** collider（目前玩家攻擊仍是 `PlayerCombat` 的球形範圍檢查，不是掃掠）。
- 武士 ContinuousThrust 帶 root motion 重新加入（assets 還在 disk）。
- 武士刀 / 玩家近戰動畫 retarget（`TC_Sword_Free_Pack`）+ Sekiro 式對決。
