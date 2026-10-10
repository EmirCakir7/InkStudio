using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;

namespace InkStudio;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            try
            {
                File.AppendAllText(@"C:\Users\emirc\.gemini\antigravity\scratch\InkStudio\crash.log", 
                    $"\n[AppDomain Unhandled] {DateTime.Now}: {args.ExceptionObject}\n");
            }
            catch { }
        };

        DispatcherUnhandledException += (s, args) =>
        {
            try
            {
                File.AppendAllText(@"C:\Users\emirc\.gemini\antigravity\scratch\InkStudio\crash.log", 
                    $"\n[Dispatcher Unhandled] {DateTime.Now}: {args.Exception}\n");
            }
            catch { }
        };
    }
}

