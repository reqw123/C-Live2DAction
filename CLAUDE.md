# Live2DAction — Project Rules

這份規則只適用於 `C:\Live2DAction\` 這個獨立 repo。它跟 `C:\Live2DFighter\`（既有 2D 格鬥專案）與
`C:\question\`（Live2D 素材來源 repo）都沒有程式碼依賴關係，三者互不影響。

> **給 AI agent / 新機器**：先讀 `Docs/AGENT_NOTES.md`（環境重建 + 踩過的坑），再讀本檔。
> 專案當前狀態、角色對照、已知問題見 `Docs/KNOWN_ISSUES.md` 與 `Docs/CHANGELOG.md`。

## Goal

製作一款原創單機遊戲：**Live2D 劇情演出 ＋ 3D 動作戰鬥**，另含一層開放世界移動探索（參考原神／鳴潮的飛行與滑翔）。
可參考第三人稱動漫動作遊戲的移動、攝影機、連段、技能、閃避、鎖定、卡通渲染、打擊感，但角色、名稱、
世界觀、服裝、武器、場景、UI、音樂音效、商標等一律原創，不得複製任何既有商業作品的受著作權保護素材。

第一個目標是可從主選單玩到結算的**垂直切片**，不是完整商業版本；垂直切片穩定前不擴充範圍
（見 `Docs/DEVELOPMENT_ROADMAP.md`）。

## Tech

- Unity **6000.0.81f1**（精確版本見 `Live2DAction/ProjectSettings/ProjectVersion.txt`）＋ **URP 17.0.4**
- 套件清單見 `Live2DAction/Packages/manifest.json`（Input System、Cinemachine、AI Navigation、
  gltfast、Unity Toon Shader、Test Framework 等）＋內嵌套件 `com.unity.springbone`
- Unity MCP（`com.coplaydev.unity-mcp` v10.1.2）用於 AI 驅動 Editor；client 設定見 repo 根的 `.mcp.json`
- Live2D Cubism SDK for Unity 5-r.4.2（已匯入 `Assets/Live2D/`，僅用於劇情/選單/UI 演出，**不用於戰鬥判定**）
- ScriptableObject 驅動的攻擊/技能/Boss 調校資料（`Assets/_Project/Settings/Combat/`）
- 執行階段程式碼組件：`Live2DAction.Runtime`（`Assets/_Project/Game/Live2DAction.Runtime.asmdef`）
- Git 版本控制；美術資產直接進版控（無 Git LFS）

## Non-negotiable rules

1. **角色/劇情/技能/UI/世界觀必須原創**，不得複製其他商業作品（含但不限於角色外觀、服裝、武器造型、
   場景、圖示、音樂、音效、UI、商標）。
2. **Live2D / 佔位素材授權界線**：076（納茲）、077（露西）為《Fairy Tail》同人模型；
   `MechaModel_DoNotShip` 機甲來源不明。這類標記 `DoNotShip` 的素材**僅可作內部原型佔位**，
   **絕對不得出現在任何要交給他人或發布的 Build**（Alpha 之後所有版本）。追蹤表見 `Docs/ASSET_LICENSES.md`。
3. 不修改 `C:\question\` 底下任何原始檔案（`.moc3`、`.model3.json`、既有 `textures/`、`motions/`）；本專案只唯讀引用。
4. Live2D 視覺模型不得用於戰鬥判定；戰鬥用 hitbox/hurtbox/pushbox 一律是獨立 collider 或幾何資料，
   由 ScriptableObject 驅動。
5. 垂直切片完成前不加入：完整開放世界、多人連線、抽卡、複數可操作角色、手機/主機平台、
   複雜裝備系統、大量支線任務。
6. 每個戰鬥/演出功能需要 EditMode/PlayMode 測試，或可在固定驗證場景（`GreyboxTest.unity`）重現。
7. 平衡數值（傷害、frame data、冷卻、能量消耗…）一律放在 ScriptableObject，不得寫死在腳本裡。
8. 玩家與 AI 輸入共用同一個輸入介面（`IInputCommand` 模式）。
9. 每次只交付一個可測試功能。大改動前先摘要受影響檔案與風險，取得確認再做。
10. Steamworks／付款／商店上架／隱私政策／法律聲明相關事項，一律停下來要求使用者確認，不自動執行。
11. **帳號/登入操作一律禁止代勞**（Sketchfab、Steam、Unity 授權啟用等），請使用者本人完成。

## 手動調校值是權威 — 不要「修正」回程式碼預設值

`ThirdPersonCameraController` 的 `distance`／`targetOffset`，以及 `CharacterController.stepOffset`／
`minMoveDistance` 等，是使用者反覆 Play-test 手調出來的，**不是預設值疏漏**。程式碼註解／欄位預設值
可能已過時。若場景序列化值與註解不符，**先問使用者，不要假設程式碼才對**。細節見 `Docs/AGENT_NOTES.md`。

## Definition of Done（單一功能）

- Unity 專案編譯無錯誤，Console 無持續刷新的錯誤。
- 相關自動化測試通過（實際跑過 `-runTests`，不是「理論上可行」）。
- 可在 `GreyboxTest.unity` 重現。
- 沒有動到 `C:\Live2DFighter\` 或 `C:\question\` 底下的檔案。
- `Docs/DEVELOPMENT_ROADMAP.md`／`Docs/KNOWN_ISSUES.md`／`Docs/CHANGELOG.md` 已更新。

## 文件索引

| 文件 | 內容 |
| --- | --- |
| `Docs/AGENT_NOTES.md` | 新機器環境重建步驟 ＋ 踩過的坑（batchmode 鎖檔、Play Mode 失焦凍結…） |
| `CONTEXT.md` | 飛行系統術語表（Flight / Glide / Aerial Combat…） |
| `Docs/PROJECT_AUDIT.md` | Phase 0 稽核結果 |
| `Docs/GAME_DESIGN_DOCUMENT.md` | 玩法、角色能力、關卡流程、上市範圍 |
| `Docs/TECHNICAL_DESIGN.md` | 系統架構、狀態機、資料流 |
| `Docs/FLIGHT_SYSTEM_DESIGN.md` / `Docs/FLOATING_ISLAND_GUIDE.md` | 飛行系統 / 空島關卡 |
| `Docs/CAT_COMBAT_DESIGN.md` | 貓咪近戰機制設計（連段/蓄力/撲擊/空中攻擊/命中反饋/敵貓，切片 2） |
| `Docs/COMBAT_SYSTEM_SNAPSHOT.md` | 玩家 vs 武士戰鬥系統現況攤平（物件/元件值、傷害管線、架勢、隻狼彈反、Boss FSM、每招數據表、已知限制）— 給 AI 分析用 |
| `Docs/WUSHI_COMBAT_ENGINEERING_SPEC.md` | 外部 AI 依 SNAPSHOT 產出的 9 項戰鬥系統改造工程規格（DeflectReaction/Tap Guard/旋轉 Sweep/玩家 Sweep/Boss 空間/格擋架勢/處決生命節點/特殊招式排程/最終調校）— 分階段實作藍圖，進度看 CHANGELOG |
| `Docs/ASSET_LICENSES.md` | 每個外部資產的授權追蹤（含 DoNotShip 清單） |
| `Docs/LARGE_ASSETS.md` | 超過 GitHub 100 MB 上限、排除版控的大型檔案清單與補回方法 |
| `Docs/DEVELOPMENT_ROADMAP.md` | Phase/Milestone 劃分與驗收標準 |
| `Docs/TEST_PLAN.md` | 測試範圍與方法 |
| `Docs/BUILD_RELEASE_GUIDE.md` | Build 與發布流程、發布前檢查清單 |
| `Docs/KNOWN_ISSUES.md` | 已知問題、阻塞項、角色命名對照表、操作警語 |
| `Docs/CHANGELOG.md` | 變更紀錄 |
