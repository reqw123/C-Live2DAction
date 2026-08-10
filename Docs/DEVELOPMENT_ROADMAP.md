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
