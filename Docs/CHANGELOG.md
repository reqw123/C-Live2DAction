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
