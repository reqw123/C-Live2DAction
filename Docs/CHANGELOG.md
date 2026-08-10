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

## 2026-08-10 — Phase 1 追加：Humanoid 角色佔位

- 研究並比較線上可用的 3D Humanoid 模型來源（Mixamo／CC0 低多邊形套件／AI 生成模型），依使用者決定採用 CC0 套件。
- 下載 Quaternius「Universal Base Characters」Standard 版（CC0 1.0，122 MB，https://quaternius.itch.io/universal-base-characters ），已確認內附授權文字，登記進 `Docs/ASSET_LICENSES.md`。
- 複製 `Superhero_Male_FullBody.fbx` 與貼圖到 `Assets/_Project/Characters/Placeholder/UniversalBaseCharacters/`。
- 新增 `PlayerHumanoidVisualSetup.cs`（Tools/Live2DAction/Replace Player Visual With Humanoid Placeholder）：設定 Humanoid Rig、建立 URP Lit 材質、取代 Player 底下先前的 Live2D 立牌（Cubism SDK／shader／billboard 元件仍保留供劇情演出使用）。
- 用命令列算圖確認貼圖、比例、站姿正確顯示，無粉紅材質。目前無 Animator/動畫，Play 起來會是 T-pose。
- 已知限制：無動畫；Roughness 貼圖未套用；只接了 Male、無髮型。詳見 `KNOWN_ISSUES.md`。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**。

## 2026-08-10 — Phase 1 追加：Maya 動漫風角色佔位

- 使用者要求動漫風角色，把上面的 Quaternius Humanoid 角色降級為備用，改用 Sketchfab「3D動漫風角色屋」的 Maya（CC-BY 4.0，須署名，見 `Docs/ASSET_LICENSES.md`）。
- Sketchfab 下載需登入帳號，AI 不能代為登入，使用者本人完成登入後下載（`.fbx` 原始格式，29MB，內含完整 Unity 套件：Humanoid Rig 已設定好的 FBX、Animator Controller（Idle/Walk/Run/Jump/Fall 動畫）、13 個材質、Prefab）。
- 複製進 `Assets/_Project/Characters/Placeholder/MayaAnime/` 時保留原始 `.meta` 檔，讓 Prefab／Animator 內部 GUID 參照直接對上。
- 新增 `PlayerMayaVisualSetup.cs`（Tools/Live2DAction/Replace Player Visual With Maya (Anime)）：把 13 個 Standard shader 材質轉成 URP Lit（`_MainTex`→`_BaseMap`、`_Color`→`_BaseColor`），關閉 Animator 的 Apply Root Motion（避免跟 CharacterController 位移邏輯衝突），取代 Player 底下先前的 Humanoid 佔位。
- 用命令列算圖確認貼圖、比例、Idle 待機動作（非 T-pose）正確顯示，無粉紅材質。
- 已知限制：目前只有內衣、無服裝；Animator 移動參數未接線，走路/跑步動畫還播不出來；發布 Build 前必須加上 CC-BY 署名。詳見 `KNOWN_ISSUES.md`。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**。

## 2026-08-10 — 評估兩個來源不明的測試模型

- 使用者提供兩個 UUID 命名的 FBX 檔案（`fbx_9f3e955d-...`、`fbx_53e34751-...`），想當「2P 角色看板」用。AI 檢查後發現兩者匯出簽章完全一致（同版本 Blender、無骨架、無貼圖、精確地都是 100 萬三角面），判斷是同一產線輸出。
- 第一個（倒臥姿勢＋草帽，疑似既有版權角色公仔）：判定不採用，已從專案移除，未提交進 git。
- 第二個（高達風機甲，疑似既有機甲動畫作品設計）：使用者在收到同樣的風險警告後，明確表示已確認來源並自行承擔風險、要求保留。已依 076/077 的模式處理——複製進 `Assets/_Project/Characters/Placeholder/MechaModel_DoNotShip/`，登記進 `ASSET_LICENSES.md`「禁止進入對外 Build」表，列為新的高風險阻塞項（見 `KNOWN_ISSUES.md`）。只能當靜態看板（`Player2`，無骨架不能做動畫），套用預設 URP Lit 白色材質，用命令列算圖確認位置/比例/材質正常。
