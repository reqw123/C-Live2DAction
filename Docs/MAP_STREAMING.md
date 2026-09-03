# 地圖串流（Map Streaming）

> 目標（使用者 2026-09-02）：做成開放世界那樣 —— 角色在哪個地圖，就只占用那個地圖的資源，需要時才載入。
> 方案：**多場景 additive**（用內建 `SceneManager`，不引入 Addressables）。分階段，每階段可回退。
>
> **續 78（2026-09-03）轉向**：學校改為**大門互動式**進入（`SceneGate`），不再是靠近自動串流（`MapStreamer`）。
> 走到學校門口 → 按 E → 載入畫面 → 跑完直接站在校園裡。`MapStreamer.cs` 留在磁碟，之後若空島之類要無縫串流可再用。

## 架構

- **常駐場景** `GreyboxTest.unity`：整個遊玩過程都在。包含 本地（Ground + BoundaryWall_*）、
  空島（SkyIsland_*、傳送門、中立者1~3、時間試煉）、以及所有跨地圖物件
  （Player / Cat / Buggy / 相機群 / CameraPossession / HUD Canvas / GameManager / ViewDirector /
  BossIntro 管理物件 / DevTools / BossAnimationDebugMode）。`VehicleRoad` 也留在這裡當「往南有路」的常駐視覺連接。
- **區域場景**（按需 `LoadSceneAsync(Additive)` / `UnloadSceneAsync`）：
  | 場景 | 內容 | 錨點 | 面數 |
  | --- | --- | --- | --- |
  | `Map_School.unity` | 學校 ground + `SchoolWall_*` ×5 + `yuanpei_*` ×4（MainBuilding / ModernGlassLibrary / PalmLinedLibrary / QuietCampusPlaza） | (0, 0, -115) | ~11.9M tris |

  區域場景**不放自己的燈** —— 沿用常駐場景的 Directional Light + skybox ambient（本專案目前無 lightmap bake）。

## `SceneGate`（`Assets/_Project/Game/World/SceneGate.cs`）—— 現行進出方式（續 78）

大門一座一顆（`[RequireComponent(Collider)]`，root 的 collider 是 isTrigger 判定範圍）。一個元件雙向：

| 欄位 | 進入門（`SchoolGate_Enter`，在 GreyboxTest） | 離開門（`SchoolGate_Exit`，在 Map_School） |
| --- | --- | --- |
| `sceneToLoad` | `Map_School` | （空） |
| `sceneToUnload` | （空） | `Map_School` |
| `arrivalPosition` | `(0, 1.1, -92)`（校園內） | `(0, 1.1, -78)`（車道上、本地側） |
| `arrivalYaw` | 180（面向校園） | 0（面向本地） |
| `promptText` | 「按 E 進入元培大學」 | 「按 E 離開元培大學」 |
| 位置 | `(0,0,-82)` 車道南端 | `(0,0,-86)` 北牆缺口內側 |

**視覺（續 80-82，續 83 加大嘗試已於續 84 回退）**：一片播放紅色漩渦影片的 Quad（`PortalSurface`，**9×5**、中心 local y2.7）+ 一片隱形實心 `Blocker`（BoxCollider 9×5×0.3）。root 上 11×6.5×5 trigger。入口 / 校內離開兩座門完全一致。
**影片視覺 `PortalVideoSurface.cs`**（掛在門的 `PortalSurface` Quad）：
- 影片 `Assets/_Project/VFX/Gate/PortalVortexVideo.mp4`（1280×720 / 10s / H.264 baseline / bt709）。
- `Awake` 建 per-instance `RenderTexture`(640×360，建完 `GL.Clear` 黑) + `VideoPlayer`（RenderTexture 模式、loop、playOnAwake、無音訊）→ per-instance 的 `GatePortalVideo.mat`（shader `Live2DAction/VFX/AdditiveUnlit`，`Blend One One`）。
- full `billboard` 每幀轉向攝影機；`pulse` 輕微縮放；`tint` 0.8。`depthLayers` **0**（續 85 —— 疊層疊在灰底上變矩形；先只留主體）。
- **自我修復**（續 85）：`Update` `if (isPrepared && !isPlaying) Play()` + `Awake` 加 `Prepare()` —— scene-0 載入時 AddComponent 的 VideoPlayer 有時 prepare 了卻沒開始播（`playOnAwake` 早於設 clip），入口門（開場載入）因此變隱形黑 quad、校內門（中途載入）沒中 → 「地圖外沒有門」。
- **沒有畫面提示**（續 85 —— 使用者不要那個文字框；漩渦本身就是提示）。`SceneGate` 只剩 trigger + 按 E。
- **未解**：限制範圍解碼的「黑底」實際 ~0.06-0.10 灰 → quad 矩形邊界有點灰白（續 83 想用硬 `_Cutoff` 解、太狠讓門消失、續 84 回退）。之後用 smoothstep。
- 兩座門各自 RT + 材質 instance，`OnDestroy` 釋放。

