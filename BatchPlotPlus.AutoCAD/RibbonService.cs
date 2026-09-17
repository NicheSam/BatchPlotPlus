using System;
using System.Linq;
using System.Windows.Input;
using Autodesk.Windows;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace BatchPlotPlus.AutoCAD
{
    internal static class RibbonService
    {
        internal const string TabId = "BatchPlotPlus.RibbonTab";
        private const int MaxDeferredAttempts = 40;
        private static readonly ICommand CommandHandler = new AutoCadCommandHandler();
        private static bool _itemSubscribed;
        private static bool _idleSubscribed;
        private static int _deferredAttempts;

        internal static void Initialize()
        {
            _deferredAttempts = 0;
            if (TryCreateRibbon("initial")) return;
            SubscribeDeferredCreation();
        }

        internal static void EnsureVisible()
        {
            if (TryCreateRibbon("ensure"))
            {
                Terminate();
                return;
            }
            SubscribeDeferredCreation();
        }

        internal static string Status
        {
            get
            {
                var ribbon = ComponentManager.Ribbon;
                if (ribbon == null) return "Ribbon control unavailable";
                return IsTabVisible() ? "BatchPlotPlus tab visible" : "BatchPlotPlus tab missing";
            }
        }

        internal static bool IsTabVisible()
        {
            var ribbon = ComponentManager.Ribbon;
            return ribbon != null && ribbon.Tabs.Any(tab => string.Equals(tab.Id, TabId, StringComparison.Ordinal));
        }

        internal static void Terminate()
        {
            if (_itemSubscribed)
            {
                ComponentManager.ItemInitialized -= OnItemInitialized;
                _itemSubscribed = false;
            }
            if (_idleSubscribed)
            {
                AcApp.Idle -= OnIdle;
                _idleSubscribed = false;
            }
        }

        private static void SubscribeDeferredCreation()
        {
            if (!_itemSubscribed)
            {
                ComponentManager.ItemInitialized += OnItemInitialized;
                _itemSubscribed = true;
            }
            if (!_idleSubscribed)
            {
                AcApp.Idle += OnIdle;
                _idleSubscribed = true;
            }
            PluginDiagnostics.Write("Ribbon is not available yet; creation was deferred.");
        }

        private static void OnItemInitialized(object? sender, RibbonItemEventArgs eventArgs)
        {
            if (TryCreateRibbon("item initialized")) Terminate();
        }

        private static void OnIdle(object? sender, EventArgs eventArgs)
        {
            _deferredAttempts++;
            if (TryCreateRibbon("idle"))
            {
                Terminate();
                return;
            }
            if (_deferredAttempts >= MaxDeferredAttempts)
            {
                PluginDiagnostics.Write("Deferred Ribbon creation stopped after " + _deferredAttempts.ToString(System.Globalization.CultureInfo.InvariantCulture) + " attempts. " + Status);
                Terminate();
            }
        }

        private static bool TryCreateRibbon(string reason)
        {
            try
            {
                var ribbon = ComponentManager.Ribbon;
                if (ribbon == null)
                {
                    PluginDiagnostics.Write("Ribbon creation skipped during " + reason + " because the Ribbon control is unavailable.");
                    return false;
                }
                if (ribbon.Tabs.Any(tab => string.Equals(tab.Id, TabId, StringComparison.Ordinal)))
                {
                    PluginDiagnostics.Write("Ribbon tab already exists during " + reason + ".");
                    return true;
                }
                CreateRibbon(ribbon);
                PluginDiagnostics.Write("Ribbon tab created successfully. Reason: " + reason + ".");
                return true;
            }
            catch (Exception exception)
            {
                PluginDiagnostics.Write("Deferred Ribbon creation failed. Reason: " + reason + ".", exception);
                return false;
            }
        }

        private static void CreateRibbon(RibbonControl ribbon)
        {
            var panelSource = new RibbonPanelSource
            {
                Title = "\u5716\u7d19\u8f38\u51fa"
            };
            panelSource.Items.Add(CreateButton("\u8f38\u51fa PDF", "BATCHPDF ", "\u958b\u555f\u6279\u6b21 PDF \u8f38\u51fa\u9801\u7c64"));
            panelSource.Items.Add(CreateButton("\u62c6\u5206 DWG", "BATCHWB ", "\u958b\u555f\u62c6\u5206 DWG \u9801\u7c64"));

            panelSource.Items.Add(CreateButton("\u5b57\u578b\u7ba1\u7406", "BATCHFONTS ", "\u67e5\u770b\u5b57\u578b\u66ff\u4ee3\u72c0\u614b\u3001\u7d00\u9304\u8207\u5feb\u53d6"));

            var tab = new RibbonTab
            {
                Id = TabId,
                Title = "\u6279\u6b21\u8f38\u51fa\u5de5\u5177"
            };
            tab.Panels.Add(new RibbonPanel { Source = panelSource });
            ribbon.Tabs.Add(tab);
        }

        private static RibbonButton CreateButton(string text, string command, string description)
        {
            var button = new RibbonButton
            {
                Text = text,
                ShowText = true,
                Size = RibbonItemSize.Large,
                Orientation = System.Windows.Controls.Orientation.Vertical,
                CommandParameter = command,
                ToolTip = description
            };
            button.CommandHandler = CommandHandler;
            return button;
        }

        private sealed class AutoCadCommandHandler : ICommand
        {
            public event EventHandler? CanExecuteChanged
            {
                add { }
                remove { }
            }

            public bool CanExecute(object? parameter)
            {
                return true;
            }

            public void Execute(object? parameter)
            {
                var document = AcApp.DocumentManager.MdiActiveDocument;
                var command = parameter is RibbonButton button
                    ? button.CommandParameter as string
                    : parameter as string;
                if (document == null || string.IsNullOrWhiteSpace(command)) return;
                document.SendStringToExecute(command, true, false, true);
            }
        }
    }
}
