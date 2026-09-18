# BatchPlotPlus 1.5.1 — 還原批次出圖與拆圖行為

此版本依使用者要求，將 `PlotService.cs`、`DwgSplitService.cs`、`BatchLogic.cs` 完整還原至 1.4.4 的 Git 基準 `43db0bf`。保留 1.5 字型管理整合與安裝遷移。

撤回 1.5 對 PDF 份數、設定還原機制、列印資源生命週期、模型空間檢查、拆圖座標及視角、保留檔名與進度計算的變更。這是功能基準還原，並非宣稱已定位或修復個別 DWG 的圖框數問題。

後續批次出圖與拆圖只做 GUI／UIUX 改善，執行行為變更須另行確認。基準雜湊檢查已加入測試。見 [功能基準政策](output-behavior-policy.md)。

安裝前保存工作並關閉 AutoCAD，再執行 `InstallOrUpdate.bat`。不覆寫使用者 DWG。既有 1.5.0 的功能修復與桌面驗證紀錄僅供歷史參考，不直接代表本版。
