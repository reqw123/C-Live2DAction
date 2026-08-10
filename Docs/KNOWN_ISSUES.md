# Known Issues

## 阻塞項

1. **076/077 Live2D 素材著作權**（高風險，Phase 3 前必須解決）：目前唯一可用的 Live2D 角色模型是《Fairy Tail》同人素材，僅能作內部原型佔位，不得進入任何對外 Build。Alpha 開始前需要原創或合法授權的 Live2D／2D 角色素材，否則 Live2D 劇情演出功能無法進入 Alpha。詳見 `ASSET_LICENSES.md`。
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
- 目前**沒有掛任何 Animator Controller／動畫**，Play 起來角色會是 T-pose 靜止不動（bind pose），這是預期中的暫時狀態；同作者的「Universal Animation Library」（也是 CC0、用同一套 Humanoid 骨架設計）可以之後接上 Idle/Run/Attack 等動作，尚未下載。
- 只複製了 Male 版本＋沒有髮型（`Hairstyles/Rigged to Head Bone/FBX (Unity)/` 裡有對應頭骨的髮型 FBX，需要的話之後再加）。
- 用命令列算圖確認角色貼圖、比例、站姿都正確顯示，沒有粉紅材質；同樣**不是使用者本人在互動 Editor 裡看到的結果**。

## 待確認

- 本機沒有配置 Unity MCP 或其他可互動的 Editor 自動化工具，本次 Phase 1 全程透過 Unity 命令列 `-batchmode`／`-executeMethod`／`-runTests` 完成，AI 端無法產生「已手動 Play 驗證」的證據，這類驗證一律需要使用者自行操作。
- 手把輸入是否列入垂直切片範圍，尚未決定（`C:\Live2DFighter` 的經驗是手把部分尚未完成測試）。

## 已解決

- ~~Unity MCP 或其他 Editor 自動化工具是否要在本專案配置~~ → 已確認本機無此類工具，Phase 1 全程用命令列批次模式完成（2026-08-10）。
- ~~Cubism SDK 尚未匯入驗證~~ → 已匯入 5-r.4.2 並確認在 URP 下可渲染（需搭配自寫 shader，見上方 Live2D 立牌視覺項）（2026-08-10）。
