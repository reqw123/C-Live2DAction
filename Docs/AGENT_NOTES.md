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
- 全部美術/場景資產（直接進版控，無 LFS — clone 較大但不需要額外步驟）。**例外**：4 個
  Meshy 校園建築原始 FBX 單檔 >100 MB（GitHub 上限），**不在版控裡**，只留在原作者本機——
  clone 後這幾棟會 missing mesh（貼圖/材質仍在）。清單與補回方法見 `Docs/LARGE_ASSETS.md`。
  新的 Meshy 內嵌貼圖 FBX（`Meshy_AI_*_texture.fbx`）會被 `.gitignore` 自動擋。
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
  之後每個碰 test runner / asset database 的 MCP 呼叫都失敗（`"tests_running"` / `error: busy`）。
  - `manage_editor(action="stop")` 先試，能清 Play Mode 轉場卡住。
  - `run_tests(clear_stuck=true)` 只清 MCP 自己的 `TestJobManager` 記帳，**清不掉** Unity 內部的
    `TestRunStatus._isRunning`（被 abort 的 run 沒呼叫 `MarkFinished()`）。
  - `validate_script` 不受影響（純 Roslyn），可當驗證退路。

### stale `tests_running` 的反射解法（2026-09-01 追加94 續 14 查出，已驗證）

根因：`MCPForUnity.Editor.Services.TestRunStatus._isRunning`（internal static）卡 `true`。
`EditorStateCache` 讀它 → `tests.is_running` → Python 端 gate 擋掉所有 `run_tests`。用 `execute_code` 反射清：

```csharp
var bf = System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
System.Type.GetType("MCPForUnity.Editor.Services.TestRunStatus, MCPForUnity.Editor").GetMethod("MarkFinished", bf).Invoke(null, null);
var nt = System.Type.GetType("MCPForUnity.Editor.Services.TestRunnerNoThrottle, MCPForUnity.Editor");
nt.GetMethod("SetTestRunActive", bf).Invoke(null, new object[]{false});
try { nt.GetMethod("RestoreThrottling", bf).Invoke(null, null); } catch {}
UnityEditor.SessionState.SetBool("TestRunnerNoThrottle_TestRunActive", false);
var tjm = System.Type.GetType("MCPForUnity.Editor.Services.TestJobManager, MCPForUnity.Editor");
tjm.GetField("_currentJobId", bf).SetValue(null, null);
System.Type.GetType("MCPForUnity.Editor.Services.EditorStateCache, MCPForUnity.Editor").GetMethod("ForceUpdate", bf).Invoke(null, new object[]{"clear"});
```

清完 **EditMode via MCP 恢復可靠**（263→270 全綠驗證過）。**PlayMode via MCP 仍是死路** ——
清完後認真試（`init_timeout` 120s、editor 回報 focused），PlayMode 進得去、`Time.time` 有前進（沒凍幀），
但 NUnit `[UnityTest]` 卡在第 1 個測試 `completed:0` 超過 5 分鐘（測試全是 `yield return null`），
`manage_editor(stop)` 解不開、又要再反射清一次。

**結論**：MCP 驅動下，PlayMode 驗證不可靠。EditMode 現在可以硬清後跑；PlayMode 一律把修改做完、
請使用者本人從 Test Runner 視窗跑。

### 不要用純文字工具改 Unity YAML（`.unity` / `.asset` / `.prefab` / `.meta`）

2026-09-04：用 Edit 工具改 `GreyboxTest.unity` 一個序列化欄位 → 整份 6 萬行檔案 CRLF→LF 重寫，
`git diff` 顯示內嵌 `Mesh:` / Cubism `ArtMesh` 區塊被刪，只能 `git checkout` 還原。
改序列化值一律走 `execute_code` 的 `new SerializedObject(comp)` → `FindProperty` → 設值 →
`ApplyModifiedPropertiesWithoutUndo` → `SetDirty` → `SaveScene`/`SaveAssets`；SO 也可用
`manage_scriptable_object`。`.cs` shader 檔可以正常 Edit。

另註：`GreyboxTest.unity` 每次存檔本來就會有巨大 diff（場景內 Live2D 立牌 `ToModel()` 每次重烘
~205 個 Cubism `ArtMesh` 子網格、fileID 全換），是正常 churn 不是壞檔，看你改的欄位有進去就好。

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

### AI 避障：NavMesh 路徑跟隨（追加71）

- `NavPathFollower`（`Assets/_Project/Game/AI/`，agent-less）掛在**武士 / 屁孩王 / Enemy**。`BossStateMachine.MoveTowardTarget` + `UpdateReturnHome`、`EnemyAI` 地面 chaseDirection 先問它要方向，fail-open 退回直線（沒 baked mesh 的 AI 不會更糟）。**沒引入 NavMeshAgent**（movement 仍是 `CharacterController.Move`）。
- **改/加地圖幾何後要重跑選單 `Tools/Live2DAction/Bake Navigation Mesh`**（`NavMeshBakeSetup.cs`，不自動呼叫）。NavMesh 存在 `Assets/_Project/Scenes/GreyboxTest/NavMesh-Navigation.asset`。角色 + 車輛用 `NavMeshModifier(ignoreFromBuild)` 排除在 bake 外。
- **Player/Cat 沒接**（輸入驅動，沒路線）；它們卡是碰撞體品質問題，另做 collider pass。
- 學校區 navmesh 目前 `PathPartial`（plaza y=−6、地形破碎），待整理後重烤。

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
  `ASSET_LICENSES.md` 的佔位/禁售素材：076、077、Mecha 機甲、Player5「lacrimosa」、狼的末路武器
  （2026-08-31 追加77 起已不掛在 Player 身上、被血刀取代，但檔案仍在磁碟）、原神劍展示組，
  外加**血刀 `BloodKatana.glb`（追加77，來源待使用者確認，確認前比照禁售）**。全部只能在開發機
  做內部原型驗證。這條 AI 不能自己放行。
- **Player 持握武器（追加77 / 追加81 續 3）**：血刀 `BloodKatana.glb` 掛 `Rhand_Weapon2`，掛載物件
  **仍命名 "WolfsGravestone"**（`UltimateAbility.FindWeapon()` 靠這個字面名字找要拋的武器＝R 大招丟武士刀）。
  結構 `WolfsGravestone`(wrapper) / `BladeMesh`(GLB)；wrapper localRotation + BladeMesh offset 是手調權威值。
  **追加81 續 3：`PlayerKatanaSetup` 在 wrapper 加 `MeshBoundsFixer`** — glb mesh bounds 退化 (0,0,0)、
  執行期會被視錐剔除、武士刀「消失」，這顆 `[ExecuteAlways]` 元件每次載入重算 bounds。
- **Player 背劍裝飾（追加81 續 3/4）**：使用者要狼末大劍**放回背上當裝飾**、擺「劍柄左上刀劍右下」。
  `PlayerBackGreatswordSetup.cs`（選單 `Attach Wolf's Gravestone As Back Decoration`）把 `Genshin_WGS.fbx`
  掛 **Player root**（scale 1；不掛脊椎骨——骨骼 80x lossy scale 會把 localPosition 乘爆甩飛，踩過）、
  命名 **`BackGreatswordDecor`（不叫 WolfsGravestone → R 大招不丟它）**、接 **兩顆 TPC**（Main Camera +
  CatCamera）的 `firstPersonHiddenAccessory`、自帶 `MeshBoundsFixer`。transform 從 git d735761/8ecb5fb
  原封還原（`localPos (1,−0.80115217,−0.2)`、`Euler(0,0,43)`、`scale 1`）——**FBX 握把在模型 +Y 端**
  （`pCylinder5` local Y≈2.37，原點端是刀尖），Euler 43° → 握把左肩、刀尖右下。**《原神》仿製 DoNotShip**。
  詳見 memory `player-weapon-mount`。
