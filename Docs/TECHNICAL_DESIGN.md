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
