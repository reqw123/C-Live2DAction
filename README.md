# Live2DAction

原創單機遊戲：Live2D 劇情演出＋固定場景 3D 動作戰鬥。第三人稱動作遊戲玩法（移動、連段、技能、閃避、鎖定），角色/劇情/世界觀全部原創。

獨立 git repo，跟 `C:\Live2DFighter`（既有 2D 格鬥專案）與 `C:\question`（Live2D 素材來源 repo）沒有程式碼依賴，僅在唯讀方向暫時借用 `C:\question` 的 Live2D 模型作內部原型佔位（見下方「重要限制」）。

## 目前狀態：Phase 0（稽核與專案初始化）

Unity 專案本體（`Assets/`／`ProjectSettings/`／`Packages/`）尚未建立，見 `Docs/DEVELOPMENT_ROADMAP.md` 的 Phase 1 計畫。

## 重要限制

- `076`（納茲）／`077`（露西）是《Fairy Tail》同人 Live2D 模型，**僅供內部原型驗證使用，不得進入任何要發布或交給他人的 Build**。正式角色待原創素材到位後替換。詳見 `Docs/ASSET_LICENSES.md`。
- 開發規則見本資料夾 `CLAUDE.md`。

## 文件索引

| 文件 | 內容 |
| --- | --- |
| `CLAUDE.md` | 專案規則、技術棧、不可違反原則 |
| `Docs/PROJECT_AUDIT.md` | Phase 0 稽核結果（環境、素材、風險） |
| `Docs/GAME_DESIGN_DOCUMENT.md` | 玩法、角色能力、關卡流程、上市範圍 |
| `Docs/TECHNICAL_DESIGN.md` | 系統架構、狀態機、資料流 |
| `Docs/ASSET_LICENSES.md` | 每個外部資產的授權追蹤 |
| `Docs/DEVELOPMENT_ROADMAP.md` | Phase/Milestone 劃分與驗收標準 |
| `Docs/TEST_PLAN.md` | 測試範圍與方法 |
| `Docs/BUILD_RELEASE_GUIDE.md` | Build 與發布流程 |
| `Docs/KNOWN_ISSUES.md` | 已知問題與阻塞項 |
| `Docs/CHANGELOG.md` | 變更紀錄 |