- **Player R 待命光環 = 火焰，閃電已移除（追加77→81）**：SwordOrbit（`不要有人形.mp4`）已刪，換成
  `PlayerUltimateAura`（`不要出現人物_...mp4`）。**它不是施放特效，是「必殺可用」（`energy.IsFull`）的常駐
  待命光環**，由 `UltimateReadyAura` 的 `flameAura` 欄位 SetActive-toggle。**追加81：**(1) 2026-08-16 的
  奇犽風繞圈電光（就是「白色一圈」那個）整條刪——`bolt` 欄位/`LightningAuraUtility`/`UltimateReadyAuraSetup.cs`/
  `LightningBolt.mat`/場景 `Player/UltimateReadyAura` 子物件都沒了；`UltimateActivationBurst`（施放瞬間金環）保留。
  (2) 火焰改成**只有來源影片**——追加79/80 疊的 front 層/自製 Embers+GroundRing/`_Brightness` 1.3/中央裁切全拆，
  prefab 就一個 billboard flipbook。atlas 重烤（完整 frame、54 幀、亮度鍵門檻 52）。尺寸「忠於來源比例，
  略大於角色」：`SizeHeight 2.7` / `SizeWidth ×1280/720` / offset `(0,0.31,0)`。
  `PlayerUltimateAuraVfxSetup.cs` 選單 `Add Player Ultimate Ready Aura VFX (flame, on full energy)`，可重跑。
  memory `sword-orbit-ultimate-vfx` 有完整 recipe。
- **Player R 施放特效 = SwordOrbit「劍體環繞」（追加69→77 刪→追加81 復原）**：使用者「player 施展 r 技能
  原來的特效不見了 就是一把劍的旋轉砍擊(我不是說大劍)」。跟上面的待命火焰**是兩回事**——火焰=充能好了、
  這個=真的按 R 施放時 spawn 一次。源 `不要有人形.mp4`（**烘進 RGB 的灰色透明棋盤格背景**——亮度鍵門檻要
  拉到 60 才鍵得掉）。`SwordOrbitVfxSetup.cs` 選單 `Add Sword Orbit Skill VFX (R ultimate cast)` 建
  `SwordOrbitSkillVFX.prefab` 接 `UltimateAbility.castVfxPrefab`（追加79 移除、追加81 加回）+ `castVfxLocalOffset`
  `(0,0.4,0)`。可重跑。
- **Player 普攻特效已移除（追加78）**：player 近戰要從拳頭改揮刀、後續做隻狼式對打。`LightAttack1/2/3`
  的 `hitEffectOverride` 清空，`Attack01/02/03.prefab` 及相關素材已刪（敵人的 `Attack3SlashEffect` 保留）。
  動畫替換（可用專案內 `CombatAnimations/TC_Sword_Free_Pack/`）＋ 對打機制未做。memory `player-melee-rework`。
- **右鍵 = 武士刀格擋（追加86 + 追加88 微調）**：右鍵不再是瞄準射擊。`IInputCommand.GuardPressed`（新 default
  member `=> false`）；`PlayerInputProvider` 的 `AimPressed`/`FirePressed` 恆 false、`AttackPressed` gate 在
  `!GuardPressed`。`Combat/PlayerGuard`（Player root，`IIncomingDamageModifier`）：正面錐 150° 內傷害
  HP ×0.15、架勢全額（`poiseMultiplier` 要跟 `StancePoise.stanceGainMultiplier` 0.2 一致）、`CharacterMovement.
  ExternalSpeedMultiplier`（新欄位）×0.35。`Health.ApplyDamage` 新增套用同物件 `IIncomingDamageModifier` 的
  一段（無 modifier 時零改變）。選單 `Add Player Katana Guard`（`PlayerGuardSetup.cs`）。
  **追加88**：格擋 pose 轉**兩根骨** —— `upperArmBone`(`Bip001-R-UpperArm`, euler (-30,-40,-18)) +
  `swordArmBone`(`Bip001-R-Forearm`, euler 改成 (-55,25,-165))，做出「刀尖左上刀柄右下」負斜率跨身格擋
  （前臂單獨轉抬不起手）。左鍵音效移除（`PlayerMeleeSfx`/`PlayerMeleeSfxSetup` 刪），改成 `PlayerGuardClashSfx`
  訂閱 `PlayerGuard.Blocked`、只在擋下 boss `BossHitbox.ActiveWindowPart==Weapon`（新 getter）時放 clank，
  選單 `Add Player Guard Clash SFX`。武士 `StancePoise` regen 調慢（`regenPerSecond` 20→8、delay 1.5→3）。
  memory `player-melee-rework`。
- **射擊系統退役但資產保留（追加86）**：Player 移除 `RangedWeapon`/`RangedAttackDistance`/tracer
  `LineRenderer`/root `AudioSource` + 場景移除 `RangedWeaponHud` + 右手 `AK47` 實例。`RangedWeapon.cs` /
  `AK47.fbx` / `RangedWeaponSetup.cs` / `GunshotSfxSetup.cs` / `GunshotSfx.wav` 全留磁碟。重跑
  `Add Ranged Weapon To Player` 會加回 component 但 `AimPressed` 恆 false → 不會走火。
- **Maya `NewAnimator` 是共用的**（`MayaAnime/Animator/NewAnimator.controller`）：Player + `中立者1` + `守望者`
  三個都在用，且 `中立者1` 也掛 `ExecutionAbility`。改上面的 state（例如 repoint `Execute`）會連他們一起改。
  要給 Player 專屬動作 → 加**新** trigger/state（追加87 F 處決就是這樣：`ExecutionAbility.executeTriggerName`
  新欄位 + Maya controller 新 `ExecuteThrust` state）。`Enemy` 用另一個同名 `NewAnimator`（`ArisaAnime/`）。
  memory `maya-newanimator-shared-by-player-and-others`。
- **連續刺刀動作（追加87 加入 → 追加89 退回）**：`CombatAnimations/Meshy/ContinuousThrust.fbx`（Meshy）
  加成武士普攻 + Player F 處決。**追加89 停用** —— 離線量測證實這 clip 是**旋身撲擊**：髖部單調前+側漂
  ~1.5m（`lockRootPositionXZ` 只清 root 淨位移、per-frame 前進烤在髖曲線 → 可見身體走出去）、chest yaw
  ±130°。已從 `武士 normalAttackPool` 移除、F 處決退回 `FlyingKick`。**FBX/asset/controller state 全留磁碟**。
  **追加89 留下的通用改動**：`BossAttackDefinition.faceTargetSnapOnStart`（bool，攻擊進入時 snap yaw 對準
  目標）；`ExecutionAbility.BeginExecution` 先 snap 對準被處決目標（FlyingKick 也受用）。
  memory `continuous-thrust-shared-anim`。
- **R 大招特效有音效了（追加78→79）**：兩支來源 mp4 內含 AAC 音軌（使用者自有），ffmpeg 抽軌 →
  `Assets/_Project/Audio/Skills/`。cat = `CatDarkQi_Cast.wav`（2.9s，施放時播）。player = 追加79 改成
  `PlayerUltimateAura_Ready.wav`（2.3s 前半段，能量剛滿時播一次「充能完成」stinger，`loop=false`）。
  `SlashVfxController` 的自毀延時也算 `AudioSource.clip.length`（cat 還在用；player 火焰追加79 拿掉了
  `SlashVfxController`）。**DarkQi 那支抽音要用 input seek**（`-ss` 放 `-i` 前），output seek 會抽出靜音。
