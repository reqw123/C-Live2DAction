# Boss 開場演出 — Samurai Boss Intro Cutscene

> **狀態（更新 追加92, 2026-09-01）：已轉正接入 `GreyboxTest.unity`。** 走近 `武士` → 過場演出 →
> 直接開打。原始的隔離 demo 場景 `SamuraiBossArena.unity` 保留當 pipeline 參考（仍不在 Build
> Settings）。轉正細節見 **§9**。
>
> 原始狀態：切片外的技術探索，由一次 `/grill-with-docs` grilling session 產出，兼作術語表與決策
> 記錄（取代 `docs/adr/`，因為本專案的慣例是 `Docs/` + `CHANGELOG.md` + memory）。§1–§8 是那次探索
> 的原文記錄。

---

## 一、術語表 (Glossary)

**Boss 開場演出 (Boss Intro Cutscene)**
玩家首次抵達 Boss 時播放一次的 Timeline 過場；播完才把戰鬥控制權交給玩家。
_避免_：cutscene（太泛）、開場動畫（暗示預錄影片——這是引擎即時演出）

**起手式 (Ready Stance)**
武士把**已出鞘**的刀舉到過頭備戰姿勢——整段演出的張力核心。
_避免_：拔刀 / Blade Draw（本探索刻意不做字面拔鞘，武士沒有刀鞘網格）、居合 / Iaido（武士不是居合流）

**拔刀信號 (Blade-Draw Signal / `OnBladeDrawSignal`)**
Timeline Signal Track 在架式頂點打出的一次性信號，同時觸發刀光 VFX + 刀鳴 SFX + 相機 Impulse。
（名稱保留「拔刀」二字是因為玩家感受上就是「出鞘的鏘一聲」，即使動作上是舉刀。）

**開場總控 (`BossIntroManager`)**
在過場前後開關「玩家控制 / 玩家 UI / Boss 戰鬥 AI / Boss 血條」的控制器。
寫成**泛型 disable 清單**（`Behaviour[]` / `GameObject[]` / `PlayableDirector`），與具體專案型別解耦——
日後若轉正，同一個 component 直接把 `GreyboxTest` 的 `CharacterMovement` / `PlayerCombat` /
`BossStateMachine` / `WushiBossHud` 拖進陣列即可。

**過場相機 (Cutscene Camera)**
demo 場景內單獨一台掛 `CinemachineBrain` 的 `Camera`，被 Timeline 的 Cinemachine Shot Track 驅動。
與 gameplay 的 `ThirdPersonCameraController` 是**兩回事**（本 demo 不含後者）。

### 命名統一

這個 boss 一律叫 **`武士`**（GameObject 名）。
- `Wushi` = 資產檔前綴（`Wushi.fbx` / `Wushi_SwordJudgment.fbx` / `Wushi.controller`），**不是** runtime 名。
- `Samurai_Boss` = 原始規格用的別名，**不採用**——runtime / 場景 / Timeline 綁定不要出現。

---

## 二、決策記錄 (Decisions)

