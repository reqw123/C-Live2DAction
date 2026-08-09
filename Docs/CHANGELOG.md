# Changelog

## 2026-08-10 — Phase 0

- 建立全新獨立專案 `C:\Live2DAction`（git repo 初始化，`main` 分支）。
- 建立 `CLAUDE.md`、`README.md` 與 `Docs/` 全套文件骨架：`PROJECT_AUDIT.md`、`DEVELOPMENT_ROADMAP.md`、`GAME_DESIGN_DOCUMENT.md`、`TECHNICAL_DESIGN.md`、`ASSET_LICENSES.md`、`KNOWN_ISSUES.md`。
- 確認環境：Unity 6000.0.81f1（本專案採用）與 6000.5.7f1 皆已安裝；本專案選用 URP（`C:\Live2DFighter` 用 BiRP，兩者無關）。
- 發現並記錄阻塞項：`C:\question\live2d_my_like\models\076\`／`\077\` 為《Fairy Tail》同人 Live2D 模型，取得使用者確認僅作內部原型佔位，不得進入對外 Build。
- 尚未建立 Unity 專案本體（`Assets/`／`ProjectSettings/`／`Packages/`），排入 Phase 1。

## 2026-08-10 — Phase 1

- 建立 Unity 專案 `Live2DAction/`（6000.0.81f1 + URP），安裝 Input System 1.19.0、Cinemachine 3.1.2、AI Navigation 2.0.5、Test Framework 1.6.0。
- 新增 `Live2DAction.Runtime` 組件：`Core/`（`IDamageable`、`DamageInfo`、`Health`）、`Input/`（`IInputCommand`、`PlayerInputProvider`）、`Characters/`（`CharacterMovement`）、`Combat/`（`AttackData`、`AttackResolver`、`PlayerCombat`）。
- 新增 `GreyboxTest.unity` 場景（地板、掩體方塊、Player、TrainingDummy、第三人稱 Cinemachine 攝影機），已加入 Build Settings；由可重複執行的 `Assets/Editor/Bootstrap/GreyboxSceneBuilder.cs` 產生。
- 新增 8 個 EditMode 測試 + 2 個 PlayMode 測試，全數透過 `-batchmode -runTests` 驗證通過。
- 修正過程中的兩個真實 bug：(1) `Health.CurrentHealth` 原本依賴 `Awake()` 初始化，在 EditMode 測試裡 `Awake` 不會自動執行，改為比照 `C:\Live2DFighter` 既有的 lazy-init 模式；(2) `PlayerCombat`／`CharacterMovement` 原本在 `Awake()` 快取 `IInputCommand` 轉型，導致之後才指定的 `inputSource` 不生效，改為每次使用時即時轉型。
- **尚未驗證**：移動手感、攝影機視角、視覺呈現皆未經人眼在互動式 Editor 中 Play 確認，僅有自動化測試覆蓋（見 `KNOWN_ISSUES.md`）。
