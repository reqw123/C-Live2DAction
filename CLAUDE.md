# Live2DAction — Project Rules

這份規則只適用於 `C:\Live2DAction\` 這個獨立 repo。它跟 `C:\Live2DFighter\`（既有 2D 格鬥專案）與 `C:\question\`（Live2D 素材來源 repo）都沒有程式碼依賴關係，三者互不影響。

## Goal

製作一款原創單機遊戲：**Live2D 劇情演出＋固定場景 3D 動作戰鬥**。可參考第三人稱動漫動作遊戲的移動、攝影機、連段、技能、閃避、鎖定、卡通渲染、打擊感，但角色、名稱、世界觀、服裝、武器、場景、UI、音樂音效、商標等一律原創，不得複製任何既有商業作品的受著作權保護素材。

第一個目標是可從主選單玩到結算的**垂直切片**，不是完整商業版本；垂直切片穩定前不擴充範圍（詳見 `Docs/DEVELOPMENT_ROADMAP.md` 的階段與範圍限制）。

## Tech

- Unity **6000.0.81f1**（與 `C:\Live2DFighter` 相同 LTS 版本，但改用 **URP**，不是 BiRP）
- Live2D Cubism SDK for Unity（僅用於劇情/選單/UI 演出，不用於戰鬥判定）
- Cinemachine（第三人稱攝影機）
- ScriptableObject 驅動的攻擊/技能資料
- Git 版本控制

## Non-negotiable rules

1. **角色/劇情/技能/UI/世界觀必須原創**，不得複製其他商業作品（含但不限於角色外觀、服裝、武器造型、場景、圖示、音樂、音效、UI、商標）。
2. **Live2D 素材授權界線**：`C:\question\live2d_my_like\models\076\`（納茲）與 `\077\`（露西）是《Fairy Tail》同人模型，**僅可作內部原型佔位**（驗證對話系統、演出流程、選角介面技術可行性），**不得出現在任何要交給他人或發布的 Build**（包含 Alpha 之後任何要給他人測試的版本、Beta、RC、正式發布）。正式角色必須是原創 Live2D 或 2D/3D 素材，見 `Docs/ASSET_LICENSES.md` 的佔位追蹤表。
3. 不修改 `C:\question\` 底下任何原始檔案（`.moc3`、`.model3.json`、既有 `textures/`、`motions/`）；本專案只唯讀引用。
4. Live2D 視覺模型不得用於戰鬥判定；戰鬥用 hitbox/hurtbox/pushbox 一律是獨立 collider 或幾何資料，由 `MoveData`/`SkillData` 驅動。
5. 不在垂直切片（`Docs/DEVELOPMENT_ROADMAP.md` 的 M-slice）完成前加入：開放世界、多人連線、抽卡、複數可操作角色、手機/主機平台、複雜裝備系統、大量支線任務。
6. 每個戰鬥/演出功能需要 EditMode/PlayMode 測試或可在固定驗證場景重現。
7. 平衡數值（傷害、frame data、冷卻、能量消耗…）一律放在 ScriptableObject（`AttackData`/`SkillData`），不得寫死在腳本裡。
8. 玩家與 AI 輸入共用同一個輸入介面（比照 `C:\Live2DFighter` 的 `IInputCommand` 模式）。
9. 每次只交付一個可測試功能。不得用一句話啟動大範圍實作；大改動前先摘要受影響檔案與風險，取得確認再做。
10. Steamworks／付款／商店上架／隱私政策／法律聲明相關事項，一律停下來要求使用者確認，不自動執行。

## Definition of Done（單一功能）

- Unity 專案編譯無錯誤。
- 相關自動化測試通過。
- 可在固定驗證場景重現。
- 沒有動到 `C:\Live2DFighter\` 或 `C:\question\` 底下的檔案。
- `Docs/DEVELOPMENT_ROADMAP.md`／`Docs/KNOWN_ISSUES.md`／`Docs/CHANGELOG.md` 已更新。

## 目前環境

- Unity 6000.0.81f1 與 Unity 6000.5.7f1 皆已安裝在此機器（Unity Hub）。本專案固定用 6000.0.81f1 + URP。
- Cubism SDK 尚未匯入本專案（截至 Phase 0），匯入前需先確認版本與 Unity 6 相容性。
- 專案尚未建立 Unity 專案檔（`Assets/`／`ProjectSettings/`／`Packages/`）；建立時機見 `Docs/DEVELOPMENT_ROADMAP.md` Phase 1。
