# Known Issues

## 📛 角色命名對照表（2026-08-19 角色重新命名）

使用者要求「重新對專案當前所有角色命名，以便後續識別」，場景 GameObject 與對應的 Editor/測試腳本檔名都已改名，對照如下：

| 舊名（`Player2`/`Player3`/`Player4`） | 新名 | 備註 |
|---|---|---|
| `Player` | `Player`（不變） | 玩家操控主角（Maya） |
| `Player2` | `Mecha` | 來源不明機甲靜態看板（`MechaModel_DoNotShip`，見下方「阻塞項 1b」） |
| `Player3` | `TrainingDummy` | 站樁訓練假人（Maya 視覺、無 AI 無輸入）——**注意**：這是一個全新的名字重用，跟本文件下面很多條目提到的「舊 TrainingDummy」（2026-08-12 之前，Player4 的前身、後來被使用者刪除的白柱子敵人）完全是兩個不同的角色，只是剛好用了同一個字串。 |
| `Player4` | `Enemy` | AI 敵人（Arisa，含完整戰鬥/空戰/處刑 AI） |
| `076`／`077` | 不變 | Live2D 立牌，未改名 |

### 怪物級別（2026-08-29，使用者定義）

| 級別 | 角色 | 元件 | 城牆限制 |
|---|---|---|---|
| **普通怪** | `Enemy`（Arisa／076 佔位） | `EnemyAI` | 不受限，可追出本地門口 |
| **菁英怪** | `屁孩王` | `BossStateMachine`（`confineToArena=true`） | 被關在本地，玩家出門口 → `GateWatch` 門口觀望 |
| **boss** | `武士` | `BossStateMachine`（`confineToArena=false`） | 不受限，只吃 14m 距離 leash（追加45，原 30） |

屁孩王與武士**共用 `BossStateMachine` 腳本**，行為差異全靠逐-instance 序列化欄位。三者 AI 互相獨立、不干擾，但傷害機制（HP／架勢／擊飛）保留。舊條目/CHANGELOG 把屁孩王寫成「精怪」的，以「菁英怪」為準。

**本文件下面所有沒有標註 2026-08-19 或更晚日期的條目，一律是改名前寫的歷史記錄，故意保留原始的 `Player2`/`Player3`/`Player4`/`TrainingDummy` 稱呼，不回頭竄改**——這些是當時的真實對話/bug 記錄，改字只會讓歷史脈絡對不上。讀到舊條目時，請對照上面這張表換算成現在場景裡實際的名字。詳細改動內容（改了哪些檔案、抓到的命名衝突風險）見 `CHANGELOG.md` 同日條目。

## ⚠️ 操作警語：`GreyboxSceneBuilder.Build()` 會清空整個場景重建

**2026-08-12 事故**：AI 為了改地板材質直接呼叫 `Build()`，沒意識到它會先 `NewScene(EmptyScene)` 清空場景，只重建它自己寫的內容——把當天疊加、尚未 commit 的 Maya／Enemy 人形／FemaleStandee／076-077 立牌／Player2 機甲全部清掉了，靠重新照順序執行一整串工具腳本才復原（詳見 `CHANGELOG.md` 同日「修正 AI 誤刪場景角色的事故」條目），且無法保證找回事故前任何手動在 Editor 裡調過、沒有走腳本的數值。

**之後任何人（或 AI）要改動 `GreyboxTest.unity` 時**：
- 只需要局部修改既有場景（換材質、加物件、調參數）→ 用 `EditorSceneManager.OpenScene` 開啟現有場景直接改（照 `BackgroundSceneryStandeeSetup.cs`／各種 `Fix*.cs` 的模式），**絕對不要呼叫 `GreyboxSceneBuilder.Build()`**。
- 真的需要從零重建整個場景時，呼叫 `Build()` 之後，必須照 `CHANGELOG.md` 記錄的完整順序重跑後續所有視覺/立牌工具（Maya → Animator Link → Attack Pose Visualizer → Enemy 人形 → Enemy 落地修正 → Female Standee → 076-077 立牌 → 立牌改名 → Player2 機甲 → 背景景物），跑之前**先問使用者**，不要自己判斷「應該沒差」就動手。
- 場景是不受版本控制友善的二進位 YAML，改動前最好先 `git status` 確認目前工作目錄有沒有尚未 commit 的內容——如果有，代表工作目錄是唯一副本，任何清空重建的操作都不可逆。

**2026-08-12 追加事故**：跑 `-batchmode` 指令改場景檔時，使用者自己的互動 Editor 其實同時開著同一個專案，命令列**第一次呼叫還是回報結束碼 0（看起來成功）**，直到下一次呼叫才報「另一個 Unity 執行個體已經開著」。導致 `CoverBlock2` 消失、`Player` Y 座標被重置（已修復，見 `CHANGELOG.md` 同日條目）。**規則**：任何要用命令列 `-batchmode` 開/存這個場景檔之前，一定要先用工作管理員（`tasklist | grep -i unity.exe`）確認使用者自己的 Unity Editor 真的沒在跑，不能只看上一次指令的結束碼就假設安全——結束碼 0 不保證沒有跟另一個開著的 Editor 實例互相衝突寫壞資料。如果懷疑衝突過，動手前先完整比對場景內容（物件清單、關鍵座標）確認沒有意外遺失/跑位，不要只看最後一次要改的那個欄位。

**2026-08-12 再追加：`-batchmode` 卡死時，強制關閉後記得清殘留鎖檔**：`taskkill //F //IM Unity.exe` 強制關閉一個卡住的 batchmode 執行個體後，`Temp/UnityLockfile`／`Library/ArtifactDB-lock`／`Library/SourceAssetDB-lock` 不會正常釋放，導致**下一次啟動也會卡住**（卡在 Editor 自己的啟動流程，`Loaded scene 'Temp/__Backupscenes/0.backup'` 之後、`TrimDiskCacheJob`／`Scanning for USB devices` 附近，不是專案程式碼的問題）。強制關閉後，動手重跑之前先確認並刪除這三個鎖檔（`find "Temp" -iname "*lock*"`／`find "Library" -iname "*lock*" -maxdepth 1`），否則同一個症狀會一直重複發生，看起來像「一直卡死」但其實是鎖檔沒清乾淨。

## 阻塞項

1. **076/077 Live2D 素材著作權**（高風險，Phase 3 前必須解決）：目前唯一可用的 Live2D 角色模型是《Fairy Tail》同人素材，僅能作內部原型佔位，不得進入任何對外 Build。Alpha 開始前需要原創或合法授權的 Live2D／2D 角色素材，否則 Live2D 劇情演出功能無法進入 Alpha。詳見 `ASSET_LICENSES.md`。
1b. **Player2（GameObject 名 `Mecha`）機甲模型來源不明**（高風險）：`MechaModel_DoNotShip/MechaCharacter2.fbx` 來源與授權都無法驗證，外觀疑似既有機甲動畫作品設計。**2026-08-31：使用者要求把 `Mecha` 從場景移除**（實機看到的是「只有血量條、沒有人物的幽靈」——那顆網格 material 為 NULL、bounds 退化，Play 下完全不顯示，只剩浮空血條）。已刪 `GreyboxTest` 的 `Mecha` GameObject ＋ `GameManager` 上對應的 `RespawnController`（9→8）。**磁碟上 `MechaModel_DoNotShip/` 資料夾、`MechaVisualSetup.cs`／`MechaDamageableSetup.cs`／`MechaRespawnSetup.cs` 選單工具都還在**——重跑那些選單會把它加回來。要永久清掉再刪那些檔（DoNotShip 素材，刪掉對正式 Build 只有好處）。`GreyboxSceneBuilder.Build()` 不會重建它（只在註解裡提到）。
2. ~~缺少 3D 人形角色模型~~ → 已解決（2026-08-10），見下方「Humanoid 角色佔位」項。
3. **灰盒原型手感尚未完整人眼驗證**（中風險，Phase 2 開始前應確認）：使用者已實際 Play 過一次並回報一個真實 bug（見下方「方向鍵畫圈」項，已修好），證明人眼驗證確實會抓到自動化測試漏掉的問題。移動方向已修正，但攻擊手感、攝影機滑鼠視角順暢度、掩體方塊視覺配置是否合理，仍需要使用者再實際 Play 確認。
4. **`DoNotShipBuildGuard` 擋 build 清單有缺口**（2026-09-04 漏洞掃描發現，低～中風險）：
   - guard 的 `BlockedAssetFolders` 只有 6 條，`ASSET_LICENSES.md` 標 DoNotShip 的 `Environment/Meshy/YuanpeiLogo`（真實商標）與授權待確認的 `Characters/Weapons/BloodKatana` 不在其中。**2026-09-04 使用者回覆這兩項已有授權，暫不加入。** 日後若授權狀態改變需補上。
   - `MechaModel_DoNotShip` 那條路徑已失效（資產移除），guard 那條變成無作用；`MechaVisualSetup.cs` 選單仍會從該路徑加回。
   - guard 用 `AssetDatabase.IsValidFolder`，路徑打錯會**靜默放行**，且沒有 EditMode 測試驗證每條路徑存在。
   - guard 只擋非 Development build（設計如此，但 Development build 仍會夾帶）。
5. **`MechanicalWings.fbx` 未登記授權**（2026-09-04 發現，低風險）：`Assets/_Project/Characters/Placeholder/MechanicalWings/_FBX/MechanicalWings.fbx`（玩家翅膀，用於 `GreyboxTest`）不在 `ASSET_LICENSES.md` 任何一張表裡，來源不明。發布前需確認來源／授權或換成原創／CC0 素材。

## Live2D 立牌視覺（2026-08-10 新增，已用自動化截圖驗證，未經人眼互動確認）

- Cubism SDK for Unity 5-r.4.2 已匯入並確認可在 URP 下運作，但 SDK 內建的 `Live2D Cubism/Unlit`／`Mask` shader 是寫給 Built-in RP 的 CGPROGRAM，在 URP 下不會被渲染管線挑到（缺 `LightMode` tag），改用自寫的 `Assets/_Project/Rendering/Shaders/CubismUnlitURP.shader`（僅還原不透明度/色彩混合，**沒有實作 Mask 裁切**，含裁切的模型會顯示異常，076 這次沒觀察到明顯裁切問題）。
- `CubismModel3Json.ToModel()` 回傳的 model 根物件，`gameObject.name` 會不穩定變回空字串，原因始終未查出（SDK 原始碼裡沒找到任何清空名字的程式碼，`grep` 過整個 `Assets/Live2D/Cubism/` 沒有任何地方寫 `.name =`）。一開始只在 Play 模式下觀察到（見下方 2026-08-10 記錄），2026-08-12 發現**連編輯模式手動改名/存檔後也會再變回空字串**——`Live2DStandeeSetup.cs` 建立時明明有設定好名字（`076_DoNotShip`／`077_DoNotShip`）並存檔，過一段時間再打開卻又是空的；用修正腳本重新命名、存檔後，同一個修正腳本再跑一次還是回報「從空字串改名」，代表真的會反覆發生，不是單次意外。**這只影響用名字 `Find()` 這個物件、以及 Hierarchy 顯示的標籤**，不影響渲染或邏輯；`PlayerCubismVisualSetup.cs` 已改成用「摧毀 Player 底下所有子物件」而非按名字找繞開；`FixLive2DStandeeNames.cs`（Tools/Live2DAction/[Fix] Rename Live2D Standees To 076-077）改用出生座標比對定位這兩個立牌，不依賴名字，如果 Hierarchy 裡的名字又變空白，直接重跑這支腳本即可，不需要回報。**2026-08-12 稍後追加**：這一輪重新觀察到，幾乎**每一次**用 `EditorSceneManager.OpenScene` 開啟這個場景（不管是哪支工具開的，不限於跟 Live2D 直接相關的工具）存檔後再重新載入，這兩個名字都會回到空字串——不是偶發，實務上要當成「每次改完這個場景都要順手重跑一次改名工具」來處理，不用每次都當新問題回報。
- 縮放公式踩過一次真的 bug：`CubismCanvasInformation.CanvasHeight` 是「像素」單位，要除以 `PixelsPerUnit` 才是模型在 Unity 裡的實際單位大小（這個模型剛好兩者都是 1200，換算後模型原始大小約 1 單位高）；一開始誤把 `CanvasHeight` 當成已經是 Unity 單位直接拿來算縮放比例，導致角色縮小成肉眼幾乎看不到的小點。已修正為 `TargetHeight / (CanvasHeight / PixelsPerUnit)`，實測角色現在跟訓練假人差不多高。
- 目前只顯示 moc3 的靜止綁定姿勢（沒有播放任何 motion），076 的綁定姿勢剛好帶著出招用的火焰特效（見 `076-納茲.md` 的 Parameter 對照表），所以角色看起來像是一直在使用技能，不是真的在待機——這是預期中的暫時狀態，等 Phase 2 接上 idle motion 播放後才會正常待機。
- 立牌用 `CubismBillboard`（`Assets/_Project/Game/Characters/CubismBillboard.cs`）永遠面向 `Camera.main`，只轉 Y 軸；朝向公式假設模型預設面向本地 -Z（未經人眼確認是否方向正確，若實際 Play 後發現角色背對鏡頭，切換該元件 Inspector 上的 `Face Away Instead` 勾選框即可，不需要改程式碼）。

## Humanoid 角色佔位（2026-08-10 新增，取代 Player 身上的 Live2D 立牌）

- 使用者要求接上真正的 3D Humanoid 角色後，Player 的視覺改為 Quaternius「Universal Base Characters」（CC0，見 `ASSET_LICENSES.md`）的 `Superhero_Male_FullBody.fbx`，取代先前的 Live2D 立牌。Cubism SDK／自寫 URP shader／`CubismBillboard` 都還留在專案裡（劇情演出功能仍會用到 Live2D，只是 Player 戰鬥視覺換掉了），076 的 Live2D 模型目前沒有被任何場景物件引用。
- FBX 已設成 Humanoid Rig（`ModelImporter.animationType = Human`），材質手動建立（URP Lit + BaseColor + Normal 貼圖，**沒有處理 Roughness 貼圖**，用預設光滑度，非最終品質）。
- 目前**沒有掛任何 Animator Controller／動畫**，Play 起來角色會是 T-pose 靜止不動（bind pose）；同作者的「Universal Animation Library」（也是 CC0、用同一套 Humanoid 骨架設計）可以之後接上 Idle/Run/Attack 等動作，尚未下載。
- 只複製了 Male 版本＋沒有髮型（`Hairstyles/Rigged to Head Bone/FBX (Unity)/` 裡有對應頭骨的髮型 FBX，需要的話之後再加）。
- 用命令列算圖確認角色貼圖、比例、站姿都正確顯示，沒有粉紅材質。
- **2026-08-10 當天稍後被下方的 Maya（動漫風角色）取代成 Player 的主要視覺**，這組 Quaternius 素材保留在專案內作為備用/未來敵人角色使用，未刪除。

## Maya 動漫風角色佔位（2026-08-10 新增，取代上面的 Humanoid 佔位，目前 Player 使用中）

- 使用者要求動漫風角色，在 Sketchfab 找到「3D動漫風角色屋」的「Maya」（CC-BY 4.0，見 `ASSET_LICENSES.md`，**發布 Build 前必須加上署名**）。下載需要 Sketchfab 帳號登入，使用者本人完成登入後才能下載，AI 端無法代為登入（帳號/登入操作一律禁止代勞）。
- 這個素材包本身就是完整的 Unity 套件（FBX＋Humanoid Rig 已由原作者設定好、Animator Controller、Idle/Walk/Run/Jump/Fall 動畫、13 個材質、Prefab），複製時連同原始 `.meta` 檔一起複製，讓 Prefab／Animator 內部的參照 GUID 能對上，不用重新手動連結。
- 材質原本用 Unity Built-in RP 的 Standard shader（在 URP 下會顯示粉紅色），寫了 `ConvertMaterialsToUrp()`（在 `PlayerMayaVisualSetup.cs` 裡）批次把 13 個材質的 `_MainTex`／`_Color` 讀出來後，shader 換成 URP Lit、重新指定到 `_BaseMap`／`_BaseColor`——這次材質只用到主貼圖＋色調，沒有用到法線貼圖，所以不需要處理法線貼圖的轉換。
- Prefab 自帶的 Animator 預設 `Apply Root Motion = true`，會跟我們自己的 `CharacterController` 移動邏輯打架（動畫本身位移 + 程式碼位移疊加），已在腳本裡關掉。Animator 的 Speed/H/V/Jump/Fly/Aim/Grounded 參數目前完全沒有接線，全部維持預設值（`Grounded` 預設是 true，其餘是 0/false），角色會穩定停在 Idle 動畫，**沒有走路/跑步動畫的接線**，等 Phase 2 要做真正的移動時才需要把 `CharacterMovement` 的速度餵給這些參數。
- 這個角色目前**沒有穿衣服**（只有內衣的裸體版本），素材頁面上有另一個「Anime Girl Casual Outfit」是不同作者的獨立模型，沒有一起下載；要換裝或穿衣服是之後才需要處理的事。
- 用命令列算圖確認：角色貼圖、比例、Idle 待機動作（不是 T-pose）都正確顯示，沒有粉紅材質。**仍是自動化截圖驗證，不是使用者本人在互動 Editor 裡看到的結果**。

## 已拒絕的素材：來源不明的 2P 測試模型（2026-08-10）

- 使用者提供一個檔案（`C:\Users\homec\Downloads\fbx_9f3e955d-0c7b-4b77-8887-7ce85726100e\modelToUsed.fbx`）想試著當「2P 角色看板」用，檢查後**拒絕採用**，已從專案移除（`Player2` 場景物件與複製進來的 FBX 都刪掉了，使用者 Downloads 裡的原始檔案沒有動）。
- 拒絕原因：(1) 無骨架、無材質貼圖，只能當靜態裝飾；(2) **單一網格就有近 100 萬三角面**，遠超即時遊戲角色的合理預算（一般 1~5 萬面），效能上不可用；(3) 算圖出來後外觀是倒臥姿勢＋草帽，視覺上很像既有版權角色的公仔/雕像，來源與授權完全無法確認，風險比 076/077（至少知道是同人 Live2D）更不明確，直接牴觸 `CLAUDE.md` 角色必須原創的規則。
- 如果之後想再嘗試類似「無主 3D 模型」的素材，务必先確認來源與授權，並檢查面數是否在合理範圍，才進到匯入這一步。

## Player2 機甲看板：使用者自行承擔風險保留（2026-08-10）

- 同一天使用者又提供另一個檔案（`fbx_53e34751-943b-45ee-8202-72ab8b01c4f5/modelToUsed.fbx`），跟上面那個模型是**同一產線輸出**（完全相同的 Blender 4.2.3 匯出簽章、同樣沒有骨架/貼圖、同樣精確地是 100 萬三角面）。
- 算圖後外觀是高達風機甲戰士（翅膀狀背部裝甲＋大型劍狀武器），AI 提出跟上次一樣的警告（面數不可用＋外觀疑似既有機甲動畫作品設計），**這次使用者明確表示已確認來源並自行承擔風險，要求保留使用**。
- 已依照 076/077 的模式處理：複製進 `Assets/_Project/Characters/Placeholder/MechaModel_DoNotShip/`（資料夾名稱刻意標註 DoNotShip），登記進 `ASSET_LICENSES.md` 的「禁止進入對外 Build」表，並列為新的高風險阻塞項（見上方阻塞項 1b）。
- 因為沒有骨架，只能當**靜態裝飾看板**（`Player2` GameObject，位置 (2.5, 0, -2)），套用預設 URP Lit 材質（無貼圖，純白色），沒有任何動畫或戰鬥邏輯，也沒有作為可操作的「2P 玩家」。
- 用命令列算圖確認擺放位置、比例、材質都正常顯示（無粉紅材質），跟 Player／TrainingDummy／掩體方塊共存於同一場景不互相干擾。

## 已修正：方向鍵移動會 360 度畫圈（2026-08-10，使用者實際 Play 回報）

使用者實際在 Editor 裡 Play 後回報：純按左/右移動時，角色會像畫圓一樣持續轉圈，不是直線移動。這是這次專案第一個由「使用者實際操作」抓到、自動化測試完全沒發現的 bug（`CharacterMovementTests` 用的是固定朝向的假攝影機，測不出真正 Cinemachine 攝影機才會出現的問題），值得記錄完整的排查過程供之後參考：

- **錯誤診斷 1**：一開始懷疑是 `CinemachineOrbitalFollow.TrackerSettings.BindingMode` 沒設對（懷疑攝影機軌道的參考座標系跟著角色朝向轉）。改成 `BindingMode.WorldSpace`（原始碼裡明確寫著這個模式應該讓 `GetReferenceOrientation()` 恆等於 `Quaternion.identity`，完全跟角色朝向無關）——**實測完全沒有效果**，問題原封不動。
- **錯誤診斷 2**：懷疑即使 `BindingMode` 正確，Cinemachine 套件內部某處仍然讀了角色的旋轉，於是加了一個 `CameraFollowAnchor`（只跟隨角色位置、永遠不跟隨角色旋轉的中介物件），把攝影機的 Follow/LookAt 都改指向這個 anchor 而非角色本身——**實測依然完全沒有效果**，兩次「合理的」修法都沒用，逼著往下深挖。
- **真正原因**：用一個會即時印出角色朝向／攝影機朝向／攝影機位置的診斷測試直接量測，才發現——攝影機軌道角度（`HorizontalAxis.Value`，只受滑鼠控制）其實從頭到尾都穩定停在 0，沒有被複製角色朝向；問題其實出在 `CinemachineRotationComposer`（負責讓攝影機「看向」角色的 Aim 元件）。角色純橫向移動（strafe）時，就算完全沒有旋轉，攝影機的瞄準角度也會因為「追蹤一個持續橫向平移的目標」而自然掃動——這是正常、預期中的攝影機行為。而 `CharacterMovement` 原本是直接讀攝影機**組合後、包含瞄準修正**的 `Transform.forward` 來算「相對攝影機的移動方向」，這就形成了迴圈：移動方向決定角色朝向 → 角色平移 → 攝影機瞄準角度跟著平移量掃動 → 下一幀「相對攝影機」的定義又變了 → 角色又要跟著再轉一點 → 无限循环，按住方向鍵就會持續畫圈。
- **正確修法**：不要用攝影機「组合後」的 `Transform.forward`，改用攝影機軌道**未經瞄準修正**的原始角度（`CinemachineOrbitalFollow.HorizontalAxis.Value`，只受滑鼠輸入影響，跟角色平移/旋轉完全無關）。新增 `ICameraYawSource`／`OrbitalCameraYawSource`（`Assets/_Project/Game/Camera/`），`CharacterMovement` 改成優先讀這個穩定的 yaw 值（找不到就退回讀 `Camera.main` 的 yaw 分量，供沒有 Cinemachine 的測試環境使用）。已移除前兩次無效的嘗試（`CameraFollowAnchor`、`BindingMode` 調整）避免留下誤導性的死路徑。
- **新增永久回歸測試**：`CameraRelativeMovementRegressionTests.cs`（PlayMode），直接載入真正的 `GreyboxTest` 場景（含真的 Cinemachine 攝影機，不是假的固定攝影機），模擬持續按住純橫移輸入，驗證角色朝向會收斂穩定、不會持續漂移——這是專門防止這個 bug 未來悄悄回歸用的。
- **順帶修好一個測試隔離問題**：新增這個回歸測試後，發現它跟既有的 `CharacterMovementTests` 互相污染——如果回歸測試先跑，它載入的真實場景（含 Ground／TrainingDummy／掩體方塊／真攝影機）沒有被清掉，導致 `CharacterMovementTests` 新建的假角色會跟殘留的場景物件碰撞、`Camera.main` 也可能解析到錯的攝影機。修法：`CharacterMovementTests` 的 `[SetUp]` 改成先清空目前場景裡**所有**根物件，不只是清攝影機標籤，確保每個測試都是從真正乾淨的空場景開始，不假設自己是唯一會建立場景物件的測試。
- 13 個 EditMode + 10 個 PlayMode 測試全數通過，且連續跑 3 次確認沒有間歇性失敗。

## 已修正：走路/跑步腳步滑行（2026-08-10，使用者實際 Play 回報）

使用者在上面「畫圈」bug 修好後再次實際 Play，回報「左右移動時會順移，而非自然行走」——即角色有正確轉向、正確直線移動，但腳步動畫看起來像滑冰/飄移，不是踏實的走路。

- **原因**：`CharacterMovement.moveSpeed` 原本是 5，遠超過 Maya 的 Locomotion Blend Tree 最高速度門檻（2，對應 Run 動畫）。動畫本身用的移動速度（帶動畫演出的視覺位移）跟角色實際位移速度對不上，導致腳步跟地面位移不同步。
- 檢查過 Maya 動畫片段的 RootT 曲線，確認**沒有可用的 Root Motion** 可以拿來反推「正確」速度（例如 `NewRun.anim` 的 `RootT.z` 一個 0.7 秒循環裡只在 0.084～0.140 之間小幅擺動，屬於原地搖晃，不是真正的步幅位移），所以無法用資料驅動的方式算出精確值，只能用「跟 Blend Tree 最高門檻對齊」這個合理推測去調。
- **修法**：`moveSpeed` 從 5 降到 2（`FixMoveSpeedForAnimation.cs`），並簡化 `CharacterAnimatorLink`：原本的「先正規化、再乘上任意倍率」公式改成直接 `Clamp(currentSpeed, 0, maxAnimatorSpeed)` 餵給 Animator 的 Speed 參數，理由同上——沒有 Root Motion 可以拿來做更精確的映射，用更簡單、更少猜測空間的公式更誠實。
- **仍待確認**：這是推理出的合理起點，不是已證明正確的數值，需要使用者實際 Play 後用眼睛確認腳步是否貼地。
- **追加68：原神式切換走/跑**（Left Alt 輕點切換）。`CharacterMovement` 加 `walkSpeed`(0.9)＋`_walkMode`，地面速度 `_walkMode ? walkSpeed : moveSpeed`；起飛強制清 walk。blend tree 只有 `NewWalk`(0–0.8)/`NewRun`(2)，降速度走路動畫自動出來。`IInputCommand.WalkTogglePressed` 用 default interface member（玩家專用、AI/stub 不用改）。純函式 `CharacterMovement.NextWalkMode` + 6 EditMode 綠。相機「慢下來看風景」的拉近/收 FOV = 追加68 續：`ThirdPersonCameraController` 加 `walkDistanceMultiplier`(0.82)/`walkFieldOfViewDelta`(−6)/`walkFramingBlendSpeed`(5)，讀 `IsWalking`、blend 緩動、第三人稱限定、非破壞性。**待實機**：`walkSpeed 0.9` 對 `NewWalk` 會不會腳滑（滑就調高到 clip authored pace，或 v2 反向 sync animator 播放率）；相機拉近幅度（base distance 只有 2，×0.82 偏微妙）/ FOV −6 / 緩動速度都在 Inspector 可調。貓也吃同切換（含相機 polish）。

## 已修正：攝影機視角與角色朝向脫鉤，改用自寫攝影機（2026-08-10，使用者實際 Play 回報）

使用者回報：「我認為是攝影機視角問題，而且左右按鍵控制的方向是顛倒的。按下左鍵人物會往右跑並朝向正西方，有點奇怪，像是使用者視角沒有對其角色的第三人稱」。這是在上面「畫圈」bug 修好之後才浮現的第二層問題，需要完整記錄排查過程，因為五次合理的 Cinemachine 修法全部無效，最終決定放棄 Cinemachine 的軌道/瞄準系統。

- **驗證世界座標邏輯本身沒問題**：用診斷測試直接印出「按住純左移動輸入」時的世界座標位移與角色朝向，確認 `CharacterMovement` 產生的移動方向與朝向彼此一致（按左穩定產生 -X 方向的位移與朝向），問題不在移動計算本身，而在攝影機「畫面上呈現的」方向跟這個世界座標系對不上。
- **五次 Cinemachine 修法，全部實測無效**（依序嘗試，每次都基於看似合理的原始碼推理，但都被實測推翻）：
  1. 調整 `TrackerSettings.BindingMode` 為 `WorldSpace`（原始碼寫明這個模式下 `GetReferenceOrientation()` 應恆為 `Quaternion.identity`）。
  2. 加入一個只跟隨位置、不跟隨旋轉的 `CameraFollowAnchor` 中介物件，把 Follow/LookAt 都指到這個 anchor。
  3. 移除 `CinemachineRotationComposer`（Aim 元件），改用 `CinemachineOrbitalFollow`（Body）直接驅動旋轉。
  4. 把 `PositionDamping`／`RotationDamping`／`QuaternionDamping` 全部歸零，排除阻尼延遲造成的視覺誤差。
  5. 重做一次 anchor（`FixCameraAnchorRetry.cs`），這次在每個 `LateUpdate` 強制把 anchor 的旋轉鎖回 `(0,0,0)` 並直接印出驗證，確認 anchor 本身的旋轉全程真的是 identity——但攝影機的 `Transform.right`／`Transform.forward` 依然精確跟著角色的朝向漂移。
- 第五次的診斷數據直接跟 Cinemachine 套件自己的原始碼文件矛盾（`BindingMode.WorldSpace` 底下 `GetReferenceOrientation()` 就是回傳 `Quaternion.identity`，不應該被角色朝向影響），但編譯後的實際行為就是不一致，沒有再花時間深挖套件內部黑盒，改為架構層面的解法。
- **最終解法**：完全移除 Cinemachine 在這個攝影機上的使用（`CinemachineBrain`／`CinemachineCamera`／`CinemachineOrbitalFollow`／`CinemachineRotationComposer`／`CinemachineInputAxisController` 全部拿掉，`Unity.Cinemachine` 也從 `Live2DAction.Runtime.asmdef` 移除），改寫一個完全自己掌控的 `ThirdPersonCameraController`（`Assets/_Project/Game/Camera/ThirdPersonCameraController.cs`）：
  - 直接讀滑鼠 delta（`Mouse.current.delta`）累加 yaw/pitch，兩個都是這個腳本自己擁有的一般欄位，沒有任何其他系統會反過來影響它們。
  - 每個 `LateUpdate` 用 yaw/pitch 算出旋轉，用「目標點 − 旋轉 × 前方 × 距離」算出攝影機位置，直接 `SetPositionAndRotation`，沒有阻尼、沒有中間狀態。
  - 實作 `ICameraYawSource`（沿用原本 `OrbitalCameraYawSource` 的介面設計），`CharacterMovement` 讀的就是這個腳本自己的 `_yaw` 欄位，跟攝影機畫面呈現的旋轉是同一個數字來源，兩者不可能再對不上。
  - `GreyboxSceneBuilder.cs`（場景重建工具）與 `FixCameraCustomController.cs`（既有場景的一次性修正腳本）都已改用新的攝影機設定方式。
- **已知限制**：新攝影機刻意沒有做牆壁/障礙物碰撞閃避（deferred，非本次需求範圍）。
- 13 個 EditMode + 10 個 PlayMode 測試全數重新跑過，全數通過（含載入真實場景的 `CameraRelativeMovementRegressionTests`）。
- **仍待確認**：滑鼠視角操作是否順手（靈敏度、pitch 範圍）需要使用者在互動 Editor 裡實際試玩確認，AI 端只能確認場景結構正確、測試通過。

## 已修正：套用上面的攝影機修法後角色「消失」、方向鍵「沒反應」（2026-08-10，使用者實際 Play 回報）

使用者套用上一版 `FixCameraCustomController.cs` 後實際 Play，回報「角色消失了，按方向鍵也沒反應」。排查後發現這是上一個攝影機修法本身帶出的新 bug，不是移動或攝影機邏輯又倒退：

- **原因**：Maya 這個 Sketchfab 素材包裡，藏著一個素材作者自己預覽用的內嵌攝影機——一個名字與 tag 都是 `MainCamera` 的 GameObject，掛在角色脖子的骨頭上（`Assets/_Project/Characters/Placeholder/MayaAnime/Prefabs/Maya.prefab` 內部，帶著自己的 `Camera` 元件）。上一版 `FixCameraCustomController.cs` 用 `GameObject.FindWithTag("MainCamera")` 找攝影機，實際抓到的是**這個藏在角色骨架裡的假攝影機**，不是場景真正的 Main Camera。
  - 真正的 Main Camera 完全沒被處理到，舊的 `CinemachineBrain` 原封不動留著（且已經沒有可用的 Cinemachine 虛擬攝影機可以 blend 過去），畫面因此卡住不動——這是使用者觀察到「角色消失」的原因。
  - 新寫的 `ThirdPersonCameraController` 則被誤裝到角色脖子上那顆內嵌攝影機，每幀直接用軌道公式覆寫它的位置/旋轉，跟角色骨架動畫互相打架；`CharacterMovement.cameraYawSource` 也連帶指到這顆錯誤的攝影機。移動輸入其實仍然正常運作（角色的 `CharacterController` 照常移動），但因為畫面呈現完全跑掉、可見範圍也被這顆貼身攝影機弄亂，使用者才會覺得「按方向鍵沒反應」。
  - 場景裡因此同時存在兩台會渲染的 `Camera`（真正的 Main Camera + 角色骨架裡的內嵌攝影機），二者同時渲染時的疊圖/覆蓋順序不確定，進一步加重畫面異常。
- **修法**：
  1. `PlayerMayaVisualSetup.cs` 新增 `RemoveEmbeddedCameraRig()`，在每次把 Maya 模型換裝到 Player 身上時，自動找出並刪除視覺階層底下所有 `Camera` 元件所在的 GameObject，徹底清掉這個內嵌攝影機（角色本身沒有用到它，純粹是素材作者留下的殘留物）。
  2. `FixCameraCustomController.cs` 改用 `GameObject.Find("Main Camera")`（依名稱，跟 `GreyboxSceneBuilder.cs` 建立攝影機時用的名字一致）取代 `GameObject.FindWithTag("MainCamera")`，避免未來又被場景裡任何巧合帶有相同 tag 的物件騙到。
  3. 依序重新執行 `Replace Player Visual With Maya (Anime)` → `Wire Character Animator Link On Player` → `[Fix] Replace Camera With Custom Controller` 三個編輯器工具，重建乾淨的角色視覺並正確把 `ThirdPersonCameraController` 掛到真正的 Main Camera 上。
- 修法後直接檢查 `GreyboxTest.unity`：整個場景只剩一個 `Camera` 元件、`ThirdPersonCameraController` 正確掛在名為 "Main Camera" 的 GameObject 上、`CharacterMovement.cameraYawSource` 正確指向同一顆元件、場景中不再有任何 `Cinemachine` 相關殘留。
- 12 個 EditMode + 10 個 PlayMode 測試全數重新驗證通過。
- **教訓**：用 `GameObject.FindWithTag(...)` 在場景裡找「唯一」物件時，不能假設專案自己建立的物件是 tag 的唯一持有者——外部匯入的美術素材完全可能夾帶同樣 tag 的物件；改用明確的名稱／階層路徑查找，或是在腳本裡先用 `GetComponentsInChildren`/`GetComponent<Camera>()` 之類的型別檢查做二次過濾，會更保險。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：角色是否正常顯示、方向鍵移動與滑鼠視角是否正常回應。

