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

### Phase 1 追加：Maya 動漫風角色佔位 — ✅ 完成（2026-08-10，取代上面的 Humanoid 佔位）

使用者接著要求動漫風角色（企劃書的美術方向本來就是動漫風第三人稱動作遊戲），把上面的 Quaternius 角色降級為備用，改找動漫風 3D 角色。

- 在 Sketchfab 找到「3D動漫風角色屋 / 3D Anime Character Store」的角色系列（Megumi/Yong/Maya），確認為 **CC-BY 4.0**（可商用，須署名，禁止轉售原始檔），使用者選定 Maya。
- Sketchfab 下載需要帳號登入，AI 端依規定不能代為登入/建立帳號，使用者本人完成登入後才繼續下載。
- 下載到的是完整 Unity 套件：FBX（已預先設定好 Humanoid Rig）、Animator Controller（含 Idle/Walk/Run/Jump/Fall 動畫）、13 個材質、Prefab。複製進 `Assets/_Project/Characters/Placeholder/MayaAnime/` 時**連原始 `.meta` 檔一起複製**，讓 Prefab／Animator 內部的 GUID 參照能直接對上，不用手動重新連結。
- 新增 `PlayerMayaVisualSetup.cs`（Tools/Live2DAction/Replace Player Visual With Maya (Anime)）：材質原本是 Built-in RP 的 Standard shader（URP 下會粉紅），批次讀出 `_MainTex`／`_Color` 後轉存到 URP Lit 的 `_BaseMap`／`_BaseColor`；把 Prefab 掛到 Player 底下取代先前的 Humanoid 佔位；關閉 Animator 的 `Apply Root Motion`（避免跟 `CharacterController` 的位移邏輯打架）。
- 用命令列算圖確認：角色貼圖、比例、**Idle 待機動作**（不是 T-pose，Animator 內建的動畫直接可用）都正確顯示，沒有粉紅材質。**仍是自動化截圖驗證，不是使用者本人在互動 Editor 裡看到的結果**。
- 已知限制記入 `KNOWN_ISSUES.md`：無穿著（只有內衣）；Animator 的 Speed/H/V 等移動參數尚未接線，走路/跑步動畫還不會播放；發布 Build 前必須加上 CC-BY 署名（見 `ASSET_LICENSES.md`／`BUILD_RELEASE_GUIDE.md`）。
- Quaternius 的 Humanoid 佔位保留在專案內作為備用/未來敵人角色素材，未刪除。

## Phase 2：戰鬥垂直切片（進行中）

範圍完全比照企劃書「垂直切片最低範圍」：1 可操作角色、1 固定 3D 戰鬥場景、1 近戰敵人、1 遠程敵人、1 簡化 Boss、三段普攻、1 主動技能、1 閃避、敵人鎖定、血量、技能冷卻、受傷、死亡、勝利/失敗、暫停、重新開始、簡短 Live2D 開場/結束對話（佔位素材）、Windows 可執行 Build。

拆解成以下步驟依序推進（見下方各小節）：① 移動控制驗證＋移動動畫接線 → ② 三段普攻＋影格資料 → ③ 閃避 → ④ 敵人鎖定 → ⑤ 近戰敵人 AI → ⑥ 遠程敵人 AI → ⑦ 簡化 Boss → ⑧ 主動技能 → ⑨ 血量 UI／暫停／重新開始／勝敗畫面 → ⑩ Live2D 開場/結束對話串接、Windows Build。

### Step 1：移動控制驗證＋移動動畫接線 — ✅ 完成（2026-08-10）

目標：確保角色 1（Maya）的前後左右移動控制功能正確，並讓移動時播放對應的走路/跑步動畫（不再永遠只播 Idle）。

- 新增 `CharacterMovementTests.cs`（PlayMode）：用固定朝向的相機＋stub 輸入，驗證 W/A/S/D 四個方向的輸入分別讓角色往正確的世界座標軸移動（前→+Z、後→-Z、左→-X、右→+X），且不會有明顯的橫向漂移；另外驗證無輸入時不會漂移、面向會轉向移動方向。**過程中修正一個測試方法論問題**：headless batchmode 下 `yield return null` 每幀的 `Time.deltaTime` 極小（約 0.0003~0.003 秒），固定跑 30 幀根本不夠累積出有意義的位移；改用 `WaitForSecondsRealtime` 又發現它在這個環境下**不會**按比例把 `Update()` 跑滿等待的時間；最後改成「自己寫迴圈、每幀 `yield return null`、直到 `Time.realtimeSinceStartup` 累積到目標秒數為止」才是可靠的做法，並依實測到的位移量重新校正測試門檻（此環境下的積分效率大約只有理論值的 30%，可能跟 CharacterController.Move 或批次模式下的引擎排程有關，門檻已改成留有安全餘裕的保守值，不是照理論公式反推）。
- 新增 `CharacterMovement.CurrentHorizontalSpeed`／`MoveSpeed` 唯讀屬性，供其他系統讀取目前移動速度，不需要碰內部私有欄位。
- 新增 `CharacterAnimatorLink.cs`（`Assets/_Project/Game/Characters/`）：獨立元件，把 `CharacterMovement` 目前的速度換算成 Maya 的 Animator `Speed` 參數（Maya 的 Locomotion Blend Tree 門檻是 0/0.4/0.8/2，所以用 `(目前速度/moveSpeed) * 2` 換算），刻意不讓 `CharacterMovement` 本身知道 Animator 的存在（訓練假人沒有視覺、沒有 Animator，也不需要這個元件）。純換算邏輯抽成 `ComputeSpeedParameter` 靜態方法，5 個 EditMode 測試覆蓋（待機/滿速/半速/超速 clamp/moveSpeed 為 0 時不除以零）。
- 新增 `WireCharacterAnimatorLink.cs`（Tools/Live2DAction/Wire Character Animator Link On Player）：把 `CharacterAnimatorLink` 掛到 `GreyboxTest` 場景的 Player 上，自動找到 Maya 視覺底下的 Animator 並指定進去。已執行並用自動化測試確認 Player 上確實掛好元件、Animator 參照正確。
- 13 個 EditMode（原 8 + 新 5）與 9 個 PlayMode（原 3 + 新 6）測試全數通過。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**手感與動畫轉換是否順暢——自動化測試證明「方向正確、動畫參數正確換算」，但走路/跑步動畫切換時機是否自然、Blend Tree 過渡是否平順，需要人眼確認。

### Step 1 追加：修正兩個實際 Play 回報的 bug — ✅ 完成（2026-08-10）

使用者實際 Play 後回報兩個自動化測試沒抓到的問題，完整排查過程見 `KNOWN_ISSUES.md`／`CHANGELOG.md`：

- **腳步滑行**：`moveSpeed` 跟 Maya 動畫的 Blend Tree 門檻對不上，降到 2 對齊。
- **攝影機視角與角色朝向脫鉤**（左右顛倒、視角沒對齊角色）：五次 Cinemachine 配置修法皆實測無效，最終**移除 Cinemachine 的軌道/瞄準系統**，改寫自己掌控的 `ThirdPersonCameraController.cs`（直接讀滑鼠 delta、自算位置與旋轉，實作 `ICameraYawSource` 供移動邏輯讀取同一個 yaw 值）。`GreyboxTest.unity` 場景與 `GreyboxSceneBuilder.cs` 都已改用新攝影機；`Unity.Cinemachine` 套件參照已從 `Live2DAction.Runtime.asmdef` 移除（Cinemachine 套件本身仍安裝，僅未再被本專案程式碼使用）。
- 12 個 EditMode + 10 個 PlayMode 測試全數重新驗證通過。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：腳步是否貼地、滑鼠視角操作是否順手。

### Step 1 三次追加：攝影機改回原神風格滑鼠視角＋修正懸空落地 bug — ✅ 完成（2026-08-10）

使用者要求攝影機參考原神（滑鼠即時帶動視角），並回報角色移動時會「大跨步到很遠距離」。完整排查過程見 `KNOWN_ISSUES.md`／`CHANGELOG.md`：

- 攝影機改回滑鼠視角（`ThirdPersonCameraController` 讀 `Mouse.current.delta` 驅動 yaw/pitch），架構上跟先前 Cinemachine 版本不同（單一狀態同時驅動旋轉與移動方向），不會重演畫圈 bug。
- 「大跨步」真正原因：Player 的 `CharacterController.height` 曾被手動改成 1，重生 Y 座標沒同步調整，導致角色懸空、永遠碰不到地，重力累積到很大速度後撞到東西被彈飛。新增 `FixPlayerGroundedSpawn.cs`，改成從地面碰撞體＋角色體型動態反推重生高度。
- 12 個 EditMode + 10 個 PlayMode 測試全數重新驗證通過。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**。

### Step 2：三段普攻＋影格資料 — ✅ 完成（2026-08-10）

目標：把 Phase 1 的單發測試拳擴充成有 startup/active/recovery 影格、可連段的三段普攻，動畫先不處理（Maya 目前沒有攻擊動畫素材，先確保邏輯正確，動畫之後再補——使用者已確認此範圍）。

