# yuanpei_LogoSky 空中 Boss — 實作現況

> 依 `yuanpei_LogoSky_Boss_工程說明文件.md`（v1.0）實作。本檔記錄「做了什麼 / 沒做什麼 / 檔案在哪」。
> 進度看 `Docs/CHANGELOG.md`。⚠️ 校徽是元培真實商標 → 整套 DoNotShip（見 `ASSET_LICENSES.md` / `KNOWN_ISSUES.md §7`）。

## 一句話

空中遠距法術型 Boss：升空保持射程、9 種招式逼玩家走位（追加94 續 119：MultiAoE 多重光爆已移除、含 3 種肉身衝撞變體；續 136 新增遠距離連續發射 SpearVolley/長矛型光彈）、玩家攻擊削 HP＋累積架勢 → 架勢滿 → 墜地 →
5 秒 F 處決窗口 → 20~22% 最大 HP 傷害 →（未死）重新升空。**只有 HP 歸零才勝利。**

## 檔案

| 路徑 | 責任 |
| --- | --- |
| `Assets/_Project/Game/AI/Boss/Yuanpei/YuanpeiBossConfig.cs` | 全域數值 SO（距離/高度/資源/階段/處決/完美閃避） |
| `…/YuanpeiAttackDef.cs` | 單招 SO（Telegraph→Windup→Active→Recovery、能量、冷卻、射程、number1~3/count） |
| `…/YuanpeiBossVitals.cs` | HP（委派 `Core.Health`）／Energy／Posture 權威；倍率、階段、事件。`YuanpeiPhaseLogic` 純函式 |
| `…/YuanpeiScheduler.cs` | **純**招式選擇（候選過濾 §10.1 + 情境權重 §10.2 + LRU）。EditMode 測 |
| `…/YuanpeiBoss.cs` | 頂層 FSM（15 狀態）＋ 空中移動（懸浮/面向/保持距離/避繞出場外）＋ 排程驅動＋ Intro（從天空校徽降下縮小） |
| `…/YuanpeiAttacks.cs` | 8 招 coroutine（續 119 起），greybox 幾何＋純色 emission，明確 Hit Window |
| `…/YuanpeiProjectile.cs` | 光粒子（可閃可被武器打掉，`IDamageable`），初段輕微追蹤 |
| `…/YuanpeiHazard.cs` | 地面危險：落雷圈 / 延遲光爆圈 / 擴張衝擊環（環帶命中一次） |
| `…/YuanpeiExecution.cs` | 架勢崩潰→墜落→5s F 窗口→處決傷害（在命中事件，非按下瞬間）→重升空／死亡 |
| `…/YuanpeiBossHitReceiver.cs` | 掛在 BodyCollider / CoreWeakPoint，把玩家攻擊 route 進 Vitals（背核 / 低能量 / 倒地 / 完美反擊倍率） |
| `…/YuanpeiPerfectDodge.cs` | 危險逼近時閃避 → 完美閃避旗標（0.1s 慢動作）→ 下次攻擊 1.5× 架勢 |
| `…/YuanpeiBossHUD.cs` | 螢幕型 HUD：名稱、HP（紅大條）、Energy（青，滿→降）、Posture（橘，0→滿，近滿閃爍）、`[F] 處決` |
| `…/YuanpeiEncounter.cs` | 觸發體積 → 開場 → `BeginEncounter` → 勝利流程（HUD 淡出、lock-on 釋放、通知） |
| `Assets/_Project/Settings/Combat/Yuanpei/YuanpeiBossConfig.asset` ＋ `YuanpeiAttack_*.asset` ×6 | 資料 |
| `Assets/_Project/Tests/EditMode/YuanpeiBossLogicTests.cs` | 15 個測（階段門檻 + 排程過濾），303/303 綠 |

## 場景配置（`Map_School.unity`）

