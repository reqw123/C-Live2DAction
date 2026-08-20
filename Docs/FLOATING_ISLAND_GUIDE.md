# 空島（Floating Island）開發指南

記錄 2026-08-20 「空島池塘視角穿模」排查與修正的完整過程，把**修改前後差異**、**核心成因**、**核心解法**、以及**這座空島目前的實際設定**整理成一份獨立文件，供之後新增其他浮空島嶼時直接參考、避免重踩同樣的坑。

---

## 1. 這次修的是什麼

使用者回報空島（`JapaneseShrineVista/Torii_FloatingIsland`，`GreyboxTest.unity` 場景）上的池塘區域「視角穿模」——站在池塘附近往下看，畫面會出現不該出現的東西。實際上是**兩個完全獨立的 bug**，疊在同一個症狀回報裡：

| # | 使用者回報 | 真正問題 | 分類 |
|---|---|---|---|
| 1 | 池塘中央/大範圍看下去會穿模 | 玩家實際站在一塊**看不見的浮空碰撞平台**上，比真正的地形高約 0.9~1.0 單位 | 碰撞（Collider）與渲染（Renderer）的高度沒對齊 |
| 2 | 池塘**岸邊**看下去依舊穿模，應該要看到綠色草地 | 草地網格因為 **Mesh.bounds 是退化的零大小**，被 Unity 視錐剔除（frustum culling）整個跳過不畫，穿幫看到底下岩石地形的背面 | 匯入資產的 bounds 資料錯誤，跟碰撞完全無關 |

兩個問題都是用「實際把角色放到池塘上、實際走動、鏡頭實際壓低看下去」直接重現，而不是憑空猜測；第 2 個問題還額外用 `RenderTexture` 截圖驗證了修正前後畫面本身的差異（不只是物理 Raycast 判斷）。

---

## 2. 修改前後差異

### 2.1 `SkyIsland_ShrinePondCrackBridge`（碰撞浮空問題）

| | 修改前 | 修改後 |
|---|---|---|
| `BoxCollider.enabled` | `true` | `false`（GameObject 本身保留，沒有刪除） |
| 玩家實際站的高度 | y≈21.9（這塊隱形平台的頂面） | y≈21.5（真正的地形表面 `Terrain_RockTerrain_Material_0`，y≈20.9~21.7） |
| 鏡頭往下看第一個打到的東西 | 這塊沒有 Renderer 的隱形 Collider | 真正會渲染的地形 |
| 走路/碰撞測試 | 走在浮空平台上，跟視覺脫節 | 用真正的 `CharacterController.Move`（非瞬移）繞池塘一圈確認 `grounded=true`、腳底到地面只有正常的 `skinWidth`（~0.08）間隙 |

**這個物件原本的用途**：修另一個更早的 bug（「神社到池塘走道有裂縫會推開玩家」），做法是蓋一塊 9.1×9.0 的隱形平台整個蓋過裂縫所在的範圍。問題是這個範圍不小心把整個池塘也蓋住了，而且平台高度是抓「裂縫最高點」，比池塘一帶的真實地形高了將近一個單位——蓋裂縫沒問題，但代價是把池塘變成一塊視覺與碰撞脫節的浮空平台。停用它之前，先用 0.15 單位解析度重掃了一次整個範圍（2714 個取樣點，Collider 暫時關閉），確認沒有任何坑洞或危險陡坡——代表這塊平台目前已經不是必要的，可以安全停用。

### 2.2 `MeshBoundsFixer`（渲染剔除問題）——新增檔案

| | 修改前 | 修改後 |
|---|---|---|
| `Terrain.001_GrassTerrain_Material_0`.`sharedMesh.bounds` | `Center: (0,0,0), Extents: (0,0,0)`（退化） | `Center: (-0.15,-4.06,-0.17), Extents: (17.28,12.73,0.25)`（真實範圍） |
| `Terrain_RockTerrain_Material_0`.`sharedMesh.bounds` | 同樣退化 | `Center: (-0.26,-4.18,-4.65), Extents: (18.33,13.67,4.52)` |
| `Water_Water_Material_0`.`sharedMesh.bounds` | 同樣退化 | `Center: (0,0,0), Extents: (1,1,0.04)` |
| 池塘岸邊往下看 | 畫面是咖啡色、有裂紋的地面（岩石地形的底面背面） | 畫面是正常的綠色草地 |
| 新檔案 | — | `Live2DAction/Assets/_Project/Game/World/MeshBoundsFixer.cs`，掛在 `Torii_FloatingIsland` 上 |

**為什麼不能像其他一次性 Editor 工具那樣「修一次、存檔就好」**：這幾個 mesh 的 bounds 是 glTF/FBX 匯入產生的子資產資料，每次重新匯入都會被匯入流程重新產生、蓋回原本壞掉的值，不是場景（scene）本身的資料，`EditorSceneManager.SaveScene()` 救不了它。所以做成一個會在**每次場景載入**（Editor 預覽或 Play 都算）自動執行的 Runtime 元件，而不是一次性修正腳本。