- **AI 生成 VFX 影片常見「烘進 RGB 的灰色透明棋盤格」背景**（`不要有人形.mp4`、`幫我生成一個黑暗劍氣風格
  的版本.mp4` 都是，不是純黑）。純亮度 alpha 鍵鍵不掉（淺方塊 luma 到 ~74）→ 一層灰濁半透明霧「掉漆」感。
  兩種解法：(1) 亮度鍵門檻拉到 ~60（會犧牲暗部細節）；(2) **彩度（chroma）去背**——棋盤格是純灰
  chroma=0，彩色特效 chroma 高，`alpha = max((chroma−10)/70, (maxc−92)/120)`（第二項留白色核心），
  暗色調特效用這個乾淨很多（見 `CatDarkQiVfxSetup.cs` 追加81）。暗色素材記得把 `_Brightness` 拉到 ~2.0。
- **billboard VFX 截圖驗證這台機器很難搞**：`manage_camera` 的 scene_view / game_view 都抓不到 flipbook 粒子；
  借 game Main Camera `cam.Render()` 到 RenderTexture 時，billboard 朝向 `Camera.main`（不是臨時視角），且這輪
  一直遇到 tonemap 爆掉整片糊白（HDR / 非 HDR RT 都試過）。實務：用 ffmpeg 把 atlas 合成在深/淺底逐格
  目視檢查內容，其餘（尺寸/亮度/接縫）交給使用者實機 Play-test + 一行常數重跑選單。

## 6. 其他

- **武士 Boss 開場演出**（追加91 demo → **追加92 已接入 `GreyboxTest.unity`**）：Timeline+Cinemachine
  舉刀起手式過場，走進 `BossRoomTrigger` → 過場 → `BossStateMachine.ForceEngage()` 直接開打。轉正工具
  選單 `[Boss Intro] Wire Into GreyboxTest`（`BossIntroGreyboxSetup.cs`，可重跑、只在 GreyboxTest 為
  active scene 時執行）。腳本在 `_Project/Game/Cutscene/`。原 demo 場景 `SamuraiBossArena.unity`（選單
  `[Exploration] Build Samurai Boss Arena`）保留當參考、**仍不在 Build Settings**。決策+術語+轉正記錄在
  `Docs/BOSS_INTRO_EXPLORATION.md`（§9 = 追加92）。`Live2DAction.Runtime.asmdef` 加了 `Unity.Cinemachine`
  + `Unity.Timeline` 參照。踩坑：Timeline 播帶 root motion 的 Humanoid clip 要做只刪水平 root 曲線的
  in-place 副本（**`RootT.y` 留著**）；Meshy 模型退化 bounds 會被過場相機視錐剔除，要
  `smr.updateWhenOffscreen=true`；過場期間 `CameraPossessionSwitcher`/`ViewFocusDirector`/
  `SpectatorCameraToggle` 會在 LateUpdate 把 `Main Camera` 硬開回來，**必須一起停用**。
  memory `boss-intro-cutscene-exploration`。
- **隻狼式彈反 + 武士戰鬥系統 9 項工程改造（追加94 續 1～34，2026-09-01～09-02）**：外部 AI 規格
  `Docs/WUSHI_COMBAT_ENGINEERING_SPEC.md`（開頭有逐項「實作進度」表）。CHANGELOG「追加94 續 N」是流水帳。
  當前 EditMode **288/288**。核心：`PlayerGuard`（右鍵格擋 + `EffectiveParryWindow` 0.20s + 反連按 `_parryScale`）、
  `PlayerGuardVolume`（玩家錨定守備膠囊，proxy——不是貼刀身）、`BladeClash`/`DeflectReaction`（每 hit-window
  決定彈反是否中斷連段）、`BossLifeNodeController`（武士 2 個 Deathblow 節點 → Phase 2 → 永久死亡）、
  `SekiroDeflectDebug`（F9 gizmo + session 數據儀表）、`BossAttackTimingReport`（選單 `[9] 武士 Attack
  Timing Report`——讀 Animator state speed 印每招真實 contact 秒 + 有效 ms）。**規格進度**：M1 完成、
  M2（Boss 旋轉 Sweep 完成 / **玩家武器 Sweep 續 23 退回**，`PlayerWeaponHitbox` 留磁碟需陪同 Play debug）、
  M3（程式化攻擊位移 + **武士 root scale 4→1「做法 A」完成**（`WushiRootScaleSetup.cs`，幾何逐項驗證保留）/
  **精確 Guard collider（5C）使用者跳過**）、M4 完成、M5 groundwork 完成。**所有「程式完成」項目的 Play
  手感驗收未跑**（本機 MCP PlayMode runner 不可靠——見上方「stale `tests_running`」）。
  memory `player-melee-rework`。
- **`GreyboxSceneBuilder.Build()` 會先清空整個場景再重建**它自己寫的內容 — 曾誤刪當天尚未 commit
  的角色/立牌。**只做局部修改就用 `EditorSceneManager.OpenScene` 直接改**（照 `Fix*.cs` 的模式），
  絕不要為了改個材質就呼叫 `Build()`。真要整場重建，先問使用者，並照 `CHANGELOG.md` 記錄的完整
  工具執行順序重跑所有後續視覺/立牌腳本。
