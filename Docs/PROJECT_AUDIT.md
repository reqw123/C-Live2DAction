# Project Audit — Phase 0

日期：2026-08-10

## 專案現況

`C:\Live2DAction` 是全新建立的獨立 repo，尚未包含 Unity 專案檔（無 `Assets/`／`Packages/`／`ProjectSettings/`）。目前只有 `CLAUDE.md`、`README.md`、`Docs/` 底下的規劃文件，以及一個空的 git repo（`git init` 完成，尚無 commit）。

這是有意的決定：使用者確認此專案與既有的 `C:\Live2DFighter`（2D 格鬥）完全獨立、不共用程式碼或資產，因此沒有既有 Unity 專案可稽核，Phase 0 的重點是環境確認與風險盤點，而非既有程式碼盤點。

## 環境確認

| 項目 | 結果 |
| --- | --- |
| Unity 版本 | 已安裝 `6000.0.81f1`、`6000.5.7f1`（Unity Hub，`C:\Program Files\Unity\Hub\Editor\`）。本專案選定 **6000.0.81f1 + URP** |
| Render Pipeline | 決定使用 **URP**（`C:\Live2DFighter` 用的是 BiRP，本專案不共用該選擇，因為卡通渲染／Shader Graph 對 URP 支援較好） |
| Git | 已安裝（2.54.0），本 repo 已 `git init`，位於 `C:\Live2DAction`，目前分支 `main`，無 commit |
| Cubism SDK | 尚未匯入本專案。需另外確認 Cubism 5 SDK for Unity 與 Unity 6000.0.81f1 的相容版本（`C:\Live2DFighter` 已驗證過 Cubism 5 可在同一 Unity 版本正常運作，可參考但不共用安裝） |
| Cinemachine | 尚未安裝，Phase 1 需要透過 Package Manager 加入 |
| Unity MCP / Editor 自動化工具 | 尚未確認是否配置，Phase 1 開始建立 Unity 專案後需要另外檢查 |

## 可重用內容

- **架構經驗**（非程式碼共用，是設計參考）：`C:\Live2DFighter` 已驗證的作法值得沿用到新專案——ScriptableObject 驅動的招式資料（`MoveData`）、玩家/AI 共用輸入介面（`IInputCommand`）、`CombatManager`/`MatchManager` 分離戰鬥迴圈與比賽流程、EditMode 測試覆蓋判定框邏輯。這些是設計模式參考，**不會**直接複製檔案過來（兩專案是 2D vs 3D、Assembly 結構不同，直接搬程式碼意義不大）。
- **Live2D 角色資源（僅限內部原型佔位，見下方風險）**：`C:\question\live2d_my_like\models\076\`／`\077\` 已有 idle 待機動作與 3 個技能動作可用，可暫時拿來驗證對話系統／演出流程的技術可行性。

## 必須從零開始的部分

- Unity 專案本體、URP 設定、資料夾結構（Phase 1 才建立）。
- 第三人稱角色控制器、Cinemachine 攝影機設定（新專案是 3D，`Live2DFighter` 是 2D，完全不能沿用）。
- 3D 戰鬥判定（hitbox/hurtbox 改為 3D 範圍檢測，而非 2D AABB）。
- NavMesh 敵人 AI。
- 原創角色美術／3D 人形模型／原創世界觀與劇情文字。

## 風險

### 🔴 高風險：Live2D 角色素材著作權

`C:\question\live2d_my_like\models\076\`（納茲・多拉格尼爾）與 `\077\`（露西・海特菲莉亞）是《Fairy Tail》（尖端出版／講談社／真島浩）授權角色的同人 Live2D 模型（見該資料夾 `076-納茲.md`／`077-露西.md` 文件內明確記載的角色出處）。

- 這**不是**「授權不明」，而是**明確屬於他人、且未取得授權**的角色素材。
- 使用者已確認處理方式：**僅作內部原型佔位**，用來驗證對話系統與演出流程的技術可行性；**不得出現在任何要交付/發布給他人的 Build**（含 Alpha 之後任何外部測試版本、Beta、RC、正式發布）。
- 已記錄進 `Docs/ASSET_LICENSES.md` 的佔位追蹤表，正式角色設計定案前，任何 Live2D 演出畫面都必須視為「暫時、不可外流」狀態。
- `C:\Live2DFighter` 專案本身也使用同一組模型，但該專案性質是本機、不對外發布的格鬥原型，風險輪廓不同，不在本次稽核範圍內调整。

### 🟡 中風險：3D 人形角色素材

第一版垂直切片需要至少一個 Humanoid 3D 角色模型才能開始戰鬥系統開發。目前沒有任何 3D 角色素材，需另外取得授權清楚（且允許商業使用）的臨時 Humanoid 角色，或委託製作。這是 Phase 2（戰鬥垂直切片）開始前的阻塞項，但不阻塞 Phase 1（灰盒原型可以只用 Capsule）。

### 🟡 中風險：Unity 版本與 Cubism SDK 相容性未驗證

`6000.0.81f1` 在 `C:\Live2DFighter` 已驗證可與 Cubism 5 SDK 正常運作，但本專案是全新安裝，第一次匯入 Cubism SDK 到本專案時仍需要重新確認一次（不能假設沿用 `Live2DFighter` 的驗證結果就一定沒問題，兩個是完全獨立的 Unity 專案安裝）。

### 🟢 低風險：既有專案互相干擾

`C:\Live2DFighter` 與 `C:\question` 都是獨立 repo，本專案只會用唯讀方式讀取 `C:\question` 的模型檔，不會寫入或移動；三個 repo 的 git 歷史與工作樹互不影響。

## 素材缺口

- 原創世界觀、角色設定、劇情文字：完全空白，需要另外設計（不阻塞 Phase 1 灰盒原型）。
- 原創 3D 主角/敵人/Boss 模型：完全空白（阻塞 Phase 2 起）。
- 原創 UI 美術、圖示、音樂音效：完全空白（阻塞 Phase 3 起）。

## 授權缺口

- 076/077 Live2D 模型：授權缺口為「明確不可商用」，非缺口而是硬限制，處理方式見上方風險段落與 `ASSET_LICENSES.md`。
- 尚無任何已取得授權的 3D 角色/音樂/音效資產，需要在使用前逐一記錄進 `ASSET_LICENSES.md`。

## 技術債

目前無技術債（專案為全新建立）。

## 建議的新專案架構

沿用使用者企劃書第七節建議的資料夾骨架（`Game/Core`／`Input`／`Characters`／`Combat`／`AI`／`Camera`／`Skills`／`UI`／`Dialogue`／`Save`／`Audio`／`VFX`／`SceneManagement`／`Tests`），在 Phase 1 建立 Unity 專案時一併建立基礎骨架，但**只建立 Phase 1 實際會用到的資料夾**，避免出現一堆空殼資料夾。

## 第一階段預計修改的檔案

Phase 1 只涉及新建檔案（全新專案），預計新增：
- Unity 專案本體（`Assets/`／`ProjectSettings/`／`Packages/`）
- `Assets/_Project/Scenes/GreyboxTest.unity`
- `Assets/_Project/Scripts/Characters/`、`Camera/`、`Combat/` 底下的第三人稱控制器、Cinemachine 設定、單一攻擊與傷害判定的最小腳本
- 對應的 EditMode/PlayMode 測試

## 是否適合在原專案內建立 3D 模組

不適合——使用者已確認採用**全新獨立專案**方案，`C:\Live2DFighter` 維持 2D 格鬥不變。

## 是否應建立獨立分支或獨立 Unity 專案

已採用**獨立 Unity 專案＋獨立 git repo**（`C:\Live2DAction`），而非在既有 repo 內開分支。
