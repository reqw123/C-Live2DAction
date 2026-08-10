# Known Issues

## 阻塞項

1. **076/077 Live2D 素材著作權**（高風險，Phase 3 前必須解決）：目前唯一可用的 Live2D 角色模型是《Fairy Tail》同人素材，僅能作內部原型佔位，不得進入任何對外 Build。Alpha 開始前需要原創或合法授權的 Live2D／2D 角色素材，否則 Live2D 劇情演出功能無法進入 Alpha。詳見 `ASSET_LICENSES.md`。
1b. **Player2 機甲模型來源不明**（高風險，Phase 3 前必須解決）：`MechaModel_DoNotShip/MechaCharacter2.fbx` 來源與授權都無法驗證，外觀疑似既有機甲動畫作品設計，AI 已警告風險，使用者仍要求保留作內部靜態看板。跟 076/077 同等級的阻塞項——**絕對不能進入任何對外 Build**，Alpha 前必須換成原創或合法授權的素材，或直接移除。
2. ~~缺少 3D 人形角色模型~~ → 已解決（2026-08-10），見下方「Humanoid 角色佔位」項。
3. **灰盒原型手感尚未完整人眼驗證**（中風險，Phase 2 開始前應確認）：使用者已實際 Play 過一次並回報一個真實 bug（見下方「方向鍵畫圈」項，已修好），證明人眼驗證確實會抓到自動化測試漏掉的問題。移動方向已修正，但攻擊手感、攝影機滑鼠視角順暢度、掩體方塊視覺配置是否合理，仍需要使用者再實際 Play 確認。

## Live2D 立牌視覺（2026-08-10 新增，已用自動化截圖驗證，未經人眼互動確認）

- Cubism SDK for Unity 5-r.4.2 已匯入並確認可在 URP 下運作，但 SDK 內建的 `Live2D Cubism/Unlit`／`Mask` shader 是寫給 Built-in RP 的 CGPROGRAM，在 URP 下不會被渲染管線挑到（缺 `LightMode` tag），改用自寫的 `Assets/_Project/Rendering/Shaders/CubismUnlitURP.shader`（僅還原不透明度/色彩混合，**沒有實作 Mask 裁切**，含裁切的模型會顯示異常，076 這次沒觀察到明顯裁切問題）。
- `CubismModel3Json.ToModel()` 回傳的 model 根物件，在 Play 模式下讀取 `gameObject.name` 會是空字串，即使編輯模式存檔當下、`.unity` 檔案裡明明寫的是設定過的名字（例如 "Visual"）；原因未查出（SDK 原始碼裡沒找到任何清空名字的程式碼）。**這只影響用名字 `Find()` 這個物件**，不影響渲染或邏輯；`PlayerCubismVisualSetup.cs` 已改成用「摧毀 Player 底下所有子物件」而非按名字找，繞開這個問題。
- 縮放公式踩過一次真的 bug：`CubismCanvasInformation.CanvasHeight` 是「像素」單位，要除以 `PixelsPerUnit` 才是模型在 Unity 裡的實際單位大小（這個模型剛好兩者都是 1200，換算後模型原始大小約 1 單位高）；一開始誤把 `CanvasHeight` 當成已經是 Unity 單位直接拿來算縮放比例，導致角色縮小成肉眼幾乎看不到的小點。已修正為 `TargetHeight / (CanvasHeight / PixelsPerUnit)`，實測角色現在跟訓練假人差不多高。
- 目前只顯示 moc3 的靜止綁定姿勢（沒有播放任何 motion），076 的綁定姿勢剛好帶著出招用的火焰特效（見 `076-納茲.md` 的 Parameter 對照表），所以角色看起來像是一直在使用技能，不是真的在待機——這是預期中的暫時狀態，等 Phase 2 接上 idle motion 播放後才會正常待機。
- 立牌用 `CubismBillboard`（`Assets/_Project/Game/Characters/CubismBillboard.cs`）永遠面向 `Camera.main`，只轉 Y 軸；朝向公式假設模型預設面向本地 -Z（未經人眼確認是否方向正確，若實際 Play 後發現角色背對鏡頭，切換該元件 Inspector 上的 `Face Away Instead` 勾選框即可，不需要改程式碼）。

## Humanoid 角色佔位（2026-08-10 新增，取代 Player 身上的 Live2D 立牌）

