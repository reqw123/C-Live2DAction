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

驗收條件：✅ Unity 專案乾淨編譯、✅ Console 無錯誤、✅ 至少 1 個 EditMode 測試通過（實際 8 個 EditMode + 3 個 PlayMode 全過，含下方的 Live2D 立牌探索）；⚠️ 「可在灰盒場景手動 Play 驗證移動與單次攻擊」這項需要使用者在 Editor 內實際操作確認，AI 無法自行操作互動視窗。

### Phase 1 追加：Live2D 立牌視覺探索 — ✅ 完成（2026-08-10，使用者追加需求）

使用者要求把 Player 的視覺換成 Live2D 外觀，選定「角色一律面向鏡頭的 2D 立牌」做法（3D 場景裡自由環繞的攝影機 + 只朝 Y 軸轉向鏡頭的 2D 角色，不是全 3D 可任意角度觀察的模型）。

- 匯入 Cubism SDK for Unity 5-r.4.2（本機已下載的官方安裝包），修正一個實際遇到的匯入錯誤（複製進本專案的 076 模型 `model3.json` 原本引用一個不存在的 `.physics3.json`，會讓 Cubism 的 AssetProcessor 直接報錯——刪掉該行參照，這是本專案自己複製的檔案，不影響 `C:\question` 原始檔）。
- 新增 `Assets/_Project/Rendering/Shaders/CubismUnlitURP.shader`：SDK 內建的 Unlit/Mask shader 是 Built-in RP 專用，在 URP 下不會被渲染管線選中，改寫一份等效的 URP shader（不含 Mask 裁切支援）。
- 新增 `CubismBillboard`（Y 軸朝向攝影機）與 `PlayerCubismVisualSetup.cs`（Tools/Live2DAction/Replace Player Visual With Live2D Standee）：載入 076 model3.json → `ToModel()` → 套用 URP shader → 依 `CanvasHeight/PixelsPerUnit` 換算的真實高度縮放 → 掛到 Player 底下取代原本的 Capsule 視覺。
- 過程中修好一個真實 bug：一開始把 `CanvasHeight`（像素單位）直接當 Unity 單位換算縮放比例，角色被縮小成肉眼看不到的小點；改成除以 `PixelsPerUnit` 換算成真正的 Unity 單位後，角色高度才跟訓練假人相當。
- 用命令列把 `Camera.main` 算圖進 `RenderTexture` 存成 PNG（`ScreenCapture.CaptureScreenshot` 在 batchmode 沒有真正的 backbuffer 存不出東西，改用這個方法），AI 端目視確認截圖：角色貼圖、比例、面向鏡頭都正確顯示，沒有粉紅材質或裁切錯位。截圖用的暫時腳本已刪除，不在 repo 裡。
- 已知限制記入 `KNOWN_ISSUES.md`：不支援 Mask 裁切；目前只顯示 moc3 靜止綁定姿勢（剛好帶著技能火焰特效，看起來像一直在出招，等 Phase 2 接上 idle motion 才會正常）；`ToModel()` 產生的物件在 Play 模式下 `gameObject.name` 會變空字串（原因未查出，不影響功能，已改用摧毀全部子物件而非按名字尋找繞開）；billboard 朝向公式未經人眼確認方向是否正確（Inspector 有 `Face Away Instead` 勾選框可一鍵翻轉，不用改程式碼）。
- **這是自動化截圖驗證，不是使用者本人在互動 Editor 裡看到的結果**，正式列入驗收前仍需要使用者自己 Play 一次確認。

### Phase 1 追加：Humanoid 角色佔位 — ✅ 完成（2026-08-10，取代上面的 Live2D 立牌）

使用者接著詢問網路上是否有現成可用的 3D 模型，解決「缺少 3D 人形角色模型」阻塞項（見 `PROJECT_AUDIT.md`）；比較 Mixamo／CC0 低多邊形套件／AI 生成模型後，選定 CC0 套件並取得使用者同意下載與接上。

- 從 https://quaternius.itch.io/universal-base-characters 下載「Universal Base Characters」Standard 版（CC0 1.0，122 MB），內附 `License_Standard.txt` 已確認授權文字，登記進 `Docs/ASSET_LICENSES.md`（這是**真正取得合法商用授權**的素材，跟 076/077 的處境不同）。
- 複製 `Superhero_Male_FullBody.fbx` 與對應貼圖到 `Assets/_Project/Characters/Placeholder/UniversalBaseCharacters/`（標記 Placeholder，比照 `ART_PIPELINE.md` 政策；髮型與 Female 版本這次沒有一起接，之後要用可以再加）。
- 新增 `PlayerHumanoidVisualSetup.cs`（Tools/Live2DAction/Replace Player Visual With Humanoid Placeholder）：把 FBX 的 Import Settings 設成 Humanoid Rig，建立 URP Lit 材質（BaseColor + Normal，未處理 Roughness），取代 Player 底下先前的 Live2D 立牌。
- Cubism SDK／URP shader／`CubismBillboard` 都還留著（Live2D 劇情演出功能仍會用），只是 Player 的戰鬥視覺換成這個 Humanoid 角色，076 模型目前沒有場景在引用。
- 用命令列算圖確認：角色貼圖、比例、站姿（bind pose，因為還沒接 Animator/動畫，會是 T-pose）都正確顯示，沒有粉紅材質。**仍是自動化截圖驗證，不是使用者本人在互動 Editor 裡看到的結果**。
- 已知限制記入 `KNOWN_ISSUES.md`：無動畫（T-pose 靜止）；Roughness 貼圖未套用；只接了 Male、無髮型。

## Phase 2：戰鬥垂直切片（未開始）

範圍完全比照企劃書「垂直切片最低範圍」：1 可操作角色、1 固定 3D 戰鬥場景、1 近戰敵人、1 遠程敵人、1 簡化 Boss、三段普攻、1 主動技能、1 閃避、敵人鎖定、血量、技能冷卻、受傷、死亡、勝利/失敗、暫停、重新開始、簡短 Live2D 開場/結束對話（佔位素材）、Windows 可執行 Build。

阻塞項已解除（Humanoid 角色佔位已就位），但仍缺動畫（Idle/Run/Attack/Dodge/Hit/Death）——可以用同作者 CC0 的「Universal Animation Library」（骨架相容）補上，尚未下載。

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