## 已變更：攝影機改回固定視角，取消滑鼠視角控制（2026-08-10，使用者要求）

使用者回報畫面看起來還是舊的、行走仍有問題，並明確要求：「先將攝影機固定視角，並且明確 w/s/a/d 是控制角色前/後/左/右移動」，同時要求參考網路上一般 3D 遊戲的做法。查了幾篇 Unity 論壇討論與 Cinemachine Third Person Follow 文件，確認「固定角度跟隨攝影機（只跟位置、不跟旋轉）＋輸入方向相對攝影機換算」是常見且穩健的做法，跟先前造成多次 bug 的「滑鼠自由視角」是不同的設計取向，因此決定切換過去：

- `ThirdPersonCameraController` 拿掉滑鼠輸入（不再依賴 `Mouse.current.delta`、`mouseSensitivity`、`minPitch`/`maxPitch`/`invertY` 全部移除），改成 `yawDegrees`／`pitchDegrees` 兩個固定數值欄位（預設 0／25），攝影機每幀只會跟著角色的位置平移，旋轉角度永遠不變。
- 這讓 `CharacterMovement` 透過 `ICameraYawSource` 讀到的 yaw 值永遠是常數（0 度），移動方向與畫面呈現的方向從此不可能再因為攝影機旋轉而產生落差——這是比之前「滑鼠自由視角」更簡單、更不容易出 bug 的架構取捨，代價是玩家不能自己轉動視角。
- 實際驗算目前的 W/A/S/D 對應（`PlayerInputProvider.cs`）：W→`MoveInput.y=+1`、S→`-1`、A→`MoveInput.x=-1`、D→`+1`；配合 `CameraRelativeDirection` 在 yaw=0 時 forward=世界 +Z、right=世界 +X，換算後 **W＝遠離攝影機（前進）、S＝靠近攝影機（後退）、A＝畫面左、D＝畫面右**，且因為攝影機角度固定，這個對應在整個遊戲過程中永遠一致，不會像先前的滑鼠視角版本一樣隨時間漂移。
- `GreyboxSceneBuilder.cs`／`FixCameraCustomController.cs` 都已同步改成寫入 `yawDegrees`/`pitchDegrees` 而非舊的滑鼠相關欄位。
- 12 個 EditMode + 10 個 PlayMode 測試全數重新驗證通過。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：這是本次排查中第三次要求使用者實際 Play 驗證的修法，AI 端目前只能確認場景結構、欄位數值、測試通過，無法確認畫面實際觀感與手感是否符合預期。

## 已變更：改回滑鼠視角跟隨（原神風格），並修正「大跨步到很遠距離」的真正 bug（2026-08-10）

使用者對上一版固定視角不滿意，改要求「請參考原神那種動作遊戲 攝影機要跟著角色視角移動」，同時回報「移動控制還是有問題 會大跨步到很遠的距離」。這次是兩個獨立問題：

### 1. 攝影機改回滑鼠視角（原神風格）

- `ThirdPersonCameraController` 拿掉上一版的固定 `yawDegrees`/`pitchDegrees`，改回讀 `Mouse.current.delta` 累加 yaw/pitch（`mouseSensitivity`/`minPitch`/`maxPitch` 恢復，另外新增 `initialYaw`/`initialPitch` 作為起始角度），不需要按住任何按鍵，滑鼠移動就會即時轉動攝影機視角——符合原神那種「攝影機隨時跟著滑鼠視角、WASD 相對攝影機方向移動」的操作習慣。
- 這次跟先前 Cinemachine 版本的滑鼠視角**不是同一個 bug 的重演**：Cinemachine 出問題的根源是它把「跟隨位置」（Body/OrbitalFollow）和「瞄準角度」（Aim/RotationComposer）拆成兩個獨立元件，兩者對移動中角色的反應不同步才產生畫圈/漂移。我們自己寫的 `ThirdPersonCameraController` 只有一份 yaw/pitch 狀態，直接同時決定攝影機的旋轉和 `CharacterMovement` 讀到的相對方向，沒有「兩個系統各自反應」的架構，所以不會重現同一種 bug。
- `GreyboxSceneBuilder.cs`／`FixCameraCustomController.cs` 同步改回寫入 `mouseSensitivity`/`minPitch`/`maxPitch`/`initialYaw`/`initialPitch`。

### 2. 「大跨步到很遠距離」的真正原因：角色沒有真的站在地上

用一個暫時的診斷 PlayMode 測試（載入真實場景、餵入持續前進輸入、每幀記錄座標）直接量測，發現角色的 Y 座標**持續下降、完全不會穩定**——代表 `CharacterController.isGrounded` 從頭到尾都是 false，角色其實整段時間都在自由落體：

- 查場景檔案發現：Player 的 `CharacterController.height` 目前是 `1`（不是 `GreyboxSceneBuilder.cs` 原本設計的預設值 `2`），但 Player 的重生座標 Y 還是舊的 `1`——兩者搭配起來，膠囊體底部懸空在地板上方 0.5 單位，永遠碰不到地。專案裡沒有任何腳本會把 `height` 改成 1，判斷是使用者先前在互動 Editor 裡手動調整膠囊高度（可能是想解決之前「角色消失」時順手調的），但沒有同步調整重生高度，因而產生懸空。
- 角色因為抓不到地面，`_verticalVelocity` 每幀持續往下累加（永遠不會被 `isGrounded` 重設成貼地的小負值），下落速度隨時間無限增長，一旦終於碰到任何東西（地板、掩體方塊邊緣等），龐大的下墜速度加上 `CharacterController.Move` 的碰撞/滑動處理，就會讓角色瞬間被彈開一段很遠的距離——這正是使用者觀察到的「大跨步到很遠距離」。跟攝影機、輸入方向換算完全無關，是重生高度沒有跟著角色體型調整的落地判定 bug。
- **修法**：新增 `FixPlayerGroundedSpawn.cs`，直接從 Ground 的實際碰撞體世界邊界＋Player 目前的 `CharacterController.height`/`center` 反推正確的重生 Y（而非寫死常數），套用後 Player 的重生 Y 從 `1` 改成 `0.5`，膠囊底部剛好貼齊地面頂部。同時把 `GreyboxSceneBuilder.CreatePlayer()` 也改成用同樣的方式動態計算重生高度，之後即使 `height`/`radius` 再被調整，也不會又悄悄裂開一個懸空縫隙。
- 重新用診斷測試驗證：套用修法後角色 Y 座標穩定在同一個值不再持續下降，確認已經真正貼地。
- **教訓**：`CharacterController` 這類「高度/半徑」和「重生座標」是耦合的兩個數值，只改其中一個很容易留下肉眼不容易發現的懸空縫隙——同類型元件之後若要再手動微調外觀比例，最好同時檢查重生位置是否需要對應調整，或改用像這次一樣「從地面碰撞體反推」的動態算法，不要用寫死的常數。
- 12 個 EditMode + 10 個 PlayMode 測試全數重新驗證通過。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：滑鼠視角操作手感是否像原神一樣順手、角色現在是否穩定貼地行走、不再出現大跨步瞬移。

## 已變更：攝影機再次改為固定世界座標軸，移除第一人稱（2026-08-11，使用者要求）

使用者要求「固定世界座標軸」攝影機（類 Diablo/POE 的固定俯視角度），這是本專案第三次在「滑鼠自由視角」與「固定角度」之間切換（前兩次見上方 2026-08-10 的兩筆「攝影機改回固定視角」／「改回原神風格滑鼠視角」紀錄），但這次场景已經多了鎖定（Step ④）與第一人稱切換（Step 2 追加）兩個當時還不存在的系統，需要一併決定這兩者在固定角度下的行為：

- **鎖定敵人時鏡頭要不要跟著轉**：使用者確認不要——鏡頭角度永遠固定，只有角色自己的朝向會轉向鎖定目標（`CharacterMovement` 既有邏輯，未改動）。`ThirdPersonCameraController` 因此移除了原本鎖定時改讀 `TargetLockUtility.ComputeLockOnYawPitch` 覆寫 yaw/pitch 的分支；`TargetLockUtility.ComputeLockOnYawPitch` 本身還留著（`TargetLockUtilityTests.cs` 仍有測試覆蓋），只是攝影機不再呼叫它，日後如果要做鎖定提示 UI／瞄準輔助可能還用得到。
- **V 鍵第一人稱切換要不要保留**：使用者確認移除——固定角度鏡頭不支援自由看向的第一人稱視角。連帶刪除 `CameraViewMode.cs`、`CameraViewToggleTests.cs`、`FixFirstPersonToggleSetup.cs`；新增的 `FixFixedAxisCameraSetup.cs` 會順便確保 Player 的 "Visual" 子物件維持啟用，避免舊場景若剛好存檔在第一人稱隱藏狀態，因為 `ToggleViewMode()` 已經不存在而永遠卡住看不到角色。
- 詳細技術改動見 `CHANGELOG.md` 對應日期條目。50 個 EditMode + 27 個 PlayMode 測試通過，連跑兩次確認跟本次改動直接相關的測試（`ThirdPersonCameraControllerTests`／`LockOnFacingAndCameraTests`／`CameraRelativeMovementRegressionTests`）穩定全過；`CharacterMovementTests` 有兩個既有測試間歇性失敗，數值只差在容許門檻附近，跟本次沒碰的檔案（`CharacterMovement.cs` 未改動）與已知的 headless batchmode 積分效率問題一致，不是新迴歸。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：固定角度（yaw 0°／pitch 45°、距離 8）的俯視感是否符合預期，之後可直接調 Inspector 上的 `fixedYaw`／`fixedPitch`／`distance`。

## 已變更：移動加減速／轉向改用緩動曲線（2026-08-11，使用者回報「移動控制不夠自然」）

使用者先前用 `deep-research` 技能整理了一份現代 RPG 攝影機／移動做法研究（`Docs/Research/CAMERA_MOVEMENT_RESEARCH.md`），接著要求依研究結果做一次性工程修正，並明確表示這次核心只在移動手感，不含攝影機視角改動（攝影機視線高度調整留待下次單獨交付）：

- **原因**：`CharacterMovement.cs` 的加減速用 `Vector3.MoveTowards`（等速直線逼近目標速度）、轉向用 `Quaternion.RotateTowards`（等角速度）——兩者都是固定速率、抵達目標的瞬間硬停，沒有緩入緩出曲線，這正是研究報告點名的「機械感」來源。
- **修法**：改用 `Vector3.SmoothDamp`（水平速度）／`Mathf.SmoothDampAngle`（朝向 yaw），業界第三人稱控制器常用的緩動技巧。欄位改名並改變語意：`acceleration`/`deceleration`（每秒變化率）→ `accelerationSmoothTime`/`decelerationSmoothTime`（逼近目標所需秒數，預設 0.08/0.12）；`rotationSpeedDegrees`（度/秒）→ `rotationSmoothTime`（秒，預設 0.1）。
- `moveSpeed`（2，對齊 Maya Blend Tree 門檻避免腳步滑行）與 `gravity` 完全沒動，先前修好的滑步問題不受影響。
- 場景檔裡的舊欄位值（`acceleration: 20` 等）因為欄位已改名會被 Unity 直接忽略，載入後套用腳本新預設值，不需要手動遷移場景檔。
- 三個既有 PlayMode 測試同步更新：`CharacterMovementTests.cs`（`SetField` 改名）、`LockOnFacingAndCameraTests.cs`（原本鎖定朝向測試只等一個 frame 就斷言收斂，改成等最多 0.2 秒真實時間——緩動朝向天生需要非零模擬時間才能收斂，不像等角速度可以靠調大單一數值在一個 frame 內強制到位）。50 個 EditMode + 27 個 PlayMode 測試全數重新驗證通過。
- **刻意排除的範圍**：`EnemyAI.cs` 的等角速度轉向沒有比照修改（這次範圍限定玩家角色）；沒有加入真正的走/跑速度分層（WASD 目前仍只有單一 `moveSpeed`）；沒有做 Foot IK；攝影機仍是俯視固定角度，沒有改成角色視線高度。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：緩動後的加減速/轉向手感是否符合預期，三個新欄位數值都是合理起點猜測、非最終定案，可在 Inspector 直接調整比對。

## 已變更：攝影機改為角色視線水平高度（2026-08-11，使用者回報「視角還是高拍」）

上面的移動手感修正交付後，使用者立刻回報攝影機仍是高拍（俯視），要求「攝影機完全與角色視線平行」——這是研究報告中原本就規劃、但上一次刻意排除在範圍外的項目，這次單獨補上：

- **原因**：`ThirdPersonCameraController.fixedPitch` 原本是 45°（俯視/類 Diablo-POE 角度），`ComputeCameraPosition` 用 `rotation * Vector3.forward * distance` 算相機位置，pitch 非 0 時這個 forward 向量帶有向下分量，相機因此永遠從高處往下看，跟「視線平行」的要求相反。
- **修法**：把 `fixedPitch` 預設值改成 0——pitch=0 時 `Quaternion.Euler(0, yaw, 0) * Vector3.forward` 是純水平向量，相機會被放在跟 `targetOffset.y`（1.4，原本就是瞄準角色頭部/眼睛高度用的既有欄位）**同樣高度**、正後方、完全水平看向角色，相機視線因此跟角色的視線水平面平行，不再由上往下看。`distance` 先從 8 降到 3.5；用算圖確認水平角度正確後，使用者立刻回報「攝影機概念上要離角色很近，大概在後腦勺跨一個人的距離，才是真正的模擬人物走路視角」，於是再把 `distance` 降到 **1**——非常貼近後腦勺，讓水平視角更接近「角色自己的走路視角」而不是「旁觀者的近距離跟拍」。三個數值（`distance`／`fixedPitch`／`targetOffset`）都是合理起點猜測，非最終定案。
- **已知風險**：`ThirdPersonCameraController` 沒有做鏡頭與角色自身模型的碰撞處理，`distance=1` 這麼近有實際機會讓鏡頭卡進角色自己的頭髮/頭部模型裡穿模——這是自動化工具測不出來的，需要使用者實際 Play 才能發現；如果真的穿模，直接調高 Inspector 上的 `distance`（例如 1.2～1.5）留一點緩衝即可，不需要改程式碼。
- 因為場景檔（`GreyboxTest.unity`）裡 `distance`/`fixedPitch` 是先前「固定世界座標軸」那次修法明確寫入的序列化值，不會因為腳本預設值改變而自動更新，比照專案慣例新增一次性編輯器工具 `FixEyeLevelCameraSetup.cs`（Tools/Live2DAction/[Fix] Set Eye-Level Camera）套用到既有場景，並同步更新 `GreyboxSceneBuilder.cs`（場景重建工具）的預設值，兩者最終都寫入 `distance: 1`／`fixedPitch: 0`。
- `fixedYaw`（世界固定水平方向，跟角色朝向無關）與 `targetOffset`（1.4，瞄準高度）都沒有變動——使用者這兩次的要求都是「視線平行」與「距離」，不是攝影機要不要跟著角色轉（yaw），沿用先前「固定世界座標軸」的既定決策。
- 用一次性診斷用編輯器工具（跑完即刪除，未留在專案內）把 Main Camera 算圖存 PNG 確認，`distance=3.5` 與 `distance=1` 兩輪都確認：畫面地平線落在畫面中段、沒有向下傾斜，pitch=0 的水平取景邏輯正確生效；`distance=1` 那張圖角色頭部確實佔滿大半畫面，符合「貼近後腦勺」的預期構圖。但算圖裡角色材質/光照顯示明顯異常（黑色剪影＋詭異反光），原因未查證（可能是 batchmode 算圖缺乏正確光照設置，也可能是別的問題），這次沒有深入排查——這些圖只拿來驗證相機角度/距離幾何，不代表最終真實畫面效果，實際視覺觀感仍需要使用者在互動 Editor 裡 Play 確認。
- 50 個 EditMode + 27 個 PlayMode 測試全數重新驗證通過（攝影機這次改動的欄位只有數值，`ThirdPersonCameraControllerTests.cs` 既有測試都是直接餵參數給純函式 `ComputeCameraPosition`，不依賴元件預設值，不需要更新）。過程中 `EnemyAITests.TargetWithinDetectionRange_ChasesTowardTarget` 間歇性失敗一次，重跑即過，跟本次沒碰的 `EnemyAI.cs` 無關，屬已知的 headless batchmode 計時 flaky 問題。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：水平＋極近距離視角的實際觀感、鏡頭是否卡進角色自己的頭部模型、`distance=1` 是否太近、角色貼圖/光照在真正 Play 模式下是否正常（算圖用的 batchmode 沒有正確光照，看不出真實效果）。

## 已變更：攝影機拉遠避免只看到頭部＋Player2 補上鎖定＋這次沒有跑 Unity 驗證（2026-08-11，使用者回報記憶體不足要求跳過測試）

使用者回報上面 `distance=1` 太近，「目前畫面只看的到人物的頭部，下半身都在畫面之外」，同時要求把 Player2（機甲靜態看板）擺正到跟玩家同一水平線、並且能用 Q 鎖定它當敵人；並明確表示「這是修正不要幫我測試 我內存不夠用」——這次**完全沒有啟動 Unity Editor**（沒有算圖、沒有跑 EditMode/PlayMode 測試），改用直接編輯場景 YAML／腳本檔的方式完成，比照專案慣例的風險必須誠實記錄：

- **攝影機**：`distance=1`（正後方、跟頭部同高、pitch 完全水平）在數學上會有一個固有限制——攝影機貼在跟頭部同高的位置、又完全不能俯仰，能看到的畫面垂直範圍會集中在頭部附近，天生看不到下半身（這就是「模擬角色自己走路視角」跟「畫面要看到全身」兩個目標互相衝突的地方）。折衷做法：把 `distance` 從 1 拉到 **2.2**（比原本高拍時的 8 近很多，但比極近的 1 遠一些），`fixedPitch`／`targetOffset` 不變。`ThirdPersonCameraController.cs`／`GreyboxSceneBuilder.cs`／`FixEyeLevelCameraSetup.cs`／`GreyboxTest.unity` 場景檔的 `distance` 欄位都已同步改成 2.2。這個數值**這次沒有算圖驗證**，是否真的能完整看到全身、會不會反而又太遠，需要使用者自己在 Editor 裡試——`distance` 是 Inspector 上可直接拖曳調整的欄位，比再讓 AI 猜一次數值更快。
- **Player2 鎖定**：`LockOnTarget` 元件（沒有任何欄位需要額外設定，`TargetLockController` 會用 `FindObjectsByType<LockOnTarget>` 自動掃到場景裡所有掛這個元件的物件，不需要額外註冊）已直接用 YAML 加到 Player2 GameObject 上，比照 `TrainingDummy` 既有的掛法。玩家在鎖定範圍（15 單位）與視角（60 度）內對它按 Q 應該就能鎖定；因為完全沒有跑過 Unity，這次**沒有實際驗證過**這段手動寫入的 YAML 語法/fileID 是否完全正確，理論上比照既有元件的序列化格式，但沒有實測，如果進 Editor 後 Player2 底下沒有出現 `Lock On Target` 元件或跳錯誤，麻煩回報。
- **Player2 是否「擺正到同一水平線」**：檢查了 `GreyboxTest.unity` 現有數值——Ground 頂面在世界座標 Y=0，Player2 的 Transform Y 也是 0，兩者數字上已經對齊；FBX 匯入設定裡也沒有看到異常的軸向轉換設定；`ASSET_LICENSES.md`/`KNOWN_ISSUES.md` 先前的算圖驗證記錄也說這個模型是站立姿勢（不是它的同產線姊妹模型那種倒臥姿勢）。**沒有找到明確證據顯示目前有實際的位置/角度錯誤**，所以這次**沒有做任何猜測性的旋轉調整**——如果進 Editor 後看起來還是歪的/浮空的/陷進地板，麻煩告訴我具體是哪種歪法（角度大概幾度、往哪個方向），或者直接在 Scene 視圖用旋轉/位移 gizmo 手動喬正、存檔即可，這種一次性的視覺微調用 Editor 手動拖會比我隔著螢幕猜數字快很多。
- **這次沒有做的**：套用一次性編輯器工具 `FixEyeLevelCameraSetup.cs` 到場景（改用直接編輯場景檔達到同樣效果，跳過啟動 Unity）；跑 EditMode/PlayMode 測試；算圖驗證。**這些都還沒有被自動化證實過**，下次有餘裕（記憶體/時間允許）時應該補跑一次完整驗證，目前這批改動的正確性完全基於程式碼閱讀與手動 YAML 比對，不是實測結果。

## 已變更：Player2 擺到跟 Player1 面對面＋補算圖驗證站姿（2026-08-11，使用者要求 Play 前先看畫面）

使用者接著要求「PLAY MODE 跑之前 能不能先看到含有 PLAYER1 PLAYER2 的場景畫面」，並要求 Player2 跟 Player1 面對面站著——這次使用者等於主動同意再啟動一次 Unity 來算圖，於是這次**有跑 Unity**（跟上一項的「完全不跑」不同），用臨時編輯器工具（跑完即刪除，未留在專案內）產生了三張診斷用截圖：

- Player2 位置從 `(2.5, 0, -2)` 改成 `(1.2, 0, 0.5)`，旋轉改成朝向 Player1（`(0, 0.5, -2)`）的方向——用 `atan2` 手算出 yaw ≈ -154.36°，換算成四元數 `(0, -0.9751, 0, 0.2219)` 直接寫進場景 YAML（沒有用 Editor 的旋轉工具，是手算後貼數字）。第一版嘗試把 Player2 放在 Player1 正前方同一條軸線上（x=0，跟正後方鏡頭共線），算圖後發現這樣 Player2 會被 Player1 自己的身體整個擋住，從第一人稱視角的遊戲鏡頭幾乎看不到——所以改成現在這個帶側向偏移的位置，讓兩者能同時入鏡。
- 三張算圖都存在 scratchpad（已刪除，未留在專案內；如果要保留紀錄需要另外要求）：(1) 外部俯瞰角度，同時看到 Player1／TrainingDummy／Player2；(2) 實際遊戲用 Main Camera（Player1 背後、水平、`distance=2.2`）視角，確認 Player1 全身入鏡、Player2 也在畫面裡；(3) Player2 單獨近拍。
- **意外收穫，解決了上一項的懸念**：Player2 近拍算圖清楚顯示這個機甲模型其實是**站立的動態戰鬥姿勢**（不是躺著/歪掉），先前遠距離、光照異常的算圖看起來怪異只是姿勢本身比較張揚（翅膀外展、單手前伸）加上算圖光照差，不是真的歪掉——呼應上一項「沒找到證據顯示需要旋轉」的判斷，這次算圖證實了這個判斷是對的，不需要額外修正站姿。
- 算圖裡仍然存在光照/材質異常（這次還多了詭異的洋紅色三角形色塊，懷疑是 batchmode 算圖處理天空盒或粒子效果的已知限制），跟角色本身的模型/骨架無關，純粹是算圖環境問題，不影響判斷位置與朝向是否正確。
- **這次同樣沒有跑 EditMode/PlayMode 測試**（只用了三次一次性算圖，沒有跑完整測試套件，兼顧使用者的記憶體考量與這次「先看畫面」的實際需求）；手算的旋轉四元數理論上正確（有用 forward 向量反推驗證過），但沒有透過測試或使用者互動 Play 做最終確認。
- **仍待使用者本人在互動式 Editor 中 Play 一次確認**：Player2 的朝向角度算出來是否真的「看起來」朝向 Player1（算圖是靜態外部視角，光照又差，肉眼在 Editor 場景視圖直接看會更準）；`(1.2, 0, 0.5)` 這個距離/位置手感是否合適。

## 已釐清／已變更：使用者實際 Play 後回報三個現象（2026-08-11）

使用者第一次實際在 Editor 裡 Play 後回報：(1) Player1 身邊有個白柱子一直跟著他，(2) 有時移動角色後整個畫面卡住，(3) Player2 現在只有半身在畫面中、腳被截斷。這次**沒有跑 Unity**（純程式碼/場景檔閱讀+一項數值修正），逐項排查：

- **白柱子＝ TrainingDummy（敵人 AI）**，不是 bug：`TrainingDummy` 底下的 "Visual" 子物件是 `GameObject.CreatePrimitive(PrimitiveType.Capsule)` 產生的膠囊體，材質欄位指向 Unity 內建的預設材質（灰白色，不是專案自己的 asset，找不到對應 `.meta`），跟 `Docs/KNOWN_ISSUES.md`「近戰敵人 AI」項記錄的「敵人沒有外觀、只有沒有外觀的 Capsule」完全對得上。它「跟著玩家」是因為 `EnemyAI` 的追逐邏輯（`detectionRange=8`，玩家進入範圍就會 `Chasing` 朝玩家移動）正常運作——這是**功能正確、外觀未完成**的既有已知限制，不是這次改動造成的新問題，也不需要程式修正，等之後要幫敵人做外觀時才會處理。
- **移動後畫面卡住**：檢查了 `Assets/_Project/Game` 底下所有執行期腳本，**沒有找到任何 `while` 迴圈**（`grep` 全専案搜尋 0 筆結果），排除「無窮迴圈把 Editor 卡死」這個最常見的程式碼成因；用臨時工具重新載入過這個場景多次也都正常算圖完成、沒有拋出例外，場景檔本身結構應該是完整的。**目前沒有找到確切原因**，比較可能的解釋是 Unity Editor 在 Play 模式下第一次繪製某個材質/Shader 變體時的一次性編譯卡頓（尤其這幾輪一直在動場景/攝影機設定，很常見、不是程式錯誤），但這只是推測，需要使用者補充：卡住的時候 Console 有沒有跳出紅字錯誤訊息？是整個 Editor 沒反應要強制關閉，還是畫面停頓幾秒後自己恢復？每次 Play 都會發生還是只發生過一次？有這些細節才能進一步判斷。
- **Player2 腳被截斷**：跟先前「Player1 只看得到頭」是同一種幾何限制——鏡頭離得近、垂直視野（FOV）不夠寬，才會把站在鏡頭附近的角色垂直方向裁切掉，這次換成發生在 Player2 身上（可能是玩家在 Play 時走近了 Player2）。上次是靠拉遠 `distance` 解決 Player1 的問題，但那個做法對「站在別處的其他角色」沒有幫助，這次改成**加寬 Main Camera 的視野角（FOV）**：`60° → 75°`，這樣不管是 Player1 自己還是玩家走近的任何其他角色，垂直方向能收進畫面的範圍都會變大，不需要每次都靠拉遠鏡頭距離來換。同步改了場景檔與 `GreyboxSceneBuilder.cs`。**這個數值沒有算圖驗證**，FOV 加寬會讓畫面邊緣的透視變形增加，如果覺得邊緣看起來怪異（魚眼感），可以把這個值調低一點，或跟我說再調整。

## 已深入排查：「角色移動到一半畫面卡住」（2026-08-11，使用者回報「常常」發生，要求重新檢視專案）

上一項只是初步排查（沒跑 Unity），使用者接著回報這個現象**常常**發生，要求重新檢視整個專案——這次動用了完整的排查手段，找到兩個實際發現，但都不足以完全解釋「常常卡住」，記錄如下供之後繼續追查：

- **看了使用者本人真實 Play session 的 Unity Editor.log**（`%LOCALAPPDATA%\Unity\Editor\Editor.log`，`IsHumanControllingUs: 1` 確認是互動 session、不是我這邊的 batchmode）：進入 Play 模式那次記錄裡，domain reload 花了約 3 秒（`Domain Reload Profiling: 2984ms`）、Asset Pipeline Refresh 又花了約 3.15 秒，兩者相加 Editor 有 6 秒以上的時間在做同步且會讓 UI 停止回應的重編譯/reimport 工作——**這正是 Unity 每次帶著新的程式碼/場景改動進 Play 模式時的正常行為**，不是我們程式的 bug，但在你已經提過的記憶體不足的機器上，這幾秒很可能被拉得更長、感覺起來就是「卡住」。這幾輪我改了非常多次程式碼跟場景檔，代表你每次要重新 Play 幾乎都會觸發這個重編譯延遲。
- 同一份 log 也發現一個真實但**無關**的小問題：進 Play 模式時 Console 跳出 7 則「The referenced script on this Behaviour ... is missing!」警告。用診斷工具實際掃過場景後定位到：`Player/Visual`（Maya 模型）上有 **2 個組件的腳本參照已經失效**（原始 Sketchfab 素材包可能帶有預覽用的小工具腳本，複製進本專案時只複製了 `.fbx`/`.prefab`/`.meta`/材質，沒有複製到那些腳本，見 `Docs/KNOWN_ISSUES.md`「Maya 動漫風角色佔位」項）。**已修正**：用 `GameObjectUtility.RemoveMonoBehavioursWithMissingScript` 把這兩個失效組件從 Player 這個場景實例上移除（記錄成 Prefab Instance 的 removed-component 覆寫，沒有動到共用的 Maya prefab 資源本身）。**這類「腳本缺失」組件本身是惰性的（Unity 直接跳過、不會執行），理論上不會造成卡頓**，但清掉之後至少 Console 乾淨了，之後排查其他問題時比較不會混淆視聽。
- **直接寫了一個效能診斷 PlayMode 測試**（`MovementFrameTimingTests.cs`，已保留在測試套件裡）：載入真正的 `GreyboxTest` 場景（Player、會追逐的 TrainingDummy、Player2，一個不少），持續按住前進輸入 5 秒，記錄每一個 Update tick 的實際牆鐘耗時，找超過 300ms 的異常長 frame。**結果：連跑兩次，最長的一個 frame 只有 3.7ms／1.3ms，完全正常，沒有重現任何卡頓**。這代表：*純粹「按住移動輸入、跑滿 5 秒」這個動作本身，在我們自己的程式碼與場景設定裡，沒有確定性的效能地雷*（沒有無窮迴圈、沒有失控的 GC、沒有每幀爆量的 Physics/FindObject 呼叫——這幾項在稍早那次排查已經個別確認過）。
- **目前最可能的結論**：使用者感受到的「卡住」，比較可能是（a）每次帶著新改動進 Play 模式時的正常重編譯延遲（尤其在記憶體吃緊的機器上會被放大），而不是「移動中」真的觸發了什麼問題；或者（b）互動式 Editor 特有、我這邊 headless 測試環境完全複製不出來的狀況（例如 Scene 視圖＋Game 視圖同時算圖、真實輸入裝置輪詢、GPU 驅動或作業系統層級的資源競爭）。**這兩者都不是這次改動可以用程式碼修正的**，需要使用者下次卡住時實際記錄：卡住當下工作管理員的記憶體/CPU 使用率、Console 有沒有紅字、是進 Play 那一刻卡還是移動途中才卡、卡住的當下按什麼鍵有沒有反應（完全沒反應 vs 只是變慢）。
- 50 個 EditMode + 28 個 PlayMode（新增的 `MovementFrameTimingTests` 算一個）測試全數通過，這次確實有跑 Unity（多次 `-executeMethod`／`-runTests`）。

## 已找到真正原因並修正：Player2 的旋轉四元數沒有正確歸一化，狂洗 Console（2026-08-11，使用者提供 Console 截圖）

上一項的結論是猜測、沒有確鑿證據。使用者這次直接截圖給看 Console，**真正原因浮出來了**：

- 截圖裡 Console 洗版式地重複同一則錯誤：`Quaternion To Matrix conversion failed because input Quaternion is invalid {-0.000000, -0.975100, 0.000000, 0.221900} l=1.000060`，呼叫堆疊是 `UnityEngine.GUIUtility:ProcessEvent`——**這正是「Player2 擺到跟 Player1 面對面」那次，我手算 `atan2` 之後手動把四元數分量（`0, -0.9751, 0, 0.2219`）打進場景 YAML 造成的**：手算/四捨五入到 4 位小數後，這組數字的長度是 `1.000060`，不是精確的 `1.0`。Unity 對四元數合法性的檢查容忍度非常嚴（差 0.00006 就判定無效），每次它要幫這個 Transform 算旋轉矩陣（不管是渲染、Scene 視圖重繪、或任何 GUI 事件）都會失敗並印一次錯誤——`GUIUtility:ProcessEvent` 幾乎每個滑鼠移動/按鍵事件都會觸發一次，這代表這個錯誤在互動時是以**每秒數十次以上**的頻率洗版，這正是造成「移動時畫面卡住」最直接、最有力的解釋：不是某個迴圈真的把 Editor 卡死，而是每次事件都要花時間印一條錯誤訊息，累積起來就是肉眼可見的頓格/卡頓，在記憶體/效能吃緊的機器上更明顯。
- **教訓**：不應該手算三角函數再手動把四元數分量打進場景 YAML——人工算的精度不夠，Unity 對四元數長度的容忍度比看起來嚴格很多。之後任何非 0°/90°/180°/270° 這種乾淨角度的旋轉，都應該透過實際執行 `Quaternion.LookRotation`／`Quaternion.Euler` 這類 Unity 自己的 API 算出來再寫入（保證正確歸一化），不能再手算。
- **已修正**：把 Player2 的旋轉改回精確的 180° 翻轉（`{x: 0, y: 1, z: 0, w: 0}`，數學上正好是單位長度，不會再有這個問題），位置維持 `(1.2, 0, 0.5)` 不變（跟 Player1 之間仍有側向偏移，鏡頭裡看得到，只是不再是精確瞄準 Player1 的角度，而是概略朝向那個方向）——用直接改場景檔的方式修的，**沒有啟動 Unity**（你的 Editor 這次是開著的，怕跟我這邊另開一個 batchmode Unity 實例互相干擾/衝突寫入，這次特地避開）。
- **仍待你確認**：麻煩讓 Unity 重新載入這個場景（或直接重開 `GreyboxTest.unity`）再 Play 一次，確認 Console 裡那個 `Quaternion To Matrix conversion failed` 錯誤是否已經消失、卡頓是否真的解決了。如果你的 Editor 目前開著且已經載入了舊版場景，直接存檔可能會用記憶體裡的舊資料**覆蓋掉**我剛剛寫入的修正，記得先確認 Unity 有重新讀到這次的修正（例如切到別的視窗再切回來觸發 Unity 偵測外部檔案變更，或乾脆重開這個場景）。
- 截圖裡也還看得到「The referenced script on this Behaviour (Game Object 'Visual') is missing!」，數量比最一開始少（3 則，不是 7 則），時間戳記顯示這是**在**我修掉 Player/Visual 那 2 個失效組件**之前**的截圖（16:28），不確定是否已經解決，等你重新 Play 一次確認即可看到最新狀況。
- 這次修完後有實際重新跑過 EditMode/PlayMode 測試（確認四元數修正沒有引入新問題）：50 個 EditMode + 28 個 PlayMode 全數通過，Console 裡確認**不再出現** `Quaternion To Matrix conversion failed` 錯誤。跑測試過程中意外發現另一組跟這次改動無關的「腳本缺失」警告（`(Unknown)` + 空名稱物件，每次載入場景固定出現 6 則），推斷是先前文件記錄過的 076 Live2D Cubism 立牌殘留物件（「Play 模式下 Cubism 模型根物件名字會變空字串」的已知現象），不是新問題，暫不處理。

