> **來源**：使用者於 2026-09-01 提供的外部 AI 分析輸出（`Wushi_Combat_System_Engineering_Spec_v1.0.docx`，以 `Docs/COMBAT_SYSTEM_SNAPSHOT.md` 為輸入產出）。以 pandoc 轉為 Markdown 歸檔。逐句規格內容從第 5 節「文件控制與權威規則」開始，未改動；下方「實作進度」是本專案的落地追蹤。

---

## 實作進度（2026-09-02，對應 CHANGELOG「追加94 續 34」）

| # | 工程包 | 里程碑 | 狀態 | CHANGELOG |
|---|---|---|---|---|
| **1** | 彈反反應類型 | M1 | ✅ 程式完成（`DeflectReaction` 每個 hit-window；SwordJudgment/DoubleCombo 窗1 = ContinueCombo） | 續 12 |
| **2** | Tap Guard 一致性 | M1 | ✅ 程式完成。**Part C 未做**：上半身 AvatarMask layer + 專屬 `GuardImpact`/`ParryImpact` clip（需動畫製作，目前彈反閃借用 `GuardParry` state） | 續 13 |
| **3** | Boss 旋轉 Sweep | M2 | ✅ 程式完成 + 已接線（武士刀 `BladeHitbox` root/mid/tip 多點掃掠） | 續 17 |
| **4** | 玩家武器 Sweep | M2 | ⛔ **退回**：續 23 實機 Play 完全無傷害；`PlayerWeaponHitbox`/`WeaponSweepUtility`/選單/測試全留磁碟，`useSweptBladeHitbox=false`。重做需對焦 Editor 陪同逐步 Play debug | 續 15-16、21、23 |
| **5A** | 程式化攻擊位移 | M3 | ✅ 程式完成 + `Wushi_Attack_DoubleCombo` 已套 `attackMotion` lunge | 續 18、26 |
| **5B** | 比例／骨架正規化 | M3 | ✅ **「做法 A」完成**：武士 gameplay root `localScale` 4→1，可見模型／骨架／刀／所有骨綁 hitbox 世界幾何逐項驗證完全保留（`WushiRootScaleSetup.cs`）。**做法 B**（縮小可見武士 + 依玩家高度重做每招 clip + 拆共用 `NewAnimator`）留給未來一份完整武士副本 | 續 33 |
| **5C** | 精確 Guard Collider | M3 | ⏸ **使用者決定跳過**（2026-09-02），等做法 B。目前仍是玩家錨定的守備範圍膠囊（proxy） | — |
| **6** | 格擋架勢計算 | M1 | ✅ 程式完成（一般格擋架勢 = 每招 `PoiseDamage`，非固定 6） | 續 11 |
| **7** | 處決 + 生命節點 | M4 | ❌ **使用者決定不採用**（2026-09-03 續 99）—— 處決要「一律扣當前生命 50%」，但節點路線第一次處決會 `ResetHealth()` 回滿血。已從武士移除 `BossLifeNodeController` 元件（`.cs` + `ExecutionNodeLogic` + 測試留著）。現在武士/屁孩王/Enemy 處決一致：`health.CurrentHealth × 0.5` + `EndStagger`。武士血歸零仍 5s 復活（`permanentDeath` 未動）。 | 續 19、~~99~~ |
| **8** | 特殊招式排程 + 架勢權威 | M4 | ✅ 程式完成（§9.2 共享特殊冷卻武士 7s；§9.3 `StancePoise` 已是單一架勢權威；「重複 UltimateEnergy」為規格對本專案的誤判——ult 表與飛行耐力是兩個不同用途） | 續 20 |
| **9** | 最終數值調校 | M5 | 🔧 groundwork 完成：§10.2 戰鬥數據儀表（`SekiroDeflectDebug` F9 session tally）+ §10.4 出招真實時序報表（`BossAttackTimingReport` 選單 + `BossAttackTimingUtility`）。§10.3 pass #1–2 進行中（步驟 16 減速 OverheadSlam 1.05 / SpartanKick 1.0 / SwordJudgment 0.9；步驟 19 起手 重擊 SwordJudgment 42 / OverheadSlam 40，輕擊維持 25）。ParryRate 38%→67%。續 38–45：pool = [SwordJudgment, CrossSlash]；BladeHitbox 加大；武士固定 ~3.5m 圈外原地出招；CrossSlash（yaw -15、命中窗提前、speed 1.15）；只有 SpartanKick 擊退；全命中窗 ContinueCombo（彈反不打斷，武士無 flinch clip）；`attackRecoveryTailCutNormalized` 回 2（招式完整播完再接）；硬直倒地改 `Wushi_PostureFall`；新 `cancelClipBodyDrift` 治 Meshy clip 烤入位移通病；pool = [SwordJudgment, CrossSlash, ThrustStab, TwistCleave]（ScissorTakedown/SlideRoll 匯入未上場）。**待使用者 Play 驗收 → 步驟 20** | 續 32、34–47 |

**里程碑總結**：M1 完成、M2（3 完成 / 4 退回）、M3（5A+5B 完成 / 5C 跳過）、M4 完成、M5 groundwork 完成。EditMode **288/288** 綠。

**所有「程式完成」項目的 GreyboxTest Play 驗收未跑**——本機 MCP PlayMode runner 不可靠（見 `Docs/AGENT_NOTES.md`），PlayMode 測試需使用者從 Test Runner 視窗跑，戰鬥手感需使用者對焦 Editor Play。每項的 feature flag / 回退選單見對應 CHANGELOG 條目。

**剩餘要使用者參與的兩件事**：
1. **項目 4**（玩家揮刀 swept collider）——盲改壞過兩次，需開對焦 Editor 陪同逐步 Play debug。
2. **項目 9 §10.3 調校 pass**——需 Play 幾場、F9 看 ParryRate／削爆間隔／雙方掉血，再依 §10.3 順序調（Boss 前搖／state speed → hit window 位置 → 玩家架勢壓力 → Boss 傷害 → 最後才 A/B 測 0.20s 彈反窗）。

---

**ENGINEERING SPECIFICATION**

**武士 Boss 戰鬥系統\
九項核心改造工程說明書**

