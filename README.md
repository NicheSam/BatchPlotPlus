# BatchPlotPlus

BatchPlotPlus 是供 AutoCAD 2023（Windows 64 位元）使用的繁體中文批次出圖工具。目前版本為 **1.3.6**。

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

## 安裝

一般使用者不需要 Visual Studio 或 .NET SDK：

1. 從 [GitHub Releases](https://github.com/NicheSam/BatchPlotPlus/releases/latest) 下載 `BatchPlotPlus-1.3.6-installer.zip`。
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

部署位置：

```text
%AppData%\Autodesk\ApplicationPlugins\BatchPlotPlus.bundle
```

安裝程式不會強制關閉 AutoCAD；若偵測到 AutoCAD 正在執行，會停止部署。

## 建置

需求：

- Windows 10／11 64 位元
- AutoCAD 2023（R24.2）
- .NET SDK，可建置 `net48`

```powershell
dotnet build .\BatchPlotPlus.AutoCAD\BatchPlotPlus.AutoCAD.csproj -c Release
dotnet build .\LogicTests\BatchPlotPlus.LogicTests.csproj -c Release
.\LogicTests\bin\Release\net48\BatchPlotPlus.LogicTests.exe
```

將建置完成的 `BatchPlotPlus.AutoCAD.dll` 複製至 `Bundle\BatchPlotPlus.bundle\Contents` 後，可執行：

```powershell
python .\tools\validate_bundle.py
```

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

1.3.6 已通過：

- Release 建置：0 warnings、0 errors。
- 16 項純邏輯測試。
- Bundle 結構與 DLL 驗證。
- AutoCAD CTB 三頁黑白 PDF 測試：三頁彩色像素皆為 0。
- 實際 14 MB DWG 副本單頁輸出：彩色像素為 0。
- 暫時性顏色處理前後物件狀態數量一致，未儲存來源 DWG 變更。

## 限制與安全注意事項

- 每頁仍由 AutoCAD 原生出圖引擎產生；大量圖框本來就需要逐頁圖形生成時間。
- 缺少 SHX、外部參考或出圖裝置時，應先修復來源圖面的依賴項目。
- DWG 拆分會建立新檔，不應刪除來源物件；第一次使用新圖面時，建議保留「先測試前 2 張」。
- 本 repository 尚未指定開源授權；未經另外授權，不代表可任意重新散布或修改。

## English

BatchPlotPlus 1.3.6 is a Traditional Chinese AutoCAD 2023 plug-in for batch PDF plotting and copy-safe DWG splitting. It provides a Ribbon tab and a two-tab WinForms interface, supports native multi-page PDF output, filters CTB/STB choices to match the active drawing, and rolls back temporary monochrome overrides after plotting.

Download the installer package from [GitHub Releases](https://github.com/NicheSam/BatchPlotPlus/releases/latest), extract it, close AutoCAD, and run `InstallOrUpdate.bat`. Source builds target .NET Framework 4.8 and reference the AutoCAD 2023 managed assemblies.
