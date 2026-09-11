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
  BossIntro 管理物件 / DevTools / BossAttackDebugMode）。`VehicleRoad` 也留在這裡當「往南有路」的常駐視覺連接。
- **區域場景**（按需 `LoadSceneAsync(Additive)` / `UnloadSceneAsync`）：
  | 場景 | 內容 | 錨點 | 面數 |
  | --- | --- | --- | --- |
  | `Map_School.unity` | 學校 ground + `SchoolWall_*` ×5 + `yuanpei_*` ×4（MainBuilding / ModernGlassLibrary / PalmLinedLibrary / QuietCampusPlaza） | (0, 0, -115) | ~11.9M tris |
  | `Map_Nijigen.unity` | 二次元 ground + `NijigenWall_*` ×5（西城，內容待補） | (-115, 0, 0) | greybox |
  | `Map_Xianshi.unity` | 現世 ground（東城，空 greybox，含 Voidmoon Gate 傳送門裝飾）| (115, 0, 0) | greybox |
  | `Map_Camp.unity` | 露營區 ground(60×60) + `CampWall_*` ×5，**續 2026-09-11**（本次任務）新增；純場地，角色「猜猜看」尚未建立 | (-50, 0, -100) | greybox |

  區域場景**不放自己的燈** —— 沿用常駐場景的 Directional Light + skybox ambient（本專案目前無 lightmap bake）。

  **連接方式分兩種**：`Map_School`/`Map_Nijigen`/`Map_Xianshi` 從本地 `BoundaryWall_South`/`West`/`East` 的缺口直接向外接（沿原點輻射的三個方向）。`Map_Camp` 沒有新開牆缺口 —— 本地四面牆已各自對應學校/二次元/現世（北牆貼武士 boss 戰鬥區沒有空間），改成從既有 **`VehicleRoad_West`（x −15~−85）中段 x≈−50 分岔一條 T 字型支線 `VehicleRoad_Camp`（z 0~−70）往南**，`CampGate_Enter` 立在支線底端 (−50,0,−67)。這個 x 值離 `NijigenGate_Enter`（x=−82，同一條路上）與 `Map_School` 的 60×60 場地邊界（世界座標 x=−30）都有安全距離，避免同一顆常駐場景裡的視覺/collider 擠在一起。

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

**視覺（續 91 重做，取代續 90 的程序化 shader）**：一片播放紅色漩渦影片的 Quad（`PortalSurface`，**13×9**、中心 local y4.1，比車道 ~7.4 寬、底邊約貼地面）+ 一片隱形實心 `Blocker`（BoxCollider 12×8×0.4）。root 上 11×6.5×5 trigger。4 座門（入口×2 + 校內/校外離開×2）完全一致。
**影片視覺（續 91）**：
- **VideoPlayer 是場景序列化元件**（編輯期就 `AddComponent` 並把 `clip`/`targetTexture`/`playOnAwake=1`/`loop`/`audioOutputMode=None` 寫進場景 YAML）。**不再在 runtime `AddComponent`** —— 那正是入口門一直播不出來的原因：`playOnAwake` 在 `AddComponent` 當下 latch，早於設 `clip`，scene-0 載入的入口門沒有第二次機會。
- 每座門一張 `Assets/_Project/VFX/Gate/RT_<gate>.renderTexture`（640×360）+ `Mat_<gate>.mat`（shader `Live2DAction/PortalVideoURP`）。
- `PortalVideoURP.shader`：取樣影片 RT，`a = smoothstep(_KeyLow 0.02, _KeyHigh 0.12, luma)` key 掉近黑背景，`Blend One One` 疊加發光（近黑貢獻 0 → **無灰白矩形基座**），`_EdgeFade` 0.05 柔化 quad 邊，`_Intensity` 2.0。
- `PortalVortexVideo.mp4`：640×360 / 10s / H.264 baseline / bt709 / **全範圍（無壓黑，角落 avgLuma 實測 0.002）** / 1.3 MB。
- `PortalVideoSurface.cs` 只做：billboard（**關**，門是固定平面）＋輕微 pulse ＋ `Update()` 裡 `if (!vp.isPlaying) vp.Play()` nudge 當保險。不建立 VideoPlayer / material。
- **沒有畫面提示**（續 85 —— 使用者不要那個文字框；漩渦本身就是提示）。`SceneGate` 只剩 trigger + 按 E。
- 殘留 warning：`Color primaries 0 … WindowsMediaFoundation`（ffmpeg 沒寫 colr atom，紅色調可能極微偏移，對這個造型無感）。

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
- **`Map_Camp` 純場地**：目前只有 greybox ground + 圍牆 + 一對門，沒有任何角色/建築/互動物件。新角色
  「猜猜看」目前沒有素材，之後補上時比照 `Docs/ASSET_LICENSES.md` 追蹤授權（同人/來源不明素材要標
  `DoNotShip`）；建築聚焦「露營主題」也待補。
- **`CampGate_Enter`/`Exit` 的實機轉場尚未 focused-Play 走過一次**：本次是用 Unity MCP 在編輯器內建物件、
  存檔、靜態座標比對 + Scene View screenshot 驗證幾何，`execute_code` 進 Play Mode 時編輯器缺 OS focus
  （已知環境限制，見 `Docs/AGENT_NOTES.md`）導致 `Time.frameCount` 卡住、協程不跑，沒能跑完整個
  `SceneTransitionRunner` 淡黑→載入→傳送流程。下次有人在前台盯著 Editor 玩，麻煩實際走一次驗證清單
  （見下方「驗證」章節，路線改成沿 `VehicleRoad_West` 走到 x≈−50 再左轉南下）。

## 分階段藍圖

1. ✅ **續 73**：抽 `Map_School` 出來 + `MapStreamer` 距離觸發。
2. ✅ **續 74**：`ScreenFader` 過場遮罩。
2b. ✅ **續 75**：yuanpei MeshCollider → box proxy（cook 卡頓消除）＋ 遮罩期間鎖玩家輸入。
3. ✅ **續 78**：轉向大門互動式 —— `SceneGate` 進入門（本地→學校）+ 離開門（學校→本地），載入畫面「載入中…」，跑完直接站在目標地圖。`MapStreamer_School` instance 移除（`.cs` 留磁碟）。
4. ✅ **續 88 / 95**：`Map_Nijigen`（西城）、`Map_Xianshi`（東城）比照學校模式，從本地西/東牆缺口接出。
5. ✅ **2026-09-11（本次任務）**：`Map_Camp`（露營區）—— 第一個**不從 BoundaryWall 缺口**、而是**從既有道路中段 T 字分岔**接出的區域場景。場地優先，角色「猜猜看」晚點做。
6. spawn anchor 系統（多個出生點、由門指定 spawn id）；貓／載具也能用門。
7. 抽 `Core.unity`（Player/Cat/Buggy/相機/HUD/managers），本地變 `Map_Bendi`，空島變 `Map_SkyIsland`。
8. 每區域各自 NavMeshSurface + Lighting Settings；記憶體 profiling。