`yuanpei_LogoSky`（就是天空校徽）：root scale 1、`VisualRoot` 子物件（校徽 mesh，scale 1700 = 天空地標大小；Intro 縮到 ×0.28 ≈ 戰鬥用 ~9m）。
- 子：`ProjectileOrigin` / `LaserOrigin` / `GroundRayOrigin` / `ExecutionAnchor` / `AimPoint` / `CollisionRoot`（`BodyCollider` r3.6 trigger + `CoreWeakPoint` r1.9 trigger，各掛 HitReceiver）。
- root 掛：`Health`(hp 1200, `deferDeactivation`) + `YuanpeiBossVitals` + `YuanpeiAttacks` + `YuanpeiExecution` + `YuanpeiBoss` + `YuanpeiBossHUD` + `YuanpeiPerfectDodge` + `LockOnTarget`(cameraDistanceMultiplier 2.4)。

`YuanpeiEncounter`（新物件，plaza 內 (0,2,-105)，20×5×12 trigger）：玩家走進 → 開戰、`combatCenter (0,0.5,-114)`。

`ChargeCrashSurface` layer（slot 9）：3 顆 `yuanpei_*_Collision` box proxy 已標記 → 肉身衝撞撞上會終止＋暈眩 2.5s＋大量自身架勢。

## 已完成（spec §21 Phase 1–4）

- ✅ Boss prefab / pivot / collider / anchor
- ✅ HP / Energy / Posture + 狀態優先順序 + 死亡一次性鎖 + 3 條 HUD
- ✅ 懸浮 / 面向 / 理想距離 / 視線 / 出場外拉回 + Phase 解鎖 + 候選過濾 + Attack Lock + 冷卻 + 全域間隔
- ✅ 8 招原型（幾何＋純色），各有 Telegraph/Active/Recovery/Cancel（續 111 +3 衝撞變體、續 119 −MultiAoE）
- ✅ 架勢滿鎖定 + 攻擊取消 + 墜落 + 地面定位 + ExecutionWindow + F 判定 + 對齊 + 處決傷害事件 + 錯過處決 + 重升空
- ✅ 能量耗盡 ≠ 可處決（HUD 不顯示 F）；HP 歸零優先於一切
- ✅ 完美閃避 → 反擊架勢加成（greybox 版）

## 尚未完成（Phase 5–6 + §3.1，後續）

- ❌ **正式 VFX**（Shader / Particle / Line-Trail / Decal / Bloom）—— 目前全是 primitive + emission 色塊
- ❌ **音效**（§17 各招預警、命中、墜地、處決、死亡）
- ❌ **鏡頭**（大招前保持可視、完美閃避慢動作只用 HitStop、處決專用 Cinemachine）
- ❌ **模型最佳化**：29 萬面 → 15k~30k + LOD0/1/2（需 Blender，§3.1）；目前用整顆 mesh
- ❌ **Object Pool**：投射物 / VFX 目前 Instantiate/Destroy
- ❌ **平衡**：所有時間需依玩家實測閃避資料校準（§8.3）；傷害/HP/處決次數待 Play 調
- ❌ **場地**：plaza 中央夠平但未特別整理；背景無延伸
- ❌ Boss 戰專屬「停用防禦」Combat Rule Set（§8.1）—— 本專案玩家防禦是右鍵，進 Boss 戰未強制停用

## Play 驗證（需對焦 Editor）

1. 走學校大門進校園 → 走到 plaza 中央（過 `YuanpeiEncounter` trigger）→ 天空校徽降下、HUD 出現、開戰。
2. Boss 空中丟招；玩家攻擊 Boss → HP 降、Posture（橘條）漲。
3. Posture 滿 → Boss 失控旋轉墜地 → `[F] 處決` 出現（距離內）→ 按 F → 處決 → Boss 重升空（HP 未歸零）。
4. 重複到 Boss HP 歸零 → 死亡 → HUD 淡出 → Console `[YuanpeiEncounter] ... player victory`。

---

## Boss 支配領域全螢幕邊界特效（2026-09-06，續 174）

依使用者的完整工程規格實作。**螢幕空間**的「支配領域」效果 —— 固定在畫面四周＋四角，跟著整場 Boss 戰，Boss 死亡/戰鬥解除才消失。不是世界中的 Particle System，也不是受傷紅框。