---

## 3. 核心成因（Root Cause）

### 3.1 碰撞與渲染高度沒對齊

任何「補地形洞」或「蓋裂縫」的隱形 `BoxCollider`，只要它的**頂面高度**跟底下真實地形的**視覺表面高度**對不上，玩家就會站在一個視覺上不存在的平台上。範圍蓋得越大、跟真實地形的落差越大，這個「浮空／穿模」的破綻就越明顯——尤其是鏡頭壓低往下看的時候，因為那正是最容易讓玩家注意到「腳下的東西跟看到的東西不一樣」的視角。

**判斷準則**：任何時候要用一塊隱形碰撞體去「墊平」或「橋接」地形上的洞/裂縫，都要先確認它的頂面高度跟四周真實地形的視覺高度差距在誤差範圍內（幾公分等級，不是快一個單位），而且範圍要盡量貼合實際需要覆蓋的區域，不要為了省事直接用一個大矩形整個蓋過去。

### 3.2 glTF 匯入的 Mesh.bounds 退化，導致視錐剔除誤殺

這個專案的空島地形（草地／岩石／水面等）是透過 glTF 管線匯入的 FBX（材質 shader 是 `Shader Graphs/glTF-pbrMetallicRoughness`，物件路徑底下有一層 GUID 命名的 `.fbx`），這些網格匯入後 `Mesh.bounds` 是完全退化的 `(0,0,0)`，卡在網格自己的 local 原點，而不是它實際跨越的世界範圍（草地網格實際跨越約 25 個世界單位）。

Unity 的**每物件視錐剔除**（per-renderer frustum culling）是看這個 `Mesh.bounds`（transform 之後）決定「這個物件現在有沒有可能在畫面裡」，完全不會去看真實頂點資料。Bounds 退化成一個點之後，只要攝影機視角沒有剛好包含那個點，整個 Renderer 就會被直接跳過、完全不進入繪製流程——不是半透明、不是背面剔除，是**整個物件像不存在一樣**。站在池塘岸邊、鏡頭壓低往下看，正好是「視角範圍離網格自己的 local 原點很遠」的典型情況。

**這跟碰撞完全無關**：`MeshCollider` 用的是同一份 mesh 資料做精確碰撞判定，不受 `Mesh.bounds` 影響，所以角色站得上去、走得動，只是「看不到自己站的東西」——這也是為什麼一開始很容易被誤判成碰撞或地形破洞問題，實際上純粹是渲染層的剔除誤判。

**判斷準則**：如果一個物件「碰撞正常、材質顏色/貼圖也對、法線方向也對，但特定角度就是完全不會畫出來」，第一個該懷疑的就是 `Mesh.bounds` 是不是退化——一行 `mesh.bounds.size.sqrMagnitude <= 0f`（且頂點數 > 1）就能檢查出來，比重新檢查材質/Shader/Layer/Culling 設定都快。

---

## 4. 核心解法

### 4.1 浮空碰撞：停用不再需要的隱形平台

```csharp
GameObject bridge = GameObject.Find("SkyIsland_ShrinePondCrackBridge");
bridge.GetComponent<Collider>().enabled = false; // 保留 GameObject，只關閉碰撞
```

停用前務必先在 Collider 關閉的狀態下重新掃一次周邊區域（Raycast 找坑洞、量法線角度找過陡的面），確認底下的真實地形本身已經足夠支撐正常行走，才不會停用之後掉到島外或卡住。

### 4.2 渲染剔除：讓匯入的網格在每次載入時自我修正 bounds

新增 `Live2DAction.World.MeshBoundsFixer`（`Live2DAction/Assets/_Project/Game/World/MeshBoundsFixer.cs`），掛在匯入的地形根物件（這裡是 `Torii_FloatingIsland`）上：

```csharp
[ExecuteAlways]
public class MeshBoundsFixer : MonoBehaviour
{
    private void Awake() => FixAll();
    private void OnEnable() => FixAll();

    private void FixAll()
    {
        foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh != null && mesh.vertexCount > 1 && mesh.bounds.size.sqrMagnitude <= 0f)
            {
                mesh.RecalculateBounds();
            }
        }
    }
}
```

- `[ExecuteAlways]` 讓它在 Editor 預覽（沒按 Play）跟實際 Play 模式都會執行，Scene View 跟 Game 都會被修正。
- `Awake` + `OnEnable` 雙保險：場景重新載入、GameObject 重新啟用都會觸發。
- 只處理「頂點數 > 1 但 bounds 是零」的退化情況，不會動到本來就正常的 mesh。
- **這是目前這個專案唯一已知需要這種修正的地形匯入管線**——如果之後新島嶼用同一套 glTF/FBX 匯入流程，建議直接把新島嶼的根物件也掛上這個元件（或把它掛到一個更上層、所有島嶼共用的父物件上），不用等 bug 出現才補。