- 使用者要求接上真正的 3D Humanoid 角色後，Player 的視覺改為 Quaternius「Universal Base Characters」（CC0，見 `ASSET_LICENSES.md`）的 `Superhero_Male_FullBody.fbx`，取代先前的 Live2D 立牌。Cubism SDK／自寫 URP shader／`CubismBillboard` 都還留在專案裡（劇情演出功能仍會用到 Live2D，只是 Player 戰鬥視覺換掉了），076 的 Live2D 模型目前沒有被任何場景物件引用。
- FBX 已設成 Humanoid Rig（`ModelImporter.animationType = Human`），材質手動建立（URP Lit + BaseColor + Normal 貼圖，**沒有處理 Roughness 貼圖**，用預設光滑度，非最終品質）。
- 目前**沒有掛任何 Animator Controller／動畫**，Play 起來角色會是 T-pose 靜止不動（bind pose）；同作者的「Universal Animation Library」（也是 CC0、用同一套 Humanoid 骨架設計）可以之後接上 Idle/Run/Attack 等動作，尚未下載。
- 只複製了 Male 版本＋沒有髮型（`Hairstyles/Rigged to Head Bone/FBX (Unity)/` 裡有對應頭骨的髮型 FBX，需要的話之後再加）。
- 用命令列算圖確認角色貼圖、比例、站姿都正確顯示，沒有粉紅材質。
- **2026-08-10 當天稍後被下方的 Maya（動漫風角色）取代成 Player 的主要視覺**，這組 Quaternius 素材保留在專案內作為備用/未來敵人角色使用，未刪除。

## Maya 動漫風角色佔位（2026-08-10 新增，取代上面的 Humanoid 佔位，目前 Player 使用中）

- 使用者要求動漫風角色，在 Sketchfab 找到「3D動漫風角色屋」的「Maya」（CC-BY 4.0，見 `ASSET_LICENSES.md`，**發布 Build 前必須加上署名**）。下載需要 Sketchfab 帳號登入，使用者本人完成登入後才能下載，AI 端無法代為登入（帳號/登入操作一律禁止代勞）。
- 這個素材包本身就是完整的 Unity 套件（FBX＋Humanoid Rig 已由原作者設定好、Animator Controller、Idle/Walk/Run/Jump/Fall 動畫、13 個材質、Prefab），複製時連同原始 `.meta` 檔一起複製，讓 Prefab／Animator 內部的參照 GUID 能對上，不用重新手動連結。
- 材質原本用 Unity Built-in RP 的 Standard shader（在 URP 下會顯示粉紅色），寫了 `ConvertMaterialsToUrp()`（在 `PlayerMayaVisualSetup.cs` 裡）批次把 13 個材質的 `_MainTex`／`_Color` 讀出來後，shader 換成 URP Lit、重新指定到 `_BaseMap`／`_BaseColor`——這次材質只用到主貼圖＋色調，沒有用到法線貼圖，所以不需要處理法線貼圖的轉換。
- Prefab 自帶的 Animator 預設 `Apply Root Motion = true`，會跟我們自己的 `CharacterController` 移動邏輯打架（動畫本身位移 + 程式碼位移疊加），已在腳本裡關掉。Animator 的 Speed/H/V/Jump/Fly/Aim/Grounded 參數目前完全沒有接線，全部維持預設值（`Grounded` 預設是 true，其餘是 0/false），角色會穩定停在 Idle 動畫，**沒有走路/跑步動畫的接線**，等 Phase 2 要做真正的移動時才需要把 `CharacterMovement` 的速度餵給這些參數。
- 這個角色目前**沒有穿衣服**（只有內衣的裸體版本），素材頁面上有另一個「Anime Girl Casual Outfit」是不同作者的獨立模型，沒有一起下載；要換裝或穿衣服是之後才需要處理的事。
- 用命令列算圖確認：角色貼圖、比例、Idle 待機動作（不是 T-pose）都正確顯示，沒有粉紅材質。**仍是自動化截圖驗證，不是使用者本人在互動 Editor 裡看到的結果**。

## 已拒絕的素材：來源不明的 2P 測試模型（2026-08-10）

- 使用者提供一個檔案（`C:\Users\homec\Downloads\fbx_9f3e955d-0c7b-4b77-8887-7ce85726100e\modelToUsed.fbx`）想試著當「2P 角色看板」用，檢查後**拒絕採用**，已從專案移除（`Player2` 場景物件與複製進來的 FBX 都刪掉了，使用者 Downloads 裡的原始檔案沒有動）。
- 拒絕原因：(1) 無骨架、無材質貼圖，只能當靜態裝飾；(2) **單一網格就有近 100 萬三角面**，遠超即時遊戲角色的合理預算（一般 1~5 萬面），效能上不可用；(3) 算圖出來後外觀是倒臥姿勢＋草帽，視覺上很像既有版權角色的公仔/雕像，來源與授權完全無法確認，風險比 076/077（至少知道是同人 Live2D）更不明確，直接牴觸 `CLAUDE.md` 角色必須原創的規則。
- 如果之後想再嘗試類似「無主 3D 模型」的素材，务必先確認來源與授權，並檢查面數是否在合理範圍，才進到匯入這一步。

