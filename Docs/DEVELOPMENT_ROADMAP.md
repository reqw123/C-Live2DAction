# Development Roadmap — Live2DAction

流程：專案稽核 → 產品規格 → 技術架構 → 垂直切片 → Alpha → Beta → Release Candidate → Windows 正式 Build。每個階段可測試、可回復、可用 Git 追蹤。

## Phase 0：稽核與專案初始化 — ✅ 完成（2026-08-10）

- 確認全新獨立專案（不動 `C:\Live2DFighter`）。
- 確認 Unity 6000.0.81f1 + URP、Cinemachine 待安裝。
- 發現並記錄 076/077 Live2D 素材著作權風險，取得使用者處理決策（僅內部佔位）。
- 建立 `CLAUDE.md`、`README.md`、`Docs/` 全套文件骨架與 git repo。

## Phase 1：3D 灰盒原型 — ✅ 完成（2026-08-10）

目標：一個可以走、可以打一拳、假人會死的最小 3D 場景，不涉及 Live2D、不涉及美術資產。

- Unity 專案已建立（`Live2DAction/`，6000.0.81f1 + URP），套件：Input System 1.19.0、URP 17.0.4、Cinemachine 3.1.2、AI Navigation 2.0.5、Test Framework 1.6.0。
- `Assets/_Project/Game/` 下 Core／Input／Characters／Combat 四個資料夾與對應腳本：`IDamageable`／`DamageInfo`／`Health`（Core）、`IInputCommand`／`PlayerInputProvider`（Input，直接讀新版 Input System 的 Keyboard/Mouse，未用 .inputactions 資產）、`CharacterMovement`（CharacterController + 相機相對移動 + 平滑轉向）、`AttackData`／`AttackResolver`／`PlayerCombat`（Combat，`AttackResolver` 是純函式，`PlayerCombat` 只負責讀輸入與呼叫 `Physics.OverlapSphere`）。
- `GreyboxTest.unity`（已加入 Build Settings）：地板、3 個掩體方塊、Player（Capsule + CharacterController + 上述元件）、TrainingDummy（Capsule + `Health`）、第三人稱 Cinemachine 攝影機（`CinemachineOrbitalFollow` + `CinemachineRotationComposer` + `CinemachineInputAxisController`）。場景由 `Assets/Editor/Bootstrap/GreyboxSceneBuilder.cs`（Tools/Live2DAction/Build Greybox Test Scene）產生，可重新執行以重建場景。
- 8 個 EditMode 測試（`HealthTests`、`AttackResolverTests`）+ 2 個 PlayMode 測試（`CombatPlayModeTests`，真實 Update 迴圈跑攻擊→命中→扣血/死亡），全數通過。

**尚未驗證**：所有驗證都是透過 `-batchmode -runTests` 跑的，**沒有在互動式 Unity Editor 裡按過 Play**——移動手感、攝影機軌道/滑鼠視角是否順暢、視覺呈現是否正確，都還沒有人眼確認過，需要使用者在 Editor 打開專案親自 Play 一次才能算數（見 `KNOWN_ISSUES.md`）。

驗收條件：✅ Unity 專案乾淨編譯、✅ Console 無錯誤、✅ 至少 1 個 EditMode 測試通過（實際 8 個 EditMode + 2 個 PlayMode 全過）；⚠️ 「可在灰盒場景手動 Play 驗證移動與單次攻擊」這項需要使用者在 Editor 內實際操作確認，AI 無法自行操作互動視窗。

## Phase 2：戰鬥垂直切片（未開始）

範圍完全比照企劃書「垂直切片最低範圍」：1 可操作角色、1 固定 3D 戰鬥場景、1 近戰敵人、1 遠程敵人、1 簡化 Boss、三段普攻、1 主動技能、1 閃避、敵人鎖定、血量、技能冷卻、受傷、死亡、勝利/失敗、暫停、重新開始、簡短 Live2D 開場/結束對話（佔位素材）、Windows 可執行 Build。

阻塞項：需要至少一個授權清楚的臨時 Humanoid 3D 角色模型（見 `PROJECT_AUDIT.md` 中風險）。

## Phase 3：Live2D 與完整流程（未開始）

主選單 → Live2D 開場對話（佔位素材）→ 3D 戰鬥 → 結算 → Live2D 結束對話 → 返回選單 → Windows Build。此階段起，任何要交給他人測試的版本都必須先確認 076/077 佔位素材已被排除或不會被外流。

## Phase 4：Alpha（未開始）

3 個戰鬥場景、3 種敵人、2 個 Boss、存檔、設定、教學、音效、VFX、初步正式角色（原創 3D 模型需在此階段前到位）。**此階段開始，Live2D 佔位素材必須已被原創素材取代**，因為 Alpha 定義上會有外部測試需求。

## Phase 5：Beta（未開始）

完整內容，不再新增大型功能，修 Bug、平衡、效能、語言、操作體驗、授權稽核（含重新確認 `ASSET_LICENSES.md` 無佔位/未授權素材）。

## Phase 6：Release Candidate（未開始）

清除測試內容、完整 Build、回歸測試、發行文件、已知問題、Windows 乾淨環境測試。

## 暫時禁止（垂直切片與 Alpha/Beta 未穩定前）

開放世界、多人連線、抽卡、多名可操作角色切換、攀爬/游泳/滑翔、大型城鎮、大量 NPC 日程、程序生成世界、手機/主機平台、複雜裝備系統、大量支線任務。
