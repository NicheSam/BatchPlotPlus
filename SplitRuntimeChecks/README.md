# DWG split desktop regression checks

This helper links the production sources without running the plug-in initializer. It targets AutoCAD 2021–2024 (.NET Framework 4.8); desktop execution was verified on AutoCAD 2023.

Build with `dotnet build SplitRuntimeChecks/SplitRuntimeChecks.csproj -c Release`. Load the helper with NETLOAD and run `BPPSPLIT153` **only in a new disposable drawing**. The command adds test geometry. Close that drawing without saving afterward. Use a fresh output folder per run, because existing DWGs are not overwritten.

The `results/synthetic.txt` report records separate rectangular sheets, concave selection, complete crossing-line export, and UCS/view restoration. Output DWGs are independently reopened by the checks. Runtime results and drawings must remain local; they are not source artifacts.