- **場景是二進位 YAML，不友善版控**。改場景前先 `git status` — 有未 commit 內容代表工作目錄是唯一副本。
- **地圖串流（追加94 續 73–78）**：`學校` + `SchoolWall_*` + `yuanpei_*` 已從 `GreyboxTest` 移到
  **`Assets/_Project/Scenes/Map_School.unity`**（兩場景都在 Build Settings）。**進出用大門互動**
  （`SceneGate.cs` + `SceneTransitionRunner.cs`）：`SchoolGate_Enter`（GreyboxTest 車道南端）按 E
  → `SceneTransitionRunner`（常駐 GO，**不是門上** —— 卸 Map_School 會連門帶 coroutine 一起銷毀）跑
  `ScreenFader` 載入畫面 → `LoadSceneAsync(Additive)` → 傳送玩家進校園；`SchoolGate_Exit`（Map_School 內）
  按 E → 傳回車道 + 卸載。二次元（`Map_Nijigen.unity`，本地西側）同款。
  門的可見面是紅漩渦影片（`PortalVortexVideo.mp4`）。**續 91：VideoPlayer 一定要是場景序列化元件**
  （編輯期 `AddComponent` + 設 `clip`/`targetTexture`/`playOnAwake` 後存場景），**絕不要在 runtime
  `Awake()` 裡 `AddComponent<VideoPlayer>()`** —— `playOnAwake` 在 `AddComponent` 當下就 latch，早於設
  `clip`，scene-0 載入的入口門會永遠不播（試錯 ~9 次的根因）。每座門一張 `RT_<gate>` +
  `Mat_<gate>`（shader `Live2DAction/PortalVideoURP`：`smoothstep` key 掉近黑 + `Blend One One`）。
  影片要**全範圍轉檔**（無壓黑）才不會有灰白矩形基座。
  **續 91-93：scene-0 的入口門 VideoPlayer 連序列化 + playOnAwake 都不會自己播** —— 解法是 `OnEnable`
  coroutine：等 2 幀 → `Prepare()` → 等 `isPrepared` → `Play()` → 每 0.5s 補。**不要用 `APIOnly`**
  （續 92 試過 → D3D11 掉紅色通道 → 整片青色矩形）。續 93 = `RenderTexture` mode + coroutine。
  有 `[PortalVideoSurface] <門名>` Console log。
  **續 94：proximity-gated** —— 傳送門載入時 `Prepare()` 好但不播、renderer 關；玩家進 32m 才淡入現身、
  出 40m（或穿門）消失。`proximityActivated` 可 per-gate 關掉回常駐。`MapStreamer.cs` 留磁碟未使用。
  **要改學校/二次元/現世物件先在 Editor 把對應 `Map_*.unity` 開起來**。詳見 `MAP_STREAMING.md`。
  續 95：第三座城市「現世」在本地**東側**（`Map_Xianshi.unity`，空地），橋接門用 Meshy FBX `VoidmoonGate`
  （`幽冥星環傳送門`，改名避開 gitignore、`useFileScale=false`、擺放繞 X −90° 立起）框住漩渦影片。
  二次元的門這次一起轉了 Y=90（原本 portal 面朝 +Z ＝ 對西路側面看不見）。
  Editor 失焦時 Play 會凍結 → 轉場 coroutine MCP 測不了，要對焦 Play。
  **續 185 / 185b：`SchoolGate_Enter`（且只有它）加了「動態互動提示 UI」** —— 靠近顯示 `PortalDialogueFrameVideo.mp4`
  對話框 + 置中「按下 F 進入 Boss 地圖」。`PortalInteractionUIController.cs`（狀態機）+ `Live2DAction/UI/PortalDialogueFrame`
  去黑底 UI shader（標準 alpha blend）+ `PortalInteractionUISetup` 選單建置。**續 185b**：拿掉深色底板（`DarkBackdrop`）；
  `RawImage.uvRect` 裁到只剩對話框（原片下半部那條寬扁框），`VideoContainer` = 框本身、置中，文字置中 → 文字在框裡。
  `SceneGate` 加 `portalUI` + 序列化 `Key interactKey`（可空/預設 `Key.F`），其他門不受影響（續 85 不變）；傳送呼叫一字未改。
  **續 185b：所有傳送門互動鍵 E→F**（`SceneGate.interactKey` 預設 `Key.F`、`Portal.cs` 改 `fKey`；`Map_*` 內 `*_Exit`
  門靠初始值自動吃 F、不用改場景）。**F 也是上車/駕駛 + 處決鍵**。專案沒有 `.inputactions`（全 `Keyboard.current` 輪詢）、
  沒匯入 TMP（所有中文 UI = legacy `Text` + `LegacyRuntime.ttf` OS fallback）。
  **續 185c 三修**：(1) 文字等 `frameRiseSeconds`(1.4s) 框立起動畫跑完才淡入;(2) **駕駛時用門會掉虛空** —— 因為上車時
  `Player` 被 reparent 到車底下,解析成車,`Begin()` 傳的是 950kg Rigidbody buggy → 穿地。修:`SceneGate` 只拿「Player」
  transform、`OccupantSeated`→`Blocked`(坐車 F=下車,下車再按 F 傳)、`ForceDismountAll` 保底、`SceneTransitionRunner.Teleport`
  加 Rigidbody 分支;(3) 三座 `*_Enter` 門 `Blocker` 放大成 `16×22×1.5` 整面牆擋住門後。
  **續 185d 三修**：(1) **UI 生命週期改「距離驅動」** —— 不再靠 `OnTriggerExit`(關 CC 的傳送不觸發它 → UI 曾卡在畫面上、離門很遠還顯示)。
  `SceneGate.Update` 用實際水平距離 + `RangeHysteresis`(1.5m):`uiShowRange`(4.5) 顯示 / `interactRange`(3.2) 才能按 F。
  玩家用 `OnTriggerEnter` 快取 + `ScanForPlayer` 每秒補償(比照 `PortalVideoSurface`);(2) **重複顯示** = 同一根因的 `_near` flicker,
  遲滯後消失;(3) **入口出口都要 UI** —— `PortalInteractionUIController` 改**單例** `Instance`(Canvas 在常駐 `GreyboxTest`),
  `SceneGate` 拿掉 `portalUI` 序列化欄位改 `Instance`,加 per-gate `showInteractionUI`(預設 true) + `promptMessage`。
  `PortalInteractionUISetup` 現在配置**全部 6 座門**(開 `Map_School`/`Map_Nijigen`/`Map_Xianshi` additive 設 exit 門)。
  **這次改到 4 個場景**(GreyboxTest + 3 個 Map_*)。
  **續 185e:車輛 vs UI 互動 → 互動優先** —— `SceneGate.Blocked` 拿掉坐車判定(傳送前 `ForceDismountAll`);
  `SceneGate.PlayerHasPortalInteraction(Transform)` 靜態方法讓 `VehicleEntrySystem` 坐著按 F 時先問、為真就讓位不下車。
  **續 185f 兩修**:(a) **對話框仍立起兩次** —— 原片只有一次立起,是遊戲端 `ShowRoutine` 跑第二次。三層防護:
  `SceneGate.hideGraceSeconds`(0.6s,`wantUI` 轉 false 不立刻 Hide)+ `PortalInteractionUIController.ResumeRoutine`
  (淡出中被 `Show()` → 淡回、**不 rewind 影片**)+ 距離遲滯。MCP 驗:靜置 12s 與狂彈 6 次都 0 次狀態變化。
  (b) **互動範圍太廣、開車頂到門就被搶控制權** —— 收緊。**續 185g 又再砍半**:`interactRange` **1.0**、`uiShowRange` **1.8**、
  `RangeHysteresis` const 0.7、**Blocker 牆改薄 0.6 厚**(1.5 厚會把徒步玩家擋在門外 1.23m,構不到 1.0 的 interactRange;
  薄成 0.6 後能貼到 0.78m,牆 16 寬 × 22 高擋牆功能不變 —— raycast 驗各高度都擋)。UI 只在 1.8m 內出現、F 只在貼門 ~0.8m 吃。
  車體讓駕駛座 ~2.8m 遠 → 完全不會被門搶 F。185e 讓位機制保留但實務上車輛觸發不到。
  **續 186:Boss 地圖正式影片載入畫面** —— `SchoolGate_Enter` 進 `Map_School` 全螢幕播 `BossLoadingVideo_{Seal,Blood}.mp4`(輪流)
  + TMP「正在進入元培禁域……」/「載入中 XX%」。**擴充 `ScreenFader`+`SceneTransitionRunner`,不建新 manager**:`BossLoadingScreen`
  單例(自建 canvas sortingOrder 32100)疊在 `ScreenFader` 上,`ScreenFader` 照樣 covered = 黑底 + 既有 `PlayerInputProvider`
  輸入鎖(不 disable 腳本)。`SceneTransitionRunner.Begin(..., useLoadingScreen)` 用 `allowSceneActivation=false` 把
  `op.progress/0.9` 顯示成真實 0~100%,`minLoadingScreenSeconds` 1.2s 防閃一幀,`FadeOverlay` 黑到 `prepareCompleted` 才淡出防閃白。
  `SceneGate.useLoadingScreen` 只 `SchoolGate_Enter` 開,其他門/回程維持「載入中…」黑幕。
  **專案第一個 TMP**:`NotoSansTC-Regular.otf`(OFL,登記 ASSET_LICENSES)→ dynamic `NotoSansTC SDF.asset`;`Live2DAction.Runtime.asmdef`
  加了 `Unity.TextMeshPro`;`Assets/TextMesh Pro/` essentials 進版控。`BossLoadingScreenSetup` **首次要跑兩次**(第一次匯入 TMP
  essentials,第二次建字型)。未對焦時 VideoPlayer 不 prepare(6s timeout 淡出)—— 影片實際播放要對焦 Play。
  **續 185h：橫向擴大** —— 續 185g 砍成「只有貼正中心才能互動」。判定從圓形改成**門 local 空間的長方形**
  (`transform.InverseTransformPoint(player)` → `.z`=深度、`.x`=橫向)。`uiShowRange`/`interactRange` 只管**深度**(1.8/1.0),
  新增 `lateralHalfWidth`(**6**)管**橫向** → 站門前 ±6m 內都算「在門前面」(蓋滿傳送門 quad 寬 + 車道寬)。
  rot-Y90 的門靠 `InverseTransformPoint` 自動吃到旋轉。`CanInteractNow`/`PlayerHasPortalInteraction`/scan/drop 全換成
  `InFront(local, depthRange)`。**續 185i**:`uiShowRange`=`interactRange`=**1.5**(深度),UI 一出現 F 就能按、無空檔。
  目前全 6 門:深度 UI/interact = 1.5、`lateralHalfWidth` = 6、`hideGraceSeconds` = 0.6、Blocker 牆 16×22×**0.6**。
  **未對焦時 VideoPlayer 媒體管線不跑、RT 全黑;合成鍵盤 edge 不觸發;MCP 無法模擬駕駛** —— 影片 + 去黑底 + 按鍵傳送 +
  車輛進門要對焦 Play 才驗得到。
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
- **怪物級別**（2026-08-29 使用者定義）：普通怪＝`Enemy`（`EnemyAI`，不受限）、菁英怪＝`屁孩王`
  （`BossStateMachine`，`confineToArena=true`，被關本地、GateWatch）、boss＝`武士`（`BossStateMachine`，
  `confineToArena=false`，**不受城牆限制——使用者原始設計**；追加72 曾誤改 true、追加73 撤回。只吃
  `leashRange` 32 距離 leash）。共用腳本，差異全靠逐-instance 欄位；三者 AI 互不干擾但傷害
  （HP/架勢/擊飛）保留。舊文件的「精怪」＝菁英怪。
  **死亡→復活**（追加70）：武士＋屁孩王都 `permanentDeath=false` / `reviveDelaySeconds=5`，死亡 5 秒後進
  `BossState.GettingUp`（把死亡 clip 倒放 `StandUpSeconds`≈1.8s 當起身）再回 Alert。屍體不消失
  （`Health.deferDeactivationToDeathAnimation=true` + 無 `DeathAnimationLink`）。兩隻死亡 clip 設
  `lockRootHeightY=true`+`heightFromFeet=true` 讓屍體貼地不飄。