## 已修正：訓練假人不再追逐玩家＋新增邊界牆（2026-08-11，使用者回報）

使用者要求「讓白柱子不要跟著角色」，並回報「角色到了邊界就會消失」：

- **白柱子（TrainingDummy）不再追逐**：`EnemyAI.detectionRange` 從 8 改成 **0**。`EnemyBehaviorUtility.DetermineState` 的第一個判斷是「距離 > detectionRange 就回傳 Idle」，設成 0 之後幾乎任何實際距離都會滿足這個條件，等於永遠停在 Idle，不會再追逐也不會主動攻擊——比較符合「訓練假人」這個名字該有的樣子（站在原地讓你打，不會反過來追你）。同步改了場景檔與 `GreyboxSceneBuilder.cs` 的預設值。兩個既有測試檔（`EnemyAITests.cs`／`EnemyAttacksPlayerTests.cs`）都是用反射直接設定自己的 `detectionRange`，不吃這次改的預設值，不受影響。
- **角色在邊界消失＝真的會掉出地圖**：`Ground` 是一塊 30×30 的方塊（X／Z 各 -15～15），場景裡完全沒有任何東西擋著，玩家走到邊緣以外就等於踩空，重力持續往下拉、永遠掉不到地——這正是「消失」的原因，不是視覺 bug，是真的掉出可玩範圍外。**已修正**：新增 4 面看不見的邊界牆（`BoundaryWall_North/South/East/West`，只有 `BoxCollider`，沒有 `MeshRenderer` 所以看不到，緊貼 `Ground` 四個邊、高 6 單位、四片互相重疊蓋住轉角避免對角線縫隙），把玩家真正擋在地板範圍內。`GreyboxSceneBuilder.cs` 新增 `CreateBoundaryWalls()` 並在建場景流程中呼叫，之後重建場景會自動包含這 4 面牆；既有場景則用一次性編輯器工具套用（跑完即刪除，未留在專案內，工具本身有做成 idempotent／可重複執行不會建立重複牆）。
- 兩項改動都有實際跑 Unity 驗證（這次判斷你的 Editor 已經處理過場景，重新開 batchmode 執行沒有額外風險），50 個 EditMode + 28 個 PlayMode 測試全數通過。
- **仍待你本人確認**：邊界牆的厚度/高度是否合適（目前 6 單位高、1 單位厚，正常人形角色跳不過去，但沒有實際測試跳躍相關功能，因為專案目前也沒有跳躍系統）；訓練假人完全不追逐是否符合預期，如果之後想要「靠近才會反擊但不追過來」這種折衷行為，需要另外調整（目前 `attackRange` 判斷邏輯是接在 `detectionRange` 判斷之後，`detectionRange=0` 會讓 `attackRange` 永遠判斷不到）。

## 已修正：攝影機再拉近拉低＋Player2 補上碰撞（2026-08-11，使用者回報「還是太高太遠」）

使用者回報前一版（水平視角、`distance=2.2`、FOV=75）還是太高太遠，且「有時移動角色感覺攝影機視角更遠了」；另外要求柱子（TrainingDummy）／Player1／Player2 之間都要有碰撞阻擋、不能互相穿透：

- **攝影機再拉近拉低**：`targetOffset.y`（瞄準/相機高度）從 1.4 降到 **1.15**（更貼近實際眼睛高度，不是頭頂上方）；`distance` 從 2.2 降到 **1.5**；Main Camera 的 `field of view` 從 75° 降到 **65°**——上次為了讓垂直視野塞得下全身而把 FOV 加寬到 75°，但**寬 FOV 本身會讓畫面裡的東西看起來更小/更遠**（這是鏡頭透視的自然效果，跟距離數值無關），這很可能就是「移動時感覺攝影機更遠」的原因：`ComputeCameraPosition` 的公式完全只吃 `target.position`，沒有任何東西會隨移動動態改變距離，所以那個「更遠」的感覺應該是視覺上的（FOV 太寬＋固定世界座標軸鏡頭在你轉向或橫移時、角色會偏離畫面正中央，寬 FOV 在畫面邊緣的透視壓縮更明顯，两者疊加放大了這個感覺），不是真的有 bug 讓鏡頭數值跑掉。這次把 FOV 稍微收回來，同時維持比原本 60° 稍寬一點的餘裕。三個數值同步更新 `ThirdPersonCameraController.cs`／`GreyboxSceneBuilder.cs`／`FixEyeLevelCameraSetup.cs`／場景檔。**仍是合理猜測，沒有到「精確算出來」的程度**，可以直接在 Inspector 上調 `distance`／`targetOffset`／Main Camera 的 `Field of View` 三個欄位比對手感。
- **Player2 補上碰撞**：檢查發現 `Player2` 這個 GameObject 從頭到尾**只有 Transform 跟 `LockOnTarget`，完全沒有任何 Collider**——玩家會直接穿過去，這是這次「不會穿透」要求裡唯一真的缺東西的部分。已加上 `CapsuleCollider`（半徑 0.6、高度 2.2，粗略對應機甲模型的體型，非精確量測）。`TrainingDummy` 跟 `Player` 本來就都有 `CharacterController`（本身自帶碰撞），彼此之間理論上已經會互相阻擋，這次沒有額外改動，只是補上驗證。
- **新增碰撞阻擋的永久回歸測試**（`CharacterCollisionBlockingTests.cs`，保留在測試套件裡）：載入真實場景，分別讓玩家貼著 `TrainingDummy`／`Player2` 往前衝 1 秒，驗證兩者都不會被穿透（沒收斂到幾乎重疊的距離）。**兩個測試都通過**，證實 `TrainingDummy`／`Player` 的既有 `CharacterController` 確實有互相阻擋，`Player2` 補上 `CapsuleCollider` 後也確實擋得住。
- 50 個 EditMode + 30 個 PlayMode（新增 2 個碰撞測試）測試全數通過，這次有實際跑 Unity 驗證。
- **仍待你本人確認**：這次的攝影機數值（1.5／1.15／65°）觀感如何；`Player2` 的碰撞體大小抓得準不準（半徑/高度是粗略猜測，如果模型的手腳／武器伸出範圍比膠囊體大，還是可能有一點穿模，之後可以再調整或換成更貼合外形的 Collider）。

## 已釐清：「移動到一半畫面卡住」目前不會再發生（2026-08-11）

使用者提供 10 秒螢幕錄影，截圖分析後發現真正現象是**鏡頭在某個時間點停止跟隨玩家**（背景的訓練假人／Player2／掩體方塊像素級靜止不動，玩家角色卻持續在畫面裡變大、往下緣沉，最後幾乎消失出框），比先前猜測的「Editor 重編譯延遲」更具體、更嚴重。寫了一個直接量測 Main Camera 實際座標（對照 `ComputeCameraPosition` 公式）的診斷測試，讓玩家連續 10 秒往 8 個不同方向移動，結果**全程幾乎零誤差（最大 0.08 單位，只在第一幀），沒有重現鏡頭卡住**——這代表問題不是「移動這件事」本身就會觸發的固定邏輯錯誤，比較可能跟當時那次 session 裡的特定操作有關（例如場景中途被重新載入，畫面裡曾經多出一個不該有的 `DontDestroyOnLoad` 分類，暗示某個東西被標記跨場景存活）。**使用者後續回報「現在已經不會卡住了」**，先記錄成已釐清、非持續性問題；如果之後又出現，麻煩比照這次直接提供螢幕錄影，或至少「卡住當下 Hierarchy 有沒有出現重複的 Player／Main Camera」的截圖，會比文字描述好定位得多。

**事後補充（2026-08-11 稍晚）**：下面「Player 座標被意外拖走」那次排查，實際發現 `Player` 座標曾經被拖到 `(10, -0.5, 0)`——這跟這次影片裡「鏡頭停在原地、角色越走越近變大」的畫面**很可能是同一個根本原因**：鏡頭其實有正確跟著座標跑掉的 Player（不是真的「停止跟隨」），只是 Player 本身被拖到很遠的地方，跟畫面裡看到的「靜止背景」（訓練假人/Player2/掩體，都還在原本正確的位置）距離拉開，才造成視覺上像是鏡頭沒跟上的錯覺。當時的診斷測試量測的是「鏡頭有沒有跟上 Player.transform.position」，這個邏輯本身沒錯，只是沒想到 Player.transform.position 自己已經是錯的。

## 已新增：Player2 隨機漫遊＋邊界內回頭（2026-08-11，使用者要求）

使用者希望 Player2（機甲看板）能緩慢隨機移動，碰到邊界要回來，不要一直站著不動：

- 新增 `WanderUtility.cs`（純邏輯，EditMode 可測，比照 `EnemyBehaviorUtility`/`TargetLockUtility` 既有的純邏輯先行模式）＋ `WanderMovement.cs`（MonoBehaviour）：每隔幾秒（預設 3 秒）選一個新的隨機水平方向慢慢走（預設 0.5 單位/秒），一旦超出邊界（預設半徑 13，比實際邊界牆的 15 留了緩衝，不需要真的撞到牆才轉向）就改成朝原點方向走回來。轉向用 `Mathf.SmoothDampAngle`（緩動角度），**不是**手算四元數硬塞——這是直接記取上面「Quaternion 沒歸一化狂洗 Console」那次教訓後的做法。
- 這個元件刻意做得很輕量：不用 `CharacterController`（沒有重力/輸入/戰鬥需求），直接改 `Transform.position`，因為 Player2 本身不需要在地形高低差之間移動，單純水平漫遊。
- 已掛到場景裡的 `Player2` GameObject 上。
- 新增測試：`WanderUtilityTests.cs`（EditMode，5 測試，涵蓋一般漫遊/超出邊界四個方向/退化情況/方向永遠是水平單位向量）、`WanderMovementTests.cs`（PlayMode，2 測試：獨立元件的邊界測試、真實場景裡 Player2 確實掛了這個元件且沒有跑出邊界牆範圍）。
- 55 個 EditMode + 32 個 PlayMode 測試全數通過，這次有實際跑 Unity 驗證。
- **仍待你本人確認**：漫遊速度／轉向頻率的手感是否符合預期（`moveSpeed`／`directionChangeIntervalSeconds` 都可以直接在 Inspector 上調）。

## 攝影機調整教學（給使用者自行操作，2026-08-11）

使用者要求教學而非直接改數值，記錄在這裡方便之後查閱：

1. Hierarchy 選 `Main Camera`。
2. Inspector 裡的 **Third Person Camera Controller** 元件：
   - `Distance`：鏡頭離角色的距離。數字變小＝拉近，變大＝拉遠。
   - `Target Offset` 的 **Y**：鏡頭瞄準/所在的高度。數字變小＝鏡頭變低（更貼近腰部），變大＝變高（更貼近頭頂甚至以上）。
3. 同一個 GameObject 上的 **Camera** 元件：
   - `Field of View`（視野角）：數字變小＝像望遠鏡拉近、視角變窄，畫面裡東西看起來更大更近；數字變大＝像廣角鏡，看到範圍變大但東西看起來更小更遠，邊緣會有透視變形。
4. **這三個欄位在 Play 模式中可以直接拖動即時看到效果，不需要重新 Play。** 但 Unity 有個常見陷阱：**Play 模式中修改的 Inspector 數值，退出 Play 後預設會被還原、不會自動存檔**。所以要用這個流程：Play 中拖到滿意 → 記下最終數字 → 退出 Play 模式 → 回到 Edit 模式，把同樣的數字再手動填一次到同一個欄位 → 存檔（Ctrl+S）。

## 已新增：執行前（Edit 模式）也能即時預覽攝影機（2026-08-11，使用者問「如何調整執行前的預覽畫面」）

上面的教學有個前提沒說清楚：`ThirdPersonCameraController.LateUpdate()` 原本只在 Play 模式才會被 Unity 呼叫，所以**沒按 Play 之前，Game 視窗看到的攝影機畫面只是它上次被移動到的舊位置**，不會反映目前的 `Distance`／`Target Offset` 數值——這就是「執行前的預覽畫面」調不動、或跟實際 Play 起來不一樣的原因。

- **已修正**：`ThirdPersonCameraController` 加上 `[ExecuteAlways]`，讓 `LateUpdate` 在 Edit 模式（沒按 Play）也會執行。效果：Game 視窗即使沒按 Play，也會即時反映攝影機目前應該在的位置；在 Inspector 拖動 `Distance`／`Target Offset`／`Field of View` 時可以直接在 Edit 模式看到效果，**而且這時候的修改是真的存檔（不是 Play 模式那種按退出會還原的暫時修改）**——等於解決了上面教學提到的「Play 中改完要手動抄一次數字」的麻煩，之後可以直接在 Edit 模式調到滿意，直接存檔即可。
- 這次因為使用者的 Editor 開著（跑 batchmode 測試時遇到 Unity「另一個實例正在使用這個專案」的錯誤），**沒有實際跑測試驗證**，純粹是程式碼層面的修改（加一個屬性，沒有動邏輯）。理論上風險很低，但按照專案規則仍要誠實記錄「這次沒驗證」。麻煩你的 Editor 讀到這次修改（存檔會觸發重新編譯）後，自己在 Edit 模式試著調一下 `Distance` 確認 Game 視窗真的會跟著動。

## 已找到「每次 Play 都浮空掉落」的真正原因：Player 座標被意外拖走（2026-08-11）

使用者關閉 Editor 後，這次有跑 Unity 完整診斷：

- **不是程式碼 bug**：場景檔裡 `Player` 的座標被改成 `(10, -0.5, 0)`——Y 是 `-0.5`，代表角色膠囊體整個陷進地板下面一半（`CharacterController` 高度 1、置中在自己原點，正常應該是 Y=0.5 讓底部剛好貼地）。這跟上一輪注意到 `Ground` 座標也被移動過是同一件事：使用者在 Editor 裡操作時（很可能是想用 Scene 視窗導覽、卻不小心用了移動工具）把場景裡的物件拖走了位置。因為攝影機當時已經是 `[ExecuteAlways]`，鏡頭又正確地跟著跑掉的角色跑到很遠的地方，才造成「浮空掉落＋視角完全偏離」的觀察。
- **已修正**：`Player` 座標改回 `(0, 0.5, -2)`（跟地板頂面 Y=0 對齊，膠囊體底部剛好貼地）。
- **新增永久回歸測試**（`PlayerSpawnGroundingTests.cs`）：用**真實重力**（大多數其他移動測試會把重力歸零以隔離水平移動的測試，這個測試刻意保留真實重力）載入真實場景，在完全沒有輸入的情況下觀察 1 秒，確認角色 Y 座標不會往下掉超過 0.5 單位——以後不管是程式碼真的出問題、還是又不小心拖到場景物件，這個測試都會抓到。
- 55 個 EditMode + 33 個 PlayMode 測試全數通過，這次有實際跑 Unity 驗證。

## 已新增：滑鼠視角控制（RPG 風格），並修正一個連帶發現的測試隔離 bug（2026-08-11，使用者要求）

使用者要求「設計角色視角能被滑鼠控制（像是 RPG）」——這是本專案第四次在「滑鼠自由視角」與「固定角度」之間切換（前三次見 2026-08-10～08-11 的多筆記錄），這次改用歷史上已經驗證過不會重演「畫圈」bug 的架構（見下方說明），並吸取先前手算四元數出包的教訓：

- **設計**：`ThirdPersonCameraController` 的 `fixedYaw`／`fixedPitch`（兩個常數）改成 `initialYaw`／`initialPitch`（只決定起始角度）+ 內部的 `_yaw`／`_pitch`（真正驅動攝影機的可變狀態）。Play 模式下，每個 `LateUpdate` 讀 `Mouse.current.delta`，用 `mouseSensitivity` 換算成角度變化量累加到 `_yaw`／`_pitch`，`_pitch` 用 `minPitch`／`maxPitch`（預設 -40°／70°）夾住避免翻過頭。**不需要按住任何按鍵**，滑鼠移動就會即時轉動視角，符合「RPG 風格」的操作習慣。
- **為什麼這次不會重演 Cinemachine 那次「畫圈」bug**：當年的問題是 Cinemachine 把「跟隨位置」（Body）和「瞄準角度」（Aim）拆成兩個各自獨立反應的元件，兩者對移動中角色的反應不同步才產生無窮迴圈。這個自己寫的攝影機從頭到尾只有**一份** yaw/pitch 狀態（`_yaw`／`_pitch`），同時決定攝影機旋轉**和** `CharacterMovement` 透過 `YawDegrees` 讀到的相對移動方向，沒有「兩個系統各自反應」的架構，所以不會重現同一種 bug——這個推理在上一次「原神風格」滑鼠視角時就已經驗證過。
- **鎖定敵人的行為不變**：攝影機依然不會因為鎖定而旋轉（只有角色自己的朝向會轉向目標，既有邏輯，沒有改動），這次新增滑鼠視角跟鎖定系統完全獨立。
- **`[ExecuteAlways]` 的 Edit 模式預覽**：mouse 的讀取包在 `if (Application.isPlaying)` 裡，Edit 模式下只會停在 `initialYaw`／`initialPitch` 這個起始角度，不會嘗試讀不存在意義的滑鼠 delta。
- 場景裡舊的 `fixedYaw: 15`／`fixedPitch: 0`／`distance: 0.8`／`targetOffset: (0.7, 1, 0)` 這些數值，是使用者在攝影機因為角色座標跑掉而顯示錯亂時，自己嘗試手動調整攝影機留下的實驗值——這次全部重設回合理預設（`distance=1.5`／`targetOffset=(0,1.15,0)`／`initialYaw=0`／`initialPitch=0`），`mouseSensitivity`／`minPitch`／`maxPitch` 也一併設好。
- **連帶發現並修好一個真實的測試隔離 bug**：套用這次改動後跑 PlayMode 測試，`TargetLockControllerTests.cs` 的 3 個測試失敗——原因是它的 `[SetUp]` 沒有比照其他測試檔清空場景根物件，這在之前不是問題（因為當時沒有其他測試會把「真正的 GreyboxTest 場景」留在載入狀態），但這次新增的 `WanderMovementTests`／`CharacterCollisionBlockingTests`／`MovementFrameTimingTests` 都會載入真實場景，如果先跑過，會把 Player2／TrainingDummy 這些帶 `LockOnTarget` 的物件留在場景裡，讓 `TargetLockControllerTests` 自己建的假目標不再是唯一候選，打破了測試對「只有一個候選」的假設。已修正：`TargetLockControllerTests.SetUp()` 比照其他測試檔，先清空場景所有根物件。
- 55 個 EditMode + 33 個 PlayMode 測試全數通過，這次有實際跑 Unity 驗證。
- **仍待你本人確認**：滑鼠視角的靈敏度（`mouseSensitivity=3`）、上下視角限制（`minPitch=-40`／`maxPitch=70`）手感如何，這些都是合理起點猜測，可以直接在 Inspector 調整（拜 `[ExecuteAlways]` 所賜，Edit 模式調整也會存檔）。

## 已修正：滑鼠靈敏度太小＋角色重生位置改到柱子旁邊（2026-08-11）

- **滑鼠靈敏度找到真正原因**：`Mouse.current.delta` 本身就已經是「這一幀滑鼠移動了幾個像素」，是每幀的量，不是每秒的量；上一版寫成 `delta.x * mouseSensitivity * Time.deltaTime` 又多乘了一次 `Time.deltaTime`（60fps 時大約是 0.0167），等於把靈敏度硬生生除了快 60 倍，才會「幅度小到幾乎沒反應」。**已修正**：拿掉 `Time.deltaTime` 那段乘法，`mouseSensitivity` 預設值同步從 `3` 改成 `0.15`（現在單位是「每移動 1 像素轉多少度」，數值意義完全不同，不能沿用舊的 3）。
- **角色重生位置改到訓練假人旁邊**：使用者回報「出生都會在白柱子上面，然後摔下來」。實際檢查 `Player`（`-2.5→改前是 0`, 0.5, `0→改前是 -2`）跟 `TrainingDummy`（0, 0.5, 0）座標，其實一直是相距 2 個單位，沒有真的重疊——但貼身的攝影機距離（`distance=1.5`）加上兩者幾乎同一直線，很容易讓畫面看起來像疊在一起，物理引擎的正常落地校正也可能被誤認成「摔下來」。不糾結「到底哪裡的視覺誤會」，直接照使用者要求把 `Player` 重生點改到 `TrainingDummy` **正側邊**（`(-2.5, 0.5, 0)`，跟假人同一個 Z、差在 X），徹底避免任何「在柱子上面」的觀感疑慮。同步更新 `GreyboxSceneBuilder.cs` 的預設重生點。用算圖確認新位置畫面上清楚分開、沒有重疊。
- 55 個 EditMode + 33 個 PlayMode 測試全數通過，這次有實際跑 Unity 驗證。

## 已變更：改成真正的第一人稱攝影機（`distance=0`），並隱藏角色自己的模型（2026-08-11，使用者要求）

使用者回報：「請固定在角色身上，角色往左看攝影機就往左看，攝影機就像是角色的眼睛，而不是現在這樣移動視角會變第三人稱」——先前即使 `distance` 調得很小（1.5），只要有距離就代表攝影機是「繞著角色軌道環繞」，轉動視角時鏡頭還是會掃過一個弧線，不是「鏡頭焊在眼睛上、只會轉動不會位移」的真第一人稱：

- **原理**：`ComputeCameraPosition` 是 `瞄準點 - 旋轉 × 前方 × distance`，只要 `distance` 不是 0，改變旋轉角度就一定會讓「旋轉 × 前方 × distance」這個位移向量跟著轉，鏡頭因此會繞著瞄準點畫弧——這就是使用者形容的「變第三人稱」的數學原因。**`distance=0` 時這個位移向量恆為零向量，不管怎麼轉都不會影響位置**，鏡頭永遠精確地釘在瞄準點（`targetOffset` 決定的眼睛高度）上，只剩旋轉——這才是「鏡頭就是角色的眼睛」。
- **已修正**：`ThirdPersonCameraController` 的 `distance` 預設值改成 `0`（原本 1.5）。`GreyboxSceneBuilder.cs` 同步更新。
- **連帶處理**：`distance=0` 代表鏡頭现在精確地在角色頭部/眼睛的位置，如果角色自己的 3D 模型還顯示著，會看到自己頭部模型的內側（穿模、一片黑或詭異的貼圖內壁）。這正是專案更早之前「V 鍵切換第一人稱」那個功能被移除前就已經處理過的同一個問題（當時的作法是「第一人稱先隱藏整個角色模型」）——這次直接把 `Player` 底下的 `Visual`（Maya 模型）**永久停用**（不是像當年那樣做成可切換的第一/第三人稱雙模式，因為使用者這次要的就是「固定」第一人稱，不是切換）。`GreyboxSceneBuilder.cs` 重建場景時新產生的 Player 也會自動隱藏 Visual。
- 用算圖確認：鏡頭座標精確落在 `Player 位置 + (0, 1.15, 0)`（例如 Player 在 `(-2.5, 0.5, 0)` 時鏡頭在 `(-2.5, 1.65, 0)`），畫面裡看不到角色自己的身體/頭部，水平線置中（pitch 仍然是水平）。
- 移除了已經過時、被超越兩次的一次性攝影機修正工具 `FixEyeLevelCameraSetup.cs`（訊息裡還留著上上一版「distance 2.2」的過時文字，繼續留著只會誤導之後的自己）。
- 55 個 EditMode + 33 個 PlayMode 測試全數通過，這次有實際跑 Unity 驗證。
- **仍待你本人確認**：真正的第一人稱手感如何（完全看不到自己的手/身體，這是目前刻意的取捨，不是遺漏——如果之後想要「看得到自己的手腳」，那是另一套做法，通常需要額外的第一人稱專用手臂模型，不是單純把 `Visual` 顯示回來就好，因為那樣鏡頭會直接卡在自己的頭部網格裡）。

## 已變更：接受一點點環繞感，把攝影機拉開一點點讓角色露臉（2026-08-11，使用者確認要這個取捨）

使用者問「攝影機不能往後一點，這樣既能保有目前視角控制、又能看到一點角色樣貌嗎」——這是主動確認要接受「有一點點環繞感」換「看得到自己」的取捨（`distance=0` 的說明裡已經先講清楚這個取捨關係）：

- `distance`：`0` → `0.5`。轉動視角（尤其上下看）時鏡頭會有一點點繞著頭部畫弧的感覺，不會像 `0` 那樣完全焊死不動——這是所有「過肩鏡頭」動作遊戲的標準做法，這麼小的距離通常不會太明顯。
- 因為鏡頭不再精確卡在頭部位置（拉開了 0.5 單位），角色自己的 `Visual`（Maya 模型）**重新啟用**，不然使用者會什麼都看不到，白拉開距離沒有意義。
- `GreyboxSceneBuilder.cs` 同步更新（`distance=0.5`，`Visual` 預設可見）。
- **這次沒有跑 Unity 驗證**：你的 Editor 又開著，跑 batchmode 撞到「另一個實例正在使用專案」的錯誤。純數值調整（`distance` 一個 float、`Visual` 的啟用狀態），風險低，但按規則誠實記錄「這次沒驗證」。麻煩存檔讓 Unity 重新讀取後 Play 一次，確認看到的角色樣貌／環繞感的程度是否符合預期，`distance` 可以直接在 Inspector 微調（拜 `[ExecuteAlways]` 所賜）。

## 已修正：視角會慢慢跑掉＝滑鼠沒有鎖定在視窗裡（2026-08-11）

使用者回報改成 `distance=0.5` 後「視角會慢慢跑掉」：

- **原因**：`Mouse.current.delta` 讀的是滑鼠實際物理移動量，不管游標視覺上有沒有跑出遊戲畫面——沒有鎖定游標的情況下，任何滑鼠移動（滑到別的地方點東西、滑鼠雜訊飄移）都會被當成「轉視角」疊加進 `_yaw`／`_pitch`，看起來就像視角自己慢慢跑掉。
- **已修正**：`ThirdPersonCameraController` 新增 `OnEnable`／`OnDisable`，Play 模式時鎖定游標（`Cursor.lockState = CursorLockMode.Locked`）並隱藏（`Cursor.visible = false`），所有滑鼠移動都只拿來轉視角。在 Unity Editor 裡 Play 時按 **Esc** 可以隨時解鎖游標（Editor 內建行為，不需要額外寫程式碼處理）。
- **順便修好一個意外發現的問題**：跑測試時發現 `Player2` 被意外取消勾選（`m_IsActive: 0`）——又是使用者操作 Editor 時不小心關掉的（這是本次對話第三次出現「東西被意外拖動/關閉」的情況了，模式很清楚：在 Scene 視窗／Hierarchy 裡操作時很容易誤觸）。已重新勾選啟用。
- 55 個 EditMode + 32 個 PlayMode 測試通過（1 個 `EnemyAITests.TargetWithinDetectionRange_ChasesTowardTarget` 間歇性失敗，是先前就記錄過的 headless batchmode 計時 flaky 問題，跟這次改動的檔案`無關`——`EnemyAI.cs` 這次完全沒有被動到）。
- **給你的建議**：這幾次都是在 Editor 裡操作時不小心拖動/取消勾選到東西，之後如果只是想「看一眼」場景，盡量用滑鼠中鍵拖曳／滾輪縮放（純導覽，不會動到物件本身），移動工具（左上角箭頭圖示，快捷鍵 W）容易在點擊物件時意外拖動位置，取消勾選物件名稱前的核取方塊也容易手滑點到。

## 已找到並修正：「專案開不起來」＝ Editor 被兩萬多次警告洗到卡死（2026-08-11）

使用者回報專案開不起來。檢查後發現 `Unity.exe` 其實還在跑，但已經沒有回應——`Editor.log` 已經 22 分鐘沒有任何新內容，而且檔案異常肥大（11MB、23 萬行），裡面藏著同一則警告重複了 **26,034 次**：

```
Animator is not playing an AnimatorController
[Assets/_Project/Game/Characters/CharacterAnimatorLink.cs line 46]
```

- **根本原因是我造成的**：`CharacterAnimatorLink.cs` 掛在 `Player` 上（永遠是啟用狀態），每一幀都會對著 `Player > Visual` 底下的 `Animator` 元件呼叫 `SetFloat(...)`。**幾輪前為了做「真正第一人稱」把 `Visual` 停用過**——那段期間只要在 Play 模式移動角色，這個腳本還是會每一幀對著已經停用的 Animator 呼叫 `SetFloat`，Unity 不會報錯但會印一次警告；印警告要抓完整呼叫堆疊，這件事本身不便宜，累積兩萬多次很可能就是把 Editor 拖到沒反應的直接原因。
- **已修正**：`CharacterAnimatorLink.Update()` 判斷條件從單純 `animator == null` 改成 `animator == null || !animator.isActiveAndEnabled`——Animator 所在的物件被停用時直接跳過，不會再對著停用的 Animator 硬呼叫。以後不管 `Visual` 是不是被停用（不管是我改的，還是之後又不小心被誤觸），都不會再洗版。
- **這次沒有跑 Unity 驗證**：`Unity.exe` 目前卡住占用著這個專案，batchmode 進不去。純程式邏輯修正（多加一個條件判斷），改動的既有測試 `CharacterAnimatorLinkTests.cs` 只測純函式 `ComputeSpeedParameter`，沒有測 `Update()` 本身，理論上不受影響，但按規則誠實記錄「這次沒驗證」。
- **請你先處理卡住的 Unity**：打開工作管理員看 `Unity.exe` 是否「沒有回應」，如果是，可能需要強制結束（未存檔的修改會遺失）；重開後我可以幫忙確認場景狀態是否正常。
- **後續**：使用者確認已強制關閉舊的卡住的 `Unity.exe`（PID 92888），已幫忙用命令列重新啟動一個乾淨的新 Editor 實例，使用者確認開啟成功。

## 已新增：空白鍵跳躍＋修正放開移動鍵沒有馬上停止（2026-08-11，使用者要求）

- **跳躍**：`IInputCommand` 新增 `JumpPressed`（跟 `DodgePressed`／`LockOnPressed` 同樣是「按下瞬間」觸發，不是持續按著），`PlayerInputProvider` 綁空白鍵。`CharacterMovement` 新增 `jumpSpeed`（預設 7，粗略對應重力 -20 時跳起約 1.5～2 個單位高，非精確計算，可調）：只有在**貼地**（`_controller.isGrounded`）時按空白鍵才會給一個向上的初速度，空中再按不會有雙跳效果。目前**沒有跳躍動畫**（Maya 素材包本身雖然含 Jump/Fall 動畫，但這次只接物理邏輯，跟先前「三段普攻沒有對應動畫」是同樣的「先求邏輯正確」做法），純物理判定不會出錯，但畫面上角色跳起來時看起來還是播著 Idle/Walk/Run 的 blend tree,不會播放專門的跳躍動作。
  - **連帶的按鍵調整**：空白鍵原本同時綁著「攻擊」（跟滑鼠左鍵重複），這次把空白鍵從攻擊移掉、改綁跳躍，**攻擊維持只用滑鼠左鍵觸發**，功能沒有减少。
  - `IInputCommand` 是共用介面（玩家與 AI 都要實作，比照 `C:\Live2DFighter` 的 `IInputCommand` 模式，這是專案的既有硬性規則），這次新增一個介面成員，`EnemyAI.cs`（永遠回傳 `false`，敵人不會跳）跟全部 9 個測試檔案裡各自的 `StubInputBehaviour` 都要同步補上，已經全部處理。
  - 新增 `JumpTests.cs`（PlayMode，2 測試）：貼地按跳躍會被往上推、空中再按不會疊加二次跳躍。
- **移動放開沒有馬上停止**：`decelerationSmoothTime`（放開移動鍵後的減速緩動時間）從 `0.12` 降到 `0.05`——之前特意調得比加速慢一點（想營造「停下來有一點重量感」），但實際玩起來變成「滑行感太明顯」，這次優先改成幾乎立即停止，仍保留一點點緩動（不是像最一開始那種完全瞬間停止的機械感）。
- 55 個 EditMode + 35 個 PlayMode（新增 2 個跳躍測試）測試全數通過，這次有實際跑 Unity 驗證。
- **仍待你本人確認**：跳躍高度／手感是否合適（`jumpSpeed` 可以直接在 Inspector 調）；放開移動鍵停止的速度現在是否符合預期。

## 待確認

