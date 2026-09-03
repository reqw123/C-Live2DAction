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

## 維護

這份檔案是 `~/.claude/projects/C--Live2DAction/memory/` 的版控化副本。
在該 memory 目錄新增/修改記憶時，把仍然相關的內容同步進這裡，讓乾淨 clone 也拿得到。
