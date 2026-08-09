# Art Pipeline (Draft)

## 3D 角色管線（正式角色，非佔位）

```
原始 2D 角色設計 → 正面/側面/背面設定 → 合法 3D 人形模型（取得或製作）
→ Blender/VRoid 調整 → Humanoid Rig → Unity Avatar
→ 動畫重定向 → Animator → 戰鬥測試
```

正式角色至少需要動畫：Idle、Run、Attack1-3、Skill、Dodge、Hit、Death、Victory。

## Placeholder 政策

灰盒原型（Phase 1）使用 Unity 內建 Capsule/Cube，不需要外部素材。Phase 2 起若缺少正式 3D 角色，先用授權清楚的臨時 Humanoid 角色，並在 `ASSET_LICENSES.md` 標註為 Placeholder；Placeholder **不得進入 Release Build**。

## Live2D 佔位政策

`076`／`077` 僅供內部驗證對話系統/演出流程，見 `ASSET_LICENSES.md`、`KNOWN_ISSUES.md`。正式角色的 Live2D 或 2D 立繪素材待原創設計定案後另行製作或委託，不使用生成式 AI 直接產出未經檢查拓撲/UV/骨架/穿模問題的最終素材。