---

## 5. 這座空島（Torii_FloatingIsland）目前的實際設定

給之後要蓋新島嶼時對照用的「現況快照」，數值都是直接從場景裡讀出來的即時值，不是憑印象寫的。

### 5.1 階層結構

```
JapaneseShrineVista
└─ Torii_FloatingIsland          ← 掛 MeshBoundsFixer（本次新增）
   └─ <GUID>.fbx                 ← glTF/FBX 匯入根
      └─ RootNode
         ├─ Terrain              ← 岩石地形（Terrain_RockTerrain_Material_0 + Terrain_GrassTerrain_Material_0 兩個子網格）
         ├─ Terrain.001          ← 草地地形（Terrain.001_Material_0 + Terrain.001_GrassTerrain_Material_0 兩個子網格）
         ├─ RockExtra1 / RockExtra2 / Rocks.001 ← 池塘周邊裝飾岩石
         ├─ Water / waterPlant / waterPlant.001 ← 池塘水面與水生植物裝飾
         ├─ BambooGroup.003~007  ← 竹林裝飾
         └─ 鳥居各部件（Hashira_Red／Kasagi_Black／Nemaki_Black／Daiwa_Red…）
```

同層另外還有（不在 FBX 匯入範圍內，是額外手動加的場景物件）：

- `SkyIsland_Ground`（已停用 Collider）——早期真正地形資產放進來之前的佔位平台，現在留著但不生效，**新島嶼不需要複製這個東西**。
- `SkyIsland_UndersideBlocker`——深層安全網，見 5.3。
- `SkyIsland_ShrinePondCrackBridge`（已停用 Collider，見上）。
- `SkyIslandCameraBoundary`——鏡頭邊界環，見 5.4。

### 5.2 地形碰撞：`Terrain` + `Terrain.001` 必須兩個都保持啟用

- 兩者的 Transform 完全重疊（同一個 parent、同一個位置），但**不是重複資產**——是互補的兩塊，各自覆蓋島嶼碰撞範圍裡不同的區塊（一塊主要負責岩石陡坡、一塊主要負責草地平面，實際交界處會混雜）。
- 曾經誤判成「重複所以關掉一個」，結果玩家真的從關掉的那塊對應區域整個掉出島外——**這兩個物件必須同時保持啟用**，不要因為看起來重疊就關掉其中一個。
- 兩者的 `MeshFilter.sharedMesh` 跟 `MeshCollider.sharedMesh` 都指向同一份資產（渲染跟碰撞用同一份幾何），這是正常、預期的設計，不是 bug。

### 5.3 深層安全網：`SkyIsland_UndersideBlocker`

- `BoxCollider`：世界座標 `center=(-70, 11, -25.2)`，`size=(30, 6, 34)`（即 X∈[-85,-55]、Y∈[8,14]、Z∈[-42.2,-8.2]）。
- 用途：角色萬一真的掉出地形碰撞範圍外，在墜落到很深之前先接住，避免直接掉出世界底部。
- **Y 高度刻意壓得比真實地形低很多**（地形表面約 y≈20.9~25，這個安全網頂部只到 y=14）——早期版本把它做得太高，結果玩家實際上是站在這個安全網的咖啡色材質上而不是真正的草地，這個高度是刻意留出的緩衝，新島嶼比照辦理：安全網頂面要遠低於任何預期會被踩到的真實地形，只當最後一道防線，不要當主要地板用。
- 材質是 `SkyIslandUndersideBlocker_Rock`（咖啡色、有裂紋的貼圖）——這也是本次 bounds 剔除 bug 曝光時，鏡頭「看穿」草地之後實際看到的東西（透過岩石地形的背面看到更底下的安全網／或直接看到岩石地形自己的底面）。

### 5.4 鏡頭邊界環：`SkyIslandCameraBoundary`（`SkyIslandCameraBoundarySetup.cs`）

新島嶼如果也需要「玩家可以自由飛進飛出、但鏡頭不能在邊緣甩出去看到世界外面」，直接沿用這一套做法，不用重新設計：

