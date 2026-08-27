# Agent Notes — 環境重建 & 踩過的坑

給在**乾淨機器上 clone 這個專案**、之後由 AI agent 接手的人。這裡是不寫在程式碼裡、
但接手前必須知道的事。專案規則見 `../CLAUDE.md`，當前狀態見 `KNOWN_ISSUES.md` / `CHANGELOG.md`。

---

## 1. 乾淨 clone 後的環境重建

版控裡**已經有**的（clone 完就齊）：

- Unity 專案本體：`Live2DAction/Assets/`、`ProjectSettings/`（26 檔）、`Packages/`
- 精確 Unity 版本：`Live2DAction/ProjectSettings/ProjectVersion.txt` → `6000.0.81f1 (6238fec1e98f)`
- 套件依賴：`Packages/manifest.json` + `packages-lock.json`（決定性還原）
- 內嵌套件 `com.unity.springbone`（`Packages/com.unity.springbone/`，已一起 commit）
- **Live2D 完整鏈**（見下方第 6 節）：Cubism SDK 5-r.4.2（`Assets/Live2D/Cubism/`，含**所有平台的原生
  plugin** — Windows/macOS/Linux/Android/iOS/UWP）＋ 076/077 佔位模型（已複製進 `Assets/_Project/Live2D/`）
  ＋ URP 版 shader。**`C:\question\` 不是依賴**，SDK 不用重新下載或啟用授權。
- 全部美術/場景資產（直接進版控，無 LFS — clone 較大但不需要額外步驟）
- `.mcp.json`（Unity MCP client 設定）

需要**人手動**做、AI 無法代勞的：

1. **裝 Unity Editor `6000.0.81f1`**（透過 Unity Hub）＋ 對應模組（至少 Windows Build Support）。
   授權啟用（Unity 帳號登入）也必須本人做。
   預設安裝路徑：`C:\Program Files\Unity\Hub\Editor\6000.0.81f1\Editor\Unity.exe`
2. **首次用 Unity Hub 開啟 `C:\Live2DAction\Live2DAction\`**，讓它還原套件、重建 `Library/`
   （`Library/`、`Temp/`、`obj/`、`*.csproj`、`*.sln` 都在 `.gitignore`，會自動重生）。
3. **啟動 Unity MCP 橋接**：Editor 裡開 `Window > MCP For Unity`（CoplayDev unity-mcp v10.1.2），
   確認 HTTP server 跑在 `http://127.0.0.1:8080/mcp`（跟 `.mcp.json` 對上）。
   Claude Code 在專案目錄啟動時會自動讀 `.mcp.json`；第一次要在 `/mcp` 或設定裡核准這個 server。
4. **記得**：`~/.claude/projects/.../memory/` 的 auto-memory **不會**跟著 repo 走。本檔就是那些
   記憶的版控化副本；新機器上以本檔為準。

---

## 2. Unity batchmode 操作（AI 跑 `-runTests` / Editor 腳本時）

指令範例（Git Bash）：

```bash
"/c/Program Files/Unity/Hub/Editor/6000.0.81f1/Editor/Unity.exe" \
  -batchmode -nographics -projectPath "C:/Live2DAction/Live2DAction" \
  -runTests -testPlatform EditMode -testResults "C:/.../results.xml" -logFile -
```

- 算圖驗證時**不要**加 `-nographics`（會拍出全灰畫面）；跑測試時可以加。
- **動手前先確認使用者的互動式 Editor 沒開著**：`tasklist | grep -i unity.exe` 必須是空的。
  兩邊同時開同一個專案時，命令列**第一次呼叫可能還是回報結束碼 0**，下一次才報衝突，
  中間已經可能寫壞場景檔。結束碼 0 ≠ 安全。

### batchmode 卡死 → 強制關閉後一定要清鎖檔

若 `-batchmode` 卡在 Editor 啟動流程（`Loaded scene 'Temp/__Backupscenes/0.backup'` 之後、
`TrimDiskCacheJob` / `Scanning for USB devices` 附近，**在任何測試碼執行之前**），
`taskkill //F //IM Unity.exe` 之後這三個鎖檔不會釋放，下一次啟動會卡在同一個點：

```bash
rm -f "C:/Live2DAction/Live2DAction/Temp/UnityLockfile" \
      "C:/Live2DAction/Live2DAction/Library/ArtifactDB-lock" \
      "C:/Live2DAction/Live2DAction/Library/SourceAssetDB-lock"
```

清完若還是卡在同一點：**換一個指令**（不同 `-testFilter` / 不同 platform，或先跑無過濾的完整
EditMode 套件）當下一步診斷，不要一直重跑同一條。這是環境 flake，不是程式碼 bug。

### headless batchmode 的時序怪異（既有測試偶爾失敗是已知的）

