# Asset Licenses

每個外部資產（非本專案原創產出）必須在這裡登記。**授權不明或授權不允許商業使用的素材，只能用於內部原型，不能進入正式上市 Build。**

## 佔位素材（禁止進入對外 Build）

| 資產名稱 | 作者 | 來源 | 取得日期 | 授權類型 | 允許商用 | 需要署名 | 允許修改 | 允許重新散布 | 實際使用位置 | 授權證明保存位置 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Live2D 模型 `076`（納茲・多拉格尼爾，*Fairy Tail*） | 真島浩／講談社（原著角色），同人模型作者不明 | `C:\question\live2d_my_like\models\076\` | 沿用 `C:\Live2DFighter` 專案既有引用（本專案於 2026-08-10 決定僅作佔位） | 無授權（同人／角色著作權屬原著方） | **否** | 不適用 | 不適用 | **否** | 僅限開發機本地原型驗證，**不得出現在任何要交付/發布給他人的 Build**。複製一份到 `Assets/_Project/Live2D/PlaceholderCharacter/`（僅 `c_7001.moc3`／`model3.json`／`texture_01.png`／4 個 motion，未動 `C:\question` 原始檔），2026-08-10 起也用於 `GreyboxTest` 場景 Player 的面向鏡頭 2D 立牌視覺驗證 | 無（本來就無授權可證明） |
| Live2D 模型 `077`（露西・海特菲莉亞，*Fairy Tail*） | 真島浩／講談社（原著角色），同人模型作者不明 | `C:\question\live2d_my_like\models\077\` | 同上 | 無授權 | **否** | 不適用 | 不適用 | **否** | 同上 | 無 |
| 機甲角色模型（Player2 靜態看板，高達風設計） | 不明——使用者提供，來源與實際作者未知 | `C:\Users\homec\Downloads\fbx_53e34751-943b-45ee-8202-72ab8b01c4f5\modelToUsed.fbx`（2026-08-10，AI 已檢查發現：無骨架、無貼圖、單一網格約 100 萬三角面、外觀酷似既有機甲動畫作品的機體設計，AI 建議不採用，**使用者已知悉上述風險並明確要求保留使用**） | 不明 | **否**（未經證實授權，不得假設可商用） | 不適用 | 不適用 | **否** | 僅限開發機內部測試用的靜態裝飾看板（無骨架不能做動畫，未接入任何戰鬥/互動邏輯），**不得出現在任何要交付/發布給他人的 Build**，`Assets/_Project/Characters/Placeholder/MechaModel_DoNotShip/` | 無 |

**處理原則**（2026-08-10 使用者確認）：
- 上述三個素材（076／077／機甲模型）只能用來做內部技術驗證，**不得出現在任何要交付/發布給他人的 Build**（含 Alpha 之後任何外部測試版本、Beta、RC、正式發布）。
- 機甲模型是使用者在 AI 提出風險警告（面數過高不可用、外觀疑似既有機甲 IP）後，**明確表示已確認來源並自行承擔風險**才保留的，AI 端無法驗證其真實來源與授權。
- 正式角色設計定案、取得原創或合法授權的素材後，需要在此表新增對應項目並移除佔位項目的依賴，詳見 `KNOWN_ISSUES.md` 的追蹤項。

## 已授權/原創素材

| 資產名稱 | 作者 | 來源 | 授權類型 | 允許商用 | 需要署名 | 實際使用位置 |
| --- | --- | --- | --- | --- | --- | --- |
| Cubism SDK for Unity 5-r.4.2 | Live2D Inc. | `CubismSdkForUnity-5-r.4.2.unitypackage`（官方發布的可重新散布安裝包，非角色專屬素材） | Live2D Open Software License | 是（依官方授權條款，需遵守 Live2D 的使用規範） | 否 | `Assets/Live2D/Cubism/`（SDK 執行環境，本身不含角色美術） |
| Universal Base Characters（Standard，Superhero Male/Female FullBody + 貼圖） | Quaternius | https://quaternius.itch.io/universal-base-characters （2026-08-10 下載，`Universal Base Characters[Standard].zip`，122 MB，內附 `License_Standard.txt`） | **CC0 1.0 Universal**（公有領域宣告） | 是，正式上市 Build 也可以用 | 否 | `Assets/_Project/Characters/Placeholder/UniversalBaseCharacters/`。2026-08-10 起被下面的 Maya 取代成 Player 主要視覺，**保留在專案內作為備用角色**，未來也可能用在別的敵人/NPC上 |
| 【Anime Character】Maya (Free/Unity 3D) | 3D動漫風角色屋 / 3D Anime Character Store（Sketchfab @alex94i60） | https://sketchfab.com/3d-models/anime-charactermaya-freeunity-3d-44691835bd56472f9f890d380b836b28 （2026-08-10 下載，需 Sketchfab 帳號登入才能下載，使用者本人登入完成，`.fbx` 原始格式 29MB，含 Animator/動畫/材質的完整 Unity 套件） | **CC Attribution (CC-BY 4.0)** | 是，明確標示「Commercial use allowed」，但「Forbid secondary sales」（禁止轉售原始檔案本身） | **是**——署名文字：「[Anime Character] Maya (Free/Unity 3D) by 3D動漫風角色屋 / 3D Anime Character Store is licensed under Creative Commons Attribution」，並附上 Sketchfab（https://sketchfab.com ）與 VRoidHub 連結，需放進 `BUILD_RELEASE_GUIDE.md` 規劃的「第三方授權與署名頁面」 | `Assets/_Project/Characters/Placeholder/MayaAnime/`（FBX + 貼圖 + 材質 + 附帶的 Idle/Walk/Run/Jump/Fall 動畫與 Animator Controller），2026-08-10 起取代 Player 的視覺（Humanoid Rig，材質已從 Standard shader 轉成 URP Lit） |

灰盒原型的地板／掩體方塊／訓練假人維持使用 Unity 內建基本形狀（Capsule／Cube）與 URP 預設材質，不需要外部授權。

**署名待辦**：只要正式 Build 有包含 Maya 這個角色，就必須在遊戲內某處（設定/製作名單/授權頁）顯示上表的署名文字，這是 CC-BY 的強制要求，不是選配——`Docs/BUILD_RELEASE_GUIDE.md` 的正式 Build 檢查清單需要加這一項。