## Player2 機甲看板：使用者自行承擔風險保留（2026-08-10）

- 同一天使用者又提供另一個檔案（`fbx_53e34751-943b-45ee-8202-72ab8b01c4f5/modelToUsed.fbx`），跟上面那個模型是**同一產線輸出**（完全相同的 Blender 4.2.3 匯出簽章、同樣沒有骨架/貼圖、同樣精確地是 100 萬三角面）。
- 算圖後外觀是高達風機甲戰士（翅膀狀背部裝甲＋大型劍狀武器），AI 提出跟上次一樣的警告（面數不可用＋外觀疑似既有機甲動畫作品設計），**這次使用者明確表示已確認來源並自行承擔風險，要求保留使用**。
- 已依照 076/077 的模式處理：複製進 `Assets/_Project/Characters/Placeholder/MechaModel_DoNotShip/`（資料夾名稱刻意標註 DoNotShip），登記進 `ASSET_LICENSES.md` 的「禁止進入對外 Build」表，並列為新的高風險阻塞項（見上方阻塞項 1b）。
- 因為沒有骨架，只能當**靜態裝飾看板**（`Player2` GameObject，位置 (2.5, 0, -2)），套用預設 URP Lit 材質（無貼圖，純白色），沒有任何動畫或戰鬥邏輯，也沒有作為可操作的「2P 玩家」。
- 用命令列算圖確認擺放位置、比例、材質都正常顯示（無粉紅材質），跟 Player／TrainingDummy／掩體方塊共存於同一場景不互相干擾。

## 已修正：方向鍵移動會 360 度畫圈（2026-08-10，使用者實際 Play 回報）

使用者實際在 Editor 裡 Play 後回報：純按左/右移動時，角色會像畫圓一樣持續轉圈，不是直線移動。這是這次專案第一個由「使用者實際操作」抓到、自動化測試完全沒發現的 bug（`CharacterMovementTests` 用的是固定朝向的假攝影機，測不出真正 Cinemachine 攝影機才會出現的問題），值得記錄完整的排查過程供之後參考：

- **錯誤診斷 1**：一開始懷疑是 `CinemachineOrbitalFollow.TrackerSettings.BindingMode` 沒設對（懷疑攝影機軌道的參考座標系跟著角色朝向轉）。改成 `BindingMode.WorldSpace`（原始碼裡明確寫著這個模式應該讓 `GetReferenceOrientation()` 恆等於 `Quaternion.identity`，完全跟角色朝向無關）——**實測完全沒有效果**，問題原封不動。
- **錯誤診斷 2**：懷疑即使 `BindingMode` 正確，Cinemachine 套件內部某處仍然讀了角色的旋轉，於是加了一個 `CameraFollowAnchor`（只跟隨角色位置、永遠不跟隨角色旋轉的中介物件），把攝影機的 Follow/LookAt 都改指向這個 anchor 而非角色本身——**實測依然完全沒有效果**，兩次「合理的」修法都沒用，逼著往下深挖。
- **真正原因**：用一個會即時印出角色朝向／攝影機朝向／攝影機位置的診斷測試直接量測，才發現——攝影機軌道角度（`HorizontalAxis.Value`，只受滑鼠控制）其實從頭到尾都穩定停在 0，沒有被複製角色朝向；問題其實出在 `CinemachineRotationComposer`（負責讓攝影機「看向」角色的 Aim 元件）。角色純橫向移動（strafe）時，就算完全沒有旋轉，攝影機的瞄準角度也會因為「追蹤一個持續橫向平移的目標」而自然掃動——這是正常、預期中的攝影機行為。而 `CharacterMovement` 原本是直接讀攝影機**組合後、包含瞄準修正**的 `Transform.forward` 來算「相對攝影機的移動方向」，這就形成了迴圈：移動方向決定角色朝向 → 角色平移 → 攝影機瞄準角度跟著平移量掃動 → 下一幀「相對攝影機」的定義又變了 → 角色又要跟著再轉一點 → 无限循环，按住方向鍵就會持續畫圈。
- **正確修法**：不要用攝影機「组合後」的 `Transform.forward`，改用攝影機軌道**未經瞄準修正**的原始角度（`CinemachineOrbitalFollow.HorizontalAxis.Value`，只受滑鼠輸入影響，跟角色平移/旋轉完全無關）。新增 `ICameraYawSource`／`OrbitalCameraYawSource`（`Assets/_Project/Game/Camera/`），`CharacterMovement` 改成優先讀這個穩定的 yaw 值（找不到就退回讀 `Camera.main` 的 yaw 分量，供沒有 Cinemachine 的測試環境使用）。已移除前兩次無效的嘗試（`CameraFollowAnchor`、`BindingMode` 調整）避免留下誤導性的死路徑。
- **新增永久回歸測試**：`CameraRelativeMovementRegressionTests.cs`（PlayMode），直接載入真正的 `GreyboxTest` 場景（含真的 Cinemachine 攝影機，不是假的固定攝影機），模擬持續按住純橫移輸入，驗證角色朝向會收斂穩定、不會持續漂移——這是專門防止這個 bug 未來悄悄回歸用的。
- **順帶修好一個測試隔離問題**：新增這個回歸測試後，發現它跟既有的 `CharacterMovementTests` 互相污染——如果回歸測試先跑，它載入的真實場景（含 Ground／TrainingDummy／掩體方塊／真攝影機）沒有被清掉，導致 `CharacterMovementTests` 新建的假角色會跟殘留的場景物件碰撞、`Camera.main` 也可能解析到錯的攝影機。修法：`CharacterMovementTests` 的 `[SetUp]` 改成先清空目前場景裡**所有**根物件，不只是清攝影機標籤，確保每個測試都是從真正乾淨的空場景開始，不假設自己是唯一會建立場景物件的測試。
- 13 個 EditMode + 10 個 PlayMode 測試全數通過，且連續跑 3 次確認沒有間歇性失敗。