*Unity 3D Action RPG / Sekiro-style Guard, Deflect, Weapon Sweep and
Boss Lifecycle*

**文件版本：**v1.0

**建立日期：**2026-09-01

**基準場景：**Assets/\_Project/Scenes/GreyboxTest.unity

**基準狀態：**COMBAT_SYSTEM_SNAPSHOT.md / CHANGELOG 追加94 續8之後

**主要讀者：**專案開發者、Claude Unity MCP、戰鬥設計與測試人員

**文件目的：**將9項改造轉為可排程、可實作、可驗收的工程規格

> **核心結論　**本文件不要求一次重寫整套戰鬥系統。改造以既有
> DamageInfo、Health、StancePoise、BossHitbox 與 BossStateMachine
> 為基礎，依相依性分階段替換，並保留已經實測的場景序列化值。

# **文件控制與權威規則**

- 場景與 ScriptableObject
  的序列化值高於腳本預設值；不得因預設不同而自動覆寫手調參數。

- 改造過程應維持 GreyboxTest 可隨時進入 Play
  Mode；每一階段都要有可回退的 Feature Flag 或相容路徑。

- 0.20秒彈反窗口在第9項調校前視為鎖定基準，不在前期邊改架構邊調整。

- 目前大型 GuardVolume 是4倍比例 Boss
  與漂移動畫的暫時代理判定；第5項完成前不得誤稱為精確刀身碰撞。

# **1. 文件範圍與目標架構**

本規格涵蓋玩家與武士 Boss
之間的攻擊、格擋、完美彈反、架勢、武器掃掠、攻擊位移、處決、生命節點、特殊招式排程與最終數值調校。它不包含新角色美術製作、最終音效資產採購、網路同步或多人權威伺服器設計。

## **1.1 目標戰鬥資料流**

Input / Boss AI\
-\> Animator State + Hit Window\
-\> Weapon Sweep Sample (Root / Mid / Tip)\
-\> Combat Contact Resolver\
-\> Parry / Guard / Body Hit / Miss\
-\> Health + StancePoise\
-\> DeflectReaction / HitReaction / PostureBroken\
-\> VFX + SFX + HitStop + Camera Feedback

所有攻擊必須先形成一致的接觸資料，再決定防禦結果。不得再由 PlayerGuard
透過搜尋 Source 底下是否存在啟用中的 BossHitbox 來猜測攻擊種類。

## **1.2 建議共用接觸資料**

public readonly struct CombatHitContext\
{\
public int AttackInstanceId;\
public GameObject Attacker;\
public GameObject Defender;\
public BossHitboxPart HitPart;\
public float HealthDamage;\
public float PoiseDamage;\
public Vector3 ContactPoint;\
public Vector3 ContactNormal;\
public Vector3 AttackDirectionFlat;\
public bool CanGuard;\
public bool CanParry;\
public DeflectReaction DeflectReaction;\
}

> **相容策略　**第一階段可先擴充 BladeClashInfo，不必立即全面替換
> DamageInfo；當 Boss 與玩家 Sweep 共用 Resolver 後，再逐步收斂為
> CombatHitContext。

## **1.3 九項工程總覽**

  ----------------------------------------------------------------------------------------------
   **\#**  **工程包**           **結果**                             **里程碑**   **相對點數**
  -------- -------------------- ------------------------------------ ------------ --------------
   **1**   彈反反應類型         防止每次彈反都中止Boss連段           M1           5

   **2**   Tap Guard一致性      同步輸入、Collider、Animator與移動   M1           5

   **3**   Boss旋轉Sweep        刀根／中段／刀尖的高速掃掠           M2           8

   **4**   玩家武器Sweep        取代Root周圍球形攻擊判定             M2           8

   **5**   Boss空間與位移       比例、Clip空間、位移與精確Guard      M3           13

   **6**   格擋架勢計算         使用每招PoiseDamage                  M1           3

   **7**   處決與生命節點       Deathblow、Phase與永久死亡           M4           8

   **8**   特殊招式與架勢權威   統一排程及單一資料來源               M4           8

   **9**   最終數值調校         速度、傷害、架勢與0.2秒窗口          M5           5
  ----------------------------------------------------------------------------------------------

> **估算說明　**相對點數只表示複雜度，不等於工時。總量為63點；第5項最大，必須拆成暫時程式位移與正式資產空間兩段。

# **2. 工程項目1：彈反反應與 Boss 連段控制**

## **2.1 問題與目標**

目前每次成功彈反都會呼叫 BossStateMachine.NotifyParried()，設定強制
HitReaction。由於 HitReaction 在狀態優先序中高於一般
Attack，DoubleCombo、SwordJudgment
等多段招式可能在第一擊被彈反後直接終止。

> **設計決策　**彈反永遠造成架勢傷害與回饋，但是否中止招式，必須由每個
> Hit Window 自己決定。

## **2.2 資料結構**

public enum DeflectReaction\
{\
ContinueCombo, // 保留Attack狀態，不取消後續Hit Window\
Recoil, // 中止目前攻擊並進入短HitReaction\
CancelAttack // 強制取消，回到安全恢復狀態\
}\
\
\[Serializable\]\
public sealed class BossHitWindow\
{\
public float startNormalized;\
public float endNormalized;\
public BossHitboxPart part;\
public DeflectReaction deflectReaction;\
}

PostureBroken 不屬於 DeflectReaction 的第四種選項。當 Boss
架勢達上限時，PostureBroken 仍依 FSM 現有最高優先級處理，覆蓋
ContinueCombo、Recoil 與 CancelAttack。

## **2.3 初始配置**

  ------------------------------------------------------------------------
  **招式**            **第一命中窗**        **後續／特殊規則**
  ------------------- --------------------- ------------------------------
  **SwordJudgment**   第1窗 ContinueCombo   第2窗 Recoil

  **DoubleCombo**     第1窗 ContinueCombo   第2窗 Recoil

  **ChargeCut**       Recoil                ---

  **SpartanKick**     Recoil                ---

  **OverheadSlam**    Recoil                ---

  **LeapSlam腳部**    ContinueCombo         落地AOE不可彈反
  ------------------------------------------------------------------------

## **2.4 實作步驟**