- `AttackData.cs` 新增影格資料欄位：`startupFrames`／`activeFrames`／`recoveryFrames`／`comboWindowFrames`（皆以 60fps 為基準換算成秒數，`FramesPerSecond` 常數集中管理），數值仍是可調的 ScriptableObject 資產，不寫死在程式碼。
- 新增 `AttackPhase.cs`（Idle/Startup/Active/Recovery）與 `ComboAttackState.cs`：純 C# 狀態機（非 MonoBehaviour），比照既有 `AttackResolver` 的「純邏輯先於 MonoBehaviour」慣例，可在 EditMode 直接測試時序與連段邏輯，不需要 Play 迴圈。狀態機每次攻擊只在進入 Active 的那一步觸發一次判定，Recovery 期間有連段視窗（`comboWindowFrames`），視窗內按下攻擊鍵會取消剩餘 Recovery 直接銜接下一段；視窗外按下不會連段，攻擊鍵在 Idle 狀態下按下才會開始新的一輪。
- `PlayerCombat.cs` 改成持有 `AttackData[] comboAttacks`（取代原本單一 `attackData` 欄位），每幀把 `Time.deltaTime` 與攻擊鍵狀態餵給 `ComboAttackState.Tick()`，回傳 true 時才執行 `Physics.OverlapSphere` + `AttackResolver.ResolveHits`。狀態機延遲到第一次 `Update()` 才建立（而不是 `Awake()`），沿用專案既有慣例：測試在 `AddComponent` 之後才用 reflection 設欄位，避免被 `Awake()` 提前快取用到舊值卡住。
- 新增 `Assets/_Project/Settings/Combat/LightAttack1/2/3.asset` 三個攻擊資料（預設數值：傷害 8/10/16，startup 6/7/10 影格，active 4/4/5 影格，recovery 14/16/22 影格，連段視窗 10/10/0 影格——第三段沒有下一段可接，數值不影響行為），取代舊的單一 `TestPunch.asset`（已刪除）。`GreyboxSceneBuilder.cs` 與新的一次性修正腳本 `FixComboAttacksSetup.cs` 都已改成建立/載入這三個資產並寫入 `comboAttacks` 陣列。
- 新增 `ComboAttackStateTests.cs`（EditMode，8 個測試）：涵蓋 Idle 無輸入不動作、攻擊鍵從 Idle 進入 Startup、Active 期間恰好判定一次、Recovery 逾時無輸入回到 Idle、連段視窗內按鍵成功銜接下一段、視窗外按鍵不會連段、第三段沒有第四段可接、Idle 時 `CurrentAttack` 為 null。
- 更新既有 `CombatPlayModeTests.cs`（原本假設攻擊鍵按下當幀就立刻命中）：改用 `comboAttacks` 陣列欄位，並把測試用的 `AttackData` 的 startup/active/recovery 全設為 0 影格，讓狀態機在幾幀內就確定性地走到判定步驟（headless batchmode 下單幀 `Time.deltaTime` 極小，見既有已知問題，0 影格門檻能確保不受這個時序怪異影響），迴圈等待 5 幀後再斷言傷害結果。
- 20 個 EditMode（原 12 + 新 8）＋ 10 個 PlayMode 測試全數通過。
- **已知限制**：連段判定完全靠邏輯與 debug 觸發驗證，Maya 沒有對應的三段攻擊動畫，Play 起來攻擊時角色視覺上不會有揮擊動作（只有 `Physics.OverlapSphere` 判定跟傷害會真的生效）；攻擊時是否要鎖定/減速移動也尚未處理，兩者都留給之後的步驟。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：連段輸入手感（尤其連段視窗的時機）是否合理，之後可依此調整 `LightAttack1/2/3.asset` 的影格數值。

### Step 2 追加：玩家＋敵人攻擊動作（程式驅動的簡易佔位揮擊姿勢）— ✅ 完成（2026-08-11）

使用者要求幫玩家與敵人加攻擊動作；因為 Maya 沒有攻擊動畫素材、敵人是無骨架 Capsule，兩者都做不出正式骨骼動畫，改採「用程式即時旋轉一個 Transform」的佔位方案，完整說明見 `Docs/CHANGELOG.md` 同日條目。

- `ComboAttackState`／`PlayerCombat` 新增 `PhaseProgress`（目前影格階段內的 0~1 進度）；新增純函式 `AttackPoseUtility.ComputeSwingAngle` 把影格階段換算成揮擊角度；新增 `AttackPoseVisualizer` 在 `LateUpdate` 把角度疊乘到指定 Transform（玩家用右手臂骨骼、敵人用整個 `Visual`），沒有攻擊時角度為 0，不影響 Maya 原本的 Idle/Walk 動畫。
- 新增一次性編輯器腳本 `WireAttackPoseVisualizers.cs` 並已對 `GreyboxTest.unity` 執行套用。
- 64 EditMode + 37 PlayMode 測試全數通過（新增 8 個），有實際跑 Unity 驗證。
- **已知限制**：這是「角度動畫」不是美術動畫，方向/角度是合理猜測，未經人眼確認（Inspector 有 `invert` 可調），見 `KNOWN_ISSUES.md`。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：揮擊方向/角度是否合理、敵人前傾出拳的視覺是否可接受。

### Step 2 追加：第一/第三人稱視角切換 — ✅ 完成（2026-08-10）

使用者要求「把畫面做成第一視角」，確認範圍為：先做成可切換的第一/第三人稱（保留第三人稱），第一人稱下先隱藏整個角色模型（Maya 沒有分離的第一人稱手臂素材）。

- `ThirdPersonCameraController` 新增 `CameraViewMode`（ThirdPerson/FirstPerson）與 V 鍵切換（`Keyboard.current.vKey.wasPressedThisFrame`）。第三人稱沿用原本的軌道公式；第一人稱直接把攝影機放在 `target.position + firstPersonEyeOffset`，不做距離拉遠、也不受旋轉影響位置。兩種模式都只讀同一份 `_yaw`／`_pitch`，`ICameraYawSource.YawDegrees` 不受視角模式影響，`CharacterMovement` 的相對移動方向計算完全不用改。
- 切換時透過 `visualToHide`（Player 的 "Visual" 子物件）`SetActive` 隱藏/顯示整個角色模型，避免第一人稱時攝影機卡在自己頭部模型裡面。
- 純位置計算抽成靜態方法 `ComputeCameraPosition(mode, targetPosition, rotation, distance, thirdPersonOffset, firstPersonEyeOffset)`，比照專案既有慣例，可在 EditMode 直接測試兩種模式的公式正確性，不需要 Play。
- 新增 `ThirdPersonCameraControllerTests.cs`（EditMode，4 個測試）驗證第一人稱模式忽略距離/旋轉、只用 eye offset；第三人稱模式維持原本「距離拉遠＋跟隨目標位置」的行為。新增 `CameraViewToggleTests.cs`（PlayMode，2 個測試）驗證 `ToggleViewMode()` 確實切換 `visualToHide` 的顯示狀態、且切換視角不會意外改動 yaw。
- `GreyboxSceneBuilder.cs` 與新的一次性修正腳本 `FixFirstPersonToggleSetup.cs` 都已同步寫入 `firstPersonEyeOffset`（預設 (0, 1.6, 0)，粗略的眼睛高度猜測值，未經人眼確認）與 `visualToHide`（Player 的 "Visual" 子物件）。
- 24 個 EditMode（原 20 + 新 4）＋ 12 個 PlayMode（原 10 + 新 2）測試全數通過。
- **已知限制**：第一人稱下攻擊的判定方向（`PlayerCombat.attackOrigin` 預設是 Player 根物件的 `forward`）目前仍然跟著「移動朝向」走，不是跟著攝影機視角走——因為 `CharacterMovement` 只有在有移動輸入時才轉向，第一人稱站著不動時攻擊方向不會跟著滑鼠視角轉。這次範圍只處理攝影機視角本身，攻擊瞄準方向的重新綁定留給之後的步驟（可能跟敵人鎖定 Step ④ 一起處理）。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：按 V 切換視角是否正常、第一人稱的眼睛高度是否合理、隱藏角色模型後畫面觀感如何。

### Step 3：閃避 — ✅ 完成（2026-08-10）

目標：加入按鍵觸發的短距離閃避（衝刺＋無敵幀＋冷卻），依 Roadmap 順序完成後才推進到下一步。

- `IInputCommand` 新增 `DodgePressed`；`PlayerInputProvider` 綁定左 Shift 鍵（`wasPressedThisFrame`，單幀觸發不是持續按住），玩家與 AI 共用同一個輸入介面的規則不變。
- 新增 `DodgeData.cs`（ScriptableObject，比照 `AttackData` 影格資料模式）：`distance`／`durationFrames`／`invulnerabilityFrames`／`cooldownFrames`，衝刺全程採固定速度（不做加速/減速曲線，符合「一旦觸發就全程投入」的手感），無敵幀範圍 clamp 在 duration 之內。
- 新增 `DodgePhase.cs`（Idle/Dodging/Cooldown）與 `DodgeState.cs`：純 C# 狀態機，比照 `ComboAttackState` 的既有慣例，可在 EditMode 直接測試時序，不需要 Play。沒有移動輸入時觸發閃避會朝角色目前面向的反方向（後撤步），有輸入時朝輸入的攝影機相對方向閃避；Cooldown 期間按鍵完全無效，不會插隊搶到下一次閃避。
- `CharacterMovement.cs` 整合 `DodgeState`：Dodging 期間完全接管水平移動（略過原本的加速/減速輸入邏輯），朝閃避方向轉向；非 Dodging 時行為與之前完全一致。新增 `CurrentDodgePhase`／`IsDodgeInvulnerable` 唯讀屬性供之後的系統查詢。
- 新增 `DodgeStateTests.cs`（EditMode，7 個測試）涵蓋：無輸入不動作、觸發後正確進入 Dodging 並依方向給出速度、方向會被正規化、無敵幀在 Dodging 期間為真、Duration 結束後轉 Cooldown 且無敵幀消失、Cooldown 期間按鍵不會插隊、沒有 `DodgeData` 時安全地維持 Idle。新增 `DodgeMovementTests.cs`（PlayMode，3 個測試）驗證：無輸入觸發閃避時角色確實後撤位移且立即無敵、經過完整 duration+cooldown 後回到 Idle 且無敵幀解除、Cooldown 期間持續按住不會插隊觸發下一次閃避。
- 新增 `Assets/_Project/Settings/DodgeData.asset`（預設 3 單位／12 影格＝0.2 秒衝刺、全程無敵、20 影格＝約 0.33 秒冷卻，合理起步值待實際 Play 手感調整），`GreyboxSceneBuilder.cs` 與新的一次性修正腳本 `FixDodgeSetup.cs` 都已同步建立並寫入 `CharacterMovement.dodgeData`。
- 31 個 EditMode（原 24 + 新 7）＋ 15 個 PlayMode（原 12 + 新 3）測試全數通過。
- **已知限制**：閃避的無敵幀（`IsDodgeInvulnerable`）目前還沒有接到任何實際的傷害判定——Player 身上根本沒有掛 `Health` 元件（目前場景裡只有 TrainingDummy 會受傷，還沒有任何敵人會反過來打玩家），所以這個屬性目前只是「準備好、還沒人用」的狀態，等 Step ⑤ 近戰敵人 AI 讓玩家真的會被打時才需要接上（屆時要決定 `AttackResolver`／`Health.ApplyDamage` 怎麼查詢攻擊目標的無敵狀態）。閃避跟攻擊系統一樣互相獨立，攻擊中可以直接閃避、閃避中攻擊鍵仍會照常觸發連段狀態機（沒有互相打斷的邏輯）。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：按左 Shift 閃避的距離/速度/冷卻手感是否合理，之後可直接調整 `DodgeData.asset` 數值。

### Step 4：敵人鎖定 — ✅ 完成（2026-08-10）