| # | 決策 | 定案 | 理由 / 取捨 |
|---|---|---|---|
| S1 | 這套系統佔專案哪個位置 | **切片外的獨立技術探索**。不進正式流程、不動 `GreyboxTest`、不加 Build Settings | 切片 Boss 戰本身還在調手感/架勢/格擋（Roadmap Step 7–9 之間）；CLAUDE.md 規則 5「切片穩定前不擴充範圍」。做成可丟棄的隔離 demo，先驗 pipeline |
| S2 | 呈現方式：3D 引擎過場 vs Live2D 演出層 | **3D 引擎過場**（Timeline + Cinemachine），標記為「與 Live2D 演出層並行、正式方向未定案」 | 專案 Goal 是「Live2D 劇情演出 ＋ 3D 動作戰鬥」，劇情本來走 Live2D；但 Boss 開場用 3D 運鏡展示氣場（隻狼 / 仁王式）其實更合適。這是要寫進 GDD 的方向決策，本探索先做出來看效果 |
| C1 | 過場相機技術 | **Cinemachine 3.x**（`CinemachineCamera` ×3 + `CinemachineBrain`） | 專案當年放棄 Cinemachine 是**跟隨/瞄準**（`CinemachineOrbitalFollow` + `RotationComposer`）的「畫圈」bug；Timeline 的**固定機位切換**是 Cinemachine 最穩、跟那個坑無關的用法。套件（3.1.2）還在 manifest。完全隔離在過場相機上，不碰 gameplay 相機 |
| A2 | 「拔刀」語意 | **舉刀起手式**（不加刀鞘、非字面拔鞘） | 武士沒有 saya 網格、沒有拔刀 clip；他是持刀作戰的設定不是居合流。加刀鞘＋找對的居合動畫＋對準拔鞘幀 = 工太多，探索不值得 |
| A1 | 前搖 clip | **`Wushi_SwordJudgment`**，Timeline clamp 到 nt 0–0.42、~0.6× 速度、Signal ≈ nt 0.15 | 離線量測：SwordJudgment 在 nt 0.13（~0.43s）就快速舉刀過頭（手高於髖 1.67），並維持高舉到 nt ~0.38。「霍然舉刀、定格」的張力比 `Wushi_ChargeCut` 的拉弓式蓄力更有 boss 氣場。root 不位移 |
| E1 | demo 場景內容 | **最小替身**：`Wushi.fbx`+Animator（名 `武士`）／Player5 視覺或膠囊當 `Player`+`DemoPlayerController`／空殼 `DemoBossAI`+`DemoBossHealthBar` GameObject | demo 是過場不是戰鬥，不需要真的 `BossStateMachine`/`Health`/hitbox/HUD。專案的 `Player`/`武士` 都沒有 Prefab（是十幾支 bootstrap 腳本拼的場景實例），copy 過來會把整套戰鬥機器拖進要丟的場景 |
| — | `BossIntroManager` 形狀 | 泛型 disable 清單（`Behaviour[]` playerControlScripts / `GameObject[]` playerUi / `Behaviour` bossCombatAI / `GameObject` bossHealthBar / `PlayableDirector` introTimeline） | 轉正時可直接指向真實元件，不用改腳本 |
| V1 | 刀光 | 複用 `T_SlashCrescent_XSlash8x8_Clean`（或 `SwordOrbit_Atlas`）+ `SlashFlipbookURP`，刀身一顆 one-shot ParticleSystem，`OnBladeDrawSignal()` → `ps.Play()` | 零新素材，比照專案既有 VFX 做法 |
| V1 | 拔刀音效 | **程序合成金屬「鏘—」placeholder WAV**（比照 `GunshotSfxSetup`），`Assets/_Project/Audio/Skills/KatanaDraw.wav`，標可替換 | 專案沒有拔刀音檔、使用者手邊也沒有 |
| C3 | 相機震動 | **Cinemachine Impulse**：`CinemachineImpulseSource` 掛 `武士`、`CinemachineImpulseListener` 掛過場相機，`OnBladeDrawSignal()` → `GenerateImpulse()` | 隔離在過場相機上、跟 gameplay 的 `CameraShake.cs` 零衝突；是規格明寫的、也是 Cinemachine 過場的標準配對 |
| — | 相機交接 | 單台 Camera + Brain；3 台 vcam 走 Timeline Cinemachine Shot Track；`introTimeline.stopped` 後第 4 台「gameplay」vcam 接手（demo 沒有 `ThirdPersonCameraController`） | — |
| — | 路徑 | 場景 `Assets/_Project/Scenes/SamuraiBossArena.unity`；腳本 `Assets/_Project/Game/Cutscene/`（`Live2DAction.Runtime` asmdef，Timeline 綁定需要可解析的 assembly；規格的 `Assets/Scripts/Boss/` 會落到專案刻意避開的 Assembly-CSharp） | — |
| — | Build Settings | **不加入** `SamuraiBossArena` | 把要丟的探索場景擋在正式 Windows build 外 |

---

## 三、場景物件結構

