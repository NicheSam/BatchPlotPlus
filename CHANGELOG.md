# 更新紀錄

## 1.4.3 — 2026-08-13

### 新增

- PDF 頁籤新增「列印物件線粗」與「列印物件透明度」開關，分別對應 AutoCAD `PrintLineweights` 與 `PlotTransparency`。
- 圖紙方向新增自動、橫向、直向三種選擇，並保留另外旋轉 180° 與圖框置中。
- 新增全域 UI／工作流契約測試與安裝器特殊路徑沙盒測試。

### 修正

- 安裝主體改為 PowerShell，BAT 僅作為一鍵入口，修正路徑含空格、括號、`&`、中文或 `!` 時的解析失敗。
- UAC 提升後會等待安裝完成並回傳真實退出碼，不再在子程序失敗時誤報成功。
- 部署位置改為 `%ProgramData%\Autodesk\ApplicationPlugins`，並在匯出備份後清除指向已刪除舊 bundle 的 R24／R25 Loader。
- `PackageContents.xml` 改用 Autodesk 支援的 `LoadReasons="LoadOnAutoCADStartup"` 寫法。
- PDF 與 DWG 的排序模式與反轉順序改為獨立狀態，切換頁籤不再互相覆寫。
- 選擇「無」出圖樣式時明確關閉 plot style，PDF 與 DWG 失敗對話框也改為各自正確標題。
- 診斷工具新增 Loader 路徑檢查，可區分未曾完成 AutoCAD 載入與指向舊版 DLL。

### 驗證

- `net48` 與 `net8.0-windows` Release 建置：0 warnings、0 errors。
- 31 項純邏輯測試、bundle 驗證、安裝契約與 UI／工作流契約測試通過。
- 空格、括號、`&`、中文、`!` 五種路徑通過；缺少 manifest 的失敗路徑正確回傳 40。

> 本版依專案要求使用 1.4.3，並取代之前標示為 1.4.21 的安裝包。

## 1.4.21 — 2026-08-13

### 修正

- 安裝程式會逐一解除並檢查來源、暫存及部署後 bundle 內所有檔案的 `Zone.Identifier`。
- 任何檔案無法解除封鎖或仍有下載封鎖標記時，安裝會立即失敗並顯示明確原因。
- 部署後封鎖檢查或 DLL 完整性檢查失敗時，會移除無效的新版本並嘗試還原舊版。
- 安裝診斷改為掃描全部 bundle 檔案，偵測到封鎖標記時列為失敗，不再只檢查 DLL 或僅提出警告。

### 驗證

- 使用真實 NTFS `Zone.Identifier` 執行負向與修復測試：未修復時回傳失敗，修復後逐檔確認無殘留標記。
- `net48` 與 `net8.0-windows` Release 建置：0 warnings、0 errors。
- 21 項純邏輯測試、bundle 驗證與強化後的安裝契約測試通過。

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