1.  在 BossHitWindow 加入 deflectReaction，既有資產遷移時預設使用
    Recoil，以保持舊行為。

2.  將 PlayerGuard.TryResolveClash 的 Parried 分支改為傳回完整結果，不在
    PlayerGuard 直接無條件呼叫 NotifyParried。

3.  由 BossHitbox／Combat Contact Resolver 將命中窗的 DeflectReaction
    傳給 BossStateMachine。

4.  ContinueCombo 僅播放 HitStop、火花、音效與姿勢傷害，不切換
    FSM；Recoil 才呼叫短 HitReaction。

5.  若同幀彈反使 StancePoise 進入 IsStaggered，直接進入
    PostureBroken，避免先進 HitReaction 再切換。

### **驗收條件**

- DoubleCombo 第一擊被完美彈反後，第二擊仍依原動畫與Hit Window發生。

- DoubleCombo 第二擊被彈反後，Boss進入短後震並結束該連段。

- ContinueCombo不重設_currentAttack、不關閉後續Hit
  Window，也不刷新整招冷卻。

- 彈反造成架勢破壞時，Boss直接進入PostureBroken且不執行普通Recoil。

### **必要測試**

  ---------------------------------------------------------------------------
  **測試情境**                  **預期結果**
  ----------------------------- ---------------------------------------------
  **第一刀Parry、第二刀不防**   第一刀無血傷；第二刀正常命中玩家

  **兩刀都Parry**               兩次架勢傷害；第二刀後Boss Recoil

  **第一刀使Boss架勢滿**        立即PostureBroken，第二窗不得再啟用

  **普通Guard**                 不觸發DeflectReaction，只走Guard結果
  ---------------------------------------------------------------------------

# **3. 工程項目2：Tap Guard、GuardVolume 與 Animator 一致性**

## **3.1 問題與目標**

目前 GuardVolume 在 IsBlocking 或 InTapGuardWindow 時啟用，但 Animator
的 IsGuarding 只跟隨
IsBlocking。玩家快速點按後放開，可能已經退出防禦動畫，防禦膠囊仍在0.55秒內有效，形成不可見格擋。

> **設計決策　**輸入狀態與防禦動作狀態必須分離。按鍵是否仍按住不等於防禦動作是否已結束。

## **3.2 狀態定義**

public bool DefenseActionActive =\>\
CanDefend &&\
(IsGuardButtonHeld \|\| InTapGuardWindow);\
\
public DefenseState CurrentDefense =\>\
InParryWindow ? DefenseState.Parry :\
DefenseActionActive ? DefenseState.Guard :\
DefenseState.None;

GuardVolume、Animator、玩家移動減速、武器防禦姿勢、視覺提示與 Debug
Overlay 必須全部讀取 DefenseActionActive／CurrentDefense，不得各自組合
IsBlocking 與 InTapGuardWindow。

## **3.3 時間模型**

  ---------------------------------------------------------------------------------
  **輸入後時間**          **防禦結果**   **動畫／Collider**
  ----------------------- -------------- ------------------------------------------
  **0.00--0.20s**         Parry          GuardStart／GuardHold；GuardVolume啟用

  **0.20--0.40s**         Guard          短Tap防禦仍保持；GuardVolume啟用

  **0.40s後且已放開**     None           播放GuardEnd並關閉GuardVolume

  **0.20s後且持續按住**   Guard          維持GuardHold直到放開
  ---------------------------------------------------------------------------------

> **調校界線　**0.40秒是工程初始值；0.20秒Parry
> Window在第9項前保持不動。若仍使用0.55秒，動畫與Collider也必須完整維持0.55秒，不允許隱形防禦。

## **3.4 Animator 改造**

- IsGuarding Bool改由 DefenseActionActive 驅動。

- Guard進場轉場由0.14秒縮至約0.03--0.06秒，避免大部分Parry
  Window仍在抬刀。

- 建立短促的GuardImpact與ParryImpact上半身反作用動畫；成功彈反時即使仍按著防禦也必須播放。

- 優先使用Animator上半身Layer與AvatarMask，避免每次彈反破壞腳步、鎖定與地面移動。

- 死亡、Staggered、GuardBreak、Execution與Ultimate進場時強制呼叫CancelDefenseAction。

### **驗收條件**

- 快速點按後，角色防禦姿勢、移動減速、GuardVolume與UI在同一幀結束。

- 玩家持續按住防禦時，0.20秒後只剩一般Guard，不會刷新Parry Window。

- 成功彈反時無論按鍵是否仍按住，都能看到短ParryImpact反作用。

- 連按使Parry Window縮至0時仍可一般格擋，但會依第6項累積足額架勢。

### **必要測試**

  ---------------------------------------------------------------------------------
        **測試情境**        **預期結果**
  ------------------------- -------------------------------------------------------
     **按下50ms後放開**     Parry／Guard動作持續到Tap窗口結束，視覺與Collider同步

         **按住2秒**        前0.2秒Parry，之後持續Guard

    **Stagger期間按防禦**   DefenseActionActive保持false

        **後方攻擊**        即使GuardVolume啟用也不得分類為Guard／Parry
  ---------------------------------------------------------------------------------

# **4. 工程項目3：Boss 刀根／中段／刀尖旋轉 Sweep**

## **4.1 問題與目標**

現行 BossHitbox 主要從上一個中心位置 Cast
到目前中心位置。對旋轉武器而言，刀柄附近可能幾乎不移動，但刀尖已經掃過很大弧線；只掃中心會漏掉真正的刀尖路徑。

> **設計決策　**Sweep的基本單位改為武器線段的多點取樣，不再以Collider中心平移代表整把刀的運動。

## **4.2 元件與資料**

public sealed class WeaponSweepSampler : MonoBehaviour\
{\
\[SerializeField\] Transform bladeRoot;\
\[SerializeField\] Transform bladeMid;\
\[SerializeField\] Transform bladeTip;\
\[SerializeField\] float sweepRadius;\
\[SerializeField\] LayerMask targetMask;\
\[SerializeField\] float maxSampleTravel = 0.25f;\
}

若沒有獨立 bladeMid Transform，可用 Vector3.Lerp(bladeRoot.position,
bladeTip.position, 0.5f)
計算。每個採樣點都保存上一個已評估動畫姿勢的位置。

