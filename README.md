# Live2DAction

原創單機遊戲：**Live2D 劇情演出 ＋ 3D 動作戰鬥**，另含一層開放世界移動探索（飛行/滑翔，參考原神・鳴潮）。
第三人稱動作玩法（移動、連段、技能、閃避、鎖定、Boss 戰）；角色、劇情、世界觀全部原創。

獨立 git repo，跟 `C:\Live2DFighter`（既有 2D 格鬥專案）與 `C:\question`（Live2D 素材來源）沒有程式碼依賴，
僅唯讀借用 `C:\question` 的 Live2D 模型作內部原型佔位（見下方限制）。

## 環境

- **Unity 6000.0.81f1**（精確版本見 `Live2DAction/ProjectSettings/ProjectVersion.txt`）＋ URP
- Unity 專案本體在 `Live2DAction/` 子目錄
- 乾淨機器 clone 後的完整重建步驟見 **`Docs/AGENT_NOTES.md` 第 1 節**

## 快速開始

1. Unity Hub 安裝 `6000.0.81f1` ＋ Windows Build Support，完成授權啟用
2. Unity Hub 開啟 `C:\Live2DAction\Live2DAction\`（首次會還原套件、重建 `Library/`）
3. 開場景 `Assets/_Project/Scenes/GreyboxTest.unity`，按 Play
4. （AI 驅動）Editor 裡開 `Window > MCP For Unity`，確認 `http://127.0.0.1:8080/mcp`；
   `.mcp.json` 已在 repo 根

## 重要限制

- `076`（納茲）／`077`（露西）是《Fairy Tail》同人 Live2D 模型；`MechaModel_DoNotShip` 機甲來源不明。
  標記 `DoNotShip` 的素材**僅供內部原型驗證，不得進入任何要發布或交給他人的 Build**。
  正式角色待原創素材到位後替換。詳見 `Docs/ASSET_LICENSES.md`。
- 開發規則與不可違反原則見 **`CLAUDE.md`**。

## 文件索引

| 文件 | 內容 |
| --- | --- |
| `CLAUDE.md` | 專案規則、技術棧、不可違反原則、DoD |
| `Docs/AGENT_NOTES.md` | 新機器環境重建 ＋ 踩過的坑（AI agent 接手前必讀） |
| `CONTEXT.md` | 飛行系統術語表 |
| `Docs/PROJECT_AUDIT.md` | Phase 0 稽核結果 |
| `Docs/GAME_DESIGN_DOCUMENT.md` | 玩法、角色能力、關卡流程、上市範圍 |
| `Docs/TECHNICAL_DESIGN.md` | 系統架構、狀態機、資料流 |
| `Docs/FLIGHT_SYSTEM_DESIGN.md` / `Docs/FLOATING_ISLAND_GUIDE.md` | 飛行系統 / 空島關卡 |
| `Docs/ASSET_LICENSES.md` | 外部資產授權追蹤（含 DoNotShip 清單） |
| `Docs/DEVELOPMENT_ROADMAP.md` | Phase/Milestone 與驗收標準 |
| `Docs/TEST_PLAN.md` | 測試範圍與方法 |
| `Docs/BUILD_RELEASE_GUIDE.md` | Build 與發布流程、發布前檢查清單 |
| `Docs/KNOWN_ISSUES.md` | 已知問題、阻塞項、角色命名對照表、操作警語 |
| `Docs/CHANGELOG.md` | 變更紀錄 |
