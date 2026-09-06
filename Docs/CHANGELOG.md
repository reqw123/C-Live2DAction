# Changelog

## 2026-08-10 — Phase 0

- 建立全新獨立專案 `C:\Live2DAction`（git repo 初始化，`main` 分支）。
- 建立 `CLAUDE.md`、`README.md` 與 `Docs/` 全套文件骨架：`PROJECT_AUDIT.md`、`DEVELOPMENT_ROADMAP.md`、`GAME_DESIGN_DOCUMENT.md`、`TECHNICAL_DESIGN.md`、`ASSET_LICENSES.md`、`KNOWN_ISSUES.md`。
- 確認環境：Unity 6000.0.81f1（本專案採用）與 6000.5.7f1 皆已安裝；本專案選用 URP（`C:\Live2DFighter` 用 BiRP，兩者無關）。
- 發現並記錄阻塞項：`C:\question\live2d_my_like\models\076\`／`\077\` 為《Fairy Tail》同人 Live2D 模型，取得使用者確認僅作內部原型佔位，不得進入對外 Build。
- 尚未建立 Unity 專案本體（`Assets/`／`ProjectSettings/`／`Packages/`），排入 Phase 1。

## 2026-08-10 — Phase 1

- 建立 Unity 專案 `Live2DAction/`（6000.0.81f1 + URP），安裝 Input System 1.19.0、Cinemachine 3.1.2、AI Navigation 2.0.5、Test Framework 1.6.0。
- 新增 `Live2DAction.Runtime` 組件：`Core/`（`IDamageable`、`DamageInfo`、`Health`）、`Input/`（`IInputCommand`、`PlayerInputProvider`）、`Characters/`（`CharacterMovement`）、`Combat/`（`AttackData`、`AttackResolver`、`PlayerCombat`）。
- 新增 `GreyboxTest.unity` 場景（地板、掩體方塊、Player、TrainingDummy、第三人稱 Cinemachine 攝影機），已加入 Build Settings；由可重複執行的 `Assets/Editor/Bootstrap/GreyboxSceneBuilder.cs` 產生。
- 新增 8 個 EditMode 測試 + 2 個 PlayMode 測試，全數透過 `-batchmode -runTests` 驗證通過。
- 修正過程中的兩個真實 bug：(1) `Health.CurrentHealth` 原本依賴 `Awake()` 初始化，在 EditMode 測試裡 `Awake` 不會自動執行，改為比照 `C:\Live2DFighter` 既有的 lazy-init 模式；(2) `PlayerCombat`／`CharacterMovement` 原本在 `Awake()` 快取 `IInputCommand` 轉型，導致之後才指定的 `inputSource` 不生效，改為每次使用時即時轉型。
- **尚未驗證**：移動手感、攝影機視角、視覺呈現皆未經人眼在互動式 Editor 中 Play 確認，僅有自動化測試覆蓋（見 `KNOWN_ISSUES.md`）。

## 2026-08-10 — Phase 1 追加：Live2D 立牌視覺

- 匯入 Cubism SDK for Unity 5-r.4.2；複製 076 模型到 `Assets/_Project/Live2D/PlaceholderCharacter/`（僅供內部原型驗證，見 `ASSET_LICENSES.md`）。
- 新增 `Assets/_Project/Rendering/Shaders/CubismUnlitURP.shader`（URP 版 Cubism Unlit，SDK 內建版只支援 Built-in RP）、`CubismBillboard.cs`（永遠面向鏡頭）。
- `PlayerCombat`/`CharacterMovement` 之外新增 `Tools/Live2DAction/Replace Player Visual With Live2D Standee` 編輯器工具，把 Player 的 Capsule 視覺換成 076 立牌。
- 修正兩個問題：(1) 複製進本專案的 model3.json 有一個指向不存在檔案的 Physics 參照導致匯入報錯，已從這份複製的 json 移除該欄位；(2) 縮放公式一開始誤把 `CanvasHeight`（像素）當 Unity 單位，角色縮到看不見，改用 `CanvasHeight / PixelsPerUnit` 換算後角色恢復正常高度。
- 用命令列把 Camera.main 算圖進 RenderTexture 存 PNG 的方式做了目視驗證（`ScreenCapture.CaptureScreenshot` 在 batchmode 沒有 backbuffer 存不出東西），確認角色貼圖、比例、朝向都正確顯示。
- 已知限制：不支援 Mask 裁切；目前顯示的是 moc3 靜止綁定姿勢（帶著技能火焰特效，非真正待機動作）；Cubism `ToModel()` 產生的物件在 Play 模式下名字會變空字串（原因未查出，不影響功能）。詳見 `KNOWN_ISSUES.md`。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**，這次只有自動化截圖驗證。

## 2026-08-10 — Phase 1 追加：Humanoid 角色佔位

- 研究並比較線上可用的 3D Humanoid 模型來源（Mixamo／CC0 低多邊形套件／AI 生成模型），依使用者決定採用 CC0 套件。
- 下載 Quaternius「Universal Base Characters」Standard 版（CC0 1.0，122 MB，https://quaternius.itch.io/universal-base-characters ），已確認內附授權文字，登記進 `Docs/ASSET_LICENSES.md`。
- 複製 `Superhero_Male_FullBody.fbx` 與貼圖到 `Assets/_Project/Characters/Placeholder/UniversalBaseCharacters/`。
- 新增 `PlayerHumanoidVisualSetup.cs`（Tools/Live2DAction/Replace Player Visual With Humanoid Placeholder）：設定 Humanoid Rig、建立 URP Lit 材質、取代 Player 底下先前的 Live2D 立牌（Cubism SDK／shader／billboard 元件仍保留供劇情演出使用）。
- 用命令列算圖確認貼圖、比例、站姿正確顯示，無粉紅材質。目前無 Animator/動畫，Play 起來會是 T-pose。
- 已知限制：無動畫；Roughness 貼圖未套用；只接了 Male、無髮型。詳見 `KNOWN_ISSUES.md`。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**。

## 2026-08-10 — Phase 1 追加：Maya 動漫風角色佔位

- 使用者要求動漫風角色，把上面的 Quaternius Humanoid 角色降級為備用，改用 Sketchfab「3D動漫風角色屋」的 Maya（CC-BY 4.0，須署名，見 `Docs/ASSET_LICENSES.md`）。
- Sketchfab 下載需登入帳號，AI 不能代為登入，使用者本人完成登入後下載（`.fbx` 原始格式，29MB，內含完整 Unity 套件：Humanoid Rig 已設定好的 FBX、Animator Controller（Idle/Walk/Run/Jump/Fall 動畫）、13 個材質、Prefab）。
- 複製進 `Assets/_Project/Characters/Placeholder/MayaAnime/` 時保留原始 `.meta` 檔，讓 Prefab／Animator 內部 GUID 參照直接對上。
- 新增 `PlayerMayaVisualSetup.cs`（Tools/Live2DAction/Replace Player Visual With Maya (Anime)）：把 13 個 Standard shader 材質轉成 URP Lit（`_MainTex`→`_BaseMap`、`_Color`→`_BaseColor`），關閉 Animator 的 Apply Root Motion（避免跟 CharacterController 位移邏輯衝突），取代 Player 底下先前的 Humanoid 佔位。
- 用命令列算圖確認貼圖、比例、Idle 待機動作（非 T-pose）正確顯示，無粉紅材質。
- 已知限制：目前只有內衣、無服裝；Animator 移動參數未接線，走路/跑步動畫還播不出來；發布 Build 前必須加上 CC-BY 署名。詳見 `KNOWN_ISSUES.md`。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**。

## 2026-08-10 — 評估兩個來源不明的測試模型

- 使用者提供兩個 UUID 命名的 FBX 檔案（`fbx_9f3e955d-...`、`fbx_53e34751-...`），想當「2P 角色看板」用。AI 檢查後發現兩者匯出簽章完全一致（同版本 Blender、無骨架、無貼圖、精確地都是 100 萬三角面），判斷是同一產線輸出。
- 第一個（倒臥姿勢＋草帽，疑似既有版權角色公仔）：判定不採用，已從專案移除，未提交進 git。
- 第二個（高達風機甲，疑似既有機甲動畫作品設計）：使用者在收到同樣的風險警告後，明確表示已確認來源並自行承擔風險、要求保留。已依 076/077 的模式處理——複製進 `Assets/_Project/Characters/Placeholder/MechaModel_DoNotShip/`，登記進 `ASSET_LICENSES.md`「禁止進入對外 Build」表，列為新的高風險阻塞項（見 `KNOWN_ISSUES.md`）。只能當靜態看板（`Player2`，無骨架不能做動畫），套用預設 URP Lit 白色材質，用命令列算圖確認位置/比例/材質正常。

## 2026-08-10 — Phase 2 Step 1：移動控制驗證＋移動動畫接線

- 新增 `CharacterMovementTests.cs`（PlayMode，6 個測試）：驗證角色 1 前後左右移動控制方向正確、無輸入不漂移、面向正確轉向移動方向。
- 過程中發現並修正測試方法論問題：headless batchmode 下固定幀數或 `WaitForSecondsRealtime` 都無法可靠估計實際模擬時間，改成自訂迴圈依 `Time.realtimeSinceStartup` 累積到目標秒數，並依實測積分效率（約理論值 30%）重新校正判定門檻。
- `CharacterMovement` 新增 `CurrentHorizontalSpeed`／`MoveSpeed` 唯讀屬性。
- 新增 `CharacterAnimatorLink.cs`：把移動速度換算成 Maya Animator 的 `Speed` 參數（0/0.4/0.8/2 對應 Idle/Walk/Jog/Run 的 Blend Tree 門檻），5 個 EditMode 測試覆蓋換算邏輯。
- 新增 `WireCharacterAnimatorLink.cs`（Tools/Live2DAction/Wire Character Animator Link On Player）並執行，把元件掛到場景裡的 Player 上。
- 13 個 EditMode + 9 個 PlayMode 測試全數通過。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**手感與動畫轉換是否順暢。

## 2026-08-10 — 修正方向鍵移動 360 度畫圈的真實 bug

使用者實際 Play 後回報的第一個真實 bug：純按左/右移動會持續轉圈，不是直線移動。完整排查過程見 `KNOWN_ISSUES.md`，摘要：

- 前兩次診斷都錯了：`CinemachineOrbitalFollow.BindingMode` 調整、`CameraFollowAnchor` 中介物件，兩者都對症狀毫無效果，已從專案移除。
- 真正原因：`CharacterMovement` 讀的是攝影機「組合後」的 `Transform.forward`（含 `CinemachineRotationComposer` 為了追蹤平移中的角色而產生的瞄準修正），純橫移時這個瞄準角度本身就會自然掃動，跟角色朝向形成無限迴圈。
- 修法：新增 `ICameraYawSource`／`OrbitalCameraYawSource`，改讀攝影機軌道未經瞄準修正的原始角度（只受滑鼠控制），`CharacterMovement.CameraRelativeDirection` 改成接受 yaw 角度參數而非直接讀 `Camera.main.transform.forward`。
- 新增永久回歸測試 `CameraRelativeMovementRegressionTests.cs`（載入真實場景+真攝影機，防止此 bug 未來回歸）。
- 順帶修好一個測試隔離 bug：`CharacterMovementTests` 的 `[SetUp]` 改成清空場景所有根物件，避免跟其他會載入真實場景的測試互相污染。
- 13 個 EditMode + 10 個 PlayMode 測試全數通過，連續驗證 3 次無間歇性失敗。

## 2026-08-10 — 修正腳步滑行 + 移除 Cinemachine 改用自寫攝影機

使用者實際 Play 後再回報兩個問題，完整排查過程見 `KNOWN_ISSUES.md`，摘要：

- **腳步滑行**：`moveSpeed`（5）遠超過 Maya Locomotion Blend Tree 的最高門檻（2），確認動畫片段沒有可用的 Root Motion 可以反推正確值後，把 `moveSpeed` 降到 2 對齊 Blend Tree 門檻（`FixMoveSpeedForAnimation.cs`），並把 `CharacterAnimatorLink` 的速度換算簡化成直接 `Clamp`，不再做無根據的任意倍率縮放。
- **攝影機視角與角色朝向脫鉤**（使用者形容為「按左鍵人物往右跑、朝向正西方，像是視角沒對齊角色」）：五次針對 Cinemachine 的修法（`BindingMode` 調整、position-only anchor、移除 `CinemachineRotationComposer`、歸零阻尼、anchor 二次嘗試並直接驗證其旋轉鎖定）全部實測無效，且第五次的診斷數據直接跟 Cinemachine 套件原始碼的文件化行為矛盾。決定放棄 Cinemachine 的軌道/瞄準系統，改寫完全自己掌控的 `ThirdPersonCameraController.cs`（`Assets/_Project/Game/Camera/`）：直接讀滑鼠 delta 累加 yaw/pitch、每幀直接算位置與旋轉並套用，同時實作 `ICameraYawSource` 供 `CharacterMovement` 讀取同一個 yaw 值，兩者不可能再對不上。
- 移除場景與程式碼裡所有 Cinemachine 相關元件與參照：`CinemachineBrain`／`CinemachineCamera`／`CinemachineOrbitalFollow`／`CinemachineRotationComposer`／`CinemachineInputAxisController`、舊有的 `OrbitalCameraYawSource`／`CameraFollowAnchor` 腳本、`Live2DAction.Runtime.asmdef` 的 `Unity.Cinemachine` 參照。新增 `FixCameraCustomController.cs`（一次性場景修正工具）並更新 `GreyboxSceneBuilder.cs`（場景重建工具）改用新攝影機。
- 新攝影機刻意未實作牆壁/障礙物碰撞閃避（deferred，非本次需求範圍）。
- 12 個 EditMode + 10 個 PlayMode 測試全數重新驗證通過（含載入真實場景的 `CameraRelativeMovementRegressionTests`）。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：腳步是否貼地、滑鼠視角操作是否順手。

## 2026-08-10 — 修正攝影機修法本身帶出的「角色消失」bug

使用者套用上一版攝影機修法後回報「角色消失了，按方向鍵也沒反應」。完整排查過程見 `KNOWN_ISSUES.md`，摘要：

- 原因是 Maya 素材包裡藏著一個素材作者自己預覽用的內嵌攝影機（GameObject 名字與 tag 都叫 `MainCamera`，掛在角色脖子骨頭上），`FixCameraCustomController.cs` 原本用 `GameObject.FindWithTag("MainCamera")` 找攝影機，結果找到的是這顆假攝影機，導致真正的 Main Camera 完全沒被處理（舊 `CinemachineBrain` 留著、畫面卡住），新攝影機控制腳本則被誤裝到角色骨架上、跟動畫互相打架。
- 修法：`PlayerMayaVisualSetup.cs` 新增 `RemoveEmbeddedCameraRig()`，換裝 Maya 模型時自動清掉視覺階層裡所有內嵌 `Camera`；`FixCameraCustomController.cs` 改用 `GameObject.Find("Main Camera")`（依名稱）取代依 tag 查找。依序重跑三個編輯器工具重建乾淨場景。
- 教訓：用 tag 在場景裡找「唯一」物件時，不能假設專案自己建立的物件是該 tag 的唯一持有者，外部美術素材可能夾帶同 tag 的物件；改用明確名稱查找更保險。
- 12 個 EditMode + 10 個 PlayMode 測試全數重新驗證通過。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：角色是否正常顯示、方向鍵與滑鼠視角是否正常回應。

## 2026-08-10 — 攝影機改回固定視角，取消滑鼠自由視角

使用者要求「先將攝影機固定視角，並且明確 w/s/a/d 是控制角色前/後/左/右移動」，並要求參考網路上一般 3D 遊戲的做法。完整討論見 `KNOWN_ISSUES.md`，摘要：

- `ThirdPersonCameraController` 移除滑鼠輸入，改成固定的 `yawDegrees`/`pitchDegrees`（預設 0/25）；攝影機只跟隨角色位置平移，旋轉永遠不變。
- 驗算確認 W/A/S/D 與 `CameraRelativeDirection` 換算後對應：W=前進（遠離攝影機）、S=後退、A=畫面左、D=畫面右，且因攝影機角度固定，此對應永遠一致。
- `GreyboxSceneBuilder.cs`／`FixCameraCustomController.cs` 同步更新欄位寫入。
- 12 個 EditMode + 10 個 PlayMode 測試全數重新驗證通過。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**。

## 2026-08-10 — 改回原神風格滑鼠視角，並修正「大跨步到很遠距離」的懸空落地 bug

使用者要求參考原神的攝影機操作方式（滑鼠帶動視角、WASD 相對攝影機移動），並回報移動時角色會突然大跨步移動到很遠的地方。完整排查過程見 `KNOWN_ISSUES.md`，摘要：

- `ThirdPersonCameraController` 改回讀 `Mouse.current.delta` 驅動 yaw/pitch（不需按住按鍵），`GreyboxSceneBuilder.cs`／`FixCameraCustomController.cs` 同步更新欄位寫入。這次的滑鼠視角架構跟先前 Cinemachine 版本不同（只有一份 yaw/pitch 狀態同時決定攝影機旋轉與移動方向），不會重現當初 Body/Aim 兩個元件互相打架的畫圈 bug。
- 用暫時的診斷測試量測後發現「大跨步」其實跟攝影機/輸入無關：Player 的 `CharacterController.height` 曾被手動改成 1（原設計是 2），但重生 Y 座標沒有跟著調整，導致角色懸空在地板上方 0.5 單位、永遠碰不到地，重力持續累加到很大的下墜速度，一旦撞到任何東西就會被彈開一大段距離。
- 新增 `FixPlayerGroundedSpawn.cs`：從 Ground 的實際碰撞體邊界＋Player 目前的 `CharacterController` 尺寸動態反推正確重生高度（不是寫死常數），套用後重生 Y 從 1 改成 0.5，`GreyboxSceneBuilder.CreatePlayer()` 也同步改成動態計算，避免未來再度悄悄裂開懸空縫隙。
- 12 個 EditMode + 10 個 PlayMode 測試全數重新驗證通過。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：滑鼠視角手感、角色是否穩定貼地不再瞬移。

## 2026-08-10 — Phase 2 Step 2：三段普攻＋影格資料

- `AttackData.cs` 新增 startup/active/recovery/comboWindow 影格欄位（60fps 基準），新增純 C# 狀態機 `ComboAttackState.cs`（`AttackPhase.cs`）處理三段連段的時序與連段視窗判定，比照既有 `AttackResolver` 的純邏輯慣例可在 EditMode 直接測試。
- `PlayerCombat.cs` 改用 `AttackData[] comboAttacks` 陣列取代原本單一 `attackData` 欄位，每幀把狀態機推進一步，只在進入 Active 判定的那一步做一次 `Physics.OverlapSphere` + 傷害判定。
- 新增三個 `LightAttack1/2/3.asset`（`Assets/_Project/Settings/Combat/`）取代舊的 `TestPunch.asset`；`GreyboxSceneBuilder.cs` 與新的 `FixComboAttacksSetup.cs` 都已同步建立/寫入。
- 新增 `ComboAttackStateTests.cs`（EditMode，8 個測試），更新 `CombatPlayModeTests.cs` 配合新的陣列欄位與影格時序。
- 20 個 EditMode + 10 個 PlayMode 測試全數通過。
- 已知限制：Maya 目前沒有攻擊動畫（先做邏輯，動畫之後補，使用者已確認範圍）；攻擊時是否鎖定/減速移動尚未處理。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：連段輸入手感、連段視窗時機是否合理。

## 2026-08-10 — 新增第一/第三人稱視角切換

使用者要求把畫面做成第一人稱，確認範圍為可切換（保留第三人稱）、第一人稱先隱藏整個角色模型。

- `ThirdPersonCameraController` 新增 `CameraViewMode`，V 鍵切換第一/第三人稱；第一人稱直接把攝影機放在角色眼睛高度、不受距離/旋轉影響位置，第三人稱維持原本軌道公式。兩種模式共用同一份 yaw/pitch，不影響 `CharacterMovement` 的相對移動方向計算。
- 新增純位置計算靜態方法 `ComputeCameraPosition`（EditMode 可測），切換時透過 `visualToHide` 隱藏/顯示角色模型。
- 新增 `ThirdPersonCameraControllerTests.cs`（EditMode，4 測試）＋ `CameraViewToggleTests.cs`（PlayMode，2 測試）。`GreyboxSceneBuilder.cs`／`FixFirstPersonToggleSetup.cs` 同步寫入場景。
- 24 個 EditMode + 12 個 PlayMode 測試全數通過。
- 已知限制：第一人稱下攻擊方向仍跟著移動朝向走，不是跟著視角走，留待之後處理。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：V 鍵切換是否正常、眼睛高度是否合理。

## 2026-08-10 — Phase 2 Step 3：閃避

- `IInputCommand` 新增 `DodgePressed`（`PlayerInputProvider` 綁左 Shift），新增 `DodgeData.cs`（ScriptableObject 影格資料：距離/持續/無敵幀/冷卻）、`DodgePhase.cs`、純 C# 狀態機 `DodgeState.cs`（比照 `ComboAttackState` 慣例，EditMode 可測）。
- `CharacterMovement.cs` 整合閃避：Dodging 期間完全接管水平移動與朝向，非 Dodging 時行為不變；新增 `CurrentDodgePhase`／`IsDodgeInvulnerable` 唯讀屬性。
- 新增 `DodgeStateTests.cs`（EditMode，7 測試）＋ `DodgeMovementTests.cs`（PlayMode，3 測試）。`GreyboxSceneBuilder.cs`／`FixDodgeSetup.cs` 建立 `DodgeData.asset`（3 單位/12 影格衝刺、全程無敵、20 影格冷卻）並寫入場景。
- 31 個 EditMode + 15 個 PlayMode 測試全數通過。
- 已知限制：`IsDodgeInvulnerable` 尚未接到任何傷害判定（Player 目前沒有 `Health` 元件，還沒有敵人會反擊），留給 Step ⑤ 近戰敵人 AI 一起處理。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：閃避距離/速度/冷卻手感。

## 2026-08-10 — Phase 2 Step 4：敵人鎖定

- `IInputCommand` 新增 `LockOnPressed`（`PlayerInputProvider` 綁 Q 鍵）。新增 `Assets/_Project/Game/Targeting/`：`LockOnTarget`（可鎖定標記）、`ILockOnSource`（比照 `ICameraYawSource` 模式）、`TargetLockUtility`（純邏輯：候選篩選＋角度數學，EditMode 可測）、`TargetLockController`（按鍵鎖定/解鎖＋每幀有效性檢查，目標死亡時因為 `Health` 本來就會 `SetActive(false)` 而自動解鎖，不需要額外程式碼）。
- `ThirdPersonCameraController` 鎖定時改用 `ComputeLockOnYawPitch` 算 yaw/pitch（忽略滑鼠），`CharacterMovement` 鎖定時朝向永遠面對目標（閃避優先）；因為攝影機跟移動共用同一份 yaw，WASD 天然變成環繞鎖定目標移動，不需要另外寫環繞邏輯。
- `GreyboxSceneBuilder.cs`／`FixTargetLockSetup.cs` 把 `TargetLockController` 掛到 Player、`LockOnTarget` 掛到 TrainingDummy 並交叉接線。
- 新增 `TargetLockUtilityTests.cs`（EditMode，12 測試）、`TargetLockControllerTests.cs`（PlayMode，4 測試）、`LockOnFacingAndCameraTests.cs`（PlayMode，2 測試）。
- 43 個 EditMode + 21 個 PlayMode 測試全數通過。
- 已解決：先前「第一人稱下攻擊方向跟著移動朝向走」的已知限制——鎖定後角色朝向直接對準目標。
- 已知限制：鎖定/解鎖沒有平滑過渡、沒有多目標循環切換。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：按 Q 鎖定後攝影機/角色朝向與環繞移動手感。

## 2026-08-10 — Phase 2 Step 5：近戰敵人 AI

- 新增 `Assets/_Project/Game/AI/`：`EnemyState`/`EnemyBehaviorUtility`（純邏輯狀態判定，EditMode 可測）、`EnemyAI`（自己驅動 `CharacterController` 移動，同時實作 `IInputCommand` 讓 `PlayerCombat` 能原樣重用來跑跟玩家一樣的連段判定管線）。
- `Health.cs` 新增 `IsInvulnerable`，`CharacterMovement.cs` 每幀把閃避的無敵狀態同步進去——接上了 Step 3 當時特意留白的接點。
- `GreyboxSceneBuilder.cs`：Player 新增 `Health`，`TrainingDummy` 從靜態假人改寫成真正的 AI 敵人（`CharacterController`＋`EnemyAI`＋重用的 `PlayerCombat`＋新的 `EnemyAttack.asset`）。新增 `FixEnemyAISetup.cs` 套用到既有場景。
- 新增 `EnemyBehaviorUtilityTests.cs`（EditMode，5 測試）、`EnemyAITests.cs`（PlayMode，5 測試）、`EnemyAttacksPlayerTests.cs`（PlayMode，2 個端到端測試：敵人真的能打到玩家、閃避真的能擋下傷害）；`HealthTests.cs`／`DodgeMovementTests.cs` 各新增測試覆蓋無敵幀機制。
- 50 個 EditMode + 29 個 PlayMode 測試全數通過。
- 已知限制：敵人沒有外觀/動畫、只有單一攻擊、數值未經實測調整、死亡沒有演出效果。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：敵人 AI 手感、被攻擊/被閃避擋下是否符合預期。

## 2026-08-11 — 角色移動手感調整：加減速與轉向改用緩動曲線

使用者回報「角色移動控制不夠自然」，並先前用 `deep-research` 技能整理了一份現代 RPG 攝影機／移動控制研究（`Docs/Research/CAMERA_MOVEMENT_RESEARCH.md`）。本次範圍依照專案規則收斂到**移動手感單一項**（角色視線高度固定攝影機留待下一次單獨交付，不在本次變更內）：

- **問題**：`CharacterMovement.cs` 原本用 `Vector3.MoveTowards`（等速直線逼近目標速度）做加減速、`Quaternion.RotateTowards`（等角速度）做轉向朝向，兩者都是「固定速率、到達目標瞬間硬停」的曲線，沒有緩入緩出，讀起來偏機械化。
- **修法**：改用 `Vector3.SmoothDamp`（水平速度）與 `Mathf.SmoothDampAngle`（朝向 yaw），兩者都是業界第三人稱控制器常用的緩動技巧（漸近逼近目標，而非到點瞬間停止），對應研究報告裡「damping 曲線讓移動有重量感」的建議。欄位改名：`acceleration`/`deceleration` → `accelerationSmoothTime`/`decelerationSmoothTime`（預設 0.08s／0.12s），`rotationSpeedDegrees` → `rotationSmoothTime`（預設 0.1s）；數值語意從「每秒變化率」變成「逼近目標所需時間」，小值＝反應快。
- 閃避（Dodge）行為不變：Dodging 期間仍完全接管水平速度與朝向，只是新增在接管時把 `SmoothDamp` 的內部速度參考歸零，避免閃避結束後緩動曲線繼承閃避前的殘留速率造成一瞬間的曲率異常。
- `moveSpeed`（2，對齊 Maya Locomotion Blend Tree 門檻，避免腳步滑行）與 `gravity` 完全沒有變動，不影響先前修好的腳步滑行問題。
- 場景檔（`GreyboxTest.unity`）裡舊欄位（`acceleration: 20`／`deceleration: 25`／`rotationSpeedDegrees: 720`）沒有手動遷移——Unity 載入時會直接忽略這些已不存在的欄位名稱，套用腳本裡新欄位的預設值，行為等同直接套用本次的新手感設定。
- 3 個檔案的既有測試同步更新以配合新欄位名稱與緩動特性：`CharacterMovementTests.cs`（2 處 `SetField` 改名並調整數值）、`LockOnFacingAndCameraTests.cs`（`CharacterMovement_WithLockedTarget_FacesTargetEvenWithoutMoveInput` 原本只等一個 frame 就斷言朝向已收斂，改成等最多 0.2 秒真實時間再斷言——等角速度瞬間轉到位在單一 frame 內可行，緩動朝向需要非零的模擬時間才能收斂，理由同其他測試已有的「headless batchmode 單 frame 的 `Time.deltaTime` 可能趨近於零」註解）。
- 只動了 `CharacterMovement.cs`，`EnemyAI.cs` 的等角速度轉向（`rotationSpeedDegrees`）刻意不動——這次範圍限定在玩家角色手感，敵人是否要比照處理留到之後視需要再議。
- 50 個 EditMode + 27 個 PlayMode 測試全數重新驗證通過。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：加減速/轉向的緩動手感是否符合預期，`accelerationSmoothTime`／`decelerationSmoothTime`／`rotationSmoothTime` 三個數值都是合理起點猜測，非最終定案，可直接在 Inspector 上調整比對。
- **未涵蓋**（下次視需要另外交付）：角色視線高度固定攝影機（目前仍是俯視固定角度）；真正的走／跑速度分層（目前 WASD 只有單一 `moveSpeed`，沒有走路節奏）；Foot IK。

## 2026-08-11 — 攝影機改為角色視線水平高度

使用者在上面「移動手感調整」交付後立即回報「視角還是高拍」，要求「攝影機完全與角色視線平行」——這是研究報告（`Docs/Research/CAMERA_MOVEMENT_RESEARCH.md`）原本規劃、上一次刻意排除在範圍外的項目，這次單獨補上：

- `ThirdPersonCameraController.fixedPitch` 預設值從 45°（俯視）改成 0°（水平）；pitch=0 時相機會被放在跟 `targetOffset.y`（1.4，瞄準頭部/眼睛高度用）同樣的高度、正後方、完全水平看向角色，相機視線因此跟角色的視線水平面平行。
- `distance` 從 8 降到 3.5，第一輪用算圖確認水平角度正確後，使用者立刻回報「攝影機概念上要離角色很近，大概在後腦勺跨一個人的距離，才是真正的模擬人物走路視角」，於是再把 `distance` 從 3.5 進一步降到 **1**——很貼近後腦勺的距離，讓水平視角更接近角色自己的走路第一人稱視角觀感，而不只是「一個離得比較近但仍是旁觀者角度」的跟拍鏡頭。`fixedYaw`（世界固定水平方向）、`targetOffset`（瞄準高度）都沒有變動。
- **已知風險**：這個攝影機系統沒有做鏡頭與角色自身模型的碰撞處理（見 `Docs/KNOWN_ISSUES.md`），distance=1 這麼近有機會讓鏡頭卡進角色自己的頭髮/頭部模型裡穿模，這是自動化工具看不出來的，需要使用者實際 Play 確認；如果真的穿模，可以直接調高 `distance`（例如 1.2～1.5）留一點緩衝。
- 新增一次性編輯器工具 `FixEyeLevelCameraSetup.cs`（Tools/Live2DAction/[Fix] Set Eye-Level Camera）套用到既有 `GreyboxTest.unity`（場景檔的欄位是先前修法明確寫入的序列化值，不會因腳本預設值改變而自動更新），並同步更新 `GreyboxSceneBuilder.cs` 的預設值，兩者都已是最終的 `distance: 1`。
- 用臨時診斷編輯器工具（跑完即刪除，未留在專案內）把 Main Camera 算圖存 PNG，兩輪（3.5 與 1）都確認畫面地平線落在畫面中段、沒有向下傾斜，相機幾何角度正確；`distance=1` 那張圖角色頭部確實佔滿大半畫面，符合「貼近後腦勺」的預期構圖。算圖裡角色材質/光照顯示明顯異常（原因未查證），這張圖只拿來驗證相機角度/距離幾何，不代表最終畫面效果。
- 50 個 EditMode + 27 個 PlayMode 測試全數重新驗證通過（純數值調整，不需要更新既有測試；其中一輪 `EnemyAITests.TargetWithinDetectionRange_ChasesTowardTarget` 間歇性失敗一次，重跑即過，跟本次沒碰的 `EnemyAI.cs` 無關，是已知的 headless batchmode 計時 flaky 問題）。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：水平視角＋極近距離的實際觀感、鏡頭是否卡進角色自己的頭部模型、`distance=1` 是否太近，這些都是合理起點猜測。

## 2026-08-11 — Phase 2 Step 4 追加：攝影機改為固定世界座標軸，移除第一人稱

使用者要求攝影機「固定世界座標軸」（類 ARPG 俯視角度），並確認兩個邊界案例：鎖定敵人時鏡頭角度不跟著旋轉（只有角色朝向會轉，沿用既有邏輯）、移除 V 鍵第一人稱切換（固定角度鏡頭不支援自由看向的第一人稱）。

- `ThirdPersonCameraController` 大幅簡化：移除滑鼠 delta 讀取、`CameraViewMode`／`ToggleViewMode()`／第一人稱眼睛位置／`visualToHide`、以及鎖定時改讀 `TargetLockUtility.ComputeLockOnYawPitch` 覆寫 yaw/pitch 的邏輯。改成兩個固定欄位 `fixedYaw`／`fixedPitch`（預設 0°／45°），`LateUpdate` 每幀只用固定角度＋角色目前位置重算相機位置，旋轉永遠不變——不管是滑鼠、角色旋轉、或鎖定敵人都不會再改變鏡頭角度。因為 `ICameraYawSource.YawDegrees` 現在是常數，`CharacterMovement` 的相機相對移動方向也連帶變成永遠相對同一組世界座標軸。
- 刪除 `CameraViewMode.cs`（列舉不再被任何地方使用）、`CameraViewToggleTests.cs`（PlayMode，測試的是已移除的 `ToggleViewMode()`）、`FixFirstPersonToggleSetup.cs` 與 `FixCameraCustomController.cs`（兩個一次性場景修正工具，寫入的欄位已不存在，功能被下面新增的 `FixFixedAxisCameraSetup.cs` 取代）。
- 新增 `FixFixedAxisCameraSetup.cs`（Tools/Live2DAction/[Fix] Set Fixed-World-Axis Camera）：把既有 `GreyboxTest.unity` 場景的相機欄位設成新的固定角度預設值，並確保 Player 的 "Visual" 子物件維持啟用（防止舊場景若剛好存檔在第一人稱隱藏狀態，因為 `ToggleViewMode()` 已不存在而永遠卡在隱藏）。`GreyboxSceneBuilder.cs`（`CreateCamera`）與 `FixTargetLockSetup.cs` 同步移除寫入相機 `lockOnSource`／舊滑鼠欄位的程式碼。
- 重寫 `ThirdPersonCameraControllerTests.cs`（EditMode，4 測試，涵蓋固定角度下的位置公式與 `YawDegrees` 不受滑鼠/鎖定影響）；`LockOnFacingAndCameraTests.cs` 的攝影機測試改成驗證「鎖定目標時 yaw/pitch 維持不變」（原本驗證的是鏡頭會轉向目標，行為已相反）。
- 50 個 EditMode + 27 個 PlayMode 測試全數通過（EditMode 數量不變，PlayMode 少 2 個是刪除 `CameraViewToggleTests.cs` 的緣故）。連跑兩次 PlayMode 全套，`CharacterMovementTests.MoveInput_Left/Right_...` 兩個既有測試間歇性失敗（差值都在容許門檻附近的個位數百分比，跟本次改動的檔案無關）——這是 `KNOWN_ISSUES.md` 已記錄多次的 headless batchmode 積分效率不穩定問題，不是本次改動造成的新迴歸；跟本次修改直接相關的測試（`ThirdPersonCameraControllerTests`、`LockOnFacingAndCameraTests`、`CameraRelativeMovementRegressionTests`）兩次都全數通過。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：固定角度（0°／45°、距離 8）看起來是否符合預期的 ARPG 俯視感，之後可直接調整 `ThirdPersonCameraController` 的 `fixedYaw`／`fixedPitch`／`distance` 欄位。

## 2026-08-11 — 攝影機拉遠避免只看到頭部＋Player2 補上鎖定（本次未跑 Unity 驗證）

使用者回報前一版 `distance=1` 太近「只看的到人物的頭部，下半身都在畫面之外」，同時要求 Player2（機甲靜態看板）擺正到跟玩家同一水平線、並能用 Q 鎖定當敵人；並明確要求「這是修正不要幫我測試 我內存不夠用」——這次全程用直接編輯場景 YAML／腳本檔完成，**沒有啟動 Unity Editor**，因此沒有算圖、沒有跑任何測試，細節與風險見 `Docs/KNOWN_ISSUES.md` 同日條目：

- 攝影機 `distance`：1 → **2.2**（貼頭高度＋完全水平俯仰的組合天生看不到下半身，這是物理/幾何限制不是 bug；拉遠一點讓垂直視野涵蓋全身，同步改了 `ThirdPersonCameraController.cs`／`GreyboxSceneBuilder.cs`／`FixEyeLevelCameraSetup.cs`／場景檔）。這次沒有算圖驗證，建議使用者直接在 Inspector 拖 `distance` 微調更快。
- Player2 新增 `LockOnTarget` 元件（直接寫入場景 YAML，比照 `TrainingDummy` 既有掛法，`TargetLockController` 自動掃描不需要額外註冊），Q 鍵應該可以鎖定它。
- Player2 是否「沒對齊水平線」：檢查 Transform 數值（Y=0，跟 Ground 頂面 Y=0 一致）與 FBX 匯入設定都沒發現異常，沒有找到需要修正的具體證據，這次沒有做任何猜測性旋轉調整——如果進 Editor 還是看起來歪的，麻煩告知具體歪法或直接用 Scene 視圖手動喬正存檔。
- **本次沒有做**：跑 `FixEyeLevelCameraSetup.cs`、EditMode/PlayMode 測試、算圖驗證，這批改動的正確性目前只靠程式碼閱讀與手動比對既有 YAML 格式，不是實測結果，等使用者記憶體充裕時應該補驗證一次。

## 2026-08-11 — Player2 擺到跟 Player1 面對面＋Play 前先算圖確認

使用者要求 Play 之前先看含 Player1／Player2 的場景畫面，並要 Player2 跟 Player1 面對面站著——這次同意再跑一次 Unity 算圖（跟上一項不同），細節見 `Docs/KNOWN_ISSUES.md` 同日條目：

- Player2 位置改到 `(1.2, 0, 0.5)`，旋轉手算朝向 Player1（四元數 `(0, -0.9751, 0, 0.2219)`，對應 yaw ≈ -154.36°）；第一版試過放在 Player1 正前方同一軸線上，算圖後發現會被 Player1 自己的身體完全擋住看不到，改成現在的側向偏移位置。
- 用三張臨時算圖（跑完即刪除）驗證：外部俯瞰、實際遊戲 Main Camera 視角（確認 Player1 全身＋Player2 同時入鏡）、Player2 近拍。近拍意外證實 Player2 其實是站立的動態戰鬥姿勢，不是歪的/躺著的——解決了上一項「擺正水平線」的懸念，不需要額外旋轉修正。
- 算圖環境仍有光照/色塊異常（batchmode 已知限制），不影響位置/朝向判斷。這次沒有跑 EditMode/PlayMode 測試套件。
- **仍待使用者本人 Play 確認**：算出來的朝向角度肉眼看是否真的對著 Player1，`(1.2, 0, 0.5)` 距離手感是否合適。

## 2026-08-11 — 使用者實際 Play 後回報三個現象：白柱子／畫面卡住／Player2 腳被截斷

使用者第一次實際 Play 後回報。逐項排查結果（細節見 `Docs/KNOWN_ISSUES.md` 同日條目），這次沒有跑 Unity：

- **白柱子**＝ `TrainingDummy`（敵人 AI），用 Unity 內建預設灰白材質的 Capsule 佔位，會在偵測範圍（8 單位）內追逐玩家——功能正確、外觀未完成的既有已知限制，不是 bug。
- **移動後畫面卡住**：全專案 `Assets/_Project/Game` 搜尋 `while` 迴圈 0 筆，排除無窮迴圈；場景本身多次自動化重新載入都正常無例外。**沒找到確切原因**，已回問使用者細節（Console 有沒有紅字、是完全沒反應還是卡頓幾秒自己恢復、每次都發生嗎）以便進一步判斷，懷疑是 Editor 首次繪製材質/Shader 的一次性編譯卡頓。
- **Player2 腳被截斷**：跟上次 Player1 只看到頭同一種幾何限制（近距離＋窄 FOV 裁切垂直範圍），但這次發生在別的角色身上，靠拉遠 `distance` 治標不治本。改成加寬 Main Camera 的 `field of view`：60° → 75°，`GreyboxSceneBuilder.cs` 同步更新。未算圖驗證，FOV 加寬可能增加邊緣透視變形，需要使用者確認觀感。

## 2026-08-11 — 深入排查「角色移動到一半畫面卡住」（使用者回報常常發生）

使用者回報這個現象很常見，要求重新檢視整個專案。這次動用完整排查手段（讀使用者真實 Play session 的 Unity `Editor.log`、寫效能診斷測試），細節見 `Docs/KNOWN_ISSUES.md` 同日條目：

- 讀了使用者的 `Editor.log`：進 Play 模式那次 domain reload＋asset reimport 合計超過 6 秒同步阻塞 UI——這是 Unity 每次帶著新程式碼/場景改動進 Play 模式的正常行為，不是我們的 bug，但在記憶體吃緊的機器上很可能被放大成「卡住」的感覺；這幾輪改了很多次程式碼，幾乎每次 Play 都會觸發。
- 同一份 log 找到一個真實但無關的小問題：`Player/Visual`（Maya 模型）有 2 個組件的腳本參照失效（Sketchfab 素材包遺留、原本就是惰性的，不會執行），**已修正**：用 `GameObjectUtility.RemoveMonoBehavioursWithMissingScript` 從場景實例上移除（沒有動到共用的 Maya prefab 資源）。
- 新增效能診斷測試 `MovementFrameTimingTests.cs`（已保留在測試套件裡）：載入真正的 `GreyboxTest` 場景，持續按住前進輸入 5 秒，記錄每個 Update tick 的實際耗時。連跑兩次，最長的 frame 只有 3.7ms／1.3ms，**沒有重現任何卡頓**——排除了無窮迴圈、失控 GC、每幀爆量呼叫等確定性的程式碼問題。
- 目前結論：卡頓比較可能是 Play 模式進入時的正常重編譯延遲（被低記憶體放大），或互動式 Editor 特有、headless 測試環境重現不出來的狀況，已回問使用者下次卡住時的具體細節（記憶體/CPU 使用率、Console 紅字、進 Play 那刻卡還是移動途中才卡）。
- 50 個 EditMode + 28 個 PlayMode 測試全數通過。

## 2026-08-11 — 找到真正原因：Player2 旋轉四元數沒歸一化，狂洗 Console

使用者截圖 Console 給看，找到真正原因，細節見 `Docs/KNOWN_ISSUES.md` 同日條目：

- Console 洗版重複 `Quaternion To Matrix conversion failed ... l=1.000060`，呼叫堆疊是 `GUIUtility:ProcessEvent`（幾乎每個滑鼠/鍵盤事件都觸發一次）——來源是先前「Player2 面對面」那次手算 `atan2` 後手動打進場景 YAML 的四元數 `(0, -0.9751, 0, 0.2219)`，四捨五入後長度變成 `1.000060` 而非精確的 `1.0`，Unity 對此容忍度很嚴，每次都判定無效並印錯誤——這才是「移動時畫面卡住」最直接的原因：不是迴圈卡死，是每次互動事件都要花時間印一次錯誤訊息，累積成肉眼可見的頓格。
- **教訓**：非乾淨角度（0°/90°/180°/270°）的旋轉不該手算三角函數後手動打進 YAML，之後一律要透過 Unity 自己的 API（`Quaternion.LookRotation` 等）算出來寫入，保證正確歸一化。
- **已修正**：Player2 旋轉改回精確的 180° 翻轉（`{x:0,y:1,z:0,w:0}`，數學上正好單位長度），位置不變。這次因為使用者 Editor 開著，直接改場景檔完成、沒有另開 Unity 避免衝突。
- **仍待使用者確認**：讓 Unity 重新讀到這次修正（重開場景或觸發外部變更偵測）後再 Play 一次，確認錯誤是否消失、卡頓是否解決。
- 事後有實際跑 Unity 驗證：50 EditMode + 28 PlayMode 全過，Console 確認不再出現該錯誤。

## 2026-08-11 — 訓練假人不再追逐玩家＋新增邊界牆

使用者要求「白柱子不要跟著角色」，並回報「角色到了邊界就會消失」，細節見 `Docs/KNOWN_ISSUES.md` 同日條目：

- `EnemyAI.detectionRange`：8 → **0**，訓練假人永遠停在 Idle、不再追逐/攻擊，比較符合「訓練假人」的定位。場景檔與 `GreyboxSceneBuilder.cs` 同步更新，既有測試不受影響（都自己用反射設定 detectionRange）。
- 「邊界消失」是真的掉出地圖：30×30 的 `Ground` 沒有任何邊界檔著，走出邊緣就會踩空、被重力一路往下拉。新增 4 面看不見的 `BoundaryWall_North/South/East/West`（只有 `BoxCollider`，無渲染），緊貼 `Ground` 四邊互相重疊蓋住轉角。`GreyboxSceneBuilder.cs` 新增 `CreateBoundaryWalls()` 並在建場景流程呼叫。
- 兩項都有實際跑 Unity 驗證，50 EditMode + 28 PlayMode 全數通過。

## 2026-08-11 — 攝影機再拉近拉低＋Player2 補上碰撞

使用者回報攝影機還是太高太遠、移動時感覺更遠，並要求柱子／Player1／Player2 互相都要有碰撞阻擋，細節見 `Docs/KNOWN_ISSUES.md` 同日條目：

- 攝影機：`targetOffset.y` 1.4→**1.15**、`distance` 2.2→**1.5**、Main Camera `field of view` 75°→**65°**。上次為了塞下全身把 FOV 加寬到 75°，但寬 FOV 本身會讓畫面裡東西看起來更小/更遠，很可能就是「移動時感覺更遠」的視覺成因（`ComputeCameraPosition` 的公式不會因移動而變）。三處腳本 + 場景檔同步更新。
- `Player2` 原本完全沒有 Collider，玩家會直接穿過去——補上 `CapsuleCollider`（半徑 0.6、高度 2.2，粗略對應體型）。`TrainingDummy`／`Player` 本來就有 `CharacterController` 互相阻擋。
- 新增永久回歸測試 `CharacterCollisionBlockingTests.cs`：驗證玩家衝向 `TrainingDummy`／`Player2` 都不會穿透，兩個測試都通過。
- 50 EditMode + 30 PlayMode（新增 2 個碰撞測試）全數通過，這次有實際跑 Unity 驗證。

## 2026-08-11 — 找不到「畫面卡住」的重現方式（螢幕錄影分析）

使用者提供 10 秒螢幕錄影。截圖分析發現真正現象是鏡頭在某個時間點停止跟隨玩家（背景物件像素級靜止、玩家角色持續變大直到出框），比先前猜測的「Editor 重編譯延遲」更具體。寫了一個直接量測 Main Camera 實際座標的診斷測試，讓玩家 10 秒內往 8 個方向移動，**全程幾乎零誤差，沒有重現**——不是移動本身會觸發的固定邏輯錯誤。細節見 `Docs/KNOWN_ISSUES.md` 同日條目。使用者隨後回報「現在已經不會卡住了」，記錄為已釐清、非持續性問題。

## 2026-08-11 — Player2 隨機漫遊＋攝影機調整教學

使用者要求 Player2 緩慢隨機移動、碰邊界要回頭，並要求教學攝影機怎麼自己調（不是直接改），細節見 `Docs/KNOWN_ISSUES.md` 同日條目：

- 新增 `WanderUtility.cs`（純邏輯）＋ `WanderMovement.cs`（MonoBehaviour）：每隔幾秒選新的隨機水平方向慢慢走，超出邊界（半徑 13，比實際邊界牆 15 留緩衝）就轉向朝原點走回來，轉向用 `Mathf.SmoothDampAngle`（不是手算四元數）。已掛到 `Player2`。
- 新增 `WanderUtilityTests.cs`（EditMode，5 測試）、`WanderMovementTests.cs`（PlayMode，2 測試）。
- 55 EditMode + 32 PlayMode 全數通過，有實際跑 Unity 驗證。
- 攝影機調整教學（`Third Person Camera Controller` 的 `Distance`／`Target Offset.Y`、`Camera` 的 `Field of View`，及 Play 模式改動需要退出後手動重填才會存檔的重要提醒）已寫進 `Docs/KNOWN_ISSUES.md`。

## 2026-08-11 — 執行前（Edit 模式）也能即時預覽攝影機

使用者問「如何調整執行前的預覽畫面」——原因是 `ThirdPersonCameraController.LateUpdate()` 只在 Play 模式才會執行，沒按 Play 時 Game 視窗看到的是舊位置。加上 `[ExecuteAlways]` 讓它在 Edit 模式也會執行，Game 視窗不按 Play 也能即時反映 `Distance`／`Target Offset` 的調整，而且 Edit 模式的修改是真的存檔（不像 Play 模式改動退出會還原）。使用者 Editor 開著、跑 batchmode 測試撞到「另一個 Unity 實例正在使用專案」的錯誤，**這次沒有實際跑測試驗證**，純程式碼修改（加一個屬性），已在 `Docs/KNOWN_ISSUES.md` 誠實記錄。

## 2026-08-11 — 修正角色浮空掉落＋新增滑鼠視角控制

使用者關閉 Editor 後，這次有跑 Unity 完整診斷，細節見 `Docs/KNOWN_ISSUES.md` 同日條目：

- **浮空掉落找到真正原因**：不是程式碼 bug，是 `Player` 座標被意外拖到 `(10, -0.5, 0)`（陷進地板下面一半）。改回 `(0, 0.5, -2)`；新增永久回歸測試 `PlayerSpawnGroundingTests.cs`（用真實重力，確認無輸入時角色不會往下掉）。這很可能也是先前那次「螢幕錄影卡住」的同一個根本原因（Player 座標跑掉，鏡頭其實有跟上，只是背景看起來像沒跟上）。
- **新增滑鼠視角控制（RPG 風格，不需按住按鍵）**：`ThirdPersonCameraController` 的 `fixedYaw`／`fixedPitch` 改成 `initialYaw`／`initialPitch`（起始角度）+ 內部可變的 `_yaw`／`_pitch`（Play 模式下每幀讀 `Mouse.current.delta` 累加）。沿用先前「原神風格」滑鼠視角驗證過不會重演 Cinemachine 畫圈 bug 的架構（單一 yaw/pitch 狀態同時驅動攝影機旋轉與 `CharacterMovement` 的相對移動方向）。鎖定敵人時攝影機依然不轉，只有角色朝向會轉（不變）。
- 場景裡使用者手動實驗留下的攝影機數值（因為 Player 座標跑掉、鏡頭顯示錯亂時嘗試調整的）已重設回合理預設。
- **連帶修好一個真實的測試隔離 bug**：`TargetLockControllerTests.cs` 的 `[SetUp]` 沒有清空場景根物件，被新增的、會載入真實場景的測試（`WanderMovementTests` 等）污染，已修正為比照其他測試檔先清空。
- 55 EditMode + 33 PlayMode 全數通過，有實際跑 Unity 驗證。

## 2026-08-11 — 修正滑鼠靈敏度太小＋角色重生位置改到柱子旁邊

使用者回報滑鼠視角幅度太小、角色出生像在柱子上面然後摔下來，細節見 `Docs/KNOWN_ISSUES.md` 同日條目：

- 滑鼠靈敏度：找到真正原因是多乘了一次 `Time.deltaTime`（`Mouse.delta` 本身已經是每幀的量，不該再乘 deltaTime，60fps 下等於把靈敏度除了快 60 倍）。拿掉這段乘法，`mouseSensitivity` 預設值同步從 `3` 改成 `0.15`（單位改成「每像素轉幾度」）。
- `Player` 重生點從 `(0, 0.5, -2)` 改到 `(-2.5, 0.5, 0)`——`TrainingDummy` 正側邊，不再跟假人幾乎同一直線。`GreyboxSceneBuilder.cs` 同步更新。算圖確認新位置清楚分開、沒有重疊。

## 2026-08-11 — 改成真正的第一人稱攝影機（distance=0），隱藏角色自己的模型

使用者要求「鏡頭固定在角色身上，就像角色的眼睛，不是移動視角會變第三人稱」，細節見 `Docs/KNOWN_ISSUES.md` 同日條目：

- 原理：`ComputeCameraPosition` 是 `瞄準點 - 旋轉 × 前方 × distance`，只要 `distance` 不是 0，轉動視角時這段位移向量就會跟著轉，鏡頭因此繞著角色畫弧（第三人稱環繞的本質）。`distance=0` 時這個向量恆為零，鏡頭永遠精確釘在瞄準點上，只剩旋轉——這才是真第一人稱。
- `ThirdPersonCameraController.distance` 預設值 `1.5` → `0`，`GreyboxSceneBuilder.cs` 同步更新。
- 連帶把 `Player` 底下的 `Visual`（Maya 模型）永久停用，避免鏡頭卡在自己頭部模型內側穿模——沿用專案更早之前「第一人稱先隱藏整個角色模型」的既有做法，這次是固定第一人稱、不是可切換的雙模式。
- 算圖確認鏡頭座標精確落在 `Player 位置 + (0, 1.15, 0)`，畫面看不到自己的身體，水平線置中。
- 移除過時的一次性攝影機修正工具 `FixEyeLevelCameraSetup.cs`（已被超越兩次）。
- 55 EditMode + 33 PlayMode 全數通過，有實際跑 Unity 驗證。

## 2026-08-11 — 接受一點點環繞感，把攝影機拉開一點點讓角色露臉

使用者主動確認要接受「一點點環繞感」換「看得到自己」的取捨，細節見 `Docs/KNOWN_ISSUES.md` 同日條目：

- `distance`：`0` → `0.5`。轉動視角時鏡頭會有一點點繞頭部畫弧（過肩鏡頭的標準行為），不再像 `0` 一樣完全焊死。
- 角色自己的 `Visual` 重新啟用（拉開距離後不再卡在頭部網格裡，不重新啟用的話拉開距離就沒意義）。
- `GreyboxSceneBuilder.cs` 同步更新。
- **這次沒有跑 Unity 驗證**（使用者 Editor 又開著），純數值調整，風險低但誠實記錄未驗證。

## 2026-08-11 — 修正視角慢慢跑掉：鎖定游標

使用者回報 `distance=0.5` 之後視角會慢慢跑掉，細節見 `Docs/KNOWN_ISSUES.md` 同日條目：

- 原因：沒有鎖定游標時，`Mouse.current.delta` 讀的是滑鼠實際物理移動量，不管游標有沒有跑出遊戲畫面，任何滑鼠移動都會疊加進視角旋轉。
- `ThirdPersonCameraController` 新增 `OnEnable`／`OnDisable`：Play 模式鎖定＋隱藏游標，所有滑鼠移動只用來轉視角。Editor 裡按 Esc 可隨時解鎖（內建行為）。
- 順便修好：`Player2` 被意外取消勾選（`m_IsActive: 0`），已重新啟用——這是本次對話第三次「Editor 操作時不小心動到東西」，已在 `KNOWN_ISSUES.md` 給使用者一些操作建議。
- 55 EditMode + 32 PlayMode 通過（1 個 `EnemyAITests` 既有的計時 flaky 測試失敗，跟本次改動無關，`EnemyAI.cs` 未被觸碰）。

## 2026-08-11 — 找到並修正「專案開不起來」：Editor 被兩萬多次警告洗到卡死

使用者回報專案開不起來，細節見 `Docs/KNOWN_ISSUES.md` 同日條目：

- 診斷發現 `Unity.exe` 其實還在跑但已無回應，`Editor.log` 22 分鐘沒有新內容，檔案異常肥大（11MB），裡面有 26,034 次重複的 `Animator is not playing an AnimatorController` 警告，來自 `CharacterAnimatorLink.cs:46`。
- 根本原因：`CharacterAnimatorLink` 掛在永遠啟用的 `Player` 上，每幀對 `Player > Visual` 的 `Animator` 呼叫 `SetFloat`；先前為了第一人稱把 `Visual` 停用過，那段期間每幀都對著停用的 Animator 硬呼叫，累積出天文數字的警告，很可能是拖垮 Editor 的直接原因。
- **已修正**：`CharacterAnimatorLink.Update()` 的判斷條件加上 `!animator.isActiveAndEnabled`，Animator 所在物件被停用時直接跳過。
- **這次沒有跑 Unity 驗證**（`Unity.exe` 卡住占用專案），純邏輯修正，改動的既有測試只測純函式不受影響，但誠實記錄未驗證。
- 後續：使用者確認已強制關閉卡住的 Unity，已用命令列重新啟動乾淨的新 Editor 實例，確認開啟成功。

## 2026-08-11 — 新增空白鍵跳躍＋修正放開移動鍵沒有馬上停止

使用者要求加跳躍、並回報放開移動鍵後角色沒有馬上停止，細節見 `Docs/KNOWN_ISSUES.md` 同日條目：

- `IInputCommand` 新增 `JumpPressed`，`PlayerInputProvider` 綁空白鍵；空白鍵原本兼職攻擊鍵（跟滑鼠左鍵重複），這次移掉，攻擊維持只用滑鼠左鍵。`CharacterMovement` 新增 `jumpSpeed`（預設 7），只有貼地時才能跳，空中再按不會雙跳。目前沒有跳躍動畫，純物理邏輯（跟先前「連段沒動畫」同樣的「先求邏輯正確」做法）。
- `IInputCommand` 是共用介面，新增成員後 `EnemyAI.cs` 與全部 9 個測試檔的 `StubInputBehaviour` 都同步補上實作。
- 新增 `JumpTests.cs`（PlayMode，2 測試）。
- `decelerationSmoothTime`：`0.12` → `0.05`，放開移動鍵後幾乎立即停止（仍保留一點點緩動，不是完全瞬間停止）。
- 有實際跑 Unity 驗證。

## 2026-08-11 — 玩家（Maya）＋敵人攻擊動作：程式驅動的簡易佔位揮擊姿勢

使用者要求幫角色1（玩家）／角色2（敵人）加攻擊動作，確認範圍是「玩家＋敵人」、動畫來源是「先用簡易佔位動作」（Maya 動畫套件只有 Idle/Walk/Run/Jump/Fall，沒有攻擊素材；敵人是沒有骨架的 Capsule，兩者都做不出正式骨骼動畫）：

- `ComboAttackState.cs` 新增 `PhaseProgress`（目前所在 Startup/Active/Recovery 影格階段內的 0~1 進度，相對「這一階段」而非整次攻擊累計時間），`PlayerCombat.cs` 直接透傳同名唯讀屬性。
- 新增 `AttackPoseUtility.cs`（純函式，比照 `AttackResolver`／`ComboAttackState` 既有的「純邏輯先於 MonoBehaviour」慣例）：`(Phase, PhaseProgress) → 揮擊角度`，Startup 後拉蓄力、Active 快速揮出、Recovery 收回，影格資料沒變、只是多算一個角度。
- 新增 `AttackPoseVisualizer.cs`（`Assets/_Project/Game/Characters/`）：在 `LateUpdate`（晚於 Animator 每幀算完姿勢的時機）把算出的角度疊乘到指定 Transform 上，不快取基準旋轉、每幀直接疊在 Animator 當下算好的姿勢上——沒有攻擊時角度是 0（單位四元數），完全不影響 Maya 原本的 Idle/Walk 手臂擺動動畫。同一個元件同時給玩家（甩手臂骨骼）跟敵人（整個 Capsule 前傾）用，之後要換成正式動畫時不用改這個類別。
- 新增一次性編輯器腳本 `WireAttackPoseVisualizers.cs`（Tools/Live2DAction/Wire Attack Pose Visualizers）：玩家用 `Animator.GetBoneTransform(HumanBodyBones.RightUpperArm)` 找右手臂骨骼；敵人用它的 `Visual` 子物件（沒有骨架，整個前傾代表出拳）。**沒有**併進 `GreyboxSceneBuilder.Build()`：它需要玩家的 Animator，而 Animator 是 `PlayerMayaVisualSetup` 之後才接上的，跟 `WireCharacterAnimatorLink` 目前也沒被併進 `Build()` 是同樣的理由。已對現有 `GreyboxTest.unity` 執行過。
- 新增 4 個 EditMode 測試（`AttackPoseUtilityTests.cs`：Idle/Startup/Active/Recovery 各階段的角度曲線、Progress 超出 0~1 會被 clamp）、擴充 `ComboAttackStateTests.cs` 4 個測試（`PhaseProgress` 在 Idle 為 0、在 Startup 內遞增、切換到新階段會歸零重算而非累計整次攻擊、零長度階段回傳 1 不除以零）。新增 2 個 PlayMode 測試（`AttackPoseVisualizerTests.cs`）：真實 Update 迴圈驗證攻擊 Active/Recovery 期間 Transform 確實偏離基準旋轉、完全沒按攻擊鍵時 Transform 全程維持在基準旋轉。
- 64 個 EditMode + 37 個 PlayMode 測試全數通過（含新增的 8 個），有實際跑 Unity `-runTests` 驗證；跑了兩輪 PlayMode，兩輪各自有 1~3 個既有 `CharacterMovementTests`／`JumpTests` 間歇性失敗，是已知的 headless batchmode 時序問題（本次沒有動 `CharacterMovement.cs`／`EnemyAI.cs`），本次新增的測試兩輪都全過。
- **已知限制**：手臂/前傾的旋轉軸與角度只是合理猜測，方向可能是反的——Inspector 加了 `invert` 勾選框可以直接試、不用改程式碼；這是「角度動畫」不是美術動畫，揮擊看起來會是生硬的甩動，不是流暢拳法。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：揮擊角度/方向是否合理（`invert`／`windUpAngleDegrees`／`swingAngleDegrees` 都能直接在 Inspector 調），敵人前傾出拳的視覺是否可以接受（過渡方案，等敵人有正式外觀再重做）。

## 2026-08-11 — 修正玩家攻擊動作方向反了

使用者實際 Play 後回報玩家的攻擊揮擊動作方向反了（上面條目已預先記錄這是合理猜測、未經人眼確認的已知限制）：

- 新增一次性編輯器腳本 `FixAttackPoseDirection.cs`（Tools/Live2DAction/Fix Player Attack Pose Direction），把玩家 `AttackPoseVisualizer.invert` 從 `false` 改成 `true`，已對 `GreyboxTest.unity` 執行套用並確認寫入。`WireAttackPoseVisualizers.cs` 的預設值同步更新，未來重新套用/重建場景會直接是對的方向，不用再跑這支修正腳本。
- 敵人（`TrainingDummy`）的 `invert` 維持 `false` 沒有動——使用者這次回報的是「角色」的攻擊動作，先當作是玩家；如果敵人前傾的方向也不對，需要再確認一次分開處理。
- 這是純資料調整（Inspector 欄位），沒有改程式邏輯，不影響已通過的測試。

## 2026-08-11 — 修正攝影機視角角度/位置不對

使用者實際 Play 後回報「滑鼠固定遊戲視窗視角」壞了，追問確認症狀後是「視角會動，但角度/位置不對」（不是完全不會動、也不是游標跑出視窗）：

- 檢查 `ThirdPersonCameraController.cs` 的滑鼠鎖游標邏輯（`OnEnable`/`OnDisable`）跟這次改動完全沒有交集，本次沒有動過這個檔案。
- 但檢查 `GreyboxTest.unity` 裡攝影機元件的實際數值，發現 `distance=0.8`、`targetOffset=(0, 0.5, 0)`——兩者都跟 `ThirdPersonCameraController.cs` 本身的欄位預設值（`distance=0.5f`、`targetOffset=(0, 1.15f, 0)`）與程式碼註解描述的設計（`targetOffset.y` 是「眼睛高度」旋鈕、`distance=0.5` 是「剛好露出角色又不會鑽進頭部網格」的既定取捨）對不上，`targetOffset.y=0.5` 會把視角瞄準點拉到腰部高度而不是眼睛高度，正好符合「角度/位置不對」的描述。`CHANGELOG.md`／`DEVELOPMENT_ROADMAP.md` 過去也沒有任何條目記錄過這兩個值被改成 0.8／0.5，判斷是先前 Editor 操作時的意外調整（這個專案已有數次類似案例，見 `KNOWN_ISSUES.md`），不是這次改動造成的。
- 新增一次性編輯器腳本 `FixCameraOffsetDrift.cs`（Tools/Live2DAction/Fix Camera Offset Drift），把 `distance` 重設回 `0.5`、`targetOffset` 重設回 `(0, 1.15, 0)`，已對 `GreyboxTest.unity` 執行套用並確認寫入。
- 這是根據程式碼本身的預設值與註解做的合理推斷修正，不是 100% 確認的診斷——**仍待使用者本人 Play 一次確認**角度/位置現在是否正確；如果還是不對，需要更多細節（例如实际看起來是什麼樣子的截圖或描述）才能繼續排查。
- 純資料調整，沒有改程式邏輯，64 個 EditMode 測試全數通過（重新驗證，跟這兩次修正無關的既有測試沒有受影響）。

## 2026-08-12 — 撤銷上面那次攝影機修正：使用者確認 `distance=0.8`／`targetOffset=(0,0.5,0)` 是刻意調的

上面 2026-08-11「修正攝影機視角角度/位置不對」的診斷是錯的。使用者說明：那組數值是他自己調整、要的攝影機位置，不是意外/漂移，明確要求「記錄她不要再改了」：

- **撤銷**：刪除已經是錯誤前提的一次性腳本 `FixCameraOffsetDrift.cs`（留著的話之後不小心重新執行會再把使用者調好的值改掉）。
- **改成把使用者的值當作權威來源，寫回程式碼本身**：`ThirdPersonCameraController.cs` 的欄位預設值改成 `distance = 0.8f`、`targetOffset = (0, 0.5f, 0)`（原本錯誤地寫死 `0.5f`／`(0, 1.15f, 0)`），並把附近註解裡「eye height」／「0.5 是既定取捨」的舊描述改掉，改成明確説明這兩個值是使用者手動調的、之後看起來「不對」也不要自己改回舊數字，要先問。`GreyboxSceneBuilder.cs` 的 `CreateCamera()` 同步更新，未來重建整個場景也不會再跑出舊數值。
- **教訓**：程式碼欄位的預設值／註解會過時，不能拿來當「正確答案」反過來判定場景裡使用者調過的數值是不是漂移/意外——這次就是誤把使用者刻意的調整當成 bug 修掉了。已存進 AI 端的長期記憶，避免同樣的事再發生一次。
- 純資料/註解調整，沒有改變任何執行邏輯，不影響已通過的測試。

## 2026-08-12 — 「把現有角色都呈現在場景中」：Enemy 換外觀＋076/077 Live2D 立牌加入 3D 場景

使用者要求把現有角色都呈現在場景中，釐清範圍後確認三件事：Enemy 換上已有的 Quaternius Humanoid 外觀、076/077 Live2D 角色（納茲/露西）也要放進 3D 場景、Quaternius Female 變體也要放到一個新角色上（Female 檔案本機沒有，見下一則條目）。這則先記錄前兩項：

- 新增 `EnemyHumanoidVisualSetup.cs`（Tools/Live2DAction/Replace Enemy Visual With Humanoid Placeholder）：把 `TrainingDummy` 原本的純白 Capsule 換成專案裡已有的 CC0 Quaternius Humanoid 模型（`PlayerHumanoidVisualSetup.cs` 當初幫 Player 用過、後來被 Maya 取代、`ASSET_LICENSES.md` 早就寫著「保留在專案內作為備用角色，未來也可能用在別的敵人/NPC上」的那份素材），沿用同一份材質資產，子物件仍叫 `Visual`（維持既有的 Find("Visual") 慣例）。目前沒有動畫（模型沒有附帶 Animator Controller），會維持 bind pose，跟 Player 當初換上這個素材時的已知限制一樣。
- 新增 `Live2DStandeeSetup.cs`（Tools/Live2DAction/Add 076-077 Live2D Standees (DoNotShip)）：把 076（納茲，`PlaceholderCharacter/c_7001`）跟 077（露西，新複製進來的 `PlaceholderCharacter077/c_7002`）各自做成一個獨立的攝影機朝向立牌 GameObject（`NatsuStandee_DoNotShip`／`LucyStandee_DoNotShip`，沿用 `PlayerCubismVisualSetup.cs` 當初的 URP shader／CanvasHeight 換算縮放／`CubismBillboard` 手法），放在場景裡遠離其他角色的空地（`(-6,0,-8)`／`(-3,0,-8)`）。**兩個 GameObject 名稱都刻意帶 `_DoNotShip` 後綴**（比照 `MechaModel_DoNotShip` 的命名慣例），一眼就能看出不能進 Build，`ASSET_LICENSES.md` 已同步更新兩者的實際使用位置。
- 077 是這次第一次複製進本專案（`Assets/_Project/Live2D/PlaceholderCharacter077/`，僅 `c_7002.moc3`／`model3.json`／`texture_00.png`／4 個 motion，未動 `C:\question` 原始檔）：複製時發現 `model3.json` 也有跟 076 當初一樣的問題（`"Physics": ".physics3.json"` 指向不存在的檔案，Cubism 的 AssetProcessor 會直接報錯），複製時直接拿掉那一行，跟當初 076 的修法一致。
- 這次視覺替換連帶弄壞了 Enemy 的 `AttackPoseVisualizer.swingTransform`（舊的 `Visual` 被換掉後，原本指向它的參照變成懸空 `fileID: 0`）——重新跑一次 `WireAttackPoseVisualizers.Apply()` 補上正確參照，`invert` 等已確認過的欄位不受影響（腳本本身冪等，重跑不會把使用者確認過的值改掉）。
- 用命令列算圖確認（`-batchmode` 不加 `-nographics` 才有真的渲染，第一次嘗試因為加了 `-nographics` 拍出全灰畫面，找出原因後修正）：Enemy 正確顯示 Quaternius 男性人形（T-pose，無粉紅材質）；Natsu／Lucy 立牌正確顯示各自的角色圖＋特效，面向鏡頭方向正確；Player／Player2 都沒有受影響。算圖用的暫時腳本已刪除，不在 repo 裡。
- 純視覺/資料變動，沒有改動戰鬥或 AI 邏輯，64 個 EditMode 測試全數通過。
- **仍待使用者本人 Play 一次確認**：Enemy 的新外觀比例是否合理、076/077 立牌的位置/朝向/大小看起來是否正常。

## 2026-08-12 — Quaternius Female 新角色（原以為要下載，其實檔案早就在專案裡）

使用者要求把 Quaternius Female 變體也放到一個新角色上，並要求 AI 自己去下載檔案。用 `claude-in-chrome` 瀏覽器工具跑了一次完整下載流程（`quaternius.itch.io/universal-base-characters` → 「No thanks, just take me to the downloads」略過付費 → 下載 `Universal Base Characters[Standard].zip`，123MB，存在使用者本機 `Downloads` 資料夾），但解壓後對照才發現：**Female 的 FBX 跟四張貼圖其實從 2026-08-10 起就已經在專案裡**（`Assets/_Project/Characters/Placeholder/UniversalBaseCharacters/`），只是當時只有 Male 被接到 Player 身上，Female 檔案原封不動放在那裡沒人用——這次下載變成多餘的（不影響結果，只是白跑一趟；下載回來的 zip 還留在 `Downloads` 裡，使用者可自行刪除）。

- 新增 `FemaleStandeeSetup.cs`（Tools/Live2DAction/Add Quaternius Female Standee）：把 `Superhero_Female_FullBody.fbx` 的匯入設定改成 Humanoid Rig（原本是 Generic，跟 Male 當初一樣要手動改）、建立 URP Lit 材質（BaseColor + Normal，比照 `PlayerHumanoidVisualSetup.cs`／`EnemyHumanoidVisualSetup.cs` 的既有模式），放進場景一個全新的獨立 GameObject `FemaleStandee_Placeholder`（`(0,0,-8)`，緊接著 076/077 立牌排開，形成一排「角色展示」），**沒有掛任何戰鬥/AI/移動邏輯**——單純靜態站立，跟 Player/Enemy/Player2 都不相關，之後如果要讓她動再另外處理。
- 用命令列算圖確認：三個新角色（Enemy 的 Quaternius Male、Natsu、Lucy）加上這次新增的 Female 站在一起都正確顯示，沒有粉紅材質，比例合理（T-pose，跟 Male 一樣沒有動畫）。算圖用的暫時腳本已刪除，不在 repo 裡。
- 純新增，沒有改動戰鬥或 AI 邏輯，64 個 EditMode 測試全數通過。
- **仍待使用者本人 Play 一次確認**：Female 角色的比例/位置是否合理；也請自行確認要不要清掉 `Downloads` 裡這次多下載的 `Universal Base Characters[Standard].zip`（AI 不會主動刪除使用者檔案）。

## 2026-08-12 — 修正 076/077 立牌改不了名字、Enemy 一直飄在空中

使用者回報兩個問題：076/077 立牌在 Editor 裡改不了名字、只穿褲子的 Enemy 一直飄在空中（已經調過 Y 座標還是沒用）。

- **076/077 改名**：調查發現不是「改不了」，是**名字真的變成空字串**存進場景檔——對得上 `KNOWN_ISSUES.md` 早就記錄過的 Cubism SDK 已知怪癖（`ToModel()` 產生的物件名字不穩定，先前只在 Play 模式下看過變空字串，這次看起來連存檔後也會發生，成因依然沒查出來）。因為名字是空的，`GameObject.Find(名字)` 根本找不到它們，改用出生座標比對來定位（`Live2DStandeeSetup.cs` 設定的 `(-6,0,-8)`／`(-3,0,-8)`，只比 X/Z，Y 會因為立牌置中邏輯有些微差異）。新增 `FixLive2DStandeeNames.cs`（Tools/Live2DAction/[Fix] Rename Live2D Standees To 076-077），**保留 `_DoNotShip` 後綴**沒有照字面改成純 `076`/`077`——`CLAUDE.md` 規則 2 是硬性規定這兩個素材不能出現在對外 Build，後綴是在 Hierarchy 一眼就能看出來的提醒，兩個名字仍然以 `076`/`077` 開頭。**這個修正腳本第一次跑完後名字又變回空字串**（推測是又被同一個怪癖影響），重跑一次才真正存住——這個名字不穩定的毛病看起來還會再發生，之後如果又變空白，重新執行這支腳本即可（用座標比對，不受名字影響）。
- **Enemy 飄浮**：查出 `TrainingDummy` 的 `CharacterController.height` 在某個時間點被改成 `1`（場景裡現在存著 `m_Height: 1`），但重生 Y 座標沒有跟著重算——**這正是先前 Player 也發生過、已經修過一次的同一種 bug**（見 `FixPlayerGroundedSpawn.cs`：手動調角色本身的 Y 座標救不了，因為正確的 Y 值要跟著 `height`/`center` 一起算，不是憑感覺拖）。因為 Enemy 目前設定成 Idle 不會動（`detectionRange=0`），連重力都沒有機會把它拉正，只會一直停在原本錯誤的高度。新增 `FixEnemyGroundedSpawn.cs`（Tools/Live2DAction/[Fix] Ground Enemy Spawn Height），沿用 `FixPlayerGroundedSpawn.cs` 一模一樣的公式（從 Ground 實際世界座標邊界＋目前的 `height`/`center` 反推正確 Y），把 Y 從 `0` 改成 `0.5`。
- 純資料修正，沒有改動任何程式邏輯，64 個 EditMode 測試全數通過。
- **仍待使用者本人 Play 一次確認**：Enemy 現在是否真的貼地了；076/077 的名字這次有沒有撐住（如果又變回空白，直接重跑修正腳本，不用回報給我也可以自己動手）。

## 2026-08-12 — 地板貼圖／背景景物／天空盒

使用者要求幫 `GreyboxTest` 的地板跟背景加上畫面，確認範圍是「地板＋背景＋天空盒」三層都做，素材用免費可商用素材包。

- **地板貼圖**：下載 Poly Haven「Stone Floor」CC0 貼圖（1K Diffuse/Normal/Roughness，直連 `api.polyhaven.com`，不需登入），套進 `Ground` 的新材質 `Assets/_Project/Environment/Materials/Ground_StoneFloor.mat`（URP/Lit，只接了 Diffuse+Normal，10x10 平鋪；Roughness 貼圖留在資料夾內未接——URP/Lit 的 Metallic 工作流程要packed Mask Map，不是單張 Roughness 圖，之後要接再處理）。
- **背景地形＋天空盒**：新增 `BackgroundTerrain`（300x300 純色平面，只做視覺填補邊界外的空洞，無 Collider，玩家碰不到）；新增 `Skybox_Procedural.mat`（Unity 內建 `Skybox/Procedural` shader，不需外部素材）並指定給 `RenderSettings.skybox`／`RenderSettings.sun`。三者都直接寫進 `GreyboxSceneBuilder.cs`（`CreateGround`／`CreateBackgroundTerrain`／`CreateSkybox`），跟著 Build Greybox Test Scene 一起重建。
- **背景景物**：用瀏覽器工具從 Quaternius 官方 Google Drive 分享資料夾下載「Simple Nature Pack」（CC0，13 個未貼圖低模：Tree/Rock/Bush/Grass），複製進 `Assets/_Project/Environment/Placeholder/QuaterniusSimpleNature/`。新增 `BackgroundSceneryStandeeSetup.cs`（Tools/Live2DAction/Add Background Scenery，仿照 `FemaleStandeeSetup.cs` 的兩段式模式，Build 完場景後另外執行）：用固定亂數種子在邊界牆外圍（半徑 17–26，牆內是玩家能走的範圍）隨機灑 40 個道具，可重複執行、結果一致。素材內嵌的是 Built-in Standard shader 材質，在 URP 下會變粉紅色，比照 Maya/Female 的做法逐一轉成 URP/Lit（依原本顏色重建，快取成獨立 `.mat` 資產，同色只轉一次）。
- 三個新的 Editor 工具都跑過 `-batchmode`：`Build Greybox Test Scene` → `Add Background Scenery` → EditMode 測試，64 個全數通過，沒有動到任何戰鬥/AI/移動邏輯。
- 素材登記見 `Docs/ASSET_LICENSES.md`（Poly Haven Stone Floor、Quaternius Simple Nature Pack，皆為 CC0，可進正式 Build，不同於 076/077/機甲那三個 DoNotShip 佔位素材）。
- **已知限制**：`Skybox/Procedural` 是 Editor 一定找得到的內建 shader，但還沒加進 Graphics Settings 的「Always Included Shaders」，正式 Build 前要確認沒被 shader stripping 拿掉（見 `KNOWN_ISSUES.md`）；Simple Nature Pack 的模型比例/密度未經人眼確認，可能偏大偏小或太密/太疏。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：地板貼圖平鋪效果、天空盒觀感、背景景物的比例與擺放密度是否合理——這次全程只有 batchmode 驗證編譯與測試，沒有人眼看過渲染結果。

## 2026-08-12 — 修正 AI 誤刪場景角色的事故

上面那一輪加地板/天空盒/背景景物時，AI **直接呼叫了 `GreyboxSceneBuilder.Build()`**，沒意識到這支工具會先 `NewScene(EmptyScene)` 整個清空場景再重建——只重建它自己寫的內容（地板/牆/掩體/素體 Player·Enemy/攝影機），把當天稍早才疊上去、**還沒進 git commit** 的 Maya、Enemy 的 Quaternius 人形、FemaleStandee、076/077 立牌、Player2 機甲全部清掉了，`Add Background Scenery` 只是加在被清空後的場景上，沒有補回這些。使用者發現「角色不見了」回報後才查出問題。

- **查證**：`git log` 確認最後一次 commit（`32d34cf`／"0811"）本來就沒有這些角色——它們全是當天用腳本疊加、尚未提交的工作，場景檔又只有工作目錄這一份，git 救不回來。
- **復原方式**：這個專案的角色本來就是靠一串可重複執行的 Editor 工具疊上去的（不是手動在 Editor 裡拖），所以照當初的順序重新跑一次同一組工具：`Replace Player Visual With Maya (Anime)` → `Wire Character Animator Link On Player` → `Wire Attack Pose Visualizers` → `Replace Enemy Visual With Humanoid Placeholder` → `[Fix] Ground Enemy Spawn Height` → `Add Quaternius Female Standee` → `Add 076-077 Live2D Standees (DoNotShip)` → `[Fix] Rename Live2D Standees To 076-077` → `Add Mecha As Player2 Standee`。先讀過每支「Fix」腳本的註解，排除掉已經被折進 `GreyboxSceneBuilder.cs`／各元件預設值裡、重跑反而多餘的舊修正（`FixDodgeSetup`／`FixComboAttacksSetup`／`FixTargetLockSetup`／`FixPlayerGroundedSpawn`／`FixFixedAxisCameraSetup`／`FixEnemyAISetup`／`FixMoveSpeedForAnimation`／`FixAttackPoseDirection`），只跑真正還需要的。
- **076/077 名字又變空字串**的老毛病（見 `KNOWN_ISSUES.md`）這次也重現了，`FixLive2DStandeeNames` 照文件說的重跑第二次才真正存住。
- **驗證方式**：因為場景是二進位 YAML，靠比對關鍵 GUID／物件名稱確認每個角色真的接回去了（Maya 的 Prefab guid、Enemy 的人形 FBX guid、Player2 機甲 FBX guid、`CharacterAnimatorLink`／`AttackPoseVisualizer` 腳本 guid 都在場景裡找得到對應引用），而不是只看工具跑完沒報錯。64 個 EditMode 測試全數通過。
- **救不回來的部分**：如果在事故發生前，使用者曾經在 Editor 裡手動微調過任何位置/數值（不是透過腳本設定的，例如直接拖動座標），這些手動調整沒有腳本可以重放，只會回到腳本的預設值——CHANGELOG 更早以前就記過「Player 座標／Player2 啟用狀態／地板座標多次被使用者在 Editor 裡操作時意外拖動或取消勾選」，這類調整這次很可能遺失了。
- **教訓**：`GreyboxSceneBuilder.Build()` 是「清空重建」而不是「疊加」，之後任何只需要局部修改既有場景的操作（像這次的地板/天空盒），都應該用「開啟現有場景直接改」的模式（像 `BackgroundSceneryStandeeSetup`／各種 `Fix*.cs` 那樣 `EditorSceneManager.OpenScene`），不能圖方便呼叫 `Build()`。
- **仍待使用者本人 Play 一次確認**：所有角色的視覺/位置/比例是否真的跟事故發生前一樣，尤其是任何可能被手動調過但這次用腳本預設值回填的地方。

## 2026-08-12 — 使用者手動調參數後回報三個問題

使用者在 Editor 裡自己調整過一些參數後，回報：(1) Player1 沒有貼地、也沒有站在白柱子（`TrainingDummy`）旁邊，(2) 滑鼠視角沒有穩定鎖定在遊戲視窗、無法持續跟隨角色轉動，(3) 076/077 立牌又變回沒有名字。

- **Player 沒有貼地**：檢查場景檔發現 `Player` 的 `CharacterController.height` 是 `1`（不是預設的 `2`），但 spawn Y 還是 `0`，跟 `FixPlayerGroundedSpawn.cs` 當初記錄的那個 bug是同一種成因（`height` 被手動改過、Y 沒有跟著重算）。直接重跑 `[Fix] Ground Player Spawn Height`，Y 從 `0` 改成 `0.5`（`groundTopY(0) + center.y(0) + height/2(0.5)`）。
- **白柱子不見了**：查證發現 `TrainingDummy`（Enemy AI，`EnemyAI`／`PlayerCombat`／`Health`／人形視覺）**整個 GameObject 已經從場景裡消失**，不是改名或改位置——場景裡完全找不到 `EnemyAI` 腳本的任何引用。詢問使用者後確認**是使用者自己故意刪除的，不用復原**——目前 `GreyboxTest` 場景裡沒有可鎖定/可對戰的敵人是預期狀態，不是 bug。
- **滑鼠視角鎖定**：`ThirdPersonCameraController` 原本只在 `OnEnable()`（Play 開始那一刻）鎖一次滑鼠游標，一旦中途因為按 Escape、切到別的視窗、或者 Play 開始時 Game 視窗還沒被點擊過而失去鎖定，就再也不會自動重新鎖上。改成在 `LateUpdate()` 裡偵測「目前沒鎖定 + 這一幀按了滑鼠左鍵」就重新鎖定＋隱藏游標，點一下遊戲視窗就會恢復跟隨，同時保留 Escape 可以暫時跳出的行為（不會變成滑鼠永遠出不去）。
- **076/077 又變空字串**：重現的還是那個沒查出成因的 Cubism 命名 bug（見 `KNOWN_ISSUES.md`），重跑 `[Fix] Rename Live2D Standees To 076-077` 修好，這次也一樣重跑了兩次才在磁碟上的存檔確認住。
- 這次的改動：一個場景數值修正（Player Y）、一個程式碼修正（`ThirdPersonCameraController.cs` 的滑鼠鎖定重試）、一個場景命名修正（076/077），64 個 EditMode 測試全數通過。

追問「滑鼠視角」的訴求後，使用者進一步澄清是要**固定在角色自己視線、完全不要有第三人稱環繞感**，不只是鎖定游標而已：

- **改成真第一人稱**：`ThirdPersonCameraController.distance` 從使用者先前自己調的 `0.8`（過肩第三人稱）改成 `0`（class comment 本來就寫「distance 0 = true first-person，鏡頭精確坐在眼睛位置，只轉動不環繞」）。同步更新 `GreyboxSceneBuilder.cs` 的預設值，跟 `[[camera-user-tuned-values-are-authoritative]]` 這份記憶一起更新，避免以後又被誤判成「跟 code 對不上」而改錯方向。
- **補回消失的自身視覺隱藏邏輯**：發現 class comment 一直寫著「distance 0 時會隱藏自己的視覺，避免看到自己頭部模型內側」，但實際程式碼裡**這段邏輯在更早的重構（拿掉 Cinemachine 的那次）中被刪掉了、註解沒有跟著更新**。補回 `SetOwnVisualHidden()`——只關閉 `Visual`子物件底下的 `Renderer`（不是整個 GameObject 用 `SetActive` 關掉），因為之前就發生過 `CharacterAnimatorLink` 對著停用的 Animator 狂洗 `SetFloat` 警告的事故（見 `KNOWN_ISSUES.md`），關 Renderer 不會讓 Animator 停止運作，不會重蹈覆轍。
- 沒有呼叫 `GreyboxSceneBuilder.Build()`，新增 `FixCameraToFirstPerson.cs`（Tools/Live2DAction/[Fix] Set Camera To True First-Person）直接開現有場景改這一個數值，照上一輪事故後定下的規矩來。
- 64 個 EditMode 測試全數通過。
- **仍待使用者本人 Play 一次確認**：Player 現在是否真的貼地站好；滑鼠點一下遊戲視窗後視角是否能穩定跟著轉；真第一人稱下有沒有意外看到自己模型內側的破綻；076/077 名字這次有沒有撐住。
- **附帶發現**：076/077 名字這個 bug 看起來幾乎每次場景重新載入（`EditorSceneManager.OpenScene`）都會重現，不是偶發——之後任何工具只要開過這個場景又存檔，都可能需要事後重跑一次改名工具，`KNOWN_ISSUES.md` 已補充說明。

## 2026-08-12 — 攝影機改成固定右肩視角＋控制方式改坦克式轉向

使用者自己在 Editor 調過參數後回報三件事：(1) Player1 沒貼地、飄浮感，(2) 要攝影機「永遠在角色右手邊肩膀上，角色看著前方攝影機也要看著相同方向，決不會跑到角色左邊」，(3) TrainingDummy（白柱子）整個從場景消失了。

- **TrainingDummy 消失**：詢問後確認**使用者自己故意刪的，不用復原**——現在 `GreyboxTest` 沒有敵人是預期狀態。
- **相機鎖定角色朝向、永遠不到左邊**：第一次實作直接把 `ThirdPersonCameraController` 的 `_yaw` 改成每幀讀 `target.eulerAngles.y`（角色自己的朝向），而不是滑鼠獨立控制——**這個做法直接跑 PlayMode 測試後被抓到會造成兩隱想技那種「原地旋轉」bug**：移動邏輯本來是「相對攝影機方向算移動方向，然後角色轉向去面對移動方向」，攝影機朝向又反過來等於角色朝向，形成沒有獨立參考系的封閉迴圈，純側移輸入會轉到停不下來（`CameraRelativeMovementRegressionTests` 精準抓到，見該測試 class comment 的完整歷史）。**在跟使用者確認取捨後**（見下方選項），改成正確的修法：
  - `CharacterMovement.cs` 改成**坦克式控制**（沒鎖定目標時）：A/D 原地轉向、W/S 沿目前朝向前後移動，不再是「WASD 相對攝影機平移＋自動轉向面對移動方向」。鎖定目標時维持原本邏輯不變（繞著目標走、朝向鎖定目標，這個分支從來就不在迴圈裡）。移除了 `CharacterMovement` 對 `cameraYawSource`/`ICameraYawSource` 的依賴（移動不再需要讀攝影機，直接讀角色自己的 `transform` 就好）。
  - `ThirdPersonCameraController.cs`：`_yaw` 一樣讀角色自己的 `transform.eulerAngles.y`，但這次沒有循環風險了，因為移動已經不再依賴攝影機的 yaw。`targetOffset` 的水平分量改成**用角色目前 yaw 旋轉過**才加上去（不再是攤平的世界座標加法）——不然角色一轉身，「右肩膀」就會變成隨便某個世界方向，不是真的右邊。距離/偏移的起始值（`distance=2.5`、`targetOffset=(0.5,1.4,0)`）是合理猜測，還沒讓使用者實際 Play 確認過。
  - 新增 `FixCameraToRightShoulder.cs`（取代上一輪的 `FixCameraToFirstPerson.cs`，那支保留當歷史記錄不刪除），直接開現有場景套用新數值。
- **Player 貼地**：查出 `CharacterController.height` 又是 `1`、Y 還是 `0`，重跑 `[Fix] Ground Player Spawn Height` 修成 `0.5`。
- **意外挖到一個真實、會影響正式遊戲的 bug**：`CharacterController.minMoveDistance` 預設 `0.001`，會直接丟掉小於這個值的 `Move()` 呼叫——headless batchmode 量到約 9000fps，`moveSpeed*deltaTime` 幾乎每一幀都小於這個閾值，導致移動测试測出來的位移只有預期的一小部分（一開始誤判是「測試環境時序不穩」，後來加診斷計時才發現是這個）。**這不只是測試問題，在跑得夠快的機器上（高更新率螢幕、關垂直同步）玩家的真實遊戲也可能被靜默丟幀移動**。已在 `GreyboxSceneBuilder.cs` 幫 Player／Enemy 的 `CharacterController` 都加上 `minMoveDistance = 0f`，並在所有手動建立 `CharacterController` 的 PlayMode 測試裡比照設定。
- **PlayMode 測試整批更新**：`CharacterMovementTests.cs` 的左右移動測試改成測「原地轉向、不平移」；新增「前進不會改變朝向」的迴歸測試（坦克控制的反向驗證）；`CameraRelativeMovementRegressionTests.cs` 重寫成同時涵蓋新舊兩種旋轉迴圈 bug 的成因；`EnemyAITests`／`JumpTests` 兩個測試的時間窗/容錯值原本是照著「`minMoveDistance` bug 讓移動變超慢」校準的，movement 修好後這兩個舊容錯值反而太緊，已重新校準並寫清楚原因。
- **`CharacterCollisionBlockingTests` 的 TrainingDummy 測試**：改成偵測到 TrainingDummy 不存在時 `Assert.Ignore`（跳過，不算失敗）而不是紅字失敗——因為它現在是使用者故意留空的狀態，不是迴歸。
- **順便補回 Player2 遺失的元件**：上一輪誤刪事故復原時，只用 `Player2MechaVisualSetup.Apply()` 重建了 Player2 的視覺，沒發現它原本還掛了 `CapsuleCollider`（半徑0.6/高度2.2，2026-08-11 補上避免被穿透）、`WanderMovement`（2026-08-11 隨機漫遊）、`LockOnTarget`（2026-08-11 讓 Q 鍵能鎖定）三個元件——這次全部找出來**直接寫進 `Player2MechaVisualSetup.cs` 本體**（不是另開一支 Fix 腳本），這樣以後重跑這支工具不會再把這三個元件弄丟。
- 65 個 EditMode 測試、37 個 PlayMode 測試（1 個上述原因的 Ignore）全數通過，多跑幾次確認穩定。
- **已知殘留的既有 flaky 測試**（跟今天的功能無關，不影響上面的結論）：`JumpTests.JumpPressed_WhileGrounded_LiftsPlayerUpward` 偶爾還是會抓到角色在「讓 isGrounded 穩定」的極短窗口內意外往下掉超過預期，機率大約一半——已經修正過一次明顯的重生高度誤差（0.5→1.0），但還會偶發，懷疑是 `isGrounded` 判定在極高幀率下有邊界情況，值得之後單獨排查，這次沒有繼續深挖，見 `KNOWN_ISSUES.md`。
- **仍待使用者本人 Play 一次確認**：右肩攝影機的距離/偏移是否舒服（`distance=2.5`／`targetOffset=(0.5,1.4,0)` 純猜測）；坦克式控制的手感（A/D 轉向、W/S 前後）是否符合預期；Player 貼地是否正常。

## 2026-08-12 — 攝影機/移動改回自由視角＋WASD 平移（參考原神／鳴潮）

使用者對上面右肩攝影機＋坦克式控制的結果不滿意，明確要求「改回剛剛那樣視角可以左右上下移動，a/d能角色左右移動，參考rgb遊戲原神鳴潮等等」——也就是自由視角（滑鼠上下左右都能轉）＋ WASD 相對攝影機平移（A/D 是左右平移，不是轉向），這正是這個專案原本（今天右肩視角實驗之前）就有的設計。

- **`ThirdPersonCameraController.cs`**：`_yaw` 改回滑鼠獨立控制（`delta.x * mouseSensitivity` 累加），不再讀角色自己的 `transform.eulerAngles.y`。補回 `initialYaw` 欄位。`ComputeCameraPosition` 改回單一 rotation 參數（`targetOffset` 攤平加到世界座標，不再隨角色 yaw 旋轉——自由視角下沒有「右肩膀跟著轉」的需求）。`distance`／`targetOffset` 欄位預設值改回使用者原本的手動調校值 `0.8`／`(0, 0.5, 0)`。游標重新鎖定、`SetOwnVisualHidden`（`distance=0` 時隱藏自己模型）這兩個今天稍早加的修正保留，跟視角模式無關。
- **`CharacterMovement.cs`**：改回相對攝影機平移＋自動轉向面對移動方向（`cameraYawSource` 欄位、`ICameraYawSource` 依賴全部加回來），移除今天稍早加的坦克式轉向邏輯與 `turnSpeed` 欄位。鎖定目標時的行為完全沒變。
- **`GreyboxSceneBuilder.cs`**：攝影機/移動的預設值與欄位跟著改回去；新增 `FixCameraToFreeLook.cs`（Tools/Live2DAction/[Fix] Set Camera To Free-Look (Revert Right Shoulder)）直接開現有場景套用，跟前幾次一樣沒有呼叫 `Build()`。`FixCameraToFirstPerson.cs`／`FixCameraToRightShoulder.cs` 兩支今天稍早的工具保留當歷史記錄，不刪除。
- **測試**：`CharacterMovementTests.cs`／`ThirdPersonCameraControllerTests.cs`／`LockOnFacingAndCameraTests.cs`／`CameraRelativeMovementRegressionTests.cs` 全部改回今天最早的版本（測平移／滑鼠獨立控制／鎖定不影響攝影機），只保留有價值的追加（`minMoveDistance=0` 修正、測試註解裡補上今天完整的來龍去脈方便以後查）。今天新增的「坦克式轉向」「相機鎖定角色朝向」相關測試因為設計已經改回去而移除。
- 64 個 EditMode、37 個 PlayMode 測試（1 個 TrainingDummy 已知跳過）全數通過，只剩 `KNOWN_ISSUES.md` 記錄過的既有 flaky `JumpTests` 測試偶發（跟本次改動無關）。
- **記憶更新**：`[[camera-user-tuned-values-are-authoritative]]` 這份持久記憶已同步更新，記錄今天一天內攝影機設計換了三次的完整脈絡，避免以後又被表面上的「code 跟 scene 對不上」誤導。
- **仍待使用者本人 Play 一次確認**：自由視角滑鼠轉動、WASD 平移手感是否符合預期。

## 2026-08-12 — 修正 Play 模式角色從地板彈飛懸空的真實 bug

使用者實際在互動式 Editor 按 Play 後回報：進 Play 之前明明站在地上，一按 Play 角色就從地板彈起懸空，截圖附上 Inspector 畫面。

- **AI 端一開始重現不出來**：用命令列跑了一次乾淨的 Play session（開場景→進 Play→跑 300 幀→記錄 `Player` 位置跟所有錯誤/例外），結果 `Player`（父物件）全程穩穩貼地、`isGrounded: True`，完全沒有錯誤——但這次量錯物件了，只查了 `Player` 自己的 Transform，沒有查 `Player/Visual`（Maya 模型本身）的位置。
- **使用者的截圖給了關鍵線索**：Play 前選取 `Visual` 顯示 Local Position `(0, -0.5, 0)`；Play 中同一個物件變成 `(0, 0.4, 0)`——`Visual` 這個子物件的**本地座標在 Play 期間自己動了**，而且 Inspector 上明擺著 `Visual` 掛了一個 **`Rigidbody`（Mass 80、Use Gravity 開啟）跟一個 `CapsuleCollider`**。
- **根因**：Maya 這個 Sketchfab 素材包（`Assets/_Project/Characters/Placeholder/MayaAnime/Prefabs/Maya.prefab`）**本身就內建了 Rigidbody／CapsuleCollider**，八成是原作者自己做 turntable 展示用的殘留物，跟拿掉的內嵌攝影機（`RemoveEmbeddedCameraRig`）是同一類「素材包自帶的雜物」。這個 Rigidbody 掛在 Player 底下、`m_UseGravity: 1`、`m_Constraints` 只鎖旋轉沒鎖位置——Unity 物理引擎會**獨立於 CharacterController 之外**去模擬這個子物件的位置（自由落體＋跟 Player 自己的 CharacterController 碰撞體互相推擠），跟父物件（由 CharacterController 正確地驅動、確實貼地）完全脫鉤，畫面上看到的、實際會動的 Maya 模型本體就這樣被彈飛到空中——這正是專案本來完全用不到 Rigidbody 物理（角色移動全部靠 `CharacterController`）卻意外混進來的一個真實 bug，不是猜測。
- **修正**：`PlayerMayaVisualSetup.cs` 新增 `RemoveEmbeddedPhysicsRig()`，比照移除內嵌攝影機的做法，實例化 Maya prefab 後把身上所有 `Rigidbody`／`Collider`（含子物件）都摧毀掉。重跑這支工具套用到現有場景。
- **連帶發現並修正兩個問題**：
  1. 重新套用 Maya 視覺會整個摧毀重建 `Visual`，導致 `CharacterAnimatorLink`／`AttackPoseVisualizer` 原本指向舊 `Animator`/手臂骨頭的直接參照變成空的——重新跑 `WireCharacterAnimatorLink.Apply()`／`WireAttackPoseVisualizers.Apply()` 補上。
  2. 跑 `WireAttackPoseVisualizers.Apply()` 時發現它原本要求 Player**跟** TrainingDummy 都存在才會動手，TrainingDummy 已經被使用者故意刪除，導致這支工具整個提早 return、Player 自己的部分也沒接上——改成 Player／Enemy 各自獨立判斷，缺其中一個只跳過那一半，不影響另一半。
- 用診斷用的暫時腳本（跑完即刪）在乾淨環境重新驗證：這次改成量測 `Player/Visual` 的座標，300 幀內穩定維持在 `localPosition (0,0,0)`，確認真的修好了。
- 64 個 EditMode 測試全過；PlayMode 37 個測試裡有 2 個既有的 flaky（`JumpTests`、`CharacterCollisionBlockingTests.WalkingIntoPlayer2_DoesNotPassThrough`，同一類極高幀率下物理解算不穩定的既有問題，跟這次的 Rigidbody 修正無關，詳見 `KNOWN_ISSUES.md`），1 個因 TrainingDummy 已知跳過。
- **仍待使用者本人 Play 一次確認**：這次終於是照著使用者實際回報並附截圖的真實 bug 修的，理論上應該解決了，但還是需要你本人實際按一次 Play 確認飄浮真的不會再發生。

## 2026-08-12 — 修正腳沒貼地＋命令列/互動 Editor 同時開專案造成的資料遺失

使用者回報：角色不會飛了，但腳沒踩在地板上，懸空站著。

- **根因**：`PlayerMayaVisualSetup.cs` 把 `Visual` 的位置重設成 `Vector3.zero`，但 Player 的 `CharacterController`（`center=(0,0,0)`／`height=1`）是以**膠囊體正中心**為軸心貼地，不是腳底——Maya 的模型原點在腳底（人形骨架的標準做法），所以 `Visual` 擺在軸心（世界座標 Y=0.5）會讓腳懸空在半個身高的高度。改成用 `controller.center.y - controller.height / 2f` 動態算出這個偏移量（等於 `-0.5`），這樣以後 `height` 再調也不會又對不上，比照 `GreyboxSceneBuilder` 自己算重生高度的公式做法。
- **過程中撞到一次真的資料風險**：套用這個修正時，命令列的 Unity 跟使用者自己開著的互動 Editor **同時打開同一個專案**，命令列回報「另一個 Unity 執行個體已經開著」——檢查場景檔發現 `CoverBlock2` 整個不見了、`Player` 自己的 Y 座標變回 `0`。逐一排查後：`Visual` 的名字/座標/移除的 Rigidbody 其實都正確存進去了（一開始誤判是「Visual 不見了」，其實只是它是 Prefab Instance，物件名稱存在修改覆寫清單裡，不是純文字的 `m_Name: Visual`，用錯 grep 方式找錯地方）；`CoverBlock2` 消失、`Player` Y 歸零則是真的——最可能是使用者這幾輪一直在互動 Editor 裡開著測試/看 Inspector，操作時不小心動到的（跟這個專案先前好幾次「Player 座標被意外拖動」是同一類狀況），不是命令列寫壞的。**在確認使用者的 Unity.exe 真的關閉（用工作管理員確認）之後才繼續動手**，新增 `FixCoverBlock2AndPlayerY.cs` 補回 `CoverBlock2`（照 `GreyboxSceneBuilder.cs` 原本的座標 `(-3, 0.5, -2)`）、把 Player Y 修回 `0.5`。
- **用算圖驗證，不是只看數字**：寫了一個一次性診斷腳本，把攝影機貼近角色腳邊算圖存 PNG——目視確認腳底真的踩在地磚上，沒有懸空也沒有陷進去，驗證完就把診斷腳本刪掉（不是專案功能）。
- 64 個 EditMode、37 個 PlayMode 測試（1 個既有 flaky `JumpTests`、1 個 TrainingDummy 已知跳過）全數確認過。
- **教訓，補進 `KNOWN_ISSUES.md` 的操作警語**：命令列跑 Unity batchmode 之前，一定要先用工作管理員確認使用者自己的 `Unity.exe` 真的沒在跑——單看指令「結束碼 0」不代表真的成功寫入，兩邊同時打開同一個專案時命令列有時候還是會跑完一整個流程才在下一次呼叫時才報衝突錯誤，不能只看第一次的結束碼就假設安全。
- **仍待使用者本人 Play 一次確認**：這次是照著截圖實際量出來的偏移量修的，加上算圖驗證過，但還是需要你本人實際 Play 一次確認腳真的貼地了；也麻煩留意一下 Hierarchy 裡東西有沒有其他跟你預期不符的地方（這次修的兩個意外走位只是剛好被我們發現）。

## 2026-08-12 — 攝影機加上可選的自動回正（參考原神／鳴潮，附完整規格需求）

使用者提出完整規格需求：維持自由視角＋鏡頭相對移動（不要坦克式控制、不要每幀硬鎖 `player.transform.forward`），只在玩家放開滑鼠一段時間**且**角色正在移動時，讓攝影機平滑、選擇性地往角色背後靠近；一有滑鼠輸入立刻交還控制權。

- **先盤點現狀，沒有亂猜欄位或重建**：確認場景 Hierarchy 沒有獨立的「Camera Pivot」物件（`ThirdPersonCameraController` 本來就是用 `target.position + targetOffset` 算虛擬軸心，不是真的物件）；確認 `CameraRelativeDirection` 早就只用水平 yaw 組出移動方向（`Quaternion.Euler(0f, yaw, 0f)`，從沒有 pitch/Y 分量污染過），角色只在有移動輸入時才轉向、鏡頭自己轉動不會強迫角色轉——這些需求原本就已經滿足，不用改。
- **新增自動回正**：`ThirdPersonCameraController.cs` 加 `enableAutoCenter`／`autoCenterDelay`（預設 0.8 秒）／`autoCenterSpeed`（預設 2，`Mathf.LerpAngle` 的趨近速率，不是嚴格的度/秒）／`lockOnSource`（鎖定目標時直接跳過，不跟鎖定邏輯打架）四個 Inspector 欄位。核心公式抽成 `ComputeAutoCenterYaw()` 純函式（比照 `ComputeCameraPosition`／`CameraRelativeDirection` 的既有寫法，方便寫 EditMode 測試，不用真的跑 Play）。
- **不是走回頭路**：這次刻意跟稍早那個被迴歸測試抓到 bug 的「每幀硬鎖 `_yaw = target.eulerAngles.y`」版本不一樣——是有延遲閘門（滑鼠一動立刻停止，等 0.8 秒都沒動作才開始）、平滑趨近（`LerpAngle`，不是瞬間指定）的版本。
- **但實測還是抓到一個真的會漂移的邊界情況**：純側移（按住 A 不放）+ 角色移動中自動回正同時開，會讓 `CameraRelativeMovementRegressionTests` 這個既有迴歸測試量到「0.5 秒到 2 秒之間漂移 134.8 度」——原因是純側移時角色的朝向本身還在追著「鏡頭相對側移方向」跑，這時候鏡頭又去追角色朝向，兩邊互相追逐收斂得比想像中慢很多（一開始純理論估算以為會收斂到一個固定夾角，實測發現不是這樣，SmoothDampAngle 的真實阻尼特性比簡化模型複雜）。使用者原始需求就有先預留「如果測到轉圈問題，要進一步限制自動回正條件」的授權，所以：
  - `CharacterMovement.cs` 新增 `CurrentMoveInput` 公開屬性（存下每幀的原始 W/S/A/D 輸入軸，不是換算後的世界方向），純粹是暴露既有資料，沒有新邏輯。
  - 自動回正改成只在「有實際位移（`CurrentHorizontalSpeed > 0.05`）**且**輸入是前後為主（`|moveInput.y| >= |moveInput.x|`）」時才觸發——單純側移／側移為主的斜移不會觸發，避免踩到上面那個收斂變慢的邊界情況，跟大部分動作 RPG「側移時鏡頭不會硬拉回正後方」的手感也比較接近。
- **驗證**：67 個 EditMode（含新增的 3 個 `ComputeAutoCenterYaw` 純函式測試：收斂到目標、已經在目標上不會亂動、跨 0/360 度邊界走近路）、37 個 PlayMode（`CameraRelativeMovementRegressionTests` 修正後穩定通過兩次；只剩 `KNOWN_ISSUES.md` 早就記錄過的兩個既有 flaky 物理測試，跟這次改動無關）全數確認過。
- **誠實補充一個現狀，不是這次的功能**：這個專案目前完全沒有「攝影機碰撞」邏輯（鏡頭穿牆/穿地板/穿角色都沒有防護），使用者規格裡「若目前已有攝影機碰撞功能，必須保留」這句是條件句，因為本來就沒有，這次沒有新增，維持原狀（不是被拿掉）。
- **仍待使用者本人 Play 一次確認**：自動回正的節奏（0.8 秒延遲、`autoCenterSpeed=2`）手感是否符合預期；純前進/後退時鏡頭回正是否自然；純側移時鏡頭維持不回正的手感是否OK。

## 2026-08-12 — 新增 Player4（動漫風角色 Arisa，純靜態展示站姿）

使用者要求「幫我在網路上爬取免費的3d模型 作為player4加入」，澄清風格要「動漫風角色（像 Maya 那種）」、用途是「之後可能要做成敵人或可鎖定目標」但目前先做靜態展示。

- **素材來源**：找到 Maya 同一位作者/店家（3D動漫風角色屋 / 3D Anime Character Store，Sketchfab @alex94i60）發布的「【Anime Character】Arisa (Free / Unity 3D)」，CC-BY 4.0，跟 Maya 同一套授權條款（允許商用、禁止轉售原始檔），明確提供 FBX 格式（過程中排除掉好幾個候選：一個是半身胸像不是全身模型、好幾個只提供 glTF/GLB 而專案沒裝 glTF importer、一個候選的下載說明裡有可疑的「訂閱＋跳轉 letsboost.net」下載閘門直接放棄不用）。
- **新增 `Player4AnimeVisualSetup.cs`**：比照 `PlayerMayaVisualSetup.cs` 的既有模式（`PrefabUtility.InstantiatePrefab` + 保留原始 `.meta` 讓 GUID 引用不斷、材質 Standard→URP/Lit 轉換、移除內嵌 Rigidbody/Collider/Camera），新增獨立的 `Player4` GameObject（`(5, 0, -8)`，跟 Live2D 立牌同一排），掛 `CapsuleCollider`（跟 Player2 先例一樣不讓玩家穿模）跟 `LockOnTarget`（因為使用者提到之後可能要做成可鎖定目標，`LockOnTarget` 本身沒有任何欄位/行為，只是讓 `TargetLockController` 掃描得到，先加上去成本很低）。刻意不匯入原始套件的 `Script/`／`Demo/`／`Readme/`／`_VRM/`，避免跟本專案自己的移動/攝影機腳本衝突或留下用不到的死碼。
- **算圖驗證抓到兩個問題，都修掉了**：
  1. 第一次拿診斷用暫時腳本近距離算圖，角色整個是純黑剪影——一開始以為是材質壞掉，檢查材質資料（`_BaseMap`／`_BaseColor`／`_Metallic`）其實都正確，後來算出場景平行光方向、比對相機角度，發現只是相機剛好站在角色被自己擋住陽光的那一面（背光）。換個從光源那一側拍的角度，模型其實貼圖、比例、站姿都正常（黑白水手服風格，非破損材質）。
  2. PlayMode 測試跑出「The referenced script on this Behaviour (Game Object 'Visual') is missing!」的警告噴很多次——原廠 prefab 上掛著作者自己 `Script/` 資料夾裡的元件（沒有匯入那些腳本），沒清乾淨就變成「Missing Script」的空殼元件。加了 `RemoveMissingScripts()`（`GameObjectUtility.RemoveMonoBehavioursWithMissingScript`，遞迴套用到整個 Visual 底下）清掉，重新驗證這個警告數少了一半（剩下的另一半是 Maya 自己 `Visual` 上同樣類型、這次沒動過也不在本次範圍內的既有殘留，記錄進 `KNOWN_ISSUES.md`）。
- **場景重開會讓 076/077 Live2D 立牌名稱變回空白**：這是已知的 Cubism SDK 既有問題（見前面條目），這次因為 `Player4AnimeVisualSetup.Apply()` 會重開場景，跑完後照慣例重跑一次 `FixLive2DStandeeNames.Apply()` 補回名稱。
- 67 個 EditMode、37 個 PlayMode 測試（34 過、2 個既有已記錄的 flaky——`JumpTests.JumpPressed_WhileGrounded_LiftsPlayerUpward`／`CharacterCollisionBlockingTests.WalkingIntoPlayer2_DoesNotPassThrough`，同一類極高幀率物理解算不穩定，`KNOWN_ISSUES.md` 早就記錄過、跟這次新增 Player4 無關、確認沒動過的元件、1 個 TrainingDummy 已知跳過）全數確認過，前後跑了兩輪（清 Missing Script 前後）數字一致，排除新增這個角色造成任何迴歸。
- `Docs/ASSET_LICENSES.md` 已新增 Arisa 的授權登記列（署名待辦一併更新，包含 Maya 跟 Arisa 兩個角色）。
- **仍待使用者本人 Play 一次確認**：這次的材質/光照分析是靠算圖 + 手算光源方向推理出來的合理解釋，不是在真正的互動 Editor 光照管線下親眼確認過，需要你本人實際 Play 一次看看 Player4 站在場景裡是否真的顯示正常（不是背光死黑），以及站姿/比例/位置是否符合預期。

## 2026-08-12 — Player4 轉為 AI 自主攻擊敵人＋鎖定鍵改滑鼠滾輪點按＋鎖定搜索改用角色朝向

使用者要求「把 player4 當作敵人開始製作 ai 自主攻擊模式，並且鎖定敵人從 q 改為滑鼠滾輪點按，以角色1正面視線方向向量去搜索最近的敵人來鎖定」。三個改動都先摘要受影響檔案/風險並取得確認才動手（`CLAUDE.md` 第 9 條）。

- **鎖定鍵改滑鼠滾輪點按**：`PlayerInputProvider.cs` 的 `LockOnPressed` 從讀 `keyboard.qKey.wasPressedThisFrame` 改成讀 `Mouse.current.middleButton.wasPressedThisFrame`（Input System 裡「滾輪點按」就是中鍵，沒有獨立於中鍵之外的「滾輪按下」事件）。
- **鎖定搜索方向改用角色1（Player）自己朝向，不是攝影機朝向**：核心搜尋邏輯 `TargetLockUtility.FindBestTarget` 本來就已經是「視線方向錐角內最近的目標」，這次只是把 `TargetLockController.viewOrigin` 從 `cameraController.transform`（攝影機）改成 `player.transform`（角色自己）——`GreyboxSceneBuilder.cs` 的預設值同步更新（下次重建場景生效），另外寫一支一次性 `FixLockOnViewOriginToPlayer.cs` 套用到目前已存在的場景（不能重跑整個 `Build()`，會摧毀現有場景，見 `KNOWN_ISSUES.md` 既有警語）。**真正的行為改變**：自由視角下鏡頭朝向可以跟角色朝向不同步，改用角色朝向後，站著不動只轉鏡頭去看某個敵人不會鎖定它，除非角色本身也面向那邊——這是使用者這次明確要的效果。
- **Player4 轉為 AI 自主攻擊敵人**：新增 `Player4EnemyAISetup.cs`，比照場景裡原本 `TrainingDummy`（已被使用者刪除）的既有做法：拿掉 Player4 原本站立展示用的 `CapsuleCollider`，換成 `CharacterController`（跟 Player/Enemy 同樣的預設膠囊尺寸），加上 `Health`、`EnemyAI`（`target`=Player，**維持類別本身預設的偵測/攻擊範圍 8/2**，不像 `TrainingDummy` 當年特地把 `detectionRange` 歸零——這次就是要真的會追、會打的自主攻擊敵人）、`PlayerCombat`（`inputSource`=EnemyAI，複用場景裡已經存在的 `EnemyAttack.asset`，`TrainingDummy` 被刪除後這個資產其實還留在專案裡，不用重建）。原本掛著的 `LockOnTarget` 維持不動，仍然可以被鎖定。
  - **座標系統換算，這是這次風險最高的一步**：從「站死不動、直接貼地擺放（root Y=0）」換成「CharacterController 中心點貼地（root Y=膠囊半高）」，比照 `PlayerMayaVisualSetup.VisualFeetOffset` 的公式重新算 `Visual` 的腳底偏移量，這正是 Maya 跟 Arisa 各自踩過一次的「腳沒貼地」雷。套用完用算圖驗證（用真正的 GfxDevice、選光源那一側的角度，避開之前踩過的 `-nographics` 假圖／背光死黑兩個陷阱），目視確認腳確實貼在地板上，不是懸空或陷進去。
- **新增自動化測試**：`Player4EnemyIntegrationTests.cs`（PlayMode，載入真正的 `GreyboxTest` 場景）——一個驗證 Player4 的元件/欄位真的照預期接好（`CharacterController`／`Health`／`EnemyAI.target`／`PlayerCombat.inputSource`／`comboAttacks`，且不再有殘留的 `CapsuleCollider`），另一個是端到端行為測試：把 Player 傳送到 Player4 偵測範圍內，確認真的會離開 Idle 開始追、追到範圍內真的會進入 Attacking。`EnemyAI` 本身的狀態機邏輯已經有 `EnemyAITests.cs` 涵蓋，這次新增的測試只補「Player4 在真實場景裡的接線」這塊沒人測過的部分。
- 67 個 EditMode（無新增，這次的新測試都是 PlayMode）、39 個 PlayMode 測試（37 個既有 + 2 個新增）跑了兩輪：第一輪 3 個失敗、第二輪 2 個失敗，兩輪失敗的組合都完全落在 `KNOWN_ISSUES.md` 早就記錄過的既有 flaky 測試類別（`JumpTests` 兩個測試共用同一個「起跳前置條件」斷言、`WalkingIntoPlayer2_DoesNotPassThrough`，同一類極高幀率下 `CharacterController` 解算不穩定，這次完全沒有動到跳躍/重力/Player2 碰撞體相關程式碼），新增的 2 個 Player4 測試兩輪都全過，確認沒有新的迴歸。
- **仍待使用者本人 Play 一次確認**：滑鼠中鍵鎖定的手感（是否跟滾輪縮放之類的其他滑鼠操作打架）；角色朝向鎖定搜索的手感（站著不動轉鏡頭看敵人鎖不到，是否符合預期）；Player4 實際被玩家靠近時的追擊/攻擊手感、傷害數值（沿用 `EnemyAttack.asset` 既有的 5 點傷害／frame data，沒有另外調校）。

## 2026-08-12 — 角色1／Player4 頭頂加紅色血條（100 HP，攻擊命中一次扣 10）

使用者要求「幫角色1和角色4頭頂加上紅色血條100滴血 攻擊命中一次扣10滴血」。

- **血條 UI**：新增 `Live2DAction.UI` 命名空間，`HealthBarUtility.ComputeFillAmount(currentHealth, maxHealth)` 是純函式（`Mathf.Clamp01`，`maxHealth<=0` 防除以零），`WorldSpaceHealthBar` 是掛在血條 Canvas 上的 MonoBehaviour：`Update()` 每幀把 `Health.CurrentHealth/MaxHealth` 寫進一個 `Image.fillAmount`（World Space Canvas 底下的紅色 `Filled` 類型 Image），`LateUpdate()` 讓血條的旋轉直接等於 `Camera.main.transform.rotation`（血條永遠正對鏡頭視平面，跟 `CubismBillboard` 只鎖 Y 軸的做法不同，血條不需要考慮「模型正面朝向」這種語意，直接對齊鏡頭旋轉最單純）。
- **新增 `HealthBarSetup.cs`**：在 Player／Player4 底下各生成一個 `HealthBarCanvas`（World Space Canvas + 深色 Background Image + 紅色 Filled Fill Image），位置比照 `PlayerMayaVisualSetup`／`Player4EnemyAISetup` 的既有做法，從 `CharacterController.center.y + height/2`（膠囊頂端＝頭部大略高度）往上加 0.25 個單位的邊界，而不是寫死座標——兩個角色目前的 `CharacterController` 高度剛好都是 1，但公式本身不假設這點，之後任一邊的膠囊尺寸調整都不會讓血條位置跟著跑掉。
- **`Health.MaxHealth` 本來就是 100**（`Health.cs` 的類別預設值，Player／Player4 都沒有另外覆寫過），這次沒有改動，符合「100 滴血」的要求。
- **攻擊傷害統一改成 10**：新增一次性 `FixAttackDamageToTen.cs`，把 `Assets/_Project/Settings/Combat/` 底下所有 `AttackData` 資產的 `damage` 都設成 10——**這會改變既有平衡設計**：玩家原本的三段連段是遞增的 8/10/16（見 Step 2 的連段功能條目），敵人攻擊原本是 5，這次全部拉平成 10，是使用者這次「攻擊命中一次扣10滴血」的字面要求，不是側面影響到才順手改的；如果之後想恢復連段遞增手感，`LightAttack1/2/3.asset` 的 `damage` 欄位可以直接在 Inspector 調回去。傷害值維持放在 ScriptableObject 資產裡，沒有寫死在程式碼（`CLAUDE.md` 第 7 條）。
- **新增測試**：`HealthBarUtilityTests.cs`（EditMode，6 個：滿血/半血/扣一刀後 90%/歸零/負血 clamp/`maxHealth=0` 防除以零）、`WorldSpaceHealthBarTests.cs`（PlayMode，2 個：孤立情境下 `ApplyDamage(10)` 後 `fillAmount` 真的變成 0.9；載入真實 `GreyboxTest` 場景，確認 Player／Player4 底下都有正確接線的 `WorldSpaceHealthBar`）。
- **用算圖驗證**：拿 Player 跟 Player4 各拍一張近距離角圖，確認紅色血條真的浮在頭頂、滿血狀態。Player4 那張因為算圖是在非 Play 狀態（Animator 沒有在跑，角色停在 T-pose，雙手平舉剛好跟血條同高），畫面上血條被手臂擋到一部分——這是 T-pose 的算圖限制，不是血條位置設錯，Play 模式下 Idle 動畫雙手自然下垂就不會有這個問題，已記錄進 `KNOWN_ISSUES.md`。
- 73 個 EditMode（67 既有 + 6 新增）、41 個 PlayMode（39 既有 + 2 新增）測試跑過，唯一失敗是 `KNOWN_ISSUES.md` 早就記錄過的既有 flaky `JumpTests`，跟這次改動的血條/傷害數值無關；新增的測試全過。
- **仍待使用者本人 Play 一次確認**：血條的大小/位置/跟著鏡頭轉動的手感是否符合預期；統一成 10 點傷害後戰鬥節奏（10 下打死一個角色）是否符合預期，如果覺得太快/太慢可以再調 `damage` 或 `Health.maxHealth`。

## 2026-08-12 — 修正「很靠近敵人時角色1突然消失，畫面定格」（真實 bug 回報）

使用者回報：一旦很靠近 Player4，角色1就突然消失，畫面像是定格了。

- **根因排查用診斷測試重現，不是用猜的**：寫了一支暫時的 PlayMode 測試，讓 Player 實際用真正的移動輸入（不是瞬移）走向 Player4，跑 30 秒模擬時間並每幀記錄座標。結果發現 Player 的 Y 座標從 0.58 一路爬升到 1.66（推入 Player4 幾秒內就爬到它的肩膀/頭部高度），之後卡住在 X≈5.0 附近來回震盪超過 20 秒不動——這正是使用者說的「消失＋定格」：這個專案的攝影機完全沒有防穿模邏輯（見 `KNOWN_ISSUES.md` 既有記錄），角色被推到 Player4 頭頂高度後，鏡頭很可能就卡進 Player4 的頭部模型裡，畫面看起來像整個卡住。
- **實際根因是 Unity `CharacterController.stepOffset` 的已知陷阱**：這個欄位預設值 0.3，允許 CharacterController 自動「爬上」擋在前面、高度在這個範圍內的東西——包含另一個角色自己的 CharacterController 膠囊體圓頂。兩個角色正面互推時，圓頂表面的法向量會有一部分朝上，`stepOffset` 就讓移動方看起來像在爬一小段樓梯一樣，一直推、一直往上爬，最終卡在對方頭頂附近來回震盪。**用同一支診斷測試驗證修法**：把 Player／Player4 的 `stepOffset` 都設成 0 後重跑，Y 座標整場保持在 0.58 完全沒有爬升，兩者穩定卡在正確的貼合距離（≈1.08，剛好是兩個半徑 0.5 膠囊體相加），戰鬥也持續正常進行（血量持續下降）。
- **修法**：`GreyboxSceneBuilder.cs`（`CreatePlayer`／`CreateEnemy`）與 `Player4EnemyAISetup.cs` 都把新建的 `CharacterController.stepOffset` 設成 0（這個場景是平地＋掩體方塊，掩體本來就是設計成「擋住」不是「踩上去」，設成 0 沒有副作用）；新增一次性 `FixCharacterControllerStepOffset.cs` 套用到現有場景的 Player／Player4／TrainingDummy（如果存在）。
- **順便發現並修正一個相關的次要 bug**：排查過程中發現，兩個角色卡在真正貼身距離（≈1.08）時，Player4 對 Player 的攻擊大多會打空——原因是 `PlayerCombat.ResolveActiveHit` 原本只在攻擊者正前方「Range 距離處」放一個判定球（`Physics.OverlapSphere`），如果目標實際距離比 Range 近很多（貼身距離常常遠小於 Range=1.5），判定球會直接飛過目標打到更遠的空氣。改成用 `Physics.OverlapCapsule`，從攻擊者位置一路延伸到 Range 距離都算判定範圍，貼身距離也能正常命中，原本剛好在 Range 邊界的既有測試行為不受影響（已跑過 `CombatPlayModeTests.cs`／`EnemyAttacksPlayerTests.cs` 確認）。
- **新增永久回歸測試**：`CharacterCollisionBlockingTests.WalkingIntoPlayer4_DoesNotClimbOnTop`——把 Player 推向 Player4 走 2 秒，斷言 Y 座標漂移小於 0.2（爬牆 bug 出現時實測會漂移超過 1 個單位）。過程中這個新測試第一次跑失敗，是我自己抄 `WalkingIntoPlayer2` 測試的起始 Y 偏移（`+ Vector3.up * 0.5f`）沒注意到那是配合 Player2 不同高度調的，套到 Player4 身上讓 Player 一開始就懸空，量到的其實是重力把它拉回地面的正常下降，不是爬牆——已修正成直接用 Player4 自己的 Y 當起點。
- 73 個 EditMode、42 個 PlayMode 測試（40 過、1 個既有已記錄的 flaky `WalkingIntoPlayer2_DoesNotPassThrough`、1 個 TrainingDummy 已知跳過）確認過，新增的爬牆回歸測試穩定通過。
- **仍待使用者本人 Play 一次確認**：這次是照著你的真實回報用診斷測試重現、找到根因、驗證修法有效才動手的，理論上應該解決了，但麻煩你實際 Play 一次故意走近 Player4 確認真的不會再消失/卡住；順便留意一下貼身近戰的命中手感有沒有變得比較合理。

## 2026-08-12 — 攝影機加上真正的防穿模＋血條位置/大小修正

使用者回報：把攝影機 `distance` 調到 2（使用者自己在 Editor 裡調的，這次確認並同步更新程式碼預設值）之後，角色1靠近 Player4 還是會消失，並問「是血量計算有問題嗎」；另外血條太低（應該在頭部上方）、希望再小一點、要能清楚看到血條隨傷害減少。

- **排查血量計算**：寫了多支診斷測試，模擬玩家用真正的移動輸入（含鎖定/不鎖定、直線/斜角接近）走向 Player4，追蹤血量／Y 座標／攻擊狀態／鏡頭座標。結果：血量計算完全正常（每次命中扣 10，沒有重複扣血或漏算），`stepOffset=0` 的爬牆修正也持續有效（Y 座標全程沒有再爬升）。排除了血量計算問題。
- **真正的根因：這個專案從頭到尾沒有攝影機防穿模邏輯**——這是先前就記錄在 `KNOWN_ISSUES.md` 的已知缺口，這次終於實際做了：`ThirdPersonCameraController` 新增 `enableCameraCollision`（預設開）／`cameraCollisionRadius`（0.2）／`cameraCollisionSkin`（0.15），`LateUpdate` 用 `Physics.SphereCastAll` 從角色頭部往攝影機理論位置方向做偵測，撞到東西就把攝影機拉近到障礙物前面（扣掉一點緩衝距離），不會再穿進 Player4 的模型或掩體/牆壁裡。核心夾取算式 `ClampDistanceForObstruction` 抽成純函式方便測試，實際的 Physics 查詢部分留在 `LateUpdate`（跟滑鼠讀取一樣，只在 Play 模式且有意義時才跑）。
- **攝影機距離**：確認場景裡 `distance` 已經是使用者調的 2，同步更新 `GreyboxSceneBuilder.cs` 的預設值（下次重建場景才會一致）；新的防穿模欄位都是全新序列化欄位，套件會自動套用程式碼預設值，不需要額外的一次性 Fix 腳本。
- **血條位置/大小修正**：原本用 `CharacterController.center.y + height/2` 算頭頂高度（Player／Player4 的 `height` 都只有 1），但這只是碰撞膠囊的高度，跟角色模型實際的視覺高度（含頭髮）對不太上，導致血條浮在肩膀/胸口附近而不是頭頂——改成量測 `Visual` 底下所有 Renderer 的實際世界座標邊界（`Bounds.Encapsulate`），真正抓到模型算圖後的最高點。血條尺寸從 `(0.8, 0.12)` 縮到 `(0.5, 0.06)`，邊距從 0.25 降到 0.15（頭頂量測已經比較準，不需要留這麼大空間）。用算圖驗證：血條現在清楚浮在頭頂上方，不再跟 T-pose 手臂重疊。
- **新增測試**：`ThirdPersonCameraControllerTests`（EditMode，4 個：`ClampDistanceForObstruction` 純函式的無障礙/有障礙/障礙物更遠/夾到底部不會變負數）、`ThirdPersonCameraObstructionTests`（PlayMode，2 個：真的用 `Physics.SphereCastAll` 對一個實體方塊測試攝影機會被拉近、沒有障礙物時維持原本距離）。過程中第一次跑 PlayMode 測試量到攝影機距離變成 0，排查發現是測試自己的問題——`GameObject.CreatePrimitive` 建立時物件會先在原點，測試裡緊接著設定 `.position` 但沒有呼叫 `Physics.SyncTransforms()`，導致同一畫格的 Physics 查詢還讀到舊座標（等於障礙物幾乎黏在攝影機出發點上），不是防穿模邏輯本身的 bug，已在測試裡補上 `Physics.SyncTransforms()`。
- **過程中的插曲**：跑診斷測試時 batchmode Unity 連續卡死兩次，都停在 Editor 自己的啟動流程（`Loaded scene 'Temp/__Backupscenes/0.backup'` 之後），不是這次程式碼造成的（卡住的時間點在任何測試程式碼執行之前）；強制關閉後 `Temp/UnityLockfile`／`Library/ArtifactDB-lock`／`Library/SourceAssetDB-lock` 沒有正常釋放，導致下一次啟動又卡住——清掉這三個殘留鎖檔後才恢復正常，記進 `KNOWN_ISSUES.md` 給下次參考。
- 77 個 EditMode、44 個 PlayMode 測試（41 過、1 個既有已記錄的 flaky `WalkingIntoPlayer2_DoesNotPassThrough`、1 個既有已記錄的 flaky `JumpTests`、1 個 TrainingDummy 已知跳過）確認過，新增的攝影機防穿模測試穩定通過。
- **仍待使用者本人 Play 一次確認**：這次終於補上攝影機防穿模，理論上應該能解決靠近 Player4 時的視覺消失問題，但麻煩你實際 Play 一次確認；順便看一下血條新的位置/大小是否符合預期，攻擊時是否能清楚看到血條隨傷害減少。

## 2026-08-12 — 找到「角色消失」的真正根因：Player 死亡沒有任何處理，加上原地重生

使用者回報攝影機防穿模修完後「角色依舊消失」，並附上截圖。這次因為使用者自己的互動 Editor 開著，沒辦法用命令列排查，改成請使用者打開 Console 視窗重現一次並回報看到什麼。

- **從截圖找到真正根因**：Console 只有 10 則警告（都是已知的 `Visual` Missing Script 舊問題，不是「兩萬多則警告洗到卡死」那種情況，排除了 Editor 被日誌洗死的可能）；但 Hierarchy 裡 `Player` 那一列是灰色的——Unity 顯示「GameObject 已停用」的樣式。**真正原因**：`Health.ApplyDamage` 血量歸零時本來就會 `gameObject.SetActive(false)`，但這個專案完全沒有做「玩家死亡後該怎麼辦」——沒有重生、沒有 Game Over，就是整個關掉。Player 一旦被關掉，掛在它身上的 `CharacterMovement`／`PlayerInputProvider` 全部跟著停止運作，所以按什麼鍵都沒反應，讀起來就像「畫面凍結」。而且很諷刺，同一天稍早修的兩個東西（角色不會再互相爬牆、貼身攻擊不再打空）反而讓 Player4 更容易真的把角色1打死，暴露了這個原本就存在、只是不容易踩到的破洞。
- **使用者確認要的死亡處理**：原地重生，血量補滿（三選一裡最簡單的選項）。
- **新增 `Health.ResetHealth()`**：把 `CurrentHealth` 補回 `maxHealth`、`IsDead` 清成 `false`，讓同一個 `Health` 元件之後可以正常再次受傷/死亡。
- **新增 `PlayerRespawnController.cs`**：0.5 秒延遲後（給玩家一點「你死了」的反應時間，不需要額外的 UI/Game Over 畫面）呼叫 `ResetHealth()` 並把 Player 重新 `SetActive(true)`。**刻意不掛在 Player 自己身上**——`Health.ApplyDamage` 是先 `Died?.Invoke()` 再緊接著 `gameObject.SetActive(false)`，如果重生用的 Coroutine 是從掛在 Player 上的元件啟動，Player 自己被關掉的瞬間這個 Coroutine 也會跟著被砍掉，永遠不會執行到重生那一行——改成掛在一個新的、永遠是啟用狀態的 `GameManager` GameObject 上（`GreyboxSceneBuilder.cs`／新增的 `PlayerRespawnSetup.cs` 都會建立/接好這個物件）。
- **過程中抓到自己寫的一個真實 bug**：第一版用 `OnEnable()` 訂閱 `Health.Died` 事件，但 `OnEnable()` 在 `AddComponent()` 當下就會同步執行——不管是編輯器工具用 `SerializedObject` 接欄位，還是測試用 reflection 接欄位，都是**在 `AddComponent()` 之後**才設定 `playerHealth`，代表 `OnEnable()` 訂閱的當下 `playerHealth` 還是 null，事件永遠訂閱不到，重生永遠不會觸發——`PlayerRespawnControllerTests` 第一次跑就直接抓到（斷言「1 秒內應該重生」失敗）。改成跟這個專案其他地方（`CharacterMovement.InputCommand`／`PlayerCombat.InputCommand` 的既有寫法）一樣，**不依賴 `Awake`/`OnEnable` 快取，每幀輪詢** `Health.IsDead` 的上升沿來觸發重生，不會有初始化順序問題。
- **新增測試**：`HealthTests.cs`（EditMode，2 個 `ResetHealth` 測試）、`PlayerRespawnControllerTests.cs`（PlayMode，驗證死亡後 1 秒內確實原地重生、血量補滿、`IsDead` 清除）。
- 79 個 EditMode、45 個 PlayMode 測試（42 過、2 個既有已記錄的 flaky、1 個 TrainingDummy 已知跳過）確認過，新增的重生測試穩定通過。
- **這次的排查流程也值得記錄**：使用者的互動 Editor 開著時，我沒辦法用命令列驗證/接線，改成先請使用者截圖 Console＋Hierarchy，從截圖裡的視覺線索（Hierarchy 灰階列）直接定位到根因，再請使用者關閉 Editor 讓我接手用命令列完成修改跟測試——這個「使用者截圖協助排查、確認安全後再動手」的模式，之後遇到類似情況（懷疑跟畫面/UI 狀態有關，且使用者 Editor 開著）可以優先採用。
- **仍待使用者本人 Play 一次確認**：故意讓角色1被 Player4 打死一次，確認真的會在原地重生、血量補滿，不會再卡住不動。

## 2026-08-12 — 排查血條沒扣血的回報＋重生延遲改成 5 秒

使用者回報「被攻擊時血量條貼圖不會扣」，並要求重生延遲從 0.5 秒改成 5 秒。

- **排查「血條不會扣」**：寫了三支診斷測試直接對真實場景的物件驗證——① 直接對 Player4 的 `Health` 呼叫 `ApplyDamage`，確認 `fillAmount` 正確從 1 變成 0.9；② 讓 Player 實際攻擊 Player4（走完整個 `PlayerCombat` 連段判定流程），確認 Player4 血量正確扣 10；③ 讓 Player4 實際攻擊 Player，一開始量到「血量已經扣到 90，但 `fillAmount` 還停在 1」，看起來像抓到真的 bug，但補測「再多等幾幀」後發現 `fillAmount` 在**下一幀**就正確追上 0.9 並穩定維持——這是 `WorldSpaceHealthBar.Update()` 跟造成傷害的 `PlayerCombat.Update()` 兩者執行順序不保證誰先跑，剛好卡在傷害生效的**同一幀**檢查，量到的是那一幀更新前的舊值，不是真的邏輯壞掉（多等一幀就自動追上）。三支測試都證明底層邏輯完全正常，兩個方向（Player 打 Player4／Player4 打 Player）都會正確扣血並反映在血條上。
- **最可能的解釋**：使用者的互動 Editor 在我們這幾輪修改血條/攝影機/重生邏輯期間一直開著，很可能場景/元件的最新接線狀態沒有真正重新載入過（沒有重新進 Play 模式或重新載入場景），看到的是舊狀態。**沒有改動任何血條相關程式碼**，因為找不到真的 bug 可以改。
- **新增永久回歸測試**：`WorldSpaceHealthBarTests.PlayerBar_UpdatesWhenPlayer4DamagesPlayer_InRealScene`——直接用真實場景的 Player／Player4 走一次完整攻擊流程，斷言血條數值正確反映傷害（會多等一幀避免上述的執行順序誤判），之後如果這裡真的壞掉會被自動抓到。
- **重生延遲 0.5→5 秒**：`PlayerRespawnController.respawnDelaySeconds` 類別預設值改成 5，並更新 `PlayerRespawnSetup.cs`／`GreyboxSceneBuilder.cs` 明確寫入這個數值——這個欄位在元件第一次被加進場景時就已經序列化了實際數值，只改 C# 類別預設值不會回頭更新已經存在的場景資料，所以需要透過重跑 `PlayerRespawnSetup.Apply()` 才能真正生效，不能只改程式碼就假設場景會自動跟上（跟 `stepOffset`／`CharacterController.height` 那次的教訓一樣）。
- 79 個 EditMode、46 個 PlayMode 測試（42 過、2 個既有已記錄的 flaky、1 個 TrainingDummy 已知跳過）確認過，新增的血條回歸測試穩定通過。
- **仍待使用者本人 Play 一次確認**：麻煩用**全新的 Play 階段**（重新進入/離開 Play 模式，或重新載入場景）再測一次血條會不會扣；確認重生延遲現在是 5 秒。

## 2026-08-12 — 真正找到血條不會扣的根因：`Image.Type.Filled` 沒指定 Sprite 就完全沒有視覺效果

使用者更正說明：不是「血量計算」的問題，是**畫面上的血條貼圖本身沒有視覺變化**——血條維持滿格，直到敵人被打死直接消失。並附上截圖，畫面裡看得到一個滿格的紅色血條。

- **前一輪的排查方向錯了**：前一輪只驗證了 `fillImage.fillAmount`這個「數值」有沒有正確更新（用 reflection 直接讀取 C# 屬性），確認數值正確就以為沒事，沒有真正去看「畫面渲染出來的像素」有沒有跟著變。
- **這次用畫面截圖直接比對抓到真正的根因**：寫了一支在真正 Play 模式下（不是編輯器靜態指令，那個模式 Canvas 不會正常重建幾何，一開始還被這個誤導了一次）幫 Player4 分別在滿血跟 50% 血量時截圖比對——**滿血跟 50% 血量的截圖是同一張圖**，血條完全沒有變窄。
- **真正原因**：Unity 的 `Image.Type.Filled`（血條這種「填充式」圖片的標準做法）**如果沒有指定 `Sprite`，`fillAmount` 這個數值完全不會影響畫面**——數值本身照樣正常更新（這也是為什麼前一輪讀 `fillAmount` 屬性看起來一切正常，卻抓不到真正的問題），但 Unity 內部產生「填充到一半的網格」這個運算需要實際的 Sprite 資料才能算，沒有 Sprite 就固定畫滿格的矩形，不管 `fillAmount` 是多少。`HealthBarSetup.cs` 建立 Fill/Background 兩張圖片時，一直沒有指定任何 `Sprite`。
- **修法**：`HealthBarSetup.CreateStretchedImage` 補上 `image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd")`——這是 Unity 內建的預設 UI 圖片資源，跟你在 Editor 選單「GameObject > UI > Image」建立圖片時系統自動幫你接好的是同一個，不需要另外準備美術素材。**副作用**：這個內建圖片是圓角矩形，血條外觀從方形變成橢圓形藥丸狀，如果不喜歡這個造型，之後可以換成純白色的自訂方形貼圖。
- **用真正 Play 模式截圖驗證修好了**：滿血是完整的紅色橢圓，50% 血量截圖清楚看到一半紅、一半底色——確認畫面真的會隨血量變化。
- **新增的永久回歸測試**：`WorldSpaceHealthBarTests`裡的 `AssertHasWiredHealthBar` 補上 `Assert.IsNotNull(fillImage.sprite, ...)`——這是唯一真正能抓到這個 bug 的斷言（讀 `fillAmount` 數值不夠），之後如果又有人建立新的血條/進度條沒接 Sprite，這裡會直接報錯。
- 79 個 EditMode、46 個 PlayMode 測試（43 過、2 個既有已記錄的 flaky、1 個 TrainingDummy 已知跳過）確認過。
- **教訓**：UI 元件的「數值正確」跟「畫面正確」是兩件事，UI 相關的 bug 排查不能只靠讀程式碼屬性驗證，一定要實際截圖比對渲染結果——這次第一輪排查漏掉真正的根因，就是因為只驗證了前者。
- **仍待使用者本人 Play 一次確認**：血條現在應該會隨傷害正確縮短；順便看一下橢圓形血條外觀是否可以接受，如果想要方形血條可以之後再換掉 Sprite。

## 2026-08-12 — 新增攻擊命中特效（粒子/閃光，命中點出現）

使用者確認血條修好了，接著要求加「攻擊特效」——澄清範圍是命中特效（打中目標時在命中點出現粒子/閃光），不是揮擊軌跡或畫面震動。順便問了 Maya／Arisa 兩隻角色有沒有自帶攻擊動作。

- **兩隻角色都沒有攻擊動作**：檢查 `MayaAnime`／`ArisaAnime` 的 Animator Controller，實際帶進專案、能正常播放的只有 `NewIdle`／`NewIdle02`／`NewWalk`／`NewWalkBack`／`NewRun`／`NewJump`／`NewFall`／`NewPose`（靜態展示姿勢，不是攻擊）——Controller 裡其實還有更多狀態（`crouched_covering_idle`／`Strafing`／`sneak left/right`），但引用的動畫檔案根本沒有複製進這個專案，觸發到會是空的/T-pose。這也是為什麼戰鬥系統本來就是用 `AttackPoseVisualizer` 程式驅動的揮擊角度，不是真的動畫。
- **`AttackResolver.ResolveHits` 從只回傳命中數改成回傳實際命中點**：原本回傳 `int`，這次改成 `List<Vector3>`（每個命中點的世界座標），才能知道特效要生成在哪裡，不用再多打一次 Physics 查詢。純邏輯本身沒有變，仍然保持跟 MonoBehaviour/場景無關，EditMode 可測。更新了 4 個既有測試改讀 `.Count`，新增 1 個測試驗證回傳的命中點真的落在目標表面上（不是原點或目標中心）。
- **`PlayerCombat` 新增 `hitEffectPrefab` 欄位**：命中時對每個命中點 `Instantiate` 一次，沒有指定就跳過（不影響傷害邏輯）。
- **新增 `HitEffectSetup.cs`**：純程式產生一個 `ParticleSystem` 預置物（不需要美術素材）——短暫的球形爆發（14~18 顆粒子、0.25 秒壽命）、淡黃白色、`ColorOverLifetime` 淡出、`stopAction = Destroy`（放完自己銷毀，不用額外寫「N 秒後刪除」的腳本）。材質用 URP 內建的 `Universal Render Pipeline/Particles/Unlit` shader＋Additive 混合模式，做出偏「閃光」的效果，存成 `Assets/_Project/VFX/HitEffect.prefab`，同步接到 Player／Player4 兩邊的 `PlayerCombat`。`GreyboxSceneBuilder.cs`／`Player4EnemyAISetup.cs` 也同步更新，之後重建場景會自動帶上。
- **新增測試**：`AttackResolverTests`（EditMode，改 4 個既有 + 新增 1 個命中點驗證）、`PlayerCombatHitEffectTests`（PlayMode，2 個：命中時真的生成特效在命中點附近、沒接特效時傷害邏輯不受影響也不會噴例外）。過程中兩個新測試第一次跑都失敗，都是測試本身的問題（不是程式碼壞）：① 只等 1 幀，但 `ComboAttackState` 就算 0 格 startup/active 也需要至少 3 次 `Update()` 才會真的觸發命中（Idle→Startup→Active→resolve 各佔一次 tick）；② 拿來測試的樣板物件設成 `SetActive(false)`，但 `Instantiate()` 會複製來源的啟用狀態，複製出來的實例也是停用的，`FindObjectsByType` 預設不找停用物件，誤以為沒有生成——兩個都已修正。
- **用真正 Play 模式截圖驗證**：讓 Player 站在 Player4 面前打一拳，截圖清楚看到命中點附近有一叢淡黃色粒子噴發，血條同時也確實少了一截。粒子外觀目前是方塊狀（沒有接柔邊圓形貼圖，用的是預設方形網格），能用但不夠精緻，之後如果想要更像「火花」的圓形柔邊效果，需要另外準備一張圓形漸層貼圖接到材質上。
- 80 個 EditMode、48 個 PlayMode 測試（44 過、2 個既有已記錄的 flaky、1 個 TrainingDummy 已知跳過）確認過。
- **仍待使用者本人 Play 一次確認**：命中特效的時機/大小/顏色是否符合預期；方塊狀粒子外觀能不能接受，還是想換成圓形柔邊貼圖。

## 2026-08-12 — 攻擊範圍視覺化（Gizmo）＋回答攻擊動畫問題

使用者問「如何看到兩個角色的攻擊範圍？」「可否透過手戳程式碼創造攻擊動作？」

- **`PlayerCombat.cs` 新增 `OnDrawGizmosSelected()`**：畫出跟 `ResolveActiveHit` 實際查詢時完全一樣的膠囊範圍（`attackOrigin` 出發、`attackOrigin.forward * Range` 到底端、半徑 `Radius`），連段三段各用紅→橙→黃區分。在 Scene 視窗選取 Player／Player4 就看得到；Game 視窗右上角的 Gizmos 開關打開的話，Play 模式下也看得到。純 Editor 視覺化，不影響任何遊戲邏輯，Gizmos 方法本身在正式 Build 裡會自動被排除，不需要額外的 `#if UNITY_EDITOR`。
- **確認 Maya／Arisa 都是 Humanoid 骨架**（`.fbx.meta` 的 `animationType: 3`）——代表理論上可以套用 Mixamo 等來源的免費 Humanoid 動畫（跟角色本身骨架不同也能重定向）。
- 80 個 EditMode、48 個 PlayMode 測試（45 過、2 個既有已記錄的 flaky、1 個 TrainingDummy 已知跳過）確認過，Gizmo 是純視覺化沒有動到任何測試邏輯。
- **仍待使用者本人在互動 Editor 中確認**：選取 Player／Player4 後 Scene 視窗看到的攻擊範圍框大小/位置是否符合直覺；是否要進一步做「攻擊動畫」（純程式手刻 vs. 找免費 Mixamo 動畫，兩者取捨已在對話中說明，等使用者決定方向）。

## 2026-08-12 — 真的攻擊動畫（Mixamo，取代 AttackPoseVisualizer）

使用者選擇「找免費 Mixamo 動畫套用」。從 mixamo.com 下載三個免費動作捕捉動畫（Cross Punch／Hook Punch／Uppercut，對應連段三段），確認 Maya／Arisa 都是 Humanoid 骨架後套用 Unity 的 Humanoid Retargeting 重定向到兩隻角色共用，並確認要直接關掉舊的 `AttackPoseVisualizer`（程式轉手臂骨頭那套）。

- **下載流程**：這次需要登入 Adobe 帳號才能下載，我不能幫忙輸入帳密/建帳號，改成用瀏覽器工具導到 mixamo.com，發現使用者的 Adobe 帳號其實已經是登入狀態（跳出「啟用兩步驟驗證」提示），選擇「Remind me later」跳過（不擅自幫使用者決定要不要開啟這個帳號安全設定），接著搜尋、預覽、確認下載內容（檔名/來源/大小）取得使用者同意後才下載——`Cross Punch.fbx`（424KB）／`Hook Punch.fbx`（440KB）／`Uppercut.fbx`（398KB），格式選「Without Skin」（只要骨架動畫，不要 Mixamo 自己的角色模型）。
- **`CombatAnimationImportSetup.cs`**：把 3 個 FBX 的 Import 設定改成 Humanoid（`Create From This Model`，Mixamo 自己的骨頭命名慣例可以直接自動對應，不需要指定其他角色的 Avatar），關掉 `loopTime`（攻擊動作是單發的，不該循環播放），把萃取出來的動畫片段改名成 `CrossPunch`／`HookPunch`／`Uppercut`。
- **`CombatAnimatorSetup.cs`**：用 `UnityEditor.Animations.AnimatorController` API 直接寫程式碼在 Maya／Arisa 兩份 Animator Controller 裡各自新增 `Attack1`／`Attack2`／`Attack3` 三個狀態（接上同一組動畫片段，Humanoid Retargeting 讓同一份動畫可以套用在不同骨架的角色上）、3 個 Trigger 參數、AnyState→攻擊狀態的立即轉場（不等安全時機，跟 `ComboAttackState` 的 Startup 立刻開始一致）、攻擊狀態→Locomotion 的 Exit Time 轉場（動畫播完自動回到待機/移動）。
- **`CharacterAttackAnimationLink.cs`**（新元件，取代 `AttackPoseVisualizer`）：每幀讀 `PlayerCombat.ComboIndex`，數值一變就對 Animator 打對應的 Trigger，讓真的動畫播放時機跟既有的 frame-data 連段判定同步。跟 `CharacterAnimatorLink` 一樣刻意獨立於 `PlayerCombat`（戰鬥邏輯不需要知道 Animator 存不存在）。
- **`WireCharacterAttackAnimationLink.cs`**：把新元件接到 Player／Player4，並移除 Player 身上的舊 `AttackPoseVisualizer`（Player4 從來沒有掛過，因為當初只接了 Player／TrainingDummy，TrainingDummy 已經不在場景裡）。
- **測試技巧記錄**：第一版 PlayMode 測試想直接在測試裡用 `UnityEditor.Animations.AnimatorController` 生一個假的 Animator Controller 來驗證，但 PlayMode 測試組件（`Live2DAction.PlayModeTests.asmdef`）沒有引用 `UnityEditor` 組件（刻意讓這個測試組件可以被打包進玩家版本），編譯失敗——改成直接載入真實的 `GreyboxTest` 場景，驗證 Player 身上**真正接好的** Animator 會不會在按下攻擊後真的轉到 `Attack1` 狀態，反而更貼近實際狀況也不需要碰 `UnityEditor` 命名空間。
- **新增測試**：`CharacterAttackAnimationLinkTests`（EditMode，4 個：combo index → trigger 名稱對應）、`CharacterAttackAnimationLinkIntegrationTests`（PlayMode，載入真實場景驗證按攻擊後 Animator 真的轉狀態）。
- **用真正 Play 模式截圖驗證**：Player 按下攻擊後確認 Animator 真的進入 `Attack1` 狀態（`reachedAttack1=True`），畫面上角色手臂確實是揮拳姿勢，不是原本的待機/T-pose。**這張截圖裡 Maya 的貼圖顏色看起來偏白/曝光過度**，懷疑又是診斷相機角度撞到順光/背光的問題（這次沒有動過任何 Maya 材質，只動了 Animator Controller），沒有進一步深挖，請使用者自己 Play 一次確認材質正常。
- 84 個 EditMode、49 個 PlayMode 測試（47 過、1 個既有已記錄的 flaky `JumpTests`、1 個 TrainingDummy 已知跳過）確認過。
- **已知限制**：動畫本身的播放時長跟 `AttackData` 的 frame data（startup/active/recovery）沒有互相對齊——兩者是各自獨立設定的數字，命中判定時機不一定剛好對上動畫畫面上揮拳的瞬間，之後如果要精修手感可以考慮用 Animation Event 或調整 frame data 貼近動畫實際長度。
- **仍待使用者本人 Play 一次確認**：三段連段動畫實際播放的手感、時機是否合理；Maya 的材質/貼圖在真正 Play 模式下顯示是否正常（這次截圖看起來偏白，需要人眼確認）。

## 2026-08-13 — 攻擊距離調整說明＋Player2 補上血條與受擊（不會反擊）

使用者問「如何調整攻擊距離」，並要求「幫我讓player2也有血條 也能受擊，但是他不會自主攻擊」。

- **攻擊距離調整方式（純說明，沒有改動任何數值）**：所有攻擊距離都放在 `Assets/_Project/Settings/Combat/` 底下的 `AttackData` 資產（`LightAttack1/2/3.asset`／`EnemyAttack.asset`）裡，直接在 Project 視窗點選該資產、Inspector 裡改 `Range`（攻擊距離）／`Radius`（判定粗細）欄位即可，不需要碰任何程式碼。目前 4 個攻擊都是 `Range=1.5`／`Radius=0.75`。改完可以選取 Player／Player4，靠 2026-08-12 新增的 `OnDrawGizmosSelected()` Gizmo 直接在 Scene 視窗看到新的攻擊範圍框，不用真的跑 Play 才能確認。
- **Player2 補上血條＋可受擊**：Player2 本來就有 `CapsuleCollider`（碰撞阻擋修過），這次只加 `Health` 元件（`AttackResolver.ResolveHits` 找 `IDamageable` 本來就是照 collider 所在的 GameObject 找，不需要其他改動就能被打）跟血條（沿用 `HealthBarSetup` 既有的血條生成邏輯，改成 `internal` 讓 `Player2DamageableSetup.cs` 可以直接複用，不重複寫一份）。**刻意沒有加 `PlayerCombat`／`EnemyAI`**——這正是使用者要的「能受擊但不會自主攻擊」，Player2 維持原本的漫遊＋可鎖定行為，純被動挨打。
- **新增測試**：`WorldSpaceHealthBarTests.Player2_HasHealthBarAndCanBeDamaged_ButHasNoAttackCapability`（PlayMode，載入真實場景驗證 Player2 有正確接線的血條、沒有 `PlayerCombat`／`EnemyAI`、`Health.ApplyDamage` 真的會扣血）。
- **用算圖驗證**：Player2 頭頂清楚看到滿格紅色血條，位置正確。
- 84 個 EditMode、50 個 PlayMode 測試（47 過、2 個既有已記錄的 flaky `JumpTests`、1 個 TrainingDummy 已知跳過）確認過。
- **仍待使用者本人 Play 一次確認**：Player2 血條位置/大小是否符合預期；被打時是否正常扣血、確認完全不會反擊。

## 2026-08-13 — Player2 死亡後也能復活（沿用 Player 的復活邏輯，改成通用元件）

使用者要求「設計player2可以復活」。

- **`PlayerRespawnController` 更名為 `RespawnController`**：這個元件（2026-08-12 為了修 Player 死亡後整個角色永久消失、遊戲看起來當機而做的）邏輯本來就跟 Player 沒有耦合，只需要一個 `GameObject` 目標＋它的 `Health`，所以沒有另外複製一份給 Player2，而是把類別／欄位（`player`→`target`、`playerHealth`→`targetHealth`）改成通用名稱後直接重用。**改法很重要**：用 `mv` 同時搬動 `.cs` 跟 `.cs.meta`（保留原本的 GUID），沒有直接刪掉重建——如果直接新增一個同名新檔案，Unity 會發一個新 GUID，既有場景裡 GameManager 上掛的元件就會變成「Missing Script」。
- **`GameManager` 現在掛兩個 `RespawnController`**：一個接 Player（沿用原本的 5 秒延遲、原地復活），新增一個接 Player2（同樣 5 秒延遲、原地復活——使用者沒有特別要求不同參數，沿用 Player 的設定）。新增 `Player2RespawnSetup.cs`（`Tools/Live2DAction/Add Player2 Respawn Controller`）負責接線，寫法上會先找 `GameManager` 上是否已經有一個 `target` 指向 Player2 的 `RespawnController`，避免重複執行時疊加出兩個。
- **新增測試**：`Player2RespawnControllerTests`（PlayMode，2 個）——`Player2HealthReachesZero_RespawnsInPlaceWithFullHealthAfterDelay` 驗證 Player2 死亡後會在原地滿血復活；`TwoRespawnControllersOnSameGameManager_EachRevivesOnlyItsOwnTarget` 驗證 `GameManager` 上同時掛 Player 跟 Player2 兩個 `RespawnController` 時，兩者互不干擾（打死 Player2 不會誤觸 Player 的復活邏輯，反之亦然）。既有的 `PlayerRespawnControllerTests` 沿用元件更名後的欄位名稱，測試本身沒有改邏輯，繼續過。
- **場景內驗證**：寫了一個暫時性的 PlayMode 診斷測試，直接對 `GreyboxTest` 場景裡真正的 Player2 GameObject 打致命傷，斷言死亡後 `activeSelf` 立刻變 `false`、5 秒延遲內重新變 `true` 且滿血——全部通過。有嘗試截圖佐證，但場景主攝影機是綁定 Player、不是 Player2，三張截圖看起來一樣（因為鏡頭本來就沒對著 Player2），視覺上沒有參考價值，因此改以斷言結果為準；驗證完立刻刪除診斷腳本、其 `.meta` 與截圖，沒有留下任何暫存檔。
- 84 個 EditMode、52 個 PlayMode 測試（48 過、3 個既有已記錄的 flaky `JumpTests`／`CharacterCollisionBlockingTests.WalkingIntoPlayer2_DoesNotPassThrough`、1 個 TrainingDummy 已知跳過）確認過。
- **仍待使用者本人 Play 一次確認**：Player2 死亡後 5 秒的等待感受起來是否合理；復活瞬間有沒有任何視覺突兀（例如原地淡入/瞬間出現，目前跟 Player 一樣是直接 `SetActive(true)`，沒有做任何淡入效果）。

## 2026-08-13 — 修正：Player 復活失效（同日更名 `RespawnController` 造成的真實回歸）

使用者回報「現在角色1不會復活」。**真正根因**：更名 `PlayerRespawnController`→`RespawnController` 時欄位也一起改名（`player`→`target`、`playerHealth`→`targetHealth`）——Unity 是照欄位名稱序列化資料的，Player 身上**原本就存在**的那個元件實例，資料是存在舊欄位名稱底下的，改名後舊資料變成孤兒、新欄位名稱從來沒有被序列化過，所以直接變成 `null`。`RespawnController.Update()` 一開始就檢查 `targetHealth == null` 直接 `return`，所以 Player 死亡後這個元件完全不會做任何事——這正是使用者說的「不會復活」。

- **當下修法沒有做對第一次**：只重跑了 `Player2RespawnSetup.Apply()`（幫新加的 Player2 接線），忘記同時重跑 `PlayerRespawnSetup.Apply()` 把 Player 原本就存在的元件重新接上新欄位名稱。
- **第二次修的時候又發現更深一層的問題**：重跑 `PlayerRespawnSetup.Apply()` 後 Player 確實接好了，但兩支接線工具（`PlayerRespawnSetup`／`Player2RespawnSetup`）原本的比對邏輯只找「`target` 剛好等於目標角色」的元件，找不到就直接新增一個——完全沒有考慮「`target` 是 `null` 的孤兒元件」這種情況，導致 `GameManager` 上留下 3 個 `RespawnController`：1 個永久失效的孤兒（來自更名當下）＋ 2 個正確接線的新元件。已修正兩支工具的比對邏輯，改成優先精準比對，找不到才回收孤兒元件（`target == null`）重新接線，真的沒有孤兒才新增——這樣以後如果又發生類似的欄位改名，重跑一次接線工具就會自我修復，不會再一直疊加壞掉的元件。新增 `RespawnControllerCleanup.cs`（`Tools/Live2DAction/Remove Orphaned Respawn Controllers`）把這次已經產生的孤兒元件清掉。
- **新增測試**：`RespawnControllerSceneWiringTests`（PlayMode）直接載入真實 `GreyboxTest` 場景，斷言 `GameManager` 上剛好有 2 個 `RespawnController`、且分別正確指向 Player／Player2 的 `Health`——這類「場景內既有元件的實際接線資料」的 bug，用「建立全新元件、reflection 直接設欄位」的單元測試（例如原本的 `PlayerRespawnControllerTests`）完全測不到，一定要像 `WorldSpaceHealthBarTests` 的場景測試那樣，載入真實場景檢查真正存在的資料才抓得到。
- **教訓**：`CLAUDE.md` 已經有「重命名序列化欄位會讓場景舊資料變成孤兒」的認知，但這次只顧著記得「要重跑接線工具」，沒有想到「重跑的時候，工具本身要有能力認出並修復孤兒資料，不能只會新增」——單純重跑一次是不夠的，工具的比對邏輯也要對孤兒資料有防禦性處理。
- 84 個 EditMode、53 個 PlayMode 測試（50 過、2 個既有已記錄的 flaky `JumpTests`、1 個 TrainingDummy 已知跳過）確認過，Player 現在死亡後確實會在 5 秒後原地滿血復活。

## 2026-08-13 — 鎖定目標改用鏡頭（滑鼠）朝向判斷，不再要求角色本身面對敵人

使用者問「目前鎖定目標需要角色去面對敵人，能不能改為鼠標鏡頭面相來判斷?」。這是 2026-08-12 明確要求「鎖定用角色自己面向判斷」的反向改動——`TargetLockController.viewOrigin` 這個欄位本來就是可以指到任意 Transform 的（`FindTarget()` 只是讀取 `viewOrigin.forward`，沒有 `viewOrigin` 才退回 `transform.forward`），改動不需要碰任何判斷邏輯，只需要把場景裡這個欄位從「Player 自己的 Transform」重新接到「Main Camera 的 Transform」——`ThirdPersonCameraController.LateUpdate()` 本來就每幀把攝影機的旋轉同步成滑鼠拖出來的 `_yaw`/`_pitch`，所以攝影機的 `forward` 天生就是「滑鼠鏡頭目前朝向」。

- **改動範圍**：`GreyboxSceneBuilder.cs`（供之後重新從零建置場景用）＋新增 `LockOnViewSourceSetup.cs`（`Tools/Live2DAction/Use Camera Facing For Lock-On`，套用到已經建好的現有場景）。距離/範圍判定（`maxLockRange`）仍然是從角色本人的位置量測，不受這次改動影響；鎖定後「攝影機不會轉、只有角色自己的朝向會轉向目標」這個既有行為也完全沒動。
- **新增測試**：`TargetLockControllerTests.LockOnPressed_ViewOriginFacesCandidateButCharacterFacesAway_StillLocksOn`——刻意讓角色本身面向跟目標相反的方向，只有 `viewOrigin`（模擬攝影機）面向目標，驗證按下鎖定鍵依然能鎖到，證明判斷依據真的換成 `viewOrigin` 而不是角色自身朝向。
- 84 個 EditMode、54 個 PlayMode 測試（51 過、2 個既有已記錄的 flaky `JumpTests`／`WalkingIntoPlayer2_DoesNotPassThrough`、1 個 TrainingDummy 已知跳過）確認過。
- **仍待使用者本人 Play 一次確認**：實際用滑鼠轉鏡頭對準敵人再按鎖定鍵，手感是否符合預期；因為 `TargetLockController.Update()` 早於 `ThirdPersonCameraController.LateUpdate()` 執行，讀到的攝影機朝向會落後一幀（毫秒等級，理論上感受不到），如果實際玩起來鎖定判定感覺「差一點點」，這是已知、可能的原因。

## 2026-08-13 — 修正：敵人攻擊距離加長後「沒有被隔空打到」＋角色碰撞體總體檢

使用者先前把 `EnemyAttack.asset` 的 `Range` 自己調到 7.5（`Radius` 1.5，約原本的5倍，Editor 裡直接調的，還沒提交），實際玩過後回報「我沒有被敵人隔空打到」，並要求檢查所有角色碰撞體是否都有套用。

- **真正根因**：`AttackData.Range`（攻擊判定膠囊能打多遠）跟 `EnemyAI.attackRange`（AI 自己判斷「夠近了、可以開始攻擊」的門檻）是兩個完全獨立的欄位。使用者只改了前者，Player4 場景裡的 `attackRange` 還停在類別預設值 2——代表 Player4 永遠要先走到跟玩家距離 2 以內才會進入 Attacking 狀態開始出手，不管 `Range` 調多長，AI 根本沒機會在遠處觸發攻擊，所以感受不到任何「隔空」的效果。
- **修法**：保留使用者已經調好的 `Range=7.5`／`Radius=1.5`（沒有動這組數值），新增 `EnemyAttackRangeSync.cs`（`Tools/Live2DAction/Sync Player4 Attack Range To EnemyAttack Data`）——動態讀取 `EnemyAttack.asset` 目前的 `Range`，把 Player4 的 `attackRange` 同步成「`Range` 減 0.5 緩衝」（這次算出來是 7），而不是寫死一個數字，這樣以後不管 `Range` 再怎麼調，重跑這支工具就會自動保持同步，不會重蹈同一個坑。順便檢查 `detectionRange`（AI 開始注意到玩家的距離，目前 8）有沒有小於新的 `attackRange`，太小的話會一起補上去，不然 AI 會變成「永遠沒發現玩家所以攻擊距離設定形同虛設」。
- **角色碰撞體總體檢結果**：實際掃描場景所有根物件，戰鬥相關角色都有正確套用碰撞體——Player（`CharacterController`, radius 0.5）、Player4（`CharacterController`, radius 0.5）、Player2（`CapsuleCollider`, radius 0.6，被動可受擊）都各自搭配 `Health` 元件，攻擊判定打得到。076/077 Live2D 立牌跟 FemaleStandee 這三個純視覺展示物件刻意沒有碰撞體/`Health`——這是設計上的預期行為（它們本來就只是靜態展示，沒有接任何戰鬥邏輯），不是漏掉。
- **新增測試**：`EnemyAttackRangeSceneTests.Player4_AttacksPlayerFromRangeWithoutClosingToMeleeDistance_InRealScene`（PlayMode，載入真實場景，把玩家放在距離 Player4 5 個單位遠的地方，驗證 Player4 真的能在不用先走近的情況下命中——如果之後 `attackRange` 又意外跟 `Range` 脫鉤，這個測試會抓到）。
- 84 個 EditMode、55 個 PlayMode 測試（53 過、1 個既有已記錄的 flaky `JumpTests`、1 個 TrainingDummy 已知跳過）確認過。
- **仍待使用者本人 Play 一次確認**：現在 Player4 應該會在明顯比之前遠的距離開始攻擊，實際手感是否符合預期；因為 `attackRange(7)` 很接近 `detectionRange(8)`，Player4 發現玩家後幾乎只追一小段距離（約1個單位）就會開始攻擊，如果覺得「幾乎沒有追逐感、一發現就打」，這是這組數值下的直接結果，需要的話可以再調整 `detectionRange`／`attackRange` 的差距拉開一點。

## 2026-08-13 — Player4 攻擊距離縮小到3倍＋加上死亡復活

使用者實際玩過上面那組 `Range=7.5` 的設定後回報兩件事：「發現敵人死了不會復活」、「敵人離我離得很遠就開始原地揮拳」。確認方向後動手：

- **攻擊距離縮小**：`EnemyAttack.asset` 的 `Range`／`Radius` 從 7.5／1.5 調回 4.5／1（3倍，使用者在兩個選項——「縮小到2~3倍」／「維持7.5」——之間選了前者）。原因：判定距離(7.5)遠遠超過揮拳動畫本身的視覺長度，玩起來像「站在很遠的地方對空氣揮拳卻真的打到人」，違和感明顯。套用時發現資產檔案裡的暫存值又被改成了 `range=1`（使用者應該還在 Editor 裡自己試），沒有沿用那個暫時值，直接套用確認過的 3 倍數字。重跑 `EnemyAttackRangeSync.cs` 把 `EnemyAI.attackRange` 同步成 4（原本因為 Range=7.5 同步出來的 7 也一併更新），跟 `detectionRange`(8) 的差距從原本只剩 1 拉開到 4，追逐感也跟著自然一點。
- **Player4 死亡後也能復活**：原本是刻意設計成「打倒＝永久關掉」（見先前 `KNOWN_ISSUES.md` 條目），使用者這次明確要求改成跟 Player／Player2 一致。新增 `Player4RespawnSetup.cs`（`Tools/Live2DAction/Add Player4 Respawn Controller`），在 `GameManager` 上加第三個 `RespawnController` 指向 Player4，沿用相同的 5 秒延遲、原地滿血復活，邏輯完全重用既有的 `RespawnController` 元件，沒有另外寫一份。至此 Player／Player2／Player4 三個角色都會復活，敵人（Player4）也不會再打一次就永久消失。
- **新增測試**：`Player4RespawnControllerTests`（PlayMode，Player4 死亡後原地滿血復活）；`RespawnControllerSceneWiringTests` 從驗證「剛好 2 個 `RespawnController`」擴充為「剛好 3 個」，三個角色分別正確接線。既有的 `EnemyAttackRangeSceneTests` 不用改邏輯（距離門檻改成 4 之後，測試原本設定的「站在 5 個單位遠」還是會先追近一點點才攻擊，但斷言「沒有貼身到 2 個單位內」依然成立，只更新了說明性註解裡的數字）。
- 84 個 EditMode、56 個 PlayMode 測試（52 過、2 個既有已記錄的 flaky `JumpTests`／`WalkingIntoPlayer2_DoesNotPassThrough`、1 個 TrainingDummy 已知跳過）確認過。
- **仍待使用者本人 Play 一次確認**：新的攻擊距離（4.5）跟揮拳動畫搭配起來的違和感是否已經改善；Player4 死亡復活後的手感（例如連續刷怪打起來節奏是否合理，5秒延遲是不是太短/太長）。

## 2026-08-13 — 攻擊距離／警備距離用不同顏色 Gizmo 呈現（角色1、4都有）

使用者要求「能不能把 攻擊距離 警備距離 用不同顏色線條呈現嗎 角色1和4都要」。攻擊距離早就有膠囊 Gizmo（`PlayerCombat.OnDrawGizmosSelected`，紅/橙/黃區分連段），這次確認後維持不動，只新增「警備距離」這個新概念。

- **「警備距離」對 Player（角色1）跟 Player4 對應到不同欄位，兩者概念相同（「這個角色多遠能注意到東西」）但方向相反**：Player4 是 AI，`EnemyAI.detectionRange`（多遠會注意到玩家）本來就有明確對應；Player 是玩家操作、沒有 AI，使用者確認用 `TargetLockController.maxLockRange`（玩家能偵測/鎖定敵人的範圍）當作對應欄位——概念上一個是「敵人多遠能發現我」，一個是「我多遠能發現敵人」，方向相反但都是「警備／感知範圍」。
- **各自畫在自己的元件上**：`TargetLockController.OnDrawGizmosSelected()`／`EnemyAI.OnDrawGizmosSelected()` 各自新增，畫一顆以 `maxLockRange`／`detectionRange` 為半徑的青色（`(0.2, 0.9, 1, 0.5)`）線框球，跟攻擊距離的紅/橙/黃刻意區分開，一眼就能分辨這是「感知範圍」不是「攻擊範圍」。沒有集中寫成一個獨立的 Gizmo 工具腳本——延續這個專案的既有模式，每個元件負責畫自己身上可調的數值（`PlayerCombat` 畫自己的攻擊距離，這次也讓 `TargetLockController`/`EnemyAI` 畫自己的警備距離）。
- 純 Editor 視覺輔助，不影響任何執行期邏輯，`OnDrawGizmosSelected` 只在 Editor 選取物件時執行，不會被打包進正式 Build（跟 `PlayerCombat` 那個既有 Gizmo 的既定行為一致）。84 個 EditMode 全過；PlayMode 部分因為使用者這次同時在 Editor 裡持續調整 `EnemyAttack.asset`／場景裡的 `attackRange`（跟這次 Gizmo 改動無關），出現了一個非既知的測試失敗，細節見下方另一則條目，跟本次 Gizmo 功能本身無關（純視覺方法，不會影響任何測試邏輯）。
- **仍待使用者本人在 Scene 視窗確認**：選取 Player／Player4，青色圓圈半徑是否符合預期；沒有做自動化截圖驗證——Gizmo 是 Unity Editor 的 SceneView 疊加層，不是相機實際渲染畫面的一部分，批次模式下用 `Camera.Render()` 截圖截不到 Gizmo，這點延續 2026-08-12 那個攻擊距離 Gizmo 一開始就是靠使用者自己在 Scene 視窗肉眼確認的做法。

## 提醒：`EnemyAttack.asset` 又被改動，`EnemyAttackRangeSceneTests` 因此暫時失敗（非程式碼回歸）

這次做 Gizmo 功能時順便跑了完整測試，發現 `EnemyAttack.asset` 又變了（目前 `range=1.5`／`radius=1`，跟這次 3× 的 4.5／1 不一樣），但場景裡 Player4 的 `attackRange` 還停在上次同步的 4——兩者又不同步了，跟之前那次「隔空揮拳」根因一樣，但這次方向相反：AI 判斷「距離4以內就夠近了」開始攻擊，但攻擊判定膠囊其實只有 1.5 那麼長，導致 Player4 得一路追到接近貼身距離（測試量到 1.497）才真的打中，`EnemyAttackRangeSceneTests` 因此斷言失敗。**這不是這次 Gizmo 改動造成的，是資料本身在使用者手上持續變動**，只是剛好這次順便跑測試才發現。已回報給使用者確認要維持目前這組數值（並重跑 `EnemyAttackRangeSync.cs` 讓 `attackRange` 跟上）還是要換回 3× 那組。

## 2026-08-13 — 玩家／敵人攻擊距離 Gizmo 顏色分開＋判定頂端加上實心標記

使用者要求「我需要分開敵人與玩家的攻擊判定 攻擊距離物件 並且顏色都要有區別，最好攻擊判定頂端要有更明顯的視覺效果」——目前 Player（LightAttack1/2/3）跟 Player4（EnemyAttack）共用同一個 `PlayerCombat.OnDrawGizmosSelected()`，也共用同一組紅/橙/黃配色，導致 Player 的第一段攻擊（紅）跟 Player4 唯一的攻擊（也是紅，因為 `comboAttacks` 陣列只有一格、index=0）視覺上完全分不出來。

- **玩家／敵人改用完全不同的色系，不只是深淺不同**：Player 現在是綠色系（`PlayerGizmoColors`，三段連段由淺到深），Player4 是紅色系（`EnemyGizmoColors`）——特意選跟警備距離的青色（2026-08-13 較早新增）也不衝突的兩組顏色，一眼就能分辨「這是我的攻擊範圍」還是「這是敵人的攻擊範圍」還是「這是感知範圍」。
- **怎麼判斷是玩家還是敵人**：`PlayerCombat` 本身沒有玩家/敵人的旗標欄位——用 `GetComponent<EnemyAI>() != null` 判斷，這正好是 Player4 之所以能重用同一個 `PlayerCombat` 元件的既有機制（`EnemyAI` 實作 `IInputCommand`，靠這個假裝成輸入來源），不需要另外新增欄位或改任何接線工具。
- **攻擊判定頂端（`far`，攻擊實際能打到多遠的那一點）新增實心球標記**：其餘線框都是半透明線條，`far` 那一點額外畫一顆不透明的實心球（`Gizmos.DrawSphere`，半徑是 `Radius` 的一半），讓「這個攻擊到底能打多遠」在一堆線框裡最顯眼，不會跟攻擊者自己身體那端（`near`）混在一起同等顯眼。
- 純 Editor 視覺輔助，不影響任何執行期邏輯，跟先前的 Gizmo 改動一樣沒有自動化截圖驗證（Gizmo 是 SceneView 疊加層，批次模式相機截圖截不到），需要使用者自己在 Scene 視窗選取 Player／Player4 肉眼確認。
- 84 個 EditMode 全過；PlayMode 55 過 3 個失敗，2 個是既有已記錄的 flaky `JumpTests`，1 個是上一則條目提到、還沒解決的 `EnemyAttackRangeSceneTests`（`EnemyAttack.asset` 資料還在使用者手上持續變動，跟這次 Gizmo 顏色改動無關，等使用者確認要保留哪組數值）。

## 2026-08-13 — 修正：攻擊距離 Gizmo 兩顆線框球重疊糾結，看不清楚邊界

使用者附截圖回報「線條很多，紅色的有兩圈銜接，我分不清楚攻擊距離的邊界在哪」——`Radius` 相對 `Range`偏大時（目前敵人這組數值正是如此），`near`／`far` 兩顆線框球幾乎疊在一起，糾結成一團看不出真正的邊界在哪。

- **修法**：拿掉 `near` 那顆線框球（攻擊者自己的角色模型本來就清楚標示了「這裡是起點」，畫一顆圈完全是多餘的），也拿掉 `far` 那顆線框球，改成只在 `far`（攻擊實際能打到多遠）畫**一顆**不透明實心球，而且半徑用 `Radius` 原始大小（不是先前縮小一半的裝飾用小球）——整個 Gizmo 現在只剩一個圓形標記，就是真正的判定邊界，不會再有第二顆容易混淆的圈。中間 4 條連接線保留，仍然如實呈現攻擊的寬度。
- 純 Editor 視覺輔助，不影響任何執行期邏輯／任何測試斷言。84 個 EditMode 全過；`CombatPlayModeTests` 額外單獨確認過攻擊判定本身沒有受影響（膠囊查詢的實際半徑/距離完全沒變，只是不畫線框球了）。
- **仍待使用者本人在 Scene 視窗確認**：新的單一實心球邊界標記是否夠清楚。

## 2026-08-13 — 修正：攻擊距離 Gizmo 頂端實心球太大，反而擋住線條

上一則條目改成「唯一一顆全尺寸實心球」標記邊界，使用者馬上回報「頂端紅色區塊會遮擋線條影響判斷」——半徑用 `Radius` 原尺寸的實心球是一大塊不透明的 3D 物件，把匯聚進來的 4 條連接線跟旁邊的東西都蓋住了，反而比之前更難讀。

- **修法**：邊界標記改回線框圓（`Gizmos.DrawWireSphere`，只有細線、不會遮擋任何東西），這次只畫**一顆**（不是先前那個「兩顆疊在一起」的版本），所以「兩圈糾結」的問題還是解決的。中心再加一個很小的實心點（半徑只有 `Radius` 的 15%）做「這裡是重點」的視覺強調，因為夠小，不會蓋住匯聚進來的線條。
- **教訓**：兩次回報合起來看，问题其實是同一件事的兩個極端——完全不用實心（線框太多重疊）跟完全用大實心（擋住東西）都不對，關鍵是「邊界標記本身要用不佔視覺面積的線框，額外的『更明顯』效果要用很小的裝飾物，不能拿邊界標記本身去放大」。
- 這次改動時使用者的 Unity Editor 是開著的（互動模式，非批次），照慣例先問過、使用者選擇關閉 Editor 後才繼續跑批次模式驗證。
- 純 Editor 視覺輔助，不影響任何執行期邏輯。84 個 EditMode 全過；`CombatPlayModeTests` 額外確認攻擊判定本身沒有受影響。
- **仍待使用者本人在 Scene 視窗確認**：這次的線框圈＋小實心點組合是否終於清楚又不擋視線。

## 2026-08-13 — 攻擊範圍 Gizmo 改成動態偵測：真的有人站進來才會亮

使用者反饋「還是很難判斷 有沒有明確的視覺表達方式 能讓我知道究竟有沒有進入到攻擊範圍」——之前幾輪都只是調整靜態線框的畫法，但線框本身不會因為有沒有人站在裡面而改變，玩家還是得自己用肉眼估角色是否落在那個 3D 形狀裡，在任意鏡頭角度下其實很難準確判斷。

- **改成動態偵測，不再是純裝飾線框**：`OnDrawGizmosSelected()` 現在會即時跑一次跟 `ResolveActiveHit` 完全一樣的 `Physics.OverlapCapsule` 查詢（同樣的 near/far/radius，同樣排除自己的 `transform.root`），只要真的有 `IDamageable` 目標在範圍內，整個攻擊判定就變成醒目不透明的亮黃色實心球；沒有目標在範圍內就維持原本低調的線框圈＋小實心點。這樣「有沒有進入攻擊範圍」不再是用眼睛估，而是跟真正的傷害判定共用同一個查詢算出來的真實答案——不管是在 Edit 模式（場景裡碰撞體本來就在，不用真的按 Play）還是 Play 模式中都能用。
- **亮黃色是刻意挑的新顏色**：不跟玩家綠色系、敵人紅色系、警備距離青色系任何一組重複，一看到黃色亮起就知道「現在真的有東西在攻擊範圍裡」，不會跟其他既有的顏色語意混淆。
- **過程中一次真實的批次模式環境問題**：驗證這個新邏輯時新增了一個暫時性診斷測試（用 reflection 呼叫私有的 `IsAnyDamageableInRange`），跑批次模式時連續 3 次卡在 Unity 啟動流程的 `TrimDiskCacheJob` 那一步（跟這次程式改動無關——同一批 EditMode 編譯檢查／`CombatPlayModeTests` 都已經先正常跑完過），強制關閉＋清 lock 檔都重試過仍然卡住；後來單獨再跑一次同樣的 EditMode／PlayMode 檢查卻又立刻正常完成，判斷是這次環境暫時性的卡頓（懷疑可能跟稍早使用者關閉互動 Editor 後留下的 `Temp/__Backupscenes/0.backup` 或當下的磁碟/USB裝置掃描有關，沒有深挖根因），不是專案程式碼的問題。放棄了這個額外的自動化驗證（刪除診斷測試），改成單純依賴既有的 EditMode 編譯檢查＋`CombatPlayModeTests` 不受影響的確認，邏輯本身（`Physics.OverlapCapsule` + 排除自身 `root`）直接複用 `ResolveActiveHit` 已經被大量測試覆蓋過的同一套查詢方式，風險評估上可以接受。
- 84 個 EditMode 全過；`CombatPlayModeTests`（2 個）確認攻擊判定邏輯本身沒有受影響。這次改動時使用者的 Unity Editor 一度是開著的，照慣例先問過、使用者選擇關閉 Editor 後才繼續。
- **仍待使用者本人在 Scene／Game 視窗確認**：把角色移到攻擊範圍內外，觀察亮黃色是否真的隨著距離即時切換。

## 2026-08-13 — 修正：亮黃色實心球把角色整個包住看不見了

使用者回報「不要這樣包覆整個物體 看不見 能不能作成判定區域邊界最外圍畫一條線或是製作一個圓點標記，代表極限距離 碰到這個一定被攻擊」——上一輪改成「有目標在範圍內就整顆變成全尺寸亮黃色實心球」，這顆球剛好會把站在那個位置的角色整個包住蓋住，變成看不到人。

- **修法：徹底不再填滿任何東西，「有沒有進入範圍」改成邊界線本身變色**：拿掉填滿的實心球，攻擊範圍全程只用細線／小點表示——4 條側邊連接線＋最外圍一顆線框圓（就是 `Physics.OverlapCapsule` 實際判定用的 `Radius`，準確代表「碰到這條線一定被攻擊」的極限距離）＋中心一個很小的參考點（半徑只有 `Radius` 的 12%，遠小於角色本身大小，不會擋住任何東西）。有目標真的進入判定範圍時，整組線條（含邊界圓）變成醒目的亮黃色，邊界圓額外疊 2 圈幾乎貼在一起的線框圓做出「變粗」的效果（Gizmos 沒有線寬控制，只能疊圈模擬），但從頭到尾都只是線條，不會有任何實心區塊把角色蓋住。
- **這是同一天第 4 次根據實際回饋調整這個 Gizmo**：① 兩顆線框球疊在一起看不清邊界 → 拿掉一顆；② 改完的實心球太大遮擋線條 → 縮小成線框圈＋小實心點；③ 靜態線框看不出「究竟有沒有進入範圍」→ 改成動態偵測，但用了填滿的實心球；④ 這次回報實心球把角色整個包住看不見 → 徹底改成「邊界線本身變色變粗」，不再有任何填滿的形狀。教訓已經記錄在 `KNOWN_ISSUES.md`（Gizmo 這類視覺設計光憑文字描述很難一次到位，需要使用者實際看過才會收斂到真正的解法）。
- 84 個 EditMode 全過；`CombatPlayModeTests`（2 個）確認攻擊判定邏輯本身沒有受影響——這次改動純粹是畫法，判定用的 `Physics.OverlapCapsule` 查詢完全沒變。
- **仍待使用者本人在 Scene／Game 視窗確認**：這次的「邊界線變色變粗＋小參考點」是否終於做到「看得到人、也看得出有沒有進範圍」兩者兼顧。

## 2026-08-13 — 修正真實 bug：視覺呈現（Gizmo）跟 Player4 實際攻擊判定不一致

使用者回報「我已經盡到敵人範圍內，線條從紅色變成黃色，但敵人尚未作出攻擊，這代表視覺呈現與數值邏輯判定很明顯不一致，請去校正」——這不是 Gizmo 畫法問題，是**真正的遊戲邏輯 bug**，Gizmo 只是恰好把它暴露出來。

- **根因**：`PlayerCombat` 的 Gizmo（跟 `ResolveActiveHit` 真正的傷害判定）用的是 `Physics.OverlapCapsule`，這個判定膠囊的實際最遠可達距離是 `Range + Radius`（膠囊遠端本身是一顆半徑 `Radius` 的球，會比 `Range` 那個點再往前多伸出 `Radius`）。但 `EnemyAI` 自己決定「要不要攻擊」用的是完全不同的邏輯——單純的全方向球體距離判斷（`Vector3.Distance <= attackRange`），`attackRange` 是另一個獨立欄位，需要靠 `EnemyAttackRangeSync.cs` 手動同步。這次場景裡 `attackRange` 又跟 `EnemyAttack.asset` 的 `Range` 脫鉤了（卡在舊值 1，`Range`／`Radius` 已經改成 1／1，真正膠囊可達距離其實是 2）——玩家站在 1~2 之間時，Gizmo（用膠囊算）判定「打得到」變黃色，但 `EnemyAI`（用單純球體算，只到 1）還在判斷「不夠近」，於是出現使用者看到的矛盾。
- **這次不是單純再同步一次數字，是徹底修正架構性的落差**：新增 `PlayerCombat.PrimaryAttack` 公開屬性（讓外部讀到目前實際會用的 `AttackData`），`EnemyAI` 新增可選的 `combat` 欄位——一旦接上，「要不要攻擊」的判斷改成每一幀直接從 `PrimaryAttack.Range + PrimaryAttack.Radius` 即時算出來，不再是一個需要手動保持同步、遲早又會跟真正判定脫鉤的獨立數字。`combat` 沒接時（例如既有的獨立單元測試）行為完全不變，繼續退回原本的 `attackRange` 欄位，向下相容。新增 `Player4EffectiveAttackRangeSetup.cs`（`Tools/Live2DAction/Wire Player4 Effective Attack Range`）把這個接線套用到 Player4 身上，`EnemyAttackRangeSync.cs` 留著當作沒接 `combat` 時的後備數值來源，不再是權威數值。
- **新增測試**：`EnemyAITests.TargetBeyondAttackRangeButWithinCapsuleReach_StillAttacksWhenCombatWired`（目標站在超過舊 `attackRange` 但在真正膠囊可達範圍內，接上 `combat` 後應該還是會攻擊，直接驗證這次回報的情境被修好）；`TargetBeyondAttackRange_WithoutCombatWired_StaysChasing`（確認沒接 `combat` 時舊行為完全不變）。
- **套用後意外多修好兩個既有測試**：套用到真實場景後，重跑完整測試發現這個修正順便讓 `Player4EnemyIntegrationTests`／`WorldSpaceHealthBarTests.PlayerBar_UpdatesWhenPlayer4DamagesPlayer_InRealScene`（原本因為場景裡 `attackRange` 過小而失敗）也一併通過了——這兩個不是這次新增的測試，是既有測試因為場景數值同步問題順帶被連累失敗，修好根因後自然一起恢復。`EnemyAttackRangeSceneTests` 那個已知未解決的問題（`EnemyAttack.asset` 的 `Range` 目前是 1，太短，還在等使用者確認要保留還是換回更大的值）距離門檻從「差很多」（1.497）進步到「幾乎壓線」（1.997，門檻是 >2.0），但因為 `Range` 本身仍然偏小，還是差一點沒過——這是已知、獨立於這次修正的問題，見 `KNOWN_ISSUES.md`。
- 84 個 EditMode 全過；58 個 PlayMode 測試（55 過、1 個既有已記錄的 flaky `JumpTests`、1 個上述已知未解決的 `EnemyAttackRangeSceneTests`、1 個 TrainingDummy 已知跳過）確認過。

## 2026-08-13 — 玩家連段攻擊 Range/Radius 套用敵人已調好的數值

使用者要求「我調整好的敵人的參數配置，以一樣的公式和邏輯套用在player1身上」。確認範圍後（只套用 `Range`／`Radius`，`startupFrames`／`activeFrames`／`recoveryFrames`／`comboWindowFrames`／`damage` 維持 Player 自己原本的連段設計不變）：

- `LightAttack2.asset`／`LightAttack3.asset` 的 `range`／`radius` 從 1.5／0.75 改成 0.5／0.5，跟 `EnemyAttack.asset`（使用者目前調好的敵人數值）以及已經是 0.5／0.5 的 `LightAttack1.asset` 一致——`LightAttack1` 本來就已經是這組數值，判斷是使用者自己先動手改的，這次補齊 2、3 段。
- **傷害／時機（startup/active/recovery/comboWindow）完全沒動**：LightAttack1（6/4/14/10）、LightAttack2（7/4/16/10）、LightAttack3（10/5/22/0）三段連段原本「越後面起手/收招越久」的節奏設計維持不變，只有攻擊距離跟著敵人的數值走。
- **提醒一個連動（沒有動手改，先告知）**：`Range+Radius`（真正的判定膠囊最遠可達距離）現在剛好是 0.5+0.5=1.0，等於兩個預設半徑角色（0.5+0.5）貼身時的中心距離下限——理論上剛好搆得到，但完全沒有多餘緩衝。之前「怎麼計算適當的攻擊距離」那次討論過，貼身距離實測會有一點誤差（可能略高於理論值），如果之後實際 Play 測試發現「明明貼在一起卻偶爾打不到」，這組數值目前是零緩衝，是最可能的原因，需要的話可以再往上加一點。
- 純資料變動（`.asset` 檔案），沒有碰任何程式碼。既有測試都是用自己建的合成 `AttackData`（`ScriptableObject.CreateInstance` + reflection 設欄位），不是載入真實資產，所以理論上不受影響——實際跑過確認：84 個 EditMode 全過；58 個 PlayMode 測試（54 過、2 個既有已記錄的 flaky `JumpTests`／`WalkingIntoPlayer2_DoesNotPassThrough`、1 個上一則條目提到還沒解決的 `EnemyAttackRangeSceneTests`、1 個 TrainingDummy 已知跳過），跟這次改動前完全一致，沒有新增任何失敗。
- **仍待使用者本人 Play 一次確認**：LightAttack2／LightAttack3 縮短攻擊距離後的連段手感是否符合預期（原本第2、3段比第1段伸得更遠，現在三段距離一致）。

## 2026-08-13 — 新增 Player3：跟 Player 完全一樣的攻擊機制，但完全不會動、不會攻擊

使用者要求「引入 "C:\Users\homec\Downloads\Cross Punch.fbx"，攻擊、動作判定、機制完全與p1一致，差別只在於他完全不會動，也不會攻擊」。

- **先檢查了 Cross Punch.fbx 這個檔案**：只有動畫資料（`AnimationCurve`/`AnimStack`），沒有任何網格/蒙皮資料（`Geometry`/`Deformer` 都是 0），是標準 Mixamo「Without Skin」動作匯出格式。用 MD5 checksum 比對，跟專案裡已經存在的 `Assets/_Project/Characters/Placeholder/CombatAnimations/Mixamo/CrossPunch.fbx`**逐位元組完全相同**——這個檔案早就在專案裡了（Player 自己的 Attack1 動畫用的就是它，透過 Humanoid Retargeting）。**沒有重複匯入**，避免專案裡出現兩份一模一樣的動畫資產；新角色沿用 Player 自己的 Maya 視覺模型（跟其 Animator Controller 一起），本來就會自動共用同一份 Attack1/2/3 動畫，不需要另外接線。
- **確認範圍**：使用者確認 (1) 新角色沿用 Player 自己的模型（Maya），(2) 可受擊，跟 Player2 一致（有 Health、會掃血，但永遠不會反擊）。
- **新增 Player3**：`Player3TrainingDummySetup.cs`（`Tools/Live2DAction/Add Player3 (Stationary Damageable Dummy, Maya Visual)`）——「機制完全一致」是靠**直接重用 Player 自己的資產**達成的，不是另外做一份相似的設定：`comboAttacks` 指向的是跟 Player 完全相同的 `LightAttack1/2/3.asset` 參照（不是拷貝），之後這幾個資產再被調整，Player3 會自動跟著變，不會有第二份數字需要手動同步（這正是這整個 session 一路踩過的「兩份獨立數字容易脫鉤」教訓的直接應用）。視覺是 Maya 的 prefab 實例（跟 Player 用同一份 prefab、同一個共用的 Animator Controller，天生就有 Attack1/2/3 動畫狀態）。
- **保證「完全不會動、不會攻擊」的方式**：`PlayerCombat.inputSource` 刻意留空——`InputCommand` 解析成 null，`attackPressed` 永遠是 false，`ComboAttackState` 永遠停在 Idle，不會觸發任何攻擊。沒有掛 `CharacterMovement`／`PlayerInputProvider`／`EnemyAI`，用的是普通的 `CapsuleCollider`（不是 `CharacterController`，因為完全用不到移動機制），跟 Player2 當初「純被動可受擊」的選擇一致。
- **可受擊部分沿用 Player2 的既有模式**：`Health`、`WorldSpaceHealthBar`（重用 `HealthBarSetup.AddHealthBar`）、`LockOnTarget`（可被鎖定）。
- **新增測試**：`WorldSpaceHealthBarTests.Player3_SharesPlayersExactCombatData_ButNeverMovesOrAttacks`（PlayMode，載入真實場景，驗證：血條正確接線；`comboAttacks` 三格都跟 Player 的參照到同一個 `AttackData` 資產物件，不是拷貝；沒有 `CharacterController`／`PlayerInputProvider`／`EnemyAI`；兩幀之後位置完全沒變、`ComboIndex` 停在 -1；真的會被 `ApplyDamage` 扣血）。
- **視覺驗證**：算圖截圖確認 Maya 模型正確顯示、腳有貼地、血條位置正確（滿血紅色）。
- 84 個 EditMode 全過；59 個 PlayMode 測試（55 過、2 個既有已記錄的 flaky `JumpTests`／`WalkingIntoPlayer2_DoesNotPassThrough`、1 個既有已知未解決的 `EnemyAttackRangeSceneTests`、1 個 TrainingDummy 已知跳過），跟改動前一致，沒有新增任何失敗。
- **仍待使用者本人 Play 一次確認**：Player3 目前放在場景座標 (5, ground+0.5, 0)，跟其他角色沒有重疊，位置是否需要調整；血條/鎖定手感是否符合預期。

## 2026-08-13 — Player5 換裝＋接上 Attack3 專屬特效＋武器（狼的末路）

大段落彙整：把 Player 的視覺從 Maya 換成使用者提供的「Player5」（暱稱 lacrimosa）角色、幫 Attack3 做了專屬的斬擊序列動畫特效（歷經 3 次素材更換）、最後在右手掛上武器模型。三個外部素材（Player5 本體、Attack3 特效圖、武器模型）授權來源都不明，已登記進 `ASSET_LICENSES.md`，僅限個人原型驗證，禁止進入對外 Build。

**Player5 視覺替換**（`Player5VisualSetup.cs`，`Tools/Live2DAction/Replace Player Visual With Player5 (Lacrimosa)`）：
- 原始 FBX 骨架匯入時被判定成 Generic（Unity 不會自動偵測 Humanoid），但骨架其實是標準 3ds Max Biped 命名（`Bip001-Pelvis` 等），手動切成 Humanoid 後 Unity 自動配對成功，因此可以直接共用 Maya 的 `NewAnimator.controller`（Idle/Walk/Run/Attack1-3 全部可用），不需要另外做動畫。
- 材質原本是 FBX 內嵌的暫存資產（改了會在下次 reimport 時被沖掉），改用 `ModelImporter.AddRemap` 抽成正式 `.mat` 檔案（`Player5Anime/Materials/`）才能持久化，10 個材質裡 7 個配對到貼圖，`eyelash`／`gaoguang`／`03` 三個材質使用者沒提供對應貼圖，維持預設灰色。
- 縮放改成動態量測 Maya 本人 Prefab 的實際身高去比例換算（而非寫死常數），量出來 Player5 目標身高約 1.34m。
- **修了一個真實 bug**：`Player5VisualSetup` 一開始沿用 Maya 腳本「砍掉 Player 底下所有子物件重建」的寫法，結果把跟 `Visual`同層、額外掛在 Player 底下的 `HealthBarCanvas`（血條）一起砍掉了（"現在看不到玩家血量條"）。改成只精準砍 `Visual` 這一個節點（照抄 `EnemyHumanoidVisualSetup` 的安全寫法），並在流程最後補呼叫 `HealthBarSetup.AddHealthBar` 重建血條。
- **修了第二個真實 bug**：`CharacterAttackAnimationLink`／`CharacterAnimatorLink` 這兩個原本指著 Maya Animator 的既有欄位，換視覺後變成空引用（Unity 不會自動重新指），攻擊動畫／走跑動畫混合因此失效。改成每次執行都自動重新指向 Player5 的新 Animator。

**Attack3 專屬斬擊特效**（`Attack3SlashEffectSetup.cs`，`Tools/Live2DAction/Add Attack3 Slash Effect`）：
- `AttackData` 新增 `HitEffectOverride`（per-attack 特效覆寫，null 時退回 `PlayerCombat` 共用的打擊火花）與 `AlwaysSpawnHitEffect`（沒打中人也要出特效，僅 `LightAttack3` 設為 true，其餘攻擊不受影響——`PlayerCombat.ResolveActiveHit` 沒打中時改在攻擊距離最遠端播放）。
- 素材前後換了 3 次：5 幀單排 PNG → 6x4=24 幀 JPG（無 alpha） → 6x3=18 格、由 `Attack3SlashFrameAtlasBuilder` 從 17 張各自裁切大小不一的 PNG 拼成的圖集（沒有座標中繼資料，靠猜測每張置中對齊）→ 最終定案：使用者提供的 8x8=64 格（62 格有效）、自帶精確座標 `index.json` 的「X 型交叉斬」特效（原始 10240x5760、27MB，import 時限制在 4096）。
- **修了三個真實 bug**：(1) 用 `AssetDatabase.GetBuiltinExtraResource<Mesh>("Quad.fbx")` 拿內建四方形網格在這個 Unity 版本悄悄回傳 null，導致 Mesh 渲染模式沒有網格可畫、完全不出特效——改用 `GameObject.CreatePrimitive(PrimitiveType.Quad)` 抓網格。(2) 8x8 那批素材的「空白」背景其實是不透明的深灰色（非透明也非純黑），Additive 混合只會讓純黑消失，結果背景洗成一片灰白——新增 `Attack3SlashBackgroundCleaner.cs`，量測背景色後對整張圖做「每個像素扣掉背景色」的清理。(3) 最根本的一個：URP 內建 `Particles/Unlit` shader 的 `_SrcBlend`/`_DstBlend` 用程式碼強制設成 One/One 後，只要材質被重新驗證（例如重開 Editor）就會被 shader 自帶的 GUI 邏輯依照 `_Blend` 下拉選單悄悄改回 SrcAlpha/One，特效又變得幾乎看不見——寫了專屬的最小 Shader `Assets/_Project/VFX/Shaders/AdditiveUnlit.shader`，把 `Blend One One` 寫死在 Pass 裡，不再是可被覆寫的屬性。
- 特效渲染方式從 Billboard（永遠面向鏡頭）改成 Mesh + World 對齊（依角色出招方向站在場景裡，不會像貼紙一樣一直轉向鏡頭），`PlayerCombat` 生成特效時也從 `Quaternion.identity` 改成用攻擊者當下的實際朝向。

**武器（狼的末路，*原神*武器仿製）**（`Player5WeaponSetup.cs`，`Tools/Live2DAction/Attach Wolf's Gravestone Weapon To Player5`）：
- 掛在右手 `Rhand_Weapon2` 骨骼（Player5 自帶的武器掛點骨骼，使用者指定），完整路徑：
  `Player/Visual/player_004_lacrimosa_skin_LOD1_Skeleton/root/Bip001/Bip001-Pelvis/Bip001-Spine/Bip001-Spine1/Bip001-Spine2/Bip001-R-Clavicle/Bip001-R-UpperArm/Bip001-R-Forearm/Bip001-R-Hand/Bip001-Prop1/Rhand_Weapon2/WolfsGravestone`
- 材質只接了 BaseColor／Normal／Emissive 三張貼圖；Metallic／Roughness 因為要正確用在 URP/Lit 得先把兩張圖打包進同一張貼圖的不同色版（跟拼 Attack3 特效圖集同一類工作），先用固定數值（Metallic 0.8、Smoothness 0.5）代替。
- **位置/縮放是使用者手動校正的權威值，不是公式算出來的**：第一版用 FBX 量出來的握把座標＋等比例縮放公式（`TargetLength/RawLength`）算出初始位置，這個環境沒辦法用截圖驗證握把對齊是否正確；使用者實機 Play 後在 Inspector 手動調整 Scale／Position 到正確大小/位置，把這兩個值（`localPosition=(-0.03,-0.18,-0.05)`、`localScale=(0.03,0.03,0.03)`）寫回腳本常數，之後重跑工具會重現使用者校正過的結果，不會被腳本自己的公式蓋回去——跟 `ThirdPersonCameraController` 的攝影機數值是同一種「使用者調過的值即權威」處理原則（見專案記憶 `camera-user-tuned-values-are-authoritative`）。

- 每個步驟都跑過 84 個 EditMode 測試全過（`PlayerCombat`/`AttackData` 改動不影響既有合成測試資料）。這次 VFX 相關的視覺細節（貼圖顯示是否正確、握把對齊、朝向）大多沒辦法在這個環境用截圖可靠驗證，全部靠使用者本人多輪 Play 模式回報問題來回修正——過程記錄在上面，供之後遇到類似「特效看不見/顏色不對/背景沒透明」問題時參考排查方向。

## 2026-08-13 — 場景裝飾：10 把《原神》風格劍展示組 ＋ 鍵盤即時調整工具

- 新增 `GenshinSwordDisplaySetup.cs`（`Tools/Live2DAction/Add Genshin Sword Display (Scene Decoration)`）：把使用者提供的 10 把劍模型（Bakufu／Boreas／Cool Steel／Dull Blade／Freedom-Sworn／Katana／Lion's Roar／Mistsplitter Reforged／Narukami／Prototype Rancour）當場景裝飾放進 `GreyboxTest`，物件名 `GenshinSwordDisplay_DoNotShip`，放在裝飾環外圍（跟 `BackgroundSceneryStandeeSetup` 同一圈範圍）。這批素材的貼圖檔名（`Equip_Sword_Narukami_01_Tex_Diffuse.png` 等）跟《原神》官方內部資產命名規則一致，疑似資料探勘而非單純仿製，風險等級記錄進 `ASSET_LICENSES.md`（現在共 6 個佔位/風險素材禁止出貨）。
- 材質沿用 Player5 那套「抽出正式 `.mat` 檔＋`AddRemap`」做法（避免重新匯入時被沖掉）；貼圖對應是用《原神》公開命名知識猜的（例如 Freedom-Sworn 內部代號 Widsith），9 把配到貼圖，`Lion's Roar` 是排除法猜的，`Mistsplitter Reforged` 完全沒有對應貼圖。
- 模型原本已經排好一列展示隊形（各自帶約 100 倍的獨立縮放），沒有像散佈樹木/岩石那樣打亂，維持原始排列當一個整體場景物件、套一個整組縮放（從量到的整組高度換算，目標最高的劍約 1.3m）。
- 新增 `SwordDisplayAdjuster.cs`（`Assets/_Project/Game/DebugTools/`）：Play 模式下 Z/X 持續調整整組高度、C/V 持續等比縮放，掛在展示物件上，讓使用者不需要透過 AI 就能自己微調到滿意的位置——這個環境沒辦法截圖驗證擺放對不對，這是繞開這個限制的做法。
- 84 個 EditMode 測試全過。

## 2026-08-13 — 玩家必殺技：藍色能量條 ＋ R 鍵釋放（武器放大5倍／Attack1傷害x10／持續5秒）

使用者要求：「賦予角色藍色能量條，初始0，每三秒回復5點，最大100，100時可按下按鍵R釋放必殺技，必殺技是讓角色身上的武器瞬間放大5倍，attack1傷害乘10倍，持續時間5秒」。

- 新增 `UltimateEnergy.cs`（`Assets/_Project/Game/Core/`）：純粹的能量回復元件（0-100，每 3 秒回 5 點），跟 `Health` 一樣不知道任何戰鬥/技能邏輯，只負責數值本身；`Consume()` 歸零並重置回復計時器。
- 新增 `UltimateAbility.cs`（`Assets/_Project/Game/Combat/`）：`IInputCommand.UltimatePressed`（R 鍵，新增到介面，`PlayerInputProvider`／`EnemyAI` 以及 13 個測試用的 `StubInputBehaviour` 都要跟著補上這個成員，否則編譯失敗）在能量滿時觸發——把武器（用名字 `WolfsGravestone` 在階層裡找，不用序列化欄位寫死參照，因為武器是另一個可重複執行的 Editor 工具建立的，找不到就跳過縮放）暫時放大 5 倍、把 `PlayerCombat.Attack1DamageMultiplier` 設成 10，5 秒後自動復原兩者，期間無法重複觸發。
- **傷害倍率刻意不是直接改 `AttackData.Damage`**：`LightAttack1.asset` 是跟 Player3 共用的同一個 ScriptableObject 資產物件（見上面 Player3 那則），在 Play 模式直接改欄位值，不但會讓 Player3 也一起被加成，離開 Play 模式後 ScriptableObject 的欄位變動還不會像場景物件一樣自動還原、會真的寫回資產檔案。改成 `AttackResolver.ResolveHits` 新增一個預設值 1 的 `damageMultiplier` 參數，實際傷害在套用當下才臨時乘上去，`AttackData` 資產本身完全沒被動到。
- 新增 `WorldSpaceEnergyBar.cs`（藍色，疊在既有紅色血條正上方）與 `UltimateAbilitySetup.cs`（`Tools/Live2DAction/Add Ultimate Ability (Blue Energy Bar + R Skill)`）把上述元件掛到 Player 身上並接好血條。
- 84 個 EditMode + 59 個 PlayMode 測試都跑過，PlayMode 2 個失敗是既有已記錄的 flaky（`JumpTests`、`EnemyAttackRangeSceneTests`），跟改動前一致，沒有新增任何失敗。
- **仍待使用者本人 Play 一次確認**：滿能量按 R 的視覺效果（武器變大、下一次 Attack1 傷害是否明顯變高）是否符合預期；能量條位置（血條正上方）是否會跟頭髮/其他裝飾重疊。

## 2026-08-27 — 修正：武士 LeapSlam（每 20 秒定時落地劈砍）沒有完全著地／落地位置浮空

使用者回報「關於每20秒的非空落地攻擊 武士似乎沒有完全著地」，第一版修正後仍回報「落地位置仍然不對 / 還是浮空 沒踩到地」。共三個根因，全部只動程式碼（`BossStateMachine.cs` + `BossTuning.cs`）：

1. **整段 LeapSlam 都停掉重力**：`ApplyMotion()` 之前在整個 `LeapSlam` 狀態期間都關掉重力與 `isGrounded` 貼地夾制（理由是這招的垂直高度由 `UpdateLeapSlam` 的 script height arc 自己每幀驅動 `_verticalVelocity`）。但 arc 在 normalized 0.53 回到 0 後，還有約 1.4 秒的落地定格＋起身動畫完全沒有向下的力，任何觸地殘差都沒東西修正。→ 新增 `_leapSlamArcAirborne` 旗標，只在 arc 還在抬升/收尾（`targetExtraHeight > 0 || _leapSlamPrevExtraHeight > 0`）時關重力，其餘時間（起跳前蹲伏、落地後定格/起身）交還給正常重力＋貼地夾制。
2. **arc 收尾 delta 被丟掉**：第一版用 `normalized < fallEnd` 當判斷，導致最後一幀到 fallEnd 之間那段（可能 1～6 單位）的下降 delta 直接被吃掉，root 停在半空。→ 改成等 `_leapSlamPrevExtraHeight` 真的歸零才交棒，telescoping 一定把 root 帶回鎖定的落地 Y。
3. **落地砸在玩家頭上**：`TryEnterLeapSlam` 直接傳送到玩家「精確 XZ」，武士垂直落下時 CharacterController 對 CharacterController 相撞，`isGrounded` 在離地約一個玩家身高的位置就變 true，武士整段落地動畫就懸在那。→ 新增 tuning `leapSlamLandingOffset`（預設 2 世界單位），沿「玩家→武士起跳點」方向落在玩家前方一小段；落地 AOE 半徑 3.0 仍足夠涵蓋玩家（見 `Wushi_Attack_LeapSlam` designNotes）。設 0 可回到正中央落地。
4. **落地 Y 沿用「當下高度」會累加**（使用者回報「變得比剛剛還上面了」）：`landingPos.y = transform.position.y`，只要有一跳沒回到地面，下一跳就把那個浮空高度當基準，越跳越高。→ 改成從落點往下 raycast 打真正的地面，加上「膠囊底到 transform 原點」的固定偏移（實測 ≈0.123，地面 y=0.50 → 落地 transform y=0.623）。
5. **crossfade 第一幀 stale normalizedTime 讓 arc 一幀彈高 30**（per-frame probe 抓到：某跳進場時 Animator 還回報上一個 clip 的 nt≈0.30 → 高度曲線算出滿值 30 → root 一幀被拉高 30）：→ 新增 `_leapSlamClipConfirmed`，必須先確認 `normalized` 真的低到 clip 起點（或夠多真幀後 < peak）才允許啟動高度曲線，之前維持貼地。
- **實機 per-frame 驗證（play 中反射觸發 + `EditorApplication.update` 逐幀取樣）**：修正後兩次自然觸發的 LeapSlam 都是 起跳 y=0.623 → 最高 y≈30 → 落地 y=**0.623**、之後 `isGrounded=True` 全程穩定。（第 5 點的 stale-frame 保護是新加的，待 play 重啟後再驗一次。）
- `validate_script` standard 兩檔皆無錯誤。沒有動 tuning 資產（`Wushi_Tuning.asset` 沿用新欄位預設值）、動畫、場景。
- **仍待使用者本人 Play 一次確認**：落點跟玩家的相對位置是否合理、劈砍還是有打中玩家、進場那一幀有沒有殘留的彈跳抽動。

## 2026-08-27 — 新增「守望者」高空觀察者 ＋ ViewFocusDirector 視角轉換

使用者要求：「有一個角色紫色頭髮穿著泳裝（= Maya 佔位），讓他在空中(本地正上方)待著，在他身上掛攝影機，提供一個方式把視角從 player 轉向守望者」。追加確認：按鍵 + 程式 API 都要；框法是守望者自己的 POV 往下看戰場；位置要能框到武士 LeapSlam 起跳最高點。

追加需求：按鍵 + 程式 API 都要；框法是守望者自己的 POV 往下看戰場；「太高了」→ 守望者 Y 45→40→**33**；守望者視角要能 **W/A/S/D + 滑鼠 控制攝影機**；**玩家駕駛車輛時 T 也要能切**；**守望者本人要隨攝影機一起飛，但保留現有攝影機參數**。

- 新增 `Assets/_Project/Game/Camera/ViewFocusDirector.cs`（`Live2DAction.CameraSystem`，`[DefaultExecutionOrder(200)]`）：掛在獨立 `ViewDirector` 物件上（**不掛在相機上**，這樣車輛進出把相機 SetActive 切換時 director 仍持續運作）。
  - 同時接「步行相機」（Main Camera + `ThirdPersonCameraController`）與「車輛相機」（`VehicleCamera` + `VehicleCameraController`）。每幀取「當下 active 的那顆」來驅動，所以 **T 在步行或駕駛狀態都有效**。若守望者視角期間相機被 SetActive 換掉（中途上/下車），自動還原 controller、退回玩家視角。
  - 守望者視角期間把 active 相機的 controller `enabled=false`、自己每幀寫 transform；切回時先 re-enable controller（它已算好本幀玩家鏡頭 pose），再從守望者 pose eased-lerp 過去，交棒無縫。不引入 CinemachineBrain。
  - `suspendWhileWatching[]`：守望者視角期間停用的元件（玩家 `CharacterMovement` + 車輛 `VehicleController`），這樣 W/A/S/D + 滑鼠只控制攝影機、**不會同時操控角色/車**。
  - **移動的是整個「守望者」root（Maya + Viewpoint 子物件一起飛/轉）**，Viewpoint 子物件自己的 local offset(0,1.62,0.25)/pitch 72° 與 director 的 FOV 都不碰 —— 「保有現有攝影機參數設定」。切回玩家視角時（`resetViewOnFocus`=true）root 還原回掛載點。
  - W/A/S/D 沿當前 yaw 的地面方向飛、E/Q 世界上下（`panSpeed`/`verticalPanSpeed`）；滑鼠 = 自由轉視角（`watcherMouseLook`、`mouseLookSensitivity`；yaw 帶動 root 轉向，pitch 只動相機、夾在 `watcherMinPitch`/`watcherMaxPitch` = -80/85，Maya 保持直立）。進守望者視角自動鎖游標、切回釋放。
  - 觸發：按鍵（預設 `T`，可設 None）＋公開 `FocusWatcher()` / `FocusPlayer()` / `Toggle()`。
  - 其他欄位：`blendDuration`（1.5s，0=硬切）、`blendEase`、`watcherFieldOfView`（70，0=不動 FOV）、`startFocusedOnWatcher`。
  - 純函式 `BlendPose(from,to,t)` 拆出來給 EditMode 測。
- 新增 `Assets/Editor/Bootstrap/WatcherSetup.cs`（可重複執行，`Tools/Live2DAction/Add Watcher (Sky Observer + View Focus)`）：
  - 放 Maya prefab 當「守望者」的 `Visual` 子物件（紫髮泳裝佔位，CC-BY，出貨需標註，已列 `ASSET_LICENSES.md`），沿用 `PlayerMayaVisualSetup` 的 rig 清除 + 移除 Maya prefab 自帶的 2 個 missing script。無 collider、無 Rigidbody，懸空 idle。
  - 位置 `(0, 33, 0)`（本地 30×30 場地正中央正上方；武士 LeapSlam root 最高點 ~y30.6，33 讓武士幾乎升到守望者眼前）。
  - 子物件 `Viewpoint`：disabled Camera 當 pose marker + 框景輔助。local pos `(0,1.62,0.25)`、pitch 72°、FOV 70。
  - 建 `ViewDirector` 物件，接好步行/車輛兩組相機 + `suspendWhileWatching`（自動找 `Player` 的 `CharacterMovement` 與場上所有 `VehicleController`）。舊版若把 director 掛在 Main Camera 上，重跑 tool 會清掉。
- 新增 `Assets/_Project/Tests/EditMode/ViewFocusDirectorTests.cs`（7 個，全過）。
- **實機驗證（play 中反射 + `EditorApplication.update` 逐幀）**：
  - 步行：`FocusWatcher()` → 相機到 Viewpoint、FOV 70、`ThirdPersonCameraController` 與 `CharacterMovement` 皆 disabled；`FocusPlayer()` → 兩者 re-enable。
  - 車輛（模擬 Main Camera off / VehicleCamera on）：`FocusWatcher()` → **車輛相機**移到 Viewpoint、`VehicleCameraController` 與 `VehicleController` 皆 disabled；`FocusPlayer()` → re-enable。
  - rig 跟隨：守望者視角注入 yaw 90° + fly(8,3,-5) → root 移到 (8,36,-5)、yaw 90、**Maya(Visual) world 同步到 (8,36,-5)**、相機在 root + 隨 yaw 旋轉後的 Viewpoint 偏移處、pitch 維持 72；`FocusPlayer()` → root 還原回 (0,33,0)。
  - 截圖確認 y=33 時武士 LeapSlam 到最高點幾乎頂到鏡頭、整個場地在框內。守望者 renderer 在 POV 期間自動隱藏、切回後還原。
- EditMode 全套（含新 7 個）通過；既有無關失敗 `CharacterAttackAnimationLinkTests...FallsBackToAttack3`（預期 Attack3 得 Attack4，Attack4 連段先前加入所致）未處理。

## 2026-08-28 — 守望者放大3倍 ＋ 存檔視角 ＋ WASD 誤操控車輛修正 ＋ 武士 LeapSlam 著地Y

- **守望者放大 3 倍**：`WatcherSetup` 把「守望者」root scale 設 `(3,3,3)`（Maya + Viewpoint 一起放大，相機停在放大後的頭部，世界高度 ~34.6→~37.9）。位置維持 `(0,33,0)`。
- **存檔守望者視角**（"要能保存守望者視角中攝影機的變更設置" → "不能自動保存" → 自動存）：
  - 新增 `WatcherViewConfig` ScriptableObject（`Assets/_Project/Settings/WatcherViewConfig.asset`）—— SO 的寫入撐得過離開 Play 模式。
  - 守望者視角新增：**滾輪 = 縮放（FOV）**、`K` = 手動存、`autoSaveView`（預設開）= 離開守望者視角（按 T / 停 Play / 上下車）時自動把當前 fly 位置 / yaw / pitch / FOV 存進 config。之後 `FocusWatcher()` 從存檔開始。
  - 重置：取消勾選 `WatcherViewConfig.hasSavedView`（或刪 asset）。commit 只在編輯器寫檔，build 內按 K 只影響本 session。
  - 欄位：`commitViewKey`(K)、`autoSaveView`、`scrollZoomStep`(4)、`watcherMinFov`/`MaxFov`(15/110)。
- **修正真實 bug（"非駕駛模式時控制w/a/s/d時會玩家連同car一直做移動控制"）**：`ViewFocusDirector.SetSuspended` 在離開守望者視角時**盲目把 `suspendWhileWatching` 全部 `enabled=true`**，導致 Buggy 的 `VehicleController`（步行時本來被 `VehicleEntrySystem` 關著）被打開 → 停著的車跟玩家一起吃 W/A/S/D。改成 suspend 時快照每個元件的實際 enabled 狀態、restore 時還原成快照值（本來關的維持關）。
- **武士 LeapSlam 著地 Y 從 0.623 改成 0.5**（"武士飛空後著地y座標從0.623改為0.5"）：
  - 新增 tuning `leapSlamLandingGroundedOffset`（`BossTuning`，預設 **0**）：`landingPos.y = 地面raycast命中 + 這個offset`。舊版是自動算的 ~0.123（讓膠囊貼齊地面）。
  - 高度曲線跑完後 `UpdateLeapSlam` 直接把 transform.y 釘在 `_leapSlamLandingY`（暫時關 CharacterController 來設，因為 0.5 讓膠囊陷地、`_controller.Move` 碰地到不了），`ApplyMotion` 這期間整個跳過 `_controller.Move`。
  - **已知取捨**：LeapSlam 結束回 Idle 後，正常物理把陷地的膠囊推出 → transform.y 從 0.5 回 ~0.623（1~2 幀，+0.123 的小彈跳）。要永久停 0.5 得改 CharacterController center（CLAUDE.md 標記為手調權威值，未動），或把武士 Visual 子物件往下偏 0.123（transform 維持 0.623、視覺腳在 0.5、無彈跳）—— 待使用者決定。
- **武士參數/邏輯檢查發現**：
  - `Wushi_Tuning.asset` 只序列化到 `breakdanceTriggerSeconds`，之後加的欄位（全部 `leapSlam*`、`tooCloseDistance/DurationSeconds`、`leapSlamLandingGroundedOffset`）都跑 `BossTuning.cs` 的 code default，asset 裡看不到也改不到，直到有人在 Inspector 開一次並存檔。目前生效值：leapSlamTriggerSeconds=20、leapSlamExtraHeight=30、rise/peak/fallEnd=0.05/0.30/0.53、leapSlamLandingOffset=2、leapSlamLandingGroundedOffset=0。
  - LeapSlam `baseHealthDamage=500` = 玩家滿血一擊必殺，每 20 秒觸發一次。是先前明確要求，但對「定時小技能」偏重，flag 給使用者。
  - `vanishTriggerSeconds=999999` → 整個 Vanish→DiveAttack 循環永不觸發（既有的 disabled 狀態，LeapSlam 現在借用它的 `landingAoeHitbox`）。
  - `postureBreakDurationMin/Max` 都是 3（無隨機）。
  - LeapSlam/Breakdance 打斷可中斷的普通攻擊時不清 `_currentAttack`；因為武士所有攻擊 `useRootMotion=0`，唯一會讀到 stale 值的路徑（ApplyMotion root motion）不會做事，目前無害，但加 root-motion 攻擊時是陷阱。
  - 清掉：已無用的 `_leapSlamArcAirborne` flag；修了 `TryEnterLeapSlam` 一段還在描述最初「不動 root、直接在玩家身上起 clip」舊實作的過時註解。
- EditMode 158/159 過（唯一失敗 `CharacterAttackAnimationLinkTests...FallsBackToAttack3` 為既有無關）。
- **待使用者 focus Unity 後實機驗證**：WASD 不再誤動車、LeapSlam 著地 y=0.5、守望者自動存檔往返。

## 2026-08-28 (追加) — 守望者顯示修正 ＋ 縮 2 倍 ＋ 圍牆開洞

- **守望者放大改 2 倍**（"將守望者放大2倍"）：`WatcherSetup.WatcherScale` 3→2。
- **修正："守望者能記住攝影機位置 但玩家視角看不見他"**：`SetWatcherVisualHidden` 之前只在狀態轉場事件時切 renderer，各種路徑（auto-save commit、停 Play、相機被換）會 desync。改成 `LateUpdate` 每幀宣告式呼叫 `SetWatcherVisualHidden(IsFocusedOnWatcher)` —— 只有在看她自己的 POV（Watcher / 轉入中）才隱藏，其餘一律顯示（含轉回玩家的整段 blend，讓你看著相機從她身邊拉開）。她停在自動存檔的位置、可見。
- **圍牆開洞**（"目前圍牆是封死的，提供一個比車身大1.2倍的洞口"）：新增 `Assets/Editor/Bootstrap/VehicleWallOpeningSetup.cs`（可重複執行，`Tools/Live2DAction/Add Vehicle Wall Opening (Buggy gap in a BoundaryWall)`）：
  - 在 `BoundaryWall_South` 正中央切一個**全高、車寬 × 1.2** 的缺口。車寬用 Buggy 的輪距 + 輪半徑實測（1.52 + 2×0.33 = 2.18），× 1.2 = **2.62 寬**，`GapMinWidth` 下限 2.4。
  - 做法：移掉牆原本的實心 + trigger BoxCollider，換成左右兩段實心 collider；停用牆自己的 MeshRenderer，parent 兩個 `WallSegment_L/R` cube 視覺（同材質）讓缺口看得見；停用這面牆的 `BoundaryBlockEffect` 並關掉 `RippleEmitter` 子物件（不然會在缺口冒一根白柱），其餘三面牆不動。
  - Raycast 驗證：x=-8~-2 與 x=4~8 被擋、x=-1~1 貫穿到外面。缺口 ~2.62 寬、full height。
  - 洞口在 x=0；車 ~2.18 寬 → 每邊約 0.22 餘裕，偏緊但可過（"1.2倍"）。要放寬改 `GapWidthMultiplier`/`GapMinWidth`。畫面上缺口附近那根白色光柱是既有的 `Updraft_MainArea/WindColumn`（飛行上升氣流），跟開洞無關。
- 4 檔編譯無錯。CHANGELOG 已更新。
- **待使用者 focus Unity 實機驗證**（累積 3 項）：WASD 不再誤動停著的車、LeapSlam 著地 y=0.5、守望者切回玩家視角後看得見且在存檔位置、Buggy 能開出南牆缺口。

## 2026-08-28 (追加2) — 守望者角色本身不見了：根因是壞掉的存檔視角

- **症狀**：守望者（Maya 模型）在玩家視角完全看不到 —— 不是位置太遠，是模型本身不見。
- **根因**：`WatcherViewConfig.asset` 存了一組壞掉的視角，`rootPosition.y = -3.08`（**在地面下**）。`autoSaveView` + `resetViewOnFocus` 讓守望者每次都被 seed 到那個地下座標 → 看不見。壞值來自這次開發過程中我用反射跑的自動測試 hook（灌了測試用的 `_flyOffset` 再呼叫 `CommitCurrentView`），寫進了真的 asset。
- **修正**：
  - 清掉 config（`hasSavedView = false`），守望者回到 `(0,33,0)` 可見。
  - `WatcherSetup` 重跑現在會**重置 config**（重跑本來就是「回到預設框法」的動作，也能救壞掉的存檔）。
  - 新增 `ClampWatcherPos`（`ViewFocusDirector`）：把守望者 rig 的世界 Y 夾在 `watcherMinHeight`（預設 1.5）以上、水平距離掛載點 `watcherMaxFlyRadius`（預設 120）以內。**config 載入時、每幀 `ApplyWatcherRig`、`CommitCurrentView` 寫檔前都夾一次** —— 壞掉的存檔或失控的 W/A/S/D 飛行再也不能把守望者弄到地下 / 飛出地圖。
- 編譯無錯。CHANGELOG 已更新。

## 2026-08-28 (追加3) — 守望者放大5倍 ＋ 車道通行

- **守望者放大 5 倍**：`WatcherSetup.WatcherScale` 2→5。Viewpoint 是 root 子物件，相機座標自動跟著到 `(0, 41.1, 1.3)`；俯角 72°、FOV 70、WASD 速度、滑鼠靈敏度都不受 scale 影響、不用改。
- **`VehicleWallOpeningSetup` 擴充成「開洞 + 鋪路」**，選單改名 `Tools/Live2DAction/Add Vehicle Wall Opening + Road`：
  - **洞口加寬 1.5 倍**：`GapWidthMultiplier` 1.2 → 1.8（= 原本「比車身大1.2倍」× 後來「寬度增加1.5倍」）。實測缺口 2.62 → **3.92 寬**（車 ~2.18 × 1.8）。
  - **新增 `VehicleRoad`**：一片薄板（collider + 視覺），置中對齊缺口，從場地邊緣（z=-15）往外鋪 65 單位到 z=-80，寬度 = 缺口 + 2 ≈ 5.9，頂面在 y≈0.505（比地面高 5mm 免 z-fighting）。**layer=Default**（WheelCollider 打得到）。
  - 為什麼要 collider：`BackgroundTerrain`（300×300）**本身沒有 collider**，車開出缺口後外面沒地板會掉下去 —— `VehicleRoad` 就是外面的可行駛面。
  - 新建材質 `Assets/_Project/Environment/Materials/RoadSurface.mat`（深灰 URP/Lit）。
  - Raycast 驗證：z=-10~-15 打到 Ground、z=-16~-80 打到 VehicleRoad、缺口貫穿。
- 編譯無錯、場景已存檔。
- **待使用者 focus Unity 開 Buggy 實測**：能不能從場地開出南牆缺口、沿路行駛不掉出去。

## 2026-08-28 (追加4) — 清掉 Console 缺失腳本錯誤

- 症狀：`The referenced script on this Behaviour (Game Object 'Visual'/'') is missing!` 在載入/進 Play 時洗版。
- 根因：`MayaAnime/Prefabs/Maya.prefab` 與 `ArisaAnime/Prefabs/Arisa.prefab` 各掛了 3 個第三方角色控制器腳本（ThirdPersonController 類的 `playerCamera/turnSmoothing`、移動 `walkSpeed/jumpHeight`、攝影機 `player/pivotOffset` + MainCamera 子物件上一個），這些腳本的 GUID 整個 repo（含 PackageCache）都找不到——來自未匯入的付費資源包。Maya 被 `TrainingDummy` 當視覺、Arisa 被 `Enemy`（場景 `Arisa.prefab` 實例）當視覺。
- 修法：`PrefabUtility.LoadPrefabContents` + `GameObjectUtility.RemoveMonoBehavioursWithMissingScript` 逐一清掉，各移除 3 個。
- 驗證：全專案 `.prefab`/`.unity` 掃描已無未解析的 script GUID；重進 Play，Console 全清（0 error / 0 warning）。

## 2026-08-28 (追加5) — 武士「進步連斬」(AdvancingCuts)：加入後同日移除

- 使用者要求導入 `Meshy_AI_Parkside_Portrait_biped (7).zip` 作為武士普通進攻池第五招（中距離、可派生）。已完整接上：FBX（Humanoid，clip `Wushi_AdvancingCuts`）、`Wushi.controller` +1 state、`Wushi_Attack_AdvancingCuts.asset`（`BossAttackDefinition`，派生 → OverheadSlam）、`normalAttackPool` 4→5。
- 過程中修掉：clip import `keepOriginalPositionY` 沒對到 sibling 導致 Play 裡半身陷地（改 True）；`maxDistance` 2.8 太窄實戰幾乎選不到（改 3.2）。
- **使用者實測後回報「不好用」，要求移除。** 已完整回退：`normalAttackPool` 回到原本 4 招、`Wushi.controller` 移除該 state（byte-identical 回 HEAD）、刪除 `Wushi_AdvancingCuts.fbx` + `Wushi_Attack_AdvancingCuts.asset`、`GreyboxTest.unity` 直接 `git checkout` 丟掉存檔時 Cubism ArtMesh 的重序列化雜訊。Console 無錯。
- 另發現、尚未處理：武士 LeapSlam「偶爾飛天後消失沒下來」（見 `KNOWN_ISSUES.md`，疑似既有問題，非本次改動造成）。

## 2026-08-28 (追加6) — 移除武士「前衝斬」(LungeSlash)

- 使用者實測回報：「似乎沒有前衝，展擊也不夠圓弧」，要求移除。
- 診斷：clip 動畫裡有 Hips 相對 root 前移 ~2 單位的撲步，但 asset `useRootMotion: 0`（武士所有招都關），那個位移沒轉成實際位置移動 → 看起來沒前衝。要加前衝得開 root motion（clip root 是斜左前、4x 放大會歪）或改程式加正前方衝刺（動 `BossStateMachine.cs`）。揮砍軌跡是 Meshy clip 烘死的下劈、非橫向圓弧，程式改不了。使用者選擇整招移除。
- 完整移除：`normalAttackPool` 4 → **3 招**（SwordJudgment / SpartanKick / OverheadSlam）、`Wushi.controller` 移除 `Wushi_LungeSlash` state（7 states）、刪除 `Wushi_Attack_LungeSlash.asset` + `Wushi_LungeSlash.fbx`。無其他資產引用（已 grep 確認）。
- `AttackReadinessDistance` 仍是 1.7（SpartanKick 的 maxDistance，未受影響）。Console 無錯。
- 場景 diff 帶 Cubism ArtMesh 重序列化雜訊（存檔必然，非本次造成）；pool 移除是唯一功能變更。

## 2026-08-28 (追加7) — 武士新增普通攻擊「雙重連段」(DoubleCombo)

- 使用者要求加入 `Meshy_AI_Parkside_Portrait_biped (9).zip` 的 `Double_Combo_Attack`（注意：檔名的 `(9)` 只是 Meshy 下載計數，跟先前 LeapSlam 用的 `(9).zip` 是不同動畫；這次是 `without_skin` 206KB 純動畫）。
- 新增檔案：
  - `Animations/Wushi_DoubleCombo.fbx`（+meta，Humanoid、clip `Wushi_DoubleCombo` 裁 1–86 幀、`keepOriginalPositionY=true` —— AdvancingCuts 陷地的教訓）
  - `Animator/Wushi.controller` +1 state `Wushi_DoubleCombo`
  - `Settings/Combat/Boss/Wushi_Attack_DoubleCombo.asset`（`BossAttackDefinition`）
  - `GreyboxTest.unity` `normalAttackPool` 3 → **4**
- **這個 clip 幾何上比前兩個被移除的好**：兩段揮砍都真的往下掃過 PlayerHurtbox 高度（刀世界 Y：4.3→0.5、4.5→0.7），有前伸。離線量測 + Play 實測都做了（這次 Play 沒凍結）。
- **但命中偏 marginal**：Player 貼 1.8 單位時 hit 1 實測 -14（正中窗），但 2.0+ 就打空。原因：clip 把角色動畫成「起手時比 root 後退 3 單位、連段中往前走到 root」，`useRootMotion=0` 時可見身體/刀落後 boss 實際座標。
  - 折衷：`maxDistance` 3.3 → **2.5**，讓 boss 只在夠近時才選這招。
  - 之後若還是常打空 / 或嫌「沒前衝」→ 正解是 `useRootMotion=1`（快測 window 0–0.55、距離 2.2 時兩段都命中 -46）＋ 修「LeapSlam/Breakdance 打斷攻擊時清 `_currentAttack`」（CHANGELOG 標記過的 root-motion 陷阱）。留待後續、不盲改。
- 調校值：health 14（第二段 ×1.15）、poise 12、knockback 3、cooldown 2.8s、非 major、無派生。hit windows nt 0.23–0.31 / 0.60–0.66。
- 驗證：Play 實跑進 `Wushi_DoubleCombo` state、播完回正常 FSM、BakeMesh 逐格量最低頂點 0.64–0.67（不陷地）、Console 無錯。
- **待使用者實機打感確認**：命中率、要不要前衝、窗要不要調。全部數值在 asset，`designNotes` 有完整量測。
## 2026-08-28 (追加8) — 武士非大招攻擊改為「目標最大血量的 5%」百分比傷害

- 使用者要求：大招以外的攻擊固定扣 5% 血量，**且不要硬編碼血量數字、要用百分比設定**。
- **`BossAttackDefinition` 新增欄位 `healthDamageIsPercentOfTargetMax`（bool）**：開啟時 `baseHealthDamage` 被讀成「目標最大血量的百分比」（5 = 5%），命中當下用目標的 `Health.MaxHealth` 換算，不是固定 HP 數字。玩家最大血量之後改動也不用再改這些 asset。
- **`BossHitbox.TryResolveHit`**：命中時若 `pctMode` 開，`GetComponentInParent<Health>()`（命中的通常是子 hurtbox collider，如 PlayerHurtbox）取 `MaxHealth`，`healthDamage = MaxHealth * (baseHealthDamage/100) * damageMultiplier`；抓不到 Health 就退回當作固定值。poise 不受影響。
- 套用（`isMajorAttack: 0`）：
  - `Wushi_Attack_SpartanKick`：`healthDamageIsPercentOfTargetMax: 1`、`baseHealthDamage: 5`
  - `Wushi_Attack_DoubleCombo`：同上，兩段 hit window `damageMultiplier` 都是 1
  - → 對玩家（maxHealth 500）每擊 = 25，但由程式在命中時算出，不寫死
- 大招不動（`pctMode: false`、flat）：SwordJudgment 32、OverheadSlam 28、LeapSlam 500。
- 驗證：編譯無錯；EditMode 158/159 過（唯一失敗 `CharacterAttackAnimationLinkTests...FallsBackToAttack3` 為既有無關）；Play 裡確認 `GetComponentInParent<Health>` 從 PlayerHurtbox 能取到 Player 的 Health、`MaxHealth=500` → 5% = 25（Play Mode 這次 frame-frozen 無法實射，但傷害計算路徑已逐段確認）。
- **flag（同前）**：DoubleCombo 有 2 段判定，兩段都中 = 10%。要「整招」封 5% 再說。
- 範圍只有武士。

## 2026-08-28 (追加9) — 武士攻擊欲望調高（使用者「攻擊欲望太低」）

純調校值下修（無程式改動），讓武士出招更頻繁、減少站著「伸懶腰」的空檔：

**`Wushi_Tuning.asset`：**
| 欄位 | 舊 | 新 |
|---|---|---|
| globalRestPhase1 Min/Max | 0.35 / 0.5 | 0.15 / 0.3 |
| globalRestPhase2 Min/Max | 0.25 / 0.4 | 0.1 / 0.2 |
| majorAttackExtraRest Min/Max | 0.5 / 1.0 | 0.25 / 0.55 |
| attackReadinessBuffer Min/Max | 0.2 / 0.35 | 0.1 / 0.2 |
| decisionIntervalPhase1 Min/Max | 0.25 / 0.45 | 0.12 / 0.25 |
| decisionIntervalPhase2 Min/Max | 0.15 / 0.3 | 0.06 / 0.15 |

**攻擊 cooldown（4 招裡有 2 招是 major、cd 長 → 常常「沒招可用」是站著不動的主因）：**
| 招 | 舊 cd | 新 cd |
|---|---|---|
| SwordJudgment | 2.5 | 1.6 |
| SpartanKick | 1.8 | 1.0 |
| OverheadSlam | 2.5 | 1.8 |
| DoubleCombo | 2.8 | 1.6 |

- 出招間隔約砍半（rest + decision + readiness buffer 全部 ~×0.5），加上 cooldown 大幅下修讓 `PickAttack()` 幾乎不會回 null。
- `maxConsecutiveUses` 維持 1（不讓 boss 連續刷同一招，觀感會變差）、`disallowImmediateRepeat` 不動。
- CLAUDE.md 標記這些是手調權威值——這次是使用者明確要求「調高攻擊欲望」才動，全部集中在上表，要往回收很容易。
- **待使用者實機確認節奏**：太兇的話把 globalRest / cooldown 往回加。

## 2026-08-28 (追加10) — 武士攻擊欲望再往上（使用者「再激進」）

在追加9 的基礎上再砍一輪（仍純調校值）：

**`Wushi_Tuning.asset`：**
| 欄位 | 追加9 | 追加10 |
|---|---|---|
| globalRestPhase1 Min/Max | 0.15 / 0.3 | 0.05 / 0.15 |
| globalRestPhase2 Min/Max | 0.1 / 0.2 | 0.03 / 0.08 |
| majorAttackExtraRest Min/Max | 0.25 / 0.55 | 0.1 / 0.3 |
| attackReadinessBuffer Min/Max | 0.1 / 0.2 | 0.05 / 0.12 |
| decisionIntervalPhase1 Min/Max | 0.12 / 0.25 | 0.05 / 0.12 |
| decisionIntervalPhase2 Min/Max | 0.06 / 0.15 | 0.03 / 0.08 |
| approachDecelerationDistance | 0.5 | 0.35 |

**cooldown：** SwordJudgment 1.6→**1.0**、SpartanKick 1.0→**0.5**、OverheadSlam 1.8→**1.1**、DoubleCombo 1.6→**1.0**

**`maxConsecutiveUses`：** 兩個非大招（SpartanKick、DoubleCombo）1 → **2**（可連續刷 2 次才被 `disallowImmediateRepeat` 擋，大招維持不可重複）

- 出招間隔實質剩 ~0.2–0.5s + 該招 recovery。cooldown 0.5–1.1 + 4 招池 + 非大招可連 2 → `PickAttack()` 基本不會 null，boss 幾乎不會有站著的空檔。
- **這一輪幅度很大**，可能過兇。要收的話優先加 `globalRestPhase1` 跟各 cooldown。全部值集中在上兩表。

## 2026-08-28 (追加11) — 武士「不夠快」：加快攻擊動畫 + 移動速度

追加9/10 已把休息/cooldown 砍到近乎 0，剩下的慢是**攻擊動作本身太長**（每招 clip 2.5–3.3s）＋ **Phase 1 用走的（walkSpeed 3）追不上玩家**。

**Animator state m_Speed（`Wushi.controller`，hit window 是 normalized、會跟著等比縮放）：**
| state | 舊 | 新 | 實際時長 |
|---|---|---|---|
| Wushi_SwordJudgment | 1.0 | 1.35 | 3.30s → 2.44s |
| Wushi_SpartanKick | 1.0 | 1.4 | 1.27s → 0.90s |
| Wushi_OverheadSlam | 1.0 | 1.4 | 2.53s → 1.81s |
| Wushi_DoubleCombo | 1.0 | 1.4 | 2.83s → 2.02s |

（LeapSlam 不動——它的高度 arc 綁 normalizedTime，加速會弄壞落地。PostureKneel/Death 不動。）

**`Wushi_Tuning.asset`：**
- `walkSpeed` 3 → **5.5**（Phase 1 approach 用這個；`ResolveMoveSpeed` 只在 `UpdateApproach` 呼叫，不影響別的）
- `runSpeed` 6 → **7.5**（Phase 2）
- `rotationSpeedDegrees` 360 → **520**（轉向對準玩家更快）

- 已知取捨：clip 加速 → BladeHitbox 的實際啟用秒數變短（normalized 窗不變但秒數 = 窗×新時長），已量測會命中的 SwordJudgment/OverheadSlam 應仍 OK，DoubleCombo 本來就 marginal 可能更容易揮空。真的常揮空就把該 state speed 收回 1.2 或把窗調寬。
- Play Mode 這次仍 frozen 無法實測，麻煩使用者打一場。

## 2026-08-28 (追加12) — 文件：武士攻擊節奏模型寫進 TECHNICAL_DESIGN.md

- 使用者要求把「技能本身時長 vs 出招間隔」用表格 + 文字寫進說明文件。
- `TECHNICAL_DESIGN.md` 新增「武士 Boss：攻擊節奏模型」一節：
  - A. 技能本身時長 = clip 長度 ÷ AnimatorState `m_Speed`；FSM 播到 normalized 0.98 才結束，收招 pose 也算在內
  - B. 出招間隔 = globalRest + majorAttackExtraRest + decisionInterval + (需要時) approach + readinessBuffer + (沒招可用時) 等 cooldown，逐項對照表 + 目前值
  - ⚠️ `startupSeconds` / `recoverySeconds` 是死欄位（BossStateMachine 不讀，改了沒用）
  - 「想改什麼 → 動哪裡」快速對照表
  - 順手在「敵人 AI」段加註：實作的武士 Boss 用 `BossStateMachine`、不用 NavMesh，跟 Phase 0 草案不同
- `KNOWN_ISSUES.md` 武士段加了指向該節的一行。

## 2026-08-28 (追加13) — 武士新增普通攻擊「衝刺斬」(ChargeCut)

- 使用者要求加入 `Meshy_AI_Parkside_Portrait_biped (10).zip`（fresh without_skin 生成，MD5 跟已移除的 LungeSlash 那個 (10).zip 不同）。clip `Wushi_ChargeCut`（61 幀 / 2.03s，最短的武士攻擊 clip）。
- 新增：`Wushi_ChargeCut.fbx`(+meta，keepOriginalPositionY=true)、`Wushi.controller` +state（m_Speed 1.3 → 1.56s）、`Wushi_Attack_ChargeCut.asset`、`normalAttackPool` 4 → **5**。
- **1 個 hit window（nt 0.19–0.29）**：刀從 Y 3.19 砸到 1.15，速度峰 **88 u/s（全武士最快的單幀）**，刀前伸 ~2.5（相對 root），在正常近戰距離不靠 root motion 就命中。
- clip 後半（nt 0.4–1.0）是大幅前撲（Hips 前移 ~6 單位）+ 第二下劈，**沒接**——`useRootMotion=0` 時 boss 不動、第二下的刀落在 root 前方 6–8 單位，一般距離打不到。要用完整前撲得開 `useRootMotion`（同 DoubleCombo，需配 `_currentAttack` 清除修正）。
- 傷害：非大招 → 照「大招以外固定 5%」規則，`healthDamageIsPercentOfTargetMax=1`、`baseHealthDamage=5`。cooldown 1.0、knockback 3.5。
- **⚠️ 已知小瑕疵**：低姿前撲斬的姿勢（nt 0.20–0.42，正好蓋住 hit window）會讓 boss 最低網格頂點掉到 Y~0.34，地面在 Y 0.50 → 約 0.16 單位的腳/小腿穿地約 0.4 秒。`keepOriginalPositionY` / `heightFromFeet` 都試過修不掉（烘進 clip 的深蹲）。裁不掉——穿地的幀就是命中的幀。**待使用者判斷**：當成前撲攻擊的視覺瑕疵接受，或整招移除。
- Play Mode 這次又 frozen 無法實射，命中/穿地都是 AnimationMode 離線量的。

## 2026-08-28 (追加14) — 武士 LeapSlam 前搖蹲下 + 血量/架勢條 + 能量觸發

三項使用者要求一起做（動了 `BossState.cs` / `BossStateMachine.cs` / `BossTuning.cs`，事前有摘要確認）。

### 1. LeapSlam 前搖蹲下 1 秒
- 新增 `BossState.LeapSlamWindup`。`TryEnterLeapSlam` 現在先進這個 state：站定、面向玩家、把 `Wushi_LeapSlam` clip 用 `animator.Play(..., 0.09)` + `speed=0` 凍在自己的開場蹲姿，撐 `BossTuning.LeapSlamWindupSeconds`（預設 1.0）。
- 撐完 → 消耗能量（見 3）→ `CommitLeapSlamLanding()`（原本 inline 在 `TryEnterLeapSlam` 的瞬移/鎖定，抽成方法，現在在**蹲完之後**才跑，所以玩家看到的是原地蹲下、不是先瞬移再蹲）→ `ChangeState(LeapSlam)`。
- `_leapSlamFromWindup` 旗標讓 `OnEnterState(LeapSlam)` 不重播 clip——`OnExitState` 把 `animator.speed` 還原成 1，同一段 clip 從 0.09 無縫接飛天。
- `BossTuning` 新增 `leapSlamWindupSeconds`(1.0) / `leapSlamWindupPoseNormalized`(0.09)。⚠️ `Wushi_Tuning.asset` 只序列化到 `breakdanceTriggerSeconds`，這兩個跑 code default，要調得在 Inspector 開一次存檔。
- `TryEnterTooCloseKick` 的 block list 補上 `LeapSlamWindup`/`LeapSlam`（本來漏了 LeapSlam，貼身踢會切斷飛空）。

### 2. 武士血量條 / 架勢條（+ 能量條）
- 新增 `Assets/Editor/Bootstrap/WushiBarsSetup.cs`（選單 `Tools/Live2DAction/Add Wushi Bars`）：直接 `Instantiate` 屁孩王已完成的 `HealthBarCanvas`/`StanceBarCanvas`/`EnergyBarCanvas`（reference-art 版，含 spark FX）到武士身上，依 4x 體型 rescale（canvas localScale 0.55 → 世界 scale 2.2、bar 寬 ~2.18m ≈ 體寬 60%），重新指向武士的 `Health`/`StancePoise`/`UltimateEnergy`（Instantiate 已自動 remap 所有指向複製體內部的參照）。
- 三條浮頭頂、面向鏡頭，world Y 5.5 / 5.15 / 4.8（武士實際網格頂 ~4.6）。

### 3. LeapSlam 改能量觸發（100 能量 / 20 秒滿）
- `BossStateMachine` 新增 `[SerializeField] UltimateEnergy leapSlamEnergy`。`UpdateCombatTimer` 的觸發判定：wired 時看 `leapSlamEnergy.IsFull`，unwired 時退回舊的 `LeapSlamTriggerSeconds` 計時（其他 boss 不受影響）。
- 武士加 `UltimateEnergy`：`maxEnergy=100`、`regenAmount=5`、`regenIntervalSeconds=1` → 5/秒 → 20 秒滿。
- 能量在 `UpdateLeapSlamWindup` 蹲完的 commit 點 `Consume()` 歸零（跟 `UpdateUltimatePrepare` 一致，蹲到一半被架勢破防打斷則能量保留）。

### 驗證
- 編譯無錯；EditMode 158/159 過（唯一失敗 `FallsBackToAttack3` 既有無關）。
- Play Mode frozen 無法實跑，改用 reflection 逐段驗（HealthBar README 記載的手法）：設能量=100 → `IsFull` → `UpdateCombatTimer` 設 `_leapSlamPending`；`TryEnterLeapSlam` → `LeapSlamWindup`、animator 凍在 `Wushi_LeapSlam` nt 0.09 speed 0；`UpdateLeapSlamWindup`(t=2) → 能量歸 0、武士瞬移到玩家旁 y=0.5、state=LeapSlam、animator nt 0.09 speed 1（不重播）。三條 bar 的 Fx `Update()` 都跑得動、fillAmount 正確追值（HP 1.0 / Energy 0 / Stance 0）。截圖確認三條在頭頂。
- **待使用者實機確認**：bar 大小/位置手感、前搖姿勢好不好看、能量節奏。
- LeapSlam「偶爾飛天不下來」的既有 bug 沒動到、也沒修（見 KNOWN_ISSUES）。

## 2026-08-28 (追加15) — LeapSlam 前搖改站姿 + 武士 HUD 改隻狼式螢幕條

追加14 的兩點修正。

### 1. 前搖不蹲下，站在原地
- `OnEnterState(LeapSlamWindup)` 從「凍結 LeapSlam clip 在蹲姿」改成 `PlayState(Locomotion)`（idle 站姿）。tell = boss 停止移動/攻擊 1 秒，沒有蹲。
- 拿掉 `_leapSlamFromWindup` 旗標和 `BossTuning.leapSlamWindupPoseNormalized`（不再需要無縫接續，`LeapSlam` state 正常 CrossFade from 0）。
- reflection 驗證：`TryEnterLeapSlam` → `LeapSlamWindup` 播 Locomotion speed 1；t=2 → `LeapSlam`、能量歸 0。

### 2. 武士血量/架勢/能量條：世界空間頭頂 → 螢幕空間頂部（隻狼式）
- 使用者回報武士太大、頭頂的條看不見。改成固定螢幕 HUD。
- `WushiBarsSetup.cs` 改寫：不再複製屁孩王的世界空間 canvas，改複製 **`PlayerCornerHud` 的三個螢幕空間 track**（`架勢Track`/`生命Track`/`必殺Track`，pixel 單位、billboard off、reference-art）到新的 `WushiBossHud`（ScreenSpaceOverlay、sortingOrder 1、CanvasScaler 1920×1080）。
  - 頂部置中堆疊：架勢 (y-42, w760) / 生命 (y-70, w760) / 能量 (y-98, w560)，高度 ×1.7。
  - 每個 track 的子美術層寬度撐到 boss 寬、EdgeGlow 移到新右緣；Fx 的 `health`/`stance`/`energy` 重新指向武士。
  - 選單改名 `Tools/Live2DAction/Add Wushi Bars (Sekiro-style Boss HUD + LeapSlam Energy)`；重跑會先刪掉舊的頭頂 canvas + 舊 HUD。
- 武士頭上的 `HealthBarCanvas`/`StanceBarCanvas`/`EnergyBarCanvas` 已移除。
- 驗證：Play 裡 reflection 灌值 + pump Fx `Update()`，三條 fillAmount 正確追值（生命 220/1000=0.22、能量 72/100=0.72、架勢 tracking），無例外。截圖確認三條在螢幕頂部置中。
- 編譯無錯、EditMode 158/159 過。
- **待使用者實機確認**：HUD 大小/位置/粗細、要不要加 boss 名字、前搖 1 秒的節奏。

## 2026-08-28 (追加16) — 武士 HUD:血量置頂 + 只在戰鬥時顯示

追加15 的兩點回饋。

### 1. 血量條第一順位
`WushiBarsSetup.Bars[]` 重排：生命 (y-42, w760) → 架勢 (y-70, w700) → 能量 (y-96, w560)。血量在最上面。

### 2. 只在戰鬥狀態顯示
- 新增 `Assets/_Project/Game/UI/WushiBossHudVisibility.cs`（掛在 `WushiBossHud` 上）：`Awake` 先把 `Canvas.enabled=false`（載入時隱藏），`LateUpdate` 依 `BossStateMachine.CurrentState` 切換——「戰鬥中」= 不是 `Dormant`/`Dead`/`Victory`（跟 BossStateMachine 驅動 `CombatActive` animator bool 同一條件）。
- 切 `Canvas.enabled` 而非 `SetActive`，子 `*BarFx` 的 Update 繼續跑、重現時不會 snap-in。
- `WushiBarsSetup` 建 HUD 後自動加這個元件並把 `boss` 指向武士的 BossStateMachine。
- reflection 驗證：Awake → enabled false；state=Alert/Idle → true；state=Dormant → false。
- 編譯無錯、EditMode 158/159 過。

## 2026-08-28 (追加17) — LeapSlam 兩個 bug:攻擊幀外溢 + 能量沒及時清空連觸發兩次

### 1. 「飛空前到飛空後這一整段不該有攻擊幀」
LandingAOE hitbox 會在自己的窗（nt 0.32–0.56）之外開啟：
- **前**：`LeapSlamWindup` 播 Locomotion，接 `LeapSlam` CrossFade 進 leap clip 時，`AnimatorNormalizedTime()` 頭幾幀讀到「外送 Locomotion clip」的 stale normalizedTime，剛好落在 0.32–0.56 → boss 還在地上/上升就開刀。
- **後**：leap clip 非 looping，`normalizedTime` 過 1 之後 `% 1f` wrap 回來，在著地保持姿勢/起身那段又掃過窗。
- 修法（`UpdateLeapSlam`）：hit windows 只在 `_leapSlamClipConfirmed`（確認 clip 真的在播、不是 crossfade 殘影）時才跑（擋前），一旦 `normalized` 越過最後一個窗的 `endNormalized` 就 latch `_leapSlamHitWindowsDone=true` 永久關閉這次 leap 的判定（擋後 + wrap）。
- reflection 驗證：nt 0.10/0.25 → AOE 關；0.40/0.50 → AOE 開；0.60 → latch、AOE 關；0.75/0.90 → 關；latch 後回到 nt 0.45（模擬 wrap）→ 仍關。

### 2. 能量沒及時清空 → 必殺被連續觸發兩次
- 根因：`leapSlamEnergy.Consume()` 原本在蹲完/前搖結束的 commit 點（~1 秒後）才呼叫。那 1 秒內 `UpdateCombatTimer` 每幀看到 `IsFull` 還是 true → 重新 `_leapSlamPending=true` → 第一次 LeapSlam 一結束馬上又觸發第二次。
- 修法：`Consume()` 移到 `TryEnterLeapSlam`（進 windup 的當下就清）。另外 `UpdateCombatTimer` 加 `leapInProgress`（`CurrentState` 是 LeapSlamWindup/LeapSlam 時）guard 當保險（也涵蓋 timer-based fallback）。
- 取捨：前搖被架勢破防打斷也會花掉能量——對一個全程被 telegraph 的必殺是合理的（committed）。
- reflection 驗證：`TryEnterLeapSlam` 後能量立刻 = 0；windup 期間 5× `UpdateCombatTimer` `_leapSlamPending` 維持 False;強制把能量灌回 100 中途 + `UpdateCombatTimer` 仍 False（leapInProgress guard）。

編譯無錯、EditMode 158/159 過。

## 2026-08-28 (追加18) — 武士能對屁孩王造成傷害

- 根因：`BossStateMachine.Awake` 把 `BossHitbox.Configure` 的 attacker team **寫死成 "Boss"**，而每個 boss 的 `BossTeamMember.team` 也都是預設 "Boss" → `BossHitbox.TryResolveHit` 的友軍傷害判定（`teamMember.Team == _attackerTeam`）擋掉所有 boss 對 boss 的傷害。
- 修法：
  - `BossStateMachine.Awake` 改讀自己的 `BossTeamMember.Team`（沒掛的話 fallback "Boss"）當 attacker team。
  - `GreyboxTest.unity` 武士的 `BossTeamMember.team` "Boss" → **"武士"**（屁孩王維持 "Boss"）。
- 效果：武士（team 武士）打屁孩王（team Boss）→ 不同隊 → 傷害生效。**對稱**：屁孩王也能打武士。自己打自己的 hurtbox 仍被擋（`transform.root == _attackerRoot` 先判）。武士打玩家不受影響（玩家 team "Player"）。
- 注意：屁孩王沒有武士那種分部位 hurtbox，只有 root CharacterController 膠囊（上面直接掛 Health）——武士的刀刃 trigger 撞到那個膠囊就會造成傷害。
- 驗證：Play 裡 `BladeHitbox.Activate(SwordJudgment)` + 反射呼叫 `TryResolveHit(屁孩王 root collider)` → 屁孩王 HP 1000 → 968（-32 = SwordJudgment `baseHealthDamage`）。編譯無錯、EditMode 158/159 過。

## 2026-08-28 (追加19) — 鎖定黃色圓圈某些視角會消失

- 根因：lock-on ring 是 world-space UGUI Image，用預設 UI shader → 會跟場景幾何做深度測試。低視角、或被鎖定目標的身體/地面擋在圓圈跟攝影機之間時，圓圈被遮擋而消失。（同一類問題也在 KNOWN_ISSUES 的空島草地剔除那節出現過。）
- 修法：
  - 新增 `Assets/_Project/Rendering/Shaders/UIAlwaysOnTop.shader`（`Live2DAction/UIAlwaysOnTop`）—— 就是 stock `UI/Default` 加 `ZTest Always` + `Queue = Overlay`，其餘 UGUI 行為（tint、sprite atlas、RectMask2D、alpha clip）不變。URP 相容。
  - 新增 `Assets/_Project/VFX/Materials/UILockOnRing.mat`（用該 shader）。
  - `LockOnIndicatorSetup` 建圓圈時把這個 material 指到 Ring image（`EnsureRingMaterial()`，重跑會自動建/修）。
- 驗證：Play 裡把圓圈埋進敵人身體正中央（正常 UI 會被完全遮住），截圖確認黃圈仍畫在角色之上。編譯無錯、EditMode 158/159 過。

## 2026-08-28 (追加20) — 鎖定大型 boss 的隻狼式對決攝影機

使用者要求:鎖定武士(大型 boss)時,畫面要同時清楚看到玩家與 boss 的動作,依兩者體積來框。事前有摘要確認。

### `LockOnTarget` 新欄位
- `useDuelCamera`(bool)— 開啟後這個目標被鎖定時走專用對決框景,覆蓋舊的 `cameraDistanceMultiplier`/`cameraFrameBias`(那兩個留給一般小怪 fallback)
- `duelTargetHeight`(float)— 目標完整站高(武士量測 3.87,設 4.1)

### `ThirdPersonCameraController.UpdateDuelCamera()`(新)
只在「第三人稱 + 鎖定 + 目標有 useDuelCamera + 非瞄準 + Play」時啟用,`_duelActive` 以外的路徑一行不動。每幀:
- **yaw**:滑鼠靜止時以 `duelYawRecenterSpeed` 回正到「玩家背後對準 boss」;滑鼠可甩 `duelYawMaxDeviation`(35°)偏移
- **distance**:取三個約束的最大值再夾在 `[duelMinDistance 5, duelMaxDistance 12]`:(a) boss 頭頂 + margin 塞進垂直 FOV;(b) 近端玩家不超過 `duelPlayerMaxFrameFraction`(0.5)畫面;(c) 玩家腳底不掉出下緣(`duelPlayerBottomMargin` 0.08)。看向點的「前伸」`duelLookMaxAhead`(1.3)絕對上限,避免遠距時玩家掉出下緣
- **look-at**:玩家→boss 連線上 `duelLookBiasToBoss`(0.35,受上限夾)、高度 `duelLookHeightBias`(0.4)—— 玩家推向下緣、boss 佔畫面主體
- **pitch**:攝影機騎在玩家頭頂 ↔ span 頂之間(`duelCamHeightBias` 0.35),瞄準 look-at;滑鼠可 `duelPitchMaxDeviation`(18°)微調、放開回正
- yaw/pitch/distance 全部 smoothing(`duelSmoothSpeed` 6),鎖定當下不硬切

### 驗證(reflection,Play frozen)
- 各分離距離 2/3.5/5/7/10 單位:玩家腳底 viewport Y 穩定在 ~0.21、玩家中心 ~0.30、boss 頭頂 0.68–0.81 —— 全部框內,不 clip
- yaw 從 20° / pitch -5° 起 → 回正到 yaw 0 / pitch 6.2°
- 截圖確認:武士全身置中佔主體、玩家在下前方可見(隻狼式)
- gating:無鎖定 / 鎖一般小怪 / 第一人稱 → `_duelActive=False`,一般攝影機行為零改動
- 編譯無錯、EditMode 158/159 過

**待使用者實機微調**:距離/pitch/玩家在畫面的高度、yaw 回正速度手感。全部值在 Main Camera 的 `ThirdPersonCameraController` 的「Locked-duel camera」區 + 武士的 `LockOnTarget`。

## 2026-08-28 (追加21) — 對決攝影機自適應化(未來更大體積 / 全螢幕 boss)

使用者問「往後放入更大體積 BOSS 或全螢幕怪物需要調整甚麼,有辦法自適應嗎」→ 確認後把追加20 的三個寫死上限改成隨 boss 體積推導。**先 git 同步(追加20 已 push 為 `002ecba`),再做本次改動,並確認武士現有框景零改動。**

### `LockOnTarget` — 高度自動量測
- `duelTargetHeight` 預設由 `2` 改為 `0`(= 自動)。`≤ 0` 時 `DuelTargetHeight` 惰性量測子物件 renderer bounds 的 Y 聯集,結果 sanity-clamp(`< 0.3` 或 `> 60` → fallback 2)。新 boss 掛上 `useDuelCamera` 就能用,不必手填數字。
- 武士維持顯式 `4.1`(手驗值優先於自動量測)。

### `ThirdPersonCameraController` — 三個上限改成隨體積推導
- **`duelMaxDistancePerHeight`(4)**:有效拉遠上限 = `max(duelMaxDistance 12, bossHeight × 4)`。15 單位高的 boss 攝影機能退到 ~60 而不被 12 卡住。
- **`duelMaxFov`(85)**:當「有效上限距離仍塞不下」時,垂直 FOV 從基礎值(65)放寬到最多 85 來補,寧可先廣角再談裁切。武士 `idealDistance ≪ 有效上限` → `_duelFov` 恆等於 65,零改動。
- **`duelMaxVisibleAboveLook`(7)**:對決攝影機最多保證 look-at 之上 7 個單位入鏡。超過此高度的巨物(colossus / 全螢幕怪)頭部裁切出上緣,而不是攝影機無限後退 —— 玩家與 boss 具威脅性的下半身維持可讀。設高(如 20)即恢復「永遠框全身」。
- FOV apply 改:`_duelActive ? _duelFov : 原第三人稱 FOV`。

### 驗證(reflection,Play frozen)
| bossHeight | dist | pitch | FOV | 玩家腳底 viewport Y | boss 頭頂 | 結果 |
| --- | --- | --- | --- | --- | --- | --- |
| **4.1(武士)** | **5.8** | **6.2°** | **65** | 0.21 | 0.77 | 與追加20 逐位元相同 |
| 8 | 8.5 | 2.9° | 65 | 0.15 | 0.86 | 全身框內 |
| 15 | 13.2 | 0.4° | 65 | 0.11 | 0.95 | 全身框內(攝影機自動退到 13.2) |
| 30 | 23.4 | -1.6° | 65 | 0.07 | 1.03 | 頭部裁切(巨物 fallback) |

- 編譯無錯、Console 乾淨、EditMode 158/159 過(唯一失敗 `CharacterAttackAnimationLinkTests...FallsBackToAttack3` 為既有無關項)。
- 三個新欄位已寫進 `GreyboxTest.unity` 的 Main Camera(`duelMaxDistancePerHeight: 4` / `duelMaxFov: 85` / `duelMaxVisibleAboveLook: 7`);武士 `LockOnTarget` 的 `useDuelCamera:1 / duelTargetHeight:4.1` 不動。

## 2026-08-28 (追加22) — 武士 LeapSlam 落地前追蹤玩家位置

使用者要求「Wushi_LeapSlam 落地前我想要讓他能追蹤玩家位置 然後落地」。**反轉**先前「落地 xz 起跳時鎖死、飛行中不 homing」的設計(那條 comment 是 2026-08-27 使用者自己定的)。

### 行為
- `CommitLeapSlamLanding()`(前搖結束)仍做**粗略首次定位**:傳送到玩家附近(退 `LeapSlamLandingOffset` 2 單位,避免疊在玩家膠囊上)。
- `UpdateLeapSlam()` 新增 **homing 區塊**:`_leapSlamClipConfirmed` 且 `normalized < LeapSlamTrackUntilNormalized`(0.45)期間,每幀把 `_horizontalVelocity` 導向玩家**當下**位置(同樣退 offset),並 `FaceTarget(1f)` 重新面向玩家、每幀重打落地 Y raycast。移動速度 = 剩餘距離 ÷ 剩餘時間(用 `_stateTimer / normalized` 推總時長),上限 `LeapSlamMaxTrackSpeed`(30 u/s),不會瞬移。
- `normalized ≥ 0.45` → `_leapSlamLandingLocked` latch,`_horizontalVelocity` 歸零,剩下的下墜是**已鎖定的直線落下** —— 這段是玩家最後一刻的閃避窗。
- 高度弧在 `LeapSlamHeightFallEndNormalized`(0.53)後把 transform pin 死、ApplyMotion 不再 Move,所以 homing 一定在那之前結束(tuning tooltip 有註明 `TrackUntil < FallEnd`)。

### 新 tuning 欄位(`BossTuning`,`Wushi_Tuning.asset` 目前吃程式碼預設)
| 欄位 | 預設 | 意義 |
| --- | --- | --- |
| `leapSlamTrackUntilNormalized` | 0.45 | 追到這個 normalized clip time 就鎖定,之後直線落下 |
| `leapSlamMaxTrackSpeed` | 30 | 空中 homing 速度上限(u/s),玩家狂奔逃離時武士是「可見地追」而非瞬移 |

### 驗證
- 編譯無錯、Console 乾淨、EditMode 158/159 過(唯一失敗 `CharacterAttackAnimationLinkTests...FallsBackToAttack3` 既有無關項)。
- 兩個 tuning 資產(`Wushi_Tuning` / `PW2_Tuning`)確認讀得到新屬性:`trackUntil=0.45 < fallEnd=0.53`。
- **homing 實際手感待使用者實機測**(Play Mode 失焦凍結,無法互動觸發 20 秒能量條 + 中途操控玩家跑位)。若尾段有頓挫感 → 調低 `leapSlamMaxTrackSpeed`;想給更大閃避窗 → 調低 `leapSlamTrackUntilNormalized`。

## 2026-08-29 (追加23) — 匯入貓角色 ＋ 專屬低視線攝影機 ＋ C 鍵切換視角

使用者要求「導入這個模型(Meshy AI 生成的貓)，向玩家一樣提供他攝影機視角並且可將視角切換到他身上，注意他視線較低，與先前攝影機風格不同」。事前有摘要 + 確認(貓可 WASD 控制、C 鍵、放本地出生區、Meshy 付費方案有商用權)。

### 匯入
- `Assets/_Project/Characters/Cat/Cat.glb`(42 MB,gltfast 匯入,同鳥居/寶塔的 `.glb` 路徑)。單一 SkinnedMeshRenderer + 1 材質 + 3 貼圖,43 根 Meshy auto-rig 骨頭,**0 個動畫 clip**。
- 材質保留 gltfast 的 `Shader Graphs/glTF-pbrMetallicRoughness`(URP 相容,實機算圖確認有貼圖、無粉紅)。
- glb 的 SkinnedMeshRenderer 序列化 bounds 是退化的 (0,0,0)(同 KNOWN_ISSUES 鳥居那節的 glTF 匯入坑)→ 設 `updateWhenOffscreen = true`,執行期每幀由骨頭重算,不會被視錐剔除。

### 場景(全部由 `CatCharacterSetup.cs` 產生,可重跑)
- **`Cat`**:本地出生區 `(-2.5, _, 2.0)`,`CharacterController`(h 0.76 / r 0.2 / step 0)+ **共用玩家的 `CharacterMovement`**(rule 8:玩家與 AI 共用輸入介面)+ 自己的 `PlayerInputProvider`。`moveSpeed 3`,dodge/stance/health/flight/lock-on 全部留 null(`CharacterMovement` 每個讀取都有 null 保護)。Visual 縮 0.45 →站高 ~0.77、視線 ~0.45(玩家 ~1.08 的一半 → 「視線較低」)。
- **`CatCamera`**:複製整個 Main Camera rig(Camera + URP data + `ThirdPersonCameraController`),retarget 到貓並**重調低視線風格**:`targetOffset (0, 0.5, 0)`(玩家 0.5,0.5,0)、`distance 1.9`(玩家 2)、`initialPitch 11`、`minPitch -12 / maxPitch 70`(大多往下看地面生物)。`lockOnSource`/`inputSource`/`ultimateAbility` 清空、`enableDescendAutoPitch` 關。起始 inactive、tag `MainCamera`。
- **`CameraPossession`**:`CameraPossessionSwitcher`(新,`Live2DAction.CameraSystem`)。`C` 鍵 + `FocusCat()`/`FocusPlayer()` API。SetActive 切換兩台 Camera(比照 `VehicleEntrySystem`)+ 硬切「一邊 `CharacterMovement` on、另一邊 off」,所以 WASD 只會動你正在看的那隻。`OnDisable` 防呆:中途被拆掉時把控制權還給玩家。

### 驗證
- 編譯無錯、Console 乾淨、EditMode **162/163**(新增 `CameraPossessionSwitcherTests` 4 個全過;唯一失敗仍是既有無關的 `CharacterAttackAnimationLinkTests...FallsBackToAttack3`)。
- Play mode 實機算圖(失焦凍結,用 reflection 驅動):`FocusCat()` → Main Camera inactive、CatCamera 成為唯一 live camera(`Camera.main=CatCamera`)、貓 `CharacterMovement` on、玩家 off;截圖確認貓有貼圖、低視線俯看框景。`FocusPlayer()` → 完全還原玩家視角(Maya + 武士 + 綠色鎖定圈)。
- **待使用者實機微調**:`CatCharacterSetup` 的 `CatScale` / `CatCam*` 常數(距離、pitch、貓在畫面的高度),重跑選單即套用。
- **已知**:貓也會跟著跳(Space→`CharacterMovement`)。佔位可接受,見 `KNOWN_ISSUES.md`。

## 2026-08-29 (追加24) — 貓的四肢程序化行走(依移動速度驅動)

使用者:「這個GLB應該是有骨架的 尤其是四肢，能否將四肢參照貓咪行走姿態來對應移動控制」。glb 有 43 根 auto-rig 骨頭但 0 個 clip,把外來四足 rig retarget 到 generic 骨架又很脆,所以直接**程序化驅動四條腿**。

### `CatProceduralWalk.cs`(新,`Live2DAction.Characters`)
- 從 `Cat.glb` 的 live BakeMesh + 骨樹 dump 認出四條腿:前腿 `Bone_034/032`(FL)、`Bone_042/040`(FR);後腿 `Bone_018/016`(BL)、`Bone_023/021`(BR)。`Bone_000` = 骨盆,`Bone_004-008` = 尾巴。
- 每條腿:**肩/髖骨** fore-aft 擺(`cos(相位)`,相位 0 最前、0.5 最後)+ **肘/膝骨** 只在擺動半週(腳離地往前收)彎(`max(0, -sin(相位))`,`bendSign` 讓前肘與後膝反向折)。→ 每隻腳「往後移時著地推進、往前移時抬起收腿」。
- 對角小跑步態:FL+BR 同相位(0)、FR+BL 差半週(0.5)。四個 `phaseOffset` / `bendSign` 都是 serialized,可改 4-beat walk。
- 旋轉是繞**貓 root 的 right 軸**、換算到每根骨的 parent space 再套:`bone.localRotation = AngleAxis(度, 右軸@parent空間) * 靜止localRotation`。跟 generic 骨頭各自的朝向無關。
- 幅度隨 `CharacterMovement.CurrentHorizontalSpeed`(同 `CharacterAnimatorLink` 讀的速度訊號)縮放,`_gaitBlend` 在起步/停下時 ease in/out。靜止 → 回靜止姿勢。
- 在 `LateUpdate` 跑(貓身上沒有 Animator,沒東西跟它搶骨頭)。

### tuning 預設(全部 serialized)
`speedForFullStride 3`(= `CatMoveSpeed`)、`strideFrequency 1.7`、`swingDegrees 16`、`bendDegrees 30`、`blendSpeed 9`、`bodyBobDegrees 0`(選配,掛在 `Bone_000`)。

### 驗證
- 編譯無錯、Console 乾淨、EditMode **165/166**(新增 `CatProceduralWalkTests` 3 個全過;唯一失敗仍是既有無關項)。
- Play mode(這次 Editor 有焦點、真 frame 有跑):`FocusCat` + 灌入移動 → `CurrentHorizontalSpeed` 上升、`_gaitBlend→1`、`_phase` 循環;量測四隻腳趾骨相對貓身的 z(前後)/y(高度)確認會隨相位在對角模式下位移;側視算圖(phase 0.15 / 0.40)確認四肢有前後擺、腳貼近地面、無抽搐/反折。
- **步態手感待使用者實機微調**(程序化 gait 一定要看著動的才調得準):`CatProceduralWalk` 上調 `swingDegrees` / `bendDegrees` 加大擺幅、`strideFrequency` 調步頻、`bodyBobDegrees` 加身體起伏;想換 4-beat walk 就改四個 `phaseOffset`。`CatCharacterSetup` 重跑會重建並重新抓骨頭。

## 2026-08-29 (追加25) — C 鍵切換:診斷 log ＋ 解掉 C 鍵衝突

使用者回報「C按鍵並沒有對應在貓身上」。查證:`CameraPossessionSwitcher` 的切換邏輯完全正確——play mode 強制驗證 C 鍵路徑(合成 C 事件 → `switcher.Update()` → `Current` Player→Cat)、合成 W → **貓移動 (0,0,0.99)、玩家不動 (0,0,0)**、貓 `CharacterMovement` on / 玩家 off。committed 場景的 `CameraPossession`(active、enabled、`toggleKey:17`=C、四個參照都解析得到)也沒問題。

無法在程式面重現,推測是環境問題(Game view 沒 focus / 沒注意到切換 / 舊 build)。改動:
- **`CameraPossessionSwitcher.Update()`**:C 鍵被偵測到時印一行 `[CameraPossession] C pressed -> switching to Cat/Player`。使用者按 C 看 Console 就知道「按鍵有沒有被吃到」——有 log = 有效(WASD 這時控制貓、攝影機變低);沒 log = focus / 場景問題。
- **`SwordDisplayAdjuster`**:縮放鍵從 `C`/`V` 改成 `,`/`.`(逗號/句號)。`C` 本來被這支 dev 工具吃去縮放劍展示組(`V` 也跟 `PlayerInputProvider` 的第一人稱切換撞),不影響 `CameraPossessionSwitcher` 的偵測(兩者各自讀鍵盤),但是個該解的衝突。移動鍵 `Z`/`X` 不變。

編譯無錯、Console 乾淨、EditMode 165/166(唯一失敗仍是既有無關項)。純程式碼改動,場景不變。

## 2026-08-29 (追加26) — 貓的飛行 ＋ 衝刺(接線,參照玩家)

使用者:「想讓此貓做出攻擊動作」→ 決定分三個切片(1 飛行+衝刺 → 2 程序化攻擊 → 3 飛行/衝刺姿態)。本則是**切片 1**。

關鍵事實:飛行 / 飛行 boost / 地面衝刺(dodge)的邏輯**本來就全部寫在 `CharacterMovement`**(null-safe、靠 serialized 欄位 opt-in),就是貓已經掛的那顆元件;貓 2026-08-28 的初版是刻意沒接(`dodgeData` / `flightEnergy` 留 null)。按鍵也早就共用(貓用同一個 `PlayerInputProvider`):按住 Ctrl 飛、按住 Shift 下降、按住 Q boost、點 Shift 衝刺。CatCamera 是玩家攝影機 rig 的複製,`ThirdPersonCameraController` 已會讀 `IsFlying`/`CurrentBankRollDegrees`/`IsDescending`。所以切片 1 = 純接線,不寫新移動碼。

### `CatCharacterSetup.cs` — 新增 `WireFlightAndDash()`
- `cat.AddComponent<UltimateEnergy>()` 當飛行體力(同 `FlightSetup` 對玩家的做法),設 `maxEnergy 500 / regenAmount 30 / regenInterval 1 / regenIdleDelay 3`(**與玩家場景值一致**)。
- 產生 `Assets/_Project/Settings/Movement/Cat/CatDodgeData.asset`(`CreateOrLoadCatDodgeData`,再跑不覆蓋;`EnsureFolder` 遞迴補資料夾):`distance 1.6`(玩家 3,約 2 個貓身長)、frame 12/12/20 與玩家相同。
- 寫進貓 `CharacterMovement`:`flightEnergy`、`dodgeData`、`flightMoveSpeed 7`(玩家 9)、`flightAscendSpeed/DescendSpeed 5`(玩家 6)。其餘(boost 1.8、drain、dive、bank)留 `CharacterMovement` 預設 = 玩家值。
- 這些「比玩家小」的數字是照貓 0.45 縮放 + 地面速度已是 3 抓的**起點值**,同 `CatCam*` 一樣待實機手調,重跑選單即套用。

### `CatProceduralWalk.cs` — 飛行時腿不要空踩
- 飛行時 `CurrentHorizontalSpeed` 是(較快的)飛行巡航速度,會把 gait 開到滿、看起來像貓在空中狂奔。新增 `ComputeGaitTarget(flying, speedNorm)`(pure,可測):飛行時 gait target 強制 0,腿 ease 回靜止姿勢。真正的「收腿飛行姿態」是切片 3。

### 驗證
- **選單已跑(MCP 重連後)**:`Tools/Live2DAction/Add Cat Character + Camera` 執行成功,場景已存。確認貓 `CharacterMovement`:`flightEnergy` → 貓身上的 `UltimateEnergy`(max 500 / regen 30 / idleDelay 3)、`dodgeData` → 新產生的 `Assets/_Project/Settings/Movement/Cat/CatDodgeData.asset`(distance 1.6 / 12 frames)、`flightMoveSpeed 7` / `flightAscendSpeed 5`。`catMovement.enabled=False`(附身才啟用,正確)。
- **EditMode:實跑通過**。168 tests,唯一失敗是既有無關項 `CharacterAttackAnimationLinkTests.TriggerNameForComboIndex_BeyondThirdHit_FallsBackToAttack3`(Attack4 vs Attack3,與本次無關)。含 `CatProceduralWalkTests` 的 `ComputeGaitTarget_*`(飛行→0、地面隨速度)。
- **PlayMode:尚未實跑** —— Test Runner 在 Editor 短暫失焦後 wedge(`tests_running` 卡住,見 KNOWN_ISSUES「Unity MCP PlayMode tests can wedge the Test Runner」)。需使用者點回 Editor 或重啟後重跑 `CatFlightAndDashTests`(0.45 縮放 rig:按住 Fly → `IsFlying` 真 / 體力降 / 爬升;放開 Fly 仍懸停;點 Dodge → `DodgePhase.Dodging` + 無敵 + 位移 + 回 Idle)。
- **待使用者實機**:貓沒有飛行體力條 UI(切片 3)、體力初值 0(同玩家,附身後 regen 到 30 才能起飛,約 1 秒);飛行/衝刺數值手感。按 C 附身 → 等 1 秒 → 按住 Ctrl 飛 / 點 Shift 衝刺。

## 2026-08-29 (追加27) — 修:第一人稱下按 C 切貓,玩家角色消失只剩劍

使用者回報:「有時用 C 會切換不到貓咪視角,而是卡在玩家……玩家消失了 只剩一把大劍裝飾品」。

### 根因
`ThirdPersonCameraController` 在第一人稱(按住右鍵瞄準 **或** V 鍵 toggle)時,每個 `LateUpdate` 都呼叫 `SetOwnVisualHidden(true)`——關掉玩家 `Visual` 底下**所有 Renderer**,只留 `firstPersonVisibleWeapon`(那把劍)。`CameraPossessionSwitcher` 按 C 切貓時用 `SetActive(false)` 關掉玩家攝影機,那顆 `ThirdPersonCameraController` 的 `LateUpdate` 就停了,先前關掉的 Renderer **永遠不會被重新開啟**;`OnDisable` 當時只還原鼠標鎖,沒管 visual。→ 附身貓期間玩家整隻隱形,只剩劍浮在原地。「有時」= 只在切 C 前處於第一人稱時才會。

### 修法(`ThirdPersonCameraController.cs`)
- 新增 `_firstPersonHideApplied` 旗標,`LateUpdate` 末尾記錄目前是否套用了隱藏。
- `OnDisable`:若旗標為真,呼叫 `SetOwnVisualHidden(false)` + `SetAccessoryHidden(firstPersonHiddenAccessory, false)` 還原,並清旗標。切回玩家時仍在第一人稱 → `LateUpdate` 自然會再隱藏一次(正確、自洽)。
- `SetOwnVisualHidden` 加 `target == null` 防護(play-mode-stop 拆場景時 `OnDisable` 可能在 target 已銷毀後跑)。

### 驗證
- **EditMode:實跑通過**(MCP 重連後,168 tests,失敗項同上述既有無關項)—— 編譯無誤,`ThirdPersonCameraController` 改動沒造成 regression。
- **PlayMode:尚未實跑** —— 新檔 `CameraPossessionFirstPersonRestoreTests`(第一人稱下 `FocusCat()` → 玩家 body Renderer 重新開啟,劍本來就沒關;切回玩家仍第一人稱 → body 再次隱藏)跟 `CatFlightAndDashTests` 一起卡在 Test Runner wedge,待使用者點回 Editor 重跑。
- **另註**:使用者「切換不到貓咪視角」的部分,若清 body 隱藏後仍覺得沒切到,多半是貓出生點離玩家僅 2m + 0.45 縮放,CatCamera 視野裡塞滿(現在可見的)玩家;把貓開遠一點再切 C 對照。真的沒切到就看 Console 有無 `[CameraPossession] C pressed`(沒有 = Game view 失焦,見 KNOWN_ISSUES 的 Play Mode 失焦凍結)。

## 2026-08-29 (追加28) — 貓空中衝刺無感:CatDodgeDistance 1.6 → 3(數值微調)

使用者實機回報:「貓咪視角時 空中沒法衝刺 且衝刺時沒特效」。

### 空中衝刺無感 —— 已調
- 診斷:貓 dodge 其實有觸發(位移、無敵、回 Idle 都正常),但 `CatDodgeDistance 1.6` → dash speed `1.6 / 0.2s = 8`,只比飛行巡航 `flightMoveSpeed 7` 快 **1.14x**,在空中/地面都讀不出「衝」的感覺。玩家是 `3 / 0.2s = 15` vs 飛行 9 = **1.67x**。
- 修法:`CatCharacterSetup.CatDodgeDistance` **1.6 → 3**(與玩家一致)。現在 dash speed 15 vs 貓飛行巡航 7 = **2.14x**,空中衝刺明顯。frame timing(12/12/20)不變。
- `CreateOrLoadCatDodgeData` → 改名 `CreateOrUpdateCatDodgeData`:**每次重跑選單都把 `Cat*` 常數重寫進 `CatDodgeData.asset`**(舊版「asset 已存在就原樣回傳」→ 改 `CatDodgeDistance` 重跑選單其實沒作用)。跟 flight 數值一樣的「改常數、重跑選單即套用」契約。此 asset 無人手調,由 setup script 擁有。
- `CatFlightAndDashTests.CreateCatDodgeData()` 的 stub 值 1.6 → 3 保持一致。

### 衝刺沒特效 —— 未做,留切片 3
- 貓沒有 `Health` 元件,也沒掛 `InvulnerabilityRippleEffect`(玩家衝刺無敵時的世界空間銀白漣漪,靠 `Health.IsInvulnerable` 驅動;`CharacterMovement` 只在 `health != null` 時同步 `IsDodgeInvulnerable → health.IsInvulnerable`)。
- 使用者選擇本次只調數值,特效歸切片 3(飛行/衝刺姿態 + 飛行體力條 UI + 數值微調)。切片 3 要嘛給貓加 `Health` + `InvulnerabilityRippleEffect`(可能重用 `InvulnerabilityRippleSetup`),要嘛做輕量版直接讀 `CharacterMovement.CurrentDodgePhase`。

### 驗證
- 選單重跑成功,確認 `CatDodgeData.asset` distance=3 / speed=15、貓 `CharacterMovement.dodgeData` 指向它、場景已存。
- EditMode 實跑 168 tests 通過(唯一失敗仍是既有無關的 `CharacterAttackAnimationLinkTests…FallsBackToAttack3`)。
- PlayMode(`CatFlightAndDashTests` 等)仍卡 Test Runner wedge,待使用者實機。使用者實測空中衝刺手感。

## 2026-08-29 (追加29) — 貓咪戰鬥機制:完整設計文件（切片 2 規劃）

使用者:「接下來是貓咪普通攻擊的部分,先探討作法,我要做完整機制大項目」。經一輪探討 + AskUserQuestion 定案,產出 **`Docs/CAT_COMBAT_DESIGN.md`**(照 `FLIGHT_SYSTEM_DESIGN.md` 格式)。

### 範圍(使用者定案)
三段連段 ＋ 蓄力重擊 ＋ 撲擊 pounce ＋ 空中攻擊 ＋ 命中反饋(hitstop ＋ 螢幕震動 ＋ 擊退 ＋ 音效)＋ 敵貓,全部要做,分 7 個切片交付。

### 關鍵調查結論
- **可複用**:`PlayerCombat`(主動 `OverlapCapsule`/`OverlapSphere`,攻擊端不需 collider)、`ComboAttackState`、`AttackData`、`AttackResolver`、`EnemyAI`(已 `IInputCommand` + `ICharacterSpeedSource`)。
- **要全新做**:蓄力機制(`ComboAttackState` 只有連點推進、`AttackData` 無 charge 欄位、`IInputCommand` 無 held 信號)、通用命中反饋(hitstop/knockback/screenshake 只有 Boss 專屬版)、音效/震動基礎設施(只有 `RangedWeapon` 有 `AudioSource`)、撲擊、多骨頭 procedural pose(`AttackPoseVisualizer` 單骨頭已停用)、貓的 `Health`/`Hurtbox`。
- 玩家目前是 **3 段** combo、**無真正蓄力**(所謂「續力」= 連點續段)。

### 操作定案(AskUserQuestion)
- 蓄力重擊 = 按住左鍵 ≥0.35s 放開(取代普攻起手);點放 = 連段。
- 撲擊 = 地面移動中點左鍵(移動中無法原地揮爪,已接受);靜止點左鍵 = 原地連段。
- hitstop **只在貓附身時**啟用 —— 不碰玩家/Boss 戰的 `Time.timeScale`,附身切回玩家立即還原 1。
- 攻擊時自由移動(不鎖腳)、攻擊朝身體正面(不給貓 lock-on)、空中用 `PlayerCombat.UseSphericalJudgment`。

### 切片順序
2-1 骨架+單招 → 2-2 三段連段 → 2-3 `CatAttackPose` → **2-4 命中反饋(提前)** → 2-5 蓄力重擊 → 2-6 撲擊 → 2-7 貓承傷+敵貓。

尚未寫任何實作碼。下一步:開工切片 2-1。

## 2026-08-29 (追加30) — 貓咪近戰機制:切片 2 全部 7 項一次實作

使用者:「開始動工 一次全部做完再回報」。7 個子切片一次做完(設計見 `Docs/CAT_COMBAT_DESIGN.md`)。

### 共用碼改動(全部 additive,177 EditMode tests 中 176 綠、唯一失敗是既有無關的 `CharacterAttackAnimationLinkTests…FallsBackToAttack3`)
- **`ComboAttackState`**:加 `StartOverride(AttackData)` / `IsOverrideAttackActive` —— 連段陣列外的單發招(蓄力重擊 / 撲擊),跑同一套 Startup/Active/Recovery,`ComboIndex` 全程留 -1,不串連段。既有 12 個 `ComboAttackStateTests` 不受影響。
- **`AttackData`**:加 `knockbackForce` / `knockbackLaunches`(預設 0/false → 既有所有 asset 行為不變)。
- **`AttackResolver.ResolveHits`**:`DamageInfo.Direction` 從 `Vector3.zero` 改成「遠離攻擊者的水平單位向量」(`StancePoise` 不讀 Direction,既有無影響);命中且 `KnockbackForce > 0` 且目標有 `IKnockbackReceiver` 時派發擊退。
- **`PlayerCombat`**:加 `TryStartOverrideAttack` / `FeedAttackPressed`(外部輸入路徑,`_externalAttackPressed` 預設 false → Player/Enemy/dummy 不受影響)/ `IsIdle` / `IsOverrideAttackActive` / `CurrentAttackId` / `Hit` 事件(命中/揮空各發一次,只有 `CatCombatFeedback` 訂閱)。
- **`PlayerInputProvider`**:加 `AttackHeld`(**非** `IInputCommand` 成員,player-only —— 不動任何 AI stub / 介面實作)。
- **`CharacterMovement`**:未動 `Update`(撲擊改用既有 `ApplyDash`);切片 2-1 只多接了貓的 `health` 欄位。

### 新 runtime 元件
- `CatAerialJudgment`(飛/滯空 → `UseSphericalJudgment`)、`CatChargeAttack`(按住左鍵放開 → 重擊,tap → 連段)、`CatPounce`(移動中點左鍵 → `ApplyDash` 前衝 + 撲擊招,order -8 先於 charge)、`CatAttackPose`(多骨頭 procedural,`ComputeSwing` pure,LateUpdate order 20,疊在 `CatProceduralWalk` 上,出招時 `SetAttackSuppression(1)`)。
- `HitStopController`(scene-single,`Time.timeScale` dip;只有 `CatCombatFeedback` 在貓附身時呼叫,切回玩家 `CancelAndRestore`)、`CameraShake`(掛雙相機,order 100)、`CombatSfx`(clip 待補,wired-but-silent)、`CatCombatFeedback`(訂 `PlayerCombat.Hit` → hitstop/shake/sfx,自己 gate 附身狀態)、`MeleeKnockback`(通用擊退:`CharacterMovement`/`CharacterController`/transform 三種目標)。
- `CatProceduralWalk`:加 `SetAttackSuppression` hook(乘 gait 幅度)。

### Editor
- `CatCharacterSetup.WireCombat()`:貓 root 掛整套 melee stack、產 `Settings/Combat/Cat/CatSwipe1-3 + CatHeavy + CatPounce.asset`、`AttackOrigin` 子物件(嘴前)、`catControl` 加 PlayerCombat/CatChargeAttack/CatPounce/CatAerialJudgment、`HitStopController` 掛 `CameraPossession`、`CameraShake` 掛雙相機。**已跑,場景已存,execute_code 逐項確認接線正確。**
- 新選單 `Tools/Live2DAction/Add Enemy Cat (AI-driven)`:`EnemyCat`(EnemyAI + PlayerCombat[CatEnemySwipe] + CatProceduralWalk[speedSource=EnemyAI] + CatAttackPose + Health + MeleeKnockback),GreyboxTest (6,6)。**已跑。**

### 測試
- **EditMode 實跑通過**:新增 `CatCombatTests`(8:ComputeSwing/LeadPaw/StartOverride×3/LungeSpeed/DecayFactor)+ `CatProceduralWalkTests` 的 suppression clamp。177 total,176 綠。
- **PlayMode `CatMeleeCombatTests`(6:打假人扣血 / 出界不中 / combo 續段 / 球判側擊 / 重擊override+擊退 / hitstop dip+cancel)已寫、compile 過、尚未實跑** —— MCP 下 Play mode 失焦凍結,測試 coroutine 卡在第一個 yield(見 KNOWN_ISSUES)。待使用者點著 Editor 跑。
- **待使用者實機**:所有 pose 角度/軸(generic auto-rig 骨頭方向未知)、frame data 手感、hitstop 長度、撲擊距離、震動強度、敵貓難度、SFX clip。

### 追加30 修正 — 「有時普通攻擊也會衝刺」
使用者回報:貓站著揮爪有時會變成撲擊(前衝)。兩個原因:
1. **輸入讀取晚了一幀**:`CatPounce` 在 order -8 讀 `PlayerInputProvider.MoveInput`,但 `PlayerInputProvider` 在 order 0 才更新 → 讀到上一幀的 `MoveInput`,玩家已放開方向鍵、撲擊卻還用舊值觸發。修:`PlayerInputProvider` 加 `[DefaultExecutionOrder(-100)]`,所有 consumer(movement/combat/camera/CatPounce/CatChargeAttack)都讀到當幀輸入。Input System 的 `wasPressedThisFrame` 依事件時間軸判定、不看 poll 順序,提前 poll 不會漏 edge 或重複消費。179 EditMode tests 無迴歸。
2. **觸發門檻太鬆**:原本「按住任一方向鍵 + 點左鍵」就撲擊 → 站著微調位置手還在鍵上就誤觸。修:`CatPounce.ShouldPounce`(新 pure 函式,`CatCombatTests` +2 鎖行為)現在要求**同時**有方向輸入**且**實際水平速度 ≥ `moveSpeed × 0.7`(貓 3 → 2.1)。站著、貼牆、剛起步加速中、放鍵滑行 → 全部維持普通揮爪;真的在跑 + 點左鍵才撲擊。
選單已重跑,`pounceMinSpeedFraction=0.7` 已套用,場景已存。

### 追加30 續 — 貓咪 5 秒復活 ＋ 場上兩隻敵貓
使用者:「1.貓咪死後5秒復活 2.場上有第二隻貓咪」。

- **新 `RespawnWiring`**(Editor 共用 helper):在 `GameManager` 上找/建/回收一顆 `RespawnController` 指向某角色,同 `EnemyRespawnSetup` / `MechaRespawnSetup` 的 reclaim-orphan 模式。in-place 復活、delay 5s(全場景一致)。
- **玩家貓**:`CatCharacterSetup` 現在也給 `Cat` 掛 `RespawnController`(5s)。附身狀態下貓死掉時控制/視角會凍 5s —— 同玩家自己的 `RespawnController` 既有取捨(最簡死亡處理,無 game-over 畫面)。
- **`CameraPossessionSwitcher` 加 `catHealth`**:附身貓時貓死掉 → 自動 `FocusPlayer()` 把控制/視角交還玩家(否則會卡在關掉的 CatCamera 看黑畫面 5s);貓復活後再按 C 重新附身。null-safe,`CameraPossessionSwitcherTests` 無迴歸。
- **敵貓 ×2**:`EnemyCatSetup` 改成一次產 `EnemyCat` @ (6,6) ＋ `EnemyCat2` @ (-6,6),各自掛 `RespawnController`(5s)。選單改名 `Tools/Live2DAction/Add Enemy Cats (AI-driven, x2, respawn)`。re-runnable(先移除舊 RC 再 destroy+rebuild)。
- 選單已重跑:`GameManager` 上 Cat/EnemyCat/EnemyCat2 各一顆 RC(health 已接、delay 5、0 orphan),兩隻敵貓就位,`switcher.catHealth` 已接,場景已存。EditMode 179 tests 178 綠(唯一失敗仍是既有無關項)。

### 追加30 再修 — 敵貓移除 ＋ 脖子骨頭確認
使用者:「場上仍然有兩隻貓 請排除」(→ AskUserQuestion:兩隻敵貓都刪)、「貓咪不是有脖子骨頭嗎 幫我操控看看效果」。

- **敵貓移除**:`EnemyCat` / `EnemyCat2` 從場景刪除,它們的 `RespawnController` 一併移除(`GameManager` 剩 9 顆:8 原有 + `Cat`)。刪掉 `EnemyCatSetup.cs` 與 `CatEnemySwipe.asset`。切片 2-7 的「敵貓」部分**取消**(使用者規劃時說要、實際看到後決定不要)。`CatAttackPose` / `CatProceduralWalk` / `MeleeKnockback` 保留(玩家貓在用)。場景現在只有一隻可附身的 `Cat`,戰鬥標靶用現有 `TrainingDummy` / `Enemy`。
- **脖子骨頭確認 + 微調**:解 glb 骨樹確認脖子鏈是 `Bone_028`(頸根,接肩胸 Bone_011)→ `Bone_027`(頸中)→ `Bone_026`(頭尾端,leaf)。繞各自 local X 軸 pitch → 頭乾淨地上下擺(截圖驗證:35° 明顯低頭、25°×2 是強力咬擊下探、20°×2 抬頭)。大角度時頸部有可見擠壓但可接受。據此把 `CatAttackPose` 的頭部 pose 從「只有 Bone_027、strike 16°(幾乎看不出)」改成「Bone_028 + Bone_027、strike 合計 ~32° 下探 / windUp ~18° 抬頭」。選單已重跑套用。
- 玩家貓 5 秒復活、`CameraPossessionSwitcher.catHealth` 自動交還控制 —— 保留(使用者要的)。
- EditMode 179 tests 178 綠(同既有無關失敗)。

### 追加30 再修2 — player / 守望者 / cat 三視角互切
使用者:「讓 player 守望者/cat 三者可以互相切換視角」。之前 `ViewFocusDirector`(T 鍵,守望者)只認得 `onFootCamera`(Main Camera)/ `vehicleCamera`,附身貓時(CatCamera active、Main Camera inactive)按 T 沒反應。

- **`ViewFocusDirector` 加 `catCamera` / `catController`**:`ActiveCamera()` / `ControllerFor()` 多一個候選。T 現在接管「當下 active 的那台相機」——不管你附身 player 還是 cat。
- **`suspendWhileWatching` 加貓的控制組**(`CharacterMovement` / `PlayerCombat` / `CatChargeAttack` / `CatPounce` / `CatAerialJudgment`)。守望者視角下 W/A/S/D 只平移攝影機、不會驅動貓。director 的 snapshot-restore 機制自動處理「你是 player 時貓控制早就 disabled」的情況。
- **`CameraPossessionSwitcher` 加 `viewDirector`**：守望者視角中按 C —— 追加初版是「忽略」，但使用者回報「可以從 c 轉回 t 但反過來不行」（按 C 想回角色卻沒反應）。改成 **C 在守望者視角中 = 離開守望者，回到你剛剛附身的角色**（跟 T 一樣）；再按一次 C 才 player↔cat 切換。（不做「離開+切換」一鍵完成：`ViewFocusDirector` 的返回路徑會在 LateUpdate 還原 pre-Watcher 的控制 snapshot，會蓋掉同一幀在 Update 做的附身切換。）
- 新 `WatcherCatWiring`(Editor 共用 helper)雙向接線,`CatCharacterSetup` 與 `WatcherSetup` 結尾都呼叫(哪個選單後跑哪個補完)。
- **操作**：C = player ↔ cat 附身；T = 守望者視角開關（從 player 或 cat 都行，回來時回到你剛剛附身的那個）；**守望者視角中 C 和 T 都能離開**。三視角任意兩者間 ≤2 鍵可達。
- Play mode 反射驗證：cat→watcher→（C 或 T）離開 → 貓控制正確 restore、附身狀態保留；player 同理。相機 active、`catMove.enabled` 逐步確認正確。EditMode 179 tests 178 綠（同既有無關失敗）。

## 2026-08-29 (追加31) — 新區域「學校」greybox（新城市 第 1 步）

使用者:「接下來我要製作新城市。本地不是有一個洞口給車出去嗎，出去有一條通道，通道頂端再銜接建立一個像 ground 一樣的土地，命名為'學校'」。

- **新選單 `Tools/Live2DAction/Add School Area (學校 ground + walls)`**(`SchoolAreaSetup.cs`,re-runnable delete+rebuild)。
- **`學校`**:30×30 的 Cube 地板(同 `GreyboxSceneBuilder.CreateGround` 慣例:`Ground_StoneFloor` 材質、上表面 y=0.5)。位置:北緣**貼齊 `VehicleRoad` 南端**(讀 live collider `bounds.min.z`,現況 z=-80,fallback -80)→ 中心 (0, 0, -95),z 範圍 [-110, -80]、x [-15, 15]。與道路無縫接(實測 `學校` 北緣 z = 道路南端 z = -80.0)。
- **三面邊界牆** `SchoolWall_South` / `_East` / `_West`(BoxCollider + 灰色 `GreyboxWall.mat` visual,高 6、厚 1、跨 32,鏡射 `GreyboxSceneBuilder.CreateBoundaryWalls`)。**北面留空給道路**。
- 選單已跑、場景已存、compile 乾淨。截圖確認:本地 → 道路 → 學校 三段相連、地板齊平。

### 追加31 續 — 學校圍牆改成單洞口(不同顏色)＋ car 面向本地洞口
使用者:「1.學校的圍牆要跟本地一樣只留一個洞口 圍牆用不同顏色 2.car 的位置改為 面向本地的洞口出口」。

- **學校圍牆改成完整周界 + 單一洞口**(跟本地南牆同形狀):`SchoolAreaSetup` 現在建 5 段 —— `SchoolWall_South`/`_East`/`_West` 全牆 + `SchoolWall_NorthLeft`/`_NorthRight` 夾住北面路口。洞口置中 x=0(對齊道路)、寬 = 道路寬 + 0.6 ×2 ≈ 7.2(讀 live road collider)。
- **圍牆改用 `SchoolWall.mat`(teal 青綠)** —— 明顯區別於本地南牆的橘色、和 greybox 灰。(舊 `GreyboxWall.mat` 不再使用。)
- **Buggy 移到 (0, 1.2, −10)、朝向 y=180**(forward = −Z),正對本地南牆洞口(z=−15.5,5.5m 前方),往前開就直接穿洞口上道路。清掉 rigidbody 殘留 velocity。場景已存。
- **待辦(新城市後續)**:建築 / 生成點 / 傳送點 / 道路正式併入 `學校`;`學校`/`SchoolWall` 目前沒有 `BoundaryBlockEffect` 撞牆 FX、背景地形沒延伸過去。

## 2026-08-29 (追加32) — 武士加 leash(警備範圍)

使用者:「武士感覺警備範圍怪怪的 明明玩家離得很遠」。根因:`BossStateMachine` 完全沒有 leash —— 一旦 `alertRange`(5) 內觸發交戰,`Approach`/`Attack` 就無止境追玩家,沒有「放棄」距離,會一路追過整張地圖。

- **`BossStateMachine` 新增 `leashRange`(預設 14)＋ `returnHomeSpeedFraction`(0.6)**。leash 從**駐點**(`_homePosition`,Awake 時的位置)量,不是 boss 當下被追到的位置。
- **新 `TryLeashReset()`** 進 `Update` 優先序(HitReaction 之後):任何非終止狀態下,**玩家離駐點 > `leashRange`** → 立刻 `ChangeState(Dormant)`、清速度。terminal 狀態(Dead/Victory)＋ 相位轉換演出不受影響。`leashRange = 0` = 停用(回到舊的無限追擊)。
- **`UpdateDormant()`** 現在會「走回駐點」——被 leash 拉回 / 被打飛離開駐點後,Dormant 狀態下朝 `_homePosition` 移動 + 轉向,回到 0.6m 內才停。玩家回到 `alertRange` 內照樣重新 Alert。
- Play mode 反射驗證:玩家瞬移到 z=-30(離駐點 30m)→ boss 從 `Attack` 轉 `Dormant`;玩家回 spawn(離駐點 2.5m)→ boss 轉 `Alert`。EditMode 179 tests 178 綠(同既有無關失敗)。
- **`武士` 移到本地北端** (0, 0.6, 11)、面向南(forward −Z,朝玩家來的方向)—— 使用者選的。離玩家 spawn 11.3m(> `alertRange` 5、< `leashRange` 14),所以一進 Play 是 Dormant、`WushiBossHud` 隱藏;往北走 ~6m 進 alertRange 才交戰;往南跑離駐點 >14m boss 放棄走回北端。Play mode 反射驗證這三段都對。

## 2026-08-29 (追加33) — 車輛操控:降低轉向靈敏度 ＋ 加抓地/角阻尼(治甩尾與原地打轉)

使用者:「感覺car有點難操控」→(AskUserQuestion)「轉向太靈敏/容易過彎過度、甩尾」＋「容易打滑/原地打轉」。

`VehicleController` 調校(code default ＋ 場景 Buggy 兩邊都改,`OnValidate`/`Awake` 會套用):
- **轉向收斂**:`maximumSteeringAngle` 32°→**24°**;`steeringSpeedFalloffReference` 70→**45 km/h**(速度一起來轉向權限就明顯下降,不會高速還大打方向);`steeringSmoothSpeedDegrees` 120→**90**(轉入更漸進)。
- **側向抓地**:`sidewaysFriction` `stiffness` 1.6→**2.2**、`asymptoteSlip` 0.5→**0.7**、`asymptoteValue` 0.75→**0.85**(車尾不那麼容易脫出,而且滑動中還能保有抓地力,不會過峰值就掉到很低)。
- **角阻尼**:新增 `angularDamping` 欄位(套到 Rigidbody,`Awake`/`OnValidate`),Unity 預設 0.05 → **0.5** —— 這是治「原地打轉」的關鍵:底盤一旦開始偏航(路緣、急彎、顛簸)以前幾乎沒有阻尼收斂它。
- 極速 90km/h、馬力 3500、重心 −0.45 不動(使用者沒選「太快/翻車」)。
- EditMode 179 tests 178 綠(同既有無關失敗)。Play 進去確認 `Awake` 有套用(rb.angularDamping=0.5、輪子 sidewaysFriction stiffness=2.2)。**實際手感待使用者實機**。

## 2026-08-29 (追加34) — 武士 leash 5m ＋ 起飛時鎖定丟失/視角亂轉修復

使用者:「1.武士警備距離改為5m 2.武士在起飛後 玩家如果是鎖定視角會瞬間丟失 並且視角不規則轉向」。

### 1. leash 5m
`BossStateMachine.leashRange` 14 → **5**(＝ `alertRange`)。加 `leashGraceSeconds`(0.35):玩家離駐點 > leashRange 要**持續 0.35s** 才 disengage —— 避免 leash 跟 alert 同距離時在邊界 Dormant↔Alert 每幀跳。

### 2. 起飛時鎖定丟失 + 視角亂轉
根因兩個:
- **`TargetLockUtility.IsStillValid` 用 3D 距離對 `breakRange`(20)**:武士 leap slam / dive 起飛往上竄 15-20m,3D 距離就破 20 → 鎖定瞬間消失。→ **改成只算水平距離**。往上飛不管多高都不丟鎖(隻狼式);真的水平跑遠 >20m 才丟。既有 `TargetLockUtilityTests` 4 個(地面目標)不受影響,+2 新測(頭頂高空保持鎖定 / 水平遠丟鎖)。
- **`ThirdPersonCameraController.UpdateDuelCamera`(武士的隻狼決鬥鏡頭)拿 boss 真實 Y 算垂直 span/看向點/pitch**:boss 20m 高時 span 變成 ~24m,鏡頭猛拉遠 + pitch 打到天上,boss 在頭頂繞圈時 `centerYaw` 亂跳 → 鏡頭亂轉。修:
  - 新 `duelMaxBossHeightAbovePlayer`(4.5):框景用的 boss 高度封頂在「玩家 +4.5m」,起飛時鏡頭穩定微仰追 boss 的**水平位置**,落地恢復完整框景。鎖定準心照樣顯示 boss 真實位置。
  - 新 `duelOverheadSeparation`(3):boss 水平距離 < 3m(近乎頭頂)時 yaw 停止追它(方向雜訊會讓鏡頭轉圈),保持上一個穩定朝向。
- Play mode 反射驗證:boss 瞬移到 y=25(水平 0.7m)→ 仍鎖定;boss 水平 30m+高 25m → 丟鎖(正確);boss 20m 頭頂時鏡頭 pitch=0.7°(以前會打到 minPitch)。
- EditMode 181 tests 180 綠(同既有無關失敗)。場景 `leashRange` 已設 5、已存。

## 2026-08-29 (追加35) — 武士 leash 6m ＋ Enemy 移到 NW 牆角

使用者:「1.武士警備距離改為6m 2.enemy移動到另一邊的圍牆上方(90度夾角處)→(AskUserQuestion)西北角 NW (-15, 7, 15)」。

- **`BossStateMachine.leashRange` 5 → 6**(code default ＋ 場景)。稍微大於 `alertRange`(5),邊界更不會抖。
- **`Enemy`(076/Arisa,棲息在圍牆上,有 `PerchRejector`)從北牆正中偏東 (5, 7.08, 15.5) 移到西北角 (-15.5, 7.08, 15.5)** —— 北牆 × 西牆的 90° 夾角上方,rot y=135 面向本地中央。距武士 17.4m、距玩家 spawn 21.1m(`EnemyAI.detectionRange` 8,所以平時在角落待命,玩家靠近西北才啟動)。截圖確認站在牆角交會處。純場景座標調整,沒動 `EnemyAI`/`PerchRejector` 邏輯。
- EditMode 181 tests 180 綠(同既有無關失敗)。場景已存。

## 2026-08-29 (追加36) — 修:武士飛空技能失效(leash 太緊)＋ 警備距離釐清

使用者:「武士的飛空技能失效 只會待在原地做出像是重複動作」＋「列出武士/enemy/屁孩王的警備距離」。

**根因**:追加34/35 把 `leashRange`(從駐點量的「放棄追擊」半徑)縮到 5→6m。但一場正常戰鬥裡玩家閃避拉開很容易就離駐點 >6m → `TryLeashReset` 觸發 → `ChangeState(Dormant)` 把 leap slam / dive 的起跳**打斷**、掉出戰鬥 → 走回駐點 → 玩家又靠近 → 再交戰 → 再被打斷……在駐點附近來回 = 「重複動作待在原地」。**「警備距離」(guard/alert range) 應該是 tuning 的 `alertRange`,不是 leash** —— 之前幾次把它當 leash 改是誤解。

- **`leashRange` 6 → 30**(武士 ＋ 屁孩王,code default ＋ 場景)。30m 從駐點 (0,11) 涵蓋整個本地(駐點到最遠牆角 ~29m),只有玩家真的**穿過車庫洞口往道路/學校**才會 disengage。
- **`TryLeashReset` 加特殊狀態排除**:`Vanishing`/`DiveAttack`/`LeapSlamWindup`/`LeapSlam`/`Ultimate*`/`DodgeCounter`/`Breakdance`/`PostureBroken`/`HitReaction` 期間永不 leash-abort(鏡射 `TryEnterTooCloseKick` 的排除集)—— 起跳中的攻擊一定讓它打完。
- **`Wushi_Tuning.alertRange` 5 → 6**(這才是使用者要的「武士警備距離 6m」)。
- **`Enemy` 加 `EnemyPerchPad`**(隱形 3.5×3.5 collider,牆角上方 top y=6.9):CharacterController + 重力站在 1m 寬牆頂會慢慢滑下去(play 8000 幀後掉到地面),給它一塊穩定平台。Enemy 移到 (-15, 7.5, 15) 站在 pad 上。
- EditMode 181 tests 180 綠(同既有無關失敗)。**飛空實機驗證待使用者**(MCP 下 play mode 凍幀,無法跑真實戰鬥時序)。

### 警備距離一覽（追加36 當下）
| 角色 | 警備距離（進入交戰的偵測半徑）| 放棄追擊 leash |
| --- | --- | --- |
| 武士 | **6m**（`Wushi_Tuning.alertRange`）| 30m（`BossStateMachine.leashRange`）|
| 屁孩王 | 5m（`PW2_Tuning.alertRange`）| 30m |
| Enemy / 076 | 8m（`EnemyAI.detectionRange`）| 無（`EnemyAI` 沒有 leash）|

## 2026-08-29 (追加37) — 修:武士返回走路姿勢異常(改瞬移) ＋ Enemy/屁孩王 出門口即脫離

使用者:「1.有時武士丟失目標後返回時走路姿勢不正常 2.設定 enemy 屁孩 無論當下警備距離和追擊距離是否滿足,只要玩家出了門口(他們出不了牆) 視為丟失玩家目標,回歸原位(武士不受此限,屬於 boss;enemy 屬於普通怪物;屁孩王屬於精怪)」。

### 1. 武士返回走路姿勢異常 → 改「瞬移回駐點」
**根因**:`UpdateDormant()` 之前會讓 boss 在 Dormant 狀態下**走回駐點**。但 Dormant 的 animator state 是靜態 pose、不是 locomotion blend → 一邊移動一邊放靜止動畫 = 腳底打滑/姿勢怪。
- **`UpdateDormant()` 移除走回駐點的位移/轉向碼**(連同 `returnHomeSpeedFraction` 欄位),現在 Dormant 恆為真正靜止。
- **新 `SnapToHome()`**:leash 觸發時 `_controller.enabled = false` → `transform.SetPositionAndRotation(_homePosition, _homeRotation)` → 還原 —— 直接瞬移。觸發當下玩家離駐點 30m+ 或已出本地,看不到 boss,瞬移無破綻。
- Awake 多存 `_homeRotation`(以前只存 `_homePosition`)。

### 2. Enemy(普通怪物)/ 屁孩王(精怪)出門口即脫離
新增純靜態盒判定 `Live2DAction.AI.ArenaBounds.IsOutside(pos, centerXZ, halfExtent)`(軸對齊方形,忽略 Y;鏡射 `TargetLockUtility` 的純函式風格)。
- **`EnemyAI`** 新增 `returnHomeWhenPlayerLeavesArena`(預設 false)/`arenaCenterXZ`(原點)/`arenaHalfExtent`(15.5)。`Update` 在 `target == null` 檢查後:旗標開 ＋ 玩家在本地方形外 → 清 MoveInput/AttackPressed/速度、`CurrentState = Idle`、瞬移回 Awake 時的 spawn(`_controller` 停用/還原),`return`。無視 `detectionRange`。
- **`BossStateMachine`** 新增 `leashOnLeaveArena`(預設 false)/`arenaCenterXZ`/`arenaHalfExtent`。`TryLeashReset` 內:旗標開 ＋ 玩家出本地 → **免 grace 立即** disengage(仍受特殊狀態排除保護,不會砍飛行中的 leap)。
- **場景設定**:`屁孩王` `leashOnLeaveArena = true`;`Enemy` `returnHomeWhenPlayerLeavesArena = true`;**`武士` 兩個旗標都維持 false**(boss 不受門口限制,只吃 30m 距離 leash)。
- 新測 `ArenaBoundsTests`(6 個:中心/邊緣內/X 外/Z 外穿門口/Vector3 忽略高度/非零中心=學校)。EditMode **187 tests 186 綠**(唯一失敗 `CharacterAttackAnimationLinkTests...FallsBackToAttack3` 為既有無關項)。
- 武士飛行時序、Enemy/屁孩王 實際脫離行為**待使用者實機驗證**(MCP 下 play mode 凍幀)。

### 警備距離一覽（追加37 當下,無變動）
| 角色 | 分類 | 警備距離（偵測半徑）| 脫離條件 |
| --- | --- | --- | --- |
| 武士 | boss | 6m（`Wushi_Tuning.alertRange`）| 玩家離駐點 > 30m（`leashRange`）|
| 屁孩王 | 精怪 | 5m（`PW2_Tuning.alertRange`）| 玩家離駐點 > 30m **或玩家出本地門口**（`leashOnLeaveArena`）|
| Enemy / 076 | 普通怪物 | 8m（`EnemyAI.detectionRange`）| **玩家出本地門口**（`returnHomeWhenPlayerLeavesArena`）|

## 2026-08-29 (追加38) — Enemy/屁孩王 移動速度 ×1.5 ＋ 腳步同步(治腳滑) ＋ Enemy 解除門口限制

使用者:「1. enemy 和屁孩王移動速度太慢了 *1.5 倍,腳步要配合 2. enemy 解除城牆內限制」。

### 1. 移動速度 ×1.5
- **Enemy**:`EnemyAI.moveSpeed` 2 → **3**(場景值。空中 `aerialHorizontalSpeed` 2.4 不動 —— 它綁定「1.2× 玩家飛行速度」的設計註解,而且使用者講的是走路慢)。
- **屁孩王**:`PW2_Tuning` `walkSpeed` 2→**3**、`runSpeed` 4.5→**6.75**、`unsteadyWalkSpeed` 1.2→**1.8**。(存檔時 SO 順帶把先前用預設值、沒寫進資產的欄位補序列化出來 —— 值全等 `BossTuning.cs` 的 code default,`alertRange`/`reviveDelaySeconds` 等未變。)

### 2. 腳步同步(foot-sync,治腳滑)
兩邊的 Locomotion blend tree 頂端 clip(NewRun / PW2_Running)是照「特定速度」做的動作;把角色實際位移速度推過那個值,參數只會 clamp、跑步 clip 維持原本步頻 → 身體滑過去(腳滑)。新增「超速時等比例加快 Animator 播放速率」:
- **`CharacterAnimatorLink`**(Enemy 用):新 `syncStrideToGroundSpeed`(opt-in,場景在 Enemy 上勾)＋ `maxStrideRate`(2.5)。著地且非飛行時 `animator.speed = clamp(實際速度 / maxAnimatorSpeed, 1, maxStrideRate)`。攻擊/僵直/待機時速度為 0 → 比值自然回 1,攻擊 clip 不會被加速。**玩家不受影響**(旗標預設關,且玩家 moveSpeed 剛好等於 blend tree 頂端)。
- **`BossStateMachine`**(屁孩王用):新 `locomotionAuthoredSpeed`(0=停用,屁孩王設 **2** ＝ PW2_Running 原本的速度)。**只在 `Approach` 狀態**套用 `animator.speed = clamp(實際速度 / locomotionAuthoredSpeed, 0.6, 2.5)`;`OnExitState` 本來就會把 `animator.speed` 歸 1,所以離開 Approach 立刻復原(也不會蓋掉 PostureBroken 的 speed=0)。武士留 0(不同 rig / controller,且不在這次需求內)。
- 純函式 `CharacterAnimatorLink.ComputeStrideRate` / `BossStateMachine.ComputeStrideRate` 各 4 個 EditMode 測試。

### 3. Enemy 解除門口限制
`Enemy` 的 `returnHomeWhenPlayerLeavesArena` 追加37 剛設 true → 這次改回 **false**。使用者定調:只有 **屁孩王(精怪)** 受「玩家出本地門口即脫離」限制,**Enemy(普通怪物)不受限**,可依 `detectionRange`(8m)一路追出門口。`ArenaBounds` 及 `EnemyAI` 的欄位保留(旗標關著,隨時可再開)。

- EditMode **195 tests 194 綠**(唯一失敗 `CharacterAttackAnimationLinkTests...FallsBackToAttack3` 為既有無關項)。
- **腳步同步的實際觀感、×1.5 後的追擊/命中手感待使用者實機**(MCP 下 play mode 凍幀)。

### 移動/脫離一覽（追加38 當下）
| 角色 | 分類 | 地面移動速度 | 出本地門口即脫離？ |
| --- | --- | --- | --- |
| 武士 | boss | walk 5.5 / run 7.5（`Wushi_Tuning`）| ❌（只吃 30m 距離 leash）|
| 屁孩王 | 精怪 | walk 3 / run 6.75（`PW2_Tuning`）| ✅ `leashOnLeaveArena` |
| Enemy / 076 | 普通怪物 | 3（`EnemyAI.moveSpeed`）| ❌（追加38 改回不受限）|

## 2026-08-29 (追加39) — 被限制角色改「走到門口觀望」，不再碰邊界就瞬移回家

使用者:「被限制城牆內移動的角色,一旦碰到邊界時不要直接傳回原本位置,而是判斷是否有目標在警備範圍內(追擊距離同理),一直在門口觀望著目標」。

追加37/38 的行為:玩家一出本地方形,被限制的角色(屁孩王)立刻 `SnapToHome` + Dormant。太突兀。改成:

### 共用純函式（`ArenaBounds`）
- `ClassifyTarget(aiPos, targetPos, center, halfExtent, watchRange)` → `Inside` / `OutsideNearby`(出了牆但離 AI 還在 watchRange 內)/ `OutsideFar`(出了牆又走遠)。watchRange 從 **AI 當下位置**量(牠站在門口),不是駐點。
- `ClampInside(worldPos, center, halfExtent)`:把世界座標拉回方形(Y 不動),soft-confine CharacterController。
- +5 個 EditMode 測試(共 `ArenaBoundsTests` 11 個)。

### 屁孩王（`BossStateMachine`）
- 欄位 `leashOnLeaveArena` → **`confineToArena`**(`[FormerlySerializedAs]` 自動搬移場景值);新增 `gateWatchRange`(0=用 `tuning.AlertRange`,屁孩王設 **10**)。
- 新狀態 **`BossState.GateWatch`**:走到邊界(沿著往玩家的方向,`ApplyMotion` 的 arena clamp 讓牠停在牆邊),然後面向玩家站定,**完全不攻擊**(`TryLeashReset` 在此狀態每幀回 true → 整條下層 cascade Ultimate/Vanish/LeapSlam/TooCloseKick/DodgeCounter 全跳過)。
- `TryLeashReset` 重寫:`OutsideNearby` → 進/維持 GateWatch;`OutsideFar` → `SnapToHome` + Dormant;`Inside` 且在 GateWatch → 回 Idle 讓正常 cascade 重新交戰。特殊狀態排除(飛行中的 leap/dive/ultimate)移到最前面,永不被 GateWatch 打斷。距離 leash(駐點 30m)邏輯保留在後段。
- `ApplyMotion` 尾端:`confineToArena` 時每幀 `transform.position = ArenaBounds.ClampInside(...)`。
- `OnEnterState` / foot-sync(追加38)/ `RunCurrentState` 都補上 GateWatch(當 Locomotion blend tree 處理,走路時腳步同步)。

### Enemy（`EnemyAI`，目前場景關著,保持一致）
- `returnHomeWhenPlayerLeavesArena` → **`confineToArena`**(`[FormerlySerializedAs]`)。
- 玩家出本地:`flat.magnitude > detectionRange` 才瞬移回 spawn;否則照常追到邊界、面向玩家,`_playerOutsideArena` 時強制 `AttackPressed = false`(不隔牆揮擊)。`_controller.Move` 後 `ClampInside`。

### 場景
`屁孩王` `confineToArena=1` / `gateWatchRange=10`;`武士` + 兩個 `EnemyAI` 都 `confineToArena=0`。`GateWatch` 插在 enum 第 4 位(Approach 之後),其後 ordinal 各 +1 —— 沒有資產序列化 BossState 的 int,CrossFade 全走狀態名,無影響。

- EditMode **200 tests 199 綠**(唯一失敗 `CharacterAttackAnimationLinkTests...FallsBackToAttack3` 為既有無關項)。
- **GateWatch 實際觀感、走到門口的位置準不準、watchRange 10 是否合適、玩家回門內重新交戰,全待使用者實機**(MCP 下 play mode 凍幀)。

### 脫離規則一覽（追加39 當下）
| 角色 | 分類 | 出本地門口後 |
| --- | --- | --- |
| 武士 | boss | 不受限（只吃駐點 30m 距離 leash）|
| 屁孩王 | 精怪 | 走到門口 `GateWatch` 觀望;玩家離牠 >10m 才 `SnapToHome` 回駐點 |
| Enemy / 076 | 普通怪物 | `confineToArena` 關 → 一路追出門口都可以（旗標與邏輯保留，隨時可開）|

## 2026-08-29 (追加40) — 修:屁孩王還是一碰邊界就瞬移回原位（追加39 的 give-up 太早）

使用者:「現在屁孩王還是一碰到邊界就瞬間回到初始位置」。

**根因**:追加39 的 `ClassifyTarget` 從 **boss 當下位置** 量「玩家還在不在 watchRange 內」。但屁孩王駐點在遠角 (12,12)、門口在 (0,-15.5),相距 ~30m —— 玩家一出門口,boss 還在半路(甚至還在駐點附近),距離遠 > watchRange(10) → 判成 `OutsideFar` → 立刻 `SnapToHome`。等於沒修。

**修**:
- 移除 `ArenaBounds.ClassifyTarget` / `ArenaTargetStatus`(連同 3 個測試)—— 「該不該放棄」不是純幾何問題,取決於 boss 有沒有真的走到牆邊。
- `TryLeashReset`:玩家一出方形 → **無條件進 `GateWatch` 並開始走向門口**,絕不在這裡放棄。
- `UpdateGateWatch` 現在自己管 give-up:只有在 **(a) boss 已經抵達牆邊**(往玩家方向探 0.5m 已出界)**且 (b) 玩家離 boss > watchRange 持續 `gateWatchGiveUpSeconds`(1.5s)** 才 `SnapToHome` + Dormant。還在走去門口的路上永不放棄。
- 新欄位 `gateWatchGiveUpSeconds`(1.5,離開觀望範圍後的緩衝,一個「多看你一眼」的滯留)。
- `EnemyAI` 同樣的錯也修了(給 `confineToArena` 加 `atWall` 前置條件才 teleport home),雖然場景 Enemy 仍 `confineToArena=0`。

- EditMode **197 tests 196 綠**(唯一失敗 `CharacterAttackAnimationLinkTests...FallsBackToAttack3` 為既有無關項)。
- **實機驗證仍待使用者**:屁孩王從駐點走到門口 ~28m @ WalkSpeed 3 ≈ 9 秒(但正常情況牠是追在玩家後面、出門口時已在門邊,不會從駐點起步);走到門口後面向玩家站定不攻擊;玩家離門口 >10m 撐 1.5s 才回駐點;玩家回門內立刻回 Idle 重新交戰。

## 2026-08-29 (追加41) — 釐清:GateWatch 改動只影響屁孩王,武士零變動 ＋ 撤掉 EnemyAI 的死碼

使用者:「屁孩王是屁孩王、boss 是 boss,彼此獨立,不該互相干擾(但仍然有傷害機制)。前面修正回覆裡提到 boss 讓我覺得奇怪,我要做的改動應該只跟屁孩王有關係」。

**釐清**:屁孩王和武士**共用同一個 `BossStateMachine` 腳本**,所以追加39/40 的 commit / 回覆文字會出現「boss」字眼 —— 但每一行新增邏輯都掛在 `confineToArena` 這個**逐 instance 的序列化 bool** 後面:
- 屁孩王:`confineToArena = true`、`gateWatchRange = 10`、`locomotionAuthoredSpeed = 2`(追加38 腳步同步)
- 武士:`confineToArena = false`、`gateWatchRange = 0`、`locomotionAuthoredSpeed = 0`

驗證武士行為**逐位元相同**:
- `TryLeashReset` 的 `if (confineToArena) { ... }` 整塊武士直接跳過,落到原本的「離駐點 > `leashRange`(仍是 30) → SnapToHome」邏輯,和追加39 之前完全一致(唯一的 reordering —— 特殊狀態排除移到 `leashRange<=0` 早退之前 —— 對 leashRange=30 的武士是 no-op)。
- `ApplyMotion` 的 arena clamp、`UpdateGateWatch`、foot-sync 全部 `confineToArena`/`locomotionAuthoredSpeed` 為假 → 武士不執行。
- `BossState.GateWatch` 插在 enum 第 4 位造成 ordinal 位移 —— 查過 `Wushi` AnimatorController **沒有任何 int-equality transition**(只有 `Phase` 0/1、`AttackID`,都不是 BossState),CrossFade 全走狀態名。零影響。
- **傷害機制不受影響**:武士↔屁孩王的 HP 傷害、架勢累積、擊飛全部照舊(`BossTeamMember` 兩隊不同 —— 武士='武士'、屁孩王='Boss' —— friendly-fire guard 放行;這次沒動任何 hitbox / Health / StancePoise 程式)。使用者確認要「HP + 架勢/擊飛都保留」。

**撤掉死碼**:追加37/39/40 給 `EnemyAI` 也加了一份平行的 arena-confine 邏輯(`confineToArena` 欄位 + clamp + watch),但場景 Enemy 是 `confineToArena=0`(追加38 使用者定調不受限),整段是 inert dead code。`git checkout` 把 `EnemyAI.cs` 還原成 commit 版,場景 EnemyAI 元件上殘留的 `confineToArena: 0` 等欄位重存後已清掉。arena-confine 現在**只存在於 `BossStateMachine`**,`ArenaBounds` 註解也改成「目前只有屁孩王」。

- EditMode **197 tests 196 綠**(唯一失敗 `CharacterAttackAnimationLinkTests...FallsBackToAttack3` 為既有無關項)。

## 2026-08-29 (追加42) — 屁孩王加兩招普攻：PW2_LeapSmash（跳躍重砸）＋ PW2_ChargeSlam（衝刺掌擊）

使用者:「新增這兩個動作到屁孩王身上」（`Meshy_AI_Man_in_Black_at_the_P_biped (1).zip` ＋ `.zip`，各一個 `Animation_<guid>_without_skin.fbx`）→（AskUserQuestion）「兩招都加進普通攻擊池 normalAttackPool」＋「給合理預設值,你之後實機調」。

- **匯入 ＋ retarget**：兩個 without_skin FBX 複製進 `PiHaiWangV2/Animations/`，改名 `PW2_LeapSmash.fbx`（3.07s）/`PW2_ChargeSlam.fbx`（2.03s），`animationType=Human`、`keepOriginalPositionY=1`（防沉地，AdvancingCuts 教訓）、motion 抽出不烘進 pose（＝所有已上線 boss 招式的做法）。retarget 到 `PiHaiWangV2Avatar` 成功（`humanMotion=true`）。
- **Animator**：`PiHaiWangV2.controller` 加兩個獨立 state（`PW2_LeapSmash` m_Speed 1.25、`PW2_ChargeSlam` m_Speed 1.2），比照現有 PW2 招式（無 transition，靠 `PlayState` CrossFade 進、`EndAttack` 回 Locomotion）。
- **`BossAttackDefinition`（ScriptableObject，rule 7）**：新建 `PW2_Attack_LeapSmash.asset` / `PW2_Attack_ChargeSlam.asset`，加入場景 `屁孩王` 的 `normalAttackPool`（3→5 招）。
  - **LeapSmash**：跳躍過頭雙手重砸。hit window t=0.50–0.60（雙手，Y 2.6→1.4 掃過受擊框，`clip.SampleAnimation` 實測）。30 HP / 24 poise / 擊飛 / knockback 7 / cooldown 6 / `isMajorAttack`（多休息）。`maxDistance` 1.3（保守）。
  - **ChargeSlam**：前衝雙掌推擊。hit window t=0.38–0.50（雙手前推）。26 HP / 20 poise / knockback 10（推開）/ 不擊飛 / cooldown 5。`maxDistance` 1.15。
- **已知限制（designNotes 有記）**：兩個 clip 都內建大量向前位移（root 走 ~4–6m），為了配合「站定出招」模型把位移抽掉了 → 只接了「位移前」的近距離 window，`maxDistance` 很保守。要恢復完整跳躍/衝刺距離＝`useRootMotion=1`（window ~0.1–0.7）＋ 修「LeapSlam/Breakdance 打斷攻擊時清 `_currentAttack`」（CHANGELOG 標記的 root-motion 陷阱），跟 `Wushi_DoubleCombo`/`Wushi_ChargeCut` 一樣留待後續，不盲改。
- 授權：`ASSET_LICENSES.md` 補上屁孩王 Meshy 條目（付費方案、可出貨，同 Cat）。
- EditMode **197 tests 196 綠**（唯一失敗 `CharacterAttackAnimationLinkTests...FallsBackToAttack3` 為既有無關項；無 boss FSM 單元測試，靠 play 驗證）。
- **待使用者實機**：兩招的命中幀、傷害/knockback 手感、選用權重（現 18/20，跟 HighKick 25 差不多）、是否要 useRootMotion 恢復位移。

## 2026-08-29 (追加43) — 屁孩王 LeapSmash/ChargeSlam 改 useRootMotion=1（把位移加回來）

使用者:「這兩招應該都是有位移的 目前沒看到」。追加42 比照現有 boss 招式用 `useRootMotion=0`，位移被丟掉了。

- **新 `BossAnimatorRootMotionRelay`**（掛在 `屁孩王/Visual`，Animator 所在的物件）：`Awake` 開 `applyRootMotion=true` ＋ 一個**空的 `OnAnimatorMove()`**。Unity 只有在 Animator 物件有 `applyRootMotion` 或 `OnAnimatorMove` 時才會**計算** avatar root motion；空的 `OnAnimatorMove` 讓 `Animator.deltaPosition` 有值、又不會把位移自動套到這個子物件上 —— 由 `BossStateMachine.ApplyMotion` 自己讀 `deltaPosition` 餵給根部 CharacterController。Play 實測 `deltaPosition` 確實有值、attack 期間 boss 會位移。
- **`BossStateMachine.ApplyMotion` root motion 判斷加 `CurrentState == BossState.Attack`**：修 CHANGELOG 標記過的「root-motion 陷阱」—— 以前只看 `_currentAttack != null`，LeapSlam/Breakdance 打斷可中斷攻擊後 `_currentAttack` 還留著一兩幀 → 會亂讀 stale root motion。順便把 `deltaPosition.y` 歸零（垂直永遠交給重力/貼地，不讓 clip 的跳躍分量把 boss 拋上天）。
- **兩個 asset 改 `useRootMotion=1`**、`rootMotion` window `0.05–0.90`、`minDistance` 1 / `maxDistance` 3.5。
- **實測到的位移量偏小**（retarget 後 Humanoid root motion 被正規化到 avatar 比例）：ChargeSlam 全 window ~1.3m、LeapSlam 水平接近 0（它主要是垂直跳，Y 被歸零）。**位移「有了」但幅度、命中距離、LeapSmash 要不要保留一點縱跳感，全待使用者實機定**。
- EditMode **197 tests 196 綠**。
- **既有無關**：Console 有 `Parameter 'Hash 1367192179' does not exist`（= `BreakdanceTrigger`）—— `PiHaiWangV2.controller` 少這個參數，屁孩王每次 Breakdance（每 15 秒戰鬥）就 warn 一次。無害（Breakdance state 靠 CrossFade 名稱進，不靠 trigger），非這次改動造成，未修。

## 2026-08-29 (追加44) — 屁孩王 LeapSmash 改成正式飛空劈砍 ＋ 兩隻 boss 脫離後跑步歸位

使用者:「1. 屁孩王舉雙手置頂那招 讓他飛高一點，攻擊前先鎖定玩家，觸發攻擊時往玩家方向垂直發起攻擊 2. 武士與屁孩王在脫離追擊範圍後，要使用跑步歸位」。

### 1. PW2_LeapSmash → 屁孩王的 leapSlamAttack
之前是 normalAttackPool 一招 + useRootMotion 硬塞位移（效果不好）。改成接上**現有的 `LeapSlamWindup` + `LeapSlam` 狀態機**（武士在用的那套）:
- `屁孩王.leapSlamAttack = PW2_Attack_LeapSmash`，從 `normalAttackPool` 移除（剩 4 招：PunchCombo1/HighKick/GuardKick/ChargeSlam）。
- **攻擊前先鎖定玩家**：`LeapSlamWindup` 1 秒前搖，全程 `FaceTarget` 面向玩家。
- **飛高**：`PW2_Tuning.leapSlamExtraHeight` 30 → **8**（30 是武士 4x 尺寸用的，屁孩王只 ~2.5m 高，8 已經是身高 3 倍、明顯騰空）。這是腳本高度弧（`ComputeLeapSlamExtraHeight`），不是 clip 位移。
- **往玩家方向垂直發起**：`UpdateLeapSlam` 上升期間 homing 追玩家實時位置到 `leapSlamTrackUntilNormalized`(0.45)，之後鎖定、垂直落下。
- `PW2_Attack_LeapSmash.asset`：`useRootMotion` 關回 false（LeapSlam 自己驅動位置）、`maxDistance` 999、選用權重 0（改由 `leapSlamTriggerSeconds` 20 秒定時觸發，不進 pool 抽選）、`interruptible=false` + `superArmor`（committed 空中招）。hit window 移到 **0.42–0.60**（下降+落地，高度弧 fallEnd 在 nt 0.53），用**雙手 hitbox**（屁孩王沒有 LandingAOE hitbox，這招本來就是雙手過頭砸）。AnimatorState m_Speed 1.25 → 1.0（滯空可讀性）。

### 2. 脫離追擊 → 跑步歸位（`BossState.ReturnHome`）
追加37 把武士的歸位從「走回去」改成瞬移（因為 Dormant animator 是靜態 pose、走路腳滑）。使用者現在要跑步歸位:
- 新 `BossState.ReturnHome`（enum 第 5 位，Attack 後移一格；無 int 序列化依賴）。`UpdateReturnHome`:朝 `_homePosition` 以 `RunSpeed`(6.75/7.5) 移動、面向移動方向、播 Locomotion blend tree（`MovementSpeed = CurrentHorizontalSpeed` 驅動 Running clip）。到駐點 0.4m 內 → `SnapToHome`（精準 pose/朝向）→ Dormant。
- **中途玩家回到 `AlertRange` 內 → 立刻 `Alert` 重新交戰**。
- `TryLeashReset` 的距離 leash disengage、`UpdateGateWatch` 的 give-up：`SnapToHome + Dormant` → 改 `ChangeState(ReturnHome)`。`TryLeashReset` 開頭加 `CurrentState == ReturnHome → return true`（讓歸位跑完、擋下 cascade 不讓 LeapSlam/Breakdance 中途觸發）。
- `WriteAnimatorParameters`:ReturnHome 時 `CombatActive=false`（戰鬥暫停）＋ foot-sync（追加38）也套用到 ReturnHome。`WushiBossHudVisibility`:ReturnHome 時隱藏血條。
- 兩隻 boss 都吃這套（武士靠距離 leash、屁孩王靠距離 leash 或 GateWatch give-up）。`SnapToHome` 保留給最終到站的精準對位 ＋ LeapSlam 落地釘位。

- EditMode **197 tests 196 綠**。
- **待實機**：LeapSmash 飛 8 高會不會太高/太低、垂直落點準不準、hit window 對不對；ReturnHome 跑步姿勢（foot-sync 有沒有到位）、被打斷後會不會卡在半路（目前打斷→HitReaction→Idle→可能 Dormant 卡原地，非致命）。MCP play mode 凍幀跑不了真實驗證。

## 2026-08-29 (追加45) — 武士：脫戰更早放棄 ＋ LeapSlam 不再跨場瞬移

使用者:「武士判定距離怪怪的 我明明沒接近他 突然他就像我衝過來的 脫離戰鬥後他也沒跑回原位」。追加44 的 `ReturnHome` 機制本身沒問題，但觸發條件太寬，武士在本地內幾乎永不放棄。

### 根因
- `leashRange` 30（從駐點 (0,0.6,11) 量）＝整個本地都在半徑內，玩家在本地怎麼跑都觸發不到距離 leash。
- `UpdateApproach` **本身沒有任何脫戰判斷**：唯一的距離脫戰在 `UpdateIdle`（`> AlertRange*1.5` = 9m），但玩家一直移動時 boss 追不上、進不了 Idle → 永遠卡在 Approach 追擊，`ReturnHome` 沒機會觸發。同時 breakdance/leapSlam 計時器照跑（Approach 算「戰鬥中」）。
- `TryEnterLeapSlam` 的 `leapCap = max(AlertRange*3, 15)` ≈ **18m**：`CommitLeapSlamLanding` 是瞬移到玩家頭上，18m 內都能發 → 玩家自認離很遠時被瞬移劈 =「突然衝過來」。

### 修法
- **場景**：武士 instance `leashRange` 30 → **14**（純 Inspector；`TryLeashReset` 每幀從 Approach/Attack/Idle 都跑，玩家離駐點 >14m 就 `DisengageAndReturnHome` → 跑步歸位）。屁孩王那顆維持 30（`confineToArena=1` 本來就有牆）。
- **`BossStateMachine.UpdateApproach`**：開頭加上跟 `UpdateIdle` 一模一樣的 `distance > tuning.AlertRange * 1.5f → DisengageAndReturnHome` 判斷（Idle 本來就只把 <AlertRange*1.5 的缺口丟進 Approach，Approach 也照這規則收尾，一致）。這是「玩家在駐點附近繞圈風箏、leash 看不到」的補網。
- **`BossStateMachine.TryEnterLeapSlam`**：`leapCap` 收到 `max(AlertRange*1.5, 9f)` ≈ **9m** —— 短距離撲擊，不再是跨半個本地的瞬移。屁孩王同碼受惠（原 15m → 9m）。

- 編譯無錯（既有 CS0414 warning 不變）。EditMode 測試因使用者當下在 Play Mode 沒跑成；`BossStrideRateTests` 是純靜態函式、與本次改動無關。
- **待使用者實機**：`leashRange` 14 會不會太短（打到一半玩家後退一點就脫戰）、Approach 的 9m 脫戰是否太敏感、LeapSlam 9m 撲擊距離手感。三個都是手調值，實機再校。

## 2026-08-29 (追加46) — 屁孩王攻擊：位移招從遠處發、前搖不被排程技打斷、出招小幅追身、HighKick 下架

使用者:「屁孩王感覺有些攻擊打不倒玩家 或著直接打斷當前動作 只看到起手姿勢 確定每一招都有用上嗎 那種有連續位移的可以不用綁死近戰攻擊距離」。

### 診斷
- **ChargeSlam（唯一有位移的普通招）幾乎不發**：`minDistance: 1` > `AttackReadinessDistance()`（＝池裡最小 maxDistance ＝ PunchCombo1 的 0.98），boss 追到 0.98m 停下抽招時每次被 `0.98 < 1.0` 濾掉；maxDistance 3.5 也沒用，因為 boss 只在站定的 Idle 抽招，距離一 >0.98 就切 Approach 用走的。
- **「只看到起手」**：池裡 4 招全 `interruptible: 1` → `AttackFinishedCommittableWindow()` 只要 interruptible 就直接放行 → Breakdance(15s)/LeapSlam(20s)/Ultimate(能量~15s滿) 可在普通攻擊前搖任一幀切掉。
- **打不到**：`UpdateAttack` 一進攻擊就 `_horizontalVelocity=0` 站定只轉向，1.5x 大隻 boss、~1m reach，玩家在 0.3–0.7s 前搖裡退一步就出 reach。
- **HighKick clip**：自己的 designNotes 標了「real peak 揮到頭高 ~2.2m，飛過站著玩家的 hurtbox」「may need retiming/re-choreography」——只有 0.8–1.0 收腳那段能中，最容易「起手沒中」。

### 改動（`BossStateMachine.cs`）
- **`PickAttack()` → `PickAttackFiltered(Func<BossAttackDefinition,bool>)`**：共用本體。Idle 走 `extraFilter=null`（原行為）；Approach 走 `a => a.UseRootMotion`。
- **`UpdateApproach`**：決策 tick 時，若仍在接近（`distance > readinessDistance`）且不在 rest window，roll 一次「只含 useRootMotion 招」的 `PickAttackFiltered`，中了就直接 `BeginAttack`（不先走進近戰距離）。maxDistance/角度/冷卻照 `PickAttackFiltered` 內部把關。
- **`AttackFinishedCommittableWindow()`**：interruptible 攻擊不再從第 0 幀就可被搶——**排程技**（Breakdance/LeapSlam/Ultimate/Vanish，都經由此函式）要等 `AnimatorNormalizedTime() >= FirstStrikeNormalized(attack)`（＝第一個 hit window 起點）才准 pre-empt。玩家造成的 stagger（`TryEnterPostureBroken` / 載具 `RequestBeHitFlyUp`）走別條路，不受影響、照樣即時打斷。新增純函式 `FirstStrikeNormalized`（無 hit window 時 fallback 0.5）。
- **`UpdateAttack`**：非 useRootMotion 招、還在 tracking 期間（`normalized < trackingDropNormalizedTime`）、且 `HorizontalDistance() > MaxDistance*0.85` 時，給 `WalkSpeed*0.5` 朝玩家的低速位移（reel-in，不是全追）。其餘照舊 `_horizontalVelocity=0` 站定。useRootMotion 招不碰（自己驅動位移）。

### 資產
- `PW2_Attack_ChargeSlam`：`minDistance` 1→**0**（它自己 root motion 衝上去，近距也 OK；現在 Idle 也選得到）。
- `PW2_Attack_HighKick`：`selectionWeightPhase1/2` 25/30→**0/0**（下架，clip 幾何上打不到站立玩家；asset 仍接著、可還原）。屁孩王普通池實際剩 3 招：PunchCombo1 / GuardKick / ChargeSlam。
- `PW2_Attack_PunchCombo1`：`maxConsecutiveUses` 1→**2**（池變薄後，GuardKick+ChargeSlam 都在冷卻時不至於沒招可出；對齊該 asset 自己的註解「spec 允許 Punch_Combo 連兩次」）。

- 編譯無錯（既有 CS0414 warning 不變）。EditMode **197 / 196 綠**（唯一失敗 `CharacterAttackAnimationLinkTests...FallsBackToAttack3` 為既有無關項）。
- **待使用者實機**：gap-closer 會不會太常從遠處衝、前搖保護後排程技是否偶爾還是切到揮擊尾、追身 `WalkSpeed*0.5` 幅度、`MaxDistance*0.85` 門檻、3 招池夠不夠（可能要再補一支能打站立玩家的近戰 clip 換掉 HighKick）。

## 2026-08-29 (追加47) — 屁孩王：攻擊慾望拉高（縮短出招間隔）＋ 招式輪流施放偏置

使用者:「1. 每招 盡量做到輪流施放 不要有技能被孤立 2. 屁孩王攻擊慾望太低 攻擊間隔太長」。

### 1. 招式輪流（新 `BossTuning` 兩欄 + `PickAttack` 偏置）
- `BossTuning`：`attackRotationRecoverySeconds`(6) / `attackRotationRecentFactor`(0.15)。純加欄位 + 兩個 accessor，舊 asset 缺 key 由 field initializer 補預設；PW2_Tuning / Wushi_Tuning 都明寫進 asset。
- `BossStateMachine`：新 `Dictionary<BossAttackDefinition,float> _lastUsedTime`（`BeginAttack` 記 `Time.time`）。`PickAttackFiltered` 算完 base weight 後乘上 `Lerp(recentFactor, 1, clamp01(sinceUsed / recoverySeconds))` —— 剛用過的招權重掉到 15%，6 秒線性回滿。軟性 LRU，不是硬輪替鎖；跟每招自己的 `cooldownSeconds` 硬門檻分開。`recoverySeconds=0` 可停用（回純加權隨機）。
- 效果：PunchCombo1（w50）不再長期洗掉 GuardKick / ChargeSlam；三招會自然循環。

### 2. 攻擊慾望（純 `PW2_Tuning.asset`，武士不動）
- `globalRestPhase1` 1.6–2.4 → **0.5–0.9**；`globalRestPhase2` 1.1–1.8 → **0.3–0.55**（每次攻擊後的強制休息 = 主要的「間隔太長」來源）。
- `majorAttackExtraRest` 2–3 → **0.8–1.5**（LeapSmash 是 `isMajorAttack`）。
- `decisionIntervalPhase1` 0.5–0.9 → **0.22–0.4**；Phase2 0.3–0.5 → **0.14–0.28**（Idle 抽招節奏）。
- 攻擊間空檔從 ~2.3–3.5s → ~0.75–1.35s。仍比武士（~0.1–0.3s）保守。
- `PW2_Attack_GuardKick` cd 5.5→**3.5**、`PW2_Attack_ChargeSlam` cd 5→**4**（縮短休息後，避免另外兩招都在冷卻時 PunchCombo1 唱獨角戲／出現空手站定）。`PunchCombo1.maxConsecutiveUses` 維持 2（追加46）當保險閥，輪流偏置會讓它其實很少連兩次。

- 編譯無錯。EditMode **197 / 196 綠**（既有無關 `...FallsBackToAttack3`）。
- **待使用者實機**：新節奏會不會太兇（globalRest 再往上一點就緩下來）、輪流偏置 6s/0.15 的循環感、GuardKick/ChargeSlam 縮 cd 後是否過度洗版。全部值在 `PW2_Tuning` + 三個 `PW2_Attack_*` asset。

## 2026-08-29 (追加48) — 屁孩王 LeapSmash：真的飛過去（不再瞬移）＋ 固定 50% 傷害

使用者:「飛向天空那朝沒有鎖定玩家方向飛過去攻擊 傷害固定百分比50」。

### 飛過去（不瞬移）
- **`BossTuning` 新 `leapSlamTeleportToLanding`（bool, 預設 true = 武士行為）**。`CommitLeapSlamLanding` 把「起跳瞬間 blink 到玩家腳邊」包進 `if (tuning.LeapSlamTeleportToLanding)`；false 時只鎖朝向 + 落地 Y，不移動位置。
- **`PW2_Tuning.leapSlamTeleportToLanding: 0`**、`Wushi_Tuning: 1`（明寫，維持原樣）。
- 效果：屁孩王前搖結束後**留在原地**，`UpdateLeapSlam` 既有的空中 homing（追玩家實時位置到 `leapSlamTrackUntilNormalized` 0.45，`leapSlamMaxTrackSpeed` 30 封頂）負責整段水平位移 → 從起跳點飛到玩家、垂直落下。變成真正的撲擊弧線而非瞬移。leapCap（`TryEnterLeapSlam`，屁孩王 = max(alertRange 5 ×1.5, 9) = 9m）夠短，homing 來得及在鎖定前貼上。

### 固定 50% 傷害
- **`PW2_Attack_LeapSmash`**：`healthDamageIsPercentOfTargetMax` 0→**1**、`baseHealthDamage` 30→**50**（= 玩家 max HP 的 50%，`BossHitbox` 命中時 `MaxHealth * 50/100` 結算）。
- **hitWindows 2→1**（只留 LeftHand，0.42–0.60）：原本 LeftHand + RightHand 兩個獨立 `BossHitbox` 各自結算，站在正下方會吃兩下 = 100% 秒殺。收成一下 = 固定 50%。`basePoiseDamage` 24 也從「兩下 48」變回「一下 24」。
- 武士的 LeapSlam 不受影響。

- 編譯無錯。EditMode **197 / 196 綠**（既有無關 `...FallsBackToAttack3`）。
- **待使用者實機**：飛行弧線觀感（起跳→前衝→落下的節奏）、homing 追不追得上、9m 內起手是否夠近、50% 傷害 + launch + 24 poise 會不會太重（`leapSlamTriggerSeconds` 20s 一次）。

## 2026-08-29 (追加49) — 屁孩王 LeapSmash：專屬 pounce 路徑（真的飛到玩家）＋ 降低飛行高度

使用者:「飛空那招可以不用飛那麼高 比站著高一倍就好 然後目前沒有鎖定玩家位置飛過去」。追加48 拿掉瞬移後改靠共用 homing，但那段 homing 的速度是用 `_stateTimer / normalizedTime` 反推「總時長」再算的，crossfade blend 期間 `normalizedTime` 是髒/混合值 → 早期反推出的速度趨近 0，等 normalized 正常時已經沒剩多少追蹤時間 → 看起來沒飛過去。

### 專屬 pounce 路徑
- **`BossTuning` 新 `leapSlamFlightSeconds`（預設 1.3，只在 `leapSlamTeleportToLanding=0` 時用）**。
- **`UpdateLeapSlam` 開頭**：`if (!tuning.LeapSlamTeleportToLanding) { UpdateLeapSlamPounce(normalized); return; }`。武士（teleport=1）走原本共用路徑，**一個 byte 都沒動**。
- **新 `UpdateLeapSlamPounce`**（自足、全部用 `_stateTimer` 牆鐘驅動，不碰 `normalizedTime` 那條脆弱路徑）：
  - 水平：每幀重新瞄準玩家實時 xz（短 `leapSlamLandingOffset` 2m），速度 = 剩餘距離 / 剩餘飛行時間，`leapSlamMaxTrackSpeed` 30 封頂 → 保證在 `leapSlamFlightSeconds` 內貼到玩家，然後鎖定直落。
  - 垂直：`_stateTimer` 拋物線，`leapSlamFlightSeconds` 中點衝到 `leapSlamExtraHeight`、結束回地面，一樣用逐幀高度差驅動 `_verticalVelocity`（Move-consistent）。
  - hit window 仍讀 clip 的 normalized 窗（落地時 ~1.3s，normalized 早就正常了，可靠）；落地後 pin 落地 Y + `_leapSlamHolding` 讓 `ApplyMotion` 跳過 Move（同共用路徑的 landed 分支）。

### 高度
- `PW2_Tuning.leapSlamExtraHeight` 8 → **2.5**（屁孩王站著 ~2.5m，弧頂 +2.5 ≈ 站著的兩倍高＝「比站著高一倍」）。
- `leapSlamFlightSeconds: 1.3` 對齊 clip 自己的 slam 窗起點（hitWindow 0.42 × 3.07s ≈ 1.29s）→ 落地正好接上揮擊判定。

- 編譯無錯。EditMode **197 / 196 綠**（既有無關 `...FallsBackToAttack3`）。
- **待使用者實機**：飛行弧線觀感、`leapSlamFlightSeconds` 1.3 落地時機對不對、高度 2.5 夠不夠低、髒 normalizedTime 期間 hit window 有沒有提早/延後開。

## 2026-08-29 (追加50) — 屁孩王 LeapSmash 命中修正（時間驅動判定窗）＋ 攻擊間隔再縮短

使用者:「飛天那朝仍然沒有攻擊到玩家 可能是動畫問題 另外攻擊間隔本身再簡短」。

### LeapSmash「沒攻擊到」= crossfade 髒 normalizedTime
追加49 的 pounce 位移改吃 `_stateTimer`，但 **hit window 跟 exit 還在讀 clip 的 `normalizedTime`**。crossfade blend 的頭幾幀 `AnimatorNormalizedTime()` 回傳 OUTGOING clip 的殘值：
- 若殘值 `>= 0.6`（hit window endNormalized）→ `_leapSlamHitWindowsDone` 第一幀就 latch 成 true → **整招 hitbox 永遠不開**。
- `IsAttackAnimationFinished` 在殘值 `>= 0.98` 時（`_stateTimer > 0.1s` 後）→ **整個 LeapSlam state 起跳後 ~0.1s 就 abort 回 Idle**。

**修**（`UpdateLeapSlamPounce`，只影響屁孩王，武士 teleport 路徑不動）：
- hit window 改 **`_stateTimer` 相對落地**：`t ∈ [leapSlamFlightSeconds - 0.1, +0.35]` 開左手 hitbox（`part`/`damageMultiplier`/50% 傷害仍讀 asset 的 `HitWindows[0]`）。落地即命中，跟物理位置同步。
- exit 改 **`_stateTimer` 相對**：`t >= leapSlamFlightSeconds + max(0.3, recoverySeconds)` → Idle（`AnimatorHasFinished` 只當 `t > flight+0.5` 後的後備）。
- **落地後補 `_globalRestUntil`**：50% 核彈招走 LeapSlam state 直接回 Idle、跳過 EndAttack 的休息邏輯 → 補上「一般休息 + major-attack 額外休息」，不讓它立刻接下一招。

### 攻擊間隔再縮短（純 `PW2_Tuning`）
- `globalRestPhase1` 0.5–0.9 → **0.25–0.5**；`globalRestPhase2` 0.3–0.55 → **0.15–0.3**。
- `majorAttackExtraRest` 0.8–1.5 → **0.4–0.9**。
- `decisionIntervalPhase1` 0.22–0.4 → **0.12–0.25**；Phase2 0.14–0.28 → **0.08–0.16**。
- `attackReadinessBuffer` 0.1–0.2 → **0.06–0.12**。
- 攻擊間空檔 ~0.75–1.35s → **~0.35–0.7s**（已接近武士 ~0.1–0.3s）。

- 編譯無錯。EditMode **197 / 196 綠**（既有無關 `...FallsBackToAttack3`）。
- **待使用者實機**：LeapSmash 這次有沒有打到、`leapSlamFlightSeconds` 1.3 落地時機、間隔 0.35–0.7s 會不會太壓迫（`globalRest` 往上調就緩）。

## 2026-08-29 (追加51) — 屁孩王 LeapSmash 落點修正（原本刻意落在玩家 2m 外）

使用者:「飛天這招很奇怪 落點永遠沒有在玩家身上」。

### 根因
`leapSlamLandingOffset` = **2**。這欄位是武士時代加的：落在玩家精確 xz 會讓 boss 掉進玩家的 CharacterController 膠囊、卡在玩家頭高（「浮空」bug）→ 所以刻意落在玩家往 boss 方向退 2m 的點。武士有 3m 半徑的 LandingAOE 還罩得到，但**屁孩王沒有 LandingAOE**、用左手 hitbox 做過頭劈 → 落在 2m 外那一拳根本搆不到玩家。

### 修
- **`PW2_Tuning.leapSlamLandingOffset` 2 → 0.4**（落在玩家身上；CharacterController 對撞會自然把 boss 停在 ~0.57m 外，過頭劈的手 hitbox 搆得到）。
- **`UpdateLeapSlamPounce` 的落地 Y raycast 改 `RaycastAll` + 跳過玩家**：瞄準點離玩家這麼近時，普通 `Raycast` 會打到玩家膠囊頂端、把 boss 釘在頭高（就是 offset=2 當初在閃的「浮空」）。現在取「不是 boss 自己、也不是玩家」的最近命中 → 拿到真正的地面。
- hit window 從 `[flight-0.1, flight+0.35]` 放寬到 **`[flight-0.15, flight+0.45]`**。

- 編譯無錯。EditMode **197 / 196 綠**（既有無關 `...FallsBackToAttack3`）。
- **待使用者實機**：這次落點在不在玩家身上、過頭劈有沒有命中、`leapSlamLandingOffset` 0.4 會不會偶爾把 boss 擠進玩家。武士 LeapSlam（teleport 路徑）不受影響。

## 2026-08-29 (追加52) — 屁孩王 LeapSmash 觸發變頻（20→10s）＋ Breakdance 讓路（15→24s）

使用者:「感覺屁孩王很少放這招」。

- **`PW2_Tuning.leapSlamTriggerSeconds` 20 → 10**：屁孩王沒接 `leapSlamEnergy`，走的是純戰鬥累計計時器。20s 太長，加上計時器在**每次脫戰 / 死亡復活**都歸零（`DisengageAndReturnHome` line ~1250、revive reset line ~2282），一場戰鬥常常湊不到一次。減半。
- **`PW2_Tuning.breakdanceTriggerSeconds` 15 → 24**：cascade 裡 `TryEnterBreakdance` 排在 `TryEnterLeapSlam` 前面，Breakdance 15s < LeapSlam 20s → 每次都是 Breakdance 先搶到「排程技」的空檔。而且 `PW2_Attack_Breakdance` 的 hit window 是 `measured: 0`（placeholder 猜的、designNotes 自己標未驗證），本來就是低價值 flourish。往後推讓 LeapSlam 先。
- 現在 LeapSlam 10s 先 arm、先觸發；Breakdance 24s 殿後。

- 純 asset 改動，無 code。編譯無錯。EditMode **197 / 196 綠**。
- **待使用者實機**：10s 一次會不會太頻繁（往上調 leapSlamTriggerSeconds）。若還是很少 → 下一個嫌疑是 **Ultimate（RisingFlyingKick，能量 ~15s 滿，cascade 更前面，300 傷害幾乎秒殺）** 在吃 LeapSlam 的空檔 + 提前結束戰鬥；那需要另外討論 ultimate 的定位。

## 2026-08-29 (追加53) — 屁孩王 Ultimate 飛踢：真的離地（原本是貼地平移）

使用者:「跳下來瞬間就把我即飛了（LeapSmash 讚）… 但這個可以做到秒即飛 為甚麼飛踢步行呢」。

### 根因
`UpdateUltimateAttack` 的撲擊只有水平：`_horizontalVelocity = transform.forward * UltimateLeapSpeed(10)`，**貼地、沒有垂直分量**。RisingFlyingKick clip `useRootMotion=0`，所以身體在原地播踢擊動作、根部貼地滑 → 看起來像快走 / 滑步，不是「飛」踢。

### 修
- **`BossTuning` 新 `ultimateLeapJumpSpeed`（預設 7）**。
- **`OnEnterState(BossState.UltimateAttack)`**：進入撲擊的第一幀，`if (_controller.isGrounded && jumpSpeed > 0) _verticalVelocity = jumpSpeed`。`ApplyMotion` 對 `UltimateAttack` 有正常重力（-20）→ 自然把這個上拋弧線帶回地面、正好落在 strike window。7 ≈ 弧頂 ~1.2m、滯空 ~0.7s。0 = 舊的貼地滑。
- 純加值，不改弧線邏輯（用現成重力，任何地形都會正確落地）。PW2/Wushi tuning 都明寫 7（武士其實沒接 ultimate，只是補齊欄位）。

- 編譯無錯。EditMode **197 / 196 綠**（既有無關 `...FallsBackToAttack3`）。
- **待使用者實機**：飛踢離地感夠不夠、`ultimateLeapJumpSpeed` 7 會不會太高讓腳踢空（往下調）、落地時機跟 strike window 對不對。`UltimateReposition` 的後退（速度 4、播前進 clip 的「月球漫步」）沒動 —— 那是 setup、不是踢擊本身，要的話另外處理。

## 2026-08-29 (追加54) — 屁孩王 Ultimate 飛踢：擊退不再延遲（仿 LeapSmash 的 _stateTimer 命中）

使用者:「飛踢碰到玩家時 動畫上玩家沒有馬上被擊退出去 能仿照 LeapSmash 的作法嗎」。

### 根因
`Rising_Flying_Kick` clip = 45f @ 30fps = **1.5s**。舊 `UpdateUltimateAttack`：撲擊跑 `transform.forward * UltimateLeapSpeed(10)` 整個 normalized 前搖窗（`< hitWindows[0].start` 0.6 → 0.9s）＝ **~9m 位移**，從 ~5m 觸發距離衝過去 → **暴衝 3–4m 過頭**。腳部 hitbox 才在 normalized 0.6 打開（boss 已經飛過玩家），命中只能靠腳「收回」時 `SweepCheck` 掃回玩家身上結算 → 慢、跟視覺踢擊脫節（正是 KnockbackReceiver.cs 2026-08-25 註解裡那個「延遲 不銜接」老症狀，當時只修了 receiver 端，沒修命中時機）。

### 修（仿 `UpdateLeapSlamPounce`，純 code）
- 撲擊改成**衝到玩家就停**：`transform.forward * UltimateLeapSpeed` 直到 `HorizontalDistance() <= 1.3` 或 `_stateTimer >= 0.75s`（time cap），不再跑滿固定時間暴衝過頭。方向仍鎖 `transform.forward`（spec「起跳後不追蹤」，側移可閃）。
- **命中窗改 `_stateTimer` 相對「觸地/停下」**：`t ∈ [contact-0.06, contact+0.35]` 打開 `HitWindows[0]` 的 hitbox（part/傷害仍讀 asset）。boss 停在玩家身上 + hitbox 開啟當幀就重疊 → `OnTriggerEnter` 立刻結算 → 擊退跟視覺同步（LeapSmash 的即飛就是這個機制）。
- exit 改吃真實 clip 長度 `t >= 1.5s`（+ `AnimatorHasFinished` 後備），不用會被 crossfade 髒 normalizedTime 提早 abort 的 `IsAttackAnimationFinished`。
- 新欄位 `_ultimateContactTime` / `_ultimateHitDone` / const `UltimateLungeTimeCap 0.75`，在 `OnEnterState(UltimateAttack)` 重置。asset 不動（normalized 窗值變成不影響時機、只留 part/damageMultiplier）。

- 編譯無錯。EditMode **197 / 196 綠**（既有無關 `...FallsBackToAttack3`）。
- **待使用者實機**：擊退這次有沒有即時、暴衝有沒有收掉、1.3m 停下 + 上一版加的跳躍弧會不會讓 boss 半空停住再掉、側移閃避判定。

## 2026-08-29 (追加55) — 貓咪也能開車：F 鍵進出 ＋ 模型塞進駕駛座（可見）

使用者:「接下來來讓貓咪也可以使用車輛 F功能 以及模型塞進車裡」。

`VehicleEntrySystem`（車上的 F 鍵）原本寫死綁 `Player`。改成**「當前操控角色」感知**：

### `VehicleEntrySystem.cs`（重寫）
- 保留全部舊欄位（`player` / `playerMovement` / `playerCombat` / `playerRenderersToHide` / `playerCamera` / `driverSeatAnchor` / `vehicleController` / `vehicleCamera` / `enterRange` / `exitLocalOffset`）→ 場景既有序列化值不動、`possession` 留空時行為跟原本一模一樣。
- 新增：`possession`（`CameraPossessionSwitcher`，判斷 F 用哪隻角色）、`cat`、`catControlToDisable[]`、`catCamera`、`catDriverSeatAnchor`、`catRenderersToHide[]`（空 = 貓可見）。
- F 進車：`switcher.Current` 決定用 player 還是 cat；停用該角色的控制 consumer（player: movement+combat；cat: movement/PlayerCombat/CatChargeAttack/CatPounce/CatAerialJudgment）＋ `CharacterController` ＋ 停在座位錨點 ＋ 切到 VehicleCamera。**貓的 renderer 不隱藏**（player 照舊隱藏）。`_drivingCat` 記住是誰進來的，出車還原對的那隻。
- `CatProceduralWalk` / `CatAttackPose` **不停用**（留著讓腿/pose 自己 ease 回中性靜止姿，停用反而會凍在半步）。

### `CameraPossessionSwitcher.cs`
- 新 `vehicleEntry` 欄位；`IsDriving` 時 C 鍵忽略（比照「守望者視角時忽略 C」），先按 F 下車。

### 新 `VehicleCatWiring.cs`（Editor，可重跑）
- `Tools/Live2DAction/Wire Cat Into Vehicle` ＋ 從 `CatCharacterSetup.Apply()` 結尾呼叫（貓每次重建都要重接）。
- 找場景的 `VehicleEntrySystem` / `CameraPossessionSwitcher` / `Cat` / `CatCamera`，雙向接線；在車根建 `CatDriverSeatAnchor` 子物件（car-local `(0, 0.55, -0.1)`，比 player 的 `(0, 0.75, -0.1)` 低一點，起點值待實機微調）。
- 選單已重跑，場景已存：`VehicleEntrySystem` 貓欄位全接、`switcher.vehicleEntry` 已接、`CatDriverSeatAnchor` 在 Buggy 的 `m_AddedGameObjects` 裡。

**進貓車流程**：C 切成貓 → 靠近車 → F 進車（貓坐在駕駛座、看得到）→ F 下車 → C 切回 player。

- 編譯無錯。EditMode **197 / 196 綠**（既有無關 `...FallsBackToAttack3`）。
- **待使用者實機**：`CatDriverSeatAnchor` 座位高度/前後（貓 scale 0.45、只有綁定姿勢、會 ease 成中性四足站姿，塞在座位上大概是「貓站在駕駛座」的樣子）、下車位置、開車中攝影機。

## 2026-08-29 (追加56) — 開車中也能 C 切換角色（GTA 式：駕駛留在車上）

使用者:「PLAYER和CAT在駕駛車輛時沒辦法互相切換視角嗎」→ 選 GTA 式（駕駛留車上、切去控另一隻）。追加55 是把 C 擋掉，這次改成能切。

### 新狀態模型（`VehicleEntrySystem` 重寫）
- 唯一存的旗標：`SeatedCharacter`（None / Player / Cat，誰被 parent 在座位）。
- 「正在開」= derived：`SeatedCharacter` 等於 `CameraPossessionSwitcher.Current`。
- `LateUpdate()` 每幀對帳：`youDrive` 時車引擎（`vehicleController.enabled`）＋ VehicleCamera 開、兩隻角色相機關；`!youDrive`（你切去控另一隻）時車熄火、VehicleCamera 關（切換器已開了另一隻的相機）。座位上那隻不管你控誰，控制組件 + CharacterController 永遠 forced off。
- `[DefaultExecutionOrder(-50)]` → VES 的 Update/LateUpdate 跑在攝影機控制器(0)、切換器(150)前面，所以對帳能覆蓋切換器在 keypress 幀設錯的狀態、且在相機自己的 LateUpdate 前就關掉它。

### 按鍵
- **C**：永遠可切 Player↔Cat（不再擋）。切離駕駛 → 車自動熄火停好、駕駛留在座位（貓看得到）。切回駕駛 → 引擎重開、繼續開。
- **F**：站在車旁 → 上車開；正在開（你是駕駛）→ 下車；座位被「另一隻」佔著 → 無作用（先 C 切回那隻）。

### 其他
- `CameraPossessionSwitcher.Apply()`：切到「被 VES 停在座位的角色」時，不開它自己的第三人稱相機、不開它的控制（VES 管 VehicleCamera + parked passenger）。移除追加55 的「開車中忽略 C」。
- `VehicleEntrySystem` 加 `viewDirector` 欄位：守望者(T)視角期間 LateUpdate 對帳讓位（不跟 director 搶相機），`VehicleCatWiring` 一併接線。
- 選單已重跑，場景已存。

- 編譯無錯。EditMode **197 / 196 綠**。
- **待使用者實機**：C 切離/切回駕駛的相機切換順不順、座位那隻靜止姿、開車中按 T 守望者再回來。

## 2026-08-30 (追加57) — 車輛雙人座 ＋ 兩隻角色都在車上可見 ＋ 貓駕駛仰角

使用者:「1.貓咪駕駛時可能要把模型上仰望 不然看不到臉 2.PLAYER駕駛時不再隱藏人物 3.車輛改為雙人座，車身後方攤平的區塊可以將人物放置在上方」。

### 1. 貓駕駛仰角
- `CatDriverSeatAnchor` 加 **-50° X 旋轉**（四足貓往後仰、chase cam 從後方看得到臉）。`Mount()` 對齊錨點姿勢，所以錨點轉多少貓就轉多少。`VehicleCatWiring` 的 `CatSeatLocalEuler` 常數，實機拖錨點微調。

### 2. PLAYER 不再隱藏
- `VehicleEntrySystem.playerRenderersToHide` 清空（`VehicleCatWiring` 每次重跑都清）。
- `DriverSeatAnchor`（player）降到 footwell `(0, 0.1, 0.1)`，讓可見的 ~1.8m humanoid 不會站穿車頂。`VehicleCatWiring` 現在也接管這個錨點（可重跑）。

### 3. 雙人座（`VehicleEntrySystem` 重構）
- 狀態改成 `PlayerSeat` / `CatSeat` ∈ {None, Driver, Passenger}。「正在開」= 你操控的角色持有 Driver 座。
- **F**：不在車上 → 駕駛座空就進駕駛座、被佔就上**後方平台當乘客**、都滿就無作用；在車上（駕駛或乘客）→ 下車。
- **C**：照舊切控制。控駕駛 → 車能開；控乘客或地面上的另一隻 → 車熄火停好，兩隻都留在原位（貓／player 都看得到）。
- 想換人開 → 兩隻都 F 下車，再 F 進去。
- 相機：駕駛看 VehicleCamera；乘客看自己的第三人稱相機（看著自己坐在後平台）；`CameraPossessionSwitcher.Apply()` 改用 `DriverOccupant` / `PlayerSeat` / `CatSeat` 判斷（原 `SeatedCharacter` enum 改名 `Occupant` + 新 `Seat` enum）。
- 4 個座位錨點都是 Buggy 的子物件，由 `VehicleCatWiring` 建/定位（car-local 起點值：player 駕駛 `(0,0.1,0.1)`、貓駕駛 `(0,0.5,-0.1)` -50°、player 乘客 `(0,0.55,-1.25)`、貓乘客 `(0,0.7,-1.25)` -50°）。後平台約 car-local z -1.65..-1.05、頂面 y ~0.57（`MainBodyCollider` 後段、`CabinCollider` 後面）。

- 編譯無錯。EditMode **197 / 196 綠**。
- **待使用者實機**：4 個座位錨點的位置/角度（貓仰角夠不夠看到臉、player 塞在座位的高低、後平台乘客會不會浮空/穿模）、乘客視角觀感、C 在「兩隻都在車上」時切換順暢度。

## 2026-08-30 (追加58) — 車輛 4 個座位錨點分位置微調

使用者:「PLAYER和貓當作主駕駛和後座時位置不一樣，要微調不同位置下的座標，不然看起來很奇怪」。

追加57 已經是 4 個獨立錨點，只是起點值粗糙。用 edit-mode 定位截圖對著真實幾何（`CabinCollider` 中心 y 0.95 / 頂 1.375、`MainBodyCollider` 頂 y 0.57 = 後平台面）逐一調：

| 錨點 | car-local pos | rot | 說明 |
|---|---|---|---|
| `DriverSeatAnchor`（player 駕駛） | (0, 0.65, 0.15) | 0 | 站在開頂座艙裡，腳約在車底板、頭露出車頂（開放式 buggy 無實體車頂） |
| `CatDriverSeatAnchor`（貓 駕駛） | (0, 0.6, -0.1) | (-8, **180**, 0) | **面向後方 chase cam** + 微仰（四足貓面朝行進方向的話，後方追尾相機只看得到背，又沒有坐姿 pose） |
| `PlayerPassengerAnchor`（後平台） | (0, 1.15, -1.3) | 0 | 站在後平台上，腳約在平台面 |
| `CatPassengerAnchor`（後平台） | (0, 0.95, -1.3) | (-8, **180**, 0) | 後平台、面向 chase cam |

- `VehicleCatWiring` 的常數同步更新（重跑選單會套用這組值）。場景 4 個錨點已存。
- **player 的最終外觀仍待 Play 確認**（edit mode 下 Maya 綁定姿勢渲染不準；跑起來播 Idle 才是站姿）。貓沒有動畫，edit=runtime，截圖可信。
- 編譯無錯。EditMode **197 / 196 綠**。
- **待使用者實機**：4 個錨點在 Buggy 底下，Play 裡直接拖；貓面朝後（看得到臉）vs 面朝前（正常但看不到臉）哪個要，使用者定。

## 2026-08-30 (追加59) — 貓駕駛面朝前 ＋ player 駕駛裁掉下半身（收腿 + 藏翅膀）

使用者:「1.貓咪主駕駛時請讓貓臉朝前 2.PLAYER駕駛時請裁減到他下半身 不然會看到他的腳在地上」。

### 1. 貓臉朝前
- `CatDriverSeatAnchor` / `CatPassengerAnchor` 拿掉追加58 的 yaw 180，改 **面向行進方向** + 微仰 `euler (-6, 0, 0)`。（代價：後方 chase cam 看不到臉 —— 使用者這次接受這個取捨。）

### 2. player 裁下半身
player 的身體是**一張 skinned mesh**，`renderer.enabled` 沒辦法只藏一半。兩段處理（都只在坐上車時，下車還原）：
- **收腿**：`VehicleEntrySystem` 新 `playerCollapseBones`（兩根 UpperLeg 骨）+ `playerCollapseBoneScale`(0.02)。`Mount` 時把骨頭 localScale 縮到 ~0 → 小腿/腳掌塌進骨盆；`Dismount` 還原到 Mount 當下存的原始 scale。`HoldSeated` 每幀重申（Mecanim 不碰 scale，保險）。`OnDisable` 也還原。
- **藏翅膀**：`polySurface2197`/`2631`（`Player/Visual/.../WingsAnchor/Wings` 下）會垂到車底 → 進 `playerRenderersToHide`（`Mount` 關、`Dismount` 開）。`VehicleCatWiring` 用 `Wings` 物件名找 renderer、用 Animator `GetBoneTransform(HumanBodyBones.Left/RightUpperLeg)` 找骨頭，都自動接。
- 背上的刀展示（`Blade`/`pCube*`）在頭頂上方，沒動（它是 player 的裝備）。

### 座位錨點（edit-mode 截圖對幾何調過）
| 錨點 | car-local | rot |
|---|---|---|
| `DriverSeatAnchor` | (0, 0.62, 0.12) | 0 |
| `CatDriverSeatAnchor` | (0, 0.72, -0.05) | (-6, 0, 0) |
| `PlayerPassengerAnchor` | (0, 0.55, -1.3) | 0 |
| `CatPassengerAnchor` | (0, 0.72, -1.3) | (-6, 0, 0) |

- `VehicleCatWiring` 常數同步、選單已重跑、場景已存（Player/Cat 已還原到出生點、骨頭 scale 1、翅膀開）。
- 編譯無錯。EditMode **197 / 196 綠**。
- **待使用者實機**：收腿後 player 坐姿（Play 才是 Idle 站姿基礎，edit 綁定姿勢不準）、貓朝前的位置、翅膀藏了會不會怪。骨頭縮放法若有 mesh 爆裂再把 `playerCollapseBoneScale` 調大一點（0.05）。

## 2026-08-30 (追加60) — player 駕駛時定住 ＋ 貓駕駛大仰角 ＋ 貓後座朝後

使用者:「1.PLAYER在主駕駛時必須靜止狀態 2.貓咪主駕駛時面向前方且上仰望至窗戶可看見的角度 貓咪後座時則朝向反方向」。

- **player 靜止**：`VehicleEntrySystem` 新 `playerAnimatorToFreeze` 欄位（接 `Player/Visual` 的 Animator）。`Mount`(player) 時 `animator.enabled = false`（Idle 凍結）；`Dismount` / `OnDisable` 還原。SkinnedMeshRenderer 仍照骨頭 skinning，所以收腿還是有效。
- **貓駕駛仰角**：`CatDriverSeatAnchor` euler `(-6,0,0)` → **`(-40,0,0)`**（面向前方、臉大幅上仰，chase cam 從後方看得到臉）。截圖確認：貓「坐起來、臉朝前上方」的樣子。
- **貓後座朝後**：`CatPassengerAnchor` euler `(-6,0,0)` → **`(-8,180,0)`**（yaw 180，面向車尾／追尾相機方向）。當你操控貓乘客時，CatCamera 就看得到牠的臉。

- `VehicleCatWiring` 常數同步、選單已重跑、場景已存（Player/Cat 已還原、animator on、骨頭 scale 1）。
- 編譯無錯。EditMode **197 / 196 綠**。
- **既有無關**：截圖時發現貓模型胸口有個米色球體 artifact（`Cat/Visual/output_unwrapped` Meshy auto-rig 的東西，或模型自帶的球），一直都在、非這次改動造成，之後另處理。
- **待使用者實機**：player 凍結姿勢（Play 才是 Idle 基礎姿）、貓 -40° 仰角夠不夠/會不會太後仰、貓後座朝後的觀感。

## 2026-08-30 (追加61) — 第一人稱駕駛藏駕駛模型 ＋ 屁孩王更兇（切後搖、縮間隔）

使用者:「1.貓咪主駕駛且 V第一人稱時會看到貓咪的臉 2.屁孩王攻擊慾望不夠強，技能銜接不夠快，且攻擊後搖太長」。

### 1. V 第一人稱藏駕駛
`VehicleCameraController` 的 `firstPersonLocalOffset (0,1.15,0.15)` 是當初 player **被隱藏**時調的；追加57 起駕駛看得到，所以 cockpit view 直接對著貓/player 的臉。
- `VehicleCameraController` 加 `public bool IsFirstPerson`。
- `VehicleEntrySystem.LateUpdate`：`youDrive && IsFirstPerson` → 把**當前駕駛角色底下所有 renderer** 關掉（`SetAllRenderers`）；離開第一人稱 / 換人 / 下車 → 還原（還原時仍套用 seated 隱藏清單，翅膀維持關）。`_fpHiddenOccupant` 追蹤、只在狀態變化時 toggle（不是每幀）。

### 2. 屁孩王攻擊節奏
- **切後搖**（新機制）：`BossTuning.attackRecoveryTailCutNormalized`（預設 2 = 不切；武士明寫 2）。`UpdateAttack`：最後一個 hit window 關閉 + 這個 normalized 量之後、且不在 active window 內 → 直接 `EndAttack()` CrossFade 出去，跳過 clip 的收招 recovery 尾巴。`PW2_Tuning` 設 **0.15**。
- **縮間隔**（`PW2_Tuning`）：`globalRestPhase1` 0.25–0.5 → **0.1–0.25**、Phase2 0.15–0.3 → **0.06–0.15**、`majorAttackExtraRest` 0.4–0.9 → **0.2–0.5**、`decisionIntervalPhase1` 0.12–0.25 → **0.06–0.15**、Phase2 0.08–0.16 → **0.04–0.1**、`attackReadinessBuffer` 0.06–0.12 → **0.03–0.08**。已接近武士的兇度。

- 編譯無錯。EditMode **197 / 196 綠**。
- **待使用者實機**：第一人稱下有沒有殘留的 renderer（VFX ring 之類，一起關了應該沒事）；屁孩王切後搖 0.15 會不會太急（招式看起來被砍尾）、間隔會不會過猛（`globalRest` 往上一點就緩）。

## 2026-08-30 (追加62) — player 後座坐上綠色板子 ＋ 翅膀真的藏起來

使用者:「PLAYER坐後座時Y座標太低了 沒有坐在綠色板子上」。

- **後座 Y 提高**：`PlayerPassengerAnchor` car-local y 0.55→**0.9**（root world 1.75→2.1）、`CatPassengerAnchor` 0.72→**1.15**。後方綠色 deck 面板是視覺件，頂端約 world 2.0–2.2，比 `MainBodyCollider` 頂（world 1.77）高。截圖確認 player 現在坐在綠板上。
- **翅膀 bug 修**：追加59 把翅膀 renderer 加進 `playerRenderersToHide`，但 `Wings` 物件上有 `WingFlap` 每幀重新開啟 renderer → 沒藏成。改成 `VehicleEntrySystem` 新 `playerHideObjectsWhileSeated`（GameObject[]），`Mount` `SetActive(false)` 整個 `Wings` 物件（連 WingFlap 一起停）、`Dismount`/`OnDisable` 開回。`playerRenderersToHide` 清空。`VehicleCatWiring` 改接 `Wings` GameObject。

- `VehicleCatWiring` 常數同步、選單已重跑、場景已存（Player/Cat 已還原、animator on、Wings active、骨頭 scale 1）。
- 編譯無錯。EditMode **197 / 196 綠**。
- **待使用者實機**：後座 y 0.9/1.15 坐得剛不剛好（截圖看略高一點點，可再降 0.05）、背上的 `WolfsGravestone` 刀展示很大支很搶戲（使用者沒提，要的話也能一起藏）。

## 2026-08-30 (追加63) — 學校土地放上第一棟建築（Meshy「元培大學」佔位）

使用者:「把這個建築（`Meshy_AI_Yuanpei_University_Bu_0830053851_texture_fbx.zip`）放到第二座城市-學校的土地上」。

- **匯入**：`import_model_file` 解壓進 `Assets/_Project/Environment/Meshy/YuanpeiUniversityBuilding/…/Meshy_AI_Yuanpei_University_Bu_0830053851_texture.fbx`（單一 mesh、~2.57M 頂點、無 LOD、無骨架）。附帶的重複 `.zip` 已從 Assets 刪除。Meshy 付費方案輸出 → 商用 OK、非 DoNotShip。
- **材質**：FBX 匯入後只有白色預設材質。手建 URP/Lit `YuanpeiUniversityBuilding.mat`：`_BaseMap` = `_texture.png`、`_BumpMap` = `_texture_normal.png`（已改 texture type = NormalMap）、`_MetallicGlossMap` = `_texture_metallic.png`、smoothness 0.25。指到場景 renderer。
- **場景擺放**（`GreyboxTest`，root `YuanpeiUniversityBuilding`）：等比縮到水平footprint ≈ 20m（localScale ≈ 0.206，等於抵消 FBX 自帶的 ×100 root scale）→ 世界尺寸約 18.7(x) × 20.6(高) × 20(z)。旋轉 `(0,90,0)` 讓 Meshy 唯一有貼圖的正立面朝 **+Z（北）**，正對學校北牆的路口 / `VehicleRoad`。位置 `(0, 10.75, -99.8)`，mesh 最低點 world y = 0.5 剛好坐在學校地板上表面；z 範圍 [-110, -90]，距北牆路口約 10.5m 當前庭。footprint 在 x ∈ [-9.3, 9.3]、z ∈ [-110, -90]，都在 30×30 地板內。
- **碰撞**：mesh object 加非凸 `MeshCollider`；root + mesh object 設 static。
- 場景已存、compile 乾淨。
- **已知/待辦**：(1) 背面與兩側是 Meshy 單視角生成的空白面，只有正面能看。(2) 2.57M 頂點單 mesh 對 greybox 場景偏重，之後換正式資產或減面。(3) 建築目前不在任何 `學校` 父物件下（`學校`/`SchoolWall` 仍是各自的 scene root，追加31 待辦未做）。(4) 生成點 / 傳送點 / 室內仍未做。

### 追加63 續 — 建築轉向（正立面朝北路口）＋ 縮小 ＋ 加基座解決懸空

使用者:「建築物角度不對，正立面（有窗戶/玻璃中庭那面）要朝北正對路口，整棟要坐在地板上，尺寸縮小一點」。

- **轉向**：`YuanpeiUniversityBuilding` euler `(0,270,0)` → **`(0,90,0)`**。Meshy 模型的有貼圖正立面是 local −X，`(0,90,0)` 把它轉到 world **+Z（北）**，正對 `SchoolWall_NorthLeft/_NorthRight` 的路口 / `VehicleRoad`。玩家從本地穿洞進學校就正面看到大樓正門與玻璃中庭。
- **縮小**：等比 scale `0.206` → **`0.154`**（水平最長邊 20m → **15m**）。world 尺寸約 **14 × 15.4高 × 15**，占 30×30 基地約一半，正立面到北牆路口留約 12m 前庭。
- **懸空修正**：抽樣模型底面發現 Meshy 這棟**沒有完整平底** —— 只有正面/中庭那條往下到接近地面，後 2/3 的底面浮在離地約 3.5–5m。做法：(1) 整棟再下沉 1.6m（正面/中庭稍微沒入地面，看不出來）；(2) 新增 `YuanpeiUniversityBuilding_Base` —— 一個 15.4 × 3.9 × 16.4 的 Cube 基座（`Ground_StoneFloor` 材質 + 內建 BoxCollider），頂面 y≈2.4、底沉入地下，填滿浮空的縫。視覺上變成「大樓坐在一個矮石造基座/廣場平台上」，合理的校園配置。
- 位置：`(0, 6.44, −99.85)`，基座 `(0, 0.45, −100)`。合併範圍 x ∈ [−7, 7]、z ∈ [−107.5, −92.5]，都在基地內、離南牆約 2m。
- `YuanpeiUniversityBuilding` + `YuanpeiUniversityBuilding_Base` 都設 static。場景已存、compile 乾淨。
- **仍未解**：背面/兩側 Meshy 空白面；模型本身有輕微「往前傾」的造型（烘進 mesh，非擺放問題）；中庭圓柱體略微前突出基座邊緣。

### 追加63 續 2 — 移除石造基座 ＋ 建築再往右轉 90°

使用者:「不需要石造基座，並且讓現在的學校角度往右轉動 90 度」。

- **移除** `YuanpeiUniversityBuilding_Base`（追加63 續加的石造 Cube 基座）。
- **建築** euler `(0,90,0)` → **`(0,180,0)`**（繞 Y +90，俯視順時針＝往右）。有貼圖正立面 local −X 現在朝 **world +X（東）**。位置 `(0.15, 8.19, −100)`，最低頂點重新對齊地板頂 y=0.5。
- **懸空問題回來了**：拿掉基座後，模型沒有平底的老問題外露 —— 大約南半 / 西側的底面浮在離地約 4–6m，從基地內側低角度看得到縫。追加63 續的下沉 1.6m 也一併還原（現在只靠 bounding-box 最低點貼地）。**待使用者決定怎麼收**（下沉一半讓正門側稍微入土 / 只加一片薄地板 / 暫時不管 / 換正式資產）。
- 場景已存、compile 乾淨。

### 追加63 續 3 — 找到正確朝向：模型本來是「躺著」的

使用者澄清:「大門面向門口；『底盤』是指模型自帶的那片完全沒外觀的灰色地基，要踩在土地上」。

- **根因**：這個 Meshy FBX 的 local 座標軸不是 Y-up。之前所有版本（識別、追加63 系列）都當成 Y-up 直接擺，等於把整棟**橫躺**放 —— 灰色地基面朝側面、細節正立面朝上、那根「玻璃圓柱」其實是建築正面的**圓弧玻璃塔樓**橫躺著。難怪一直有懸空 + 傾斜感。
- **正確朝向**：`euler (0,180,0)` → **`(270, 90, 0)`**：模型 local −Z（那片大面積灰色地基 slab）→ world **down**、local −X（帷幕牆正立面 + 元培大樓招牌 + 入口雨庇）→ world **+Z（北，正對路口）**。抽樣底面：25/25 格最低點都在 y≈0.50–0.54 —— 地基整片平貼學校地板。
- scale `0.15`（footprint 最長邊 100→**15m**）。world 約 15(寬) × 13.6(高) × 14.6(深)。位置 `(0, 7.35, −99.86)`，z ∈ [−107.3, −92.7]，正立面到北牆路口留約 13m 前庭。非凸 `MeshCollider`、static。
- 現在 eye-level 看是一棟正常的科大教學樓：圓弧玻璃塔樓 + 帶招牌的入口 + 前庭綠化，正面朝路口。
- 場景已存、compile 乾淨。背/側仍是 Meshy 單視角空白面；2.57M 頂點無 LOD 不變。

## 2026-08-30 (追加64) — 學校再放兩棟圖書館（Meshy 佔位），組成校園中庭

使用者:「部署 `Meshy_AI_Modern_Glass_Library_...` 和 `Meshy_AI_Palm_Lined_Library_...` 這兩個」。

- **匯入**：`import_model_file` 解壓進 `Assets/_Project/Environment/Meshy/ModernGlassLibrary/…` 與 `…/PalmLinedLibrary/…`（各單一 mesh，ModernGlass ~1.62M 頂點、PalmLined ~3.37M；無骨架/動畫/LOD）。附帶重複 `.zip` 已從 Assets 刪。Meshy 付費輸出＝可 ship。
- **材質**：各手建 URP/Lit（`_BaseMap`/`_BumpMap`（normal 已改 type）/`_MetallicGlossMap`，smoothness 0.35 / 0.3）。
- **朝向**：兩個都跟元培大樓一樣**非 Y-up**。base rotation `euler (270,90,0)` 讓灰色地基 slab 朝下、貼地。ModernGlass 這樣正立面就朝 +Z（北）；PalmLined 正立面在 local −X，額外繞世界 Y +90 → `euler (270,180,0)`，正立面才朝北。
- **佈局（校園中庭）**：三棟都正面朝北路口。
  - `YuanpeiUniversityBuilding`：後方中央，scale 0.12、footprint 12×11.7、高 10.9，x[−6,6] z[−108.8,−97.2]（比追加63 續 3 再縮小、往後靠）。
  - `ModernGlassLibrary`：西側，scale 0.14、footprint 8×14、高 6（低矮弧形玻璃館），x[−14.5,−6.5] z[−97,−83]。
  - `PalmLinedLibrary`：東側，scale 0.12、footprint 12×9.7、高 8.1，x[2,14] z[−94.8,−85.2]。
  - 中央從路口（x≈0）留約 8.5m 走道通到元培大樓；三棟無重疊、都在圍牆內。
- 每棟非凸 `MeshCollider` + 全 static。場景已存、compile 乾淨。
- **已知**：三棟的背/側都是 Meshy 單視角空白面（中庭內側看得到）；模型自帶不少樹木/綠化；頂點量大無 LOD。佈局偏密，使用者要調位置/大小/朝向再說。

## 2026-08-30 (追加65) — 學校擴大到 60×60 ＋ 車道加長加寬

使用者:「(學校地基不夠大) 好 60×60，銜接兩邊城市的車道也要跟上」。

- **`SchoolAreaSetup.AreaSize` 30 → 60**。重跑選單 `Add School Area`：`學校` 現在 60×60，z ∈ [−145, −85]、x ∈ [−30, 30]、中心 (0, 0, −115)，上表面 y=0.5 不變。北緣仍貼齊 `VehicleRoad` 遠端。周界牆（teal）跨距自動跟著長；北面路口自動對齊道路，寬 8.6。南緣 z=−145 在 `BackgroundTerrain`（z ±150）內留 5m。
- **`VehicleWallOpeningSetup`**：`RoadOutwardLength` 65 → 70、`RoadWidthOverGap` 2.0 → 3.5。重跑選單 `Add Vehicle Wall Opening + Road`：`VehicleRoad` 現在 **7.4 寬 × 70 長**（z ∈ [−15, −85]），本地南牆缺口重測 3.92。道路讀出後 `SchoolAreaSetup` 再跑，接點無縫。
- **三棟樓重擺 + 放大**進 60×60 校園中庭（都正面朝北路口，`euler` 沿用非-Y-up 朝向）：
  - `YuanpeiUniversityBuilding`：後方中央，scale 0.22、footprint 22×21.4、高 20，x[−11,11] z[−138.7,−117.3]。
  - `ModernGlassLibrary`：西側，scale 0.20、footprint 11.3×20、高 8.6，x[−22.7,−11.3] z[−118,−98]。
  - `PalmLinedLibrary`：東側，scale 0.20、footprint 20×16.1、高 13.5，x[7,27] z[−116,−100]，`euler (270,180,0)`。
  - 三棟無重疊、都在牆內，中庭中軸從路口（z=−85）到元培大樓約 32m 深、寬約 18m。
- 非凸 `MeshCollider` + static 全保留。場景已存、compile 乾淨、Console 無錯。
- **待辦**：中庭偏空曠（之後補綠化/座椅/步道）；`arenaCenterXZ` 若要在學校放怪要設 (0, −115)；三棟背/側仍是 Meshy 空白面。

## 2026-08-30 (追加66) — 入場前平地（Meshy 校園廣場）＋ 校園物件改 yuanpei_ 前綴

使用者:「把 `Meshy_AI_Quiet_Campus_Plaza_...` 做為入場前平地，相關建築物全部以 `yuanpei_` 前綴命名，接下來我要調整位置」。

- **匯入** `Assets/_Project/Environment/Meshy/QuietCampusPlaza/…`（單一 mesh ~1.59M 頂點、無骨架/動畫/LOD）。附帶 `.zip` 已從 Assets 刪。手建 URP/Lit 材質。
- **`yuanpei_QuietCampusPlaza`**：非 Y-up（同其他 Meshy），`euler (270,90,0)` 讓廣場面平貼；scale 0.4 → world 40×40（含樹木/座椅/圓形中央區/步道/人形），base y=0.5。**粗擺**在學校北牆路口前 x[−20,20] z[−90,−50]（跨路口 + 沿 `VehicleRoad` 往北），非凸 MeshCollider + static。**位置待使用者自行微調**。
- **場景物件改名（yuanpei_ 前綴）**：`YuanpeiUniversityBuilding` → `yuanpei_MainBuilding`、`ModernGlassLibrary` → `yuanpei_ModernGlassLibrary`、`PalmLinedLibrary` → `yuanpei_PalmLinedLibrary`。（Asset 資料夾/材質名沒動，只改 scene GameObject。）
- 場景已存、compile 乾淨、Console 無錯。
- **注意**：廣場中央目前壓到一顆既有低模岩石（場景原本就有的環境件）；廣場南緣稍微越過學校北牆——都等使用者調位置時處理。

### 追加66 續 — 廣場放大成 60×60 當學校地基，三棟樓坐在上面

使用者:「這個廣場我想讓它變成 60×60 放在學校領地上當作地基，其他建築物承載之上」。

- **`yuanpei_QuietCampusPlaza`**：scale 0.4 → **0.6**，world **60×60**，對齊學校 lot（x[−30,30] z[−145,−85]、中心 (0,0,−115)）。它的可走平台面（mesh 內約 y2.5）整體下推 2m，讓平台面落在 y≈0.5 —— 跟原本 `學校` 灰板地面同高，牆/樓/其他東西的高度都不用動。次結構沉到 `學校` 板下面藏起來。
- `學校` 灰板保留在底下當碰撞地板 + 邊緣填補（廣場不規則邊之外會露出石地面，可接受）。
- **三棟樓重新落在廣場面上**：對每棟 footprint 往下 raycast 取平台高（`yuanpei_MainBuilding` / `yuanpei_PalmLinedLibrary` 落 y≈0.5、`yuanpei_ModernGlassLibrary` 壓到微高處 y≈1.0），base 對齊。
- 4 個物件 static 仍關著（追加66 為了使用者手調關的），位置/擺放待使用者微調。場景已存、Console 無錯。

### 追加66 續 2 — 清掉廣場的占位草木（mesh surgery）

使用者:「這個廣場有很多占位的草木，有辦法單獨清理掉嗎」。

- 這個 Meshy FBX 是**單一 mesh、單 submesh、貼圖幾乎純灰**（無綠色可分類，也沒有子物件/submesh 可單獨關）。只能做 mesh 手術。
- 寫了個一次性 edit-time 程序：世界座標高度門檻 + XZ 格點「植栽欄位」偵測，逐三角形篩掉樹/灌木/燈柱/雕像/人形，重建 mesh、remap 頂點，存成新 asset `Assets/_Project/Environment/Meshy/QuietCampusPlaza/QuietCampusPlaza_NoFoliage.mesh`，指到 `yuanpei_QuietCampusPlaza` 的 MeshFilter + MeshCollider。
- 結果：**2.87M tris → 578k tris（−80%）**、1.59M → 311k verts。留下鋪面 + 中央同心圓廣場紋路 + 低地面細節；門檻 y>0.85（deck≈0.6）以上全清。原始 FBX mesh 沒動，要還原把 MeshFilter 指回去即可。
- 三棟建築 mesh 各自也有烘進去的綠化（`yuanpei_MainBuilding` 一棵樹、`yuanpei_PalmLinedLibrary` 棕櫚…），那是各自 mesh 裡的，不在這次範圍；要清用同一招另跑。
- 場景已存、Console 無錯。

### 追加66 續 3 — 修：上一版清理過度把廣場地面也削掉了

使用者:「清理過度了，連地面都被破壞，有很缺口」。

- 續 2 的高度門檻 + 密度格法太粗暴，把鋪面本身也篩掉了 → 地面破洞。
- 做法改保守：先把 MeshFilter/MeshCollider **指回原始 FBX mesh**（沒動過），重新只做兩件事：
  1. 樹冠：`centroid.y > 1.9`（deck 最高才 ~1.1）整片砍。
  2. 樹幹：只在「樹冠密集格」裡、且**法線接近垂直**（trunk 壁面）、高度 0.55–1.9 的三角形才砍。
  - **鐵律：`centroid.y ≤ 1.25` 的三角形一律保留**（deck 面全在這之下）→ 鋪面 100% 完整，零破洞。
- 驗證：40×40 格點往下打，plaza 命中 371、`學校` 灰板命中 227（廣場鋪面本來就不是滿版 60×60，邊緣露灰板，那是模型形狀不是破損）、**真正落空 0**。
- 結果 mesh：2.87M → 955k tris。三棟樓重新 raycast 落回 deck（這次排除建築自身 collider）。場景已存、Console 無錯。

### 追加66 續 4 — 廣場回歸完全未清理狀態

使用者:「先回歸到完全未清理的狀態」。

- `yuanpei_QuietCampusPlaza` 的 MeshFilter + MeshCollider 指回原始 FBX mesh `mesh_node`（1.59M v / 2.87M tris，全部樹木/灌木/燈柱/座椅原封不動）。
- 三棟樓重新 raycast 落回廣場 deck（取 y<1.8 的命中避開樹冠）：MainBuilding base 0.66、ModernGlass 1.06、PalmLined 1.52。
- 清理過的 `QuietCampusPlaza_NoFoliage.mesh` asset 還留在 `Assets/_Project/Environment/Meshy/QuietCampusPlaza/`（目前無人引用，之後決定要不要重做清理再用或刪）。
- 場景已存、Console 無錯。

### 追加66 續 5 — 套用使用者 Play Mode 擺位 + 底部貼齊 + 存檔

使用者在 Play Mode 把三棟樓移到想要的位置（Play Mode Transform 不存檔），要求套回 Edit Mode + 調底部 + 存檔。

- Play Mode 中抓下 4 個 `yuanpei_` 物件的 P/Q/S（備份在 scratchpad）。
- 使用者的擺位：`yuanpei_MainBuilding` (18.1, −96.5) 轉朝 Y=0；`yuanpei_ModernGlassLibrary` (−8.5, −124.1) 轉朝 Y=0；`yuanpei_PalmLinedLibrary` (26.1, −111.8) Y=180；`yuanpei_QuietCampusPlaza` 使用者把 pos.y 降到 −5.7 **（正確的——讓廣場 deck 落在地面高度，一度誤判成失手拖到，實測 deck 的三角形法線朝下、downward raycast 要 `queriesHitBackfaces` 才打得到，才會之前量錯）**。
- Edit Mode 重套 P/Q/S，plaza pos.y 精調到 **−5.91**（deck median = 0.50，齊平 `學校` 灰板 / 圍牆底）。三棟樓 raycast 貼齊 deck（base y ≈ 0.45–0.50）。
- 場景已存、Console 無錯。
- **注意**：廣場 deck 面的 mesh 法線朝下，`Physics.Raycast` 預設打不到（會穿到底下 `學校` 灰板 y=0.5）——實際碰撞沒問題（MeshCollider 不看 winding），且灰板就在同高當底。

### 追加66 續 6 — 學校校園配置定案

使用者:「關於學校的建築物我的配置好了，幫我記住設定」。

- 使用者在 Editor 又微調過（`ModernGlassLibrary` 放大到 scale 0.5、`MainBuilding` 轉朝 Y=340、`PalmLined` 轉朝 Y=270、`QuietCampusPlaza` 微移到 (0.08,−5.7,−114.44)）。
- 三棟樓底部最後再 raycast 貼齊 deck（base y=0.50）。4 個 `yuanpei_` 物件 **static 重新開啟**（layout 鎖定）。
- 定案 transform 記錄在 `Docs`（本則）＋ AI memory `yuanpei-campus-building-layout.md`：
  - `yuanpei_QuietCampusPlaza`  pos (0.08, −5.70, −114.44)  euler (270,90,0)  scale 0.60
  - `yuanpei_MainBuilding`       pos (17.81, 9.63, −97.55)   euler (270,340,0) scale 0.20
  - `yuanpei_ModernGlassLibrary` pos (−5.01, 11.29, −125.34) euler (270,0,0)   scale 0.50
  - `yuanpei_PalmLinedLibrary`   pos (21.21, 7.21, −116.64)  euler (270,270,0) scale 0.20
- 場景已存、Console 無錯。往後不要自行搬動這幾棟。

## 2026-08-30 (追加67) — 車輛 Ctrl 飛行（原理參考 player，綁在車本身）

使用者:「回到車輛，car 幫我增加 ctrl 飛行功能，原理參考 player（功能綁訂車本身）」。

- **新檔**
  - `VehicleFlightData`（ScriptableObject，`Assets/_Project/Settings/Movement/Vehicle/VehicleFlightData.asset`）—— 爬升/下降/巡航速度、boost 倍率、偏航速度、俯仰/迴正平滑、起飛上衝、體力耗速、重進門檻。數值全在這（rule 7）。
  - `VehicleFlightState`（純 C# 類，非 MonoBehaviour）—— 飛行狀態機 + 速度解算，`Tick()` 回傳 `VehicleFlightOutput`。跟 `DodgeState` vs 玩家 dodge 同一種「抽出可單測邏輯」的切法。
  - `VehicleFlightController`（掛 Buggy，`[RequireComponent(Rigidbody)]`）—— 薄殼：直接讀 `Keyboard.current`（沿用 `VehicleController`/`VehicleEntrySystem` 慣例，不用 IInputCommand），把 `VehicleFlightState` 的輸出寫到 Rigidbody（`useGravity` 關 + 每 FixedUpdate 寫 `linearVelocity` + `MoveRotation` 迴正/偏航）。
  - `VehicleFlightSetup`（editor menu `Tools/Live2DAction/Add Vehicle Flight (Ctrl)`，re-runnable）—— 把 `VehicleFlightController` + `FlightEnergy` 子物件（`UltimateEnergy`，max 500 / +5 每 1s / idle 3s，同玩家 flightEnergy 設定）接上 Buggy，指到 data asset。
  - `VehicleFlightStateTests`（EditMode，16 個）。
- **改既有**
  - `VehicleController`：加 `public bool FlightModeActive`（飛行中 `FixedUpdate` 跳過輪子動力/煞車/轉向，只 `ApplyParkingBrake(0)` 清 stale torque）＋ `public bool AnyWheelGrounded`（飛行落地判定）。
- **原理照 player**：按住 Ctrl 起飛 + 爬升；放開 → 懸停（垂直速度 SmoothDamp 回 0，不是墜落）；飛行一旦啟動就持續，只有「輪子著地 且 沒按 Ctrl」或「體力耗盡」才結束；重進需 `resumeEnergyThreshold`（30）。
- **鍵位**（配合車）：Ctrl 起飛/爬升、**Space** 下降（地面時是手煞車）、**Shift** 加速巡航（沿用車 boost 鍵，額外耗體力）、**W/S** 沿車頭推進、**A/D** 偏航。車身隨垂直速度微俯仰、迴正 roll。只在「你正在開這台車」（`VehicleController.enabled`）時可飛；中途下車立即結束飛行。
- **測試**：EditMode `VehicleFlightStateTests` **16/16 綠**；全套 213 tests 212 綠（唯一失敗 `CharacterAttackAnimationLinkTests...FallsBackToAttack3` 為既有無關項）。編譯無錯（Console 兩個 `CubismRenderController` IndexOutOfRange 是既有 Live2D SDK 問題，與此無關）。
- **待使用者實機**（MCP Editor 沒 OS 焦點、Play Mode 會凍幀，無法自己跑真實飛行）：起飛手感、巡航/爬升速度、Space 下降、迴正快慢、`VehicleCameraController` 是水平跟隨相機、車爬升時鏡頭偏平（v1 沒動相機）。數值全在 `VehicleFlightData.asset`。

### 追加67 續 — 修：飛行沒有落地手段 ＋ 空中車身抖動

使用者實機:「1. 沒有落地的手段 2. 空中時車身會抖動」。

- **落地**（`VehicleFlightState.Tick` 加 `heightAboveGround` 參數）：落地條件從「輪子著地 且 沒按 Ctrl」放寬成「(輪子著地 **或** 離地 ≤ `landingClearance` 1.6m) 且 沒按 Ctrl」。實際操作：Ctrl 起飛 → 放開懸停 → **按住 Space 下降**，一進到離地 1.6m 內就自動落地、車身掉到輪子上；全程按 Ctrl 可中止落地重新爬升。`VehicleFlightController` 用「從車體 pivot 往下 raycast、忽略車自身 collider」算離地高。落地瞬間下墜速度夾到 `landingImpactSpeedCap`(4) 免得撞爛懸吊。
- **抖動**（`VehicleController.FlightModeActive` 的 setter 現在做事）：
  - 飛行時**停用 4 個 WheelCollider** —— enabled 的 WheelCollider 只要擦到任何幾何就會自己跑懸吊 raycast + 施加彈簧力，把底盤踢一下、跟硬寫的飛行速度打架，就是抖動來源。落地時開回。
  - 飛行時 `Rigidbody.interpolation` 從 `None`（地面預設）切到 `Interpolate`（`None` 下畫面只跟著 50Hz 物理步更新，空中平移看起來就是抖）；落地還原。
  - 姿態改用**自持的 `_flightRot`**（不再每幀讀回 `Rigidbody.rotation` 再 slerp，避免物理/插值 nudge 反饋成抖動）。
- `VehicleFlightData` 加 `landingClearance` / `landingImpactSpeedCap`（既有 asset 自動吃到新預設值）。
- EditMode `VehicleFlightStateTests` **18/18 綠**（+2：離地高落地、低空按 Ctrl 不落地）；全套 215 tests 214 綠（唯一失敗 `...FallsBackToAttack3` 既有無關）。編譯無錯。

### 追加67 續 2 — 修：飛行時輪胎貼圖被拉伸

使用者實機:「飛行時輪胎貼圖沒跟隨導致拉伸」。

- 根因：追加67 續飛行時停用了 WheelCollider，但 `VehicleController.FixedUpdate` 還是每步呼叫 `WheelVisualSync.SyncVisual()`，它把輪骨 `position` 釘在 `WheelCollider.GetWorldPose()` —— disabled 的 collider 回傳的是停用前的**地面舊姿態**，車身飛走輪骨還留在地上，蒙皮就被拉伸。
- 修：`FlightModeActive` 時**跳過 4 個 `SyncVisual()`**（連同已跳過的動力/煞車一起）。不同步 = 輪骨保持起飛當下相對車身的 local 姿態、跟著車身一起飛，正是空中要的樣子。落地 `FlightModeActive` 轉回 false 就恢復同步（一幀內對回真實懸吊姿態，位移極小）。
- EditMode `VehicleFlightStateTests` 18/18 綠（純視覺修正，飛行邏輯沒動）。編譯無錯。

## 2026-08-30 (追加68) — 原神式切換走/跑（Left Alt，沉浸式）

使用者:「設計像原神那樣的 切換式 跑步/慢走 沉浸式體驗」。

- **鍵位**：**Left Alt** 輕點切換走/跑（`切換式`＝按一下翻轉、持久）。預設跑。
- Maya Locomotion blend tree 只有 `NewWalk`（speed 0–0.8）/`NewRun`（speed 2），`CharacterAnimatorLink` 直接餵實際水平速度 → **降速度走路動畫就自動出來，不用動 animator**。
- **`IInputCommand`**：加 `bool WalkTogglePressed`，用 **default interface member（`=> false`）** —— 玩家專用（AI 不切換），所以 15 個測試 stub + EnemyAI 全部不用改照樣編譯，只有 `PlayerInputProvider` override。
- **`PlayerInputProvider`**：`WalkTogglePressed = leftAltKey.wasPressedThisFrame`。
- **`EnemyAI`**：明寫 `=> false`（風格一致）。
- **`CharacterMovement`**：加 `walkSpeed`（0.9，~45% run pace）＋ `_walkMode`。地面移動 `_isFlying ? flightMoveSpeed : (_walkMode ? walkSpeed : moveSpeed)`。起飛強制清 `_walkMode`（落地回跑）。純函式 `static NextWalkMode(current, togglePressed, isFlying)` 抽出可單測。公開 `IsWalking`（給之後相機拉近/FOV 的沉浸 polish 用）。
- 貓也吃同一個切換（共用 `PlayerInputProvider`），各自記自己的 `_walkMode`。Player walkSpeed 0.9 / moveSpeed 2；Cat walkSpeed 0.9 / moveSpeed 3（比例可各自在 Inspector 調）。
- **測試**：EditMode `WalkRunToggleTests` **6/6 綠**（切換翻轉/持久/飛行強制跑）；全套 221 tests 220 綠（唯一失敗 `...FallsBackToAttack3` 既有無關）。PlayMode `CharacterMovementTests.WalkToggle_MakesGroundMovementSlower` 已加（Editor 沒焦點無法自跑，待使用者/CI）。編譯無錯，無場景改動（新欄位吃 C# 預設值）。
- **v1 只做速度+動畫**。相機「慢下來看風景」的拉近/FOV polish 沒做，之後 `ThirdPersonCameraController` 讀 `IsWalking` 再加。**待實機**：`walkSpeed 0.9` 對 `NewWalk` clip 會不會腳滑（滑就往 clip 的 authored pace 調高）；Left Alt 在你的鍵位習慣順不順。

### 追加68 續 — 走路時相機「慢下來看風景」的拉近 + 收 FOV

使用者:「相機『慢下來看風景』的拉近/FOV 沒做（IsWalking 已公開，之後要再加）幫我做這個」。

- `ThirdPersonCameraController` 新增（都在第三人稱、非瞄準/非對決時才作用）：
  - `walkDistanceMultiplier`（0.82）—— walk 模式時 `desiredDistance` 乘這個，鏡頭微拉近。非破壞性（不覆寫 `distance`），同 `flightDistanceMultiplier` 慣例。
  - `walkFieldOfViewDelta`（−6）—— walk 模式時第三人稱 FOV 加這個，微收。
  - `walkFramingBlendSpeed`（5）—— `_walkFramingBlend` 0↔1 lerp-per-frame 緩動，切換走/跑時鏡頭 ~0.4s 平滑進出，不是硬切。
- 讀 `targetMovement.IsWalking`（追加68 的 walk 切換）。飛行時 `IsWalking` 本來就 false → blend 自動回 0。瞄準/對決相機各自覆寫 distance & FOV，不衝突。
- 純函式 `StepBlend01` / `WalkFramedDistance` / `WalkFramedFieldOfView` 抽出可單測（同 `ComputeAutoCenterYaw` 慣例）。
- Main Camera（target Player）＋ CatCamera（target Cat）都自動吃到（新欄位吃 C# 預設值，無場景改動）。
- **測試**：EditMode `ThirdPersonCameraControllerTests` +6 綠（blend 緩動/夾值、距離拉近、FOV 收窄）；全套 227 tests 226 綠（唯一失敗 `...FallsBackToAttack3` 既有無關）。編譯無錯。
- **待實機**：拉近幅度（base distance 已經只有 2，×0.82 = 1.64，可能偏微妙）／FOV −6 收多少合適／緩動速度；數值全在 `ThirdPersonCameraController` Inspector。

## 2026-08-30 (追加69) — 影片技能特效：R 大招施法「劍體環繞」（不要有人形.mp4）

使用者:「把這段影片做為 player 的技能特效，要處理透明通道 和仿3d問題」→「用推薦」（綁到 R 大招施法）。

- **素材**：`不要有人形.mp4`（1280×720、H.264 **無 alpha**、24fps、240 幀）—— 一把 3D 劍被橘/藍能量軌道環繞，黑底，右下角小星星浮水印。歸檔在 `Assets/_Project/VFX/Skills/SwordOrbit/Source/SwordOrbitSource.mp4`（僅供重製，**執行期不播 MP4**，同 `SlashSourceVideo.mp4` 慣例）。
- **透明通道**：ffmpeg 離線烘成 `SwordOrbit_Atlas.png` —— 抽 50 幀（第 20~216 每 4 幀）、`drawbox` 塗掉右下浮水印、`geq` 由亮度算 alpha（`a = 255·pow(clip((max(rgb)−40)/180,0,1), 1.5)`，黑底→透明、噪點地板夯到 0）、`tile=8×7` 拼圖集（2560×1260，透明 padding）。
- **仿3d（2.5D）**：`SwordOrbitVfxSetup.cs`（選單 `Tools/Live2DAction/Add Sword Orbit Skill VFX (R ultimate cast)`，可重跑）建 flipbook 預製體 —— 主體 billboard flipbook quad（`SlashFlipbookURP` shader、**premultiplied Alpha blend** src=One/dst=OneMinusSrcAlpha，因為有白/藍核心會過曝、不能純 Additive；`_Brightness` 1.7 給 bloom）＋ 2 個小子粒子（Sparks 火花爆、GlowPulse 藍光暈）給體積感。billboard = 從任何角度都不會看到「側面變一條線」。`SlashVfxController` 自動回收。
- **接線**：`UltimateAbility` 加 `castVfxPrefab` + `castVfxLocalOffset`（null-safe），發動大招瞬間（`burst.Play()` 旁）`Instantiate` 到玩家身上（parent 到玩家、隨起手旋轉）。`SwordOrbitVfxSetup` 把預製體接到 Player 的 `castVfxPrefab`（offset (0,1.1,0) 胸口高）。
- 匯入設定：mipmap off、clamp、alphaIsTransparency、**npotScale None**（保 2560×1260 不被縮成 2048×1024）。
- 編譯無錯；全套 227 EditMode tests 226 綠（唯一失敗 `...FallsBackToAttack3` 既有無關）。VFX 純視覺無單測，靠 `GreyboxTest` 重現（R 鍵、能量滿）。
- **待實機**：Play Mode 我沒法自己看（失焦凍幀）。編輯期 simulate 預覽：特效渲染正常、透明乾淨、billboard 各角度一致。要調的：大小（`SlashVfxController.sizeMultiplier` 或 `SwordOrbitVfxSetup.SizeHeight` 3.4）、亮度、壽命 1.05s、子粒子強度；低角度對天空還看得到極淡的 quad 霧（實際打鬥場景應該無感，真的礙眼再把 alpha key 推更硬）。

## 2026-08-31 (追加70) — 修:貓視角下攻擊/技能會連帶觸發 player ＋ 武士死亡飄在空中

使用者:「1. 守望者視角 player 和貓視角，切換時一些移動、攻擊按鍵只有當下那個視角的角色會觸發，現在的問題是 cat 視角下攻擊會連帶觸發 player 攻擊 2. 武士死亡後卡在飄浮在空中的狀態」。

### 1. 附身切換沒有關掉 player 的戰鬥輸入元件

- **病因**：`CameraPossessionSwitcher.playerControl[]` 只放了 `CharacterMovement`。附身貓的時候只有 player 的「移動」被關，`PlayerCombat`（左鍵普攻）、`UltimateAbility`（R）、`TargetLockController`（滾輪鎖定）、`RangedWeapon`（右鍵瞄準/開火）、`ExecutionAbility`（F 處決）全部照常吃共用滑鼠/鍵盤 → 你操作貓的時候 player 也在原地揮刀。附身系統（追加于 2026-08-28）比貓戰鬥（追加30）早，之後沒回頭補。
- **修法**：`CatCharacterSetup` 新增 `CollectPlayerControl(player)`（`CollectCatControl` 的鏡像），把 player 上所有讀輸入的元件都收進 `playerControl[]`：`CharacterMovement`＋`PlayerCombat`＋`TargetLockController`＋`UltimateAbility`＋`RangedWeapon`＋`ExecutionAbility`。`BuildSwitcher` 簽章由 `CharacterMovement` 改吃 `Behaviour[]`。
- **場景**：直接把 `GreyboxTest.unity` 裡 `CameraPossession` 的 `playerControl` 陣列補成這 6 個並存檔（不用重跑選單）。貓側 `catControl[]` 本來就完整（5 個），不動。貓有自己的 `PlayerInputProvider` 實例，兩邊不互相污染。
- **順帶把兩個「中途被停用」的破口補起來**（附身切換、player 死亡、場景拆除都會觸發）：
  - `UltimateAbility.OnDisable`：大招丟劍是 coroutine，會把劍 `SetParent(null)` 拿在手上逐幀驅動。中途被停用 → coroutine 被 Unity 直接砍掉，劍留在半空、`_active` 卡 true（大招從此再也放不出來）。現在 `OnDisable` 會 `StopAllCoroutines` + 把劍瞬移回背上 + 清狀態。
  - `RangedWeapon.OnDisable`：瞄準中被停用 → 準心 UI / 手上 AK 模型 / tracer 線會留在畫面上。現在 `OnDisable` 一律關掉。
  - `TargetLockController` 不動：殘留的鎖定無害（consumer 都停用了），重新附身回 player 時 `Update` 第一幀就會重新驗證距離。

### 2. 武士死亡後屍體飄在半空

- **病因**：武士的 Animator 在**根物件**上、`applyRootMotion=False`。死亡播 `Wushi_DeathFallForward`（clip `Fall_Dead_from_Abdominal_Injury`），該 mocap 的 `RootT.y` 從 0.95 掉到 0.18（人往地上倒的垂直位移**在 root motion 裡**）。`applyRootMotion=False` 把這段位移整個丟掉，最後定格在「站姿髖高」的躺平姿勢 → ×4 模型縮放下 hips/head/feet 全在 y≈2.4–2.7，地面在 0.5，屍體飄 ~2m。（實測：修前 Hips y=2.701 / Head 2.538 / LeftFoot 2.409。）
- **修法**：把 `Wushi_DeathFallForward.fbx` 這個 clip 的匯入設定改成 **Bake Into Pose (Root Transform Position Y) = ON**（`lockRootHeightY=true`）＋ **Based Upon = Feet**（`heightFromFeet=true`）—— 垂直位移改成留在 pose 裡（不靠 root motion 就會演），並以腳為基準錨定，讓站姿起手和躺平收尾都貼著 transform 的地面。修後定格：Hips y=1.04 / Head 0.88 / Feet 0.75–1.17，屍體躺在地上。站姿起手不變（Hips 2.58 / Foot 0.93），crossfade 進場無 pop。
- 只動這一個 clip。`Wushi_PostureKneel`（`falling_down`）的 `RootT.y` 有一樣的下墜側寫，理論上跪地也會飄一點，但使用者沒回報（跪→起身很快），先不動。屁孩王的死亡 clip 走另一套（Animator 在 Visual 子物件 + `BossAnimatorRootMotionRelay`），不受影響。

- **測試**：EditMode 全套 227 tests，唯一失敗仍是既有無關的 `CharacterAttackAnimationLinkTests...FallsBackToAttack3`（Attack4 已加、測試沒跟上）。PlayMode `CameraPossessionFirstPersonRestoreTests` 待跑（Editor 失焦卡住，需使用者點進 Editor 視窗）。
- **待實機**：(1) 貓視角下確認左鍵/R/F/滾輪只作用在貓身上、切回 player 一切正常；(2) 打死武士看屍體是否貼地（LeapSlam 半空被打死的極端情況我沒法逐幀驗），跪地招式順帶留意有沒有飄。

### 3. 武士＋屁孩王都要 5 秒後復活、屍體不消失、慢慢站起來（追加70 續）

使用者:「不管是武士還是屁孩王都要讓他們在死亡五秒後復活 並且做完死亡動作後屍體不要消失 復活時間到慢慢站起來」。

- **武士改成會復活**：`Wushi_Tuning.permanentDeath` **true → false**（屁孩王本來就 false）。兩隻 `reviveDelaySeconds` 都已是 5。
- **屍體不消失**：兩隻的 `Health.deferDeactivationToDeathAnimation=true` 且都沒掛 `DeathAnimationLink` → `Health` 從不 `SetActive(false)`，死亡 clip 也不 loop（`loopTime=false`），本來就不會消失。順手把**屁孩王死亡 clip** `PW2_ShotAndFallForward` 也套上跟武士同樣的 `lockRootHeightY=true`＋`heightFromFeet=true`（它的 `RootT.y` 也是 0.98→0.13，同樣會飄，只是它會復活比較不明顯）→ 屍體貼地。
- **慢慢站起來**：新 `BossState.GettingUp`（enum **最後一位**，其他 ordinal 不動；無資產序列化 BossState int）。`UpdateDead` 復活時序：先 `ResetHealth`/`EndStagger`/清 phase&排程旗標/`RestoreRenderers` → `ChangeState(GettingUp)`（不再直接 `ChangeState(Alert)`）。
  - `OnEnterState(GettingUp)`：`animator.Play(死亡state, 0, 1f)`（硬切到死亡 clip 最後一幀＝當下已在的趴地姿，無 pop）＋ `animator.speed = 0`；`Health.SetInvulnerable` 起手 i-frame。
  - `UpdateGettingUp`：每幀 `animator.Play(死亡state, 0, 1f - t)`，`t = _stateTimer / StandUpSeconds` → 手動把死亡動畫**倒著刮**回站姿（沒有專屬起身 clip，倒放死亡＝爬起來）。`_stateTimer >= StandUpSeconds`（新 tuning 欄位，預設 **1.8s**，吃 C# 預設值不用改 asset）→ 還原 `animator.speed=1`、清 i-frame、`ChangeState(Alert)` → Alert(0.3s)→Idle→Locomotion 正常接。
  - 守門：優先序 cascade（`Tick`）、`UpdateCombatTimer` eligible、`WriteAnimatorParameters` 的 `CombatActive`、`RequestBeHitFlyUp` 全部把 `GettingUp` 跟 `Dead` 一起排除 → 起身期間不被打斷、不吃排程技計時、不進戰鬥架勢、不被 launch。`OnExitState` 補一個「離開 GettingUp 一定清 invuln」保險（`animator.speed` 本來就每次 exit 無條件歸 1）。
  - `DeathStateName()` helper（`deathClipName` 有設用它，否則 `behitFlyUpClipName`）給 Dead + GettingUp 共用。
- **實測（Play Mode，Editor 有焦點）**：武士連killtwo次，log 都是 `Dead -> GettingUp -> Alert -> Idle`，`_deathElapsed` 到 ~5.00 才離開 Dead；屁孩王反射檢查同樣 `_deathElapsed≈5.0` 後站起、`activeSelf=true`、renderer 全開、滿血。武士站姿 Hips y≈2.7、屁孩王 Hips y≈1.5（貼地）。
- **測試**：EditMode 227，唯一失敗仍是既有無關 `...FallsBackToAttack3`。BossStateMachine 無 FSM 單測（巨型 MonoBehaviour，靠 play 驗證，慣例）。
- **待實機**：倒放死亡當起身動畫的觀感（placeholder mocap 倒放本來就會有點怪）；`standUpSeconds` 1.8 的長短；LeapSlam/Vanish **半空**被打死時 `UpdateDead` 不主動施重力弧、只靠一般重力掉下來再躺平，沒逐幀驗過。

### 4. 屁孩王有招式讓半身陷到地板下（追加70 續，使用者回報）

使用者:「檢查屁孩王的有一個動作會讓他半身在地板之下」。

- **是哪兩招**：`PW2_Breakdance1990`（定時 flourish）＋ `PW2_KneelOnOneKneeAndStand`（架勢崩潰跪地 / `kneelStandClipName`）。實測（FSM 驅動、Play Mode 有跑幀）Breakdance 時腳 y ≈ **−0.33**（地板 0.5，等於整個下半身埋進去 ~0.8m）。之前 2026-08-26 有人寫過 `PW2_FixBreakdanceKneelClipBaking.cs`（一次性，從沒被跑過）想修同一件事，但方向錯（假設 Humanoid + heightFromFeet）。
- **根因**：這兩招唯一的來源是 `Meshy_AI_Meshy_Merged_Animations.fbx`——它是 **Generic**（屁孩王其他所有招式來自各自獨立的 `*_withSkin.fbx`，都是 Humanoid）。這顆 FBX 的 **position 曲線比 rig 小 100 倍**（cm/m 單位不一致）：clip 把 `Hips.m_LocalPosition.y` 打到 **0.834**，但 rig 的 bind Hips 是 **83.43**（`LeftLeg` 0.379 vs 37.86、`LeftFoot` 0.330 vs 32.98…全部剛好 ×100）。Generic clip 直接套原始 transform 曲線 → 骨架被壓到近原點 → 埋進地板。Humanoid clip 走 muscle 重定向、不受單位影響，所以只有這兩招出事。
- **修法**：`Meshy_AI_Meshy_Merged_Animations.fbx` importer `useFileScale: 1 → 0`（globalScale 留 1）→ 有效匯入尺度從 0.01 變 1.0，position 曲線值對回 rig（Hips.y 0.834 → 83.43，逐骨驗證與 live rig 一致）。不改 `animationType`（維持 Generic）、不動 clip 切割、controller state 連結不變。那顆 FBX 裡另外 8 個 clip 沒被任何東西用到。
- **⚠️ 沒能實機驗最終效果**：改完當下 Editor 失焦、Play Mode 凍幀（`Time.frameCount=1`），`AnimationMode.SampleAnimationClip` 對這顆多-clip FBX 子資產取樣也不可靠。單位×100 的 bug 本身是鐵證、修正無疑問，但「是否已完全貼地、有沒有第二層問題」要**使用者在 Editor 有焦點時 Play 看一眼**。曾嘗試改 Humanoid（`CreateFromThisModel` + `heightFromFeet`）但 take 自動切割會跑掉、已 `git checkout` 還原。

### 5. 武士只待在警備範圍、不追玩家到門口（追加70 續，使用者回報）

使用者:「發現武士只會待在警備範圍 而不是像屁孩王一樣會追逐玩家到門口」。

- **病因**：`UpdateIdle` / `UpdateApproach` 的「玩家太遠就脫戰回家」判定是寫死的 `tuning.AlertRange * 1.5f`——武士 `alertRange=6` → **9m**。玩家離武士 9m 就 `DisengageAndReturnHome`。諷刺的是「不受城牆限制」的 boss 反而追得比「被關在本地」的菁英怪還近（屁孩王 `confineToArena=true` → 玩家一出方形就進 `GateWatch` 一路走到門口）。追加45 把武士 `leashRange` 30→14 是為了「脫戰後真的跑回家」，但那個 9m 的 from-boss 判定更早觸發、成了實際上限。
- **修法**：
  - 新 `ChaseGiveUpDistance()` = `Mathf.Max(tuning.AlertRange * 1.5f, leashRange)`。`UpdateIdle` / `UpdateApproach` 兩處 from-boss 脫戰判定改用它 → from-POST 的 `leashRange`（`TryLeashReset`，有 0.35s grace）成為「追多遠」的唯一權威；`AlertRange*1.5` 只當「leash 很小的 boss 被繞著駐點放風箏」的地板。
  - **武士場景 `leashRange` 14 → 32**（駐點在本地北端 z=11、南邊載具門口約 z=−15.5 ＝ ~26m）→ 武士會一路追出門口才回頭。玩家真的繼續往南跑、離駐點 >32m 撐過 grace → 才 `DisengageAndReturnHome`。
  - `TryEnterLeapSlam` 的 `leapCap`（追加45 收緊到 ~9m 防「憑空衝過來」）**不動**——那是瞬移距離、跟追擊無關。
- **屁孩王不受影響**：`confineToArena=true` → `TryLeashReset` 的 confine 區塊先跑（GateWatch），而且方形只有 31 寬、玩家在方形內離不了屁孩王 `max(7.5,30)=30m`。`leashRange` 本來就 30。
- **測試**：EditMode 227，唯一失敗仍是既有無關 `...FallsBackToAttack3`。BossStateMachine 無 FSM 單測。**待實機**：武士追出門口的路徑（direct steering、非 NavMesh，玩家沿外牆側移時武士會頂牆）；32m 的距離感；追出去後 ReturnHome 有沒有正常跑回。Editor 失焦凍幀，我沒法自己看。

## 2026-08-31 (追加71) — AI 避障：NavMesh 路徑跟隨（agent-less）

使用者:「所有角色在移動時有可能會被地圖物件擋住路線從卡住 有沒有演算法可以避開這個問題」→ 分析後選「AI: NavMesh 路徑跟隨」。

- **病因**：`EnemyAI` 和 `BossStateMachine.MoveTowardTarget()` 都是**直線朝目標推**（`(target - self).normalized * speed`），零避障。中間有牆/柱/建築就一直往裡頂、`CharacterController` 擋死 → 原地磨。剛才「武士追出門口」那條 direct-steering 也會踩到。場景**本來完全沒有 baked NavMesh**（triangulation vertices=0）。
- **做法**（不引入 NavMeshAgent、movement 系統不動）：
  - **`NavPathFollower.cs`**（`Live2DAction.Runtime`）：`SteeringDirection(targetPos)` → 問 baked NavMesh 要路徑、回「朝下一個轉角」的水平單位方向。路徑每 `repathInterval`(0.3s) 或目標移動 >1.5m 才重算。**fail-open**：沒 NavMesh / 目標在 mesh 外 / 不可達 → 退回直線方向（＝今天的行為），所以掛了這元件但底下沒 baked mesh 的 AI 不會更糟。純函式 `AdvanceCorner(corners, self, current, reach)` 抽出，7 個 EditMode 測試。`IsDetouring` 供之後「還在繞路時先別出招」用。
  - **`BossStateMachine`**：`MoveTowardTarget` + `UpdateReturnHome` 改成先問 `_pathFollower`（null-safe）。`EnemyAI`：**地面** chaseDirection 改路由（空戰 chase 和 facing/MoveInput 仍用玩家原方向）。
  - **`NavMeshBakeSetup.cs`**（選單 `Tools/Live2DAction/Bake Navigation Mesh`，可重跑，**不自動呼叫**）：在 `Navigation` 物件掛 `NavMeshSurface`（`CollectObjects.All` + `PhysicsColliders` → 建築/牆自動變障礙），角色 + 車輛掛 `NavMeshModifier(ignoreFromBuild)` 不讓它們在 bake 時挖洞。同步 `BuildNavMesh()` + 把 `NavMeshData` 存成 `Assets/_Project/Scenes/GreyboxTest/NavMesh-Navigation.asset`（CopySerialized 保 GUID），再把 `NavPathFollower` 掛到武士/屁孩王/Enemy。
- **實測**：baked 3461 verts；南牆 doorway 在 x=0（navmesh 正確留洞）；路徑穿過 Mecha 的 query 回 4 個轉角繞開（straight 會撞）；Play Mode（Editor 有焦點）`SteeringDirection` 對「Mecha 另一側的點」回 `(0,0,-1)`（沿 x≈0 南下繞開）而非 straight 的 `(0.15,0,-0.99)`，`IsDetouring=true`，Awake 建 `NavMeshPath` 無 exception。
- **測試**：EditMode 234（+7 NavPathFollower），唯一失敗仍是既有無關 `...FallsBackToAttack3`。
- **範圍/待辦**：
  - 這次只接 AI（Enemy/武士/屁孩王）。**Player/Cat 沒接**——它們是輸入驅動、沒有「路線」可規劃，卡是碰撞體品質問題（凹角楔住、collider 接縫），要另做 collider pass（凹面 MeshCollider → primitive/convex、內凹角切斜面、「有輸入但位移≈0」的脫困輔助）。
  - **NavMesh 要跟地圖維護**：改/加地圖幾何後要重跑 `Bake Navigation Mesh`。greybox scene builder 沒加自動 bake（全套 bake 慢、地圖不是每次都重建）。
  - 學校區 navmesh 目前 `PathPartial`（plaza 在 y=−6、地形破碎），校園內避障效果打折，待該區 collider/地面整理後重烤。
  - 角色被擊飛出 NavMesh 的邊界情況：`SamplePosition` radius 2.5 通常能拉回；真的飛很遠會 fail-open 直線。
  - **待實機**：實戰情境下 AI 繞牆追人順不順、0.3s 重算頻率的手感、`cornerReachDistance` 0.75 會不會「切角」切進牆。

## 2026-08-31 (追加72) — 修:武士會突破本地領地跑出去（追加70/71 的過頭修正）

使用者:「武士現在會突破本地30*30範圍領地限制」。

- **背景**：追加70 為了「武士追玩家到門口」把 `leashRange` 14→32、加 `ChaseGiveUpDistance`。但那是**理解錯方向**——使用者要的是「像屁孩王一樣追到門口」＝追到**本地邊界為止就停**（屁孩王 `confineToArena=true` → `GateWatch` 走到牆邊站定），不是穿過門口跑出本地。追加71 的 NavMesh 避障又讓武士真的能順順地跑出去到車道。
- **修法**：武士場景 `confineToArena` **false → true**（＋ `arenaCenterXZ`(0,0) / `arenaHalfExtent` 15.5 / `gateWatchRange` 10，全部對齊屁孩王）。純場景值，無程式改動。
  - 玩家**在本地內** → 武士照常追打（`ApplyMotion` 每幀 `ArenaBounds.ClampInside` 把它夾在 15.5 半徑方形內）。
  - 玩家**出門口** → `TryLeashReset` confine 區塊先跑 → 武士進 `GateWatch` 走到牆邊、面向玩家站定、不攻擊、**不出本地**。玩家回來 → 重新交戰；玩家離門口 >10m 撐 1.5s → `ReturnHome` 跑回駐點。
  - 這條路徑跟屁孩王完全同碼、已驗證過。`leashRange` 32 留著（要 ≥ 駐點到方形對角 ~30.7m，才不會在方形內誤觸發）。
- **taxonomy 更新**：武士**不再是「不受城牆限制的 boss」**——武士＋屁孩王現在都 `confineToArena=true`、都被關在本地。差異退回純數值（HP/招式池/leashRange/tuning）。
- 編譯無錯（無程式改動）；EditMode 不受影響。**待實機**：武士被打到架勢崩潰或飛空技能中途玩家跨界的邊角（屁孩王同碼沒回報過問題）。

## 2026-08-31 (追加73) — 復原:武士回到「不受城牆限制」（追加72 撤回）

使用者:「我想起來了 是我一開始設計讓它不受城牆限制的 請復原」。

- 追加72 把武士 `confineToArena` 0→1（以為使用者要「像屁孩王一樣被關本地」）。使用者確認**原始設計就是讓武士不受城牆限制**——boss 可以追出本地。撤回：武士場景 `confineToArena` **1 → 0**、`gateWatchRange` **10 → 0**（confine 關了本來就不用）。純場景值。
- **武士 vs 屁孩王 又分開了**：屁孩王 `confineToArena=1`（被關本地、GateWatch）；武士 `confineToArena=0`（不受限，只吃 `leashRange` 32 的距離 leash）。
- **`leashRange` 32 保留**（追加70）：這是「武士只會待在警備範圍」那個回報的修正——武士會追出門口、到離駐點 32m（~z=−21，過門口上車道一小段）才 `DisengageAndReturnHome` 跑回。`ChaseGiveUpDistance()` 也保留。
- 追加71 的 NavMesh 避障讓武士追出去時能繞開障礙、不頂牆。
- 無程式改動、EditMode 不受影響、Console 無錯。

## 2026-08-31 (追加74) — 貓咪三條 HUD（生命/能量/架式）＋ 貓咪削韌機制

使用者:「為貓咪補上三個血量條 能量條 架式條」→ 選「只在操控貓時顯示」＋「架式條接真機制」。

### 貓咪削韌（`StancePoise`，真機制）

- 貓本來**完全沒有架勢/削韌**（`PlayerCombat.stance` / `CharacterMovement.stance` 都 null）。現在 `CatCharacterSetup.BuildCat` 加 `StancePoise`（`maxStance` **50**、`staggerDurationSeconds` **4**，其餘吃預設；比 player 的 60 低——「小生物比較快斷」）＋ 接 `combat.stance` / `movement.stance`。
- `StancePoise` 是 drop-in：訂閱 `Health.Damaged`、任何進 `Health.ApplyDamage` 的傷害都按 `stanceGainMultiplier`(0.2) 累積削韌；滿 → `IsStaggered` 4 秒、期間 `PlayerCombat`/`CharacterMovement` 已 null-safe gate 住（不能出招、不能移動）；沒被打會回復。
- **貓沒有硬直動畫**（Meshy 模型無 clip）→ 硬直時就是定住不動 ~4s、無視覺回饋（跟貓其他佔位動畫狀態一致）。
- **待實機平衡**：貓 200 HP、削韌 5/擊（25 傷 ×0.2）、10 擊滿；25 傷/擊 8 擊就打死 → 目前敵人打貓**打死比打斷快**（跟 player 的「balance coincidence」同款）。要讓貓真的會斷得調低 `maxStance` 或調高貓的 `stanceGainMultiplier`。

### 三條 HUD（`CatCornerHud` + `PossessionHud`）

- 新 `CatBarsWiring.cs`（選單 `Tools/Live2DAction/Add Cat Bars`，也從 `CatCharacterSetup` 結尾呼叫，同 `WatcherCatWiring`/`VehicleCatWiring` 慣例）：**Clone 整個 `PlayerCornerHud`** → `CatCornerHud`，砍掉 `飛行` 那行 + `PlayerCornerHud` 元件，`必殺Label`→「能量」、`架勢Label`→「架式」，Panel 高 156→122。三個 `*BarFx` 重新指向貓：
  - `生命Track` `PlayerHealthBarFx.health` → `Cat.Health`（200）
  - `必殺Track` `UltimateEnergyBarFx.energy` → `Cat.UltimateEnergy`（貓唯一的能量 = 飛行能量 500）
  - `架勢Track` `StancePoiseBarFx.stance`+`.health` → `Cat.StancePoise` + `Cat.Health`
- 新 `PossessionHud.cs`（Runtime）：`LateUpdate` 讀 `CameraPossessionSwitcher.Current`，`Current==Cat` → 開 `CatCornerHud`.Canvas / 關 `PlayerCornerHud`.Canvas，否則相反。toggle `Canvas.enabled`（不 SetActive，BarFx 繼續跑、不 snap-in，同 `WushiBossHudVisibility` 手法）。**不 gate 戰鬥狀態**（「只在操控貓時顯示」，不是「戰鬥中才出現」）。switcher 遺失 → fallback 顯示 player HUD。純函式 `ShowCatHud(hasSwitcher, catPossessed)` 2 個 EditMode 測試。
- 位置跟 player HUD 一樣在右上角（互斥、換人時整組換掉）。
- **實測**：`Wire()` 建出結構正確（3 行、標籤、所有 Fx ref 都指向貓）；Play Mode（凍幀）手動叫 `Apply()`：`Current=Cat`→PlayerHud off/CatHud on，`FocusPlayer`→相反；削韌實測 3×25 傷 → 15/50，數學對。`LateUpdate` 自動 toggle 沒能實機看（Editor 失焦凍幀）。
- **測試**：EditMode 236（+2 `PossessionHudTests`），唯一失敗仍是既有無關 `...FallsBackToAttack3`。
- **待實機**：HUD 位置/大小觀感（右上角跟 player 同位，換人整組換）；貓削韌數值平衡（見上）；`LateUpdate` 換 HUD 有沒有一幀閃爍。

## 2026-08-31 (追加75) — 貓咪大招：能量滿格 R 施放「黑暗劍氣」技能特效

使用者:「讓 cat 能量滿格時可以施放 '幫我生成一個黑暗劍氣風格的版本.mp4' 這個技能特效 要處理透明通道和仿3d問題」。

- **素材**：`幫我生成一個黑暗劍氣風格的版本.mp4`（1280×720、H.264 **無 alpha**、24fps、240 幀）—— 暗紅/紫的符文劍在黑影中成形 → 旋成漩渦盤 → 揚塵消散，黑底，右下角小星星浮水印。歸檔在 `Assets/_Project/VFX/Skills/DarkSwordQi/Source/DarkSwordQiSource.mp4`（僅重製用，執行期不播 MP4，同 `SwordOrbitSource.mp4` / `SlashSourceVideo.mp4` 慣例）。
- **透明通道**：ffmpeg 離線烘成 `DarkSwordQi_Atlas.png` —— 抽第 24~156 幀每 3 幀共 **45 幀**、`crop=720:720:400:0`（中央方形裁切，順帶把右下浮水印裁掉、不用 drawbox）、`scale 256`、`geq` 由亮度算 alpha（`a = 255·pow(clip((max(rgb)−26)/145,0,1), 1.45)`）、`tile=8×6` 拼圖集（2048×1536，透明 padding）。
- **仿3d（2.5D）**：`CatDarkQiVfxSetup.cs`（選單 `Tools/Live2DAction/Add Cat Dark Sword-Qi Skill`，可重跑，跟 `SwordOrbitVfxSetup` 同一套）建 flipbook 預製體 `CatDarkQiSkillVFX.prefab` —— 主體 billboard flipbook quad（`SlashFlipbookURP` shader、**premultiplied Alpha blend** One/OneMinusSrcAlpha、`_Brightness` 1.6 給 bloom）＋ 2 個小子粒子（Embers 暗紅火花爆、GlowPulse 暗紫光暈）給體積感。`SlashVfxController` 自動回收（~1.3s）。
- **機制（`CatUltimateAbility.cs`，Runtime）**：
  - 貓加**專屬技能能量** `Cat/SkillEnergy`（新 `UltimateEnergy` 子物件，100 / 5-per-1s = 20s 滿，跟 player 大招同節奏；**跟飛行能量 500 那顆完全獨立**，兩顆各管各的，同 player 有兩顆的做法）。
  - `CatCornerHud` 的「能量」條**改指向 SkillEnergy**（追加74 原本指飛行能量）——所以貓的能量條現在代表「大招充能」；貓的飛行能量目前無條顯示（追加74「順帶做掉飛行體力條」的那個回退，note 一下）。
  - `Update`：`input.UltimatePressed`（R）＋ `energy.IsFull` ＋ 非硬直非死亡 → `energy.Consume()` + `Instantiate(castVfxPrefab)` 掛貓身上（offset (0,0.55,0)）+ 一發 AOE（`CatDarkQi.asset` AttackData：**120 傷 / OverlapSphere 3.2m / 擊退 9**，走 `AttackResolver.ResolveHits`）。
  - 掛進 `CameraPossessionSwitcher.catControl` → R 只在**操控貓時**施放（player 自己的 `UltimateAbility` 同樣被 catControl 停用）。
- **接線**：`CatCharacterSetup.BuildCat` 加 `SkillEnergy` + `CatUltimateAbility`（enabled=false）；結尾呼叫 `CatDarkQiVfxSetup.Wire()` 填 castVfxPrefab / attack / stance / health、re-point HUD 能量條、加進 catControl（no-op 若 atlas/prefab 還沒烤 → 跑一次選單）。
- **實測（Play Mode，Editor 有焦點）**：可以附身貓（`CatUltimateAbility.enabled` 變 true）、`SkillEnergy` 灌滿 `IsFull=true`；反射叫 `ResolveBurst()` → 假人 100→0（120 傷）；prefab spawn 出 3 個 particle system、材質/貼圖/blend 都對；`CatDarkQi.asset` 建出來（dmg 120 / range 2.6 / radius 0.6）。**VFX 在場景視圖對天空 simulate 預覽有渲染**（暗紅漩渦環＋亮核＋火花），但 game-view MCP 截圖抓不到粒子（SwordOrbit 追加69 同樣狀況、當時也只靠 simulate 預覽）。
- **測試**：EditMode 236，唯一失敗仍是既有無關 `...FallsBackToAttack3`。VFX 純視覺無單測；`CatUltimateAbility` 靠 `AttackResolver` 既有覆蓋 + 實測 ResolveBurst。
- **待實機**：(1) 黑暗劍氣對深色戰鬥場景的觀感（暗紅色調對亮橘 greybox 牆對比低，實際場景 + bloom 應該更明顯；亮度調 `DarkSwordQiFlipbook.mat` `_Brightness` 或 `SlashVfxController` 欄位）；(2) 大小 `SizeHeight` 3.2 / offset (0,0.55,0)、壽命 1.15s；(3) 120 傷 / 3.2m AOE / 20s 充能 的平衡；(4) R 在操控貓時的鍵位習慣；(5) SFX 無聲（專案還沒原創戰鬥音效，rule 1）。

## 2026-08-31 (追加76) — 貓吃泉水、貓普攻改 50 傷、確認兩組特效處理

使用者:「1.讓cat也可以吃到泉水恢復效果 2.檢查cat的r技能特效是否有參照player r技能特效的處理方法 3.cat改成每段普通攻擊扣50點傷害 4.檢查player普通攻擊特效是否有處理透明通道 彷3d 特效渲染」。

### 1. 貓吃泉水（`HealingSpring` 重寫）

- 舊 `HealingSpring`：(a) 單槽快取（`_playerHealth` 等欄位）→ player 跟貓同時站進去只有後進的一個吃得到；(b) `GetComponentInParent<UltimateEnergy>()` 只找到**一顆**能量 → 貓的 `SkillEnergy`（大招能量，子物件）從沒回復、飛行能量還被兩個 rate 灌到。貓其實早就過了「是玩家」那道 gate（共用 `PlayerInputProvider`，rule 8），只是這兩個 bug。
- 重寫：`Dictionary<GameObject root, Occupant>` 追蹤**每個**站在裡面的角色；每個 Occupant 快取 `Health` + `CharacterMovement.FlightEnergy` + **所有其他** `UltimateEnergy`（`GetComponentsInChildren`）。`Update` 對每個 occupant：Health `Heal(healPerSecond)`、飛行能量 `AddEnergy(flightEnergyPerSecond)`、其餘每顆 `AddEnergy(energyPerSecond)`。
- **player 行為逐位元不變**（它的「其他能量」＝那顆 100 的大招能量，吃 `energyPerSecond` 40，跟以前一樣）。貓現在：Health + 飛行能量(500) + SkillEnergy(100) 三個都回。
- 實測（反射，凍幀）：`OnTriggerEnter(catCol)` → 1 occupant（root=Cat、Health✓、FlightEnergy✓、OtherEnergies=1 = SkillEnergy）；套一次 tick → HP 50→150 / 飛行 0→100 / skill 0→40 三個都漲。
- **既有無關**：場景的 `HealingSpring_MainArea` `healPerSecond` 序列化值是 **40**（程式預設 100，註解說 2026-08-19 調到 100，但場景實例沒重序列化過 → 實際生效一直是 40）。沒動它（CLAUDE.md「手調值是權威，別改回預設」）。

### 2. 貓 R 特效 vs player R 特效 — 確認：**有參照，同一套**

- `DarkSwordQiFlipbook.mat` vs `SwordOrbitFlipbook.mat`：同 shader `Live2DAction/VFX/SlashFlipbook`、同 blend `_SrcBlend=1`(One)/`_DstBlend=10`(OneMinusSrcAlpha)=premultiplied alpha、同 render queue 3000、`_Brightness` 1.6 vs 1.7（都 >1 餵 bloom）。
- prefab 結構相同：主 flipbook ParticleSystem + 2 個 accent 子粒子 + `SlashVfxController`、`mainRenderMode=Billboard`。
- 透明通道：兩者都 ffmpeg 離線由亮度算 alpha 烘 atlas（黑底→透明）。仿3d：billboard flipbook + 立體感子粒子。`CatDarkQiVfxSetup.cs` 就是照 `SwordOrbitVfxSetup.cs` 抄的。

### 3. 貓普攻 50 傷

- `CatSwipe1/2/3.asset` `damage` **6/7/12 → 50/50/50**（「每段普通攻擊」＝三段連段）。`CatHeavy`(蓄力重擊 22)、`CatPounce`(撲擊 16) **沒動**——它們不是「普通攻擊」。
- **⚠️ 待實機**：現在 CatHeavy 22 / CatPounce 16 **比一段普攻 50 還低**，蓄力/撲擊感覺會很弱。要不要等比拉高（heavy ~80、pounce ~65？）等使用者定。

### 4. player 普攻特效 — 確認：**三項都有處理**

- player 三段普攻 `LightAttack1/2/3` 的 `hitEffectOverride` → `Assets/_Project/VFX/Slash/Prefabs/Attack01/02/03.prefab`（`SlashVfxSetup.cs` 建、`alwaysSpawnHitEffect=1` 打空也放）。
- **透明通道**：sprite sheet 由來源影片經外部 python `pack_sheets.py` 抽幀+alpha-key+打包（`Assets/_Project/VFX/Slash/Textures/AttackNN_SpriteSheet.png`）。匯入 `alphaIsTransparency=true`、`wrapMode=Clamp`（不讓相鄰格滲）、`mipmapEnabled=false`。shader `color.rgb *= color.a` premultiply。
- **仿3d（立體感）**：主 flipbook（`renderMode=Mesh` + quad，`alignment=Local` 貼角色 forward 讓劍氣橫掃世界平面、非永遠面向鏡頭）+ 3 個子粒子（Sparks 爆閃 / Smoke 煙 / Glow 光暈）給體積。
- **特效渲染**：hand-written URP HLSL shader `SlashFlipbookURP`（無 Shader Graph 套件）、`_Brightness 1.5` HDR emission 餵 URP Bloom、queue Transparent、`ZWrite Off`、`Cull Off`。`SlashVfxController` 逐-instance 調參 + 自動回收。
- 跟貓 R / player R 用**同一個 `SlashFlipbookURP` shader**，差別只在 blend（普攻是 Additive One/One、R 大招是 premult One/OneMinusSrcAlpha）和 renderMode（普攻 Mesh 貼 forward、大招 Billboard）。

- **測試**：EditMode 236，唯一失敗仍是既有無關 `...FallsBackToAttack3`。無新測試（HealingSpring 重寫靠實測 + player 行為不變推導；damage 是純資產值）。

## 2026-08-31 (追加77) — 貓 R 500 傷、血刀銜接 player 右手、換掉 player 大招周邊特效

使用者:「1.car r 技能可造成500傷害 2.將 blood_katana_retextured.glb 此武士刀銜接在 player 右手上握著，幫我調整尺寸 3. 不要出現人物_單純的周邊特效_請重新生成.mp4 將這個周邊特效取代 player 原本能量滿格時的特效，同樣處理透明通道和彷3d」。

### 1. 貓 R 技能（黑暗劍氣）改 500 傷

- `Assets/_Project/Settings/Combat/Cat/CatDarkQi.asset` `damage` **120 → 500**。純資產值，`CatUltimateAbility` / `AttackResolver` 邏輯不變。
- **待實機平衡**：500 傷 / 3.2m AOE / 20s 充能。

### 2. 血刀銜接 player 右手（取代 Genshin「Wolf's Gravestone」）

- 素材 `blood_katana_retextured.glb` → `Assets/_Project/Characters/Weapons/BloodKatana/BloodKatana.glb`（4 材質 Base/Blader/Hilt/Wrapping、`Shader Graphs/glTF-pbrMetallicRoughness`；GLB 匯入 renderer bounds 退化成 (0,0,0)，setup 會重算 mesh bounds）。
- 新 `PlayerKatanaSetup.cs`（選單 `Tools/Live2DAction/Attach Blood Katana To Player Hand`，可重跑）取代 `Player5WeaponSetup.cs`：
  - 掛到 `Rhand_Weapon2` 骨頭底下。結構 `Rhand_Weapon2 / WolfsGravestone(wrapper) / BladeMesh(GLB)`。
  - **wrapper 仍叫 "WolfsGravestone"**：`UltimateAbility.cs` 靠這個名字找要拋的武器（`WeaponObjectName` const / `FindWeapon()`），保留名字 = R 大招拋劍序列**零程式碼改動**照常運作。實測 player 的 `UltimateAbility.FindWeapon()` 正確回傳新 wrapper。
  - **為什麼要 wrapper**：`UltimateAbility.ThrowSequence` 假設拋的 transform pivot 在刀尖、local −Y 指向刀尖（它用 `FromToRotation(Vector3.down, flightDirection)` 對齊）。GLB 自己的 pivot 在刀身中央、刀身沿 mesh −X。wrapper 把 pivot 移到握把/拳心、local −Y 對齊刀身朝刀尖；`BladeMesh` 帶固定 offset 讓握把落在 wrapper 原點、刀身沿 wrapper −X。
  - wrapper `localRotation` + `BladeMesh` offset 是**手工調的權威值**（刀握在右拳、刀身朝前下傾 ~19°，「持刀垂下待機」），不是公式；同攝影機 distance / stepOffset 的「未經使用者許可別改回公式」地位。刀長 world ~0.96m（`BladeMesh.localScale` 0.00075 在 ~80× 手骨底下）。
  - `Player/WolfsGravestone`（原本掛在 Player root、被 R 大招拋的**那把背劍**）已移除。
- **舊 Genshin 佔位仍在磁碟上**（`Assets/_Project/Characters/Placeholder/Weapons/WolfsGravestone/` + `Player5WeaponSetup.cs`），`DoNotShipBuildGuard` 那條沒動（新血刀在 `Characters/Weapons/` 不在 `Placeholder/` 下、不會誤觸）。**待使用者確認**血刀來源授權後再決定是否連同 `Player5WeaponSetup.cs` 一起刪。
- **待實機**：刀在拳心的位置/角度/大小；刀刃平面朝向（wrapper roll 是 `FromToRotation` 給的任意 roll，可能刀刃朝向怪）；R 大招拋刀動畫用新 wrapper 的表現。

### 3. player 大招周邊特效換成火焰光柱（取代「劍體環繞」SwordOrbit）

- 素材 `不要出現人物_單純的周邊特效_請重新生成.mp4`（1280×720、H.264 **無 alpha**、24fps、240 幀）—— 橘＋青能量絲旋成漩渦上升、成火焰光柱、地面光環、黑底、右下小星星浮水印。歸檔 `Assets/_Project/VFX/Skills/PlayerUltimateAura/Source/PlayerUltimateAuraSource.mp4`（僅重製用，執行期不播）。
- **透明通道**：ffmpeg 離線烘 `PlayerUltimateAura_Atlas.png` —— 抽第 4~172 幀每 3 幀共 **56 幀**、`drawbox` 塗掉右下浮水印、`scale 320:180`、`tile=8×7`（2560×1260）、`geq` 由亮度算 alpha（`a = 255·pow(clip((max(rgb)−46)/150,0,1), 1.28)`；門檻拉高到 46 把黑底的微弱塵點/煙霧鍵乾淨、只留亮火焰，第一版門檻 22 上半截留了一片洗白的半透明卡）。完整 recipe 在 `PlayerUltimateAuraVfxSetup.cs` 檔頭。
- **仿3d（2.5D）**：新 `PlayerUltimateAuraVfxSetup.cs`（選單 `Tools/Live2DAction/Add Player Ultimate Aura VFX (R ultimate cast)`，可重跑，照 SwordOrbit 那套）建 `PlayerUltimateAuraVFX.prefab` —— 主體 billboard flipbook quad（`SlashFlipbookURP`、premult One/OneMinusSrcAlpha、`_Brightness` 1.7）＋ 子粒子 `Embers`（暖色火花上升、World space、cone 向上）＋ `GroundFlash`（地面水平 billboard 閃光盤，紮住光柱腳）。`SlashVfxController` 自動回收（~1.3s）。
- **接線**：`UltimateAbility.castVfxPrefab` → 新 prefab、`castVfxLocalOffset` (0, 1.3, 0)（`SizeHeight` 4.6，地面光環對齊腳底、火焰過頭頂）。
- **刪除**：`Assets/_Project/VFX/Skills/SwordOrbit/`（整個資料夾：atlas / mat / prefab / Source/SwordOrbitSource.mp4）＋ `Assets/Editor/Bootstrap/SwordOrbitVfxSetup.cs`。`CatDarkQiVfxSetup.cs` / `UltimateAbility.cs` 裡提到 `SwordOrbitVfxSetup` 的註解改指 `PlayerUltimateAuraVfxSetup`（`CatDarkQiVfxSetup` 只有註解關聯、無程式碼相依）。
- **實測**：menu 跑過、prefab 結構 + material blend/shader/atlas + 接線都驗過；場景視圖對天空 simulate 預覽有渲染（地面亮火焰 + 地閃 + 火花，上半截殘留一點淡煙霧霾——同 SwordOrbit「低角度對開闊天空會看到淡卡」的老註記）。game-view MCP 截圖抓不到粒子（同追加69/75）。
- **測試**：EditMode **236**，唯一失敗仍是既有無關 `CharacterAttackAnimationLinkTests...FallsBackToAttack3`。VFX / 武器銜接純視覺無單測。
- **待實機**：火焰光柱在實際戰鬥場景 + bloom 的觀感（上半截淡霧要不要再把 alpha key 推更兇 / 調 `_Brightness`）；`SizeHeight` 4.6 / offset (0,1.3,0) / 壽命 1.3s；施放瞬間跟拋刀 spin-up 的時間對齊。

## 2026-08-31 (追加78) — 移除 player 普通攻擊特效（改揮刀前置）＋ R 大招特效加上音效

使用者:「1.移除 player 普通攻擊相關特效，我要將他的攻擊手段從拳頭改為揮刀，後續要做成像隻狼那樣的對打機制 2. player 和 cat 的 r 大招特效來源資產應該是有音源的，把音效也加入綁定在特效上」。

### 1. 移除 player 普通攻擊特效

- **背景**：player 現在握著武士刀（追加77），使用者要把近戰從拳頭（Mixamo CrossPunch/HookPunch/Uppercut → Attack1/2/3）改成揮刀，之後做成隻狼式對打（拚刀/彈反）。**這則只做「移除特效」**——動畫替換與對打機制是後續，另開。
- `LightAttack1/2/3.asset`：`hitEffectOverride` 清空（原本各指向 `Attack01/02/03.prefab` 的動漫劍氣 flipbook）、`alwaysSpawnHitEffect` 0（原本 1，打空也放）。落地命中仍會有 `PlayerCombat.hitEffectPrefab` 那顆共用小火花（沒動——那是命中回饋不是「攻擊特效」，且敵人共用）。
- **這三顆 AttackData 是 Player / TrainingDummy / 中立者1-3 共用**，所以那幾隻的劍氣也一起沒了（一致，都是 player-clone）。敵人（`EnemyAttack3`/`GiantAttack3` → `Attack3SlashEffect.prefab`「X 斬」）**不受影響**。
- **刪除**（僅 player 專屬、無其他引用）：`Assets/_Project/VFX/Slash/Prefabs/`（Attack01/02/03）、`Materials/Attack01/02/03Mat.mat`、`Materials/SoftDotAlphaMat.mat`（刪 Attack0N 後變孤兒）、`Textures/Attack01/02/03_SpriteSheet.png`、`Source/SlashSourceVideo.mp4`、`Editor/Bootstrap/SlashVfxSetup.cs`、`Game/VFX/SlashVfxSpawner.cs`（無場景引用的預留類）。
- **保留**（敵人 / R 大招 VFX 共用）：`Attack3SlashEffect.prefab` + `SlashCrescent.mat` + `T_SlashCrescent_*`、`Attack3SlashEffectSetup.cs`（+ `Attack3SlashBackgroundCleaner`/`Attack3SlashFrameAtlasBuilder`）、`SoftDotAdditiveMat.mat` + `SoftDot.png`、`SlashVfxController.cs`、`SlashFlipbookURP.shader`。
- `Attack3SlashEffectSetup.cs` 改名選單 `Rebuild Enemy Attack3 Slash Effect`、**拿掉 `WireToLightAttack3`**（不再碰 player 的 AttackData，只重建敵人用的 prefab），避免重跑時把 player 特效加回來。
- **後續（未做，另開）**：拳頭動畫 → 揮刀。專案裡已有 `_Project/Characters/Placeholder/CombatAnimations/TC_Sword_Free_Pack/`（`KBS_Sword_ATK_Combo_01` 等）可 retarget，不用再下載。隻狼式拚刀/彈反機制是更後面。

### 2. R 大招特效綁定音效

- 兩支來源影片（`PlayerUltimateAuraSource.mp4` / `DarkSwordQiSource.mp4`）都內含 **AAC 立體聲軌**（使用者提醒的）。使用者自有 AI 生成素材，音軌同屬使用者自有（rule 1 的「原創」——非仿製既有商業作品），登記進 `ASSET_LICENSES.md`。
- ffmpeg 離線抽音 + 裁切 + 淡入淡出 + `loudnorm`：
  - `PlayerUltimateAura_Cast.wav`（4.6s，火焰上升的 whoosh/roar；`-ss 1.0 -t 4.6`）
  - `CatDarkQi_Cast.wav`（2.9s，暗能量蓄力→劈砍→衰減；`-ss 3.25 -t 2.9`，**input seek**——這支檔 output seek 會抽出數位靜音）
  - 存 `Assets/_Project/Audio/Skills/`。完整 recipe 在各自 `*VfxSetup.cs` 檔頭。
- 兩個 VFX prefab（`PlayerUltimateAuraVFX` / `CatDarkQiSkillVFX`）root 加 `AudioSource`：`playOnAwake`、3D（`spatialBlend 1`，跟著施法者定位）、`volume 0.85`、log rolloff 4~50m、`dopplerLevel 0`。prefab 一 Instantiate（大招施放）就播。
- `SlashVfxController` 改：`Destroy(gameObject, ...)` 的存活時間現在也把 `AudioSource.clip.length`（除以 pitch）算進去 → 音效比視覺長時（火柱 roar 拖尾）不會被切掉。null-safe，沒 AudioSource 的舊 prefab 不受影響。
- 由 `PlayerUltimateAuraVfxSetup.cs` / `CatDarkQiVfxSetup.cs` 新增 `ConfigureCastAudio()` 接線，重跑選單即套用。

- **測試**：EditMode **236**，唯一失敗仍是既有無關 `CharacterAttackAnimationLinkTests...FallsBackToAttack3`。無新單測（VFX/音效純資產＋接線；`SlashVfxController` 改動靠 null-safe 推導 + 既有測試不回歸）。
- **待實機**：音效音量/3D 衰減/裁切長度是否合施法節奏；player 現在普攻無揮擊特效（預期，改揮刀前的中間狀態）。

## 2026-08-31 (追加79) — player R 火焰特效改成「必殺可用」的常駐待命光環（非施放特效）

使用者:「1.這個 player r 技能的周邊特效，不是施放時才出現，而是滿格能量條...代表必殺技已可使用的狀態，然後將這個特效的大小對齊 player 體型和位置(從腳底到頭部)，音效部分切掉後半段」。

**背景**：追加77/78 把 `PlayerUltimateAura` 火柱接在 `UltimateAbility.castVfxPrefab`（施放瞬間 spawn 一次）。使用者澄清它應該是**能量滿格 = 必殺可用**的**常駐待命指示**，跟場上早就有的 `UltimateReadyAura` 閃電（2026-08-16，`energy.IsFull` 時繞圈的奇犽風閃電）同一個機制。

### 角色

- `UltimateReadyAura.cs`（`Live2DAction.UI`）加 `[SerializeField] GameObject flameAura`。`Update` 在**跟閃電完全相同的 `energy.IsFull` 條件**下 `SetActive(true/false)` 這個火焰子物件。閃電 bolt 保留——現在滿格時**閃電 ＋ 火焰兩層一起亮**。`flameAura` 留空 = 只有閃電，行為不變。guard 從 `energy==null || bolt==null` 改成 `energy==null`（讓火焰不依賴 bolt）。
- `UltimateAbility.cs`：**移除 `castVfxPrefab` / `castVfxLocalOffset` 欄位 + 施放時的 `Instantiate` 區塊**（這特效不再是施放特效；施放瞬間的「霸氣」還是 `UltimateActivationBurst` 那條 line-renderer 衝擊波）。場景 YAML 的 stale key 下次序列化自動掉。
- `PlayerUltimateAuraVFX.prefab` 改成**常駐迴圈**：主 flipbook `loop=true` + 連續發射（rate `2/Lifetime`、maxParticles 4、粒子彼此錯相位靠 colorOverLifetime 淡入淡出無接縫）；`Embers` `loop=true` 連續、Local space（貼著移動中的 player 不拖尾）；`GroundFlash` → `GroundRing`（每個 loop 一次脈動的地環）。**拿掉 `SlashVfxController`**（那會自毀，常駐光環不需要）。

### 尺寸「對齊 player 體型（腳底到頭部）」

- player 身體在 Player transform local space：**腳底 ~Y 0.05、頭頂 ~Y 0.83**（沿用閃電光環 `baseHeight 0.05` + `climbHeight 0.78` 那組**使用者手調的權威值**；`SkinnedMeshRenderer.bounds` 被翅膀撐大不可靠）。
- 量 atlas alpha：滿焰格的火從 cell 底部 **8%** 起（火根/地環）到 **80%**（濃焰頂）。→ `SizeHeight 1.1` + `AuraLocalOffset (0, 0.5, 0)`：實測火根落在 local Y **0.038**（腳底 0.05）、濃焰頂 **0.830**（頭頂 0.83，剛好）、飄焰尖 0.95（頭頂略上）。`SizeWidth = SizeHeight × 1.4`（≈1.54，比 16:9 窄一點，免得繞在矮角色身上顯得瘦長）。
- Embers 速度/壽命/cone 縮小（0.35~0.9 m/s、0.4~0.8s、cone radius 0.22）讓火花不衝出頭頂；GroundRing 直徑縮到 ~0.45~0.6。

### 音效「切掉後半段」

- `PlayerUltimateAura_Cast.wav`（4.6s）→ 刪。新 `PlayerUltimateAura_Ready.wav`（**2.3s**，前半段，`-ss 1.0 -t 2.3` + 淡入淡出 + loudnorm）。
- 掛在火焰子物件 root 的 `AudioSource`（`playOnAwake`、3D、vol 0.8、**loop=false**）→ 每次子物件 `SetActive(true)`（能量剛滿）播一次「充能完成」stinger，不是常駐 ambience。
- `SlashVfxController` 追加78 那個「自毀延時也算 `AudioSource.clip.length`」的改動保留（cat R 還在用），player 火焰現在沒 `SlashVfxController` 了不受影響。

### 接線

- `PlayerUltimateAuraVfxSetup.cs` 選單改名 `Add Player Ultimate Ready Aura VFX (flame, on full energy)`。`Apply` 建 material + prefab → `WireReadyAura`：在 Player 底下（重）建一顆 inactive 子物件 `ReadyFlameAura`（localPos `(0,0.5,0)`）→ 填 `UltimateReadyAura.energy` + `.flameAura`。可重跑。

- **測試**：EditMode **236**，唯一失敗仍是既有無關 `CharacterAttackAnimationLinkTests...FallsBackToAttack3`。無新單測（純 VFX/音效 + `UltimateReadyAura` 的 toggle 是既有 pattern 加一層）。
- **待實機**：閃電＋火焰兩層同時亮會不會太滿（要的話一句話拿掉閃電）；火焰尺寸/位置對真實 play 姿勢（走路/待機骨架位移）；音量/裁切；game-view MCP 截圖抓不到粒子、scene-view 程式化取景這次也不配合，尺寸是照 atlas alpha 剖面 + 使用者腳底/頭頂校正值**解析對位**的（數學驗證：火根 0.038 / 濃焰頂 0.830）。

## 2026-08-31 (追加79 續) — 火焰待命光環改大 + 修「只有上半身」+ 前後兩層包住全身

使用者回報:「角色特效火焰目前只有上半身且範圍小，沒有做到從腳底開始往上覆蓋全身」。

- **根因 1：feet/head 校正值是舊的**。追加79 初版沿用閃電光環的 `baseHeight 0.05` / `climbHeight 0.78`——那是**舊的、比較矮的 player rig** 的數字。實測現在的 Player5 骨架（`Bip001-Toe0` / `Bip001-Head` / skin renderer 頂）：**腳趾 local Y ≈ −0.58（world 0.50）、頭頂 ≈ +0.70（world 1.78）**，身高 ~1.28。初版把火焰放在 local 0.5 附近 = 角色腰部以上。改成 `SizeHeight 2.5` + `AuraLocalOffset (0, 0.42, 0)`（照 atlas alpha 剖面：滿焰格的火占 cell 高 ~10%~60%，把這段對到 feet→head）。
- **根因 2：單一 billboard 只能貼在身體一側**。camera-facing billboard 放在身體中心，正面看時下半身被角色網格擋掉、只露出頭頂和腰部那圈亮光 →「只有上半身」。修法：**兩層 flipbook**——
  - **back 層**（root PS，`_ZTest 4` LEqual 正常深度）：正面看被身體擋在後面。
  - **front 層**（新 `FrontFlame` 子物件，`_ZTest 8` Always，`SlashFlipbookURP` 新增 `_ZTest` 浮點屬性、預設 4 對既有材質零影響）：畫在角色網格**之上**，較淡（peak alpha 0.55）、略小（0.85×）。
  - 兩層合起來 = **任何鏡頭角度火焰都包住身體前後**。用 game camera（billboard 面向的那台）算圖驗證：正面/背面/3⁄4 都是火焰從地面裹到頭頂。
- **其他**：flipbook 只循環 sheet 尾段（`LoopStartFraction 0.72`，frame ~40~55 的成形滿焰，不再從火花 build-up 脈動）；兩粒子錯半個 lifetime + 三角 fade → 無縫循環、亮度不疊加爆白；`_Brightness` 1.7→**1.0**；`SizeWidth` 1.4×→**0.9×**（atlas 地面輝光占 cell 寬 ~57%，1.4× 會甩出一條寬亮帶）；Embers 速度/壽命放大回覆蓋 1.28 身高、從腳底發射；GroundRing 縮小。
- **診斷踩坑（記一下）**：用臨時 camera `cam.Render()` 到 RenderTexture 算圖時，**billboard 粒子朝向的是 `Camera.main` 不是那台臨時 camera**——從別的角度拍就會看到 billboard 近乎側面 = 一條假的水平亮帶，一度以為是 bug。要驗 billboard VFX 必須**借 game 的 Main Camera** 移位算圖。scene-view 的程式化取景 / `manage_camera` 的 scene_view 截圖這台機器上都不吃 `view_position`，game_view 截圖又抓不到粒子。
- **測試**：EditMode **236**，唯一失敗仍是既有無關 `...FallsBackToAttack3`。shader 加 `_ZTest` 屬性對 cat DarkQi 材質零影響（reimport 後 `_ZTest=4`=舊行為）。
- **待實機微調**：腰際仍有一條淡黃色橫帶（atlas 地面輝光被 billboard 寬度攤開）、小腿以下火焰較弱、整體強度/GroundRing 大小——都是 `PlayerUltimateAuraVfxSetup.cs` 常數一行可調、重跑選單即套用。

## 2026-08-31 (追加81) — 待命光環「完全照資產外觀」＋ 移除奇犽風閃電

使用者:「上次做到 player 能量條滿格時的特效，要完全顯示資產來源的特效外觀，並移除舊特效(白色一圈的那)」。尺寸取向：「忠於來源比例，略大於角色」。

### 移除舊閃電（「白色一圈的那」）

`UltimateReadyAura` 從 2026-08-16 起除了火焰還驅動一條奇犽風繞圈電光（藍白 `LineRenderer`，bloom 下看起來就是「白色一圈」）。整條移除：

- `UltimateReadyAura.cs` 砍掉 `bolt` 欄位與全部電光幾何邏輯，只剩「`energy.IsFull` → `flameAura.SetActive`」。guard 改 `energy==null || flameAura==null`。
- 刪 `UltimateReadyAuraSetup.cs`（電光建置選單）、`LightningAuraUtility.cs` ＋ `LightningAuraUtilityTests.cs`（16 個單測）、`Assets/_Project/VFX/LightningBolt.mat`、場景裡的 `Player/UltimateReadyAura` 子物件（含 `Bolt`）。
- `UltimateActivationBurst`（R 施放瞬間的金色衝擊環）**保留**——那是施放特效不是待命特效。
- 選單 `Add Ultimate Ready Lightning Aura` 隨之消失。

### 火焰「完全照資產外觀」——把加上去的裝飾全拆掉

追加79/80 在來源影片上疊了一堆自製東西：第二層 `_ZTest=Always` 的 front billboard（0.3 alpha）、手做的上升火花 / 脈動地環粒子、`_Brightness` 1.3、以及一個把來源外圈地環切掉的中央裁切。全部拿掉——現在 prefab 就只有**一個 billboard flipbook**播來源影片：

- **atlas 重烤**（`PlayerUltimateAura_Atlas.png`）：完整未裁切 1280×720 frame，只取穩定火柱段（source 72..231 每 3 格 = **54 frames**，8×8，2560×1440）；`drawbox` 塗掉角落 ✦ 浮水印；亮度鍵 threshold **52**（更低會讓地環之間的暗焦土鍵成半透明棕色板 / 再低會整片殘留 alpha 糊住畫面）、range /60、gamma 0.80 → 火焰實心、細地環＋藍舌保留。recipe 在 `PlayerUltimateAuraVfxSetup.cs` 檔頭。
- **prefab**：單層 billboard、`_ZTest=Always`（角色網格永不裁切它）、premult alpha（shader 自己 premultiply）、`_Brightness` 1.2（只夠吃到 URP Bloom 讓它像火，不改色不改形）。無 Embers / GroundRing 子粒子——來源影片自帶地環、藍舌、火花。兩粒子錯相位 ＋ 三角 alpha fade 仍用來抹平 flipbook 迴圈接縫（只是接縫平滑，不是外觀處理）。
- **尺寸「忠於來源比例，略大於角色」**：不再把火柱壓成身高。`SizeHeight 2.7`（可見火焰 ~1.9m，尖端略過頭）、`SizeWidth = SizeHeight × 1280/720`（不變形，地環維持圓形，~3.8m across——那就是來源相對火柱的比例）、`AuraLocalOffset.y 0.31`（地環落在腳底）。
- 音效 `PlayerUltimateAura_Ready.wav` 不動。

- **測試**：EditMode **220**（少了 16 個 `LightningAuraUtilityTests`），唯一失敗仍是既有無關 `CharacterAttackAnimationLinkTests...FallsBackToAttack3`。
- **待實機**：`cam.Render()` 到 RenderTexture 的算圖這輪一直吐 tonemap 爆掉的糊白圖（HDR RT / 非 HDR RT 都試過），沒能用截圖目視收尾——atlas 內容已用 ffmpeg 合成在深/淺底逐格檢查過（火柱、細地環、藍舌、火花都在、浮水印已除）。尺寸/位置（2.7m 高、地環 3.8m）、`_Brightness`、迴圈接縫平滑度都是 `PlayerUltimateAuraVfxSetup.cs` 一行常數、重跑選單即調。

### 追加81 續 — 復原 R 施放特效（劍體環繞 SwordOrbit）

使用者:「player 施展 r 技能原來的特效不見了 就是一把劍的旋轉砍擊(我不是說大劍)」。

- **背景**：`不要有人形.mp4`（一把長劍＋藍/橘能量絲像行星環繞著它旋轉）追加69 接在 `UltimateAbility` 當 R 施放特效 → 追加77 被火焰取代 → 追加79 火焰移去待命，R 施放就只剩 `UltimateActivationBurst` 衝擊環，那個劍體環繞特效整個沒了。使用者要它回來。它是**施放特效**（按 R 當下 spawn），跟待命火焰光環是兩回事（火焰=「必殺充能好了」，這個=真的施放時）。
- 源檔仍在 `~/Downloads/3d遊戲資源/不要有人形.mp4`，重新歸檔到 `Assets/_Project/VFX/Skills/SwordOrbit/Source/SwordOrbitSource.mp4`。
- **透明通道坑**：這支不是純黑底，是**烘進 RGB 的灰色透明棋盤格**（AI VFX 產生器慣例）。亮度鍵 threshold 拉到 **60**（黑底只要 ~30）才能把 ~luma 55-70 的淺格清掉；range /165、gamma 1.4 保住藍/橘光絲＋幽靈劍。`drawbox` 塗掉角落浮水印。source 33..177 每 3 格 = **49 frames**、8×7、2560×1260。recipe 在 `SwordOrbitVfxSetup.cs` 檔頭。
- **prefab** `SwordOrbitSkillVFX.prefab`：billboard flipbook（`SlashFlipbookURP` premult、`_Brightness` 1.7 吃 Bloom）＋ `Sparks`（藍白火花 burst）＋ `GlowPulse`（中央輝光脈動）＋ root `SlashVfxController`（播完自毀，~1.6s）。`SizeHeight 3.0` / `SizeWidth ×320/180`。
- **`UltimateAbility.cs`**：重新加回 `castVfxPrefab` / `castVfxLocalOffset`（追加79 移除的），在 `energy.Consume()` + `burst.Play()` 之後 `Instantiate` 一次，parent 到 player，offset `(0, 0.4, 0)`（胸口高度）。
- **`SwordOrbitVfxSetup.cs`** 新選單 `Add Sword Orbit Skill VFX (R ultimate cast)`，建 material + prefab + 接 `UltimateAbility.castVfxPrefab`，可重跑。
- 無音效（追加69 原版也沒有；要的話同 CatDarkQi 的做法一句話補）。
- **測試**：EditMode 220，唯一失敗仍是既有無關 `...FallsBackToAttack3`。borrow-Main-Camera 算圖這次成功（PP volume 關掉後不爆白）——確認藍/橘光絲環繞幽靈劍、棋盤格已鍵乾淨、位置在角色上半身。offset/size 待實機微調（`SwordOrbitVfxSetup.cs` 常數）。

### 追加81 續 2 — 貓 R「黑暗劍氣」修「掉漆」（chroma 去背 + 拿掉裁切）

使用者:「為什麼感覺 `幫我生成一個黑暗劍氣風格的版本.mp4` 掉漆很嚴重 是因為透明通道或 3d 問題嗎」。

- **診斷**：主因是**透明通道**。這支跟 `不要有人形.mp4` 一樣是**烘進 RGB 的灰色透明棋盤格**背景，但 `CatDarkQiVfxSetup` 追加75/80 的 alpha 鍵用**亮度**、門檻只有 **28**——遠低於棋盤格淺方塊（~luma 74），棋盤格 + 暗部 H.264 壓縮雜訊全漏進來變一層**灰濁半透明霧**，亮場景下特別慘。加乘：素材本身暗紅/暗紫暗色調，premult 疊在超亮 greybox 上幾乎看不見（為暗場景 + bloom 設計）。追加75 的 `crop=720:720:400:0` 又把外圈漩渦環切掉。**不是 3D 問題**。
- **修法**：改用 **chroma（彩度）去背**。棋盤格是純灰（chroma == 0），特效是暗紅/暗紫（chroma 40~200）→ `alpha = max( (chroma−10)/70 , (maxc−92)/120 )`：第一項鍵彩色特效、棋盤格自動消失；第二項留住近白火花核心（92 > 棋盤格 74 上限故乾淨）。gamma 0.9。
- **拿掉中央裁切**：現在整張 1280×720 frame，`drawbox` 塗掉角落浮水印。8×8、320×180 cell、2560×1440、source 21..210 每 3 格 = 64 幀（全程：符文劍成形→暗紅漩渦→紫爆→消散）。缺點：結尾灰色煙塵是低彩度、鍵得比較淡——可接受（不是重點畫面）。recipe 在 `CatDarkQiVfxSetup.cs` 檔頭。
- `_Brightness` 1.6 → **2.0**（material + `SlashVfxController`），暗素材拉亮一點在非暗場景也讀得出來、吃到 Bloom。`SizeWidth` 改回 `SizeHeight × 320/180`（cell 從 720 方形變 16:9，不壓扁漩渦）。atlas import `maxTextureSize` 2048 → 4096。
- 貓 R 的 AOE / 傷害 / 能量 / 機制**完全沒動**——純資產 + 材質。
- **驗證**：ffmpeg 把新 atlas 合成在橘色 + 深色底逐格檢查——棋盤霧完全消失、全寬漩渦、暗紅/暗紫/紫爆都在、浮水印已除。borrow-Main-Camera in-engine 算圖確認 greybox 地面上不再有灰霧。EditMode 220，唯一失敗仍是既有無關 `...FallsBackToAttack3`。

### 追加81 續 3 — Player 武器：修武士刀被剔除消失 ＋ 狼末大劍放回背上當裝飾

使用者:「1. player 背上那把 wolf 大劍裝飾品消失了 2. katana 武士刀應該被握在 player 右手手上，將來會以此武器為核心進行揮劍動作設計」。

- **武士刀「消失」根因**：血刀 `BloodKatana.glb` 其實一直掛在右手（`Rhand_Weapon2/WolfsGravestone/BladeMesh`），但 glb 匯入時 mesh bounds 是退化的 (0,0,0)，`PlayerKatanaSetup` 只在編輯器 `RecalculateBounds()`（不會存下來），**執行期沒有 `MeshBoundsFixer`** → 一進 Play mode 手不在畫面正中央整把刀就被視錐剔除。修法：`PlayerKatanaSetup` 在 wrapper 上加 `MeshBoundsFixer`（同空島/貓用的那顆 `[ExecuteAlways]` 元件），並補進現有場景。
- **狼末大劍**：追加77 的 `PlayerKatanaSetup` 會刪掉任何叫 `WolfsGravestone` 的物件——使用者 2026-08-23 手擺在背上的那把就是這樣沒的。新增 `PlayerBackGreatswordSetup.cs`（選單 `Attach Wolf's Gravestone As Back Decoration`）：把 `Genshin_WGS.fbx` 掛在 **Player root**（直接掛、scale 1，不掛脊椎骨——骨骼有 ~80x lossy scale 會把 localPosition 乘爆甩飛數公尺，第一版就踩到）、**命名 `BackGreatswordDecor`（不叫 WolfsGravestone）** 所以 R 大招不會丟它、接上 **兩顆** `ThirdPersonCameraController.firstPersonHiddenAccessory`（Main Camera + CatCamera 各一顆，原本只接到第一個 iterate 到的）、自帶 `MeshBoundsFixer`。
- **追加81 續 4**：使用者「檢查幾個版本前的大件裝飾品是如何擺放...應該是劍柄左上刀劍右下」。從 git d735761 / 8ecb5fb（0830 前 commit）撈出使用者 2026-08-23 手調的 transform 原封不動還原：`localPosition (1, −0.80115217, −0.2)`、`localRotation Euler(0,0,43)`、`localScale 1`。FBX 的握把在模型 +Y 端（mesh `pCylinder5` local Y≈2.37，不是原點；原點端是刀尖），所以 Euler 43° 把握把甩到左肩上方、刀尖落右下——正是「劍柄左上刀劍右下」。borrow-Main-Camera 算圖確認。
- **R 大招**：仍丟右手的 `WolfsGravestone`（＝武士刀，戰鬥核心武器）。背上大劍純裝飾、不丟。
- ⚠ **`Genshin_WGS.fbx` 是《原神》「狼的末路」的直接仿製品，DoNotShip**（`DoNotShipBuildGuard.cs` + `ASSET_LICENSES.md` 已擋）。只能當內部原型佔位，絕不進任何對外 build（CLAUDE.md 規則 2）；日後要換原創大劍模型。
- **驗證**：borrow-Main-Camera 算圖確認武士刀在右手（不再被剔除）、大劍斜背「劍柄左上刀劍右下」。EditMode 220，唯一失敗仍是既有無關 `...FallsBackToAttack3`。

## 2026-08-31 (追加82) — 4 個 Meshy 校園建築 FBX 排除版控（>100 MB）

使用者:「我的專案中好像有超過 100mb 的檔案導致我無法 git」→ 確認後要求「創建說明文件記錄這些大型資產，然後 git 排除他們」。

- **問題**：追加63/64/66 匯入的 4 個 Meshy 校園建築原始 FBX（內嵌貼圖版）單檔 108~130 MB，全部超過 GitHub 的 100 MB 單檔硬上限。當時還在暫存區、**尚未 commit**（`git log` 確認歷史裡無 ≥50 MB blob，`main` 與 `origin/main` 齊平），所以**不需要改寫歷史**。
  - `ModernGlassLibrary/.../Meshy_AI_Modern_Glass_Library_0830064510_texture.fbx` — 108 MB / 294 萬三角面
  - `PalmLinedLibrary/.../Meshy_AI_Palm_Lined_Library_En_0830064603_texture.fbx` — 130 MB / 304 萬三角面
  - `QuietCampusPlaza/.../Meshy_AI_Quiet_Campus_Plaza_0830071958_texture.fbx` — 106 MB / 287 萬三角面
  - `YuanpeiUniversityBuilding/.../Meshy_AI_Yuanpei_University_Bu_0830053851_texture.fbx` — 120 MB / 309 萬三角面
- **烘 Unity `.mesh` 不可行**：實測把 ModernGlassLibrary 網格 `AssetDatabase.CreateAsset` 成 `.mesh` = **216 MB**，比 FBX 還大（獨立 `.mesh` 資產不壓縮）。`QuietCampusPlaza_NoFoliage.mesh` 之所以只有 55 MB，是因為已被減面成 54.5 萬頂點 / 95.5 萬三角面（原始約 1/3、去植被、剝切線）——`.mesh` 路線一定要先外部減面。
- **處理**（本次）：
  - 新增 `Docs/LARGE_ASSETS.md`：登記這 4 個檔的 repo 路徑、大小、面數、外觀、原始 zip 檔名、補回方法、長期正解（回 Meshy 下載低面數版 / Blender decimate）。
  - `.gitignore` 加規則 `Live2DAction/Assets/_Project/Environment/Meshy/**/Meshy_AI_*_texture.fbx`（＋ `.fbx.meta`）——只擋 `.fbx` 網格本身。
  - `git rm --cached` 這 4 個 `.fbx` ＋ 對應 `.fbx.meta`（檔案留在本機硬碟，Unity 照常能用）。
- **沒動到**的：同資料夾的 `*_texture.png` / `*_normal.png` / `*_metallic.png` / `*_roughness.png` 貼圖、`Materials/*.mat`、`QuietCampusPlaza_NoFoliage.mesh`（55 MB，過得了 100 MB）全部照常進版控。fresh clone 後材質與貼圖都在，只有這 3 棟（QuietCampusPlaza 有減面 mesh 例外）的**網格**會缺、場景對應物件顯示 missing mesh——本機開發不受影響。
- 排除後暫存區最大單檔 56 MB（`QuietCampusPlaza_NoFoliage.mesh`），總量 ~158 MB，可正常 push。
- **TODO**（見 `KNOWN_ISSUES.md`）：這 4 個模型各 ~300 萬三角面，遊戲裡本來就不能直接用，需回 Meshy 下載低面數版或減面後才重新進版控。

## 2026-08-31 (追加83) — 十足蟲.glb → 可戰鬥的高速近戰敵人（TenLeggedBugController）

使用者:「將十足蟲.glb 實作為可戰鬥的高速近戰敵人」＋一份完整規格（骨頭編號 / 巡邏追蹤攻擊搜尋硬直死亡六態 / 程序步態 / 犀牛角刺擊 / 3 連段後硬直 / 翻腹淡出死亡 / 全數值 Inspector）。

### 先檢查（沒重造衝突系統）
- **傷害**：沿用 `Core.IDamageable` + `DamageInfo(amount,point,dir,source)` + `Core.Health`（`maxHealth`、`Damaged`/`Died` 事件、`deferDeactivationToDeathAnimation`）。玩家受擊點 = `Player/PlayerHurtbox`(CapsuleCollider trigger + `HurtboxLink`)。**專案無專屬 layer**，靠 `IDamageable` + `BossTeamMember.Team` 過濾。
- **敵人 hitbox 慣例**：`Combat/Boss/BossHitbox`（trigger 預設關、`Activate/Deactivate`、kinematic Rigidbody、每 FixedUpdate swept-cast、per-activation `HashSet` 去重）。
- **移動/NavMesh**：專案**唯一**移動系統 = `CharacterController` + 手動 `.Move()`（**無 NavMeshAgent**）。`AI/NavPathFollower`（agent-less、`SteeringDirection(targetPos)` 繞障、fail-open）。NavMesh 已 baked。
- **程序動畫**：`Characters/CatProceduralWalk`（capture rest localRotation → `ApplyHinge` 相對 root right 軸 → 速度縮放 → pure helper）——十足步態照這架構。
- **死亡淡出**：專案**沒有**現成溶解工具（`DeathAnimationLink` 只延遲 SetActive、需 Humanoid clip）。自己在控制器做。

### 新增檔案（全新，未改既有遊戲邏輯 / 玩家 / 敵人）
- `_Project/Game/AI/TenLeggedBugController.cs` — 主控制器。六態 `BugState`（Patrol/Chase/Attack/Search/Stagger/Death）。所有數值（偵測/脫離/攻擊距離、追蹤/巡邏/搜尋速度、轉向速度、重力、巡邏半徑、步態頻率/幅度、攻擊夾角 30°、攻擊循環 1s、角三段時間 0.25/0.45、刺擊命中窗、角傷害 10、連段數 3、硬直 0.7–1s、搜尋 2–3s、翻腹 0.5–0.8s / 屍體 1s / 淡出 1–1.5s）皆 `[SerializeField]`（同 `EnemyAI` 慣例；偏離規則 7 的 ScriptableObject 但依規格明確要求）。
- `_Project/Game/AI/TenLeggedBugHornHitbox.cs` — 角部獨立 trigger hitbox，精簡 `BossHitbox`（kinematic RB、每 FixedUpdate swept BoxCast、per-activation 去重）。只在下刺命中幀 `Activate()`，命中一次 10 HP，**不因玩家待範圍內連續扣血**。
- `_Project/Game/AI/TenLeggedBugGaitUtility.cs` — pure：嚴格 1→N→重複、一次一腳、`SteppingLegIndex`/`LegLift01`/`LegStride`/`AdvancePhase`（速度縮放）。
- `_Project/Game/AI/TenLeggedBugAttackUtility.cs` — pure：30° 正面錐、犀牛角三段 pitch 曲線、命中窗、前腳張開 telegraph、搜尋左右掃。
- `_Project/Tests/EditMode/TenLeggedBugTests.cs` — 11 個 EditMode 測試，**全綠**（gait 一次一腳、lift 峰值、stride 前後掃、速度縮放、錐角、角三段、命中窗只在下刺、rest-pose 快照、bend 骨自動抓 first-child）。
- `_Project/Tests/PlayMode/TenLeggedBugPlayModeTests.cs` — 4 個整合測試（角只在命中幀傷害且一次一擊、背後不打會轉身、HP0 停一切、遠→巡邏近→追）。**MCP test runner 這輪卡死（既有環境問題，見 AGENT_NOTES），待使用者本機跑**。
- `Assets/Editor/Bootstrap/TenLeggedBugSetup.cs` — 選單 `Tools/Live2DAction/Build Ten-Legged Bug Enemy (十足蟲)`。把 `Shizuchong.prefab` 改造：加 `Rig` wrapper（Y 180，因原模型頭朝 local −Z，root +Z 才是 forward）＋ `CharacterController`（h1.7 r0.7 c(0,0.85,0)、step0、minMove0）＋ `Health`(100、defer=true)＋ `BossTeamMember`("Bug")＋ `NavMeshModifier`(ignoreFromBuild)＋ `NavPathFollower`＋控制器＋角 hitbox 子物件（Bone_002 底下）。**12 個骨頭在 Editor 端用名字解析、絕不在 runtime 猜**。場景實例只設 `target=Player`。

### 骨頭對照（十足蟲.glb 只綁 8 條地面腿，非 10——「十足」是名字）
`BodyRoot=Bone_001`、`Horn=Bone_002`（頭骨，pitch 做刺擊＋前兆）。腿 1..8（因 Rig Y180，模型左右已對調）：`Bone_033 / Bone_037 / Bone_025 / Bone_021 / Bone_017 / Bone_009 / Bone_029 / Bone_013`（前左、前右、左2、右2…）。控制器吃任意腿數，1→N 循環。

### 其他
- `NavMeshBakeSetup.cs` 的 `ExcludeFromBake` / `PathFollowerTargets` 加入 `十足蟲`。
- `十足蟲` 場景實例：scale 0.3、掛滿上述元件、`target=Player`、貼地 y≈0.52。BoxCollider（追加前置作業做的）已由 CharacterController 取代。
- `ASSET_LICENSES.md`：`十足蟲.glb`（Meshy AI 付費輸出，使用者持商用權）移到「已授權/原創素材」表。
- **待使用者**：跑 `Tools/Live2DAction/Bake Navigation Mesh` → Play 手感微調（距離/速度/角度/時間全在控制器 Inspector）；跑 PlayMode 測試；指派骨頭若要微調（目前 Editor 已自動填）。

### 追加83 續 — 修：蟲頭尾接反 + 攻擊搆不到玩家（使用者實機回報）

使用者:「1.蟲有在做攻擊動作但沒真的打到玩家，中間隔一段距離，不能衝破碰撞體到玩家腳下嗎 2.蟲攻擊的部位是尾巴，控制錯骨頭了 應該是反方向那個」。

- **頭尾接反**：初版把 `Bone_002`（其實是**尾/螫刺**，模型 −Z）當角，又給 `Rig` wrapper 轉 180° → 蟲**尾巴朝前、倒退走向玩家**、拿尾巴刺。**用逐骨標記渲染 + BakeMesh 頂點高度**確認：真正的頭/角是 `Bone_005 → Bone_004 → Bone_048` 鏈（模型 +Z 端、該半邊幾何最高 y=1.03＝角）。修：
  - `Rig` wrapper 轉回 **identity**（模型 +Z 頭＝root +Z forward，頭朝前走）。
  - `HornBoneName` `Bone_002` → **`Bone_004`**（真頭骨，pitch 它＝角上下刺＋頭壓）。角 hitbox 移到 `Bone_004` 底下、用 `Bone_004.InverseTransformPoint(Bone_048)` 定在角尖、放大到 world ~0.33×0.33×0.54。
  - 8 腿重排（無 L/R 翻轉了，模型自然左右、依 +Z→−Z 前到後）：`Bone_013/029/009/017/021/025/037/033`。
  - **場景 `十足蟲` + `Shizuchong.prefab` 已用 execute_code 直接改好**（MCP test runner 卡死沒法重跑選單）；`TenLeggedBugSetup.cs` 原始碼也同步更新，之後重跑選單一致。
- **搆不到玩家**：`TickAttack` 原本一過 `attackRange`(1.6) 就硬停 → 留 ~1m 空隙。改成：
  - `attackRange` 1.6 → **1.3**（Chase→Attack 的「投入攻擊」距離，非揮擊距離）。
  - 新欄位 `playerBodyRadius`(0.45)：Attack 態內先算 `contactDistance = 自身世界半徑 + playerBodyRadius + 0.08`，**還沒貼上就繼續全速前壓**（速度隨剩餘距離收斂、不推人）、步態續走、刺擊時鐘不啟動；**貼上才 plant + 揮**。＝蟲會真的頂到玩家身上才刺。
  - `TenLeggedBugHornHitbox` 加 `StaticOverlapCheck`：`Activate()` 後第一個 FixedUpdate 若角盒已與玩家重疊（貼身開窗，swept cast 和 OnTriggerEnter 都可能漏），用 `Physics.OverlapBox` 補一次結算。
- **待使用者**：點回 Editor 解 `tests_running` wedge → 腳本自動重編（吃到 press-in + StaticOverlapCheck）→ 重跑 `Build Ten-Legged Bug Enemy (十足蟲)`（可選，冪等）→ `Bake Navigation Mesh` → Play。`playerBodyRadius` / `attackRange` / 角盒大小位置都在 Inspector 微調。

### 追加83 續 2 — 血量條 + 5 秒復活 + 純觀看攝影機視角（使用者需求）

使用者:「1.根據他的身長製作血量條 UI 2.死亡五秒後復活 3.新增一個按鍵得到他的攝影機視角(純粹觀看不影響行為)，要考慮身高和視線」。

- **血量條**（`TenLeggedBugSetup.AddHealthBar`，加在 prefab 上）：世界空間 `Canvas` + 紅色 `WorldSpaceHealthBar`（既有元件、每幀 poll `Health`、billboard 對 `Camera.main`）。**寬度＝身長**：`SkinnedMeshRenderer.BakeMesh` 量最長水平軸 × 0.9 → 世界寬 ~0.86 m（body length），高 = 寬 × 0.13。位置：body 頂 + 0.18 m，world Y ~1.14（蟲頂 ~1.03）。Fill 用 Unity 內建 `UISprite`（否則 `Image.Type.Filled` 無視覺效果，同 `HealthBarSetup` 的坑）。
- **5 秒復活**：`TenLeggedBugController` 死亡不再淡出+`Destroy`，改 `DeathThenReviveSequence`：翻腹（`flipOverSeconds` 0.65）→ 躺平 → 總計 `respawnDelaySeconds`(5) 秒後 → `Health.ResetHealth()` + 翻回站立（`getUpSeconds` 0.6）+ 重置狀態 → `BugState.Patrol`。**GameObject 全程不 deactivate/destroy**（`Health.deferDeactivationToDeathAnimation=true`），所以 coroutine 活著跑完整輪——不像專案通用 `RespawnController` 得掛在角色外。`respawnMode`：`InPlace`（原地站起，同 boss 慣例）/ `AtSpawn`（回出生點），Inspector 可選。移除了 `BuildFadeMaterials` / `fadeOutSeconds` / `corpseHoldSeconds`。
- **純觀看攝影機（按 B）**：新 `Live2DAction.CameraSystem.SpectatorCameraToggle`（掛 `BugSpectator` 空物件）——**只換視角、不換操控**（不 reference 蟲的 controller，蟲照跑 AI）。按 B：快照當前所有 active 相機 → 全關 → 開 spectator；再按 B → 還原快照。組合得了任何當下的相機（player/cat/vehicle/守望者）。`BugSpectatorCamera` = 複製 Main Camera rig（`Object.Instantiate`，同 CatCamera 做法）retarget 到蟲，**低視線**：`distance` 1.5、`targetOffset` (0,0.35,0)、`initialPitch` 17°（往下看地面生物）、`minPitch` −18 / `maxPitch` 78。player-only 欄位（lockOn/input/ultimate/descendAutoPitch）清掉。起始 inactive。
- **測試**：EditMode `TenLeggedBugTests` 仍 **11 綠**（無回歸）。PlayMode `TenLeggedBugPlayModeTests` 加 3 個（復活滿血回巡邏、spectator toggle 只換視角會還原、既有 4 個）→ **6 個**，MCP test runner 又卡死（`tests_running` wedge，AGENT_NOTES 記載），**待使用者本機跑**。
- setup 工具修：`BugSpectatorCamera`/`BugSpectator` 清舊用掃 scene roots（`GameObject.Find` 跳過 inactive → 之前重跑會產生重複相機）。已手動清掉一個重複。
- **待使用者**：點回 Editor（若 wedge）→ `Bake Navigation Mesh` → Play。血量條 billboard 只在 Play 對得準（Editor 截圖是側面線）。按 B 切蟲視角。

### 追加83 續 3 — 蟲位置 + 更低視角 + 修駕車時無法切蟲視角（使用者實機回報）

- **移到右下角圍牆下、與屁孩王直行對齊**：`TenLeggedBugSetup` 把 `十足蟲` 擺到 `(屁孩王.x, groundY, Ground 南緣 +1.5m)` = **(12, 0.52, −13.5)**（屁孩王在 (12, 12)，同一 X 直線、一南一北）。`patrolRadius` 5（角落用小一點）。**待使用者重跑 `Bake Navigation Mesh`**。
- **視角不夠低 → 壓到接近蟲眼高**：`BugSpectatorCamera` 的 `ThirdPersonCameraController`：`distance` 1.5→**1.05**、`targetOffset` (0,0.35,0)→**(0,0.12,0)**、`initialPitch` 17°→**4°**（幾乎與蟲平視、微微往下）、`minPitch` −18→−30。實測相機世界 Y ~0.72（蟲頭高度）。
- **駕車時切不了蟲視角**：`VehicleEntrySystem.LateUpdate` 每幀 `SetActiveSafe(vehicleCamera, youDrive)`，會把 spectator 相機搶回去（同 `viewDirector.IsFocusedOnWatcher` 早退的情境）。修：`SpectatorCameraToggle` 加 `LateUpdate`，spectating 時每幀重申（開 spectator、關其他所有 `Camera.allCameras`）。`[DefaultExecutionOrder(150)]` 保證跑在 VehicleEntrySystem（order 0）之後，同幀內覆蓋掉、渲染前生效、無閃爍。被搶走的相機記進 restore list，退出時還原。**不需要 cross-reference**——對 vehicle / 守望者 / possession 任何 camera owner 都通用。
- setup 清舊 `BugSpectator*` 物件改掃 scene roots（`GameObject.Find` 跳 inactive → 重跑生重複相機，已修）。
- **待使用者**：重跑 `Bake Navigation Mesh`（蟲換位置了）→ Play：蟲在右下角、按 B 低視角看牠、上車開一開再按 B 應能切到蟲視角。

### 追加83 續 4 — 骨架重驗：確認 10 條腿，重接 leg bones（第二份分析 + 頂點驗證）

使用者提供另一個 AI 的分析（10 條腿），我用**地面接觸頂點分群**驗證：`y<0.12` 的蒙皮頂點分群 → **10 個腳掌，5 對完全對稱**（不是先前抓到的 8）。第一次漏掉是因為只找「Bone_001 直接子骨 + 乾淨 4 節鏈」。

**正確對照**（前 +Z 到後 −Z、L/R；每腿：swing 骨 / knee 骨）：
| # | 位置 | swing | knee |
|---|---|---|---|
| 1 | L 前 | `Bone_046` | (無) |
| 2 | R 前 | `Bone_029` | `Bone_028` |
| 3 | L 二 | `Bone_013` | `Bone_012` |
| 4 | R 二 | `Bone_017` | `Bone_016` |
| 5 | L 三 | `Bone_009` | `Bone_008` |
| 6 | R 三 | `Bone_025` | (無) |
| 7 | L 四 | `Bone_021` | `Bone_020` |
| 8 | R 四 | `Bone_024` | `Bone_023` |
| 9 | L 後 | `Bone_037` | `Bone_036` |
| 10 | R 後 | `Bone_033` | `Bone_032` |

**兩隻腿骨架不乾淨**（兩份分析都指出，要 Blender 重綁才能根治）：
- **腿 1（Bone_046）**：掛在頭鏈底下（`Bone_005 ← Bone_004 ← Bone_047 ← Bone_046`），權重跟身體混——踏步時腿根附近網格會輕微拉扯（驅動它**不會**帶動頭，child 旋轉不往上傳）。
- **腿 6 & 8（Bone_025 / Bone_024）**：`Bone_025` 是腿 8 骨鏈的父骨——**踏腿 6 會小幅拖到腿 8**（反向不會）。

**程式改動**：
- `TenLeggedBugController.CaptureRestPose`：`legBendBones` 若**逐腿都給**（Count == legRootBones）就**照用不自動補**（null = 這腿沒膝骨）——否則腿 6 會自動抓到唯一的子骨 `Bone_024`＝腿 8。
- `TenLeggedBugSetup`：`LegRootBoneNames` 改 10 個 + 新 `LegBendBoneNames`（10 個、腿 1/6 為空）。已重跑套上。
- EditMode `TenLeggedBugTests` 仍 **11 綠**。
- **待使用者**：Play 看十足交替步態（腿 1 / 6 / 8 會有輕微滑步/拉扯，那是骨架問題不是程式）；要完美步態就在 Blender 把腿 1、6、8 各自獨立出根骨再重匯。

### 追加83 續 5 — 蟲移動速度 ×0.7（使用者實機回報「太快」）

`TenLeggedBugController` 移動速度整組 ×0.7：`chaseSpeed` 5.5→**3.85**、`patrolSpeed` 1.4→**0.98**、`searchMoveSpeed` 3→**2.1**。`gaitSpeedForFullRate` 也跟著 5.5→**3.85**（腿步頻率維持與衝刺速度匹配）。`rotationSpeedDegrees`（轉向）/ `gaitBaseRateHz`（步態每秒循環數）不動。已改欄位預設值 + 場景實例 + prefab。

## 2026-08-31 (追加81 續 4) — Player 滿能量待命火焰光環調小（使用者:「燃燒過大蓋住角色和視線」）

`PlayerUltimateAuraVfxSetup`（`UltimateReadyAura.flameAura`，能量滿時亮）：
- **尺寸**：`SizeHeight` 2.7 → **1.7**（可見火焰 ~1.9m → ~1.2m，約角色身高、火尖到頭不過頭），`SizeWidth = H × 1280/720` 隨之 4.8 → **3.0m**（比例不變、地環維持圓形）。
- **`_Brightness`** 1.2 → **1.0**（bloom 不再洗版視線）。
- **`_Opacity`** 1.0 → **0.6**（火焰半透明，角色從火中看得清楚 —— 直接解「蓋住角色」）。ZTest 維持 Always（半透明後不再擋人，且避免 billboard 半邊被裁）。
- `AuraLocalOffset.y` 0.31 → **−0.02**（縮小後地環重新對回腳底）。
- 已重跑選單、材質 + prefab + 場景實例已更新存檔。**待使用者 Play 確認**，還要更小/更淡就調 `SizeHeight` / `_Opacity`。

### 追加81 續 5 — Player 待命火焰光環換新資產（青金靈火柱）

使用者換來源影片（`請根據需求描述製作影片：一個孤立的_D遊戲特效...mp4` → 青綠+金色靈火柱、白色能量絲環繞、火花、地面漣漪環，乾淨黑底，1280×720/24fps/10s）。需求：體型匹配、只有角色中心輪廓稍微透明、其他部分完全還原資產、處理透明通道、模仿 3D。

- **來源 + atlas 覆蓋**：`Source/PlayerUltimateAuraSource.mp4` 換新片；`PlayerUltimateAura_Atlas.png` 重烤（同檔名 → material/prefab/wiring GUID 不變）。**64 幀、source 30..156 每 2**、8×8、2560×1440。
- **透明通道**：乾淨黑底 → 亮度鍵 threshold **22** / range /50 / gamma 0.80（火焰實心、微光去乾淨）。`delogo`（不是黑 drawbox —— 浮水印壓在地環上，挖黑洞會破環）去掉角落 ✦。
- **中心輪廓稍微透明**：alpha = 亮度鍵 **×** 一個逐-cell 高斯「輪廓凹陷」——角色站的中下段（frame 55.6% 處、sx 7%W / sy 19%H）alpha 最低 ~0.45，往外回全 alpha。＝「只有中心稍微透明、其他完全還原」。
- **體型匹配**：`SizeHeight` 1.7（可見火焰 ~1.2m ≈ 角色身高），`SizeWidth = H×1280/720`（不變形，地環維持圓）。新片地環只佔 frame 寬 ~58% → 佔地更小、更不擋視線。`_Opacity` 回 **1.0**（透明現在烤在 atlas 中心，其他全強度）、`_Brightness` **1.05**。`AuraLocalOffset.y` −0.02 → **−0.1**（新片地環在 frame ~78% 處，對回腳底）。
- 選單重跑、material + prefab + 場景 `Player/ReadyFlameAura` 更新存檔。已用 `ps.Simulate` 截圖確認：青靈火包住角色、角色從火中央看得清楚、火尖到頭不過頭、地環貼腳。
- **待使用者 Play 確認**（能量滿）。中心透明度太淡/太濃調 `geq` 的 `0.55`，位置調 `0.556*H`；大小調 `SizeHeight`；重跑選單。

## 2026-08-31 (追加84) — 移除場上兩個物件：泳裝女標準人形 + 幽靈血條機甲

使用者:「場上兩個物件想移除 一個是穿著泳裝的女士 一個是只有血量條 UI 沒看到人物的幽靈」。

- **`FemaleStandee_Placeholder`**（Quaternius Superhero_Female，CC0，外觀像白泳裝，`(0, 0.5, -8)`，純靜態展示、無 Health/AI）→ 刪除。沒有任何腳本 serialize 引用它。
- **`Mecha`**（`MechaModel_DoNotShip`，來源不明、KNOWN_ISSUES 阻塞項 1b）`(2.5, 1.5, -2)`：那顆網格 `sharedMaterial` = NULL、renderer bounds 退化成 (0,0,0)，Play 下**完全不顯示**，只剩 `MechaDamageableSetup` 加的浮空紅血條＝使用者說的「幽靈」→ 刪除 GameObject ＋ `GameManager` 上指向它的 `RespawnController`（9→8）。唯一的引用就是那顆 RespawnController，已一併清掉。
- 場景 roots 68 → 66，已存檔，Console 無錯誤/missing reference。
- **選單工具沒動**：`FemaleStandeeSetup.cs` / `MechaVisualSetup.cs` / `MechaDamageableSetup.cs` / `MechaRespawnSetup.cs` 都還在，重跑會把物件加回來。`GreyboxSceneBuilder.Build()` 不會重建（只在註解提及）。要永久清 Mecha 就再刪那些檔 + `MechaModel_DoNotShip/` 資料夾（DoNotShip 素材，對正式 Build 只有好處）。
- **Mecha 佔的 navmesh**：它 layer=Default + CapsuleCollider 且無 `NavMeshModifier`，之前有 carve 進 baked mesh（出生點附近）。移除後重跑 `Bake Navigation Mesh` 可把那塊還原。`FemaleStandee` 是 layer=Scenery，本來就排除在 bake 外，不影響。

## 2026-08-31 (追加85) — 移除 TrainingDummy + 玩家武士刀每段攻擊配音效

### 1. 移除 `TrainingDummy`
Maya 視覺的站樁假人（原 Player3）@ (5,1,0)。掃過全場 MonoBehaviour 的 ObjectReference 欄位——**沒有任何腳本引用它**（不像 Mecha 有 RespawnController）。刪除 GameObject，並從 `NavMeshBakeSetup.ExcludeFromBake` 拿掉 "TrainingDummy"（那名字現在指不到東西）。場景 roots 66→65，已存檔。**選單 `TrainingDummySetup.cs` 還在**，重跑會加回。

### 2. 武士刀攻擊音效（`刀碰撞聲效.mp3`）
- 素材 → `Assets/_Project/Audio/Combat/KatanaClash.mp3`（使用者提供，1.23s；import 設 forceToMono + DecompressOnLoad + PCM）。
- 新 `Combat/PlayerMeleeSfx.cs`：訂閱 `PlayerCombat.Hit`（**每個 combo 段落 resolve 時各觸發一次，含揮空**）→ `PlayOneShot`，每次隨機 pitch 0.96–1.07（4 段連段不會四聲一模一樣）。`onlyOnHit`（預設 false）可切成只在命中時響。**沒動 `PlayerCombat`**，只是掛個訂閱者。
- 新 `Player/MeleeSfx` 子物件：獨立 `AudioSource`（3D、spatialBlend 1、minDist 3）＋ `PlayerMeleeSfx`，接 Player 的 `PlayerCombat` + 該 clip。**獨立子物件**避免跟 root 的 RangedWeapon 槍聲 AudioSource 共用 pitch 狀態。
- 選單 `Tools/Live2DAction/Add Player Katana Attack SFX`（`PlayerMeleeSfxSetup.cs`，可重跑）。
- 玩家 combo 是 `LightAttack1..4`（4 段），每段都會響。**待使用者 Play 確認音量/pitch 手感**（在 `MeleeSfx` 的 Inspector 調）。

## 2026-08-31 (追加86) — 滑鼠右鍵改武士刀防禦 + 退役射擊系統

使用者:「把滑鼠右鍵改成武士刀防禦，移除射擊系統和步槍資產可以保留到以後，另外目前 player 動作其實都不適合武士刀的風格，網路上有好的資源嗎」。

### 1. 右鍵 = 武士刀格擋（新機制）
- **輸入層**：`IInputCommand` 新增 `GuardPressed`（held，**default member `=> false`**，跟 `WalkTogglePressed` 同模式 → 所有 AI/測試 stub 不用改就編譯過）。`PlayerInputProvider`：右鍵 `isPressed` → `GuardPressed`；`AimPressed`/`FirePressed` 改成恆 `false`（expression-bodied）；`AttackPressed`/`AttackHeld` 改 gate 在 `!GuardPressed`（舉刀時左鍵不能起手，放開才能揮）。
- **傷害管線**：新 `Core/IIncomingDamageModifier`（泛用 pre-damage hook）。`Health.ApplyDamage` 加一段：套用**同物件上**的 `IIncomingDamageModifier`（lazy-cache，停用的 Behaviour 跳過，沒有 modifier 時 = 一次 length-0 迴圈，零行為改變）。既有 240 EditMode 測試全綠 → 無回歸。
- **`Combat/PlayerGuard`**（掛 Player root，`IIncomingDamageModifier`，Inspector 全參數化）：
  - 按住右鍵 → `IsBlocking`（死亡/硬直時不成立）。
  - `ModifyIncoming`：**只擋正面**（`guardArcDegrees` 150° 錐，用 `DamageInfo.Direction` 判定攻擊者方位）。命中：HP 傷害 ×`blockedDamageMultiplier`（0.15）；但 `ExplicitPoiseAmount` 設成**未減傷的架勢量**（`poiseMultiplier` 0.2，需與 `StancePoise.stanceGainMultiplier` 保持一致）→ **架勢照樣全額累積**（龜盾一樣會被打崩，沿用 boss `Boxing_Guard` 先例）。背後/側面命中不減傷。
  - 移動減速：`CharacterMovement` 新增 `ExternalSpeedMultiplier`（預設 1，只乘進地面 `baseSpeed`，不碰飛行/翻滾）；格擋時 PlayerGuard 設 0.35。
  - 程序化舉刀 pose：`LateUpdate` 對 `Bip001-R-Forearm` 疊加 local 旋轉、blend in/out（現有動畫不適用武士刀 → 佔位手法，同 `AttackPoseVisualizer` / 貓步）。
  - `Blocked` 事件（帶原始 `DamageInfo`）= 之後火花/clash 音效/完美格擋時機窗的掛點。
  - `LastBlockTime` = 之後 UI 閃光/parry 窗的掛點。
- **`PlayerGuardSetup.cs`**（選單 `Add Player Katana Guard`，可重跑）：掛 `PlayerGuard`，接 input/movement/health/stance + 抓 Humanoid 右前臂骨。已跑上場景。
- **`CameraPossessionSwitcher.playerControl`**：加入 `PlayerGuard`（貓附身時一起停用，才不會用共用滑鼠替 player 舉盾）——場景陣列已補上、`CatCharacterSetup.CollectPlayerControl` 也加了。

### 2. 退役射擊系統（**資產與腳本保留在磁碟**）
- Player 上移除：`RangedWeapon`、`RangedAttackDistance`、tracer `LineRenderer`、root `AudioSource`（只服務槍聲）。場景移除 `RangedWeaponHud` crosshair canvas、右手骨下的 `AK47` 實例（`WolfsGravestone` 武士刀留著）。
- **保留在磁碟**：`RangedWeapon.cs` / `RangedAttackDistance.cs` / `RangedWeaponSetup.cs` / `GunshotSfxSetup.cs`、`AK47.fbx` + 材質貼圖、`RangedTracer.mat`、`GunshotSfx.wav`。
- `RangedWeaponSetup` menu 加註「已退役、延後」；`GunshotSfxSetup` 找不到 `RangedWeapon` 時改 warning + skip（不再報 error）。
- `RangedWeapon.Update` 現在永遠看不到 aim/fire（`AimPressed` 恆 false）→ 完全 inert，就算日後把 component 加回去也不會走火，得先在 `PlayerInputProvider` 恢復一個 aim/fire 綁定。

### 3. 動畫資源（回答使用者提問）
專案內**已有** `Assets/_Project/Characters/Placeholder/CombatAnimations/TC_Sword_Free_Pack/`（MocapOnline TC Sword FREE：Ready Idle / Walk / Run / 三連斬，MotusMan_v50 骨架，retarget 到 Player5 Humanoid(Bip001) 需調 offset）。缺格擋/受擊/處決。線上補充（登入/下載由使用者本人，規則 11）：Mixamo（sword slash/katana/block/parry/hit reaction/death，免費商用）、Unity Asset Store 免費 Mecanim 劍術包（Kevin Iglesias 等）、MocapOnline 付費 Sword & Shield / Katana 大包、itch.io。建議：先用專案內 pack retarget combo/idle，格擋走程序化，之後從 Mixamo 補專用 clip。

### 測試
- EditMode：新 `PlayerGuardUtilityTests`（8）——正面錐判定（含忽略垂直分量、退化向量）、減傷 clamp、架勢全額、blend clamp。**240/240 綠**（含把 `CharacterAttackAnimationLinkTests` 一個自 08-17 起就跟 4 段連段脫節的舊測試 `BeyondThirdHit_FallsBackToAttack3` 更新成 Attack4）。
- PlayMode：新 `PlayerGuardPlayModeTests`（4，合成場景）——正面減傷/背後全傷/無盾全傷/減速再還原。**MCP runner 又卡在 `editor_unfocused`**（AGENT_NOTES 已知），**待使用者在互動式 Editor 跑一次**。

### 待使用者
- Play 確認：右鍵舉刀正面擋傷、背後不擋、舉刀時移動變慢、放開右鍵才能揮刀、舉刀 pose 方向對不對（不對就翻 `PlayerGuard.invertPose` 或調 `guardPoseLocalEuler`）。
- 手感值全在 `PlayerGuard` 的 Inspector（減傷倍率、錐角、減速、blend 速度、pose 角度）。

## 2026-09-01 (追加87) — 連續刺刀動作：武士普通攻擊 + Player F 處決（共用一支 Humanoid clip）

使用者提供 `連續刺刀.zip`（Meshy `Meshy_AI_Parkside_Portrait_biped` 的 Animation，without_skin FBX 205KB）→ 問「能同時被武士跟 PLAYER 使用嗎」→「武士的部分讓他作為普通攻擊加入 PLAYER 則做為 F 處決加入」。

### 匯入
- `Assets/_Project/Characters/Placeholder/CombatAnimations/Meshy/ContinuousThrust.fbx`。Meshy biped 骨架（Mixamo 式命名）→ 匯入設 **Humanoid**（`animationType:3` / `avatarSetup:1`），23 根骨頭自動對應、零手動。
- clip `ContinuousThrust`：**92 幀 / 3.033s / 30fps**、`isHumanMotion=True` → **一支 clip retarget 到 Player5Avatar 和 WushiAvatar 兩邊**（Humanoid 動畫骨架無關，武士 4× 縮放對 retarget 無影響）。
- clip import：`loopTime=0`、`keepOriginalPositionY=1`（比照 `Wushi_DoubleCombo` 的「AdvancingCuts 下沉教訓」）、`lockRootPositionXZ=1`（原地化 —— Meshy 慣例「起點在 root 後方往前走」）、`lockRootRotation=1`。

### 武士 —— 普通攻擊
- `Wushi.controller` 加 state `Wushi_ContinuousThrust`（motion = ContinuousThrust，speed 1.25）。boss 靠名字 CrossFade，不接 transition。
- 新 `Assets/_Project/Settings/Combat/Boss/Wushi_Attack_ContinuousThrust.asset`（`BossAttackDefinition`），複製 `DoubleCombo` 結構。**離線量測**（`AnimationMode.SampleAnimationClip` retarget 到 4× 武士，讀 BladeHitbox vs Chest 的前伸量+速度）= **5 段連刺**：
  - jab1 nt 0.045-0.10（前伸 2.6、Y~3.0、nt 0.10 抽刀 spd 92）
  - jab2 nt 0.22-0.29
  - jab3 nt 0.335-0.385（Y~4.1 頭高刺）
  - jab4 nt 0.445-0.495（Y~4.5）
  - jab5 nt 0.575-0.665（前伸 **3.3 最深**、Y~2.0 下段突進，dmgMult 1.3；之後 0.67-0.77 刀停在前方＝不算新命中，0.8-1.0 抽刀 recovery）
  - 全 `part=Weapon`、`measured=1`
- 傷害：`healthDamageIsPercentOfTargetMax=true` + `baseHealthDamage=1` → 每刺 1%、5 刺全中 ~5%（守「大招以外固定扣 5%」）。poise 3/刺（~15 total，接近 DoubleCombo 的 24）。
- selection：`maxDistance 2.8`（Meshy「起點在 root 後方」→ 保守，見 DoubleCombo notes）、`maxAngleDegrees 45`（突刺窄）、`cooldown 4`、`useRootMotion=0` 第一版、`isMajorAttack=0`。
- 加進武士 `BossStateMachine.normalAttackPool`（5 → **6**）。

### Player —— F 處決
- **不能直接改 Maya `NewAnimator` 的 `Execute` state** —— 掃描發現 `中立者1` 也用同一個 controller **且也掛 `ExecutionAbility`**（`守望者` 也用該 controller 但無此元件）。改 `Execute` 會連 `中立者1` 的處決一起換掉。
- 隔離做法：`ExecutionAbility.cs` 加 `[SerializeField] string executeTriggerName = "Execute"`（`Awake` 算 hash，取代寫死的 `"Execute"`）。Maya `NewAnimator` 加**新** trigger `ExecuteThrust` + 新 state `ExecuteThrust`（motion = ContinuousThrust，speed 1.35，AnyState->ExecuteThrust / ExecuteThrust->Locomotion exitTime 0.9，比照 `SpecialMoveAnimatorSetup.WireState`）。
- Player 的 `ExecutionAbility`：`executeTriggerName = "ExecuteThrust"`、`executionAnimationSeconds` 1.5 -> **2.25**（3.033s / 1.35 speed；處決 50%-當前血傷害在動畫播完才結算）。
- `中立者1` 的 `ExecutionAbility` 維持預設 `"Execute"` -> 原 `Execute` state / `FlyingKick` 不動。`EnemyExecutionAbility`（Arisa，另一個 `NewAnimator`）也不動，仍 FlyingKick。

### 驗證 / 待辦
- EditMode **240/240 綠**（本次無新增 EditMode 測試，純 animator/asset 接線 + 一個 serialized 欄位；util 測試全過＝無回歸）。
- **retarget 高度**：離線 BakeMesh 量測，ContinuousThrust 在 Player 上最低頂點 ~0.55（CrossPunch 落地基準 ~0.45）-> **Player 處決時可能浮 ~5-10cm**。武士上比照 DoubleCombo，`keepOriginalPositionY=1` 已是防下沉的正解。**待使用者 Play 確認**，浮太多就調 `ExecuteThrust` state 或 Visual 的 Y。
- **hit window 待實機確認**：離線量的（frames-advancing 的 in-Play 才算數，見 DoubleCombo 同樣的確認流程）。若武士刺不到玩家，比照 DoubleCombo 上 `useRootMotion=1`（要一併補「root-motion attack 被打斷時清 `_currentAttack`」的坑）。
- **手感**：Meshy clip 當攻擊天生要調（`KNOWN_ISSUES` 2026-08-28）。speed（武士 1.25 / Player 1.35）、windows、傷害/poise 都在 asset / state 上。
- PlayMode 測試：MCP runner 仍卡 `editor_unfocused`，**待使用者點回 Editor 重跑** boss / execution 整合測試。

## 2026-09-01 (追加88) — 武士架勢回復調慢 + 左鍵音效改成防禦刀刃碰撞 + 格擋姿勢改負斜率

使用者三點：1. 武士架勢條太容易就遽減（→ 確認是「我累積的一下就被回掉」＝回復太快）2. 把 PLAYER 左鍵音效移到右鍵(防禦)，且只在「防禦時玩家與武士刀刃碰撞」時響（隻狼機制）3. 防禦時武士刀角度不對，要負斜率（刀尖左上、刀柄右下）。

### 1. 武士 StancePoise 回復調慢（只動武士）
`regenPerSecond` 20 → **8**、`regenDelaySeconds` 1.5 → **3**。`maxStance`(60) / `stanceGainMultiplier`(0.2) 不動。玩家中斷攻擊 3 秒後才開始退、退速砍半 → 累積的架勢不會一放手就蒸發，打得崩了。屁孩王/Player/Enemy 的 StancePoise 不變。

### 2. 左鍵音效 → 防禦刀刃碰撞（隻狼）
- **移除** `PlayerMeleeSfx`（每段 `PlayerCombat.Hit` 都放 `KatanaClash.mp3`）＋ `PlayerMeleeSfxSetup.cs`（連 .meta 一起刪）。場景 `Player/MeleeSfx` 子物件移除。
- **新** `Combat/PlayerGuardClashSfx.cs`：訂閱 `PlayerGuard.Blocked`（正面擋下時觸發，帶原始 `DamageInfo`）。只在確認是「boss 刀刃攻擊」時放音效 —— 從 `info.Source` 找 `BossHitbox` 且 `IsActive && ActiveWindowPart == Weapon`（新 `BossHitbox.ActiveWindowPart` getter，1 行）。擋下踢擊(RightFoot)不會響。`clashOnAnyBlock` 開關（預設 false）可放寬成「任何正面格擋都響」。
- **新** `PlayerGuardClashSfxSetup.cs`（選單 `Add Player Guard Clash SFX`，取代舊的 `Add Player Katana Attack SFX`）：同一支 `KatanaClash.mp3`、新 `Player/GuardClashSfx` 子物件(獨立 AudioSource, 3D)，接 `PlayerGuard`。可重跑、會清掉殘留的舊 MeleeSfx 子物件。
- 左鍵普攻現在**完全沒有音效**（照使用者「移動」的字面意思）。

### 3. 格擋姿勢：負斜率武士刀（刀尖左上刀柄右下）
- **`PlayerGuard` 從只轉前臂 → 轉兩根骨**：新 `upperArmBone`（`Bip001-R-UpperArm`）＋ `upperArmGuardLocalEuler`。前臂單獨轉沒辦法把手抬起來做跨身體格擋，加上大臂才行。`LateUpdate` 兩根都疊 `_poseBlend` 權重（跟 `AttackPoseVisualizer` 同疊法）。
- 新預設：`upperArmGuardLocalEuler = (-30, -40, -18)`、`guardPoseLocalEuler = (-40,15,-55) → (-55, 25, -165)`。離線 `AnimationMode` 逐格算圖掃出來的（~35 張 render 比對刀刃在螢幕上的斜率）：刀柄在右手邊、刀刃往左上斜切過身體，**負斜率、刀尖左上、刀柄右下**。
- `PlayerGuardSetup` 加抓 `Bip001-R-UpperArm` 並寫入。已重跑上場景。
- **注意**：離線 render 用的是 bind pose；Play 時姿勢是疊在 idle 動畫上的，實際角度會有差。兩個 euler 都在 `PlayerGuard` Inspector 可調。場上還有一把 `BackGreatswordDecor`（背上的紅色狼末大劍裝飾，DoNotShip）從背後鏡頭看會擋住武士刀視線 —— 它不受 `PlayerGuard` 控制。

### 測試
- EditMode **240/240 綠**（`PlayerGuardUtilityTests` 不受影響 —— 改的是 Inspector 值 + 第二根骨 + SFX 換觸發源）。
- **待使用者 Play 確認**：武士架勢回復手感、防禦時擋武士刀有 clank 音（擋踢沒有）、格擋姿勢刀刃斜率（不對就調 `PlayerGuard` 的兩個 euler）。PlayMode 測試 MCP runner 仍卡 `editor_unfocused`。

## 2026-09-01 (追加89) — 連續刺刀 clip 不堪用，退回；順帶做 face-target 修正

使用者回報：1. 防禦判定帧和武士判定帧沒對齊、防禦音效散亂沒規律 2. 武士/玩家的連續刺擊「很像打空氣、會亂位移」，要先鎖定目標方向再施展。

### 診斷：`ContinuousThrust.fbx`（追加87 匯入的 Meshy clip）本身不堪用
離線 `AnimationMode` 逐格量測，retarget 到 4× 武士 + 1× 玩家：
- **髖部單調位移**：整段往前 + 往右各漂 ~4.5/4.75 世界單位（4× 武士）≈ 1× 上 **~1.5m 前 + ~1.6m 側**，**且不回來**。`lockRootPositionXZ`(Bake Into Pose) 只清掉 root 節點的淨位移，per-frame 的前進是烤在髖/脊椎曲線上的 → root 不動但**可見身體整個走出去**（"亂位移"）。
- **軀幹狂轉**：chest yaw 相對 root 在一段內擺 **-70° → +73° → +133°(!) → 回**。這是個**旋身撲擊**動作，不是連續直刺 → 我追加87 量的「5 段 hitWindow」其實是刀刃在旋身時掃過去的位置，難怪「打空氣」。
- 側視 render 確認：nt 0.3 時武士已整個移出畫面、刀刃視覺脫節。

→ **調 tuning 修不了動畫資料本身**。

### 處置：退回可用狀態，資產全留磁碟
- `Wushi_Attack_ContinuousThrust` 從 `武士 BossStateMachine.normalAttackPool` **移除**（回到原本 5 招）。
- Player F 處決 **退回 `Execute`/`FlyingKick`**（`ExecutionAbility.executeTriggerName` = "Execute"、`executionAnimationSeconds` = 1.5）。
- **全部留在磁碟、只是沒接**：`ContinuousThrust.fbx`、`Wushi_Attack_ContinuousThrust.asset`、`Wushi.controller` 的 `Wushi_ContinuousThrust` state、Maya `NewAnimator` 的 `ExecuteThrust` state + trigger。clip 修好後重接即可。

### 有價值、留下來的改動（對任何有方向性的攻擊都有用）
- **新 `BossAttackDefinition.faceTargetSnapOnStart`**（bool）：true 時 boss 在攻擊 state 進入的當下（frame 0 前）**直接 snap yaw 對準目標**，不再只用 `startupTracking` 速度慢慢轉 → 早期命中帧不會因為 commit 時稍微歪掉就刺空。`BossStateMachine.OnEnterState` 的 Attack case 實作。
- **`ExecutionAbility.BeginExecution` 先 snap 對準被處決目標**（壓平），處決動作整個指向對方，不再朝原本面向打空氣。對 `FlyingKick` 一樣有效。

### 建議（clip 要重用的話，擇一）
1. Meshy 重生成，prompt 強調「站定原地、連續向前直刺、不轉身不位移」。
2. 換 Mixamo 的 "sword thrust / stab"（有原地版）。
3. Blender：把 root/髖部前進位移 bake 掉 + 收掉旋身幅度。

### 測試
EditMode **240/240 綠**。PlayMode MCP runner 仍卡 `editor_unfocused`。

## 2026-09-01 (追加90) — 修：武士架勢滿了不做蹲下動作

使用者:「武士架式條滿了後 沒有做出蹲下的動作」（追加88 把回復調慢後才變得填得滿，這 bug 才浮現）。

**根因**：`BossStateMachine.UpdatePostureBroken()` 沒防 `CrossFadeInFixedTime` 的一幀延遲（這個坑 `AnimatorHasFinished()` / 攻擊路徑的 `_stateTimer` 下限早就有註解記錄）。架勢是靠**打武士的攻擊硬直**去累積的 → 破防幾乎都發生在 `Attack` state 中。破防當幀 `CrossFadeInFixedTime("Wushi_PostureKneel")` 還沒生效，`GetCurrentAnimatorStateInfo(0)` 仍回報**離開中的攻擊 clip**，其 `normalizedTime` 通常早就 > `PostureKneelNormalizedTime`(0.5) → 舊碼立刻 `animator.speed = 0`，把 Animator **凍在攻擊姿勢的 frame 0**，跪地 clip 根本沒開始播。結果：武士破防時只是卡住不動，沒有跪。

**修法**：在取 `normalizedTime` 前先確認 Animator **真的進到跪地 state**（`!IsInTransition(0) && stateInfo.IsName(kneelName)`）；crossfade 若 1.5s 還沒 land（state 名錯/clip 缺）就 fallback 不卡死。純 `BossStateMachine.cs` 改動、無場景變更。

**Play 實測驗證**：在武士 `Attack` state 時強制 `AddPostureDamage` 破防 → 確認 `state=PostureBroken`、Animator `IsName("Wushi_PostureKneel")=true`、`normalizedTime` 推進到 0.501、`animator.speed=0` **凍在跪姿**（不是 frame 0），3 秒後站起、回 combat。EditMode 240/240 綠。

**同一個坑也修到屁孩王**：`UpdatePostureBroken` 是兩隻 boss 共用的，屁孩王(`PW2_KneelOnOneKneeAndStand`)先前若也在攻擊中被破防會有同樣症狀，一併修好。

## 2026-09-01 (追加91) — 武士 Boss 開場演出（Timeline 過場，切片外探索）

使用者用 `/grill-with-docs` 討論後定案（完整決策記錄 + 術語表見新檔 `Docs/BOSS_INTRO_EXPLORATION.md`）。**這是切片外的可丟棄技術探索**，驗證「Timeline 運鏡 + Signal + 舉刀起手式」pipeline，不接入正式遊戲、不動 GreyboxTest、**不在 Build Settings**。

### grilling 定案
S1 切片外探索 / S2 3D 過場（與 Live2D 演出並行、方向未定）/ C1 用 Cinemachine 3.x / A2 舉刀起手式（不加刀鞘）/ A1 `Wushi_SwordJudgment` 前搖 / E1 最小替身 / V1 複用 flipbook + 程序合成拔刀音效 / C3 Cinemachine Impulse。

### 交付
- **場景**：`Assets/_Project/Scenes/SamuraiBossArena.unity`（深色反射地板 + 壓暗 Directional + boss 頭頂 Spot；`Wushi.fbx` 實例當 `武士` scale 2.2 / 膠囊當 `Player` + `DemoPlayerController` / 空殼 `DemoBossAI` + `DemoBossHealthBar` + `PlayerUI` canvas）。
- **腳本**（`Assets/_Project/Game/Cutscene/`，`Live2DAction.Runtime` asmdef，皆註明探索用）：
  - `BossTrigger` — 進 trigger（tag Player）→ `BossIntroManager.StartIntro()` + 自身 inactive。
  - `BossIntroManager` — **泛型 disable 清單**（`Behaviour[]` / `GameObject[]` / `PlayableDirector`），`StartIntro` 關控制/UI/AI/血條 + `Play()`；`introTimeline.stopped` 或 `duration+1.5s` realtime failsafe → 全開回來 + `CM_Vcam_Gameplay` 接手。
  - `BossSignalReceiver`（掛 `武士`）— `OnBladeDrawSignal()` → `ps.Play()` + `AudioSource.Play()` + `CinemachineImpulseSource.GenerateImpulse()`。
  - `DemoPlayerController`（新 Input System WASD 走路）/ `DemoBossAI`（空殼，log enable/disable）。
- **Timeline** `Assets/_Project/Timeline/BossIntro.playable`：Animation Track（`武士` Animator ← `Wushi_SwordJudgment_InPlace` clip，nt 0–0.28、0.4× 速度）/ Cinemachine Track（Brain on CutsceneCamera，**硬切** Back→Face→Action）/ Signal Track（`武士` SignalReceiver，apex ~1.3s → `BladeDraw.signal` → `OnBladeDrawSignal`）。4 台 `CinemachineCamera`（Back/Face/Action/Gameplay）。
- **`Assets/_Project/Timeline/Wushi_SwordJudgment_InPlace.anim`** — `Wushi_SwordJudgment` 去掉 `RootT.x`/`RootT.z`/`RootQ` 曲線的副本（**`RootT.y` 保留**，否則 Humanoid pose 塌到地上）。原 FBX / GreyboxTest 戰鬥不動。
- **`Assets/_Project/Audio/Skills/KatanaDraw.wav`** — 程序合成金屬掃頻「鏘」（比照 `GunshotSfxSetup`），可替換。
- `Live2DAction.Runtime.asmdef` 加 `Unity.Cinemachine` + `Unity.Timeline` 參照（不採用就刪 Cutscene 資料夾 + 這兩行）。
- **測試** `BossIntroManagerTests`（EditMode，3 個：`StartIntro` 關掉清單裡每個 behaviour/GameObject、`stopped` 全開回來、null-safe idempotent）。**243/243 綠**、零編譯錯誤/警告。

### 建置踩坑（記進探索檔第八節）
1. 「Timeline `stopped` 立即觸發」是錯覺 —— Editor 空場景 >1000fps，整段 Timeline 在兩次取樣間就跑完 + `WrapMode.None` 把 time 歸零。
2. `Wushi_SwordJudgment` 帶 root motion，Timeline 任何 trackOffset 壓不住 → boss 播放中漂 ~1m + 沉地板。解：in-place clip 副本（只刪水平 root 曲線）。
3. **Meshy 模型退化 SkinnedMeshRenderer bounds**（extents ~44）→ 過場相機一沒對準幻影中心 boss 就被視錐剔除消失。解：`smr.updateWhenOffscreen = true`（GreyboxTest boss / 校園 FBX 同招）。
4. 前搖範圍 0.5→**0.28**（0.5 會播到下劈蹲身、不像起手式）。

### 待微調（Scene view 拖 vcam，非決策）
Back/Face 框景 OK；Action 廣角要拉遠、平衡玩家與 boss 比例。Editor game view 目前超寬比例（2.78），正式 16:9 框景會不同。Signal 精確幀 / shot 切點 / Impulse 振幅 / 音色 / 燈光亮度都 in-Editor 調。**待使用者手動 Play 完整跑一次**（走進 trigger）確認交接手感。

## 2026-09-01 (追加92) — Boss 開場演出「轉正」接入 GreyboxTest（過場完直接開打）

使用者：「開場演出完能跟 boss 對打了嗎」→「繼續做完」。把追加91 的隔離 demo 接進真正的 `GreyboxTest.unity`：走近 `武士` → 過場舉刀演出 → 交還控制的同時 boss 直接進戰鬥。完整記錄見 `Docs/BOSS_INTRO_EXPLORATION.md` §9。

### 改既有腳本（3 支）
- **`BossStateMachine.cs`** — 加 `public void ForceEngage()`：從 `Dormant`/`ReturnHome`/`GateWatch` 直接 `_hasEngaged=true` + `ChangeState(Alert)`（跟 `UpdateDormant` 進 Alert 同路徑），讓過場一結束就接敵、玩家不用再走近 `alertRange`。terminal state / 已接敵 = no-op。
- **`BossIntroManager.cs`** — 加 `cutsceneCameraRoot`（過場 SetActive(true)/結束 (false)）、`gameplayCamera` 反向切換、`UnityEvent onIntroComplete`（`RestoreControl` 尾端 fire 一次，接 `ForceEngage`）、`_finished` 一次性旗標。`EditorConfigure` 7-arg 簽章不動（demo 的 `SamuraiBossArenaSetup` 照用）。
- **`BossTrigger.cs`** — 加 `playerRoot` Transform：`GreyboxTest` 的 `Player` 是 Untagged，改判「碰撞體在不在 `playerRoot` 底下」；沒設才 fallback 回 tag。

### 新增
- **`Assets/Editor/Bootstrap/BossIntroGreyboxSetup.cs`** — 選單 `Tools/Live2DAction/[Boss Intro] Wire Into GreyboxTest`（可重跑、idempotent、只在 GreyboxTest 為 active scene 時執行）。在場景加 `BossRoomTrigger`（BoxCollider trigger @ z=4，`alertRange`=6 外）、`BossIntroCutsceneRig`（起始 inactive：`IntroCam` Camera depth 20 + `CinemachineBrain` Cut + `CinemachineImpulseListener` + 3 vcam）、`BossIntroManagerObject`（`PlayableDirector` + `BossIntroManager`）；在真實 `武士` 掛 `BladeDrawVFX` 子物件（`Attack3SlashEffect` 實例）+ 拔刀 `AudioSource`（`KatanaDraw.wav`）+ `CinemachineImpulseSource` + `BossSignalReceiver` + Timeline `SignalReceiver`。
- **`Assets/_Project/Timeline/BossIntro_Greybox.playable`** — 3 軌，複用 demo 的 `Wushi_SwordJudgment_InPlace.anim` + `BladeDraw.signal`。
- `BossIntroManagerTests` 加第 4 個測試 `OnIntroComplete_FiresExactlyOnce_EvenOnDoubleStop`。

### `BossIntroManager` 接線
`playerControlScripts`（11）= `PlayerInputProvider`/`CharacterMovement`/`PlayerCombat`/`TargetLockController`/`UltimateAbility`/`PlayerGuard`/`ExecutionAbility`（`Player`）+ `ThirdPersonCameraController`（`Main Camera`）+ `CameraPossessionSwitcher`/`ViewFocusDirector`/`SpectatorCameraToggle`（過場中它們的 LateUpdate 會硬把 `Main Camera` 開回來，必須一起關）。`playerUi` = `PlayerCornerHud`。`bossCombatAI` = 真實 `BossStateMachine`。`bossHealthBar` 留 null（`WushiBossHudVisibility` 已按 state 開關）。`cutsceneCameraRoot`/`gameplayCamera` = rig / `Main Camera`。`onIntroComplete` = serialized persistent listener → `ForceEngage`。

### 驗證
- EditMode **244/244 綠**、零編譯錯誤。
- Play（Editor 失焦、frame frozen，只驗控制交接）：`StartIntro` → 控制關 + `Main Camera` inactive + rig active ✅；模擬 `stopped` → 控制全開回 + `Main Camera` active + rig inactive + `PlayerCornerHud` 回來 + **`boss.CurrentState == Alert`**（`ForceEngage` 有觸發）✅。
- **待使用者在有焦點 Editor 手動 Play**：走進 trigger → 過場 3 鏡頭 + 起手式 + Signal → 跑完 → 武士追來對打。
- `GreyboxTest.unity` 存檔照例有 ~25k 行 Live2D mesh 重序列化 churn（追加91/追加90 commit 同樣量級，非本次改動造成）。

### 追加92 續 — 相機框景修正（使用者：「視角不對 只看到 boss 的頭」）
第一版估錯 4× 武士高度（當 7m，BakeMesh 實測 **腳 y≈0.6 / 頭 y≈4.6，~4m**），vcam 擺在頭頂以上。改用 viewport 投影 + 真 `Camera.Render()` 離線截圖對照重擺：`Face` (−1.6,3.25,4.1) fov42 正面全身、`Action` (−10.5,4,5.3) fov52 廣角、`Back` (0.85,3.95,14.6) fov50 貼身過肩（`BoundaryWall_North` 在 z=15.5，武士後方只有 4m，再退就拍到牆背面）。武士 `SkinnedMeshRenderer.updateWhenOffscreen=True`（Meshy 退化 bounds，Back 機位整個 boss 被視錐剔除）。`BossIntroGreyboxSetup.cs` 常數同步更新、場景已存。

## 2026-09-01 (追加93) — 近戰判定實體提示：先做攻擊版 → 同日改成防禦版

使用者：「參照 boss 攻擊時刀刃紅色範圍提示一樣，把 player 的刀也附上」→ 看過後：「我不要攻擊的碰撞顯示 改成防禦」。

### 第一版（`PlayerAttackHitboxVisualizer`）—— 已移除
比照 `BossHitboxVisualizer`，重建 `PlayerCombat.ResolveActiveHit` 的即時 `Physics.OverlapCapsule` 成綠色不透明 mesh、只在 Active 窗口顯示。做出來後使用者不要 → **元件 / 選單 / 測試檔全刪**。

### 定案（`PlayerGuardVisualizer`）
- **`PlayerGuardVisualizer.cs`**（新，`_Project/Game/Combat/`）—— Player 防禦的實體提示。`PlayerGuardUtility.IsFrontalBlock` 純看**水平角度**（`transform.forward` vs 來擊方向，flatten Y）在 `PlayerGuard.GuardArcDegrees` 內，所以提示是一片**平放的扇形**（pie slice），在玩家前方沿那個弧展開，只在 `PlayerGuard.IsBlocking`（右鍵按住、非死亡/硬直）時顯示。純幾何 `BuildFanVertices(arc, range, segments)` 抽出可測；扇形雙面、`range` 2m、`height` 1.1m、**藍色**（防禦色，跟 boss 紅、玩家攻擊綠都區隔）。runtime 若改 `guardArcDegrees` 會自動重建 mesh。
- **`PlayerGuard.cs`** —— 加 `GuardArcDegrees` 唯讀 getter。
- **`PlayerCombat.cs`** —— 保留第一版加的 `CurrentActiveAttack` + `AttackOrigin`（下一步「刀真的 swept collider」會用到）。
- **`PlayerGuardVisualizerSetup.cs`**（新）—— 選單 `Tools/Live2DAction/Add Player Guard Telegraph`（＋`Remove …`），加到 GreyboxTest 的 Player、存檔。已執行。順帶清掉殘留的舊 attack 元件。
- **測試**：EditMode `PlayerGuardVisualizerTests` 3 個（apex 在原點/rim 在 range、扇形邊在 ±half-arc、segment/range clamp）。**247/247 綠**、零編譯錯誤。離線 `Camera.Render()` 截圖 `player_guard_wedge.png` 確認扇形朝向與角度對。
- **待使用者 Play 確認**：右鍵按住時藍扇出現、朝向跟隨轉身、平放在胸高會不會太懸空（要的話改貼地或加高度漸層）。

## 2026-09-01 (追加94) — 隻狼式刀刃交鋒：防禦 / 一般格擋 / 完美彈反（Phase 1a：地基）

使用者完整 spec（防禦輸入、Parry/Guard 窗口、GuardVolume、Boss sweep 整合、命中結果、視聽回饋、debug）。**優先擴充既有系統，不建新血量/傷害系統**。先做稽核，再分階段。

### 稽核結論（既有可用）
`Core/Health` + `Core/IIncomingDamageModifier`（傷害前置 hook）、`Combat/StancePoise`（`AddPostureDamage(float)` 註解就寫「for a future parry system」、`DamageInfo.ExplicitPoiseAmount`）、`Combat/PlayerGuard`（右鍵格擋 + `Blocked` event）、`Combat/Boss/BossHitbox`（**已是真實 swept CapsuleCast**、`HitWindow` normalized 時間軸、`_hitTargetsThisActivation` 去重、`ActiveWindowPart`）、`Combat/HitStopController`（scene-single）、`Camera/CameraShake`（Main Camera）、`Wushi.controller` `HitReaction`/`HitFlyUpTrigger`/`PostureBreakTrigger` + `BossState.HitReaction`/`PostureBroken`。**缺口**：無防禦動畫 clip（`NewAnimator` 共用，決議純程式碼驅動窗口）、無 Hurtbox/GuardWeapon/BossWeapon layer、輸入無 press-edge。

### Phase 1a 交付（純程式碼地基，尚未接 runtime 行為）
- **`ProjectSettings/TagManager.asset`** —— 新增 3 layer：`PlayerHurtbox`(3) / `PlayerGuardWeapon`(6) / `BossWeapon`(7)。
- **`Input/IInputCommand` + `PlayerInputProvider`** —— 加 `GuardPressedThisFrame`（右鍵 `wasPressedThisFrame`，press-edge）。default false（AI 不防禦）。
- **`Combat/BladeClash.cs`**（新）—— `BladeClashResult` enum（None/Guarded/Parried）、`BladeClashInfo` struct（attacker/傷害/架勢/交點/攻擊方向）、`IBladeClashReceiver` interface（`TryResolveClash`）、**`BladeClashUtility`** 純函式：`Classify(guarding, frontal, inParry, inGuard)` 固定優先序、`WithinParryWindow(now, guardStartTime, dur)`（從 press-edge 起算，hold 不刷新）、`ClashCooldownElapsed`。
- **`Combat/PlayerGuard.cs`** —— 加：press-edge 記 `_guardStartTime`（放開/崩解/死亡清 -1）、`parryWindowDuration` 0.12、`CurrentDefense` getter（None/Guard/Parry）、實作 `IBladeClashReceiver.TryResolveClash`（Parry→玩家 0 HP + Boss `AddPostureDamage(parryBossPoiseDamage 14)` + `BossStateMachine.NotifyParried()` + hitstop 0.10 + shake；Guard→玩家 `AddPostureDamage(6)` + 可選 chip + hitstop 0.05 + shake）、`Parried`/`Guarded` event（交點）、clash 冷卻 0.1s。`guardArcDegrees` 預設 150→**120**。所有數值 Inspector 公開。
- **`AI/Boss/BossStateMachine.cs`** —— 加 `NotifyParried()`：`_forcedHitReactionPending = true`（沿用 `RequestBeHitFlyUp` 的 `HitReaction` recoil，不含 launch；架勢若因此爆表，`UpdateHitReaction` 自己會轉 `PostureBroken`）。
- **測試** `BladeClashUtilityTests`（10，對應 spec 六情境的可單元化部分：優先序 5 種、parry 窗從 press-edge 起算/hold 不刷新、無 press、冷卻）。**257/257 綠**、零編譯錯誤。
- `PlayerGuard.ModifyIncoming` 暫時仍軟擋所有正面命中（含刀）—— Phase 1b 接上 clash volume 後才加「刀繞過防禦打到身體 = 全額傷害」的 gate，避免半接狀態下格擋退化。

### Phase 1b 交付（接上 runtime）
- **`Combat/PlayerGuardVolume.cs`**（新）—— trigger CapsuleCollider，僅 `PlayerGuard.IsBlocking` 時啟用，自己一個**未縮放** GameObject，每 FixedUpdate（`[DefaultExecutionOrder(-50)]`，排在 BossHitbox sweep 前）重定位。`GuardRoot`/`GuardTip` = spec 的「刀根/刀尖」。**（見下方修正：改成 hand-anchor + 前方延伸的 shield zone。）**
- **`Combat/Boss/BossHitbox.cs`** —— `SweepCheck` 擴充 `TryResolveBladeClash(hitCount)`：只在 `_activeWindow.part == Weapon` 時，掃過的 hit 找最先命中的 active guard volume；若身體明顯在 guard 前面（>0.05m）才放行身體命中（spec C「繞過防禦」），否則呼叫 `receiver.TryResolveClash(BladeClashInfo{...})`；回 `None` fall-through、非 None 則吃掉這次 sweep（`_hitTargetsThisActivation` 去重）。傷害計算抽出 `ComputeHealthDamage()`（`TryResolveHit` 也改用它）。
- **`Combat/PlayerGuard.cs`** —— 實作 `IBladeClashReceiver.TryResolveClash`（前一則已列）；修 `_guardStartTime` sentinel `-1f`→`float.NegativeInfinity`，`BladeClashUtility.WithinParryWindow` 拿掉 `guardStartTime < 0` 特判（會誤殺 session 早期的小值，改由上界自然過濾）。
- **`Combat/PlayerGuardAnimatorLink.cs`**（新）+ **`NewAnimator.controller`** 加 `IsGuarding` bool（Player 專用；共用 controller 的中立者1/守望者不設它）。`Parried` event → 可選 `GuardParry` trigger（param 不存在就跳過）。Phase 3 接真 clip 用。
- **`Combat/PlayerGuardVisualizer.cs`** —— 依 `CurrentDefense` 換色：Parry 白、Guard 藍、None 隱藏。
- **`Editor/Bootstrap/PlayerDeflectSetup.cs`**（新）—— 選單 `Tools/Live2DAction/Wire Sekiro Deflect Into GreyboxTest`（可重跑）。已執行：`PlayerHurtbox`→`PlayerHurtbox` layer、`BladeHitbox`→`BossWeapon` layer、Player 加 `GuardVolume`（追蹤 katana `BladeMesh`，local X 2..18、radius 0.13）、`PlayerGuard.guardArcDegrees=120` / `parryWindowDuration=0.12`、`PlayerGuardAnimatorLink` + `IsGuarding`。
- **驗證**：EditMode **257/257 綠**、零編譯錯誤。Play（frame-frozen，直接呼叫 `TryResolveClash` 驗解析）：S1 按下 0.05s 內＋正面 → **Parried**、Boss 架勢 0→14、`Parried` event、`NotifyParried` 有設 `_forcedHitReactionPending`；S2 提早按住(0.5s) → **Guarded**；S3 沒防禦 → None；S4 背後命中 → None；S5 連續兩次 → 第 2 次冷卻擋掉（架勢 +0）。
- **待使用者在有焦點 Editor 手動 Play**：實際揮刀時 sweep 抓不抓得到 guard volume（物理整合，frozen 測不了）、彈反/格擋手感、Boss `HitReaction` 反應、`guardArcDegrees=120` 是否太窄、GuardVolume 的 `bladeLocalStart/End` 覆蓋範圍。

### Phase 2 交付（視聽回饋 + debug + spec C gate）
- **`Combat/PlayerClashFeedback.cs`**（新）—— 訂閱 `PlayerGuard.Parried`/`Guarded`（帶交點），在**武器交點**（非玩家中心）放火花 + 播音效。兩階：Guard 短白黃火花 8-12 顆 + 較鈍音；Parry 亮白核心 22-30 顆 + 較脆音（pitch range 1.03-1.14 vs guard 0.90-1.02）。全 Inspector 指派（PS/clip/source/`feedbackCooldownSeconds` 0.1）—— 無硬編路徑。setup 建 `Player/ClashFeedback`（AudioSource + `GuardSparks`/`ParrySparks` 程序化 additive PS，比照 `HitEffectSetup`），兩個 clip 都接 `KatanaClash.mp3`（parry 靠 pitch 區分）。
- **`Combat/SekiroDeflectDebug.cs`**（新，F9 切換）—— Gizmos：玩家刀根→刀尖線、`GuardVolume` capsule、正面 120° 扇形、每個 active `BossHitbox` 的上一段 translation-sweep 路徑（新增 `BossHitbox.LastSweepFrom/To/HasSwept`）、最近命中點（Parry 白/Guard 青/Hit 紅）。OnGUI：`CurrentDefense` + 最近 outcome（None/Parry/Guard/Hit）+ 點 + 幾秒前。
- **`Combat/PlayerGuard.ModifyIncoming`** —— 加 `if (WasBossWeaponStrike(incoming)) return incoming;`：刀繞過 guard volume 打到身體 = **全額傷害**（spec C），只有踢擊等非刀正面命中才軟擋。物理步序：`PlayerGuardVolume` FixedUpdate(-50) 先定位 → `BossHitbox` sweep → `OnTriggerEnter`，所以 sweep 先攔 clash，被攔的目標進 `_hitTargetsThisActivation` 後 `OnTriggerEnter` 的身體命中會被去重擋掉。
- **`PlayerGuardClashSfx` 退役**（.cs 留磁碟）—— 它 gated 在 `ActiveWindowPart==Weapon` 的 `Blocked`，加了 gate 後刀不再走 `Blocked` → 全 inert。setup 刪 `Player/GuardClashSfx` child，`ClashFeedback` 取代。
- **驗證**：EditMode **257/257 綠**。Play（frozen，直接觸發 Parry）：`ParrySparks` 在交點噴 22 顆、AudioSource pitch 1.09、`SekiroDeflectDebug._lastOutcome=Parry` + 點記錄正確。
- **待使用者手動 Play**：火花/音效手感、扇形/sweep debug 顯示、spec C（刀繞過防禦）實測。

### 追加94 Phase 1b 續 — 修「玩家防禦都沒觸發（沒聽到防禦音效）」
使用者實機回報。診斷：`GuardVolume` 第一版沿 katana `BladeMesh` 的 local 軸展開，但這支佔位刀在 rest/guard pose **朝上朝後**（`GuardTip` z=−0.67，在玩家背後），而且只有 ~1.2m、y 1.0–1.4；武士刀 sweep 一律從前方（+Z）來 → **guard volume 永遠碰不到**。而且 Phase 2 剛加的 spec-C gate 讓沒攔到的刀直接全額打身體。
- **`PlayerGuardVolume` 改**：不再沿刀的 local 軸，改成 **hand-anchor（`WolfsGravestone` 刀柄骨，跟著手臂動）＋朝 `facing.forward` 延伸的斜向 shield zone**：`nearRise` 0.9（腰）、`reach` 1.4、`farRise` 1.6（tip 高過頭）、`radius` 0.4。實測覆蓋 y 1.7–4.1 / z −0.4–1.8 —— 夠高攔得到 4× 武士的刀。理由：這支佔位刀的 pose 不可靠，「正面 120° + 0.12s parry window」才是真正的 block gate；蠻大的正面 zone 反而是隻狼的正確手感。此偏離已記錄。
- **驗證**：Play（frozen，手動步進 `BladeHitbox.FixedUpdate` 掃過 shield zone）：Parry swing → `Parried` event ×1、`ParrySparks` 噴、audio pitch 1.09；Guard swing → `Guarded` event ×1、`GuardSparks` 10 顆、audio pitch 0.94。**物理 sweep→clash→火花/音效 全鏈路通。** 257/257。
- 仍待使用者實機：實際戰鬥中手感、shield zone 會不會太寬鬆（可在 `PlayerGuardVolume` Inspector 收）。

### 追加94 續 2 — 使用者實機回報 3 點修正
1. **武士的踢擊也要能彈反** —— `BossHitbox` 的 clash 路由從只認 `part==Weapon` 改成 `IsClashablePart()`（Weapon + 雙手 + 雙腳；只有 `LandingAOE` 震波和純 `Body` 不可彈）。
2. **玩家很靠近武士時防禦碰不到攻擊** —— `PlayerGuardVolume` 從「錨在刀柄骨、朝前延伸」改成 **錨在玩家身上的 shield zone**：`nearHeight` 0.8（腰）、`backReach` 0.35（近端在胸口後方，貼身攻擊也在體積內）、`reach` 1.5、`farHeight` 3.4（遠端高過頭）、`handLean` 0.35（保留一點跟手臂偏移的感覺）、`radius` 0.45。實測 point-blank（武士 1.2m）：覆蓋 y 1.4–4.9 / z −0.7–1.9。刀 + 踢擊 point-blank 都能彈反。
3. **攝影機視角有點晃** —— 一般格擋 `guardShakeAmplitude` 0.04→**0**（spec 只要求彈反震鏡；每次擋刀都震＝畫面抖）、`guardHitStopScale` 新欄位 = 0.4（原本用 `HitStopController` 預設 0.05＝硬凍 95%，太頓）；彈反 `parryShakeAmplitude` 0.12→0.06、`parryHitStopScale` 0.15。都 Inspector 可調，setup 一併寫入。
- 驗證：Play（frozen，手動步進）踢擊彈反 ✓、point-blank 刀彈反 ✓、shake 值套用 ✓。**257/257**、場景已存。

### 追加94 續 3 — 修「沒辦法用單點防禦按鍵彈反（隻狼 tap-deflect）」

**不是使用者技術問題，是真 bug。** 舊邏輯：`CurrentDefense`/`BladeClashUtility.Classify` 都要求 `GuardPressed` **當下按著**才會考慮彈反，而且 `GuardVolume` 只在按著時啟用。玩家「輕點」（按下→馬上放開）→ 刀命中那一刻按鍵已放開 → `IsBlocking` false → 直接吃刀。
- **`BladeClashUtility.Classify`** 簽章改 `(isFrontal, withinParryWindow, guardHeld)`：彈反**只看 press-edge 有沒有落在 window 內**，按鍵放不放開無所謂；只有持續格擋（Guard）要按著。
- **`PlayerGuard`**：加 `InParryWindow`（純 0.18s 計時器，從 press-edge 起算，不管 hold）；`CurrentDefense` = InParryWindow→Parry / IsBlocking→Guard / else None；`Update` 放開按鍵**不再**清 `_guardStartTime`（只有死亡/硬直清）；程序化持刀 pose 在 parry window 也會 flash 一下（tap 也看得到抬刀）。
- **`PlayerGuardVolume`**：`guard.IsBlocking || guard.InParryWindow` 時啟用（tap 也要有 volume 讓 boss sweep 抓）。
- **`parryWindowDuration` 0.12→0.18**（~11 幀，比較打得中；spec 的 0.12 只是起點）。
- 驗證：Play（frozen）**輕點→放開、刀 0.08s 後命中 → `Parried` ✓**；輕點太早（0.3s 前）→ None，正常吃刀 ✓；按住 0.5s → Guarded ✓。**256/256**（少 1 是合併了一個 Classify 測試）。

## 2026-09-01 (追加94 Phase 3) — 防禦動畫（`防禦.zip` → Guard.fbx）

使用者提供 `防禦.zip`（Meshy `Meshy_AI_Parkside_Portrait_biped` 動作，同「連續刺刀」來源）。附註「動作遊戲的防禦彈反是一瞬間的事，要考慮動作長度與彈反的適配性」。

### 評估（離線 `AnimationMode` 量測）
2.03s / 62 幀 Humanoid。**root 漂移僅 ~0.15m**（vs ContinuousThrust 的 1.5m）、**chestYaw 穩定 6–19°**（不旋身）。結構：中性 → 雙手抬到胸口（nt 0.1–0.2）→ 收到腰前雙手持握 → **穩定 hold（nt 0.5–1.0）**。剛好是 spec 的三段防禦。**可用。**

### 交付
- **`CombatAnimations/Meshy/Guard.fbx`** —— 匯入 Humanoid + `lockRootPositionXZ`（原地化）+ clip 名 `Guard`。`ASSET_LICENSES.md` 已登記（Meshy 付費方案、可 ship）。
- **`NewAnimator.controller`** 加 Player 專用 2 state（比照 追加87 ExecuteThrust 的做法，中立者1/守望者不設參數故不受影響）：
  - `Guard` —— Guard clip、`cycleOffset 0.5`（**直接從「收到腰前 hold」那段起播，跳過抬手 wind-up**，0.09s blend 內就到位）、非 loop（clamp 在最後一幀＝hold）；`AnyState → Guard [IsGuarding]`（不自我打斷）、`Guard → Locomotion [IsGuarding IfNot]`。
  - `GuardParry` —— 同 clip、**`speed 2.4`**（抬手在 ~0.16s 內完成 ≈ 0.18s parry window）、one-shot、`AnyState → GuardParry [GuardParry trigger]` blend 0.03、`GuardParry → Locomotion` exitTime 0.35（還按著的話 AnyState 再抓回 Guard）。
  - 新 trigger param `GuardParry`。
- **`PlayerGuard`** 加 `useProceduralPose` bool（`PlayerDeflectSetup` 設 false）—— 有真 clip 後停用 `LateUpdate` 的程序化 2 骨 pose（否則兩者打架）。找不到 Guard.fbx 時 fallback 回程序化。
- **`PlayerGuardAnimatorLink`**（既有）驅動：`IsGuarding` bool ← `guard.IsBlocking`；`Parried` event → `GuardParry` trigger。
- **對「一瞬間」的適配**：Guard 用 `cycleOffset` 跳過 wind-up 直接到 hold；Parry 用 `speed 2.4` 壓縮成快閃、one-shot 不逗留。
- 驗證：Play（手動步進 Animator）`IsGuarding=true → Guard state` ✓、`GuardParry trigger → GuardParry state → 回 Guard` ✓、`useProceduralPose=false` ✓。**256/256**、零編譯錯誤、場景+controller 已存。
- **待使用者手動 Play**：hold 姿勢像不像持刀防禦、parry 快閃節奏對不對。

### 追加94 Phase 3 續 — 使用者實機回報 2 點
1. **一直按著就把手舉著、鬆開才放下** —— 原 `Guard` state 播完整 clip（會慢慢把手放下）。改成播新的**短循環 sub-clip `GuardHold`**（Guard.fbx 第 7–15 幀 = 雙手舉到胸口的峰值，`loopTime`）→ 按著手就一直舉著，鬆開才 `Guard→Locomotion`(0.15s) 放下。AnyState→Guard blend 0.14s 當「抬刀」。
2. **點按判定不了彈反** —— 放寬：
   - `parryWindowDuration` 0.18→**0.28**（~17 幀）
   - 新 `tapGuardWindowSeconds` **0.55** —— 點按稍微失準（過了 parry window 但還在 0.55s 內）**仍算一般格擋**（軟擋），不會直接吃滿刀
   - `clashCooldownSeconds` 0.1→**0.06**（多段攻擊每段各自結算得到）
   - `GuardVolume` 啟用條件從 `IsBlocking || InParryWindow` → `IsBlocking || InTapGuardWindow`（點按後 0.55s 內 volume 都在，sweep 抓得到）
   - `PlayerGuardAnimatorLink`：`Guarded`（非按住時）也觸發 `GuardParry` flash，所以點按格擋也有手臂動作
- 驗證：Play（frozen）點按放開 0.10s/0.25s → **Parried**；0.40s → **Guarded**（grace）；0.70s → None；按住 → Guarded；held → Guard state 循環 `GuardHold`。**256/256**、場景+controller 已存。

### 追加94 Phase 3 續 2 — Guard Collider 改回「貼刀身」（使用者明確要求，只補強判定不重做攻擊/生命）

`PlayerGuardVolume` 從「錨在玩家的 shield zone」改回**貼著武士刀刀身的細膠囊**（`GuardWeapon` layer）：
- 沿刀刃方向的 CapsuleCollider：`bladeStart` 0.12（跳過握柄）、`radius` 0.2（細）、追隨 `weaponMount`（`WolfsGravestone`）。
- **防禦時把 `weaponMount` 轉向**（`FromToRotation` blend，`poseBlendSpeed` 12）—— 讓刀尖朝前上方（上段の構え）；放開就 blend 回、動畫接手。視覺刀跟著轉。
- 啟用：`IsBlocking || InTapGuardWindow`（按住或點按 grace 內）+ pose weight > 0.15；放開/受擊/崩解/死亡即關。
- `OnDrawGizmos`：畫刀根→刀尖線 + 兩端 wire sphere + 正面 120° 錐，依狀態變色（灰=關/藍=Guard/白=Parry）。
- Inspector：`bladeStart`/`bladeEnd`/`radius`/`bladeRise`/`poseBlendSpeed`/`drawGizmo`；角度在 `PlayerGuard.guardArcDegrees`。

**⚠️ 一個必要偏離**：`bladeEnd` = **3.2m**（遠超視覺刀身 ~0.9m）、`bladeRise` 2.6（刀近乎垂直向上）。原因：**4× 武士的刀刃有效幀落在世界 y≈2.5–4.5 的柱狀範圍**（正常身高玩家的頭頂上方），真刀身長度的 collider 物理上搆不到。這條 collider 是「沿刀刃方向、細、隨武器+動畫」（符合 spec 精神），只是**比視覺刀長很多**。等**武士刀刃攻擊的 hit window 重新對準玩家高度**（改 `BossAttackDefinition`，非重做攻擊）後就能收成真刀長。

- **BossHitbox** 不變（既有刀根→刀尖 sweep，`TryResolveBladeClash` 已優先檢測 guard volume；`IsClashablePart` = 刀+手+腳）。命中 → 交點火花 + `KatanaClash.mp3` + `clashCooldownSeconds` 0.06 冷卻。
- **驗證**（Play，乾淨場景）：正面 overhead / chest 揮刀 → **Parried** ✓；背後命中 → P=0 G=0（正常受傷）✓。**256/256**、場景已存。
- **待使用者手動 Play**：上段舉刀姿勢觀感、彈反命中率、`bladeRise`/`bladeEnd` 微調（`PlayerGuardVolume` Inspector）。

### 追加94 Phase 3 續 3+4 — Guard Collider 拉鋸戰的結論：改回「守備範圍體積」

**續 3（已 revert）**：試著把 4 個 `Wushi_Attack_*.asset` 的 `hitWindows` nt 值重新對準玩家高度，讓 Guard Collider 能收成貼刀身。用離線 `AnimationMode` 量測 —— 但這些 Meshy 攻擊 clip 帶 root motion，離線 sampler 的 XZ/rotation 偏移讓量測不可信，且 `Wushi_Attack_DoubleCombo` 的 designNotes 早就寫過「clip 起點在 root 後方 ~3 units、`useRootMotion=0` 下刀刃連身體都勉強打得到」。→ **4 個 window 值全部 `git checkout` 還原**（原值是 2026-08-26 相對 root 正確量測過的）。

**續 4（定案）**：`PlayerGuardVolume` 從「貼刀身細膠囊」改回**錨在玩家的守備範圍膠囊**（`nearHeight` 0.9 / `backReach` 0.35 / `reach` 1.5 / `farHeight` 3.4 / `handLean` 0.35 / `radius` 0.45，覆蓋 rel-y 0.5–3.9）。
- **原因**：4× 武士的刀刃攻擊**連打到玩家身體都是勉強的**（Meshy clip「起點在 root 後方」的老問題），一條貼刀身的細 collider 完全攔不到頭頂砸下來的攻擊。用真實 trajectory（從唯一一次可信的 real-FSM 量測：刀刃 rel-y 1.5–3.5、rel-z −0.5–0、砸在玩家頭上）測 → shield zone **`Parried=1`、火花 29 顆、音效 pitch 1.05 有播** ✓。
- **保留**：`rotateWeapon`（防禦時把視覺武士刀轉成上舉朝前，看起來像持刀防禦，`bladeRise` 1.1）、`GuardWeapon` layer、`OnDrawGizmos`（刀線+球+正面錐，依狀態變色）、Inspector 全欄位。
- **這就是「你聽不到彈反聲音」的原因**：續 2 的貼刀身 collider 太細太窄、頭頂攻擊全數穿過。改回大體積後，直接呼叫 + 真實 trajectory 測都有 Parried + 音效。
- EditMode **256/256**、場景已存、window asset 已還原。
- **待使用者手動 Play** 確認實機命中率；F9 debug 看藍體積 vs 紅 sweep。真的要「貼刀身精準判定」得等有正常比例的 boss + 對玩家高度校準過的攻擊。

### 追加94 Phase 3 續 5 — 隻狼式「防連按」+ 彈反窗口回到 0.2s（使用者引用隻狼資料拆解）

隻狼標準彈反窗 ≈ 12 幀 @60 = **0.2s**；且**反覆放開再按會逐步縮短彈反窗、最差變 0，成功彈反後恢復**。照這個設計：

- `PlayerGuard.parryWindowDuration` 0.28→**0.2**（base）。
- **`_parryScale` (0..1)**：實際彈反窗 = `parryWindowDuration × _parryScale`（`EffectiveParryWindow`）。
  - 按下的那次若距上次按下 < `mashResetSeconds`(0.35s) → 算連按 → `_parryScale -= mashShrinkPerTap`(0.4)，下限 `minMashScale`(0=可歸零)。
  - 不連按時 `_parryScale` 以 `mashRecoverPerSecond`(1.2/s) 回充向 1。
  - **成功彈反** → `_parryScale` 直接回 1（`restoreScaleOnParry`）。
- 沒抓準的連按（過了縮短後的彈反窗但還在 `tapGuardWindowSeconds` 0.55s 內）→ 一般格擋（玩家架勢 +6）—— 這就是「亂點的代價」：變擋不變彈、累積自己的架勢。
- `SekiroDeflectDebug` OnGUI 顯示 `ParryWin: XXXms (xScale)`，連按縮短時變黃/紅。
- 全部 Inspector 可調（`PlayerGuard` 的「Anti-mash」區）。

**實測**（Play）：間隔 0.5s 按 → scale 1.0、窗 200ms；連按 4 下（0.1s 間隔）→ scale 1.0→0.6→0.22→**0**、窗 200→120→45→**0ms**；scale 0.2 時成功彈反 → 立刻回 1.0。EditMode **256/256**、場景已存。

### 追加94 續 7 — 武士攻擊調整（使用者實機演藝後）

1. **OverheadSlam 改為每 30 秒定時觸發**（原本在隨機池）。`BossStateMachine` 加 `periodicSlamAttack` 欄位 + `periodicSlamIntervalSeconds`(30) + `_periodicSlamTimeAccumulated`/`_periodicSlamPending`（比照 `breakdance`/`leapSlam` 的 combat-time timer pattern）+ `TryEnterPeriodicSlam()`（優先序在 breakdance/leapSlam 之後、普通攻擊之前，`BeginAttack(periodicSlamAttack)` 走一般 Attack state）。timer 在 disengage/getting-up/CancelAllPending 一併清。GreyboxTest 的 `武士`：`Wushi_Attack_OverheadSlam` 從 `normalAttackPool` 移除、接到 `periodicSlamAttack`。屁孩王共用 `BossStateMachine` 但欄位留 null → 不受影響。驗證：`TryEnterPeriodicSlam` pending 時 → `Attack` state、`_currentAttack = OverheadSlam`、pending 清、timer 歸 0。
2. **SwordJudgment 放慢**（使用者：「快速連劈…看不見完整揮刀軌跡…很難反應」）：`Wushi.controller` 的 `Wushi_SwordJudgment` state speed **1.35 → 1.0**（clip 實際長度 2.44s → 3.30s，揮刀軌跡更看得清、反應時間 窗1 0.74s→0.97s / 窗2 1.79s→2.42s）。DoubleCombo（也是 2 窗連劈）暫時不動，待使用者確認要不要一起放慢。

EditMode **256/256**、零編譯錯誤、`Wushi.controller` + GreyboxTest 場景已存。

### 追加94 續 8 — 武士刀刃有效傷害幀全面收緊 + 對齊（使用者定原則）

使用者定案：一般斬傷害窗 0.10–0.16s、重斬 0.14–0.20s、同一次攻擊對同目標只結算一次、彈反窗 0.20s。

**「有效幀持續」= 傷害判定開啟時間**（`BossHitbox` collider 啟用 + swept CapsuleCast 掃描的那段），不是揮刀視覺時間。**「只結算一次」已內建**：`BossHitbox._hitTargetsThisActivation`（`HashSet<Transform>`，`Activate()` 清空）—— 每個 hit window activation 對每個 target root 只結算一次（傷害/格擋/彈反共用同一個去重）。多段攻擊（SwordJudgment/DoubleCombo）的每個 window 各自 activate → 各自結算一次，是刻意的連段。

用 **blade-rel-Hips（去 root-drift）** 逐幀量測每招刀刃在有效幀時的高度，把 window 收到「刀刃真的掃過玩家軀幹/頭高（rel-hips y ≈ −2~+0.5）且還在移動」的那段：

| 招式 | 舊 window | 新 window | 出手→命中 | 傷害窗 |
|---|---|---|---|---|
| SwordJudgment（重斬，speed 1.35→1.0）| 0.16-0.21 / 0.59-0.72 | **0.175-0.225 / 0.61-0.66** | 0.93s / 2.36s（前刀後 1.44s）| **165 / 165ms** |
| DoubleCombo（一般，speed 1.4）| 0.23-0.31 / 0.60-0.66 | **0.24-0.32 / 0.61-0.68** | 0.70s / 1.23s（前刀後 0.53s）| **115 / 101ms** |
| ChargeCut（一般，speed 1.3→**1.15**）| 0.19-0.29 | **0.21-0.28** | 0.57s（放慢後 0.53→0.57）| **124ms** |
| OverheadSlam（重擊，speed 1.4）| 0.55-0.66 | **0.56-0.64** | 1.41s | **145ms** |
| SpartanKick（踢擊）| — | 未動 | 0.57s | 109ms |

- **舊 window 常常在刀刃還在頭頂/身後就開判定** → 這是「還沒出手就被擊退」「刀還沒落就受傷」的主因。新 window 對齊刀刃到位那一下 → 應改善。若還有殘留，`knockbackForce`（SwordJudgment 4、DoubleCombo 3）可再降。
- `Wushi.controller`：`Wushi_SwordJudgment` speed 1.35→1.0（使用者：連劈太快看不見軌跡）、`Wushi_ChargeCut` 1.3→1.15（使用者：0.5s 偏快，給更明顯蓄力）。
- ChargeCut 的「刀身發光/蓄力音效」telegraph = 待做（VFX）。
- EditMode **256/256**、4 個 asset + controller + 場景已存。
- **待使用者手動 Play**：F9 看每招有效幀時紅 sweep 是否貼在玩家身上（不再提前）；SwordJudgment 0.93s 首刀會不會太慢（想要 0.8 附近就把 speed 調 1.1）。

### 未做
ChargeCut 蓄力 telegraph VFX；一般格擋的「被推開」反應（spec B）需另一段 clip；`BeginGuard`/`EndParryWindow` Animation Event（窗口是純程式碼計時器，spec 允許）。

### 追加94 續 9 — 戰鬥系統現況說明文件（給 AI 分析）

使用者：「把目前 玩家 武士 物件跟所有相關戰鬥系統機制的繼續細節整理一個說明文件 我要給 ai 分析」。

- 新增 `Docs/COMBAT_SYSTEM_SNAPSHOT.md`：把玩家 / 武士兩個 GameObject（階層、每個元件的場景序列化值）、傷害管線（`DamageInfo`→`Health.ApplyDamage`→`IIncomingDamageModifier`）、`StancePoise` 架勢系統、`BossHitbox` 掃掠判定 + 一次性去重、隻狼式彈反系統全套（`BladeClash.cs` / `PlayerGuard` 窗口與 anti-mash / `PlayerGuardVolume` / spec C routing / `PlayerClashFeedback` / `SekiroDeflectDebug` / Animator Guard-GuardParry state）、`BossStateMachine` 21 state + cascade + 攻擊選擇 + 週期計時器、`Wushi_Tuning` 關鍵值、武士每招 hit-window/傷害/knockback/反應時間表、三個 combat layer、`HitStop`/`CameraShake`/`Execution`/`Knockback`、以及所有已知限制（刀身級 guard 做不到、Meshy root-drift、失焦幀凍結、共用 Animator、手調值權威清單）攤平成一份參考文件。
- 純文件，無程式/場景改動。
- 過程中確認：`BossAttackDefinition.startupSeconds` / `recoverySeconds` 在 `BossStateMachine` 中**未被消費**（純 metadata）；真正命中時機只由 `hitWindows` normalized 區間 + clip 長度 + Animator state speed 決定（`t_接觸 ≈ 0.08 crossfade + startNormalized × clipLen ÷ stateSpeed`）。

### 追加94 續 10 — 玩家左鍵揮刀音效（`9月1日.mp3` → `KatanaSwing.mp3`）

使用者提供 `9月1日.mp3`（0.76s / 44.1kHz 立體聲）當左鍵攻擊音效。背景：追加85 的 `KatanaClash.mp3` 在 2026-09-01 移去做防禦/彈反刀刃碰撞聲（`PlayerClashFeedback`），左鍵揮刀從此無聲。

- 匯入 `Assets/_Project/Audio/Combat/KatanaSwing.mp3`（import：forceToMono + DecompressOnLoad + PCM，同 `KatanaClash` 慣例）。
- 新增 `PlayerAttackSfx.cs`（`Combat/`，`[RequireComponent(AudioSource)]`）：訂閱 `PlayerCombat.Hit`，每個 Active combo 段落播一次（**揮空也播**——這是刀劃過空氣的聲音，不是命中聲），隨機 pitch 0.92–1.10、volume 0.85。
- 新增 `PlayerAttackSfxSetup.cs`（選單 `Tools/Live2DAction/Add Player Katana Attack SFX`，可重跑）：在 `Player/AttackSfx` 子物件建 3D `AudioSource`（min/max 3/45、log rolloff、blade 高度 y≈1）+ `PlayerAttackSfx`，wire `PlayerCombat` + clip，存 `GreyboxTest.unity`。
- 已跑選單：`Player/AttackSfx` 就位、clip 0.758s mono、無編譯錯誤。
- 場景 Player 子物件：`AttackSfx`（左鍵揮刀）＋ `ClashFeedback`（防禦/彈反刀刃碰撞）並存，各司其職。
- **待使用者對焦 Editor Play 實聽**：左鍵連段音效節奏、與 `KatanaClash` 防禦聲是否會混淆。
- `ASSET_LICENSES.md`：新增 `KatanaSwing.mp3` 列 + 更新 `KatanaClash.mp3` 列（用途改為防禦/彈反）。

### 2026-09-01 — 外部 AI 戰鬥系統改造規格歸檔

使用者提供 `Wushi_Combat_System_Engineering_Spec_v1.0.docx`（外部 AI 依 `COMBAT_SYSTEM_SNAPSHOT.md` 產出的 9 項改造工程規格，M1–M5，共 63 點）。pandoc 轉 Markdown 歸檔 `Docs/WUSHI_COMBAT_ENGINEERING_SPEC.md`，加進 `CLAUDE.md` 文件索引。9 項：1 DeflectReaction／2 Tap Guard 一致性／3 Boss 旋轉 Sweep／4 玩家武器 Sweep／5 Boss 空間位移比例／6 格擋架勢用每招 Poise／7 處決生命節點永久死亡／8 特殊招式排程＋架勢單一權威／9 最終數值調校。實作進度以本 CHANGELOG 為準。

### 追加94 續 11 — spec 項目 6：一般格擋改用每招 PoiseDamage（M1）

規格 §7：先前所有一般格擋固定加玩家 6 點架勢，輕斬/重劈/大招對防禦壓力幾乎一樣。改為由該招自己的 poise 決定。

- `PlayerGuardUtility.GuardPoiseGain(attackPoise, guardMul, fallback)`（新純函式）：`attackPoise > 0 ? attackPoise : fallback`，再 × `guardMul`（負值 clamp 0）。
- `PlayerGuard`：新 `[SerializeField] guardPoiseMultiplier`（預設 1，"Clash outcome - Guard" header）。
  - Guarded 分支：`stance.AddPostureDamage(GuardPoiseGain(info.PoiseDamage, guardPoiseMultiplier, guardPlayerPoiseDamage))`——`info.PoiseDamage` 是 `BossHitbox` 已算好的 `BasePoiseDamage × window.damageMultiplier`（`BladeClashInfo` 早就帶著）。
  - `ModifyIncoming`（踢擊軟格擋）：poise 改用 `incoming.ExplicitPoiseAmount ?? FullPoiseAmount(...)` 再過 `GuardPoiseGain`。**行為變更**：被格擋的 SpartanKick 玩家架勢 5 → 14（本來 `Amount×0.2`，現在用該招 poise 14）。
  - 舊 flat `guardPlayerPoiseDamage`（6）降級為「該招無 poise 時的 fallback」，數值不變。
- `PlayerDeflectSetup.cs`：重跑時寫入 `guardPoiseMultiplier = 1`。
- **Parry 完全不變**（玩家 +0、Boss `AddPostureDamage(14)`）；血量傷害不變；FSM 不變。
- 序列化遷移：只加一個 MonoBehaviour float 欄位，預設 1（缺欄位回落 C# 初始值）。
- **玩家 guard-break 縮短**（2026-09-01 續）：場景 Player `StancePoise.staggerDurationSeconds` **6 → 1.2s**（原本沿用 Boss 處決僵直的 6s，但沒有東西會處決玩家；spec §7.4 建議 0.8–1.5s）。`maxStance` 100 不變。GreyboxTest 已存。精確秒數留待 spec 項目 9 調校。
- per-window `guardPoiseMultiplier`（spec 7.2）依 spec「第一版 = 1.0」先略過。
- 測試：`PlayerGuardUtilityTests` +3（`GuardPoiseGain`）→ **EditMode 262/262 全綠**。`PlayerGuardPlayModeTests` +2（Guarded 重招比輕招多加架勢；Parry 玩家架勢不動、Boss 架勢上升；已移除會在凍結 play 卡死的 `WaitForSeconds`，改用 `clashCooldownSeconds=0`）——MCP PlayMode runner 持續卡 `tests_running`（本專案老問題），**待使用者從 Test Runner 視窗手動跑**。
- **待使用者 Play 測**：連續格擋 SwordJudgment（22）約 5 次觸發 guard-break（現在只僵直 1.2s）；格擋 ChargeCut（12）壓力明顯較低。

### 追加94 續 12 — spec 項目 1：DeflectReaction（每個 hit-window 決定彈反是否中斷連段）（M1）

規格 §2：目前每次成功彈反都 `NotifyParried()` → 強制 HitReaction，而 HitReaction 優先序高於 Attack → DoubleCombo / SwordJudgment 被彈第一刀就整招中止。改為：彈反永遠造成架勢傷害 + 火花/音效/hitstop，但**是否中斷由每個 hit-window 自己決定**。

- `BladeClash.cs`：新 enum `DeflectReaction { Recoil=0, ContinueCombo=1, CancelAttack=2 }`。**Recoil=0 是舊行為**，所以所有沒設定的 window 自動維持原樣、零資產遷移。`BladeClashInfo` + `Reaction` 欄位（ctor 第 6 參數，預設 Recoil）。
- `BossHitWindow.cs`：+ `public DeflectReaction deflectReaction = DeflectReaction.Recoil;`（`Live2DAction.Combat.Boss` 巢狀在 `Live2DAction.Combat` 下，enum 直接可見）。所有既有 15 個 `Wushi_/PW2_` 攻擊資產的 window 缺此 key → 反序列化為 0 = Recoil = 原行為。
- `BossHitbox.TryResolveBladeClash`：把 `_activeWindow.deflectReaction` 塞進 `BladeClashInfo`。
- `PlayerGuard.TryResolveClash` Parried 分支：`NotifyBossParried(info.Attacker, info.Reaction)` → `boss.NotifyParried(reaction)`（不再無條件 Recoil）。
- `BossStateMachine`：`NotifyParried()` → `NotifyParried(Recoil)`；新 `NotifyParried(DeflectReaction)` switch：
  - `ContinueCombo` → 不動 FSM，這招後續 window 照播（架勢傷害 PlayerGuard 已加；若因此爆架勢，`TryEnterPostureBroken()`（cascade 最高優先）下一幀仍會接手）。
  - `CancelAttack` → `CancelAttackInProgress()` + 若在 Attack → `ChangeState(Idle)`。
  - `Recoil`（default）→ `_forcedHitReactionPending = true`（原行為）。
- 資產：**只改 2 個**——`Wushi_Attack_SwordJudgment.asset` / `Wushi_Attack_DoubleCombo.asset` 的**第 1 個 window** → `deflectReaction: 1`（ContinueCombo），第 2 個 window → `0`（Recoil，結束連段）。其餘 13 個資產不動（= Recoil）。符合 spec §2.3 表。
- SpartanKick / ChargeCut / OverheadSlam / LeapSlam / ContinuousThrust：維持 Recoil（spec §2.3）。
- 測試：`BladeClashUtilityTests` +3（`BladeClashInfo` 5-arg 預設 Recoil／帶明確 reaction／`BossHitWindow.deflectReaction` 預設 Recoil）。
- **尚未編譯/測試**：Unity MCP test runner 卡在 `tests_running`（`manage_editor stop` + `clear_stuck` 都沒解，連 `refresh_unity` 都被擋）——**需使用者點一下 Unity Editor 視窗解鎖**，然後編譯 + 跑 EditMode 全套 + 2 個新 PlayMode 測試。
- **驗收（GreyboxTest Play）**：彈 DoubleCombo 第 1 刀 → 第 2 刀仍照動畫發生；彈第 2 刀 → Boss 短後震、連段結束；第 1 刀彈反直接爆架勢 → 立即 PostureBroken、第 2 window 不啟用；一般 Guard 不觸發任何 DeflectReaction。

### 追加94 續 13 — spec 項目 2：Tap Guard / GuardVolume / Animator 一致性（M1）

規格 §3：`GuardVolume` 在 `IsBlocking ‖ InTapGuardWindow` 時啟用，但 Animator `IsGuarding` 只跟 `IsBlocking` → 快點放開後動畫退出防禦、膠囊還活 0.55s = **隱形格擋**。

- `PlayerGuard`：新 `DefenseActionActive`（= `CanDefend && !_defenseSuppressed && (IsBlocking ‖ InTapGuardWindow)`）——**唯一**「玩家正在防禦動作中」訊號。`CurrentDefense` 改用 `PlayerGuardUtility.DefenseStateCode(InParryWindow, DefenseActionActive)`（不再看 `IsBlocking`）。`Update` 的移動減速 + 姿勢混合改讀 `DefenseActionActive`。
- 新 `CancelDefenseAction()`：`_guardStartTime = -∞` + `_defenseSuppressed = true`（按鍵還按著也強制結束防禦；放開時自動解除）。`IsBlocking` getter 加 `_defenseSuppressed` 檢查。死亡/僵直在 `Update` 呼叫它。
- 讀取端統一：`PlayerGuardVolume.ShouldBeActive` → `guard.DefenseActionActive`（原本自己組 `IsBlocking‖InTapGuardWindow`）；`PlayerGuardAnimatorLink` `SetBool("IsGuarding", guard.DefenseActionActive)`（原 `IsBlocking`）+ 移除「按住時不播 parry flash」的 `if (guard.IsBlocking) return`（spec §3.4：彈反成功即使按著也要播 ParryImpact）。`PlayerGuardVisualizer` / `SekiroDeflectDebug` 已讀 `CurrentDefense` → 自動跟上。
- Execution / Ultimate 進場 → `GetComponent<PlayerGuard>()?.CancelDefenseAction()`（`ExecutionAbility.BeginExecution` / `UltimateAbility.ThrowSequence`）。
- Animator：`NewAnimator.controller` `AnyState→Guard` 轉場 **0.14 → 0.05s**（spec §3.4：0.14s 讓大半個 0.2s 彈反窗還在抬刀）。`PlayerDeflectSetup.WireGuardStates` 常數同步。中立者1/守望者不設 `IsGuarding` → 不受影響。
- `PlayerGuardUtility.DefenseStateCode(inParryWindow, defenseActionActive)`（新純函式，None=0/Guard=1/Parry=2）。
- **未做（spec 項目 2 Part C，需動畫素材）**：專用上半身 AvatarMask Layer + 自製 GuardImpact / ParryImpact clip（目前 parry flash 借用 `GuardParry` state）。
- 測試：`PlayerGuardUtilityTests` +1（`DefenseStateCode`）→ **EditMode 263/263 全綠**。`PlayerGuardPlayModeTests` +4（released tap 仍算 active defense／staggered 時 DefenseActionActive false／CancelDefenseAction 按著也生效+放開恢復），寫成凍結-play-耐受（純 getter + 反射設 `_guardStartTime`，無 `WaitForSeconds`）——MCP PlayMode runner 仍卡（本專案老問題），**待使用者從 Test Runner 視窗跑**。
- **驗收（Play）**：快點放開 → 姿勢/減速/膠囊/UI 同幀結束；按住 2s → 前 0.2s Parry 之後 Guard；僵直中按防禦無效；後方攻擊不算 Guard/Parry。

### 追加94 續 14 — MCP PlayMode test runner 卡死的真正解法（反射清 stale flag）

使用者問「mcp 真的沒辦法嗎」。查出 `run_tests` 一直回 `tests_running` 的根因：上一 session 被 abort 的 PlayMode run 把 `MCPForUnity.Editor.Services.TestRunStatus._isRunning`（internal static）留在 `true`，而 `run_tests(clear_stuck)` / `manage_editor(stop)` **都不碰這個 flag**（它們只清 MCP 自己的 `TestJobManager` 記帳）。`EditorStateCache` 讀 `TestRunStatus.IsRunning` → `editor_state.tests.is_running` 卡 true → Python 端 gate 擋掉所有 `run_tests`。

- **解法**（`execute_code` 反射，已驗證）：`TestRunStatus.MarkFinished()` + `TestRunnerNoThrottle.SetTestRunActive(false)` + `RestoreThrottling()` + SessionState key `TestRunnerNoThrottle_TestRunActive=false` + `TestJobManager._currentJobId=null` + `EditorStateCache.ForceUpdate()`。清完 `run_tests` 立即恢復。
- **EditMode via MCP 現在可靠**：清完後跑 263/263 → 之後 270/270 全綠。
- **PlayMode via MCP 仍不行**：清完後認真試一次（`init_timeout` 120s、editor 回報 focused），PlayMode 有進去、`Time.time` 有前進（這次沒凍幀），但 NUnit `[UnityTest]` 執行卡在第 1 個測試 `completed:0` 超過 5 分鐘（那些測試全是 `yield return null`）。`manage_editor(stop)` 解不開，又得反射清一次。**確認是死路 —— PlayMode 測試一律 Test Runner 視窗手動跑。**
- 已同步 `Docs/AGENT_NOTES.md` §3。

### 追加94 續 15 — spec 項目 4 第一步：`PlayerWeaponHitbox` + `WeaponSweepUtility`（M2，未接線）

規格 §5：玩家近戰現在是從 Player root 發 `Physics.OverlapCapsule`（Range/Radius 0.5），視覺刀刃與命中點脫鉤。項目 4 要改成沿刀身多點 swept cast（對稱 `BossHitbox`）。**這是最低風險的第一步 —— 只做元件 + 純邏輯 + 測試，不碰 `PlayerCombat`、不碰 `AttackData`、不碰場景。**

- **新增 `Combat/WeaponSweepUtility.cs`**（純 static，`Live2DAction.Combat`）：`SubdivisionCount(travel, maxSampleTravel)`（一步移動超過刀寬就細分，防快揮穿透）、`SubSegmentStart` / `SubSegmentLength`（等分子段）、`ResolveMidpoint`（無 bladeMid transform 時取 root/tip 幾何中點，spec §4.2）。
- **新增 `Combat/PlayerWeaponHitbox.cs`**（MonoBehaviour，Player 專屬、**尚未掛任何物件**）：讀 `PlayerCombat.CurrentActiveAttack`（追加93 留的 getter）當 sweep gate；每 FixedUpdate 對刀身 root/mid/tip 三點做 prev→curr 的 `SphereCastNonAlloc`（沿用 BossHitbox 的固定 buffer 無 GC 慣例）；`_hitThisAttack` HashSet 一次揮刀對同 root 只結算一次、可命中多個不同敵人（spec §5.4）；命中走 `damageable.ApplyDamage(new DamageInfo(...))` + `IKnockbackReceiver`，鏡像 `AttackResolver`。`combat.UltimateDamageMultiplier` 照乘。green gizmo（root→tip 線 + 三點球）。
- **已知限制**：靜止刀身（travel 0）的 zero-length SphereCast 對 initial-overlap 不可靠 —— 玩家近戰窗口短、揮刀時刀恆在動，暫不處理；真要「走上靜止刀刃」的 case 留到接線階段看 playtest。
- **未做（下一步）**：`PlayerCombat.useSweptBladeHitbox` feature flag（預設 false，Enemy/dummy/貓/中立者維持舊 OverlapCapsule）+ setup 選單（放 BladeRoot/Mid/Tip 空物件、掛元件、GreyboxTest Player 設 flag）+ PlayMode 測試（spec §5.4 驗收表：貼近但揮外側不中 / 刀尖穿過但 root 遠仍中 / 單次揮刀一次）。
- 測試：`WeaponSweepUtilityTests` +7（EditMode）→ **270/270 全綠**（MCP 跑過）。編譯乾淨、Console 無錯。
- 回退：元件沒掛任何物件 = 完全 dormant；`PlayerCombat` 一行沒動。

### 追加94 續 16 — spec 項目 4 第二步：接線進 `PlayerCombat` + GreyboxTest（M2）

續 15 的元件現在真的接上了。

- **`PlayerCombat.cs`**：新 `[SerializeField] bool useSweptBladeHitbox`（預設 **false**）+ `[SerializeField] PlayerWeaponHitbox sweptBladeHitbox`。`UseSweptBlade` = 兩者皆備。`ResolveActiveHit` 的 candidate 查詢包進 `if (UseSweptBlade) { hitPoints = 空 } else { 原 OverlapCapsule/OverlapSphere }` —— **flag off（Enemy / dummy / 中立者1-3 / 貓）逐位元不變**。swept 分支仍照跑尾段：whiff VFX（`AlwaysSpawnHitEffect`）+ `Hit` 事件（count 0，`PlayerAttackSfx` 揮刀聲照響）。`FixedUpdate` 加 `combat.isActiveAndEnabled` 檢查（貓附身時 `PlayerCombat` 停用，不從凍結的揮刀狀態繼續掃）。
- **`PlayerWeaponHitbox.cs`**：新 optional `hitEffectPrefab` —— 命中點 spawn 共用火花（swept 版的 `PlayerCombat.hitEffectPrefab`）。per-attack `HitEffectOverride`（劍氣類）仍是 swing-level、留給 `PlayerCombat`。
- **新選單 `PlayerWeaponHitboxSetup.cs`**（`Tools/Live2DAction/Add / Remove Player Swept Blade Hitbox`，可重跑）：在 `WolfsGravestone`（血刀 wrapper，~80x 骨骼 lossyScale）底下建 `BladeSamples/BladeRoot`+`BladeTip` 空物件 —— **世界座標放置**（沿 `-wrapper.up`，root 偏移 0.12、長 0.90），Unity 自動反推微小 localPos（避開「80x scale 甩飛 localPosition」陷阱，見 memory `player-weapon-mount`）。實測落點與可見刀刃 mesh（`defaultMaterial_2` world bounds）幾乎完全吻合（Y 0.87→1.12、Z 0.10→0.84）。掛 `Player/WeaponHitbox`（`PlayerWeaponHitbox`），wire combat / attackerRoot=Player / bladeRoot / bladeTip / 共用火花；`PlayerCombat.useSweptBladeHitbox=true` + ref。已跑，GreyboxTest 存檔（場景 +15 GO / +16 MB，含本次前既有的 deflect churn）。
- **偏離 spec §5.3 step 8-9**：沒有「新舊並行、debug 比對」——直接 flag 切換（回退 = flag 設 false 或跑 Remove 選單，舊 `OverlapCapsule` 路徑一行沒刪）。理由：並行會製造雙重傷害/困惑，flag 本身就是 A/B 開關。
- **`AttackData` 仍沒動**：玩家攻擊目前無 poise 概念；`PoiseDamage` / `SweepRadius` per-attack 欄位（spec §5.4 表）留待需要時再加。
- 測試：新增 `PlayerWeaponHitboxPlayModeTests`（4 個，spec §5.4：刀尖穿過遠目標命中 / 刀揮向外側點放不中 / 單次揮刀對同目標一次 / 一次揮刀命中兩個不同目標）—— 用 reflection 驅 `PlayerCombat` 進 Active window + `WaitForFixedUpdate` 推掃。EditMode 仍 **270/270**（`PlayerCombat` 改動有 guard，無回歸）。**PlayMode 待使用者從 Test Runner 跑**（MCP 又試一次：init timeout 120s 內連測試都沒列出來，反射清乾淨後 EditMode 照常）。
- **待使用者 Play 驗收**：GreyboxTest 揮左鍵 → 選 `Player/WeaponHitbox` 看 green gizmo 貼在刀刃上；打武士/敵人傷害正常；`BladeRoot`/`BladeTip` 位置不對就在 hierarchy 直接拖（gizmo 即時跟）。不想用 → `Remove Player Swept Blade Hitbox`。

### 追加94 續 17 — spec 項目 3：Boss 刀根/中段/刀尖旋轉 sweep（M2）

規格 §4：`BossHitbox.SweepCheck` 現在只掃 collider **中心** 的平移（外加 `distance < 0.0001` 早退）。刀繞手腕旋轉時刀柄幾乎不動、刀尖掃一大弧 → 中心掃法漏掉整條刀尖弧線（追加86 那句「刀亮紅時機正確但始終沒碰撞」的殘留成因之一）。

- **`BossHitbox.cs`**：新 `[SerializeField] bool useRotationalSweep`（預設 **false**）+ `bladeSweepMaxSampleTravel`（0.25，spec §4.2）。`SweepCheck` 重構：
  - 舊中心掃法抽成 `SweepCentreShape(distance, direction, out hitCount)` —— **邏輯逐位元不變**（EditMode 270/270 驗證），只是包成回傳 bool 的方法。
  - `useRotationalSweep && hitCollider is CapsuleCollider` → 改走 `MultiPointBladeSweep`：`CapsuleWorldEnds` 算膠囊在**上一姿勢**和**當前姿勢**的 root/mid/tip 世界座標（沿用中心掃法那套 lossyScale 縮放規則），三點各自 prev→curr 做 subdivided `SphereCastNonAlloc`（用 `WeaponSweepUtility` 的 `SubdivisionCount`/`SubSegmentStart`/`SubSegmentLength`，跟玩家 `PlayerWeaponHitbox` 同一套），依 collider 去重塞進 `_sweepHitsBuffer`。**不吃 `distance` 早退**（重點：旋轉時中心不動）。
  - 之後的 `TryResolveBladeClash` + `TryResolveHit` 迴圈兩條路共用、完全沒動。
  - `LastSweepFrom/To`（SekiroDeflectDebug overlay）旋轉路徑改設成刀尖弧線 chord。
- **新選單 `BossRotationalSweepSetup.cs`**（`Tools/Live2DAction/Enable / Disable 武士 Rotational Blade Sweep`，可重跑）：只把 root 名為「武士」且有 `CapsuleCollider` 的 BossHitbox（＝ `KatanaMesh/BladeHitbox`）設 `useRotationalSweep=true`。**已跑** —— 只有那一個 hitbox 被改，屁孩王全部 + 武士的 LandingAOE/RightFoot（sphere）維持 false。GreyboxTest 存檔（+16 行，GO count 不變）。
- **已知限制 / 偏離**：`TryResolveBladeClash` 的接觸點/最近距離估算仍用中心 delta（多點掃法下略不準，只影響火花位置）。膠囊本身很短（世界 root→tip ~1.0、r 0.32；4x 武士刀「連身體都勉強碰到」是既有問題）—— sample 弧線抓對了，長度/命中窗校準留給 spec 項目 5/9。`targetMask` 維持 `~0`（跟既有 BossHitbox 一致；追加94 已加 layer 但收窄 mask 是另一項風險，暫不動）。
- 測試：新增 `BossHitboxRotationalSweepTests`（3 個：刀尖弧線命中兩幀間會 tunnel 的目標 / flag off 平移命中照常 / 一次啟用對同目標一次）。EditMode **270/270**（`SweepCentreShape` 抽取無回歸）。**PlayMode 待使用者從 Test Runner 跑**。
- **待使用者 Play 驗收**：GreyboxTest 開武士戰，選 `武士/.../BladeHitbox` 看 SekiroDeflectDebug（F9）刀尖 sweep 線；旋轉刀招（SwordJudgment 多段斬、ChargeCut）現在刀尖掃到玩家該命中/該可彈；不想要 → `Disable 武士 Rotational Blade Sweep`。
- **M2（項目 3+4）程式完成**。下一步 spec M3 = 項目 5（Boss scale/root-motion/clip 空間，5A 程式位移→5B scale→1→5C 精確 guard），依賴 M2 除錯資料，需先讓使用者 Play。

### 追加94 續 18 — spec 項目 5 sub-step 5A：程式化攻擊位移（M3）

使用者「測試過了 繼續」（M1+M2 Play 驗收通過）。5A = 全 spec 最危險項目 5 的**安全第一小步** —— **不碰 Boss scale、不碰 shared Animator**，只加程式化前衝讓 gameplay root 追上「前進位移烤進骨骼」的 clip（ChargeCut / DoubleCombo）。

- **新 `Combat/Boss/BossAttackMotionProfile.cs`**（`[System.Serializable]`）：`moveStart/EndNormalized`、`forwardDistance`（0＝關，預設）、`movementCurve`（EaseInOut）、`stopOnDeflectRecoil`。`TravelFraction01(normalized)` 純函式 —— 窗外 clamp 0/1、窗內取曲線、結果 clamp 0..1。
- **`BossAttackDefinition.cs`**：+`attackMotion`（`new BossAttackMotionProfile()`）+ `AttackMotion` getter。`forwardDistance 0` = 每個既有攻擊逐位元不變（缺 key 反序列化成 C# 預設，`HasDisplacement` false）。
- **`BossStateMachine.cs`**：
  - `BeginAttack`：若 `attack.AttackMotion.HasDisplacement` → 鎖 `_attackMotionOrigin` + `_attackMotionDir`（flat 朝目標，或 `transform.forward`）—— **不無限追蹤**（spec §6.2）。`_attackMotionApplied` / `_attackMotionHalted` 歸零。
  - `UpdateAttack`：新第一分支 —— profile 有位移 → 依 `TravelFraction01(normalized) × forwardDistance` 算該幀應到位置，換成 `_horizontalVelocity`（`ApplyMotion` 照舊 `× dt` 消化）。取代原「plants feet / creep」（互斥）。`useRootMotion` 攻擊不走這條。
  - `NotifyParried(Recoil)`：若 `stopOnDeflectRecoil` → `_attackMotionHalted = true`（剩下的衝刺凍結，不滑穿玩家）。
  - `EndAttack` / `CancelAttackInProgress`：reset。terminal state（DodgeCounter/LeapSlam/PostureBroken/Dead/…）本來就 `_horizontalVelocity=0` + 只有 `BossState.Attack` 跑 `UpdateAttack`，安全。
- **新選單 `BossAttackMotionSetup.cs`**（`Set / Clear ChargeCut Attack Lunge (5A)`）：只給 `Wushi_Attack_ChargeCut` 寫起始 profile（`forwardDistance 5.5`、`0.38-0.92`、EaseInOut）—— 該 clip designNotes 白紙黑字「hips 前進 ~6 units、刀尖落在靜止 boss 前方 6-8m」。**已跑**，只有 ChargeCut 一個資產改。**DoubleCombo 留 inert**（它的 clip 是「起點在 root 後方 3 units 往前追」，反方向，硬推前衝會更糟 —— 留給使用者 Play 後在 Inspector 調）。
- 數值是起始值，`forwardDistance` / `moveStart` / `moveEnd` 全在資產 Inspector，Play 後調。
- 測試：`BossAttackMotionProfileTests` +6（EditMode）→ **276/276 全綠**（`BossStateMachine` 改動無回歸）。5A 的整合面（boss 真的衝過去）**待使用者 Play**。
- **待使用者 Play 驗收**：開武士戰引 ChargeCut → boss 下砍後身體跟著往前衝（不再原地、刀尖不再打空 6m 外）；彈反第一刀 → 衝刺應該停住不滑穿。不對 → `Clear ChargeCut Attack Lunge (5A)` 或 Inspector 調 `forwardDistance`。ChargeCut 第 2 段命中窗（nt 0.83-0.9，designNotes 有測量值）尚未 wire —— 想接就在資產加一個 hitWindow。
- **⚠️ 5B（scale 4→1 + 拆 Player/中立者/守望者共用 Animator）+ 5C（精確刀身 guard）各自需要另外的完整風險摘要 + 明確確認才做** —— 那才是項目 5 的大頭（spec 13 點、明訂拆兩段）。

### 追加94 續 19 — spec 項目 7：處決生命節點 + 永久死亡（M4）

使用者「先做 c」（跳過危險的 5B/5C，先做 M4 —— spec §11.1 說 M4 只依賴 M1）。規格 §8：`ExecutionAbility` 只扣當前血量 50% → 處決本身殺不死；武士 `permanentDeath=false` → 打死了 5 秒後自己復活。跟「處決 / Boss 戰結束」語意衝突。

- **新 `Core/Execution.cs`**：`enum ExecutionOutcome {Refused, PhaseTransition, Killed}`；`interface IExecutable {CanBeExecuted / OnExecutionStarted / ResolveExecution}`；`static ExecutionNodeLogic.Deathblow(remainingBefore)` 純函式（>0→減 1 + 判斷 phase/kill，0→refused）。
- **新 `AI/Boss/BossLifeNodeController.cs`**（`IExecutable`，掛武士）：`maxDeathblowNodes 2` / `remainingNodes 2` / `restoreHealthOnPhaseChange` / `executionWindupSeconds 1.7`。`CanBeExecuted` = 未執行中 + 有節點 + boss 在 `PostureBroken`。`OnExecutionStarted` → `boss.BeginExecutionHold()`。`ResolveExecution` → `ExecutionNodeLogic.Deathblow` → phase 轉換 or 永久死亡。`NodeConsumed` event 給未來 UI。
- **`BossStateMachine.cs`**：
  - `BeginExecutionHold(s)` / `EndExecutionHold()`：finisher 動畫期間凍結 `PostureBroken` 跪姿（`UpdatePostureBroken` 的「站起來」判斷加 `&& !ExecutionHoldActive`）+ `health.SetInvulnerable(this, true)`。`OnExitState(PostureBroken)` 有 safety net 解除（動畫中斷不留無敵，spec §8.3）。
  - `DeathblowPhaseTransition(restoreHealth)`：`CancelAllPending` + `CloseAllHitboxes` + （選擇性）`health.ResetHealth` + 鎖 Phase2 + `stance.EndStagger` + 全新架勢條 + `ChangeState(GettingUp)`（沿用既有起身 i-frame）。
  - `DeathblowFinalKill(executor)`：`_deathblowFinalKill = true` → 走傷害管線觸發 `Died`→`OnBossDied`→`Dead`；`UpdateDead` 的復活判斷改成 `if (tuning.PermanentDeath || _deathblowFinalKill) return;`。**只擋這一次死亡** —— 一般把血打到 0（非處決）仍照舊 5 秒復活（2026-08-24 明確需求不變）。
- **`ExecutionAbility.cs`**：新 `[SerializeField] bool instantKillNonExecutableTargets`（預設 **false**）。`BeginExecution` 抓 target 的 `IExecutable`，`CanBeExecuted` → 存 `_pendingExecutable` + `OnExecutionStarted`。`ResolveExecution`：有 `_pendingExecutable` → 交給它、`return`（它自己管血量 + stagger）；否則 fallback（`instantKill` ? 當前血量全扣 : ×0.5，**預設維持 2026-08-18 的 50%**）。`EnemyExecutionAbility` **完全沒動**。
- **新選單 `BossLifeNodeSetup.cs`**（`Add / Remove 武士 Deathblow Life Nodes (item 7)`）：只給 GreyboxTest 的**武士**掛 `BossLifeNodeController`（2 節點）。**已跑** —— 屁孩王沒掛（elite 不是戰鬥終結 boss，維持一般處決）。全部 `ExecutionAbility.instantKillNonExecutableTargets` 維持 false。GreyboxTest 存檔（GO count 不變）。
- **行為變更聲明**：武士被處決後不再無限復活（第 1 刀處決 → Phase 2 滿血重戰、第 2 刀處決 → 永久死亡）。非處決死亡不變。普通敵人處決不變（除非翻 `instantKillNonExecutableTargets`）。
- 測試：`ExecutionNodeLogicTests` +3（EditMode）→ **279/279 全綠**（`BossStateMachine` + `ExecutionAbility` 改動無回歸）。新增 `ExecutionAbilityRoutingTests`（3 個 PlayMode：executable target 只走 IExecutable 不吃 fallback / 非 executable 照吃 50% / executable 拒絕 → 退回 50%）—— **待使用者 Test Runner 跑**。
- **未做（spec §8.3 剩項）**：處決期間玩家端 i-frame（boss 已在 PostureBroken 無害，暫略）；Phase transition / permanent death 的過場信號 / Boss UI 關閉（`NodeConsumed` event 是 hook，UI 另做）；Phase 2 的實際強化（目前只換 `Phase` flag + 滿血 + 新架勢條，強度靠既有 phase-scaled tuning）。
- **待使用者 Play 驗收**：削武士架勢到爆 → 跪地 → F → 起身演出 + 滿血 Phase 2 繼續打；再削爆一次 → F → 永久倒地不復活。中途切場景/玩家死不留無敵。不想要 → `Remove 武士 Deathblow Life Nodes`。

### 追加94 續 20 — spec 項目 8：特殊招式排程 + 架勢單一權威（M4）

規格 §9。使用者「好」。

**§9.1–9.2 特殊招式排程**：Breakdance/LeapSlam/OverheadSlam 各自 15/20/30s 計時，多個 Pending 同時到期時會依 cascade 優先序**連續幀釋放**（60s 戰鬥理論上 9 次週期特殊招式）。

- **`BossStateMachine.cs`**：新 `[SerializeField] float sharedSpecialCooldownSeconds`（預設 **0 = 關 = 舊行為**）+ `_lastSpecialFireTime`。
  - `SharedSpecialReady`（走新純函式 `SpecialScheduleUtility.SharedCooldownReady`）：`cooldown<=0` 或距上次特殊招式夠久。
  - `TryEnterBreakdance` / `TryEnterLeapSlam` / `TryEnterPeriodicSlam`：pending 檢查後加 `if (!SharedSpecialReady) return false;`（pending 保持 armed，冷卻過了才發 —— spec §9.2「計時器只代表取得資格」）。發動時 `MarkSpecialFired()`。
  - `TryEnterTooCloseKick`：發動時 `MarkSpecialFired()`（**占用**冷卻，防止「踢完立刻接 LeapSlam」）但**不受**冷卻 gate（安全機制恆可用）。
  - `TryEnterUltimate`：發動時 `MarkSpecialFired()`（延後普通特殊招式，spec §9.2）。
  - **未動**：Vanish / DodgeCounter（各有自己的 cycle / reaction gating，spec §9.2 的排程模型就是週期池 + TooCloseKick + Ultimate）。
  - **未做**：spec §9.2 的 `Score: overdue + contextWeight...` 加權選擇 —— cascade `else if` 順序目前就是優先序，全 scoring 重寫留給項目 9 調頻階段。
- **新 `Combat/Boss/SpecialScheduleUtility.cs`**：`SharedCooldownReady(lastFireTime, now, cooldownSeconds)` 純函式。`SpecialScheduleUtilityTests` +3。
- **新選單 `BossSpecialCooldownSetup.cs`**（`Set / Clear 武士 Shared Special Cooldown (item 8)`）：只設**武士** = 7（spec 建議 6–10）。**已跑**，屁孩王維持 0。GreyboxTest 存檔。

**§9.3 架勢單一權威 + 重複 UltimateEnergy**：

- **架勢**：查證後 —— `StancePoise` **早就是**唯一權威（`CurrentStance` / `IsStaggered` / regen / grace 全在它自己的 `Update`；`BossStateMachine` 只讀值 + 呼叫 `EndStagger`/`RestoreStanceFractionAfterRecovery`，**沒有**第二套 regen 迴圈）。`BossTuning.postureRegenDelaySeconds` / `postureRegenPerSecond` 是**死欄位**（有 getter 沒人讀）→ 加 `UNUSED` 註解 + tooltip，不刪（免 Wushi_Tuning/PW2_Tuning 兩個資產 re-serialize）。
- **重複 UltimateEnergy**：查證後 —— spec 的前提**在本專案是錯的**。玩家 root 上的兩個 `UltimateEnergy` 不是重複：`id=4541594`（Max 100，regen 5/3s）= 必殺能量（`UltimateAbility` / `UltimateReadyAura` / 角落 HUD 讀它）；`id=4541612`（Max 500，regen 30/1s，idle-delay 3）= **飛行體力**（`CharacterMovement.flightEnergy` / 飛行 HUD 讀它）。`UltimateEnergy` class header 就說它是通用可重用元件。**不動。**
- 測試：EditMode **282/282 全綠**（`BossStateMachine` 排程改動無回歸）。整合面（兩個特殊招式不連發）**待使用者 Play**（把 `sharedSpecialCooldownSeconds` 設 7 後，戰鬥 60s 內不該看到 Breakdance→LeapSlam 貼著發）。
- **待使用者 Play 驗收**：長時間戰鬥觀察特殊招式間隔 ≥7s；踢擊後不會立刻接 LeapSlam；`[Boss FSM]` log 看 `pending=true` 但延後發動。不想要 → `Clear 武士 Shared Special Cooldown`。
- **M4（項目 7+8）程式完成。** 剩：M3 5B/5C（需風險簽核）、M5 項目 9（最終數值調校，依賴 M1–M4 全部完成 + 大量 Play 數據）。

### 追加94 續 21 — 修「玩家完全傷害不到武士」（項目 4 swept blade bug）

使用者實測回報。查出根因（兩個疊加）：

1. **`PlayerWeaponHitbox._nearestPerTarget` 會被非傷害碰撞體霸佔 slot**：SphereCast 掃到武士時常同時命中武士**自己的**攻擊 hitbox（`BladeHitbox` / `RightFootHitbox` —— 這些 collider 身上沒有 `IDamageable`，Health 在 parent）。舊碼把「每個 target root 最近的 collider」存進 `_nearestPerTarget`，若最近的是那些非 hurtbox collider → 之後 `TryGetComponent<IDamageable>` 失敗 → `continue` → **整個武士被跳過、零傷害**。
   - 修：`AccumulateSweep` 收集時就加 `if (!hit.collider.TryGetComponent(out IDamageable _)) continue;`（只認身上帶 `IDamageable` 的 hurtbox —— 跟 `AttackResolver` 同一個 gate）。武士的 `BodyHurtbox` + root `CharacterController` 都是 same-GO `IDamageable`，武士自己的攻擊 hitbox 不是。
2. **`sweepRadius 0.12` 對 4x 武士太細**：`PlayerWeaponHitboxSetup.SweepRadius` 0.12 → **0.25**，重跑選單。
   - 附帶查到既有 rig 問題（**不是本次造成**）：武士的 `BodyHurtbox` bounds 飄在 Y≈8–12（4x scale × 骨骼 local offset 甩上去），視覺身體在 Y 0.5–4.4。武士**目前只能透過 root `CharacterController` 受擊**（它在正確高度、same-GO 有 Health）。舊 OverlapCapsule 路徑也是靠打到 CharacterController。刀身 sweep 現在同樣打得到它。
- 測試：`PlayerWeaponHitboxPlayModeTests` +1（`NearerNonDamageableCollider_DoesNotBlockTheHurtboxHit` —— 較近的非 damageable 子 collider 不擋後方 hurtbox）。EditMode **282/282**。**整合面待使用者 Play 確認打得到武士**。
- **立即退路**：還是打不到 → `Remove Player Swept Blade Hitbox` 回到舊 OverlapCapsule（一行 flag，確定可用）。

### 追加94 續 22 — 武士 BladeHitbox 加長到符合可見刀身 + 攻擊距離

使用者：「武士的刀不夠長 或著是刀柄和刀尖連成的攻擊判定區域沒有做得很好 也沒有搭配武士的攻擊距離」。實測資料：

- 武士 `BladeHitbox` CapsuleCollider 世界長度**只有 ~1.0m**（local height 0.52 × 3.2 blade lossyScale），且 center=(0,0,0) 貼在刀柄側 → 蓋在 **~3m 可見武士刀**的中間三分之一，**刀尖完全沒判定**。
- 舊 tip 端離武士 root 只有 flat **2.84m**，但刀招 `maxDistance` 是 2.5–3.5（ChargeCut 3.0 / DoubleCombo 2.5 / OverheadSlam 3.2 / SwordJudgment 3.5）→ boss 從那些距離出手時刀根本搆不到。
- （這也拖累 續 17 的項目 3 旋轉 sweep —— 它 sample 這個 capsule 的 root/mid/tip，capsule 只有 1m 時「刀尖 sample」根本不在真刀尖。）

**修 —— 新選單 `WushiBladeHitboxSetup.cs`（`Resize 武士 Blade Hitbox To Visible Katana`，可重跑）**：每次執行時**現場量測** `KatanaMesh` renderer bounds，把 8 個角投影到 capsule 的 local 軸（dir 0），找出刀尖端／刀柄端，把 capsule 設成從「刀柄往刀尖跳過 14% 握把」到「刀尖」。已跑：

- capsule `height` 0.52 → **0.862**（world **~2.76m**，原 ~1.0m）；`center.x` 0 → **0.149**；`radius` 0.1（world ~0.32m）不變。
- 新 tip 端世界座標 Z=8.25 —— **可見刀尖在 Z=8.26，完全對上**。
- flat dist boss-root → 刀尖端 **3.20m**（原 2.84）。ChargeCut/DoubleCombo/OverheadSlam 的 maxDistance 現在都在刀身可及範圍內（前揮時手還會往前 → 更遠）。
- **SwordJudgment maxDistance 3.5 略超**靜止刀身 3.2 —— 它是前衝多段斬、startup 會 creep/track，實戰應搆得到；Play 若還是打空，把它的 `maxDistance` 砍到 3.2。**本次沒動任何 `Wushi_Attack_*.asset`**（它們是 `measured` 值，待 Play 數據）。
- 純場景 + 新 editor script，無 runtime 改動。EditMode **282/282**。GreyboxTest 存檔。
- **待使用者 Play 驗收**：F9 看武士刀紅色 sweep 線現在覆蓋整條可見刀身到刀尖；武士從中遠距出招打得到玩家；旋轉刀招（SwordJudgment/ChargeCut）刀尖弧線判定正常。不對 → 選 `武士/.../BladeHitbox` 在 Inspector 微調 capsule，或重跑選單。

### 追加94 續 23 — Play 回報三修：swept blade 退回 / 貓受擊 / 凍結鍵

使用者實測：「1.player 無法對任何物件造成傷害 2.cat 沒有正確受到傷害 3. 提供一個按鍵讓畫面可以直接停止(模擬 play mode stop)」。

1. **玩家 swept blade 整個退回**：續 15/16/21 的 `PlayerWeaponHitbox` 在真 Play 完全打不到任何東西（凍結 play mode 下無法 debug 根因）。跑 `Remove Player Swept Blade Hitbox` → `PlayerCombat.useSweptBladeHitbox=false`、`Player/WeaponHitbox` 移除 → **回到舊 `Physics.OverlapCapsule` 路徑（確定可用）**。swept blade 全套（`PlayerWeaponHitbox` / `WeaponSweepUtility` / `PlayerWeaponHitboxSetup` / 3 個測試）留在磁碟，spec 項目 4 待日後在真 Play 前一起重做。**`BossHitbox` 旋轉 sweep（項目 3）不受影響 —— 那是武士出招用的，仍 live。**

2. **貓加受擊框**：查證 —— 貓只有 root 上一顆極小 `CharacterController`（world Y ~0.5–1.3、寬 0.4）當受擊目標，4x 武士的刀/踢弧線從貓頭上掃過去。跟玩家早先的 `HurtboxLink` 修法一樣：新選單 `Add Cat Hurtbox`（`PlaytestFixesSetup.cs`）→ `Cat/CatHurtbox`（CapsuleCollider trigger r0.6 h1.9 dir Y，world Y **0.10–2.00**）+ `HurtboxLink` → 貓 `Health`。**已跑**，GreyboxTest 存檔。（傷害到 Health → `StancePoise.OnDamaged` 也照吃，架勢正常。）

3. **凍結鍵**：新 `Dev/DevTimeFreeze.cs`（runtime，`Live2DAction.Dev`）—— toggle `Time.timeScale` 0↔1（模擬 Play mode stop：物理/動畫/移動全停）。`OnDisable` 保證不會卡在凍結。凍結時畫面上方顯示 "PAUSED" banner。選單 `Add Dev Time Freeze Key (Backquote)` → `DevTools/DevTimeFreeze`。
   - 鍵位歷程：Backspace（使用者指出佔用 —— 是 `VehicleController` 翻車鍵）→ F10 → **`` ` `` Backquote**（`Key.Backquote`，鍵盤 1 左邊/Tab 上面，經典 dev console 鍵；使用者指定）。想換直接改 `DevTools/DevTimeFreeze` Inspector 的 `toggleKey`。

### 追加94 續 24 — 武士出招 Console log 一致化 + Portal domain-reload 例外修掉

使用者：「檢查動作名稱是對映 console，確定出招前列印出的動作是正確名稱」。

- 查證：`logStateChanges` 對武士是開的。舊 log 不一致 ——
  - 普通池 4 招：`PickAttack: chose ChargeCut (...)` + `PlayState: Wushi_ChargeCut`（兩種命名）。
  - 定時 OverheadSlam / 貼身 SpartanKick / 派生招：**只有** `PlayState: Wushi_X`（`TryEnterPeriodicSlam`/`TryEnterTooCloseKick`/`EndAttack` 直接呼 `BeginAttack`，繞過 `PickAttack`）。
- **`BossStateMachine.BeginAttack` 開頭加一行**：`Log($"BeginAttack: {attackId} (clip {ClipName})")` —— **每一條進攻路徑都會印**（池/派生/貼身/定時）。`attackId` 是招式本名、`ClipName` 是即將 CrossFade 的 Animator state。名稱對映確認無誤（attackId = ClipName 去掉 "Wushi_"，Animator state 同名）。
- **LeapSlam** 走 LeapSlamWindup→LeapSlam 不經 `BeginAttack`，但本來就有 `leapSlamPending=true` / `LeapSlamWindup: charging` / `LeapSlam: teleported to...` 明確命名 —— 不動。
- **順手修 `Portal.cs`**（**非本次 request、非我造成**）：script 在 Play 中重編譯 → domain reload 把非序列化的 `_propertyBlock` 清成 null，`Update()` 續跑 → `GetPropertyBlock` 每幀 `ArgumentNullException` 洗版。`ApplyVisualState` 加 lazy `_propertyBlock ??= new()`。這是使用者要「看乾淨 console」的直接障礙。
- EditMode **282/282**。

**武士招式名稱對照**（Console `BeginAttack: X` → 中文）：SwordJudgment=劍裁 / DoubleCombo=雙連斬 / ChargeCut=蓄力斬 / SpartanKick=斯巴達踢 / OverheadSlam=頭頂劈（定時30s）/ LeapSlam=躍擊（看 `LeapSlamWindup: charging`）。

### 追加94 續 25 — 武士 moveset 調整（移除 ChargeCut / 重命名 / DoubleCombo 放慢 / LeapSlam 50s）

使用者 4 項要求，全部生效、EditMode 282/282：

1. **移除 ChargeCut**：`Wushi_Attack_ChargeCut.asset` + `.meta` 刪除；從 GreyboxTest 武士 `normalAttackPool` 移除（pool 4→3：SwordJudgment / SpartanKick / DoubleCombo）；刪 `Editor/Bootstrap/BossAttackMotionSetup.cs`（那個選單只服務 ChargeCut 的 5A lunge profile）。**保留在磁碟**：`Wushi_ChargeCut.fbx` + Animator state `Wushi_ChargeCut`（unwired，比照 ContinuousThrust）；`BossAttackMotionProfile` + `BossAttackDefinition.attackMotion` 欄位 + 6 個測試（通用，任何攻擊可在 Inspector 設）。
2. **SwordJudgment → 蓄力斬（只改標籤，使用者選）**：`Wushi_Attack_SwordJudgment.asset` 的 `attackId` `SwordJudgment` → **`ChargeCut`**。clip / Animator state / 檔名 / 傷害 32 / 架勢 22 / 2 段命中窗 / maxDist 3.5 **全部不變**。查證 `attackId` 純作 log/label 用（無字串 key lookup，只有 `CatAttackPose` 比對貓自己的 id）。Console 現在會印 `[Boss FSM] BeginAttack: ChargeCut (clip Wushi_SwordJudgment)`。
3. **DoubleCombo 兩段劈砍間隔變長**：`Wushi.controller` `Wushi_DoubleCombo` state speed **1.4 → 1.0**。命中窗（normalized 0.24-0.32 / 0.61-0.68）不動 —— speed 降 → 整個 2 段連招在真實時間拉長 ~40%，兩刀之間的停頓明顯變長（也順帶把每刀變慢、命中窗變寬、更好看/更好彈）。**若只想要「間隔」變長不想每刀變慢**，那要動 clip 本身（另一件事）。
4. **LeapSlam 20s → 50s**：`Wushi_Tuning.leapSlamTriggerSeconds` **20 → 50**。（OverheadSlam periodicSlam 仍 30s；兩者共享 7s 冷卻不變。）

- 改動檔案：`Wushi_Attack_ChargeCut.asset`(刪) / `Wushi_Attack_SwordJudgment.asset` / `Wushi.controller` / `Wushi_Tuning.asset` / `GreyboxTest.unity` / `BossAttackMotionSetup.cs`(刪)。
- **待使用者 Play**：戰鬥不再出現 ChargeCut（原蓄力斬）；「劍裁」現在 Console 叫 ChargeCut；DoubleCombo 兩刀之間停頓變長；LeapSlam 頻率降到 50s。

### 追加94 續 26 — Play 回報三修：武士能量 50s / DoubleCombo 第二刀 / 踢擊彈反

1. **武士能量條 → 50 秒滿格**：查證 —— LeapSlam 觸發實際是走 `leapSlamEnergy`（= 武士 root 的 `UltimateEnergy`），**續 25 改的 `leapSlamTriggerSeconds` 是 fallback、被忽略**。該能量條 `max 100 / regen 5 每 1s` = 20s 滿。改 **`regenAmount 5 → 2`（每 1s）→ 50s 滿**。這條也是 `WushiBossHud/武士_能量` 顯示的條。

2. **DoubleCombo 第二刀（左下→右上 rising cut）打不到 / 紅提醒不亮 / 彈不了**：離線量測（`AnimationMode.SampleAnimationClip`）確認 —— 第二刀命中窗（nt 0.61-0.68）時刀身在 `Y 0.14-1.71`，但更關鍵是 **clip 把角色烤成「起點在 root 後方 ~3 units、往前走」**（DoubleCombo designNotes 早記過：「hit 2 blade retracts toward the body by nt 0.67」），`useRootMotion=0` → 第二刀揮出去時可見刀身已縮回武士身邊、搆不到玩家。
   - **修**：給 `Wushi_Attack_DoubleCombo.asset` 加 **5A `attackMotion` 前衝 profile**（續 18 的機制，續 25 刪的只是 ChargeCut 專用選單、機制還在）：`forwardDistance 2.5`、`nt 0.30-0.64`、EaseInOut。武士在兩刀之間往前衝 2.5 units，把第二刀的刀身帶回玩家距離。
   - `stopOnDeflectRecoil` 生效：彈反第一刀（ContinueCombo）**不**停衝刺（第二刀照樣接近）；彈反第二刀（Recoil）才停。

3. **踢擊（SpartanKick）不能被彈反**：**使用者猜「底盤較低」—— 量測後確認是錯的**。踢擊命中窗（nt 0.58-0.75）foot hitbox 在 `Y 1.4-3.0`，完全在 `PlayerGuardVolume` 的 Y 範圍內。真正原因：**`PlayerGuardVolume` 是一根陡峭的斜膠囊**（近玩家端低 Y~1.8、遠端高 Y~4.3，為 4x 武士的**頭頂刀招**調的）—— 水平的胸口高度踢擊只擦到膠囊最靠玩家的低端，那裡身體也在旁邊 → `TryResolveBladeClash` 的「body 比 guard 近就走 body hit」判定 bail 掉彈反。
   - **修**：`PlayerGuardVolume` 加粗放平 —— `radius 0.45 → 0.6`、`farHeight 3.4 → 2.9`、`nearHeight 0.9 → 0.7`。膠囊變短胖、比較水平，胸口高度的水平攻擊（踢擊 / rising cut）更容易被 sweep 掃到。

- 純資料改動（`Wushi_Tuning` 沒動、無 code）。EditMode **282/282**。改動：`GreyboxTest.unity`（武士 UltimateEnergy + Player GuardVolume）/ `Wushi_Attack_DoubleCombo.asset`。
- **待使用者 Play 驗收**：(1) 武士能量條 ~50s 滿 → LeapSlam；(2) DoubleCombo 兩刀都打得到、都能彈、紅提醒都亮；(3) 踢擊能彈反。#2/#3 是幾何微調，數字可能還要再調 —— 不對回報，或 Inspector 直接改 `Wushi_Attack_DoubleCombo.attackMotion` / `Player/…/GuardVolume`。

### 追加94 續 27 — SwordJudgment(蓄力斬) 第一段命中窗提前

使用者：「Wushi_SwordJudgment 很難彈反，紅色判定太晚，揮刀已經進行一半了才出現，應該要蓄力剛出手就開始判定」。

離線量測 `Wushi_SwordJudgment` clip（3.30s，Animator speed 1.0）刀尖軌跡：
- nt 0.00-0.05：靜止蓄力（刀在身後 -2.0）
- nt 0.08-0.14：**上抬 → 舉過頭**（tip Y 2.4→5.0、速度 16→36）← 「出手」在這裡
- nt 0.17-0.20：**下劈 crash**（tip Y 4.5→1.5、速度峰值 73.6）← 真正命中
- 舊命中窗 **0.175-0.225** 正好卡在 crash 峰值 → 玩家看到紅色時刀已經舉起+開始下劈了，反應不及。

**修**：`Wushi_Attack_SwordJudgment.asset` 第一段命中窗 **0.175-0.225 → 0.09-0.23**。紅色/彈反判定現在從刀開始上抬就亮（nt 0.09 ≈ 0.30s），一路開到下劈結束（nt 0.23 ≈ 0.76s）。
- **不會提前受傷**：nt 0.09-0.17 刀尖在 Y 3.6-5.0（舉過頭），站立玩家（身體 Y 0.3-2.1）搆不到；真正的低點傷害仍在 nt 0.20。但玩家可以在舉刀階段就 parry（guard volume 頂端 ~Y 4.4 跟舉起的刀有重疊）。
- 第二段窗（0.61-0.66 / Recoil）不動。`deflectReaction` 第一段仍 ContinueCombo。
- 純資產改動（`Wushi_Attack_SwordJudgment.asset` 一個）。無 code、無場景、EditMode 不受影響。
- **仍嫌快** → 降 `Wushi.controller` 的 `Wushi_SwordJudgment` state speed（目前 1.0，可到 0.8）讓整個揮刀更慢更好讀。

### 追加94 續 28 — Play 回報四修：踢擊彈反距離帶 / 粗糙格檔音 / 連續彈反 / 開場 360 運鏡

1. **踢擊離太近/太遠都很難彈反**：`*Cast` 忽略「開始時就已重疊」的 collider → 貼身踢擊 foot 一開始就在 guard volume 裡 → 永遠不判成 clash、直接走 body hit。
   - `BossHitbox.TryResolveBladeClash` 加 **`Physics.OverlapSphereNonAlloc` 探針**（`OverlapProbeRadius()` = collider 尺寸 + 0.35m）抓「hitbox 現在坐在 guard volume 裡」（距離 0、guard 贏）。
   - `SweepCheck` 重構：hitbox 這幀幾乎沒動時，clashable window **仍跑 clash 檢查**（讓探針有機會發動），不再直接 `return`。
2. **完美彈反音效複製 + 粗糙版**：ffmpeg `KatanaClash.mp3` → **`KatanaClash_Rough.mp3`**（0.52s、highpass 150 + lowpass 2600 悶掉金屬泛音 + acrusher grit + 壓縮 + fade）。`PlayerClashFeedback` 新 `blockClip`/`blockPitch`/`blockVolume` + 訂閱 `PlayerGuard.Blocked`（踢擊軟格檔，之前無聲）。`guardClip`+`blockClip` → rough；`parryClip` → clean。完美彈反維持清脆，一般格檔 + 踢擊格檔 = 悶悶粗糙 clank。
3. **雙連斬連續彈反**：`PlayerGuard` 新 `comboParryGraceSeconds`（預設 **0.8s**）—— 成功彈反後這段時間內，防禦沒放下 → 下一記正面 clash **自動算 Parry**（免重新 press edge），且重按防禦**不吃 anti-mash 懲罰**。`InComboParryGrace` getter。可調（太寬降 0.5）。
4. **武士開場 360 運鏡**：新 `Cutscene/IntroOrbitCamera.cs`（掛 boss-intro vcam，驅動自己 transform：0-2.5s 繞武士轉一圈 radius 4.6/height 3.1/起始角 200°，然後 0.7s smoothstep 拉到正面固定機位並保持）。`BossIntroGreyboxSetup` 運鏡段重寫：3 機位 → **單一 `CM_Vcam_Intro` + `IntroOrbitCamera`**；Timeline 4.35s = Cinemachine 單鏡 0-4.35s + 武士揮刀 clip（`Wushi_SwordJudgment_InPlace` nt 0-0.40、speed 0.8、start 2.3s）+ Signal（刀光）~3.3s。已跑選單，GreyboxTest 存檔。運鏡數字在 `CM_Vcam_Intro` Inspector 調。
- 順手修 `Portal.cs`（recompile-during-Play → `_propertyBlock` null → 每幀 `ArgumentNullException` 洗版）：`ApplyVisualState` lazy init。
- 改動：`BossHitbox.cs` / `PlayerGuard.cs` / `PlayerClashFeedback.cs` / `Portal.cs` / 新 `IntroOrbitCamera.cs` / `BossIntroGreyboxSetup.cs` / `KatanaClash_Rough.mp3` / `GreyboxTest.unity` / `BossIntro_Greybox.playable`。EditMode **282/282**。
- **待使用者對焦 Editor Play**：(1) 踢擊各距離都彈得到；(2) 一般格檔/踢擊 = 悶聲、完美彈反 = 清脆；(3) 雙連斬第一記彈反後能順接第二記；(4) 走進 `BossRoomTrigger` → 360 環繞 2.5s → 轉正 → 武士揮刀 + 刀光 → 開打。

### 追加94 續 29 — Play 回報三調：雙連斬頻率 / 架勢速度 / 僵直姿勢落地

1. **雙連斬施展次數太少**：pool 3 招都 weight 1，但 DoubleCombo `maxDistance 2.5` 比 SwordJudgment(3.5) 窄 → 玩家在 2.5m 外時只出 SwordJudgment。改 **`selectionWeightPhase1/2` 1 → 2.5**、**`maxDistance` 2.5 → 3.0**（續 26 加了前衝 profile，搆得到更遠了）。

2. **武士架勢條太快滿**：`maxStance` 60、玩家左鍵 25 傷 × `stanceGainMultiplier 0.2` = 5/擊、彈反 `parryBossPoiseDamage 14`/次 → ~3 次彈反或 3 套連段就爆。改 **武士 `StancePoise.maxStance` 60 → 100**（對齊玩家）+ **`PlayerGuard.parryBossPoiseDamage` 14 → 9**。約需 2 倍努力才削爆。（都是可迭代的手調值。）

3. **僵直姿勢浮在空中 → 躺地板**：`Wushi_PostureKneel` 其實是 `falling_down` clip（前撲倒地）。舊 `postureKneelNormalizedTime 0.5` 把 Animator 凍結在**下墜途中**（spine 61° 半蹲、hipY 1.0）= 「浮空」。量測：nt 0.75-0.9 才真的趴平（spine 83°、hipY 0.94）。
   - 改 **`tuning.postureKneelNormalizedTime` 0.5 → 0.78**（凍結在真的趴地那刻）。
   - `Wushi_PostureKneel.fbx` importer：`lockRootPositionXZ = true`（倒地不滑）、`lockRootRotation = true` + `keepOriginalOrientation = true`（把倒地旋轉烤進 pose、保留武士自身朝向 → 朝面前方向倒）。
   - `heightFromFeet` 試 true → 身體反而陷進地板（腳在空中當基準），已 revert 回 false。
   - **待使用者 Play 確認角度**：若還是浮/陷，微調 `postureKneelNormalizedTime`（0.72-0.92 都是趴地）。**已知**：hold 結束後從趴地站起是硬 pop（沒有像 Dead→GettingUp 那樣倒放起身）—— 使用者沒抱怨這個，要的話另做。

- 純資料 / import 改動（`Wushi_Attack_DoubleCombo.asset` / `Wushi_Tuning.asset` / `GreyboxTest.unity` 武士 StancePoise + Player PlayerGuard / `Wushi_PostureKneel.fbx.meta`）。無 code。EditMode **282/282**。

### 追加94 續 30 — 一般格檔音效換成 `9月1日 (3).mp3`

使用者提供 `9月1日 (3).mp3`（0.63s / 44.1kHz 立體聲）取代原本的一般格擋音效。
- ffmpeg 轉 mono + 尾端 fade → `Assets/_Project/Audio/Combat/KatanaGuard.mp3`（0.68s，import：forceToMono + DecompressOnLoad + PCM）。
- `PlayerClashFeedback.guardClip`（**一般刀刃格檔**）：`KatanaClash_Rough` → **`KatanaGuard`**。
- 沒動：`blockClip`（踢擊軟格檔）仍 `KatanaClash_Rough`；`parryClip`（完美彈反）仍 `KatanaClash`（清脆）。
- `ASSET_LICENSES.md` 更新。GreyboxTest 存檔，無 code、EditMode 不受影響。

### 追加94 續 31 — 僵直姿勢真的貼地（程式強制拉低，不再只靠調 nt）

使用者：「五適硬值時仍然是在空中躺平，而非貼齊地板表面」（續 29 只調了 `postureKneelNormalizedTime`，姿勢對了但高度還是浮）。

用 `SkinnedMeshRenderer.BakeMesh` 量測真實頂點（不信任 Meshy 退化 bounds）：nt 0.78 的網格 Y span **0.17–1.87**（有肢體戳穿地板、軀幹卻還懸在半空）——`falling_down` clip 把 Hips 的高度曲線烤死在骨骼動畫裡，而武士的 **Animator 就掛在 root 本身**（沒有獨立 Visual 子物件可單獨位移），root 在 PostureBroken 期間又完全不受 CharacterController 影響 → 光調 nt 治不好，純鬼畫符式浮空。

**改用程式強制拉低 root**（比照 LeapSlam 的「凍結期間跳過 CC.Move，直接寫 transform」手法）：
- `BossTuning` 新 `postureBrokenGroundDropOffset`（預設 **0.35**，手動調校值，因為網格 bounds 不可信、只能憑肉眼調）。
- `BossStateMachine`：kneel 凍結那一刻（`_postureKneelReached` 剛變 true 的兩個分支）呼叫 `ApplyPostureBrokenGroundDrop()` → `transform.position += Vector3.down * offset`（一次性，冪等）。
- `ApplyMotion` 新增 guard（跟 LeapSlam 那條並列）：`CurrentState==PostureBroken && _postureBrokenDropApplied` 時**整個跳過 `_controller.Move()`**——否則 CC 每幀都會把 capsule 重新頂回地面高度、把手動下拉的效果吃掉。
- `EndPostureBroken()` 呼叫 `RestorePostureBrokenGroundDrop()`（往上加回同樣的量），`OnExitState(PostureBroken)` 加 safety net（被 Dead 提前打斷時也一定復原，不會卡在地底）。
- 全程式改動，`Wushi_Tuning.asset` 沒動（新欄位缺值時吃 C# 預設 0.35，資產不用重新序列化）。EditMode **282/282**。
- **待使用者 Play 驗收角度/高度**：`postureBrokenGroundDropOffset` 是手動值，不對就在 `Wushi_Tuning` Inspector 直接調（往上/往下微調 0.05-0.1 一次）。
- **續 31 續（2026-09-02）**：用 `SkinnedMeshRenderer.BakeMesh` 量真實骨骼位置（Hips/Spine/Head 團在世界 Y 0.91-1.09、地板 ~0.5）→ `postureBrokenGroundDropOffset` **0.35 → 0.4**（明確寫進 `Wushi_Tuning.asset`）。腳趾骨會稍陷地板一點，比整個軀幹懸空好看。

### 追加94 續 32（2026-09-02）— spec 項目 9 §10.2：戰鬥數據儀表（下階段起點）

工程文件 M5 = 項目 9「最終數值調校」，spec §10.2 第一步是「必須收集的指標」。之前的調校（架勢/頻率/彈反窗/音效…）全靠感覺，這次把數據收起來。

- **`SekiroDeflectDebug.cs` 擴充**（既有 F9 overlay，已掛 `Player`、wire 到 `武士`）：新增 session tally——
  - clash 結果：Parry / Guard / HitBlocking（防禦中被打）/ HitOpen（沒防被打）計數 + **ParryRate %**
  - 武士 PostureBroken 次數 + **平均間隔秒數**（削爆節奏）
  - 玩家 stagger 次數
  - 雙方**累積掉血**（用 delta，復活/phase 補血不算「掉」）
  - session 時長（從第一次事件起算）
- `resetKey` **F8** 歸零。overlay 第 2、3 行顯示這些。
- `Awake` 從 `boss` transform 抓 `Health`/`StancePoise`/`BossStateMachine`；PostureBroken / stagger 用 `Update` 邊緣偵測；HP 用逐幀 delta。
- 純程式（`SekiroDeflectDebug.cs` 一個檔），無場景/資產，EditMode **282/282**。
- **用法**：Play → F9 開 overlay → 打一場 → 看 ParryRate、削爆間隔、雙方掉血比 → F8 重來。這些數字餵給後續 spec §10.3 的調校順序（前搖→命中窗→架勢壓力→傷害→彈反窗）。

**工程文件剩餘卡點**（見對話 recap）：
- 項目 4（玩家 swept blade）——續 23 Play 完全沒傷害已退回，重做需**對焦 Editor 陪同 Play** 逐步 debug（盲改風險太高）。
- 項目 5B/5C（Boss scale 4→1 + 拆共用 Animator + 精確刀身 guard）—— 全 spec 最高風險，需**明確簽核**才動。

---

### 追加94 續 33（2026-09-02）— spec 項目 5B「做法 A」：武士 gameplay root scale 4→1（幾何完全保留）

使用者簽核「玩家可以正常造成傷害 請做 5B/5C」→「做A（將來完全複製一份武士做B）」。**做法 A** = 只把 gameplay root 正規化到 `localScale 1`，可見模型／骨架／刀／所有骨綁 hitbox **一格不差**；把「縮小可見武士 + 重做 clip」的重活留給未來一份完整武士副本（做法 B）。

- **`Assets/Editor/Bootstrap/WushiRootScaleSetup.cs`**（新）——選單 `Tools/Live2DAction/[5B] Normalise 武士 Root Scale To 1` ＋ `[5B] Restore 武士 Root Scale To 4`。
  - 每個 **direct child**：`localScale *= 4`（lossyScale 不變）＋ `localPosition *= 4`（相對 root 的世界位移不變）。更深層自動繼承不變的鏈。
  - 例外：`BladeDrawVFX` 的 ParticleSystem `scalingMode = Local`（只讀自身 transform.localScale、無視階層），所以它**不乘 localScale**（乘了會大 4 倍），只乘 localPosition。
  - `CharacterController` 的 `height/radius/center/skinWidth/minMoveDistance` 全 `*= 4`（隨 root lossyScale 縮放）；`stepOffset` 維持 0（刻意）、`slopeLimit` 是角度不動。
  - idempotent + 可逆：Normalise 只在 root ≠ 1 時作用；Restore 放回 factor 0.25。
- **GreyboxTest.unity 已套用並存檔**。世界量測逐項驗證前後一致：
  - root `pos=(0,0.6,11)` `lossyScale (1,1,1)`
  - CC world `c=(0,2.44,11) size=(0.90,3.88,0.90)`、底部 Y=0.500
  - `RightFootHitbox c=(-0.61,0.93,11.21)`、`BladeHitbox c=(-1.39,2.03,9.57) size=(1.03,1.16,2.66)`（`lossyScale 3.2` 保留 → `BossHitbox` 膠囊縮放數學不變）
  - `BodyHurtbox c=(0,9.78,11)`（仍是 rig 缺陷浮空，武士只能靠 root CC 受擊——不變）
  - `LandingAOEHitbox c=(0,1.80,11) size 6`、`ChestAimPoint world=(-0.02,3.15,10.76)`、`SMR char1 c=(-0.02,2.48,11.10) size=(3.31,4.42,2.67)`
- 純 Editor 工具 + 場景序列化值，無 runtime 程式改動。EditMode **282/282**。
- **好處**：`transform.position` / CC / hurtbox 世界尺寸現在是可讀的公尺數（spec §6.3），也是 5C 的前置。
- **5C 仍卡住**：需要做法 B（把可見武士縮到人身尺寸 + 依玩家身高重新校正每招 + 拆共用 Animator），留給未來完整武士副本。

---

### 追加94 續 34（2026-09-02）— spec 項目 9 §10.4：武士出招真實時序報表（M5 groundwork 第 2 步）

使用者「5C先跳過 接下來的項目」。spec 剩下的就是項目 9（7、8 已完成，4 卡在需陪同 Play）。項目 9 §10.1 說「前 8 項穩定才能開始真正調數值」，但 §10.4 驗收條件之一是可先做的工具：**「調整 state speed 後，自動重新計算並顯示實際首次接觸與有效窗毫秒數」**。續 32 做了 §10.2 的數據儀表，這次做 §10.4 的量尺。

- **`Assets/_Project/Game/Combat/Boss/BossAttackTimingUtility.cs`**（新，純靜態，Runtime asmdef）：
  - `RealClipSeconds(clipLength, stateSpeed)` — 套 Animator state speed 後的真實秒數（speed ≤ 0 視為 1）
  - `NormalizedToSeconds(normalized, realClipSeconds)` — normalized time → 秒
  - `WindowMilliseconds(startN, endN, realClipSeconds)` — hit window 有效毫秒（反向/空窗 = 0）
  - `ParryDifficultyRatio(windowMs)` — 對玩家 0.20s 彈反窗的比值（>1 好抓、<1 靠前兆撐）
- **`Assets/Editor/Bootstrap/BossAttackTimingReport.cs`**（新）——選單 `Tools/Live2DAction/[9] 武士 Attack Timing Report`。讀每個 `Wushi_Attack_*` 定義 + `Wushi.controller` 對映的 state（`clipName` → state 名，遞迴含子 state machine）的 `speed` 與 clip 長度，印出每個 hit window 的真實 contact 秒數區間、有效 ms、parry 比值；`<-- tight vs parry` / `very wide` / `EMPTY WINDOW` 旗標。**唯讀，不改任何東西**，是 §10.3 調校順序（先 state speed + 前兆 → 再 hit window 位置 → 最後才動窗長）的量尺。任何 Animator state speed 或 `Wushi_Attack_*.asset` 窗編輯後重跑。
- 首跑輸出（供後續調校參考）：SwordJudgment 窗 1 現在 462ms/parry ×2.31（續 27 為了「刀一起手就判定」刻意放寬）、窗 2 165ms/×0.83；DoubleCombo 227ms + 198ms；OverheadSlam 145ms/×0.72；SpartanKick 154ms/×0.77；LeapSlam AOE 728ms（旗標 very wide，本來就是 AOE 不是可彈反刺擊）。
- `BossAttackTimingUtilityTests` +6 → EditMode **288/288**。
- 純程式 + 純 Editor 工具，無場景/資產改動。
- **§10.3 實際調校 pass 仍待**：需要對焦 Editor Play + F9 儀表（續 32）跑幾場收 ParryRate / 削爆間隔 / 雙方掉血 的實測數據。

### 追加94 續 35（2026-09-02）— spec 項目 9 §10.3 調校 pass #1：Boss state speed（步驟 16「第一刀可讀」）

使用者對焦 Editor 打了一場（F9 儀表，64s）：
- Parry 8 / Guard 7 / HitBlocking 6 / HitOpen 7 → **ParryRate 38%**
- 武士 PostureBreak x2（mean 17.7s）、玩家 Stagger **x0**
- 掉血 玩家 311 / 武士 1000（武士死、玩家收在 304/500）

讀數：**HitOpen 7（25% 的攻擊完全沒防就中）= 可讀性問題**（步驟 16）；PlayerStagger 0 = 玩家格擋架勢壓力太低（步驟 18，之後做）；玩家只掉 311 就打死 boss = 風險/報酬偏弱（步驟 19，之後做）。§10.3 規定照順序、先做 16。

**改動（`Wushi.controller` state speed，兩個 attack）**：
- `Wushi_OverheadSlam` **1.40 → 1.05**（spec §10.4 明講這招要「慢重擊、適合高架勢壓力」，1.40 根本不慢）。timing report：real 1.81s→**2.41s**、contact 1.01s→**1.35s**、窗 145ms→**193ms**、parry ×0.72→**×0.97**。
- `Wushi_SpartanKick` **1.40 → 1.15**（report 一直標 "placeholder timing" ×0.77；normal pool + tooCloseAttack 兩處都用它，出招頻繁）。real 0.90s→**1.10s**、contact 0.52s→**0.64s**、窗 154ms→**187ms**、parry ×0.77→**×0.94**。
- SwordJudgment（speed 1.0，窗1已超寬）、DoubleCombo（speed 1.0，ratio ~1.0）不動。

確認：`normalAttackPool` = [SwordJudgment, SpartanKick, DoubleCombo]，**ContinuousThrust 沒在池裡**（追加89 已退，timing report 仍讀得到它的 dead state 純屬雜訊）。開場武士浮空 = LeapSlam 起跳 + LandingAOE telegraph，**不是**續 31 的姿勢浮空 bug。

純 `Wushi.controller` 改動，無 code / 無場景 / 無資產。EditMode 不受影響。
**待使用者對焦 Play 再打一場**：F8 歸零 → 開打 → 回報 (1) HitOpen 有沒有降；(2) 現在哪一招還是反應不及（講招名）；(3) ParryRate / PostureBreak 間隔 / 雙方掉血。收到數據後做步驟 17（hit window 位置）或步驟 18（玩家架勢壓力）。

### 追加94 續 36（2026-09-02）— §10.3 pass #1 結果：步驟 16 收斂，等步驟 17/18 的主觀輸入

續 35 改完後使用者再打一場（F9，**143s**）：
- Parry **40** / Guard 8 / HitBlocking 12 / HitOpen 9 → **ParryRate 38% → 67%**
- HitOpen rate 0.109/s → **0.063/s**（每秒少一半沒防中招）
- 武士 PostureBreak x5（mean **27.3s**，前一場 17.7s）、玩家 Stagger **x0**
- 掉血 玩家 **545** / 武士 1000（玩家這場疑似死一次、武士死）

判讀：
- **步驟 16（可讀性）基本收斂** — ParryRate 大幅上升、沒防中招砍半。OverheadSlam/SpartanKick 減速有效。
- **PlayerStagger x0 不急著當調校缺口**：這場玩家 40 parry / 只 8 guard，根本沒 turtle；guard poise 路徑吃每招 `basePoiseDamage`（SwordJudgment 22，規格 item 6），配 `StancePoise.regenPerSecond 20`（1.5s 延遲後 5s 回滿）→ 攻擊間隔 >~2.5s 就歸零。玩家不龜就不崩，屬正常；步驟 18 待「玩家改龜縮打法會不會被崩」的實測。
- **步驟 20（架勢節奏）**：27.3s 一次削爆對 Sekiro-like 偏慢，但 §10.3 排在後面，先不動。

**這輪無改動**（純判讀 + 記錄）。等使用者回報：(1) HitOpen 現在主要哪一招打中；(2) 這場有沒有死、太硬/太拖/剛好 → 決定接步驟 17 或 19。

### 追加94 續 37（2026-09-02）— §10.3 pass #2：SpartanKick 再減速 + 延遲混招減速 + 步驟 19 起手（重擊傷害拉開）

使用者回報（續 36 兩問）：HitOpen 主要來自 **① SpartanKick 前踢 ② 第二段延遲混招**；手感 **「還是太簡單」**（即使掉 545、疑似死一次）。

**改動：**
1. **SpartanKick**（步驟 16 收尾）：`Wushi.controller` state speed **1.15 → 1.0**。real 1.10s→**1.27s**、contact 0.64s→**0.73s**、窗 187ms→**215ms**、parry ×0.94→**×1.08**（終於穩過 0.20s 窗）。
2. **SwordJudgment**（延遲混招可讀性，續 27 早標過可降速）：state speed **1.0 → 0.9**。第二刀 contact 2.01s→**2.24s**、窗 165ms→**183ms**、parry ×0.83→**×0.92**。整招 real 3.30s→3.67s，起手更好讀。
3. **步驟 19 起手（重擊 vs 輕擊拉開差距，§10.2「未防禦死亡所需命中」）**：
   - `Wushi_Attack_SwordJudgment.baseHealthDamage` **32 → 42**（8.4% of 500）
   - `Wushi_Attack_OverheadSlam.baseHealthDamage` **28 → 40**（8%）
   - 輕擊不動：SpartanKick / DoubleCombo 仍各 25（= 5% of max）。現在重擊 ≈ 輕擊 ×1.7，站錯位置吃一記重劈很痛。
   - LeapSlam 仍 500（沒閃 = 死）、poise 值全不動。

`Wushi.controller`（2 state）+ 2 個 attack asset 的 `baseHealthDamage`。無 code、無場景。EditMode 不受影響。
**待使用者對焦 Play 打一場**：F8 → 開打 → 回報 (1) SpartanKick / 延遲第二刀現在擋得到嗎；(2) 重擊變痛後還「太簡單」嗎；(3) ParryRate / PostureBreak 間隔 / 雙方掉血。若仍太簡單 → 步驟 20（武士架勢上限往上、削爆變難）＋ 檢查 phase2 是否該加壓。

### 追加94 續 38（2026-09-02）— 修：武士出招站位（1.7m）幾乎貼著踢擊圓圈 → 把 SpartanKick 移出隨機招式池

使用者回報：「武士靠近我進行攻擊時，範圍進入到腳底圓圈導致頻繁觸發踢擊」。澄清本意（**確認保留**「圓圈 = 玩家極限攻擊距離」的設計）：「如果玩家攻擊武士的最遠距離就一定是站在圈內的，這樣武士的踢擊才有意義，為的就是防止玩家一直近身；而武士的所有攻擊手段一定都是大於圓圈的，不然就會頻繁觸發踢擊。」

實測數字：
- 玩家 katana `MaxAttackReach` = Σ combo step `Range+Radius` = 0.5+0.5 = **1.0m**（LightAttack1–4 都是 0.5/0.5）。
- 故 `EffectiveTooCloseDistance` = `Max(tuning.TooCloseDistance 1.6, 1.0)` = **1.6m**（圓圈其實只有 1.6m，不是先前誤判的 ~3m）。
- `AttackReadinessDistance()`（武士 approach 停下來出招的距離）= 招式池裡**最小的 MaxDistance** → SpartanKick 的 **1.7m**。武士走到 1.7m 才停 = 只在圓圈外 0.1m，玩家稍動一下就進圈 → 每 2s 強制踢擊、打斷武士自己的招。

**修（不動 `EffectiveTooCloseDistance` 邏輯，維持 `Max(1.6, playerReach)`；`BossStateMachine.cs` 只還原註解、無邏輯改動）：**
- **把 `Wushi_Attack_SpartanKick` 從 `武士.normalAttackPool` 移除** → 池子剩 `[SwordJudgment (maxDist 3.5), DoubleCombo (maxDist 3.0)]`。`AttackReadinessDistance()` 現在 = **3.0m**，武士在圓圈外 1.4m 就停下出招，走近揮刀不再自己踩圈。
- SpartanKick **仍是 `tooCloseAttack`**（玩家貼身滿 2s 的強制懲罰踢 + 擊退）——這正是它該有的角色，本來就不該被隨機 roll。
- 場景改動（`武士` component 序列化陣列）＋ GreyboxTest 存檔。編譯無錯、EditMode 不受影響。

先前一版（拿掉 `Max(…, MaxAttackReach)` 耦合、把圓圈固定 1.6m）方向錯誤、已完全還原——使用者要的是圓圈涵蓋玩家攻擊距離，問題在武士出招站位太近。
**待使用者 Play 確認**：站著不動等武士過來 → 武士在 ~3m 停下揮刀、不再自己觸發踢擊；只有玩家主動走進 ~1.6m 連砍滿 2s 才吃踢擊 + 擊退。

### 追加94 續 39（2026-09-02）— 武士出招後若卡在踢擊圈內 → 主動退到 standoff 再出手

使用者回報：「發動 Wushi_OverheadSlam 武士會突然很接近玩家且處於踢擊範圍內」。查出 `Wushi_Attack_DoubleCombo`（招式池權重最高）帶 `attackMotion.forwardDistance 2.5` —— 從 3m 出手會前衝到離玩家 ~0.5m，之後武士停在那繼續下一招 / 觸發強制踢擊。續 38 只擋了 approach 停下距離，沒管「前衝招 / 強制招把武士自己塞進圈裡」。

**修（`BossStateMachine.cs` + `BossTuning.cs`）：**
- 新 `BossTuning.forcedAttackStandoffMargin`（預設 **0.6**，已寫進 `Wushi_Tuning.asset`）。
- 新 `AttackStandoffFloor` = `EffectiveTooCloseDistance + forcedAttackStandoffMargin`（≈ 1.6+0.6 = 2.2m，在踢擊圈外、又低於 approach 的 ~3m readiness）。
- **`UpdateApproach`**：新最高優先分支 —— `distance < AttackStandoffFloor` 時**往反方向退**（新 `MoveAwayFromTarget` helper，直線、不走 NavPath），把 gap 重新拉開到 standoff 才停。
- **`UpdateIdle`**：出手前若 `distance < AttackStandoffFloor` → 直接轉 `Approach`（讓上面那條退位），不從貼身出招。
- **`TryEnterPeriodicSlam`**：`distance < AttackStandoffFloor` 時保持 pending（不從圈內放 OverheadSlam）。
- **`EndAttack`**：剛結束的招若帶 `AttackMotion.HasDisplacement`（= 前衝招，只有 DoubleCombo）→ 歸零 `_tooCloseTimer`，前衝拉近的距離不算「玩家貼身」，玩家有完整 2s 再決定要退還是繼續貼。

淨效果：武士只從 ~2.2m 外出招；DoubleCombo 前衝進來打完 → 退回 ~2.2m → 下一招又從外面來。玩家主動走進 1.6m 連砍滿 2s 才吃踢擊（設計不變）。

改 4 處（`BossStateMachine.cs`）＋ 1 新 tuning 欄位。編譯無錯。

**續 39 修正（同日）— 「退開」被使用者否決**：使用者：「我要的不是退開，而是這些招數始終都能在同一個位置下打到玩家（必須是踢擊圈外，或需要調整武士刀長度和大小？）」。要的是**武士站在圈外一個固定 standoff、所有招都從那打得到**，不前衝也不後退。
- **已還原**：`UpdateApproach` 退位分支、`UpdateIdle` 太近→Approach、`MoveAwayFromTarget` helper、`EndAttack` 的 `_tooCloseTimer` 歸零。
- **保留**：`AttackStandoffFloor` 屬性 + `forcedAttackStandoffMargin` tuning 欄位（只剩 `TryEnterPeriodicSlam` 用作「不從圈內放週期 slam」的保險）；SpartanKick 移出 pool（續 38）。
- **現況**：`AttackReadinessDistance()` = pool 最小 maxDistance = min(SwordJudgment 3.5, DoubleCombo 3.0) = **3.0m** → 武士本來就站 3.0m 出招（圈外）。SwordJudgment 無前衝、實戰能從 ~2.5-3m 打到 + 被彈反。**DoubleCombo 才是問題**：它自己的 designNotes 寫「clip 角色起始位置在 root 後方 ~3 單位、往前走過來」，`useRootMotion=0` 下 blade 落後、**超過 ~1.7m 就打空** → 當初加 2.5m 前衝就是為了讓它打得到。離線 AnimationMode 取樣不可靠（clip root motion 沒被 retarget 成 runtime 那樣），blade 前伸量測不出穩定值。
- **決策待使用者選**（見對話 AskUserQuestion）：DoubleCombo 要 (A) 移出 pool、(B) 縮短前衝到剛好圈外、(C) 重烤 clip + 加長 BladeHitbox 讓它原地打得到、(D) 全域加大武士刀 hitbox。
- **EditMode 待跑**（revert 後）。

### 追加94 續 40（2026-09-02）— DoubleCombo 移除 + 全域加大武士刀 hitbox（使用者選定）

使用者選：「先移除 Wushi_DoubleCombo，然後全域加大武士刀 hitbox」。

**改動：**
- **`武士.normalAttackPool` → 只剩 `[Wushi_Attack_SwordJudgment]`**（DoubleCombo 移除，前衝招不要了）。
- `Wushi_Attack_SwordJudgment.asset`：`disallowImmediateRepeat` 1 → **0**（現在是唯一 pool 招，不能不准重複，否則 PickAttack 每隔一次回 null、武士半發呆）。SwordJudgment 的 `derivedAttack` = OverheadSlam（deriveChance P1 0.6 / P2 0.85）→ 連段仍有變化：SwordJudgment →常→ OverheadSlam。
- **`BladeHitbox` capsule 加大**（場景 instance 上，非 prefab 本體）：`radius` 0.10→**0.14**、`height` 0.8625→**1.25**、`center.x` 0.149→**0.35**。世界尺寸：長 2.76m→**4.0m**、半徑 0.32m→**0.45m**（+45% 長、+40% 粗，且往刀尖方向外推）。SwordJudgment / 衍生 OverheadSlam / 週期 OverheadSlam 都吃這個更大的判定，從 ~3m standoff 打得到。
- `AttackReadinessDistance()` 現在 = SwordJudgment 的 maxDistance **3.5m**（唯一 pool 成員）→ 武士站 3.5m 出招，遠在踢擊圈 1.6m 外。全部招式原地打、不前衝不後退。

**副作用要注意**：`BossHitbox.TryResolveBladeClash` 的彈反探針 = `collider 尺寸 + 0.35` → hitbox 變大 = 彈反判定也變寬鬆（可能更好彈、也可能覺得太容易）。Play 時留意。

`GreyboxTest.unity` 存檔 + `Wushi_Attack_SwordJudgment.asset`。編譯無錯，**EditMode 288/288 綠**。
續 39 的「退開」程式已全部還原；保留 `AttackStandoffFloor` + `forcedAttackStandoffMargin`（只給 `TryEnterPeriodicSlam` 當保險）+ SpartanKick 移出 pool（續 38）。

**待使用者 Play 確認**：(1) 武士站 ~3.5m 揮刀，SwordJudgment / OverheadSlam 都打得到、不再打空；(2) 武士不前衝、不進踢擊圈、不亂踢；(3) 彈反判定有沒有因為 hitbox 變大而變得太寬鬆；(4) 只剩一招 + 衍生 OverheadSlam 會不會太單調（要的話再加一招原地劍招）。

### 追加94 續 41（2026-09-02）— 武士新增第二招式：Wushi_CrossSlash（Meshy 原地雙擊斬）

使用者提供 `Meshy_AI_Parkside_Portrait_biped.zip`，要武士加入這個攻擊動作（承續 40「只剩一招會不會太單調」）。

**匯入 + 量測（離線 AnimationMode.SampleAnimationClip，逐 0.01 nt 追 BladeHitbox 世界 Y / 前伸 / 速度）：**
- clip「Scene」92 幀 / 3.03s → `Wushi_CrossSlash.fbx`（Humanoid、`keepOriginalPositionY`）。
- **原地雙擊**（hips localZ 全程 0.4-0.7、**無前衝**，跟被移除的 DoubleCombo 相反）：
  - 命中 1（nt 0.19-0.30）：刀從 Y 3.6 斜劈下砍到 Y 1.0，速度峰值 50 u/s @ nt 0.20，前伸 -1.3→+3.3 —— 過頭斜劈。`deflectReaction` ContinueCombo。
  - 命中 2（nt 0.68-0.76）：前伸橫掃，reach 2.4→**4.1m** @ nt 0.68-0.71，速度峰值 44 u/s，刀在軀幹高度 —— 全 clip 最遠前伸，從 3.5m standoff 穩穩打到。`deflectReaction` Recoil。

**接線：**
- `Wushi.controller` 新 `Wushi_CrossSlash` state（speed 1.0、WD=true 對齊其他 state）。
- `Wushi_Attack_CrossSlash.asset`（新，guid 401db810...）：attackId `CrossSlash`、maxDistance 3.5、maxAngle 65、startup 0.4 / recovery 0.55、傷害 30 / poise 20、cooldown 1.5、命中窗 nt 0.20-0.29 + 0.68-0.76（measured）、無 attackMotion / 無 useRootMotion / 無 derivedAttack、isMajorAttack 0。
- `武士.normalAttackPool` → **[SwordJudgment, CrossSlash]**（SwordJudgment `disallowImmediateRepeat` 還原 1 → 兩招交替）。
- timing report：CrossSlash 窗1 contact 0.61-0.88s（273ms、parry ×1.37）、窗2 2.06-2.31s（243ms、×1.21）—— 兩窗都好讀。
- `ASSET_LICENSES.md` 新增一列（Meshy 付費輸出、可商用、可進 Build）。

`GreyboxTest.unity` + `Wushi.controller` + 2 新資產 + `Wushi_Attack_SwordJudgment.asset`。編譯無錯，**EditMode 288/288 綠**。
**待使用者對焦 Play 確認**：(1) 武士會交替出 SwordJudgment / CrossSlash；(2) CrossSlash 兩段都從 ~3.5m 打得到、不打空、不前衝進踢擊圈；(3) 兩段的紅光/彈反時機好不好讀；(4) 傷害 30 / poise 20 手感。

### 追加94 續 42（2026-09-02）— CrossSlash Play 回報兩修：偏右 → 往左轉；紅光太晚 → 命中窗提前

使用者：「Wushi_CrossSlash 攻擊比較偏右，你可能要讓武士稍微往左轉一點才能打到玩家；而且感覺兩段劈砍都是揮刀完才亮紅色。」

**1. 偏右 → 新 per-attack yaw 偏移：**
- `BossAttackDefinition` 新欄位 `facingYawOffsetDegrees`（Range -60..60，負=往左、正=往右）。套在 `faceTargetSnapOnStart` 的 snap 與 `UpdateAttackState` 每幀 `FaceTarget(trackAmount, offset)` 兩處（`FaceTarget` 加選用參數 `extraYawDegrees`）。
- `Wushi_Attack_CrossSlash.asset`：`faceTargetSnapOnStart` 0→**1**、`facingYawOffsetDegrees` **-15**（clip 揮刀烤在角色右側，武士左轉 15° 讓正前方玩家吃到）。可在 Inspector 調。

**2. 紅光太晚 → 命中窗提前**（同 SwordJudgment/OverheadSlam 的老 bug「揮刀完才紅」）：
- 離線量測：hit 1 刀在 nt 0.14-0.18 舉在頭上、nt 0.19-0.26 才下砍；hit 2 nt 0.58-0.65 蓄、0.66-0.72 快掃。舊窗（0.20-0.29 / 0.68-0.76）卡在下砍/快掃的**中後段** → 紅光出現時揮刀已過半。
- 新窗：hit 1 **0.13-0.28**（紅光在舉刀階段就亮）、hit 2 **0.58-0.77**（前掃一蓄就亮）。real 時序（speed 1.0）：窗1 contact 0.39-0.85s、窗2 1.76-2.33s。

改 `BossAttackDefinition.cs` / `BossStateMachine.cs`（`FaceTarget` 選用參數 + 2 處套用）/ `Wushi_Attack_CrossSlash.asset`。編譯無錯。**EditMode 待跑**（使用者正在 Play）。
**待使用者 Play 確認**：(1) CrossSlash 現在打得到正前方玩家（不夠再調 `facingYawOffsetDegrees`，-20/-25…）；(2) 兩段紅光在揮刀**前/中**就亮、來得及彈反。

### 追加94 續 43（2026-09-02）— Play 回報三修：只有踢擊擊退 / CrossSlash 快一點 / 縮短出招間隔

使用者三點：(1) 只有踢擊能把玩家擊退；(2) CrossSlash 銜接快一點但動作完整明顯；(3) 武士動作間隔休憩太長、出招慢，玩家來不及削架勢條。

**1. 只有踢擊擊退：** `knockbackForce` → **0** 於 SwordJudgment(4→0) / CrossSlash(4→0) / OverheadSlam(4.5→0) / DoubleCombo(3→0) / LeapSlam(5→0) / ContinuousThrust(1.5→0)。**SpartanKick 維持 4.5**（唯一擊退，也是 tooCloseAttack 把玩家推出圈的機制）。玩家被劍招打中不再被推開 → 站得住、繼續輸出。

**2. CrossSlash 快一點：** `Wushi.controller` `Wushi_CrossSlash` state speed **1.0 → 1.15**。real 3.03s → 2.64s，動作全程仍完整（1.15 不會糊）。timing report：窗1 contact 0.34-0.74s（396ms、×1.98）、窗2 1.53-2.03s（501ms、×2.51）—— 紅光現在都在揮刀前/中就亮。

**3. 縮短出招間隔：** `Wushi_Tuning.attackRecoveryTailCutNormalized` **2（=停用）→ 0.15**（比照屁孩王 PW2_Tuning 的實證值）。武士的招現在在最後命中窗結束後 nt +0.15 就 `EndAttack`，砍掉「揮完刀慢慢站回架勢」的長尾巴：SwordJudgment 省 ~0.7s、OverheadSlam 省 ~0.5s、CrossSlash 省 ~0.4s。武士的 `globalRest`(0.05-0.15) / `majorAttackExtraRest`(0.1-0.3) / `decisionInterval`(0.05-0.12) 本來就很短，長尾巴才是主因。→ 出招頻率明顯上升 → 玩家有更多彈反/命中機會削架勢。

純資料 / controller / tuning 改動，無 code（`facingYawOffsetDegrees` 的 code 在續 42）。編譯無錯。**EditMode 待跑**（使用者正在 Play；續 42 的小 code 改動也還沒補跑 runner，但編譯乾淨）。
**待使用者 Play 確認**：(1) 只有踢擊會把你推開；(2) CrossSlash 節奏 OK、動作看得清楚；(3) 武士出招變密、架勢條削得動；(4) CrossSlash 偏右修正（-15°）夠不夠。

### 追加94 續 44（2026-09-02）— Play 回報：彈反時武士動作「突然消失」/「被自身打斷」→ 全部命中窗改 ContinueCombo

使用者：(1) 每個攻擊動作有被自身打斷的跡象；(2) CrossSlash 第二段被彈反時動作軌跡突然消失。

根因：武士**沒有 hit-reaction / flinch clip**。`behitFlyUpClipName` 指到 `Locomotion`（blend tree），所以任何 `deflectReaction: Recoil(0)` 的命中窗被彈反 → `NotifyParried(Recoil)` → `_forcedHitReactionPending` → `TryEnterHitReaction` → `PlayState(Locomotion)` = 揮刀中途硬切回站/走姿 = 「動作軌跡突然消失」。ParryRate ~67% → 幾乎每招都會踩到 → 看起來「每個攻擊都被自身打斷」。

**修：武士所有命中窗 `deflectReaction` → ContinueCombo(1)**（SwordJudgment 窗2、CrossSlash 窗2、OverheadSlam 窗1、DoubleCombo 窗2、SpartanKick 窗1；窗1 本來就多半是 1）。彈反照樣結算架勢傷害 + 火花回饋（`PlayerGuard` 那邊做的），但**不再把武士拽進壞掉的 HitReaction**——揮刀動畫自然播完，架勢削爆時才由 `TryEnterPostureBroken`（跪地 clip）接管。這也是 Sekiro-like 的正解：單次彈反不硬直，架勢條累滿才崩。

純資料改動（5 個 `Wushi_Attack_*.asset` 的 hitWindows deflectReaction）。無 code。編譯無錯。
**已知未解**：武士缺真正的受擊/彈反 flinch 動畫（`behitFlyUpClipName=Locomotion` 是佔位）——`RequestBeHitFlyUp`（大招擊飛路徑）仍會踩到同一個問題，但目前沒觸發。要「完美彈反時武士明顯一頓」得另外弄個短 flinch clip。
**待使用者 Play 確認**：(1) 彈反 CrossSlash 第二段不再「消失」；(2) 攻擊不再像被自身打斷。若還有「被打斷」感 → 是續 43 的 `attackRecoveryTailCutNormalized 0.15` 砍尾巴砍太多（尤其 SwordJudgment→衍生 OverheadSlam 的銜接），回報就把它調到 0.25-0.3 換平順。

### 追加94 續 45（2026-09-02）— 暫不做彈反動畫（招式播完再接）／硬直改新倒地 clip／匯入剪刀腳摔（尚未上場）

使用者三點：(1) 先不做彈反動畫，單純讓每個動作做完再接下一個；(2) 架勢削爆硬直改用 `跌倒.zip`；(3) 加入新攻擊 `跳躍頭部剪刀腳摔.zip`。

**1. 招式播完再接：** `Wushi_Tuning.attackRecoveryTailCutNormalized` **0.15 → 2**（= 停用，續 43 那次砍尾巴撤回）。武士每招現在完整播到底才 `EndAttack`。配合續 44 全 ContinueCombo（彈反不打斷），攻擊動作永遠完整。武士的 rest/decision interval 本來就 <0.15s，所以「播完立刻接」。

**2. 硬直倒地 clip：** `跌倒.zip`（`falling_down`，2.27s）→ `Wushi_PostureFall.fbx`（Humanoid + `lockRootPositionXZ`/`lockRootRotation`/`keepOriginalOrientation` 原地倒）→ `Wushi.controller` 新 `Wushi_PostureFall` state。`武士.kneelStandClipName` 由 `Wushi_PostureKneel` 改指 `Wushi_PostureFall`；離線量測趴平 pose 在 nt ~0.75（spine 91°、hipsWorldY 1.06）→ `Wushi_Tuning.postureKneelNormalizedTime` 0.78 → **0.75**。`postureBrokenGroundDropOffset` 維持 0.4（新舊 clip hips 高度幾乎一樣）。舊 `Wushi_PostureKneel` state + fbx 留著沒刪。

**3. 剪刀腳摔（匯入但未上場）：** `跳躍頭部剪刀腳摔.zip`（`Jumping_Head_Scissor_Takedown`，3.67s）→ `Wushi_ScissorTakedown.fbx`（Humanoid）→ `Wushi.controller` `Wushi_ScissorTakedown` state ＋ `Wushi_Attack_ScissorTakedown.asset`（跳躍 command grab：hips 前進 ~10 單位、Y 1.7→4.2 起跳→0.69 摔下，命中窗 nt 0.50-0.72，傷害 45/poise 24、useRootMotion 1、knockback 0、ContinueCombo）。
- **未加入 `normalAttackPool`**：`useRootMotion` 需要 `武士` Animator GameObject 上有 `BossAnimatorRootMotionRelay`（屁孩王 有、**武士沒有**）＋ `applyRootMotion`；沒有的話 ~10 單位前跳的 root 位移吃不到，模型會飛出 CharacterController。武士 Animator 又直接掛在 root（同 CC），全域開 `applyRootMotion` 會影響所有現有招式。**要上場得另做**：加 relay + 比照 LeapSlam 的落點鎖定，然後陪同 Play 調前跳距離。asset/state/clip 都留著。

`Wushi_Tuning.asset` / `Wushi.controller` / `GreyboxTest.unity`（kneelStandClipName）/ 2 新 fbx + 1 新 asset。編譯無錯，**EditMode 288/288 綠**。`ASSET_LICENSES.md` 更新。
**待使用者對焦 Play 確認**：(1) 攻擊不再有「被自身打斷」感（全程完整）；(2) 架勢削爆武士倒地 pose 對不對、有沒有浮空/陷地（不對就調 `postureKneelNormalizedTime` 0.68-0.88 或 `postureBrokenGroundDropOffset`）。

### 追加94 續 46（2026-09-02）— 剪刀腳摔上場：FSM 驅動的「撲向玩家」前跳（不用 root motion relay）

使用者要 (1) 幫武士 Animator 加 root motion 元件 (2) 前跳鎖定玩家落下（比照 LeapSlam）(3) 陪同 Play 調距離。

**改用更省風險的做法（不加 relay、不開全域 applyRootMotion）：**
- `Wushi_ScissorTakedown.fbx` **重匯入 `lockRootPositionXZ`**（把烤進 clip 的 ~10 單位前跳位移剝掉，跳躍高度保留）。
- 新 `BossAttackDefinition.lungeDistanceFromTargetGap`（+ `lungeTargetGapMeters` 1.4 / `lungeMaxMeters` 7）：為 true 時，`BeginAttack` 把 `attackMotion` 的位移距離**換成 commit 當下與玩家的實際水平距離 − 1.4m**（clamp 0..7），沿鎖定方向、用既有的 `attackMotion` 曲線在 nt 0.05-0.55 內走完 → **不管玩家站多遠都落在他面前 ~1.4m**。
- `BossStateMachine`：`_attackMotionDistanceOverride` 欄位；`BeginAttack` 的 attackMotion 啟動條件加 `|| LungeDistanceFromTargetGap`；`UpdateAttack` 位移套用讀 override。
- `Wushi_Attack_ScissorTakedown.asset`：`useRootMotion` 1→0、`lungeDistanceFromTargetGap` 1、attackMotion window 0.05-0.55、命中窗 nt 0.57-0.72（807ms→~550ms）、傷害 45/poise 24、knockback 0、ContinueCombo、weight 0.5/0.6、cooldown 7s、minDist 2 / maxDist 7。**加入 `武士.normalAttackPool`** → [SwordJudgment, CrossSlash, ScissorTakedown]。
- `Wushi.controller` `Wushi_ScissorTakedown` state（speed 1.0，續 45 已建）。

改 `BossAttackDefinition.cs` / `BossStateMachine.cs` / `Wushi_Attack_ScissorTakedown.asset` / `Wushi_ScissorTakedown.fbx.meta` / `GreyboxTest.unity`。編譯無錯，**EditMode 288/288 綠**。既有 `attackMotion`（DoubleCombo 那條）行為不變——override 只在新 flag 為 true 時生效。
**待使用者對焦 Play 調校**：(1) `lungeTargetGapMeters` 落點距離；(2) attackMotion window nt 0.05-0.55 對不對得上畫面跳躍弧線；(3) root 釘在原地、腳只靠骨骼動畫離地，跳躍看起來 OK 嗎（不 OK 的話才需要走 relay + 真 root motion 那條路）；(4) 命中窗 nt 0.57-0.72、傷害 45。

### 追加94 續 47（2026-09-02）— 三招 Meshy 動作 + 新 `cancelClipBodyDrift` 系統（治 Meshy clip 烤入位移的通病）

使用者給 `武士刀前刺.zip` / `扭轉前劈.zip` / `滑行翻滾.zip` 要加入。

**發現：這批 Meshy clip 有系統性問題。** 全部把「往前走好幾公尺」烤進 **Hips 肌肉動畫**（不是 root motion 曲線）——`lockRootPositionXZ` 完全無效（實測：開了鎖，身體照樣前進）。各 clip 身體前進量：ScissorTakedown 14m、SlideRoll 20m、TwistCleave 6.6m、ThrustStab 4.5m、CrossSlash 只有 0.07m（例外）。

**新系統 `BossAttackDefinition.cancelClipBodyDrift`（+ `BossStateMachine`）：**
- `Awake` 快取 `_hipsBone`（`animator.GetBoneTransform(Hips)`）。
- 攻擊每幀：算 Hips 相對 root 的 XZ 位移，跟該招第一幀的基準比，把**增量**反向加到 `_horizontalVelocity` → 畫面身體釘住原地。delta-based，基準稍有誤差也不會累積。
- 之後 `attackMotion` / `lungeDistanceFromTargetGap` 再加「這招真正該有的」可控前移。
- 純加法、對沒開旗標的招零影響。

**接線（`Wushi.controller` + 各自 asset + pool）：**
- **`Wushi_Attack_ThrustStab`**（武士刀前刺，3.03s，2 段刺）：`cancelClipBodyDrift` + `lungeDistanceFromTargetGap`（gap−1.7m, clamp 0..4）。傷害 28/poise 18、命中窗 nt 0.18-0.32 + 0.53-0.70。**加入 pool**。
- **`Wushi_Attack_TwistCleave`**（扭轉前劈，2.03s，早揮 + 大劈 speed 63）：`cancelClipBodyDrift` + lunge（gap−1.6m）。傷害 34/poise 22、isMajor、命中窗 nt 0.10-0.22 + 0.36-0.50。**加入 pool**。
- **`Wushi_SlideRoll`**（滑行翻滾，2.77s，前進 **20m**）：clip + state 匯入，**未接線**——20m 位移最極端，而且這比較像**閃避/翻滾**不是攻擊。待使用者決定：當 boss 迴避動作？還是翻滾攻擊？
- `Wushi_Attack_ScissorTakedown`（續 46）現在也可以用 `cancelClipBodyDrift` 救回，但仍留在 pool 外（使用者已跳過）。

`normalAttackPool` = [SwordJudgment, CrossSlash, **ThrustStab, TwistCleave**]。改 `BossAttackDefinition.cs` / `BossStateMachine.cs` / 2 新 asset / 3 新 fbx / `Wushi.controller` / `GreyboxTest.unity`。編譯無錯，**EditMode 288/288 綠**。
**待使用者對焦 Play 調校**：(1) ThrustStab / TwistCleave 的 `cancelClipBodyDrift` 有沒有把身體釘穩（腳會不會滑）；(2) `lungeTargetGapMeters` 落點；(3) 命中窗 nt；(4) SlideRoll 要當什麼用。

### 追加94 續 48（2026-09-02）— 滑行翻滾接線為「翻滾撲擊」（使用者選 b）

`Wushi_Attack_SlideRoll.asset`（新，guid 5bf4cca7...）：
- nt 0-0.68 貼地翻滾前衝（`cancelClipBodyDrift` 釘住身體 + `lungeDistanceFromTargetGap` gap−1m / clamp 0..9 帶動前移 → 3.5m standoff 滾 ~2.5m、8m 外滾 ~7m，真 gap-closer）→ nt 0.72-0.80 起身下劈。
- 命中窗 nt 0.74-0.90（起身那記劈，blade speed 42）→ contact 2.05-2.49s（443ms、×2.21）。前搖 0.30s + ~1.7s 翻滾 = 長前兆，好讀。
- 傷害 32/poise 20、knockback 0、ContinueCombo、weight 0.5/0.7、cooldown 6s、minDist 2.5 / maxDist 9。
- **加入 pool** → `normalAttackPool` = [SwordJudgment, CrossSlash, ThrustStab, TwistCleave, **SlideRoll**]（5 招）。

`Wushi.controller` `Wushi_SlideRoll` state（續 47 已建）。編譯無錯，**EditMode 288/288 綠**。
**待使用者 Play**：翻滾撲擊的距離/落點/命中時機；翻滾途中要不要加一個輕的碰撞命中（nt 0.15-0.35）。

### 追加94 續 49（2026-09-02）— 三招位移統一設計（有效行程 1~4m，我方設計、待使用者測）

使用者：「先都給你自行設計，範圍 1~4m，我來測試結果。」

三招都 `cancelClipBodyDrift=1`（歸零 Meshy clip 烤入的 4~20m 位移）＋ `lungeDistanceFromTargetGap=1`（自動對準玩家、`forwardDistance` 不用），調 gap/max 讓有效行程落在 1~4m、且**結束位置 ~1.6-2.2m（剛好在踢擊圈 1.6m 外，刀還搆得到）**：

| 招 | lungeTargetGapMeters | lungeMaxMeters | window(nt) | 3.5m standoff 行程 | 5m 行程 |
|---|---|---|---|---|---|
| ThrustStab | 1.8 | 4 | 0.08-0.40 | ~1.7m | ~3.2m |
| TwistCleave | 2.2 | 3 | 0.10-0.42 | ~1.3m | ~2.8m |
| SlideRoll | 1.6 | 4 | 0.00-0.66 | ~1.9m | ~3.4m |

CrossSlash / SwordJudgment 不動（本來就無 drift、原地打）。純 asset 值改動，無 code、無 EditMode 影響。
**待使用者 Play**：三招行程/落點/命中感；不順就改 `lungeTargetGapMeters`（往上 = 停更遠）或 `lungeMaxMeters`。

### 追加94 續 50（2026-09-02）— 三個位移招改為「遠距 gap-closer」，近距只出原地招

使用者：「這幾招由於都有位移，改為玩家距離較遠時才觸發，作為快速接近玩家手段。」

- **`BossStateMachine` 的 approach-time gap-closer filter** 從 `a => a.UseRootMotion` 放寬成 `a => a.UseRootMotion || a.LungeDistanceFromTargetGap`。原本這條（追加29「有連續位移的可以不用綁死近戰距離」）只讓 root-motion 招在 approach 途中直接出；現在 `lungeDistanceFromTargetGap` 招（會追玩家實時位置）也算，作為從遠處衝進來的手段。
- **`ThrustStab` / `TwistCleave` `minDistance` 0→4.0、`maxDistance`→11；`SlideRoll` `minDistance` 2.5→4.5、`maxDistance`→13。** → 近距 standoff（`AttackReadinessDistance` 仍 3.5，取 pool 最小 maxDistance = SwordJudgment/CrossSlash 的 3.5）時 `PickAttack()` 永遠 roll 不到這三招（3.5 < 4）；只有 approach 途中、玩家 ≥4m 時那條 gap-closer 路徑會選它們。
- 效果：玩家 4m+ → 武士不再慢慢走過來，直接前刺/扭劈/翻滾衝進來（落點 ~1.6-2.2m，續 49 調的）→ 貼近後用 SwordJudgment / CrossSlash 原地打 → 玩家拉開距離 → 再衝一次。
- 近距 pool 實質剩 [SwordJudgment（+衍生 OverheadSlam）, CrossSlash] + SpartanKick（貼身）+ 週期 OverheadSlam + LeapSlam。

改 `BossStateMachine.cs`（1 行 filter）+ 3 asset 的 min/maxDistance。編譯無錯，**EditMode 288/288 綠**。
**待使用者 Play**：(1) 玩家站遠時武士會衝進來（三招輪替）；(2) 衝的距離感；(3) 貼近後切回原地招順不順。

### 追加94 續 51（2026-09-02）— 修：黃點鎖定亂跳 + 武士貼近後還一直衝 → `cancelClipBodyDrift` 撤掉，改真 root motion

使用者：用三個位移招時 (1) 玩家對武士的黃點鎖定亂挑、定位到錯位置；(2) 武士明明已經衝到玩家面前還一直做位移招，「是不是距離判定有問題」。

**根因確認：`cancelClipBodyDrift`（續 47）把 root 往後推來抵銷 Hips 前飄 → `transform.position` 跟畫面上武士的實際位置脫鉤。**
- `LockOnTarget` 掛在 root（`aimPoint = ChestAimPoint`）→ root 被推到武士身後 2~3m → 黃點指到空的地方 / 掃描距離爆掉導致重挑目標。
- `HorizontalDistance()` = `target.position − transform.position`（root）→ 武士畫面上已到玩家面前，但 root 在後面，距離讀成「玩家還很遠」→ approach gap-closer 條件一直成立 → 一直衝。

**改成真 root motion（比照屁孩王 LeapSmash/ChargeSlam，transform / capsule / 鎖定點一起動、不脫鉤）：**
- **`武士` Animator GameObject 加 `BossAnimatorRootMotionRelay`**（空 `OnAnimatorMove` + `applyRootMotion=true`；對非 `useRootMotion` state 零影響，FSM 只在 `useRootMotion` 招才讀 `deltaPosition`）。
- 三個 clip **重匯入 `lockRootPositionXZ=false`** → RootT 曲線驅動 transform。實測 RootT 真實位移：ThrustStab **2.2m**、TwistCleave **0.8m**、SlideRoll **5.4m**（續 47 的「4.5/6.6/20m」是 `AnimationMode` 取樣不套 root motion 的假象、嚴重高估）。
- 新 `BossAttackDefinition.rootMotionScale`（Range 0-2，乘在 `deltaPosition` 上）：ThrustStab **1.3**（→2.9m）、TwistCleave **1.5**（→1.2m）、SlideRoll **0.55**（→3.0m）。都在 1~4m。
- 三招改 `useRootMotion=1` / `cancelClipBodyDrift=0` / `lungeDistanceFromTargetGap=0`；`rootMotionEndNormalized` 0.65-0.85 收在位移做完。gap-closer filter 還原回 `a => a.UseRootMotion`（現在剛好涵蓋這三招）。min/maxDistance（4/11、續 50）不變。
- `cancelClipBodyDrift` / `lungeDistanceFromTargetGap` 程式與欄位**留著**（沒別的招用，當備援）。

改 `BossStateMachine.cs`（deltaPosition × scale、filter 還原）/ `BossAttackDefinition.cs`（新 `rootMotionScale`）/ 3 asset / 3 fbx.meta / `GreyboxTest.unity`（武士 + relay）。編譯無錯，**EditMode 288/288 綠**。
**待使用者對焦 Play**：(1) 鎖定黃點不再亂跳、跟著武士；(2) 武士衝到面前就切原地招、不再連續衝；(3) 三招位移距離（不對就調 `rootMotionScale`）；(4) 真 root motion 下模型有沒有跟 capsule 分家（若有殘留脫鉤，`cancelClipBodyDrift` 可再疊回去微調）。

### 追加94 續 52（2026-09-02）— gap-closer 衝不到玩家 → root motion 改「按實際距離縮放」；TwistCleave 退回近戰

使用者：「有觸發但沒衝到玩家面前，所以都是空砍。」續 51 用**固定** `rootMotionScale`（ThrustStab ×1.3=2.9m、SlideRoll ×0.55=3m），但 gap-closer 從 4~11m 開打，固定 3m 位移 → 差 1~8m → 空砍。

- **新 `BossAttackDefinition.rootMotionAimAtTarget`**（+ `rootMotionAimGapMeters` 1.8 / `rootMotionAimMaxMeters` / `rootMotionClipForwardMeters`）：為 true + `useRootMotion` 時，`BeginAttack` 依 `HorizontalDistance()` 算出「要落在玩家前 1.8m」需要的位移，除以 clip 自己的實測淨前進量得出**這一次施展的縮放值**（clamp 0~2）。`_rootMotionScaleRuntime`；`ApplyMotion` 的 `deltaPosition ×` 讀它。仍是真 root motion、不脫鉤。
- **ThrustStab**：`rootMotionAimAtTarget`、clipFwd 2.2、min 4 / max 7。gap 5m 時縮放 ~1.45 → 走 3.2m → 落玩家前 1.8m。
- **SlideRoll**：`rootMotionAimAtTarget`、clipFwd 5.4、min 4.5 / max 13。gap 7m → 縮放 ~0.96 → 走 5.2m；gap 13m → clamp 2.0 → 走 10.8m → 落前 ~2.2m。
- **TwistCleave 退回近戰池**：它 clip 淨前進只有 **0.8m**，當 gap-closer 沒意義。改 `minDistance` 0 / `maxDistance` 3.6、`rootMotionAimAtTarget` off、固定 `rootMotionScale` 1.6（從 3.5m standoff 小步進 ~1.3m）。
- 近戰池 = [SwordJudgment, CrossSlash, **TwistCleave**]；gap-closer（只走 approach 路徑）= [ThrustStab, SlideRoll]。`AttackReadinessDistance` 仍 3.5。

改 `BossAttackDefinition.cs`（新 4 欄位）/ `BossStateMachine.cs`（`_rootMotionScaleRuntime` 在 BeginAttack 算、ApplyMotion 用）/ 3 asset。編譯無錯，**EditMode 288/288 綠**。
**待使用者 Play**：(1) ThrustStab / SlideRoll 現在衝得到玩家、不空砍；(2) 落點 1.8m 對不對（調 `rootMotionAimGapMeters`）；(3) TwistCleave 近戰步進感；(4) 鎖定黃點正常（續 51 的脫鉤已解）。

### 追加94 續 53（2026-09-02）— 加入 `Wushi_TwinStrike`（雙重連擊，原地雙擊）

使用者 `雙重連擊.zip` → `Wushi_TwinStrike`。檔名 `Double_Combo_Attack`、2.83s，與已退役的 `Wushi_DoubleCombo` 同源 clip。

**這次量對了**：實測真 RootT 淨前進只有 **0.63m**（舊 DoubleCombo designNotes 說「起始在 root 後 3m、往前走」是 `AnimationMode` 取樣不套 root motion 的假象——當初為此加的 2.5m 前衝根本是在補一個不存在的位移）。→ 這其實是**原地雙擊下劈**。

- 匯入 Humanoid（`lockRootPositionXZ=false`）→ `Wushi.controller` `Wushi_TwinStrike` state（speed 1.0）。
- `Wushi_Attack_TwinStrike.asset`（新，guid eaffe536...）：`useRootMotion=1` + `rootMotionScale 1.4`（0.63m→~0.9m 小步進）、`rootMotionAimAtTarget=0`、近戰 pool（minDist 0 / maxDist 3.6）。
- 命中窗 nt 0.18-0.32（第一劈）+ nt 0.52-0.68（第二劈，`damageMultiplier 1.15`）→ contact 0.51-0.91s（397ms、×1.98）/ 1.47-1.93s（453ms、×2.27）。傷害 22（第二劈 25.3）/ poise 16、knockback 0、ContinueCombo、weight 1.2/1.3、cooldown 2s。
- **加入近戰 pool** → `normalAttackPool` = [SwordJudgment, CrossSlash, ThrustStab, TwistCleave, SlideRoll, **TwinStrike**]（近戰 4 + gap-closer 2）。

`Wushi.controller` + 2 新 asset + 1 新 fbx + `GreyboxTest.unity`。編譯無錯，**EditMode 288/288 綠**。
**待使用者 Play**：TwinStrike 兩劈的命中時機、傷害 22/25.3、小步進感。

### 追加94 續 54（2026-09-02）— 屁孩王新增 4 個機動招（跑酷翻越 / 繩索後空翻 / 剪刀腳摔 / 滑行翻滾）

使用者為屁孩王（菁英怪）加入 4 個 Meshy 動作。屁孩王 Animator 掛在 `Visual` 子物件、已有 `BossAnimatorRootMotionRelay`，所以真 root motion 直接可用。

實測真 RootT 淨前進（clip `averageSpeed.z × length`）：ParkourVault **2.1m**、BackflipCrouch **5.6m**、ScissorTakedown **4.5m**（左偏 2.2m）、SlideRoll **5.4m**。全部 `useRootMotion=1`，`lockRootPositionXZ=false` 匯入。

| PW2_Attack_* | clip | 觸發 | 命中 | 傷害 | 說明 |
|---|---|---|---|---|---|
| **ParkourVault** | Parkour_Vault_with_Roll 2.10s | 近距（min 0 / max 3.6）| Body nt 0.55-0.70 | 20 | 翻越 + 前滾撞擊，`rootMotionScale` 1（~2m） |
| **BackflipCrouch** | Rope_Hang_Backflip_to_Crouch 1.90s | gap-closer（min 4.5 / max 13、`rootMotionAimAtTarget`）| Body nt 0.58-0.72 | 26 | 高處後空翻 → 落地蹲，戲劇性長距入場 + 落地衝擊 |
| **ScissorTakedown** | Jumping_Head_Scissor_Takedown 3.67s | gap-closer（min 4 / max 11、aim）| RightFoot nt 0.48-0.68 | 30 | 前跳剪腿摔 |
| **SlideRoll** | sliding_rool 2.77s | gap-closer（min 4 / max 13、aim）| RightHand nt 0.72-0.88 | 26 | 貼地翻滾 → 起身手擊 |

全部 knockback 0、ContinueCombo、weight 0.5-1.0。`PiHaiWangV2.controller` 加 4 state；`屁孩王.normalAttackPool` = [PunchCombo1, HighKick, GuardKick, ChargeSlam, **ParkourVault, BackflipCrouch, ScissorTakedown, SlideRoll**]。

4 新 fbx（PW2 命名）+ 4 新 asset + `PiHaiWangV2.controller` + `GreyboxTest.unity`。編譯無錯，**EditMode 288/288 綠**。
**待使用者對焦 Play 調校**：(1) 各招衝到玩家的距離（`rootMotionClipForwardMeters` / `rootMotionScale` —— 屁孩王 1.5x scale 下實際位移可能是量測值的 1.5 倍，要調）；(2) 命中窗時機（`part` / nt）；(3) BackflipCrouch 起手在空中會不會怪（clip 起始 hipY 2.1，屁孩王站地上）；(4) 傷害。

### 追加94 續 55（2026-09-02）— 武士開場演出觸發範圍縮小（本來擋住去屁孩王的路）

使用者：武士開場演戲的觸發範圍讓玩家沒辦法接近屁孩王。

`BossRoomTrigger`（`BossTrigger` → 武士 intro Timeline）的 BoxCollider 本來是 **30(寬) × 4 × 1** 的橫牆，位在 Z=4，橫跨整張地圖 X[-15..15]。從出生點（-2.5, 0）往北走去**任何**北側目標都會穿過它 → 被迫看武士開場。屁孩王在 (12, 12)。

- 改成 **8 × 4 × 4** 的方塊，移到武士正前方（pos (0, 1.6, 8)，world X[-4..4] Z[6..10]）。
- 從出生點直線往北走向武士（X≈-2.5）→ 進方塊 → 武士 intro 照常觸發。
- 從出生點斜走向屁孩王（X=12）→ Z=6 時人在 X≈4.8、已在方塊外 → 不觸發。

純場景改動（`GreyboxTest.unity` 的 `BossRoomTrigger` transform + BoxCollider）。無 code、EditMode 不受影響。
**待使用者 Play 確認**：(1) 走向屁孩王不再被武士 intro 攔截；(2) 正面走向武士仍會觸發開場。

### 追加94 續 56（2026-09-02）— 十足蟲關掉自動追擊 ＋ 新增「Boss 動作除錯模式」

**1. 十足蟲不自動追擊**：`TenLeggedBugController` 新 `autoAggro`（預設 true）；`TickPatrol` 的 detection→Chase 現在 gate 在它上面。GreyboxTest 的 `十足蟲` 實例設 **false** → 永遠留在 Patrol（在出生點附近晃），除非被外部（除錯工具/腳本）驅動。被打不會 aggro（本來就沒這條路徑）。

**2. Boss 動作除錯模式**（`BossAnimationDebugMode.cs` in `_Project/Game/Debug/` ＋ 選單 `Tools/Live2DAction/[Debug] Setup Boss Animation Debug Mode`）：
- **F7** 進/出。進入時：切到守望者的 `Viewpoint` 攝影機（其他 active 攝影機快照→關閉→離開時還原，同 `SpectatorCameraToggle` 手法）；把所有 target 的 `BossStateMachine` / `NavPathFollower` / `HealthRegeneration` / `BossLifeNodeController` / `BossSignalReceiver` 關掉並把 root transform 釘在原地。
- **Tab** 循環 target（武士 ↔ 屁孩王）。
- **數字 1–9、0** 播該 target 的第 1–10 個 Animator state（`CrossFadeInFixedTime`）。畫面左上 `OnGUI` 列出 target + state 清單 + 對應鍵。
- **P** 暫停/繼續、**R** 重播、**-/=** 慢放/快放（animator.speed 0.05–2）、**,/.** 環繞攝影機。
- 離開時全部還原（攝影機、被關的元件、animator.speed）。無 gameplay 依賴、預設不啟用。
- 設定工具讀 `Wushi.controller`（16 state）/ `PiHaiWangV2.controller`（13 state）的 Base Layer state 名（排除 Locomotion）自動填。守望者攝影機每幀被工具重新定位到框住當前 target（`cameraOffset` / `aimHeight` 可調）。
- Boss 有新招時重跑選單即可。

改 `TenLeggedBugController.cs`（1 欄位 + 1 行 gate）＋ 2 新檔（`BossAnimationDebugMode.cs` / `BossAnimationDebugSetup.cs`）＋ `GreyboxTest.unity`（十足蟲 autoAggro、新 `BossAnimationDebugMode` 物件）。編譯無錯，**EditMode 288/288 綠**。
**待使用者 Play**：(1) 十足蟲不再追人；(2) F7 進除錯模式、Tab 切目標、數字播動作、守望者視角框住目標。

### 追加94 續 57（2026-09-02）— 除錯模式：按數字有 log 但畫面沒動 → 守望者攝影機的 Camera 元件本身是關的

使用者：Boss 動作除錯模式看得到選單、按數字沒反應。查 log 發現 `[BossAnimDebug] 武士 -> Wushi_XXX` 有印、`CrossFadeInFixedTime` 有呼叫、state 名都對——**輸入沒問題，是畫面沒 render**。

根因：守望者的 `Viewpoint` 攝影機用的是「GameObject 一直 active、**`Camera` 元件 `.enabled=false`**」的模式（跟場景其他攝影機「GameObject toggle、元件常開」相反）。原本 `BossAnimationDebugMode` 只對 debugCamera 做 `SetActive`，沒碰 `Camera.enabled` → 進除錯模式時把其他攝影機全關、Viewpoint 又沒真的開 → 全黑（選單 OnGUI 疊在上面照樣看得到）。

**修（`BossAnimationDebugMode.cs`）：**
- 新 `EnableDebugCamera(bool)`：進入時快取 + 開 debugCamera 底下**每個 `Camera` 元件的 `.enabled`**（順便關它的 AudioListener），離開時完整還原。`KeepDebugCameraLive()` 每 LateUpdate 重新確保。
- 進除錯模式時把 target 的 `Animator.cullingMode` 暫時設 `AlwaysAnimate`（框鏡頭途中角色短暫離畫面也不會停格），離開還原。
- 相機掃描排除清單改用 `_debugCams`（不只 debugCamera 本身，含子 Camera）。
- Edit-mode 驗證：Enter → Viewpoint.enabled True / FSM.enabled False / cull AlwaysAnimate；Exit → 全部還原。

編譯無錯，**EditMode 288/288 綠**。
**待使用者 Play 確認**：F7 → 看到守望者視角框住武士、按 1-0 武士做出對應動作、Tab 切屁孩王。

### 追加94 續 58（2026-09-02）— 除錯模式：`-/=` 調的 speed 換動作就被重設回 1

使用者：用 `-/=` 調慢後，按數字播下一個動作，speed 又回 1。

`BossAnimationDebugMode.Play()` 之前每次都 `_animSpeed = 1f; animator.speed = 1f`。改成：
- `_animSpeed`（使用者選的播放速率 0.05–2）**跨動作保留**，`Play()` 只 `ApplySpeed()` 套用現值、不重設。
- 新 `_paused` bool 跟 speed 分開；`P` 切換 pause（暫停不再吃掉慢放設定），`-/=` 調 speed 時順帶解除 pause。
- 有效 `animator.speed` = `_paused ? 0 : _animSpeed`，每 LateUpdate 重新 assert（防其他東西偷偷改回）。
- 只有進除錯模式（F7）會把 speed 重設 1、pause 清除。OnGUI 顯示 `[PAUSED]`。

編譯無錯，**EditMode 288/288 綠**。
**待使用者 Play**：`-/=` 調慢後連續按數字播不同動作，速率維持；`P` 暫停/繼續不影響慢放值。

### 追加94 續 59（2026-09-02）— 除錯模式：播動作時放開位移，動作結束才彈回原位

使用者：站原地正確，但播動作時應該讓動作**照原本的位移跑**，整段結束後再彈回原位。之前 LateUpdate 每幀都把 transform 釘死 → 動作永遠原地播。

`BossAnimationDebugMode`：
- 新 `_clipRunning` / `_clipTarget` / `_clipState`。按數字播動作時記錄，並先把上一個還在跑的 target `SnapPlayingTargetBack()`（回原位）再開新的。
- LateUpdate：正在播的 target **不釘**，改成每幀 `pinRoot.position += animator.deltaPosition` + `deltaRotation`（跟 FSM 讀 root motion 同手法，武士/屁孩王的 relay 保證 `deltaPosition` 有值）→ 動作的真實位移會跑出來。
- 偵測結束：`GetCurrentAnimatorStateInfo(0)` 不在 transition、`IsName(_clipState)`、`normalizedTime >= 1` → `SnapPlayingTargetBack()` 把 pinRoot 設回進除錯模式當下記的原位/原朝向。
- 其他沒在播的 target 照樣每幀釘死。
- Tab 切目標 / F7 離開 也會先 `SnapPlayingTargetBack()`。
- 暫停（P，speed 0）時動作不前進 → 不會判定結束、停在半途讓你檢視；解除後繼續跑完才彈回。

編譯無錯，**EditMode 288/288 綠**。
**待使用者 Play**：F7 → 按數字，角色照動作位移移動，整段播完彈回原位；連按不同數字每次從原位開始。

### 追加94 續 60（2026-09-02）— 除錯模式：武士「空中飛劈」→ deltaPosition 的 Y 歸零，貼地

使用者：除錯播放跟實戰效果不一樣，武士像在空中飛劈。

續 59 每幀套 `pinRoot.position += animator.deltaPosition` 是**含 Y** 的。有些 clip 的根節點有垂直位移（`keepOriginalPositionY` 匯入），於是武士整段飄起來。實戰不會——`BossStateMachine.ApplyMotion` 是 `rootMotionDelta.y = 0f` + 重力把 boss 壓在地上。

修（`BossAnimationDebugMode` LateUpdate）：只套水平位移（`d.y = 0`），每幀把 `pinRoot.position.y` 壓回進除錯模式當下記的地面 Y。跟實戰一致。

**已知仍有差異**（動畫檢視夠用、非 bug）：(1) 無目標 → `faceTargetSnapOnStart` / yaw 偏移不生效，招式朝武士被釘的方向；(2) 水平位移是 clip 原生 RootT 全量，實戰的 `rootMotionScale` / `rootMotionAimAtTarget` 縮放不套用（所以 SlideRoll 在除錯裡衝比較遠）。要的話再讓除錯模式吃 attack asset 的縮放。

編譯無錯，**EditMode 288/288 綠**。

### 追加94 續 61（2026-09-02）— 除錯模式：LeapSlam{6} 在實戰飛高空、除錯裡只原地短飛 → 加「FSM 腳本高度弧線」還原

使用者：武士第 6 個動作（`Wushi_LeapSlam`）實戰飛到高空，除錯模式下只原地短飛。

根因：LeapSlam 的「off-screen 高度」在實戰是 `BossStateMachine` 的**腳本弧線**（`ComputeLeapSlamExtraHeight` — 三角形曲線，`tuning.LeapSlamExtraHeight` **30** 世界單位，rise 0.05 / peak 0.3 / fallEnd 0.53），clip 本身的骨骼上下移動很小。續 60 把 deltaPosition.y 歸零後，那個弧線完全沒了。

修：
- `BossAnimationDebugMode.Target` 新 `verticalArcs`（`{stateName, peakHeight, riseNt, peakNt, fallEndNt}`）。播到有登記的 state 時，LateUpdate 在地面 Y 之上疊一條同樣的三角形高度弧（`HeightAt(normalizedTime)`）。
- 設定工具讀每隻 boss 的 `leapSlamAttack.clipName` → 對應 state 名，從其 `tuning` 抓 `leapSlamExtraHeight` / rise / peak / fallEnd 自動填。已跑：武士 `Wushi_LeapSlam` peak **30**、屁孩王 `PW2_LeapSmash` peak **2.5**。
- 非弧線 state 照舊貼地；動作結束照舊彈回原位。

（Ultimate / Vanish / DiveAttack 等其他 FSM 腳本位移招在除錯裡仍不完整——同理可加 arc，使用者沒提就先不做。）

改 `BossAnimationDebugMode.cs` / `BossAnimationDebugSetup.cs` / `GreyboxTest.unity`。編譯無錯，**EditMode 288/288 綠**。
**待使用者 Play**：F7 → 按 6，武士飛高空、落地劈砍、彈回原位。不夠高就調該 target 的 `verticalArcs[0].peakHeight`。

### 追加94 續 62（2026-09-02）— 除錯模式 #8/#9 動作「套用回真實」：ChargeCut 取代 TwistCleave、ContinuousThrust 復活

使用者：「我比較喜歡武士除錯模式下的 8/9 動作的展現，請套用回真實模式。」除錯清單 #8 = `Wushi_ChargeCut`、#9 = `Wushi_ContinuousThrust`，兩個都是孤兒 clip（沒在任何 pool）。

實測（真 RootT）：`Wushi_ChargeCut` net 前進 **0.78m**（原地）、`ContinuousThrust` net **0m**（原地）。（舊 designNotes 的「起始在 root 後方」全是 AnimationMode 假象。）

**#8 `Wushi_ChargeCut`** —— 跟 `Wushi_TwistCleave`（扭轉前劈）同 Meshy 來源家族、不同 export，但除錯 state 跑 **speed 1.15**（使用者偏好的就是這個）＋多一段「前伸刺」相位。
- 新 `Wushi_Attack_ChargeCut.asset`（attackId **CleaveCharge**，ChargeCut 被 SwordJudgment 佔）：3 段命中窗 nt 0.13-0.21 / 0.40-0.50（×1.15）/ 0.80-0.92，傷害 30/poise 20、isMajor、原地、close pool。`Wushi_ChargeCut` state speed 維持 1.15。
- **`Wushi_Attack_TwistCleave` 從 pool 移除**（同招、這是使用者指的版本）。asset/state 留磁碟。

**#9 `Wushi_Attack_ContinuousThrust`** —— 復活（追加89 退役的旋身連刺）。既有 asset 修：5 段命中窗 `deflectReaction` 0→1（ContinueCombo）、`maxDistance` 2.4→3.5、`maxAngle` 30→45。`Wushi_ContinuousThrust` state speed 1.25。healthDamageIsPercentOfTargetMax（1%/刺、~5% 全套）不動。加入 close pool。

`normalAttackPool` = [SwordJudgment, CrossSlash, ThrustStab, SlideRoll, TwinStrike, **ChargeCut, ContinuousThrust**]（近戰 5 + gap-closer 2）。timing report：ChargeCut real 1.77s（窗 141/177/212ms）、ContinuousThrust real 2.43s（5 窗 121-218ms）。

2 新 asset（ChargeCut）+ 3 asset 修（ContinuousThrust windows/dist）+ `GreyboxTest.unity`。編譯無錯，**EditMode 288/288 綠**。
**待使用者 Play**：(1) ChargeCut / ContinuousThrust 在實戰的手感跟除錯裡一致；(2) ChargeCut 前兩段窗偏短（speed 1.15），太難彈就降 state speed；(3) 移除 TwistCleave 對不對。

### 追加94 續 63（2026-09-02）— ContinuousThrust 重做為「低頭連刺 + 前墊步 + 每下頂開」；ChargeCut 權重拉高

使用者：#8 ChargeCut 很少出；#9 ContinuousThrust 向前連刺「鎖定玩家胸口（有高度差）+ 小段前墊步 + 每一下把玩家頂開」。

**新 `BossAttackDefinition.attackPitchDegrees`（0-45）+ `BossStateMachine`：** 攻擊中把整個 visual **前傾/低頭** N 度（`UpdateAttack` 在 `FaceTarget`（純 yaw）之後套 local pitch，nt 0→0.15 ease in、0.78→1.0 ease out）。把高個子武士的刀身判定壓到站地玩家的胸口高度，直立的 CharacterController 不動。

**`Wushi_Attack_ContinuousThrust`：**
- `attackPitchDegrees` **20**（低頭刺玩家胸口）。
- `attackMotion.forwardDistance` 0 → **3.5**（nt 0.05-0.68）＝前墊步，武士整段連刺往前推進 3.5m，追著被頂開的玩家。
- `knockbackForce` 0 → **1.8**（每個命中窗都 `ApplyKnockback` 一次 → 5 刺各頂開玩家一次；本招是 session「只有踢擊擊退」規則的**刻意例外**）。
- `lateTracking` 0.1→0.2、`trackingDropNormalizedTime` 0.15→0.6（連刺期間持續微調朝向追玩家）。
- 權重 1/1 → **1.8/2**（更常出）。

**`Wushi_Attack_ChargeCut`：** 權重 1.2/1.3 → **2/2.2**。

改 `BossAttackDefinition.cs` / `BossStateMachine.cs`（pitch）/ 2 asset。編譯無錯，**EditMode 288/288 綠**。
**待使用者對焦 Play**：(1) ContinuousThrust 低頭刺打得到站地玩家、每刺頂開、武士追著推進；(2) `attackPitchDegrees` 20 夠不夠低（打不到就加到 25-30）；(3) `forwardDistance` 3.5 / `knockbackForce` 1.8 的推進 vs 頂開平衡；(4) ChargeCut / ContinuousThrust 現在夠常見。

### 追加94 續 64（2026-09-02）— 「正式模式看不到動作 9」→ 其實有選到、被踢擊打斷；武士重新拉開站位

使用者：正式模式看不到武士動作 9（ContinuousThrust）。查 console `PickAttack: chose ContinuousThrust` **有出現**（dist=2.82 / **0.92**）——問題是武士一堆招都在 **dist≈0.92m** 出（點名一票 `dist=0.92/0.93`），那在踢擊圈 1.6m 內 → 每 2s 強制 SpartanKick 把正在連刺的動作切掉 → 玩家看到的是踢擊不是連刺。

根因：前衝招（ContinuousThrust `forwardDistance` 續 63 設 3.5、TwinStrike root motion ×1.4）一路把武士推進點名，武士停在那不斷從貼身出招、從不重新拉開。

**修：**
1. `Wushi_Attack_ContinuousThrust.forwardDistance` **3.5 → 1.5**（前墊步是小步、不是衝鋒）；`maxAngle` 45→60、`maxDistance` 3.5→4、`cooldown` 4→3、權重 →2/2.2。
2. **`BossStateMachine` 重新加回「太近就退位」**（續 39 加、續 52 移除的較溫和版）：`UpdateApproach` 新最高優先分支 `distance < AttackStandoffFloor`(~2.2m) → `MoveAwayFromTarget`（新加回的 helper）；`UpdateIdle` 同條件 → 轉 `Approach`。武士被前衝招推進點名後會退回 ~2.2m（仍在踢擊圈 1.6m 外、所有原地招刀夠得到）再出招 → 不再卡貼身、不再被踢擊洗掉連刺。
   - 續 39 使用者當時反對「退開」是因為那時劍招搆不到遠距；現在劍招都能從 3.5m 打到，退到 2.2m 完全 OK。

改 `BossStateMachine.cs`（MoveAwayFromTarget 回歸 + 2 分支）/ `Wushi_Attack_ContinuousThrust.asset`。編譯無錯，**EditMode 288/288 綠**。
**待使用者 Play**：(1) 現在看得到 ContinuousThrust 完整連刺（低頭 + 前墊步 + 每刺頂開）；(2) 武士不再卡在玩家臉上循環貼身招；(3) 退位動作會不會太頻繁/來回。

### 追加94 續 65（2026-09-02）— 修：R 大招丟出武士刀後，回收把刀掛回 Player root（1/80 尺寸）而非手骨 → 刀「不見」

使用者：玩家的武士刀不見了、沒握在手上。

查場景：`WolfsGravestone`（血刀 wrapper）在 `Rhand_Weapon2` 手骨底下、active、mesh bounds 正常——**場景本身沒壞**。是 runtime bug：`UltimateAbility.ThrowSequence` 丟刀時 `weapon.SetParent(null)` 但**沒記原本的 parent**；回收（正常返回 + `OnDisable` 中斷）都 `weapon.SetParent(transform, true)`——`transform` 是 **Player root**，不是刀原本掛的 `Rhand_Weapon2` 手骨（帶 ~80x 骨骼縮放）。捕捉的 localPos/Rot 是「相對手骨」的值，套到 root 底下 → 刀掉到 Player 原點、縮成 **1/80 大小** → 看起來就是消失。

**修（`UltimateAbility.cs`）：**
- 丟刀前 `Transform homeParent = weapon.parent`，存進新欄位 `_weaponHomeParent`。
- 返回動畫的 `homeWorldPos`/`homeWorldRot` 用 `homeParent` 算（不是 Player root），飛回正確的手部世界位置。
- `SetParent` 回收（返回 + `OnDisable`）改成 `homeParent`（null 才 fallback `transform`）。

R 大招丟出/收回不影響 EditMode。編譯無錯（Console 的 `CubismRenderController` IndexOutOfRange 是既有 Live2D SDK 問題、無關）。**EditMode 288/288 綠**。
**待使用者 Play**：按 R 丟出武士刀 → 收回後刀正常握在右手（正確尺寸/位置）。（若刀在**還沒按 R 前**就不見，那是別的問題，回報 Play 重現步驟。）

### 追加94 續 66（2026-09-02）— 屁孩王：除必殺技外所有攻擊都墊步/縮放位移到玩家面前

使用者：屁孩王除必殺技（`PW2_Attack_LeapSmash`）外的攻擊都要盡可能外觀上接近玩家距離再打。

現況問題：`AttackReadinessDistance` = 0.98m（PunchCombo maxD），但 ChargeSlam（maxD 3.5、實測 RootT 只 **0.84m**）/ ParkourVault（maxD 3.6、RootT 2.1m）從遠處出手時位移搆不到玩家 → 打空氣。

**改（8 個 asset，不動 `PW2_Attack_LeapSmash`）：**
- **PunchCombo1 / HighKick / GuardKick**：`maxDistance` ~1 → **2.5**、`faceTargetSnapOnStart` 開、`lungeDistanceFromTargetGap` 開（墊步到玩家前 0.7m、上限 2.5m、window nt 0.05-0.55）。
- **ChargeSlam**：`rootMotionAimAtTarget` 開（clipForward 0.84、落點玩家前 1.0m、上限 5）、`maxDistance` 3.5 → 2.5（clip 位移小、拉太遠搆不到）。
- **ParkourVault**：`rootMotionAimAtTarget` 開（clipForward 2.1、落點前 1.0m、上限 5）。
- **BackflipCrouch / ScissorTakedown / SlideRoll**（本來就 aim）：落點 gap 1.2m。
- **Breakdance**（`breakdanceAttack` flourish，非必殺技）：`maxDistance` 1.4 → 3、snap 開、`lungeDistanceFromTargetGap`（前 0.8m）。

`AttackReadinessDistance` 0.98 → **2.5m**（屁孩王 approach 停 2.5m，近戰墊步到 ~0.7m、位移招縮放到玩家面前）。

9 個 `PW2_Attack_*.asset`。無 code。編譯無錯，**EditMode 288/288 綠**。
**待使用者對焦 Play**：(1) 屁孩王每一招（除 LeapSmash）都墊步/衝到玩家面前才打、不再打空氣；(2) 墊步/落點距離（`lungeTargetGapMeters` / `rootMotionAimGapMeters`）合不合適；(3) Breakdance / ChargeSlam 有沒有 clip 自帶位移 + 墊步疊加。

### 追加94 續 67（2026-09-02）— 確認屁孩王有剪刀腳摔 ＋ F7 除錯模式加滑鼠滾輪縮放

1. **屁孩王的「跳躍頭部剪刀腳摔」** —— 已在（續 54 加的）：`PW2_Attack_ScissorTakedown` 在 `屁孩王.normalAttackPool`、attackId ScissorTakedown、clip `PW2_ScissorTakedown`、state 存在、fbx 在、`useRootMotion` + `rootMotionAimAtTarget`、命中窗 part 3(RightFoot) nt 0.48-0.68、weight 0.5。無需再加。

2. **F7 除錯模式滾輪縮放**（`BossAnimationDebugMode`）：`Mouse.current.scroll` → `_zoom`（相機距離倍率，clamp 0.3-3、每格 0.12），套在 `cameraOffset * _zoom`（縮放整個 offset 保持取景角度）。進除錯模式重設 1。OnGUI 顯示 `zoom X.XX` + 提示 `wheel zoom`。

改 `BossAnimationDebugMode.cs`。編譯無錯，**EditMode 288/288 綠**。

### 追加94 續 68（2026-09-02）— F7 除錯：第 11 項以後綁 Shift+數字（原本超過 10 項就沒鍵）

`BossAnimationDebugMode` 原本只綁數字 1-0 → 前 10 個 state。屁孩王 13 個、武士 16 個，第 11 項以後點不到（`PW2_ScissorTakedown` 是屁孩王第 12 項）。

- 加 **Shift + 數字 1-0 → state 11-20**。OnGUI 每項標 `[數字]` 或 `[Shift+N]`。
- 目前對照：
  - **屁孩王 跳躍頭部剪刀腳摔 = Shift+2**（PW2_ScissorTakedown，第 12 項）
  - 屁孩王：Shift+1 BackflipCrouch、Shift+2 ScissorTakedown、Shift+3 SlideRoll
  - 武士：Shift+1 PostureFall … Shift+2 ScissorTakedown … Shift+6 TwinStrike

改 `BossAnimationDebugMode.cs`。編譯無錯，**EditMode 288/288 綠**。

### 追加94 續 69（2026-09-02）— 屁孩王 剪刀腳摔（Shift+2）改為「頭部剪刀鎖 → 跩起甩飛」

需求：這招的「倒立雙腳內扣」中段動作要朝玩家頭部鎖定，接觸到就把玩家跩起來再反向甩出擊飛。

`PW2_Attack_ScissorTakedown.asset`：
- **鎖頭**：`lateTracking` 0.05→0.25、`trackingDropNormalizedTime` 0.14→0.4 —— 起跳＋內扣期間持續朝玩家 yaw，讓內扣朝玩家頭部去。`attackPitchDegrees` 15 讓屁孩王朝倒地/站立玩家的頭部前傾。
- **落點**：`rootMotionAimGapMeters` 1.2→0.6 —— gap-closer 縮放後直接落在玩家身上（原本落 1.2m 外會抓空）。
- **命中窗**：單一窗 part 3(RightFoot) 從 nt 0.48-0.68 移到 **nt 0.40-0.55** —— 正好是雙腳內扣＋倒立（head-below-hips）那一刻，剪刀夾合＝鎖上玩家頭部。
- **跩起來 / 反向甩飛**：`basePoiseDamage` →200（必定 stagger＝被抓住）、`knockbackForce` →11、`launchesTarget` 1（上拋＋沿屁孩王前進方向硬甩，hurricanrana 式）。`superArmorDuringActiveWindows` 1（對拋不破抓）。`baseHealthDamage` 38。
- weight 0.5、`minDistance 4 maxDistance 11`、`cooldownSeconds 7`、`disallowImmediateRepeat` + `maxConsecutiveUses 1`。
- **限屁孩王**（武士版本先前已從 pool 撤掉）。
- **尚未做**真正的 socket 抓取鎖定（玩家目前是被擊飛，不是被 parent 到屁孩王身上跟著甩）—— 那是更大的後續工作。

只改 1 個 `.asset`，無 code。編譯無錯，**EditMode 288/288 綠**。
**待使用者對焦 Play**：(1) 內扣那刻 RightFoot hitbox 打不打得到站立玩家的頭/上半身（可能要調窗或 pitch）；(2) 甩飛方向感（目前沿屁孩王前進方向，非「往回」）；(3) poise 200 一定 stagger 會不會太硬。

### 追加94 續 70（2026-09-02）— 剪刀腳摔改「腳本近身抓取」（續69 的 collider 命中打不到，實測沒被抓）

續 69 給 ScissorTakedown 一個 RightFoot collider 命中窗（內扣那刻），**實戰完全打不到**：這招把屁孩王倒立過來，剪刀的雙腳落在 ~2m 高，站著的玩家 hurtbox 上緣比那低，加上落點只離玩家 0.6m，0.35m 的腳部球體永遠碰不到。

- 新 `BossAttackDefinition` 欄位：`commandGrab` / `commandGrabNormalized`(0.45) / `commandGrabRadius`(2.2)。
- `BossStateMachine.TryResolveCommandGrab()`：當 `commandGrab` 時，在 `commandGrabNormalized` 那一刻對玩家做**一次水平距離判定**，`commandGrabRadius` 內就直接套用這張 asset 的數值 —— `baseHealthDamage 38`、`basePoiseDamage 200`（必定 stagger＝跩起來）、`knockbackForce 11` + `launchesTarget 1`（上拋＋沿屁孩王前進方向硬甩＝反向甩出擊飛）。每次攻擊最多結算一次（`_commandGrabResolved`）。繞過 collider hit window。
- `PW2_Attack_ScissorTakedown.asset`：`hitWindows` 清空、`commandGrab: 1`。仍是 gap-closer（`useRootMotion` + `rootMotionAimAtTarget`，落點 0.6m）。`lateTracking 0.25` + `trackingDropNormalizedTime 0.4` 讓內扣持續朝玩家、`attackPitchDegrees 15` 前傾。
- **限屁孩王**。仍未做真正 socket 抓取鎖定（玩家是被擊飛，不是被 parent 跟著甩）—— 較大的後續工作。

改 `BossAttackDefinition.cs` + `BossStateMachine.cs` + 1 個 `.asset`。編譯無錯，**EditMode 288/288 綠**。
**待使用者對焦 Play**：(1) `commandGrabRadius 2.2` / `commandGrabNormalized 0.45` 抓取時機與範圍合不合適（Console 有 `CommandGrab ScissorTakedown: caught/whiffed` log）；(2) 甩飛方向（目前沿屁孩王前進方向，非「往回」）；(3) poise 200 必定 stagger 硬不硬。

### 追加94 續 71（2026-09-02）— 剪刀腳摔「在空中原地轉圈」修正：改用程式位移取代 root motion

實測：屁孩王只放這招時，會在空中原地轉圈、衝不到玩家面前。原因：`PW2_ScissorTakedown` clip 把約 380° 的身體旋轉烤進 **root**（avgAngularSpeed 1.8），續 70 用 `useRootMotion` 只套 `deltaPosition`（位移不套旋轉），於是前進向量被自身旋轉帶著繞圈、互相抵銷 → 幾乎不位移。

- `PW2_Attack_ScissorTakedown.asset`：`useRootMotion` 1→**0**、`rootMotionAimAtTarget` 1→0；改用程式驅動衝刺 —— `lungeDistanceFromTargetGap: 1`、`lungeTargetGapMeters 0.6`、`lungeMaxMeters 13`、`attackMotion` nt 0.12-0.45。`BeginAttack` 在出招當下鎖定與玩家的距離，沿鎖定方向直線推進，clip 的旋轉純粹變成視覺表演。`commandGrabRadius` 2.2→2.5。
- `BossStateMachine`：approach 階段的 gap-closer 選擇 `PickAttackFiltered(a => a.UseRootMotion || a.LungeDistanceFromTargetGap)` —— 程式衝刺招也能在接近階段被選（之前只認 `UseRootMotion`），這樣剪刀腳摔回到完整 pool 後仍是遠距快速接近手段。

改 `BossStateMachine.cs` + 1 個 `.asset`。編譯無錯，**EditMode 288/288 綠**。屁孩王 pool 仍暫時只有這招（測試用，還原清單在 scratchpad）。
**待使用者對焦 Play**：衝刺速度／落點、`commandGrab` 半徑與時機、甩飛方向。

### 追加94 續 72（2026-09-02）— 剪刀腳摔確認 OK，屁孩王 pool 還原

使用者確認「沒問題了」。屁孩王 `normalAttackPool` 從測試用的單招 `[ScissorTakedown]` 還原為完整 8 招：PunchCombo1 / HighKick / GuardKick / ChargeSlam / ParkourVault / BackflipCrouch / ScissorTakedown / SlideRoll。場景已存檔。無 code 變更（續 71 的 `.cs` + `.asset` 修正保留）。

### 追加94 續 73（2026-09-02）— 地圖串流 Phase 1：學校抽成 additive 場景，靠近才載入

使用者要做開放世界式的分區資源載入。方案：多場景 additive（不引入 Addressables）。詳見新文件 `Docs/MAP_STREAMING.md`。

- **新場景** `Assets/_Project/Scenes/Map_School.unity`：把 `學校` ground、`SchoolWall_*` ×5、`yuanpei_*` ×4（約 **11.9M 面**）從 `GreyboxTest` 移出（世界座標保留）。學校為純景物，零腳本、零跨場景引用；無 lightmap，沿用常駐場景的燈。加入 Build Settings（enabled）。
- **新腳本** `Assets/_Project/Game/World/MapStreamer.cs`：一區域一顆。距 `anchor`（學校中心 (0,0,-115)）`loadRadius` 90m 內 → `LoadSceneAsync(Additive)`；所有追蹤角色離開 `unloadRadius` 125m → `UnloadSceneAsync` + `Resources.UnloadUnusedAssets()`。`trackedCharacters` 留空自動抓 Player。進 Play 若場景已 additively 開著會直接接管。選取畫綠/橘雙環 gizmo。Console 印狀態轉換。
- `GreyboxTest`：加 `MapStreamer_School` GameObject（掛 `MapStreamer`，預設值即設定好）。`VehicleRoad` 留在 `GreyboxTest` 當往南的常駐視覺連接。開場只載入 `GreyboxTest`（`sceneCount == 1`），學校完全不占資源。
- 本地／空島／Player 全部仍在 `GreyboxTest`，尚未抽 `Core.unity`（Phase 4）。

改 1 新場景 + 1 新腳本 + `GreyboxTest`（+MapStreamer GO、-10 個學校 root）+ Build Settings + 新 `Docs/MAP_STREAMING.md`。編譯無錯，**EditMode 288/288 綠**。
**待使用者對焦 Play 驗證**（Editor 失焦時 Play 會凍結、MCP 測不了）：沿 VehicleRoad 往南 → 距學校 90m 時 Console `[MapStreamer] loading 'Map_School'` → `loaded`、建築出現；走回本地 → 超過 125m → `unloaded`、`sceneCount` 回 1。已知：3M 面 MeshCollider 在載入 activation 時主執行緒 cook 會頓一下（Phase 5 優化）。

### 追加94 續 74（2026-09-02）— 地圖串流 Phase 2：載入過場黑幕遮住 pop-in

- **新腳本** `Assets/_Project/Game/World/ScreenFader.cs`：單例，掛常駐場景的 `ScreenFader` GameObject。`Awake` 自建全螢幕黑 `Canvas`（`sortingOrder 32000`，蓋過所有 HUD）+ `CanvasGroup`。`SetCovered(bool, fadeSeconds)`，用 `unscaledDeltaTime`（不受 hit-stop / timeScale 影響）。無 prefab、無 setup 選單。
- **`MapStreamer` 串接**：新欄位 `useLoadCurtain`(true) / `curtainRadius`(95，要 ≥ loadRadius) / `curtainFadeSeconds`(0.35) / `curtainSettleFrames`(2)。`BeginLoad` 時追蹤角色在 `curtainRadius` 內 → 遮黑；場景 `isDone` 後進新 `Settling` 狀態多壓 2 幀（等 MeshCollider cook + 首張畫面穩定）→ 淡回。遠距觸發載入不遮（靜默串流），載入中玩家衝進範圍會補遮。卸載不遮。`OnDisable` 保險清除。無 `ScreenFader` 時整套 no-op。
- `GreyboxTest`：加 `ScreenFader` GameObject。

改 1 新腳本 + `MapStreamer.cs` + `GreyboxTest` + `Docs/MAP_STREAMING.md` / KNOWN_ISSUES。編譯無錯，**EditMode 288/288 綠**。
**待使用者對焦 Play 驗證**：往南走近學校，距 95m 內畫面應淡黑 → 學校載入 → 淡回（Console `loading 'Map_School' (curtain)...` → `loaded (curtain).`）。已知：黑幕只蓋住 pop-in、沒消除 MeshCollider cook 卡頓；黑幕期間玩家輸入沒鎖。

### 追加94 續 75（2026-09-02）— 地圖串流 Phase 2b：載入卡頓根治 ＋ 遮罩鎖輸入

續 74 的黑幕只蓋住 pop-in、沒消除 MeshCollider cook 卡頓，且黑幕期間玩家還能盲走。這次兩個都處理：

- **yuanpei 四棟的 3M 面 `MeshCollider` 全部移除**（卡頓來源）。改成：
  - `yuanpei_MainBuilding` / `ModernGlassLibrary` / `PalmLinedLibrary` 各加一顆 scene-root `<name>_Collision`（zero rotation、`BoxCollider` = 建築 renderer 世界 AABB、底部落 y≈0.5 地面）。
  - `yuanpei_QuietCampusPlaza` 直接不放 collider —— `學校` 那顆 60×60 BoxCollider（頂面 y=0.5）就是地板。
  - Box collider cook 幾乎零成本 → **載入卡頓消除**，不只是被黑幕蓋住。也順帶清掉 4 條 `Source mesh has over 2,097,152 triangles` console 警告。粗略 box 碰撞夠 greybox；要精細再手調。
- **遮罩期間鎖玩家輸入**：`PlayerInputProvider.Update` 開頭若 `ScreenFader.Instance.IsCovered` → 整個 input command 歸零（走「沒鍵盤」同一路徑）。淡出+hold 全程鎖，reveal 一開始就解。AI 的 `IInputCommand` 不受影響。

改 `PlayerInputProvider.cs` + `Map_School.unity`（-4 MeshCollider、+3 box proxy root）+ `Docs/MAP_STREAMING.md` / KNOWN_ISSUES。編譯無錯，**EditMode 288/288 綠**。
**待使用者對焦 Play 驗證**：走近學校載入時應**不再頓**、黑幕期間 WASD 無反應；學校建築仍擋得住（box 碰撞）。

### 追加94 續 76（2026-09-02）— 修：學校走到底也不載入（MapStreamer 追蹤到貓不是玩家）

實測玩家沿通道走到底、學校沒出現。原因：`MapStreamer.ResolveAutoTrackedCharacter` 用 `FindFirstObjectByType<PlayerInputProvider>()` —— 本專案 **Player 和 Cat 都掛 `PlayerInputProvider`**（貓可被附身操作），而它抓到的是貓。貓一直待在出生點、從不靠近學校 → 永遠不觸發載入。

- 改成 `FindObjectsByType<PlayerInputProvider>` 追蹤**全部**（Player + Cat 的 root）。這也才符合串流本意：不管玩家在操作誰、走向哪個區域都該把它拉進來。Play 驗證：`trackedCharacters` = [Cat, Player]。

改 `MapStreamer.cs`。編譯無錯，**EditMode 288/288 綠**。**待使用者對焦 Play 重驗**：Player 沿 VehicleRoad 往南，距學校 90m（玩家 z≈−25，剛上車道沒多久）就該淡黑載入。

### 追加94 續 77（2026-09-02）— MapStreamer 半徑收緊：走回本地才會真的卸載

續 76 讓 `MapStreamer` 追蹤 Player + Cat。但本地出生區離學校錨點 (0,0,-115) 只有 ~115m，而貓一直待在出生點（~117m）—— 落在舊 `unloadRadius` 125m 內 → 學校載入後貓自己就一直撐開，走回本地也不卸載（等於「載入一次後常駐」）。

- `MapStreamer_School`（+ code 預設）：`loadRadius` 90→**75**、`unloadRadius` 125→**100**、`curtainRadius` 95→**80**。
- 現在：Player 沿車道往南到約 z −40 載入（黑幕）；走回本地（出生 ~115m > 100）→ `UnloadSceneAsync` + `Resources.UnloadUnusedAssets()` 真的卸載。中間 75→100 有 25m 遲滯緩衝，不抖動。

改 `MapStreamer.cs` + `GreyboxTest`。編譯無錯，**EditMode 288/288 綠**。

### 追加94 續 78（2026-09-03）— 學校改為大門互動式進入（取代靠近自動串流）

使用者：「在學校面前設計大門，只有在跟大門互動後，進入加載畫面，跑完後會看到新地圖的場景，玩家直接在該地圖上」。

- **新腳本 `Assets/_Project/Game/World/SceneGate.cs`**：一座門一顆（root collider = isTrigger 判定）。一元件雙向 —— `sceneToLoad` / `sceneToUnload`（都可空）+ `arrivalPosition` / `arrivalYaw`。走進 trigger → 頭上世界空間「按 E」提示（程式建的小 Canvas，billboard）→ 按 E → `RunTransition` coroutine：`ScreenFader` 淡黑 + 「載入中…」→ `LoadSceneAsync(Additive)` → 壓 settleFrames → 關 CC 傳送玩家 + `camera.SnapYawToTarget()` → 壓 2 幀讓相機貼上 → `UnloadSceneAsync` + `Resources.UnloadUnusedAssets()`（在傳送之後）→ 淡回。`s_transitionRunning` static 擋雙門/連按重入。黑幕期間 `PlayerInputProvider` 已歸零輸入。
- **`ScreenFader`**：加 `SetLabel(string)` / `ClearLabel()`（置中白字，跟黑幕同 CanvasGroup 一起淡）。
- **`ThirdPersonCameraController`**：加 `SnapYawToTarget()`（傳送後把自由視角 yaw 對到新朝向；相機位置本來就每幀硬算不 damp，會自己到位）。
- **GreyboxTest**：移除 `MapStreamer_School`；新增 `SchoolGate_Enter`（z −82 車道南端，greybox 鳥居兩柱+楣+實心門板，`SchoolWall.mat`；`sceneToLoad=Map_School`、arrival (0,1.1,-92) yaw 180）。
- **Map_School**：新增 `SchoolGate_Exit`（z −86 北牆缺口內；`sceneToUnload=Map_School`、arrival (0,1.1,-78) yaw 0）。
- `MapStreamer.cs` 留在磁碟（未使用，之後空島之類要無縫串流可再用）。

改 `SceneGate.cs`(新) + `ScreenFader.cs` + `ThirdPersonCameraController.cs` + `GreyboxTest` + `Map_School` + `Docs/MAP_STREAMING.md`。編譯無錯，**EditMode 288/288 綠**。
**待使用者對焦 Play 驗證**：走到門口 → 提示出現 → 按 E → 淡黑「載入中…」→ 站在校園裡；校園裡的門 → 按 E → 回車道、學校卸載。（Editor 失焦時 Play 凍結，coroutine 轉場 MCP 測不了。）

### 追加94 續 79（2026-09-03）— 修：進學校後「按 E 進入元培大學」提示殘留在畫面上

`SceneGate` 傳送玩家用「關 CharacterController → 移動 → 開回」，這樣 **不會觸發 `OnTriggerExit`** → `SchoolGate_Enter._playerInside` 一直是 true → 轉場結束 `s_transitionRunning` 一放，`Update` 又把世界空間提示牌打開，飄在新地圖裡。

- `RunTransition` 結尾（淡回之後、`s_transitionRunning=false` 之前）清掉 `_playerInside=false` / `_occupant=null` + `SetPromptVisible(false)`。玩家之後真的走回門的 trigger 會由 `OnTriggerEnter` 重新 arm。

改 `SceneGate.cs`。編譯無錯，**EditMode 288/288 綠**。

### 追加94 續 80（2026-09-03）— 學校大門視覺換成紅色漩渦影片（取代 greybox 鳥居）

使用者提供 `固定鏡頭…畫面中央是一個.mp4`（紅色火焰漩渦傳送門，黑底、1280×720、10s）當真正的門，一樣按 E 互動進場。

- 影片轉檔進版控：`Assets/_Project/VFX/Gate/PortalVortexVideo.mp4`（ffmpeg → H.264 Constrained Baseline / yuv420p / bt709，清掉 VideoPlayer 的 "non-baseline timestamp" + "color primaries unknown" 警告）。3.3MB，正常進 git。
- **新腳本 `PortalVideoSurface.cs`**（`Live2DAction.World`，`[RequireComponent(MeshRenderer)]`）：`Awake` 建 per-instance `RenderTexture`(640×360) + `VideoPlayer`（RenderTexture 模式、loop、playOnAwake、`audioOutputMode=None`）→ 塞進 per-instance 的材質（`Instantiate(materialTemplate)`）的 `_BaseMap`。`OnDestroy` 釋放 RT/材質。
- **新材質 `Assets/_Project/VFX/Gate/GatePortalVideo.mat`**：既有 shader `Live2DAction/VFX/AdditiveUnlit`（`Blend One One`）→ 影片黑底自動變透明、只有火光/火花發亮。
- **兩座門重建**（`SchoolGate_Enter` in GreyboxTest、`SchoolGate_Exit` in Map_School）：移除 greybox 兩柱+楣+門板 cube，改成 `PortalSurface`（Quad 5×3.4 + `PortalVideoSurface`）+ 隱形 `Blocker`（BoxCollider 5×3.4×0.3，實心擋路）。root 的 trigger + `SceneGate` 不變。
- 每座門各自 RT + 材質 instance，不互搶。

改 `PortalVideoSurface.cs`(新) + `GatePortalVideo.mat`(新) + `PortalVortexVideo.mp4`(新) + `GreyboxTest` + `Map_School` + `Docs/MAP_STREAMING.md`。編譯無錯，**EditMode 288/288 綠**。
**待使用者對焦 Play 驗證**：門口應看到紅色漩渦影片循環播放（發光）、按 E 一樣進場。（Editor 失焦時 Play 凍結、VideoPlayer 不會前進，MCP 測不了播放。）

### 追加94 續 81（2026-09-03）— 大門三修：離開失效 ＋ 白光 ＋ 影片變 3D

1. **只能進不能出** —— `SceneGate.RunTransition` coroutine 跑在門物件上，離開門在 `Map_School` 裡，`UnloadSceneAsync(Map_School)` 把門連 coroutine 一起銷毀 → 序列卡在中間、畫面回不來。
   - 新腳本 **`SceneTransitionRunner.cs`**（`Live2DAction.World`，單例，掛 GreyboxTest 常駐物件 `SceneTransitionRunner`）：整個載入/傳送/卸載 coroutine 搬到這裡跑，不管卸哪個場景都跑得完。`SceneGate` 只剩 trigger + 「按 E」提示 + 按鍵，按下就呼叫 `SceneTransitionRunner.Instance.Begin(...)`。`s_transitionRunning` static → `SceneTransitionRunner.IsRunning`。

2. **進入後畫面莫名白光** —— `PortalVideoSurface` 建的 `RenderTexture` 沒清，未初始化內容是亂碼（常偏亮）→ additive 混合 = 整片白，直到第一張影片幀進來才蓋掉。
   - `Awake` 建完 RT 後 `GL.Clear(true,true,Color.black)`。實測 RT 亮度歸零。
   - 另外把 additive 疊層調暗（見下）＋主體 `tint` 預設 0.8，避免多層疊加把漩渦亮部沖成白。

3. **影片特效變 3D** —— 原本一片平面 quad，側看就穿幫。`PortalVideoSurface` 現在：
   - **billboard**：每幀轉向攝影機（`LateUpdate` `LookRotation`）。
   - **景深疊層**：Awake 在後方（local +Z）生 `depthLayers`(2) 片同影片的 quad、逐層縮小（0.82/0.64）＋逐層變暗（`_BaseColor × 0.6^n`），讀起來像往門內凹的漩渦隧道。
   - **脈動**：`pulseAmount` 0.04 / `pulseSpeed` 0.5Hz 輕微縮放。
   - 兩座門的 `PortalSurface` 重建套用新預設。每片自己的材質 instance，`OnDestroy` 全部釋放。

改 `SceneTransitionRunner.cs`(新) + `SceneGate.cs` + `PortalVideoSurface.cs` + `GreyboxTest`（+`SceneTransitionRunner` GO、重建 gate surface）+ `Map_School`。編譯無錯，**EditMode 288/288 綠**。
**待使用者對焦 Play 驗證**：進校園沒白光、門是有景深的旋轉漩渦、校園裡的門按 E 能正常回車道＋卸載。

### 追加94 續 82（2026-09-03）— 大門傳送門加大、加深，確認校內離開門同款

- **加大**：portal Quad 5×3.4 → **9×5**（比 7.42 車道寬一點、上下拉長），中心 y1.9→2.7（底邊貼近地面）。`Blocker` 9×5×0.3、root trigger 11×6.5×5 跟著放大。
- **加深**：`PortalVideoSurface` 景深疊層 `depthLayers` 2→**5**、`depthLayerSpacing` 0.55→**1.2**、`depthLayerShrink` 0.18→0.12、逐層變暗 `0.6^n`→`0.68^n`。疊層 z 1.2→6.0、scale 0.88→0.40 —— 往門內凹約 6 單位深的漩渦隧道。
- **校內離開門**：`SchoolGate_Exit`（Map_School，z −86，`sceneToUnload=Map_School`、arrival 車道 (0,1.1,-78)、「按 E 離開元培大學」）用同一支 `rebuild` 重建，portal 尺寸/景深跟入口門完全一致。在北牆缺口內側、玩家轉身即見。

改 `PortalVideoSurface.cs` + `GreyboxTest` + `Map_School`（兩座門 surface 重建）。編譯無錯，**EditMode 288/288 綠**。

### 追加94 續 83（2026-09-03）— 大門修：灰框/白立體框移除、擴大、貼齊地板

實測（使用者截圖）：門口前一個小灰色框、門口後一疊大白色立體框。根因 —— 限制範圍（limited-range）H.264 解碼後「黑底」其實是 ~0.06-0.10 灰，`AdditiveUnlit` 直接加上去 → 整片 quad 邊界（含 5 層景深疊層）變成看得見的灰/白矩形。

- **新 shader `Assets/_Project/Rendering/Shaders/PortalVideoURP.shader`**：additive（`Blend One One`）＋ `_Cutoff`（0.14）—— `saturate((c - cutoff) / (1-cutoff))`，把黑底基座壓成全透明，矩形邊界消失。`GatePortalVideo.mat` 換用它。
- **影片再轉檔**：`-color_range pc` + `curves` 把 0~0.10 壓到 0 + 微增飽和。雙保險。
- **`PortalVideoSurface`**：改用 `PortalVideoURP`（設 `_Cutoff` = `blackCutoff` 0.14）；`depthLayers` 5→**3** 且大幅變暗（`0.45^n`）；billboard 改 **yaw-only**（保持直立，底邊不會歪）；prompt 拿掉半透明底板（那就是「小灰色框」），只剩粗體白字＋黑描邊。
- **擴大＋貼齊**：portal quad 9×5 → **12×9**，中心算成 **底邊正好落在車道地面 y0.51**。`Blocker` 縮成 8×4（只擋走道，不用跟視覺一樣大）、trigger 12×4.5×5。
- 兩座門（入口／校內離開）同款重建。

改 `PortalVideoURP.shader`(新) + `PortalVideoSurface.cs` + `SceneGate.cs`（prompt）+ `GatePortalVideo.mat` + `PortalVortexVideo.mp4` + `GreyboxTest` + `Map_School`。編譯無錯，**EditMode 288/288 綠**。
**待使用者對焦 Play 驗證**：門是乾淨的旋轉漩渦（沒有灰/白矩形邊框）、比車道寬、底邊貼著路面、字沒有底框。若還看到疊層矩形就把 `depthLayers` 設 0。

### 追加94 續 84（2026-09-03）— 回退續 83（`_Cutoff` 讓門整個消失）

續 83 的 `PortalVideoURP` shader `_Cutoff` 0.14 太狠 —— 把整支影片壓沒了，門看不見。使用者要求回到續 82。

- 刪 `PortalVideoURP.shader`；`GatePortalVideo.mat` 換回 `Live2DAction/VFX/AdditiveUnlit`。
- `PortalVideoSurface.cs` 回到續 82 版（AdditiveUnlit、`depthLayers` 5、full billboard、無 `_Cutoff`）。
- `PortalVortexVideo.mp4` 重轉檔回續 81 狀態（baseline + bt709，**不做壓黑 curves**）。
- `SceneGate` 提示牌半透明底板還原。
- 兩座門重建回 9×5 / 中心 y2.7。
- **保留**續 81 的修正（`SceneTransitionRunner` 讓校內離開門能用、RT `GL.Clear` 黑防白閃、按 E 清 `_playerInside` 防提示殘留）—— 那些不是造成消失的原因。

改 `PortalVideoSurface.cs` + `SceneGate.cs` + `GatePortalVideo.mat` + `PortalVortexVideo.mp4` + 刪 shader + `GreyboxTest` + `Map_School`。編譯無錯，**EditMode 288/288 綠**。

**現況（＝續 82）**：門是可見的旋轉紅漩渦，9×5，5 層景深。已知：限制範圍解碼的灰黑底基座還在（矩形邊界會有點灰白）—— 之後用更保守的方法（例如 `smoothstep` 而非硬 cutoff，或只留主體不疊層）再處理。

### 追加94 續 85（2026-09-03）— 大門：拿掉提示文字框、修地圖外的門不顯示

1. **靠近時的矩形（提示文字 UI）移除**：`SceneGate` 整個拿掉 `GatePrompt`（世界空間 Canvas + 半透明底板 + 「按 E」文字）—— 使用者不要那個框。門的漩渦本身就是互動提示，按 E 邏輯不變。
2. **地圖外（車道）的門不顯示**：`SchoolGate_Enter` 在 `GreyboxTest`、遊戲一開始就 `Awake`；runtime `AddComponent` 的 `VideoPlayer` 在 scene-0 載入時 `playOnAwake` 早於 clip/target 設定、之後的 `Play()` 又太早 → 影片沒真的開始播（additive 黑 RT = 看不見）。校內的 `SchoolGate_Exit` 在遊戲中途載入所以沒中。
   - `PortalVideoSurface`：`Awake` 加 `Prepare()`；新 `Update` 自我修復 `if (isPrepared && !isPlaying) Play()`。
3. `depthLayers` 預設 5 → **0**（疊層疊在限制範圍灰底上會變成矩形；先只留主體，把基礎弄穩，景深之後用別的方法）。
4. 兩座門重建成一致（9×5、depthLayers 0、無 prompt）。

改 `SceneGate.cs` + `PortalVideoSurface.cs` + `GreyboxTest` + `Map_School`。編譯無錯，**EditMode 288/288 綠**。
**待使用者對焦 Play 驗證**：車道盡頭跟校內都看得到旋轉紅漩渦、沒有文字框、走進去按 E 能進出。

### 追加94 續 86（2026-09-03）— 元培校徽 3D 標誌放進學校上空

使用者 `元培logo.zip`（Meshy AI，元培醫事科技大學圓形校徽 3D 立體版，~29 萬頂點扁平圓盤）。

- 匯入 `Assets/_Project/Environment/Meshy/YuanpeiLogo/`（FBX 29 MB + 5 張 PNG，進版控）。手建 URP/Lit `YuanpeiLogo.mat`（base + normal + metallic_roughness，metallic 0.6 / smoothness 0.55）。normal map import type 已設。
- 放進 **`Map_School.unity`**：`yuanpei_LogoSky` @ `(0, 42, -132)`（校園上空、綠玻璃圖書館正後上方）、`euler(255,180,0)`（面朝入口、略低頭）、scale **1700**（世界 ~32×32m 巨大空中地標）。無 collider、shadowCasting Off、static。非 Y-up 故 X=270 系。
- 玩家從大門進校園（arrival `(0,1.1,-92)` 面朝校園）時，抬頭即見巨大校徽懸在建築群上方（見 scratchpad `logo_d.png`）。隨學校場景串流載入/卸載。

⚠️ **`yuanpei_LogoSky` 標記 DoNotShip**：校徽是元培醫事科技大學的**真實註冊商標**，違反 CLAUDE.md 不可協商規則 1（不得複製商標）。已登記 `ASSET_LICENSES.md`。整個「元培」校園命名同屬此風險 —— 發布前必須換原創校徽／改名（見 KNOWN_ISSUES）。

改 Map_School（+`yuanpei_LogoSky`）+ 新資產 + `ASSET_LICENSES.md` / KNOWN_ISSUES。編譯無錯，**EditMode 288/288 綠**。

### 追加94 續 87（2026-09-03）— 路口傳送門又消失：改用 VideoPlayer MaterialOverride（拿掉 RenderTexture）

`SchoolGate_Enter`（GreyboxTest，開場載入）的漩渦一直不顯示，`SchoolGate_Exit`（Map_School，中途載入）正常。強烈懷疑是 `PortalVideoSurface.Awake` 在 scene-0 載入時建 `RenderTexture` + `GL.Clear` —— render context 還沒好、`Awake` 中斷 / 影片沒真的起播 → additive 黑 quad = 隱形。

- `PortalVideoSurface` 大改：**丟掉整條 RenderTexture 路線**，改 `VideoPlayer.renderMode = MaterialOverride`（`targetMaterialRenderer` + `targetMaterialProperty="_BaseMap"`）—— 每幀直接把影片寫進 per-instance 材質的 `_BaseMap`，不需要 RT、不需要 `GL.Clear`、不吃 render-context 時機。
- `_BaseMap` 初始給 `Texture2D.blackTexture`（首幀進來前 additive 黑 = 隱形，不閃）。
- `Update` 自我修復簡化成 `if (!_player.isPlaying) _player.Play()` 每幀 nudge；首次真的在播會印一行 `[PortalVideoSurface] '...' video playing`。
- 移除 `depthLayers` / `renderSize` 等欄位（本來就設 0）。
- 兩座門重建成一致（9×5、MaterialOverride、無提示）。

改 `PortalVideoSurface.cs` + `GreyboxTest` + `Map_School`。編譯無錯，**EditMode 288/288 綠**。
**待使用者對焦 Play 驗證**：車道盡頭的漩渦門要顯示（Console 應印兩行 `video playing` —— 入口 + 校內門）。Editor 失焦時 Play 凍結，我這邊看不到影片播放。

### 追加94 續 88（2026-09-03）— 本地西側新增通道 → 二次元城市（60×60，串流場景）

比照南邊往學校的做法，本地西牆開洞 + 往西車道 + 大門傳送門 → 新的串流城市「二次元」。

**`GreyboxTest`**：
- `BoundaryWall_West` 開洞（比照 `BoundaryWall_South`）：拆兩顆 collider、關 MeshRenderer + `BoundaryBlockEffect`、關 `RippleEmitter`、加兩段 `WallSegment_L/R`（collider + 可見 cube，`BoundaryWallDebugVisible` 材質，沿 Z、中央留口）。
- `VehicleRoad_West`：cube 於 `(-50, 0.41, 0)`、`(70, 0.2, 7.42)` → x −15 ~ −85，`RoadSurface` 材質，Default layer。
- `NijigenGate_Enter` @ `(-82, 0, 0)`：`SceneGate`（`sceneToLoad=Map_Nijigen`、arrival `(-92, 1.1, 0)` yaw 270 面朝城內）+ `PortalSurface`（漩渦影片 quad 9×5）+ `Blocker`。跟 `SchoolGate_Enter` 同構。

**新場景 `Assets/_Project/Scenes/Map_Nijigen.unity`**（加入 Build Settings）：
- `二次元` ground：cube `(-115, 0, 0)`、`(60, 1, 60)`、`Ground_StoneFloor`（頂面 y0.5）。
- 5 道隱形周界牆（collider-only，比照 `SchoolWall_*`）：West/North/South 全牆 + `EastTop`/`EastBottom` 夾東側路口（gap z ±4.32，朝本地）。
- `NijigenGate_Exit` @ `(-86, 0, 0)`：`SceneGate`（`sceneToUnload=Map_Nijigen`、arrival `(-78, 1.1, 0)` yaw 90 回車道）+ 同款漩渦門。

流程：本地往西穿牆口 → `VehicleRoad_West` → `NijigenGate_Enter` 按 E → 載入畫面 → 站在二次元城內；城內 `NijigenGate_Exit` 按 E → 回車道、城市卸載。`SceneTransitionRunner` 共用。城市內容（建築/生成點）待填。

改 `GreyboxTest` + 新 `Map_Nijigen.unity` + Build Settings。無 code 變更。編譯無錯，**EditMode 288/288 綠**。
**已知**：西邊車道上空剛好有空島（`Torii_FloatingIsland`）飄著，走過去會在島下方——要的話之後挪空島或西通道。
**待使用者對焦 Play 驗證**：本地往西 → 穿口上車道 → 漩渦門按 E → 進二次元；城內門按 E → 回本地。

### 追加94 續 89（2026-09-03）— yuanpei_LogoSky 空中 Boss（工程文件 v1.0，Phase 1–4 完成）

依使用者提供的 `yuanpei_LogoSky_Boss_工程說明文件.md` 實作空中遠距法術型 Boss。詳見新文件 `Docs/YUANPEI_LOGO_SKY_BOSS.md`。

**12 支新腳本** `Assets/_Project/Game/AI/Boss/Yuanpei/`：`YuanpeiBossConfig`(SO) / `YuanpeiAttackDef`(SO) / `YuanpeiBossVitals`(HP委派Health＋Energy＋Posture權威，`YuanpeiPhaseLogic`純函式) / `YuanpeiScheduler`(純招式選擇) / `YuanpeiBoss`(15狀態FSM＋空中移動＋排程＋Intro降下) / `YuanpeiAttacks`(6招coroutine) / `YuanpeiProjectile` / `YuanpeiHazard` / `YuanpeiExecution`(架勢崩潰→墜落→5s F窗→處決) / `YuanpeiBossHitReceiver` / `YuanpeiPerfectDodge` / `YuanpeiBossHUD` / `YuanpeiEncounter`。

**資料**：`Assets/_Project/Settings/Combat/Yuanpei/YuanpeiBossConfig.asset`（HP 1200、arena (0,0.5,-114) r11）＋ `YuanpeiAttack_*.asset` ×6（光粒子三連射／聚焦雷射／雷擊標記／多重延遲光爆／近身震退／肉身衝撞，能量/冷卻/射程/傷害/時間軸全在 SO）。

**場景（`Map_School.unity`）**：`yuanpei_LogoSky` 重構為 boss —— root scale 1 + `VisualRoot` 子（校徽 mesh scale 1700，Intro 縮到 ×0.28）+ 5 anchor + `CollisionRoot`（BodyCollider / CoreWeakPoint trigger + HitReceiver）。root 掛全套 boss 元件 + `Health`(defer) + `LockOnTarget`(距離×2.4)。新 `YuanpeiEncounter` trigger 在 plaza (0,2,-105)。新 layer `ChargeCrashSurface`（slot 9），3 顆 `yuanpei_*_Collision` 已標記。

**測試**：`YuanpeiBossLogicTests` 15 個（階段門檻 + 排程過濾 8 種 skip 條件）。**EditMode 303/303 綠**（原 288 + 15）。編譯無錯。

**已完成 Phase 1–4**：三條數值 + 狀態優先 + 死亡鎖 + HUD；懸浮/面向/距離/視線/Phase/候選過濾/Attack Lock/冷卻/間隔；6 招原型（幾何+純色，各有 Telegraph/Active/Recovery/Cancel）；架勢滿→墜落→F 處決→重升空/死亡；能量耗盡≠可處決；HP 歸零優先。

**未完成（後續）**：正式 VFX/音效/鏡頭（Phase 5）；模型減面+LOD（§3.1，需 Blender）；Object Pool；依玩家閃避資料的平衡校準（Phase 6）；Boss 戰強制停用玩家防禦的 Rule Set（§8.1）。

**待使用者對焦 Play 驗證**：學校 plaza 中央觸發開戰 → 打到架勢滿 → 按 F 處決 → 重複到 HP 歸零勝利。

### 追加94 續 90（2026-09-03）— 傳送門改程序化 shader（放棄影片）＋ 空中 Boss 兩個修正

**1. 入口傳送門又消失** —— 影片方案（VideoPlayer→RT / MaterialOverride）試了 ~9 次，入口門（scene-0 載入）就是不 render。**整個放棄影片**：`PortalVideoSurface` 改成套用既有的程序化漩渦 shader `Live2DAction/PortalVortexURP`（空島傳送門用的那支，fragment 自己動、無 VideoPlayer / 無 RenderTexture / 無時機問題），紅橘火焰配色。新 `GatePortalVortex.mat`，4 座門（Schoolx2 + Nijigenx2）全部重指向。**截圖驗證會 render 了**。mp4 檔留在 `VFX/Gate/` 給之後正式 VFX。

**2. 進場後 Boss 升起但沒動靜** —— `YuanpeiBoss.BeginEncounter` 用 `FindFirstObjectByType<PlayerInputProvider>()` 抓「玩家」→ 抓到**貓**（Player 和 Cat 都有 PIP）→ 距離判定永遠不符 → 不出招。修正：
- `YuanpeiEncounter.OnTriggerEnter` 只認 root 名為 `Player` 的角色，把它傳給 `BeginEncounter`。
- `ResolvePlayer()` 備援：優先名為 "Player"、否則有 `PlayerCombat` 且非貓、否則第一個。
- 螢幕外判定放寬（viewport -0.4~1.4，抓不到相機時當「可見」不卡戰鬥）。
- LOS 改 `RaycastAll` 忽略玩家自己＋Boss 自己（原本單 Raycast 容易被地面/自身擋掉）。
- Stuck watchdog：Hover/Reposition 超過 4s 沒出招 → 忽略軟性 gate，硬選一個範圍內負擔得起的招。

改 `PortalVideoSurface.cs` + `YuanpeiBoss.cs` + `YuanpeiEncounter.cs` + 新 `GatePortalVortex.mat` + 4 場景重指向。編譯無錯，**EditMode 303/303 綠**。
**待使用者對焦 Play 驗證**：車道盡頭看得到紅漩渦門；進學校 plaza 開戰後 Boss 會開始丟招。

### 追加94 續 91（2026-09-03）— 傳送門改回 mp4：場景序列化 VideoPlayer（不再 runtime AddComponent）

使用者要求用 `固定鏡頭，純特效展示…mp4`（紅色漩渦傳送門）重做**入口＋出口**傳送門，取代續 90 的程序化 shader。

**根因（為什麼之前入口門播不出來）**：`PortalVideoSurface.Awake()` 在執行階段 `gameObject.AddComponent<VideoPlayer>()` —— `playOnAwake` 在 `AddComponent` 呼叫當下就 latch，那時 `clip` / `targetTexture` 都還沒指定；入口門在 scene-0 載入時建立，沒有第二次機會。出口門（遊戲中 additive 載入）剛好能用，入口門永遠不行。

**修正**：VideoPlayer 改成**場景序列化元件** —— 編輯期就 `AddComponent` 並把 `clip` + `RenderTexture` + `playOnAwake=1` + `loop=1` + `audioOutputMode=None` 寫進場景 YAML。Unity 正常反序列化路徑會正確 honor `playOnAwake`。`PortalVideoSurface.cs` 不再建立 VideoPlayer / material，只做 billboard(關)＋脈動＋`Update()` 裡 `Play()` nudge 保險。

**新資產**（`Assets/_Project/VFX/Gate/`）：
- `PortalVideoURP.shader`（`Live2DAction/PortalVideoURP`）—— 取樣影片 RT，`smoothstep(_KeyLow,_KeyHigh,luma)` 把近黑背景 key 掉，`Blend One One` 疊加發光。近黑底貢獻 0 → **沒有灰白矩形基座**（續 82 的老問題靠新的全範圍轉檔 + 這支 shader 一起解掉：轉檔後角落 avgLuma 實測 0.002）。`_EdgeFade` 柔化 quad 邊。
- 每座門一張 `RT_<gate>.renderTexture`（640×360）+ `Mat_<gate>.mat`（`_Intensity` 2.0、`_KeyLow` 0.02、`_KeyHigh` 0.12、`_EdgeFade` 0.05）。
- `PortalVortexVideo.mp4` 重轉檔：640×360、H.264 baseline、bt709、**全範圍（無壓黑）**、1.3 MB。

4 座門（`SchoolGate_Enter`/`NijigenGate_Enter` 在 GreyboxTest；`SchoolGate_Exit` 在 Map_School；`NijigenGate_Exit` 在 Map_Nijigen）的 `PortalSurface` quad 加大到 13×9 @ local y4.1（比車道 ~7.4 寬、底邊約貼車道地面），`Blocker` 對應加大到 12×8。

編譯無錯，**EditMode 303/303 綠**。編輯期截圖確認：影片 → RT → keyed 疊加 shader，車道盡頭是發光漩渦傳送門、背景全透明無矩形框。殘留 warning：`Color primaries 0 … WindowsMediaFoundation`（ffmpeg 沒寫 colr atom，紅色調可能極微偏移，對這個造型無感）。
**待使用者對焦 Play 驗證**：入口門在 Play（scene-0）確實會自動播放（場景序列化 + `Update()` nudge 雙保險）；出口門一樣。

### 追加94 續 92（2026-09-03）— 學校入口門 Play 還是消失：VideoPlayer 改 APIOnly + coroutine prepare/play

使用者 Play 測試：**出口門正常、入口門消失**（＝ scene-0 的 VideoPlayer 還是沒開始播 → `RT_SchoolGate_Enter` 空白 → keyed shader 全透明）。續 91 的場景序列化 VideoPlayer + `playOnAwake` + `Update()` nudge 仍不夠。

`PortalVideoSurface.cs` 重寫：
- `VideoRenderMode.APIOnly` —— VideoPlayer 自己持有解碼貼圖（`vp.texture`），**完全不用 RenderTexture asset**（沒有配置/清空的時序 race）。每幀把 `vp.texture` 塞進 per-instance 材質的 `_BaseMap`。
- `playOnAwake` 強制關；`OnEnable` 起一個 coroutine 做 `Prepare()` → 等 `isPrepared`（最多 8s，>2s 每秒補一次 Prepare）→ `Play()`，之後每 0.4s realtime 檢查掉出 `isPlaying` 就補。**只在「未 prepared」時才重發 Prepare**（不再每幀 spam）。
- `Debug.Log` 麵包屑（前綴 `[PortalVideoSurface]`）：prepared 花幾秒、`Play()` 後 isPlaying、首幀貼圖尺寸 —— Play 測試可對 Console 確認入口門走到哪一步。
- 初始 `_BaseMap = Texture2D.blackTexture`（keyed shader → 透明），影片首幀到位前不閃。

`RT_<gate>.renderTexture` 4 張現在沒用到（腳本 runtime 蓋成 APIOnly）；場景裡 VideoPlayer 的 `targetTexture`/`renderMode` 序列化值無害（Awake 覆蓋）。之後清。

編譯無錯，**EditMode 303/303 綠**。
**待使用者對焦 Play 驗證**：走到學校入口門看漩渦有沒有出現；沒有的話把 Console 的 `[PortalVideoSurface] SchoolGate_Enter …` 幾行貼回來。

### 追加94 續 93（2026-09-03）— APIOnly 掉紅色通道變青色框 → 回 RenderTexture + 保留 coroutine

使用者 Play 測試續 92：入口門 VideoPlayer **有播了**（Console：`prepared=True`、`Play() -> isPlaying=True`、`first frame texture 640x360`）—— coroutine 修法成功。但 **APIOnly 的 `vp.texture` 在這台 D3D11 讀回來掉了紅色通道** → 紅色漩渦 −紅 = **整片青色矩形疊在畫面上**（出口門也一起壞，因為腳本 runtime 強制 APIOnly）。

`PortalVideoSurface.cs` 續 93：
- **renderMode 回 `RenderTexture`**（Unity 自己做 YUV→RGB blit 進 per-gate RT，出口門在續 91 已證實正常）。
- **保留續 92 的 coroutine**：`OnEnable` → 等 2 幀 → `Prepare()` → 等 `isPrepared`（≤8s）→ `Play()` → 每 0.5s realtime 補。這是讓 scene-0 入口門真的開始播的關鍵（續 92 log 已證實）。
- 不再 runtime 建 material / 每幀塞 texture（那是 APIOnly 才需要）。material 直接用 per-gate `Mat_<gate>`（`_BaseMap` = per-gate RT，續 91 就接好）。
- log 前綴帶父物件門名（`SchoolGate_Enter` / `NijigenGate_Enter` …）方便分辨。

場景 YAML 未動（續 91 的 VideoPlayer 序列化值 `renderMode: 2` + `targetTexture` 都還在，4 座門都確認過）。編譯無錯，**EditMode 303/303 綠**。**使用者 Play 確認 4 座門都正常**（紅漩渦、無青色框）。

### 追加94 續 94（2026-09-03）— 傳送門改成「玩家靠近才現身」

使用者：傳送門是「憑空出現」的動畫，希望玩家靠近入口時才播放。

`PortalVideoSurface.cs` 加 proximity gating（`proximityActivated` 預設 on，4 座門共用）：
- 載入時 `Prepare()` 好但**不播**，`MeshRenderer` 關（漩渦不在）。
- 每幀量最近玩家（`FindObjectsByType<PlayerInputProvider>` 的 root，每 1s 重掃一次 —— Player 和 Cat 都有 PIP，取最近的；水平距離）：
  - 進 `activateRange`(32m) → 開 renderer + `vp.frame = 0` + `Play()` + `_Intensity` 用 MaterialPropertyBlock 在 `appearFadeSeconds`(0.45s) 內 smoothstep 淡入 → 「現身」。
  - 出 `deactivateRange`(40m，含 hysteresis) → `Pause()` + `GL.Clear` RT 黑 + renderer 關。傳送穿門瞬間拉遠也會觸發。
- 續 93 的 coroutine（`Prepare→isPrepared→Play` + 掉出 `isPlaying` 補）保留，只在 `_active` 時補。
- pulse 只在 renderer 開時跑。
- `proximityActivated` 可 per-gate 關掉 → 回續 93 的常駐播放。

編譯無錯，EditMode 303/303。
**待使用者對焦 Play 驗證**：走學校南入口路，漩渦門在 ~32m 處淡入現身；走遠/穿門後消失。

### 追加94 續 95（2026-09-03）— 第三座城市「現世」：本地東側橋接 + 幽冥星環傳送門模型

使用者：以 `幽冥星環傳送門.zip`（Meshy AI「Voidmoon Gate」）當第三座城市「現世」的橋接傳送門，方位東邊。

**模型匯入** `Assets/_Project/Environment/Meshy/VoidmoonGate/`：
- FBX 改名 `VoidmoonGate.fbx`（28 MB，避開 `.gitignore` 的 `Meshy_AI_*_texture.fbx` → **進版控**）。`useFileScale=false`（原 `fileScale 0.01`）→ ~1.92m 寬、Y=0.25 薄；擺放時繞 X −90° 立起。305,771 tris。
- 手建 URP/Lit `VoidmoonGate.mat`（albedo/normal/metallicRoughness ＋ 微弱藍 emission 0.18）。normal→NormalMap、MR/metallic/roughness→linear。addCollider off、isReadable off、材質 import None。
- 授權：Meshy 付費方案，使用者持商用權，可進 Build（見 ASSET_LICENSES）。**非 DoNotShip**。

**GreyboxTest 東側**（鏡射西側二次元）：
- `BoundaryWall_East` 重建自 `BoundaryWall_West`（開口版：BoundaryBlockEffect/MeshRenderer 關、兩段 WallSegment），移到 x=+15.5。
- `VehicleRoad_East` @ (50, 0.41, 0) scale (70, 0.2, 7.42)。
- `XianshiGate_Enter` @ (82, 0, 0) rot **Y=90**（讓門面朝東路 —— 順手也把 `NijigenGate_Enter` 轉 Y=90，它原本面朝 +Z ＝ 對西路是側面看不見的 bug）。`SceneGate` sceneToLoad=`Map_Xianshi`、arrival (92, 1.1, 0) yaw 90。自己的 `RT_XianshiGate_Enter` + `Mat_XianshiGate_Enter`。`PortalSurface` 縮到 7.5×8.5 @ y3.9 塞進門框中央、`Blocker` 7×8。
- `VoidmoonGateFrame` 子物件：`VoidmoonGate.fbx` prefab、localScale 6.8、繞 X −90°、localPos y3.7 → 世界 ~13m 寬 × 10m 高 立在路口框住漩渦。

**新場景 `Map_Xianshi.unity`**（`AssetDatabase.CopyAsset` 自 `Map_Nijigen` → 全 root 鏡射 X、改名 Nijigen→Xianshi / 二次元→現世 / East↔West token 對調）：
- `現世` 地板 cube @ (115, 0, 0) scale (60,1,60)、5 面隱形牆 `XianshiWall_*`。
- `XianshiGate_Exit` @ (86, 0, 0) rot Y=90、`sceneToUnload=Map_Xianshi`、arrival (78, 1.1, 0) yaw 270。自己的 `RT_XianshiGate_Exit` + `Mat_XianshiGate_Exit` + `VoidmoonGateFrame`。
- 加入 Build Settings（現在 4 個場景）。城市內容（建築/生成點）待填 —— 目前為空地。

編譯無錯，**EditMode 303/303 綠**。編輯期截圖確認模型立在東路盡頭、朝向正確、貼地。
**待使用者對焦 Play 驗證**：本地往東穿牆口 → `VehicleRoad_East` → 幽冥星環門在 ~32m 淡入 → 按 E → 載入「現世」空地；門框中間漩渦影片的大小/位置可能要再調（`XianshiGate_Enter/PortalSurface` 的 scale/localPos）。二次元門轉向後要一起確認。

### 追加94 續 96（2026-09-03）— 現世門改用「幽冥星環傳送門.mp4」整支影片（仿 3D）

使用者：移除現世門原本的特效（紅漩渦 + FBX 模型），改用 `幽冥星環傳送門.mp4`（整支就是幽冥星環門本身的渲染動畫：靜態門框 + 紫色漩渦，純黑底，1672×940/24fps/5s），附在門上、仿 3D。

- `VoidmoonGateVideo.mp4`：ffmpeg `crop=1220:940:226:0`（去左右黑邊）→ `scale=912` → H.264 baseline / 912×702 / 5s / ~1 MB。放 `Assets/_Project/VFX/Gate/`。
- 新 shader `Live2DAction/PortalVideoAlphaURP`：**alpha blend**（不是 `PortalVideoURP` 的 additive）—— `smoothstep(_KeyLow 0.015, _KeyHigh 0.06, luma)` 只 key 掉純黑外框，深紫門框與漩渦全不透明顯示（additive 會把暗門框洗掉）。
- 兩支 portal shader 都加 `_PortalFade`（0..1）；`PortalVideoSurface` 的靠近淡入改驅動 `_PortalFade`（原本驅動 `_Intensity`，對 alpha shader 無效），兩支通用。
- `PortalVideoSurface` 加回 `billboard`（yaw-only，保持直立、Slerp 平滑轉向面對相機）—— 仿 3D。
- `XianshiGate_Enter`（GreyboxTest）+ `XianshiGate_Exit`（Map_Xianshi）：刪掉 `VoidmoonGateFrame`（FBX 模型，asset 留磁碟）；`PortalSurface` → clip `VoidmoonGateVideo`、材質改 `PortalVideoAlphaURP`、quad 13×10（1.30 aspect）@ y5、`billboard=on`。學校/二次元門不動（照舊紅漩渦 additive）。

兩支 shader 編譯 supported、`VoidmoonGateVideo` 匯入（912×702、120 幀）、`PortalVideoSurface` 有 `billboard`。**EditMode 303/303 綠**。用擷取的單幀 PNG 在編輯期預覽確認：整座門的影片黑底 key 乾淨、貼路面、面對相機。

### 追加94 續 97（2026-09-03）— 現世門：關 billboard（固定朝向）＋ FBX 模型 ＋ 影片兩者重疊

使用者：門要固定不要跟玩家轉；把 FBX 模型加回來，模型跟影片**兩者重疊**。

- `PortalVideoSurface.billboard` = **false**（兩座現世門）—— quad 回 local identity，跟著門根的 Y=90 面朝東路，不再轉向玩家。
- `VoidmoonGateFrame`（FBX，`VoidmoonGate.mat`）加回兩座現世門，localScale **7.1**（~13.6w × 10.5h，對齊 quad）、繞 X −90°、localPos y4.6。
- 影片 quad（`PortalSurface`）推到 local **z −1.15**（模型 ~1.8 深、貼在模型正面前）→ alpha keyed，純黑處露出後面的 3D 模型、影片的門框＋漩渦疊在模型正面。fixed 朝向。
- Blocker localPos y4。學校/二次元門不動。

編輯期單幀預覽確認：3D 模型 ＋ 影片漩渦兩層都看得到、對齊、貼地、朝向固定。**EditMode 303/303 綠**。

### 追加94 續 98（2026-09-03）— 影片完全貼合模型：量測模型 bounds 對尺寸、貼正面、關脈動

使用者：影片不夠近、要完全附著在模型上、完全貼合模型大小、不要忽大忽小。

- 程式量 `VoidmoonGateFrame` 的世界 bounds（13.60 W × 10.47 H × 1.80 D）→ `PortalSurface` quad scale 設成 **1.02× 模型尺寸**（13.87 × 10.68，微 overscan 讓影片裡的門和 3D 門對齊）。
- quad localPos 對到模型中心（y4.56），z 推到 **模型正面前 0.03m**（−0.93，模型半深 0.90 + epsilon）→ 完全貼在模型正面。
- `PortalVideoSurface.pulseAmount = 0`（兩座現世門）→ 不再忽大忽小。billboard 維持 off。

編輯期預覽：影片的門框/漩渦與 3D 模型的框、頂環、月牙翼精準疊合。
**待使用者對焦 Play 驗證**。

### 追加94 續 99（2026-09-03）— 處決一律「扣當前生命 50%」：拿掉武士的生命節點系統

使用者回報：處決應該固定扣對手當前生命的 50%，但對武士處決完之後他會**直接滿血**。

原因：武士掛了 `BossLifeNodeController`（`IExecutable`，工程規格 §8 項目 7 的兩節點 Deathblow 系統）。`ExecutionAbility` 偵測到 `IExecutable` 就走節點路線 → 第一次處決 `DeathblowPhaseTransition(restoreHealth=true)` → `health.ResetHealth()` **回滿血** + 進 Phase 2；第二次才永久死。完全不走「扣 50% 當前血量」。

修法：**從武士身上移除 `BossLifeNodeController` 元件**（0 個外部參照，乾淨移除；`BossLifeNodeController.cs` 留著、`ExecutionNodeLogic`/測試不動）。現在 `ExecutionAbility.BeginExecution` 找不到 `IExecutable` → `_pendingExecutable` = null → `ResolveExecution` 走一般路線：`health.CurrentHealth × 0.5` 傷害 + `EndStagger()`。武士、屁孩王、Enemy 全部一致。

- `武士` `Wushi_Tuning.permanentDeath=False` / `reviveDelaySeconds=5` **未動** —— 武士血歸零仍會 5 秒後復活（本來非處決致死就是這行為；只有 deathblow 的 final-kill 路線會讓它永久死，那條路線現在沒了）。要永久死再說。
- `ExecutionAbility.instantKillNonExecutableTargets` 維持 False（＝一律 50%，不是秒殺）。
- `SamuraiBossArena.unity`（demo 場景）本來就沒有 `BossLifeNodeController`，不受影響。
- 工程規格 §8 項目 7「處決 + 生命節點」＝ **使用者決定不採用**，回歸統一的 50% 當前血量處決。

編譯無錯，**EditMode 303/303 綠**（`ExecutionNodeLogicTests` / `ExecutionAbilityRoutingTests` 測邏輯類、不受場景移除影響）。

### 追加94 續 100（2026-09-03）— yuanpei_LogoSky boss：進場後不出招（arena 卡在圖書館 collision 裡）

使用者：進學校 → 看到 boss 升天（intro）→ 之後就沒動靜。續 90 的修正不夠。

**根因**：`yuanpei_ModernGlassLibrary_Collision` 是整個 Meshy mesh 的 AABB（50 寬 × 28 深，mesh 含掃描進去的地形/樹），範圍 z[-139.6,-111.3] x[-30,20]。boss arena（0,0.5,-114 半徑 11）**幾乎整個泡在這個看不見的 box 裡**。`YuanpeiBoss.HasLineOfSight`（`losBlockers` = Everything）的射線穿過這個 box + MainBuilding box 時斷斷續續 fail，而 `YuanpeiScheduler.Select` 一遇到 `!hasLineOfSight` 就 `return null` → 出不了招。4s watchdog 也被 range 擋掉時就永遠 hover。

修正：
- `YuanpeiBoss.losBlockers` → **只留 layer 0**（真正的 `SchoolWall_*` 邊界牆；無視 layer 9 那三個粗略過大的建築 AABB）。
- stuck watchdog 4s → **2s**；`ForceAnyInRangeAttack` 加**無視距離的最終保底**（射程外也硬選第一個負擔得起、沒 cooldown 的招）→ boss 絕不會永遠 hover。
- arena 放大：`YuanpeiBossConfig.arenaCenter` + `YuanpeiEncounter.combatCenter` → (0, 0.5, -110)，`arenaRadius` 11 → 14（boss 能追玩家往入口方向）。
- `YuanpeiBoss.verboseLog`（預設 on）：每秒印 `[YuanpeiBoss] state= player= dist= LOS= onScreen= energy= phase= anyInRange=` 供 Play 診斷。
- **未修**：三個 `yuanpei_*_Collision` 是整 mesh AABB，玩家在廣場也會撞到看不見的牆——之後要按各建築實際 footprint 重畫。

改 `YuanpeiBoss.cs` + `YuanpeiBossConfig.asset` + `Map_School.unity`（`YuanpeiEncounter`）。編譯無錯，**EditMode 303/303 綠**。
**待使用者對焦 Play 驗證**：進場 → boss intro → **開始丟招**；還是不動就把 Console 的 `[YuanpeiBoss] …` 幾行貼回來。

### 追加94 續 101（2026-09-03）— yuanpei boss：持續攻擊、Boss HUD 比照玩家 UI、§8.1 禁防禦規則

使用者三項要求：1. boss 持續鎖定＋攻擊 2. 能量/血量/架勢條比照玩家 UI 3. 依工程文件完善機制。

**1. 持續攻擊**：續 100 已處理（`losBlockers` 只留 layer 0、watchdog 2s + 無視距離保底）。boss 在 Hover/Reposition/攻擊各狀態每幀 `FaceTarget` 對準玩家——本來就會持續轉向。

**2. Boss HUD（`YuanpeiBossHUD` 重寫）**：改成跟玩家角落 HUD 同一套視覺語言——
- 每條血條後面加「延遲 ghost 條」（用玩家血條同一支 `HealthBarTweenUtility.ComputeDelayedFill`）
- 主填充改 frame-rate-independent tween（`SmoothApproach`，玩家血條同款）
- HP 填充邊緣有 edge-glow 節點（`ComputeEdgeGlowLocalX`）
- 受擊時整條 panel 抖動（訂閱 `vitals.Health.Damaged`、`ComputeShakeOffset`）
- 配色比照玩家紅/紫/金家族：HP 深紅（最醒目）、能量青（滿→降）、架勢金（0→滿）
- 架勢接近滿格閃爍＋標籤變白（spec §16.5）
- 「能量」「架勢」文字標籤（spec §16「顏色不能是唯一辨識方式」）
- `[ F ] 處決` + 倒數秒數；`PromptVisible` 已 gate 在 ExecutionWindow + HP>0 + 距離內，**能量歸零不會顯示**（spec §16.7）
- 位置維持螢幕上方置中（boss 血條慣例）

**3. §8.1 禁防禦規則集**：`YuanpeiEncounter.StartEncounter` 進戰鬥時 `PlayerGuard.enabled = false`（`OnDisable` 會自己釋放 speed knob），勝利 / encounter 物件銷毀時還原。避免「防禦輸入看似成功卻仍受不明傷害」。

改 `YuanpeiBossHUD.cs` + `YuanpeiEncounter.cs`。編譯無錯，**EditMode 303/303 綠**。
**待使用者對焦 Play 驗證**：進場 boss 持續丟招；Boss HUD 三條的 tween/ghost/抖動/閃爍；防禦鍵在此戰無效。

### 追加94 續 102（2026-09-03）— yuanpei boss：§9.4 安全路線、預警可讀性、完美閃避白光

繼續依工程文件補機制：

- **§9.4 MultiAoE 安全路線保證**：新 `YuanpeiAoePlacement.EnsureSafeRoute`（純函式，EditMode 3 測）—— 在玩家周圍以「一次閃避」半徑取樣一圈逃脫點，若全被警示圈覆蓋，就從「覆蓋最多逃脫點的圈」開始逐一移除，直到有一個可達的安全點（保底 2 圈，不會整個取消攻擊）。`YuanpeiAttacks.MultiAoE` 生成前先過這關。
- **預警可讀性（§22.2「清楚且互不混淆」）**：`YuanpeiHazard` 加**亮色外圈**（比填充盤大 0.35m、emission ×2、隨計時脈動）；填充盤半透明。
- **蓄力視覺（§3.2「縮放脈衝、Emission」）**：`YuanpeiAttacks.Run` 在 Telegraph/Windup 階段對 `VisualRoot` 做縮放脈衝（telegraph ±4%、windup ±9%）。
- **光彈**：加 `TrailRenderer` 拖尾 + emission ×3.5。
- **聚焦雷射**：加原點蓄力光球（隨鎖定進度長大、變色）+ 線加粗加 cap。
- **完美閃避白光（§8.2）**：新 `YuanpeiScreenFlash`（自建螢幕白幕、unscaled 淡出，處決/HUD 之上）；`YuanpeiPerfectDodge` 命中完美閃避時 `Flash(0.5, 0.13)` + 既有 hit-stop。
- **架勢崩潰墜地回饋（§11.3）**：`YuanpeiExecution` 落地時 `LandingImpactFx`（擴張灰塵環 + 8 顆彈跳塵粒，0.55s 自清）+ hit-stop 加強到 0.06/0.18。鏡頭震動待 Cinemachine impulse 設定。
- **BodyCharge 撞牆暈眩（§9.6）已確認接線**：`YuanpeiAttacks.chargeCrashMask` = layer 9（ChargeCrashSurface），3 個 `yuanpei_*_Collision` box 都在該 layer；`losBlockers` 續 100 只留 layer 0 是分開的，撞牆判定不受影響。

改 `YuanpeiAttacks.cs` + `YuanpeiHazard.cs` + `YuanpeiExecution.cs` + `YuanpeiPerfectDodge.cs` + 新 `YuanpeiAoePlacement.cs` / `YuanpeiScreenFlash.cs` + `YuanpeiBossLogicTests.cs`(+3)。編譯無錯，**EditMode 306/306 綠**。

**仍未做**（spec 第五~六階段，需美術/Blender/Play 迭代）：真正的 shader/particle VFX 美術、完整招式音效、完美閃避/處決 Cinemachine 專用鏡頭、模型減面 + LOD、投射物/VFX object pool、依真實閃避資料逐招校時。

### 追加94 續 103（2026-09-03）— yuanpei boss：Play 回報三修（攻擊慾望、HUD 電流層、廣場被牆擋）

**1. 玩家被擋在廣場中心**：`yuanpei_ModernGlassLibrary_Collision` 是整 mesh AABB（x[-30,20] z[-140,-111]），把廣場南半整片橫向擋死。三個 `yuanpei_*_Collision` box 全部縮到各自建築 footprint（ModernGlass → x[-20,2] z[-138,-122]、PalmLined → x[15,29] z[-126,-110]、Main → x[10,28] z[-107,-87]）。grid 掃描確認 arena 內 0 個阻擋格。arena 移到 **(-2, 0.5, -105) r11**（驗證過的可走區）。

**2. 攻擊慾望極低**：
- stuck watchdog 2s → **1s**（scheduler 的軟性 gate 讓遠距 boss 太被動）
- `ForceAnyInRangeAttack` 已有「無視距離保底」（續 100）
- `TrackOnScreen` viewport 判定放寬到 -0.9~1.9（大型空中 boss 出框外仍算「在場」）
- config：`globalAttackInterval` 0.7-1.0 → **0.35-0.6**、`onScreenGraceBeforeAttack` 0.5 → **0.25**、`energyRegenPerSecond` 5 → **7**（撐得住連續施法）
- 招式 recovery 縮短：FocusLaser 1.5→1.0、MultiAoE 1.0→0.7、BodyCharge 1.0→0.7；LightningMark active 2.2→1.8

**3. Boss HUD 沒有玩家的電流/閃電效果**：`YuanpeiBossHUD` 每條加**流動能量層**，用玩家血條同一支材質 `HealthEnergyFlowUI.mat`（shader `Live2DAction/UI/HealthEnergyFlow`）—— per-bar instance、驅動 `_HpRatio`/`_GlowIntensity`/`_FlashIntensity`/`_SpeedBoost`/`_GlowColor`（HP 紅、能量青、架勢金）。HP/能量「低→躁動」、架勢「高→躁動」（餵 1-ratio，同 `StancePoiseBarFx` 手法）。受擊 flash + speed boost。`flowMaterial` 欄位已在 `Map_School` 接上。

改 `YuanpeiBoss.cs` + `YuanpeiBossHUD.cs` + `YuanpeiBossConfig.asset` + 4 個 `YuanpeiAttack_*.asset` + `Map_School.unity`。編譯無錯，**EditMode 306/306 綠**。
**待使用者對焦 Play 驗證**：廣場整片可走、boss 頻繁丟招、Boss HUD 三條有電流流動。

### 追加94 續 104（2026-09-03）— yuanpei boss：Y 軸天花板（飛太高）

使用者：設一條 Y 軸線，boss 不要飛超過，太高了。

**根因**：`SampleFloorY` 用 `groundMask = ~0` 往下打，boss 飛到建築碰撞箱（layer 9、續 103 縮小後仍 y[1,23]）上空時，射線打到箱頂 y≈23 → 誤判「地板」在 23 → hover 目標 = 23 + 3 = **26m**。

修正：
- **`YuanpeiBossConfig.maxWorldY = 8`**（世界 Y 硬上限）。`YuanpeiBoss.ClampWorldY()` 每幀在 state tick 後把 root Y 夾到 ≤ 8（Falling/ExecutionWindow/Executing/Intro 由 `YuanpeiExecution` 驅動、不夾）。
- `SampleFloorY` 回傳值 `Mathf.Min(hit.y, arenaCenter.y + 2)` = 上限 2.5 —— 打到屋頂也不會當地板。
- `groundMask` 在 `YuanpeiBoss` / `YuanpeiAttacks` / `YuanpeiExecution` 改成 `~(1<<9)`（排除建築碰撞層）。
- Intro 落點 `endPos.y` 也夾 `maxWorldY`。

**數值**：廣場地板 ≈ y0.5、`hoverHeight` 3、bob ±0.35 → **正常懸浮高度 ≈ y3.85**；MultiAoE「升高一點」有餘裕；**硬天花板 y8**（可在 `YuanpeiBossConfig.maxWorldY` 調）。編譯無錯，**EditMode 306/306 綠**。

### 追加94 續 105（2026-09-03）— yuanpei boss：地板攻擊提示浮空

使用者：地板攻擊（雷擊標記 / 多重光爆）的提示圈沒貼地，浮在半空離地不遠。

**根因**：`ProjectToGround(player.position)` 從玩家頭上 30m 往下打單一 Raycast，射線先打到**玩家自己的 CharacterController**（layer 0，跟地板同層、非 trigger）≈ y1.5 → 提示圈放在 y1.5 而非地板 y0.52。

修正：
- `YuanpeiAttacks.ProjectToGround` 改 `RaycastAll` + 依距離排序 + 跳過玩家（`PlayerInputProvider`/`CharacterController`）、boss 自己、其他 runtime hazard/projectile，取第一個真正的地面。fallback y = `arenaCenter.y + 0.02`（原本 +0.52 也浮空）。
- `Shockwave`：環形波原點從 `transform.position`（懸浮高度 y3.85）改成 `ProjectToGround(transform.position)`（boss 腳下的地面）。
- `YuanpeiExecution.SampleGround` 同樣改 `RaycastAll` + 跳過玩家/自己 —— 架勢崩潰墜落點不會誤落在玩家頭上。

改 `YuanpeiAttacks.cs` + `YuanpeiExecution.cs`。編譯無錯，**EditMode 306/306 綠**。

### 追加94 續 106（2026-09-03）— yuanpei boss：完整死亡/勝利演出 + 自動退場

使用者：boss 血量 0 → 掉落到地上 → 慢慢化成碎片後消失 → 畫面中心「戰鬥勝利」→ 5 秒後自動退場、玩家返回路口前。

`YuanpeiEncounter.Victory()` 重寫：
1. **墜地 dissolve**（`DeathDissolve` coroutine，跑在 encounter 上 —— `YuanpeiBoss.EnterDeath` 已停掉 boss 自己的 coroutine + 關 collider/lock-on）：
   - boss root 0.7s ease 落到地面（`SampleGroundY` = RaycastAll 跳過玩家/自己/建築層）+ hit-stop
   - 生成 22 塊發光碎片 cube（向外爆散 + 重力 + 隨機自轉，隨 k 縮小）
   - `VisualRoot` 在 `dissolveSeconds`(1.6s) 內 scale→0 + emission 衰減 + 加速自轉 → 消失 → `SetActive(false)`
2. **`YuanpeiVictoryBanner.Show("戰鬥勝利")`**（新，自建螢幕置中大字 + 半透明橫幅、`DontDestroyOnLoad`、unscaled 0.5s 淡入）；Boss HUD 隱藏；送 `OnYuanpeiEncounterWon`
3. `WaitForSecondsRealtime(victoryHoldSeconds 5)`
4. `YuanpeiVictoryBanner.Hide()` + **`SceneTransitionRunner.Instance.Begin("", "Map_School", player, (0,1.1,-78), yaw 0, "", 0.4, 3)`** —— 卸載 Map_School、玩家傳回 GreyboxTest 路口前（跟 `SchoolGate_Exit` 同落點）。Runner 是常駐的，encounter 隨 Map_School 卸掉也不影響。

新 serialized 欄位：`victoryMessage`（戰鬥勝利）、`dissolveSeconds`(1.6)、`victoryHoldSeconds`(5)、`returnUnloadScene`(Map_School)、`returnArrivalPosition`((0,1.1,-78))、`returnArrivalYaw`(0)。

改 `YuanpeiEncounter.cs` + 新 `YuanpeiVictoryBanner.cs`。編譯無錯，**EditMode 306/306 綠**。

### 追加94 續 107（2026-09-03）— yuanpei boss：死亡攝影機演出、架勢調整、攻擊慾望/多樣性

**1. 死亡演出加攝影機動畫 + 回廣場空中震動再碎裂**（`YuanpeiEncounter.DeathDissolve` 重寫）：
- 接管相機：關 `ThirdPersonCameraController`，`DriveDeathCam` 依階段（升空/震動/碎裂）繞行、推近、拉遠 + 碎裂瞬間小震
- boss root 1.2s 升回**廣場中心上空（地面 +13m）**、慢轉
- **震動 1.5s**：Perlin 位置抖動（幅度隨時間加大）+ 加速自轉 + 縮放脈衝 + emission 越來越亮
- **碎裂**：26 塊發光碎片爆散 + `VisualRoot` scale→0 + emission 爆閃再衰減（`dissolveSeconds` 1.6s）
- 演出結束把相機還給 `ThirdPersonCameraController`（勝利橫幅期間是正常跟隨鏡頭），再走續 106 的橫幅→5s→`SceneTransitionRunner` 退場

**2. 架勢**：`postureGainPerDamage` 0.55→**0.9**（玩家可靠地累到滿 → PostureBreak）；`hoverHeight` 3→**2.6**（boss 更好打，BodyCollider r3.6 從 y2.6 底部到 y-1 一定搆得到地面玩家）。架勢本來就從 0 起算、滿了觸發 `OnPostureFull → execution.BeginPostureBreak`（墜落 + F 窗口）—— 之前不明顯是因為打不到 boss、架勢沒累積。

**3. 攻擊慾望太低 + 手段太少**：
- **6 招全部 `requiredPhase = 1`**（工程文件的階段解鎖是為了漸進教學，使用者明確要更多變化）+ 冷卻全部 ×0.7
- `ForceAnyInRangeAttack` watchdog 改成：**隨機挑**候選（不再固定 pool[0] 猛丟）+ 不重複上一招；觸發門檻 1s→**0.6s**
- config：`globalAttackInterval` 0.35-0.6 → **0.2-0.45**、`onScreenGrace` 0.25 → **0.15**、`maxEnergy` 100→**120**、`energyRegen` 7→**9**（撐得住連丟）
- phase 門檻 0.70/0.35 → **0.65/0.30**
- `[YuanpeiBoss]` 診斷 log 加 hp / posture / y

改 `YuanpeiBoss.cs` + `YuanpeiEncounter.cs` + `YuanpeiBossConfig.asset` + 6 個 `YuanpeiAttack_*.asset`。編譯無錯，**EditMode 306/306 綠**。

### 追加94 續 108（2026-09-03）— Boss HUD 三條完全採用玩家血條的分層美術

使用者：boss 三條狀態條要用**跟玩家完全相同**的 UI 設計。

先前（續 101/103/107）只加了 flow shader 材質，用純色 Image。這次照玩家血條（`StancePoiseBarFx` 等）的實際場景結構重建：`YuanpeiBossHUD` 每條改成 `HudRoundedRect`(Sliced) 容器 + **`00_Frame` / `01_Background` / `02_DelayedFill`(ghost) / `03_Fill`(HP)｜`03_Fill_Energy`｜`03_Fill_Stance` / `05_EnergyFlow*`(材質 `HealthEnergyFlowUI`) / `EdgeGlow`(Spark) / 6× `Spark0-5` / `Value` 數字**，跟玩家逐層對應（`Assets/_Project/UI/Textures/HealthBarArt/`）。

- 行為比照三個 `*BarFx`：`HealthBarTweenUtility` 的 tween/delayed-fill/edge-glow/spark-burst/shake，flow 材質驅動 `_HpRatio`/`_GlowIntensity`/`_FlashIntensity`/`_SpeedBoost`/`_GlowColor`
- HP/能量「低→躁動」、架勢「高→躁動」（餵 1-ratio）；受擊 flash + HP 條噴 spark
- 12 個 sprite + `flowMaterial` 已在 `Map_School` 場景接上（HUD 上新增序列化欄位）
- 位置維持螢幕上方置中（boss 血條慣例）、加 boss 名稱 + `[F] 處決`

編輯期驗證 hierarchy 逐層對上玩家血條。編譯無錯，**EditMode 306/306 綠**。

### 追加94 續 109（2026-09-03）— yuanpei boss：白框、架勢太快、攻擊間隔

Play 回報三修：
1. **白色背景框** —— `YuanpeiBossHUD` 每條的 `HudRoundedRect` 容器 Image 之前設白色 0.9。玩家血條其實是把這個容器 Image **停用**（`enabled=false`、留 color 白 0.14），只靠 `01_Background` sprite 當底。跟進：容器 Image `enabled=false`。
2. **架勢太容易滿** —— `postureGainPerDamage` 續 107 從 0.55 拉到 0.9 太多，回 **0.3**。
3. **攻擊間隔太長** —— `globalAttackInterval` 0.2-0.45 → **0.1-0.25**；stuck watchdog 0.6s → **0.3s**；6 招 telegraph + recovery 全部再砍（ProjectileBurst tele/reco 0.35/0.35、LightningMark 0.25/0.3、Shockwave 0.45/0.6、BodyCharge 0.55/0.5、FocusLaser 0.8/0.8、MultiAoE 0.35/0.6）。

改 `YuanpeiBossHUD.cs` + `YuanpeiBoss.cs` + `YuanpeiBossConfig.asset` + 6 個 `YuanpeiAttack_*.asset`。編譯無錯，**EditMode 306/306 綠**。

### 追加94 續 110（2026-09-03）— yuanpei boss：架勢再慢一半、能量池 ×5 + 被動回能

1. **架勢成長慢一倍**：`postureGainPerDamage` 0.3 → **0.15**。
2. **能量條 ×5**：`maxEnergy` 120 → **600**；相關門檻按比例放大（`lowEnergyThreshold` 72、`energyRechargeExitThreshold` 300、`energyAfterExecution` 300、`energyAfterMissedExecution` 240）。
3. **每 5 秒回 10%**：`energyRegenPerSecond` → **12**（= 600 × 10% ÷ 5s）。**並修一個 bug**：`RegenEnergy` 原本只在 `EnergyRecharge` 狀態呼叫，一般 Hover/Reposition 完全不回能（違反 spec §5.2）—— 現在 `TickAirCombat` 每幀 `vitals.RegenEnergy`（攻擊 Active 階段不跑此 tick，符合「Active 不恢復」）。→ 能量現在幾乎不會見底，被迫充能狀態基本不再觸發，boss 持續施壓。

改 `YuanpeiBoss.cs` + `YuanpeiBossConfig.asset`。編譯無錯，**EditMode 306/306 綠**。

### 追加94 續 111（2026-09-03）— yuanpei boss：新增 3 種肉身衝撞

使用者要 3 種新的肉身衝撞手段。`YuanpeiAttackId` 加 `ChargeLine` / `ChargeCrush` / `OrbitDash`，各建 SO + 加進 `attackPool`（現 9 招）。

1. **`ChargeLine`（長距離直線衝）** —— 走既有 `BodyCharge` 邏輯，SO 給更長更快的數字（速度 46、距離 24m、傷害 45）。撞 `ChargeCrashSurface` 一樣暈眩 + 自架勢。
2. **`ChargeCrush`（頭頂垂直下壓，命中＝秒殺）** —— 新 coroutine：地面追蹤陰影標記 → boss 滑到玩家頭頂正上方（`boss.SuspendYClamp` 暫時解除 Y 天花板）→ 鎖定當下 XZ、標記變紅、0.25s dodge window → 垂直高速下砸；命中判定 `DamagePlayer(999999)` 走正常傷害管線＝秒殺；落地小震波 + hit-stop + 0.5s 落地空檔給玩家反擊。
3. **`OrbitDash`（繞圈後突然直衝）** —— 新 coroutine：以 8m 半徑繞玩家轉（方向、轉速、繞多久都隨機）→ 隨機時間點（`dashAt`）鎖定方向、hit-stop 0.03s 當「！」提示 → 直線衝過去、鎖定後不轉向（spec §9.6）；撞牆暈眩、命中傷害 + 擊退。

`YuanpeiScheduler.Matches` 加三者的情境權重（ChargeLine 遠距、ChargeCrush 玩家原地不動、OrbitDash 中距）。`YuanpeiBoss.SuspendYClamp(float)` 新增（讓 ChargeCrush 合法飛高）。

改 `YuanpeiAttackDef.cs` + `YuanpeiAttacks.cs` + `YuanpeiScheduler.cs` + `YuanpeiBoss.cs` + 3 新 `YuanpeiAttack_*.asset` + `Map_School.unity`（attackPool）。編譯無錯，**EditMode 306/306 綠**。

### 追加94 續 112（2026-09-03）— 衝撞前搖/預警、OrbitDash 反應時間、玩家死亡「你菜完了」

1. **所有衝撞攻擊加前搖 + 地面預警範圍**：新 `YuanpeiAttacks.ChargePathTelegraph(origin, dir, length, halfWidth, seconds)` —— 沿衝撞方向在地面畫一條**紅色危險車道**（warn→danger 漸變 + 寬度脈動），前搖期間顯示。
   - `BodyCharge` / `ChargeLine`：先小幅後退 → 顯示車道預警 `max(0.45, windupSeconds)` → 再對玩家最後位置鎖定方向 → 衝。SO windup/telegraph 拉到 0.5-0.6。
   - `OrbitDash`：鎖定方向後**不再立刻衝**，停在繞圈環上顯示車道預警 0.5s → 才衝（使用者：「衝撞前要給玩家反應時間」）。
   - `ChargeCrush` 已有地面追蹤陰影標記（續 111）。
2. **玩家血量 0 → 死亡動畫 → 畫面中心「你菜完了」**：新 `PlayerDeathScreen`（自建螢幕置中大字 + 暗紅 vignette、`DontDestroyOnLoad`、unscaled 0.6s 淡入）。`RespawnController` 加 `showGameOverScreen` / `gameOverMessage`（預設關，只在 Player 的 instance 開）—— 死後 `gameOverScreenDelaySeconds`(0.9s，讓死亡動畫先跑) → `PlayerDeathScreen.Show("你菜完了")` → 到 `respawnDelaySeconds`(5s) 復活時 `Hide()`。`DeathAnimationLink` 的死亡動畫不變。

改 `YuanpeiAttacks.cs` + `RespawnController.cs` + 新 `PlayerDeathScreen.cs` + 2 個 `YuanpeiAttack_*.asset` + `GreyboxTest.unity`（Player RespawnController）。編譯無錯，**EditMode 306/306 綠**。

### 追加94 續 113（2026-09-03）— 玩家死於 boss 戰後退場、衝撞時圓盤立直加大命中面

1. **玩家死在 boss 戰 → 「你菜完了」→ 退出 boss 地圖**：`YuanpeiEncounter.Update` 監看玩家 `Health.IsDead`（`boss.Player` 上）。玩家死 → `Defeat()` coroutine：等 5.6s realtime（讓 `RespawnController` 跑完死亡動畫 + 「你菜完了」 + 復活）→ `PlayerDeathScreen.Hide` → `boss.ResetForRematch()`（新 —— StopAllCoroutines、State→Inactive、vitals 全滿重置、回天空起點、collider/lock-on 重開）→ `Started=false` → `SceneTransitionRunner.Begin("", "Map_School", player, (0,1.1,-78), 0…)` 卸載 Map_School、玩家回路口前。再走進去＝全新開打。
   - `YuanpeiBoss.Update`：玩家 GameObject 變 inactive（`RespawnController` SetActive(false)）時，boss 不再追打，只 `HoldHover`、取消進行中的招式/hazard。
2. **衝撞時把圓盤立直**（使用者：「一定要先把表面立直 這樣才有更多面積打中玩家」）：
   - 新 `FaceDiscAlong(dir)` —— logo 圓盤的 face normal = VisualRoot local Y（mesh 薄軸），`LookRotation(_, dir)` 讓圓盤正面朝衝撞方向。
   - `BodyCharge`/`ChargeLine`：鎖定後 + 衝刺每幀 `FaceDiscAlong`；`ChargeCrush`：下砸時 `FaceDiscAlong(down)`（臉朝下壓）；`OrbitDash`：衝前 + 衝刺 `FaceDiscAlong`。
   - 命中判定改 `DiscFaceHitsPlayer`：玩家在衝撞軸前方 + **垂直軸距離 < hitR × 2.8**（寬扁的圓盤正面，不是細管）。
   - `Run` 的 Recovery 階段把 VisualRoot rotation slerp 回戰鬥朝向（不加時間）。

改 `YuanpeiEncounter.cs` + `YuanpeiBoss.cs` + `YuanpeiAttacks.cs`。編譯無錯，**EditMode 306/306 綠**。

### 追加94 續 114（2026-09-03）— 雷擊標記：紅圈影片預警 + 連續 6 發鎖定範圍攻擊

使用者：「雷擊標記的紅圈特效改成 `紅圈攻擊特效.mp4`，並且設計成連續 6 個雷擊標記，每個標記攻擊前都鎖定玩家位置，做成像 RPG 遊戲那樣帶有預警提示的範圍攻擊」。

**先做了 VideoPlayer 版 → 使用者兩次回報「沒看到這招」**。MCP 焦點 Play 診斷：boss 排程 log 顯示 `ATTACK -> LightningMark` 確實有觸發、也有扣血/累架勢，但地面完全沒特效——runtime spawn 的 `VideoPlayer`（RenderTexture 模式）在這台 D3D11 一樣算成全黑（跟 APIOnly 掉紅通道同一類毛病）。**改用烘好的 flipbook 圖集**，可在 Edit Mode 截圖驗證。

1. **素材**：`紅圈攻擊特效.mp4`（1920×1080、92 frames、3.07s）→ ffmpeg `crop=1080:1080:420:0, fps=35/3, scale=256, tile=6x6` → `Assets/_Project/VFX/Boss/RedCircleStrike_Flip.png`（1536²、36 frame、6×6、**mipmap off / Clamp / Uncompressed**——mipmap 開著會把每格平均成一坨橘色）。內容：石地板上的紅色符文魔法陣（warn）→ 能量匯聚 → 紅色火柱（strike）。
2. **shader**：`Assets/_Project/VFX/Boss/GroundStrikeURP.shader`（`Live2DAction/GroundStrikeURP`）改成 **flipbook 版**——`_Cols`/`_Rows`/`_Frame` 算 tile UV（frame 0 = 圖集左上，翻 row），影片背景是烘進去的深灰石地板非乾淨黑底所以 key 在「亮 OR 飽和紅」上去背，`Blend One One` 貼地發光。材質 `RedCircleStrike.mat`：`_Intensity 1.1`、`_Tint (1,0.62,0.5)`、`_FloorCut 0.34`、`_KeyWidth 0.26`、`_RedBoost 1.4`。
3. **`YuanpeiHazard.SetFlipbook(mat, cols, rows, frames, impactFrac, frameScale=3)`**（Configure 之後）：關掉 primitive disc **和** ring（圖集自帶符文環，primitive 在底下只會變成中間一坨橘光），建一片貼地 quad（localRot X90、scale = radius×2×frameScale），instanced 材質。`Update` 把 warn 期間播 frame 0→impactFrame（impactFrac×(frames-1)，預設 0.62），burst 後 1.3s tail 播完剩下的 frame + `_Fade` 淡出再 Destroy。移除所有 VideoPlayer/RenderTexture 程式碼、`using UnityEngine.Video`。
4. **`YuanpeiAttacks.LightningMark` 改寫**：`count` 3→**6**，每發 spawn 時**重新** `ProjectToGround(player.position)` 鎖定玩家當下位置，錯開發射（每 `number3`=0.55s 一發，非嚴格序列）形成「持續走位」壓力；每發 `number2`=1.4s warn 窗後爆，`number1`=2.0m 半徑。新 `[SerializeField] Material strikeFlipbookMaterial` + cols/rows/frames/impactFraction，已在 `Map_School` 的 `yuanpei_LogoSky` YuanpeiAttacks 上接好。
5. SO `YuanpeiAttack_LightningMark.asset`：count 3→6、number1 1.4→2.0、number2 0.95→1.4、number3 0.5→0.55、baseWeight 1→2、situationalWeightBonus 2.5→3.5、cooldownSeconds 4.2→3.5（提高出現率）。
6. `YuanpeiBoss.BeginAttack` 加 `verboseLog` 一行 `[YuanpeiBoss] ATTACK -> <id>` 診斷 log。

7. **真正看不到的原因**（flipbook 版仍看不到 → Edit-Mode 逐格截圖 debug）：貼地 quad 的 Y 只比廣場地板網格高 0.06，**沉進地板被 opaque floor 的 depth 擋掉**（ZTest LEqual）。加 **`ZTest Always`**（ground decal 本來就該永遠蓋在地板上）+ quad 墊到 localY 0.06 → 修好，魔法陣正常顯示。
8. **shader blend 改 alpha-blend**：純 `Blend One One` 加法在大白天的廣場被曬到看不見（加一點暗紅≈沒差）。改 `Blend One OneMinusSrcAlpha` premult——底層符文圈用 alpha 實際「畫」在地上（可讀的危險區），亮部（火柱/火花，luma > `_AddBright`）再 additive 疊上去。材質 `_Tint (1,.28,.14) / _Opacity 1.8 / _AddBright .78`。

改 `YuanpeiHazard.cs` + `YuanpeiAttacks.cs` + `YuanpeiBoss.cs` + flipbook shader（alpha-blend + ZTest Always）/材質/圖集 PNG + `YuanpeiAttack_LightningMark.asset` + `Map_School.unity`。舊 `RedCircleStrike.mp4` 已刪。編譯無錯，**EditMode 306/306 綠**。Edit-Mode 截圖驗證通過：貼地符文魔法陣 warn 圈清楚可讀，burst 是地面亮閃（平面 quad 畫不出垂直火柱，故火柱那段讀作放射狀亮閃）。

### 追加94 續 115（2026-09-03）— 雷擊標記特效收尾 + OrbitDash 側身衝刺

使用者回報（特效終於看得到後）：1. 特效是長方形，圓形以外要裁掉；2. 特效會遮住玩家建模；3. 繞圈衝刺那招衝刺時改用 boss 側身而非正面。

1. **圓形裁切**：`GroundStrikeURP` frag 加圓形 mask（`_MaskRadius 0.5` / `_MaskSoft 0.09` / `_MaskCenterY` / `_MaskAspectY` 給橢圓藝術用），`smoothstep` 出一個內切圓把長方形四角（烘進去的石地板）裁掉。
2. **不再遮玩家**：shader `ZTest Always` → **`ZTest LEqual` + `Offset -1, -1`**。玩家是 opaque、先畫、寫 depth；decal 在 Transparent queue 用 LEqual → 玩家站在圈上時玩家像素較近、decal 片段被 discard → 玩家蓋在特效上。polygon offset 把 decal 往鏡頭拉一點點壓過地板 z-fighting，不必再靠 ZTest Always。quad localY 0.06→**0.12** 再墊高一點保險。
3. **尺寸修正**：`SetFlipbook` frameScale 預設 3.0→**1.6**（配合圓形 mask，整片 quad ≈ 圓，貼近命中半徑 `number1`=2.0m，只留一點點公平餘裕），之前 11m 視覺 vs 4m 命中差太多。
4. **OrbitDash 側身衝刺**：新 `FaceDiscSideAlong(dir)` —— `LookRotation(dir, cross(dir,up))` 讓圓盤像滾動的硬幣立起來、**邊緣朝前**、正反面朝左右。OrbitDash 衝刺前 + 衝刺中的 `FaceDiscAlong` 都換成這支（其餘 BodyCharge/ChargeLine/ChargeCrush 仍維持正面立直）。

改 `GroundStrikeURP.shader` + `YuanpeiHazard.cs` + `YuanpeiAttacks.cs` + `RedCircleStrike.mat`。編譯無錯，**EditMode 306/306 綠**。Edit-Mode 截圖驗證：圓形裁切乾淨、玩家膠囊站圈上不被遮。OrbitDash 側身待焦點 Play 確認視覺。

### 追加94 續 116（2026-09-04）— 近距離擊退「順移/掉帧」修正

使用者：「非常近距離接觸時 boss 擊退玩家的動畫不自然, 感覺有點掉帧, 順移的感覺」。

1. **玩家擊退的瞬移 pop**：`KnockbackReceiver.instantDisplacementFraction` 0.15→**0.04**（程式預設 + GreyboxTest 的 Player instance）。0.15 時擊退瞬間會 `CharacterController.Move(dir × force×0.15)` ＝ 單幀直接位移 ~1.5-1.8m、中間沒有任何動畫過渡，就是那個「順移」。改成只留一個很小的即時 nudge，實際推移距離交給會線性衰減的 `_dashVelocity`（force×0.25 / 0.5s）逐幀平滑帶過。
2. **補推力**：因為砍了即時 pop，把 yuanpei 的擊退力道補上——BodyCharge/ChargeLine 10→**13**、OrbitDash 12→**15**，總推移距離維持。
3. **Boss 前搖的瞬移**：`BodyCharge` 前搖的 `transform.position -= dir * 1.5f`（單幀瞬間後退，貼身時看起來像 boss 瞬移）改成新 `EaseMove(to, 0.16s)` smoothstep 過去。
4. **防掉帧瞬移**：`BodyCharge` / `OrbitDash` 衝刺每幀位移 `speed * Time.deltaTime` → `speed * Mathf.Min(Time.deltaTime, 0.04f)`，一次幀卡頓不會讓 boss 直接穿場。

改 `KnockbackReceiver.cs` + `YuanpeiAttacks.cs` + `GreyboxTest.unity`（Player KnockbackReceiver）。編譯無錯，**EditMode 306/306 綠**。手感待焦點 Play 確認。

> ⚠️ 踩坑：用 Edit 工具直接改 `GreyboxTest.unity` 會把整份 CRLF→LF 重寫、還可能吃掉內嵌 Mesh 區塊——已 `git checkout` 還原，改走 Unity SerializedObject 存檔。Unity 場景/asset 一律不要用純文字 Edit。（另註：GreyboxTest.unity 每次存檔本來就會因為內嵌 Cubism ArtMesh 重新烘出巨大 diff，非本次造成。）

### 追加94 續 117（2026-09-04）— 專案漏洞掃描 + debug 腳本擋出正式 Build

使用者要求「檢查專案中是否有漏洞」。掃描結果：**無密鑰外洩**（全 repo grep `api_key/secret/token/private_key` 命中皆為英文註解字；`ProjectSettings.asset` 的 `ps4NPTitleSecret`／`metroCertificatePassword` 皆空）、**無危險執行期程式碼**（無 `BinaryFormatter`／`XmlSerializer`／執行期網路連線／`Process.Start`／`Assembly.Load`；檔案 I/O 全在 Editor 端烘圖工具與第三方匯入器）。單機離線遊戲，無傳統資安面。

發現並記錄（未改，待處理）：

1. `DoNotShipBuildGuard.cs` 的擋 build 清單與 `ASSET_LICENSES.md` 對不齊——`Environment/Meshy/YuanpeiLogo`（文件標 DoNotShip／真實商標）與 `Characters/Weapons/BloodKatana`（授權待確認）不在清單裡；`MechaModel_DoNotShip` 那條路徑已失效（資產已移除，但 `MechaVisualSetup.cs` 選單仍會加回）；guard 用 `AssetDatabase.IsValidFolder` 檢查、路徑打錯會靜默放行且**無對應 EditMode 測試**；guard 只擋非 Development build。**2026-09-04 使用者回覆：YuanpeiLogo／BloodKatana 兩項已有授權，第 1 點暫不處理。**
2. `MechanicalWings.fbx`（玩家翅膀，用於 GreyboxTest）未登記在 `ASSET_LICENSES.md`，來源不明。
3. `Skybox_Procedural` shader stripping 風險（`KNOWN_ISSUES.md` 既有項，非新發現）。

本次唯一改動：三個 debug overlay 腳本包 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 全檔——`SekiroDeflectDebug.cs`（F9）、`BossAnimationDebugMode.cs`（F7）、`DevTimeFreeze.cs`（`` ` ``）。三者都掛在 build scene `GreyboxTest`、原本無任何 `#if` 保護，會編進正式 Build 讓熱鍵在出貨版可用（作弊、檢視未完成內容）。三者僅被 Editor setup 工具與註解引用（無執行期相依），包全檔編譯安全；正式 Build 下 `GreyboxTest` 上的對應元件變成無害的 missing-script 空槽。`DevTimeFreeze` 檔頭「Remove before shipping」註解一併更新。編譯無錯、Console 無錯。

### 追加94 續 118（2026-09-04）— 元培 Boss 衝撞攻擊「剛看到就中／只有頭跟尾」修正

使用者：「元培boss 衝撞類攻擊常常剛看到動畫玩家就受到攻擊了 而且過程沒平滑 感覺只有頭跟尾」。

**根因**：`YuanpeiBoss.Update()` 在 `Attacking` state 每幀呼叫 `HoldHover()`，以 8 m/s 把 Boss 的 Y 拉回 hover 高度（config `hoverHeight` 2.6）。衝撞協程只用 `dir.y *= 0.3` 加一點點向下位移 → HoldHover 贏 → **Boss 在離地 2.6m 高度平移衝過去，完全沒有俯衝**。再加上 `DiscFaceHitsPlayer` 命中體積 `hitR * 2.8`（≈5m 球）＋ forward window `forwardReach * 1.6`（提早 ~2.9m 觸發），衝刺一啟動玩家就被判中、迴圈立刻 `hitPlayer=true` 結束 → 只剩前搖（頭）＋ recovery（尾）。

1. **`YuanpeiBoss.cs`**：新增 `SuspendHover(seconds)` + `_hoverSuspendUntil`；`AttackTelegraph/Attacking/AttackRecovery` 分支在 suspend 期間跳過 `HoldHover()`（`FaceTarget` 照跑），讓衝撞招式自己掌管 Y。
2. **`YuanpeiAttacks.cs` `BodyCharge`（含 `ChargeLine`）**：
   - 進招即 `SuspendHover(前搖+衝刺+尾巴的總預算)`。
   - 保證助跑：後退到離玩家 ~6.5m（`EaseMove` 0.32s 平滑、夾在 arena 內），取代固定 1.5m。
   - 鎖定時用**完整俯衝向量**瞄 `player + up*0.9`，衝刺途中 Y 一路 `MoveTowards` 到 `groundY + 1.1` 後轉平——先俯衝再貼地推進的可讀弧線。
   - `DiscFaceHitsPlayer` 命中體積 `hitR*2.8`→`hitR*1.3`；forward window `forwardReach*1.6`→`forwardReach`（只有圓盤真的跟玩家齊平才判中）。
   - 新 `SlerpDiscInto(dir, 0.12s)`：衝刺入場的 VisualRoot 轉向改 slerp、不硬切（修「沒平滑」）。
3. **`OrbitDash`**：同樣 `SuspendHover`（涵蓋繞圈+衝刺）、衝刺改完整俯衝向量 + 貼地 skim、命中體積 `hitR*1.3`、`SlerpDisc`。衝刺方向拆 `dashDir`（3D 俯衝）/ `dashFlat`（水平，給 SphereCast、命中判定、`FaceDiscSideAlong`）。
4. **`ChargeCrush`**：進招時除既有 `SuspendYClamp` 外一併 `SuspendHover`（原本 HoldHover 也在扯它爬升到 12m 頂點）。
5. **數值（ScriptableObject，走 MCP SerializedObject 不文字編輯）**：`YuanpeiAttack_BodyCharge.number1` 26→18、`YuanpeiAttack_ChargeLine.number1` 46→28、`YuanpeiAttack_OrbitDash.number2` 34→24（衝刺速度），讓可見衝刺拉長到 ~20–40 幀。

改 `YuanpeiBoss.cs` + `YuanpeiAttacks.cs` + 3 個 `YuanpeiAttack_*.asset`。`validate_script` 乾淨、**EditMode 306/306 綠**、Console 無錯。實際俯衝弧線 + 助跑手感待焦點 Play 確認。

### 追加94 續 119（2026-09-04）— 移除多重光爆 + 加強元培 Boss 攻擊慾望

使用者：「移除 多重光爆 這個招式；目前對玩家的施壓程度低 請加強 boss 攻擊慾望」。

**移除 MultiAoE（多重延遲範圍光爆）**：
- 從 `yuanpei_LogoSky` 的 `YuanpeiBoss.attackPool` 移除（Map_School.unity，走 SerializedObject，9→8 招）
- 刪 `YuanpeiAttack_MultiAoE.asset`、`YuanpeiAoePlacement.cs`（+ meta）、`YuanpeiBossLogicTests` 的 3 個 AoE safe-route 測試、`YuanpeiAttacks.MultiAoE()` 協程 + switch arm + 只給它用的 `RandXZ()`、`YuanpeiScheduler.Matches` 的 MultiAoE case。`YuanpeiAttackId.MultiAoE` enum 值保留（無害標籤，switch 無對應 arm＝no-op）。`Situation.arenaHasGoodFloor` 變成死欄位（留著，移除牽涉 struct + PickAttack）。

**加強攻擊慾望**（施壓低的主因是冷卻長 + 大型法術互斥槽把遠程招卡死，只剩衝撞在冷卻）：
- **冷卻全面砍**（`.asset` `cooldownSeconds`）：ProjectileBurst 2→1.5、FocusLaser 5.6→4.2、LightningMark 3.5→2.8、Shockwave 6.3→4.5、BodyCharge 5.6→3.8、ChargeLine 5→3.8、ChargeCrush 12→9、OrbitDash 9→6.5
- **ProjectileBurst `isMajorHazard` 1→0**：不再佔用/被「同時只能一個大型法術」槽擋 → 隨時可當填招（三連射本來就是最便宜最短的 chip 傷）。大型法術槽現在只剩 FocusLaser + LightningMark 兩招互斥
- `YuanpeiBossConfig`：`onScreenGraceBeforeAttack` 0.15→0.08、`rotationRecoverySeconds` 6→4、`rotationRecentWeightFactor` 0.35→0.55（LRU 抑制變輕、恢復更快 → 剛用過的招權重回升快，不會被逼著一直換招而卡住）
- `YuanpeiBoss` 看門狗：`PickAttack` 回 null 撐過 0.3s→**0.12s** 就 `ForceAnyInRangeAttack`

改 `YuanpeiAttacks.cs` + `YuanpeiScheduler.cs` + `YuanpeiBoss.cs` + `YuanpeiBossConfig.cs`（註解）+ `YuanpeiBossLogicTests.cs` + 刪 `YuanpeiAoePlacement.cs` + 8 個 `YuanpeiAttack_*.asset` + `YuanpeiBossConfig.asset` + `Map_School.unity`（attackPool）。編譯無錯、Console 無錯、**EditMode 303/303 綠**（少了 3 個 AoE 測試）。實際施壓強度待焦點 Play 確認——冷卻若砍太兇再往回加。

### 追加94 續 120（2026-09-04）— 三連射→六連射、衝撞情境放寬、下壓拋進虛空、架勢隨時間累積、處決特寫鏡頭

使用者 4 項：(1) 光粒子三連射→6連射且權重調高；(2) 肉身衝撞拿掉「能量<25」情境條件；(3) 頭頂垂直下壓命中→把玩家擠出地圖外（虛空）同樣秒殺；(4) 架勢條可隨時間緩慢累積、滿→落地硬直、按 F 處決給近距離側邊特寫鏡頭。

1. **六連射**：`YuanpeiAttack_ProjectileBurst.asset` `count` 3→6、`baseWeight` 1.2→**2.5**、`displayName` 三連射→六連射、`cooldownSeconds` 已在續 119 設 1.5。`ProjectileBurst()` 節奏：起手一對慢拍（0/0.4s）後轉快串流（0.16s），最後**兩顆**改預判走位（原本只有最後一顆）。約 1s 打完 6 顆。
2. **肉身衝撞情境**：`YuanpeiScheduler.Matches` 的 `BodyCharge` 拿掉 `|| s.energy < 25f`，只剩距離 5–12m。
3. **下壓拋進虛空**：`ChargeCrush` 命中改成 `crushed=true; break` 出砸落迴圈 → 生成衝擊環 → **`VoidPunt(player)`**（停用玩家 `CharacterController`+`CharacterMovement`、0.5s 沿「遠離場地中心」方向外拋 + 小上拋弧再急墜到場地邊緣外 14m、地面下 32m）→ 才 `DamagePlayer(999999)`。安全性：玩家死後 `YuanpeiEncounter.Defeat()` 本來就會在 ~5.6s 後把死亡玩家傳回大馬路，死亡畫面「你菜完了」蓋著虛空墜落。`VoidPunt` 停用的元件記在欄位裡，`CancelAll()` 會 `RestorePuntedPlayer()` 還原（協程被中途 stop 也不會把玩家鎖死）。`YuanpeiAttacks` 加 `using Live2DAction.Characters`。
4a. **架勢隨時間累積**：新 config `postureRegenPerSecond`（=1.6，約 62s 從空到滿）。`YuanpeiBoss.Update()` 在 `IsActiveCombatState()`（Hover/Reposition/AttackTelegraph/Attacking/AttackRecovery）每幀 `vitals.AddPosture(rate * dt)`。滿→既有 `PostureFull`→`OnPostureFull`→落地硬直→F 窗口流程不變。downed/recharge/intro/dead 狀態不累積。
4b. **處決特寫鏡頭**：`YuanpeiExecution.Finisher()` 抓 `Camera.main`、關 `ThirdPersonCameraController`、起 `DriveExecutionCam` 協程——玩家↔Boss 連線側面（隨機左右肩）、近距離 4.3m→2.9m 慢推進、微低角、看向兩者中點。處決 anim 結束停協程：致命→不還原（交給 `DeathDissolve` 抓鏡頭）、非致命→還原 `ThirdPersonCameraController`。

改 `YuanpeiAttacks.cs` + `YuanpeiScheduler.cs` + `YuanpeiBoss.cs` + `YuanpeiBossConfig.cs` + `YuanpeiExecution.cs` + `YuanpeiAttack_ProjectileBurst.asset` + `YuanpeiBossConfig.asset`。`validate_script` 對 `YuanpeiExecution` 誤報「Duplicate PlayerInRange」（heuristic 對 `while(true)+yield` 誤判，實際只有一個、Unity 編譯乾淨）。編譯無錯、Console 無錯、**EditMode 303/303 綠**。虛空拋飛弧線、架勢累積速率、處決鏡位待焦點 Play 確認。

### 追加94 續 121（2026-09-04）— 六連彈實體命中、下壓改「壓穿地板」、處決後 Boss 回正

使用者 Play 回饋 3 點：(1) 六連彈要真的碰到玩家表面體積才造成傷害；(2) 頭頂垂直下壓動畫奇怪、不像「被壓進地下虛空」；(3) 處決後 Boss 變歪斜、沒回到正面直立。

1. **`YuanpeiProjectile` 實體命中**：命中判定從「orb 位置 vs `player.position + up*1.0` 單點球檢查（半徑 `hitRadius + 0.35`）」改成 **`Physics.OverlapSphereNonAlloc`**（半徑＝可見 orb 半徑 `hitRadius*1.3 + 0.05` skin），逐一比對 `col.transform.root == _player` → 只有 orb 球體真的和玩家 collider 重疊才 `ApplyDamage`。static buffer 免 GC。
2. **`VoidPunt` 改成「壓穿地板」**：不再往場地外側拋。改成 press（0.42s，玩家沿 `k²` 加速直落到地面下 2.2m、只留 0.35m 橫向偏移不穿過圓盤軸心；圓盤跟著壓到 `groundY+0.9` 且維持在玩家頭頂上方 1.7m）→ sink（0.5s，玩家繼續 `k²` 加速直墜 36m 進虛空）→ 才秒殺。衝擊環 + HitStop 抽成 `SpawnCrushImpact()`，crush 時在 press 結束（圓盤落地那刻）觸發、miss 時照舊。移除 crush 分支原本的 boss Y snap（VoidPunt 現在自己掌管）。
3. **處決後 Boss 回正**：`YuanpeiBoss` 新增 `_skyVisualLocalRot`（Awake 記錄 `visualRoot.localRotation` 原始直立姿態）。`RecoverRoutine`（重新升空）把原本「只遞減 yaw 自轉、留著墜落的 X/Z 翻滾」改成 **slerp `visualRoot.localRotation` → `_skyVisualLocalRot`**（SmoothStep over `reAscendSeconds`）、結尾強制設準。`ResetForRematch` 也一併還原 `visualRoot.localRotation`。

改 `YuanpeiProjectile.cs` + `YuanpeiAttacks.cs` + `YuanpeiBoss.cs`。編譯無錯、Console 無錯、**EditMode 303/303 綠**。壓穿弧線 / orb 命中判定 / 處決回正待焦點 Play 確認。

### 追加94 續 122（2026-09-04）— 六連彈鎖定人物中心、下壓改「完全蓋地才觸發＋運鏡」

使用者 Play 回饋：(1) 六連彈每一下都要先鎖定玩家「當前人物中心」才射，不預判；(2) 頭頂垂直下壓要**真的碰到**（圓盤完全蓋地）才播玩家被擠出的演出，且擠出瞬間攝影機快速聚焦回玩家、做一個運鏡。

1. **六連彈鎖定人物中心**：`ProjectileBurst` 每顆瞄準改成 `PlayerCenter(player)`（新 helper：玩家 `CharacterController` 的世界 collider 中心，退而求其次 collider bounds 中心 / `pos + up*0.9`）——**無預判、無提前量**，每顆在射出當下重讀。順帶把 homing 全關（`homeTime`/`homing` 傳 0，鎖定射擊不需追蹤）。移除已無用的 `PredictedPlayerPoint` + `_lastPP`。
2. **下壓「完全蓋地才觸發」**：砸落迴圈不再中途做 XZ 距離猜測，**一路砸到 `floorY + 0.15`**（圓盤真的貼地＝完全蓋地）→ 才判定玩家是否在圓盤 footprint 內（`hitR` 半徑，對齊可見標記圈）＋玩家在地面高度附近。中了才 → `CrushEjectCam` + `VoidPunt` + `SpawnCrushImpact` + 秒殺；沒中 → 照舊落地硬直節奏。
3. **運鏡 `CrushEjectCam`**（1.15s，`attacks` 上平行協程）：抓 `Camera.main`、關 `ThirdPersonCameraController` → 前 ~0.35s 以 rate 20 快速 whip 到玩家側面 6.5m→3.6m、俯角，之後 rate 9 平滑跟拍玩家被壓穿地板往虛空下沉（相機高度 3.6→1.4 隨之放平）→ 結束還原 `ThirdPersonCameraController`。停用的元件記在 `_crushCamCtrl`，`CancelAll()` 加 `RestoreCrushCam()`（協程被中途 stop 也不會卡住相機）。

改 `YuanpeiAttacks.cs` + `YuanpeiProjectile.cs`。編譯無錯、Console 無錯、**EditMode 303/303 綠**。六連彈準度、下壓命中時機、運鏡待焦點 Play 確認。

### 追加94 續 123（2026-09-04）— 勝利相機回位、處決鎖操控、六連彈加難、下壓運鏡放平、車輛觸發、中心點才啟動

使用者 Play 回饋 6 點：

1. **勝利後相機沒回到玩家**：`DeathDissolve` 結尾原本 `camCtrl.enabled = camCtrlWas`——若這場是 F 處決致命，`Finisher` 把 `ThirdPersonCameraController` 關掉、沒還原（memory 留給死亡演出接手），`camCtrlWas` 讀到 false → 死亡演出「還原」成關閉 → 相機永遠凍在死亡角度。改成**無條件 `camCtrl.enabled = true` + `SnapYawToTarget()`**。
2. **處決要進運鏡視角 + 鎖玩家操控**：`YuanpeiExecution.Finisher()` 加 `LockPlayer(true)`（停用玩家 `CharacterMovement` + `PlayerCombat`，記原狀態）→ 處決結束/致命/`OnDisable` 都 `LockPlayer(false)` 還原。運鏡（`DriveExecutionCam`）本來就有，維持。
3. **六連彈太好躲（shift）**：`number1`(速度) 16→**27**、`number2`(半徑) 0.35→**0.5**；發射節奏改成**兩波緊發**（3+3，波內每顆 0.09s、波間 0.34s）——一次 shift 閃避（~0.5s 無敵）只能吃掉一波，第二波要再抓時機。每顆仍鎖當前人物中心。
4. **下壓運鏡突兀**：`CrushEjectCam` 重寫——不再 rate-20 硬 whip。改成擷取相機當下 pos/rot，用單一 `SmoothStep` 混合曲線（1.2s）平滑帶到俯視追拍位（距離 5.5→3.4、高度 3.2→1.3），一氣呵成不跳。
5. **車輛駛入無法觸發 boss**：`YuanpeiEncounter` 觸發判定重寫——`ResolvePlayerFrom(other)`：從 `other.transform.root` 往下找 `PlayerInputProvider`、再往上找名為 `Player` 的 GO。玩家坐車時 `VehicleEntrySystem` 會把 Player 掛進車底座錨點，所以車體 collider 進 trigger 時一樣找得到 Player（貓單獨開車 → null，正確忽略）。
6. **要到廣場最內部中心才啟動**：新 `centerActivationRadius`(3.5m)。`OnTriggerEnter/Exit` 只記錄 `_zonePlayer`（人在 trigger 體積內），`Update()` 每幀檢查 `_zonePlayer` 到 `combatCenter` 的平面距離 ≤ 半徑才 `StartEncounter`——光站在觸發區邊緣不算。

改 `YuanpeiEncounter.cs` + `YuanpeiExecution.cs` + `YuanpeiAttacks.cs` + `YuanpeiAttack_ProjectileBurst.asset`。編譯無錯（domain reload 後使用者已在 Play）。EditMode 因使用者正在 Play 未跑（`YuanpeiScheduler` 改動不影響任何測試）。6 項全待焦點 Play 確認。`validate_script` 對 `YuanpeiExecution` 續報「Duplicate PlayerInRange」誤判（heuristic，實際單一、Unity 編譯乾淨、已在 Play 佐證）。

### 追加94 續 124（2026-09-04）— boss 不把車輛當目標

使用者：續 123 讓開車能觸發 boss 戰後，boss 把車輛當成目標物件。

原因：玩家坐車時 `VehicleEntrySystem.Mount` 把 Player GameObject `SetParent` 到車底座錨點 → `Player.root` 變成車體。`YuanpeiBoss.BeginEncounter` 的 `player = triggeringPlayer.root` 與 `ResolvePlayer()` 的 `p.transform.root` 都因此指到車。

1. **`VehicleEntrySystem`** 新增 `public void ForceDismountAll()`（不用按 F，直接把 Player + Cat 下車；空座位 no-op）。
2. **`YuanpeiEncounter.StartEncounter`** 開場先掃全場 `VehicleEntrySystem`，只要 Player/Cat 有坐 → `ForceDismountAll()`——這場在地面打、車不進戰場目標。加回 `using Live2DAction.Vehicles`。
3. **`YuanpeiBoss.BeginEncounter`** 防呆：不再盲取 `triggeringPlayer.root`，改成往上走父鏈找名為 `Player` 的 GO；若 `player` 名字不是 `Player` 就 `ResolvePlayer()` 重解。

改 `VehicleEntrySystem.cs` + `YuanpeiEncounter.cs` + `YuanpeiBoss.cs`。編譯無錯、Console 無錯、**EditMode 303/303 綠**。`validate_script` 對 `VehicleEntrySystem` 誤報「Duplicate CurrentPossessed」（同 heuristic bug，實際單一、Unity 編譯乾淨）。開車進場→強制下車→boss 鎖玩家待 Play 確認。

### 追加94 續 125（2026-09-04）— 下壓運鏡改「放遠看擠出→平滑回玩家」

使用者：下壓運鏡改成放遠視角，看得到玩家被 boss 壓穿地板/擠出地圖，隨後攝影機平滑快速回到玩家身上。

`CrushEjectCam(player, crushGround)` 重寫成兩段（都 `SmoothStep` 不跳）：
1. **Phase 1 wide（0.9s）**：從相機當下姿態 ease 到「crushGround 後方 15m、上方 12m」的高遠位、看向 crushGround 下方——整個「圓盤蓋地壓玩家穿地板」全都入鏡。
2. **Phase 2 return（0.5s）**：從遠景平滑帶回 crushGround 的普通過肩位（後方 4.5m、上方 1.9m）。

結束後**刻意讓 `ThirdPersonCameraController` 保持關閉**——此時玩家已死、在虛空下方 36m，開回控制器會把鏡頭甩到屍體。改由 **`YuanpeiEncounter.Defeat()` 在死亡畫面 hold + 傳送前無條件 `tpc.enabled = true`**（`SceneTransitionRunner` 只 `SnapYawToTarget`、不開控制器）。`CrushEjectCam` 收尾把 `_crushCamCtrl` 清 null（`CancelAll()` 的 `RestoreCrushCam()` 安全網仍在，協程被中途 stop 時才動作）。

改 `YuanpeiAttacks.cs` + `YuanpeiEncounter.cs`。編譯無錯、Console 無錯、**EditMode 303/303 綠**。遠景構圖 + 回鏡平滑度待焦點 Play 確認。

### 追加94 續 126（2026-09-04）— 六連彈專屬前搖、處決全套運鏡、直線衝刺預警＋加速

使用者 Play 回饋 3 點：

1. **六連彈缺專屬辨識**：`ProjectileBurst` 起手加 `MuzzleCharge(shots, radius, 0.55s)`——`shots` 顆光點螺旋內收、聚成漸亮的核心，再開火。跟其他招的通用 `TelegraphPulse` 明顯不同（「子彈正在成形」）。加上 `Run()` 既有的 telegraph pulse ≈ 1s 預告。
2. **F 處決全套運鏡**：`Finisher()` 重寫——先擷取玩家/boss 位置，`DriveExecutionCam` 改成 4 段 `ExecCamMode` state machine：
   - `FrameBoth`：側面、依兩者間距動態拉遠（3.7–6.8m），處決動畫期間同時框住玩家＋boss。
   - `FollowPlayer`：處決命中後，boss `BossBump`（往玩家方向撲 1.1m 再彈回＝肉身彈開）＋玩家 `ShoveBack`（沿遠離 boss 方向滑退 5m、ease-out、小跳；CC 暫關），鏡頭跟玩家。
   - `FollowBossUp`：`RecoverToAir` 讓 boss 升空，鏡頭低位仰角同時帶到地面玩家＋升空 boss。
   - `ReturnToPlayer`：平滑帶回玩家過肩位。
   - `Done`：`DriveExecutionCam` 開回 `ThirdPersonCameraController` + `SnapYawToTarget`。玩家操控（`LockPlayer`）到 `ReturnToPlayer` 結束才解鎖。致命則 `_execCamHandBack = false` + 停協程、交給 `DeathDissolve`。
3. **直線衝刺（`BodyCharge`/`ChargeLine`/`OrbitDash`）**：
   - **預警看得見**：`ChargePathTelegraph` 重寫——`Live2DAction/VFX/AdditiveUnlit`（Blend One One）的地面填充 quad ＋兩條亮脈動邊軌（cube），寬度＝2×hitR（對齊實際命中 `faceRadius`）。lane 不再一啟動就消失，改由新 `FadeChargeLane` 在衝刺頭 ~0.35–0.4s 邊衝邊淡出。
   - **加速不跳**：衝刺 `speed` 前 3–3.5m 由 0.35→1.0 ramp（原本 0→全速瞬跳）；`SlerpDiscInto` 0.12→0.2s ＋ 加 `EaseMove` 0.7m 後座（load-then-fire）。
   - **命中對齊視覺**：`DiscFaceHitsPlayer` 的 `forwardReach` `hitR`→`hitR*0.55`（原本圓盤還離玩家 ~2m 就判中＝「撞擊與視覺不匹配」）。

改 `YuanpeiAttacks.cs` + `YuanpeiExecution.cs`。編譯無錯（domain reload 後使用者已在 Play）。EditMode 因使用者 Play 中未跑（改動不碰任何 pure-class 測試面）。3 項全待焦點 Play 確認。

### 追加94 續 127（2026-09-04）— F 處決完強制玩家降落地面

使用者：F 處決完後要強制讓玩家降落到地面（處決運鏡裡 `ShoveBack` 的小跳、或半空按 F，會讓玩家在運鏡剩下的時間浮空）。

新 `SnapPlayerToGround(bool puff)`：以 `SampleGround`（跳過玩家/boss/hazard）從玩家當下 XZ 往下打地面 → CC toggle 把 root 傳送到 `groundY + footToRoot + 0.02`（`footToRoot` = `cc.height/2 - cc.center.y`）→ 可選 `SpawnLandingImpact` 落地灰塵。

- `Finisher()` 存活路徑：`ShoveBack` 之後、`FollowBossUp` 之前呼叫 `SnapPlayerToGround(true)`——玩家滑退落定即刻著地，運鏡剩餘段落站在地上。
- 對齊步驟：`stand.y = _player.position.y`（沿用當下高度、半空按 F 就浮空）改成 `SampleGround(...).y + footToRoot + 0.02`——處決一開始就站穩在錨點前地面。

改 `YuanpeiExecution.cs`。編譯無錯、Console 無錯、**EditMode 303/303 綠**。

### 追加94 續 128（2026-09-04）— 撞擊只在本體接觸才生效、擠出地圖只留秒殺、紅圈追蹤

使用者 Play 回饋 3 點：

1. **只有秒殺招擠出地圖**：`BodyCharge`/`ChargeLine` 的擊退力 13→**6**、`OrbitDash` 15→**7**——firm stagger 而非 launch，不會把玩家推下地圖邊緣。`VoidPunt`（把玩家壓穿地板進虛空）本來就只有 `ChargeCrush` 用，維持。
2. **撞擊只在 boss 本體碰到玩家本體才生效**：`DiscFaceHitsPlayer` 重寫——(a) 圓盤平面必須跟玩家沿行進軸齊平（`|along| ≤ hitR*0.35`，原本 `hitR*0.55` 還會提早 ~1m），(b) **`Physics.OverlapSphereNonAlloc`（半徑 `hitR*1.15`）真的和玩家 collider 重疊**（`col.transform.root == player.root`）才判中——不再是幾何 proximity 猜測，「站在預警範圍上但 boss 還沒到」不會受擊。命中後照舊擊退（力道已降）。預警 lane 寬度也改成 `hitR*1.15`＝實際命中 `faceRadius`（`ChargePathTelegraph` 兩處呼叫）。
3. **紅圈攻擊（`LightningMark`）加難**：`YuanpeiHazard` 新增 `SetHoming(trackSeconds, easeRate, groundMask)`——`StrikeCircle` 在 warn 前 55% 時間內以 ease rate 4 追蹤玩家地面位置、然後鎖定（走出去沒用）。SO 數值：radius 2→**2.4**、warn 1.4→**1.1**、between 0.55→**0.4**、count 6→**7**。

改 `YuanpeiAttacks.cs` + `YuanpeiHazard.cs` + `YuanpeiAttack_LightningMark.asset`。編譯無錯、Console 無錯、**EditMode 303/303 綠**。撞擊命中時機 / 紅圈難度待焦點 Play 確認。

### 追加94 續 129（2026-09-04）— 直線衝撞把玩家頂出地圖：boss 本體 collider 其實是實心

使用者：續 128 之後還是被直線衝撞頂出地圖外。

**真因**：boss 的 `CollisionRoot/BodyCollider`（SphereCollider r3.6）與 `CoreWeakPoint`（r1.9）**在 Map_School 場景裡是 `isTrigger = 0`（實心）**——memory 舊記錄「both trigger」是錯的。boss 以 ~28 m/s 衝過玩家位置時，玩家 `CharacterController.Move()` 每幀對這顆 3.6m 實心球做 depenetration，把玩家一路推出地圖。擊退力（續 128 已降到 6）根本不是元凶。

- **兩顆 SphereCollider 改成 trigger**（Map_School.unity，走 MCP SerializedObject）。玩家武器命中仍有效（`PlayerWeaponHitbox` 用 `QueryTriggerInteraction.Collide`、`PlayerCombat` 的 `OverlapCapsule` 預設也含 trigger）；鎖定、`YuanpeiBossHitReceiver`（`IDamageable`）都不受影響。衝撞命中一律由 `DiscFaceHitsPlayer`（續 128 的 OverlapSphere）判定。
- `YuanpeiBoss.EnterDeath`：`if (!col.isTrigger) col.enabled = false` → 直接 `col.enabled = false`（現在都是 trigger 了，舊寫法變 no-op）；`ResetForRematch` 照舊全開。

改 `Map_School.unity`（2 個 collider）+ `YuanpeiBoss.cs`。編譯無錯、Console 無錯、**EditMode 303/303 綠**。

### 追加94 續 130（2026-09-04）— 直線衝刺改側身、下壓虛空可見、處決穿模、觸發點更深

使用者 Play 回饋 4 點：

1. **直線衝刺改側身（硬幣最窄那面）**：`BodyCharge`/`ChargeLine`/`OrbitDash` 全改 `FaceDiscSideAlong`（rim 領先，原本 BodyCharge 是 `FaceDiscAlong` 平面領先）。`SlerpDiscInto` 加 `edgeFirst` 參數（各招入場 slerp 對）。`DiscFaceHitsPlayer` 重寫：sphere 中心從 root 改到 **圓盤前緣**（`transform.position + dir * hitR*0.9`），半徑 1.1m（薄刃 + 玩家）——命中點＝視覺前緣。預警 lane 寬度也縮到 2.2m 配合。
2. **下壓掉虛空看不到**：`VoidPunt` 重排——press 只把玩家壓到地表 `groundY+0.12`（仍可見）0.36s → pin 0.14s → 46 m/s 加速直墜（0.42s）。新增 `MakeVoidHole`/`GrowVoidHole`/`FadeVoidHole`：地面深色 Quad「虛空洞」隨壓下擴張到 5.5m、玩家墜入、之後縮回消失——不透明地板從上方遮不住它。`CrushEjectCam`：wide 鏡頭 **前 40% 就到位**（原本 SmoothStep 整段、鏡到位時玩家已墜完）、加**遮擋 raycast**（wide 位卡在建物內就往內拉+往上抬，最多 4 次）。
3. **F 處決穿模**：對齊距離 `anchorPos - faceDir * 1.6` → **2.8m**（玩家模型原本插進圓盤）；`BossBump` lunge 1.1m → **0.7m**；落地 clearance +0.02 → +0.05。
4. **觸發點太靠門口**：`YuanpeiEncounter` 新增 `centerActivationOffset`（Vector3，往 boss/廣場內），`centerActivationRadius` 3.5→**2.0**（使用者指定）。啟動點＝`combatCenter + centerActivationOffset` = `(-2,0.5,-105) + (0,0,-4)` = `(-2,-109)`。**場景已更新**（走 MCP SerializedObject）：`centerActivationRadius` 2.0、`centerActivationOffset` (0,0,-4)、trigger box 往南拉大（local center.z 0→-3、size.z 12→18，world Z span [-111,-99]→[-117,-99]，北緣不動）。啟動點在 box 內、離門口（~Z -99）約 10m。

改 `YuanpeiAttacks.cs` + `YuanpeiExecution.cs` + `YuanpeiEncounter.cs` + `Map_School.unity`（YuanpeiEncounter 欄位 + BoxCollider）。編譯無錯、Console 無錯、**EditMode 303/303 綠**。4 項的手感/構圖待焦點 Play 確認。

### 追加94 續 131（2026-09-04）— boss 戰觸發改成「跨過一條橫線」

使用者：改成地圖中心切一條線，玩家不管走到 X（橫向）哪裡，只要跨過這條線（Z）就觸發 boss 戰。

`YuanpeiEncounter`：移除 `centerActivationRadius` + `centerActivationOffset`（點 + 半徑），改成 `activationLineZ`（world Z，預設 -109）+ `activationCrossSouth`（預設 true）。`Update()`：玩家在 trigger 體積內（`_zonePlayer` 有值）且 `position.z <= activationLineZ`（或 `>=`，看 `activationCrossSouth`）→ `StartEncounter`。X 完全不管。

場景（走 MCP SerializedObject）：`activationLineZ = -109`、`activationCrossSouth = true`；trigger BoxCollider X 加寬 `size.x 20→34`（world X span [-17,17]，「任何橫向位置」名符其實），Z 維持 [-117,-99]。

改 `YuanpeiEncounter.cs` + `Map_School.unity`。編譯無錯、Console 無錯、**EditMode 303/303 綠**。

### 追加94 續 132（2026-09-04）— 今日修正回頭審查：2 個真 bug 修掉

回頭審 續 118–131 全部改動（YuanpeiAttacks +698 行等）。抓到 2 個真 bug，已修：

1. **`YuanpeiEncounter.StartEncounter` 的 `ForceDismountAll` 會誤踢貓**：開場迴圈條件是 `PlayerSeat != None || CatSeat != None`——貓在地圖別處開車時，玩家徒步觸發 boss 戰也會把貓從車上強制彈下來。改成只在 `PlayerSeat != None`（玩家真的在車上）才 `ForceDismountAll`。
2. **被動架勢在攻擊 Active 中累滿會中斷攻擊**：`IsActiveCombatState()` 原本含 `Attacking`/`AttackRecovery`。若被動架勢剛好在此累到滿 → `OnPostureFull` → `attacks.CancelAll()`——最糟情況是打斷 `ChargeCrush` 的 `VoidPunt`，讓 `DamagePlayer(999999)` 沒執行到，玩家「被完美壓中」卻活下來。改成只在 `Hover`/`Reposition`/`AttackTelegraph` 累積（Telegraph 打斷無害、涵蓋大部分非 hover 時間，累積速度不受影響）。

其他審過認為可接受（非 bug）：boss 秒殺後 ~1.5s 內若玩家還沒 deactivate 可能再出一招（既有行為）；`YuanpeiProjectile` 極低幀率下理論可穿透（greybox 容忍）；`Situation.arenaHasGoodFloor` 死欄位（無害）。

改 `YuanpeiBoss.cs` + `YuanpeiEncounter.cs`。編譯無錯、Console 無錯、**EditMode 303/303 綠**。

### 追加94 續 133（2026-09-04）— 雷擊標記紅圈換成新的「仿3D」版本

使用者提供 `C:\Users\homec\Downloads\紅圈特效.mp4`，要求把元培 boss 地板攻擊（雷擊標記）的紅圈特效換成這個。

**素材內容**：1920×1080/30fps/3.23s，純黑底，右下角生成工具浮水印。以低角度透視畫成橢圓（不是正圓）的符文法陣——稀疏線圈成形 → 圓心紅點長大成實心盤 → 邊緣起火 → 中心爆出白熱火柱＋石地裂開＋碎石＋粉塵。這個「橢圓透視」本身就是「仿3D」的來源：從第三人稱斜角看下去會讀成貼在地上的立體法陣，不是平面貼圖。

**沿用既有管線**：`Live2DAction/GroundStrikeURP` shader（追加94 續 114 建的）本來就是為「矩形貼圖裡有一顆用 luma/紅色飽和度 key 出來的橢圓法陣＋要裁掉貼圖角落的深色地板」設計的，新素材完全符合這個假設，**shader 本身不用改**，只要換新的 flipbook atlas + 重新量測橢圓的位置/扁率參數：

1. **ffmpeg 離線烘圖**：`drawbox` 蓋掉浮水印 → `crop=1500:1080:210:0` 去黑邊 → `fps=19.7938,scale=288:208,tile=8x8` → `RedCircleStrike2_Atlas.png`（2304×1664，8×8＝64 幀）。burst 落在第 44 幀（≈68%），對應 `strikeFlipbookImpactFraction` 0.68。
2. **踩坑**：texture importer 沒設 `npotScale=None` 時，Unity 預設會把非 2 的冪次邊長「各自」四捨五入到最近的 2 的冪次——2304×1664 被誤壓成 2048×2048，整張圖被拉伸變形。改回 `npotScale: None` 後才正確保留原生 2304×1664。Import 設定比照舊版：Uncompressed／No Mipmap／Clamp。
3. **材質**（`RedCircleStrike.mat`，沿用同一份，`YuanpeiAttacks.strikeFlipbookMaterial` 的參照不用動）：`_Cols`/`_Rows` 6→8，橢圓遮罩參數依新素材的螢幕座標重新量測：`_MaskCenterY` 0.5→0.47、`_MaskRadius` 0.5→0.41、`_MaskAspectY` 1→1.37、`_MaskSoft` 0.09→0.10。
4. **`YuanpeiAttacks`**（`yuanpei_LogoSky`，Map_School.unity）：`strikeFlipbookCols/Rows` 6→8、`strikeFlipbookFrames` 36→64、`strikeFlipbookImpactFraction` 0.55→0.68。
5. 來源影片歸檔 `Assets/_Project/VFX/Boss/Source/RedCircleStrike2Source.mp4`（僅留底、執行期不播）；舊版 `RedCircleStrike_Flip.png`（6×6）留在磁碟未刪，材質已切到新貼圖不再引用它。

**驗證**：Edit-Mode 建暫時 Quad + Camera 套用新材質，分別截圖 charge 幀（30）與 burst 幀（45）——橢圓遮罩乾淨、沒有矩形地板破邊、火柱與裂地細節清楚，截完即刪除暫時物件，沒有動到任何場景。Console 無錯。這次只動了美術資產＋材質參數＋場景欄位值，沒有改任何 C# 腳本，不需要重跑 EditMode 測試。改 `RedCircleStrike.mat` + `RedCircleStrike2_Atlas.png`（新）+ `Source/RedCircleStrike2Source.mp4`（新）+ `Map_School.unity`（YuanpeiAttacks 欄位）+ `Docs/ASSET_LICENSES.md`。

### 追加94 續 134（2026-09-04）— 直線衝刺頂穿圍牆 + boss 戰觸發後封閉競技場

使用者兩點：1. 發現 boss 有機會因為直線衝刺衝出圍牆之外。2. boss 戰觸發時，把地圖變成封閉的立方體，玩家和 boss 不可用非正常手段離開此地圖（玩家與傳送門對話、玩家被下壓秒殺除外）。

**1. 衝刺頂穿圍牆真因**：`BodyCharge`/`OrbitDash` 的牆壁檢查只用 `Physics.SphereCast(..., chargeCrashMask, ...)`——`chargeCrashMask` 只覆蓋 3 個專用的 `yuanpei_*_Collision` 代理（給 boss 自己撞牆暈眩用），**不含地圖一般的圍牆**。只要衝刺方向剛好對著沒鋪代理碰撞體的圍牆縫隙，`maxDist`（BodyCharge 預設 15m）/ 硬編碼 `16f`（OrbitDash）就會讓 boss 直接衝穿出去。

- `YuanpeiAttacks.cs` 新增 `ClampToArena(start, flatDir, requestedDist)`：射線 vs 競技場圓（`_cfg.arenaCenter`/`arenaRadius`）求交點，扣掉 1.2m margin，回傳 `[3, requestedDist]` 內的安全距離——跟實際牆壁碰撞體完全無關，直接用戰鬥自己定義的圓形邊界卡死，地圖圍牆有沒有縫都不受影響。
- `BodyCharge`：方向鎖定後 `maxDist = ClampToArena(start, flatDir, maxDist)`，餘下的 while 迴圈距離判斷自動吃到裁切後的值。
- `OrbitDash`：新增 `dashMaxDist` 區域變數（預設 16f 只當上限），衝刺方向鎖定的當下 `dashMaxDist = ClampToArena(transform.position, dashFlat, 16f)`，取代原本迴圈條件與 `ChargePathTelegraph` 呼叫裡的硬編碼 `16f`。

**2. boss 戰觸發後封閉競技場**：`YuanpeiEncounter.StartEncounter()` 末端呼叫新的 `SpawnArenaLockdown()`——以 `combatCenter` 為中心、`boss.Config.arenaRadius + lockdownMargin`（預設 2.5m）為半徑，蓋出 4 面牆 + 天花板 + 地板共 6 片純 collider 面板（`YuanpeiArenaLockdown` 底下的 `ArenaWall_North/South/East/West/Ceiling/Floor`），Y span 從 -8 到 45（涵蓋飛行系統可能的高度）。每片沿用「本地」圍牆既有的雙 collider 慣例：一顆實心 `BoxCollider`（擋 CharacterController / 載具）+ 一顆外擴 0.6m 的 trigger `BoxCollider` + 掛既有的 `BoundaryBlockEffect`（runtime 元件，不用透過 Editor-only 的 `BoundaryWallBlockEffectSetup`；`ripple` 欄位留空，null-safe）——碰到牆一樣會有既有的 `BoundaryBlockHud`（已常駐在 `GreyboxTest.unity`）畫面震動回饋，沒有另外做新的 VFX/UI 資產。牆掛在 `YuanpeiArenaLockdown`（`YuanpeiEncounter` 自己的子物件）底下，`Victory()`（銀幕 hold 結束、傳送前）與 `Defeat()`（`ResetForRematch` 之後）都呼叫 `DestroyArenaLockdown()` 拆除；場景意外卸載時子物件也會被 Unity 自動清掉，不用額外掛 `OnDestroy`。

兩個排除項天生滿足、不用特別處理：`SceneTransitionRunner` 的返回傳送與 `VoidPunt` 的秒殺下壓都是直接 `player.position` 賦值（`VoidPunt` 全程關閉 `CharacterController`），Unity 不會對停用中的 CharacterController 或非物理移動做 collider 阻擋，所以兩者原本就會直接穿過這些牆——不需要對牆加白名單，也不用在傳送/秒殺前手動先拆牆。傳送門對話走的是既有 `SceneGate` 流程（不同場景/不同觸發），跟這個鎖定 box 完全不相交。

改 `YuanpeiAttacks.cs` + `YuanpeiEncounter.cs`。編譯無錯、Console 無錯、**EditMode 303/303 綠**。牆的手感（會不會擋到既有戰鬥運鏡、有沒有卡到 boss 自己的移動)待焦點 Play 確認。

### 追加94 續 135（2026-09-04）— 封閉範圍太小：改框整個學校 60×60

使用者：「你檔的地方太小了 應該是整個學校領地60*60往上框起來」——續 134 的鎖定範圍是繞著 `combatCenter`＋`arenaRadius+2.5m`（約 13.5m 半徑）的一個小圈，不是整個「學校」場地。

**量測真實場地**（開 Map_School.unity 讀場景物件）：`學校` ground X[-30,30] Z[-145,-85]（60×60，中心 (0,-115)）；既有永久圍牆 `SchoolWall_South/East/West` 完全封死三邊，`SchoolWall_NorthLeft/Right` 只留北面一個缺口（實測邊界 X∈[-4.31,4.31]），高度只有 6m；`SchoolGate_Exit`（傳送門互動點）就坐落在這個缺口正中央 (0,0,-86)。

**改法**：`SpawnArenaLockdown()` 改成以整個學校地皮為準（新欄位 `lockdownCenterXZ` (0,-115)、`lockdownHalfX`/`lockdownHalfZ` 各 31.5、`lockdownGateGapHalfWidth` 4.31，取代舊的 `lockdownMargin`）：

- 南／東／西三面各蓋一片整跨度的高牆（Y span 仍是 -8 到 45，遠高於永久牆的 6m —— boss 衝刺/下壓飛得比 6m 高，永久牆本來就擋不住，這次連牆帶頂一起補上）。
- 北面比照永久牆缺口，切成 `NorthLeft`/`NorthRight` 兩段，缺口寬度精確對齊實測的 `SchoolGate_Exit` 位置——缺口全高度（-8 到 45）都保持開放，玩家可以正常走到 `SchoolGate_Exit` 前互動離開（符合使用者的「玩家與傳送門對話除外」），只有缺口以外的部分被封死。
- 天花板／地板一樣蓋滿整個 65×65（含牆厚 overlap）的範圍。

改 `YuanpeiEncounter.cs`。編譯無錯、Console 無錯、**EditMode 303/303 綠**。實際框住範圍、缺口是否精準對齊 `SchoolGate_Exit` 待焦點 Play 確認（走到門口試按 E）。

### 追加94 續 136（2026-09-04）— 新招式：長矛型光彈 SpearVolley（Crimson Void Spear，遠距離連續發射）

使用者提供 `長矛型光彈.zip`（Meshy AI 生成的 3D 長矛模型：銀色矛身＋紫水晶簇握把＋十字護手，98 MB FBX＋4 張貼圖）＋ `長矛型光彈-3d.mp4`（緋紅能量絲＋白色閃電核心的抽象特效參考影片，定調配色氛圍），要求設計一個「遠距離連續性發射型」的新 boss 攻擊手段。

**素材處理**：FBX 匯入 `Assets/_Project/Environment/Meshy/CrimsonVoidSpear/`。**踩坑**：原始檔名 `Meshy_AI_Crimson_Void_Spear_..._texture.fbx` 會命中 `.gitignore` 的 `Meshy_AI_*_texture.fbx` 規則（那條規則是給 >100MB 檔案排除版控用的名稱萬用字元，不看實際大小）——這顆模型只有 98MB，本來不需要排除，用 `AssetDatabase.MoveAsset` 改名成 `CrimsonVoidSpear.fbx`（同 `VoidmoonGate.fbx` 先例）逃出規則、順便保留 GUID 不斷參照。手建 URP/Lit `CrimsonVoidSpear.mat`（base+normal+metallic 貼圖，沿用 `YuanpeiLogo.mat` 同一套接法，略過 roughness）。量測模型：單一 mesh、1,347,363 頂點、世界尺寸 1.87×0.72×0.79m、長軸沿 local +X（用隔離預覽場景＋截圖確認，尖端在 +X、握把水晶簇在 -X）。

**投射物 prefab**（`Assets/_Project/VFX/Boss/CrimsonVoidSpearProjectile.prefab`）：Model 子物件套用材質、`localRotation = euler(0,-90,0)`（把 local +X 尖端轉到 root 的 +Z/forward，配合 `YuanpeiProjectile.Update()` 的 `transform.forward = _dir`）、`localScale = 64`（世界半長 ≈0.6m）。Root 掛 `CapsuleCollider`（isTrigger，沿 Z 軸）＋已烤好的 `YuanpeiProjectile` 元件。以隔離預覽場景驗證：對著世界 +X 方向「發射」後，尖端確實朝 +X（截圖確認）。

**`YuanpeiProjectile.cs` 小改**：`Launch()` 新增可選參數 `tipForwardOffset`（預設 0，orb 行為不變）——命中檢查改成在 `transform.position + transform.forward * tipForwardOffset` 做 OverlapSphere，讓細長的長矛判定發生在「矛尖」而不是幾何中心（跟 `DiscFaceHitsPlayer` 系列「命中點＝視覺前緣」的既有原則一致）。

**`YuanpeiAttacks.SpearVolley` 新攻擊**：跟既有 `ProjectileBurst`（兩波齊射、完全鎖定不追蹤）刻意做出不同的遠程節奏——原地連續發射 9 發，每發都各自重新瞄準發射當下的玩家位置＋0.4s 短暫追蹤（`homingStrength` 1.1），逼玩家持續走位而不是抓兩個時間點閃避。前搖用新的 `SpearMuzzleGlow`（單顆核心緋紅→虛空紫脈動點亮，刻意不用 `MuzzleCharge`——那是 ProjectileBurst 的招牌螺旋聚粒視覺，用同一招會分不清是哪個攻擊要來了）。每發都動態加 `TrailRenderer`（緋紅→虛空紫漸層）＋一顆小 `Point Light`。新增專屬色系欄位 `spearGlowColor`（緋紅）/`spearCoreColor`（虛空紫），不動用既有 `castColor`/`burstColor`（那組是其他招式共用的暖金/橘色系）。`spearProjectilePrefab` 欄位留空時會退回一個灰盒 Capsule，攻擊仍可運作。

**資料/排程**：`YuanpeiAttackId` 新增 `SpearVolley`（enum 索引 9，接在 `OrbitDash` 後面，不影響既有索引）。`YuanpeiScheduler.Matches()` 新增情境：玩家距離 ≥13m 時加權（跟 `ChargeLine` 的「≥9m 加權」區隔開，SpearVolley 專屬「更遠」的距離帶）。新建 `YuanpeiAttack_SpearVolley.asset`（`minRange`12／`maxRange`20／`isMajorHazard`=true／`cooldownSeconds`4.0／`healthDamage`11／`count`9／`number1`(速度)20／`number2`(發射間隔)0.22／`number3`(追蹤強度)1.1），加進 `yuanpei_LogoSky`（Map_School.unity）的 `attackPool`（8→9 個招式），`YuanpeiAttacks.spearProjectilePrefab` 欄位接上新 prefab。

**測試**：`YuanpeiBossLogicTests` 新增 2 個（遠距離時選中 SpearVolley／太近時被 minRange 排除）。

改 `YuanpeiAttackDef.cs` + `YuanpeiAttacks.cs` + `YuanpeiProjectile.cs` + `YuanpeiScheduler.cs` + `YuanpeiBossLogicTests.cs` + `Map_School.unity`（attackPool + spearProjectilePrefab 欄位）+ 新增 `CrimsonVoidSpear.mat`/`CrimsonVoidSpearProjectile.prefab`/`YuanpeiAttack_SpearVolley.asset` + `Docs/ASSET_LICENSES.md`。編譯無錯、Console 無錯、**EditMode 305/305 綠**（+2 新測試）。實際發射手感、追蹤強度、傷害量待焦點 Play 確認。

### 追加94 續 137（2026-09-04）— SpearVolley 調校：觸發距離 13→8m、權重稍微提高

使用者 Play 回饋：「13m 太長了 8m，並且權重稍微提高」。

- `YuanpeiScheduler.Matches()` 的 SpearVolley 情境加權門檻 13→8m。
- `YuanpeiAttack_SpearVolley.asset`：`minRange` 12→8（跟情境門檻對齊，否則 8m 那個門檻永遠碰不到——原本 12 的硬性下限會先把 8-12m 這段距離擋在候選名單外）、`baseWeight` 1.5→2.0（稍微提高，介於 ProjectileBurst 的 2.5 跟 FocusLaser 的 1 之間，往上調一階）。
- `YuanpeiBossLogicTests.cs`：2 個 SpearVolley 測試的 `Def()` minRange 參數同步 12→8（測試本身的距離判斷邏輯不受影響，只是讓測試資料跟正式數值對齊）。

改 `YuanpeiScheduler.cs` + `YuanpeiAttack_SpearVolley.asset` + `YuanpeiBossLogicTests.cs`。編譯無錯、Console 無錯、**EditMode 305/305 綠**。

### 追加94 續 138（2026-09-04）— SpearVolley 太好躲 + boss 圓盤滲入地板

使用者：「這招太容易被玩家躲開，然後boss很長有一部份身體滲入地板」。兩個獨立問題。

**1. SpearVolley 太好躲**：順便把 `SpearVolley()` 裡違反專案規則 7（平衡數值不得寫死在腳本）的兩個 `const`（`homingSeconds`/`hitRadius`）扶正成 `YuanpeiAttackDef` 欄位——`YuanpeiAttackDef` 新增 `number4`/`number5`（沿用 `number1-3` 的命名慣例，給以後其他招式也能用）。`YuanpeiAttack_SpearVolley.asset` 調校：`number1`(速度) 20→23、`number2`(發射間隔) 0.22→0.15（齊發更密，走位間隔變小）、`number3`(追蹤強度) 1.1→1.9、`number4`(追蹤秒數，新) 0.7（原本硬編碼 0.4）、`number5`(命中半徑，新) 0.36（原本硬編碼 0.28）。

**2. boss 圓盤滲入地板**：查證後發現問題比字面描述更具體——boss 視覺（`VisualRoot`）借用元培校徽天空地標模型（idle 時 scale 1700、世界直徑 ~32m），`YuanpeiBoss.IntroRoutine()` 早就有把它縮小成戰鬥尺寸的機制（`combatVisualScaleFraction`，code 預設 0.28，場景也是 0.28）——縮小後直徑 ~9m，但戰鬥懸浮高度 `hoverHeight` 只有 2.6（圓心離地僅 3.1m），縮小後的圓盤半高（~4.43m）比懸浮高度還高，底部穩定滲入地板約 1.7-2.2m（含 bob 擺動的低點）。問過使用者「戰鬥時縮小圓盤 vs 大幅拉高懸浮高度」，選了縮小圓盤——但單靠縮小圓盤到能完全塞進現有 2.6m 懸浮高度以內（需要 F≤0.14，直徑僅 ~4.6m）會比現有 `BodyCollider` 判定球（直徑 7.2m）還小，視覺會比判定範圍還小、打擊感很怪，所以額外小幅拉高懸浮高度配合（沒有完全照原本回答「懸浮高度不用重調」，這裡誠實記一筆）：

- `YuanpeiBoss.combatVisualScaleFraction`（Map_School.unity 場景欄位）0.28→**0.24**（縮小後直徑 ~7.76m，仍比 7.2m 判定球大一點，比例正常）
- `YuanpeiBossConfig.hoverHeight`（`YuanpeiBossConfig.asset`）2.6→**4.7**（含 0.35 bob 低點後，圓盤底部離地約 +0.56m 安全距離）

這是與眾多攻擊共用的全域欄位，但衝撞類招式（BodyCharge/ChargeLine/OrbitDash/ChargeCrush）都會 `SuspendHover`/`SuspendYClamp` 自行接管 Y 軸，遠程類（ProjectileBurst/FocusLaser/SpearVolley）只是瞄準玩家發射、不依賴固定的懸浮高度數字，理論上都不受影響，但這是全域數值，**其他招式的手感、F 處決運鏡、鎖定視角待使用者焦點 Play 逐一確認沒有被這次拉高的懸浮高度意外牽動**。

改 `YuanpeiAttackDef.cs` + `YuanpeiAttacks.cs`（SpearVolley 讀 number4/5）+ `YuanpeiAttack_SpearVolley.asset` + `Map_School.unity`（combatVisualScaleFraction）+ `YuanpeiBossConfig.asset`（hoverHeight）。編譯無錯、Console 無錯（Console 出現的 2 個 `CubismRenderController.UpdateBlendColors` IndexOutOfRangeException 跟本次改動無關——是 Live2D 範例模型既有的、與 Yuanpei boss 系統毫無關聯的例外，未追查）、**EditMode 305/305 綠**。

### 追加94 續 139（2026-09-04）— 續138 調完還是滲地板：改用即時量測取代手調常數

使用者：「調整之後仍然有一部份是陷入地板，請讓他稍微浮在空中；使用衝撞攻擊時就讓圓的外弧切線地板」。

續138 靠手算 AABB 尺寸調 `hoverHeight`/`combatVisualScaleFraction`，估出來的圓盤半高（~3.79m）跟懸浮高度（4.7m）搭配 bob 擺動後理論上只有 0.4-0.05m 安全距離——太緊，稍有誤差就穿模，而且衝撞攻擊（BodyCharge/ChargeLine/OrbitDash）側身時用的 `skimHeight` 完全是另一個獨立的舊手動常數（1.1），跟圓盤縮小後的實際半徑毫無關係，同樣會滲地板或懸空過高。這次不再猜數字，改成**即時量測實際渲染範圍**：

- `YuanpeiBoss.VisualBottomOffset()`（新公開方法）：取 `visualRoot` 目前所有 Renderer 的合併世界座標 bounds，回傳「pivot 到 bounds.min.y 的距離」——這個值會自動反映**當下**的縮放與旋轉（懸浮的扁平姿態、衝撞的側身姿態都各自量到正確答案），不用每次改模型/縮放比例就要重算一次常數。
- `HoldHover()`：新增地板安全下限 `minY = floor + VisualBottomOffset() + groundClearanceMargin`，`targetY` 低於這條線時夾回來——不管 `hoverHeight`/bob 怎麼擺動，圓盤最低點永遠保證至少有 `groundClearanceMargin`（新欄位，`YuanpeiBossConfig.asset`，預設 0.5m）的浮空間隙，滿足「稍微浮在空中」。
- `BodyCharge`/`OrbitDash`：原本寫死的 `skimHeight`（1.1）改成側身姿態鎖定後（`SlerpDiscInto(...edgeFirst:true)` / `FaceDiscSideAlong` 之後）當場呼叫 `_boss.VisualBottomOffset()`，**不加安全間隙**（使用者明確要求「外弧切線地板」＝剛好碰到，不是浮空也不是插入）。`ChargeCrush` 沒有動——它的下壓是整個圓盤平躺貼地（`FaceDiscAlong(Vector3.down)`），扁平姿態下 pivot 到地面本來就只差半個厚度（近乎 0），跟這次「側身圓弧」的情境是不同的幾何問題，原本的 0.15/0.6 手動值沒有這個 bug。

Console 驗證：暫時把 boss 縮到戰鬥比例（scale ×0.24）＋懸浮座標 (floor+hoverHeight)，呼叫 `VisualBottomOffset()` 量到 3.80m（跟續138手算的 3.79m 幾乎一致，證實手算沒錯——問題是安全間隙太薄，不是估算錯誤），套新公式後穩定狀態安全間隙 0.40m、bob 最低點靠新的 `HoldHover` 夾線強制頂到 0.5m——不會再看誤差吃掉間隙。

改 `YuanpeiBoss.cs`（`VisualBottomOffset()` + `HoldHover()` 夾線）+ `YuanpeiBossConfig.cs`（`groundClearanceMargin` 欄位）+ `YuanpeiAttacks.cs`（`BodyCharge`/`OrbitDash` 的 `skimHeight` 改即時量測）+ `YuanpeiBossConfig.asset`。編譯無錯、Console 無錯、**EditMode 305/305 綠**。實際懸浮/衝撞貼地效果待使用者焦點 Play 確認。

### 追加94 續 140（2026-09-05）— 新開發者工具：Yuanpei Attack Debug Mode（F8）

使用者：「有沒有一種開發者模式 可讓我讓清楚看到boss的每一種攻擊手段的機制 外觀 ui 範圍等等 專門用來優化美術系統的」。

`yuanpei_LogoSky` 沒有 Animator 驅動的攻擊（跟武士/屁孩王不同，全部走 `YuanpeiAttacks` 裡的程序化 coroutine），所以既有的 `BossAnimationDebugMode`（F7，播 Animator state）架構不能直接套用。新建一套同精神、但機制不同的獨立工具：

**`YuanpeiAttackDebugMode.cs`**（`_Project/Game/Debug/`，跟其他 dev overlay 一樣包 `#if UNITY_EDITOR || DEVELOPMENT_BUILD`）：
- **F8** 切換：進入時 `boss.enabled = false`（直接停掉 `YuanpeiBoss.Update()`——boss 自己的排程/懸浮/選招全部停止，改成完全手動控制），玩家保留完整操控（不像武士工具需要借守望者鏡頭——這裡的重點就是要能自己走位到不同距離觀察，玩家自己的攝影機本來就夠用，沒有另外做鏡頭）、玩家短暫無敵（`Health.SetInvulnerable`，測試時不會真的被打死）。
- **數字鍵 1-9** 直接對應 boss `attackPool` 裡的 9 個招式，跳過 scheduler 的冷卻/距離/能量/連續判定，直接呼叫 `YuanpeiAttacks.Run(def, player, boss, ...)`——同一招可以無限重複觸發，方便逐幀觀察。**R** 重放上一招、**P** 暫停（`Time.timeScale=0`）、**-/=** 調慢/調快（`Time.timeScale`，coroutine 的 `WaitForSeconds`/`Time.deltaTime` 都會跟著變speed，等於全域慢動作）。
- **G** 切換範圍圈：白圈＝競技場邊界（`arenaCenter`/`arenaRadius`），綠圈＝目前選定招式的 `minRange`，紅圈＝`maxRange`（`LineRenderer` 即時畫在 boss 位置周圍）——這是「範圍」需求的具體回應，讓使用者不用看數值就知道要站多遠測試。
- 左上角 `OnGUI` 面板列出全部 9 招（id／顯示名稱／範圍／是否 major hazard），目前選中的標星號。
- **安全網**：`ChargeCrush` 的下壓秒殺（`VoidPunt`）正常只有靠 `YuanpeiEncounter.Defeat()`（死亡→傳送回起點）才會把掉進虛空的玩家撿回來——這個工具完全繞過 `YuanpeiEncounter`，不會走那條路。加了保險：每招放完後檢查玩家 Y 是否掉到地板以下太多（預設 8m），有的話自動把玩家傳送回 boss 旁邊、順便修正可能還沒復原的 `CharacterController`。
- Exit 時全部還原：boss 恢復 `enabled=true`（FSM 正常接手）、玩家無敵解除、HUD 顯示狀態還原、範圍圈清除。

**`YuanpeiAttackDebugSetup.cs`**（Editor 選單 `Tools/Live2DAction/[Debug] Setup Yuanpei Attack Debug Mode`，比照 `BossAnimationDebugSetup.cs` 的慣例）：在目前已載入的場景裡找 `YuanpeiBoss`，把新的 `YuanpeiAttackDebugMode` GameObject 建在 boss 所在的**同一個場景**（Map_School，不是 GreyboxTest，讓它跟著地圖串流一起載入/卸載）並接好 `boss`/`attacks`/`hud` 三個欄位。已在 Map_School.unity 跑過一次，`pool size=9` 全部接上。

`YuanpeiBoss.cs` 額外加一個小的公開存取器 `AttackPool`（原本 `attackPool` 是純 private serialized list，debug 工具需要在執行期讀取才知道有哪些招式可以按數字鍵觸發）。

改 `YuanpeiBoss.cs`（`AttackPool` 存取器）+ 新增 `YuanpeiAttackDebugMode.cs` + `YuanpeiAttackDebugSetup.cs` + `Map_School.unity`（新 GameObject）。編譯無錯、Console 無錯、**EditMode 305/305 綠**（純新增工具，沒動任何戰鬥邏輯，不需要新測試）。工具本身待使用者實際進 Play 用 F8 試按確認。

### 追加94 續 141（2026-09-05）— 續140 工具的 3 點回饋：稻草人目標 + 可移動 + 暫停自由視角

使用者：「1. 如果boss都站著不動的話 衝撞類攻擊其實無法觀察 2. boss現在瞄準玩家位置進行攻擊也很難觀察，請製作一個稻草人當目標對象，並且要有手段可以移動稻草人位置 3. 使用p按鍵暫停時，請讓玩家視角不受限制 這樣才能多角度觀察」。

**1+2. 稻草人目標**：新增 `YuanpeiDebugTargetDummy`——純原生幾何拼出來的稻草人剪影（木樁＋稻草色 Capsule 身體＋Sphere 頭＋交叉 Arms，全部用 `MaterialPropertyBlock` 上色，沒有另外做/匯入美術資產，反正只是除錯用的靶）＋一顆真的 `CapsuleCollider`（跟玩家本體同尺寸，讓攻擊原本「檢查 `col.transform.root == 目標`」的命中判定邏輯照樣能用）＋一顆 `Health` 元件（承受傷害不會報錯，順便能看到扣血）。所有攻擊現在瞄準**稻草人**（`attacks.Run(def, _dummy.transform, boss, ...)`），不再瞄準真玩家——真玩家完全不會被除錯攻擊打到，稻草人可以固定站在你想要的位置，不會因為玩家自己在動而讓攻擊軌跡難以觀察。

- **方向鍵**：移動稻草人（世界座標 XZ，`Time.unscaledDeltaTime` 驅動，暫停時也能移動）。
- **Shift+方向鍵**：改成移動 boss 本體——這樣就能自己拉開 boss 與稻草人的距離，衝撞類攻擊（BodyCharge/ChargeLine/OrbitDash）才有真正的助跑空間可以觀察到完整衝刺過程（回應第 1 點）。
- **T**：稻草人瞬移到玩家目前所在位置（快速把靶放到你站的地方）。
- 稻草人第一次建立時預設放在 boss 前方 10m（地面貼齊，`Physics.Raycast` 找地板），之後移到哪就留在哪，不會每次開關 F8 就重置。
- `ChargeCrush` 的秒殺下壓安全網（掉出地板自動撿回）改成檢查**稻草人**的 Y 座標（原本檢查玩家，現在攻擊已經不打玩家了）。

**3. 暫停時自由視角**：真因是暫停用 `Time.timeScale = 0` 會讓 `ThirdPersonCameraController` 的視角平滑轉向也一起停格（它的旋轉內插讀 `Time.deltaTime`，timescale 0 時這個值等於 0，滑鼠怎麼動視角都不會轉）。**沒有動 `ThirdPersonCameraController` 本身**（CLAUDE.md 手動調校值权威原則，這條路線風險較高）——改成在除錯工具自己內部做一個獨立、暫停時才啟用的自由視角/飛行鏡頭：暫停瞬間停用 `ThirdPersonCameraController`、之後用滑鼠原始位移（不受 timescale 影響）直接轉 `Camera.main` 的角度、`WASD`＋`Space`/`Ctrl` 用 unscaled time 飛行移動；取消暫停時把控制權還給 `ThirdPersonCameraController`。`WASD` 平常是玩家移動鍵，但暫停時角色本來就因為 `Time.deltaTime=0` 動不了，借來當飛行鏡頭按鍵沒有衝突。

改 `YuanpeiAttackDebugMode.cs`（稻草人建置/移動/瞄準改向、`SetFreeLook`/`DriveFreeLook` 自由鏡頭）。編譯無錯、Console 無錯、**EditMode 305/305 綠**（純工具改動不影響戰鬥邏輯）。三點回饋的實際手感待使用者用 F8 重新測試。

### 追加94 續 142（2026-09-05）— 續141 沒修好：稻草人自身碰撞體污染了地板偵測

使用者：「1. 必須讓稻草人可以移動位置 2. 必須可以讓衝撞類攻擊可以完整對稻草人演示 位移 紅圈預警等等」——回去查程式碼，抓到兩個具體、確定的真因（不是猜測）：

**1. 稻草人「移不動」的真因**：`SnapToGround(t)` 每次移動後都會從稻草人自己目前的位置往上 40m、往下打一條 Raycast 找地板——但這條射線會先打到**稻草人自己的 `CapsuleCollider`**（續141 特地加上去、讓攻擊命中判定能用的那顆），`Physics.Raycast` 只回傳第一個命中，所以每次移動後 Y 座標都會立刻被吸回稻草人自己的身高（~1.8m），而不是真正的地板——等於稻草人的水平位移雖然有生效，但垂直高度每幀都被自己的碰撞體「拉回原地」，看起來就像卡住/移不動。改成 `Physics.RaycastAll` 排除 `collider.transform.root == t`（正在被貼地的那個物件自己）後取第一個命中。

**2. 衝撞類攻擊「紅圈預警、位移都出不來」的真因**：`YuanpeiAttacks.ProjectToGround(Vector3 p)` 原本只排除**真玩家**的碰撞體（`PlayerInputProvider`／`CharacterController`）跟 boss 自己，完全不知道「稻草人」這種東西存在。稻草人有真的 `CapsuleCollider`（同上，命中判定需要），所以任何「在目標角色所在 XZ 位置往下找地板」的呼叫（`ChargeCrush` 的紅色警示標記 `MakeGroundMarker`／鎖定下壓點 `lockGround`、`BodyCharge`/`OrbitDash` 的貼地高度 `groundY`/`floorY`、`LightningMark` 的紅圈落點）全部打到稻草人自己的身體頂端，不是地板——警示標記位置跟衝撞的貼地高度全部算錯，衝撞類攻擊自然「沒有位移、紅圈預警出不來」（游到錯誤高度、標記位置飄在稻草人頭上等等）。改法：`ProjectToGround` 新增可選參數 `Transform ignoreRoot`，呼叫端在對著 `player`（現在是稻草人）取地板高度時一律多傳 `player` 進去排除；boss 自己位置的呼叫（`ChargePathTelegraph` 的地板起點）不受影響，本來就有排除 boss 自己。

**順便**：方向鍵移動稻草人／Shift+方向鍵移動 boss 改成**跟隨攝影機方向**（原本是死板的世界座標軸，「上」不一定是螢幕上的「前方」，容易讓人誤以為沒有在動）；稻草人頭頂加一根高 3m 的亮綠色柱狀 beacon，遠遠的、任何角度都看得到它現在站在哪。

改 `YuanpeiAttacks.cs`（`ProjectToGround` 加 `ignoreRoot` 參數＋ 6 個呼叫點）+ `YuanpeiAttackDebugMode.cs`（`SnapToGround` 改用 RaycastAll 排除自己、移動改跟攝影機方向、稻草人加 beacon）。編譯無錯、Console 無錯、**EditMode 305/305 綠**。這次是真因層級的修正（不是又一輪猜測），但仍待使用者用 F8 實測確認。

### 追加94 續 143（2026-09-05）— 稻草人在高空：真因是 boss 從沒被放到戰鬥位置過

使用者：「你需要提供一種方法移動稻草人 並且現在稻草人在非常高的高空 請移動到地面」。

**真因**：`yuanpei_LogoSky` 在場景裡的**預設／閒置狀態就是掛在天空的巨大校徽地標**（`(0, 42, -132)`、scale 1700，見追加94 續86）——平常要靠正式觸發 boss 戰、跑 `YuanpeiBoss.IntroRoutine()`（2.6 秒降落＋縮小演出）才會落到地面戰鬥尺寸。這個除錯工具原本只有「凍結 boss 現在的 FSM」，**從沒讓 boss 真的進入戰鬥姿態**——如果使用者是直接走進 Map_School 就按 F8（沒有先正常觸發一次 boss 戰），boss 當下還停在天空巨大地標的位置／尺寸，稻草人預設又是「boss 前方 10m」，於是也跟著生在天上。

**修法**：`YuanpeiBoss.cs` 新增公開方法 `SnapToCombatPose(Vector3 center)`——瞬間（不跑 2.6 秒演出、不動 FSM 狀態）把 boss 放到戰鬥位置＋縮小到戰鬥尺寸，本質上是 `IntroRoutine()` 的無動畫版本，`YuanpeiAttackDebugMode.Enter()` 進入時一律先呼叫這個，boss 不會再卡在天空巨大地標狀態。稻草人預設出生點改成相對 `arenaCenter`（固定、已知的地面座標），不再依賴 boss 當下的任意朝向。

**另外加了兩層保險**（不只是「應該不會再發生」，而是就算又有類似狀況也有辦法自救）：
- **Home 鍵**：新增「無條件重置稻草人到 boss 旁邊地面」——完全不靠 Raycast（不會有任何物理判定失敗的可能），永遠可用的逃生手段。
- `SnapToGround`（貼地用的 Raycast）現在如果真的找不到地板（或打到 boss 自己的碰撞體），會 fallback 回 `arenaCenter.y`（關卡設計時就定好的、可信賴的地面高度），不會放著錯誤高度不管。

改 `YuanpeiBoss.cs`（`SnapToCombatPose`）+ `YuanpeiAttackDebugMode.cs`（Enter() 呼叫、稻草人預設出生點、`ResetDummyPosition()`／Home 鍵、`SnapToGround` fallback）。編譯無錯、Console 無錯、**EditMode 305/305 綠**。

（本次未 commit，2026-09-05 新對話透過 MCP 連上 Unity 直接進 Play 用反射呼叫 `Enter()`/`SnapToGround()` 驗證：boss 從天空的 `(0,42,-132)` 正確落到戰鬥位置 `(-2,5.2,-105)`，稻草人生在地面 `(-2,0.5,-100)`，水平移動後 Y 仍穩定在地面高度——續 143 的修法確認有效。）

### 追加94 續 144（2026-09-05）— 稻草人加上垂直移動

使用者：「稻草人只能水平移動不能上下移動」——續 141-143 的方向鍵只驅動 XZ，從沒給過 Y 軸控制。

**修法**：新增 `verticalUpKey`/`verticalDownKey`（預設 PageUp/PageDown）：不按 Shift 時上下移動稻草人，按 Shift 時上下移動 boss（跟既有「不按 Shift＝稻草人、Shift＝boss」的方向鍵慣例一致）。

垂直位移獨立於既有的「水平移動後自動貼地」邏輯之外——新增 `_dummyHeightOffset` 累積玩家手動加的垂直位移，`SnapToGround(t, heightOffset)` 新增可選參數，水平移動後重新貼地時把這個 offset 疊加在算出來的地板高度上，而不是每次都蓋掉。否則水平＋垂直交替使用時，下一次按方向鍵水平移動就會把手動抬高的稻草人立刻拉回貼地高度，等於垂直調整形同虛設。`T`（稻草人瞬移到玩家）跟 Home（無條件重置）這兩個「已知安全位置」的重置點會把 offset 歸零，避免殘留的垂直偏移污染這些原本該是「乾淨已知位置」的操作。

**驗證**：Play 模式下用反射直接呼叫 `SnapToGround` 確認：手動疊加 3.5m 高度後，水平移動＋重新貼地（offset=3.5）維持在抬高的 Y；同一位置用 offset=0 重新貼地則正確掉回地板高度——證實 offset 疊加邏輯正確,不會被水平移動的貼地覆寫掉。

改 `YuanpeiAttackDebugMode.cs`（`verticalUpKey`/`verticalDownKey` 欄位、`_dummyHeightOffset`、`HandleReposition` 垂直分支、`SnapToGround` 加 `heightOffset` 參數、`T`／`ResetDummyPosition` 歸零 offset、log／`OnGUI` 說明文字更新）。編譯無錯、Console 無錯（`refresh_unity` 後只有既有無關警告）。純工具改動不影響戰鬥邏輯，不需要新測試。

### 追加94 續 145（2026-09-05）— 稻草人預設重生點改成錨定元培廣場

使用者：「可不可以直接稻草人座標拉下來 目前他位於高空 讓他在元培廣場就行 參照物件做逼近」。

`ResetDummyPosition()`（F8 首次建立稻草人／Home 鍵）原本的參考點是 `boss.Config.arenaCenter`——一個 ScriptableObject 欄位，數值上跟真正的地面位置一致（Play 模式驗證過落點都在 Y=0.5 的「學校」地面），但不是使用者能直接在場景裡指認的東西。改成 `GameObject.Find("yuanpei_QuietCampusPlaza")`：找得到就用這個實際場景物件的 XZ 當基準（+ 5m forward，Y 一樣交給既有的 `SnapToGround` 貼地判定，跟物件本身的 pivot Y 無關），找不到才退回舊的 `arenaCenter` 當備援。

**Play 驗證**：反射呼叫 `Enter()` → 稻草人生在 `(0.08, 0.50, -109.44)`，X/Z 對齊元培廣場中心 `(0.08, ?, -114.44)` + 5m forward，Y 落在真實地面 0.5——不是高空。

改 `YuanpeiAttackDebugMode.cs`（`ResetDummyPosition` 改用 plaza 物件當參考點，找不到時 log 提示走 fallback）。編譯無錯、Console 無錯。純工具改動，不需要新測試。

### 追加94 續 146（2026-09-05）— 稻草人真正卡在天空的真因：撞到「已觸發過的真實戰鬥」留下的競技場封閉天花板

使用者在真正的 Play session 裡回報「我停在play mode 你幫我看看稻草人還在天空是怎麼回事 我在boss旁邊」——這次直接連上正在跑的 Play session 現場診斷（不是重新猜），從 `SnapToGround` 實際跑的那條 raycast 抓到真因：

**真因**：使用者在按 F8 之前，顯然先正常觸發過一次真的 boss 戰（走進 `BossRoomTrigger`），`YuanpeiEncounter.StartEncounter()` 因此蓋出了續 134/135 的「競技場封閉」——6 片實心 `BoxCollider`（南/東/西/南北缺口/天花板/地板），天花板 `ArenaWall_Ceiling` 在 Y≈45-46。這個戰鬥沒有正常跑完 `Victory()`/`Defeat()`（沒被拆除），使用者接著按 F8 進入這個除錯工具——**這個工具完全繞過 `YuanpeiEncounter`**，不知道場上還立著這片天花板。`SnapToGround`/`ProjectToGround` 的 raycast 排除清單只排除了「自己」跟「boss」，從沒排除過 `YuanpeiEncounter` 這片封閉牆——於是從稻草人位置往上打的 raycast，往下打到的第一個東西就是這片天花板（Y=46），稻草人就被「貼地」貼到天花板上，讀起來像是莫名其妙卡在高空。

實測現場抓到的 raycast 結果印證：`hit[1]: ArenaWall_Ceiling (root=YuanpeiEncounter) y=46`（真正的地面 `學校` 在 `hit[2]`，y=0.5，天花板排在地板前面先被打到）。

**修法**：`YuanpeiAttackDebugMode.SnapToGround` 與 `YuanpeiAttacks.ProjectToGround` 都新增一條排除規則——`col.GetComponentInParent<YuanpeiEncounter>() != null` 就跳過，不當作地板。兩處都改（不只除錯工具那邊）：`ProjectToGround` 是真實 boss 戰攻擊（ChargeCrush 警示標記、BodyCharge/OrbitDash 貼地高度）也在用的共用方法，同一個「天花板被誤判成地板」的 bug 理論上也能在真實戰鬥中發生（例如戰鬥途中 boss 飛得比封閉天花板還低時），一併修掉。

**現場收尾**：改完後 Console 出現的一批 `NullReferenceException`/`ArgumentNullException("dest")`（`YuanpeiBossHUD`/`PortalVideoSurface`/`PlayerGuardAnimatorLink`）是 Unity 編輯期間重新編譯觸發的 domain reload 造成的過渡期雜訊，跟這次改動的檔案無關（棧軌跡對不上）。這次的 domain reload 也让 Unity 自動重啟了那個 Play session（`isPlaying` 全程維持 `true`，但執行期建立的稻草人物件被清掉、F8 狀態被重置回 `Active=false`），順便造成使用者原本卡在天空的那個稻草人實例本身已經不在了——不是修好了舊的那個，是舊的那個連同整個 Play session 一起被 domain reload 重置掉了。已確認場上目前沒有殘留的 `YuanpeiArenaLockdown`（域重載時剛好沒有真實戰鬥在進行）。使用者接下來直接重新按 F8 即可用到新邏輯，理論上不會再卡天花板。

改 `YuanpeiAttackDebugMode.cs`（`SnapToGround` 排除 `YuanpeiEncounter`）+ `YuanpeiAttacks.cs`（`ProjectToGround` 排除 `YuanpeiEncounter`）。編譯無錯、Console 無新增與本次改動相關的錯誤。

### 追加94 續 147-149（2026-09-05）— 稻草人無限血量／記住上次位置、boss 動作卡玩家鏡頭

使用者一次提 4 點：1. 稻草人必須無限血量 2. boss 第5/6項技能沒有完整展示 3. 記住上次稻草人設定位置 4. 有時使用 boss 動作時玩家視角會改變且無法再控制。逐項處理：

**1. 稻草人無限血量（續 147）**：`BuildDummy()` 幫稻草人掛的 `Health` 元件原本沒設無敵，改成掛上後立刻 `SetInvulnerable(this, true)`——它是重複測試用的靶，不該真的死亡（死亡可能觸發攻擊的死亡反應邏輯，或單純扣血歸零後失去當靶的意義）。

**3. 記住上次稻草人位置（續 149）**：新增 `PlayerPrefs` 存讀（`YuanpeiDebugDummy.X/Y/Z/HeightOffset`）——方向鍵、PageUp/PageDown、T、Home 這些會移動稻草人的操作都順便存一次；`Enter()` 建立全新稻草人時（`freshDummy`）先試著讀存檔位置，讀不到才照舊退回元培廣場預設點。這樣不只同一個 Play session 內的 F8 開關會記得，連續開新的 Play session 也會記得上次放在哪。

**4. boss 動作卡玩家鏡頭（續 148）**：真因——`ChargeCrush` 命中判定為真時會跑 `CrushEjectCam`，這個 coroutine 會關掉 `ThirdPersonCameraController` 給運鏡用，設計上是靠 `YuanpeiEncounter.Defeat()`（死亡畫面＋傳送完之後）才重新打開。這個除錯工具完全繞過 `YuanpeiEncounter`，所以只要 F8 底下對著稻草人開出一次「命中」的 ChargeCrush，玩家攝影機就會被關掉且永遠沒人把它打開——完全對應使用者說的「有時使用boss動作時 玩家視角會改變且無法再控制」。修法：不去逐招堵（以後可能還有別的招式會關攝影機），改成在 `Update()` 加一條自我修復——只要目前不是工具自己的暫停自由視角（`_paused`）在佔用鏡頭，每一幀都強制確保 `ThirdPersonCameraController.enabled = true`。

**2. boss 第5/6項技能沒有完整展示（調查中，需要使用者現場確認）**：查證 attackPool 順序，數字鍵 5＝index 4＝`BodyCharge`（肉身衝撞，range 5-12，n1速度18/n2距離15）、數字鍵 6＝index 5＝`ChargeLine`（長距離高速直線衝，range 8-20，n1速度28/n2距離24）——兩者都是共用同一段衝撞邏輯的長距離招式，實際能跑多遠受兩件事同時限制：(a) `YuanpeiBossConfig.arenaRadius` 目前是 **11**（競技場只有 22m 直徑），`ClampToArena` 一定會把任何衝刺距離砍到頂多這個範圍內，`ChargeLine` 設計上「24m」的最大值本來就不可能在這個競技場完整跑滿；(b) F8 預設的稻草人／boss 距離只有 ~4.9m，比 `ChargeLine` 自己的 `minRange`(8) 還近，衝撞前雖然有 `runway=6.5` 的自動後退，但後退量被硬性夾在 0-7m，湊出來的起始距離通常還是明顯短於這兩招「正常在真實戰鬥中」該有的起手距離（那兩個 range 值本來就是給 scheduler 挑招用的，暗示真實戰鬥時目標本來就该在那個距離帶）。這能不能解釋使用者說的「沒有完整展示」還不確定——這次沒能用 MCP 實際看過場面（Unity Editor 沒有 OS 焦點時 `Time.frameCount` 卡住不動，這次測試從進 Play 到退出全程都停在 frame 2，等於一次都沒真的推進，見既有 memory）。已請使用者說明具體是「衝一半就停」「命中判定沒觸發」「動畫/特效沒播」還是別的現象，才能對症下藥而不是瞎猜著改。

改 `YuanpeiAttackDebugMode.cs`（`BuildDummy` 無敵、`SaveDummyPrefs`/`TryLoadDummyPrefs` + 各移動路徑呼叫、`Enter()` 改先讀存檔、`RestorePlayerCameraControl` 每幀自癒）。編譯無錯、Console 無新增錯誤。第 2 點待使用者回報具體現象。

### 追加94 續 150-151（2026-09-05）— 稻草人視角鍵 + boss 近身震退加後仰後退＋前衝擠壓（正式版）

使用者兩點：1. 需要一個按鍵進入稻草人視角 2. boss 超近距離的近身擊退技能（Shockwave，近身震退），要求攻擊前做「後仰＋後退」再「往前擠壓」的動作，**明確要求這是正式 boss 版本，不是只改除錯工具**。

**1. 稻草人視角（續 151，`YuanpeiAttackDebugMode.cs`）**：新增 **V** 鍵切換。跟既有暫停自由視角（`SetFreeLook`/`DriveFreeLook`）同一套「借用 `Camera.main`、關掉 `ThirdPersonCameraController`」手法，差別是鏡頭固定釘在稻草人的「眼睛」位置（頭頂+1.6m，且每幀都重新貼齊，稻草人被移動時鏡頭跟著动），只給滑鼠自由看方向、不给 WASD 飛行——目的是「從被打的那一方視角確認命中時機/預警範圍」，不是到處亂飛。開啟時初始朝向自動看向 boss。跟暫停自由視角互斥（按 P 會先關掉稻草人視角，按 V 只在非暫停時生效），避免兩邊搶同一顆 `_camController`/`_camControllerWasEnabled` 記錄。Play 模式驗證：`SetDummyView(true)` 後鏡頭座標精確等於稻草人位置+(0,1.6,0)，`ThirdPersonCameraController.enabled` 正確變 false；`SetDummyView(false)` 後正確還原 true。

**2. Shockwave 加後仰後退＋前衝擠壓（續 150，`YuanpeiAttacks.cs`，正式招式邏輯本體，非除錯工具）**：這招（近身震退，range 0-4.5）原本查證後發現是全招式池唯一沒有任何前搖動作的招——直接原地生成一個擴散圈特效，boss 本體完全沒動。改寫成三段：(a) 後仰＋後退——`VisualRoot` 往後傾 18°、boss 本體後退 `number4`(預設1.4m，因為這是近身招不能退太多退出自己命中範圍)，用 `def.windupSeconds` 的時間（跟 `BodyCharge` 一样用 `Mathf.Max` 夾一個下限 0.3s，windupSeconds 資料本身留著 0.1 不動，只在程式邏輯這層夾下限，跟既有 charge 招式手法一致，不是新的硬編碼）；(b) 往前擠壓——0.14s 的快速 ease-in（先慢後快，讀起來像蓄力後的瞬間釋放）衝過原本站的位置 `number5`(預設0.6m)，這才是實際的攻擊瞬間；(c) 落點才生成擴散圈震退特效（沿用原本的 `SpawnHazard`/`Configure` 呼叫，傷害/擊退數值完全沒動），衝完再 ease 回原位。`number4`/`number5` 沿用 續138 建立的通用欄位慣例（每招自己詮釋），`YuanpeiAttack_Shockwave.asset` 補上兩個值（透過 `manage_scriptable_object` 改，不是文字編輯 .asset）。這是 attackPool 裡實際跑的邏輯本體，F8 除錯工具跟正式 `YuanpeiEncounter` 戰鬥呼叫的是同一個 `Shockwave()` 方法，兩邊播的動作完全一致，不是除錯限定版。

**驗證**：Play 模式呼叫 `Fire(3)` 觸發 Shockwave，Console 無例外（coroutine 開頭到第一個 `yield` 那段同步跑過，能看到有沒有立即報錯）；受限於 Unity Editor 沒有 OS 焦點時 `Time.frameCount` 卡住不推進（這次全程停在 frame 2），沒能實際看到後仰/後退/前衝的完整播放效果，待使用者自己在 Focus 的 Editor 裡按 F8→5 現場確認手感/距離感是否符合預期。

改 `YuanpeiAttackDebugMode.cs`（`dummyViewKey`/`_dummyView`/`SetDummyView`/`DriveDummyView` + log/OnGUI 文字）+ `YuanpeiAttacks.cs`（`Shockwave` 重寫）+ `YuanpeiAttack_Shockwave.asset`（`number4`=1.4, `number5`=0.6）。編譯無錯、Console 無新增錯誤。

### 追加94 續 152（2026-09-05）— VFX Inspect 模式：單一物件全螢幕動畫展示

使用者：「我很需要像長矛型光彈、六連彈、雷擊標記 這些物件全螢幕單一物件動畫展示的視覺檢查 比較清楚」——要看單一 VFX 物件（不管是 3D 投射物模型、粒子特效、還是地面貼花）乾淨地全螢幕播放，不被場景其他東西干擾。

**機制**：新增 **I** 鍵切換的「VFX Inspect」模式。核心想法是不去為每種攻擊各自寫框取邏輯（長矛=模型、六連彈=粒子、雷擊標記=地面貼花，形狀完全不同），而是共用一個通用機制：

1. `YuanpeiAttacks.cs` 新增只讀存取器 `SpawnedCount`/`GetSpawnedAt(index)`，讓外部能看到 `_spawned`（本來就存在、原本只給 `CancelAll()` 清場用的內部清單，每個攻擊產生的每一個 VFX GameObject 都會塞進去）。
2. `YuanpeiAttackDebugMode.cs` 開啟 Inspect 模式時記下當下的 `SpawnedCount` 當水位線，之後每一幀（`DriveInspectView`）掃描水位線之後新增的項目，不管是投射物、粒子系統、或貼花 quad，一律撈進 `_inspectTargets`。
3. 這些物件被撈到的當下，遞迴把自己＋所有子物件的 layer 改到新建的專用 layer **`YuanpeiVfxInspect`**（第 11 層——原本 `add_layer` 工具自動挑到第 10 層，但查證後發現第 10 層雖然沒被命名，實際上已經被 `DistantMountains`/`BackgroundScenery`/`JapaneseShrineVista` 等場景背景物件在用，撞到會讓背景漏進隔離畫面，改用 `SerializedObject` 直接對 `ProjectManager.asset` 的 layers 陣列寫入來指定第 11 層——先掃過 GreyboxTest+Map_School 全部物件的 layer 值，確認 11-31 完全沒人用）。
4. `Camera.main` 同時被接管：`clearFlags=SolidColor`（深灰背景，不畫場景）、`cullingMask` 只留這個新 layer——畫面上只剩追蹤到的 VFX 物件，其他全部背景/角色/地形都被裁掉，不是靠拉近鏡頭「擠出畫面」，是真的沒有被畫進去。
5. 每幀依追蹤物件們的合併 Renderer bounds 算出剛好塞滿畫面的距離，鏡頭平滑跟拍（`Lerp`/`Slerp`），投射物飛走、貼花在地上、粒子噴發都能一路跟著看，不會飛出框外。

跟既有的暫停自由視角(P)、稻草人視角(V) 三者互斥（開任一個會自動關掉另外兩個，避免搶同一顆攝影機控制器記錄），沿用同一套「借用 `Camera.main`、關/還原 `ThirdPersonCameraController`」慣例。

**驗證**：Play 模式測到真實攻擊因為 Editor 沒有 OS 焦點、`Time.frameCount` 卡住而還沒真的跑到會產生 VFX 的那一幀（跟續150 一樣的既有限制）；改成直接塞一個假的測試物件（Sphere，世界座標 50,20,50）進 `_spawned` 清單驗證框取邏輯本身：物件正確被撈進 `_inspectTargets`（count=1）、layer 正確被設成 11、鏡頭正確從遠處迅速貼近到物件附近（距離從 75.8 降到 4.0，符合一顆 scale=2 圓球该有的框取距離）——核心追蹤+框取機制確認正確；實際攻擊 VFX 的顯示效果（長矛的模型、六連彈的粒子、雷擊標記的貼花分別長什麼樣）待使用者在 Focus 的 Editor 用 F8→I→按對應數字鍵現場確認。

新增專案設定改動：Layer 11 命名為 `YuanpeiVfxInspect`（`ProjectSettings/TagManager.asset`）——純新增一個空 layer 名稱，沒有任何現有物件被搬到這個 layer（除了這個工具動態指派的臨時 VFX），不影響其他系統。

改 `YuanpeiAttacks.cs`（`SpawnedCount`/`GetSpawnedAt`）+ `YuanpeiAttackDebugMode.cs`（`inspectKey`/`_inspectMode`/`SetInspectMode`/`DriveInspectView`/`SetLayerRecursive` + `Fire()`/`Exit()`/log/OnGUI 更新）+ `ProjectSettings/TagManager.asset`（新增 layer 11）。編譯無錯、Console 無新增錯誤。

### 追加94 續 153（2026-09-05）— VFX Inspect 改成固定鏡頭 + 左boss右稻草人的展示台

使用者：「這個模式請你改為從畫面最左邊發射 往右邊飛行 主要是為了觀察物件外觀，與boss本身和稻草人位置無關」——續152 做的是「鏡頭每幀自動貼著 VFX 的即時 bounds 跟拍」，這樣物件永遠被鏡頭拉回畫面正中央，根本不會有「從左飛到右」的效果，跟使用者要的相反。

**改法**：拿掉每幀跟拍，改成**固定鏡頭**——`RestageShowcase()` 把 boss 暫時搬到 `arenaCenter` 為中心、世界 -X 方向 `showcaseHalfSeparation`(8m) 處，稻草人搬到 +X 方向對稱處（boss 保留原本的懸浮高度 `hoverHeight`、稻草人維持地面高度，兩者水平線仍落在同一個 `arenaCenter` 附近的真實地面上，讓每招攻擊自己內部的 `ProjectToGround`/`SampleFloorY` 這類地板 raycast 邏輯照樣能找到真的地板，不用另外處理），鏡頭則固定架在這條線的正對面（依 FOV/aspect 反推距離，讓兩點各自落在畫面左右兩側、留一點邊界不貼死），之後**完全不再移動**。攻擊自己算的瞄準方向本來就是「boss → 稻草人」，兩者被擺成左右之後，這個方向自然就是螢幕上的「從左飛到右」——不用去理解每招 VFX 的形狀/邏輯，任何招式都適用同一套暫時擺位。

**暫時性**：`SetInspectMode(true)` 時存下 boss/稻草人「真正」的座標（`EnterShowcaseStage`），`SetInspectMode(false)` 時精確還原（`ExitShowcaseStage`）——這只是一個過場展示台，不會弄丟續149剛做的「記住稻草人上次位置」。每次按數字鍵開新一輪展示前也會重新擺回展示台位置（`RestageShowcase`），避免上一招是衝撞類、把 boss 撞去別的地方，下一招從奇怪的地方開始。

**驗證**（Play 模式）：`SetInspectMode(true)` 後 boss 世界座標變成 `(-10, 5.2, -105)`、稻草人變成 `(6, 0.5, -105)`；`Camera.WorldToViewportPoint` 驗證 boss 投影在 `x=0.12`（畫面左側）、稻草人在 `x=0.88`（畫面右側），兩者都在 0-1 可視範圍內留有邊界，沒有貼死screen邊緣；`SetInspectMode(false)` 後兩者精確還原成展示前的原始座標。固定鏡頭+左右擺位機制確認正確；實際攻擊飛行時的視覺效果（長矛/六連彈/雷擊標記等）待使用者現場用 F8→I→數字鍵確認。

改 `YuanpeiAttackDebugMode.cs`（`EnterShowcaseStage`/`RestageShowcase`/`ExitShowcaseStage` 新增，`DriveInspectView` 移除鏡頭跟拍只留 layer 掃描，`SetInspectMode`/`Fire()` 接上新流程，新增 `showcaseHalfSeparation`/`showcaseFrameMargin` 欄位）。編譯無錯、Console 無新增錯誤。

### 追加94 續 154（2026-09-05）— 隱藏面板鍵 + 新增 VFX Close-up 模式（單物件全螢幕慢動作）

使用者兩點：1. 提供一個按鍵能隱藏 I 模式下的面板提示文字 2. 另一種情境——要能看到每個攻擊物件的特效與移動動畫，必須單個物件占用畫面非常大、慢速播放、一次只看一個物件（子彈），才看得清楚。

**1. 隱藏面板（H 鍵）**：`OnGUI()` 開頭加 `_hidePanel` 檢查，為真就只畫一行極小提示「(H to show panel)」然後 return，其餘完全不畫——工具本身仍是 `Active`（數字鍵等都照常運作），純粹只是不擋畫面，方便截圖/錄影。

**2. VFX Close-up 模式（C 鍵）**：跟 VFX Inspect（I 鍵）共用同一套「接管攝影機＋隔離 layer＋純色背景」機制，但用途和呈現方式刻意不同：
- **只追一個物件**：`DriveCloseupView()` 每幀檢查目前追蹤目標是否為 null（初始為 null，或前一個 VFX 已經被摧毀歸零），是的話才去 `YuanpeiAttacks.SpawnedCount`/`GetSpawnedAt` 找「水位線之後新出現的第一個」，抓到就鎖定、`break`（不理會同一波後續還會生出的物件，例如六連彈的第2-6發）。等這個物件本身結束消失（`== null`），下一幀才會抓下一個——一次只會有一個物件在追蹤，符合「一次就一個物件才能看得清楚」。
- **攝影機貼近填滿畫面**：跟 VFX Inspect 拿掉的「即時 bounds 跟拍」邏輯這次留著（而且用意完全相反——Inspect 要固定鏡頭讓物件飛過整個畫面，Close-up 要鏡頭死咬著單一物件不放，讓它盡量佔滿畫面），每幀依目前鎖定物件的 Renderer bounds 算出剛好貼合畫面的距離、`Lerp`/`Slerp`平滑貼近，鏡頭本身的跟隨用 `Time.unscaledDeltaTime`（不受下面的慢動作影響，鏡頭反應保持靈敏，只有物件的動畫本身變慢）。
- **慢動作**：開啟時 `Time.timeScale`（連同 `_animSpeed`）設成 `closeupTimeScale`（預設 0.2 = 1/5 速），關閉時還原成 1。
- 跟其餘三個會佔用攝影機的模式（暫停自由視角/稻草人視角/VFX Inspect）互斥——新增 `ExitSpecialModesExcept(SpecialMode)` 集中管理，取代原本 續151/152 那種每個開關各自手動關掉「自己知道的其他一兩個」的寫法（那樣寫法在模式一路長到 4 個之後會變成不好維護的兩兩檢查）。

**驗證**（Play 模式，塞假測試物件繞開 Editor 沒 OS 焦點時 frame 卡住的限制，跟續152/153一樣的既有做法）：
- `_hidePanel=true` 正確生效。
- `SetCloseupMode(true)` 後 `Camera.main.clearFlags=SolidColor`、`cullingMask` 正確對應 layer 11、`Time.timeScale=0.2`。
- 塞兩個假物件（FakeCloseupA 在前、FakeCloseupB 在後）進 `_spawned`，呼叫一次 `DriveCloseupView()` 後 `_closeupTarget` 正確鎖定 **FakeCloseupA**（第一個），完全沒有理會 FakeCloseupB——證實「一次一個」邏輯正確；鏡頭座標也確認從遠處移動貼近 A 的位置。
- `Destroy(FakeCloseupA)` 後立即在同一個 execute_code 呼叫內再跑一次 `DriveCloseupView()`，`_closeupTarget` 仍顯示 FakeCloseupA——這是 Unity `Destroy()` 延後到當前幀結束才真的清空物件的既有行為（測試方法論的限制，同一個 C# 呼叫內沒有真正跨幀），不是接續邏輯有問題；`_closeupTarget == null` 之後才會抓下一個的判斷本身是單純的 null 檢查，沒有理由不成立，但這次沒能用真實跨幀證實。
- `SetCloseupMode(false)` 後 `Time.timeScale` 正確還原成 1。

改 `YuanpeiAttackDebugMode.cs`（`hidePanelKey`/`_hidePanel` + `OnGUI()` 提早 return；`closeupKey`/`_closeupMode`/`_closeupTarget`/`_closeupWatermark`/`closeupTimeScale`/`closeupFrameMargin`/`SetCloseupMode`/`DriveCloseupView` 新增；`SpecialMode` enum + `ExitSpecialModesExcept` 取代原本分散的兩兩手動排他；`Fire()`/`Exit()`/log/OnGUI 文字同步更新）。編譯無錯、Console 無新增錯誤。

### 追加94 續 155（2026-09-05）— 真因：按鍵鍵位跟遊戲本身的鍵位撞在一起

使用者：「進入c模式後按技能都沒反應」。

**真因**：這個除錯工具跟遊戲本身用的都是同一顆 `Keyboard.current`（原始輪詢，沒有互相「消費」input 的機制），而 **C 鍵早就被 `CameraPossessionSwitcher`（貓咪附身切換）拿去用了**——按 C 進 Close-up 模式的同時，也把 `Camera.main` 跟操控權整個切給了貓咪自己的攝影機／控制器，而這個切換這個除錯工具完全不知情。結果是：數字鍵開火其實有正常執行（攻擊、VFX 都照樣生成），只是 Close-up 模式的隔離層/純色背景/慢動作全部套用在玩家原本那顆、已經被停用的攝影機上——玩家透過貓咪的攝影機在看，畫面自然「沒反應」。

**順便全面稽查**：把這個工具目前綁定的每一顆鍵，對照 `Assets/_Project` 全專案裡所有 `Key.X`／`keyboard.xKey` 的原始輪詢，抓到另外兩個一樣會撞的：**T**（`snapDummyToPlayerKey`）撞到 `ViewFocusDirector` 的守望者視角切換、**V**（`dummyViewKey`）撞到 `PlayerInputProvider.ViewTogglePressed`（玩家第一人稱切換）。另外 `replayKey`（R）雖然這次不是使用者回報的症狀，但撞到 `PlayerInputProvider.UltimatePressed`（玩家大絕），一樣是「按了工具的鍵，玩家角色也跟著動作」的同一類問題，順便一起修掉。

**改法**：三個＋一個全部換成全專案掃過確認完全沒人用的鍵——`replayKey` R→**Y**、`snapDummyToPlayerKey` T→**J**、`dummyViewKey` V→**L**、`closeupKey` C→**U**。另外修掉 `OnGUI()`/log 文字裡兩處寫死的字面「R replay」（沒有跟著 `replayKey` 欄位走，換了鍵位面板上還是顯示舊的 R，容易誤導）。

**場景欄位同步**：`replayKey`/`snapDummyToPlayerKey`/`dummyViewKey`/`closeupKey` 這幾個欄位在 `Map_School.unity` 裡本來就有明確序列化的值（不是「沿用 C# 預設」），光改程式碼預設值不會覆蓋掉已經存檔的舊鍵位——用 `SerializedObject` 直接對場景裡的元件寫入新值＋存檔（不是文字編輯 .unity 檔）。Play 模式讀取執行期欄位值＋反射呼叫 `SetCloseupMode(true)`／`Fire()` 都確認正常，沒有例外。

**已知但沒動的殘留風險**：方向鍵重新定位用的 Shift 修飾鍵（Shift+方向鍵＝移動 boss 而非稻草人）跟玩家的 `DodgePressed`（左 Shift）共用——`DodgePressed` 只在按下瞬間觸發一次，held 期間不會重複觸發，影響僅止於「開始用 Shift+方向鍵時玩家會閃避一次」，不是持續性的功能性錯誤；Ctrl/Alt 也都被載具飛行/走跑切換佔用，沒有乾淨的修飾鍵可換，這次先不處理，記一筆。

改 `YuanpeiAttackDebugMode.cs`（4 個鍵預設值 + 2 處字面字串修正）+ `Map_School.unity`（`YuanpeiAttackDebugMode` 元件的 4 個鍵欄位序列化值）。編譯無錯、Console 無新增錯誤。

### 追加94 續 156（2026-09-05）— 結構性修法：F8 期間直接停用會搶攝影機的兩個系統

使用者：「還是沒有反應 並且進入到這種f7 f8開發者模式，照理說要停用原本世界的按鍵邏輯 提供一個全新的鍵盤邏輯控制才對」。

續155 逐一改鍵位只堵住當時查到的那幾個洞，使用者這次直接點出結構性問題：這個工具在跟「真實世界」共用同一顆 `Keyboard.current`，只要有其他系統也在監聽鍵盤，這類 bug 就永遠堵不完。

**改法**：不再逐個改鍵位追著堵，直接把「真正會搶攝影機」的兩個系統，在整個 F8 session 期間停用：
- `CameraPossessionSwitcher`（貓咪附身切換，C 鍵）——會把 `Camera.main` 和操控權整個 SetActive 切到貓咪自己的攝影機，這正是續155 那個「Close-up 沒反應」的真因。
- `ViewFocusDirector`（守望者視角，T 鍵）——它自己的註解就寫「會接管當下正在使用的攝影機」，跟這個工具的攝影機操作性質完全衝突。

`Enter()` 時透過 `FindFirstObjectByType` 抓到這兩個元件、存下原本的 `enabled` 狀態後停用；`Exit()` 精確還原。兩個都是純粹的「按鍵觸發→切一次狀態」的 toggle 元件，沒有連續性的內部狀態（不像玩家的 `MoveInput`/`GuardPressed` 是持續讀取的），中途被停用/恢復不會有「卡在半個動作」的殘留風險。

**刻意沒動的部分**：`PlayerInputProvider`／`CharacterMovement` 這兩個真正驅動玩家移動/攻擊的元件沒有停用——`PlayerInputProvider.Update()` 一旦被停用，`MoveInput`／`GuardPressed` 這類「持續讀取」的欄位會凍結在停用當下那一刻的值（例如玩家剛好按著 W 或握著防禦鍵時進 F8，移動/防禦動作可能卡住不放），這是比原本要修的 bug 更糟的新 bug；而且續155 已經把這個工具自己的鍵位跟 `PlayerInputProvider` 讀的所有原始按鍵（WASD／V／R／Shift／Space／Ctrl／Q／E／Alt／滑鼠鍵）逐一核對過沒有重複，移動/戰鬥系統本來就不是這次回報的症狀來源，維持原本 續140「玩家保留完整操控」的設計。

**驗證**（Play 模式）：`Enter()` 前 `CameraPossessionSwitcher.enabled`/`ViewFocusDirector.enabled` 皆為 `true`；呼叫 `Enter()` 後兩者皆變 `false`；呼叫 `Exit()` 後精確還原成 `true`。

改 `YuanpeiAttackDebugMode.cs`（`SetWorldInputLocked` 新增，`Enter()`/`Exit()` 接上，log 文字補充說明）。編譯無錯、Console 無新增錯誤。

### 追加94 續 157（2026-09-05）— U 模式重做：單發子彈、放大、左飛右、隱藏血條

使用者確認 I 模式正常運作，接著要求修正 U 模式（VFX Close-up）：主要用來看「發射型攻擊」（六連彈 ProjectileBurst、長矛型光彈 SpearVolley），要一次只看一個物件、一次只射一發子彈、從左往右飛、非常緩慢、外觀放大方便觀看；並且 I/U 兩個模式都要隱藏 boss 血量條。

**真因回顧**：續154 的 Close-up 是「鏡頭每幀動態貼著物件 bounds 跟拍」——物件永遠被拉回畫面中央，跟 I 模式續153 修掉的問題是同一類、只是這次換成鏡頭跟拍造成一樣的「看起來沒在飛」。

**改法**：Close-up 不再動態跟拍，改成跟 I 模式共用同一套「boss=左、稻草人=右、固定鏡頭」機制（`EnterShowcaseStage`/`RestageShowcase`），差別只在 `closeupHalfSeparation`（1.6，遠比 I 模式的 8 窄）——物件因此還是會沿著兩者連線飛過畫面，但因為整個舞台被拉得很近，子彈仍然佔畫面很大一塊。

1. **一次一發、慢、直線飛**：`ProjectileBurst`/`SpearVolley` 的發數/速度是 `YuanpeiAttackDef`（ScriptableObject）上的欄位，不能直接改——改了就是動到真正的戰鬥數值。改成 `BuildCloseupFireDef()`：只在 Close-up 模式開著、且這次要放的招式是這兩招其中之一時，用 `Instantiate(original)` 複製一份「只存在這一次 Fire() 期間」的暫時副本，複製體上把 `count`（發數）改成 1、`number1`（飛行速度，兩招共用的欄位語意）壓低成 `closeupProjectileSpeed`（預設 2.5，再疊加 Close-up 本來就有的 `closeupTimeScale`(0.2) 慢動作，實際觀感更慢），`SpearVolley` 額外把追蹤相關的 `number3`/`number4` 歸零讓它飛直線不拐彎。複製體用完在下一次開新的 Close-up/關閉時 `Destroy()`，不會累積或動到磁碟上的真正資產。其他招式（非這兩者）不受影響，`BuildCloseupFireDef` 直接原樣回傳原本的 def。
2. **外觀放大**：`DriveCloseupView()` 抓到「這次新生出的第一個物件」的當下，直接 `go.transform.localScale *= closeupTargetScaleMultiplier`（預設 2.5）——不管是六連彈的光球還是長矛的模型，統一用這個方式放大，不用個別去猜每招各自的哪個欄位控制大小（長矛的視覺其實是 prefab 直接 Instantiate，沒有對應的 def 欄位可以調）。
3. **隱藏血條**：`SetInspectMode(true)`/`SetCloseupMode(true)` 都加一行 `hud.SetVisible(false)`，離開時 `hud.SetVisible(_hudWasVisible)` 還原（`YuanpeiBossHUD.SetVisible` 本身是漸隱漸顯，不是瞬間切換，不用額外處理）。

**順便修掉的框取 bug**：改窄 `half` 之後才發現 `RestageShowcase` 原本只用「水平方向能不能塞進畫面」算鏡頭距離——boss 比稻草人高出一截 `hoverHeight`（固定值，跟 `half` 大小無關），I 模式的寬 `half`(8) 讓這個垂直落差相對很小，從沒露餡；U 模式窄 `half`(1.6) 一用，垂直落差反而變成更吃緊的那一項，boss/稻草人直接被推出畫面上下邊界（實測 viewport y 分別是 2.28 跟 -1.28，遠超出 0-1）。改成水平/垂直兩個方向都各自算一次需要的距離，取比較遠的那個，兩個模式現在都會兩個方向一起塞得下。

**驗證**（Play 模式）：
- Close-up 模式下 boss/稻草人 viewport 座標從壞掉的 y=2.28/-1.28 修正為 y=0.93/0.07（都在 0-1 內），x=0.39/0.61 維持左右分邊。
- `_closeupDefClone.count=1`、`number1=2.5` 確認複製體覆寫生效；`attacks.SpawnedCount` 開火當下尚未真的生出物件（跟先前幾次一樣，卡在 Editor 沒 OS 焦點、`Time.frameCount` 不推進的既有限制，這次連續兩次檢查都停在同一個 frame，沒能等到真的生出子彈那一刻）。
- `YuanpeiBossHUD._target` 在 Inspect/Close-up 開啟時皆確認變 0，關閉後皆確認還原成 1。
- 順便驗證：I 模式本身的框取（寬 `half`=8）在雙軸修正後數值完全沒變（viewport 0.12/0.88，跟續153 驗證時一致），確認這次的修正沒有把 I 模式弄壞。

改 `YuanpeiAttackDebugMode.cs`（`RestageShowcase`/`EnterShowcaseStage` 加 `half`/`margin` 參數並改雙軸距離計算；`SetCloseupMode`/`DriveCloseupView` 重寫為固定舞台，拿掉動態跟拍；新增 `BuildCloseupFireDef`/`closeupHalfSeparation`/`closeupProjectileSpeed`/`closeupTargetScaleMultiplier`/`_closeupDefClone`；`Fire()` 接上覆寫 def 邏輯；`SetInspectMode`/`SetCloseupMode` 加 HUD 隱藏/還原）。編譯無錯、Console 無新增錯誤。實際子彈飛行手感（速度、大小是否恰當）待使用者現場用 F8→U→1（六連彈）／9（長矛型光彈）確認。

### 追加94 續 159（2026-09-05）— U 模式限定三招按鍵 + 修正斜飛為水平直飛 + 大幅減速

使用者：「u模式的話就只提供那三個技能得按鍵，且目前是從左上往右下發射，我要你直接從畫面左邊直線往右邊飛行，且飛行速度大幅放緩」。

**1. 限定三招按鍵**：新增 `IsCloseupEligible(def)`——只認 `ProjectileBurst`／`LightningMark`／`SpearVolley` 這三個 attackId（對應數字鍵 1／3／9，用 attackId 判斷不寫死索引，pool 順序以後變動也不會跟著錯）。數字鍵迴圈跟 `replayKey` 重播都加這個檢查，Close-up 模式下按其他數字鍵直接是 no-op，不會誤發不支援覆寫的招式。`BuildCloseupFireDef` 也同步擴大認得 `LightningMark`（單純把 `count` 壓成 1——雷擊標記本身是原地標記，不像另外兩招會飛行，沒有速度可以放慢）。

**2. 斜飛修正為水平直飛（真因）**：`RestageShowcase` 原本不管 Inspect 或 Close-up 都用同一套「boss 站在正常戰鬥懸浮高度（`hoverHeight`，約 4.7）、稻草人站在地面」的擺法——這對 Inspect（純粹看整段飛行弧線）沒問題，但攻擊瞄準的是稻草人的**碰撞體中心**（`PlayerCenter` 讀 collider bounds，比稻草人的地面座標高 1.1），boss 站在 4.7 高處往一個只有 1.1 高的目標瞄準，飛行路線自然是明顯的左上往右下斜線。新增 `flatTrajectory` 參數：Close-up 模式改把 boss 直接放在跟稻草人瞄準點同一個高度（`anchor.y + 1.1`，不是 `hoverHeight`），兩者打平之後，飛行路徑自然是水平直線，不用另外做「飛行軌跡拉平」的特殊邏輯。Inspect 模式不受影響（`flatTrajectory` 預設 false，維持原本的懸浮弧線）。

**3. 大幅減速**：`closeupProjectileSpeed` 2.5→**0.6**（疊加原本的 0.2 倍慢動作，實際飛行速度只剩原本的 1/4 左右）。

**驗證**（Play 模式）：`boss.y - dummy.y` 從續157的 `~4.7`（斜飛的根源）修正為 `1.1`（精確對齊稻草人瞄準中心，水平飛行）；viewport y 座標（0.92/0.08）仍在 0-1 範圍內，沒有被這次的高度調整弄出框；`IsCloseupEligible` 對 pool[0]/[2]/[8]（ProjectileBurst/LightningMark/SpearVolley）回傳 `true`，對 pool[1]（FocusLaser）回傳 `false`；直接呼叫 `Fire(2)`（LightningMark）確認 `_closeupDefClone.count=1` 且無例外。

改 `YuanpeiAttackDebugMode.cs`（`IsCloseupEligible` 新增並接進數字鍵/重播判斷；`BuildCloseupFireDef` 擴大支援 `LightningMark`；`RestageShowcase`/`EnterShowcaseStage` 加 `flatTrajectory` 參數；`closeupProjectileSpeed` 2.5→0.6；log 文字更新）。編譯無錯、Console 無新增錯誤。實際飛行手感待使用者現場用 F8→U→1／3／9 確認。

### 追加94 續 160（2026-09-05）— U 模式：限制放大倍率、-/= 調整子彈速度、面板精簡

使用者：「1 3 g[9] 技能都沒聚焦在畫面上 從左到右 請修正 要考量到畫面大小 物件不能沒有限制的放大 然後提供speed調整手段 u模式下清理掉非必要提示」。

**真因（「都沒聚焦在畫面上」）**：續157 的放大邏輯是固定倍率 `localScale *= 2.5`，對六連彈/長矛的小型物件還好，但雷擊標記的地面標記本身半徑就有 ~2.4m，`×2.5` 之後變成接近 12m 直徑的巨物——而 Close-up 的舞台（`closeupHalfSeparation`=1.6）整個寬度只有 3.2m，這個巨物直接大到遠遠超出整個鏡頭框取範圍，畫面上只會看到色塊的一小角，完全「沒有聚焦」可言，不是太小看不到，是太大到認不出形狀。

**改法**：`ScaleCloseupTargetToFit()` 取代原本的固定倍率——量測物件實際 Renderer bounds 半徑，換算成「要縮放到目標半徑」所需的倍率（目標半徑＝`closeupHalfSeparation × closeupTargetRadiusFraction`，預設抓舞台寬度的 28%，舞台改窄/改寬時目標大小會跟著等比例調整，不用另外重調），倍率本身再夾在 `closeupMinScaleFactor`(0.15) ～ `closeupMaxScaleFactor`(5) 之間（避免量到退化的極小 bounds 時算出離譜的縮放值）。小物件放大、大物件縮小，最後都收斂到差不多的、跟舞台成比例的合理大小。

**speed 調整手段**：`-`/`=` 這兩個既有的速度鍵，在 Close-up 模式下改成調整 `closeupProjectileSpeed`（子彈飛行速度本身），非 Close-up 模式時維持原本調整全域播放速度（`_animSpeed`/`Time.timeScale`）的行為——同一組鍵，依情境切換意義，不用另外占用新鍵位。

**面板精簡**：`OnGUI()` 新增 Close-up 專用的精簡面板（只列 3 招按鍵、子彈速度、重播鍵），取代原本那份列出全部 9 招＋方向鍵重定位提示＋範圍圈說明的完整面板——Close-up 模式下 boss/稻草人站在固定舞台上，方向鍵重定位、範圍圈這些提示本來就用不到。

**驗證**（Play 模式）：塞一顆模擬雷擊標記大小的假物件（scale 4.8，半徑~2.4）進 `_spawned`，跑一次 `ScaleCloseupTargetToFit` 後縮小到 scale 0.72（bounds 半徑 ~0.51，貼近目標值 0.448）；另外塞一顆極小假物件（scale 0.05）驗證放大邏輯，結果被放大到 scale 0.25，卡在 `closeupMaxScaleFactor`(5) 的上限，沒有無限放大。Console 無新增例外。

改 `YuanpeiAttackDebugMode.cs`（`ScaleCloseupTargetToFit` 取代固定倍率的 `closeupTargetScaleMultiplier`；`closeupTargetRadiusFraction`/`closeupMinScaleFactor`/`closeupMaxScaleFactor` 新增；`-`/`=` 鍵在 Close-up 模式下改接 `closeupProjectileSpeed`；`OnGUI()` 新增 Close-up 專用精簡面板）。編譯無錯、Console 無新增錯誤。實際觀感（放大後大小是否恰當、速度快慢）待使用者現場用 F8→U→1／3／9 確認，另外「都沒聚焦」是否完全解決也待現場確認（受限於本次 Editor 沒有 OS 焦點、無法即時看到畫面播放）。

### 追加94 續 161（2026-09-05）— U 模式真正的 bug：watermark 沒跟著 CancelAll 同步，導致第二次以後開火全部失效

使用者在自己的 Play session 裡實測回報：「這三種技能起始位置都不對，感覺在畫面邊界上，而且起始等待時間長，要確保每次按下數字或y都能撥放」。這次直接連上使用者正在跑的 Play session 現場量測，抓到一個之前都沒測到的真正功能性 bug。

**真因（「確保每次按下數字或y都能撥放」）**：`Fire()` 裡的 `_inspectWatermark`／`_closeupWatermark` 一直都是在呼叫 `StartCoroutine(FireRoutine(...))` **之前**用 `attacks.SpawnedCount` 設定——但 `attacks.CancelAll()`（真正把 `_spawned` 清空的地方）是寫在 `FireRoutine` 內部，只有等 `StartCoroutine` 真的執行到那一行才會清空。所以第一次開火沒事（水位線設 0，清空後还是 0，正常對齊），**第二次開火開始就全部壞掉**：水位線在清空「之前」被設成舊清單的長度（比如 1），`CancelAll()` 隨後把清單清空、新攻擊再從索引 0 重新生成物件——水位線卡在一個舊清單長度算出來的值，永遠對不上重新從 0 算起的新清單，新生成的 VFX 從此再也不會被撈進 `_inspectTargets`／指定成 `_closeupTarget`：Inspect 模式下新物件不會被搬到隔離 layer（鏡頭只看那個 layer，等於直接隱形）、Close-up 模式下新物件永遠不會被追蹤/放大。這不是「有時候」失效，是**除了每次進入該模式後的第一發，之後每一發都會失效**，跟使用者「按了沒反應」的描述完全吻合。

**修法**：把 `attacks.CancelAll()` 提到 `Fire()` 開頭、兩個水位線指定**之前**，同步呼叫（不透過 coroutine），確保兩個水位線永遠是從真正清空後的狀態（count=0）算起。`FireRoutine` 內部原本那次 `CancelAll()` 留著當保險（清單已空，等於 no-op，無害）。

**驗證**（直接連上使用者的 Play session，非另開session）：模擬「塞假物件→Fire()→塞下一個假物件→Fire()」兩輪連續開火——修好前這個測試在第二輪就會讓 `_closeupTarget` 卡在 `null`（重現使用者回報的症狀），修好後兩輪都正確追蹤到各自新生成的物件。

**「起始位置在邊界上」**：實測 viewport 座標 boss=(0.07,0.92)、稻草人=(0.93,0.08)——margin 只留不到 10% 緩衝，確實貼著邊界。`closeupFrameMargin` 1.15→**1.6**，兩軸同時後退，實測改善為 (0.19,0.80)/(0.81,0.20)，緩衝拉開到約 20%。

**「起始等待時間長」**：`closeupTimeScale`（0.2＝5倍慢動作）連 telegraph／windup 一起拖慢，兩者相加常態要價 0.35-1s 的攻擊前搖，乘上5倍變成好幾秒的純等待。既然子彈本身的飛行速度已經有獨立的 `closeupProjectileSpeed` 在控制，慢動作不需要獨自扛這個責任——`closeupTimeScale` 0.2→**0.5**，另外 `BuildCloseupFireDef` 的複製體把共用的 `telegraphSeconds`/`windupSeconds` 夾在很小的上限（0.15／0.05），雷擊標記自己的符文警示秒數（`number2`）砍半（1.1→至多0.6，不是歸零——警示動畫本身還值得看，只是不要拖那麼久）。六連彈/長矛自己招式特有的前搖（`MuzzleCharge` 0.55s／`SpearMuzzleGlow` 0.4s）是寫死在攻擊程式碼裡、不吃 def 覆寫，這次沒有動（不想為了除錯工具去碰核心戰鬥邏輯），改善幅度有限但至少不會再被慢動作乘數放大。

**注意**：`closeupTimeScale`／`closeupFrameMargin` 這兩個欄位在 `Map_School.unity` 場景裡已經有明確序列化的舊值（0.2／1.15），跟續155 遇到的狀況一樣，光改程式碼預設值不會生效——已用 `SerializedObject` 直接寫入場景並存檔。

改 `YuanpeiAttackDebugMode.cs`（`Fire()` 把 `attacks.CancelAll()` 提前到水位線指定之前；`closeupTimeScale` 0.2→0.5；`closeupFrameMargin` 1.15→1.6；`BuildCloseupFireDef` 加 telegraph/windup/雷擊標記警示秒數的上限夾制）+ `Map_School.unity`（`closeupTimeScale`/`closeupFrameMargin` 場景序列化值同步）。編譯無錯、Console 無新增錯誤。連續開火兩輪的追蹤修復已現場驗證；實際手感（等待時間是否夠短、位置是否舒適）待使用者接續確認。

### 追加94 續 162（2026-09-05）— 修正縮放造成的垂直偏移 + 長矛型光彈額外放大 2 倍

使用者：「這三個技能我都有看到了 還是偏畫面上方 請往下移動一點到螢幕中心 長矛型光彈請整體放大2倍」。

**真因（偏畫面上方）**：`ScaleCloseupTargetToFit` 直接 `localScale *= factor`——縮放是繞著物件的 **pivot**（transform 原點）進行，如果物件模型的 pivot 不在視覺中心（很常見，尤其是膠囊體/角色型模型的 pivot 常常在某一端而非正中央），放大之後視覺外觀會往 pivot 的對側偏移，讀起來就像「整個物件飄向畫面上方」——即使 boss/稻草人的擺位本身（續157驗證過）在垂直方向是完全對稱置中的，縮放這個動作本身就會把物件的視覺重心搬離原本置中的位置。

**修法**：`ScaleCloseupTargetToFit` 縮放前後各量一次 Renderer bounds 中心，縮放完直接把 `transform.position` 補上「縮放前中心－縮放後中心」的差值，把視覺中心精確地釘回原本的位置——不管 pivot 偏在哪一側，縮放後看起來都不會位移。

**長矛型光彈額外放大 2 倍**：`_selected`（目前正在開火的 pool 索引，`Fire()` 呼叫 coroutine 前就設好）拿來判斷這次擊發是不是 `SpearVolley`，是的話在原本「縮放到符合畫面比例」的倍率之上，再乘上新欄位 `closeupSpearVolleyExtraScale`（預設 2）——`ScaleCloseupTargetToFit` 新增 `extraScale` 參數承接這個逐招式的加成倍率。

**驗證**（Play 模式）：
- 建一個 pivot 刻意偏離視覺中心的假物件（父物件 pivot 在原點，子網格局部往上偏移 1 單位）——縮放前後量測子網格 Renderer bounds 中心，兩次結果完全相同（delta=0），確認位移補償公式正確抵銷了 pivot 偏移造成的漂移。
- 分別模擬 `_selected`=SpearVolley 索引／`_selected`=ProjectileBurst 索引，對兩顆起始尺寸完全相同的假物件跑 `ScaleCloseupTargetToFit`，量測縮放後尺寸比值 ≈1.96-1.98（預期 2.0，誤差來自兩者最終倍率各自被 min/max 夾制範圍浮動），確認額外倍率只套用在長矛型光彈身上。

改 `YuanpeiAttackDebugMode.cs`（`ScaleCloseupTargetToFit` 加縮放後位置補償 + `extraScale` 參數；`DriveCloseupView` 依 `_selected` 的 attackId 判斷是否套用 `closeupSpearVolleyExtraScale`(2) 新欄位）。編譯無錯、Console 無新增錯誤。實際觀感待使用者現場用 F8→U→1／3／9 確認。

### 追加94 續 163（2026-09-05）— 再往下移一點；「U 模式要先玩過 I 模式才正常」查無code層級成因

使用者：「不夠下面 繼續往下移動 並且[現]我要先在i模式撥放動作,u模式下的按鍵功能才會正常 請修正」。

**再往下移動**：續162 的 pivot 補償只修掉「縮放造成的位移」，沒有處理「舞台本身在畫面上的垂直位置」——加了 `closeupVerticalBiasFraction`（相對 `closeupHalfSeparation` 的比例，套在鏡頭高度上，鏡頭抬高、維持水平視角，畫面中的一切就會整體往下移）只套用在 Close-up 的 `flatTrajectory` 情境（Inspect 沒人反應過這個問題，不動它）。

**過程中一個插曲**：第一次試 0.35 這個比例，實測直接把稻草人推出畫面下緣（viewport y=-0.10，超出可視範圍），才發現這個比例對「鏡頭離舞台很近」的 Close-up 尺度來說影響量級遠比預期大。退回 0.15 重新量測：boss 從 0.80→0.67、稻草人從 0.20→00.07——確實往下移動了，且兩者都還在 0-1 可視範圍內，稻草人離下緣還有約 7% 緩衝（比較窄，但沒有被推出去）。這只是量測 boss/稻草人「舞台」座標當代理值——實際攻擊物件本身在畫面上確切落點沒辦法用這個方法直接驗證，這輪的數字是盡力而為的近似值，仍待使用者現場确认是否已經置中，需要再微調隨時再說。

**「U模式要先玩過I模式才正常」**：這次直接在 Play session 裡測了一次「完全沒碰過 Inspect 模式、F8 進來就直接開 Close-up」，依序開火 `ProjectileBurst`／`LightningMark`／`SpearVolley` 三招，Console 全程無任何例外，三次都正確印出「firing ...」的 log——沒有重現「U 模式按鍵沒反應」這個症狀，程式碼邏輯本身查不出讓 U 模式功能依賴 I 模式的地方（兩個模式各自獨立初始化 `_inspectLayer`、各自呼叫 `EnterShowcaseStage`，互不相依）。**這點目前無法確認修好**——比較可能的解釋是這次一併修掉的「起始等待時間長」問題（續161）讓沒耐心等的第一次嘗試看起來像「沒反應」，換到另一個模式重試時因為已經等過一次前搖而顯得「正常」；也可能是遊戲視窗本身的輸入焦點問題，不是這個工具的程式碼邏輯。如果之後還發生，麻煩留意當下 Console 有沒有跳出任何紅字，能幫忙判斷到底是完全没执行到 `Fire()`，還是有執行但畫面沒顯示。

**場景欄位同步**：`closeupVerticalBiasFraction` 這個新欄位在 Play 模式期間一度停在編譯前的舊預設值（0.35，不是 0.15）而非新的程式碼預設——這次的 Play session 是在改程式碼「之後」才進的，理論上應該要吃到新預設值，但實測沒有，猜測是同一個 session 內連續幾次重編譯時，編輯器沒有完整重新套用最新程式碼預設到已在場景中的元件欄位（不是續155/161那種「場景本來就存了舊值」的情況——這次編輯模式下場景檔本身其實已經是新值 0.15，但 Play 中的執行期物件一度沒吃到，額外重新 Stop→Play 一次後才吃到正確的 0.15）。記一筆做為這類欄位調整時的已知現象，不是每次都需要，但數值改完看起來沒生效時，先試著整個 Stop→Play 重進一次再下結論。

改 `YuanpeiAttackDebugMode.cs`（`RestageShowcase` 加 `closeupVerticalBiasFraction` 套用在 Close-up 的鏡頭高度上）。編譯無錯、Console 無新增錯誤。垂直置中效果、以及「U模式沒反應」是否還會重現，都待使用者接續確認。

### 追加94 續 164（2026-09-05）— 延長飛行距離 + 附上招式特效（不只模型）

使用者確認前面的問題都解決了，接著提兩點：「子彈本身飛行一段很小距離就消失了 請讓延長飛行距離，另外除了模型之外也請附上該攻擊具有的特效(boss戰會出現的)」。

**真因（飛行距離太短）**：`YuanpeiProjectile.OrbSurfaceHitsPlayer()` 每幀檢查子彈表面有沒有碰到目標（稻草人）的真實 `CapsuleCollider`，碰到就自我摧毀——這是正常戰鬥的命中判定，設計上沒有問題，但 Close-up 舞台原本只有 `closeupHalfSeparation`=1.6（總寬度 3.2m），子彈離開 boss 沒多久就進入稻草人的命中半徑，觸發自我摧毀，讀起來就是「飛沒多遠就消失了」。

**改法**：`closeupHalfSeparation` 1.6→**5**（總距離 3.2m→10m）。這個數值是其他所有 Close-up 相關量測（目標縮放比例、垂直偏移、框取邊界）共同的基準單位，全部都是相對這個值的比例，所以單純拉大它，子彈能飛更遠，但畫面上的相對大小、置中程度都會跟著一起等比例縮放，不會因為舞台變寬而變小或跑位。

**真因（特效缺失）**：`DriveCloseupView` 原本只認「這次開火後新出現的第一個物件」，抓到就不再繼續掃——但一次真實攻擊（即使 `count` 已經被覆寫成 1）往往還會另外生成好幾個獨立 GameObject（前搖光暈、拖尾、命中特效等），這些都是在攻擊播放過程中陸續才生成，不是一開始就跟主物件同時出現。原本的邏輯只把「第一個」搬到隔離 layer，其餘的全部留在預設 layer——而 Close-up 鏡頭只畫隔離 layer，等於除了模型本體，其餘特效全部被鏡頭忽略（不是沒生成，是生成了但看不到）。

**改法**：`_closeupTarget`（單一物件）改成 `_closeupTargets`（清單），邏輯比照 Inspect 模式的 `_inspectTargets`——每一幀持續掃描新出現的物件，全部搬去隔離 layer（讓 Close-up 鏡頭看得到），但**只有清單裡第一個**（真正的子彈/標記本體）套用「縮放到符合畫面比例」，之後陸續出現的（拖尾、光暈、命中特效等）維持原本的大小不去動它——強行把一個粒子系統或點光源縮放到跟子彈一樣大小，看起來只會更奇怪。

**驗證**（Play 模式）：
- `boss.transform.position`／`_dummy.transform.position` 距離從 ~3.2 變成 ~10.06，符合預期；viewport 座標仍在 0-1 範圍內（框取的等比例縮放正確運作）。
- 模擬一次開火：塞「主子彈」（scale 0.1）＋「伴隨特效」（scale 0.3）兩個假物件進 `_spawned`，跑一次 `DriveCloseupView()` 後 `_closeupTargets.Count=2`（兩個都被追蹤到）、兩者 layer 皆為隔離用的 11（都看得到）、主子彈縮放成 0.5（套用了縮放）、伴隨特效維持原本的 0.3（沒有被強制縮放）——完全符合預期。

改 `YuanpeiAttackDebugMode.cs`（`closeupHalfSeparation` 1.6→5；`_closeupTarget`→`_closeupTargets` 清單，`DriveCloseupView` 持續掃描＋只縮放清單首項）+ `Map_School.unity`（`closeupHalfSeparation` 場景序列化值同步）。編譯無錯、Console 無新增錯誤。實際觀感（飛行距離、特效是否完整顯示）待使用者現場用 F8→U→1／3／9 確認。

### 追加94 續 165（2026-09-05）— I 模式的長矛型光彈（[9]）本體放大 3 倍

使用者確認續164的兩點都解決了，接著要求：「接下來i模式的[9] 讓他本體放大三倍」。

**改法**：跟 Close-up 模式的 SpearVolley 加成放大（續162）同一個道理，但這次是 Inspect 模式——先把續162那段「縮放＋位置補償」邏輯拆成獨立共用方法 `ScaleObjectPreserveCenter(go, factor)`（`ScaleCloseupTargetToFit` 內部改呼叫它，行為不變），`DriveInspectView` 比照 Close-up 的做法：只在**這次開火看到的第一個物件**（`_inspectTargets.Count == 0` 那一刻）且**目前開火的是長矛型光彈**（透過 `_selected` 的 attackId 判斷）時，套用新欄位 `inspectSpearVolleyExtraScale`(3) 的位置補償縮放；之後陸續出現的拖尾/光暈等伴隨特效不受影響，維持原尺寸——跟 Close-up 一樣的「只放大本體、特效原樣」原則。

**驗證**（Play 模式）：模擬 `_selected`=長矛型光彈索引，塞一顆起始尺寸 (0.2,0.6,0.2) 的假物件跑 `DriveInspectView()`，縮放後變成 (0.6,1.8,0.6)，精確 3 倍；另外模擬 `_selected`=六連彈索引（非長矛型光彈），同一顆假物件尺寸維持不變，確認加成只套用在長矛型光彈身上。

改 `YuanpeiAttackDebugMode.cs`（`ScaleObjectPreserveCenter` 抽出為共用方法；`DriveInspectView` 加 `inspectSpearVolleyExtraScale`(3) 新欄位＋長矛型光彈判斷）。編譯無錯、Console 無新增錯誤。實際觀感待使用者現場用 F8→I→9 確認。

### 追加94 續 166（2026-09-05）— 抓到 Close-up 縮放公式漏算 margin 的真因：物件變小不是錯覺

使用者傳了一張 F8→U→9（長矛型光彈）的實際截圖，回報「感覺更小了」。

**真因**：`ScaleCloseupTargetToFit` 的目標半徑公式一直是 `closeupHalfSeparation * closeupTargetRadiusFraction`——沒有乘上 `closeupFrameMargin`。但鏡頭實際距離（`RestageShowcase`）是解出「畫面實際可視半寬 = half × margin」這個式子算的，不是只有 `half`。續161 為了修「舞台貼著畫面邊緣」把 `closeupFrameMargin` 從 1.15 調到 1.6，這個調整只影響鏡頭要退多遠（拉開緩衝），但目標半徑公式沒有把新的 margin 算進去——結果變成物件實際佔畫面的比例從 `0.28/1.15≈24%` 掉到 `0.28/1.6≈17.5%`，縮小了將近 3 成，不是錯覺，是續161那次改動的真實副作用，只是這次才第一次認真檢視大小。

**修法**：目標半徑公式改成 `closeupHalfSeparation * closeupFrameMargin * closeupTargetRadiusFraction`——這樣「目標半徑 ÷ 畫面實際可視半寬」永遠精確等於 `closeupTargetRadiusFraction` 這個純比例，不管 `margin` 或 `half` 之後再怎麼調都不會互相污染（margin 只管鏡頭退多遠當緩衝，不該連帶影響物件縮放後看起來多大）。順便把 `closeupMaxScaleFactor` 5→**8**——目標半徑變大後，長矛型光彈「符合畫面比例的倍率 × 額外2倍加成」總和有機會超過舊的 5 倍上限被夾住，導致 2 倍加成打折扣，拉高上限讓額外加成能完整生效。

**驗證方法的插曲**：一開始想直接在 Play 模式即時開火量測，但這次又卡在 Editor 沒有 OS 焦點、`Time.frameCount` 不推進——改用反射抓 `SpearVolley` 這個 private coroutine、手動 `MoveNext()` 硬跑，結果量到荒謬的巨大 bounds（half-extent 66，正常應該不到1）——這是手動硬跑 coroutine 繞過 Unity 正常的每幀計時機制，導致 `Time.deltaTime` 在單次 `MoveNext()` 內炸出不合理的大數值（子彈瞬間飛超遠、拖尾 bounds 跟著爆掉），不是真正遊戲裡會發生的行為，這個測法本身不可靠、放棄。改用直接 `Instantiate` 長矛 prefab 量測「剛生成、還沒被攻擊程式碰過」的原始 bounds（extents.magnitude≈0.69-0.89，依測法而定），確認是合理的小物件尺寸，再拿這個真實數字手算＋實測驗證新公式：修正前用同樣的假設算出來的縮放倍率是 3.16 倍，修正後實測是 **5.06 倍**——精確等於 3.16×1.6（正是這次補上的 margin 倍數），數學上完全對得上，物件會比之前明顯更大。

改 `YuanpeiAttackDebugMode.cs`（`ScaleCloseupTargetToFit` 目標半徑公式加乘 `closeupFrameMargin`；`closeupMaxScaleFactor` 5→8）+ `Map_School.unity`（`closeupMaxScaleFactor` 場景序列化值同步）。編譯無錯、Console 無新增錯誤。實際觀感待使用者現場用 F8→U→9 確認變大了多少、是否已經足夠。

### 追加94 續 167（2026-09-05）— 兩個真正的縮放 bug：認錯物件 + TrailRenderer 假 bounds

使用者：「還是很小 這是什麼情況 有用及時座標烘培嗎」——續166 修完margin問題後理論上應該要明顯變大，使用者卻說還是很小，代表理論計算跟實際結果之間一定還有落差沒抓到。這次deep dive查出兩個各自獨立、疊加起來讓修正完全沒生效的真因。

**真因1：認錯物件**——`DriveCloseupView`/`DriveInspectView` 一直假設「這次開火後第一個生出來的物件＝本體」，但六連彈(`ProjectileBurst`)跟長矛型光彈(`SpearVolley`)都有自己專屬的發射前特效（`MuzzleCharge`螺旋聚粒／`SpearMuzzleGlow`光暈脈動），這兩個特效物件在真正的子彈/長矛生成**之前**就已經呼叫 `_spawned.Add(...)` 註冊進清單——所以「放大」「額外2/3倍加成」這些操作，從續157這一系列改動開始，其實一直是套用在這個小小的發射前光暈特效上，真正的長矛/光球模型從頭到尾都沒被動過，維持原始的迷你尺寸。改法：新增 `IsAttackHeroObject(go)`，改用**物件名稱**判斷誰是真正的攻擊本體（`YuanpeiLightOrb`＝六連彈、`YuanpeiSpearProjectile`＝長矛型光彈、`YuanpeiHazard`＝雷擊標記，雷擊標記沒有獨立發射前特效物件所以原本沒受影響），不再迷信「先出現的就是本體」。

**真因2：TrailRenderer 的假 bounds**——就算抓對了物件，量測它「目前多大」時用 `GetComponentsInChildren<Renderer>()` 撈到的所有 Renderer 一起算包圍盒——長矛跟光球都在生成時掛了一個 `TrailRenderer`（拖尾特效），**剛掛上、還沒有任何拖尾歷史紀錄**的 `TrailRenderer.bounds` 回報了完全不合理的巨大數值（實測：真正的模型 `MeshRenderer` bounds 只有 (0.51, 0.46, 1.20)，同一個物件上的 `TrailRenderer` bounds 卻回報 (1, 9, 132)——大了兩個數量級）。這個假 bounds 被吃進「量測目前多大」的計算，導致算出來的「目前尺寸」被誤判成巨大，接下來「該放大幾倍」的公式自然算出一個遠小於1的縮放值（被下限 0.15 夾住），子彈不但沒被放大，反而被**縮小**到下限——不管 `closeupSpearVolleyExtraScale` 調多高都沒用，因為源頭的「目前尺寸」量測本身就是錯的。改法：新增 `GetSizeRenderers(go)`，量測時明確排除 `TrailRenderer`／`LineRenderer`（同類型的、沒有實際「模型體積」意義的線性特效渲染器），只用有意義的 Renderer（MeshRenderer 等）計算包圍盒。

**驗證方法的教訓**：這次進一步發現，先前用反射硬跑 coroutine（`enumerator.MoveNext()`）測試時量到的巨大 bounds（66）並不是我原本猜測的「手動跑 coroutine 導致 Time.deltaTime 爆炸」造成的——是 TrailRenderer 本身天生就會這樣，跟測試手法無關，是真實會在正式遊戲裡發生的 bug。這次順便解開了上一輪「這個測法不可靠」的誤判，兩個 bug 都是真的，不是測試手法的假象。

**驗證**（Play 模式，真實生成的長矛型光彈物件，非合成假物件）：`GetSizeRenderers` 對真實生成的長矛物件回傳 1 個 Renderer（`MeshRenderer`，正確排除掉 `TrailRenderer`）；量到的 Mesh bounds 從 (0.51, 0.46, 1.20) 正確放大到 (3.30, 3.01, 7.77)，縮放倍率 **6.5 倍**（精確符合「目標半徑2.24 ÷ 原始半徑0.69 × 額外2倍加成」的手算結果）——長矛型光彈現在應該會顯著變大（長度接近 8m）。

改 `YuanpeiAttackDebugMode.cs`（`IsAttackHeroObject` 新增並取代原本的「第一個物件＝本體」邏輯，兩個 Drive 方法都套用；`GetSizeRenderers` 新增排除 `TrailRenderer`/`LineRenderer`，`ScaleCloseupTargetToFit`／`ScaleObjectPreserveCenter` 都改用它）。編譯無錯、Console 無新增錯誤。實際觀感待使用者現場用 F8→U→9／F8→I→9 確認這次是否真的夠大了。

### 追加94 續 168（2026-09-05）— 修正 I/U 模式「有時出不來」的兩個真因

使用者確認大小正常了，接著回報：「進入 u或i模式後 有時會出不來」。

**真因1**：`dummyViewKey`／`inspectKey`／`closeupKey` 這三個切換鍵的判斷式都多寫了一個 `&& !_paused`——如果使用者在 I/U 模式裡不小心按到 P（暫停自由視角，這個鍵在另一個除錯工具 F7 也有一樣的按鍵慣例，很容易手滑按到），`_paused` 就會變 `true`，接下來想按 I 或 U 想離開，這個判斷式會直接**無聲擋下**，不會有任何 log 或提示告訴使用者「你現在是暫停狀態，要先按 P」——體感上就是「按 I/U 沒反應，出不去」。實際上 `ExitSpecialModesExcept` 本來就會正確把暫停狀態關掉並換到新模式，唯一擋住這件事發生的只有這個多餘的額外判斷——拿掉之後 I/U/L 三個鍵不管當下是不是暫停中都能正常運作。

**真因2**：`SetInspectMode`／`SetCloseupMode` 的「關閉」分支，原本是重新查詢一次「當下的」`Camera.main` 來還原設定，而不是用「開啟當下」實際被動過手腳的那顆攝影機。如果在 I/U 模式開著的期間，因為某些這個工具不知情的原因（例如按了 B 切到守望者視角，或載具/貓咪附身之類跟攝影機相關的其他系統）導致 `Camera.main` 指向了另一顆攝影機，「關閉」時的還原動作就會套用到**錯誤的（當下的）**攝影機身上，真正被隔離／改成純色背景的那顆攝影機永遠不會被還原——切換鍵本身「有作用」（布林值有翻轉），但畫面死死卡在隔離狀態，完全符合「出不來」的體感。新增 `_isolatedCam` 欄位在「開啟」當下記住實際被動手腳的那顆攝影機，「關閉」時改用這個記住的參照還原，不管 `Camera.main` 中途換成誰都一樣正確。

**驗證**（Play 模式）：
- 模擬「開啟 Close-up 後 `_paused` 意外變 `true`」，直接呼叫跟 `Update()` 現在完全一樣的離開流程（不再檢查 `_paused`），確認結果 `_closeupMode=False`、`_paused=False`、攝影機 `clearFlags` 正確從 `SolidColor` 還原成 `Skybox`。
- 模擬「開啟 Close-up 後 `Camera.main` 被別的系統換成另一顆攝影機」（建一顆假的、tag 為 MainCamera 的攝影機，停用原本那顆），確認關閉 Close-up 後，**原本被隔離的那顆**攝影機（不是新的 `Camera.main`）正確還原 `clearFlags` 成 `Skybox`。

改 `YuanpeiAttackDebugMode.cs`（`dummyViewKey`/`inspectKey`/`closeupKey` 拿掉 `&& !_paused`；`_isolatedCam` 新增，`SetInspectMode`/`SetCloseupMode` 開啟時記住、關閉時改用它而非重新查詢 `Camera.main`）。編譯無錯、Console 無新增錯誤。

### 追加94 續 169（2026-09-05）— 長矛型光彈疊加影片烘焙 flipbook 特效（Crimson Void Spear × 使用者提供影片）

使用者：「目前大小正常了，但是發現進入 u或i模式後 有時會出不來 你幫我排查，再來是長矛型光彈的特效，能採用 `C:\Users\homec\Downloads\長矛型光彈-3d.mp4` 這個影片當特效嗎」（U/I 出不來已在續168修好）。透過 `AskUserQuestion` 確認做法：疊加在現有 3D 模型上（模型／拖尾／點光源完全不動），額外加一層影片烘焙的 flipbook 當能量視覺層，不取代。

**素材處理**：來源影片 1280×720、24fps、10 秒、H.264。用 ffmpeg 逐段擷取 contact sheet 看過整段影片的視覺弧線（黑畫面起手→波動能量線成形→箭頭輪廓固化＋白閃轉場→乾淨的「箭頭朝向飛行方向＋紫紅拖尾能量＋火花粒子」飛行段→溶解成透明透鏡狀→淡出黑幕），選定 frame 144-191（第 6-8 秒，48 幀）當飛行中最乾淨的一段。背景用 Python/PIL 取樣：四角落深灰黑（RGB 約20-23，中上方有淡淡暈影到36-42），跟先前 `不要有人形.mp4`（SwordOrbit 用的淺灰棋盤格透明慣例）不同，luma-key 門檻改用 55（SwordOrbit 是60）。畫面右下角每一幀都有一個固定的四角星水印小圖示，用 `drawbox` 在 alpha-key 之前先塗成背景色蓋掉（範圍 x:1110-1270/y:555-715，全幀 1280×720 座標）。

沿用 `SwordOrbitVfxSetup.cs` 建立的 ffmpeg 烘焙慣例（`geq` luma-key 產生 alpha、`drawbox` 蓋水印、texture import 設 `npotScale=None`／`alphaIsTransparency`、`SlashFlipbookURP` shader premultiplied blend）烘出 `SpearFlipbook_Atlas.png`（8×6 grid、240×135/格＝1920×810，48 幀剛好填滿整張圖，無多餘空格）。實際用 Python 逐點採樣驗證 alpha：背景角落 alpha=0、箭頭核心亮部 alpha=255、中段拖尾漸層 alpha≈70-120，水印被蓋掉的區域也確認 alpha=0——key 抓得乾淨。

**跟 SwordOrbit 的兩個關鍵差異**（新建 `SpearFlipbookVfxSetup.cs`，未動 `SwordOrbitVfxSetup.cs`）：
1. **朝向**：SwordOrbit 是 Billboard（永遠面向攝影機）；長矛型光彈必須跟著飛行方向走，所以用 Mesh render mode + `alignment=World`，quad 掛在 `CrimsonVoidSpearProjectile.prefab` 底下當子物件，本地旋轉 `Euler(0,-90,0)`——因為內建 Quad 本身攤平在自己的本地 XY 平面（法線 -Z），這樣轉完之後圖片的「長邊」（箭頭指向的軸）會對齊到父物件的本地 Z 軸，正好是 `SpearVolley()` 呼叫 `Instantiate(prefab, origin, Quaternion.LookRotation(dir, Vector3.up))` 時已經對準玩家的飛行方向。有用非 Play 模式的編輯器截圖實際驗證朝向（把一顆預覽物件搬到天空無干擾處，物件面朝世界 +Z，用 `manage_camera` 側面取景）——箭頭尖端確實朝向 +Z（飛行方向），紫紅拖尾在後方，方向抓對，不用再翻轉。
2. **播放方式**：SwordOrbit 是固定時長的一次性施放特效，靠 `SlashVfxController` 播完自動 `Destroy`；長矛型光彈的飛行時間跟速度、距離有關（不固定），所以這次不掛 `SlashVfxController`，改成 `main.loop=true`＋單一 burst 在 t=0（Unity 對 loop 中的粒子系統，同一顆 burst 每次循環都會自動重新觸發），每輪 0.6 秒循環撥放 48 幀——飛多久就跟著循環播多久，物件真正銷毀時（`YuanpeiProjectile` 自己的邏輯，撞到玩家或壽命到期）這顆子物件跟著一起消失，不需要自己另外排 `Destroy`。

**改法**：新增 `SpearFlipbookVfxSetup.cs`（menu「Tools/Live2DAction/Add Spear Flipbook VFX (SpearVolley overlay)」），直接編輯（非新建）`Assets/_Project/VFX/Boss/CrimsonVoidSpearProjectile.prefab`——加一個新的子物件 `SpearFlipbookVFX`（`ParticleSystem` + `SpearFlipbookMat` 材質，shader 沿用既有的 `Live2DAction/VFX/SlashFlipbook`，`_Brightness`=2.0 讓箭頭白熱核心吃到 URP Bloom）。`YuanpeiAttacks.SpearVolley()` 完全沒有改動一行——因為 `spearProjectilePrefab` 欄位本來就指向這個 prefab（Play 模式讀取確認路徑一致），新增的視覺層自動隨每一發長矛型光彈一起出現。

**驗證**：編譯無錯（`refresh_unity` + `read_console` 無新增錯誤）。菜單執行成功（console log 確認「added/updated 'SpearFlipbookVFX' on ...」）。非 Play 模式下用 `execute_code` 實例化＋`manage_camera` 側面截圖確認：飛行方向朝向正確（箭頭尖端朝 +Z）、alpha key 乾淨（背景角落透明、水印區域透明、能量核心不透明）、跟既有 3D 模型輪廓疊在一起視覺上吻合（都是箭頭/長矛形狀，方向一致）。實際 Play 模式下 F8→U/I→9 觀感（循環撥放速度手感、跟真實移動速度搭配起來是否自然）待使用者現場確認。

### 追加94 續 170（2026-09-05）— 修正長矛型光彈 flipbook 左右方向顛倒的真因

使用者：「影片特效的左右方向顛倒了，請反轉」。

**真因（比想像中複雜，兩層問題疊在一起）**：
1. **續169的驗證方法本身就是假的**——非 Play 模式下用 `execute_code` 生一個預覽物件截圖，看起來「方向正確」，但 `ParticleSystem` 在非 Play 模式下不會自動 tick（沒有呼叫 `Simulate()` 就不會真的跑起來），那次截圖裡看到的「箭頭形狀」其實全部是**3D 模型本身**的輪廓（模型本來就長得像箭頭/長矛），flipbook 粒子系統從頭到尾都沒有真的渲染出來過——等於那次驗證完全沒驗證到 flipbook。
2. **真正的 bug**：`renderer.alignment = ParticleSystemRenderSpace.World`——這個模式下 Mesh 渲染完全不吃這個 transform 自己的旋轉。用 `ps.Simulate(t,true,true,true)` 強制模擬＋隱藏 3D 模型只留 flipbook，把 child 的本地 Y 旋轉掃過 0/45/90/.../315 八個角度，`World` 模式下全部八個角度渲染出來的畫面一模一樣（箭頭都指向飛行方向的反方向）——證實問題根本不在旋轉數值，而在 alignment 模式本身。

**修法**：`renderer.alignment` 改成 `ParticleSystemRenderSpace.Local`（會確實套用這個 transform 自己的旋轉），改用 Local 之後重新掃一次同樣八個角度：0°/180° 剛好側面對到鏡頭（完全看不到，等於卡片轉到跟視線幾乎平行）、45°/90°/135° 箭頭方向正確（指向飛行方向），225°/270°/315° 方向依然是反的，其中 **90°** 卡片最正面朝向鏡頭（45/135 都有明顯的斜角透視縮短）。改用 90° + Local。

**驗證**：用真實的 F8 Showcase 幾何（boss 在 -X、玩家/假人在 +X，飛行方向世界 +X；鏡頭固定在 -Z 往 +Z 看，跟 `RestageShowcase` 完全一致的座標關係，不是隨便取一個角度）重新截圖確認：箭頭尖端正確指向畫面右側（飛行方向），紫紅拖尾能量正確拖在後方（畫面左側），3D 模型跟 flipbook 疊在一起方向一致。

**順便回答使用者的另外兩個問題**：
- **這個特效是否已經正式上線到 BOSS 戰？**——是，從續169開始就已經是了，不是只存在於 F8 除錯工具。原因：這個 flipbook 是直接烘進 `CrimsonVoidSpearProjectile.prefab` 這個檔案本身（不是另外建一個 debug-only 的複本），而 `YuanpeiAttacks.spearProjectilePrefab` 這個欄位本來就指向同一個檔案（Play 模式讀取確認過路徑完全一致）——`SpearVolley()` 一行都沒改，所以正式關卡裡 boss 真的打長矛型光彈時，這個視覺層本來就會自動出現，F8 只是拿來檢視，不是唯一入口。
- **加入特效前後的差異，怎麼做視覺比較？**——非 Play 模式下用 `execute_code` 直接生兩顆真實 prefab 副本（一顆停用 flipbook 子物件＝加入前、一顆啟用並用 `ParticleSystem.Simulate()` 強制跑到飛行中的某一幀＝加入後），對齊同一個攝影機角度分別截圖，再用 Python/PIL 裁切拼成一張上下對照圖——這個方法後續要看任何「改動前後差異」都可以照搬（前提是像這次一樣先用 `Simulate()` 避開粒子系統在非 Play 模式不會自動 tick 的問題，續169最初的失敗验证就是漏了這一步）。

改 `SpearFlipbookVfxSetup.cs`（`renderer.alignment` World→Local；child 本地旋轉沿用 Euler(0,90,0) 但這次是在正確的 alignment 模式下重新掃描驗證出來的，不是續169那次的誤判）。編譯無錯、Console 無新增錯誤。已重新執行 bake menu item 把修正套用到 `CrimsonVoidSpearProjectile.prefab` 上（正式 boss 戰使用的同一個檔案）。

### 追加94 續 171（2026-09-05）— F8 U模式新增「有/無特效對照」（K 鍵）

使用者：「能不能在F8模式 U模式 同時射出有無特效版本的比較」。

**做法**：新增 `compareKey`(K)，跟 Close-up 本身分開,是一個獨立的開關(切一次就持續生效,不用每次都按),只對長矛型光彈(9)有作用。開著 K 的狀態下在 U 模式按 9,`DriveCloseupView` 抓到真正的長矛本體、套用完原本的縮放之後,額外呼叫新方法 `BuildCompareClone`——複製一份剛剛那顆(已經縮放/定位/隔離好)的物件當「無特效對照組」,把複製體的 `SpearFlipbookVFX` 子物件強制關閉,疊在正牌那顆的**正下方**(間距 = 物件本身高度 × `compareStackGapFraction`(1.3),不是固定距離,縮放倍率變動時對照組間距會跟著等比例調整)——正牌本體完全不動,不去干擾 Close-up 鏡頭本來就算好的置中位置。

複製體會帶著自己那份 `YuanpeiProjectile`(`Instantiate` 連同飛行方向/速度/追蹤等 runtime 狀態一起複製,不只是外觀),等於兩顆會各自平行飛行、各自命中/消失,不用另外寫一套獨立的生命週期管理——但因為它是這個工具自己生出來的、不在 `attacks` 的 `_spawned` 清單裡,`attacks.CancelAll()` 不會連帶清掉它,所以額外在 `Fire()` 開頭、`SetCloseupMode(false)` 都手動 `Destroy` 它,避免下一次開火或離開 Close-up 時殘留。

**驗證**：編譯無錯、Console 無新增錯誤。非 Play 模式下無法端到端驗證(粒子系統/攻擊 coroutine 都要 Play 模式才會真的跑),新欄位是全新序列化欄位(不是改既有預設值),不需要額外同步場景檔——下次載入場景會自動套用程式碼預設值(K / 1.3)。實際兩顆是否確實不重疊、間距是否夠清楚,需要使用者在 Play 模式下 F8→U→K(開對照)→9 現場確認一次。

### 追加94 續 172（2026-09-05）— 抓到「K沒發現區別」的真因：ParticleSystemRenderer 汙染了 bounds 量測（連帶修好一個從續169就潛伏的隱藏迴歸）

使用者：「1.按下K沒發現區別 2. 在幫我確認正式BOSS戰 /F8(非I,U模式)的長矛型光彈特效是否有套用」。

**問題2先回答**：載入 `Map_School.unity` 直接讀取場景裡真正的 `yuanpei_LogoSky` 的 `YuanpeiAttacks.spearProjectilePrefab` 欄位，確認路徑就是 `CrimsonVoidSpearProjectile.prefab`——跟 F8 除錯工具、跟正式 BOSS 戰用的是同一份檔案，`SpearVolley()` 沒有任何 debug-only 的分支去替換它。所以答案是：**F8（不管有沒有開 I/U）、正式 BOSS 戰，全部都已經套用**，不是只有 U/I 模式才看得到。

**問題1的真因**：寫了一個非 Play 模式的受控重現測試（直接用反射呼叫 `ScaleCloseupTargetToFit`／`BuildCompareClone` 這兩個 private 方法,不需要真的進 Play 模式)，量到 `BuildCompareClone` 算出來的間距 `gap` 是 **910**——對照組被塞到離正牌本體 910 個單位遠的地方，等於扔到看不見的天涯海角，這才是「沒發現區別」的真正原因（不是縮放太小、也不是顏色不夠明顯，是根本不在畫面附近）。

往上追一層,連 `ScaleCloseupTargetToFit` 本身都被牽連：同一次測試量到長矛型光彈的縮放倍率變成 **0.30**（正常應該是 6.5 倍放大，續167驗證過的數字）——即使沒有 K，續169加了 flipbook 之後，U/I 模式長矛型光彈的自動放大其實已經默默壞掉了，只是沒人剛好在這幾天測試時注意到「怎麼變小了」。

**根本原因**：`GetSizeRenderers`（續167為了排除 `TrailRenderer` 剛生成時的假 bounds 而寫的方法）沒有把 `ParticleSystemRenderer` 也排除掉——續169新增的 `SpearFlipbookVFX` 子物件，牠的 `ParticleSystemRenderer` 在**從沒被 `Simulate()` 過**的狀態下，`bounds` 回報的是一個位於**世界原點 (0,0,0)**、大小 (0,0,0) 的退化值——完全不合理，連物件自己的座標都對不上。當這個座落在世界原點的退化 bounds 被 `Encapsulate` 進長矛本體（座落在關卡裡某個座標，例如 y=700）的真實 bounds 時，量出來的「大小」直接被拉伸成「從世界原點到長矛所在位置」的距離（好幾百甚至上千），是一個跟長矛實際大小完全無關的天文數字——`ScaleCloseupTargetToFit` 的縮放公式跟 `BuildCompareClone` 的間距公式都是拿這個被污染的數字去算，前者算出離譜的小縮放(0.30)，後者算出離譜的大間距(910)。這是跟續167（TrailRenderer 剛生成的假 bounds）完全同一類的 bug，只是這次的元凶換成了 `ParticleSystemRenderer`。

**修法**：`GetSizeRenderers` 的排除清單加上 `ParticleSystemRenderer`（現在排除 `TrailRenderer`／`LineRenderer`／`ParticleSystemRenderer` 三種）——量測「這個物件的模型到底多大」只看真正的網格渲染器（`MeshRenderer`/`SkinnedMeshRenderer`），特效類渲染器一律不列入考慮。

**驗證**：修正後重新跑同一套非 Play 模式受控測試——縮放倍率恢復成 **6.50**（跟續167的舊數字完全吻合），間距恢復成 **3.91**（合理的物件本身高度等級,不是天文數字）；接著用跟 `RestageShowcase` 完全相同的鏡頭距離公式重新截圖，確認兩顆物件（有特效的正牌＋無特效的對照組）都清楚落在畫面內、都看得見。

**額外發現（附帶一提，非本次修正範圍）**：把兩顆疊在一起（垂直方向）截圖時，發現這個測試用的空曠場景本身自帶某種「地面倒影」效果（在天空中飄浮的物件下方會出現一個上下顛倒的鏡像），跟對照組疊放的位置意外地接近，光用肉眼掃過去確實有可能誤把「對照組」看成「正牌的倒影」而忽略掉——這可能是造成「沒發現區別」的次要原因（次要於上面那個 910 的主因）。這個倒影效果在正式 BOSS 戰的關卡場地是否存在、疊放位置會不會撞上，還沒有確認；如果使用者在 Play 模式實測後仍然覺得兩顆長得太像沒辦法一眼分辨，可以再考慮把對照組改成左右並排而不是上下疊放。

改 `YuanpeiAttackDebugMode.cs`（`GetSizeRenderers` 排除清單加 `ParticleSystemRenderer`）。編譯無錯、Console 無新增錯誤。用反射直接呼叫 private 方法的非 Play 模式受控測試驗證兩個數字都恢復正常；實際 Play 模式下 F8→U→K→9 是否已經看得出兩顆的差異，待使用者現場確認。

### 追加94 續 173（2026-09-06）— K鍵沒觸發的三個疊加原因 + flipbook 尺寸改成跟著3D模型走

使用者：「1.按下k鍵沒有正確觸發 2. 影片特效應該要調整跟3d模型對應的大小」。

**問題1：K鍵「沒觸發」其實是三個問題疊在一起**
1. **鍵位衝突**：`Key.K` 撞到 `ViewFocusDirector.commitViewKey`（也是 K）。雖然 `SetWorldInputLocked` 在整個 F8 期間會停用那個 director、實際上不會真的搶輸入，但照續155立下的規矩（這個工具的按鍵一律不跟世界按鍵衝突），還是把它換成 `Key.N`（"no VFX" 好記）。
2. **對照組被塞到畫面外（下方）**：續171 把對照組疊在正牌**下方**——但 Close-up 模式為了讓舞台落在畫面偏下（`closeupVerticalBiasFraction` 把鏡頭抬高），畫面下緣幾乎沒有空間，上緣才有 headroom。對照組往下疊等於直接掉出畫面下緣。改成疊在**上方**。
3. **加成放大讓兩顆塞不下**：長矛型光彈在 Close-up 有「符合畫面 ×2」的加成放大（續162），單獨一顆就快要頂到畫面上下緣了，再往上疊第二顆一定爆框。開對照模式時（且只有這時）**取消那個 ×2 加成**，兩顆都用純「符合畫面」的尺寸，才疊得下。

（另外續172修好的 `ParticleSystemRenderer` 假 bounds 也是其中一環——沒修那個的話間距會算成 910，比上面三個都嚴重。）

**驗證**：反射直接呼叫 private 方法的非 Play 受控測試——對照模式下長矛縮放 3.25 倍（純符合畫面、無加成），對照組落在正牌**上方 +1.96**（間距 = 物件高度 1.50 × `compareStackGapFraction` 1.3），正牌 flipbook 開、對照組 flipbook 關；用 RestageShowcase 相同的鏡頭幾何截圖，確認兩顆都完整落在畫面內、上下排開、肉眼可分辨（上＝純模型無特效、下＝帶白紫能量特效）。`Map_School.unity` 的 `compareKey` 序列化值同步成 28（Key.N）。

**問題2：flipbook 尺寸改成量測 3D 模型**
原本 card 是寫死的 2.2 長（模型本身 world bounds 只有 1.20 長 / collider 1.25），等於特效比模型大了 1.8 倍，看起來像一團脫離模型的能量雲而不是「疊在模型上」的覆蓋層。改成 `BakeOntoPrefab` 時實際量測 "Model" 子物件的 renderer bounds 最長軸（1.20），card 長度 = 1.20 × `CardLengthVsModel`(1.12)（留一點點尾巴讓拖尾能量還是稍微超出矛尖），card 高度依影片 240:135 比例換算 = 0.75。重新烘焙後 card 從 2.2×1.24 變成 **1.34×0.75**，非 Play 模式截圖確認能量現在是貼著矛身走、不再是外擴的一大團。Close-up/Inspect 的整體放大是乘在父物件 localScale 上、flipbook 子物件會等比例跟著縮放，所以改基準比例後各種放大倍率下都維持正確。

改 `SpearFlipbookVfxSetup.cs`（card 尺寸改成量測模型 bounds）+ `YuanpeiAttackDebugMode.cs`（`compareKey` K→N；對照模式疊上方、取消長矛加成放大；`BuildCompareClone` 間距公式加 clamp 上限）+ `Map_School.unity`（`compareKey` 序列化同步）。編譯無錯、Console 無新增錯誤。已重新執行 bake menu 把新 card 尺寸套到 `CrimsonVoidSpearProjectile.prefab`。實際 Play 模式下 F8→U→N→9 是否能清楚看到兩顆對照、以及新的 flipbook 尺寸是否恰當，待使用者現場確認。

### 追加94 續 174（2026-09-06）— Boss 支配領域全螢幕邊界特效（yuanpei_LogoSky，URP Fullscreen Pass）

使用者提供完整工程規格，要求分析專案（Render Pipeline / Main Camera / HUD / Boss 戰控制器 / 階段事件）後，在不破壞既有戰鬥系統的前提下實作一套螢幕空間的「Boss 支配領域」邊界特效。

**分析結論**：URP 17.0.4（單一 pipeline asset + 單一 renderer，`m_RendererFeatures` 原本空）；3 台互斥 Base Camera、無 Camera Stack、無 Overlay Camera；所有 HUD 都是 Screen-Space-Overlay（天生蓋在整條 pipeline 之後）；`yuanpei_LogoSky` 的階段事件在 `YuanpeiBossVitals.PhaseChanged`；生命週期接點在 `YuanpeiEncounter.StartEncounter/Victory/Defeat`；專案無 Bloom。

**新增**：`BossDomainScreenVFX.shader`（fullscreen HLSL，程序 fbm 噪聲、螢幕高度單位 edge mask、中央硬 early-out、四角加權、不規則 dissolve、黑霧、翠綠 emission、呼吸、僅邊界 UV 扭曲、進場/常駐/脈衝/消散參數）+ `BossDomainScreenVFXRendererFeature.cs`（自訂 ScriptableRendererFeature，RenderGraph copy→blit，Game camera only，未註冊材質時零成本早退）+ `BossDomainScreenVFX.cs`（控制器 + 可測試的 `BossDomainEnvelope` 狀態機，runtime 材質實例、快取 property ID、Update 無 GC）+ `BossDomainScreenVFXSetup.cs`（選單一鍵接線）+ `BossDomainScreenVFX.mat` + `BossDomainScreenVFXTests.cs`（12 測試全綠）。

**改**：`Live2DAction_Renderer.asset`（+1 Renderer Feature，注入 BeforeRenderingPostProcessing）；`YuanpeiEncounter.cs`（+1 欄位、Awake 自動尋找、StartEncounter → BeginDomain、Victory/Defeat → EndDomain，共 4 處，不動既有邏輯）；`Map_School.unity`（`yuanpei_LogoSky` 掛控制器 + 接線）。

**驗證**：EditMode 12/12 綠。Play 模式（Editor 對焦）實測 —— BeginDomain 進場 → Active（截圖確認四周翠綠火焰＋四角較強＋黑霧、中央清楚、HUD 在上）；玩家移動時特效固定螢幕邊界；SetPhase(2) 觸發一次脈衝後回 Active；EndDomain 消散 → Inactive，feature `s_Material` 清空、Pass 完全停止。Shader `isSupported=True` 訊息數 0，Console 無 Shader/RendererFeature/RenderGraph/NRE 錯誤。詳見 `Docs/YUANPEI_LOGO_SKY_BOSS.md` §Boss 支配領域全螢幕邊界特效。

**尚需人工**：rune 貼圖（`runeTexture` 欄位，留空＝關）；正式夜空場地的觀感微調；`onPhasePulse` 接天空巨劍增亮；（可選）加 Bloom override。

### 追加94 續 175（2026-09-06）— Boss 戰夜空全景圖（rogland_clear_night，CC0）

使用者：「rogland_clear_night_4k.exr 可以當作 boss 戰的全景圖嗎」→ 可以（Poly Haven「Rogland Clear Night」，4096×2048 equirectangular、真夜空銀河、CC0）。

**匯入**：ffmpeg 降 2K（ZIP16 half-float，8MB）→ `Assets/_Project/Environment/Textures/rogland_clear_night_2k.exr`（BC6H HDR，進版控 OK；4K 原檔留 Downloads）。

**下半球沙漠地面**：自訂 skybox shader `Live2DAction/Environment/SkyboxNightPanorama`（legacy CG lat/long + exposure + rotation + tint + horizon-down darken）—— `_HorizonDarken` 把地平線以下淡成近黑，只留上方星空。材質 `Skybox_NightRogland.mat`（exposure 0.62、rotation 205°、horizonDarken 0.92）。

**runtime 切換**：地圖串流不把 Map_School 設 active scene，Boss 戰實際用 GreyboxTest 的白天 skybox 渲染。`BossDomainScreenVFX.BeginDomain()` 時 swap `RenderSettings.skybox` + 壓低 ambient(0.35) + 開低霧，快取原值，退場/OnDisable/OnDestroy 完整還原（「進入 Boss 支配領域，天空本身就變了」）。控制器 +欄位 `domainSkybox` / `darkenEnvironment` / `domainAmbientIntensity` / `domainAmbientColor`；BeginDomain/EndDomain +`ApplyEnvironment`/`RestoreEnvironment`。

**改**：`BossDomainScreenVFX.cs`（+夜空/ambient swap）；`BossDomainScreenVFXSetup.cs`（+匯入 EXR、建夜空材質、wire domainSkybox；修好 CloseScene 後 `boss.name` 的 MissingReferenceException — 先存字串）。**新增** `SkyboxNightPanorama.shader`、`Skybox_NightRogland.mat`、`rogland_clear_night_2k.exr`。

**驗證**：EditMode 12/12 綠。Play 模式（frozen-frame）確認 BeginDomain 換成夜空、ambient 1.0→0.35、綠色領域邊界在暗背景下明顯；EndDomain 跑完 exit → skybox/ambient/fog 全還原、`s_Material` NULL。詳見 `Docs/YUANPEI_LOGO_SKY_BOSS.md` §夜空全景圖。

### 追加94 續 176（2026-09-06）— Boss 外觀被夜空色調染到 → 加 4 個可調鈕

使用者：「boss 的外觀似乎有被夜空全景圖影像(色調)，能調整嗎」。真因：續175 換夜空時 ambient 硬設飽和深藍綠 + `ambientMode=Flat` + `DynamicGI.UpdateEnvironment()` 重烘反射，整個場地(含 Boss)被染色。

`BossDomainScreenVFX.cs` 新增：`domainAmbientColorTint`(0=不改色相只調暗，預設0.35)、`domainAmbientIntensity`(0.35→0.5)、`updateReflectionsFromSky`(預設 false = 不重烘反射，Boss 高光維持原樣)、`bossFillLight`(選填 Light，domain 期間開/退場關，可把 Boss 單獨重新照亮)。ApplyEnvironment 改成 `Color.Lerp(場景原 ambient, domain 色, tint)`；退場完整還原 ambientMode/三色/reflection。`BossDomainScreenVFXSetup` 同步新預設值。

驗證：EditMode 12/12 綠。Play 實測 ambient (0.212,0.227,0.259)@1.0 → (0.157,0.176,0.203)@0.5（同色相只調暗），Boss 白羽/紅刃回到接近正常，夜空+綠邊界仍在；EndDomain 全還原。

### 追加94 續 177（2026-09-06）— alt 靜走模式：更慢、身體搖擺減少（沉浸式觀景行走）

使用者：「調整 alt 靜走模式 目前移動模式還是太快，身體搖擺太明顯，請真的做成 rpg 遊戲中的緩慢沉浸式行走 觀賞景觀的感覺」。

分析：Maya Locomotion blend tree 的 3 個 <0.8 子節點(threshold 0/0.4/0.8)其實**全部是 `NewWalk`**,threshold 2 才是 `NewRun`。`walkSpeed` 原本 0.9 > 0.8 → 混了一點 `NewRun` 進來(額外搖擺);而且 0.9 translation 太快。

改法:
- `CharacterMovement.walkSpeed` 0.9 → **0.55**(~27% 跑速,完全落在純 NewWalk 區間,無 run 混入)。code default + GreyboxTest 的 Player/Cat 序列化值都同步。
- `CharacterAnimatorLink` 新增 `walkAnimatorSpeed`(0.65):`IsWalking && grounded` 時 `animator.speed = 0.65`,整段走路 clip(步頻＋烘進去的胯部/肩膀搖擺)一起放慢 → 更沉穩、腳步不打滑(0.55 translation vs NewWalk ~0.9 authored × 0.65 ≈ 0.585,接近吻合)。退出走路模式 → `animator.speed` 回 1。
- `ICharacterSpeedSource` 加 `IsWalking`(Enemy/Boss → `false`);`CharacterMovement.IsWalking` 已存在直接滿足介面。
- 抽出純函式 `CharacterAnimatorLink.ComputeGroundAnimatorSpeed(...)`,7 個新 EditMode 測試。

驗證:EditMode 20/20 綠(CharacterAnimatorLinkTests + WalkRunToggleTests)。Play 模式(frozen-frame + 反射)確認:walkSpeed 序列化 0.55、`IsWalking` true 時 `animator.speed` = 0.65、切回 run → 1.00。實際手感(是否夠慢、搖擺是否可接受)待使用者對焦 Editor Play-test;若搖擺仍太大,下一步是對 Visual/胯部骨骼做程序性 upright-damp 或加 additive Animator layer。

改 `CharacterMovement.cs`、`CharacterAnimatorLink.cs`、`ICharacterSpeedSource.cs`、`EnemyAI.cs`、`BossStateMachine.cs`、`CharacterAnimatorLinkTests.cs` + `GreyboxTest.unity`(Player/Cat walkSpeed)。

### 追加94 續 178（2026-09-06）— 靜走「姿勢還是跑步」→ 換成真正的慢走 clip（玩家專用 override）

使用者：「速度正確但是 姿勢還是跑步 能調整與速度匹配的姿勢嗎」。真因：靜走時 translation 已經是 0.55(正確),但播的 clip 是 Maya 的 `NewWalk`(cycle 0.83s)—— 只比 `NewRun`(0.70s)慢一點點,姿勢本身就是小跑步。在 Speed 0.55~0.6 時 Locomotion 是 ~90% `NewWalk` + 10% `NewIdle`,沒有 run 混入,但 NewWalk 這支 clip 本身太急。

改法:新增 `PlayerImmersiveWalkSetup.cs`(選單「Tools/Live2DAction/Setup Player Immersive Walk Pose」)——
- 把 `TC_Sword_Free_Pack/KBS_Walk_F_001_IP`(cycle **1.17s**,真正放鬆的散步)的 import 設 Loop Time = true
- 建 `NewAnimator_PlayerImmersiveWalk.overrideController`(wrap 共用的 `NewAnimator`),把 `NewWalk` → `KBS_Walk_F_001`
- 指到 **Player 的 Visual Animator**(GreyboxTest)—— **玩家專用**,共用的 `NewAnimator.controller`(中立者1/守望者也在用)完全沒動

配合 `CharacterAnimatorLink.walkAnimatorSpeed`(0.65):KBS_Walk 1.17s × (1/0.65) ≈ 1.8s/cycle 的緩慢刻意散步,跟 0.55 位移大致腳步吻合。手感微調就調 `walkAnimatorSpeed`(調高=散步快一點,調低=更慢)。

驗證:編譯無錯、選單執行成功、Play 讀取確認 Player Visual controller = `NewAnimator_PlayerImmersiveWalk` (AnimatorOverrideController)、靜走時播 KBS_Walk。實際姿勢觀感待使用者對焦 Editor Play-test(截圖受翅膀 cosmetic 遮擋 + frozen editor 限制,無法清楚呈現)。

新增 `PlayerImmersiveWalkSetup.cs` + `NewAnimator_PlayerImmersiveWalk.overrideController`;改 `KBS_Walk_F_001_IP.fbx`(loopTime)+ `GreyboxTest.unity`(Player Visual Animator controller)。

### 追加94 續 179（2026-09-06）— 靜走速度微調（0.55→0.70）

使用者：「接下來調整速度 有點太慢」。`CharacterMovement.walkSpeed` 0.55 → **0.70**(~35% 跑速,仍完全落在純 walk blend,無 run 混入 — Play 確認 Speed 0.7/0.8 都是 ~100% KBS_Walk)。`CharacterAnimatorLink.walkAnimatorSpeed` 0.65 → **0.82** 配合(KBS_Walk 1.17s ÷ 0.82 ≈ 1.43s/cycle,舒適的放鬆散步節奏,腳步跟得上)。Player + Cat 的 code default + GreyboxTest 序列化值同步。EditMode 全綠、Console 無錯誤。

### 追加94 續 180（2026-09-06）— yuanpei_LogoSky 觸發 Boss 戰的 6 拍過場動畫

使用者不滿意舊過場,要求:①360全景圖從地平線往上晴天→夜 ②鏡頭拉近 boss ③boss 邊轉圈邊升起 ④鏡頭拉遠、玩家跳過去劈砍 ⑤側面近 2-shot、劈中瞬間 boss 蓄力頂飛玩家 ⑥正式開戰。決定:白天用 shader 內建漸層、boss 從地下升起、~12s、不可跳過。

Coroutine 驅動(沿用 `DeathDissolve` 模式,不用 Timeline)。新 `YuanpeiIntroCinematic.cs` —— `YuanpeiEncounter.StartEncounter` `yield` 它跑完再 `boss.BeginEncounter(playIntro:false)`。玩家/相機控制 + CharacterController + boss AI hand-off(`finally` + failsafe),控制腳本 runtime 依型別解析(跨場景不能序列化)。

- `SkyboxNightPanorama.shader` +`_NightRise`(0晴/1夜,夜色 sweep 從 `d.y=-1.15` 往 `1.15` 爬)+ `_DayZenith/_DayHorizon/_DayGround` 白天漸層
- `BossDomainScreenVFX` 改用 `domainSkybox` runtime 實例 + `SetNightRise()`
- `YuanpeiBoss` `BeginEncounter` +`playIntro` 參數、新 `DriveRiseAndSpin(startPos, startScale, t01, spin)`
- `YuanpeiEncounter` +`introCinematic` 欄位 + `IntroThenFight` coroutine

新增 `YuanpeiIntroCinematic.cs` / `YuanpeiIntroCinematicSetup.cs`(選單) / `YuanpeiIntroCinematicTests.cs`(9)。改 `YuanpeiBoss.cs` `YuanpeiEncounter.cs` `SkyboxNightPanorama.shader` `BossDomainScreenVFX.cs` `Map_School.unity`。

驗證:用 `EditorApplication.Step()` 逐幀跑完整段(Editor 失焦也能驗)—— 拍1 `_NightRise` 0→0.98、拍3 boss Y −2.5→6.5 / scale 85→400 / 自轉、拍5 玩家拋物線飛出 ~10m 峰值 Y≈2.2、拍6 IsRunning=False / State=Hover / 控制全還原、Console 無錯、EditMode 331/331 綠。細部時長/鏡頭/顏色/力道待 Play-test 微調;HUD 隱藏(`playerUiRoots`)+ 細緻躍擊動作待補。詳見 `Docs/YUANPEI_LOGO_SKY_BOSS.md` §觸發 Boss 戰的過場動畫。

### 追加94 續 181（2026-09-06）— 過場動畫 v2:全景圖放慢、boss 原地升空、空中水平 2-shot 頂飛

使用者修訂:①全景圖渲染太快 → 放慢 + 鏡頭再拉遠,看到大部分天空慢慢變夜 ②boss 不要跑到玩家背後 —— 在**原本位置**轉圈升到**真正的高空**,鏡頭斜向拍 boss → 玩家跳到 boss 面前(高空水平平行線)→ 玩家出手前一瞬鏡頭拉近特寫(玩家左側面 / boss 右側面)→ boss 快速蓄力後仰再往前把玩家頂飛 → 鏡頭拉遠拍玩家被擊飛落地。

- `YuanpeiIntroTimeline.Default` → `SkyWipe 5.0 / PushToBoss 1.8 / BossRise 2.6 / PlayerLeap 2.6 / Clash 3.0 / Settle 1.6`(共 16.6s;舊 ~12s)。拍1 鏡頭 `skyCamBack=34 / skyCamHeight=15 / skyCamAimHeight=16`(遠 + 高 + 朝天瞄),`SetNightRise` 走完整 5s。
- `RunBeats` 重寫:boss 埋在**競技場中心自己的點**(`arenaCenter + down*3`),從不橫移;拍3 `DriveRiseAndSpin(center, …, bossAirAltitude=13, …)` 升到 `floor+13 ≈ Y13.5`;拍4 玩家 `leapEnd = bossPos - flatDir*airStandoff` **同 Y**(高空水平線),`side = Cross(up, flatDir)` → 玩家 screen-L / boss screen-R;拍5 k<0.22 定格特寫、0.22–0.5 boss `+flatDir*bossChargeBack` 後仰 + disc tilt、k≥0.5 `HitStop` + `Staggered` + boss lunge、玩家沿拋物線飛到 `playerHome - flatDir*launchBackDistance`(Y 落回 `groundY`)、鏡頭拉遠追墜落;拍6 ease 到玩家背後交還控制。
- `YuanpeiBoss.DriveRiseAndSpin` **移除 `config.maxWorldY` 夾制**(cinematic-only:過場期間 boss `enabled=false` 所以 `ClampWorldY` 不會打架,`SettleToHoverPose`(仍夾制)在開戰前把 boss 放回戰鬥高度)—— 這是本次關鍵修正:舊版 boss 升空被 `maxWorldY=8` 夾住,升不到「高空」。
- `YuanpeiIntroCinematic.cs` 新欄位:`skyCamBack/Height/AimHeight`、`bossStartDepthBelowArena`、`bossAirAltitude`、`airStandoff`、`bossChargeBack`、`launchBackDistance`、`launchArcHeight`、`closeFov`。`CrossFade` 用 `Animator.HasState` 護欄(避免 "State could not be found");躍起改用 `SetFloat("Speed", 2f)` 而非 CrossFade 巢狀 state。

驗證:`EditorApplication.Step()` 逐幀 + 6 張 game-view 截圖 —— 拍1 mid `_NightRise=0.42` 鏡頭 `(-2,15.5,-72)` / 拍1 end 全夜遠景 + 綠色支配領域邊框 / 拍3 boss 升到 Y12–13.5 原地自轉 / 拍4 玩家升到 boss 同高 / 拍5 特寫 2-shot 玩家左 boss 右 / 拍5 頂飛 玩家空中被擊退 / 拍6 玩家落地 `(-2,1.1,-92)` boss Hover 控制還原。`PlayerGuard` 過場後 `enabled=false` 是 `YuanpeiEncounter.ApplyNoDefenceRule` 的**既有規則**(spec §8.1 此戰禁防禦),非 bug,Victory/Defeat 會還原。EditMode 34/34(YuanpeiIntroCinematic + BossDomainScreenVFX + CharacterAnimatorLink)綠。細部節奏/鏡位/力道仍待使用者聚焦 Play 微調。

### 追加94 續 182（2026-09-06）— 過場動畫 v3:真實躍擊動畫 + 電影加速/慢動作 + 落地硬直

使用者:拍4「玩家跳過去劈砍」的動畫不真實(要用真的普通攻擊、跳過去那瞬間畫面播放速度加快帶電影感);拍5 boss 擊退動畫不真實(只要稍微傾斜 → 往後 → 往前一個「頂」的動作,而且整段用電影雙方交打的慢速感);落地後應該看到玩家做出**硬直動作(架式條滿格的那個)**。

- **拍4 真實躍擊**:`SetTrigger("AttackComboSword")`(→ `KBS_Sword_ATK_Combo_01_001_IP` 真刀連段,不再用 Speed 參數硬套跑步);先 `Jump`(→ `NewJump`)再在 k≥0.6 觸發揮刀。`Time.timeScale` envelope:k<0.55 從 1 → `leapTimeScale`(1.5)加速衝刺,之後 → `clashTimeScale`(0.4)進入慢動作。
- **拍5 慢動作交打**:`Time.timeScale = clashTimeScale`(0.4) 整段。boss 動作簡化為 k<0.4 微傾 −6° + 後退 0.7m → k0.4–0.6 前傾 +4° + 前「頂」2.2m → k>0.6 ease 回原位(不再是舊版的大 lunge)。玩家被頂飛沿拋物線落地,`timeScale` 於墜落段 ease 回 0.9。
- **落地硬直**:玩家**觸地瞬間**(grounded && lk≥0.9)`_stance.AddPostureDamage(MaxStance*2)` → `IsStaggered` → `StaggerAnimationLink` 撐住 `KneelingDown` 跪姿(= 架式條滿格的那個)。玩家 `StancePoise.staggerDurationSeconds` 場景值=1.2s → 跪 ~1.2s 後自動恢復。拍5 尾鏡頭改為掃到落點的地面 3/4 shot(`landCam = landSpot + side*5 + (-flatDir)*4.5 + up*2.4`),玩家落地跪姿填滿畫面(舊版 farCam 太遠玩家縮成一點)。
- **時間軸/還原**:`YuanpeiIntroTimeline.Default.Clash` 3.0 → **2.4**(scaled 秒,在 0.4x 下 ≈ 6s 慢動作實時)。失效保護鐘 `Time.time` → **`Time.unscaledTime`**(慢動作下 scaled time 跑太慢會誤觸;`realtimeSinceStartup` 又會被 Editor 逐幀驗證的實牆鐘誤觸)。`LockActors` 接管 `Time.timeScale` + 玩家 `Animator.speed`(重設 1),`CharacterAnimatorLink` 也納入停用清單(否則它每幀搶 Speed / animator.speed);`UnlockActors` 全部還原 + `_stance.EndStagger()` 乾淨起身。
- `YuanpeiBoss.SettleToHoverPose` 加 `visualRoot.localRotation = _skyVisualLocalRot`(清掉拍3 累積的自轉 yaw + 拍5 tilt,開戰時 disc 回正)。

新欄位:`leapTimeScale`(1.5)、`clashTimeScale`(0.4)、`groundStaggerHoldSeconds`(1.0)。`YuanpeiIntroCinematic.cs` / `YuanpeiBoss.cs` 改。

驗證:`EditorApplication.Step()` 逐幀跑完整段 + 4 張截圖 —— 拍4 timeScale 1→1.5 衝刺(`NewJump` → `AttackComboSword`)/ 拍5 特寫 timeScale 0.4 玩家揮刀(左)boss disc 傾斜(右)/ boss 後退 z−104→−107 再前頂回 −105 / 玩家拋物線落地 `(-2,1.1,-92)` timeScale ease 回 1 / **落地 `stagger=True` `KneelingDown` 撐 ~1.2s** / 拍6 `run=False` `timeScale=1` `anim.speed=1` boss `Hover` CharacterMovement/AnimatorLink/PlayerInput 全還原。EditMode 20/20(YuanpeiIntroCinematic + BossDomainScreenVFX)綠、Console 無錯。細部力道/節奏仍待使用者聚焦 Play 微調。

### 追加94 續 183（2026-09-06）— 過場動畫 v4:慢動作劈砍 → boss 後/前 RAM 交會 → 面向 boss 倒地起身

使用者不滿意舊拍4–6:①玩家應**衝**到 boss 一個武士刀距離(電影視角)→ **慢動作**播**普通劈砍** → 劈砍快碰到 boss 那一瞬**反被 boss 撞開擊退**(不再是加速衝刺)②boss 撞擊動作要真實 —— **boss 往後 → 往前 → 兩者交會 → 玩家被擊飛 → 玩家落地視角** ③玩家落地時**面向 boss 倒地**並播倒地動作 ④再**起身** ⑤才正式開打。

- **拍4 慢動作接近**:整段 `Time.timeScale` 從 0.95 ease 到 `clashTimeScale`(0.4)—— 不再有加速衝刺。`leapEnd = bossPos - flatDir*slashStandoff`(1.8m,一個武士刀)、k≥0.72 觸發 `AttackComboSword` 普通刀劈。鏡頭側面 2-shot 往內推(玩家 screen-L / boss screen-R)。
- **拍5 交會 RAM**:定義 `contactK=0.42`(刀碰到 boss 的瞬間)。boss:k<0.22 後仰 `-bossChargeBack`(2.6m)+ 低頭 tilt −8° + spin-up;0.22→contactK **爆發前衝** `+bossRamClose`(3.4m)迎上刀 + tilt +6° + spin 520°/s;contactK→0.62 急收(recoil 到 `-back*0.4`);0.62→1 ease 回原位。contactK 時 `domainVfx.Pulse(1f)` 閃光。玩家在 contactK 後被擊飛 —— **全程面向 boss(`flatDir`,非 `-flatDir`)**,拋物線 + 短上揚後長墜落到 `groundY`,`timeScale` ease 回 1。
- **拍6 倒地 → 起身**(取代舊的 `AddPostureDamage` 跪姿):玩家落地=**面向 boss**,`ScrubState("Dead", nt)` 手動 scrub `Dead` state(Mixamo Dying = 向後仰倒地)—— 墜落段 nt 0.05→0.85、落地定 0.9。(a) `downedHoldSeconds`(1.6s)倒地定格,低機位越過倒地玩家往上看 boss。(b) `getUpSeconds`(0.95s)把 `Dead` clip **倒放**(nt 0.9→0.12)= 撐起身,鏡頭 ease 到玩家背後。(c) `CrossFade("Locomotion", 0.15)` —— **必須在交還控制前離開 `Dead` state**(該 state 無退出轉場,否則交還後玩家永遠躺著)。`ResetTrigger("AttackComboSword")` 避免排隊的揮刀在過場後才觸發。
- 移除欄位:`leapTimeScale`、`airStandoff`、`groundStaggerHoldSeconds`(舊 YAML key 被 Unity 忽略)。新欄位:`slashStandoff`(1.8)、`bossRamClose`(3.4)、`downedHoldSeconds`(1.6)、`getUpSeconds`(0.95)—— 皆有 C# 預設,`YuanpeiIntroCinematicSetup` 免重跑(除非元件遺失/ref 被清)。新 helper `ScrubState(state, nt)`(每幀 `Animator.Play` 重錨,壓過任何 AnyState 轉場)。失效保護鐘改用 `downedHoldSeconds + getUpSeconds`。
- `YuanpeiIntroCinematic.cs` / `YuanpeiIntroCinematicSetup.cs` 改。`YuanpeiIntroCinematicTests`(純 timeline 數學)不變、8/8 綠。

驗證:`validate_script` 0 error / 0 warning、`refresh_unity` 編譯無錯、Console 無錯、EditMode `YuanpeiIntroCinematicTests` 8/8 綠。細部節奏/鏡位/力道/`Dead` clip 倒放起身觀感仍待使用者聚焦 Editor Play 微調(逐幀 Step 驗不出動畫觀感)。

### 追加94 續 183b（2026-09-06）— 修:boss 衝撞方向反了 + 擊飛動畫延遲觸發

使用者:boss 本體視覺上已撞到玩家,但擊飛動畫要等 boss 都回原位好幾秒後才觸發。

**根因**(兩個):
1. **boss 衝撞方向整個反了**。`flatDir` = 玩家→boss,玩家在 boss 的 `-flatDir` 側。舊碼 `boss.position = bossHome + flatDir * fwd`、`fwd` 在「後仰」段是**負的** → boss 其實往 `-flatDir`(朝玩家)衝、還衝過頭(玩家只在 1.8m 外);「前頂」段 `fwd` 正 → boss 往 `+flatDir`(遠離玩家)退。所以視覺上「後仰」在撞玩家、「前頂」在後退。改成 `boss.position = bossHome - flatDir * fwd`(fwd 正 = 朝玩家),重排四段:0–0.30 後仰**遠離**玩家(`fwd` 0→`-bossChargeBack`)+ 後傾 −9°;0.30–contactK **加速**(`a=p²`)前衝**穿過**玩家(`fwd`→`+bossRamClose`);contactK–0.66 急退過 home;0.66–1 回位。
2. **擊飛用 SmoothStep ease-in + timeScale 從 0.4 起爬** → contactK 後玩家幾乎不動、時間又慢,等玩家真的飛出去時 boss 早就退回去了。改成 **punchy ease-OUT**:`lk = 1 - (1-p)^2.4`(接觸瞬間彈射、之後減速),`Time.timeScale = Lerp(0.7, 1, p)`(接觸當下就跳回接近實時)。水平位移用 `lk`(彈射)、垂直拋物線用 `p`(自然)。鏡頭掃動用 `Ease(p)`(不用 punchy 的 lk,否則鏡頭太猛)。`Dead` clip scrub 也改用 `p`。
3. `bossRamClose` 預設 3.4 → **2.8**(≥ `slashStandoff` 1.8,接觸幀 boss 中心穿過玩家)。`bossChargeBack` tooltip 更新為「往後(遠離玩家)拉開蓄力」。

`YuanpeiIntroCinematic.cs` 拍⑤ 改。驗證:`validate_script` clean、編譯無錯、Console 無錯、`YuanpeiIntroCinematicTests` 8/8 綠。力道/接觸幀/鏡位仍待 Play 微調。

### 追加94 續 183c（2026-09-06）— 修:躍起突兀 + 過場中玩家原地跑步 + 標註總時長

使用者:①拍④玩家飛到 boss 面前突兀 ②剛觸發過場時鏡頭裡玩家還在跑步 ③問整個過場多久。

- **玩家原地跑步**:`CharacterAnimatorLink` 過場中停用 → 拍①–③(~9s)沒人驅動 `Speed`,玩家卡在觸發瞬間的跑步 clip。`LockActors` 現在 arm 當下就 `SetFloat("Speed",0)` + `SetBool("Grounded",true)` + `SetBool("Jump",false)` + `CrossFade("Locomotion",0.12)` 立刻切回待機。
- **拍④躍起重寫**(續183c):加 `crouch`(0.16)蓄力下沉段 → **ease-OUT** 起跳(`move = 1-(1-jk)^1.8`,離地爆發、接近頂點減速)→ 跳躍弧線 `Sin(jk·π)·2.6` 峰值在 boss 之上。鏡頭改成**從拍③機位乾淨 eased slerp** 到側面 2-shot(不再是每幀 `Lerp(current, target, …)` 追);look 目標從 bossPos ease 到 玩家/boss 中點(不再瞬跳)。刀劈在 `jk≥0.68` 觸發。
- **過場總時長 ≈ 22 秒**(實際牆鐘):拍① SkyWipe 5.0s / 拍② 1.8s / 拍③ 2.6s / 拍④ ~4.2s(名目 2.6 scaled,慢動作實際更久)/ 拍⑤ ~4.2s(前段 0.4x 慢動作 2.5s + 擊飛段 ~1.7s)/ 拍⑥a 倒地 1.6s / 拍⑥b 起身 0.95s / 拍⑥c settle 1.6s。名目 `timing.Total`=16.0s 少算了慢動作膨脹 + 拍⑥的倒地/起身(那 2.55s 不在 `timing` 裡)。

`YuanpeiIntroCinematic.cs` 改(`LockActors` + 拍④)。驗證:`validate_script` clean、編譯無錯、Console 無錯、`YuanpeiIntroCinematicTests` 8/8 綠。躍起手感/鏡位仍待 Play 微調。

### 追加94 續 183d（2026-09-06）— 過場長度預設:Full(≈22s,保留) / Short(≈15s,階段性)

使用者:22s 版很喜歡要**保留**,但有階段性需求要 15s 版。

**做法**:不做均勻倍率壓縮(天空轉場/慢動作壓太快會廉價),改**手調每拍**的兩套預設。
- `YuanpeiIntroLength { Full, Short }` enum(新)。`YuanpeiIntroTimeline.Short`(新 static):`SkyWipe 3.2 / PushToBoss 1.1 / BossRise 2.0 / PlayerLeap 1.9 / Clash 1.7 / Settle 1.0`。
- 新序列化欄位:`length`(預設 **Full**)、`shortTiming`、`shortClashTimeScale`(0.55,比 Full 的 0.4 淺 → 慢動作段少吃牆鐘)、`shortDownedHoldSeconds`(0.9)、`shortGetUpSeconds`(0.7)。**Full 用的欄位(`timing`/`clashTimeScale`/`downedHoldSeconds`/`getUpSeconds`)Short 完全不碰。**
- `Play()` 開頭依 `length` 解析成私有 `_tl`/`_clashTS`/`_downedHold`/`_getUp`,`RunBeats` 全部改讀這四個(sed 機械替換,`contactK`/boss RAM 四段/`crouch`/鏡頭都是 beat 比例,自動跟著縮放)。失效保護鐘也改讀解析值。
- 場景切換:Inspector `length` enum,或選單 **Tools/Live2DAction/Yuanpei Intro Length ▸ Short (~15s) / ▸ Full (~22s)**(`SerializedObject` 只寫 `length` + 存 Map_School,不動任何 Full 調校值;順帶掉 `airStandoff`/`leapTimeScale`/`groundStaggerHoldSeconds`/`hitStopSeconds` 這些已移除欄位的殘留 YAML key)。
- Map_School 場景現值:`length` 未寫入 → C# 預設 **Full**(22s 原封不動)。`timing` = 5/1.8/2.6/2.6/2.4/1.6、`launchBackDistance` = 10、`closeFov` = 34(使用者手調值,保留)。
- Short 預估牆鐘 ≈ 14–15s(3.2+1.1+2.0 + ~2.6 leap + ~2.5 clash + 0.9 downed + 0.7 getup + 1.0 settle)。名目 `Short.Total` = 10.9。

`YuanpeiIntroCinematic.cs` / `YuanpeiIntroCinematicSetup.cs`(+2 選單)/ `YuanpeiIntroCinematicTests.cs`(+1 `Short_IsShorterThanDefaultAndSane`)改。驗證:`validate_script` clean、編譯無錯、Console 無錯、EditMode `YuanpeiIntroCinematicTests` **9/9** 綠。Short 版實際節奏待 Play 微調(切到 Short 再跑)。

### 追加94 續 183e（2026-09-06）— 元培 Boss 開場「下馬威」三線齊射

使用者:「boss 開啟時來個下馬威,同時用長矛/雷射/六連彈三種攻擊向玩家當前位置發射(瞄準身體左/中/右),集中打擊,全命中基本必死」。

一次性腳本攻擊,在過場動畫結束、玩家拿回控制那一刻觸發(`YuanpeiBoss.OpeningBarrageRoutine()` —— `IntroRoutine()` 尾 + `BeginEncounter(playIntro:false)` 分支各呼叫一次,0.35s 緩衝後 `State=AttackTelegraph` + `_attackRoutine`)。**不進 `attackPool`**。開關 = boss `playOpeningBarrage`(預設 on)。

- **前搖 1.2s**(`telegraphSeconds`):disc 蓄力脈動 + `YuanpeiScreenFlash` 紅閃 + 3 條追蹤預警光束(muzzle → 三 lane 即時投影,讓玩家判斷往哪閃)。
- **鎖定** `PlayerCenter(player)` 一次 → `YuanpeiOpeningBarrageLanes.AimPoint()`(純函式)算左/中/右三點,沿 boss→玩家 垂直軸偏移 `number4`=1.15m。
- **同瞬三線齊射**:玩家左→**長矛**(Crimson Void Spear prefab,鎖定不追蹤,`number1`=45)/ 玩家中→**雷射**(0.16s 鎖定閃 → hitscan 一次性,`number2`=40)/ 玩家右→**六連彈**(6 顆光球 0.6s 內,`number3`=15/顆)。
- **傷害**:全中 45+40+90 = **175 vs ~100 HP → 必死**;任兩類都致命(85 / 130)。往側邊走清掉整條 lane。打死走 `Health → RespawnController → YuanpeiEncounter.Defeat()`(同 BodyCharge 秒殺路徑)。
- 期間 `_majorHazardActive=true` 擋排程器;齊射是開戰第一個協程,無其他協程競爭 `StopAllCoroutines`。玩家此戰禁防禦(`ApplyNoDefenceRule`)→ 唯一解是走位。

**數值** 全在新 `YuanpeiAttack_OpeningBarrage.asset`(`attackId=OpeningBarrage`,規則 7)。選單 **Tools/Live2DAction/Setup Yuanpei Opening Barrage (下馬威)** 建 asset + 掛 boss(已跑,Map_School 存檔)。

**新增**:`YuanpeiOpeningBarrageLanes.cs`(純幾何/傷害)、`YuanpeiOpeningBarrageSetup.cs`(選單)、`YuanpeiOpeningBarrageLanesTests.cs`(6)、`YuanpeiAttack_OpeningBarrage.asset`。**改**:`YuanpeiAttackDef.cs`(enum)、`YuanpeiAttacks.cs`(+`RunOpeningBarrage`+`SpawnBarrageSpear`/`BarrageLaser`/`BarrageOrbs`)、`YuanpeiBoss.cs`(欄位 + routine + 兩處呼叫)、`Map_School.unity`。

**驗證**:`validate_script` clean、`refresh_unity` 編譯無錯、Console 無錯、EditMode **337/337**(含 6 新測)。前搖長度/lane 偏移/傷害/預警可讀性仍待 Play-test(逐幀 Step 驗不出;無 F8 快捷鍵,需走進 encounter 觸發)。

### 追加94 續 183f（2026-09-06）— 下馬威大幅強化:沒命中 + 呈現直線 + 威嚇力不夠

使用者 Play 後:①三招都沒命中玩家 ②三招都呈現「直線對玩家發射」③威嚇力不夠,攻勢大幅增強。

**沒命中根因**:續183e 把左/右 lane 各偏移玩家中心 **1.15m**(> 玩家膠囊半徑 ~0.3m)→ 站著不動也被兩條 lane 錯過;中路雷射鎖定後不追蹤,玩家微動就閃掉。

**重新設計**(`RunOpeningBarrage` + 3 helper 全改):
- 三條 stream 改成**在 muzzle 扇形發射 + 全程 homing 追玩家**(`YuanpeiProjectile` homing 追活體玩家 `homingStrength=3.4` / `number4`=0.55s)—— 站著不動必死,homing 窗結束後一個果斷側步才躲得掉。視覺散開靠發射扇角(±11°)+ homing 曲線收回,不再是三條直線。
- **長矛**:單發 → **6 連發齊射**(`ceil(count/2)`),±11° 扇形、0.08s 間隔、homing,`number1`=22/發 → 132。
- **雷射**:鎖定閃 → **追玩家 0.35s → 鎖定 → 掃過玩家 ±15° 弧**,每 0.14s tick `number2`=14 → ~56。粗 0.85 半徑。
- **六連彈**:6 → **12 顆兩波**,更寬扇形 + homing,`number3`=16/顆 → 192。
- **傷害** 全中 ≈ 132+56+192 = **380**(續183e 是 175),單一 stream 就致命。
- **前搖 1.2→1.0s** 但更兇:disc **傾斜瞄準玩家** + spin-up(120→740°/s)+ 3 條粗脈動預警光束 + 每 0.32s 一次紅螢幕閃。
- **收尾標點**:玩家腳下 `ExpandingRing` 震波 + 白紅螢幕閃。
- cascade 錯開 ~0.18s(長矛 → 雷射鎖 → 六連彈兩波)—— 讀作一套壓迫連段,不是三條平行線。

`number4` 語意 lane 偏移 → **homing 秒數**。`YuanpeiOpeningBarrageLanes.TotalDamageIfAllHit` 改成 (spearDmg×spears + laserTick×ticks + orbDmg×orbs)。asset 重跑(選單)。

`YuanpeiAttacks.cs`(`RunOpeningBarrage`/`BarrageSpearVolley`/`BarrageLaser`/`BarrageOrbs` 重寫)、`YuanpeiOpeningBarrageLanes.cs`、`YuanpeiAttackDef.cs`(註解)、`YuanpeiOpeningBarrageSetup.cs`、`YuanpeiOpeningBarrageLanesTests.cs`、`YuanpeiAttack_OpeningBarrage.asset`(重跑)改。驗證:`validate_script` clean、編譯無錯、Console 無錯、EditMode **337/337** 綠。實際命中率/homing 強度/威嚇感待 Play-test。

### 追加94 續 183g（2026-09-06）— 下馬威再修+再強化（使用者原話重複:沒命中/直線/威嚇不夠）

使用者 Play 後**逐字重複**同一句回饋 → 續183f 的改動沒生效或沒差別。

**最可能根因（已修）**:`OpeningBarrageRoutine` 開頭 `WaitForSeconds(0.35)` 時 `State` 還是 `Hover` → `TickAirCombat` 的 **0.12s watchdog** 在這個空檔就 fire 了一個普通攻擊(`_attackRoutine` + `State=AttackTelegraph`);0.35s 後 barrage 用 `_attackRoutine = StartCoroutine(...)` 直接覆蓋參照卻沒停掉舊協程 → **普通攻擊與 barrage 同時跑**,互搶 `VisualRoot` / `_majorHazardActive` / 時序 → barrage 看起來沒動 / 投射物方向亂掉沒命中。
- 修:`OpeningBarrageRoutine` **立刻**接管 FSM —— `StopCoroutine(_attackRoutine)` + `attacks.CancelAll()` + `State=AttackTelegraph` + `_globalRestUntil = now + 9999`,**在** 0.2s 緩衝**之前**。barrage 跑完才還原。

**診斷 log**(`verboseLog` on):`[YuanpeiBoss] 下馬威 OpeningBarrage FIRING (player=…)` / `[YuanpeiAttacks] 下馬威 START … boss@… player@…` / `下馬威 FIRING streams` / `下馬威 laser hit x N` / `下馬威 END`。Play 後看 Console 就知道有沒有觸發、有沒有命中。

**再強化**(續183g):
- boss **前搖時下降逼近玩家**(從 hover 點往玩家方向 35% + 下降到玩家地面 +4m)→ 壓迫感 + 縮短投射物飛行距離。前搖加**鏡頭震動**(`CameraShake`,漸強 0.04→0.18,fire 時 0.35)。
- homing `strength 3.4→4.6`、`homingSecs 0.55→0.7`(站著不動幾乎必中,homing 窗後果斷側步才躲得掉)。
- 長矛 6→**9 連發**,扇角 ±11→**±24°**;六連彈 12→**18 顆 3 波**,扇角 ±15→**±34°**(明顯先散開再 homing 收回,不是直線);雷射掃射弧 ±15→**±22°**、半徑 0.85→1.0。
- 傷害:長矛 22→26(9×26≈234)、雷射 tick 14→16(~6 tick≈96)、六連彈 16→18(18×18≈324)→ **全中 ≈ 650**。收尾腳下震波 + 硬閃 + 震動。boss 打完 0.6s ease 回 hover 點。

`YuanpeiBoss.cs`(`OpeningBarrageRoutine` 重寫)、`YuanpeiAttacks.cs`(`RunOpeningBarrage` + `BarrageLaser`/`BarrageOrbs`)、`YuanpeiOpeningBarrageSetup.cs`、`YuanpeiOpeningBarrageLanesTests.cs`、`YuanpeiAttack_OpeningBarrage.asset`(重跑)改。驗證:`validate_script` clean、編譯無錯、Console 無錯、EditMode **337/337** 綠。**待 Play + 看 Console log 確認觸發/命中**。

### 追加94 續 183h（2026-09-06）— 下馬威改回三條分離直線 + 長矛子彈放大 2×

barrage 現在有觸發了。使用者:①每種攻擊軌跡直線對其,不要看到三種攻擊重疊在一起 ②長矛子彈放大 2×,發射間距要調。

183f/g 的「扇形發射 + homing 追活體玩家」把 27 顆彈都散開再全部彎回同一個玩家中心 → 一團重疊。**改回 183e 的三 lane 概念但這次調對**:
- 三條**分離的直線 lane**:玩家左=長矛、中=雷射、右=六連彈,各鎖定沿 boss→玩家垂直軸偏移 `number4`=**0.6m** 的點。**無扇形、無 homing、無大掃射**。
- 站著不動三條都吃(子彈夠胖能涵蓋 0.6m 偏移);側步清掉離開的那條 lane。
- **長矛子彈 ×2**(`go.transform.localScale *= 2`,fallback capsule `(0.52,1.6,0.52)`,hitRadius 0.5→**0.9**,tipOffset 0.6→1.2),trail 加粗。發射間距 = `windupSeconds`=**0.2s**(新用途)。長矛數 9→**6**(左線,40/發=240)。
- 雷射改**追蹤(charge)→ 鎖死中線直射**(移除 ±22° 掃射),tick 18(~6 tick≈110)。
- 六連彈 18→**12 兩波**直線右線(20/顆=240),無扇形。
- 全中 ≈ **590**。新 helper `BarrageAxis(target)`。`number4` 語意 homing 秒數 → **lane 偏移**;`windupSeconds` → 長矛間距。

`YuanpeiAttacks.cs`(`RunOpeningBarrage` cascade + `BarrageSpearVolley`/`BarrageLaser`/`BarrageOrbs` 全重寫)、`YuanpeiAttackDef.cs`、`YuanpeiOpeningBarrageSetup.cs`、`YuanpeiOpeningBarrageLanesTests.cs`(+1)、`YuanpeiAttack_OpeningBarrage.asset`(重跑)改。驗證:`validate_script` clean、編譯無錯、Console 無錯、EditMode **338/338** 綠。三線分離度/子彈大小/間距待 Play。

### 追加94 續 184（2026-09-06）— 修:元培 boss 戰結束後相機沒回到玩家身上(勝利/失敗都是)

使用者:元培 boss 戰結束後,攝影機視角沒有正確回到玩家身上 —— 勝利或失敗都一樣。

**根因**:收尾的每一條路徑都「假設」某個過場協程會跑到最後一行去 `camCtrl.enabled = true`,沒有保底:
- **勝利** `YuanpeiEncounter.Victory()` 只 `yield return DeathDissolve()`,整段相機交還全靠 `DeathDissolve` 末行。中間 3 段 `while`(rise/vibrate/shatter + `SpawnShards`/`DriveDeathCam`)任一丟例外 → 協程死 → 相機永遠停在死亡運鏡角度。`Victory()` 本身沒有 `tpc.enabled = true` 保底(`Defeat()` 有,不對稱)。
- **失敗** ChargeCrush / 下馬威秒殺:`CrushEjectCam` 故意把 `ThirdPersonCameraController` 留在關閉,只靠 `Defeat()` 一行重開。
- **共通**:所有收尾都經 `SceneTransitionRunner.Begin() → Teleport()`,但 `Teleport()` 只做 `SnapYawToTarget()`,**沒有** `cam.enabled = true`。玩家對 boss 的鎖定(`cameraDistanceMultiplier` 2.4)也從未明確解除 —— 停用 boss 的 `LockOnTarget` 不會放掉已取得的鎖定(`_lockedTarget` 是裸 `Transform`,`IsStillValid` 只查 `activeInHierarchy` + 水平距離),要等 Map_School 卸載摧毀 boss 才失效。

**修法**(防禦性,多重保底):
1. **`SceneTransitionRunner.Teleport()`** 在既有 `SnapYawToTarget()` 旁加 `cam.enabled = true` —— 所有 fight-end 回程的單一咽喉點,不管誰把控制器關掉,回程傳送一律修好;一般 SceneGate 轉場是無害 no-op。
2. **`TargetLockController.ForceRelease()`**(新 public)—— 從正常「按鍵切換 / 超出 breakRange」流程之外立即放掉鎖定。
3. **`YuanpeiEncounter.HandCameraBackToPlayer(player)`**(新 helper)—— 重開 `ThirdPersonCameraController` + `SnapYawToTarget` + 對玩家的 `TargetLockController.ForceRelease()`。`Victory()`(DeathDissolve 之後)與 `Defeat()`(取代舊的 inline `tpc.enabled = true`)各呼叫一次。
4. **`YuanpeiEncounter.RunGuarded(inner, label)`**(新 helper,沿用 `YuanpeiIntroCinematic.Play` 的 try/catch MoveNext 模式)—— `Victory()` 改用 `yield return RunGuarded(DeathDissolve(), "DeathDissolve")`,過場中丟例外會 `Debug.LogError` 後繼續交還控制 + 場景回程,不再整段中止。

**改**:`SceneTransitionRunner.cs`、`TargetLockController.cs`、`YuanpeiEncounter.cs`。**驗證**:`validate_script` 0 error、`refresh_unity` 編譯無錯、Console 無錯、EditMode **338/338** 綠。實際勝利/失敗回程相機觀感待 Play-test(走進 encounter 打贏 or 被下馬威打死)。

### 追加94 續 184b（2026-09-06）— 續:相機修好後,回到入口卻無法移動角色

使用者:續 184 的相機修好了(「有成功傳送回入口」),但回去後**角色無法移動**。

續 184 只還相機,沒還玩家控制。fight-end 的每個過場(`YuanpeiIntroCinematic`、`DeathDissolve`、`YuanpeiExecution.Finisher`、ChargeCrush `CrushEjectCam`)各自 park 一組 `{相機控制器, CharacterMovement, PlayerCombat, PlayerInputProvider, CharacterController, Time.timeScale, 鎖定, StancePoise stagger}`,靠自己最後一行還原;任一段丟例外或兩段重疊 → 玩家回來時被凍住(`CharacterMovement.Update` 的 `staggered` gate 一被卡死 / slow-mo timeScale / 元件被關)。之前相機也壞所以使用者從沒走到這步。

**修**:`HandCameraBackToPlayer` → **`HandControlBackToPlayer(player)`** —— 在 encounter 收尾單一咽喉點一次性重新確立「玩家完全可控」:重開相機控制器 + `SnapYawToTarget`、`Time.timeScale = 1`、重開玩家的 `PlayerInputProvider`/`CharacterMovement`/`CharacterAnimatorLink`/`PlayerCombat` + `CharacterController`、`StancePoise.IsStaggered` → `EndStagger()`、`TargetLockController.ForceRelease()`。沿用 `YuanpeiIntroCinematic.UnlockActors` 的收尾還原模式。任何被強制還原的項目會 `Debug.LogWarning("[YuanpeiEncounter] fight end left player frozen - force-restored: …")` —— **下次 Play 打贏後看 Console 這行就知道真正是哪個元件卡住**。`Victory()`(DeathDissolve 之後)/`Defeat()` 各呼叫一次。

**改**:`YuanpeiEncounter.cs`。驗證:編譯無錯、Console 無新錯、EditMode 338/338 綠。**待 Play 確認**回到入口能正常移動 + 回報那行 warning(若有)。

### 追加94 續 184c（2026-09-06）— 續:失敗後傳送回入口仍無法移動 + 診斷 log

使用者:續 184b 後,**死亡**出來依舊無法移動玩家(沒回報 force-restored warning)。

**懷疑根因**:`Defeat()` 用固定 `WaitForSecondsRealtime(5.6f)` 等 `RespawnController` 復活玩家,但 `RespawnController.RespawnAfterDelay` 用的是**scaled** `WaitForSeconds(5)`。若某個過場漏還 `Time.timeScale`(留在慢動作),那 5 秒實際遠超過 5.6 秒 → `Defeat()` 在玩家還 `IsDead` / `SetActive(false)` 時就跑了 restore + 傳送 → 出來時卡死。

**改**(`YuanpeiEncounter.cs`):
- `Defeat()` 開頭若 `Time.timeScale != 1` → 強制設回 1(+ warning),讓 `RespawnController` 的 scaled 等待照時程跑完。
- 固定 5.6s 等待 → 改成**輪詢等真正復活**(`Health.IsDead` 清除 + GameObject `activeInHierarchy`),15s unscaled timebox 保底。
- `HandControlBackToPlayer` 末尾加**無條件**診斷 dump:`rootActive / timeScale / forced=[…] / move.enabled / IsDead / IsStaggered / cc.enabled / input.enabled / animStateHash`。

**待使用者**:再 Play 被下馬威打死一次,把 Console 的 `[YuanpeiEncounter] HandControlBackToPlayer dump: …` 整行貼回來 —— 這行會直接指出到底是 `IsDead` 還沒清、`IsStaggered` 卡住、`timeScale`、還是 Animator state 卡在 `Dead`。

**驗證**:編譯無錯、Console 無新錯、EditMode 338/338 綠。

### 追加94 續 184d（2026-09-06）— 真正根因:死亡後 encounter 自我重觸發 → 過場鎖定卡死

用 MCP 進 Play 檢查現場找到:失敗回到入口後 `CameraPossessionSwitcher.enabled=False`、`ViewFocusDirector.enabled=False`、`PlayerInputProvider`/`CharacterMovement`/`CharacterAnimatorLink`/`PlayerCombat` 全 `False` —— 剛好是 `YuanpeiIntroCinematic` 的 `k_PlayerControlTypes` + `k_CameraControlTypes`。而 `HandControlBackToPlayer` 的 dump 顯示它跑的當下這些都還是 `True`(`forced=[]`)→ 是**之後**又被關掉的,且 Console 沒有第二次 intro/barrage log。

**根因**:`Defeat()` 會 `Started = false`(為了假想中的原地 rematch)。但此時**死掉的玩家身體還躺在 `activationLineZ` 以南、仍在 trigger volume 內**,`YuanpeiEncounter.Update()` 每幀仍跑 → `if (!Started && _zonePlayer != null && pz <= activationLineZ) StartEncounter()` → **encounter 自己重新觸發** → `IntroThenFight` → `YuanpeiIntroCinematic.Play()` → `LockActors()` 把所有玩家控制 + 兩個攝影機 director 關掉 → 幾幀後 `SceneTransitionRunner` 卸載 Map_School → `IntroThenFight` 協程隨 GameObject 一起死 → **`UnlockActors()` 永遠不會跑** → 玩家回到入口後永久無法移動。(勝利沒事:`Victory()` 不設 `Started = false`。)

**修**(`YuanpeiEncounter.cs`):加 `_teardown` 旗標,`Victory()`/`Defeat()` 一開始就設 true 且永不清除。`Update()` 的自動觸發改 `!Started && !_teardown`;`StartEncounter()` 改 `if (Started || _teardown || boss == null) return`。真正的 rematch 是重新載入 Map_School = 全新 `YuanpeiEncounter`(`_teardown` 預設 false),所以不需要清這旗標。

**驗證**:`validate_script` 0 error、EditMode 338/338 綠(待跑)。**待 Play**:再被下馬威打死一次,確認回到入口能正常移動。

### 追加94 續 184e（2026-09-06）— 使用者 Play 確認修好

進 Play 走進學校 → 被下馬威打死 → 回到入口**可正常移動**;勝利路徑也正常。續184d 的 `_teardown` 是真正的修正,其餘(`HandControlBackToPlayer` 全套還原 / `SceneTransitionRunner` 相機保底 / `Defeat` 輪詢等復活 / `TargetLockController.ForceRelease`)保留為防禦層。`HandControlBackToPlayer` 的無條件 dump warning 收成「只有真的 force-restore 了才印」。EditMode 338/338 綠。