- 16 段 `BoxCollider` 圍成一圈：中心 `(-70, 0, -25.2)`，半徑 `17`，每段高 `6.3`、厚 `1.5`，底部在 `y=22.1`（貼著地形表面高度）。
- 全部放在專屬 layer **`SkyIslandCameraBlocker`**，並且 `Physics.IgnoreLayerCollision(blockerLayer, defaultLayer, true)`——玩家的 `CharacterController`（不管走路還是飛行）完全穿過去，不會被卡住。
- `ThirdPersonCameraController` 的鏡頭防穿模是用 `Physics.SphereCastAll`（空間查詢，不受 layer 碰撞矩陣影響），所以照樣會偵測到這圈邊界、把鏡頭拉回來——**移動碰撞跟鏡頭偵測分屬兩套機制，這正是能同時滿足「玩家自由通過」跟「鏡頭不能穿出去」兩個看似衝突的需求的關鍵**。
- 早期直接用實心牆（`SkyIsland_Boundary`，24 段，完全擋住碰撞）失敗過——那會直接擋住玩家從外面飛進島內，之後才改成現在這套「碰撞穿透、只擋鏡頭查詢」的設計，**新島嶼不要重蹈覆轍直接放實心牆**。

### 5.5 池塘與水面

- 池塘水面（`Water_Water_Material_0`）世界座標中心約 `(-66.8, 21.25, -22.6)`，實際網格世界 AABB 約 X∈[-72.8,-60.9]、Z∈[-28.6,-16.7]（約 12×12 單位的不規則圓盤）。
- 水面**沒有 Collider**（`has collider=False`）——玩家能站的地方是水面底下／周邊的真實地形（`Terrain`/`Terrain.001`），不是水面本身；水面純粹是視覺裝飾，疊在地形上面。
- 池塘周邊幾顆小裝飾岩石（`RockExtra1`/`RockExtra2`/`Rocks.001`，材質都叫 `Toro_Material`）的 `MeshCollider` 目前**全部停用**——因為它們局部坡度太陡（實測最陡到 78°，遠超過 `CharacterController.slopeLimit` 的 65°），會讓玩家沿岸邊走動時不斷被判定「站不住」而推開。**這是這個專案既有的處理原則：純裝飾用、不影響主要地形連續性的小物件，坡度太陡就直接關掉它的 Collider，不用去改地形本身或調整 slopeLimit。**

### 5.6 已知還沒處理、之後可能要注意的殘留問題

- 全島最後一次完整掃描（見 `CONTEXT.md`）留下的少數過陡取樣點（30+ 個），全部集中在島嶼自己的外圍懸崖邊緣——這是預期的（懸崖本來就不該走得上去），不是要修的 bug。
- 有一處很小、只有從近乎垂直的俯視角度才看得到的地形網格接縫（cosmetic gap），過去多次排查都確認跟已修正的推擠/穿模問題無關，一直保留至今——如果之後鏡頭系統或視角有更大幅度的調整，可能值得回頭再檢查一次這個點還在不在。

---

## 6. 給下一座新島嶼的檢查清單

1. **地形碰撞**：確認匯入後所有負責承重的地形子網格（不管拆成幾塊）全部保持 `MeshCollider.enabled = true`；如果外觀上有重疊的多份地形資產，先假設它們是互補、不是重複，關掉任何一塊之前都要先大範圍掃描確認沒有製造新坑洞。
2. **渲染剔除**：匯入後**主動檢查**幾個關鍵地形/裝飾網格的 `Mesh.bounds` 是否退化（`size.sqrMagnitude <= 0f`）——不要等到「鏡頭往下看穿模」被回報才發現。如果匯入管線跟這座島一樣（glTF/FBX、`glTF-pbrMetallicRoughness` shader），直接把 `MeshBoundsFixer` 掛到新島嶼的根物件上。
3. **隱形補洞/橋接 Collider**：如果需要用隱形 `BoxCollider` 墊平地形上的洞或裂縫，範圍盡量貼合實際需要覆蓋的區域，高度要對齊周邊真實地形的視覺表面（誤差抓幾公分內），不要圖方便直接放一塊大矩形蓋過去、也不要抓「最高點」當統一高度。
4. **深層安全網**：加一個 `SkyIsland_UndersideBlocker` 同款的深層攔截 Collider，高度要明顯低於任何預期會被踩到的真實地形，只當最後防線。
5. **鏡頭邊界**：需要「玩家自由通行、鏡頭不能看到世界外」時，直接套用 `SkyIslandCameraBoundarySetup` 這一套「獨立 layer + `IgnoreLayerCollision` + `SphereCastAll` 空間查詢」的做法，不要用實心牆。
6. **裝飾小物件的陡坡**：純裝飾、不影響地形連續性的小物件（石頭、盆栽等）如果局部坡度超過 `slopeLimit`，直接關掉它自己的 `MeshCollider`，不要動主地形或全域 `slopeLimit`。
7. **驗證方式**：任何「視角/穿模」類回報，光靠 `Physics.Raycast`/`SphereCast` 只能驗證碰撞面，驗證不了純渲染剔除類問題（如本次的 `Mesh.bounds`）——懷疑是視覺層問題時，直接用 `Camera.Render()` + `RenderTexture` 截圖比對修改前後的實際畫面，比堆更多物理查詢可靠。