目標：加入按鍵鎖定敵人，鎖定後攝影機轉向目標、角色轉向目標、WASD 相對鎖定方向做環繞移動——順便解決先前記錄的已知限制（第一人稱下攻擊方向跟著移動朝向走，不是跟著視角走）。

- `IInputCommand` 新增 `LockOnPressed`；`PlayerInputProvider` 綁 Q 鍵（單幀觸發，按一下鎖定/再按一下解鎖）。
- 新增 `Assets/_Project/Game/Targeting/` 資料夾：
  - `LockOnTarget.cs`：可鎖定物件的標記元件（`AimPoint` 讓目標指定實際看向的點，例如胸口高度，預設用自己的 Transform）。
  - `ILockOnSource.cs`：比照 `ICameraYawSource` 的既有模式，讓 `CharacterMovement`／`ThirdPersonCameraController` 透過可選的 `MonoBehaviour` 欄位查詢目前鎖定目標，不需要直接依賴 `TargetLockController`。
  - `TargetLockUtility.cs`：純邏輯（候選篩選＋角度數學），比照 `AttackResolver` 既有慣例，可在 EditMode 直接測試不需要 Play。`FindBestTarget` 挑選距離內、視角範圍內、離攝影機視線最近的候選；`ComputeLockOnYawPitch` 把「玩家位置→目標位置」的方向換算成跟滑鼠視角相同的 (yaw, pitch) 表示法，確保鎖定跟自由視角切換時攝影機的旋轉運算方式完全一致，不會有兩套邏輯互相打架。
  - `TargetLockController.cs`：按鍵觸發鎖定/解鎖，每幀驗證鎖定目標是否還有效（超出 `breakRange`、被摧毀、或 GameObject 被停用——`Health.ApplyDamage` 死亡時本來就會 `SetActive(false)`，所以「死掉自動解鎖」不需要額外程式碼，直接沿用既有機制）。
- `ThirdPersonCameraController.cs` 整合：鎖定時每幀改用 `ComputeLockOnYawPitch` 計算 yaw/pitch（忽略滑鼠 delta），解鎖後立刻恢復滑鼠視角；因為兩種模式共用同一份 `_yaw`/`_pitch` 欄位與同一套 `ComputeCameraPosition` 定位公式，`CharacterMovement` 讀到的 `YawDegrees` 永遠反映攝影機實際呈現的方向，不會重演先前 Cinemachine 版本「移動邏輯跟畫面呈現各自解讀」的舊 bug。
- `CharacterMovement.cs` 整合：鎖定時（且非閃避中）角色朝向永遠面向鎖定目標，不受移動輸入影響（站著不動也會轉向面對目標）；配合攝影機的 yaw 已經指向目標，WASD 的相機相對移動天然變成繞著目標環繞——完全重用既有的 `CameraRelativeDirection` 架構，不需要另外寫「環繞移動」邏輯。閃避方向優先於鎖定朝向（閃避中維持閃避自己鎖定的方向）。
- `GreyboxSceneBuilder.cs`：Player 新增 `TargetLockController`（`viewOrigin` 指向 Main Camera，讓候選篩選以攝影機視線為準，而非角色自身朝向），TrainingDummy 新增 `LockOnTarget`，`CharacterMovement`／`ThirdPersonCameraController` 都交叉接上 `lockOnSource`。新增一次性修正腳本 `FixTargetLockSetup.cs` 套用到既有場景。
- 新增 `TargetLockUtilityTests.cs`（EditMode，12 個測試）：候選篩選（距離內/範圍外/視角外/停用物件/無候選）、有效性檢查（範圍內/範圍外/已停用/已摧毀）、`ComputeLockOnYawPitch` 用「換算出的角度重新代回同一個旋轉公式，方向要對得上目標」的往返一致性驗證（刻意不去猜測 pitch 正負號的直覺答案，避免重演先前攝影機方向感的 bug），以及 pitch clamp、同位置回傳零角度等邊界案例。新增 `TargetLockControllerTests.cs`（PlayMode，4 測試）驗證按鍵鎖定/解鎖/目標停用自動解鎖/無候選時維持未鎖定。新增 `LockOnFacingAndCameraTests.cs`（PlayMode，2 測試）驗證 `CharacterMovement` 在鎖定時即使無移動輸入也會轉向目標、`ThirdPersonCameraController` 在鎖定時的 yaw 與實際鏡頭朝向確實對準目標方向。
- 43 個 EditMode（原 31 + 新 12）＋ 21 個 PlayMode（原 15 + 新 6）測試全數通過。
- **已解決**：先前記錄的已知限制「第一人稱下攻擊方向跟著移動朝向走，不是跟著視角走」——現在鎖定敵人後，角色朝向（進而攻擊方向）會直接對準鎖定目標，不再依賴移動輸入方向；未鎖定時的行為（含第一人稱站立不動時攻擊方向仍跟著移動朝向）維持原樣不變，這是合理的階段性解法，之後如果需要「未鎖定也能瞄準視角方向」再另外處理。
- **已知限制**：`viewOrigin` 目前固定用 Main Camera 的朝向做候選篩選角度，鎖定/解鎖之間沒有平滑過渡（攝影機會瞬間轉向，不是漸進補間），沒有「多目標循環切換」功能（同一個鎖定鍵只能鎖最近的一個，無法切換到範圍內其他候選）——這些都留給之後視實際 Play 手感決定是否需要。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：按 Q 鎖定訓練假人後攝影機/角色朝向是否正確、鎖定中環繞移動手感如何、解鎖後視角是否能正常恢復。

### Step 5：近戰敵人 AI — ✅ 完成（2026-08-10）

目標：讓場景裡的敵人真的會主動接近並攻擊玩家，並把 Step 3 閃避預留的無敵幀真正接上傷害判定。使用者確認這一步先用 `TrainingDummy`（沒有外觀的 Capsule）測試邏輯，敵人外觀留到之後再處理。

- 新增 `Assets/_Project/Game/AI/`：
  - `EnemyState.cs`（Idle/Chasing/Attacking）與 `EnemyBehaviorUtility.cs`：純邏輯的狀態判定（距離 vs 偵測範圍 vs 攻擊範圍），比照 `AttackResolver` 既有慣例，可在 EditMode 直接測試。
  - `EnemyAI.cs`：自己驅動 `CharacterController` 移動（沒有重用 `CharacterMovement`，因為那個元件帶有大量玩家專屬的概念——攝影機相對方向、閃避、鎖定朝向——套用在敵人身上不合理）。但它**有**實作 `IInputCommand`，純粹是為了讓 `PlayerCombat`（原樣掛到敵人身上重用）能讀到 `AttackPressed`，跑跟玩家完全一樣的影格資料連段判定管線——符合 `CLAUDE.md` 第 8 條「玩家與 AI 輸入共用同一個輸入介面」的規則精神，同時不強迫 AI 走玩家專屬的移動邏輯。敵人在攻擊範圍內即使沒有移動也會持續轉向面對玩家，避免玩家繞到側邊後敵人還對著空氣揮擊。
- `Health.cs` 新增 `IsInvulnerable` 屬性，`ApplyDamage` 在無敵時直接忽略傷害。`CharacterMovement.cs` 新增可選的 `health` 欄位，每幀把 `DodgeState.IsInvulnerable` 同步進去——這是 Step 3 就規劃好、當時特意留白的接點，現在終於有敵人可以攻擊玩家，這個連結才有意義。
- `GreyboxSceneBuilder.cs`：Player 新增 `Health`並接上 `CharacterMovement.health`；原本的 `CreateDummy()` 改寫成 `CreateEnemy()`——`TrainingDummy` 現在有 `CharacterController`、`EnemyAI`（`detectionRange=8`／`attackRange=2`／`moveSpeed=2`，合理起步值）、重用的 `PlayerCombat`（掛一個新的 `EnemyAttack.asset`，傷害 5，比玩家的連段傷害低，讓玩家在正面對決中理論上該贏，但這是未經實測手感的推測值）。新增一次性修正腳本 `FixEnemyAISetup.cs` 套用到既有場景（會先摧毀舊的靜態 `TrainingDummy` 再重建成敵人，因為舊物件的網格/碰撞體直接掛在根物件上、不是獨立的 "Visual" 子物件，直接原地改裝不如重建乾淨）。
- 新增 `EnemyBehaviorUtilityTests.cs`（EditMode，5 個測試）驗證狀態判定的邊界條件。新增 `EnemyAITests.cs`（PlayMode，5 測試）驗證：超出偵測範圍不動作、偵測範圍內會朝目標移動、攻擊範圍內停下並觸發 `AttackPressed`、沒有目標時安全維持 Idle、攻擊範圍內即使靜止也會轉向面對目標。`HealthTests.cs` 新增 2 個測試驗證 `IsInvulnerable` 會擋下傷害、恢復後傷害正常生效。`DodgeMovementTests.cs` 新增 1 個測試驗證 `CharacterMovement` 真的會把閃避的無敵狀態同步進指定的 `Health`。新增 `EnemyAttacksPlayerTests.cs`（PlayMode，2 個端到端測試）：驗證敵人真的能透過重用的 `PlayerCombat` 管線打到玩家的 `Health`、以及玩家閃避時傷害確實被擋下——這兩個測試合起來驗證了這次「串接」的成果，不只是各自元件單獨測試通過。
- 50 個 EditMode（原 43 + 新 7）＋ 29 個 PlayMode（原 21 + 新 8）測試全數通過。
- **已知限制**：敵人沒有外觀（沿用 Capsule），沒有巡邏/待機動畫；只有一種攻擊（沒有連段），數值都是合理起步猜測，未經實際 Play 手感調整；敵人死亡後（`Health` 停用 GameObject）不會有任何演出效果，直接消失。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：敵人 AI 手感（偵測/攻擊範圍是否合理、移動速度是否過快/過慢）、被敵人打到時的傷害/被閃避擋下是否符合預期。

### Step 5 之後追加：攝影機改為固定世界座標軸，移除第一人稱 — ✅ 完成（2026-08-11）

使用者要求攝影機改成「固定世界座標軸」（類 ARPG 俯視角度），並確認鎖定敵人時鏡頭不跟著轉（只有角色朝向會轉，沿用 Step ④ 既有邏輯）、移除 V 鍵第一人稱切換（Step 2 追加的功能，固定角度鏡頭不支援自由看向的第一人稱）。詳見 `CHANGELOG.md`／`KNOWN_ISSUES.md` 對應條目。