- 本機沒有配置 Unity MCP 或其他可互動的 Editor 自動化工具，本次 Phase 1 全程透過 Unity 命令列 `-batchmode`／`-executeMethod`／`-runTests` 完成，AI 端無法產生「已手動 Play 驗證」的證據，這類驗證一律需要使用者自行操作。
- 手把輸入是否列入垂直切片範圍，尚未決定（`C:\Live2DFighter` 的經驗是手把部分尚未完成測試）。
- **三段普攻沒有對應動畫** → 部分緩解（2026-08-11）：新增 `AttackPoseVisualizer`，玩家（甩右手臂骨骼）與敵人（整個 Capsule 前傾）攻擊時現在會有程式驅動的揮擊角度可看，不再是完全靜止。**但這仍不是正式動畫**——沒有手部/身體的完整連動、沒有揮空氣的空氣感或武器拖尾，之後仍需要找/做適合 Maya 骨架的正式攻擊動畫（CC0 或需授權素材）取代這個佔位方案。
- **攻擊佔位揮擊動作的方向/角度未經人眼確認**（2026-08-11 新增）：`WireAttackPoseVisualizers.cs` 猜測玩家用右手臂骨骼繞局部 Z 軸、敵人用整個 `Visual` 繞局部 X 軸，猜測方向可能是反的或角度看起來奇怪。`AttackPoseVisualizer` Inspector 上的 `invert` 勾選框可以直接反轉、`windUpAngleDegrees`／`swingAngleDegrees` 可以直接調角度大小，都不需要改程式碼。
- **攻擊時未鎖定/減速移動**：目前 `CharacterMovement` 與 `PlayerCombat` 完全獨立，攻擊全程角色仍可自由移動，這跟大多數動作遊戲「攻擊時至少 Startup/Active 期間會停下來或大幅減速」的手感不同，之後視實際 Play 手感決定是否要加上這個耦合。
- ~~閃避的無敵幀還沒接到任何傷害判定~~ → 已解決（2026-08-10），見下方「近戰敵人 AI」項：`Health.IsInvulnerable` 已接上 `CharacterMovement` 的閃避狀態，且有敵人真的會攻擊玩家可以驗證。
- **敵人外觀** → 部分緩解（2026-08-12）：`TrainingDummy` 換上 Quaternius Humanoid CC0 placeholder（見 `EnemyHumanoidVisualSetup.cs`），不再是純白 Capsule，但**沒有動畫**（模型沒有附帶 Animator Controller，貼身跑動/攻擊時仍是 bind pose 靜止不動），之後要嘛找animation clips、要嘛之後就直接被正式敵人美術取代。**只有單一攻擊、數值未經實測**維持原狀：只有一種攻擊、沒有連段，`detectionRange`／`attackRange`／`moveSpeed`／傷害都是合理起步的猜測值，未經實際 Play 手感調整。敵人死亡時 `Health` 只會把 GameObject 停用，沒有任何死亡演出/動畫/音效。
- ~~敵人飄浮在空中~~ → 已解決（2026-08-12）：`CharacterController.height` 曾被改成 `1` 但重生 Y 沒同步調整，跟先前 Player 發生過、已修過的同一種 bug 一模一樣（見 `FixPlayerGroundedSpawn.cs`／`FixEnemyGroundedSpawn.cs`）。**手動拖曳角色本身的 Y 座標救不了這種 bug**，正確的 Y 值必須跟著 `height`/`center` 一起算，這也是為什麼使用者當時調過 Y 還是沒用。
- **閃避與攻擊系統互不干擾，沒有互相打斷的邏輯**：攻擊中可以直接閃避（不會取消攻擊狀態機，兩者各自獨立運作），閃避中按攻擊鍵一樣會照常觸發連段判定，這跟大多數動作遊戲「閃避會取消當前攻擊」或「攻擊中無法閃避」的設計不同，之後視實際手感決定是否需要加上互斥/取消規則。
- ~~第一人稱下攻擊方向跟著移動朝向走，不是跟著視角走~~ → 已解決（2026-08-10），見下方「敵人鎖定」項：鎖定敵人後角色朝向會直接對準目標。**未鎖定時**這個限制依然存在（第一人稱站立不動時攻擊方向仍跟著移動朝向，不是跟著視角），之後如果需要「未鎖定也能瞄準視角方向」要另外處理。
- **鎖定/解鎖沒有平滑過渡**（2026-08-10 新增）：`ThirdPersonCameraController` 進入/離開鎖定狀態時，攝影機的 yaw/pitch 會瞬間跳到新的角度，不是漸進補間過去，鎖定瞬間畫面可能會有明顯的「甩動」感。之後可以在 `ComputeLockOnYawPitch` 與滑鼠視角之間加入插值，這次先求邏輯正確、行為可預期。
- **沒有多目標循環切換**（2026-08-10 新增）：`TargetLockController` 一次只會鎖定「視角範圍內最近的一個」候選，沒有「範圍內有多個敵人時可以切換鎖定對象」的功能（例如搖桿右搖桿/滑鼠滾輪切換）。目前場景裡也只有一個 `TrainingDummy` 可鎖定，等 Step ⑤⑥ 加入多個敵人後才會需要這個功能，屆時再補。
- ~~攝影機仍是俯視固定角度，尚未改成角色視線高度~~ → 已解決（2026-08-11），見上方「攝影機改為角色視線水平高度」項。
- **沒有真正的走/跑速度分層**（2026-08-11 新增）：`CharacterMovement.moveSpeed` 目前只有單一數值，WASD 按下就是同一個目標速度，只是加減速曲線變緩動了（見上方項目），不是真的「走路」與「跑步」兩種節奏。研究報告建議如果要做分層需要額外決定輸入方式（例如額外按鍵切換走/跑，因為左 Shift 已經是閃避鍵），目前不在範圍內。

## GreyboxTest 現況備忘（2026-08-12）

- **場景裡目前沒有敵人**：`TrainingDummy`（Enemy AI，白柱子）已被使用者本人在 Editor 裡手動刪除，經確認是**故意的、不用復原**——不是遺失或 bug，之後如果又看到「怎麼沒有敵人可以打」不用當成問題處理，除非使用者另外要求加回來。`EnemyHumanoidVisualSetup.cs`／`FixEnemyGroundedSpawn.cs` 等相關工具腳本還在，之後要重新加回敵人隨時可以用。因為沒有敵人，`CharacterCollisionBlockingTests.WalkingIntoTrainingDummy_DoesNotFullyOverlap` 現在會 `Assert.Ignore` 跳過，不是失敗。
- **攝影機是自由視角（滑鼠上下左右都能轉）＋ WASD 相對攝影機平移**：2026-08-12 同一天內攝影機設計換過三次——早上的自由視角（`distance=0.8`／`targetOffset=(0,0.5,0)`，使用者原本手動調校值）→ 真第一人稱（`distance=0`）→ 固定右肩視角（相機鎖定角色朝向＋坦克式控制）→ **使用者對右肩視角/坦克控制不滿意，明確要求改回自由視角＋WASD 平移**（參考原神／鳴潮），已改回並確認是目前狀態。相機鎖定角色朝向那個版本已知會跟「WASD 相對攝影機平移＋自動轉向面對移動方向」的邏輯衝突形成無限旋轉迴圈（`CameraRelativeMovementRegressionTests` 抓到），這是以後如果又有人想做「攝影機跟隨角色朝向」類需求時要注意的具體失敗模式——要嘛移動邏輯也要改（坦克式），要嘛攝影機保持獨立，不能兩者同時改一半。`FixCameraToFirstPerson.cs`／`FixCameraToRightShoulder.cs`／`FixCameraToFreeLook.cs` 三支工具都保留在專案裡當歷史記錄。**2026-08-12 稍後追加**：在自由視角之上加了可選的自動回正（`enableAutoCenter`，預設開，滑鼠閒置 0.8 秒＋角色前後移動中才觸發，平滑趨近角色背後）——這不是走回「鏡頭鎖定角色朝向」的回頭路，有延遲閘門＋只在前後移動時觸發（純側移時關閉，因為純側移+自動回正同時開實測會讓角色朝向漂移 134 度，見 `CHANGELOG.md` 同日條目的完整分析），但如果之後又要調整這個功能，記得這個「純側移會跟自動回正打架」的邊界情況。
- ~~這個專案目前完全沒有攝影機碰撞（防穿牆/穿地板/穿角色模型）邏輯~~ → **已修正（2026-08-12）**：`ThirdPersonCameraController` 新增 `enableCameraCollision`（預設開）＋ `Physics.SphereCastAll` 防穿模，撞到東西就把攝影機拉到障礙物前面。細節見下方新條目與 `CHANGELOG.md` 同日條目。
- **`CharacterController.minMoveDistance` 陷阱**：預設值 `0.001` 會靜默丟棄小於這個值的 `Move()` 呼叫，在極高幀率環境（headless batchmode 量到約 9000fps，理論上高更新率螢幕/關閉垂直同步的玩家機器也可能碰到）下 `moveSpeed*deltaTime` 幾乎每幀都低於這個閾值，導致移動大幅變慢甚至幾乎不動。已在 `GreyboxSceneBuilder.cs`（Player／Enemy）跟所有手動建立 `CharacterController` 的 PlayMode 測試裡設成 `0f`。**這次只確認了 Player／Enemy／測試用的 CharacterController，如果之後又新增角色會用到 CharacterController，記得順手設這個值。**
- **`JumpTests.JumpPressed_WhileGrounded_LiftsPlayerUpward` 偶爾會 flaky**（2026-08-12 觀察到）：已經修正過一次明顯的重生高度誤差（原本 0.5，應該配合預設 `CharacterController` 高度 2 是 1.0，已改對），但大約一半機率還是會抓到角色在測試一開始「讓 isGrounded 穩定」的極短等待窗口內意外往下掉超過預期，導致跳躍判定失敗。懷疑是 `isGrounded` 在極高幀率下的邊界情況（第一次 `Move()` 呼叫前 `isGrounded` 語意不明確的已知 Unity 特性），這次沒有繼續深挖根因，值得之後單獨排查；不影響其他測試，重跑一次通常就過。
- **`CharacterCollisionBlockingTests.WalkingIntoPlayer2_DoesNotPassThrough` 也開始 flaky**（2026-08-12 稍後觀察到，同一類問題）：距離斷言原本卡在剛好卡在門檻邊緣（0.797 vs 0.8），放寬到 0.7 之後**下一輪又量到 0.4465**，代表不只是差一點點的量測誤差，是真的偶爾角色會滑進 Player2 碰撞體比較深的地方才被推開，程度不固定。懷疑跟 `JumpTests` 那個 flaky 是同一個根因（極高幀率下 `CharacterController` 的碰撞/推出解算在極端小步長時不穩定），沒有証實也沒有深挖。**不是這次「Maya 飄浮」修正造成的迴歸**——`Player2` 的 `CapsuleCollider`／`WanderMovement`／`LockOnTarget` 三個元件數值都檢查過沒有被動到。之後如果要認真排查，兩個 flaky 測試可以一起看。

## Player4（Arisa 動漫角色，2026-08-12 新增，未經人眼互動確認）

- **算圖用的 batchmode 沒有正確光照，第一次看起來像壞掉其實不是**：用 `-nographics` 跑出的算圖是假的（強制 Null GfxDevice，畫面內容沒有意義，這次踩到過一次，浪費了一輪排查）；拿掉 `-nographics`（保留真正的 GfxDevice）之後才是有意義的算圖。就算用真正的 GfxDevice，近距離特寫算圖角色還是有可能因為剛好站在背光那一面看起來全黑，跟材質本身是否正常無關——判斷角色材質是否正常，要嘛從跟光源同側拍、要嘛直接請使用者本人在互動 Editor 裡 Play 看一次，不要只憑一張角度沒選好的算圖就下結論。
- **Maya 的 `Visual` 底下也有同類「Missing Script」殘留，這次沒有動**：修 Player4 的 Missing Script 殘留時（見 `CHANGELOG.md` 同日條目）順便發現 PlayMode 測試日誌裡還有另一半同樣的警告來自 Maya 自己的 `Visual`（`PlayerMayaVisualSetup.cs` 沒有清這個），推測是同一位作者的套件都有同樣的「自己的 Script/ 資料夾沒被匯入，元件變空殼」現象，這次只清了 Player4 這一份，Maya 那份還留著，不影響功能（純粹是空的元件引用，`Debug.Log` 噴警告而已），之後如果要一起清乾淨可以照 `Player4AnimeVisualSetup.RemoveMissingScripts()` 的做法比照套用到 `PlayerMayaVisualSetup.cs`。
- ~~Player4 目前是純靜態展示…~~ → **已更新（2026-08-12 稍後）**：Player4 已轉為 AI 自主攻擊敵人（`Player4EnemyAISetup.cs`，見下方新條目與 `CHANGELOG.md`），不再是純靜態展示。
- **Player4 的 Idle/Walk/Run 動畫沒有真的接上移動**：`EnemyAI` 直接用 `CharacterController.Move()` 驅動位移，不像 Player 有 `CharacterAnimatorLink` 把速度換算成 Animator 的 Speed 參數——Player4 追擊/攻擊時模型底層還是會維持 Animator 預設狀態（通常是 Idle），不會播放對應的跑步動畫，這跟 `TrainingDummy` 當年（純 Capsule，沒有 Animator 可言）不是同一種限制，是這次沒有處理的範圍，之後如果要接可以參考 `CharacterAnimatorLink.cs`／`WireCharacterAnimatorLink.cs` 的既有模式。

## Player4 轉為 AI 敵人＋鎖定系統改動（2026-08-12，使用者要求，未經人眼互動確認）

- **鎖定鍵改成滑鼠中鍵（滾輪點按）**：`PlayerInputProvider.cs` 沒有對應的自動化測試（它直接讀真實 `Keyboard`/`Mouse` 硬體狀態，這個專案裡所有輸入來源腳本都是同樣沒測試的模式），這次的按鍵改動只能靠使用者本人實際按一次滑鼠中鍵確認有沒有生效，無法用批次模式驗證。
- **鎖定搜索方向改成角色1朝向後的真實手感沒有人眼確認過**：自由視角下常見情境是「鏡頭轉去看敵人，但角色還維持原本移動方向沒有轉過去」——這種情況現在會鎖不到，是否符合預期需要使用者本人 Play 一次判斷；如果覺得太嚴格，未來可以考慮改成攝影機/角色朝向取其一在範圍內即可，或放寬 `maxLockAngleDegrees`（目前 60 度）。
- **Player4 攻擊傷害/frame data 沿用既有的 `EnemyAttack.asset`（5 點傷害），沒有另外針對 Player4 調校**：這個資產是當年給 `TrainingDummy` 用的通用敵人攻擊數值，不是根據 Player4 的角色設定特別設計的，之後如果要讓 Player4 的攻擊有自己的強度/節奏，需要另外建一份 `AttackData`。
- **`EnemyAI` 的偵測/攻擊範圍維持類別預設值（8/2），沒有配合 Player4 的站位另外調校**：Player4 站在 `(5, 0.5, -8)`，跟 Player 預設重生點 `(-2.5, 0.5, 0)` 距離約 11 單位，超過偵測範圍，玩家要主動走近到 8 單位內才會被發現——這是合理的預設行為（不是問題），只是提醒這兩個數字目前是類別本身寫死的預設值，不是特別為這次場景配置調過的。

## 地板／背景景物／天空盒（2026-08-12 新增，未經人眼互動確認）

- `Skybox/Procedural` 是 Unity 內建的 legacy shader，Editor 裡一定找得到（`Shader.Find` 不會失敗），但**還沒加進 Graphics Settings 的「Always Included Shaders」**——正式 Player Build 有 shader stripping，理論上有可能把這個 shader 拿掉導致天空盒變回預設粉紅/黑色。Alpha 前第一次做真正的 Build 時要確認一次。
- Quaternius「Simple Nature Pack」的 13 個道具（Tree/Rock/Bush/Grass）比例、密度、擺放半徑（17–26 單位，邊界牆外）都是**沒有人眼看過的估計值**——`BackgroundSceneryStandeeSetup.cs` 用固定亂數種子灑了 40 個，如果 Play 後發現太密/太疏/大小不對，調整腳本裡的 `PropCount`／`InnerRadius`／`OuterRadius`／scale 範圍常數重跑即可，不用重新設計整個系統。
- 地板貼圖（Poly Haven Stone Floor）目前只接了 Diffuse + Normal，10x10 平鋪的比例是估計值，沒有人眼確認過貼圖有沒有明顯重複感或拉伸；Roughness 貼圖已下載但沒接上材質（見 `ASSET_LICENSES.md`）。
- `BackgroundTerrain`（邊界外的填補地形平面）只是純色、沒有貼圖，跟 `Ground` 的石板貼圖交界處會有明顯的材質分界線，不是無縫地形——這次的目標只是「不要看到空的天空盒直接接到地平線」，不是做出無縫大地形，如果覺得分界太突兀，之後可以考慮讓 `BackgroundTerrain` 也套用同一張或另一張貼圖。
- 整組改動（`GreyboxSceneBuilder.cs` 的 `CreateGround`／`CreateBackgroundTerrain`／`CreateSkybox`，加上新的 `BackgroundSceneryStandeeSetup.cs`）全程只跑過 `-batchmode`（Build 場景 → 加背景景物 → 64 個 EditMode 測試全過），**沒有人眼在互動式 Editor 裡看過實際渲染畫面**，跟這份文件其他視覺類項目一樣需要使用者本人 Play 一次確認。

## 血條 UI（2026-08-12 新增，使用者要求，已用算圖驗證，未經人眼互動確認）

- ~~算圖驗證 Player4 那張圖血條被手臂擋到~~ → **已在同一天稍後修正定位方式**：改成量測 `Visual` 底下 Renderer 的實際世界座標邊界（見下方新條目），不再只靠 `CharacterController` 高度換算，血條現在確實浮在頭頂上方，T-pose 算圖也不再重疊。
- **World Space Canvas 沒有指定 `worldCamera`**：純顯示用途（沒有 `GraphicRaycaster`，不需要點擊互動），不指定也能正常渲染——如果之後要讓血條可以被滑鼠點擊/框選鎖定之類的，需要另外加 `GraphicRaycaster` 並指定 `worldCamera`，這次沒有做。
- **攻擊傷害從遞增連段（8/10/16）跟敵人 5 點，改成統一 10 點**：這是使用者這次「攻擊命中一次扣10滴血」的明確要求，會影響既有的連段強度設計（見 `CHANGELOG.md` 同日條目），不是側面影響——如果之後想要連段遞增手感回來，直接改 `LightAttack1/2/3.asset` 的 `damage` 欄位即可，不用跑任何工具。
- ~~血條大小/邊距（`BarSize=(0.8,0.12)`／`MarginAboveHead=0.25`）是估計值~~ → **已在同一天稍後修正**：使用者回報「血條太低、應該在頭部上方、小一點」，改成 `BarSize=(0.5,0.06)`／`MarginAboveHead=0.15`，位置改用實際 Renderer 邊界量測（不是 CharacterController 高度，兩者對不上——碰撞膠囊 `height=1` 遠比角色模型的視覺高度矮）。已用算圖確認血條清楚浮在頭頂，不再跟身體重疊；大小/邊距本身仍是估計值，沒有人眼在互動 Editor 裡最終確認過，之後如果還想再調，直接改 `HealthBarSetup.cs` 常數重跑即可。

## 已修正：CharacterController 互推會爬到對方頭上，讀起來像「角色消失、畫面定格」（2026-08-12，真實 bug 回報）

使用者回報「一旦我很靠近敵人時，角色1就突然消失了 畫面定格」。用診斷測試重現：Player 用真正的移動輸入走向 Player4，Y 座標從 0.58 在約 1 秒內爬升到 1.66，之後卡在 X≈5 附近來回震盪超過 20 秒——根因是 `CharacterController.stepOffset` 預設值 0.3 讓兩個互推的膠囊體其中一個能沿著對方圓頂表面「爬」上去（Unity 已知陷阱，不是這個專案獨有的邏輯錯誤）。已把 Player／Player4／未來重建的 TrainingDummy 的 `stepOffset` 全部設成 0（`GreyboxSceneBuilder.cs`／`Player4EnemyAISetup.cs`／一次性 `FixCharacterControllerStepOffset.cs`），並新增永久回歸測試 `CharacterCollisionBlockingTests.WalkingIntoPlayer4_DoesNotClimbOnTop`。

- **這個場景目前沒有任何需要 `stepOffset>0` 的地形**：地板是平的，掩體方塊本來就設計成「擋住」不是「踩上去」，`stepOffset=0` 沒有已知副作用；如果之後加入真正的樓梯/矮牆需要角色自動踏上去，才需要重新評估這個值（屆時可能要改成用 `OnControllerColliderHit` 分辨「踩地形」跟「推角色」兩種情況，而不是整體關掉）。
- **順便修正的次要 bug：`PlayerCombat.ResolveActiveHit` 在真正貼身距離會打空**：原本用 `Physics.OverlapSphere` 只在攻擊者正前方「Range 距離處」放一個判定球，貼身距離（遠小於 Range）時判定球會飛過目標打空氣。改用 `Physics.OverlapCapsule` 從攻擊者位置延伸到 Range 距離，涵蓋整個攻擊距離。這個問題在這次 stepOffset 修好、兩個角色能穩定卡在貼身距離之後才會被穩定觸發到——如果沒有先修 stepOffset，這個判定漏洞平常不容易被踩到（正常距離的攻擊本來就沒問題）。
- ~~沒有解決攝影機防穿模~~ → **已在同一天稍後補上**（使用者回報「distance=2 還是會消失」後排查發現真正根因），見下方「攝影機防穿模」條目。

## 攝影機防穿模（2026-08-12 新增，`enableCameraCollision`）

- **`Physics.SphereCastAll` 從角色頭部往攝影機方向偵測，撞到東西就拉近攝影機**：只在 `Application.isPlaying` 時執行（跟滑鼠讀取邏輯一樣，Edit 模式預覽不需要）。過濾掉目標角色自己的碰撞體（`hit.collider.transform.root == target`），避免角色一直「擋住自己的攝影機」。
- **`cameraCollisionRadius=0.2`／`cameraCollisionSkin=0.15` 是合理起點，沒有人眼確認過手感**：如果貼牆/貼角色時攝影機拉得太近（幾乎貼臉）或還是感覺會穿一點點，可以調這兩個值；radius 太大可能會太早觸發（還沒真的貼近牆就開始拉近），太小可能還是會有一點點穿模的視覺瑕疵。
- **只防「攝影機穿模」，沒有處理「角色本身被夾在牆縫/掩體之間」這類情況**：如果玩家被夾在兩個物件中間動彈不得，這是移動/碰撞邏輯的問題，攝影機防穿模不會解決，需要另外處理。
- **測試技巧記錄**：PlayMode 測試裡用 `GameObject.CreatePrimitive` 建立障礙物後，如果同一畫格內就要用 `Physics.SphereCastAll`／`OverlapSphere` 等查詢，記得先設定好 `.position` 再呼叫 `Physics.SyncTransforms()`，否則物件仍在 `CreatePrimitive` 預設的原點，查詢結果會對不上——`ThirdPersonCameraObstructionTests.cs` 第一次跑就踩到這個，量到攝影機距離變成 0（誤以為障礙物幾乎黏在攝影機起點上）。
- **這次攝影機防穿模其實不是「角色消失」的根因**：真正根因是 Player 死亡沒有處理（見下方「玩家死亡與重生」條目），攝影機防穿模本身沒有問題，只是排查過程中先做的一個真實但不完整的修正，值得保留。

## 玩家死亡與重生（2026-08-12 新增，`PlayerRespawnController`）

- **`Health.ApplyDamage` 血量歸零時本來就會 `gameObject.SetActive(false)`，這是所有用 `Health` 的角色共用的既有行為，這次沒有改**：對敵人來說「死亡＝關掉」是合理的預設（維持「被打倒」的狀態），但 Player 沒有任何額外處理的話，關掉整個 GameObject 會讓掛在它身上的 `CharacterMovement`／`PlayerInputProvider` 全部停止運作，玩家會覺得「按什麼都沒反應，畫面卡住」——這正是 2026-08-12 稍早那個「角色消失、畫面凍結」bug 報告的真正根因（不是攝影機穿模、不是碰撞爬牆，是死亡沒有處理；那兩個之前修的東西都是真的 bug，但都不是這次「消失」的最終原因）。
- **`PlayerRespawnController` 必須掛在 Player 以外的物件上（這次是新建的 `GameManager`）**：`Health.ApplyDamage` 是先 `Died?.Invoke()` 再緊接著同一行程式碼 `gameObject.SetActive(false)`——如果重生邏輯（`StartCoroutine`）是從掛在 Player 自己身上的元件啟動，Player 被關掉的瞬間 Unity 會直接砍掉那個 GameObject 上所有正在跑的 Coroutine，重生永遠不會真的執行到。之後如果有其他「角色死亡後要做什麼」的需求（掉落道具、播放死亡特效等），切記不能掛在會被 `SetActive(false)` 的那個 GameObject 上。
- **不要用 `OnEnable()` 訂閱 `Health.Died` 事件，用輪詢 `Health.IsDead`**：`OnEnable()` 在 `AddComponent()` 當下就會同步執行，但編輯器工具（`SerializedObject`）跟測試（reflection）都是在 `AddComponent()` **之後**才設定欄位參照，代表 `OnEnable()` 訂閱事件的當下，要訂閱的目標物件參照還是 null，訂閱永遠訂閱不到——這正是這個專案已經多次踩過的「resolved on every use，不要在 Awake/OnEnable 快取」模式（`CharacterMovement.InputCommand`／`PlayerCombat.InputCommand` 等既有寫法都刻意避開這個陷阱），這次是新元件第一次沒注意到，被 `PlayerRespawnControllerTests` 直接抓到。
- ~~重生延遲 0.5 秒是隨手選的合理起點~~ → **已依使用者要求改成 5 秒（2026-08-12 稍後）**：改欄位的 C# 類別預設值不會回頭更新場景裡已經存在的元件資料（這個欄位在第一次 `AddComponent` 當下就把數值序列化進場景了），一定要重跑 `PlayerRespawnSetup.Apply()` 才會真的生效——跟 `CharacterController.stepOffset`／`height` 那次的教訓一樣，之後改 `PlayerRespawnController` 任何欄位預設值，記得同步重跑這支工具。
- ~~只有角色1（Player）有重生，Player4／未來的敵人沒有~~ → **2026-08-13 追加 Player2 重生**，見下方「Player2 死亡後重生」條目；Player4／敵人依然維持原本的「關掉＝被打倒」語意，沒有要求敵人也要重生。
- ~~血條「不會扣血」的回報排查結論：底層邏輯是對的，很可能是使用者 Editor 的舊 Play 階段狀態~~ → **這個結論是錯的，後來使用者更正說明後找到真正根因**：`Image.Type.Filled` 沒有指定 `Sprite` 的話，`fillAmount` 這個數值完全不會影響畫面渲染（數值本身照樣正常更新，只是不影響畫面）——這正是為什麼當時只讀 `fillAmount` 屬性驗證「看起來一切正常」，卻抓不到真正問題的原因。**教訓：UI 相關的 bug 排查不能只驗證程式碼裡的數值屬性，一定要實際截圖比對渲染出來的畫面**，兩者是不同的事。真正修法跟驗證過程見下方新條目／`CHANGELOG.md` 同日條目。

## 已修正：`Image.Type.Filled` 沒接 Sprite，血條畫面完全不會隨傷害縮短（2026-08-12，真實 bug 回報）

`HealthBarSetup.CreateStretchedImage` 建立 Fill／Background 兩張 `Image` 時，一直沒有指定任何 `Sprite`——`Image.Type.Filled` 在沒有 Sprite 的情況下，`fillAmount` 這個數值完全不影響畫面渲染（Unity 產生「填充到一半的網格」需要實際的 Sprite 資料才能算，沒有就固定畫滿格矩形）。這個 bug 很陰險：`fillAmount` 屬性本身照樣正確更新，只是完全不反映在畫面上，所以任何只讀程式碼屬性（不看實際渲染畫面）的驗證方式都會誤判「沒問題」。

- **用真正 Play 模式截圖比對才抓到**：先用編輯器靜態指令截圖，一度誤判修好了/沒修好——後來發現編輯器模式下手動呼叫 `camera.Render()` 不會觸發 Unity 的 Canvas 幾何重建，截圖可能是舊幾何，不能用來驗證 UI 渲染問題。改成在真正的 PlayMode 測試（`[UnityTest]`，`Application.isPlaying == true`）裡截圖，滿血跟 50% 血量兩張圖確實不一樣（一開始是同一張圖，說明真的沒有視覺變化），才確認抓到真正根因。
- **修法**：`image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd")`——Unity 內建的預設 UI 圖片，跟「GameObject > UI > Image」選單自動幫你接好的是同一個，不需要準備美術素材。
- **副作用：血條外觀從方形變成圓角橢圓形**（因為這個內建 Sprite 本身是圓角矩形素材）——沒有人眼確認過這個造型是否可以接受，如果想要方形血條，之後可以換成自己準備的純白方形貼圖。
- **新增的測試斷言**：`WorldSpaceHealthBarTests` 的 `AssertHasWiredHealthBar` 補上 `Assert.IsNotNull(fillImage.sprite, ...)`——這是唯一真正能抓到這類 bug 的斷言，光讀 `fillAmount` 數值不夠。
- **教訓，之後任何 `Image.Type.Filled` 用途都要記得**：一定要指定 `Sprite`，不能只設定顏色跟 fillAmount 就以為完成了。

## 攻擊命中特效（2026-08-12 新增，`HitEffectSetup.cs`）

- ~~兩隻角色（Maya／Arisa）都沒有真的攻擊動作~~ → **已在同一天稍後解決**：從 Mixamo 下載 3 個免費攻擊動畫（Cross Punch/Hook Punch/Uppercut），用 Humanoid Retargeting 套到兩隻角色共用，取代了 `AttackPoseVisualizer`。細節見下方「攻擊動畫（Mixamo）」條目與 `CHANGELOG.md` 同日條目。
- **粒子外觀是方塊狀，不是柔邊圓形火花**：`HitEffectSetup.cs` 建立的材質沒有接任何貼圖，粒子渲染器用預設方形網格——能正常運作，但視覺上比較陽春。之後如果想要更精緻的「火花」效果，需要另外準備一張圓形/放射狀漸層貼圖接到 `Assets/_Project/VFX/HitEffect.mat`。
- **`AttackResolver.ResolveHits` 的回傳型別改了（`int` → `List<Vector3>`）**：如果之後又有其他程式呼叫這個方法，注意回傳值已經不是命中數，要用 `.Count`。
- **測試技巧記錄**：① `ComboAttackState` 就算 `startupFrames`/`activeFrames` 都設 0，一次攻擊判定觸發也至少需要 3 次 `Update()` tick（Idle→Startup→Active→resolve 各佔一次），只等 1 幀會誤判「沒有命中」；② PlayMode 測試裡如果把「模板」物件（拿來當 `Instantiate` 來源的那個）設成 `SetActive(false)`，複製出來的實例會**繼承同樣的停用狀態**，`Object.FindObjectsByType` 預設不找停用物件，會誤以為沒有生成任何東西——這兩個都在這次寫測試時踩到過，不是真的程式碼 bug。

## 攻擊動畫（Mixamo，2026-08-12 新增，取代 AttackPoseVisualizer）

- **動畫時長跟 frame data 沒有互相對齊**：`AttackData` 的 startup/active/recovery 格數是原本為 `AttackPoseVisualizer` 這種程式驅動揮擊調的數字，跟 Mixamo 動畫片段實際的播放長度是兩組獨立的數字，沒有互相校準過——命中判定生效的瞬間，畫面上動畫播到哪個姿勢是巧合，不保證剛好是「拳頭碰到目標」的畫面。堪用但不夠精準，之後如果要精修可以考慮：用 Animation Event 在動畫真正揮拳的那一幀觸發判定，或量測動畫實際長度回頭調整 frame data 的格數。
- **`PlayMode` 測試組件（`Live2DAction.PlayModeTests.asmdef`）沒有引用 `UnityEditor`**：這是刻意的（讓這個測試組件理論上可以被打包進玩家版本），代表 PlayMode 測試裡不能用 `UnityEditor.Animations.AnimatorController` 之類的 Editor-only API 建立測試用的假 Animator Controller——要嘛改用真實場景既有的 Animator（`SceneManager.LoadScene("GreyboxTest")`），要嘛把這類測試搬到 EditMode。這次寫 `CharacterAttackAnimationLinkIntegrationTests` 時第一版直接建假 Controller，編譯失敗才發現這個限制。
- **Maya 材質在算圖截圖裡看起來偏白/曝光過度**：驗證攻擊動畫時拍的截圖裡 Maya 的貼圖顏色不太對，懷疑是診斷相機角度撞到順光的問題（這次完全沒有動過 Maya 的任何材質，只動了 Animator Controller），沒有進一步深挖——如果使用者實際 Play 時也看到材質不對，才需要回頭排查，目前先當作算圖角度問題處理。
- **只有連段前 3 段有真的動畫，兩隻角色共用同一組**：Cross Punch/Hook Punch/Uppercut 直接照 Mixamo 原始節奏播放，沒有為這個專案的角色/武器重新調校過姿勢、速度或誇張程度；兩隻角色的體型/身高不完全一樣，Retargeting 出來的動作幅度/落地位置可能有些微差異，沒有人眼逐一確認過。
- **Adobe 帳號的兩步驟驗證提示被跳過（Remind me later）**：下載動畫時登入流程跳出「啟用兩步驟驗證」的提示，選擇跳過而不是幫使用者決定要不要開啟，這個帳號安全設定沒有被改動，如果使用者想要開啟，需要自己去 Adobe 帳號設定頁面操作。

## Player2 血條與受擊（2026-08-13 新增）

- ~~Player2 死亡後跟 Player4 一樣直接關掉，沒有重生~~ → **2026-08-13 稍後依使用者要求追加重生**，見下方「Player2 死亡後重生」條目。
- **`HealthBarSetup.AddHealthBar` 從 `private` 改成 `internal`**：如果之後又有新角色要加血條，可以直接呼叫這個方法（同組件內），不用重寫一份。

## Player2 死亡後重生（2026-08-13 新增，`PlayerRespawnController` 更名為 `RespawnController`）

- **元件從 `PlayerRespawnController` 更名為 `RespawnController`，欄位 `player`/`playerHealth` 改成 `target`/`targetHealth`**：邏輯本身完全沒有 Player 專屬的內容（只需要一個 `GameObject` 目標＋它的 `Health`），所以沒有另外複製一份給 Player2，而是重用同一個類別。**改名時務必用 `mv` 同時搬動 `.cs` 跟 `.cs.meta`（保留原本 GUID）**——如果直接寫一個新檔案取代，Unity 會發新的 GUID，`GameManager` 上既有的元件參照就會變成「Missing Script」（跟先前 Player4/Arisa 那次「Missing Script」問題同一類根因）。
- **`GameManager` 現在同時掛兩個 `RespawnController`**：一個接 Player（原本就有，5 秒延遲原地復活），一個接 Player2（新增，同樣 5 秒延遲原地復活）。兩個元件是分開的 component 實例，各自只認自己 `target` 欄位指到的角色——`Player2RespawnSetup.cs` 接線時特別用「找到已經指向 Player2 的那個 `RespawnController` 才更新，否則才新增」的邏輯，避免重複執行時在 `GameManager` 上疊加出第二個指向同一角色的元件。
- **視覺驗證的侷限**：想截圖驗證 Player2 死亡→復活的畫面變化，但場景主攝影機是綁定 Player 的第三人稱攝影機，不會轉向 Player2，三張時間點的截圖看起來完全一樣（鏡頭根本沒對著 Player2），沒有參考價值。改成用 PlayMode 測試斷言（`activeSelf` 變化、`CurrentHealth` 是否回滿）在真實載入的 `GreyboxTest` 場景裡驗證，一樣具有說服力，只是不是「肉眼看畫面」的形式。之後如果真的需要視覺驗證多角色場景的個別狀態，可能需要另外開一台不受攝影機跟隨影響的診斷攝影機。

## 已修正：Player 復活失效（2026-08-13，同日更名 `RespawnController` 造成的真實回歸）

使用者回報「現在角色1不會復活」。根因：更名欄位（`player`→`target`、`playerHealth`→`targetHealth`）讓 Player **原本已存在**的元件實例的資料變成孤兒（Unity 照欄位名稱序列化，舊資料留在舊欄位名稱下，新欄位名稱從沒被序列化過，直接是 `null`）——`RespawnController.Update()` 開頭 `targetHealth == null` 就直接 `return`，Player 死亡後這個元件形同虛設。