```
SamuraiBossArena
├─ Arena (Plane, scale 5/1/5, 深色反射材質 SamuraiArenaFloor.mat)
├─ Lighting
│   ├─ Directional Light (壓暗, intensity ~0.25)
│   └─ Boss Spot Light (武士頭頂, 聚光燈感)
├─ 武士  (Wushi.fbx 實例, Animator=Wushi.controller)
│   ├─ CinemachineImpulseSource
│   └─ SignalReceiver (Unity) + BossSignalReceiver (本專案) — SignalReceiver 的 UnityEvent 接 BossSignalReceiver.OnBladeDrawSignal
│   └─ BladeDrawVFX (ParticleSystem, SlashFlipbookURP) + AudioSource (KatanaDraw.wav)
├─ Player  (Player5 視覺 or 膠囊, Tag=Player)
│   ├─ CharacterController
│   └─ DemoPlayerController
├─ DemoBossAI (空 GameObject + DemoBossAI 腳本)
├─ DemoBossHealthBar (Canvas, 世界空間或螢幕空間佔位)
├─ PlayerUI (Canvas 佔位)
├─ BossRoomTrigger (BoxCollider isTrigger, 在玩家走向武士的必經路徑上) + BossTrigger 腳本
├─ BossIntroManagerObject (PlayableDirector + BossIntroManager)
├─ CutsceneCamera (Camera + CinemachineBrain + CinemachineImpulseListener)
├─ CM_Vcam_Back    (武士背後, 特寫背影與握刀)
├─ CM_Vcam_Face    (武士正面, 特寫起手式與眼神)
├─ CM_Vcam_Action  (廣角對峙, 涵蓋玩家與武士)
└─ CM_Vcam_Gameplay (過場結束後接手的預設機位)
```

## 四、Timeline (`BossIntro.playable`) 軌道

| Track | 綁定 | 內容 |
|---|---|---|
| Animation Track | `武士` 的 Animator | `Wushi_SwordJudgment`，clamp 到起手式段、~0.6× 速度 |
| Cinemachine Track | `CutsceneCamera` 的 `CinemachineBrain` | Shot: `CM_Vcam_Back` → `CM_Vcam_Face` → `CM_Vcam_Action`，含 blend |
| Signal Track | `武士`（或專用 receiver 物件） | 起手式頂點插一個 Signal Emitter → `BladeDraw.signal` → `BossSignalReceiver.OnBladeDrawSignal()` |

## 五、流程

```
玩家走進 BossRoomTrigger
  → BossTrigger.OnTriggerEnter(tag=="Player")
    → BossIntroManager.StartIntro()
        • 停用 playerControlScripts[] + playerUi[]
        • 停用 bossCombatAI + bossHealthBar
        • introTimeline.Play()
    → Timeline 播放：SwordJudgment 前搖 + 3 鏡頭運鏡
        • 在頂點幀：Signal → BossSignalReceiver.OnBladeDrawSignal()
            → BladeDrawVFX.Play() + AudioSource.Play() + impulseSource.GenerateImpulse()
    → introTimeline.stopped 事件
        → BossIntroManager 恢復：playerControl / playerUi / bossCombatAI / bossHealthBar
        → CM_Vcam_Gameplay 接手
  → BossTrigger 自身 SetActive(false)（防重複觸發）
```

## 六、驗收

- [ ] 零編譯錯誤 / 警告
- [ ] `SamuraiBossArena.unity` 已存，**未**加入 Build Settings
- [ ] `BossIntroManagerTests`（EditMode/PlayMode）：`StartIntro()` 關掉清單裡每個 behaviour/GameObject；`OnTimelineStopped` 全部開回來
- [ ] 手動 Play：走進 trigger → 過場（3 鏡頭 + 起手式動畫 + Signal 觸發刀光/音效/震動）→ 控制交還 → trigger 不再觸發第二次
- [ ] 回報：檔案路徑、場景結構、Timeline 綁定狀態、截圖/GIF

## 七、轉正 checklist（若日後決定採用）— ✅ 追加92 完成，見 §9