- `ThirdPersonCameraController` 只剩兩個固定角度欄位（`fixedYaw`／`fixedPitch`，預設 0°／45°），移除滑鼠輸入、第一人稱、鎖定時覆寫 yaw/pitch 三塊邏輯；`CharacterMovement` 的相機相對移動因此永遠相對同一組世界座標軸，不需要另外改動。
- 50 個 EditMode + 27 個 PlayMode 測試全數通過（連跑兩次確認跟本次改動相關的測試穩定全過；兩個既有的 `CharacterMovementTests` 間歇性失敗是已知的 headless batchmode 時序問題，非本次迴歸）。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：固定角度的俯視感是否符合預期。

### Step 5 之後多輪追加：攝影機重新設計＋移動手感調整＋跳躍 — ✅ 完成（2026-08-11，同一天多輪迭代）

使用者在同一天內針對攝影機視角與移動手感做了多輪迭代式調整，完整排查過程與每一步的教訓見 `KNOWN_ISSUES.md`／`CHANGELOG.md` 對應日期條目，這裡只列結論：

- **技術調查**：先用 `deep-research` 技能整理了一份現代 RPG 攝影機／移動控制做法研究，見 `Docs/Research/CAMERA_MOVEMENT_RESEARCH.md`。
- **移動手感**：加減速從等速直線（`MoveTowards`）改成緩動曲線（`Vector3.SmoothDamp`），轉向從等角速度（`RotateTowards`）改成緩動角度（`Mathf.SmoothDampAngle`）；放開移動鍵的減速時間幾經調整，目前是 `decelerationSmoothTime=0.05`（幾乎立即停止，仍保留一點點緩動）。
- **攝影機視角**：從最早的俯視固定角度，經過「角色視線水平高度」「真正第一人稱（`distance=0`，鏡頭釘死在角色眼睛位置只轉不動）」，最後定案為**滑鼠視角控制（RPG 風格，不需按鍵）＋小距離過肩視角（`distance=0.5`，接受一點點環繞感換取看得到角色）**。Play 模式會自動鎖定/隱藏游標，避免滑鼠移動「漏」到畫面外造成視角亂飄；Editor 裡按 Esc 可隨時解鎖。角色自己的模型（`Player/Visual`）現在是顯示狀態。
- **敵人**：`TrainingDummy`（白色訓練假人）不再主動追逐/攻擊玩家（`detectionRange=0`），比較符合「訓練假人」的定位；玩家仍可按 Q 鎖定它。
- **Player2**（機甲靜態看板，`DoNotShip`）：補上碰撞體（之前完全穿透）、新增緩慢隨機漫遊＋碰邊界自動折返的行為（`WanderMovement.cs`）、可被 Q 鎖定。
- **邊界**：新增 4 面看不見的邊界牆，玩家不會再走出地板範圍掉出世界外。
- **跳躍**：新增空白鍵跳躍（貼地才能跳，無雙跳），空白鍵原本兼職攻擊鍵（跟滑鼠左鍵重複）這次移除，攻擊維持只用滑鼠左鍵。**⚠️ 這個功能明確跟 `Docs/GAME_DESIGN_DOCUMENT.md` 目前寫的「垂直切片版本不含跳躍」牴觸**——使用者當下直接要求加入，AI 端已完成實作，但**還沒有回頭跟使用者確認是否要正式修改設計文件裡的這條範圍限制**，見下方「待確認」。
- **過程中修好的真實 bug**：一次手算四元數沒有正確歸一化，導致 Console 被 `Quaternion To Matrix conversion failed` 洗版、拖累互動效能（教訓：非乾淨角度的旋轉一律要透過 Unity API 算，不能手算三角函數）；`CharacterAnimatorLink` 對著停用的 Animator 硬呼叫 `SetFloat`，單一場 Play session 洗出兩萬多次警告、疑似是一次 Editor 卡死沒回應的直接原因（已修正為檢查 `isActiveAndEnabled`）；`Player` 座標／`Player2` 啟用狀態／地板座標多次被使用者在 Editor 裡操作時意外拖動或取消勾選（不是程式碼問題，已在 `KNOWN_ISSUES.md` 給操作建議）。
- 測試數量隨每一輪異動持續增加，目前（截至這輪）約 55 個 EditMode + 30 幾個 PlayMode 測試，新增涵蓋：攝影機定位公式、移動幀時間效能回歸、碰撞阻擋（玩家 vs 訓練假人／Player2）、Player2 漫遊邊界行為、玩家重生點落地穩定性、跳躍。
- **仍待使用者本人在互動式 Editor 中 Play 一次完整確認**：這一整輪的攝影機/移動/跳躍手感是否都符合預期（先前每一步都有請使用者確認，但由於同一天內迭代速度很快，建議最後再完整玩一輪做整體確認）。

### Step 5 之後再追加：把現有角色都呈現在場景中 — ✅ 完成（2026-08-12）

使用者要求把專案裡現有的角色都放進 `GreyboxTest` 場景，確認範圍分三塊：① Enemy 換上已有的 Quaternius Humanoid 外觀、② 076/077 Live2D 立牌加入 3D 場景、③ Quaternius Female 變體另建一個新角色。三項都已完成，細節見 `CHANGELOG.md` 同日的兩則條目（③原以為需要下載 Female 素材，實際發現檔案早就在專案裡，下載變成多餘的一趟）。`GreyboxTest` 場景現在同時有：Player（Maya）、Enemy（Quaternius Male Humanoid）、Player2（機甲，`DoNotShip`）、NatsuStandee／LucyStandee（076/077 Live2D 立牌，`DoNotShip`）、FemaleStandee（Quaternius Female，純靜態展示，未接任何邏輯）。
- **已知限制**：Enemy 跟 Female 站在一起的兩個 Quaternius Humanoid 都沒有動畫（bind pose 靜止），076/077 立牌是攝影機朝向 2D 立牌，不是真正的 3D 模型。
- **仍待使用者本人 Play 一次確認**：這幾個新角色的比例/位置/朝向看起來是否合理。

### Step 5 之後再追加：地板貼圖／背景景物／天空盒 — ✅ 完成（2026-08-12）

使用者要求幫 `GreyboxTest` 加上地板與背景畫面，確認範圍是地板貼圖＋邊界外背景景物＋天空盒三層都做，素材來源選擇免費可商用素材包（CC0：Poly Haven Stone Floor 地板貼圖、Quaternius Simple Nature Pack 背景景物），寫進 `GreyboxSceneBuilder.cs`（地板/背景地形/天空盒）與新增的 `BackgroundSceneryStandeeSetup.cs`（邊界外景物，兩段式模式同 `FemaleStandeeSetup.cs`）。純視覺美術層，沒有動到任何戰鬥/AI/移動邏輯，64 個 EditMode 測試全過。細節見 `CHANGELOG.md` 同日條目、素材登記見 `ASSET_LICENSES.md`。
- **已知限制**：`Skybox/Procedural` 尚未加進 Always Included Shaders；背景景物比例/密度、地板貼圖平鋪比例都是估計值。詳見 `KNOWN_ISSUES.md`。
- **仍待使用者本人 Play 一次確認**：這次全程只跑過 batchmode 驗證編譯與測試，沒有人眼看過實際渲染畫面。

### Step 5 之後再追加：攝影機改固定右肩視角＋移動改坦克式控制 — ⏪ 同日改回自由視角（2026-08-12）

使用者要求攝影機「永遠在角色右手邊肩膀上、跟角色同方向、決不會跑到角色左邊」。`ThirdPersonCameraController` 的 yaw 改成每幀讀角色自己的朝向（不是滑鼠獨立控制），`targetOffset` 隨角色 yaw 旋轉維持右肩位置；配合這個改動，`CharacterMovement` 未鎖定目標時的移動改成坦克式控制（A/D 轉向、W/S 前後），因為原本「攝影機驅動移動方向＋角色自動轉向面對移動方向」的邏輯跟「攝影機鎖定角色朝向」放一起會形成無限旋轉的迴圈 bug（PlayMode 測試抓到後才發現，已修正並補測試）。過程中意外發現並修正一個會影響正式遊戲的真實 bug：`CharacterController.minMoveDistance` 預設值在高幀率下會靜默丟棄移動（這個修正保留，不受下面的回退影響）。細節見 `CHANGELOG.md`／`KNOWN_ISSUES.md` 同日條目。
- 65 個 EditMode、37 個 PlayMode 測試通過（1 個因 TrainingDummy 已被使用者故意刪除而合理跳過）。
- **同一天使用者對結果不滿意，要求改回自由視角＋WASD 平移**（參考原神／鳴潮）：攝影機/移動已改回右肩視角實驗之前的設計（滑鼠自由環顧、WASD 相對攝影機平移、A/D 是左右平移不是轉向），這一步的右肩視角＋坦克控制設計**已不是目前狀態**，保留在這裡當歷史記錄。細節見 `CHANGELOG.md` 同日「攝影機/移動改回自由視角」條目。
- **仍待使用者本人 Play 一次確認**：改回自由視角後的手感是否符合預期。

### Step 5 之後再追加：攝影機加上可選的自動回正 — ✅ 完成（2026-08-12）

使用者提出完整規格：維持自由視角＋鏡頭相對移動，只在放開滑鼠一段時間且角色正在前後移動時，讓攝影機平滑靠回角色背後；一有滑鼠輸入立刻交還控制權，鎖定目標時跳過。`ThirdPersonCameraController` 新增 `enableAutoCenter`／`autoCenterDelay`（0.8 秒）／`autoCenterSpeed`（2）／`lockOnSource` 四個欄位，核心公式抽成純函式 `ComputeAutoCenterYaw()`。實測抓到純側移時會跟自動回正互相追逐、漂移 134.8 度的邊界情況，改成只在前後移動為主時才觸發（`CharacterMovement` 新增 `CurrentMoveInput` 公開屬性供判斷）。細節見 `CHANGELOG.md`／`KNOWN_ISSUES.md` 同日條目。
- 67 個 EditMode（含 3 個新增的 `ComputeAutoCenterYaw` 純函式測試）、37 個 PlayMode 測試通過。
- **仍待使用者本人 Play 一次確認**：自動回正的節奏（延遲/速度）手感、純前後移動時是否自然、純側移時維持不回正的手感。

### Step 5 之後再追加：新增 Player4（動漫風角色 Arisa，純靜態展示）— ✅ 完成（2026-08-12）