- **可玩角色與切換**：`Player`（Maya humanoid）＋ 獨立 `Cat`（scale 0.45、Meshy 綁定姿勢無動畫）。
  **C** = `CameraPossessionSwitcher` 在 Player↔Cat 附身切換（`Current` 是「操控誰」的真相來源）；
  **T** = `ViewFocusDirector` 守望者視角；**F** = `VehicleEntrySystem` 進出車（追加55→57：**雙人座**、
  看 `possession.Current` 決定用誰。F = 駕駛座空就進、被佔就上後方平台當乘客、都滿無作用；在車上 = 下車。
  **開車中 C 仍可切角色** —— 駕駛留車上熄火、控乘客時看自己的相機。想換人開得兩隻都下車再 F。
  兩隻角色都不隱藏，貓的座位錨點有 -50° 仰角讓 chase cam 看得到臉）。跨系統接線：
  `WatcherCatWiring` / `VehicleCatWiring` / `CatBarsWiring`（都從 `CatCharacterSetup` 結尾呼叫；各自也有選單）。
  **貓 HUD（追加74）**：貓有 `StancePoise`（削韌，maxStance 50）＋ `CatCornerHud`（生命/能量/架式，clone 自
  `PlayerCornerHud`）。`PossessionHud` 依 `CameraPossessionSwitcher.Current` 右上角換整組（操控貓 → CatCornerHud、
  關 PlayerCornerHud）。**不 gate 戰鬥狀態**（跟 `WushiBossHudVisibility` 相反）。
  **`CameraPossessionSwitcher.playerControl[]` / `catControl[]` 必須列全該角色「所有讀輸入的元件」**
  （追加70 修「cat 視角下攻擊連帶觸發 player」——原本 playerControl 只有 `CharacterMovement`）：
  player 現在（追加86）＝`CharacterMovement`+`PlayerCombat`+`TargetLockController`+`UltimateAbility`+
  `PlayerGuard`+`ExecutionAbility`（`RangedWeapon` 退役、從陣列拿掉；`CatCharacterSetup.CollectPlayerControl`
  收）。加新輸入元件要同步補這兩個陣列 ＋ `ViewFocusDirector.suspendWhileWatching`。

---

## 露營區（Map_Camp）場景素材

- 圍牆內部 x[-79,-21] z[-129,-71]，大門在北牆缺口 (-50,-71)，`猜猜看` NPC 站 (-50,-90) 面朝大門。
- 2026-09-12 把 5 個已建好材質的 Meshy 素材（CampTent/CampCanopy/CampFenceTree/CampYellowTree/CampLawnMower）
  ＋新匯入的 `摩托超載.glb`（→`CampMotorcycle`）擺進場景，細節/座標見 CHANGELOG 同日條目。
- **用 `manage_gameobject(action=create, prefab_path=...)` 具現化 Meshy FBX 時的坑**：這批 FBX 原始結構是
  根節點（scale=1）→ 跟檔名同名的子節點（`localScale=100`，把公尺級網格頂點吃掉的必要換算，跟「80x bone
  scale」同一類 Meshy 匯出習慣）→ Mesh。但 `manage_gameobject` 的具現化會把整個階層拍扁成單一 GameObject
  （只留 Transform+MeshFilter+MeshRenderer），子節點的 100 倍不見了。若照著具現化「之前」用完整階層
  `renderer.bounds` 量出的巨大數字（~190 單位）去反推 scale，會小了 100 倍（幾公分，肉眼幾乎看不見）——
  正確做法是具現化「之後」直接讀 `MeshFilter.sharedMesh.bounds`（已經是拍扁後的合理公尺級數字）。
  **同時**具現化後 `MeshRenderer.sharedMaterial` 會指向 FBX 內嵌的預設材質（`Material.001`，URP/Lit 但貼圖
  全空、灰色 0.8），不是資料夾裡已經做好貼圖的 `CampXxx.mat`，要手動 `execute_code` 重新指回去。
  驗證擺放結果建議用 `manage_camera` 的 positioned screenshot（`view_position`/`view_target`），
  這次 `manage_scene(scene_view_frame)` 對著拍扁後的具現化物件沒能正確框選/縮放。
- **另一個坑：這批 Meshy FBX 是 Z-up，不是 Unity 的 Y-up**——跟 2026-09-11 `CampFloor`（露營區木頭地板）
  踩到的是同一個問題（"量出網格用本地 Z 軸當『上』，需要 euler(270,0,0) 立平"），但一開始擺上面那 5 個
  素材（帳篷/遮雨棚/柵欄樹/黃樹/除草機）時沒套用同一個修正，於是全部 `eulerAngles=(0,0,0)` 卻視覺上側躺
  在地上。**單一角度截圖很容易誤判**（躺平的物體湊巧從某個角度看還是像那麼回事，這次因此走了一次冤枉路：
  試了 `(270,0,0)` 後只看一個角度覺得「更扁更矮」就以為方向錯了改回 `(0,0,0)`，後來拿除草機驗證同一個
  旋轉值——四個角度看起來幾乎一樣、輪子攤平、手把橫向伸出，一眼就能看出「這是躺平的特徵」——才確認
  `(270,0,0)` 才是對的，問題出在旋轉後沒有重新用 `renderer.bounds.min.y` 校正貼地（不是旋轉方向錯）。
  **診斷用 `manage_camera(batch="surround", view_target=<物件名>)`** 一次拿 6 面 contact sheet
  （front/back/left/right/top/bird_eye）比逐一單角度截圖可靠很多。修正後這批素材同時也全部放大了
  ~2～2.3 倍（圍牆高達 6m，原本的 scale 相對太小氣），細節與最終數值見 CHANGELOG 2026-09-12 續條目。
