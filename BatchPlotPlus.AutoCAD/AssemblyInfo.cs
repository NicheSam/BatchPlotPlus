using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.Runtime;

[assembly: InternalsVisibleTo("BatchPlotPlus.LogicTests")]
[assembly: ExtensionApplication(typeof(BatchPlotPlus.AutoCAD.Plugin))]
[assembly: CommandClass(typeof(BatchPlotPlus.AutoCAD.Plugin))]