## 已修正：走路/跑步腳步滑行（2026-08-10，使用者實際 Play 回報）

使用者在上面「畫圈」bug 修好後再次實際 Play，回報「左右移動時會順移，而非自然行走」——即角色有正確轉向、正確直線移動，但腳步動畫看起來像滑冰/飄移，不是踏實的走路。

- **原因**：`CharacterMovement.moveSpeed` 原本是 5，遠超過 Maya 的 Locomotion Blend Tree 最高速度門檻（2，對應 Run 動畫）。動畫本身用的移動速度（帶動畫演出的視覺位移）跟角色實際位移速度對不上，導致腳步跟地面位移不同步。
- 檢查過 Maya 動畫片段的 RootT 曲線，確認**沒有可用的 Root Motion** 可以拿來反推「正確」速度（例如 `NewRun.anim` 的 `RootT.z` 一個 0.7 秒循環裡只在 0.084～0.140 之間小幅擺動，屬於原地搖晃，不是真正的步幅位移），所以無法用資料驅動的方式算出精確值，只能用「跟 Blend Tree 最高門檻對齊」這個合理推測去調。
- **修法**：`moveSpeed` 從 5 降到 2（`FixMoveSpeedForAnimation.cs`），並簡化 `CharacterAnimatorLink`：原本的「先正規化、再乘上任意倍率」公式改成直接 `Clamp(currentSpeed, 0, maxAnimatorSpeed)` 餵給 Animator 的 Speed 參數，理由同上——沒有 Root Motion 可以拿來做更精確的映射，用更簡單、更少猜測空間的公式更誠實。
- **仍待確認**：這是推理出的合理起點，不是已證明正確的數值，需要使用者實際 Play 後用眼睛確認腳步是否貼地。

## 已修正：攝影機視角與角色朝向脫鉤，改用自寫攝影機（2026-08-10，使用者實際 Play 回報）

使用者回報：「我認為是攝影機視角問題，而且左右按鍵控制的方向是顛倒的。按下左鍵人物會往右跑並朝向正西方，有點奇怪，像是使用者視角沒有對其角色的第三人稱」。這是在上面「畫圈」bug 修好之後才浮現的第二層問題，需要完整記錄排查過程，因為五次合理的 Cinemachine 修法全部無效，最終決定放棄 Cinemachine 的軌道/瞄準系統。