## **4.3 更新順序**

- Animator以Normal模式更新時，Sweep應在LateUpdate讀取完成後的骨骼位置；不要在Animator尚未更新的FixedUpdate使用舊姿勢。

- Hit
  Window在Update判定是否啟用；LateUpdate先取得新刀姿，再完成Sweep與命中解析。

- GuardVolume同樣在LateUpdate先對齊玩家防禦姿勢，執行順序必須早於Boss
  Sweep。

- 若堅持使用FixedUpdate，則Boss
  Animator必須改為AnimatePhysics，且PlayerGuardVolume與BossHitbox全部統一在同一物理時序。不得混用。

## **4.4 掃掠演算法**

for each point in \[Root, Mid, Tip\]:\
travel = distance(previous, current)\
subdivisions = ceil(travel / maxSampleTravel)\
for each subsegment:\
SphereCastNonAlloc(subFrom, radius, direction, distance, targetMask)\
\
collect all hits\
sort by travel distance\
resolve nearest GuardVolume vs nearest Hurtbox\
deduplicate by target root within current Hit Window activation

現有 bodyDist + 0.05 \< guardDist 的防守有利規則可以保留。新的 Resolver
仍須保存每次 Activate→Deactivate 對同一目標只結算一次的 HashSet 行為。

## **4.5 Layer 與接觸點**

- targetMask只包含PlayerGuardWeapon、PlayerHurtbox及必要的Environment，不再使用\~0。

- RightFootHitbox移到統一的BossAttack
  Layer；腳部仍可依設計進入Parry／Guard分類。

- 火花位置取最近Sweep接觸點，不取玩家中心或Boss根物件。

- Debug
  Gizmo同時畫出Root／Mid／Tip三條Previous→Current軌跡及最終採用接觸點。

### **驗收條件**

- 刀身中心幾乎不動、刀尖高速旋轉穿過玩家時仍能穩定命中。

- 60、30及不穩定幀率下，同一攻擊命中結果一致。

- 同一Hit Window不會因Root／Mid／Tip三條Sweep而重複結算。

- Sweep不會命中Boss自己的Collider、場景無關Trigger或VFX物件。

### **必要測試**

  -------------------------------------------------------------------------
  **測試情境**                **預期結果**
  --------------------------- ---------------------------------------------
  **原地180度旋轉刀身**       刀尖路徑能命中；中心平移接近0不影響

  **Guard與Body同時被掃到**   依最短路徑與5cm防守偏差只結算一次

  **三採樣點同時命中**        相同target root只出現一次傷害／Clash

  **Hit Window關閉**          即使刀仍穿過玩家也不得命中
  -------------------------------------------------------------------------

# **5. 工程項目4：玩家武士刀 Sweep 攻擊**

## **5.1 問題與目標**

玩家目前仍使用Player root附近Range 0.5／Radius
0.5的球形範圍檢查。這使視覺刀刃與命中位置脫鉤，也無法和Boss刀刃判定形成對稱架構。

> **設計決策　**玩家與Boss共用WeaponSweepSampler與Combat Contact
> Resolver；差別只存在於攻擊資料、目標Layer與防禦能力。

## **5.2 遷移架構**

PlayerCombat\
-\> CurrentActiveAttack\
-\> PlayerAttackWindowDriver\
-\> PlayerWeaponHitbox\
-\> WeaponSweepSampler\
-\> IDamageable.ApplyDamage(DamageInfo)

PlayerCombat原有Combo、傷害、HitEffectPrefab與CurrentActiveAttack可以保留。需要替換的是空間查詢，不是整套玩家戰鬥輸入。

## **5.3 實作步驟**

6.  在玩家刀身建立BladeRoot、BladeMid、BladeTip三個Transform，使用世界座標驗證80倍骨骼縮放下的位置。

7.  建立PlayerWeaponHitbox與PlayerAttackWindowDriver；由動畫事件或每招normalized
    window開關。

8.  保留舊球形判定為legacy feature
    flag，先允許Debug模式同時執行但只由新系統結算傷害。

9.  比較舊、新兩套結果：命中目標、接觸時間、接觸點、是否空揮；確認後移除舊OverlapSphere路徑。

10. HitEffect、血液特效與音效改用Sweep接觸點；Direction維持從玩家指向目標的水平向量。

## **5.4 攻擊資料需求**

  -------------------------------------------------------------------------------
  **欄位**               **用途**              **初始策略**
  ---------------------- --------------------- ----------------------------------
  **HealthDamage**       血量傷害              沿用CurrentActiveAttack

  **PoiseDamage**        架勢傷害              加入每招顯式值

  **HitWindows**         有效幀                依動畫實測normalized區間

  **SweepRadius**        刀刃容錯              依模型尺度統一調整

  **MaxTargets**         單次可命中數          一般斬擊可多目標；每目標一次

  **AttackInstanceId**   去重                  每次Combo step遞增
  -------------------------------------------------------------------------------

### **驗收條件**

- 刀刃未碰到Boss Hurtbox時，不因玩家root距離夠近而造成傷害。

- 刀尖確實穿過Boss Hurtbox時，即使玩家root稍遠仍能命中。

- 單次揮刀對同一Boss只結算一次，但可命中多個不同敵人。

- Combo Attack1--4各自有獨立AttackInstance與Hit Window。

### **必要測試**

  ---------------------------------------------------------------------------------
             **測試情境**             **預期結果**
  ----------------------------------- ---------------------------------------------
         **貼近但刀揮向外側**         不命中

   **刀尖命中、玩家root在舊Range外**  新Sweep命中

          **低幀率快速揮刀**          不中斷、不穿透

       **連段兩刀命中同一Boss**       每刀各一次，共兩次
  ---------------------------------------------------------------------------------

# **6. 工程項目5：Boss Root Motion、比例與攻擊 Clip 空間**

## **6.1 問題與範圍**

武士根物件scale=4、Animator applyRootMotion=false，Meshy
Clip又將前進位移烤入骨骼。結果是可見刀身與Boss gameplay
root分離，DoubleCombo第二段或ChargeCut衝刺可能落在Boss前方6--8公尺；大型GuardVolume只是補償這個空間錯位。