1. ✅ `BossIntroManager` 的陣列改指 `GreyboxTest` 的真實元件。
2. ✅ `BossTrigger` 放在武士前方（z≈4，在 `alertRange`=6 外），過場期間 `BossStateMachine` 直接 `enabled=false`；過場結束後 `BossStateMachine.ForceEngage()` 直接接敵（跳過 `Dormant→Alert` 的距離判定）。
3. ✅ 過場相機：`BossIntroManager` SetActive 切換 `Main Camera` ↔ `BossIntroCutsceneRig`，並把 `ThirdPersonCameraController`/`CameraPossessionSwitcher`/`ViewFocusDirector`/`SpectatorCameraToggle` 一起停用（否則它們 LateUpdate 會把 `Main Camera` 開回來）。
4. ⚠️ `DemoPlayerController`/`DemoBossAI`/`DemoBossHealthBar` **未**刪 —— 還被 `SamuraiBossArena.unity`（保留的參考 demo）用著。
5. ⬜ 是否在 Live2D 對話之後接這段（S2 正式方向）—— 仍待 GDD 決定。
6. ⬜ `SamuraiBossArena` 保留當參考，未改造成正式 Boss 房。

---

## 八、實作結果（2026-09-01 建置）

**選單**：`Tools/Live2DAction/[Exploration] Build Samurai Boss Arena`（`Assets/Editor/Bootstrap/SamuraiBossArenaSetup.cs`，可重跑）。

### 交付檔案
| 檔案 | 內容 |
|---|---|
| `Assets/_Project/Scenes/SamuraiBossArena.unity` | demo 場景（14 roots，**不在 Build Settings**） |
| `Assets/_Project/Game/Cutscene/` | `BossTrigger` / `BossIntroManager` / `BossSignalReceiver` / `DemoPlayerController` / `DemoBossAI`（皆註明「探索用」） |
| `Assets/_Project/Timeline/BossIntro.playable` | 3 軌：Animation（`武士` Animator）/ Cinemachine（Brain on CutsceneCamera）/ Signal（`武士` SignalReceiver） |
| `Assets/_Project/Timeline/BladeDraw.signal` | Signal Asset |
| `Assets/_Project/Timeline/Wushi_SwordJudgment_InPlace.anim` | `Wushi_SwordJudgment` 去掉水平 root motion 的副本（見下方踩坑） |
| `Assets/_Project/Audio/Skills/KatanaDraw.wav` | 程序合成的金屬「鏘」拔刀聲，可替換 |
| `Assets/_Project/VFX/SamuraiArenaFloor.mat` | 深色高反射地板材質 |
| `Assets/_Project/Tests/EditMode/BossIntroManagerTests.cs` | 3 個測試（disable/enable 記帳 + null-safe），**243/243 綠** |
| `Live2DAction.Runtime.asmdef` | 加 `Unity.Cinemachine` + `Unity.Timeline` 參照（轉正若不採用，刪 Cutscene 資料夾 + 這兩行即可） |

### 流程（Play → 走進 `BossRoomTrigger`）
`BossTrigger.OnTriggerEnter("Player")` → `BossIntroManager.StartIntro()`（關 `DemoPlayerController` / `PlayerUI` / `DemoBossAI` / `DemoBossHealthBar`、`introTimeline.Play()`）→ Timeline：`Wushi_SwordJudgment` 前搖 nt 0–0.28、0.4× 速度、原地播放；相機 **硬切** Back → Face → Action；`apex`(~1.3s) Signal → `BossSignalReceiver.OnBladeDrawSignal()` → 刀光 ParticleSystem + `KatanaDraw.wav` + `CinemachineImpulseSource.GenerateImpulse()` → Timeline `stopped` → `BossIntroManager` 全部開回來、`CM_Vcam_Gameplay` 接手。有 `duration + 1.5s` realtime 的 failsafe。

