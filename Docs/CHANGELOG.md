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

## 2026-08-10 — Phase 1 追加：Live2D 立牌視覺

- 匯入 Cubism SDK for Unity 5-r.4.2；複製 076 模型到 `Assets/_Project/Live2D/PlaceholderCharacter/`（僅供內部原型驗證，見 `ASSET_LICENSES.md`）。
- 新增 `Assets/_Project/Rendering/Shaders/CubismUnlitURP.shader`（URP 版 Cubism Unlit，SDK 內建版只支援 Built-in RP）、`CubismBillboard.cs`（永遠面向鏡頭）。
- `PlayerCombat`/`CharacterMovement` 之外新增 `Tools/Live2DAction/Replace Player Visual With Live2D Standee` 編輯器工具，把 Player 的 Capsule 視覺換成 076 立牌。
- 修正兩個問題：(1) 複製進本專案的 model3.json 有一個指向不存在檔案的 Physics 參照導致匯入報錯，已從這份複製的 json 移除該欄位；(2) 縮放公式一開始誤把 `CanvasHeight`（像素）當 Unity 單位，角色縮到看不見，改用 `CanvasHeight / PixelsPerUnit` 換算後角色恢復正常高度。
- 用命令列把 Camera.main 算圖進 RenderTexture 存 PNG 的方式做了目視驗證（`ScreenCapture.CaptureScreenshot` 在 batchmode 沒有 backbuffer 存不出東西），確認角色貼圖、比例、朝向都正確顯示。
- 已知限制：不支援 Mask 裁切；目前顯示的是 moc3 靜止綁定姿勢（帶著技能火焰特效，非真正待機動作）；Cubism `ToModel()` 產生的物件在 Play 模式下名字會變空字串（原因未查出，不影響功能）。詳見 `KNOWN_ISSUES.md`。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**，這次只有自動化截圖驗證。
