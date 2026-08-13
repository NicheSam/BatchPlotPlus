# BatchPlotPlus

BatchPlotPlus 是供 AutoCAD 2021–2025（Windows 64 位元）使用的繁體中文批次出圖工具。目前版本為 **1.4.2**。

它把「批次輸出多頁 PDF」與「依圖框拆分 DWG」整合在同一個視窗與 Ribbon 頁籤，並以不儲存來源圖面變更為原則執行暫時性出圖處理。

![PDF 輸出頁籤](ui-preview/PDF-tab.png)

![拆分 DWG 頁籤](ui-preview/DWG-tab.png)

## 主要功能

- 依同名圖框、矩形聚合線或封閉聚合線尋找圖紙範圍。
- 將全部圖框輸出為一份多頁 PDF，或分別輸出單頁 PDF。
- 自動符合紙張、置中、旋轉及排序。
- 依目前 DWG 模式限制出圖樣式：CTB 圖面只顯示 CTB，STB 圖面只顯示 STB。
- 使用 `monochrome` 時，在未提交的 AutoCAD transaction 中暫時處理 True Color／命名樣式顏色，出圖後回復。
- 依圖框批次拆分 DWG，並提供先測試前 2 張的安全選項。
- 自動載入「批次輸出工具」Ribbon 頁籤。

## 1.4.2 更新

- 修正部分電腦安裝後沒有 Ribbon 頁籤、按鈕與指令的問題。
- 安裝位置改為 AutoCAD 隱含信任的 `%ProgramFiles%\Autodesk\ApplicationPlugins`，安裝程式會自動要求系統管理員權限。
- 修正 bundle 的啟動載入與指令觸發載入設定；即使 Ribbon 初始化失敗，備用指令仍可使用。
- 新增 `BATCHPLOTDIAG` 執行期診斷指令，以及 `DiagnoseInstallation.bat` 一鍵安裝診斷報告。
- 安裝時會解除下載檔案封鎖，並移除同名的舊版使用者／共用安裝，避免 AutoCAD 載入錯誤副本。

完整版本紀錄請見 [CHANGELOG.md](CHANGELOG.md)。

## 1.4.1 更新

- 有完整「圖名1／圖名2」屬性時，繼續使用 圖名2-圖名1 命名。
- 圖框屬性不足或沒有屬性時，可使用自訂前綴和排序後連號，例如 圖1.dwg、圖2.dwg、圖3.dwg。
- DWG 頁籤可獨立設定手動選取、逐列、逐欄及反轉順序。

## 安裝

一般使用者不需要 Visual Studio 或 .NET SDK：

1. 從 [GitHub Releases](https://github.com/NicheSam/BatchPlotPlus/releases/latest) 下載 `BatchPlotPlus-1.4.2-installer.zip`。
2. 解壓縮全部內容。
3. 完整關閉 AutoCAD。
4. 雙擊 `InstallOrUpdate.bat`。
5. 重新開啟 AutoCAD，使用「批次輸出工具」頁籤。

備用指令：

| 指令 | 功能 |
| --- | --- |
| `BATCHPLOTPLUS` | 開啟上次使用的功能頁面 |
| `BATCHPDF` | 開啟 PDF 輸出頁面 |
| `BATCHWB` | 開啟 DWG 拆分頁面 |
| `BATCHPLOTDIAG` | 顯示目前載入的 DLL、版本及 AutoCAD 載入設定 |

部署位置：

```text
%ProgramFiles%\Autodesk\ApplicationPlugins\BatchPlotPlus.bundle
```

安裝程式不會強制關閉 AutoCAD；若偵測到 AutoCAD 正在執行，會停止部署。若安裝後仍未載入，請先執行解壓縮資料夾內的 `DiagnoseInstallation.bat`，再將桌面產生的診斷報告提供給維護者。

## 建置

需求：

- Windows 10／11 64 位元
- .NET 8 SDK
- Windows 上的 .NET Framework 4.8 targeting pack
- 建置時會從 Autodesk 官方 NuGet 套件取得 AutoCAD 2021 與 2025 API 編譯參考

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\BuildRelease.ps1
```

`BuildRelease.ps1` 會建立 AutoCAD 2021–2024 使用的 .NET Framework 4.8 組件、AutoCAD 2025 使用的 .NET 8 組件，執行測試與 bundle 驗證，再產生 release 壓縮檔。

## 專案結構

```text
BatchPlotPlus.AutoCAD/  AutoCAD .NET 插件原始碼
Bundle/                 ApplicationPlugins bundle manifest 與說明
LogicTests/             不需要 AutoCAD 的純邏輯回歸測試
UiPreview/              離線 WinForms 介面預覽
docs/                   控制項行為與介面用語檢視
tools/                  bundle 與載入驗證工具
ui-preview/             PDF／DWG 頁籤畫面
```

## 驗證狀態

1.4.2 已通過：

- `net48` 與 `net8.0-windows` Release 建置：0 warnings、0 errors。
- 21 項純邏輯測試。
- 安裝位置、manifest 載入旗標、指令宣告與診斷檔封裝契約測試。
- R24／R25 bundle 路由、目標框架、DLL 與封裝結構驗證。
- AutoCAD 2023 Core Console 實際 NETLOAD R24 組件。
- PDF／DWG 兩頁繁體中文 WinForms 離線介面回歸。
- AutoCAD CTB 三頁黑白 PDF 測試：三頁彩色像素皆為 0。
- 實際 14 MB DWG 副本單頁輸出：彩色像素為 0。
- 暫時性顏色處理前後物件狀態數量一致，未儲存來源 DWG 變更。

## 限制與安全注意事項

- 每頁仍由 AutoCAD 原生出圖引擎產生；大量圖框本來就需要逐頁圖形生成時間。
- 缺少 SHX、外部參考或出圖裝置時，應先修復來源圖面的依賴項目。
- DWG 拆分會建立新檔，不應刪除來源物件；第一次使用新圖面時，建議保留「先測試前 2 張」。
- 本 repository 尚未指定開源授權；未經另外授權，不代表可任意重新散布或修改。

## English

BatchPlotPlus 1.4.2 is a Traditional Chinese plug-in for AutoCAD 2021–2025 on 64-bit Windows. It provides a Ribbon tab and a two-tab WinForms interface for native multi-page PDF output and copy-safe DWG splitting. The bundle automatically loads a .NET Framework 4.8 assembly on AutoCAD 2021–2024 and a .NET 8 assembly on AutoCAD 2025.

Version 1.4.2 improves cross-machine installation and AutoCAD loading reliability, adds runtime and static diagnostics, and keeps commands available when Ribbon initialization fails. Version 1.4.1 added numbered fallback DWG names with a configurable prefix when title-block attributes are missing.

Download the installer package from [GitHub Releases](https://github.com/NicheSam/BatchPlotPlus/releases/latest), extract it, close AutoCAD, and run `InstallOrUpdate.bat`. AutoCAD 2021 and 2025 should still receive version-specific runtime smoke testing because those hosts are not installed in the current development environment.
