using System;
using System.Security.Principal;
using System.Windows;

namespace WindowMonitor.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static bool IsRunningAsAdmin { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        // Check for admin rights
        IsRunningAsAdmin = CheckIsRunningAsAdmin();

        if (!IsRunningAsAdmin)
        {
            var result = MessageBox.Show(
                "窗口监控功能需要管理员权限才能正常工作。\n\n" +
                "当前程序未以管理员身份运行。\n" +
                "请选择:\n" +
                "  是 - 重新以管理员身份启动\n" +
                "  否 - 继续运行（监控功能可能无法正常工作）\n" +
                "  取消 - 退出程序",
                "权限提示",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                RestartAsAdmin();
                Shutdown();
                return;
            }
            else if (result == MessageBoxResult.Cancel)
            {
                Shutdown();
                return;
            }
        }

        base.OnStartup(e);

        // Clear debug log on startup
        WindowMonitor.Business.DebugLogger.Clear();
        WindowMonitor.Business.DebugLogger.Log("[App] Application started");

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

    private static bool CheckIsRunningAsAdmin()
    {
        try
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
        catch
        {
            return false;
        }
    }

    private static void RestartAsAdmin()
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                UseShellExecute = true,
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location,
                Verb = "runas"
            };

            System.Diagnostics.Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法以管理员身份重启: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

