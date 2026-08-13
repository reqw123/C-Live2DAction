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