- **改欄位名稱／類別名稱，光「記得要重跑接線工具」是不夠的**：這次重跑 `PlayerRespawnSetup.Apply()` 修 Player 的當下才發現，工具本身的比對邏輯（找 `target` 剛好等於目標角色的元件，找不到就新增）根本沒有考慮「有一個孤兒元件、`target` 是 `null`」的情況，於是又在 `GameManager` 上新增了一個元件，而不是修好原本那個——`GameManager` 一度同時掛了 3 個 `RespawnController`（1 個永久失效的孤兒 + 2 個正確的）。**教訓：任何「找現有元件、找不到才新增」的接線工具邏輯，都要順便處理「欄位被改名導致的孤兒資料」這種情況，否則會一直疊加壞掉的元件而不自知**（症狀是元件數量比預期多，不是報錯，很容易被忽略）。
- **修法**：`PlayerRespawnSetup.cs`／`Player2RespawnSetup.cs` 的比對邏輯改成三段式——先找精準比對（`target` 等於目標角色）；找不到就回收孤兒（`target == null`）重新接線；兩者都沒有才真的新增。新增 `RespawnControllerCleanup.cs`（`Tools/Live2DAction/Remove Orphaned Respawn Controllers`）把這次已經產生的孤兒元件清掉。
- **新增的測試才是真正抓到這類 bug 的關鍵**：`RespawnControllerSceneWiringTests`（PlayMode）直接載入真實 `GreyboxTest` 場景，斷言 `GameManager` 上剛好有 2 個 `RespawnController`、且分別正確指向 Player／Player2 的 `Health`。既有的 `PlayerRespawnControllerTests`／`Player2RespawnControllerTests` 全程都過，因為它們是「建立全新元件、用 reflection 直接設欄位」的單元測試，完全不會碰到「場景裡已經存在、資料可能過期」的元件——這正是為什麼 `WorldSpaceHealthBarTests` 已經有的「載入真實場景檢查既有元件」這種測試模式很重要，之後任何會被 Editor 工具寫進場景、可能因為改欄位名稱而過期的元件，都應該比照辦理加一個場景層級的接線驗證測試。

## 鎖定目標改用鏡頭朝向判斷（2026-08-13，反轉 2026-08-12 的決定）

使用者要求「目前鎖定目標需要角色去面對敵人，能不能改為鼠標鏡頭面相來判斷?」——這剛好是 2026-08-12 當時明確要求的相反方向（見 `GreyboxSceneBuilder.cs` 該處註解的歷史）。

- **`TargetLockController` 本身完全不用改**：`viewOrigin` 欄位本來就設計成可以指到任意 Transform（`FindTarget()` 只讀 `viewOrigin.forward`，沒設才退回 `transform.forward`），只是場景裡一直接的是 Player 自己的 Transform。改成接到 Main Camera 的 Transform 就好——攝影機的旋轉本來就是 `ThirdPersonCameraController.LateUpdate()` 每幀同步滑鼠拖出來的 `_yaw`/`_pitch`，天生就是「鏡頭朝向」。**教訓：接線類欄位設計成可替換的來源時，之後改需求常常只是換一條線，不用重寫邏輯**——這正是當初把 `viewOrigin` 設計成獨立欄位（而不是寫死用 `transform.forward`）的價值所在。
- **有一幀的執行順序落後，理論上感受不到**：`TargetLockController.Update()` 在同一幀裡比 `ThirdPersonCameraController.LateUpdate()` 先執行，所以按下鎖定鍵當下讀到的攝影機朝向，是上一幀 `LateUpdate()` 算出來的（差一影格，約 16ms），不是滑鼠移動到當下這一瞬間的角度。這在毫秒等級應該感受不到，但如果之後有人回報「鎖定判定好像差一點點」，這是已知的可能原因，不是新 bug。
- **`GreyboxSceneBuilder.cs` 跟已建好的場景是兩條分開的路**：`Build()` 只在從零建置整個場景時才會執行，這次同步更新了它的接線程式碼（供之後重建用），但要套用到「已經建好、之後只做增量修改」的現有場景，仍然要另外寫一個小型 Editor 工具（`LockOnViewSourceSetup.cs`）——跟這個 session 之前每一次場景異動的模式一致，`Build()` 本身不會、也不該在專案中期被重新整個跑一次。

## 已修正：敵人攻擊距離加長後「沒有被隔空打到」（2026-08-13，`AttackData.Range` 跟 `EnemyAI.attackRange` 是兩個獨立欄位）

使用者自己把 `EnemyAttack.asset` 的 `Range` 調到 7.5（原本 1.5，約5倍），實測後回報沒有感受到遠距離被打到。

- **根因**：`AttackData.Range` 只決定「攻擊判定膠囊能打多遠」，不影響 AI 什麼時候**願意**出手——那是 `EnemyAI.attackRange` 的事，兩者是完全獨立、互不連動的欄位。Player4 場景上的 `attackRange` 停在類別預設值 2，所以不管 `Range` 調多長，Player4 永遠得先走到距離 2 以內才會進入 `EnemyState.Attacking`，等於長距離攻擊的「長」完全沒被用到。**教訓：任何「A 欄位決定判定範圍、B 欄位決定 AI 何時觸發」這種拆成兩處的設計，只調 A 很容易調完看不到效果卻不知道為什麼——這類耦合欄位最好有工具或測試把兩者的一致性綁在一起，不要指望使用者自己記得兩處都要改。**
- **修法**：新增 `EnemyAttackRangeSync.cs`，動態讀 `EnemyAttack.asset` 目前的 `Range` 值來設定 `attackRange`（`Range - 0.5` 緩衝），不寫死數字——以後 `Range` 再怎麼調，重跑這支工具就會自動同步，不需要每次都手動換算。同時檢查 `detectionRange`（AI 開始注意到玩家的距離）有沒有小於新算出來的 `attackRange`，太小會一起補上去，否則 AI 會因為根本沒發現玩家而永遠不會進入攻擊狀態，等於這次的調整完全沒用。
- **新增 `EnemyAttackRangeSceneTests.cs`**：載入真實場景，讓玩家站在距離 Player4 5 個單位遠處，驗證 Player4 不用先走近就能命中——這種「兩個獨立欄位需要保持某種數值關係」的 bug，只測試單一欄位的單元測試完全測不到，一定要像這樣在真實場景裡驗證兩者實際搭配起來的效果。

## 角色碰撞體總體檢（2026-08-13，確認沒有角色漏掉碰撞體）

實際掃描 `GreyboxTest` 場景所有根物件的碰撞體/`Health` 元件，確認：

- **戰鬥相關角色都正確套用**：Player（`CharacterController`, radius 0.5）、Player4（`CharacterController`, radius 0.5）、Player2（`CapsuleCollider`, radius 0.6）都各自有 `Health`，攻擊判定（`Physics.OverlapCapsule` + `AttackResolver`）都打得到。
- **076/077 Live2D 立牌、FemaleStandee 刻意沒有碰撞體**：這三個是純視覺展示物件（見 `Docs/DEVELOPMENT_ROADMAP.md` Phase 2 相關條目），從來沒有接過戰鬥邏輯，沒有碰撞體是設計上的預期行為，不是遺漏。
- **檢查方式的小技巧**：批次模式下用 `EditorSceneManager.OpenScene` 打開場景、`Scene.GetRootGameObjects()` 掃描一輪印出每個物件的碰撞體型別/`Health`，比逐一手動點開 Inspector 檢查快很多，之後如果又要做類似的「全場景元件盤點」，可以直接照這個模式寫一個暫時性的診斷工具（不用 `SaveScene`，純讀取不會動到場景資料，跑完直接刪除即可）。

## 攻擊距離跟動畫視覺長度脫節、Player4 沒有復活（2026-08-13）

使用者實際玩過 `Range=7.5` 的長距離攻擊設定後回報兩件事：「敵人離我離得很遠就開始原地揮拳」、「發現敵人死了不會復活」。

- **判定距離跟動畫視覺長度是兩回事，這是這次調參數才真正暴露出來的**：`Physics.OverlapCapsule` 的判定範圍完全不受動畫播放內容影響——揮拳動畫再怎麼播，看起來就是一拳的長度，但 `Range` 決定的判定膠囊可以延伸到動畫視覺完全搆不到的地方。原本 1.5 的時候兩者差距不明顯，放大到 7.5（5倍）之後違和感就非常明顯：站在遠處揮拳、動畫沒有變化，卻真的打中人。**教訓：純數值判定（沒有搭配對應的視覺/動畫）放大倍數要克制，或是要同時設計「這個判定距離對應的招式應該長什麼樣子」（例如突進攻擊、遠程特效），不能只把數字調大。** 這次選擇縮小回 3 倍（`Range=4.5`），比較貼近拳擊動畫本身還算合理的延伸感；已知限制：即使 4.5 也已經比動畫視覺長度長一截，只是沒有 7.5 那麼誇張，之後如果要做真正「看起來合理」的長距離攻擊，需要搭配專屬的突進/特效動畫，不是單純調數字能解決的。
- **Player4 原本的「打倒＝永久消失」是舊決定，這次改了**：先前明確記錄過「只有 Player／Player2 會重生，Player4／敵人維持原本語意」（見上方 Player2 死亡後重生條目），這次使用者主動要求一致性，加上 `Player4RespawnSetup.cs`，Player4 現在跟 Player／Player2 一樣，死亡 5 秒後原地滿血復活。**這代表 Player4 現在會無限復活，不會再有「打死敵人後永久清空」的結局狀態**——如果之後要做「擊敗所有敵人才能過關」之類的關卡設計，需要另外處理，不能只看敵人是否存活來判斷。

## 待確認：`EnemyAttack.asset` 又跟場景的 `attackRange` 不同步（2026-08-13，非程式碼問題）

加 Gizmo 功能時跑完整測試，發現 `EnemyAttackRangeSceneTests` 失敗——追查後發現 `EnemyAttack.asset` 又被改動（`range` 目前是 1.5，跟這次談好的 3 倍 4.5 不一樣），但場景裡 Player4 的 `EnemyAI.attackRange` 還停在上次同步的 4，沒有跟著更新。兩者不同步的後果這次剛好反過來：AI 判斷「距離 4 以內就夠近了」開始攻擊，但攻擊判定膠囊其實只有 1.5 那麼長，Player4 得一路追到接近貼身距離（測試量到 1.497）才真的打中——雖然最後還是打得到（跟原本那次「永遠打不到」不同），但「長距離攻擊」的設計意圖完全沒了。

- **這不是新 bug，是同一個「兩個獨立欄位需要手動保持同步」的已知限制又發生了一次**：只要有人（使用者在 Editor 裡，或之後任何人）改了 `EnemyAttack.asset` 的 `Range` 卻沒有跟著重跑 `Tools/Live2DAction/Sync Player4 Attack Range To EnemyAttack Data`，這個不同步狀態就會一直出現。`EnemyAttackRangeSceneTests` 存在的目的就是要在這種情況發生時讓測試紅燈，而不是靜默地讓遊戲行為跟預期不符——這次測試確實抓到了，算是正常運作。
- **待使用者確認**：目前 `range` 已經又變成 1（`radius` 也是 1，比這則條目寫的時候更小），是要保留，還是要我重新套用先前談好的 `Range=4.5`／`Radius=1` 並重跑同步工具。在確認之前不會自動覆蓋使用者在 Editor 裡的最新調整。**2026-08-13 追加**：`EnemyAI` 現在會直接從 `AttackData` 即時算出有效攻擊距離（`Range+Radius`，見下方「視覺呈現跟實際攻擊判定不一致」條目），不再需要手動重跑同步工具——但 `Range` 本身太小這件事依然是獨立的設計數值問題，不會因為這次架構修正而自動解決，還是要使用者決定最終想要的攻擊距離。

## 攻擊範圍 Gizmo 四輪迭代＋一次批次模式環境卡住（2026-08-13）

使用者對攻擊距離 Gizmo 連續給了四輪真實回饋，每輪都真的改到東西：① 兩顆線框球疊在一起看不清邊界 → 拿掉一顆；② 改完的實心球太大遮擋線條 → 縮小成線框圈＋小實心點；③ 靜態線框還是要肉眼估、看不出「究竟有沒有進入範圍」→ 改成即時跑 `Physics.OverlapCapsule` 查詢；④ 用來表示「有進入範圍」的全尺寸實心球又把站在裡面的角色整個包住看不見 → 徹底改成不填滿任何形狀，邊界永遠只用線框圓＋小參考點，「有沒有進入範圍」改成用邊界線本身變色＋疊圈模擬變粗來表示。**教訓：Gizmo／UI 這類視覺呈現的設計，光憑文字描述猜測『使用者想要的效果』很容易一次只改對一半，需要使用者實際看過（最好附截圖）才會知道真正的問題出在哪，這次四輪迭代都是靠使用者具體回饋才收斂到「線框邊界＋動態變色，永不填滿」這個真正解法，不是一次就猜對。特別是第③輪跟第④輪之間——「加上動態偵測」跟「絕對不能填滿任何東西」原來是兩個同時成立、但一開始沒有想到會衝突的需求，兩者合起來才是使用者真正要的效果。**

- **`Physics.OverlapCapsule` 在 Edit 模式（沒有按 Play）也能正常查詢**：因為它是純幾何查詢，只要場景裡碰撞體資料存在就能用，不需要 Physics 模擬在跑——這讓 Gizmo 可以在完全不進 Play 模式的情況下就準確反映「這個攻擊現在打不打得到」，是這次動態偵測方案能成立的關鍵前提。
- **驗證這個新邏輯時遇到一次批次模式環境卡住**：新增暫時性診斷測試（reflection 呼叫私有方法）後，連續 3 次跑批次模式都卡在 Unity 啟動流程的 `TrimDiskCacheJob` 那一步（不是 `LogAssemblyErrors`／編譯錯誤，是 Unity 自己的啟動階段卡住），強制關閉＋清 lock 檔重試都沒用；但同一批次的 EditMode 編譯檢查／`CombatPlayModeTests` 在這之前跟之後都各自正常跑完過，判斷是暫時性環境問題（懷疑但沒有深挖：可能跟使用者剛關閉互動 Editor、`Temp/__Backupscenes/0.backup` 殘留，或當下作業系統層級的磁碟/裝置掃描有關），不是這次程式碼改動的問題。最終放棄這個額外的自動化驗證，改依賴「邏輯直接複用 `ResolveActiveHit` 已經被大量測試覆蓋的同一套查詢方式」這個事實作為信心來源，沒有為了跑一個裝飾性的額外驗證持續跟環境卡頓奮戰。

## 已修正：Gizmo 視覺呈現跟 Player4 實際攻擊判定不一致（2026-08-13，真實邏輯 bug）

使用者回報「我已經盡到敵人範圍內，線條從紅色變成黃色，但敵人尚未作出攻擊，這代表視覺呈現與數值邏輯判定很明顯不一致」——這次 Gizmo 不是畫錯，是真的暴露出一個既有的遊戲邏輯 bug。

- **根因是兩套完全不同的幾何判斷各自獨立存在**：Gizmo／`ResolveActiveHit` 用 `Physics.OverlapCapsule`（膠囊，真正可達距離是 `Range+Radius`，因為膠囊遠端本身是一顆球，會比 `Range` 那個點再多伸出 `Radius`）；`EnemyAI` 自己決定要不要攻擊卻是單純的全方向球體距離判斷（`Vector3.Distance <= attackRange`），`attackRange` 是另一個要手動同步的獨立欄位。這已經不是第一次因為「兩個獨立欄位要手動保持同步」出包（`EnemyAttackRangeSync.cs` 就是上一次修這類問題留下的產物），但這次不只是數字沒同步，是**判斷用的形狀本身就不一樣**（球 vs. 膠囊），就算數字同步了，非正前方接近時還是可能不一致。
- **這次是架構性修正，不是再同步一次數字**：`EnemyAI` 新增可選的 `combat` 欄位，接上後每一幀直接從 `PlayerCombat.PrimaryAttack`（新增的公開屬性）即時算出 `Range+Radius` 當作攻擊距離，不再有獨立的、會過期的 `attackRange` 數字（沒接 `combat` 時退回原本欄位，向下相容，不影響既有測試）。**教訓：發現「兩個地方各自算同一件事」的程式碼時，比起持續手動同步兩者的數值，更徹底的做法是讓其中一個直接讀另一個的真實資料源，從根本上讓兩者不可能不同步。**
- **修完後意外多修好兩個既有測試**（`Player4EnemyIntegrationTests`／`WorldSpaceHealthBarTests.PlayerBar_UpdatesWhenPlayer4DamagesPlayer_InRealScene`）——這兩個原本因為場景裡 `attackRange` 過小而默默失敗，不是這次新增的測試，是同一個根因的受害者，修好後自然一起復原。這也說明了「原因不明的既有測試失敗」有時候值得回頭追查根因，而不是每次都歸類成「已知 flaky」。

## 已修正：AI 用 `SkinnedMeshRenderer.bounds` 檢查「腳有沒有踩在地板上」，Edit Mode 下腳本查詢會拿到過期快取值（2026-08-25）

使用者回報屁孩王（`BossStateMachine`）腳跟穿地，AI 順著查下去，中間多次回報「已確認貼地、已存檔」，使用者卻一再回報「屁孩王和玩家的腳還是沒踩在地上」——最後才發現不是修法沒生效，是**驗證方法本身在騙人**，值得完整記錄排查過程，避免以後又用同一個不可靠的方法自我催眠。

- **症狀**：一開始只是屁孩王待機動畫腳跟穿地約 1.3cm，順手查了全部角色，發現多人都有幾公分到十幾公分的貼地誤差，逐一用 `Visual` 子物件位移修正、每次都用 `visual.GetComponentsInChildren<Renderer>().bounds.min.y` 量測 + 跟地板 Raycast 比對，數字都收斂到 0.0000，存檔。使用者測完卻回報屁孩王和 Player 還是浮空，AI 再查一次同一組數字，還是回報「gap=0.0000，已確認」——兩邊各執一詞，逼著往下深挖驗證方法本身。
- **真正根因：`SkinnedMeshRenderer.bounds` 在 Edit Mode 下用腳本連續查詢，回傳的是上一次「真正的 GPU 蒙皮渲染」發生時的舊快取值，不會因為腳本呼叫 `clip.SampleAnimation()`、`Animator.Rebind()`，或直接修改 `Transform.localPosition` 而立刻重算**。骨骼的 `Transform.position` 本身確實會被這些呼叫即時、正確地更新（用骨骼位置量測腳趾骨頭高度是可信的），但 `Renderer.bounds` 沒有跟著同步，而且這個過期值在沒有觸發新的一次真實渲染之前，會**穩定地回傳同一個錯誤數字**——連續查詢 60 次、強制 `SceneView.RepaintAll()`、甚至完整 `EditorSceneManager.OpenScene()` 重新載入場景檔，量到的都還是同一個過期值，表面上看起來「數值穩定、可重現」，實際上只是「同一個過期快取穩定地騙你」，非常容易誤判成「已經驗證過、沒問題」。
- **這個過期快取甚至被存進場景檔裡**：`SaveScene()` 存的是 Transform 的真實序列化資料（骨骼/物件的實際座標），不是 `Renderer.bounds`——但因為 AI 拿去驗證存檔前後是否正確的工具本身（`.bounds`）是過期的，即使場景檔案裡骨骼實際上已經跑掉（例如被中途一次 `SampleAnimation()` 迴圈污染成別的姿勢），AI 自己的驗證讀數仍然顯示「正確」，導致把錯誤姿勢原封不動存進場景檔，且完全沒發現。
- **正確做法**：改用 `SkinnedMeshRenderer.BakeMesh(Mesh, bool)`——這個 API 會強制在 CPU 端即時重新計算「當下骨骼姿勢」的實際蒙皮頂點資料，不依賴任何 GPU 渲染或快取，量出來的世界座標永遠反映呼叫當下的真實姿勢。用這個重新複查全部角色，才發現真實浮空高度跟先前用 `.bounds` 量到的完全對不上：

  | 角色 | `.bounds`（錯誤，過期快取） | `BakeMesh`（正確，即時） |
  |---|---|---|
  | Player | 0.0000（已「驗證」貼地） | 實際浮空 34.06 cm |
  | 屁孩王 | 0.0000（已「驗證」貼地） | 實際浮空 18.70 cm |
  | Enemy | 0.0011（已「驗證」貼地） | 實際浮空 7.99 cm |
  | 中立者1 | 0.0000（已「驗證」貼地） | 實際浮空 **140.86 cm** |
  | 中立者3 | 0.0000（已「驗證」貼地） | 實際浮空 **136.21 cm** |

  中立者1、3 浮空超過一公尺，這種量級光看數字就該懷疑，`.bounds` 的驗證卻回報「正確」，足以說明這個方法在這個專案的 Edit Mode 腳本流程裡完全不能信任。
- **修法**：全部角色改用 `BakeMesh()` 量出的真實網格最低點，重新計算 `Visual` 子物件（連同頭上血條/能量條等 UI 子物件）需要位移的量，套用後再用 `BakeMesh()` 複查一次確認回到 0。
- **加一層不依賴任何程式讀數的驗證：實際截圖**。用 `manage_camera` 的 `screenshot` action（`view_position`/`view_target` 定位到貼近角色腳邊的水平視角，不要用會有透視誤導疑慮的俯角——斜角俯視截圖容易因為透視縮短造成誤判「這個東西是不是貼在那個東西上」，這類既有教訓也適用於這裡）直接截圖肉眼確認腳底跟地板紋理有沒有貼合、有沒有陰影懸空——這一步在這次抓出「`BakeMesh` 修完 0.0000，但其實那次是拿舊場景重載前的殘留姿勢」的又一層烏龍時，發揮了決定性作用，數字對不代表畫面對，兩者都要查。
- **教訓**：
  1. **不要相信「連續多次查詢同一個數字都一樣」就代表這個數字是對的**——過期快取的特徵正是「錯得很穩定」，穩定性不能拿來當作正確性的證據，要有至少一個獨立、不共享同一套快取機制的驗證管道（這次是 `BakeMesh` 對照 `.bounds`，以及截圖對照兩者）。
  2. **只有 `SkinnedMeshRenderer` 會有這個問題**，`Mecha`／`FemaleStandee_Placeholder` 這類用一般 `MeshRenderer`（沒有骨骼蒙皮）的靜態角色，`.bounds` 本來就沒有蒙皮快取這一層，量出來一直是可信的，不需要重查。
  3. **`Application.isPlaying == false` 且 Editor 沒有 OS 焦點時，Play Mode 進去後 `Time.frameCount` 會卡住不動**（另見上方「Unity MCP PlayMode 測試卡住 Test Runner」一類記錄），這種環境下沒辦法靠「真的按下 Play 觀察畫面」來交叉驗證，逼得這次只能在 Edit Mode 裡想辦法找到 `BakeMesh` 這個不需要真正跑起來也可信的替代方案。

## 已修正：批次校正場景高度時用名稱搜尋，漏掉了名稱歸零的 076/077，導致 076 在 Play Mode 掉出世界外（2026-08-25）

同一個 2026-08-25 Session 裡，AI 先把 `Ground` 從 y=-0.5 移到 y=0（使用者要求），照慣例找出場景裡「站在地板上」的所有角色逐一 +0.5 校正 Y 座標——但這次漏了 076/077，事後才由使用者回報「076 不見了」／「Play Mode 下看不到 076」才追出來，值得記錄避免下次同類批次操作再犯。

- **根因**：校正當時用 `GameObject.Find("角色名稱")` 這種按名稱搜尋的方式列出要一起處理的物件清單。076/077 的 `CubismModel3Json.ToModel()` 根物件正好卡在上面「Live2D 立牌視覺」那節記錄的名稱歸零 bug（`gameObject.name` 變回空字串），按名稱完全搜尋不到，於是整批校正**唯獨漏掉這兩個角色**——它們的 Y 座標停在對應「舊地板」高度的數值，比新地板低了 0.5。
- **後果比純視覺穿模嚴重得多**：077 沒有 `CharacterController`，只是視覺埋進地板 0.5 個單位；但 076（`Give076CombatSetup.cs`／`Reimport076Clean.cs` 已把它接上完整戰鬥 AI，含 `CharacterController`+重力）的碰撞膠囊底部因此卡在新地板下方，`CharacterController.isGrounded` 從頭到尾判定不到「有站在地上」，`EnemyAI` 的重力累加完全沒有東西擋——實際進 Play Mode 觀察（這次剛好 Editor 有拿到 OS 焦點，Play Mode 能真的跑，不像session其他幾輪卡在下面「Unity MCP PlayMode 測試卡住 Test Runner」那個已知限制），13 秒左右就從 Y≈2 掉到 **Y=-39945**、垂直速度衝到 -1259/s，完全掉出世界外、螢幕上自然什麼都看不到，比「肉眼看起來浮空/穿地」明顯很多，卻是同一個「批次校正漏掉一個物件」造成的。
- **修法**：不靠名稱，改用「有沒有掛 `Live2D.Cubism.Core.CubismModel` 元件」直接抓場景裡的全部 Live2D 立牌根物件（同上面「076 現在不見了」那次修名稱用的識別方式），逐一補上同樣的 +0.5，076 額外核對 `CharacterController` 底部世界座標确實對齊新地板（0.5）。再次進 Play Mode 實測 13.6 秒真實模擬時間，`isGrounded` 全程 `True`、座標完全沒有再往下掉，截圖確認畫面正常，才視為修好。
- **教訓：任何「按名稱列出場景裡所有 XX 類角色」的批次操作，都必須考慮到 076/077 這種名稱會歸零的物件會被漏掉**——不能只靠 `GameObject.Find`／按字串搜尋就假設涵蓋了全部相關物件，換成按元件類型（`FindObjectsByType<T>()`）或按場景階層位置這類不依賴名稱穩定性的辨識方式，才不會每次都要等使用者回報「東西不見了」才想起這兩個立牌的特殊狀況。之後任何要對「場景裡所有貼地角色」做批次數值調整的操作，第一步都應該先用元件類型掃過一輪确認清單完整，而不是先假設名稱搜尋就夠了。

## 教訓：Meshy 生成的 clip 拿來當武士攻擊，天生容易手感差（2026-08-28）

2026-08-28 一天內試接了三招 Meshy clip 攻擊、移除兩招、保留一招（DoubleCombo，但也有 marginal 命中問題），各有不同毛病，記下來供之後參考：

- **`AdvancingCuts`**（`Meshy_AI_Parkside_Portrait_biped (7).zip`，加入當天移除，嫌「不好用」）：clip 視覺上最猛的揮砍在 4x 武士的過頭高度（刀世界 Y 4–5.7），高過 PlayerHurtbox（Y 0.50–2.15），真正能命中的只有往下收刀那兩小段 → 玩家看到大揮砍卻不中、判定在別的地方。
- **`LungeSlash`**（2026-08-27 加入、08-28 移除，嫌「沒有前衝、展擊不夠圓弧」）：clip 有 Hips 撲步但 `useRootMotion=0` 不轉成位移；揮砍是烘死的下劈、非橫向圓弧。

- **`DoubleCombo`**（`Meshy...(9).zip` 的 `Double_Combo_Attack`，2026-08-28 加入、**保留**）：兩段揮砍都真的掃過 hurtbox 高度，是三次裡幾何最好的。但 clip 把角色動畫成「起手比 root 後退 3 單位、連段中走回 root」，`useRootMotion=0` 時可見刀身落後 boss 座標 → 命中偏 marginal（1.8 單位內才穩，實測 hit1 -14）。折衷把 `maxDistance` 收到 2.5。

共通教訓：Meshy 生成動畫的骨架動作幅度/高度/朝向常常不合戰鬥需求，接之前先用 `AnimationMode.SampleAnimationClip` 重定向到 4x 武士，量 **BladeHitbox 世界 Y**（揮砍主體要掃過 0.5–2.2）**以及 blade 相對 boss root（不是相對 hips）的前伸量**——Meshy 的「前進型」clip 常把位移烘進 Hips 而非 root，`useRootMotion=0` 會讓刀構不到。武士普通池現在 4 招：SwordJudgment / SpartanKick / OverheadSlam / DoubleCombo。

### 屁孩王 同款：PW2_LeapSmash / PW2_ChargeSlam（2026-08-29 追加42）

同樣的 Meshy「前進型」毛病。兩個 clip（`Meshy_AI_Man_in_Black_at_the_P_biped` 的兩個 without_skin 動作）root 各走 ~4m / ~6m，接進 `屁孩王.normalAttackPool` 時 `useRootMotion=0`（比照全部已上線 boss 招式），只接了「位移前」的近距離 hit window：
- **LeapSmash**（跳躍重砸）：window t=0.50–0.60 雙手下砸，`maxDistance` 1.3。30 HP / 24 poise / 擊飛 / major。
- **ChargeSlam**（衝刺掌擊）：window t=0.38–0.50 雙掌前推，`maxDistance` 1.15。26 HP / 20 poise / knockback 10。

**追加43**：改成 `useRootMotion=1`（`BossAnimatorRootMotionRelay` 掛 `屁孩王/Visual`：空 `OnAnimatorMove` + `applyRootMotion` 讓 `Animator.deltaPosition` 有值；`ApplyMotion` 加 `CurrentState==Attack` 修 root-motion 陷阱；`deltaPosition.y` 歸零）。實測位移量偏小（Humanoid retarget 把 root motion 正規化到 avatar 比例）：ChargeSlam ~1.3m、LeapSmash 水平 ~0（純垂直跳、Y 被歸零）。rm window 0.05–0.90、maxDistance 3.5。

**追加44**：`PW2_LeapSmash` 從 normalAttackPool 抽出，改接 `屁孩王.leapSlamAttack` → 走 `LeapSlamWindup`+`LeapSlam` 狀態機（武士那套）：1s 前搖鎖定玩家 → 腳本高度弧（`leapSlamExtraHeight` 30→**8**）+ 上升期 homing 追玩家 → 垂直落下。`useRootMotion` 關回 false。hit window 0.42–0.60 雙手。定時觸發（`leapSlamTriggerSeconds` 20）。`m_Speed` 1.25→1.0。ChargeSlam 還留在 pool（維持 useRootMotion=1 ~1.3m 位移）。
**待實機**：LeapSmash 飛 8 高合不合適、垂直落點、hit window；ChargeSlam 位移夠不夠。

**既有無關 warning**：`Parameter 'Hash 1367192179' does not exist` = `PiHaiWangV2.controller` 缺 `BreakdanceTrigger` 參數，屁孩王每次 Breakdance warn 一次（Breakdance 靠 CrossFade 名稱進，無害，未修）。

**追加46**（使用者:「有些攻擊打不倒玩家 / 直接打斷當前動作只看到起手 / 每一招都有用上嗎 / 有位移的別綁死近戰距離」）：
- **ChargeSlam 幾乎不發** → `minDistance` 1→0（原本 >`AttackReadinessDistance()` 0.98 被每次濾掉）；新增 Approach 決策時 roll「只含 `UseRootMotion` 招」的 `PickAttackFiltered`，中了直接 `BeginAttack` 不先走進近戰。
- **「只看到起手」= 排程技打斷前搖** → `AttackFinishedCommittableWindow()` 改：interruptible 攻擊要 `AnimatorNormalizedTime() >= FirstStrikeNormalized`（第一個 hit window 起點）才准被 Breakdance/LeapSlam/Ultimate/Vanish pre-empt。玩家 stagger（PostureBroken / 載具 RequestBeHitFlyUp）走別條路不受影響。
- **站定打不到** → `UpdateAttack` 非-rootmotion 招在 tracking 期 + `dist > MaxDistance*0.85` 時給 `WalkSpeed*0.5` 朝玩家 reel-in 位移。
- **HighKick 下架**：`selectionWeight` 25/30→0/0（clip designNotes 自己標「揮過站立玩家的頭、需重編排」）。屁孩王普通池實際剩 **3 招**（PunchCombo1 / GuardKick / ChargeSlam）；`PunchCombo1.maxConsecutiveUses` 1→2 補池薄。**可能要再找一支能打站立玩家的近戰 clip 換回第 4 招**。
- EditMode 197/196。gap-closer 頻率、追身幅度、前搖保護門檻全待實機。

**追加47**（使用者:「每招盡量輪流施放不要有技能被孤立 / 攻擊慾望太低攻擊間隔太長」）：
- **輪流偏置**：`BossTuning` 新 `attackRotationRecoverySeconds`(6) / `attackRotationRecentFactor`(0.15)。`BossStateMachine._lastUsedTime` 記每招上次施放時間，`PickAttackFiltered` 把剛用過的招權重乘到 15%，6s 線性回滿。軟性 LRU，跟 `cooldownSeconds` 硬門檻分開。PW2/Wushi tuning 都明寫。
- **攻擊慾望**（純 `PW2_Tuning`）：`globalRestPhase1` 1.6–2.4→0.5–0.9、`globalRestPhase2` 1.1–1.8→0.3–0.55、`majorAttackExtraRest` 2–3→0.8–1.5、`decisionIntervalPhase1` 0.5–0.9→0.22–0.4、Phase2 0.3–0.5→0.14–0.28。攻擊間空檔 ~2.3–3.5s → ~0.75–1.35s（仍比武士保守）。
- `PW2_Attack_GuardKick` cd 5.5→3.5、`PW2_Attack_ChargeSlam` cd 5→4（縮休息後補位）。
- EditMode 197/196。新節奏兇不兇、輪流循環感、縮 cd 後洗不洗版，全待實機。

**追加48**（使用者:「飛向天空那招沒有鎖定玩家方向飛過去攻擊 / 傷害固定百分比50」）：
- **不再瞬移**：`BossTuning` 新 `leapSlamTeleportToLanding`（bool，預設 true = 武士）。屁孩王 `PW2_Tuning` 設 0 → `CommitLeapSlamLanding` 不 blink，只鎖朝向/落地 Y，`UpdateLeapSlam` 的空中 homing 負責整段水平位移（起跳點飛到玩家）。武士維持 1。
- **固定 50%**：`PW2_Attack_LeapSmash` `healthDamageIsPercentOfTargetMax` 0→1、`baseHealthDamage` 30→50。hitWindows 2→1（只留 LeftHand，原本兩個 hand hitbox 各結算 = 100% 秒殺）。poise 也回到一下 24。
- EditMode 197/196。飛行弧線觀感、homing 追不追得上、50%+launch+24poise 會不會太重，待實機。