> **設計決策　**不要直接全域開啟Root
> Motion。先以可控制、可中斷的AttackMotionProfile修復Gameplay位移，再於正式資產階段正規化模型比例與動畫根位移。

## **6.2 子階段5A：程式化攻擊位移**

\[Serializable\]\
public sealed class AttackMotionProfile\
{\
public float moveStartNormalized;\
public float moveEndNormalized;\
public float forwardDistance;\
public AnimationCurve movementCurve;\
public float trackingDropNormalized;\
public bool stopOnDeflectRecoil;\
}

- BeginAttack時鎖定攻擊起點與有限的目標方向，不在命中前無限追蹤玩家。

- 依normalized
  time與movementCurve計算該幀應到的位置，經CharacterController或既有NavPathFollower移動。

- DodgeCounter、LeapSlam、Breakdance、PostureBroken、Dead、ReturnHome切入時立即清除AttackMotion。

- 先讓DoubleCombo與ChargeCut的視覺刀路能重新連回玩家近戰距離。

## **6.3 子階段5B：比例與骨架正規化**

- Gameplay root保持scale=1；模型比例透過FBX Import
  Scale或Visual子物件處理。

- 重新計算CharacterController、BodyHurtbox、KatanaSocket與AimPoint的世界尺寸。

- 移除需要世界座標補償的80倍武器掛點；武器Socket應在局部空間使用接近1的尺度。

- 重新取樣攻擊Clip，以blade-relative-to-hips與root
  motion曲線分別驗證武器路徑和角色位移。

- 為玩家與Boss建立各自的Animator
  Controller，停止玩家與中立者／守望者共用控制器。

## **6.4 子階段5C：精確 Guard Collider**

只有在Boss尺寸、刀路與角色位移通過驗收後，才將Directional Defense
Volume替換為沿玩家刀身80--90%的細長Guard
Collider。過渡期間保留兩種模式，以Feature Flag進行A/B測試。

  ---------------------------------------------------------------------------
  **模式**           **用途**                  **移除條件**
  ------------------ ------------------------- ------------------------------
  **Proxy            目前4倍Boss／漂移Clip     正式刀身判定連續通過全部招式
  GuardVolume**                                

  **Blade Guard      正式武器接觸              成為預設後保留一版回退
  Collider**                                   
  ---------------------------------------------------------------------------

### **驗收條件**

- DoubleCombo兩段、ChargeCut與SwordJudgment均在視覺刀身到達玩家時命中。

- 中斷任何攻擊後Boss不再殘留位移、旋轉或root delta。

- Gameplay root與Visual比例分離，Collider世界尺寸可直接以公尺理解。

- 精確Guard模式下，火花位置與兩把刀的可見交點誤差達到可接受範圍。

### **必要測試**

  -------------------------------------------------------------------------
  **測試情境**                **預期結果**
  --------------------------- ---------------------------------------------
  **攻擊中途PostureBroken**   立即停止AttackMotion，Boss停在當前合法位置

  **DoubleCombo第二段**       不再出現在靜止Boss前方6--8m

  **比例遷移前後**            鎖定、相機、Hurtbox與移動速度保持一致

  **Proxy／Blade切換**        判定模式可回退且不改變Health管線
  -------------------------------------------------------------------------

# **7. 工程項目6：一般格擋使用每招 PoiseDamage**

## **7.1 問題與目標**

目前所有一般格擋固定增加玩家6點架勢，導致輕斬、重劈與大招對防禦的壓力幾乎相同。玩家100架勢需要約17次格擋才會崩解，anti-mash只降低彈反成功率，卻沒有足夠代價。

> **設計決策　**一般格擋架勢必須由本次攻擊的PoiseDamage決定；完美彈反仍讓玩家承受0架勢，並對Boss造成反向架勢傷害。

## **7.2 計算式**

guardedPlayerPoise =\
hitContext.PoiseDamage\
\* playerGuard.guardPoiseMultiplier\
\* hitWindow.guardPoiseMultiplier;\
\
parriedPlayerPoise = 0;\
parriedBossPoise = baseParryPoise +\
hitContext.PoiseDamage \* parryRewardMultiplier;

第一版可將 playerGuard.guardPoiseMultiplier
設為1.0、每窗倍率設為1.0；Boss反向架勢仍先維持現有14，直到第9項再決定是否改為與攻擊重量連動。

## **7.3 初始格擋壓力**

  ------------------------------------------------------------------------
  **招式**                  **目前攻擊Poise**   **建議一般格擋增量**
  ------------------------- ------------------- --------------------------
  **DoubleCombo每擊**       12                  12

  **ChargeCut**             12                  12

  **SpartanKick**           14                  14

  **SwordJudgment**         22                  22

  **OverheadSlam**          22                  22
  ------------------------------------------------------------------------

## **7.4 Guard Break**

玩家架勢達100時，應進入專用GuardBreak／Stagger狀態。玩家不應直接沿用Boss為處決設計的6秒僵直；第一版建議玩家GuardBreak約0.8--1.5秒，Boss
PostureBroken則由生命節點與Execution規則控制。實際秒數延後到第9項調校。

### **驗收條件**

- 一般格擋SwordJudgment增加的玩家架勢高於ChargeCut。

- 完美彈反不增加玩家架勢，但正常增加Boss架勢。

- 連按使Parry Window降為0時，玩家雖能Guard，仍快速累積正確架勢。

- Health.Damaged與StancePoise不會因同次Guard同時走兩條路而重複增加架勢。

### **必要測試**

  ----------------------------------------------------------------------------
  **測試情境**                   **預期結果**
  ------------------------------ ---------------------------------------------
  **連續5次SwordJudgment格擋**   在無回復情況下第5次前後造成GuardBreak

  **連續Parry**                  玩家架勢不增加；Boss架勢正常增加

  **非刀刃軟格擋**               依DamageInfo
                                 ExplicitPoiseAmount或明確規則結算一次

  **Guard Break時再按防禦**      不重新開啟DefenseAction
  ----------------------------------------------------------------------------

# **8. 工程項目7：Execution、Boss生命節點與永久死亡**

## **8.1 問題與目標**