使用者要求「爬取免費的 3D 模型加入 Player4」，澄清風格為動漫風（像 Maya）、用途是之後可能做成敵人/可鎖定目標但目前先靜態展示。找到 Maya 同一位作者（3D動漫風角色屋 / 3D Anime Character Store）的「Arisa」模型，CC-BY 4.0、提供 FBX，比照 `PlayerMayaVisualSetup.cs` 的模式新增 `Player4AnimeVisualSetup.cs`：`(5,0,-8)` 加入獨立 `Player4` GameObject，材質轉 URP Lit、移除內嵌 Rigidbody/Collider/Camera 與原廠自帶腳本產生的 Missing Script 殘留，掛 `CapsuleCollider` 與 `LockOnTarget`。過程中一次算圖誤判「材質壞掉全黑」其實是背光角度問題，換角度後確認模型/貼圖/比例都正常。細節見 `CHANGELOG.md`／`KNOWN_ISSUES.md`／`ASSET_LICENSES.md` 同日條目。
- 67 個 EditMode、37 個 PlayMode 測試（2 個既有已記錄的 flaky、1 個 TrainingDummy 已知跳過，跟這次新增無關）確認過，前後兩輪（清 Missing Script 前後）數字一致。
- **已知限制**：目前沒有任何動畫播放/AI/戰鬥邏輯，Idle/Walk/Run/Jump/Fall 動畫都隨套件帶進來但沒有接上任何觸發腳本。
- **仍待使用者本人 Play 一次確認**：Player4 在互動式 Editor 光照下顯示是否正常、站姿/比例/位置是否符合預期。

### Step 5 之後再追加：Player4 轉為 AI 自主攻擊敵人＋鎖定鍵改滑鼠滾輪＋鎖定搜索改用角色朝向 — ✅ 完成（2026-08-12）

使用者要求「把 Player4 當作敵人開始製作 AI 自主攻擊模式，並且鎖定敵人從 Q 改為滑鼠滾輪點按，以角色1正面視線方向向量去搜索最近的敵人來鎖定」。三項改動範圍較大，先摘要受影響檔案/風險並取得使用者確認才動手（`CLAUDE.md` 第 9 條）。

- **`PlayerInputProvider.cs`**：`LockOnPressed` 從 Q 鍵改讀滑鼠中鍵（`Mouse.current.middleButton`，Input System 裡「滾輪點按」＝中鍵）。
- **`TargetLockController.viewOrigin`**：從攝影機改成 Player 自己的 `Transform`，讓鎖定搜索沿用既有的 `TargetLockUtility.FindBestTarget`（視線錐角內最近目標）邏輯，但視線來源換成角色1自己的朝向，不是自由視角攝影機的朝向——`GreyboxSceneBuilder.cs` 預設值與新增的一次性 `FixLockOnViewOriginToPlayer.cs`（套用到現有場景）都已更新。
- **`Player4EnemyAISetup.cs`**（新工具）：比照原本 `TrainingDummy` 的既有做法，把 Player4 的 `CapsuleCollider` 換成 `CharacterController`，加上 `Health`／`EnemyAI`（`target`=Player，維持類別預設的偵測/攻擊範圍 8/2，不像 `TrainingDummy` 當年刻意關掉偵測）／`PlayerCombat`（複用既有的 `EnemyAttack.asset`）。座標系統從「站死擺放」換成「CharacterController 中心點貼地」，比照 Maya 的 `VisualFeetOffset` 公式重算腳底偏移，用算圖（真正 GfxDevice＋順光角度）驗證腳確實貼地。
- **新增 `Player4EnemyIntegrationTests.cs`**（PlayMode，載入真實 `GreyboxTest` 場景）：驗證 Player4 的元件/欄位接線正確，並端到端確認玩家靠近後 Player4 真的會離開 Idle 追擊、追到範圍內真的會攻擊。
- 67 個 EditMode、39 個 PlayMode 測試（37 既有 + 2 新增）跑兩輪，兩輪的失敗都完全落在既有已記錄的 flaky 測試類別（`JumpTests`／`WalkingIntoPlayer2_DoesNotPassThrough`，跟這次改動的檔案無關），新增的 2 個測試兩輪全過。
- **已知限制**：Player4 的移動沒有接 `CharacterAnimatorLink`，追擊/攻擊時動畫不會跟著播放走路動畫；攻擊傷害沿用 `EnemyAttack.asset` 既有數值，沒有另外調校。詳見 `KNOWN_ISSUES.md`。
- **仍待使用者本人 Play 一次確認**：滑鼠中鍵鎖定手感；角色朝向鎖定搜索的手感（站著不動轉鏡頭看敵人鎖不到，是否符合預期）；Player4 實際追擊/攻擊的節奏與傷害手感。

### Step 5 之後再追加：角色1／Player4 頭頂紅色血條（100 HP，攻擊命中一次扣 10）— ✅ 完成（2026-08-12）

使用者要求「幫角色1和角色4頭頂加上紅色血條100滴血 攻擊命中一次扣10滴血」。新增 `Live2DAction.UI` 命名空間：`HealthBarUtility.ComputeFillAmount`（純函式）＋ `WorldSpaceHealthBar`（World Space Canvas 上的紅色 `Filled` Image，`Update()` 寫入血量比例、`LateUpdate()` 對齊 `Camera.main` 旋轉）。新增 `HealthBarSetup.cs` 在 Player／Player4 底下各生成血條（位置公式從 `CharacterController` 實際高度算出，不寫死座標，比照 `PlayerMayaVisualSetup`／`Player4EnemyAISetup` 既有做法）。`Health.MaxHealth` 本來就是 100，沒有改動。新增一次性 `FixAttackDamageToTen.cs` 把所有 `AttackData`（`LightAttack1/2/3`／`EnemyAttack`）的傷害統一改成 10——**這會改變既有連段遞增設計（原本 8/10/16）**，是使用者這次的明確要求，不是側面影響。細節見 `CHANGELOG.md`／`KNOWN_ISSUES.md` 同日條目。
- 新增 `HealthBarUtilityTests.cs`（EditMode，6 個）、`WorldSpaceHealthBarTests.cs`（PlayMode，2 個：孤立情境下扣血後 `fillAmount` 正確更新、真實場景裡 Player／Player4 都有正確接線的血條）。73 個 EditMode、41 個 PlayMode 測試（僅 1 個既有已記錄的 flaky `JumpTests` 失敗，跟這次改動無關）確認過。
- **已知限制**：血條大小/邊距是估計值，沒有人眼在互動 Editor 裡確認過比例觀感；World Space Canvas 沒有接 `GraphicRaycaster`，血條純顯示不能點擊互動。
- **仍待使用者本人 Play 一次確認**：血條大小/位置/跟著鏡頭轉動的手感；統一 10 點傷害後的戰鬥節奏（10 下打死一個角色）是否符合預期。

### Step 5 之後再追加：修正「很靠近敵人時角色1突然消失，畫面定格」— ✅ 完成（2026-08-12，真實 bug 回報）

使用者實際 Play 後回報這個 bug。用診斷測試重現：Player 走向 Player4 時 Y 座標會在約 1 秒內從 0.58 爬升到 1.66，之後卡住來回震盪——根因是 Unity `CharacterController.stepOffset`（預設 0.3）讓互推的兩個角色其中一個爬上對方的膠囊體圓頂，卡在對方頭頂附近，這個場景又沒有攝影機防穿模，讀起來就像「角色消失、畫面定格」。修法：`GreyboxSceneBuilder.cs`／`Player4EnemyAISetup.cs` 新建的 `CharacterController` 都把 `stepOffset` 設成 0，新增一次性 `FixCharacterControllerStepOffset.cs` 套用到現有場景。順便修正排查過程中發現的次要 bug：`PlayerCombat.ResolveActiveHit` 在真正貼身距離會打空（判定球只放在 Range 距離處，貼身時直接飛過目標），改用 `Physics.OverlapCapsule` 涵蓋整個攻擊距離。細節見 `CHANGELOG.md`／`KNOWN_ISSUES.md` 同日條目。
- 新增永久回歸測試 `CharacterCollisionBlockingTests.WalkingIntoPlayer4_DoesNotClimbOnTop`（斷言 Y 漂移 < 0.2）。73 個 EditMode、42 個 PlayMode 測試（40 過、1 個既有已記錄的 flaky、1 個 TrainingDummy 已知跳過）確認過。
- **已知限制**：沒有幫攝影機加防穿模邏輯（既有限制），這次修的是「避免角色被推到頭頂高度」從根本上避開這個情境，不是治本攝影機穿模本身。
- **仍待使用者本人 Play 一次確認**：實際走近 Player4 確認真的不會再消失/卡住；貼身近戰命中手感是否變得比較合理。

### Step 5 之後再追加：攝影機加上真正的防穿模＋血條位置/大小修正 — ✅ 完成（2026-08-12）

使用者把攝影機 `distance` 調到 2（自己在 Editor 裡調的）後回報靠近 Player4 還是會消失，並問是否血量計算有問題；同時回報血條太低（應該在頭部上方）、要再小一點、要能清楚看到血條隨傷害減少。用多支診斷測試排除了血量計算問題（扣血邏輯完全正常），確認真正根因是這個專案從頭到尾沒有攝影機防穿模邏輯（既有已知缺口，這次終於實作）：`ThirdPersonCameraController` 新增 `enableCameraCollision`（預設開）＋ `Physics.SphereCastAll`，撞到東西就把攝影機拉到障礙物前面。血條改成量測 `Visual` 底下 Renderer 的實際世界座標邊界（不是 `CharacterController` 高度，兩者對不上——碰撞膠囊只有 1 單位高，遠比角色視覺高度矮），尺寸從 `(0.8,0.12)` 縮到 `(0.5,0.06)`。細節見 `CHANGELOG.md`／`KNOWN_ISSUES.md` 同日條目。
- 新增 `ThirdPersonCameraControllerTests`（EditMode，4 個 `ClampDistanceForObstruction` 純函式測試）、`ThirdPersonCameraObstructionTests`（PlayMode，2 個：真實 Physics 查詢確認攝影機會被拉近/無障礙物時維持原距離）。77 個 EditMode、44 個 PlayMode 測試（41 過、2 個既有已記錄的 flaky、1 個 TrainingDummy 已知跳過）確認過。
- **過程中的插曲**：batchmode Unity 連續卡死兩次（Editor 自己的啟動流程，不是這次程式碼問題），強制關閉後忘記清 `Temp/UnityLockfile` 等殘留鎖檔導致又卡一次——已記進 `KNOWN_ISSUES.md` 給下次參考。
- **仍待使用者本人 Play 一次確認**：靠近 Player4 是否真的不會再消失；血條新的位置/大小是否符合預期，攻擊時能否清楚看到血條隨傷害減少。