- **`RenderSettings.skybox` 是全域單一值，不是逐場景的**——露營區同一天稍後又加了全景圖天空盒
  （`Assets/_Project/Environment/Skyboxes/CampPanorama.exr` + `.mat`，`Skybox/Panoramic` shader），
  直接設在 `Map_Camp` 自己的場景資料上，Edit 模式單獨開這個場景看起來完全正確，但 `Map_Camp` 實際
  永遠是用 additive 疊加進常駐 `GreyboxTest`（見 `Docs/MAP_STREAMING.md`），Unity 不會因為多疊了一個
  scene 就套用它存檔時的 RenderSettings——真正用 `SceneGate` 按 F 走一次才發現天空根本沒變。
  修法：新增可重複使用的 `Assets/_Project/Game/World/RegionSkyboxOverride.cs`（`OnEnable` 存舊值換新的
  `skyboxMaterial`＋`DynamicGI.UpdateEnvironment()`，`OnDisable`/`OnDestroy` 換回舊值），掛在該場景一個
  物件上即可，不用動 `SceneGate`/`SceneTransitionRunner`。**驗證這類跨場景視覺設定，必須實際跑一次
  Play 模式的場景轉換，只在 Editor 裡單獨打開該場景看不出這個問題。** 同時把 `CampWall_*` 這 5 個物件的
  `Visual` 子物件（實際牆面 `MeshRenderer`）`SetActive(false)`，只留根物件的 `BoxCollider` 當看不見的
  邊界——露營區外圍其實已經是常駐 `GreyboxTest` 的連續地形在撐著，拆牆後不會露空。
- **使用者偏好「密集可探索」勝過「場地開闊」**——看到全景圖版本的露營區後，使用者說地本身有點太大了
  （沒關係不用改地形），但要求把 4 個露營建築（帳篷/遮雨棚/柵欄樹/黃樹，不含已確認合適的除草機/摩托超載）
  再放大兩倍、集中成一個聚落，讓「猜猜看」用第三人稱走進來、轉鏡頭時能近距離看到好幾個建築，不要「一眼
  望去很空曠」。這次把 4 個建築的 scale 都乘 2、全部集中到「猜猜看」原本站的 (-50,-90) 附近半徑
  ~15m 內（犧牲了原本柵欄樹/黃樹「框住大門入口」的設計，改成優先滿足這次的近距離探索需求）。
  **驗證這類「空間感」問題不能只看空拍/俯視截圖**——用 `SceneTransitionRunner.Begin` 進場景＋
  `CameraPossessionSwitcher.FocusGuessWho()` 切到他自己的第三人稱攝影機，站在原地轉 4 個方向截圖，
  才能真的確認「轉頭有東西看」的效果成立。這是本專案第一次遇到「密度」而非「有沒有東西/擺對沒」的
  美術指示，之後其他區域（學校/二次元/現世……）如果也有類似「太空曠」的回饋，可以套用同一套驗證方法。
- **露營區第四輪：從開放全景場地逆轉回「環抱式中庭要塞」**——使用者貼了別的 AI（只看截圖、沒真實參數）
  寫的需求文件，明講數字是猜的、要用本專案真實參數判斷。這次任務量體很大，先用 `EnterPlanMode` 分析＋
  寫計畫核准後才動手（計畫檔案在 `~/.claude/plans/`，事後沒留在 repo 裡）。量到的真實比例：**玩家身體
  實際高度只有 ≈1.28m**（排除翅膀/劍等裝飾，這是嬌小體型設計不是量錯，之後任何跟「玩家高度」相關的
  比例計算都該用這個數字，不要用文件常見的 1.7~1.9 通用假設）、`GuessWhoCamera` 垂直 FOV=65°。
  地板過曝的具體成因是 `CampFloor.mat` 的 `_BaseColor=(1,1,1,1)` 純白，且全域共用 `PostProcessingVolume`
  完全沒有 Tonemapping/Exposure——修法是**只調材質色調 + 另外掛一個 `isGlobal=false` 的局部 Volume**
  （靠 trigger BoxCollider 涵蓋露營區範圍），不動全域資產（會影響全遊戲每張地圖/Boss 戰）。
  地板/圍牆從 60×60 縮到 38×38、圍牆重新啟用但降到 2.6m（人體比例矮牆），新增 `MainLodge`/`Workshop`/
  入口門架都是純 Cube 灰盒（先驗證空間，不做細節模型）；上一輪放大到 6.8~7.7 倍的帳篷/遮雨棚/柵欄樹/
  黃樹這次縮回 2.7~3.7 倍——兩輪設計前提不同（開放場地要跟真實照片遠景大樹比大小 vs. 緊湊要塞要人體
  比例正確），不是自相矛盾。
  **踩到的坑**：`ThirdPersonCameraController` 的攝影機朝向（`_yaw`/`_pitch` 私有欄位）不會跟著角色
  `transform.rotation` 走，`enableAutoCenter=true` 還會在 ~0.8 秒後把用 reflection 硬塞的 `_yaw` 拉回去，
  導致「轉 4 個方向截圖」透過這顆攝影機元件本身做不出穩定結果。改用 `manage_camera` 的 positioned
  screenshot（自己指定 view_position/view_target，站在真實站立高度 ~1.6m）繞過這個問題；某個建築在
  隨機掃描角度沒出現時，直接瞄準它的已知座標確認「它真的在那裡、只是沒轉到那個角度」，不要誤判成
  擺錯位置或消失。
- **「猜猜看」／`GuessWhoCamera` 必須留在場景根層級**——`CameraPossessionSwitcher.TryRelinkGuessWho()`
  （`Assets/_Project/Game/Camera/CameraPossessionSwitcher.cs:81-97`）只用
  `SceneManager.GetSceneAt(i).GetRootGameObjects()` 掃描每個已載入場景的**根層級**物件比對名字，不是
  `GameObject.Find`（刻意避開 Find 找不到未啟用物件的問題）。上面那次露營區 Hierarchy 整理把「猜猜看」
  重新掛到 `CentralYard` 底下，這支腳本第一行 `if (gw == null) return;` 直接提前結束——**同一個根因
  一次炸兩個 bug**：G 鍵切不過去（`guessWhoCamera` 欄位永遠 null）＋操控 Player 時「猜猜看」仍受影響
  （後面「依 Current 強制停用他自己元件」那段也在同一個 early return 之後，永遠不會執行），全程無
  Console 錯誤，只有使用者實際玩過才發現。**之後對任何物件做 Hierarchy 重新歸類父物件之前，先搜尋
  該物件名字有沒有腳本用 `GetRootGameObjects()` 之類的方式依賴根層級位置**，不能假設「换父物件只是
  視覺分類」。
- **`SkinnedMeshRenderer.bounds` 不可信任來反推貼地高度**——摩托超載（`CampMotorcycle`，glTF 骨架模型）
  過去每次搬動都用 `renderer.bounds.min.y` 反推 Y，這個做法對靜態網格（帳篷/樹/地板）沒問題，但對這種
  **骨架驅動的 SkinnedMeshRenderer 會悄悄回報錯誤的最低點**（跟 memory 記錄的「Meshy 角色 glb 常有
  degenerate SkinnedMeshRenderer bounds」同一個根因），多輪搬動下來誤差累積到浮空 0.87m 才被使用者
  抓到。修法：用 `SkinnedMeshRenderer.BakeMesh(mesh, true)` 烘焙目前姿勢的真實頂點去算最低點（這次
  發現這個模型的骨架根節點本來就對齊在接地點，直接把 Y 設成地板頂面高度即可），並且**一定要截圖
  肉眼確認**，不要只看數字打勾。