ExecutionAbility目前對僵直目標造成當前血量50%的傷害，數學上無法單靠處決將生命歸零；同時武士permanentDeath=false，死亡後會自動復活。這與『處決』及Boss戰結束語意衝突。

> **設計決策　**普通敵人處決直接死亡；武士Boss使用兩個Deathblow生命節點。第一次處決進入Phase
> 2，第二次處決才永久死亡。

## **8.2 生命節點元件**

public interface IExecutable\
{\
bool CanExecute(GameObject executor);\
ExecutionResult Execute(GameObject executor);\
}\
\
public sealed class BossLifeNodeController : MonoBehaviour, IExecutable\
{\
\[SerializeField\] int maxDeathblowNodes = 2;\
\[SerializeField\] int remainingNodes = 2;\
\[SerializeField\] bool restoreHealthOnPhaseChange = true;\
}

## **8.3 流程**

Boss PostureBroken\
-\> 玩家進入ExecutionRange並按F\
-\> 鎖定雙方、關閉Hitbox、播放Execution\
-\> RemainingNodes\--\
-\> \> 0: PhaseTransition / Restore Health & Stance / Grace\
-\> = 0: PermanentDeath / Boss UI close / Cutscene signal

- 第一次Deathblow移除一個UI節點，切換Phase
  2；Boss血量是否全回復由設定控制，建議第一版全回復。

- 最後節點移除時設定permanent death，取消5秒自動復活、所有Pending
  Special與攻擊。

- Execution動畫期間玩家與Boss皆由Execution鎖定來源控制無敵與移動，結束時成對釋放。

- ExecutionDamagePercentOfCurrentHealth保留給非致死Finisher時改名使用，不再作為Deathblow核心。

- Phase Transition期間清除Stance、Hit Window、AttackMotion、Guard
  Clash冷卻與Boss攻擊狀態。

### **驗收條件**

- 第一次處決不進入永久Dead，而是正確切換Phase 2並扣除一個生命節點。

- 第二次處決永久停止Boss AI、Hitbox、復活計時器與戰鬥UI。

- 處決中途切場景、玩家死亡或動畫中斷時，不留下無敵、鎖位或timeScale異常。

- 普通敵人仍可使用單節點直接死亡，不必引用BossStateMachine。

### **必要測試**

  ------------------------------------------------------------------------
  **測試情境**               **預期結果**
  -------------------------- ---------------------------------------------
  **Boss兩節點第一次處決**   Phase2；剩餘1節點；Boss重新可戰鬥

  **Boss第二次處決**         永久Dead；5秒後不復活

  **僵直逾時未處決**         Boss依規則恢復，不扣生命節點

  **Execution重複輸入**      同一PostureBroken只接受一次
  ------------------------------------------------------------------------

# **9. 工程項目8：特殊招式排程與架勢資料權威**

## **9.1 特殊招式排程問題**

Breakdance、LeapSlam與OverheadSlam分別以15、20、30秒累積並保留Pending。60秒戰鬥中理論上可排入9次週期特殊招式，尚未包含Ultimate、DodgeCounter與TooCloseKick；多個Pending同時到期時可能依優先序連續釋放。

> **設計決策　**週期計時器只代表技能『取得資格』，真正釋放由單一SpecialAttackScheduler依共享冷卻、距離、階段與最近使用紀錄選擇。

## **9.2 排程器模型**

SpecialAttackScheduler\
SharedSpecialCooldown: 6--10s\
MaxPendingSpecials: 1\
EligibleSpecials: timer + distance + phase + cooldown\
Score: overdue + contextWeight + phaseWeight - recentUsePenalty\
SelectHighestScore()

TooCloseKick仍可保留為安全機制，但觸發後應短暫占用共享特殊招式冷卻，防止踢擊後立即接LeapSlam。Ultimate可以保留更高優先權，但進場時要清除或延後普通特殊招式Pending。

## **9.3 架勢單一權威**

目前StancePoise場景值與Wushi_Tuning內部回復值並存。改造後只有StancePoise持有CurrentStance、回復延遲、回復速度、IsStaggered與Grace。BossStateMachine只訂閱事件並改變動畫／AI狀態，不再維護第二份架勢時間。

  --------------------------------------------------------------------------------
  **資料**                    **唯一擁有者**           **其他系統角色**
  --------------------------- ------------------------ ---------------------------
  **CurrentStance**           StancePoise              UI與FSM唯讀

  **RegenDelay／RegenRate**   StancePoise序列化值      Tuning只作資產預設或移除

  **IsStaggered**             StancePoise              FSM接事件進PostureBroken

  **PostureBroken時間**       Boss生命／狀態配置       StancePoise不另跑競爭計時

  **UltimateEnergy**          單一UltimateEnergy元件   移除玩家root重複實例
  --------------------------------------------------------------------------------

## **9.4 遷移步驟**

11. 建立SpecialAttackScheduler並先以Feature
    Flag旁路舊TryEnterBreakdance／LeapSlam／PeriodicSlam。

12. 三個舊計時器改為向Scheduler註冊Eligibility，不再各自持有可永久堆積的Pending。

13. 盤點BossStateMachine所有讀寫架勢欄位，改為讀取StancePoise
    API與事件。

14. 保留目前場景手調StancePoise數值100／60、regenDelay與regen，不以Tuning預設覆寫。

15. 移除或停用重複UltimateEnergy前，先確認UltimateAbility、UI與Aura各自取得哪個元件。

### **驗收條件**

- 任一時刻最多一個週期特殊招式Pending，特殊招式之間遵守共享間隔。

- 60秒附近不會因15／20／30秒公倍數連續排出三個特殊招式。

- Boss架勢條、FSM與StancePoise在所有時刻顯示相同CurrentStance與IsStaggered。

- 玩家究極能量只存在一個權威值，UI、技能與就緒光環讀取相同來源。

### **必要測試**

  -----------------------------------------------------------------------
  **測試情境**              **預期結果**
  ------------------------- ---------------------------------------------
  **三技能同時Eligible**    只選一個最高分；其餘延後，不永久排隊

  **TooCloseKick後**        共享特殊冷卻啟動，不立即接週期大招

  **Ultimate進場**          普通特殊招式延後且無殘留Hit Window

  **架勢回復**              只有StancePoise一條回復曲線，FSM無第二套值
  -----------------------------------------------------------------------

