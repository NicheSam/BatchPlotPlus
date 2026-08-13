# 更新紀錄

## 1.4.2 — 2026-08-13

### 修正

- 修正部分電腦完成安裝並選擇「永遠載入」後，仍沒有 Ribbon 頁籤、按鈕及可用指令的問題。
- 安裝目標改為 AutoCAD 隱含信任的 `%ProgramFiles%\Autodesk\ApplicationPlugins\BatchPlotPlus.bundle`。
- 修正 `PackageContents.xml` 的啟動載入與指令觸發載入設定，並明確宣告四個可呼叫指令。
- Ribbon 初始化錯誤改為獨立記錄，不再連帶阻止外掛指令註冊。
- 安裝時清除下載檔案封鎖，並移除使用者及共用位置的同名舊版，避免重複 bundle 造成版本衝突。

### 新增

- 新增 `BATCHPLOTDIAG`，可顯示實際載入 DLL、版本、路徑及 AutoCAD 載入設定。
- 新增 `DiagnoseInstallation.bat` 與 `DiagnoseInstallation.ps1`，可產生安裝診斷報告。
- 新增安裝契約測試，檢查目標位置、manifest 載入旗標、指令宣告及診斷工具封裝。

### 驗證

- `net48` 與 `net8.0-windows` Release 建置：0 warnings、0 errors。
- 21 項純邏輯測試、bundle 驗證與安裝契約測試通過。
- AutoCAD 2023 Core Console 實際載入 R24 組件並執行 `BATCHPLOTDIAG` 通過。

## 1.4.1 — 2026-08-07

- 圖框屬性完整時維持既有屬性命名規則。
- 圖框屬性不足時，允許使用自訂前綴加連號輸出，例如 `圖1.dwg`、`圖2.dwg`、`圖3.dwg`。
- DWG 拆分支援手動選取、逐列、逐欄及反轉順序。