### 使用的技術
- Render Pipeline：**URP 17.0.4**（`Live2DAction_URP.asset` → `Live2DAction_Renderer.asset`，全 Quality Level）
- **自訂 `ScriptableRendererFeature`**（`BossDomainScreenVFXRendererFeature`，RenderGraph-native，注入 `BeforeRenderingPostProcessing`）—— 加在 `Live2DAction_Renderer.asset`（唯一的 pipeline-wide 改動）
- 高效能 **Fullscreen HLSL Shader**（`Live2DAction/VFX/BossDomainScreenVFX`）—— 無影片、無大型貼圖，噪聲全程序生成（fbm）
- Screen-Space-Overlay HUD/血條/提示天生渲染在整條 pipeline 之後 → 永遠蓋在特效上方（§6.1 自動滿足）

### 檔案
| 檔案 | 內容 |
|---|---|
| `Assets/_Project/VFX/Shaders/BossDomainScreenVFX.shader` | Fullscreen shader：Edge Mask（螢幕高度單位、自適應 16:9/16:10/21:9）、中央 ~75% 硬 early-out（完全不讀取/不著色）、四角強度高於四邊、兩層不同縮放/方向 scroll 噪聲、Edge Dissolve（不規則、非矩形）、黑霧 vignette、翠綠 Emission、呼吸、劍痕/符文/灰燼、僅邊界的輕微 UV 扭曲（`* edge`）、進場/常駐/脈衝/消散參數 |
| `Assets/_Project/Game/VFX/Rendering/BossDomainScreenVFXRendererFeature.cs` | Feature：無序列化材質、無資源；靜態借用控制器註冊的 runtime 材質，未註冊時 `AddRenderPasses` 直接 return（§7 Boss 戰外零成本）。Game camera only（排除 Scene View / preview / reflection）、Base camera only |
| `Assets/_Project/Game/AI/Boss/Yuanpei/BossDomainScreenVFX.cs` | 控制器 + 純 `BossDomainEnvelope`（可單元測試的狀態機）。API：`BeginDomain / SetPhase(int) / Pulse(float) / EndDomain / SetIntensity(float)`。狀態：`Inactive / Entering / Active / PhasePulse / Exiting`。runtime 材質實例（不碰 .mat asset），參數用快取 property ID 推送（Update 無 GC）。`bossVitals` 有接就自動跟隨階段 |
| `Assets/Editor/Bootstrap/BossDomainScreenVFXSetup.cs` | 選單「Tools/Live2DAction/Setup Boss Domain Screen VFX」—— 建材質＋加 Renderer Feature（比照 URP `ScriptableRendererDataEditor.AddComponent`）＋在 Map_School 的 `yuanpei_LogoSky` 掛控制器並接線。可重複執行 |
| `Assets/_Project/VFX/Materials/BossDomainScreenVFX.mat` | 材質（shader + §4 預設值；rune 貼圖欄位留空給使用者指定） |
| `Assets/_Project/Tests/EditMode/BossDomainScreenVFXTests.cs` | 12 個 EditMode 測試（狀態轉換、enter/exit 時序、pulse 一次性衰減、phase clamp、intensity clamp、EnterExit 恆在 0..1、負 dt 不炸）—— **全綠** |

### 修改的檔案
- `Assets/_Project/Settings/Live2DAction_Renderer.asset` —— 加 1 個 Renderer Feature（`injectionPoint: 550` = BeforeRenderingPostProcessing）
- `Assets/_Project/Game/AI/Boss/Yuanpei/YuanpeiEncounter.cs` —— +1 序列化欄位 `domainVfx`；`Awake` 自動尋找；`StartEncounter` 尾端 `domainVfx?.BeginDomain()`；`Victory()`/`Defeat()` 開頭 `domainVfx?.EndDomain()`。**不動任何既有戰鬥/lockdown/no-defence/victory 流程**
- `Assets/_Project/Scenes/Map_School.unity` —— `yuanpei_LogoSky` 掛 `BossDomainScreenVFX`（sourceMaterial + bossVitals 接好）；`YuanpeiEncounter.domainVfx` 指過去

