using System;
using System.IO;
using System.Reflection;
using System.Text;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace BatchPlotPlus.AutoCAD
{
    internal static class PluginDiagnostics
    {
        internal static string LogPath
        {
            get
            {
                var folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BatchPlotPlus");
                return Path.Combine(folder, "BatchPlotPlus-load.log");
            }
        }

        internal static void Write(string message, Exception? exception = null)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath) ?? ".");
                var text = DateTime.Now.ToString("O") + " " + message;
                if (exception != null) text += Environment.NewLine + exception;
                File.AppendAllText(LogPath, text + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // Diagnostics must never prevent AutoCAD or the commands from loading.
            }
        }

        internal static string BuildLoadedReport()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var builder = new StringBuilder();
            builder.AppendLine("BatchPlotPlus loaded: YES");
            builder.AppendLine("Version: " + (assembly.GetName().Version?.ToString() ?? "unknown"));
            builder.AppendLine("Assembly: " + assembly.Location);
            builder.AppendLine("AutoCAD: " + SafeSystemVariable("ACADVER"));
            builder.AppendLine("APPAUTOLOAD: " + SafeSystemVariable("APPAUTOLOAD"));
            builder.AppendLine("SECURELOAD: " + SafeSystemVariable("SECURELOAD"));
            builder.AppendLine("TRUSTEDPATHS: " + SafeSystemVariable("TRUSTEDPATHS"));
            builder.AppendLine("Ribbon tab: " + RibbonService.Status);
            builder.AppendLine("Fonts: " + FontService.Status);
            builder.AppendLine("Log: " + LogPath);
            return builder.ToString().TrimEnd();
        }

        private static string SafeSystemVariable(string name)
        {
            try
            {
                return Convert.ToString(AcApp.GetSystemVariable(name)) ?? "<null>";
            }
            catch (Exception exception)
            {
                return "<unavailable: " + exception.GetType().Name + ">";
            }
        }
    }
}
