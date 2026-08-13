# 研究報告：角色視線高度固定攝影機 × 現代角色移動控制與步距設計

> 適用專案：`C:\Live2DAction`（Unity 6000.0.81f1 + URP + Cinemachine，第三人稱動作戰鬥垂直切片）
> 目的：技術調查，供攝影機視角策略與移動手感調整的實作決策參考，非最終規格書。
> 撰寫模式：`deep-research`（quick/lit-review 混合，技術／遊戲開發領域，非傳統學術文獻）

---

## 0. 研究範圍與問題

**核心問題**：如何把攝影機從「俯視 / 上帝視角」改為「固定貼近角色視線高度」，並讓角色移動控制（含步距 stride）符合現代動作 RPG 的手感？

**子問題**：
1. 「角色視線高度攝影機」在業界有哪些具體做法？跟俯視、傳統肩後第三人稱比較優缺點是什麼？
2. 角色移動控制（加速度曲線、root motion vs code-driven、turn-in-place、strafe）的現代做法是什麼？
3. 步距（stride/step distance）怎麼跟移動速度、動畫同步，避免滑步（foot sliding）？
4. Unity/Cinemachine 生態系有哪些現成工具可以落地？
5. 對「固定場景、非開放世界」的垂直切片來說，哪種組合投報率最高？

**範圍界限**：只討論攝影機/移動的**技術做法與設計模式**，不描述、不引用任何特定商業作品的美術/劇情/商標內容；引用來源僅取其中的工程/設計方法說明。

---

## 1. 方法

- 檢索管道：Web 搜尋（官方文件、開發者部落格、GDC 相關報導、論壇/Discussions、一篇學位論文）。
- 因為這是遊戲工程技術主題而非傳統同儕審查學術領域，來源分級採**領域相對標準**：Unity 官方文件 = 最高可信度（一手規格）；GDC 演講內容轉述、知名開發者部落格、GameAIPro 論文集 = 次高（業界共識做法）；論壇討論/教學文章 = 佐證與實作細節,需交叉比對。
- 兩個來源（GameAIPro PDF、Unreal 官方 tech blog《Six Ingredients》）在本次調查中因存取權限限制未能完整讀取全文，僅以標題/搜尋摘要形式列為**延伸閱讀**，本報告不會引用其未經驗證的具體主張。

---

## 2. 攝影機視角設計

### 2.1 三種攝影機系統的分類

業界慣用把第三人稱攝影機分成三類（來源：third-person camera 相關技術文章與學位論文綜述）：

| 類型 | 說明 | 適合場景 |
|---|---|---|
| **Tracking（跟隨式）** | 攝影機即時跟隨角色移動與轉向，是目前主流動作遊戲做法 | 大範圍移動、探索型場景 |
| **Fixed（固定式）** | 攝影機位置在關卡設計階段就決定好，不隨角色動態調整，可運用電影運鏡語言營造氣氛 | 固定場景、演出向段落 |
| **Interactive（互動式）** | 完全交給玩家手動控制視角 | 需要高自由度觀察的遊戲 |