**追加49**（使用者:「飛空那招不用飛那麼高 比站著高一倍就好 / 目前沒有鎖定玩家位置飛過去」）：追加48 拿掉瞬移後靠共用 homing，但那段速度是 `_stateTimer/normalizedTime` 反推的，crossfade blend 期間 normalizedTime 髒 → 早期速度趨近 0 → 沒飛過去。
- **`UpdateLeapSlam` 開頭**：`if (!tuning.LeapSlamTeleportToLanding) { UpdateLeapSlamPounce(); return; }`。武士（teleport=1）走原路徑不動。
- **新 `UpdateLeapSlamPounce`**：自足、全用 `_stateTimer` 驅動。水平每幀瞄玩家實時 xz、速度=剩距/剩時、`leapSlamMaxTrackSpeed` 封頂、`leapSlamFlightSeconds`(新，1.3) 內貼上再鎖定直落；垂直 `_stateTimer` 拋物線到 `leapSlamExtraHeight`。hit window 仍讀 clip normalized（落地 ~1.3s 時已可靠）。
- `PW2_Tuning.leapSlamExtraHeight` 8→**2.5**（站著 ~2.5m，弧頂 ≈ 兩倍高）。`leapSlamFlightSeconds` 對齊 slam 窗（0.42×3.07s≈1.29s）。
- EditMode 197/196。飛行觀感、落地時機、高度、髒 nt 期間 hit window 時機待實機。

**追加50**（使用者:「飛天那招仍然沒攻擊到玩家 可能是動畫問題 / 攻擊間隔再簡短」）：
- **根因**：追加49 的 pounce 位移吃 `_stateTimer`，但 hit window + exit 還讀 `normalizedTime`。crossfade 頭幾幀的殘值若 `>= 0.6` → `_leapSlamHitWindowsDone` 第一幀 latch → hitbox 永不開；殘值 `>= 0.98` → `IsAttackAnimationFinished` 讓整個 state 起跳 ~0.1s 就 abort。
- **修**（`UpdateLeapSlamPounce`，武士 teleport 路徑不動）：hit window 改 `_stateTimer` 相對落地（`t ∈ [flight-0.1, flight+0.35]` 開 `HitWindows[0].part` hitbox）；exit 改 `t >= flight + max(0.3, recoverySeconds)`；落地補 `_globalRestUntil`（50% 招跳過 EndAttack 休息邏輯）。
- **間隔再縮**（`PW2_Tuning`）：`globalRestPhase1` 0.5–0.9→0.25–0.5、Phase2 0.3–0.55→0.15–0.3、`majorAttackExtraRest` 0.8–1.5→0.4–0.9、`decisionIntervalPhase1` 0.22–0.4→0.12–0.25、Phase2 0.14–0.28→0.08–0.16、`attackReadinessBuffer` 0.1–0.2→0.06–0.12。空檔 ~0.75–1.35s→~0.35–0.7s。
- EditMode 197/196。

**追加51**（使用者:「飛天這招落點永遠沒有在玩家身上」）：根因 = `leapSlamLandingOffset` 2（武士時代加的，避免落在玩家膠囊裡卡頭高「浮空」；武士有 3m LandingAOE 罩得到，屁孩王沒有、用左手 hitbox → 2m 外搆不到）。修：`PW2_Tuning.leapSlamLandingOffset` 2→0.4；`UpdateLeapSlamPounce` 落地 Y raycast 改 `RaycastAll` + 跳過玩家（近距離普通 Raycast 會打到玩家膠囊頂 → 釘頭高）；hit window `[flight-0.15, flight+0.45]`。武士 teleport 路徑不受影響。EditMode 197/196。

**追加52**（使用者:「感覺屁孩王很少放這招」）：`PW2_Tuning.leapSlamTriggerSeconds` 20→**10**（沒接 energy、走純計時器，20s 太長 + 每次脫戰/復活歸零）；`breakdanceTriggerSeconds` 15→**24**（cascade 裡 Breakdance 排在 LeapSlam 前，15<20 每次先搶空檔；Breakdance hit window 還是 `measured:0` placeholder）。若還是少 → 嫌疑轉向 Ultimate（RisingFlyingKick，energy ~15s，cascade 更前，300 傷害秒殺）吃空檔 + 提前結束戰鬥。

**追加53**（使用者:「飛踢為甚麼步行」）：`UpdateUltimateAttack` 撲擊只有水平 `transform.forward * UltimateLeapSpeed` 貼地滑，clip `useRootMotion=0` → 看起來像快走。修：`BossTuning` 新 `ultimateLeapJumpSpeed`(7)，`OnEnterState(UltimateAttack)` 第一幀 `_verticalVelocity = jumpSpeed`（grounded 時），靠 `ApplyMotion` 的正常重力弧回地面。0 = 舊貼地滑。`UltimateReposition` 後退的「月球漫步」（播前進 clip、無後退 take）未動。EditMode 197/196。

**追加54**（使用者:「飛踢碰到玩家沒有馬上被擊退 能仿照 LeapSmash 嗎」）：clip 1.5s，舊撲擊跑滿 normalized 前搖窗（0.9s×10=~9m）從 ~5m 觸發距離暴衝過頭 3–4m → 腳 hitbox 在 boss 飛過後才開 → 命中只靠腳收回的 SweepCheck 掃回來 → 慢、脫節。修（仿 `UpdateLeapSlamPounce`）：撲擊改「衝到 `HorizontalDistance()<=1.3` 或 `_stateTimer>=0.75` 就停」；命中窗改 `_stateTimer` 相對停下時刻 `[contact-0.06, contact+0.35]`（boss 停在玩家身上 + hitbox 開啟當幀重疊 → `OnTriggerEnter` 即時結算）；exit 改 `t>=1.5s`。asset 不動。EditMode 197/196。

**追加61**（使用者:「屁孩王攻擊慾望不夠強、技能銜接不夠快、攻擊後搖太長」）：
- **切後搖**（新）：`BossTuning.attackRecoveryTailCutNormalized`（預設 2=不切；武士 2）。`UpdateAttack`：最後 hit window 關 + 這個 normalized 量之後、且不在 active window → `EndAttack()` CrossFade 出去，跳過 clip 收招尾巴。`PW2_Tuning`=**0.15**。
- **縮間隔**（`PW2_Tuning`）：`globalRestPhase1` 0.25–0.5→0.1–0.25、Phase2 0.15–0.3→0.06–0.15、`majorAttackExtraRest` 0.4–0.9→0.2–0.5、`decisionIntervalPhase1` 0.12–0.25→0.06–0.15、Phase2 0.08–0.16→0.04–0.1、`attackReadinessBuffer` 0.06–0.12→0.03–0.08。已接近武士兇度。

> 武士攻擊節奏（「技能本身時長」vs「出招間隔」是兩套獨立系統、`startupSeconds`/`recoverySeconds` 是死欄位、想調快/調慢改哪裡）整理在 **`Docs/TECHNICAL_DESIGN.md` →「武士 Boss：攻擊節奏模型」**。

## 未解決：武士 LeapSlam「偶爾飛天後消失沒下來」（2026-08-28 使用者回報，疑似既有）

- 症狀：武士施展 LeapSlam（每 20 秒的定時飛空劈砍）後偶爾卡在半空、永遠不下來。
- **不是這次新增 AdvancingCuts 造成的**——AdvancingCuts 只是普通池一招，沒碰到 LeapSlam 的程式路徑。LeapSlam 在 CHANGELOG 有一長串「浮空/落地不對」歷史，很可能是既有問題，長時間測試才撞到。
- AI 端無法重現：Editor 沒有 OS 焦點時 Play Mode frame-frozen（見下方「Play Mode 卡住」相關記錄），boss 停在第 2 幀。
- 程式面結構弱點（`BossStateMachine.UpdateLeapSlam`）：
  1. **沒有 `_stateTimer` 硬性 timeout 保險**。唯一離開條件是 `IsAttackAnimationFinished(normalized)`，而 `normalized = GetCurrentAnimatorStateInfo(0).normalizedTime % 1f`。只要有任何原因讓 LeapSlam 狀態的 `normalizedTime` 在 arc 的 0.05–0.53 區間停住（`animator.speed=0` 外洩、animator culling、被高優先級狀態打斷後 `_verticalVelocity` 殘留…），高度 arc 就凍在那個值、boss 永遠掛在半空，沒有任何機制拉回來。對比 `UpdatePostureBroken`（line ~1532 有 `+5f` 保險）、`UpdateDiveAttack` 都有這種「絕不永久卡住」的安全網。
  2. `TryEnterLeapSlam` 的落地 raycast 打空時 fallback 用 `transform.position.y`（CHANGELOG 2026-08-27 自己承認過的舊 bug，只修了一半）——boss 若已在半空，這個值就是灌水高度，`_leapSlamLandingY` 直接吃到，pin 在半空。新的南牆缺口 + `VehicleRoad` + 沒有 collider 的 `BackgroundTerrain` 讓「landingPos 落在沒有 collider 的地方」更容易發生。
- **建議修法**（都小、模仿現有安全網）：(a) `UpdateLeapSlam` 加 `_stateTimer > clip長度 + 2s → ChangeState(Idle)` 並把 Y 拉回地面 raycast；(b) raycast 打空時改用「上次貼地 Y」而非當前高度。**尚未套用**——等使用者確認要動 `BossStateMachine.cs`，或先聚焦 Editor 重現、抓卡住當下的 `CurrentState`/`_stateTimer`/`animator.speed`/`normalizedTime`/`transform.position.y`/`_leapSlamLandingY` 確定是哪條路徑。
- **2026-08-28 追加14 後更新**：LeapSlam 現在前面多了 `LeapSlamWindup`（1 秒蹲下前搖）、觸發改成能量條滿格（`leapSlamEnergy.IsFull`，5/秒 20 秒滿）。**這個「卡半空」bug 沒被碰到、也沒修**。新增的 `CommitLeapSlamLanding()`（從 `TryEnterLeapSlam` 抽出來、移到蹲完的 commit 點）保留了上面第 2 點那個 raycast-打空-用當前高度的 fallback，所以那條路徑的風險不變。
- **2026-08-28 追加22 後更新**：`UpdateLeapSlam` 現在飛行中會 homing 追玩家（`normalized < LeapSlamTrackUntilNormalized` 0.45 期間驅動 `_horizontalVelocity`），並每幀重打落地 Y raycast。**「卡半空」bug 仍沒修**——homing 只在 `!_leapSlamHolding` 時透過 `_controller.Move` 生效，一旦高度弧凍結（第 1 點那個 `normalizedTime` 停住的情境）`_leapSlamHolding` 也不會變 true、homing 也不跑，所以那條路徑沒變好也沒變壞。上面「建議修法」(a) 加 `_stateTimer` timeout 保險仍然待做。

## 已修：武士死亡屍體飄在半空 ＋ 兩隻 boss 死亡→復活流程統一（2026-08-31 追加70）

- 症狀：打死武士後屍體定格躺在 ~2m 高的半空。
- 根因：武士 Animator 在**根物件**、`applyRootMotion=False`。死亡 clip `Fall_Dead_from_Abdominal_Injury` 的往地上倒垂直位移（`RootT.y` 0.95→0.18）在 root motion 裡，被整個丟掉，只剩「站姿髖高」的躺平 pose（×4 縮放 → hips/head/feet y≈2.4–2.7，地面 0.5）。
- 修法：`Wushi_DeathFallForward.fbx`（＋屁孩王 `PW2_ShotAndFallForward`，同樣的 `RootT.y` 側寫）clip 匯入設定 → **Bake Into Pose (Root Transform Position Y) = ON** ＋ **Based Upon = Feet**。垂直位移改留在 pose、以腳錨定。修後屍體貼地（武士 Hips y≈1.04），站姿起手不變、crossfade 無 pop。
- `Wushi_PostureKneel`（`falling_down`）`RootT.y` 有相同側寫、理論上跪地也會飄一點，未回報先不動。

### 死亡→復活流程（使用者:「不管是武士還是屁孩王都要 5 秒後復活、屍體不消失、慢慢站起來」）

- **武士 `Wushi_Tuning.permanentDeath` true→false**（屁孩王本來就 false），兩隻 `reviveDelaySeconds`=5。
- **屍體不消失**：兩隻 `Health.deferDeactivationToDeathAnimation=true` 且無 `DeathAnimationLink` → `Health` 從不 `SetActive(false)`；死亡 clip 不 loop。本來就不會消失（使用者看到的「消失」實為舊版瞬間站起）。
- **慢慢站起來**：新 `BossState.GettingUp`（enum 最後一位）。`UpdateDead` 復活時 → `ChangeState(GettingUp)` 而非直接 `Alert`。`GettingUp` 每幀 `animator.Play(死亡state,0,1-t)`（`t=_stateTimer/StandUpSeconds`）把死亡動畫**倒著刮**回站姿（無專屬起身 clip），起手有 i-frame，`StandUpSeconds`（新 tuning 欄位預設 1.8）到 → `Alert`。cascade / combat timer / CombatActive / `RequestBeHitFlyUp` 全把 `GettingUp` 跟 `Dead` 一起排除。
- 實測 log：`Dead -> GettingUp -> Alert -> Idle`，`_deathElapsed` 到 ~5.0 才離開 Dead；兩隻復活後 `activeSelf=true`、renderer 全開、滿血。
- **殘留邊角**：若 boss 在 LeapSlam/Vanish/Dive **半空**被打死，`UpdateDead`/`UpdateGettingUp` 不主動施重力弧、只靠 `ApplyMotion` 一般重力落地；跟下面「LeapSlam 卡半空」共用「Dead 系狀態垂直控制薄弱」的根，沒逐幀驗過。

### 屁孩王 Breakdance / KneelStand 半身陷地板（2026-08-31 追加70，使用者回報 → 修）

- 症狀：屁孩王做 `PW2_Breakdance1990`（定時 flourish）或 `PW2_KneelOnOneKneeAndStand`（架勢崩潰跪地）時整個下半身陷進地板（實測腳 y≈−0.33，地板 0.5）。
- 根因：這兩招唯一來源 `Meshy_AI_Meshy_Merged_Animations.fbx` 是 **Generic**（其他招式來自各自的 `*_withSkin.fbx`，都是 Humanoid）。這顆 FBX 的 position 曲線比 rig 小 **100 倍**（cm/m 單位不一致，`useFileScale` 生效尺度 0.01）：`Hips.m_LocalPosition.y` clip 值 0.834 vs rig bind 83.43。Generic clip 直接套原始曲線 → 骨架壓到近原點。Humanoid clip 走 muscle 重定向不受影響，所以只有這兩招。
- 修：importer `useFileScale: 1→0`（globalScale 留 1），position 曲線值對回 rig（逐骨驗證一致）。不改 animationType（維持 Generic）、不動 clip 切割、controller 連結不變。那 FBX 裡另外 8 個 clip 沒被用到。
- **待使用者實機確認完全貼地**：改完當下 Editor 失焦 Play Mode 凍幀（`Time.frameCount=1`）、`AnimationMode` 對多-clip FBX 子資產取樣不可靠，沒能自己看最終效果。單位×100 bug 是鐵證，修正無疑問，但有沒有第二層問題要人眼確認。
- 舊 `Assets/Editor/PW2_FixBreakdanceKneelClipBaking.cs`（2026-08-26，從沒跑過）方向錯（假設 Humanoid + heightFromFeet），已被這次的 useFileScale 修法取代，可刪。

## 待實機驗證：AI 脫離規則（2026-08-29 追加37→39）

- **武士**:`confineToArena=0`（**不受城牆限制——使用者原始設計**；追加72 曾誤改 1、追加73 撤回）。距離 leash `leashRange` **32**（追加70，從駐點量；14 太緊「只待在警備範圍」）→ 追出門口、到離駐點 32m 才 `DisengageAndReturnHome`。追加71 NavMesh 避障讓它追出去繞得開障礙。飛行中的 leap/dive/ultimate 不會被打斷。**追加44**：脫離後改**跑步歸位**（`BossState.ReturnHome`，播 Running blend tree，到站才 `SnapToHome` 精準對位），不再瞬移。中途玩家回 AlertRange → 立刻重新交戰。**追加45**：`UpdateApproach` 補上跟 `UpdateIdle` 一樣的 `> AlertRange*1.5`(9m) 脫戰判斷（以前只有 Idle 有，追不上玩家時卡在 Approach 永不歸位）；`TryEnterLeapSlam` 的 `leapCap` 從 ~18m 收到 ~9m（`CommitLeapSlamLanding` 是瞬移到玩家頭上，18m 內發＝「憑空衝過來」）。
- **屁孩王（精怪，追加39→40）**:玩家出本地方形 → boss **無條件進 `BossState.GateWatch` 並走向門口**（面向玩家、完全不攻擊、下層 cascade 全跳過），`ApplyMotion` 每幀 `ArenaBounds.ClampInside` 夾在牆內。give-up 由 `UpdateGateWatch` 判：**boss 已抵達牆邊 且 玩家離 boss > `gateWatchRange`(10) 撐 `gateWatchGiveUpSeconds`(1.5s)** 才 `SnapToHome` 回駐點（還在走去門口就永不放棄 —— 追加40 修「一碰邊界就瞬移」）。玩家回方形內 → 回 Idle 重新交戰。`confineToArena=1`。
- **Enemy（普通怪物，不受限）**:一路追出門口都可以。追加41 撤掉了 `EnemyAI` 裡那份平行的 arena-confine 死碼（場景 Enemy 本來就 `confineToArena=0`），arena-confine 現在只存在於 `BossStateMachine`。
- **武士 confine 來回一趟（追加70→72→73）**：追加70「武士只會待在警備範圍」→ `leashRange` 14→32。追加72「武士會突破本地範圍限制」→ 誤加 `confineToArena=1`。追加73「我一開始設計讓它不受城牆限制 請復原」→ `confineToArena` 撤回 0、`gateWatchRange` 撤回 0。**最終**：武士不受限（可追出本地）、`leashRange` 32 是唯一的距離 leash。
- **`ChaseGiveUpDistance()`（追加70，保留）**：`UpdateIdle`/`UpdateApproach` 的 from-boss 脫戰改用 `Max(AlertRange*1.5, leashRange)`（原本寫死 `AlertRange*1.5`=9m 太早觸發）→ `leashRange`(from-post) 成為「追多遠」的權威。`leapCap`（`TryEnterLeapSlam` 瞬移距離）不動。
- 純函式 `ArenaBounds`（`IsOutside`/`ClassifyTarget`/`ClampInside`）11 個 EditMode 測試（綠）。但 **GateWatch 觀感、走到門口位置準不準、`gateWatchRange` 10 是否合適、玩家回門內重新交戰、Enemy 隔牆不揮擊，全待使用者實機**——MCP 下 play mode 凍幀無法跑真實戰鬥。
- `GateWatch` 插在 `BossState` enum 第 4 位（Approach 之後），其後 ordinal 各 +1。沒有資產序列化 BossState int（CrossFade 全走狀態名），確認無影響。
- 方形中心/半徑寫死在角色 Inspector（`arenaCenterXZ` 原點 / `arenaHalfExtent` 15.5、`gateWatchRange` 屁孩王 10）。若之後把怪物放到學校，要改成該區中心 **(0, -115)**、half-extent ~29（追加65 學校擴大到 60×60 後）。

## AI 避障：NavMesh 路徑跟隨（2026-08-31 追加71）

使用者:「所有角色在移動時有可能會被地圖物件擋住路線從卡住 有沒有演算法可以避開這個問題」。

- **病因**：`EnemyAI` / `BossStateMachine.MoveTowardTarget` 直線朝目標推、零避障，中間有牆/柱/建築就頂死原地磨。場景本來完全沒 baked NavMesh。
- **做法**：`NavPathFollower`（agent-less，問 `NavMesh.CalculatePath` 拿轉角、沿用 `CharacterController.Move`，fail-open 退回直線）。掛在武士/屁孩王/Enemy。`NavMeshBakeSetup.cs`（選單 `Tools/Live2DAction/Bake Navigation Mesh`）烤一張 NavMesh、存 `Assets/_Project/Scenes/GreyboxTest/NavMesh-Navigation.asset`。細節見 CHANGELOG 追加71。
- **範圍**：只接 AI。**Player/Cat 沒接**（輸入驅動、沒路線可規劃；它們卡是碰撞體品質問題，要另做 collider pass）。
- **NavMesh 要維護**：改/加地圖幾何後**要重跑 `Bake Navigation Mesh`**。scene builder 沒加自動 bake。
- **學校區 navmesh 是 `PathPartial`**（plaza 在 y=−6、地形破碎），校園內避障打折，待該區整理後重烤。
- **待實機**：實戰情境 AI 繞牆追人順不順、`cornerReachDistance` 0.75 會不會切角切進牆、被擊飛出 mesh 的邊界情況（`SamplePosition` radius 2.5 通常拉得回）。
- 測試：`NavPathFollowerTests` 7 個（純函式 `AdvanceCorner`），EditMode 234 綠（唯一失敗仍是既有無關 `...FallsBackToAttack3`）。

### 追加38：移動速度 ×1.5 ＋ 腳步同步（foot-sync）
- Enemy `moveSpeed` 3、屁孩王 `PW2_Tuning` walk 3 / run 6.75。超速時 Animator 播放速率等比加快治腳滑（`CharacterAnimatorLink.syncStrideToGroundSpeed` on Enemy；`BossStateMachine.locomotionAuthoredSpeed`=2 on 屁孩王，只在 Approach）。武士 `locomotionAuthoredSpeed`=0（未套用，不同 rig）。
- **腳步同步實際觀感、×1.5 後追擊/命中手感待實機**。若腳步同步過頭（跑步 clip 抽動）調低 Enemy link 的 `maxStrideRate`，或把屁孩王 `locomotionAuthoredSpeed` 調高。

## 待實機微調：隻狼式對決攝影機（2026-08-28 追加20 + 自適應化追加21）

- 鎖定武士時走 `ThirdPersonCameraController.UpdateDuelCamera()`：yaw/pitch/distance/look-at 每幀由玩家 + boss 兩者體積推導。追加21 把三個寫死上限改成隨 boss 高度推導：有效拉遠上限 = `max(duelMaxDistance 12, bossHeight × duelMaxDistancePerHeight 4)`;塞不下時 FOV 放寬到 `duelMaxFov` 85°;`duelMaxVisibleAboveLook`(7) 之上的巨物頭部**故意裁切**出上緣,不讓攝影機無限後退。
- **AI 端只用 reflection + 螢幕外算圖驗證**(Editor 沒 OS 焦點時 Play Mode frame-frozen)。武士框景已確認與追加20 逐位元相同(dist 5.8 / pitch 6.2° / FOV 65);8/15/30 單位高的假想 boss 會依序拉遠、最後啟動頭部裁切。
- **待使用者實機微調**:距離、pitch、玩家在畫面裡的高度、yaw/pitch 回正手感。全部值在 Main Camera 的 `ThirdPersonCameraController`「Locked-duel camera」區。新 boss 只要掛 `LockOnTarget` + 勾 `useDuelCamera`,`duelTargetHeight` 留 0 就自動量測 renderer bounds;量測失準(SkinnedMeshRenderer bounds 過大 / idle 蹲姿)時再手填,如武士的 4.1。
- 巨物想「永遠框全身」而非裁切:把該 Camera 的 `duelMaxVisibleAboveLook` 調高(例如 20)。

## 學校區第一棟建築（2026-08-30 追加63，`YuanpeiUniversityBuilding`）

- Meshy AI FBX（`Meshy_AI_Yuanpei_University_Bu_0830053851`），匯進 `Assets/_Project/Environment/Meshy/YuanpeiUniversityBuilding/…`，放在 `GreyboxTest` 的 `學校` 地板上（root `YuanpeiUniversityBuilding`，**euler `(270,90,0)`**、scale 0.15、world 約 15×13.6×14.6，**正立面朝北路口**，非凸 `MeshCollider`）。手建 URP/Lit 材質（base/normal/metallic 貼圖，smoothness 0.25）。
- **這個 FBX 不是 Y-up**：正確朝向是 `euler (270,90,0)`（local −Z 的大片灰色地基 slab → 世界 down，local −X 帷幕牆正立面 → 世界 +Z）。追加63 續 3 之前都當 Y-up 擺 → 整棟橫躺，才會一直有懸空/傾斜。現在地基整片平貼 y≈0.5。之後任何重擺都要沿用這個朝向。
- **追加64：學校再放 `ModernGlassLibrary`（西）、`PalmLinedLibrary`（東）兩棟 Meshy 圖書館佔位**，跟元培大樓組成朝北的校園中庭。兩個也都非 Y-up：ModernGlass `euler (270,90,0)` 正立面就朝北；PalmLined 正立面在 local −X，要 `euler (270,180,0)` 才朝北。三棟都手建 URP/Lit 材質、非凸 MeshCollider、static、背/側是 Meshy 空白面。頂點量大（1.62M / 3.37M）、無 LOD、自帶樹木。
- **追加65：學校擴大到 60×60**（`SchoolAreaSetup.AreaSize` 30→60），`VehicleRoad` 加長加寬（`RoadOutwardLength` 65→70、`RoadWidthOverGap` 2→3.5，現 7.4 寬 × 70 長）。`學校` 現在 z ∈ [−145,−85]、x ∈ [−30,30]、中心 (0,0,−115)。三棟樓重擺放大（元培 22m footprint / 20m 高；ModernGlass 20m / 8.6m；PalmLined 20m / 13.5m），組成中軸約 32m 深的校園中庭。改動要重跑兩個選單（先 `Add Vehicle Wall Opening + Road` 再 `Add School Area`）。**注意**：`ArenaBounds` / boss `arenaCenterXZ` 若要在學校放怪，中心是 **(0, −115)**、half-extent ~29（追加65 前的筆記寫 (0,−95) 已過時）。
- **只有正立面有貼圖**：背面與兩側是 Meshy 單視角生成的空白白面。從學校內部繞到後面會看到穿幫。模型造型本身還帶輕微「前傾」（烘進 mesh）。
- **單一 mesh ~2.57M 頂點、無 LOD**：對 greybox 場景偏重（且 `MeshCollider` 也吃這個 mesh）。之後換正式建築資產或減面 / 烘 LOD。
- **未併入 `學校` 父物件**：建築、`學校` 地板、`SchoolWall_*` 目前都是各自的 scene root（追加31「道路正式併入 `學校`」的待辦仍未做）。學校區的生成點 / 傳送點 / 室內 / 撞牆 FX 都還沒做。
- FBX import 時 zip 被解到一層多餘的 `YuanpeiUniversityBuilding/YuanpeiUniversityBuilding/…_fbx/` 巢狀資料夾；沒動它以免打斷材質引用。附帶複製進 Assets 的 `.zip` 已刪。
- **原始 FBX 排除版控（2026-08-31 追加82）**：這 4 棟的內嵌貼圖 FBX 單檔 108~130 MB、各約 300 萬三角面，超過 GitHub 100 MB 單檔上限。已 `git rm --cached` ＋ `.gitignore` 擋掉（`Meshy_AI_*_texture.fbx`），檔案只留本機。同資料夾貼圖 / `.mat` / `QuietCampusPlaza_NoFoliage.mesh`（55 MB 減面版）照常進版控。**待辦**：面數本來就過高不能直接用，需回 Meshy 下載低面數版或 Blender decimate 後才重新進版控——完成前 fresh clone 這 3 棟（QuietCampusPlaza 有減面 mesh 例外）會 missing mesh。細節見 `Docs/LARGE_ASSETS.md`。

## 貓角色佔位（2026-08-29 新增，`CatCharacterSetup.cs`）

- **glb 無動畫 clip**：`Cat.glb`（Meshy AI）只有 auto-rig 骨頭、沒有任何 idle/walk clip。2026-08-29 追加24 加了 `CatProceduralWalk`（程序化四足步態，繞四條腿的肩/髖+肘/膝骨，幅度隨移動速度）補上「移動時四肢會動」；靜止時仍是 bind pose（沒有 idle 呼吸動作）。真正的四足動畫（idle/walk/run clip 或一組 retarget 來源）尚未做，程序化步態的幅度/相位手感也**待使用者實機微調**（`CatProceduralWalk` 上的 serialized 欄位）。
- 貓也吃 `CharacterMovement` 的跳躍（Space）：因為刻意共用玩家的 `CharacterMovement`（rule 8），Space→跳。無害，之後若不想要可在貓的 `CharacterMovement` 上另開旗標或換成精簡 mover。
- **飛行 ＋ 衝刺（2026-08-29 追加26，切片 1；追加28 調衝刺數值）**：`CatCharacterSetup.WireFlightAndDash` 接了 `flightEnergy`（`UltimateEnergy` 500/30/1/3）＋ `CatDodgeData.asset`（**distance 3**，追加28 從 1.6 調高，與玩家一致——1.6 時 dash speed 8 幾乎等於飛行巡航 7，空中衝刺無感）。按住 Ctrl 飛、Q boost、點 Shift 衝刺——邏輯全在既有 `CharacterMovement`，只是接線。**待處理**：(1) 貓**沒有飛行體力條 UI**（切片 3），而 `UltimateEnergy` 初值 0，附身後要 regen 到 30（約 1 秒）才能起飛，玩家看不到進度；(2) 飛行時只是 ease 回靜止姿勢（`CatProceduralWalk.ComputeGaitTarget` 飛行→0），**還沒有收腿飛行姿態**（切片 3）；(3) **衝刺沒特效**——貓沒 `Health` 也沒 `InvulnerabilityRippleEffect`（玩家衝刺無敵的銀白漣漪靠 `Health.IsInvulnerable`），切片 3 處理；(4) `flightMoveSpeed 7` / `ascend 5` 仍是照 0.45 縮放抓的起點值，手感待實機微調，`CatCharacterSetup.cs` 常數改完重跑選單即套用（`CatDodgeData` 現在也是每次重跑重寫）；(5) `BuildCatCamera` 的 `enableDescendAutoPitch=false`（貓攝影機刻意不隨下降自動壓低），想跟玩家一致可改。
- **切片 1 測試**：EditMode 已實跑通過（168 tests，含 `CatProceduralWalkTests`；唯一失敗是既有無關的 `CharacterAttackAnimationLinkTests…FallsBackToAttack3`）。**PlayMode `CatFlightAndDashTests` 尚未實跑** —— Editor 短暫失焦後 Test Runner wedge（`tests_running` 卡死），需使用者點回 Editor 或重啟後重跑。選單 `Tools/Live2DAction/Add Cat Character + Camera` 已跑、接線與 `CatDodgeData.asset` 已確認、場景已存。
- **~~第一人稱下按 C 切貓 → 玩家整隻隱形只剩劍~~（2026-08-29 追加27 已修）**：`ThirdPersonCameraController` 第一人稱每幀隱藏玩家 `Visual` 的 Renderer（只留 `firstPersonVisibleWeapon`），`CameraPossessionSwitcher` 用 `SetActive(false)` 關玩家攝影機後那個 `LateUpdate` 停了、Renderer 沒被還原。修法：`OnDisable` 用 `_firstPersonHideApplied` 旗標還原隱藏。測試 `CameraPossessionFirstPersonRestoreTests`（PlayMode，尚未實跑）。切回玩家時仍第一人稱 → 會再隱藏一次（正確）。
- **貓咪三條 HUD ＋ 削韌機制（2026-08-31 追加74）**：使用者「為貓咪補上三個血量條 能量條 架式條」→「只在操控貓時顯示」＋「架式條接真機制」。
  - **削韌**：`CatCharacterSetup.BuildCat` 加 `StancePoise`（`maxStance` 50、`staggerDur` 4）＋ 接 `combat.stance`/`movement.stance`。drop-in 自動運作（訂閱 `Health.Damaged`）。**貓無硬直動畫**（Meshy 無 clip）→ 硬直＝定住 ~4s 無視覺回饋。
  - **⚠️ 削韌平衡待調**：貓 200 HP、削韌 5/擊、10 擊滿；25 傷/擊 8 擊打死 → **敵人打貓打死比打斷快**（同 player 的 balance-coincidence）。要讓貓真的會斷 → 調低 `CatMaxStance` 或調高貓 `stanceGainMultiplier`。
  - **HUD**：`CatCornerHud`（clone 自 `PlayerCornerHud`，生命/能量/架式 3 行，能量＝貓的飛行能量 500）＋ `PossessionHud`（`LateUpdate` 讀 `CameraPossessionSwitcher.Current`，操控貓時開 CatCornerHud / 關 PlayerCornerHud，右上角同位互斥、**不 gate 戰鬥狀態**）。`CatBarsWiring.cs`（選單 + `CatCharacterSetup` 結尾呼叫）。
  - **待實機**：HUD 位置/大小觀感、換人 toggle 有沒有一幀閃、削韌數值。`PossessionHudTests` 2 個 EditMode 綠（`LateUpdate` 自動 toggle 沒能實機看——凍幀）。
- **貓咪大招「黑暗劍氣」（2026-08-31 追加75）**：使用者「讓 cat 能量滿格時可以施放 [黑暗劍氣].mp4」。
  - 貓加**專屬技能能量** `Cat/SkillEnergy`（新 `UltimateEnergy` 子物件，100 / 20s 滿；跟飛行能量 500 那顆獨立）。`CatCornerHud` 能量條**改指 SkillEnergy**（追加74 原指飛行能量）→ 貓能量條現在＝大招充能；**貓飛行能量目前無條**（追加74「順帶做掉飛行體力條」回退了）。
  - `CatUltimateAbility`：R + `energy.IsFull` + 非硬直非死 → Consume + spawn VFX（`CatDarkQiSkillVFX.prefab`）+ AOE（`CatDarkQi.asset`：120 傷 / 3.2m / 擊退 9）。掛進 `catControl` → 只在操控貓時 R 施放。
  - VFX 素材 `幫我生成一個黑暗劍氣風格的版本.mp4` → ffmpeg 烘 `DarkSwordQi_Atlas.png` → flipbook（跟 player 大招同一套 `SlashFlipbookURP` premult blend）。`CatDarkQiVfxSetup.cs` 選單 `Add Cat Dark Sword-Qi Skill`。
  - **追加81 修「掉漆很嚴重」**：使用者問為什麼這特效很掉漆。**主因＝透明通道**：這支跟 `不要有人形.mp4` 一樣是**烘進 RGB 的灰色透明棋盤格**背景（追加75 註解寫「黑底」是錯的），追加75/80 的**亮度** alpha 鍵門檻只有 28、遠低於棋盤格淺方塊（~luma 74）→ 灰濁半透明霧。**改用 chroma（彩度）去背**：棋盤格純灰 chroma=0、特效暗紅/暗紫 chroma 高 → `alpha = max((chroma−10)/70, (maxc−92)/120)`。拿掉 `crop=720:720:400:0`（切掉外圈漩渦）→ 整張 frame + `drawbox` 除浮水印。8×8、64 幀、2560×1440。`_Brightness` 1.6→2.0（暗素材）。缺點：結尾灰煙低彩度鍵得淡。機制/AOE/能量沒動。ffmpeg 逐格 + in-engine 算圖確認棋盤霧消失。
  - **待實機**：大小/壽命/offset；~~120~~ **500** 傷（追加77）/ 20s 充能平衡；R 鍵位；SFX 從源影片音軌（追加78）。暗色調在實際深色場景 + bloom 應更明顯。