- **驗證世界座標邏輯本身沒問題**：用診斷測試直接印出「按住純左移動輸入」時的世界座標位移與角色朝向，確認 `CharacterMovement` 產生的移動方向與朝向彼此一致（按左穩定產生 -X 方向的位移與朝向），問題不在移動計算本身，而在攝影機「畫面上呈現的」方向跟這個世界座標系對不上。
- **五次 Cinemachine 修法，全部實測無效**（依序嘗試，每次都基於看似合理的原始碼推理，但都被實測推翻）：
  1. 調整 `TrackerSettings.BindingMode` 為 `WorldSpace`（原始碼寫明這個模式下 `GetReferenceOrientation()` 應恆為 `Quaternion.identity`）。
  2. 加入一個只跟隨位置、不跟隨旋轉的 `CameraFollowAnchor` 中介物件，把 Follow/LookAt 都指到這個 anchor。
  3. 移除 `CinemachineRotationComposer`（Aim 元件），改用 `CinemachineOrbitalFollow`（Body）直接驅動旋轉。
  4. 把 `PositionDamping`／`RotationDamping`／`QuaternionDamping` 全部歸零，排除阻尼延遲造成的視覺誤差。
  5. 重做一次 anchor（`FixCameraAnchorRetry.cs`），這次在每個 `LateUpdate` 強制把 anchor 的旋轉鎖回 `(0,0,0)` 並直接印出驗證，確認 anchor 本身的旋轉全程真的是 identity——但攝影機的 `Transform.right`／`Transform.forward` 依然精確跟著角色的朝向漂移。
- 第五次的診斷數據直接跟 Cinemachine 套件自己的原始碼文件矛盾（`BindingMode.WorldSpace` 底下 `GetReferenceOrientation()` 就是回傳 `Quaternion.identity`，不應該被角色朝向影響），但編譯後的實際行為就是不一致，沒有再花時間深挖套件內部黑盒，改為架構層面的解法。
- **最終解法**：完全移除 Cinemachine 在這個攝影機上的使用（`CinemachineBrain`／`CinemachineCamera`／`CinemachineOrbitalFollow`／`CinemachineRotationComposer`／`CinemachineInputAxisController` 全部拿掉，`Unity.Cinemachine` 也從 `Live2DAction.Runtime.asmdef` 移除），改寫一個完全自己掌控的 `ThirdPersonCameraController`（`Assets/_Project/Game/Camera/ThirdPersonCameraController.cs`）：
  - 直接讀滑鼠 delta（`Mouse.current.delta`）累加 yaw/pitch，兩個都是這個腳本自己擁有的一般欄位，沒有任何其他系統會反過來影響它們。
  - 每個 `LateUpdate` 用 yaw/pitch 算出旋轉，用「目標點 − 旋轉 × 前方 × 距離」算出攝影機位置，直接 `SetPositionAndRotation`，沒有阻尼、沒有中間狀態。
  - 實作 `ICameraYawSource`（沿用原本 `OrbitalCameraYawSource` 的介面設計），`CharacterMovement` 讀的就是這個腳本自己的 `_yaw` 欄位，跟攝影機畫面呈現的旋轉是同一個數字來源，兩者不可能再對不上。
  - `GreyboxSceneBuilder.cs`（場景重建工具）與 `FixCameraCustomController.cs`（既有場景的一次性修正腳本）都已改用新的攝影機設定方式。
- **已知限制**：新攝影機刻意沒有做牆壁/障礙物碰撞閃避（deferred，非本次需求範圍）。
- 13 個 EditMode + 10 個 PlayMode 測試全數重新跑過，全數通過（含載入真實場景的 `CameraRelativeMovementRegressionTests`）。
- **仍待確認**：滑鼠視角操作是否順手（靈敏度、pitch 範圍）需要使用者在互動 Editor 裡實際試玩確認，AI 端只能確認場景結構正確、測試通過。

## 已修正：套用上面的攝影機修法後角色「消失」、方向鍵「沒反應」（2026-08-10，使用者實際 Play 回報）

使用者套用上一版 `FixCameraCustomController.cs` 後實際 Play，回報「角色消失了，按方向鍵也沒反應」。排查後發現這是上一個攝影機修法本身帶出的新 bug，不是移動或攝影機邏輯又倒退：

- **原因**：Maya 這個 Sketchfab 素材包裡，藏著一個素材作者自己預覽用的內嵌攝影機——一個名字與 tag 都是 `MainCamera` 的 GameObject，掛在角色脖子的骨頭上（`Assets/_Project/Characters/Placeholder/MayaAnime/Prefabs/Maya.prefab` 內部，帶著自己的 `Camera` 元件）。上一版 `FixCameraCustomController.cs` 用 `GameObject.FindWithTag("MainCamera")` 找攝影機，實際抓到的是**這個藏在角色骨架裡的假攝影機**，不是場景真正的 Main Camera。
  - 真正的 Main Camera 完全沒被處理到，舊的 `CinemachineBrain` 原封不動留著（且已經沒有可用的 Cinemachine 虛擬攝影機可以 blend 過去），畫面因此卡住不動——這是使用者觀察到「角色消失」的原因。
  - 新寫的 `ThirdPersonCameraController` 則被誤裝到角色脖子上那顆內嵌攝影機，每幀直接用軌道公式覆寫它的位置/旋轉，跟角色骨架動畫互相打架；`CharacterMovement.cameraYawSource` 也連帶指到這顆錯誤的攝影機。移動輸入其實仍然正常運作（角色的 `CharacterController` 照常移動），但因為畫面呈現完全跑掉、可見範圍也被這顆貼身攝影機弄亂，使用者才會覺得「按方向鍵沒反應」。
  - 場景裡因此同時存在兩台會渲染的 `Camera`（真正的 Main Camera + 角色骨架裡的內嵌攝影機），二者同時渲染時的疊圖/覆蓋順序不確定，進一步加重畫面異常。
