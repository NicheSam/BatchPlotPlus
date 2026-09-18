# DWG splitting extension — explicit scope

The user explicitly requested rectangle and closed-polyline DWG splitting plus a complete review against a supplied drawing on 2026-09-18. This supersedes the prior freeze for DWG splitting only. PDF output (`PlotService.cs`) and shared output logic (`BatchLogic.cs`) remain byte-equivalent to the 1.4.4 baseline after newline normalization.

The user chose native WB crossing selection, with output stopped and offending objects identified when other sheets could be included. This is object copying, not geometric trimming. Rectangle and closed-polyline boundaries use their actual outline. Curved segments are approximated for AutoCAD polygon selection. Model-space XY boundaries only; invalid boundaries are rejected.

The cross-sheet check is conservative and only covers recognized frame boundaries. Entity extents can overestimate complex blocks and produce a rejection that needs inspection; unrecognized sheets are not guaranteed detected. A rejection is not proof the object is corrupt. The source drawing is never edited or saved by the splitter.

AutoCAD 2023 desktop validation passed: two rectangles produced separate DWGs with independently read text contents; a concave boundary excluded content lying only in its bounding rectangle; a crossing line stopped both affected outputs and logged its handle. Rotated UCS and view state were restored. A supplied drawing copy, using temporary diagnostic rectangles around two sheet locations, rejected a large block in both selections and wrote no DWGs. This verifies rejection, not automatic repair or successful production splitting of that drawing. Original file hashes and monitored CAD settings were unchanged. An older diagnostic session's unresolved document modification flag remains separately recorded.

## 使用方式

1. 使用 `RECTANG` 或 `PLINE` 為每張圖建立獨立、封閉、位於世界 XY 平面的邊界；建議使用專用圖層。工具不會自動猜測哪條建築線是圖框。
2. 在「拆分 DWG」選「矩形聚合線」或「封閉聚合線」，重新選取樣板並使用樣板圖層，再指定要處理的邊界或搜尋範圍。
3. 先以「安全測試：前 2 張」輸出到新資料夾，檢查內容。WB 保留整個物件，不裁切線段或圖塊。
4. 如遭攔下，依命令列或 `BatchWBlock-log.txt` 的 `handle` 定位疑似物件，例如 `(sssetfirst nil (ssadd (handent "HANDLE")))`。先確認圖塊內容及正確邊界，再決定如何整理來源圖；工具不會自動炸開或刪除它。

## 限制

- 圖塊模式仍按相同名稱與圖層比對；只有一個符合條件時不等於整張模型只有一張圖紙。
- 防護僅能比對已辨識的圖框；請將相鄰圖纸邊界也放在同一專用圖層。單一邊界無法保證偵測未知的其他圖紙。
- 以物件外包範圍保守判斷，可能誤攔複雜圖塊；弧段採不大於 0.04 弧度的折線近似，邊緣物件需人工驗收。
- 旋轉圖塊沿用拒絕輸出的限制；封閉聚合線可使用實際輪廓。不是三維裁切工具。
- 安裝須關閉 CAD，完成後重開確認正式載入。AutoCAD 2021／2022／2024／2025 僅建置與文件範圍，未實機驗收。