- **「所有營地建築物放大 3 倍」踩到的兩個坑**：(1) 第一次只把場地從 38×38 擴大到 64×64 就直接把每個
  建築乘 3，結果建築本身量體變成 15~27m 級，入口兩側柵欄樹/黃樹的緩衝空間不夠，實測進 Play 用「猜猜看」
  攝影機一看，鏡頭直接卡進樹冠裡——**光看俯視截圖檢查「有沒有重疊」不夠可靠**，一定要用第一/第三人稱
  實際站進去（尤其重生點/入口這種近距離位置）才看得出來。後來把場地再擴大到 80×80 才解決。
  (2) 這次重進 Play 測試時，移動 `Player` 的 transform 完全沒讓畫面變化——因為
  `CameraPossessionSwitcher.startPossessed=GuessWho`（2026-09-11 既有設計，開場自動先進「猜猜看」
  視角），一進 Play 就自動把鏡頭焦點切到「猜猜看」身上，跟呼叫 `SceneTransitionRunner.Begin(...,
  player,...)` 完全是兩回事，鏡頭跟拍的其實是站著不動的「猜猜看」。**之後在全新 Play session 裡驗證
  運鏡效果，要嘛直接移動「猜猜看」本人，要嘛先呼叫 `FocusPlayer()` 明確切回 Player 視角，不要預設
  移動 `Player` 就會反映在畫面上。**
- **`SceneGate.cs` 過去寫死只認名字叫「Player」的角色**——`WalkToPlayer()`／`ScanForPlayer()`
  都是比對 `t.name == "Player"`，導致「猜猜看」（根物件名字是「猜猜看」）走到**全遊戲任何一個
  傳送門**都不會被偵測到、互動提示 UI 不會跳出來、按 F 沒反應，而且完全沒有 Console 錯誤。
  2026-09-12 使用者回報「露營區裡的傳送門好像有兩個、其中一個無法對話」，查證後**沒有重複物件**
  （`find_gameobjects` 確認 `Map_Camp` 只有一個 `SceneGate`），真正原因就是這個名字寫死的問題——因為
  「猜猜看」現在是開場預設角色，這個潛藏很久的 bug 才第一次被踩到。修法：`SceneGate.cs` 新增
  `PossessableRootNames = {"Player","Cat","猜猜看"}` 陣列，兩個方法改成比對清單而非單一字串，一次修好
  全遊戲所有傳送門對他（以及 Cat）的互動。**之後如果專案再新增第四個可操控角色，記得也要把名字補進
  這個陣列。**
- **「猜猜看」腳邊常駐的粉紅色漩渦特效是 `UltimateReadyAura`（R 技能能量滿時的常駐光環），不是傳送門
  也不是 bug**——這個特效在能量滿的時候會一直顯示，這幾輪露營區截圖幾乎張張都看得到。使用者要求拿掉
  特效但保留機制時，是分別把 `UltimateReadyAura`／`UltimateActivationBurst`（兩個純視覺元件）在
  「猜猜看」**自己這個實例**上 `enabled=false`，`UltimateEnergy`／`UltimateAbility`（能量與 R 鍵機制）
  完全沒動，也沒有動到 Player／Cat 身上的同名元件。
- **不要用「固定中心點、整體等比例擴大」的方式放大一個有外部固定錨點的區域**——露營區連續三輪
  （38×38→64×64→80×80）都是保持中心 (-50,-100) 不變、只放大半邊長來塞下越變越大的建築，結果北側
  入口牆的座標（= 中心 z − 半邊長）每次都跟著往北飄（-81→-68→-60），完全沒注意到 `CampGate_Enter`
  （固定在常駐 `GreyboxTest`、玩家從外面按 F 進場的大門本體，`transform.position` 從頭到尾都沒動過，
  在 z=-67）——飄到第三輪，營地自己的入口牆已經比這個固定大門還要**北邊 7m**，兩個大門距離只剩幾公尺，
  互動範圍互相干擾，使用者就回報「傳送做了兩道，卡在中間」。修法是把露營區全部 24 個物件整體往南
  平移 12m，讓入口牆落在固定大門南邊 5m 處，`CampGate_Enter.arrivalPosition` 也跟著平移同樣的量。
  **教訓**：之後這類「入口對齊外部固定大門」的區域如果還要再擴大，不能整體等比例縮放，要先認定哪一邊
  跟外部錨點對齊（這裡是北側入口），把那一邊的世界座標釘死不動，只往沒有外部依賴的那一側（這裡是
  南側）擴張。
- **`CampFloor`（視覺網格）跟「露營區」（實際碰撞方塊）是兩個獨立物件，不會自動連動**——露營區地板從
  2026-09-11 建立時就是「隱形 `BoxCollider` 負責碰撞 + `CampFloor` 純視覺網格疊在上面」的分工（跟
  yuanpei 校園建築同一套模式）。連續三輪改場地大小（38×38→64×64→80×80）都只記得改 `CampFloor` 的
  `localScale`，完全忘了「露營區」這個真正負責碰撞的物件也要跟著改——它的 `localScale` 一路停在最初的
  `(60,1,60)` 沒動，導致視覺地板早就長到 80×80，實際碰撞卻永遠只有 60×60，入口那一帶（超出碰撞範圍
  但視覺上是實心地板）一踩就掉進虛空。**教訓**：以後只要改 `CampFloor` 的大小，一定要同時檢查並改
  「露營區」，而且驗證方式要用「真的把角色從空中丟下去看會不會接住」，不能只看截圖覺得地板看起來是
  滿的就當作沒事。
- **`CameraPossessionSwitcher.Update()` 原本只防「猜猜看死亡」，沒有防「猜猜看的場景被卸載」**——
  `guessWhoHealth != null && guessWhoHealth.IsDead` 這個判斷式，一旦 `guessWhoHealth` 本身變成 null
  （不是死亡，是玩家操控他時走出露營區出口傳送門，`Map_Camp` 整個被卸載，他跟他的專屬攝影機一起被
  摧毀）就直接短路跳過，沒有任何東西會自動切回 Player，於是變成沒有任何啟用中的攝影機，畫面顯示 Unity
  內建的「No cameras rendering」。修法：在既有死亡檢查之後，另外加一段「`Current==GuessWho` 但
  `guessWhoCamera` 或 `guessWhoHealth` 已經是 null」就自動 `FocusPlayer()`（不會誤觸發開場載入中的
  正常空窗期，因為 `Current` 只有在 `TryRelinkGuessWho()` 成功連上 `guessWhoCamera` 之後才會被設成
  `GuessWho`）。**教訓**：這類「可能會消失的被附身角色」保護，要同時防「狀態變了」（死亡）跟「參照本身
  變成 null 了」（場景卸載/物件被摧毀）兩種情況，只寫 `ref != null && ref.SomeState` 會悄悄漏掉後者。

## 猜猜看觸發元培 boss 戰 + 摩托超載可駕駛化（2026-09-12）