### Step 5 之後再追加：找到「角色消失」真正根因＋玩家死亡原地重生 — ✅ 完成（2026-08-12）

使用者回報攝影機防穿模修完後「角色依舊消失」。這次使用者的 Editor 開著沒辦法用命令列排查，改成請使用者截圖 Console＋Hierarchy——Console 只有 10 則已知警告（排除了「兩萬則警告洗死 Editor」的可能），但 Hierarchy 裡 `Player` 那一列是灰色的（Unity「已停用」樣式）。真正根因：`Health.ApplyDamage` 血量歸零時本來就會 `gameObject.SetActive(false)`，但這個專案完全沒有處理「玩家死亡後怎麼辦」——沒有重生、沒有 Game Over，Player 一關掉，掛在它身上的 `CharacterMovement`／`PlayerInputProvider` 全部停止運作，按什麼都沒反應，讀起來就像畫面凍結（不是引擎卡死，是遊戲設計上真的沒做死亡處理）。同一天稍早修的爬牆/貼身攻擊 bug 反而讓 Player4 更容易真的打死玩家，暴露了這個原本就存在的破洞。使用者選擇「原地重生，血量補滿」。新增 `Health.ResetHealth()`＋ `PlayerRespawnController.cs`（掛在新建的 `GameManager` 上，不能掛在 Player 自己身上——`Health.ApplyDamage` 是先觸發死亡事件才緊接著關掉 GameObject，掛在 Player 上的 Coroutine 會被一起砍掉）。細節見 `CHANGELOG.md`／`KNOWN_ISSUES.md` 同日條目。
- **過程中抓到自己寫的一個真實 bug**：第一版用 `OnEnable()` 訂閱死亡事件，但欄位是在 `AddComponent()` 之後才接上（不管是編輯器工具還是測試都是這樣），訂閱當下參照還是 null，永遠訂閱不到——`PlayerRespawnControllerTests` 第一次跑就抓到，改成跟這個專案其他地方一樣的輪詢寫法解決。
- 新增測試：`HealthTests.cs`（EditMode，2 個 `ResetHealth`）、`PlayerRespawnControllerTests.cs`（PlayMode，驗證死亡後 1 秒內原地重生、血量補滿）。79 個 EditMode、45 個 PlayMode 測試（42 過、2 個既有已記錄的 flaky、1 個 TrainingDummy 已知跳過）確認過。
- **已知限制**：只有 Player 有重生，Player4／未來的敵人死亡還是維持原本的「關掉」語意；重生延遲 0.5 秒沒有人眼確認過手感。
- **仍待使用者本人 Play 一次確認**：故意讓角色1被 Player4 打死一次，確認真的會原地重生、血量補滿，不會再卡住。

### Step 5 之後再追加：排查血條沒扣血的回報＋重生延遲改 5 秒 — ✅ 完成（2026-08-12）

使用者回報「被攻擊時血量條貼圖不會扣」，並要求重生延遲從 0.5 秒改成 5 秒。**第一輪排查方向錯了**：三支診斷測試只驗證了 `fillAmount` 這個數值有沒有正確更新，數值正確就誤判「沒問題」，沒有實際去看畫面渲染結果。重生延遲改成 5 秒：`PlayerRespawnController.respawnDelaySeconds` 類別預設值改了，並重跑 `PlayerRespawnSetup.Apply()` 讓場景資料同步（這個欄位第一次 `AddComponent` 時就序列化了實際數值，光改程式碼不會回頭更新場景）。細節見 `CHANGELOG.md`／`KNOWN_ISSUES.md` 同日條目。
- 79 個 EditMode、46 個 PlayMode 測試（42 過、2 個既有已記錄的 flaky、1 個 TrainingDummy 已知跳過）確認過。

### Step 5 之後再追加：真正找到血條不會扣的根因（`Image.Type.Filled` 沒接 Sprite）— ✅ 完成（2026-08-12）

使用者更正說明：不是血量計算問題，是畫面上的血條貼圖本身沒有視覺變化，並附截圖佐證。改用真正 Play 模式截圖比對（滿血 vs 50% 血量），發現兩張圖完全一樣——真正根因：Unity 的 `Image.Type.Filled` 沒有指定 `Sprite` 的話，`fillAmount` 數值完全不影響畫面渲染（這就是為什麼上一輪只讀屬性驗證「看起來沒問題」）。修法：`HealthBarSetup.cs` 補上 `image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd")`（Unity 內建預設 UI 圖片）。副作用：血條外觀從方形變圓角橢圓形。新增測試斷言 `Assert.IsNotNull(fillImage.sprite, ...)`鎖住這類 bug。細節見 `CHANGELOG.md`／`KNOWN_ISSUES.md` 同日條目。
- 79 個 EditMode、46 個 PlayMode 測試（43 過、2 個既有已記錄的 flaky、1 個 TrainingDummy 已知跳過）確認過，用真正 Play 模式截圖驗證過修好了。
- **教訓**：UI 元件「數值正確」跟「畫面正確」是兩件事，之後排查 UI 相關回報一定要截圖比對實際渲染結果，不能只驗證程式碼屬性。
- **仍待使用者本人 Play 一次確認**：血條現在應該會隨傷害正確縮短；橢圓形外觀是否可以接受，重生延遲現在是 5 秒。

### Step 5 之後再追加：攻擊命中特效 — ✅ 完成（2026-08-12）

使用者確認血條修好，接著要求「攻擊特效」，澄清為命中特效（粒子/閃光，命中點出現，不是揮擊軌跡或畫面震動）。順便確認 Maya／Arisa 都沒有真的攻擊動畫（Animator Controller 只有 Idle/Walk/Run/Jump/Fall/Pose，其他狀態引用的動畫檔案沒複製進專案），維持用 `AttackPoseVisualizer` 程式驅動揮擊角度。`AttackResolver.ResolveHits` 從只回傳命中數改成回傳實際命中點座標（`List<Vector3>`），`PlayerCombat` 新增 `hitEffectPrefab` 欄位，命中時在每個命中點生成一次。新增 `HitEffectSetup.cs` 純程式產生 `ParticleSystem` 預置物（球形爆發、淡黃白色、URP Additive 混合、放完自動銷毀），不需要美術素材，存成 `Assets/_Project/VFX/HitEffect.prefab` 並接到 Player／Player4。細節見 `CHANGELOG.md`／`KNOWN_ISSUES.md` 同日條目。
- 新增測試：`AttackResolverTests`（改 4 個既有 + 新增命中點驗證）、`PlayerCombatHitEffectTests`（PlayMode，命中生成特效／沒接特效不影響傷害）。80 個 EditMode、48 個 PlayMode 測試（44 過、2 個既有已記錄的 flaky、1 個 TrainingDummy 已知跳過）確認過，用真正 Play 模式截圖驗證特效跟血條同步正確。
- **已知限制**：粒子外觀是方塊狀（沒接柔邊圓形貼圖），能用但不夠精緻。
- **仍待使用者本人 Play 一次確認**：命中特效的時機/大小/顏色是否符合預期，方塊狀外觀能否接受。

### Step 5 之後再追加：攻擊範圍 Gizmo 視覺化 — ✅ 完成（2026-08-12）

使用者問「如何看到兩個角色的攻擊範圍」，`PlayerCombat.cs` 新增 `OnDrawGizmosSelected()`，畫出跟 `ResolveActiveHit` 實際查詢一致的膠囊範圍，連段三段紅→橙→黃區分，Scene 視窗選取角色即可看到。純 Editor 視覺化，不影響任何遊戲邏輯。順便確認 Maya／Arisa 都是 Humanoid 骨架（`animationType: 3`），為下一步找免費動畫鋪路。84 個 EditMode、48 個 PlayMode 測試（45 過、2 個既有已記錄的 flaky、1 個 TrainingDummy 已知跳過）確認過。

### Step 5 之後再追加：真的攻擊動畫（Mixamo，取代 AttackPoseVisualizer）— ✅ 完成（2026-08-12）

使用者問「可否透過手戳程式碼創造攻擊動作」，說明程式手刻（品質上限低）vs. 找免費 Mixamo 動畫（品質好很多，需要額外工作量）的取捨後，使用者選擇 Mixamo。用瀏覽器工具導到 mixamo.com，使用者本人已登入的 Adobe 帳號跳出兩步驟驗證提示，選擇跳過（不擅自幫使用者決定帳號安全設定），取得下載內容確認後下載 3 個免費動畫（Cross Punch/Hook Punch/Uppercut，Without Skin 格式）。新增 `CombatAnimationImportSetup.cs`（設定 Humanoid 匯入）＋ `CombatAnimatorSetup.cs`（程式碼直接在 Maya／Arisa 的 Animator Controller 各自新增 `Attack1/2/3` 狀態、Trigger 參數、AnyState 轉場，兩隻角色共用同一組動畫，靠 Humanoid Retargeting）＋新元件 `CharacterAttackAnimationLink.cs`（取代 `AttackPoseVisualizer`，每幀讀 `PlayerCombat.ComboIndex` 觸發對應 Trigger）。細節見 `CHANGELOG.md`／`KNOWN_ISSUES.md`／`ASSET_LICENSES.md` 同日條目。
- 新增測試：`CharacterAttackAnimationLinkTests`（EditMode，index→trigger 對應）、`CharacterAttackAnimationLinkIntegrationTests`（PlayMode，載入真實場景驗證按攻擊後 Animator 真的轉狀態——過程中發現 PlayMode 測試組件不能用 `UnityEditor` API，改用真實場景驗證解決）。84 個 EditMode、49 個 PlayMode 測試（47 過、1 個既有已記錄的 flaky、1 個 TrainingDummy 已知跳過）確認過，用真正 Play 模式截圖驗證攻擊動畫真的播放（揮拳姿勢，不是待機/T-pose）。
- **已知限制**：動畫時長跟 frame data 沒有互相對齊；截圖裡 Maya 材質看起來偏白，懷疑是算圖角度問題，需要人眼確認。
- **仍待使用者本人 Play 一次確認**：三段連段動畫的手感/時機；Maya 材質顯示是否正常。

### Step 5 之後再追加：攻擊距離調整說明＋Player2 補上血條與受擊 — ✅ 完成（2026-08-13）