- **修法**：
  1. `PlayerMayaVisualSetup.cs` 新增 `RemoveEmbeddedCameraRig()`，在每次把 Maya 模型換裝到 Player 身上時，自動找出並刪除視覺階層底下所有 `Camera` 元件所在的 GameObject，徹底清掉這個內嵌攝影機（角色本身沒有用到它，純粹是素材作者留下的殘留物）。
  2. `FixCameraCustomController.cs` 改用 `GameObject.Find("Main Camera")`（依名稱，跟 `GreyboxSceneBuilder.cs` 建立攝影機時用的名字一致）取代 `GameObject.FindWithTag("MainCamera")`，避免未來又被場景裡任何巧合帶有相同 tag 的物件騙到。
  3. 依序重新執行 `Replace Player Visual With Maya (Anime)` → `Wire Character Animator Link On Player` → `[Fix] Replace Camera With Custom Controller` 三個編輯器工具，重建乾淨的角色視覺並正確把 `ThirdPersonCameraController` 掛到真正的 Main Camera 上。
- 修法後直接檢查 `GreyboxTest.unity`：整個場景只剩一個 `Camera` 元件、`ThirdPersonCameraController` 正確掛在名為 "Main Camera" 的 GameObject 上、`CharacterMovement.cameraYawSource` 正確指向同一顆元件、場景中不再有任何 `Cinemachine` 相關殘留。
- 12 個 EditMode + 10 個 PlayMode 測試全數重新驗證通過。
- **教訓**：用 `GameObject.FindWithTag(...)` 在場景裡找「唯一」物件時，不能假設專案自己建立的物件是 tag 的唯一持有者——外部匯入的美術素材完全可能夾帶同樣 tag 的物件；改用明確的名稱／階層路徑查找，或是在腳本裡先用 `GetComponentsInChildren`/`GetComponent<Camera>()` 之類的型別檢查做二次過濾，會更保險。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：角色是否正常顯示、方向鍵移動與滑鼠視角是否正常回應。

## 已變更：攝影機改回固定視角，取消滑鼠視角控制（2026-08-10，使用者要求）

使用者回報畫面看起來還是舊的、行走仍有問題，並明確要求：「先將攝影機固定視角，並且明確 w/s/a/d 是控制角色前/後/左/右移動」，同時要求參考網路上一般 3D 遊戲的做法。查了幾篇 Unity 論壇討論與 Cinemachine Third Person Follow 文件，確認「固定角度跟隨攝影機（只跟位置、不跟旋轉）＋輸入方向相對攝影機換算」是常見且穩健的做法，跟先前造成多次 bug 的「滑鼠自由視角」是不同的設計取向，因此決定切換過去：

- `ThirdPersonCameraController` 拿掉滑鼠輸入（不再依賴 `Mouse.current.delta`、`mouseSensitivity`、`minPitch`/`maxPitch`/`invertY` 全部移除），改成 `yawDegrees`／`pitchDegrees` 兩個固定數值欄位（預設 0／25），攝影機每幀只會跟著角色的位置平移，旋轉角度永遠不變。
- 這讓 `CharacterMovement` 透過 `ICameraYawSource` 讀到的 yaw 值永遠是常數（0 度），移動方向與畫面呈現的方向從此不可能再因為攝影機旋轉而產生落差——這是比之前「滑鼠自由視角」更簡單、更不容易出 bug 的架構取捨，代價是玩家不能自己轉動視角。
- 實際驗算目前的 W/A/S/D 對應（`PlayerInputProvider.cs`）：W→`MoveInput.y=+1`、S→`-1`、A→`MoveInput.x=-1`、D→`+1`；配合 `CameraRelativeDirection` 在 yaw=0 時 forward=世界 +Z、right=世界 +X，換算後 **W＝遠離攝影機（前進）、S＝靠近攝影機（後退）、A＝畫面左、D＝畫面右**，且因為攝影機角度固定，這個對應在整個遊戲過程中永遠一致，不會像先前的滑鼠視角版本一樣隨時間漂移。
- `GreyboxSceneBuilder.cs`／`FixCameraCustomController.cs` 都已同步改成寫入 `yawDegrees`/`pitchDegrees` 而非舊的滑鼠相關欄位。
- 12 個 EditMode + 10 個 PlayMode 測試全數重新驗證通過。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：這是本次排查中第三次要求使用者實際 Play 驗證的修法，AI 端目前只能確認場景結構、欄位數值、測試通過，無法確認畫面實際觀感與手感是否符合預期。