- **讓猜猜看能真的打 boss、走完整個勝負流程，一次挖出 5 個真 bug**：觸發/鎖定本身是共用
  `Live2DAction.Input.PossessableCharacter`（新檔案，`SceneGate`/`YuanpeiEncounter`/`YuanpeiBoss`/
  `YuanpeiIntroCinematic` 四處各自的「只認 Player 名字」土砲邏輯收斂成一個）解決的。剩下 4 個都是
  **這場戰鬥以前只被 Player 觸發過，沒人踩過的既有邏輯死角**：
  1. 猜猜看在 `GameManager` 上**沒有 `RespawnController`**（Player/Enemy/中立者/屁孩王/Cat 各自有
     一份），死亡後 `Health.ApplyDamage` 把他 `SetActive(false)` 就永遠沒人救回來了。補一份，
     `showGameOverScreen=false`（比照 Cat，不是 Player 的「你菜完了」）。
  2. `YuanpeiIntroCinematic.LockActors()` 會把 `CameraPossessionSwitcher` 整個關掉，這個元件的
     `OnDisable()` 只要 `Current != Player` 就強制切回 Player——這條保險以前從沒被踩到過（一直是
     Player 觸發），第一次 `Current==GuessWho` 時整個開場動畫鏡頭瞬間跳走、之後也回不去。修法：
     乾脆不要在開場動畫期間關掉這個元件（它 Update() 沒有東西真的需要停下來）。
  3. `CameraPossessionSwitcher` 「猜猜看死亡自動切回 Player」的保護，套用在**由 `YuanpeiEncounter`
     主導的戰鬥**裡會在血量歸零瞬間就把鏡頭搶走（Player 可能站在完全無關的地方），看不到死亡/復活/
     傳送整個過程。新增 `SuppressDeathAutoFallback`，開戰時設 true、`Victory()`/`Defeat()`
     **兩條路徑都要**在 `HandControlBackToPlayer()` 後設回 false（漏掉哪一條，贏/輸那條路徑之後
     Cat/猜猜看的日常死亡保護就會永久失效）。
  4. `Map_Camp`（x[-90.5,-9.5] z[-152.5,-69.5]，開場常駐不卸載）跟 `Map_School`
     （x[-31,31] z[-146,-83.5]，走 `SchoolGate` 才 additive 疊上）地皮在 x[-31,-10] 這條 21.5m 寬帶
     完全重疊，只有兩者同時載入才看得到——boss 觸發線剛好在附近。使用者選擇「縮小露營區東側」
     （`CampWall_East` x=-10→x=-36，留 5m 緩衝），不是整塊搬家。
  - 用真實 Play 模式（讓猜猜看真的走過觸發線，不是直接呼叫 `StartEncounter`）測過 Defeat 路徑：
    下馬威齊射正確打中猜猜看、死亡→5 秒後正確復活滿血、G 切得回去。**Victory（贏）路徑的死亡震動/
    碎裂演出還沒測過**（Editor 反覆拿不到真正的 OS 焦點，Play 模式卡在第 2 幀）。
- **跨場景的 `[SerializeField]` 參照存不進場景檔**——`CameraPossessionSwitcher`（常駐
  `GreyboxTest`）跟這台摩托車（`Map_Camp`）不同場景，Inspector 裡指定、`ApplyModifiedProperties`、
  存檔，看起來都正常，**但關掉場景重新載入後全部變回 null**——Unity 根本不會把跨場景物件參照序列化
  進任一個場景檔，Editor session 內「看起來有效」只是還沒重新載入而已。跟
  `CameraPossessionSwitcher.guessWhoCamera` 當初踩的是同一個坑。修法：不要標 `[SerializeField]`，
  改成執行期用 `FindFirstObjectByType`/掃描已載入場景根物件名字解析（`Awake()` 存一次，
  `Update()`/`LateUpdate()` 發現還是 null 就重試）。
- **摩托超載可駕駛化**：新增獨立腳本 `IntegratedRiderVehicleEntry.cs`，沒有擴充既有 buggy 的
  `VehicleEntrySystem`（那支腳本的雙座/部分身體隱藏是專門配合「看得到人坐進車體」設計的，這台車
  網格本身就內建了騎士+雜物，硬套雙座系統風險比另開一支高）。F 鍵解析「目前操控的角色」
  （Player/Cat/猜猜看）上車：整個人隱形＋停用控制腳本、reparent 進車身、切到剛性掛載的「行車
  紀錄器」攝影機（純 `Camera` 子物件，無任何跟隨/彈簧邏輯）；physics 沿用 buggy 的
  `VehicleController`（4 顆 `WheelCollider` 假裝兩顆輪子，故意比視覺車身寬一點增加側傾穩定性，
  反正是純物理不渲染看不出來）。下車時 unparent 用 `SetParent(null)` 只會留在物件目前所在場景
  （這台車在 `Map_Camp`），**沒有自動搬回騎士原本的場景**，得用 `SceneManager.
  MoveGameObjectToScene` 顯式搬回去，不然騎士會被靜靜移進 `Map_Camp`、之後場景一卸載就被摧毀
  （跟猜猜看之前住 `Map_Camp` 裡被摧毀是同一個坑）。**物理調校數值（輪子位置/半徑/懸吊/扭力）全部
  是估的，還沒有真正 Play 模式測過**，需要使用者實際上車開開看再回饋。

## 摩托超載 bug 修復 + 露營區續擺 + 小牛搬運車匯入（2026-09-12 續）

- **腳本用程式加上去的 MonoBehaviour 預設 `enabled=true`**——`IntegratedRiderVehicleEntry` 只在按 F 上下車時切換 `VehicleController.enabled`，沒有在 `Awake()` 明確設初始值為 `false`，導致場景一載入車子就在讀 WASD 自己亂動（跟角色走路同一組鍵）。**教訓**：任何「應該保持關閉直到被啟動」的元件（尤其是直接讀 `Keyboard.current` 這種全域輸入的），初始 OFF 狀態要在 `Awake()` 明確設，不能只在切換事件（上下車）處理，會漏掉「場景載入到第一次真正切換之間」這段空窗期。
- **判斷剛匯入/剛放大物件的正確旋轉，鏡頭要拉遠、對準 `Renderer.bounds.center`**——匯入「小牛搬運車」時貼近巨大物體拍照，把正確的旋轉（FBX 原生的 `(270,0,0)`）連續誤判成錯的三次（試了 0°/90°/180° 全部更差），拉遠鏡頭＋對準當下重新讀出的 bounds 中心才發現原本就是對的。跟 [[oblique-screenshot-perspective-misleads-position-judgment]] 是同一類陷阱。
- **Play 模式手動擺的位置不會自動存檔**——使用者兩次在 Play 模式親自把猜猜看/摩托超載拖到想要的位置，都需要我讀出 Play 模式當下的即時座標、退出 Play 後手動套回編輯模式的正式場景物件再存檔，否則退出 Play 時 Unity 會把這些物件還原回存檔前的狀態，使用者的擺放全部消失。
- **柵欄樹放大 2 倍貼牆**：只加 Y 軸（Unity Euler 是 ZXY 外部合成順序，Y 分量繞真正世界垂直軸轉，不受既有 X=270 的 Z-up 修正影響），放大後用當下重新算出的 bounds 校正貼地高度，X 位置也要重新置中到目標牆段範圍內（放大到 ~30m 見方後，原本的 X 會讓它伸出大門缺口）。
- **移除 `MainLodge_*`／`Workshop_*` 灰盒佔位建築**（一直是純 Cube + 灰色 `Lit` 材質，「環抱式中庭要塞」那輪說好先驗證空間、之後換真模型，一直沒換）。
- **匯入 Meshy FBX 時，貼圖 sRGB 旗標預設全部是 `true`**——法線/金屬/粗糙度三張要手動改成 `sRGB=false`（法線圖另設 `textureType=NormalMap`），只有 diffuse 該留 `true`。仿照既有 Meshy 材質的做法（`_BaseMap`+`_BumpMap`+固定 `_Metallic`/`_Smoothness` 純量，不接 metallic/roughness 貼圖——URP Lit 沒有獨立的粗糙度貼圖欄位，要接上得先把兩張圖打包成一張 `_MetallicGlossMap`，這次沒做）建材質。
- **「必須超大」跟「營地本身空間有限」會直接衝突**——小牛搬運車要求做到「全部建築物裡最大」+「猜猜看初始位置看得到」，反推的目標尺寸（~48m）已經接近營地本身的總寬度（54m），貼著遮雨棚放的結果是有一大截（~43m）伸出西側圍牆外。這種情況沒有兩全其美的擺法，選擇忠於「超大」而不是遷就圍牆範圍，明確跟使用者說清楚這個取捨，讓他決定要不要接受。

## 維護

這份檔案是 `~/.claude/projects/C--Live2DAction/memory/` 的版控化副本。
在該 memory 目錄新增/修改記憶時，把仍然相關的內容同步進這裡，讓乾淨 clone 也拿得到。