使用者問「如何調整攻擊距離」（純說明：改 `Assets/_Project/Settings/Combat/` 底下 `AttackData` 資產的 `Range`／`Radius` 欄位即可，不用碰程式碼，可用既有的 Gizmo 直接在 Scene 視窗確認），並要求「幫我讓player2也有血條 也能受擊，但是他不會自主攻擊」。新增 `Player2DamageableSetup.cs`：Player2 補上 `Health` 元件（本來就有 `CapsuleCollider`，`AttackResolver` 找 `IDamageable` 本來就照 collider 所在物件找，不需要其他改動）跟血條（複用 `HealthBarSetup` 既有邏輯，改成 `internal` 給外部工具重用），**刻意沒有加 `PlayerCombat`／`EnemyAI`**——維持純被動、只挨打不反擊。細節見 `CHANGELOG.md`／`KNOWN_ISSUES.md` 同日條目。
- 新增測試：`WorldSpaceHealthBarTests.Player2_HasHealthBarAndCanBeDamaged_ButHasNoAttackCapability`（PlayMode，驗證血條接線正確、沒有攻擊元件、真的會扣血）。84 個 EditMode、50 個 PlayMode 測試（47 過、2 個既有已記錄的 flaky、1 個 TrainingDummy 已知跳過）確認過，用算圖驗證 Player2 頭頂血條正確顯示。
- **仍待使用者本人 Play 一次確認**：Player2 血條位置/大小；被打時扣血是否正常、確認不會反擊。

### Step 5 之後再追加：Player2 死亡後也能復活 — ✅ 完成（2026-08-13）

使用者要求「設計player2可以復活」。既有的 `PlayerRespawnController`（2026-08-12 為 Player 而建）邏輯本身沒有 Player 專屬內容，因此更名為通用的 `RespawnController`（欄位 `player`/`playerHealth` → `target`/`targetHealth`；用 `mv` 同時搬動 `.cs`/`.cs.meta` 保留 GUID，避免既有場景元件變成「Missing Script」）後直接重用，而不是複製一份給 Player2。新增 `Player2RespawnSetup.cs`（`Tools/Live2DAction/Add Player2 Respawn Controller`），在 `GameManager` 上再掛一個 `RespawnController` 指向 Player2，沿用跟 Player 一樣的 5 秒延遲、原地滿血復活。細節見 `CHANGELOG.md`／`KNOWN_ISSUES.md` 同日條目。
- 新增測試：`Player2RespawnControllerTests`（PlayMode，2 個：Player2 單獨死亡復活、`GameManager` 上兩個 `RespawnController` 互不干擾）。84 個 EditMode、52 個 PlayMode 測試（48 過、3 個既有已記錄的 flaky、1 個 TrainingDummy 已知跳過）確認過；另外在真實 `GreyboxTest` 場景跑了一個暫時性 PlayMode 診斷測試直接驗證 Player2 死亡→復活的行為，通過後已刪除。
- **視覺驗證侷限**：想截圖驗證但主攝影機跟隨 Player、不會轉向 Player2，畫面看不出差異，改以測試斷言為準（見 `KNOWN_ISSUES.md` 同日條目）。
- **仍待使用者本人 Play 一次確認**：Player2 死亡後 5 秒等待感受是否合理；復活瞬間直接 `SetActive(true)`，沒有淡入效果，是否需要之後加。

### Step 5 之後再追加：修正 Player 復活失效（同日更名 `RespawnController` 造成的真實回歸）— ✅ 完成（2026-08-13）

使用者回報「現在角色1不會復活」——上面那則更名 `RespawnController` 的改動讓 Player 原本就存在的元件實例欄位資料變成孤兒（`target`/`targetHealth` 變 `null`），第一次修的時候又漏了考慮「接線工具找不到精準比對時該回收孤兒、不是無腦新增」，一度在 `GameManager` 上疊出 3 個 `RespawnController`（1 個永久失效）。修正兩支接線工具的比對邏輯（精準比對 → 回收孤兒 → 才新增），新增 `RespawnControllerCleanup.cs` 清掉已產生的孤兒元件，並新增 `RespawnControllerSceneWiringTests`（PlayMode，載入真實場景驗證 `GameManager` 上剛好 2 個、都正確接線）防止同類 bug 再次不被發現。細節見 `CHANGELOG.md`／`KNOWN_ISSUES.md` 同日條目。
- 84 個 EditMode、53 個 PlayMode 測試（50 過、2 個既有已記錄的 flaky、1 個 TrainingDummy 已知跳過）確認過，Player 現在確實會在死亡 5 秒後原地滿血復活。

### Step 5 之後再追加：鎖定目標改用鏡頭朝向判斷 — ✅ 完成（2026-08-13）

使用者要求「目前鎖定目標需要角色去面對敵人，能不能改為鼠標鏡頭面相來判斷?」——反轉 2026-08-12 當時「用角色自己面向判斷」的明確決定。`TargetLockController.viewOrigin` 原本就是可替換的 Transform 來源，不需要改判斷邏輯，只把場景接線從 Player 自己的 Transform 換成 Main Camera 的 Transform（攝影機旋轉本來就每幀同步滑鼠 yaw/pitch）。新增 `LockOnViewSourceSetup.cs`（`Tools/Live2DAction/Use Camera Facing For Lock-On`）套用到既有場景，`GreyboxSceneBuilder.cs` 也同步更新供之後重建用。範圍/距離判定仍量測自角色本人，鎖定後角色朝向轉向目標的既有行為不變。細節見 `CHANGELOG.md`／`KNOWN_ISSUES.md` 同日條目。
- 新增測試：`TargetLockControllerTests.LockOnPressed_ViewOriginFacesCandidateButCharacterFacesAway_StillLocksOn`（角色刻意背對目標、只有 `viewOrigin` 面向目標，驗證依然鎖得到）。84 個 EditMode、54 個 PlayMode 測試（51 過、2 個既有已記錄的 flaky、1 個 TrainingDummy 已知跳過）確認過。
- **仍待使用者本人 Play 一次確認**：實際轉鏡頭鎖定敵人的手感；`TargetLockController.Update()` 比 `ThirdPersonCameraController.LateUpdate()` 早一幀執行，鎖定判定用的攝影機朝向理論上落後約 16ms，正常應該感受不到。

### Step 5 之後再追加：修正敵人攻擊距離加長後「沒有隔空打到」＋角色碰撞體總體檢 — ✅ 完成（2026-08-13）

使用者自行把 `EnemyAttack.asset` 的 `Range` 調到 7.5（約原本5倍），實測回報沒感受到遠距離被打到，並要求檢查所有角色碰撞體。根因：`AttackData.Range`（判定膠囊多長）跟 `EnemyAI.attackRange`（AI 何時願意出手）是兩個獨立欄位，場景裡的 `attackRange` 還停在預設值 2，Player4 永遠要先走到貼身距離才會開始攻擊，長距離完全沒被用到。新增 `EnemyAttackRangeSync.cs`（`Tools/Live2DAction/Sync Player4 Attack Range To EnemyAttack Data`）動態同步兩者（`attackRange = Range - 0.5` 緩衝），保留使用者已調好的 `Range=7.5`／`Radius=1.5`。另外實際掃描場景確認 Player／Player4／Player2 都正確套用碰撞體＋`Health`，076/077 立牌與 FemaleStandee 沒有碰撞體是設計上的預期行為（純視覺展示，未接戰鬥邏輯）。細節見 `CHANGELOG.md`／`KNOWN_ISSUES.md` 同日條目。
- 新增測試：`EnemyAttackRangeSceneTests.Player4_AttacksPlayerFromRangeWithoutClosingToMeleeDistance_InRealScene`（PlayMode，載入真實場景，驗證 Player4 在 5 個單位遠處就能命中，不用先走近）。84 個 EditMode、55 個 PlayMode 測試（53 過、1 個既有已記錄的 flaky、1 個 TrainingDummy 已知跳過）確認過。
- **仍待使用者本人 Play 一次確認**：實際感受 Player4 現在的攻擊距離；`attackRange(7)` 很接近 `detectionRange(8)`，追逐感會很短（幾乎一發現就攻擊），如果覺得不夠自然可以再拉開兩者差距。

### Step 5 之後再追加：Player4 攻擊距離縮小到3倍＋加上死亡復活 — ✅ 完成（2026-08-13）

使用者實測上面那組 `Range=7.5` 的設定後回報「敵人離我離得很遠就開始原地揮拳」（判定距離遠超過揮拳動畫視覺長度，違和感明顯）＋「發現敵人死了不會復活」（Player4 先前是刻意設計成打倒即永久消失）。確認方向後：`EnemyAttack.asset` 縮小到 `Range=4.5`／`Radius=1`（3倍，使用者在 2~3倍／維持7.5 之間選了前者），重跑 `EnemyAttackRangeSync.cs` 同步 `attackRange` 到 4；新增 `Player4RespawnSetup.cs`，在 `GameManager` 加第三個 `RespawnController` 指向 Player4，跟 Player／Player2 一樣 5 秒後原地滿血復活——Player4 從此不會再被打死後永久消失。細節見 `CHANGELOG.md`／`KNOWN_ISSUES.md` 同日條目。
- 新增測試：`Player4RespawnControllerTests`（Player4 死亡復活）；`RespawnControllerSceneWiringTests` 擴充為驗證 3 個 `RespawnController`（Player/Player2/Player4）都正確接線。84 個 EditMode、56 個 PlayMode 測試（52 過、2 個既有已記錄的 flaky、1 個 TrainingDummy 已知跳過）確認過。
- **已知限制**：`Range=4.5` 仍比揮拳動畫視覺長度長一截，只是沒有 7.5 那麼誇張；要做到真正「看起來合理」的長距離攻擊，需要專屬的突進/特效動畫，不是純調數字能解決的。Player4 現在會無限復活，之後若要做「擊敗所有敵人才能過關」的關卡設計，需要另外處理判定條件。
- **仍待使用者本人 Play 一次確認**：新的攻擊距離違和感是否已經改善；Player4 死亡復活的節奏手感。

### Step 5 之後再追加：攻擊距離／警備距離用不同顏色 Gizmo 呈現 — ✅ 完成（2026-08-13）