### 連接的 Boss 事件
- **開戰** → `YuanpeiEncounter.StartEncounter()` → `BeginDomain()`（四周變暗 → 四角黑霧內擴 → 翠綠火焰點燃 → ~1.2s 進低強度常駐，中央迅速恢復清楚）
- **第二/三階段** → 控制器輪詢 `YuanpeiBossVitals.Phase`（血量跨 `phase2/phase3HealthFraction`）→ `SetPhase(p)` → 一次翠綠脈衝＋向中央的能量波＋短暫輕微扭曲 → 回到稍強常駐。也可外部 `SetPhase()` / `Pulse()` 手動觸發
- **勝利 / 玩家死亡** → `Victory()` / `Defeat()` → `EndDomain()`（~2s 消散 → 完全透明 → `s_Material` 清空、Fullscreen Pass 完全 inert）
- **天空巨劍同步增亮**：`onPhasePulse` UnityEvent（空的、可選接）—— 未強制耦合（§5.2.4）

### Inspector 可調參數
Base Intensity / Enter・Exit・Pulse Duration / Pulse Strength / Use Unscaled Time / Edge Width（螢幕高度 0.08–0.15，預設 0.12）/ Corner Strength / Fog Opacity / Flame Intensity / Emission Speed / Noise Scale・Speed / Distortion Strength（常駐極低 0.004）/ Rune Intensity / Breath Period（5–8s）/ Breath Amount / Domain Color（深黑＋翠綠靈魂色）/ Rune Texture（**留空給使用者指定**）

### 實際測試結果（Play 模式，Editor 對焦）
- ✅ `BeginDomain()` → Entering → ~1.2s → Active；截圖確認四周翠綠火焰＋四角較強＋黑霧暗角，中央玩家/Boss/武士全清楚
- ✅ 玩家移動時效果固定於螢幕邊界（fullscreen pass 依螢幕 UV，攝影機/玩家動不了它）
- ✅ HUD 文字 + Boss 血條渲染在特效上方
- ✅ 640×230（超寬）測試下邊界厚度均勻、無拉伸
- ✅ `SetPhase(2)` → PhasePulse → 0.6s 衰減回 Active
- ✅ `EndDomain()` → Exiting → Inactive；`s_Material` = NULL（Pass 完全停止渲染）
- ✅ Console 無 Shader / RendererFeature / RenderGraph / NullReference 錯誤
- ✅ Shader `isSupported=True`、`GetShaderMessageCount=0`
- ✅ EditMode 12/12 綠
- 設計上無 per-frame GC（快取 property ID、單一 static MPB、envelope 方法零配置）；未註冊時 feature 早退不進 pass

### 尚需人工指定
- **Rune / 符文貼圖**（`BossDomainScreenVFX` 的 `runeTexture` 欄位）—— 留空＝符文關閉；目前劍痕是程序生成的噪聲脊線。要真的古老符文/劍形紋路請丟一張 R channel 貼圖
- **平衡/觀感微調**：`edgeWidth` / `fogOpacity` / `flameIntensity` / `cornerStrength` 目前是對著亮色測試場調的保守值，正式夜空場地會更明顯 —— 進 Boss 戰現場 Play-test 微調
- **`onPhasePulse`**：要讓天空巨劍在階段轉換時同步增亮，把巨劍的 emission 控制接到這個 UnityEvent
- **Bloom**：專案目前無 Bloom。shader 已自帶柔光不依賴它；若要更亮的綠火，在 `GreyboxVolumeProfile`（或 Boss 場地的 volume）加一個 Bloom override，注入點已在 post 之前所以會自動吃到

### 夜空全景圖（2026-09-06，續 175）

使用者：「`C:\Users\homec\Downloads\rogland_clear_night_4k.exr` 可以當作 boss 戰的全景圖嗎」 → 可以。Poly Haven「Rogland Clear Night」，4096×2048 equirectangular、真夜空銀河、**CC0 可出貨**。