> 來源：third-person camera 綜述文章／學位論文（"Analysis of Third Person Cameras in Current Generation Action Games", diva-portal）[link](https://www.diva-portal.org/smash/get/diva2:628121/fulltext01.pdf)；《Third Person Camera View in Games》[link](https://www.gamedeveloper.com/design/third-person-camera-view-in-games---a-record-of-the-most-common-problems-in-modern-games-solutions-taken-from-new-and-retro-games)

對「固定場景 3D 動作戰鬥」垂直切片來說，**Tracking + 局部 Fixed 混合**通常是最務實的選擇：日常移動/戰鬥用 tracking rig（貼近角色視線高度），特定演出鏡頭（開場、處決動作、Boss 登場）可以切到 fixed virtual camera。Cinemachine 的多虛擬攝影機 + priority/blend 機制天生支援這種混合（見 2.3）。

### 2.2 「角色視線高度」攝影機的設計取捨

把攝影機 pivot 從腰部/胸口拉高到接近頭部/眼睛高度，會影響：

- **沉浸感 vs 可讀性**：越接近眼睛高度，越有第一人稱式的貼近感，但看到的戰場範圍（尤其是腳下地形、多個敵人相對位置）越少，戰鬥可讀性下降。這也是為什麼多數動作遊戲即使把攝影機拉近角色，仍會把 pivot 放在「頭部到肩膀之間」而非嚴格鎖在眼球位置。
- **肩後偏移（Shoulder Offset）**：把角色偏移到畫面一側、鏡頭貼近肩膀後方，是「貼近角色視線但仍保留戰鬥可讀性」的常見折衷做法，本質上是第三人稱與第一人稱之間的連續光譜，靠 Shoulder Offset 的 X/Y/Z 數值調整。
- **FOV**：貼近角色的鏡頭通常需要略大的 FOV 補償近距離造成的空間壓迫感，但過大會造成邊緣畸變，需要配合場景尺度測試。
- **攝影機碰撞（Camera Collision）**：鏡頭越貼近角色，越容易被場景幾何體（牆角、柱子）頂到，碰撞遮擋處理的重要性隨鏡頭拉近而提高。

> 來源：Cinemachine ThirdPersonFollow 官方文件（rig 結構與 Shoulder Offset 說明）[link](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachineThirdPersonFollow.html)；Fixed/tracking/interactive 攝影機優缺點綜述 [link](https://www.gamedeveloper.com/design/third-person-camera-view-in-games---a-record-of-the-most-common-problems-in-modern-games-solutions-taken-from-new-and-retro-games)

業界也存在「整場戰鬥以極貼近角色肩後的單一運鏡完成」的做法類別（連續運鏡、幾乎不切鏡頭），其代價是攝影機系統工程複雜度大幅提高（過場/戰鬥/選單轉場都要在同一顆虛擬攝影機邏輯下處理），不建議垂直切片階段採用，僅作為「貼近角色視線高度」光譜最極端的參考點。

### 2.3 Cinemachine 具體實作對照

| 攝影機類型 | 行為模型 | 是否適合「角色視線高度」 | 備註 |
|---|---|---|---|
| **FreeLook** | 玩家輸入水平/垂直軸，攝影機沿 Top/Middle/Bottom 三個環繞 rig 軌道運動 | 部分適合：可把 Middle rig 調到接近頭部高度，但本質仍是軌道環繞式，貼身感不如 ThirdPersonFollow | 適合需要大幅度自由環繞觀察的場景 |
| **ThirdPersonFollow** | 剛性綁在 Tracking Target 上，靠三個 pivot（Origin → Shoulder → Hand）定義攝影機位置；要瞄準需要旋轉角色本體，而非攝影機 | **最適合**：透過 Shoulder Offset（X/Y/Z）與 Vertical Arm Length 可以直接把鏡頭拉到肩後貼近頭部高度；同樣的元件用不同數值也能做出接近第一人稱的效果 | 內建 Camera Collision Filter / Camera Radius 碰撞閃避 |
| **POV**（搭配 HardLockToTarget body） | 攝影機硬綁在指定骨骼（如頭骨）位置，Aim 完全交給滑鼠/搖桿控制 | 適合真正的第一人稱視角，但用在第三人稱戰鬥會失去角色本體的畫面資訊 | 常見設定：Body = Hard Lock to Target（掛在 head bone），Aim = POV |

> 來源：Cinemachine 官方文件（ThirdPersonFollow rig 結構、Shoulder Offset、碰撞參數）[link](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachineThirdPersonFollow.html)、FreeLook 官方文件 [link](https://docs.unity3d.com/Packages/com.unity.cinemachine@2.3/manual/CinemachineFreeLook.html)、Create a Third Person Camera 官方教學 [link](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/ThirdPersonCameras.html)、POV/HardLockToTarget 頭骨綁定討論 [link](https://discussions.unity.com/t/proper-cinemachine-camera-placement-on-full-body-first-person-model/1590365)

**對本專案的具體建議**：既有第一人稱/第三人稱切換系統可以重構成「同一個 ThirdPersonFollow rig，用兩組 Shoulder Offset + Vertical Arm Length 數值做 preset 切換」，而不是維護兩套完全不同的攝影機邏輯——這樣「貼近角色視線高度」其實就是把第三人稱 preset 的 Y offset 拉高、Camera Distance 拉近的一種特化設定，架構上跟現有系統相容，改動量較小。

---

## 3. 角色移動控制的現代做法

### 3.1 Root Motion vs Code-Driven vs 混合

| 做法 | 優點 | 缺點 |
|---|---|---|
| **Root Motion**（動畫驅動位移） | 移動距離/腳步精準對應動畫，不會滑步；複雜連段、翻滾、處決動作幾乎只能用這個做，可減少約 1/3 的手動位移程式碼 | 玩家操作回饋延遲（角色會先「播完」動畫路徑才能轉向），對網路同步不友善，混合兩段 root motion 動畫容易瞬移 |
| **Code-Driven**（程式碼驅動位移，動畫原地播放） | 操作回應即時、方向自由度高，網路同步簡單 | 需要額外功夫（foot IK、動畫速度縮放）避免滑步；複雜連段動作較難做出「有重量感」的位移 |
| **混合式（業界主流）** | 一般走跑用 code-driven 保操控回應，攻擊/翻滾/處決用 root motion 保精準度與打擊感 | 需要在兩套系統的轉換點做好速度/朝向的無縫銜接 |

> 來源：Root Motion vs In-Place Animation 比較文章 [link](https://mocaponline.com/blogs/mocap-news/root-motion-vs-in-place-animation)、Understanding Root Motion [link](https://www.animotionx.com/en/post/understanding-root-motion-essential-functions-in-gameplay-animation)

**對本專案的建議**：專案目前已有的閃避（i-frame burst movement）、鎖定/環繞移動屬於「精準位移」需求，適合考慮 root motion 或至少「動畫曲線驅動的位移量」；一般走/跑/戰鬥移動維持 code-driven（`CharacterController` 驅動），與 [[camera-fixed-view]]（若未來要記錄本次決策）方向一致。

### 3.2 動畫混合與轉向

- **Blend Tree（1D / 2D Freeform Directional）**：2D Freeform Directional 適合同時處理「方向 + 速度」兩個維度的locomotion（例如 velocityX 做 strafe、velocityZ 做前後速度），可以在同一個 blend tree 內從 Idle → Walk → Run 連續過渡，避免生硬切換。搭配 `Animator.SetFloat` 的 `dampTime` 可以把瞬間輸入變化平滑成有慣性感的加減速。
- **Turn-in-place**：角色原地不動、只轉身面對攝影機方向時，需要專門的原地轉身動畫層，否則角色會像溜冰一樣滑轉。這對「鎖定戰鬥 + 攝影機固定角色視線」的組合特別重要，因為玩家會頻繁面對面站定攻防。
- **Strafe vs 面向移動方向**：鎖定敵人時角色通常改用 strafe 移動（保持面向敵人、左右前後平移），這也是專案既有鎖定系統的既定方向，跟 2D Freeform Directional blend tree 是天生搭配。

> 來源：Unity 官方 Blend Tree 文件 [link](https://docs.unity3d.com/Manual/class-BlendTree.html)、2D Blending 官方文件 [link](https://docs.unity3d.com/6000.4/Documentation/Manual/BlendTree-2DBlending.html)、2D Freeform Directional 教學文章 [link](http://blog.dreasgrech.com/2013/12/a-2d-freeform-directional-blend-tree.html)、turn-in-place 討論串 [link](https://discussions.unity.com/t/character-turning-animations/372544)

### 3.3 Motion Matching（延伸選項，非必要）

Motion Matching 是近年業界（如 2015 年 GDC 揭露的近戰動作遊戲案例）用來取代傳統狀態機/Blend Tree 的技術：不手動指定「哪個動畫接哪個動畫」，而是每幀從大量動作資料庫中即時搜尋「最符合目前速度/朝向/歷史軌跡」的姿勢，讓過渡更自然。

- Unity **沒有官方內建** motion matching 系統；曾有實驗性的 Kinematica 套件，但已封存（仍可用但不再維護）。
- 社群有開源實作可參考（如 GitHub 上的 `MotionMatching` 專案），但技術門檻與資料量需求都遠高於傳統 blend tree。

> 來源：GDC Motion Matching 相關報導與資料庫技術文章 [link](https://mocaponline.com/blogs/mocap-news/motion-matching-games-guide)、GDC Vault 條目 [link](https://www.gdcvault.com/play/1023280/Motion-Matching-and-The-Road)、Unity Kinematica 與社群方案現況整理 [link](https://medium.com/@chitranshnishad27/moving-beyond-state-machines-building-a-motion-matching-system-in-unity-from-scratch-b05ffe621662)、開源實作 [link](https://github.com/JLPM22/MotionMatching)

**建議**：垂直切片階段**不採用** motion matching——投入產出比不划算（需要大量動作擷取資料 + 自建搜尋系統），2D Freeform Directional Blend Tree 搭配良好的 damping 曲線已足以做出現代手感的移動。留作垂直切片穩定後、需要進一步打磨移動手感時的候選項。

---

## 4. 步距（Stride / Step Distance）設計與調校

步距問題的本質：**動畫裡的視覺移動速度**與**遊戲邏輯的實際移動速度**必須一致，否則會出現腳底打滑（foot sliding）或腳步頻率與位移不成比例的違和感。

### 4.1 速度與步頻同步

- 常見經驗法則：如果一支跑步動畫本身內建的位移速度是 5 m/s，但程式讓角色以 8 m/s 移動，腳步落地點就會跟地面對不上，產生明顯滑步。
- 解法一（**動畫速度縮放**）：計算「實際移動速度 / 動畫標定速度」的比例，即時調整 `Animator` 的 playback speed（`Animator.speed` 或以 multiplier 參數控制 blend tree 內單一 clip 的速度），讓步頻自動跟上速度變化。開源範例：`AnimationSpeedController`。
- 解法二（**Foot IK 修正**）：用 Unity **Animation Rigging** 套件的 Two-Bone IK Constraint 綁定雙腳、Multi-Position Constraint 處理骨盆高度，讓腳掌在動畫播放的同時做貼地校正，可以在不同地形/些微速度誤差下仍保持腳步視覺正確；常見混合做法是「上半身/軀幹用一般骨骼動畫（FK），雙腳用 IK 做貼地」。

> 來源：run 動畫速度匹配問題整理 [link](https://gamedev.net/forums/topic/646774-matching-walkrun-animation-with-character-movement/)、Foot IK + Animation Rigging 做法 [link](https://taketakedevelopment.itch.io/ment-simulator/devlog/915343/locomotion-with-foot-ik-in-unity)、動畫速度自適應腳本範例 [link](https://github.com/nothingTVatYT/AnimationSpeedController)

### 4.2 走 / 跑 / 衝刺之間的步距過渡

步距（跨步距離）與步頻理論上會隨速度連續變化：速度越快，跨步越大、步頻也越快。實作上通常不會真的用程式碼算物理步幅，而是：

1. 為走、跑、衝刺各準備一支步幅/步頻已經合理的動畫клип；
2. 用 2D/1D Blend Tree 依速度做插值混合，讓中間速度區間得到「介於兩支動畫之間」的合成步伐；
3. 搭配 §4.1 的速度縮放，修正插值後仍可能出現的殘餘滑步。

> 來源：locomotion 系統設計方法整理（走跑 Blend Tree、速度縮放邏輯）[link](https://mocaponline.com/blogs/mocap-news/locomotion-system-design-guide)、Unity Blend Tree 官方文件 [link](https://docs.unity3d.com/Manual/class-BlendTree.html)

**對本專案的建議**：Phase 2 的閃避／鎖定移動已經有明確的速度分層概念，可以直接沿用「Idle → Walk → Run（→ Sprint 若需要）」的 2D Freeform Directional Blend Tree + 速度縮放腳本，之後再視覺驗證是否需要加入 Foot IK（通常在場景有明顯高低差、樓梯等地形時才需要，純平地固定戰鬥場景可以先不做，降低垂直切片複雜度）。

---

## 5. 現代做法優缺點總表

| 面向 | 選項 | 優點 | 缺點 | 垂直切片建議 |
|---|---|---|---|---|
| 攝影機系統 | Tracking（Cinemachine ThirdPersonFollow） | 貼身、可調至視線高度、內建碰撞閃避 | 快速轉身時需調好 damping 避免暈眩 | ✅ 採用，作為主攝影機 |
| 攝影機系統 | Fixed（場景預設運鏡） | 電影感強、可控性高 | 每個場景要手動擺鏡頭，工作量隨場景數線性增加 | 只在特定演出點局部使用 |
| 攝影機系統 | FreeLook 環繞 rig | 大幅度自由觀察 | 環繞式手感不如貼身 rig 適合近戰貼臉戰鬥 | 可選，非必要 |
| 移動驅動 | Code-driven | 操作即時、易於原型迭代 | 需額外處理滑步/重量感 | ✅ 一般移動採用 |
| 移動驅動 | Root motion | 精準、有打擊感 | 回饋延遲、混合複雜、聯機同步麻煩 | 攻擊/閃避/處決局部採用 |
| 移動控制 | 2D Freeform Directional Blend Tree | 現代標配、與 strafe 天生搭配 | 需要準備多方向素材 | ✅ 採用 |
| 移動控制 | Motion Matching | 過渡最自然 | 資料量與工程成本高，Unity 無官方支援 | ❌ 暫不採用 |
| 步距同步 | 動畫速度縮放 | 實作簡單、成本低 | 極端速度差時仍可能有殘餘滑步 | ✅ 採用，優先做 |
| 步距同步 | Foot IK（Animation Rigging） | 貼地精準、應付地形起伏 | 額外綁定與效能成本 | 視場景地形需要再加 |

---

## 6. 對本專案的具體行動建議

1. **攝影機**：沿用 Cinemachine `ThirdPersonFollow`（或其等效元件），新增一組「視線高度 preset」——調高 Shoulder Offset 的 Y 值、縮短 Camera Distance/Vertical Arm Length，並開啟 Camera Collision Filter + 設定合理 Camera Radius，避免鏡頭穿模。建議做成 ScriptableObject 或 preset 資料，方便和既有第一人稱/第三人稱切換系統共用同一套骨架（符合專案「數值放 SO」的規則）。
2. **移動**：維持現有 `CharacterController` code-driven 移動為主幹，攻擊/閃避動作評估導入 root motion 或至少「動畫曲線驅動位移量」以提升打擊感，兩者過渡點需要额外測試（避免瞬移感）。
3. **步距**：優先實作「速度縮放同步動畫播放速度」這個低成本方案，驗證平地場景下滑步是否已經不明顯；Foot IK 留待有地形高低差需求時再加，避免現階段垂直切片範圍擴張。
4. **驗證場景**：比照專案既有規則（每個功能需可在固定驗證場景重現），建議在現有測試場景加入「視線高度攝影機切換」與「不同速度層級移動」兩組可重現測試點。

---

## 7. 限制與未驗證項目

- 兩個延伸閱讀來源（GameAIPro 第 47 章、Unreal 官方《Six Ingredients for a Dynamic Third Person Camera》）因存取限制本次未能完整讀取全文，僅列為延伸閱讀連結，**不作為本報告任何具體主張的依據**。
- 本報告未做实机原型驗證（例如實際在 Unity 中量測 Shoulder Offset 數值與實際「視線高度」的對應關係），數值建議需要在專案角色模型完成後實測調整。
- 未涵蓋輸入裝置差異（手把 vs 鍵鼠）對攝影機/移動手感的影響，如需要可另開子題研究。

---

## 8. 參考來源

- Cinemachine ThirdPersonFollow 官方文件 — https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachineThirdPersonFollow.html
- Cinemachine FreeLook 官方文件 — https://docs.unity3d.com/Packages/com.unity.cinemachine@2.3/manual/CinemachineFreeLook.html
- Create a Third Person Camera（Cinemachine 官方教學）— https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/ThirdPersonCameras.html
- Cinemachine POV / HardLockToTarget 頭骨綁定討論 — https://discussions.unity.com/t/proper-cinemachine-camera-placement-on-full-body-first-person-model/1590365
- Third Person Camera View in Games（問題與解法整理）— https://www.gamedeveloper.com/design/third-person-camera-view-in-games---a-record-of-the-most-common-problems-in-modern-games-solutions-taken-from-new-and-retro-games
- Analysis of Third Person Cameras in Current Generation Action Games（學位論文）— https://www.diva-portal.org/smash/get/diva2:628121/fulltext01.pdf
- Root Motion vs In-Place Animation — https://mocaponline.com/blogs/mocap-news/root-motion-vs-in-place-animation
- Understanding Root Motion — https://www.animotionx.com/en/post/understanding-root-motion-essential-functions-in-gameplay-animation
- Unity Blend Tree 官方文件 — https://docs.unity3d.com/Manual/class-BlendTree.html
- Unity 2D Blending 官方文件 — https://docs.unity3d.com/6000.4/Documentation/Manual/BlendTree-2DBlending.html
- 2D Freeform Directional Blend Tree 教學 — http://blog.dreasgrech.com/2013/12/a-2d-freeform-directional-blend-tree.html
- Turn-in-place 討論串（Unity Discussions）— https://discussions.unity.com/t/character-turning-animations/372544
- Motion Matching 介紹與 GDC 背景 — https://mocaponline.com/blogs/mocap-news/motion-matching-games-guide
- GDC Vault: Motion Matching and The Road to Next-Gen Animation — https://www.gdcvault.com/play/1023280/Motion-Matching-and-The-Road
- Unity Motion Matching 現況整理（含 Kinematica）— https://medium.com/@chitranshnishad27/moving-beyond-state-machines-building-a-motion-matching-system-in-unity-from-scratch-b05ffe621662
- 開源 Motion Matching 實作 — https://github.com/JLPM22/MotionMatching
- Animation 速度匹配移動速度問題整理 — https://gamedev.net/forums/topic/646774-matching-walkrun-animation-with-character-movement/
- Foot IK + Animation Rigging 實作紀錄 — https://taketakedevelopment.itch.io/ment-simulator/devlog/915343/locomotion-with-foot-ik-in-unity
- 動畫速度自適應腳本開源範例 — https://github.com/nothingTVatYT/AnimationSpeedController
- Locomotion System Design Guide — https://mocaponline.com/blogs/mocap-news/locomotion-system-design-guide

**延伸閱讀（本次未完整驗證內容，僅供之後深入研究參考）**：
- GameAIPro Chapter 47: Tips and Tricks for a Robust Third-Person Camera System — https://www.gameaipro.com/GameAIPro/GameAIPro_Chapter47_Tips_and_Tricks_for_a_Robust_Third-Person_Camera_System.pdf
- Unreal Engine: Six Ingredients for a Dynamic Third Person Camera — https://www.unrealengine.com/en-US/tech-blog/six-ingredients-for-a-dynamic-third-person-camera

---

## AI 揭露聲明

本報告使用 AI 輔助工具（Claude, `deep-research` 技能）進行網路搜尋、資料整理與撰寫。所有引用連結均為搜尋當下實際存在的公開網頁；報告內容為技術做法歸納，未描述、未引用任何特定商業作品的受著作權保護素材（角色、劇情、美術、商標）。使用者應在導入專案前自行核實 Unity/Cinemachine 版本相容性（本專案固定 Unity 6000.0.81f1）。
