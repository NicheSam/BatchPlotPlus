# 1.5 官方文件核對與驗證邊界

核對日期：2026-09-18。範圍為 Windows 完整版 AutoCAD；不包含 LT、Mac 或所有垂直產品的實機認證。官方文件核對、建置測試與實際桌面測試分開記錄。

## 年版相容性

依 Autodesk [Managed .NET Compatibility](https://help.autodesk.com/cloudhelp/2025/ENU/AutoCAD-Customization/files/GUID-A6C680F2-DE2E-418A-A182-E4884073338A.htm)：

| AutoCAD | 系列 | 官方 SDK／執行環境基準 | 本專案配置與證據 |
|---|---|---|---|
| 2021 | R24.0 | 2021 SDK，.NET Framework 4.8 | R24 DLL，以 24.0 API 建置；文件／建置核對通過 |
| 2022 | R24.1 | 支援 2021 SDK，.NET Framework 4.8 | 同上；無該年版實機證據 |
| 2023 | R24.2 | 支援 2021 SDK，.NET Framework 4.8 | 同上；另完成桌面載入、字型 GUI、兩頁 PDF、錯誤還原測試 |
| 2024 | R24.3 | 支援 2021 SDK，.NET Framework 4.8 | 同上；無該年版實機證據 |
| 2025 原版至 Update 1.3 | R25.0 | 2025 SDK，.NET 8 | 獨立 R25 DLL，以 25.0 API／net8.0-windows 建置；無實機證據 |
| 2025 Update 1.4 起 | R25.0 | 官方現行表列 .NET 10 | 尚未核實本 net8 外掛在該宿主的完整相容性，不列為已驗證 |

2025 的更新版執行環境資訊不同於最初發行時；不可把 net8 建置通過擴張成所有 2025 更新版已認證。Bundle 的系列篩選無法區分同為 R25.0 的這些更新版。

已核对 `BatchPlotPlus.AutoCAD.csproj`、`PackageContents.xml` 及安裝器登錄範圍。R24.0–R24.3 與 R25.0 分別指向各自 DLL，沒有宣告未來 R25.1。依 [RuntimeRequirements 定義](https://help.autodesk.com/cloudhelp/2024/ENU/AutoCAD-Customization/files/GUID-1591CA01-EF87-48CD-952B-772FE26037F1.htm)，系列與 OS 是載入條件，不是測試認證。`AutoCAD*` 允許其他 AutoCAD-based products 載入，但本次並未認證每種垂直產品。

## 字型替代

官方 [Substitute Fonts (.NET)](https://help.autodesk.com/cloudhelp/2024/ENU/OARX-DevGuide-Managed/files/GUID-B2E279E6-B80A-4430-AEA0-4A8D3828BCB7.htm) 說明文字型式與 MText 個別格式共同決定字型；Big Font 替代涉及一般字型與大字體的配對。因此不把 `chineset.shx` 套用到所有缺失 TTF／一般 SHX。

本外掛的做法是自訂策略：讀取 DWG 大字體參照，為找不到的名稱建立本機自有替代檔，加入本模組快取支援路徑。這不是官方文件保證的「所有缺字提示攔截器」。保留 FONTALT／FONTMAP 與圖面型式，不散布 Autodesk 字型。字型搜尋使用宿主 API，避免固定版本及語系路徑。

已驗證掃描、重複快取、失效條件及來源雜湊；尚不能由文件推定中文字形、字寬、換行或外部參考皆正確。MText 局部格式、缺少 chineset.shx、損毀／加密／網路 DWG，以及首次開圖事件的完整順序仍需對應實例驗收。正式字型優先；字型替代後應人工核對要發出的圖紙。

## PDF 與設定還原

[PlotEngine.BeginDocument](https://help.autodesk.com/cloudhelp/2022/ENU/OARX-ManagedRefGuide/files/OARX-ManagedRefGuide-Autodesk_AutoCAD_PlottingServices_PlotEngine_BeginDocument_PlotInfo_string_object_int_modoptIsLong__MarshalAsUnmanagedType_U1__bool_string.html) 要求檔案輸出的 copies 為 1，版面必須啟用、PlotInfo 必須先驗證並保持存活至文件出圖結束。本次修復與物件生命週期符合這些條件。

AutoCAD 2023 桌面已從正式安裝位置載入 1.5.0，輸出兩頁 PDF，獨立讀回兩頁文字及頁數；無效裝置與正常出圖均還原 BACKGROUNDPLOT。監看 FILEDIA、CMDDIA、CMDECHO、SDI、APPAUTOLOAD、DEMANDLOAD、SECURELOAD、TRUSTEDPATHS、BACKGROUNDPLOT、支援路徑及開啟圖面，測試前後一致。

## DWG 拆圖與座標

官方 [SelectCrossingPolygon](https://help.autodesk.com/cloudhelp/2024/ENU/OARX-ManagedRefGuide/files/OARX-ManagedRefGuide-Autodesk_AutoCAD_EditorInput_Editor_SelectCrossingPolygon_Point3dCollection.html) 定義多邊形選取，但該頁沒有明確寫出座標系。官方 [Convert Coordinates](https://help.autodesk.com/view/ACD/2027/ENU/?caas=caas%2Fdocumentation%2FACD%2F2014%2FENU%2Ffiles%2FGUID-0EFA65CC-C1AB-4B99-8159-C31602C1A5E8-htm.html) 區分 API 一般 WCS 與命令輸入 UCS；不能單靠此通則宣稱這個 Editor 方法的轉換已證實。

目前程式把圖框 WCS 範圍轉至 UCS 後選取，暫時設定 WCS 正上視圖並在 finally 還原；旋轉圖框會明確略過。這個實作仍需旋轉／平移 UCS、視角與範圍案例實测，不能以閱讀文件取消測試缺口。

依 [Database.Wblock](https://help.autodesk.com/cloudhelp/2020/ENU/OARX-ManagedRefGuide/files/OARX-ManagedRefGuide-Autodesk_AutoCAD_DatabaseServices_Database_Wblock.html) 核對：選取物件複製至新資料庫、再 SaveAs，沒有刪除或儲存來源圖面的步驟。但 crossing selection 不是幾何裁切：跨邊界物件可能整個輸出，外部依賴也不因此自動打包。另依 [Database 文件](https://help.autodesk.com/cloudhelp/2022/ENU/OARX-ManagedRefGuide/files/OARX-ManagedRefGuide-Autodesk_AutoCAD_DatabaseServices_Database.html)，外部資料庫另存可能沒有預覽縮圖。

## 環境與發布結論

桌面測試的暫時信任路徑、測試圖面及四個最近樣板偏好已還原／關閉。安裝刻意新增新字型快取路徑，並遷移舊獨立 CadFontAuto。整個登錄設定仍有啟動預設值、對話框位置與 Vault 欄寬差異；保留紀錄，沒有把整份設定檔標成完全相同或強制重設。

本版可作為已完成上述文件核對及 2023 桌面部分驗證的升級候選版發布原始碼。文件沒有涵蓋或本機無法重現的項目保持「待實測」，不宣稱 Autodesk 認證或所有環境無錯誤。