## 已變更：改回滑鼠視角跟隨（原神風格），並修正「大跨步到很遠距離」的真正 bug（2026-08-10）

使用者對上一版固定視角不滿意，改要求「請參考原神那種動作遊戲 攝影機要跟著角色視角移動」，同時回報「移動控制還是有問題 會大跨步到很遠的距離」。這次是兩個獨立問題：

### 1. 攝影機改回滑鼠視角（原神風格）

- `ThirdPersonCameraController` 拿掉上一版的固定 `yawDegrees`/`pitchDegrees`，改回讀 `Mouse.current.delta` 累加 yaw/pitch（`mouseSensitivity`/`minPitch`/`maxPitch` 恢復，另外新增 `initialYaw`/`initialPitch` 作為起始角度），不需要按住任何按鍵，滑鼠移動就會即時轉動攝影機視角——符合原神那種「攝影機隨時跟著滑鼠視角、WASD 相對攝影機方向移動」的操作習慣。
- 這次跟先前 Cinemachine 版本的滑鼠視角**不是同一個 bug 的重演**：Cinemachine 出問題的根源是它把「跟隨位置」（Body/OrbitalFollow）和「瞄準角度」（Aim/RotationComposer）拆成兩個獨立元件，兩者對移動中角色的反應不同步才產生畫圈/漂移。我們自己寫的 `ThirdPersonCameraController` 只有一份 yaw/pitch 狀態，直接同時決定攝影機的旋轉和 `CharacterMovement` 讀到的相對方向，沒有「兩個系統各自反應」的架構，所以不會重現同一種 bug。
- `GreyboxSceneBuilder.cs`／`FixCameraCustomController.cs` 同步改回寫入 `mouseSensitivity`/`minPitch`/`maxPitch`/`initialYaw`/`initialPitch`。

### 2. 「大跨步到很遠距離」的真正原因：角色沒有真的站在地上

用一個暫時的診斷 PlayMode 測試（載入真實場景、餵入持續前進輸入、每幀記錄座標）直接量測，發現角色的 Y 座標**持續下降、完全不會穩定**——代表 `CharacterController.isGrounded` 從頭到尾都是 false，角色其實整段時間都在自由落體：

- 查場景檔案發現：Player 的 `CharacterController.height` 目前是 `1`（不是 `GreyboxSceneBuilder.cs` 原本設計的預設值 `2`），但 Player 的重生座標 Y 還是舊的 `1`——兩者搭配起來，膠囊體底部懸空在地板上方 0.5 單位，永遠碰不到地。專案裡沒有任何腳本會把 `height` 改成 1，判斷是使用者先前在互動 Editor 裡手動調整膠囊高度（可能是想解決之前「角色消失」時順手調的），但沒有同步調整重生高度，因而產生懸空。
- 角色因為抓不到地面，`_verticalVelocity` 每幀持續往下累加（永遠不會被 `isGrounded` 重設成貼地的小負值），下落速度隨時間無限增長，一旦終於碰到任何東西（地板、掩體方塊邊緣等），龐大的下墜速度加上 `CharacterController.Move` 的碰撞/滑動處理，就會讓角色瞬間被彈開一段很遠的距離——這正是使用者觀察到的「大跨步到很遠距離」。跟攝影機、輸入方向換算完全無關，是重生高度沒有跟著角色體型調整的落地判定 bug。
- **修法**：新增 `FixPlayerGroundedSpawn.cs`，直接從 Ground 的實際碰撞體世界邊界＋Player 目前的 `CharacterController.height`/`center` 反推正確的重生 Y（而非寫死常數），套用後 Player 的重生 Y 從 `1` 改成 `0.5`，膠囊底部剛好貼齊地面頂部。同時把 `GreyboxSceneBuilder.CreatePlayer()` 也改成用同樣的方式動態計算重生高度，之後即使 `height`/`radius` 再被調整，也不會又悄悄裂開一個懸空縫隙。
- 重新用診斷測試驗證：套用修法後角色 Y 座標穩定在同一個值不再持續下降，確認已經真正貼地。
- **教訓**：`CharacterController` 這類「高度/半徑」和「重生座標」是耦合的兩個數值，只改其中一個很容易留下肉眼不容易發現的懸空縫隙——同類型元件之後若要再手動微調外觀比例，最好同時檢查重生位置是否需要對應調整，或改用像這次一樣「從地面碰撞體反推」的動態算法，不要用寫死的常數。
- 12 個 EditMode + 10 個 PlayMode 測試全數重新驗證通過。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：滑鼠視角操作手感是否像原神一樣順手、角色現在是否穩定貼地行走、不再出現大跨步瞬移。