- **匯入**：ffmpeg 降成 2K（`scale=2048:1024`，ZIP16 half-float）→ `Assets/_Project/Environment/Textures/rogland_clear_night_2k.exr`（8 MB，進版控 OK；4K 原檔留在 Downloads）。Import 設 2D / sRGB off / no mipmap / wrap Repeat / max 2048 → Unity 壓成 **BC6H**（HDR）。
- **下半球沙漠地面**：來源是完整 360 環境，下半是暗色沙漠丘陵。自訂 skybox shader `Live2DAction/Environment/SkyboxNightPanorama`（legacy CG lat/long + exposure + rotation + tint + **horizon-down darken 漸層**）—— `_HorizonDarken` 0.92 把地平線以下淡成近黑，只留上方星空。
- **材質** `Assets/_Project/Environment/Materials/Skybox_NightRogland.mat`：exposure 0.62（陰暗）、rotation 205°（銀河拱門轉到 Boss 懸浮方向）、horizon darken 0.92。
- **為什麼是 runtime 切換而不是設 Map_School 的 skybox**：地圖串流從來不把 Map_School 設成 active scene（`grep SetActiveScene` = 0 命中），所以 Boss 戰其實是用 **GreyboxTest 的 skybox（`Skybox_Procedural`，白天）** 在渲染。`BossDomainScreenVFX.BeginDomain()` 時 **swap `RenderSettings.skybox` + 壓低 ambient(0.35) + 開低霧**，快取原值，退場（Exiting → Inactive）/`OnDisable`/`OnDestroy` 時完整還原。等於「進入 Boss 支配領域，天空本身就變了」。
- **新增控制器欄位**（`BossDomainScreenVFX.cs`）：`domainSkybox`（夜空材質，留空＝不換天）、`darkenEnvironment`(true)、`domainAmbientIntensity`(0.35)、`domainAmbientColor`。
- **測試**（Play 模式，frozen-frame + 反射驅動 envelope）：`BeginDomain` → skybox `Skybox_Procedural` → `Skybox_NightRogland`、ambientIntensity 1.0 → 0.35；截圖確認暗夜空＋銀河在圍牆上方、綠色領域邊界在暗背景下明顯浮現、中央清楚。`EndDomain` 跑完 exit → skybox / ambient / fog 全部還原、`s_Material` = NULL。EditMode 12/12 綠。
- **尚需人工**：`_Rotation` / `_Exposure` / `_HorizonDarken` 進真實 Boss 場地 Play-test 微調；圍牆/地面仍由場景 directional light 打亮（沒動，避免影響全遊戲），若要更暗可在 Boss 戰另外調 sun。

### 續 176 — Boss 外觀被夜空色調染到，加可調鈕

使用者：「boss 的外觀似乎有被夜空全景圖影像(色調)，能調整嗎」。真因：續175 換夜空時把 ambient 硬設成飽和深藍綠(0.055,0.08,0.10)＋`ambientMode=Flat`＋`DynamicGI.UpdateEnvironment()`，整個場地(含 Boss)都被那個藍綠 ambient ＋重烘的暗天反射染色。

修法 — `BossDomainScreenVFX.cs` 新增 4 個鈕：
- **`domainAmbientColorTint`**(0-1，預設 0.35)：ambient 顏色往 domain 色偏移多少。**0 = 完全不改場景原本的 ambient 色相,只調暗**(Boss 不吃藍綠味)；1 = 完全變 domain 色。
- **`domainAmbientIntensity`**(預設 0.35→**0.5**)：ambient 亮度(1 = 不變暗)。
- **`updateReflectionsFromSky`**(預設 **false**)：false = 不重烘 ambient probe / 預設反射,Boss 的高光/反射維持進場前的樣子,外觀幾乎不變；true = 反射改吃暗夜空。
- **`bossFillLight`**(選填 Light)：domain 期間開、退場關 —— 放一盞打在 Boss 上的燈,把 Boss 重新照亮到想要的樣子,同時場地其他地方維持暗。留空就靠上面三個鈕。

退場全部完整還原(含 `ambientMode`/`ambientSky/Equator/Ground` 色/reflection)。實測:ambient 從 (0.212,0.227,0.259)@1.0 → (0.157,0.176,0.203)@0.5(**同色相、只調暗**),Boss 的白羽毛/紅刃回到接近正常;夜空＋綠邊界仍在。

### 觸發 Boss 戰的過場動畫（2026-09-06，續 180）

使用者不滿意舊的過場(只有 `YuanpeiBoss.IntroRoutine` 2.6s 降落自轉、無運鏡),要求 6 拍的過場。