# **10. 工程項目9：最終速度、傷害與彈反窗口調校**

## **10.1 啟動條件**

本項只能在前8項的規則與判定穩定後開始。否則調整的不是戰鬥難度，而是在補償Collider、Root
Motion、動畫同步或重複資料問題。

> **鎖定基準　**調校開始前維持ParryWindowDuration=0.20秒。先調Boss前搖、動作可讀性、格擋架勢與攻擊傷害，最後才判斷是否需要改動彈反窗口。

## **10.2 必須收集的指標**

  -----------------------------------------------------------------------------------
          **指標**         **紀錄方式**                   **用途**
  ------------------------ ------------------------------ ---------------------------
      **Parry成功率**      各招第一次遭遇／熟練後成功率   辨識是否可讀

    **Guard／Parry比例**   防禦接觸中兩者占比             判斷窗口與前兆

     **Boss破架勢時間**    每Phase平均秒數與彈反次數      校正60架勢與14反向傷害

   **玩家GuardBreak時間**  連續格擋幾擊崩解               校正每招PoiseDamage

   **未防禦死亡所需命中**  輕擊／重擊組合                 校正500 HP與25--32傷害

       **武器空揮率**      視覺交叉但未命中／隔空命中     驗證Sweep與空間

      **特殊招式占比**     特殊招式時間／總戰鬥時間       校正Scheduler

     **接觸點視覺誤差**    火花與刀刃距離                 決定是否可移除Proxy Guard
  -----------------------------------------------------------------------------------

## **10.3 調校順序**

16. 先調動畫前兆與Boss state speed，使第一刀可讀。

17. 再調Hit Window位置，不先擴大Window長度補償漏判。

18. 調整玩家一般格擋架勢壓力與GuardBreak恢復時間。

19. 調整Boss對玩家血量傷害與重擊／輕擊差異。

20. 調整Boss架勢上限、彈反獎勵與回復速度。

21. 調整特殊招式出現頻率及共享冷卻。

22. 最後才A/B測試0.18、0.20、0.22秒Parry Window；沒有證據時保留0.20秒。

## **10.4 現況時間基準**

  ------------------------------------------------------------------------------
  **招式**            **首次接觸約**   **有效窗**   **調校注意**
  ------------------- ---------------- ------------ ----------------------------
  **SwordJudgment**   0.66s／2.09s     各165ms      第二刀是延遲混招，確認前兆

  **DoubleCombo**     0.57s／1.31s     116／101ms   先修位移與連段不中止

  **ChargeCut**       0.45s            124ms        需蓄力VFX／音效才公平

  **SpartanKick**     0.61s            154ms        目前placeholder timing

  **OverheadSlam**    0.99s            145ms        慢重擊，適合高架勢壓力
  ------------------------------------------------------------------------------

### **驗收條件**

- 每招的可讀性、命中時機與傷害都有Play
  Test紀錄，不只依Animator畫面主觀判斷。

- 調整state speed後，自動重新計算並顯示實際首次接觸與有效窗毫秒數。

- Boss攻擊傷害、玩家架勢壓力與Boss架勢破壞形成可說明的風險／報酬關係。

- 最終Parry Window變更具備A/B測試證據，且anti-mash行為仍通過回歸測試。

# **11. 整體排程與相依性**

## **11.1 建議里程碑**

  -----------------------------------------------------------------------------------------------------------
   **里程碑**  **包含項目**   **交付結果**                                               **進入條件**
  ------------ -------------- ---------------------------------------------------------- --------------------
      **M1     1、2、6        彈反不中斷不該中斷的連段；無隱形Guard；架勢使用每招Poise   可獨立Play Test
   規則穩定**                                                                            

      **M2     3、4           玩家與Boss共用旋轉Sweep與接觸解析                          需要M1結果分類
   命中對稱**                                                                            

      **M3     5              攻擊位移、比例與精確Guard遷移                              依賴M2除錯資料
   空間修正**                                                                            

      **M4     7、8           Deathblow生命節點、特殊排程、單一架勢權威                  依賴M1穩定狀態
   戰鬥循環**                                                                            

      **M5     9              速度、傷害、架勢、特殊頻率與Parry Window                   依賴M1--M4全部完成
   最終調校**                                                                            
  -----------------------------------------------------------------------------------------------------------

## **11.2 相依關係**

1 DeflectReaction ─┐\
2 Defense一致性 ──┼─\> 3 Boss Sweep ─\> 5 Boss空間／精確Guard ─┐\
6 Guard Poise ─────┘ └────\> 4 Player Sweep │\
├─\> 9 最終調校\
7 Life Nodes ───────────────────────────────────────────────│\
8 Special + Stance Authority ───────────────────────────────┘

## **11.3 每個里程碑的完成定義**

- 程式編譯通過且既有測試全綠；新增純邏輯單元測試與GreyboxTest整合測試。

- F9 Debug能顯示此次新增狀態、Sweep、接觸點、AttackInstance與最終分類。

- 至少完成正面、背面、低幀率、連段、HitStop、死亡／僵直中斷等回歸案例。

- 場景與資產序列化值有變更紀錄；Setup Menu可重跑且不會重複建立元件。

- 新路徑穩定前保留可回退Feature Flag；完成驗收後才移除legacy路徑。

## **11.4 建議分支／提交策略**

每個工程項目應以獨立分支或可回退提交完成。不要將Animator資產、BossStateMachine重構、碰撞Layer、數值調整和場景大規模序列化變更混在同一提交；GreyboxTest含大量Live2D內嵌Mesh序列化，提交前需排除無關churn。

# **12. 風險、回退與回歸矩陣**

