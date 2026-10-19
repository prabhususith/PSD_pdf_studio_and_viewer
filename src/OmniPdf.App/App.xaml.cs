using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace OmniPdf.App
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                string msg = $"Unhandled Exception:\n{ex?.Message}\n\nStack Trace:\n{ex?.StackTrace}";
                try { File.WriteAllText("crash_log.txt", msg); } catch { }
                MessageBox.Show(msg, "PSD PDF Studio & Viewer Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                string msg = $"UI Dispatcher Exception:\n{args.Exception?.Message}\n\nStack Trace:\n{args.Exception?.StackTrace}";
                try { File.WriteAllText("crash_log.txt", msg); } catch { }
                MessageBox.Show(msg, "PSD PDF Studio & Viewer Error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            base.OnStartup(e);
        }
    }
}
