# Known Issues

## 阻塞項

1. **076/077 Live2D 素材著作權**（高風險，Phase 3 前必須解決）：目前唯一可用的 Live2D 角色模型是《Fairy Tail》同人素材，僅能作內部原型佔位，不得進入任何對外 Build。Alpha 開始前需要原創或合法授權的 Live2D／2D 角色素材，否則 Live2D 劇情演出功能無法進入 Alpha。詳見 `ASSET_LICENSES.md`。
2. **缺少 3D 人形角色模型**（中風險，Phase 2 前必須解決）：目前沒有任何 3D 角色素材，需要取得授權清楚且允許商業使用的臨時 Humanoid 角色，或委託製作，才能開始戰鬥系統實作（灰盒原型可用 Capsule 代替，不受此阻塞）。
3. **Cubism SDK 尚未匯入驗證**（中風險，Phase 3 前）：本專案是全新 Unity 6000.0.81f1 安裝，Cubism 5 SDK 相容性需要重新驗證一次，不能假設沿用 `C:\Live2DFighter` 的驗證結果。

4. **灰盒原型手感／攝影機尚未人眼驗證**（中風險，Phase 2 開始前應確認）：Phase 1 的移動、攻擊、Cinemachine 第三人稱攝影機都只透過 `-batchmode -runTests` 自動化測試驗證過邏輯正確性，**沒有人在互動式 Unity Editor 裡實際按過 Play**。是否好操作、攝影機軌道與滑鼠視角是否順暢、掩體方塊視覺是否合理，都需要使用者親自打開 `GreyboxTest` 場景 Play 一次才能確認。

## 待確認

- 本機沒有配置 Unity MCP 或其他可互動的 Editor 自動化工具，本次 Phase 1 全程透過 Unity 命令列 `-batchmode`／`-executeMethod`／`-runTests` 完成，AI 端無法產生「已手動 Play 驗證」的證據，這類驗證一律需要使用者自行操作。
- 手把輸入是否列入垂直切片範圍，尚未決定（`C:\Live2DFighter` 的經驗是手把部分尚未完成測試）。

## 已解決

- ~~Unity MCP 或其他 Editor 自動化工具是否要在本專案配置~~ → 已確認本機無此類工具，Phase 1 全程用命令列批次模式完成（2026-08-10）。