**做法**:coroutine 驅動(沿用 `DeathDissolve` 模式,不用 Timeline/Cinemachine)。新 `YuanpeiIntroCinematic.cs`,`YuanpeiEncounter.StartEncounter()` 觸發時 `yield return introCinematic.Play(player, combatCenter)`,跑完才 `boss.BeginEncounter(playIntro:false)`(過場已做完降落,跳過 IntroRoutine)。玩家控制/相機控制/CharacterController/boss AI 全部 hand-off,`finally` + failsafe deadline 保證還原。控制腳本用**型別在 runtime 解析**(玩家在持久場景 GreyboxTest,不能跨場景序列化)。

| 拍 | 內容 | 實作 |
|---|---|---|
| ① SkyWipe 2.6s | 晴天→夜空,從地平線往上 | `SkyboxNightPanorama.shader` 加 `_NightRise`(0 晴 / 1 夜,`sweep = lerp(-1.15, 1.15, _NightRise)`,`d.y < sweep` = 夜)+ 內建白天漸層(`_DayZenith/_DayHorizon/_DayGround`)。`BossDomainScreenVFX` 改用 `domainSkybox` 的 runtime 實例 + `SetNightRise()`。過場 `BeginDomain()` → `SetNightRise(0)` → tween 到 1 |
| ② PushToBoss 1.6s | 鏡頭拉近 boss(埋在地下) | 直接寫 `Camera.main` transform,`ThirdPersonCameraController` 停用 |
| ③ BossRise 2.2s | boss 邊轉圈邊升起、變大 | `YuanpeiBoss.DriveRiseAndSpin(startPos, startScale, t01, spin)` —— Y 從 arenaCenter−3 升到 hover 高度,scale 從 skyLogo×0.05 長到 ×0.28,`visualRoot.Rotate` 自轉 |
| ④ PlayerLeap 2.4s | 鏡頭拉遠、玩家跳過去劈砍 | 腳本弧線把玩家從原地移到 boss 前 `leapStandoff`(2.2m),`Speed` 參數拉高播跑步,末端 `CrossFade("Attack3")`(`HasState` 保護) |
| ⑤ Clash 2.6s | 側面 2-shot,劈中瞬間 boss 蓄力頂飛玩家 | 鏡頭切到側面(`Cross(up, flatDir)`)。k<0.42 兩人相互靠近;impact → `HitStopController.Request(0.14, 0.06)` + boss visualRoot 後仰 14° + `CrossFade("Staggered")`;之後玩家沿拋物線往後上方飛 ~10m,boss 前推回 hover 位 |
| ⑥ Settle 1.3s | 鏡頭回到玩家後方 → 正式開戰 | 鏡頭 ease 到 `player - forward*5.5 + up*2.2`;`UnlockActors` 還原一切;`boss.BeginEncounter(playIntro:false)` → State=Hover;HUD 開 |

**新增**:`YuanpeiIntroCinematic.cs`(+ 純 `YuanpeiIntroTimeline` beat 數學)、`YuanpeiIntroCinematicSetup.cs`(選單「Setup Yuanpei Intro Cinematic (續180)」)、`YuanpeiIntroCinematicTests.cs`(9 測試)。
**改**:`YuanpeiBoss.cs`(`BeginEncounter` +`playIntro` 參數、`DriveRiseAndSpin`)、`YuanpeiEncounter.cs`(觸發過場 + `introCinematic` 欄位)、`SkyboxNightPanorama.shader`(+`_NightRise` 白天漸層)、`BossDomainScreenVFX.cs`(runtime skybox 實例 + `SetNightRise`)、`Map_School.unity`(掛 `YuanpeiIntroCinematic`)。

**驗證**(用 `EditorApplication.Step()` 逐幀跑完整段,Editor 失焦也能跑):
- 拍1:`_NightRise` 0→0.98 over 2.6s,截圖確認開頭有藍天、結尾夜空 + 綠色領域邊界
- 拍3:boss Y −2.5 → 6.5、scale 85 → 400、自轉(截圖確認巨大校徽升起)
- 拍4:玩家移到 boss 前 standoff
- 拍5:玩家拋物線飛出 —— (−0.75,1.1,−106.9) → (4.75,1.1,−115.1) ≈ 10m,峰值 Y≈2.2
- 拍6:`cine.IsRunning=False`、`boss.State=Hover`、CharacterMovement/PlayerInput/camController/CC 全部 `enabled=True` 還原
- Console 無錯誤(`HasState` 保護解決了「State could not be found」)
- EditMode **331/331 綠**