### 建置時踩到 / 修掉的坑
1. **Timeline `stopped` 立即觸發的錯覺**：其實 Editor 空場景跑太快（>1000fps），2.77s 的 Timeline 在兩次取樣之間就跑完 + `DirectorWrapMode.None` 把 `time` 歸零。系統一直是對的。
2. **`Wushi_SwordJudgment` 帶 root motion**：`lockRootPos*` 全 false，Timeline 任何 trackOffset 模式都壓不住 → boss 播放中往 +X/+Z 漂 ~1m + 沉入地板。解法：`EnsureInPlaceClip` 複製一份、只刪 `RootT.x`/`RootT.z`/`RootQ` 曲線（**`RootT.y` 必須留** —— Humanoid clip 沒有它整個 pose 會塌到地上）。
3. **Meshy 模型退化的 SkinnedMeshRenderer bounds**（localBounds extents ~44 單位）→ 過場相機一沒對準那個幻影中心，boss 就被視錐剔除、整個消失。解法：`smr.updateWhenOffscreen = true`（跟 GreyboxTest 的 boss、校園 FBX 同一招）。
4. **前搖範圍**：`ClipPortion` 0.5 會播到下劈起手、身體蹲低 → 不像「起手式」。收到 **0.28**（只有舉刀 + 停頓）。
5. **相機硬切**：`brain.DefaultBlend = Cut`、每個 shot `blendIn=0` —— 除了風格對（隻狼/仁王），也讓框景可預測（沒有多秒 blend）。

### 待微調（Scene view 拖 vcam，非決策）
- `CM_Vcam_Back` / `CM_Vcam_Face` 框景不錯（boss 填滿、氣勢夠）；`CM_Vcam_Action` 廣角要把相機再拉遠、平衡玩家與 boss 的比例（目前玩家膠囊偏大偏右）。
- Editor game view 目前是超寬比例（2.78），正式 16:9 下框景會不同。
- Signal 精確幀、shot 切點、Impulse 振幅、`KatanaDraw.wav` 音色、地板/燈光亮度 —— 全部 in-Editor 微調。
- 手動 Play 完整跑一次（走進 trigger）確認交接手感。

---

## 九、轉正實作（追加92, 2026-09-01）— 接入 `GreyboxTest.unity`

**選單**：`Tools/Live2DAction/[Boss Intro] Wire Into GreyboxTest`
（`Assets/Editor/Bootstrap/BossIntroGreyboxSetup.cs`，可重跑、idempotent —— 每次先拆掉上一輪加的東西再重建。
只在 `GreyboxTest.unity` 為 active scene 時才會執行）。

### 改動的既有腳本
| 檔案 | 改動 |
|---|---|
| `BossStateMachine.cs` | 加 `public void ForceEngage()` —— 從 `Dormant`/`ReturnHome`/`GateWatch` 直接 `_hasEngaged=true` + `ChangeState(Alert)`，讓過場一結束就進戰鬥，玩家不用再走近。terminal state / 已接敵時是 no-op |
| `BossIntroManager.cs` | 加 `cutsceneCameraRoot`（過場開/結束 SetActive 切換）、`gameplayCamera` 反向切換、`UnityEvent onIntroComplete`（過場結束後 fire，接 `ForceEngage`）、`_finished` 一次性旗標 |
| `BossTrigger.cs` | 加 `playerRoot` Transform 欄位 —— `GreyboxTest` 的 `Player` 是 Untagged，改用「碰撞體在不在 `playerRoot` 底下」判定；沒設才 fallback 回 tag |

### 選單在 `GreyboxTest` 場景裡加的東西
| 物件 | 內容 |
|---|---|
| `BossRoomTrigger` | `BoxCollider` isTrigger，pos (0,1.6,4) size (30,4,1)。武士在 z=11、`alertRange`=6，所以 z=4 = 7m 外，過場搶在 boss 自動醒來之前 |
| `BossIntroCutsceneRig`（起始 inactive） | `IntroCam`（Camera depth 20 + AudioListener + `CinemachineBrain` DefaultBlend=Cut + `CinemachineImpulseListener`）+ `CM_Vcam_Back`/`Face`/`Action` |
| `BossIntroManagerObject` | `PlayableDirector` + `BossIntroManager` |
| 掛到真實 `武士` 上 | `BladeDrawVFX` 子物件（`Attack3SlashEffect` 實例）、拔刀 `AudioSource`（`KatanaDraw.wav`）、`CinemachineImpulseSource`、`BossSignalReceiver`、Timeline `SignalReceiver` |
| `Assets/_Project/Timeline/BossIntro_Greybox.playable` | 3 軌，複用 demo 的 `Wushi_SwordJudgment_InPlace.anim` + `BladeDraw.signal` |