- **貓吃泉水 ＋ 普攻 50 傷（2026-08-31 追加76）**：
  - `HealingSpring` 重寫成多-occupant + 回復角色底下**所有** `UltimateEnergy`（貓的 `SkillEnergy` 大招能量現在也回；飛行能量不再被雙重灌）。player 行為不變。
  - `CatSwipe1/2/3` damage 6/7/12 → **50**（三段普攻）。**⚠️ `CatHeavy`(22)/`CatPounce`(16) 沒動 → 現在比一段普攻還低，蓄力/撲擊會很弱，等使用者定要不要等比拉高。**
  - 既有無關：場景 `HealingSpring_MainArea.healPerSecond` 序列化值是 40（程式預設 100，2026-08-19 註解說調到 100 但場景沒重序列化）——沒動（手調值權威）。
- **貓 R 500 傷 ＋ 血刀銜接右手 ＋ player 大招周邊特效換火焰光柱（2026-08-31 追加77）**：
  - `CatDarkQi.asset` damage 120 → **500**（貓黑暗劍氣）。待實機平衡。
  - **血刀 `BloodKatana.glb`** 掛 player 右手 `Rhand_Weapon2`，取代 Genshin「Wolf's Gravestone」背劍。結構 `Rhand_Weapon2 / WolfsGravestone(wrapper) / BladeMesh(GLB)`——**wrapper 保留 "WolfsGravestone" 名字**讓 `UltimateAbility.FindWeapon()`（靠名字比對）零改動照常運作；wrapper 把 pivot 移到握把、local −Y 對齊刀尖以配合 `ThrowSequence` 的 `FromToRotation(down, flightDir)` 假設。`PlayerKatanaSetup.cs` 選單 `Attach Blood Katana To Player Hand`（取代 `Player5WeaponSetup.cs`）。wrapper `localRotation` + `BladeMesh` offset 是**手調權威值**（刀握右拳、朝前下傾 ~19°），別改回公式。
  - **⚠️ 血刀來源授權待使用者確認**（`blood_katana_retextured.glb` 原始 mesh 來源未知）——`ASSET_LICENSES.md` 暫列佔位素材表、**確認前不得進對外 Build**。
  - **追加81 續 3（武士刀被剔除消失 + 狼末大劍放回背上）**：
    - **武士刀「不見」**：血刀 glb 匯入 mesh bounds 退化 (0,0,0)，`PlayerKatanaSetup` 只在編輯器 RecalculateBounds（不存下來）、執行期無 `MeshBoundsFixer` → Play mode 手離畫面中心整把刀被視錐剔除。修：`PlayerKatanaSetup` 在 wrapper 加 `MeshBoundsFixer`。
    - **狼末大劍放回背上**：新 `PlayerBackGreatswordSetup.cs`（選單 `Attach Wolf's Gravestone As Back Decoration`）把 `Genshin_WGS.fbx` 掛 **Player root**（直接掛、scale 1；不掛脊椎骨——骨骼 80x lossy scale 會把 localPosition 乘爆甩飛，第一版踩到）、**命名 `BackGreatswordDecor`（非 WolfsGravestone）故 R 大招不丟它**、接 **Main Camera + CatCamera 兩顆** `TPC.firstPersonHiddenAccessory`、自帶 `MeshBoundsFixer`。
    - **追加81 續 4**：使用者要「劍柄左上刀劍右下」＝從 git d735761/8ecb5fb 撈回 2026-08-23 手調 transform 原封還原：`localPos (1, −0.80115217, −0.2)`、`Euler(0,0,43)`、`scale 1`。（FBX 握把在模型 +Y 端，Euler 43° → 握把甩左肩、刀尖落右下。）
    - **R 大招**仍丟右手 `WolfsGravestone`（武士刀）。**`Genshin_WGS.fbx` 是《原神》仿製、DoNotShip**——放回去只是內部佔位裝飾，`DoNotShipBuildGuard` 已擋，日後換原創大劍。
    - 舊 `Player5WeaponSetup.cs` 仍在磁碟（現在被 `PlayerBackGreatswordSetup` 取代其角色，材質 `WGS_Top/Bottom.mat` 沿用）。
  - **player 大招周邊特效**：`不要出現人物_...mp4` → ffmpeg 烘 `PlayerUltimateAura_Atlas.png`（8×7、56 幀、亮度→alpha 門檻 46）→ `PlayerUltimateAuraVFX.prefab`（billboard flipbook + `Embers` 火花上升 + `GroundFlash` 地閃）。`PlayerUltimateAuraVfxSetup.cs` 選單 `Add Player Ultimate Aura VFX (R ultimate cast)`，接 `UltimateAbility.castVfxPrefab`、offset (0,1.3,0)、`SizeHeight` 4.6。
  - **刪除**：`Assets/_Project/VFX/Skills/SwordOrbit/` 整包 + `SwordOrbitVfxSetup.cs`（被 PlayerUltimateAura 取代）。
  - **待實機**：刀在拳心的位置/角度/大小、刀刃平面朝向（wrapper roll 任意）、R 拋刀動畫用新 wrapper 的表現；火焰光柱在實際場景 + bloom 觀感（上半截殘留淡霧要不要再推 alpha key）、大小/offset/壽命、施放時機對齊 spin-up。game-view MCP 截圖抓不到粒子（同追加69/75），scene-view simulate 確認有渲染。EditMode 236，唯一失敗仍是既有無關 `...FallsBackToAttack3`。
- **移除 player 普通攻擊特效 ＋ R 大招加音效（2026-08-31 追加78）**：
  - **player 普攻特效移除**：`LightAttack1/2/3.asset` `hitEffectOverride` 清空、`alwaysSpawnHitEffect` 0（原本指向 `Attack01/02/03.prefab` 動漫劍氣）。使用者要把 player 近戰從**拳頭改揮刀**、後續做**隻狼式對打（拚刀/彈反）**——這則只做「移除特效」，動畫替換＋對打機制**另開**。落地命中仍有 `PlayerCombat.hitEffectPrefab` 共用小火花（沒動）。
  - 這三顆 AttackData 是 Player/TrainingDummy/中立者1-3 **共用** → 那幾隻劍氣也一起沒了（一致）。敵人 `Attack3SlashEffect.prefab`（`EnemyAttack3`/`GiantAttack3`）**保留不受影響**。
  - **刪除**（player 專屬、無其他引用）：`VFX/Slash/Prefabs/`（Attack01-03）、`Attack01-03Mat.mat`、`SoftDotAlphaMat.mat`（孤兒）、`Attack01-03_SpriteSheet.png`、`Source/SlashSourceVideo.mp4`、`SlashVfxSetup.cs`、`SlashVfxSpawner.cs`（無場景引用的預留類）。**保留**：`Attack3SlashEffect*`、`SoftDotAdditiveMat.mat`+`SoftDot.png`（R 大招 accent 粒子共用）、`SlashVfxController.cs`、`SlashFlipbookURP.shader`。`Attack3SlashEffectSetup.cs` 選單改名 `Rebuild Enemy Attack3 Slash Effect`、拿掉 `WireToLightAttack3`（不再碰 player AttackData）。
  - **後續（未做）**：拳頭→揮刀動畫可用專案內既有 `CombatAnimations/TC_Sword_Free_Pack/`（`KBS_Sword_ATK_Combo_01` 等）retarget；隻狼式拚刀/彈反是更後面。
  - **R 大招音效**：兩支來源影片（`PlayerUltimateAuraSource.mp4`/`DarkSwordQiSource.mp4`）內含 AAC 音軌（使用者自有）。ffmpeg 抽軌裁切+淡入淡出+loudnorm → `Assets/_Project/Audio/Skills/PlayerUltimateAura_Cast.wav`(4.6s)、`CatDarkQi_Cast.wav`(2.9s，**input seek**——這支 output seek 會抽出靜音)。兩個 VFX prefab root 加 3D `AudioSource`（playOnAwake、vol 0.85、log rolloff 4~50m），Instantiate 即播。`SlashVfxController.Destroy` 存活時間現在也算進 `AudioSource.clip.length`（音效比視覺長不被切）。由 `*VfxSetup.cs` 的 `ConfigureCastAudio()` 接線。
  - **待實機**：音效音量/衰減/裁切長度合不合施法節奏；player 現在普攻**無揮擊特效**（預期，改揮刀前的中間狀態）。EditMode 236，唯一失敗仍是既有無關 `...FallsBackToAttack3`。
- **player R 待命光環 = 火焰（能量滿格＝必殺可用），奇犽風閃電已移除（2026-08-31 追加81）**：
  - `UltimateReadyAura` 現在只做「`energy.IsFull` → `flameAura.SetActive`」。2026-08-16 的繞圈電光（`bolt` / `LightningAuraUtility` / `UltimateReadyAuraSetup.cs` / `LightningBolt.mat` / 場景 `Player/UltimateReadyAura` 子物件）**整條刪掉**（使用者:「移除舊特效(白色一圈的那)」）。`UltimateActivationBurst`（R 施放瞬間金色衝擊環）保留。
  - **火焰 = 純來源影片**：prefab 只有一個 billboard flipbook 播 `PlayerUltimateAura_Atlas.png`。追加79/80 疊的 front 層 / 自製 Embers+GroundRing 粒子 / `_Brightness` 1.3 / 中央裁切**全拆**。atlas 重烤：完整未裁切 frame、穩定火柱段 54 格、`drawbox` 除浮水印、亮度鍵 threshold 52（recipe 在 `PlayerUltimateAuraVfxSetup.cs` 檔頭）。單層 `_ZTest=Always`、premult、`_Brightness` 1.2。
  - **尺寸**：使用者要「忠於來源比例，略大於角色」→ 不壓身高。`SizeHeight 2.7`（火焰 ~1.9m）、`SizeWidth = ×1280/720`（地環維持圓形 ~3.8m）、`AuraLocalOffset.y 0.31`（地環落腳底）。
  - 音效 `PlayerUltimateAura_Ready.wav` 不動（能量剛滿播一次 stinger）。
  - **待實機**：`cam.Render()` 算圖這輪一直 tonemap 爆糊，沒能截圖目視收尾——atlas 內容已用 ffmpeg 逐格檢查（火柱/細地環/藍舌/火花都在、浮水印已除）。尺寸/位置（2.7m 高、地環 3.8m）、`_Brightness`、迴圈接縫都是 `PlayerUltimateAuraVfxSetup.cs` 一行常數、重跑選單即調。EditMode **220**（少 16 個 `LightningAuraUtilityTests`），唯一失敗仍是既有無關 `...FallsBackToAttack3`。
  - **追加81 續**：使用者「player 施展 r 技能原來的特效不見了 就是一把劍的旋轉砍擊」——復原 SwordOrbit（追加69 建、追加77 刪）當 R **施放**特效（跟待命火焰是兩回事）。源 `不要有人形.mp4` 仍在 `~/Downloads/3d遊戲資源/`，重新歸檔 `Assets/_Project/VFX/Skills/SwordOrbit/`。**這支背景是烘進 RGB 的灰色透明棋盤格**（非黑底），亮度鍵門檻拉到 60 才鍵乾淨。`SwordOrbitVfxSetup.cs` 選單 `Add Sword Orbit Skill VFX (R ultimate cast)` 建 `SwordOrbitSkillVFX.prefab` 接 `UltimateAbility.castVfxPrefab`（追加79 移除、追加81 加回 + `castVfxLocalOffset (0,0.4,0)`）。borrow-Main-Camera 算圖確認光絲環繞幽靈劍、棋盤格鍵乾淨；offset/size 待實機微調。無音效（原版也沒有）。
- **貓咪近戰機制（2026-08-29 追加30，切片 2，見 `Docs/CAT_COMBAT_DESIGN.md`）**：連段/蓄力/撲擊/空中攻擊/命中反饋實作、EditMode 綠、選單接線已驗。**敵貓已取消**（使用者看到後決定不要，`EnemyCat*` / `EnemyCatSetup.cs` / `CatEnemySwipe.asset` 已刪；標靶用現有 `TrainingDummy` / `Enemy`）。**待處理**：(1) **`CatAttackPose` 骨頭旋轉軸/角度是眼估起點值**（`CatCharacterSetup.WireAttackPose` 的 `defs`）——脖子鏈 `Bone_028→027→026` 繞 local X 已截圖驗證正確、頭部 pose 已據此調成雙骨頭 ~32°；前掌鏈 `Bone_034/032/042/040` 仍待實機看；(2) 所有 frame data（`CatSwipe1-3`/`CatHeavy`/`CatPounce.asset`）、hitstop 長度（`CatCombatFeedback`）、撲擊距離（`CatPounce` serialized）、震動強度都是起點值；(3) **SFX 無聲**——`CombatSfx` clip 欄位留空（專案還沒有原創戰鬥音效，rule 1）；(4) 貓死亡＝ 5 秒 in-place 復活（`RespawnController` on `GameManager`，玩家貓一顆）。**附身貓時貓死掉：`CameraPossessionSwitcher`（`catHealth` 欄位）自動 `FocusPlayer()` 交還控制，復活前那 5 秒不能再附身回去；復活後按 C 重新附身**。沒有倒下動畫／死亡特效（只是消失 5 秒再出現，原地）；(5) 撲擊觸發：**只有在「跑」（水平速度 ≥ `moveSpeed×0.7`）＋ 按方向鍵時**點左鍵才撲擊（追加30 修「有時普通攻擊也會衝刺」）——站著/貼牆/剛起步/放鍵滑行都是普通揮爪。門檻在 `CatPounce.pounceMinSpeedFraction`；`PlayerInputProvider` 已改 `[DefaultExecutionOrder(-100)]` 讓輸入不再晚一幀；(6) PlayMode `CatMeleeCombatTests` 尚未實跑（Play mode 失焦凍結，coroutine 卡在第一個 `yield`）。
- **Play Mode 失焦凍結會讓 PlayMode 測試 coroutine 永久卡住**：MCP 下跑 PlayMode 測試,若 Editor 沒 OS 焦點,`[UnityTest]` 的第一個 `yield return null` 就再也不 resume（Time.frameCount 凍在 1-2,見上面「Play Mode 失焦」節）,`get_test_job` 一直 `stuck_suspected`/`completed:0`。救法:`manage_editor(stop)` + `EditorUtility.RequestScriptReload()` + `run_tests(clear_stuck:true)`,然後**請使用者滑鼠點進 Editor 視窗**再跑。EditMode 測試不受影響（不進 Play mode）。
- **SkinnedMeshRenderer 序列化 bounds 退化 (0,0,0)**：跟鳥居 `.glb` 同一個 glTF 匯入坑（見上面「空島池塘」那節的 `MeshBoundsFixer` 說明）。這次的解法是 `CatCharacterSetup` 把 `smr.updateWhenOffscreen = true`，執行期每幀由 live 骨頭重算 bounds，不會被視錐剔除。若之後重匯入 glb 後貓在某些角度消失，確認這個旗標還在。
- **兩台 Camera 都 tag `MainCamera`**：`Main Camera` 與 `CatCamera`，靠 `CameraPossessionSwitcher` 的 SetActive 硬切，同一時間只有一台 active，所以 `Camera.main` 永遠只解析到 active 的那台（實機確認 `allCamerasCount=1`）。Maya visual prefab 內另有一個 `Maya/MainCamera` 但不是 live camera（`allCamerasCount` 不含它），與此無關。
- **三視角切換（2026-08-29 追加30 再修2）**：C = player ↔ cat 附身；T = 守望者視角開關（`ViewFocusDirector`，從 player **或 cat** 都能按，接管當下 active 的相機，回來時回到你剛剛附身的那個）。`ViewFocusDirector.catCamera`/`catController` 與 `suspendWhileWatching`（含貓控制組）、`CameraPossessionSwitcher.viewDirector` 由 `WatcherCatWiring` 雙向接線（`CatCharacterSetup` + `WatcherSetup` 結尾都呼叫）。**守望者視角中 C 和 T 都能離開**（回到你剛剛附身的角色；再按 C 才切 player↔cat）。守望者視角的 W/A/S/D 平移相機不驅動角色。若附身貓時貓死掉且正在看守望者：`CameraPossessionSwitcher` 自動 `FocusPlayer()` → `ViewFocusDirector` 偵測到相機被 SetActive-swap → 自動回 Player 視角（已在 LateUpdate line ~369 處理）。
- **攝影機手感未經人眼互動確認**：Play Mode 失焦凍結，只用 reflection 驅動 + 算圖驗證。`CatScale` / `CatCam*` 常數在 `CatCharacterSetup.cs`，改完重跑 `Tools/Live2DAction/Add Cat Character + Camera` 即套用（會重建 Cat/CatCamera/CameraPossession，不動玩家 rig）。
- **貓咪也能開車 ＋ 開車中 C 切角色（2026-08-29 追加55→56）**：`VehicleEntrySystem` 重寫成「當前操控角色」感知（`possession` 欄位）＋ GTA 式狀態模型。
  - 唯一旗標 `SeatedCharacter`（None/Player/Cat）；「正在開」= `SeatedCharacter == switcher.Current`（derived）。`LateUpdate` 每幀對帳車引擎 + 相機。`[DefaultExecutionOrder(-50)]`。
  - **C**：永遠可切 Player↔Cat。切離駕駛 → 車自動熄火停好、駕駛留座位（貓看得到）；切回 → 引擎重開繼續開。
  - **F**：車旁 → 上車開；你是駕駛 → 下車；座位被另一隻佔著 → 無作用。
  - `CameraPossessionSwitcher.Apply()`：切到「被停在座位的角色」時不開它自己的相機/控制（VES 管）。追加55 的「開車中忽略 C」已移除。
  - VES 加 `viewDirector` 欄位（守望者 T 視角期間 LateUpdate 讓位）。
  - 貓控制 consumer（movement/PlayerCombat/CatChargeAttack/CatPounce/CatAerialJudgment）+ CharacterController 進座位時 forced off；`CatProceduralWalk`/`CatAttackPose` 不停用（自己 ease 回中性姿）。
  - 貓座位 `CatDriverSeatAnchor` car-local `(0,0.55,-0.1)` 起點值。新 `VehicleCatWiring.cs`（`Tools/Live2DAction/Wire Cat Into Vehicle` + `CatCharacterSetup` 結尾呼叫）可重跑接線。`possession` 留空 = 退回 player-only 原行為。
  - **待實機**：座位高度/姿勢、下車落點、C 切離/切回駕駛的相機順暢度、開車中 T 守望者再回來。
  - **追加57**：改**雙人座**。`PlayerSeat`/`CatSeat` ∈ {None, Driver, Passenger}。**F** = 駕駛座空就進、被佔就上後方平台當乘客、都滿無作用；在車上任何座位 → 下車。**C** = 照舊切控制（控駕駛→能開；控乘客/地面另一隻→車停）。想換人開得兩隻都下車再 F。相機：駕駛 VehicleCamera、乘客看自己的第三人稱。`SeatedCharacter` enum → `Occupant` + 新 `Seat` enum；switcher 改用 `DriverOccupant`/`PlayerSeat`/`CatSeat`。**PLAYER 不再隱藏**（`playerRenderersToHide` 清空）。4 個座位錨點由 `VehicleCatWiring` 建（Buggy 子物件）。**追加58→59** 分位置調過。**追加59**：貓改**面朝行進方向**（`CatDriverSeatAnchor`/`CatPassengerAnchor` 拿掉 yaw 180，euler (-6,0,0)）；**player 坐上車裁下半身**——`VehicleEntrySystem.playerCollapseBones`（兩根 UpperLeg 骨，`Mount` 縮 scale 到 `playerCollapseBoneScale`=0.02、`Dismount` 還原）+ `playerRenderersToHide`=翅膀 renderer（`Wings` 下的 `polySurface2197/2631`，垂到車底）。`VehicleCatWiring` 用 `Wings` 名 + Animator `GetBoneTransform` 自動接。**追加60**：player 駕駛時 `playerAnimatorToFreeze`（`Player/Visual` Animator）`Mount` 關 / `Dismount` 開（Idle 凍結）；貓駕駛 euler → **(-40,0,0)**（面前+大仰角）；貓後座 euler → **(-8,180,0)**（朝車尾）。座位錨點：`DriverSeatAnchor` (0,0.62,0.12)/0；`CatDriverSeatAnchor` (0,0.72,-0.05)/(-40,0,0)；`PlayerPassengerAnchor` (0,0.55,-1.3)/0；`CatPassengerAnchor` (0,0.72,-1.3)/(-8,180,0)。**player 坐姿待 Play 確認**；骨頭爆裂就把 `playerCollapseBoneScale` 調 0.05。**貓模型胸口有米色球體 artifact**（`output_unwrapped` Meshy，一直都在，非車輛改動造成）。**追加61**：V 第一人稱駕駛時藏駕駛模型（`VehicleCameraController.IsFirstPerson` + `VehicleEntrySystem.SetAllRenderers`，只在 FP 狀態變化時 toggle）—— 追加57 起駕駛看得到，cockpit view 會直接對著臉。**追加62**：後座 y 提高（`PlayerPassengerAnchor` 0.55→0.9、`CatPassengerAnchor` 0.72→1.15，坐在綠色 deck 上，該面板視覺頂 ~world 2.0-2.2 > box collider 頂 1.77）；翅膀改成 `playerHideObjectsWhileSeated` `SetActive(false)` 整個 `Wings` 物件（`WingFlap` 每幀重開 renderer，追加59 的 renderer 隱藏沒用）。
  - **追加67：車輛 Ctrl 飛行**。`VehicleFlightController`（Buggy）+ `VehicleFlightState`（純類，18 EditMode 綠）+ `VehicleFlightData.asset`（`Settings/Movement/Vehicle/`）+ `FlightEnergy` 子物件（`UltimateEnergy` 500/5/1/3）。選單 `Tools/Live2DAction/Add Vehicle Flight (Ctrl)` 接線。原理照 player：按住 Ctrl 起飛+爬升、放開懸停、體力耗盡自動墜落、重進需 30。鍵位：Ctrl 爬升 / **Space 下降** / Shift 加速巡航 / W-S 推進 / A-D 偏航。
    - **落地**（追加67 續）：按住 Space 下降到離地 ≤ `landingClearance`(1.6m) 且沒按 Ctrl → 自動落地；按 Ctrl 可中止。離地高從 raycast（忽略車自身 collider）算。
    - **抖動修正**（追加67 續）：飛行時 `VehicleController.FlightModeActive` setter 會停用 4 個 WheelCollider（擦到幾何會自跑懸吊力打架）＋ 把 `Rigidbody.interpolation` 切成 `Interpolate`（地面預設 `None`）；姿態改用自持 `_flightRot` 不讀回物理旋轉。
    - **輪胎拉伸修正**（追加67 續 2）：飛行時 `VehicleController.FixedUpdate` 也**跳過 `WheelVisualSync.SyncVisual()`** —— 它把輪骨釘在 `WheelCollider.GetWorldPose()`，disabled collider 回傳地面舊姿態，車身飛走輪骨留地上就被拉伸。跳過後輪骨保持起飛時 local 姿態跟車飛。
    - **待實機**：起飛/巡航/爬升手感、迴正快慢、Space 落地時機、`VehicleCameraController` 水平跟隨相機在車爬升時偏平（v1 沒動相機）。數值全在 `VehicleFlightData.asset`。撞牆時直寫 velocity 會蓋掉碰撞反應（knockback 仍觸發）。

## 十足蟲近戰敵人（2026-08-31 追加83，`TenLeggedBugController`）

`十足蟲.glb`（Meshy AI 付費輸出，`UniRig` 綁定、無動畫 clip）做成高速近戰敵人。全新控制器，沿用 `Health`/`IDamageable`/`NavPathFollower`/`BossTeamMember`/`CharacterController`，**沒改任何既有玩家/敵人系統**。

- **10 條腿（追加83 續 4 重驗，先前抓到 8 是錯的）**：用地面接觸頂點分群確認 = **10 腳掌、5 對對稱**。控制器吃任意腿數、`1→N→重複`。10 骨對照（swing / knee）見 CHANGELOG 追加83 續 4。**辨識腿的正確方法**：`SkinnedMeshRenderer.BakeMesh` → 取 `y<0.12` 頂點 → XZ 分群數腳掌，別數「乾淨骨鏈」。
  - **腿 1（Bone_046）**掛在頭鏈底下，權重混身體 → 踏步時腿根網格輕微拉扯（不會帶動頭）。
  - **腿 6 & 8 共用 Bone_025**：`Bone_025`（腿 6）是 `Bone_024`（腿 8）的父骨 → 踏腿 6 拖腿 8。
  - 兩者要根治得 Blender 重綁。`legBendBones` 逐腿給時控制器照用不自動補（否則腿 6 會偷抓腿 8 的骨）。
- **初版頭尾接反已修（追加83 續）**：`Bone_002` 其實是尾/螫刺、真頭是 `Bone_005→Bone_004→Bone_048` 鏈。已改 `Rig` wrapper 轉回 identity + `hornBone=Bone_004`。**辨識頭尾的正確方法**：逐骨標記渲染 + `BakeMesh` 找該半邊最高頂點（角是高結構），別只看 bind-pose bone 座標猜。
- **「攻擊搆不到玩家」已修（追加83 續）**：`TickAttack` 原本過 `attackRange` 就硬停留空隙。改成算 `contactDistance`（自身半徑+`playerBodyRadius`0.45+buffer）、貼上前繼續前壓、貼上才 plant+揮；角 hitbox 加 `StaticOverlapCheck`（貼身開窗補一次 OverlapBox）。`attackRange` 1.6→1.3。
- **血量條 / 5 秒復活 / 純觀看攝影機（追加83 續 2）**：
  - 血量條：`WorldSpaceHealthBar`，寬度 = `BakeMesh` 量的身長 × 0.9（world ~0.86 m）。**billboard 對 `Camera.main`**——Editor 靜態截圖會看到側面細線，Play 才對得準。
  - 復活：控制器內建 `DeathThenReviveSequence`（翻腹 → 躺 `respawnDelaySeconds` 5s → `Health.ResetHealth` + 站起 → Patrol）。GameObject 不 destroy，靠 `deferDeactivationToDeathAnimation=true` 讓 coroutine 活著。`respawnMode` InPlace/AtSpawn 可選。
  - 攝影機：按 **B** = `SpectatorCameraToggle`（`BugSpectator` 物件），**純換視角、不換操控**，蟲照跑 AI。`BugSpectatorCamera` 低視線（dist 1.5 / offset (0,0.35,0) / pitch 17°）。再按 B 還原原相機。
  - setup 清舊物件改掃 scene roots（`GameObject.Find` 跳 inactive → 之前重跑生重複相機，已修 + 手動清）。
- **位置 / 視角 / 駕車切換（追加83 續 3）**：
  - `十足蟲` 移到 **(12, 0.52, −13.5)**（Ground 右下角、貼南牆、與 `屁孩王` (12,12) 同 X 直線）。`patrolRadius` 5。**換位置了要重跑 `Bake Navigation Mesh`**。
  - spectator 相機壓低：`distance` 1.05 / `targetOffset` (0,0.12,0) / `initialPitch` 4° → 相機世界 Y ~0.72（蟲眼高度）。太貼牆時 obstruction cast 可能把相機往前拉，Play 觀察。
  - **駕車時切不了蟲視角**已修：`VehicleEntrySystem.LateUpdate` 每幀重申 vehicleCamera。`SpectatorCameraToggle` 加 `LateUpdate`（order 150 > VES 的 0）每幀重申 spectator 狀態、覆蓋掉。通用（vehicle / 守望者 / possession 都吃）。
- **值全在 Inspector**（`EnemyAI` 慣例、偏離規則 7 但依規格要求）：偵測 12 / 脫離 16 / 攻擊 1.6 / 追蹤速 5.5 / 巡邏 1.4 / 轉向 360°/s / 攻擊錐 30° / 攻擊循環 1s / 角三段 0.25·0.45 / 命中窗 0.28–0.45 / 角傷 10 / 連段 3 / 硬直 0.7–1s / 搜尋 2–3s / 翻腹 0.65 / 屍體 1s / 淡出 1.25。
- **身體碰撞 = CharacterController**（專案統一膠囊，玩家自己也靠它被打）。角部 = `Bone_002` 底下 `HornHitbox`（BoxCollider trigger，**預設關**，`TenLeggedBugHornHitbox` swept BoxCast + per-activation 去重，只在下刺命中窗開）。**腳無碰撞器**（規格）。
- **朝向修正**：原模型頭朝 local −Z，setup 工具在 prefab 加 `Rig` wrapper（Y 180）讓 root +Z = forward；因此模型左右對調，腿編號對照已含這個翻轉。
- **死亡淡出**：`BuildFadeMaterials` 把 renderer 換成 URP/Lit Transparent 材質**實例**（帶原 BaseMap），lerp `_BaseColor.a` → Destroy。不碰共用資產。⚠️ Build 時若 URP/Lit shader 被 strip 會失效（vertical slice 風險低，正式要加到 Always Included）。
- **測試**：EditMode `TenLeggedBugTests` **11 綠**（gait 一次一腳 / lift 峰值 / stride 前後掃 / 速度縮放 / 錐角 / 角三段 / 命中窗 / rest-pose 快照）。PlayMode `TenLeggedBugPlayModeTests`（4 個）**MCP test runner 卡死沒跑成**（`tests_running` wedge，見第 2 節「失焦陷阱」）——**待使用者點回 Editor 重跑**。以 execute_code 手動步進 Update 驗過狀態機（遠→Patrol、近→Chase、正面→Attack、背後→timer 歸零不揮）。
- **待使用者**：跑 `Tools/Live2DAction/Bake Navigation Mesh`（`ExcludeFromBake`/`PathFollowerTargets` 已加 `十足蟲`）→ Play 手感微調 → 跑 PlayMode 測試 → 骨頭指派如要調（目前 setup 已自動填 8 腿+主幹+角）。步態幅度/相位、角三段角度、攻擊/硬直節奏都在控制器 Inspector。
- glb 匯入時 SkinnedMeshRenderer bounds 退化 → prefab 上 `smr.updateWhenOffscreen=true`（先前擺件時已設）＋ 控制器不依賴 renderer bounds。

## 滑鼠右鍵 = 武士刀格擋 ＋ 射擊系統退役（2026-08-31 追加86）

使用者:「把滑鼠右鍵改成武士刀防禦，移除射擊系統和步槍資產可以保留到以後」。完整改動見 `CHANGELOG.md` 追加86。

- **右鍵現在是格擋，不是瞄準**。`PlayerInputProvider`：右鍵 held → `IInputCommand.GuardPressed`（新 default member `=> false`）；`AimPressed`/`FirePressed` 恆 `false`；`AttackPressed`/`AttackHeld` gate 在 `!GuardPressed`（舉刀時左鍵不揮刀，放開才揮）。
- **`Combat/PlayerGuard`**（Player root）：按住右鍵 → 正面錐（`guardArcDegrees` 150°）內的傷害 HP ×0.15，**架勢照樣全額累積**（`ExplicitPoiseAmount` 設未減傷值，`poiseMultiplier` 需與 `StancePoise.stanceGainMultiplier` 0.2 保持一致 —— 兩者耦合，改一個要改另一個）；背後/側面不擋。舉刀時 `CharacterMovement.ExternalSpeedMultiplier`（新欄位）= 0.35。程序化舉刀 pose 疊在 `Bip001-R-Forearm` 上（`LateUpdate`，佔位手法，方向不對翻 `invertPose` 或調 `guardPoseLocalEuler`）。
- **傷害管線**：`Health.ApplyDamage` 現在會套用同物件上的 `IIncomingDamageModifier`（新泛用介面，`PlayerGuard` 是唯一實作）。沒有 modifier 時零行為改變，既有 240 EditMode 全綠。
- **射擊系統退役但沒刪**：Player 移除 `RangedWeapon`/`RangedAttackDistance`/tracer `LineRenderer`/root `AudioSource`，場景移除 `RangedWeaponHud` + 右手 `AK47` 實例。`RangedWeapon.cs` / `AK47.fbx` / `RangedWeaponSetup.cs` / `GunshotSfxSetup.cs` / `GunshotSfx.wav` **都留在磁碟**。`RangedWeaponSetup` menu 重跑會加回 component，但 `AimPressed` 恆 false → 不會走火，得先在 `PlayerInputProvider` 恢復 aim/fire 綁定。
- **測試**：EditMode `PlayerGuardUtilityTests` **8 綠**（240/240 全綠）。PlayMode `PlayerGuardPlayModeTests`（4，正面減傷/背後全傷/無盾全傷/減速還原）**又卡在 `editor_unfocused`**（見第 2 節「失焦陷阱」）—— **待使用者點回 Editor 重跑**。
- **動畫（回答提問）**：專案內已有 `TC_Sword_Free_Pack`（MocapOnline，需 retarget 到 Bip001）。線上補：Mixamo（katana/block/parry/hit-react/death）、Unity Asset Store 免費 Mecanim 劍術包、MocapOnline 付費 Sword & Shield、itch.io。登入/下載由使用者本人（規則 11）。

### 追加88 調整（2026-09-01）
- **格擋姿勢改「負斜率」（刀尖左上刀柄右下）**：`PlayerGuard` 現在轉**兩根骨** —— `upperArmBone`(`Bip001-R-UpperArm`, `upperArmGuardLocalEuler`=`(-30,-40,-18)`) + `swordArmBone`(`Bip001-R-Forearm`, `guardPoseLocalEuler` 改成 `(-55,25,-165)`)。前臂單獨轉抬不起手做不出跨身格擋。離線 render 掃出來的，**Play 時是疊在 idle 動畫上，角度會有差**，兩個 euler 都在 Inspector 可調。⚠️ `BackGreatswordDecor`（背上紅色大劍裝飾）從背後鏡頭會擋住武士刀，它不受 PlayerGuard 控制。
- **左鍵音效 → 防禦刀刃碰撞**：`PlayerMeleeSfx`(每段 `PlayerCombat.Hit` 響) + `PlayerMeleeSfxSetup.cs` 刪除。新 `Combat/PlayerGuardClashSfx.cs` 訂閱 `PlayerGuard.Blocked`，只在 `info.Source` 有 active 的 `BossHitbox` 且 `ActiveWindowPart==Weapon`(新 getter) 時放 `KatanaClash.mp3`。擋踢(RightFoot)不響。`clashOnAnyBlock` 開關可放寬。選單 `Add Player Guard Clash SFX`（`PlayerGuardClashSfxSetup.cs`）。左鍵普攻現在無音效。
- **武士架勢回復調慢**（只動武士 StancePoise，使用者:「我累積的一下就被回掉」）：`regenPerSecond` 20→8、`regenDelaySeconds` 1.5→3。`maxStance`(60)/`gainMult`(0.2) 不動。
- **待使用者 Play 確認**：正面擋傷/背後不擋/舉刀變慢/放開才能揮刀、**新格擋姿勢刀刃斜率**、**擋武士刀有 clank 擋踢沒有**、**武士架勢回復手感**。手感值全在 `PlayerGuard` / 武士 `StancePoise` Inspector。

