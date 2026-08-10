# Asset Licenses

每個外部資產（非本專案原創產出）必須在這裡登記。**授權不明或授權不允許商業使用的素材，只能用於內部原型，不能進入正式上市 Build。**

## 佔位素材（禁止進入對外 Build）

| 資產名稱 | 作者 | 來源 | 取得日期 | 授權類型 | 允許商用 | 需要署名 | 允許修改 | 允許重新散布 | 實際使用位置 | 授權證明保存位置 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Live2D 模型 `076`（納茲・多拉格尼爾，*Fairy Tail*） | 真島浩／講談社（原著角色），同人模型作者不明 | `C:\question\live2d_my_like\models\076\` | 沿用 `C:\Live2DFighter` 專案既有引用（本專案於 2026-08-10 決定僅作佔位） | 無授權（同人／角色著作權屬原著方） | **否** | 不適用 | 不適用 | **否** | 僅限開發機本地原型驗證，**不得出現在任何要交付/發布給他人的 Build**。複製一份到 `Assets/_Project/Live2D/PlaceholderCharacter/`（僅 `c_7001.moc3`／`model3.json`／`texture_01.png`／4 個 motion，未動 `C:\question` 原始檔），2026-08-10 起也用於 `GreyboxTest` 場景 Player 的面向鏡頭 2D 立牌視覺驗證 | 無（本來就無授權可證明） |
| Live2D 模型 `077`（露西・海特菲莉亞，*Fairy Tail*） | 真島浩／講談社（原著角色），同人模型作者不明 | `C:\question\live2d_my_like\models\077\` | 同上 | 無授權 | **否** | 不適用 | 不適用 | **否** | 同上 | 無 |

**處理原則**（2026-08-10 使用者確認）：
- 這兩個模型只能用來驗證對話系統、Live2D 演出流程（表情切換、Motion 觸發、切場景）等**技術可行性**。
- 任何要交給他人測試、任何 Alpha 之後的外部分享版本、Beta、RC、正式發布，**一律不得包含這兩個模型**。
- 正式角色設計定案、取得原創或合法授權的 Live2D／2D 素材後，需要在此表新增對應項目並移除佔位項目的依賴，詳見 `KNOWN_ISSUES.md` 的追蹤項。

## 已授權/原創素材

| 資產名稱 | 作者 | 來源 | 授權類型 | 允許商用 | 實際使用位置 |
| --- | --- | --- | --- | --- | --- |
| Cubism SDK for Unity 5-r.4.2 | Live2D Inc. | `CubismSdkForUnity-5-r.4.2.unitypackage`（官方發布的可重新散布安裝包，非角色專屬素材） | Live2D Open Software License | 是（依官方授權條款，需遵守 Live2D 的使用規範） | `Assets/Live2D/Cubism/`（SDK 執行環境，本身不含角色美術） |
| Universal Base Characters（Standard，Superhero Male/Female FullBody + 貼圖） | Quaternius | https://quaternius.itch.io/universal-base-characters （2026-08-10 下載，`Universal Base Characters[Standard].zip`，122 MB，內附 `License_Standard.txt`） | **CC0 1.0 Universal**（公有領域宣告） | **是**，免署名、可修改、可再散布，正式上市 Build 也可以用 | `Assets/_Project/Characters/Placeholder/UniversalBaseCharacters/`（Humanoid FBX + 貼圖），作為 Phase 2 前的臨時可操作角色視覺，標記為 Placeholder，待正式原創角色美術到位後替換 | `Assets/_Project/Characters/Placeholder/UniversalBaseCharacters/License_Standard.txt`（隨資產一起放在專案內） |

灰盒原型的地板／掩體方塊／訓練假人維持使用 Unity 內建基本形狀（Capsule／Cube）與 URP 預設材質，不需要外部授權。