**尚需微調**(進真實 Boss 戰 Play-test):各拍時長、鏡頭角度/FOV、白天天空顏色(`_DayZenith` 等)、boss 起始深度/大小、玩家 leap 弧線、頂飛力道。`playerUiRoots` 欄位留空 → 過場中 HUD debug 文字沒隱藏(可自行填)。玩家 leap/劈砍/被頂飛的動作只用了 `Attack3`/`Staggered` + Speed 參數,細緻的躍擊動作待補。

#### 續 181 修訂 v2（2026-09-06）— 上表拍③–⑥ 已被此版取代

使用者修訂:①全景圖渲染太快、鏡頭再拉遠(看到大部分天空慢慢變夜) ②boss **不要跑到玩家背後** —— 在**原本位置(競技場中心)**轉圈升到**真正的高空**,鏡頭斜向拍 boss;玩家跳到 boss **面前**(高空的水平平行線,同 Y);玩家出手前一瞬鏡頭拉近**特寫**(玩家左側面 / boss 右側面);boss **快速蓄力後仰再往前**把玩家頂飛;鏡頭拉遠拍玩家**被擊飛落地**。

- **時長**:`YuanpeiIntroTimeline.Default` = `SkyWipe 5.0 / PushToBoss 1.8 / BossRise 2.6 / PlayerLeap 2.6 / Clash 3.0 / Settle 1.6`(共 16.6s)。拍1 鏡頭 `skyCamBack=34 / skyCamHeight=15 / skyCamAimHeight=16`,`SetNightRise` 走完整 5s。
- **拍3**:`DriveRiseAndSpin(center, startPos, startScale, bossAirAltitude=13, t01, spin)` —— boss 埋在 `arenaCenter + down*bossStartDepthBelowArena(3)`,升到 `floor + 13 ≈ Y13.5`,**從不橫移**(一直在 arenaCenter 的 X/Z)。**`DriveRiseAndSpin` 已移除 `config.maxWorldY` 夾制**(cinematic-only:過場中 boss `enabled=false`,`ClampWorldY` 不會打架;`SettleToHoverPose`(仍夾制)開戰前把 boss 放回 hover 高度)。這是關鍵修正——舊版 boss 被 `maxWorldY=8` 夾住升不到「高空」。
- **拍4**:`flatDir` = 水平 玩家→boss、`side = Cross(up, flatDir)`。`leapEnd = bossPos - flatDir * airStandoff(3)`,**Y 與 boss 相同**(高空水平線)。`SetFloat("Speed", 2f)` 播跑姿(不再 `CrossFade` 巢狀 state,會噴 "State could not be found")。鏡頭斜向 `mid + side*10 + (-flatDir)*3 + up*3`。
- **拍5**:`closeCam = clashMid + side*(airStandoff+1.6) + up*0.3` → 玩家 screen-L / boss screen-R。k<0.22 定格特寫;0.22–0.5 boss `+ flatDir * bossChargeBack(2.6)` 後仰 + disc tilt −18°;k≥0.5 `HitStopController.Request(0.14, 0.05)` + `CrossFade("Staggered")` + boss lunge(`Sin` 曲線衝過 home 再回)+ 玩家沿拋物線飛到 `playerHome - flatDir * launchBackDistance`(Y 落回 `groundY`)+ 鏡頭拉遠 `clashMid + side*13 + (-flatDir)*4 + up*5` 追墜落。
- **新欄位**(`YuanpeiIntroCinematic`):`skyCamBack/Height/AimHeight`、`bossStartDepthBelowArena`、`bossStartScaleFraction`、`bossSpinDegPerSec`、`bossAirAltitude`、`airStandoff`、`bossChargeBack`、`launchBackDistance`、`launchArcHeight`、`hitStopSeconds`、`baseFov`、`closeFov`。