使用者要求「能不能把 攻擊距離 警備距離 用不同顏色線條呈現嗎 角色1和4都要」。攻擊距離沿用既有的 `PlayerCombat` 膠囊 Gizmo（紅/橙/黃）不動，新增「警備距離」青色線框球：Player（角色1）對應 `TargetLockController.maxLockRange`（能偵測/鎖定敵人的範圍，使用者確認的對應方式），Player4 對應 `EnemyAI.detectionRange`（多遠會注意到玩家）——概念相同（「這個角色的感知範圍」）但方向相反，各自畫在自己的元件上，不集中寫成獨立工具腳本。純 Editor 視覺輔助，不影響任何執行期邏輯。細節見 `CHANGELOG.md` 同日條目。
- 84 個 EditMode 全過。沒有自動化截圖驗證——Gizmo 是 SceneView 疊加層，批次模式相機截圖截不到，延續先前攻擊距離 Gizmo 就是純靠使用者肉眼在 Scene 視窗確認的做法。
- **仍待使用者本人在 Scene 視窗確認**：選取 Player／Player4，青色圓圈半徑是否符合預期。
- **附帶發現（跟這次 Gizmo 改動無關）**：跑完整測試時發現 `EnemyAttack.asset` 又被改動（`range=1.5`），跟場景裡 Player4 的 `attackRange`(4) 又不同步，`EnemyAttackRangeSceneTests` 因此暫時失敗——已回報使用者確認要保留目前數值還是換回先前 3× 那組，細節見 `KNOWN_ISSUES.md`。

### Step 5 之後再追加：玩家／敵人攻擊距離 Gizmo 顏色分開＋判定頂端加實心標記 — ✅ 完成（2026-08-13）

使用者要求「我需要分開敵人與玩家的攻擊判定 攻擊距離物件 並且顏色都要有區別，最好攻擊判定頂端要有更明顯的視覺效果」。Player（LightAttack1/2/3）跟 Player4（EnemyAttack）原本共用同一組紅/橙/黃配色，Player 第一段連段（紅）跟 Player4 唯一的攻擊（也是紅）視覺上分不出來。改成 Player 用綠色系、Player4 用紅色系（`GetComponent<EnemyAI>() != null` 判斷是不是敵人，重用 Player4 本來就有的既有機制，沒有新增欄位），跟警備距離的青色三方都不衝突；攻擊判定的「遠端」（實際打得到多遠的那一點）額外加一顆不透明實心球，在一堆半透明線框裡最顯眼。細節見 `CHANGELOG.md` 同日條目。
- 84 個 EditMode 全過；PlayMode 55 過 3 個失敗，2 個既有已記錄的 flaky、1 個是上面提到還沒解決的 `EnemyAttackRangeSceneTests`（跟這次顏色改動無關）。
- **仍待使用者本人在 Scene 視窗確認**：選取 Player／Player4，綠/紅配色跟頂端實心球是否符合預期。
- **同日修正 #1**：使用者附截圖回報「線條很多，紅色的有兩圈銜接，分不清楚邊界」——`Radius` 相對 `Range` 偏大時 near/far 兩顆線框球疊在一起糾結成一團。改成拿掉兩顆線框球，只在 `far` 畫一顆原始半徑的實心球當唯一邊界標記，中間 4 條線保留。
- **同日修正 #2**：使用者接著回報「頂端紅色區塊會遮擋線條影響判斷」——修正 #1 的全尺寸實心球把匯聚進來的線都蓋住了。改回線框圓（不遮擋，只有細線）當邊界標記，但這次只畫一顆（不是原本疊在一起的兩顆），中心加一個很小（15% 半徑）的實心點做強調，不會蓋住線條。兩次回報合起來的教訓：邊界標記本身要用不佔視覺面積的線框，「更明顯」的效果要靠額外的小裝飾物，不能拿邊界標記本身放大。細節見 `CHANGELOG.md` 同日兩則條目。
- **同日修正 #3（改成動態偵測）**：使用者回報「還是很難判斷 有沒有明確的視覺表達方式 能讓我知道究竟有沒有進入到攻擊範圍」——靜態線框本身不會因為有沒有人站進去而改變，肉眼在任意鏡頭角度都很難準確判斷。改成即時跑一次跟 `ResolveActiveHit` 完全一樣的 `Physics.OverlapCapsule` 查詢，真的有目標在範圍內就整顆變成醒目亮黃色實心球，沒有就維持線框圈＋小實心點——不再是靠眼睛估，是跟真正的傷害判定共用同一個查詢算出來的真實答案，Edit 模式（不用按 Play）跟 Play 模式都能用。過程中遇到一次批次模式環境卡住（`TrimDiskCacheJob` 連續卡 3 次，跟這次程式改動無關，重試後正常），放棄了額外的自動化邏輯驗證，改依賴既有編譯檢查＋`CombatPlayModeTests` 不受影響確認。細節見 `CHANGELOG.md` 同日條目。
- 84 個 EditMode 全過；`CombatPlayModeTests` 確認攻擊判定邏輯沒有受影響。
- **仍待使用者本人在 Scene／Game 視窗確認**：把角色移進移出攻擊範圍，觀察亮黃色是否即時切換。
- **同日修正 #4**：使用者回報「不要這樣包覆整個物體 看不見」——修正 #3 的全尺寸實心球會把站在範圍內的角色整個包住蓋住。徹底改成不填滿任何東西：邊界只用線框圓（`Radius` 準確大小，代表「碰到這條線一定被攻擊」）＋小參考點，有目標在範圍內時整組線條變亮黃色、邊界圓疊 2 圈模擬變粗，全程沒有任何實心區塊會遮擋角色。這是同一天第 4 次根據實際回饋調整這個 Gizmo，教訓見 `KNOWN_ISSUES.md`。細節見 `CHANGELOG.md` 同日條目。
- 84 個 EditMode 全過；`CombatPlayModeTests` 確認攻擊判定邏輯沒有受影響。

### Step 5 之後再追加：修正真實 bug——Gizmo 視覺呈現跟 Player4 實際攻擊判定不一致 — ✅ 完成（2026-08-13）

使用者回報「我已經盡到敵人範圍內，線條從紅色變成黃色，但敵人尚未作出攻擊」——不是 Gizmo 畫錯，是真的暴露出既有邏輯 bug：Gizmo／`ResolveActiveHit` 用 `Physics.OverlapCapsule`（真正可達距離是 `Range+Radius`），`EnemyAI` 自己決定要不要攻擊卻是單純球體距離判斷（`attackRange`，獨立欄位，需要手動同步），兩套判斷用的形狀根本不一樣。改成架構性修正：`EnemyAI` 新增可選 `combat` 欄位，接上後每一幀直接從 `PlayerCombat.PrimaryAttack`（新增公開屬性）即時算出 `Range+Radius` 當攻擊距離，不再有獨立、會過期的數字（沒接時退回原欄位，向下相容）。新增 `Player4EffectiveAttackRangeSetup.cs`（`Tools/Live2DAction/Wire Player4 Effective Attack Range`）套用到 Player4。細節見 `CHANGELOG.md`／`KNOWN_ISSUES.md` 同日條目。
- 新增測試：`EnemyAITests.TargetBeyondAttackRangeButWithinCapsuleReach_StillAttacksWhenCombatWired`（直接驗證這次回報的情境修好）、`TargetBeyondAttackRange_WithoutCombatWired_StaysChasing`（確認沒接 `combat` 時舊行為不變）。套用後意外多修好兩個既有測試（`Player4EnemyIntegrationTests`／`WorldSpaceHealthBarTests.PlayerBar_UpdatesWhenPlayer4DamagesPlayer_InRealScene`，原本因同一根因默默失敗）。
- 84 個 EditMode 全過；58 個 PlayMode 測試（55 過、1 個既有已記錄的 flaky、1 個 `EnemyAttack.asset` 的 `Range` 仍偏小的已知未解決問題、1 個 TrainingDummy 已知跳過）確認過。

### Step 5 之後再追加：玩家連段攻擊 Range/Radius 套用敵人已調好的數值 — ✅ 完成（2026-08-13）

使用者要求「我調整好的敵人的參數配置，以一樣的公式和邏輯套用在player1身上」，確認只套用 `Range`／`Radius`（frame data／傷害維持 Player 自己的連段設計）。`LightAttack2`／`LightAttack3` 的 `range`／`radius` 從 1.5／0.75 改成 0.5／0.5，跟已經是這組數值的 `LightAttack1`／`EnemyAttack` 一致，三段連段起手/收招的節奏差異完全沒動。純資料變動，沒有碰程式碼。細節見 `CHANGELOG.md` 同日條目。
- 84 個 EditMode 全過；58 個 PlayMode 測試（54 過、2 個既有已記錄的 flaky、1 個既有已知未解決的 `EnemyAttackRangeSceneTests`、1 個 TrainingDummy 已知跳過），跟改動前完全一致，沒有新增任何失敗。
- **仍待使用者本人 Play 一次確認**：三段連段距離縮短後的手感；`Range+Radius=1.0` 剛好等於雙方預設半徑貼身時的下限，零緩衝，如果貼身偶爾打空這是最可能的原因。

### Step 5 之後再追加：新增 Player3——跟 Player 完全一樣的攻擊機制，但完全不會動、不會攻擊 — ✅ 完成（2026-08-13）

使用者要求「引入 Cross Punch.fbx，攻擊、動作判定、機制完全與p1一致，差別只在於他完全不會動，也不會攻擊」。檢查後發現該檔案跟專案裡已有的 `CrossPunch.fbx`（Player 自己 Attack1 用的）MD5 完全相同，沒有重複匯入。確認範圍（沿用 Player 的 Maya 模型、可受擊跟 Player2 一致）後，新增 `Player3TrainingDummySetup.cs`——`comboAttacks` 直接參照 Player 用的同一組 `LightAttack1/2/3.asset`（不是拷貝，之後調整會自動同步），視覺重用 Maya 的 prefab（天生共用同一個 Animator Controller，已有 Attack1/2/3 狀態）。靠 `inputSource` 留空保證永遠不會攻擊，沒有 `CharacterMovement`／`PlayerInputProvider`／`EnemyAI` 保證永遠不會動，用 `CapsuleCollider`（不是 `CharacterController`）。可受擊部分（Health／血條／LockOnTarget）沿用 Player2 的既有模式。細節見 `CHANGELOG.md` 同日條目。
- 新增測試：`WorldSpaceHealthBarTests.Player3_SharesPlayersExactCombatData_ButNeverMovesOrAttacks`（驗證血條接線、`comboAttacks` 三格都跟 Player 參照同一份資產、沒有移動/攻擊相關元件、位置與 ComboIndex 永遠不變、真的會被打）。算圖截圖確認 Maya 模型正確顯示、血條位置正確。
- 84 個 EditMode 全過；59 個 PlayMode 測試（55 過、2 個既有已記錄的 flaky、1 個既有已知未解決的 `EnemyAttackRangeSceneTests`、1 個 TrainingDummy 已知跳過），跟改動前一致。
- **仍待使用者本人 Play 一次確認**：Player3 目前放在座標 (5, ground+0.5, 0)，位置是否需要調整。

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