### `BossIntroManager` 接線
- `playerControlScripts`（11）：`PlayerInputProvider`/`CharacterMovement`/`PlayerCombat`/`TargetLockController`/`UltimateAbility`/`PlayerGuard`/`ExecutionAbility`（都在 `Player`）+ `ThirdPersonCameraController`（`Main Camera`）+ `CameraPossessionSwitcher`（`CameraPossession`）+ `ViewFocusDirector`（`ViewDirector`）+ `SpectatorCameraToggle`（`BugSpectator`）
- `playerUi`：`PlayerCornerHud`
- `bossCombatAI`：真實 `武士` 的 `BossStateMachine`
- `bossHealthBar`：**留 null** —— `WushiBossHudVisibility` 已按 boss state 開關 HUD（`Dormant` 時本來就隱藏）
- `cutsceneCameraRoot`：`BossIntroCutsceneRig`；`gameplayCamera`：`Main Camera`
- `onIntroComplete`：serialized persistent listener → `武士.BossStateMachine.ForceEngage`

### 驗證
- EditMode **244/244 綠**（新增 `OnIntroComplete_FiresExactlyOnce_EvenOnDoubleStop`）。
- Play（Editor 失焦、frame frozen，只能驗控制交接不能驗 Timeline 播放）：`StartIntro()` → 玩家控制關、`Main Camera` inactive、rig active ✅；模擬 `stopped` → 控制全開回、`Main Camera` active、rig inactive、`PlayerCornerHud` 回來、**`boss.CurrentState == Alert`**（`ForceEngage` 經 persistent UnityEvent 有觸發）✅。
- **待使用者在有焦點的 Editor 手動 Play 一次**：走進 trigger → 過場 3 鏡頭 + 起手式動畫 + Signal（刀光/鏘/震動）→ 完整跑完 → 交還控制 → 武士追過來對打。

### 相機框景修正（追加92 續，使用者回報「視角不對，只看到 boss 的頭」）
第一版把 4× 武士當 7m 高（估錯），vcam 擺在 y 5–6（頭頂以上）→ 只框到頭。用 BakeMesh 實測：4× 武士 **腳 y≈0.6、頭 y≈4.6（~4m 高、胸 y≈2.6）**。改用 viewport 投影檢查 + 真的 `Camera.Render()` 離線截圖對照，重擺三機位：
- **Face**：pos (−1.6, 3.25, 4.1) fov 42 —— 正面全身，腳在畫面 y≈0.15、頭 y≈0.88。
- **Action**：pos (−10.5, 4.0, 5.3) fov 52 —— 廣角，武士偏左、玩家偏右。
- **Back**：pos (0.85, 3.95, 14.6) fov 50 —— `BoundaryWall_North` 在 z=15.5（武士後方只有 ~4m），再往後就拍到牆的咖啡色背面 → 只能做**貼身過肩**特寫（背影 + 頭 + 肩甲）。
- 順帶把武士的 `SkinnedMeshRenderer.updateWhenOffscreen` 設 True（Meshy 退化 bounds，Back 機位一開始整個 boss 被剔除）。截圖存 scratchpad `intro2_*.png`。

### 已知風險 / 待觀察
- Timeline `AnimationTrack` 播 `Wushi_SwordJudgment_InPlace` 時 `BossStateMachine` 是 `enabled=false`，Animator 由 Timeline 接管；過場結束 `ForceEngage`→`Alert` 會 CrossFade 回正常戰鬥 clip。若看到一幀 T-pose 或姿勢跳動，需在 `RestoreControl` 後補一個 Animator rebind。
- `BossRoomTrigger` box 只擋 x −15..15；玩家若從場地邊緣繞過去仍可能直接踩到 `alertRange`。greybox 夠用，正式關卡要用實體門檻。
- 過場中同時有兩個 `AudioListener`（`Main Camera` 被 SetActive(false) 就只剩 rig 的）——已用 SetActive 切換避免，不是 disable component。