- 單幀 `Time.deltaTime` 極小（~0.0003s）；靠固定幀數或 `WaitForSecondsRealtime` 估模擬時間都不可靠，
  要自寫迴圈依 `Time.realtimeSinceStartup` 累積。此環境積分效率約理論值 30%。
- `CharacterMovementTests` / `JumpTests` 偶發失敗（差值在容許門檻附近）是已知 flaky，重跑即過；
  只要**本次改動相關的測試**兩輪都過就算數。
- `CharacterController.minMoveDistance` 預設 `0.001` 會靜默丟棄小位移 → 已在所有手建 CC 的地方設 `0f`。

---

## 3. Unity MCP 驅動 Editor 的失焦陷阱

Editor 視窗**沒有 OS 焦點**時（純用 MCP 工具驅動、沒有人點進視窗，這是常態）：

- **Play Mode 整個遊戲迴圈會凍結**：`Time.frameCount` 卡在 1–2、`Time.time≈0.02`，即使真實時間
  過了好幾分鐘、`execute_code` 呼叫都成功。`OnTriggerEnter`/`OnCollisionEnter`/`LateUpdate`/
  coroutine 全部靜默不執行。**下結論說碰撞/觸發邏輯壞掉之前，先檢查 `Time.frameCount`。**
  你自己同步呼叫的 `CharacterController.Move()` 還是會動，容易誤以為「一切正常在跑」。
- **PlayMode 測試會卡死 Test Runner**：`editor_state.tests.is_running` 卡 `true`、`current_job_id: null`，
  之後每個碰 test runner / asset database 的 MCP 呼叫都失敗（`"tests_running"`）。
  - `manage_editor(action="stop")` 先試，能清 Play Mode 轉場卡住。
  - `run_tests(clear_stuck=true)` 只清 MCP 自己的記帳，**清不掉** Unity 內部的 `tests.is_running`。
  - `validate_script` 不受影響（純 Roslyn），可當驗證退路。
  - 卡住 ~10 分鐘試遍上述都沒用 → **停手，請使用者點進 Editor 視窗 / 開 Test Runner 視窗**
    恢復焦點，不要繼續用 MCP 硬敲（每次重試都白燒 turn）。

**結論**：MCP 驅動下，PlayMode 驗證不可靠。優先用 EditMode 測試 + `validate_script` + 程式碼審查，
需要真的 Play 就把修改做完、請使用者本人在互動 Editor 裡試。

---

## 4. 手動調校值是權威，不是 bug

### 攝影機（`Assets/_Project/Game/Camera/ThirdPersonCameraController.cs`）

使用者透過反覆 Play-test 直接在 Inspector 手調 `distance` / `targetOffset` 等，**那是權威**。
歷史上 AI 曾發現「場景序列化值與程式碼註解/預設值不符」就「修正」回舊預設值，**覆蓋掉使用者的實際調校**。

- 現行設計：自寫的 free-look 滑鼠環繞（讀 `Mouse.current.delta` 累加 yaw/pitch），
  參考原神/鳴潮的「攝影機隨滑鼠、WASD 相對攝影機方向移動」。
- 已試過又**放棄**的做法（別重新提案，除非使用者再要求）：first-person、右肩鎖定 rig、
  整套 Cinemachine 軌道/瞄準系統（五種合理修法實測全無效，最後整個移除 Cinemachine 改自寫）。
- 把攝影機 yaw 鎖到角色自身旋轉、同時 `CharacterMovement` 又用同一個 yaw 算 strafe 方向 →
  無限自旋回饋迴圈（`CameraRelativeMovementRegressionTests` 專門防這個回歸）。
- 若場景序列化的攝影機/手感值跟註解不符：**先問使用者**，沒有 CHANGELOG 記錄不代表是意外。

### `CharacterController.stepOffset = 0`（Player / Enemy / TrainingDummy）

刻意設 0，**不是**預設疏漏（Unity 預設 0.3）。見 `Assets/Editor/Bootstrap/FixCharacterControllerStepOffset.cs`。

- 原因：預設 0.3 時，一個 CC 走進另一個 CC 會沿對方膠囊頂自動往上爬，Y 漂移卡住 → 角色「消失」bug。
- 影響：任何攀爬/階梯/分層地形**不能**靠 stepOffset 自動上階；連小台階都會完全擋住水平移動。
  要用平滑連續的斜坡 collider（受 `slopeLimit` 控制），參考 `JapaneseShrineVistaSetup.cs` 的
  `Pagoda_ClimbRamp`（傾斜 box collider，與裝飾用的分層屋頂 mesh 分開）。
- **不要**為了「traversal 卡卡」把 stepOffset 調回去 — 會重現原 bug。

---

## 5. Live2D 模型

**乾淨 clone 完全不用另外處理，直接能跑。** 整條鏈都在版控裡：