### 追加90 修：武士架勢滿了不做蹲下動作
`BossStateMachine.UpdatePostureBroken()` 沒防 `CrossFadeInFixedTime` 一幀延遲（`AnimatorHasFinished()` / 攻擊路徑早有這個 guard）。破防幾乎都發生在 `Attack` state 中 → 破防當幀 `GetCurrentAnimatorStateInfo(0)` 還回報離開中的攻擊 clip，其 `normalizedTime` 已 > `PostureKneelNormalizedTime`(0.5) → 舊碼立刻 `animator.speed=0` 把 Animator **凍在攻擊姿勢 frame 0**，跪地 clip 沒開始播 → 「破防只是卡住不跪」。修法：取 `normalizedTime` 前先確認 Animator 真的進到跪地 state（`!IsInTransition(0) && stateInfo.IsName(kneelName)`）＋ 1.5s crossfade 沒 land 的 fallback。Play 實測：破防→跪姿凍 3s→站起→回 combat，正常。**兩隻 boss 共用此方法，屁孩王同步修好。**

## 連續刺刀動作：追加87 加入 → 追加89 退回（clip 不堪用）

使用者的 `連續刺刀.zip`（Meshy `Meshy_AI_Parkside_Portrait_biped` Animation，without_skin）。

**⚠️ 追加89（2026-09-01）：這支 clip 目前已從場上停用。** 使用者回報「打空氣、亂位移」→ 離線量測證實 `ContinuousThrust.fbx` 是**旋身撲擊**不是連續直刺：髖部單調前+側漂 ~1.5m（不回來，`lockRootPositionXZ` 只清 root 淨位移、per-frame 前進烤在髖曲線 → root 不動但可見身體走出去）、chest yaw 一段內擺 ±130°。**調 tuning 修不了動畫資料**。已從 `武士 normalAttackPool` 移除、Player F 處決退回 `FlyingKick`。**FBX / asset / controller state 全留磁碟**，clip 修好（Meshy 重生成強調站定直刺 / 換 Mixamo sword thrust / Blender bake 掉位移）後重接即可。

**追加89 留下來的通用改動**：`BossAttackDefinition.faceTargetSnapOnStart`（新 bool，true = 攻擊進入時 snap yaw 對準目標，不只用 startupTracking 慢慢轉）；`ExecutionAbility.BeginExecution` 先 snap 對準被處決目標（對 FlyingKick 也有效）。

---
以下為追加87 原始記錄（動作本身已停用，機制/pipeline 仍有效）：

- **一支 clip 兩邊共用**：`CombatAnimations/Meshy/ContinuousThrust.fbx` 匯入 **Humanoid**（3.03s / 92 幀），`isHumanMotion=True` → 同時 retarget 到 Player5Avatar + WushiAvatar。武士 4× 縮放對 Humanoid retarget 無影響。import：`keepOriginalPositionY=1`（防下沉，比照 `Wushi_DoubleCombo`）+ `lockRootPositionXZ=1`（原地化，Meshy「起點在 root 後方」）。
- **武士**：`Wushi.controller` 加 state `Wushi_ContinuousThrust`（speed 1.25）+ 新 `Wushi_Attack_ContinuousThrust.asset`（5 段連刺 hit window，離線 `AnimationMode` 量測；每刺 1% 血、5 刺 ~5%；jab5 nt 0.575-0.665 最深 dmgMult 1.3）+ 加進 `BossStateMachine.normalAttackPool`（→6）。
- **Player F 處決**：**Maya `NewAnimator` 的 `Execute` state 不能直接改** —— `中立者1` 也用該 controller **且也掛 `ExecutionAbility`**（`守望者` 也用該 controller）。改法：`ExecutionAbility.cs` 加 `executeTriggerName`（預設 `"Execute"`），Maya controller 加**新** `ExecuteThrust` trigger + state（speed 1.35），Player 的 `ExecutionAbility.executeTriggerName="ExecuteThrust"` + `executionAnimationSeconds` 1.5→2.25。`中立者1` / Arisa `EnemyExecutionAbility` 維持 `Execute`/FlyingKick。
- **待使用者 Play 確認**：
  1. Player 處決可能浮 ~5-10cm（離線量最低頂點 ~0.55 vs CrossPunch 落地 ~0.45）→ 調 `ExecuteThrust` state 或 Visual Y。
  2. 武士 hit window 離線量的，要 in-Play（frames-advancing）確認刺得到玩家；刺不到就上 `useRootMotion=1`（+ 補「root-motion attack 被打斷清 `_currentAttack`」的坑）。
  3. Meshy clip 手感天生要調（見上面 2026-08-28 條）—— speed / windows / 傷害 poise 都在 asset。
  4. PlayMode 測試 MCP runner 卡 `editor_unfocused`，待點回 Editor 重跑。

## Boss 開場演出轉正接入 GreyboxTest（追加92, 2026-09-01，未經人眼互動確認）

追加91 的隔離 demo 已接進真正的 `GreyboxTest.unity`（選單 `Tools/Live2DAction/[Boss Intro] Wire Into GreyboxTest`，可重跑）。走進 `武士` 前方 `BossRoomTrigger`（z≈4）→ `BossIntroManager.StartIntro()` 關玩家控制/UI/相機腳本 + 切過場相機 rig + 播 `BossIntro_Greybox.playable` → Timeline `stopped` → 全開回來 + `Main Camera` 接手 + `BossStateMachine.ForceEngage()` 直接進戰鬥。設計/決策見 `Docs/BOSS_INTRO_EXPLORATION.md` §9。

- **EditMode 244/244 綠**、零編譯錯誤。Play（Editor 失焦、frame frozen）只驗到控制交接兩半：`StartIntro` 關控制 + rig active + `Main Camera` inactive ✅；模擬 `stopped` 全開回 + `boss.CurrentState==Alert`（`ForceEngage` 有觸發）✅。
- **待使用者在有焦點 Editor 手動 Play 完整跑一次**：走進 trigger → 過場 3 鏡頭（Back/Face/Action 硬切）+ 起手式動畫 + Signal（刀光/鏘/相機震動）跑完整段 → 交還控制 → 武士追上來對打。
- **相機框景是第一版估值**（rig 底下 `CM_Vcam_Back/Face/Action`，針對 4× `武士` @ (0,0.6,11)）——Scene view 直接拖 vcam 微調，非程式改動。`BossIntroGreyboxSetup.cs` 常數重跑即覆蓋。
- **待觀察**：過場中 `BossStateMachine.enabled=false`、Animator 由 Timeline 接管；`ForceEngage` 後若看到一幀 T-pose / 姿勢跳動，需在 `BossIntroManager.RestoreControl` 補 Animator rebind。
- `BossRoomTrigger` box 只擋 x −15..15，玩家從場地邊緣繞行仍可能直接踩 `alertRange`=6；greybox 夠用，正式關卡要實體門檻。
- `GreyboxTest.unity` 存檔照例帶 ~25k 行 Live2D mesh 重序列化 churn（與追加90/91 commit 同量級，非本次改動）。

## Player 防禦範圍實體提示（追加93, 2026-09-01，未經人眼互動確認）

`PlayerGuardVisualizer`（藍色平放扇形）—— `PlayerGuard.IsBlocking`（右鍵按住）時，在玩家前方沿 `GuardArcDegrees`(150°) 展開一片 2m 扇形，代表正面格擋範圍。選單 `Tools/Live2DAction/Add Player Guard Telegraph`（＋`Remove …`），已加到 GreyboxTest 的 Player。

- 扇形是**平放水平**的（`PlayerGuardUtility.IsFrontalBlock` 只看水平角度），胸高 1.1m。
- （先做的攻擊版 `PlayerAttackHitboxVisualizer` 使用者不要，已整包刪除。）
- EditMode `PlayerGuardVisualizerTests` 3 綠（247/247）。離線 `Camera.Render()` 截圖 `player_guard_wedge.png` 確認。
- **待使用者 Play 確認**：右鍵按住藍扇出現、跟隨轉身、懸空感可不可接受。

## 隻狼式刀刃交鋒 + 武士戰鬥系統改造（追加94 + 續 1–34, 2026-09-01～09-02）

防禦/一般格擋/完美彈反系統（Phase 1a/1b/2/3 完成、已接 GreyboxTest），之後接續實作外部 AI 的
9 項工程規格 `Docs/WUSHI_COMBAT_ENGINEERING_SPEC.md`。**逐項進度表見該文件開頭的「實作進度」章節**，
CHANGELOG「追加94 續 N」是流水帳。當前（續 34）：EditMode **288/288** 綠。

**規格 9 項現況**：M1（項目 1/2/6）完成、M2（3 完成 / **4 退回**——實機 Play 無傷害，需對焦 Editor 陪同 debug）、
M3（5A + 5B「做法 A」完成 / **5C 使用者決定跳過**，等未來完整武士副本）、M4（7/8）完成、M5 groundwork
（§10.2 F9 數據儀表 + §10.4 時序報表）完成。**所有「程式完成」項目的 Play 手感驗收未跑**（PlayMode
runner 本機不可靠；戰鬥手感需使用者對焦 Editor Play）。

**續 33（5B 做法 A）**：武士 gameplay root `localScale` 4→1，`WushiRootScaleSetup.cs` 把每個 direct child
的 `localScale`／`localPosition` ×4、`CharacterController` 尺寸 ×4，`BladeDrawVFX`（`scalingMode=Local`）
只補位置。世界幾何（CC、`BladeHitbox` lossyScale 3.2、`BodyHurtbox`、SMR bounds、`ChestAimPoint`）
逐項驗證前後完全一致。`transform.position` / hurtbox 現在是可讀公尺數。選單可逆（Restore To 4）。

**續 34（9 §10.4）**：`Tools/Live2DAction/[9] 武士 Attack Timing Report` 選單——讀每招 + Animator state
speed，印出 hit window 真實 contact 秒數 + 有效 ms + 對 0.20s 彈反窗的比值。唯讀量尺，給 §10.3 調校用。

--- 以下為 Phase 1a/1b/2/3 的原始記錄（追加94，2026-09-01）---

- **已接進 GreyboxTest**（選單 `Tools/Live2DAction/Wire Sekiro Deflect Into GreyboxTest`，可重跑）：3 layer、`GuardVolume`（追蹤 katana `BladeMesh`）、`BladeHitbox`→`BossWeapon`、`PlayerHurtbox`→`PlayerHurtbox`、`guardArcDegrees=120`/`parryWindowDuration=0.12`、`PlayerGuardAnimatorLink`+`IsGuarding`、`ClashFeedback`（交點火花+`KatanaClash.mp3`）、`SekiroDeflectDebug`（F9）。
- **流程**：右鍵按下瞬間開 ParryWindow(0.12s)→hold = GuardWindow。武士刀 sweep（`part==Weapon`）最先命中 active guard volume → `TryResolveClash` → Parry（玩家 0 HP＋Boss 架勢 +14＋`NotifyParried()`→`HitReaction`＋hitstop 0.10＋火花/音效）/ Guard（玩家架勢 +6＋hitstop 0.05）/ None（fall-through 打身體＝全額，spec C）。clash 冷卻 0.1s、feedback 冷卻 0.1s。
- **`GuardVolume` = 錨在玩家的守備範圍膠囊**（追加94 Phase 3 續 4）。曾試過貼刀身細膠囊（續 2）→ 太細、頭頂攻擊全穿過 → **「聽不到彈反聲音」的元凶**。定案：`nearHeight` 0.9 / `backReach` 0.35 / `reach` 1.5 / `farHeight` 3.4 / `handLean` 0.35 / `radius` 0.45（覆蓋 rel-y 0.5–3.9）。`PlayerGuardWeapon` layer。啟用 = `IsBlocking || InTapGuardWindow`。`rotateWeapon` = 防禦時視覺武士刀轉成上舉朝前（`bladeRise` 1.1）。`OnDrawGizmos`（刀線+球+正面錐）。**刀+踢擊都可彈反**（`BossHitbox.IsClashablePart` = Weapon+手+腳）。
  - **為什麼不能貼刀身**：4× 武士的刀刃攻擊連打到玩家身體都勉強（Meshy clip「起點在 root 後方 ~3 units」，見 `Wushi_Attack_DoubleCombo` designNotes）。真的要精準貼刀判定得等正常比例的 boss + 對玩家高度校準過的攻擊。
  - 續 3 曾改 4 個 `Wushi_Attack_*.asset` window nt 想配合貼刀 collider → 離線量測不可信（clip 帶 root motion）→ 已 `git checkout` 還原。

**追加94 續 6 — 逐招演藝驗證（用 `UpdateHitWindows` + 手動 sample 繞過 frozen FSM）：**
| 攻擊 | 出手→命中 | 窗口持續 | 反應 | 彈反驗證 |
|---|---|---|---|---|
| SwordJudgment | 窗1 0.74s / 窗2 1.79s | 122/318ms | 充裕 | **✅ Parried=1**（刀落在玩家 rel (-0.1,1.9,-0.3)）|
| DoubleCombo | 0.82s / 1.56s | 162/121ms | 充裕 | ⚠️ 離線 sample 漂（刀顯示 rel (2,1.5,2.1)）—— designNotes 早記錄「clip 起點在 root 後方 3 units」|
| ChargeCut | 0.50s | 156ms | 快但可 | ⚠️ 離線刀 rel (0,2.8,-0.7) 剛好在體積邊緣外 |
| OverheadSlam | 1.40s | 199ms | 充裕 | ⚠️ 離線漂嚴重（刀 rel (2,4.2,2.1)）|
| SpartanKick | 0.72s | 154ms | 充裕 | （腳 hitbox 非本測試範圍）|

- **反應時間全部 ≥0.5s → 每招都來得及看招反應。** BladeHitbox 是沿刀刃全長的 CapsuleCollider（刀尖到刀柄都算），boss sweep 是它的掃描 cast。
- **「同一次攻擊對同目標只結算一次」已內建**：`BossHitbox._hitTargetsThisActivation`（每 `Activate()` 清空的 HashSet）。傷害/格擋/彈反共用同一個去重。多段攻擊每個 window 各自 activate = 刻意連段。

**追加94 續 8 — 有效傷害幀全面收緊（使用者定原則：一般斬 0.10-0.16s / 重斬 0.14-0.20s / 彈反窗 0.20s）：**
用 blade-rel-Hips 去 root-drift 逐幀量測，把每招 window 收到「刀刃真的掃過玩家高度且還在移動」那段：
| 招式 | 新 window | 出手→命中 | 傷害窗 |
|---|---|---|---|
| SwordJudgment (speed 1.0) | 0.175-0.225 / 0.61-0.66 | 0.93s / 2.36s | 165/165ms |
| DoubleCombo (1.4) | 0.24-0.32 / 0.61-0.68 | 0.70s / 1.23s | 115/101ms |
| ChargeCut (1.15) | 0.21-0.28 | 0.57s | 124ms |
| OverheadSlam (1.4) | 0.56-0.64 | 1.41s | 145ms |
- 舊 window 常在刀刃還在頭頂/身後就開判定 → 「還沒出手就被擊退 / 刀沒落就受傷」的主因。新 window 對齊刀刃到位。若還有殘留 → 降 `knockbackForce`。
- ChargeCut 蓄力發光/音效 telegraph = 待做 VFX。
- **待使用者手動 Play + F9** 逐招確認紅 sweep 貼玩家；SwordJudgment 0.93s 首刀太慢就把 speed 調 1.1。
- **攝影機震動**：一般格擋不震鏡（`guardShakeAmplitude`=0）、hit-stop 用 `guardHitStopScale` 0.4（不是硬凍）；彈反 `parryShakeAmplitude` 0.06 / `parryHitStopScale` 0.15。
- **Tap-to-deflect + 防連按（隻狼式，追加94 續 5）**：彈反只看「按下瞬間」有沒有落在 **`EffectiveParryWindow`** 內（放不放開無所謂）。`EffectiveParryWindow = parryWindowDuration(0.2s) × _parryScale`。連按（按下距上次 < 0.35s）→ `_parryScale` 每次 −0.4、最差歸 0（彈反窗消失，只剩一般格擋 = 玩家架勢損失）；不連按時每秒回充 1.2、**成功彈反直接回滿**。全在 `PlayerGuard` 的「Anti-mash」Inspector 區。`SekiroDeflectDebug`(F9) 顯示當前 `ParryWin: XXXms (xScale)`。
- **Play 驗過**：frozen-frame 手動步進 `BladeHitbox.FixedUpdate` 掃過 shield zone → Parry swing 觸發 `Parried` + 火花 + 音效 pitch 1.09；Guard swing 觸發 `Guarded` + 火花 + pitch 0.94。**物理 sweep→clash→回饋全鏈路通。** S1-S5 邏輯全對。
- **待使用者在有焦點 Editor 手動 Play**：實際戰鬥手感、shield zone 寬鬆度、`guardArcDegrees=120` 會不會太窄、Boss `HitReaction` 反應、spec C（刀繞過防禦打身體）。
- **Phase 3 完成**：`防禦.zip` → `CombatAnimations/Meshy/Guard.fbx`（Meshy）。`NewAnimator` 加 Player 專用 `Guard` state（播短循環 sub-clip **`GuardHold`** = 第 7–15 幀「雙手舉到胸口」，按著就一直舉、鬆開才放下）+ `GuardParry` state（`Guard` 全 clip `speed 2.4` 快閃）+ `GuardParry` trigger。`PlayerGuard.useProceduralPose=false`。
- **彈反判定放寬**（使用者：點按判定不了）：`parryWindowDuration` 0.28、新 `tapGuardWindowSeconds` 0.55（失準的點按仍軟擋不吃滿刀）、`clashCooldownSeconds` 0.06、`GuardVolume` 在 `InTapGuardWindow` 都啟用、`Guarded`（非按住）也觸發手臂 flash。
- **待使用者手動 Play** 確認姿勢/節奏，`GuardHold` 幀範圍 / `parryWindowDuration` / `tapGuardWindowSeconds` 微調。
- **Phase 3 未做**：一般格擋的「被推開」反應（spec B）需另一段 clip；Animation Event 沒加（窗口是純程式碼計時器，spec 允許）。
- 決議記錄：無防禦 clip → 純計時器窗口；`NewAnimator` 共用 → 只加 Player 專用 `IsGuarding`。`PlayerGuardClashSfx` 已退役（.cs 留磁碟）。

## 其他待辦

1. **Player 刀真的 swept collider（= 規格項目 4）**：`PlayerWeaponHitbox` + `WeaponSweepUtility` 已寫好但**續 23 實機 Play 完全無傷害、已退回**（`useSweptBladeHitbox=false`，回到 `Physics.OverlapCapsule`）。程式/選單/測試全留磁碟。重做需對焦 Editor 陪同逐步 Play debug（盲改壞過兩次）。
2. **規格項目 9 §10.3 調校 pass**：M1–M4 落地後的實際數值調校，需使用者 Play 幾場 + F9 看 ParryRate／削爆間隔／雙方掉血，再依 §10.3 順序調。§10.2（F9 儀表）+ §10.4（時序報表）工具已就緒。
3. **規格項目 2 Part C**：上半身 AvatarMask layer + 專屬 `GuardImpact`/`ParryImpact` clip（需動畫製作）。
4. **規格項目 5C**：精確刀身 Guard collider——使用者決定跳過，等未來完整武士副本做「做法 B」（縮小可見武士）。
5. **武士連續刺擊（ContinuousThrust）重新加入**：`ContinuousThrust.fbx` + `Wushi_Attack_ContinuousThrust.asset` 都還在磁碟（時序報表仍會列出）。使用者要「參照動作本身的短位移和攻擊姿勢」= 開 `useRootMotion`、FBX 不要 in-place bake，重量 hit window。追加89 退役原因見下方「連續刺刀」段。
6. **地圖串流（續 73-91）**：學校在獨立 `Map_School.unity`、二次元在 `Map_Nijigen.unity`，**大門互動式**進出（`SceneGate` + `SceneTransitionRunner` 常駐 GO 跑 coroutine，續 78/81）+ `ScreenFader` 載入畫面「載入中…」+ 黑幕鎖輸入。門的可見面是紅色漩渦影片：**續 91 改成場景序列化的 VideoPlayer**（編輯期 `AddComponent` + `clip`/RT/`playOnAwake` 寫進場景 YAML，**不再 runtime `AddComponent`** —— 那是入口門播不出來的根因）→ 每座門一張 `RT_<gate>` + `Mat_<gate>`（shader `Live2DAction/PortalVideoURP`：`smoothstep` key 掉近黑、`Blend One One`、無矩形基座）。`PortalVideoSurface.cs` 只做 billboard(關)＋pulse＋`Update()` 的 `Play()` nudge 保險。13×9 quad、無提示。yuanpei MeshCollider 換成 box proxy。詳見 `Docs/MAP_STREAMING.md`。待辦：
   - **入口門 Play 消失（續 91-93，已解）** —— scene-0 的 VideoPlayer 不會自己開始播。解法：`OnEnable` coroutine `Prepare()`→等`isPrepared`→`Play()`。**不要用 APIOnly**（續 92 試 → D3D11 掉紅色通道 → 青色矩形）。續 93 = RenderTexture mode + coroutine，使用者 Play 確認 4 座門正常。
   - **傳送門 proximity（續 94，未驗證）** —— 改成玩家靠近 `activateRange` 32m 才淡入現身、走遠/穿門消失（`PortalVideoSurface.proximityActivated`）。待對焦 Play 確認淡入距離與手感。
   - 殘留 warning `Color primaries 0 … WindowsMediaFoundation`（ffmpeg 沒寫 colr atom；紅色調極微偏移，無感）。
   - 景深（`depthLayers`）暫設 0（疊層在灰底上會變矩形）；灰底解掉後再加回。
   - yuanpei box collision 是粗略 AABB（無中庭/門口凹陷），要精細再手調。
   - 傳送門影片右下角有 AI 生成工具浮水印小星星（greybox 暫留）。
   - 只有一個 spawn（門寫死 arrivalPosition）；多出生點 = spawn anchor 系統（藍圖 4）。
   - 貓／載具目前不能用門（`SceneGate` 只認 `PlayerInputProvider`，傳送的是互動者 root —— 貓其實也有 PIP，理論上可用，未測）。
   - 學校無 NavMesh（目前無 AI）。
   - Player/本地/空島仍在常駐場景，未抽 `Core.unity`（藍圖 5）。
   - `MapStreamer.cs` 留磁碟未使用。
   - **傳送門動態互動提示 UI（追加94 續 185 → 185d）** —— 靠近門顯示 `對話系統ui框.mp4` 對話框 + 「按下 F 進入/離開 XXX」。`PortalInteractionUIController`（**單例** `Instance`,Canvas 在常駐 `GreyboxTest`,狀態機 Hidden→Showing→WaitingForInput→Confirmed→Hiding）+ `Live2DAction/UI/PortalDialogueFrame` 去黑底 shader + `PortalInteractionUISetup` 選單(配置全 6 座門,含開 3 個 `Map_*` 設定 exit 門)。`SceneGate`:`showInteractionUI`(預設 true)+ per-gate `promptMessage` + `interactKey`(F)+ `uiShowRange`(4.5)/`interactRange`(3.2);傳送沿用 `SceneTransitionRunner`。**185b**:移除深色底板;`RawImage.uvRect` 裁到只剩對話框;E→F。**185c**:文字等 `frameRiseSeconds`(1.4s) 框立起才淡入;車輛傳送掉虛空修好(`ResolvePlayerRoot` 只拿 Player、`OccupantSeated`→`Blocked`、`ForceDismountAll`、`SceneTransitionRunner` Rigidbody 分支);三座 `*_Enter` 門 `Blocker` → 16×22×1.5 整面牆。**185d**:UI 生命週期改**距離驅動**(不再靠 `OnTriggerExit`,因為關 CC 的傳送不觸發它 → UI 曾卡在畫面上)+ `ScanForPlayer` 每秒補償 + `RangeHysteresis` 消除 Show/Hide/Show 閃爍;**入口出口都有 UI**(單例化 + 全 6 門配置)。**185e**:**車輛 vs 互動系統 → 互動優先** —— 坐車也能觸發傳送門(`Blocked` 拿掉 `OccupantSeated`,傳送前 `ForceDismountAll`);`VehicleEntrySystem` 坐著按 F 先問 `SceneGate.PlayerHasPortalInteraction`,為真就讓位不下車。**185f**:(a) 對話框「立起兩次」→ `SceneGate` hide 緩衝 `hideGraceSeconds`(0.6s) + `PortalInteractionUIController.ResumeRoutine`(淡出中被 `Show()` → 淡回、不 rewind 影片)+ 距離遲滯,三層防護(MCP 驗:12s 靜置 0 變化、狂彈 6 次 0 變化);(b) 互動範圍太廣搶車輛控制權 → 收緊。**185g 又再砍**:`interactRange` **1.0**、`uiShowRange` **1.8**、`RangeHysteresis` 0.7、Blocker 牆改薄 **0.6 厚**。**185h 橫向擴大**:判定從圓形改成門 local 空間的長方形(`InverseTransformPoint` → `.z`=深度、`.x`=橫向);`uiShowRange`/`interactRange` 只管深度,新增 `lateralHalfWidth`(**6**)管橫向 → 站門前 ±6m 內都能互動(蓋滿傳送門寬 + 車道寬),深度仍緊。**185i**:`uiShowRange`=`interactRange`=**1.5**(深度)—— UI 一出現、F 就能按,沒有「提示了卻按不動」的空檔。MCP 驗:UI 在深度 1.44m 出現→同刻 `PHPI=True`、x=0/x=3 都能互動、x=7 關、靜置 7s + 邊界狂彈 8 次 0 變化、Console 無錯。

   - **`SchoolGate_Enter` → Boss 地圖影片載入畫面（追加94 續 186）** —— 進 `Map_School` 全螢幕播 `BossLoadingVideo_{Seal,Blood}.mp4`(輪流)+ TMP「正在進入元培禁域……」/「載入中 XX%」(右下角)。`BossLoadingScreen`(單例,自建 canvas sortingOrder 32100)疊在 `ScreenFader` 上;`SceneTransitionRunner.Begin(..., useLoadingScreen)` 驅動,`allowSceneActivation=false` 把 `op.progress/0.9` 顯示成真實 0~100%,`minLoadingScreenSeconds`(1.2s)防閃一幀,`FadeOverlay` 黑到 `prepareCompleted` 才淡出防閃白,`op==null` 有錯誤處理不卡黑。`SceneGate.useLoadingScreen` 只 `SchoolGate_Enter` 開;`Map_*` 出口門 + boss 戰回程 + 二次元/現世都維持原「載入中…」黑幕。**專案第一個 TMP**:`NotoSansTC-Regular.otf`(OFL)+ dynamic `NotoSansTC SDF.asset` + `Assets/TextMesh Pro/` essentials。MCP 逐幀驗:流程/進度%/spawn/輸入鎖(`ScreenFader.IsCovered`)/TMP 中文 render/Console 無錯都 OK。**待對焦 Play**:(1) 影片實際播放 + 無縫循環(未對焦時 VideoPlayer 媒體管線不跑,6s timeout 後照樣淡出);(2) 按 F 一次觸發;(3) 16:9 及其他解析度文字都在框內、不遮中央圓環;(4) `BossLoadingScreenSetup` **首次要跑兩次**(TMP essentials 匯入 → 再跑建字型)。**待使用者對焦 Editor Play 驗證**:(1) 影片播放 + 去黑底 shader 呈現銀框/藍白邊(失焦時 VideoPlayer 不跑、RT 全黑);(2) 按 F 傳送一次、連按不重複;(3) 影片播完定格最後一幀不重播;(4) 文字在框中央(`uvRect`/size/y 是目視估值,可能要微調);(5) **開車到門口按一次 F → 直接進場**(不是先下車;不掉虛空);(6) blocker 擋得住(飛行超過 22m 仍能越過,greybox 可接受);(7) 只在靠近時(~4.5m)顯示、走遠消失;(8) 校園內走到 `SchoolGate_Exit` 前看到「按下 F 離開元培大學」。**已知小重疊**:`SchoolGate_Exit` 落點 (0,1.1,−78) 離 `SchoolGate_Enter` (z−82) 約 4m,出校門瞬間可能就落在 enter 門的 `uiShowRange` 內(可調落點或 `uiShowRange`)。
   - **待使用者對焦 Editor Play 驗證**：走到 `SchoolGate_Enter` → 按 E → 淡黑「載入中…」→ 站校園內、抬頭見巨大校徽；校園 `SchoolGate_Exit` → 按 E → 回車道、學校卸載（`sceneCount` 2↔1）。

7. **⚠️「元培」校園是真實大學商標，整套目前 DoNotShip**：`yuanpei_MainBuilding` / `ModernGlassLibrary` / `PalmLinedLibrary` / `QuietCampusPlaza` / **`yuanpei_LogoSky`（追加94 續 86，校園上空的 3D 立體校徽，最明確的商標物件；續 89 起也是空中 Boss）** 都是照著「元培醫事科技大學」真實校園做的，校名／校徽／校訓／建築外觀屬第三方商標與著作，違反 CLAUDE.md 不可協商規則 1。目前僅作內部原型佔位。**發布前（Alpha 之後任何外部版本）必須：改成原創校名＋原創校徽＋去識別化建築**，或整個換掉。`yuanpei_LogoSky` 已在 `ASSET_LICENSES.md` 標 DoNotShip。

8. **yuanpei_LogoSky 空中 Boss（追加94 續 89，Phase 1–4）**：可從開戰打到 HP 歸零勝利的 greybox Boss。詳見 `Docs/YUANPEI_LOGO_SKY_BOSS.md`。**未完成**：
   - **正式 VFX / 音效 / 鏡頭**（多數招式仍是 primitive + emission 色塊，無音效）。續 114：雷擊標記改用 `紅圈攻擊特效.mp4` 烘成的 6×6 flipbook 圖集（`GroundStrikeURP` shader）＋連續 6 發鎖定範圍攻擊，Edit-Mode 截圖驗證過（runtime VideoPlayer 在這台 D3D11 算全黑，故走 flipbook）。限制：貼地平面 quad 畫不出原影片的垂直火柱，burst 讀作放射狀地面亮閃。其餘招式 VFX 未做。
   - **模型減面 + LOD**：29 萬面整顆 mesh，spec §3.1 要求 15k~30k + LOD0/1/2（需 Blender）。
   - **Object Pool**：投射物 / hazard 目前 Instantiate/Destroy。
   - **平衡校準**：所有時間 / 傷害 / HP / 處決次數需依玩家實測閃避資料調（spec §8.3、§21 Phase 6）。
   - **Boss 戰停用玩家防禦的 Rule Set**（spec §8.1）：本專案玩家防禦是右鍵，進 Boss 戰未強制停用。
   - **未跑焦點 Play 實測**（Editor 失焦時 Play 凍結，coroutine / FSM 沒法在 MCP 下驗）。

## 已解決

- ~~空島池塘「岸邊」視角往下看依舊穿模，應該要看到綠色草地~~ → **已修正（2026-08-20）**：上一則「隱形平台浮空」的修正是對的，但使用者接著回報池塘邊緣還是會穿模、理論上應該看到綠色草地。這次直接截圖驗證（不只靠 Raycast 物理判斷），確認問題所在的草地網格本身幾何、Collider、材質（`baseColorFactor` 真的是綠色）全部正確、法線也朝上——但從特定俯視角度看就是完全不會被畫出來。真正根因：這個場景用 glTF 匯入管線帶進來的草地／岩石／水面等幾個網格，`Mesh.bounds` 全部是退化的零大小（卡在網格自己的 local 原點，而不是它實際跨越約 25 單位的真實範圍）——Unity 的視錐剔除（frustum culling）是看這個（壞掉的）bounds 決定要不要畫，而不是看實際頂點資料，所以只要攝影機視角沒有剛好對到那個錯誤的原點，整個渲染物件就會被靜默剔除、完全不畫——站在池塘邊往下看正好就是這種角度，草地被剔除後，看到的其實是岩石地形網格自己朝下（背面、`Cull Off`）的底面，接近 `SkyIsland_UndersideBlocker` 深度的那個咖啡色、有裂紋的材質。修正方式是新增一個「每次場景載入都自動修正」的 Runtime 元件 `MeshBoundsFixer`（掛在 `Torii_FloatingIsland` 上，`[ExecuteAlways]`），在 `Awake`/`OnEnable` 對所有子物件的 `MeshFilter` 檢查、對零大小 bounds 呼叫 `RecalculateBounds()`——之所以不能像其他一次性 Editor 工具那樣修完存檔就好，是因為這些 bounds 是 FBX/glTF 匯入產生的子資產資料，每次重新匯入都會被規則覆蓋回原本壞掉的值，只能在執行期自動修，不能只修一次。修完後同一個位置重新截圖確認：現在看到的是正常的綠色草地。這兩則池塘修正的完整前後差異、成因、解法，以及這座空島目前的完整設定快照，已整理成獨立文件 `Docs/FLOATING_ISLAND_GUIDE.md`，供之後新增其他浮空島嶼時參考。
- ~~空島池塘區域視角往下看會穿模~~ → **已修正（2026-08-20）**：根因是先前為了修「神社到池塘走道有一道裂縫會把玩家推開」而加的 `SkyIsland_ShrinePondCrackBridge`（9.1×9.0 的隱形平台 Collider）範圍蓋住了整個池塘，且比池塘真正的地形高了約 0.9~1.0 單位——角色實際上是站在一塊沒有 Renderer、完全看不見的平台上，鏡頭往下俯瞰時視線直接穿過這塊隱形平台看到底下真正的地形，就是回報的「穿模」。實際把角色放到池塘上、鏡頭壓到接近 70° 俯角、直接對鏡頭實際的 forward 方向打 Raycast 確認：修正前第一個命中的是隱形的橋接 Collider（y=21.9），修正後第一個命中的是真正會渲染出來的地形（`Terrain_RockTerrain_Material_0`，y≈20.9~21.0）。用 0.15 單位解析度重掃整個橋接範圍（停用該 Collider 後）確認沒有任何坑洞或危險陡坡，代表這塊隱形平台目前已經不是必要的，於是停用其 `BoxCollider`（保留 GameObject，沒有整個刪除）。用真正的 `CharacterController.Move`（不是瞬移）繞池塘岸邊＋池塘中心走一圈驗證：全程 `grounded=true`，腳底離地只有正常的 `skinWidth`（~0.08），鏡頭俯視時不會再穿過任何看不見的東西。已存檔，並重新進入 Play Mode 再測一次確認修正真的有寫回場景（不是只存在於單次 Play Mode）。
- ~~Unity MCP 或其他 Editor 自動化工具是否要在本專案配置~~ → 已確認本機無此類工具，Phase 1 全程用命令列批次模式完成（2026-08-10）。
- ~~Cubism SDK 尚未匯入驗證~~ → 已匯入 5-r.4.2 並確認在 URP 下可渲染（需搭配自寫 shader，見上方 Live2D 立牌視覺項）（2026-08-10）。
