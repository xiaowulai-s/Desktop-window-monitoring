using System;
using System.Windows;

namespace WindowMonitor.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var exception = args.ExceptionObject as Exception;
            MessageBox.Show($"发生未处理的异常: {exception?.Message}\n\n详细信息: {exception?.StackTrace}", 
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        };
        
        DispatcherUnhandledException += (sender, args) =>
        {
            MessageBox.Show($"发生未处理的UI异常: {args.Exception.Message}\n\n详细信息: {args.Exception.StackTrace}", 
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }
}

