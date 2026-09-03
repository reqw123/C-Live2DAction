# Large Assets（版控排除的大型檔案）

GitHub 對**單一檔案有 100 MB 硬上限**（超過直接拒絕 push），而本專案規則是
**美術資產直接進版控、不使用 Git LFS**（見 `CLAUDE.md` Tech 段）。

因此少數超過 100 MB 的原始檔**不進版控**，只留在開發機本地，並在這裡登記，
方便換機／重灌時知道少了什麼、去哪裡拿回來。

> 授權追蹤仍以 `Docs/ASSET_LICENSES.md` 為準，這份只記「為什麼不在 git 裡、怎麼補回來」。

## 目前排除的檔案

| 檔案（repo 內路徑） | 大小 | 網格 | 外觀 | 本地來源 |
| --- | --- | --- | --- | --- |
| `Live2DAction/Assets/_Project/Environment/Meshy/ModernGlassLibrary/ModernGlassLibrary/Meshy_AI_Modern_Glass_Library_0830064510_texture_fbx/Meshy_AI_Modern_Glass_Library_0830064510_texture.fbx` | 108 MB | 162 萬頂點 / 294 萬三角面 | 白色現代校園建築群、藍綠玻璃屋頂、四周密林 | `Meshy_AI_Modern_Glass_Library_0830064510_texture_fbx.zip` |
| `Live2DAction/Assets/_Project/Environment/Meshy/PalmLinedLibrary/PalmLinedLibrary/Meshy_AI_Palm_Lined_Library_En_0830064603_texture_fbx/Meshy_AI_Palm_Lined_Library_En_0830064603_texture.fbx` | 130 MB | 337 萬頂點 / 304 萬三角面 | 白色建築＋大片草地與棕櫚行道樹，掃描邊緣有破洞 | `Meshy_AI_Palm_Lined_Library_En_0830064603_texture_fbx.zip` |
| `Live2DAction/Assets/_Project/Environment/Meshy/QuietCampusPlaza/QuietCampusPlaza/Meshy_AI_Quiet_Campus_Plaza_0830071958_texture_fbx/Meshy_AI_Quiet_Campus_Plaza_0830071958_texture.fbx` | 106 MB | 159 萬頂點 / 287 萬三角面 | 幾乎整片棕灰色鋪面的校園廣場/中庭，建物很少 | `Meshy_AI_Quiet_Campus_Plaza_0830071958_texture_fbx (1).zip` |
| `Live2DAction/Assets/_Project/Environment/Meshy/YuanpeiUniversityBuilding/YuanpeiUniversityBuilding/Meshy_AI_Yuanpei_University_Bu_0830053851_texture_fbx/Meshy_AI_Yuanpei_University_Bu_0830053851_texture.fbx` | 120 MB | 257 萬頂點 / 309 萬三角面 | 白/灰建築群＋藍綠玻璃帷幕＋棕櫚扇形樹，有「元培」類中文招牌 | `Meshy_AI_Yuanpei_University_Bu_0830053851_texture_fbx.zip` |
| `Live2DAction/Assets/_Project/Environment/Meshy/YuanpeiLogo/Meshy_AI_Yuanpei_University_of_0902171624_texture.fbx` | **29 MB**（其實沒超過 100 MB，只是被 `Meshy_AI_*_texture.fbx` 這條通配規則一起擋掉）| ~29 萬頂點 / ~29 萬三角面 | 元培醫事科技大學圓形校徽 3D 立體版（藍底＋原子符號＋校名校訓） | `元培logo.zip`（2026-09-03，`Meshy_AI_Yuanpei_University_of_0902171624_texture_fbx/`）**⚠️ DoNotShip：真實大學商標** |

- 來源：全部是使用者本人用 **Meshy AI 付費方案**生成。前 4 棟建築使用者持有商用權（見 `ASSET_LICENSES.md`）；**`YuanpeiLogo` 例外——校徽圖樣是元培的真實註冊商標，標 DoNotShip，發布前必須換原創**。
- 原始 zip 由使用者提供（4 棟 2026-08-30、校徽 2026-09-03）；本機留存於使用者的下載資料夾。

## 這些資料夾裡「有進版控」的部分

同一批 Meshy 資料夾裡的其他檔案都在 100 MB 以下，**正常進版控**，不受影響：

- `Materials/*.mat`（手建的 URP/Lit 材質）
- `*_texture_fbx/` 底下的 `*_texture.png` / `*_normal.png` / `*_metallic.png` / `*_roughness.png`（貼圖）
- `QuietCampusPlaza/QuietCampusPlaza_NoFoliage.mesh`（55 MB）——QuietCampusPlaza 已減面版：
  54.5 萬頂點 / 95.5 萬三角面（原始的約 1/3，去植被、剝切線），這是目前唯一過得了 100 MB 的網格。

因此 fresh clone 後：材質與貼圖都在，只有這 4 個 `.fbx` 的**網格**會缺（場景裡對應
物件會顯示 missing mesh，QuietCampusPlaza 例外——它有 `_NoFoliage.mesh`）。

## `.gitignore` 規則

```
Live2DAction/Assets/_Project/Environment/Meshy/**/Meshy_AI_*_texture.fbx
Live2DAction/Assets/_Project/Environment/Meshy/**/Meshy_AI_*_texture.fbx.meta
```

新的 Meshy 內嵌貼圖 FBX 匯入時只要沿用 `Meshy_AI_*_texture.fbx` 命名就會自動被擋。

## 怎麼補回來 / 正確的長期做法

直接烘 Unity `.mesh` **不可行**——實測 216 MB，比 FBX 還大（獨立 `.mesh` 資產不壓縮）。

**正解：減面。** 兩種等價做法，擇一：

1. **回 Meshy 重新下載低面數版**（Meshy 下載選單的 Quad Remesh / low-poly 選項，通常幾 MB），
   直接取代這 4 個巨檔進版控。**首選。**
2. 用 Blender decimate 把面數砍到 ~30–50 萬三角面後重匯，或比照 `QuietCampusPlaza_NoFoliage.mesh`
   的做法烘成 `.mesh`（減面後才會 < 100 MB）。

在那之前，要在本機看到完整網格：把對應的原始 zip 解壓回上表的 repo 路徑即可
（`.fbx` 被 gitignore，不會誤 commit）。

## 相關文件

- `Docs/ASSET_LICENSES.md` — 這 4 個資產的授權登記
- `Docs/KNOWN_ISSUES.md` — 「Meshy 環境模型面數過高」追蹤項
- `Docs/CHANGELOG.md` 追加64 / 66 / 82 — 匯入與本次版控排除
- `Docs/AGENT_NOTES.md` — 環境重建注意事項