## 待確認

- 本機沒有配置 Unity MCP 或其他可互動的 Editor 自動化工具，本次 Phase 1 全程透過 Unity 命令列 `-batchmode`／`-executeMethod`／`-runTests` 完成，AI 端無法產生「已手動 Play 驗證」的證據，這類驗證一律需要使用者自行操作。
- 手把輸入是否列入垂直切片範圍，尚未決定（`C:\Live2DFighter` 的經驗是手把部分尚未完成測試）。
- **三段普攻沒有對應動畫**（2026-08-10 新增，使用者已確認範圍，非阻塞項）：Maya 目前只有 Idle/Walk/Run/Jump/Fall 動畫，`ComboAttackState` 的三段連段判定與傷害邏輯已完成並測試通過，但攻擊時角色視覺上不會播放任何揮擊動作，只有 debug 層面（`Physics.OverlapSphere` 命中、`Health` 扣血）看得出效果。之後需要找/做適合 Maya 骨架的攻擊動畫（CC0 或需授權素材），再串接到 `AttackPhase`（例如 Startup 觸發 Trigger、Recovery 結束或連段成功時處理過渡）。
- **攻擊時未鎖定/減速移動**：目前 `CharacterMovement` 與 `PlayerCombat` 完全獨立，攻擊全程角色仍可自由移動，這跟大多數動作遊戲「攻擊時至少 Startup/Active 期間會停下來或大幅減速」的手感不同，之後視實際 Play 手感決定是否要加上這個耦合。
- **閃避的無敵幀還沒接到任何傷害判定**（2026-08-10 新增）：`DodgeState.IsInvulnerable`／`CharacterMovement.IsDodgeInvulnerable` 已經實作並測試過時序正確，但 Player 目前沒有掛 `Health` 元件（場景裡只有 TrainingDummy 會受傷，還沒有任何敵人會反過來攻擊玩家），所以這個屬性目前無人查詢，等 Step ⑤ 近戰敵人 AI 讓玩家真的會被打時才需要決定 `AttackResolver`／`Health.ApplyDamage` 如何查詢攻擊目標的無敵狀態並跳過傷害。
- **閃避與攻擊系統互不干擾，沒有互相打斷的邏輯**：攻擊中可以直接閃避（不會取消攻擊狀態機，兩者各自獨立運作），閃避中按攻擊鍵一樣會照常觸發連段判定，這跟大多數動作遊戲「閃避會取消當前攻擊」或「攻擊中無法閃避」的設計不同，之後視實際手感決定是否需要加上互斥/取消規則。
- **第一人稱下攻擊方向跟著移動朝向走，不是跟著視角走**（2026-08-10 新增，使用者已確認範圍）：`ThirdPersonCameraController` 新增了可切換的第一/第三人稱視角（V 鍵），但 `PlayerCombat.attackOrigin` 預設是 Player 根物件的 `transform.forward`，而 `CharacterMovement` 只有在有移動輸入時才會轉向面對移動方向；站著不動時攻擊方向不會跟著滑鼠視角轉，這在第一人稱下尤其不直覺（玩家會預期攻擊朝準心方向）。這次範圍只處理攝影機本身，重新綁定攻擊瞄準方向留給之後的步驟（可能跟 Roadmap Step ④ 敵人鎖定一起處理，屆時會需要決定「移動朝向」與「攻擊朝向」是否該拆成兩個獨立概念）。

## 已解決

- ~~Unity MCP 或其他 Editor 自動化工具是否要在本專案配置~~ → 已確認本機無此類工具，Phase 1 全程用命令列批次模式完成（2026-08-10）。
- ~~Cubism SDK 尚未匯入驗證~~ → 已匯入 5-r.4.2 並確認在 URP 下可渲染（需搭配自寫 shader，見上方 Live2D 立牌視覺項）（2026-08-10）。