| 項目 | 位置 |
|---|---|
| Cubism SDK for Unity 5-r.4.2 | `Assets/Live2D/Cubism/`（198 個 script + 範例模型） |
| 原生 plugin（每個平台） | `Assets/Live2D/Cubism/Plugins/`：Windows `Live2DCubismCore.dll` x86/x64、macOS `.bundle`、Linux/Android/HarmonyOS `.so`、iOS `.a`、UWP、Emscripten |
| 076（納茲）佔位模型 | `Assets/_Project/Live2D/PlaceholderCharacter/c_7001.*`（moc3 + model3.json + texture + 4 motions + prefab + controller + mask texture） |
| 077（露西）佔位模型 | `Assets/_Project/Live2D/PlaceholderCharacter077/c_7002.*` |
| URP 版 Cubism shader | `Assets/_Project/Rendering/Shaders/CubismUnlitURP.shader` |

- **`C:\question\` 不是 clone 的依賴**：076/077 是「複製 + 改名」（`c_7001`/`c_7002`）進專案的，
  `C:\question` 原始檔沒被動過，runtime 沒有任何東西引用外部路徑。SDK 不用重新匯入、不用啟用授權。
- **URP shader 注意**：SDK 內建的 Cubism shader 是 Built-in RP 的 CGPROGRAM，缺 `LightMode` tag，
  URP 下不會被渲染管線挑到 → 用自寫的 `CubismUnlitURP.shader`（僅還原不透明度/色彩混合，
  **沒有實作 Mask 裁切**，含裁切的模型會顯示異常）。
- **已知怪異**（不用每次回報，見 `KNOWN_ISSUES.md`）：`CubismModel3Json.ToModel()` 產生的根物件
  `gameObject.name` 會反覆變回空字串（原因未查出，只影響用名字 `Find()` 和 Hierarchy 標籤，
  不影響渲染/邏輯）；每次用 `EditorSceneManager.OpenScene` 開這個場景存檔後，076/077 立牌名字
  就要順手重跑 `Tools/Live2DAction/[Fix] Rename Live2D Standees To 076-077`。
- **法律限制照舊**：076/077 是《Fairy Tail》同人模型 → **絕對不得進任何對外 Build**。
  `ASSET_LICENSES.md` 現在共 6 個 `_DoNotShip` 素材（076、077、Mecha 機甲、Player5「lacrimosa」、
  狼的末路武器、原神劍展示組），全部只能在開發機做內部原型驗證。這條 AI 不能自己放行。

## 6. 其他

- **`GreyboxSceneBuilder.Build()` 會先清空整個場景再重建**它自己寫的內容 — 曾誤刪當天尚未 commit
  的角色/立牌。**只做局部修改就用 `EditorSceneManager.OpenScene` 直接改**（照 `Fix*.cs` 的模式），
  絕不要為了改個材質就呼叫 `Build()`。真要整場重建，先問使用者，並照 `CHANGELOG.md` 記錄的完整
  工具執行順序重跑所有後續視覺/立牌腳本。
- **場景是二進位 YAML，不友善版控**。改場景前先 `git status` — 有未 commit 內容代表工作目錄是唯一副本。
- **判斷「X 是否在 Y 上/內」時，不要用斜角透視截圖** — 前縮法會讓不同距離的物件在畫面上疊在一起。
  用正交（orthographic）俯視 RenderTexture，或直接拿世界座標比對區域邊界 /
  `Camera.WorldToScreenPoint` 對照實際位置。
- **`ModelImporterAnimationType` enum 順序**：`None=0, Legacy=1, Generic=2, Human=3`。
  讀寫 `.meta` 的 `animationType: N` 時別猜「3 = Generic」（3 是 Human）。
  無 mesh 的骨架/動畫 FBX 用 `avatarSetup = CopyFromOther` 可能讓 `animationType` 停在 Human
  但**靜默產出 0 個 AnimationClip**；改用 `Generic + NoAvatar` 可靠產生可用 clip。
- **「本地」= Ground map**：`GreyboxTest.unity` 裡 `GreyboxSceneBuilder.CreateGround()` 建的
  30×30 區域（X/Z ∈ [-15,15]）＋ 四道 `BoundaryWall_*` collider，是玩家出生/休息區。
  跟空島（`Torii_FloatingIsland`，見 `FLOATING_ISLAND_GUIDE.md`）不同。
- **角色命名對照**（2026-08-19 重新命名）：`Player`（Maya，玩家）、`Mecha`（舊 `Player2`，
  DoNotShip 機甲看板）、`TrainingDummy`（舊 `Player3`，站樁假人）、`Enemy`（舊 `Player4`，Arisa，
  含完整戰鬥/空戰 AI）。`KNOWN_ISSUES.md` 裡沒標日期的舊條目一律用改名前的稱呼，讀時對照換算。

---

## 維護

這份檔案是 `~/.claude/projects/C--Live2DAction/memory/` 的版控化副本。
在該 memory 目錄新增/修改記憶時，把仍然相關的內容同步進這裡，讓乾淨 clone 也拿得到。