**驗證 v2**(`EditorApplication.Step()` 逐幀 + 6 張 game-view 截圖):拍1 mid `_NightRise=0.42` 鏡頭 `(-2,15.5,-72)` / 拍1 end 全夜遠景 + 綠色支配領域邊框 / 拍3 boss 原地自轉升到 Y13.5 scale~400 / 拍4 玩家升到 boss 同高 ~3m 前 / 拍5 特寫 2-shot 玩家左 boss 右 → boss 後仰蓄力 → lunge 頂飛玩家 / 拍6 玩家落地 `(-2,1.1,-92)`、boss `SettleToHoverPose` → `State=Hover`、`IsRunning=False`、控制全還原。`PlayerGuard` 過場後 `enabled=false` 是 `YuanpeiEncounter.ApplyNoDefenceRule` 的既有規則(spec §8.1,非 bug,Victory/Defeat 還原)。EditMode 34/34 綠。

#### 續 182 修訂 v3（2026-09-06）— 拍④–⑥ 動畫真實度:真刀躍擊 + 加速/慢動作 + 落地硬直

使用者:拍④劈砍動畫不真實(用真的普通攻擊、跳過去瞬間畫面加速帶電影感);拍⑤ boss 擊退不真實(只要稍微傾斜→往後→往前一個「頂」,整段慢速電影感);落地後要看到玩家做**硬直動作(架式條滿格的那個)**。

- **拍④ 真刀躍擊**:`SetTrigger("AttackComboSword")`(→ `KBS_Sword_ATK_Combo_01_001_IP`);先 `SetBool("Jump", true)`(→ `NewJump`),k≥0.6 收 Jump + 觸發揮刀。`Time.timeScale` envelope:k<0.55 `1 → leapTimeScale(1.5)` 加速衝刺,之後 `→ clashTimeScale(0.4)` 進慢動作。
- **拍⑤ 慢動作交打**:整段 `Time.timeScale = clashTimeScale(0.4)`(`Clash` 時長 3.0 → **2.4** scaled 秒 ≈ 6s 實時)。boss 動作簡化:k<0.4 微傾 −6° + 後退 0.7m → k0.4–0.6 前傾 +4° + 前「頂」2.2m → k>0.6 ease 回原位(取代舊版大 lunge + −18° tilt + `HitStopController`)。玩家墜落段 `timeScale` ease 回 0.9。
- **落地硬直**:玩家**觸地瞬間**(`grounded && lk≥0.9`)`_stance.AddPostureDamage(MaxStance*2)` → `IsStaggered` → `StaggerAnimationLink` 撐 `KneelingDown` 跪姿。場景 `StancePoise.staggerDurationSeconds=1.2` → 跪 ~1.2s 自動恢復,`UnlockActors` `EndStagger()` 乾淨起身。拍⑤尾鏡頭改掃到落點地面 3/4 shot(`landCam = landSpot + side*5 + (-flatDir)*4.5 + up*2.4`),跪姿填滿畫面。
- **接管清單 / 時鐘**:`LockActors` 多接管 `Time.timeScale` + 玩家 `Animator.speed`(重設 1),`CharacterAnimatorLink` 納入停用清單(否則每幀搶 `Speed`/`animator.speed`)。失效保護鐘 `Time.time` → `Time.unscaledTime`(慢動作下 scaled time 太慢會誤觸;`realtimeSinceStartup` 會被逐幀驗證的實牆鐘誤觸)。
- `YuanpeiBoss.SettleToHoverPose` 加 `visualRoot.localRotation = _skyVisualLocalRot`(清拍③累積自轉 + 拍⑤ tilt)。
- 新欄位:`leapTimeScale(1.5)`、`clashTimeScale(0.4)`、`groundStaggerHoldSeconds(1.0)`。

**驗證 v3**:逐幀 + 4 截圖 —— 拍④ timeScale 1→1.5(`NewJump`→`AttackComboSword`)/ 拍⑤ 特寫 timeScale 0.4 玩家揮刀(左)disc 傾斜(右)/ boss z −104→−107 後退再前頂回 −105 / 玩家拋物線落地 `(-2,1.1,-92)` / **落地 `stagger=True` 跪 ~1.2s** / 拍⑥ `run=False` `timeScale=1` `anim.speed=1` boss `Hover` 控制全還原。EditMode 20/20 綠、Console 無錯。
