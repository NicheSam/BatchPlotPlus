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
        private static readonly ICommand CommandHandler = new AutoCadCommandHandler();
        private static bool _subscribed;

        internal static void Initialize()
        {
            if (ComponentManager.Ribbon != null)
            {
                CreateRibbon();
                return;
            }

            if (_subscribed) return;
            ComponentManager.ItemInitialized += OnItemInitialized;
            _subscribed = true;
        }

        internal static void Terminate()
        {
            if (!_subscribed) return;
            ComponentManager.ItemInitialized -= OnItemInitialized;
            _subscribed = false;
        }

        private static void OnItemInitialized(object sender, RibbonItemEventArgs eventArgs)
        {
            if (ComponentManager.Ribbon == null) return;
            CreateRibbon();
            Terminate();
        }

        private static void CreateRibbon()
        {
            var ribbon = ComponentManager.Ribbon;
            if (ribbon == null || ribbon.Tabs.Any(tab => string.Equals(tab.Id, TabId, StringComparison.Ordinal))) return;

            var panelSource = new RibbonPanelSource
            {
                Title = "\u5716\u7d19\u8f38\u51fa"
            };
            panelSource.Items.Add(CreateButton("\u8f38\u51fa PDF", "BATCHPDF ", "\u958b\u555f\u6279\u6b21 PDF \u8f38\u51fa\u9801\u7c64"));
            panelSource.Items.Add(CreateButton("\u62c6\u5206 DWG", "BATCHWB ", "\u958b\u555f\u62c6\u5206 DWG \u9801\u7c64"));

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
            public event EventHandler CanExecuteChanged
            {
                add { }
                remove { }
            }

            public bool CanExecute(object parameter)
            {
                return true;
            }

            public void Execute(object parameter)
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
