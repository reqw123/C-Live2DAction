# yuanpei_LogoSky 空中 Boss — 實作現況

> 依 `yuanpei_LogoSky_Boss_工程說明文件.md`（v1.0）實作。本檔記錄「做了什麼 / 沒做什麼 / 檔案在哪」。
> 進度看 `Docs/CHANGELOG.md`。⚠️ 校徽是元培真實商標 → 整套 DoNotShip（見 `ASSET_LICENSES.md` / `KNOWN_ISSUES.md §7`）。

## 一句話

空中遠距法術型 Boss：升空保持射程、8 種招式逼玩家走位（追加94 續 119：MultiAoE 多重光爆已移除、含 3 種肉身衝撞變體）、玩家攻擊削 HP＋累積架勢 → 架勢滿 → 墜地 →
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
