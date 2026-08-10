# Build & Release Guide (Draft)

尚未有任何 Build（Phase 1 才建立 Unity 專案）。此文件在 Phase 3（第一次 Windows Build）前需要補完實際步驟；先記錄目標平台與發布前檢查清單。

## 目標平台

Windows 10/11 64 位元，鍵盤滑鼠，後續可擴充手把。初步效能目標：1920×1080、中等畫質、穩定 60 FPS。

## 正式 Build 前檢查清單（沿用企劃書第二十二節，Phase 6/RC 才會全部適用）

- [ ] 無編譯錯誤，無持續刷新的 Console 錯誤。
- [ ] 無 Missing Script／Missing Material／粉紅材質／阻塞流程的 Missing Reference。
- [ ] 所有場景已加入 Build Settings。
- [ ] 主選單可進入遊戲，遊戲可正常結束或返回主選單。
- [ ] 存檔、設定正常。
- [ ] `ASSET_LICENSES.md` 授權清單完成，**不包含 076/077 佔位素材或其他未授權素材**。
- [ ] 遊戲內（設定/製作名單/授權頁）已顯示所有 CC-BY 等需要署名素材的署名文字（目前至少有 Maya 角色，見 `ASSET_LICENSES.md`）——這是授權條款強制要求，不是選配。
- [ ] 不包含開發者 API Key、私人路徑、測試帳號或個人資料。
- [ ] 版本號、Product Name、Company Name、圖示可配置。
- [ ] 在乾淨資料夾（無 Unity Editor）測試過 Windows Build。

## 明確不自動執行的事項

不自動登入 Steam、不自動建立 Steamworks 應用程式、不自動接受法律協議、不自動支付費用、不自動發布遊戲、不自動傳送外部訊息。涉及商店上架、付款、隱私政策、法律聲明時一律停止並要求使用者確認。
