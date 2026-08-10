# Known Issues

## 阻塞項

1. **076/077 Live2D 素材著作權**（高風險，Phase 3 前必須解決）：目前唯一可用的 Live2D 角色模型是《Fairy Tail》同人素材，僅能作內部原型佔位，不得進入任何對外 Build。Alpha 開始前需要原創或合法授權的 Live2D／2D 角色素材，否則 Live2D 劇情演出功能無法進入 Alpha。詳見 `ASSET_LICENSES.md`。
1b. **Player2 機甲模型來源不明**（高風險，Phase 3 前必須解決）：`MechaModel_DoNotShip/MechaCharacter2.fbx` 來源與授權都無法驗證，外觀疑似既有機甲動畫作品設計，AI 已警告風險，使用者仍要求保留作內部靜態看板。跟 076/077 同等級的阻塞項——**絕對不能進入任何對外 Build**，Alpha 前必須換成原創或合法授權的素材，或直接移除。
2. ~~缺少 3D 人形角色模型~~ → 已解決（2026-08-10），見下方「Humanoid 角色佔位」項。
3. **灰盒原型手感／攝影機尚未人眼驗證**（中風險，Phase 2 開始前應確認）：Phase 1 的移動、攻擊、Cinemachine 第三人稱攝影機都只透過 `-batchmode -runTests` 自動化測試驗證過邏輯正確性，也用命令列算圖截過一張畫面（見下方 Live2D 立牌項），但**沒有人在互動式 Unity Editor 裡實際按過 Play**。是否好操作、攝影機軌道與滑鼠視角是否順暢、掩體方塊視覺是否合理，都需要使用者親自打開 `GreyboxTest` 場景 Play 一次才能確認。

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

## 待確認

- 本機沒有配置 Unity MCP 或其他可互動的 Editor 自動化工具，本次 Phase 1 全程透過 Unity 命令列 `-batchmode`／`-executeMethod`／`-runTests` 完成，AI 端無法產生「已手動 Play 驗證」的證據，這類驗證一律需要使用者自行操作。
- 手把輸入是否列入垂直切片範圍，尚未決定（`C:\Live2DFighter` 的經驗是手把部分尚未完成測試）。

## 已解決

- ~~Unity MCP 或其他 Editor 自動化工具是否要在本專案配置~~ → 已確認本機無此類工具，Phase 1 全程用命令列批次模式完成（2026-08-10）。
- ~~Cubism SDK 尚未匯入驗證~~ → 已匯入 5-r.4.2 並確認在 URP 下可渲染（需搭配自寫 shader，見上方 Live2D 立牌視覺項）（2026-08-10）。