**轉場流程** —— 跑在 **`SceneTransitionRunner`**（`Assets/_Project/Game/World/SceneTransitionRunner.cs`，單例，掛 GreyboxTest 常駐 `SceneTransitionRunner` GO），**不是門物件上**（續 81：離開門在 `Map_School` 裡，`UnloadSceneAsync` 會把門連 coroutine 一起銷毀 → 卡在中間、「只能進不能出」）。`SceneGate` 按 E → `SceneTransitionRunner.Instance.Begin(...)`：
1. `ScreenFader.SetLabel("載入中…")` + `SetCovered(true)`，等 `IsFullyCovered`
2. `sceneToLoad` 非空且未載入 → `LoadSceneAsync(Additive)`，等 `isDone`
3. 壓 `settleFrames`(3) 幀（collider cook + 首張畫面）
4. 傳送：關 `CharacterController` → `SetPositionAndRotation(arrival, yaw)` → 開回；`ThirdPersonCameraController.SnapYawToTarget()`
5. 再壓 2 幀讓相機貼上（相機位置每幀硬算 `ComputeCameraPosition`，不 damp）
6. `sceneToUnload` 非空且已載入 → `UnloadSceneAsync` → `Resources.UnloadUnusedAssets()`（**在傳送之後**）
7. `ScreenFader.ClearLabel()` + `SetCovered(false)`。`IsRunning` 擋雙門/連按重入。

黑幕期間 `PlayerInputProvider` 全程歸零輸入（`ScreenFader.IsCovered`）。無畫面提示（續 85 移除），玩家進 trigger 後按 E 即可（按下就清 `_playerInside`，傳送不觸發 `OnTriggerExit`）。

## 過場遮罩 `ScreenFader`（`Assets/_Project/Game/World/ScreenFader.cs`，續 74）

單例，掛常駐場景的 `ScreenFader` GameObject。`Awake` 自建全螢幕黑 `Canvas`（`sortingOrder 32000`，蓋過所有 HUD）+ `CanvasGroup` + 置中 `Text` label。
API：`SetCovered(bool, fadeSeconds)`（用 `unscaledDeltaTime`，不受 hit-stop / timeScale 影響）、`SetLabel(string)` / `ClearLabel()`（label 跟黑幕同一個 CanvasGroup，一起淡）。場景沒有 `ScreenFader` → 呼叫端 no-op、不報錯。

## 驗證（GreyboxTest，需對焦 Editor 的 Play）

1. Play，站本地。`SceneManager.sceneCount` == 1，學校完全沒載入。
2. 沿 `VehicleRoad` 往南走到 `SchoolGate_Enter`（z −82），被門板擋住、頭上出現「按 E 進入元培大學」。
3. 按 E → 畫面淡黑「載入中…」→ 學校載入 → 玩家出現在校園內 (0,1.1,-92) 面向建築 → 淡回。`sceneCount` == 2。
4. 走到校園裡的 `SchoolGate_Exit` → 按 E → 淡黑 → 回到車道 (0,1.1,-78) 面向本地 → `Map_School` 卸載 → `sceneCount` == 1。

## Phase 2b（續 75）— 卡頓根治 + 輸入鎖

- **yuanpei 四棟的 3M 面 `MeshCollider` 全移除**（cook 卡頓的來源）。改用：
  - `yuanpei_MainBuilding` / `ModernGlassLibrary` / `PalmLinedLibrary` 各一顆 scene-root `<name>_Collision`（zero rotation、`BoxCollider` = 該建築 renderer 世界 AABB，底部落在 y≈0.5 地面）。
  - `yuanpei_QuietCampusPlaza` 直接不放 collider —— `學校` 那顆 60×60 `BoxCollider`（頂面 y=0.5）就是地板。
  - Box collider cook 幾乎零成本 → **載入卡頓消除**（不只是被黑幕蓋住）。粗略 box 碰撞夠 greybox 用；要精細再手調。
- **遮罩期間鎖玩家輸入**：`PlayerInputProvider.Update` 開頭若 `ScreenFader.Instance.IsCovered` → 整個 command 歸零（跟沒鍵盤同一條路徑）。淡出+hold 全程鎖，reveal 一開始就解。AI 的 `IInputCommand` 不受影響。

## 已知限制 / 待辦（見 KNOWN_ISSUES）

- **NavMesh**：學校目前沒有 AI，所以沒 bake。之後學校有 AI 時，`Map_School` 裡放 `NavMeshSurface` 各自 bake。
- **Player 還在常駐場景**：目前 本地/空島 也在常駐場景，尚未抽成獨立 `Core.unity`。
- **跨場景引用**：目前學校是純景物（無腳本），零跨場景引用。之後區域場景放 AI／互動物件時，
  需要執行期角色註冊表（`GameRuntime.Player` 之類）或由 `MapStreamer` 在載入完成後接線。

## 分階段藍圖

1. ✅ **續 73**：抽 `Map_School` 出來 + `MapStreamer` 距離觸發。
2. ✅ **續 74**：`ScreenFader` 過場遮罩。
2b. ✅ **續 75**：yuanpei MeshCollider → box proxy（cook 卡頓消除）＋ 遮罩期間鎖玩家輸入。
3. ✅ **續 78**：轉向大門互動式 —— `SceneGate` 進入門（本地→學校）+ 離開門（學校→本地），載入畫面「載入中…」，跑完直接站在目標地圖。`MapStreamer_School` instance 移除（`.cs` 留磁碟）。
4. spawn anchor 系統（多個出生點、由門指定 spawn id）；貓／載具也能用門。
5. 抽 `Core.unity`（Player/Cat/Buggy/相機/HUD/managers），本地變 `Map_Bendi`，空島變 `Map_SkyIsland`。
6. 每區域各自 NavMeshSurface + Lighting Settings；記憶體 profiling。