## **12.1 主要風險**

  ---------------------------------------------------------------------------------------------------------------
   **等級**  **風險**              **原因**                           **控制措施**
  ---------- --------------------- ---------------------------------- -------------------------------------------
    **高**   旋轉Sweep重複命中     Root/Mid/Tip同時命中               保留window級target HashSet與AttackInstance

    **高**   Root Motion中斷漂移   特殊狀態切入未清delta              短期AttackMotionProfile；所有終端狀態清理

    **高**   Tap Guard隱形防禦     Animator與Collider讀不同條件       唯一DefenseActionActive

    **高**   連段被每次Parry取消   NotifyParried無條件進HitReaction   每窗DeflectReaction

    **中**   timeScale卡住         失焦／多個HitStop請求              Realtime恢復、堆疊服務、OnDisable強制1

    **中**   架勢雙重回復          FSM與StancePoise各自更新           StancePoise單一權威

    **中**   特殊招式連發          獨立Pending堆積                    共享Scheduler與Cooldown

    **中**   共用Animator受影響    Player與NPC共用NewAnimator         第5項拆分控制器
  ---------------------------------------------------------------------------------------------------------------

## **12.2 跨系統回歸測試**

  ----------------------------------------------------------------------------------------------
       **領域**       **測試**                              **最低通過標準**
  ------------------- ------------------------------------- ------------------------------------
     **防禦輸入**     按住、短Tap、連按、死亡／僵直中按下   狀態與動畫一致

     **方向判定**     Boss正前、側面、背面                  僅120度正面可防

   **方向向量符號**   DamageInfo為攻擊方→被擊方             Boss在前方必須判正面；背後判false

     **多段攻擊**     DoubleCombo、SwordJudgment            每窗一次；Continue／Recoil符合資產

      **低幀率**      15／30／60 FPS                        Sweep不穿透、窗口結果一致

      **HitStop**     連續Parry與Editor失焦                 timeScale必定恢復

   **PostureBroken**  Parry同幀達上限                       高於HitReaction並可Execution

     **Phase切換**    第一次Deathblow                       清Attack、Pending、Hitbox與位移

     **永久死亡**     最後Deathblow                         Boss不復活、UI關閉、AI停止
  ----------------------------------------------------------------------------------------------

# **13. 預計影響檔案與資產**

  -----------------------------------------------------------------------------------------------
  **檔案／資產**                       **項目**     **預計變更**
  ------------------------------------ ------------ ---------------------------------------------
  **BladeClash.cs**                    1、3、6      擴充接觸資料、DeflectReaction與分類結果

  **PlayerGuard.cs**                   2、6         DefenseActionActive、Tap時間、Poise計算

  **PlayerGuardVolume.cs**             2、5         統一啟用條件；日後Proxy／Blade模式

  **PlayerGuardAnimatorLink.cs**       2            Animator讀取DefenseActionActive；Impact觸發

  **BossHitbox.cs**                    1、3         多點Sweep、LayerMask、接觸解析

  **BossAttackDefinition.cs**          1、5、6      DeflectReaction、AttackMotion、Guard倍率

  **BossStateMachine.cs**              1、5、7、8   反應策略、位移、生命節點、Scheduler

  **PlayerCombat.cs**                  4            保留輸入／Combo，移除舊球形空間查詢

  **新增PlayerWeaponHitbox.cs**        4            玩家刀刃Sweep與AttackInstance

  **StancePoise.cs**                   6、8         單一架勢權威與事件

  **ExecutionAbility.cs**              7            改呼叫IExecutable／Deathblow

  **新增BossLifeNodeController.cs**    7            生命節點、Phase與永久死亡

  **新增SpecialAttackScheduler.cs**    8            共享特殊招式排程

  **Wushi_Tuning.asset**               5、8、9      位移、Scheduler與最終調校值

  **Wushi_Attack\_\*.asset**           1、5、6、9   每窗反應、位移、Poise與時間

  **Wushi.controller／玩家Animator**   2、5、9      Guard Impact、控制器拆分與速度

  **GreyboxTest.unity**                全部         序列化接線；避免無關Live2D churn
  -----------------------------------------------------------------------------------------------

## **13.1 不應在早期修改的數值**

- Player CharacterController.stepOffset=0。

- 使用者手調的相機distance與targetOffset。

- Player／Boss StancePoise場景序列化值，除非進入第6或第9項。

- ParryWindowDuration=0.20秒，直到第9項A/B測試。

- 已量測的Boss Hit Window
  normalized區間，除非Sweep／Clip空間驗證指出錯位。

# **14. Claude Unity MCP 執行規則**

每次交給Claude Unity
MCP時，只執行一個工程項目或一個明確子階段。提示中需先要求檢查現有檔案與序列化值，禁止依腳本預設覆寫場景手調值。

## **14.1 每項工作固定輸出**

- 列出新增與修改檔案，以及每個檔案的責任變化。

- 列出新增序列化欄位、預設值、遷移策略與Setup Menu是否需要更新。

- 提供EditMode純邏輯測試與PlayMode／GreyboxTest整合測試。

- 編譯並執行既有測試；不得只說程式碼看起來正確。

- 保存場景前檢查是否產生無關Live2D序列化churn。

- 提供實際Play Test步驟與F9 Debug應看到的結果。

## **14.2 完成回報格式**

完成項目：\<工程項目／子階段\>\
修改檔案：\<列表\>\
序列化遷移：\<欄位與值\>\
測試：\<通過數／失敗數\>\
GreyboxTest驗證：\<步驟與結果\>\
已知限制：\<尚未解決項\>\
回退方式：\<Feature Flag／提交\>\
下一個相依項目：\<編號\>

# **15. 最終完成檢查表**

- Boss多段攻擊可依每窗設定繼續、後震或取消。

- Tap Guard、GuardVolume、Animator、移動與UI完全同步。

- Boss與玩家皆使用刀根／中段／刀尖Sweep，沒有舊Root球形命中。

- Boss攻擊位移與視覺Clip空間一致，精確Guard Collider可取代Proxy。

- 一般格擋使用每招PoiseDamage，重擊確實提高架勢壓力。

- 武士Boss使用Deathblow生命節點，最終處決永久死亡。

- 特殊招式只有單一Scheduler，架勢只有單一權威來源。

- 速度、傷害與0.20秒Parry Window在完整系統上完成數據化調校。

- 所有回歸測試通過，timeScale、無敵、Hitbox、AttackMotion無殘留。

> **文件結束　**本規格的核心策略是先修規則，再修命中，再修空間與Boss循環，最後才調數值。任何跨階段提前調參都必須記錄為暫時補償，避免成為正式設計。
