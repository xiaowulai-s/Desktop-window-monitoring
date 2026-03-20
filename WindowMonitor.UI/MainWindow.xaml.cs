using System;
using System.Windows;
using WindowMonitor.UI.ViewModels;

namespace WindowMonitor.UI
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _viewModel;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                _viewModel = new MainWindowViewModel();
                DataContext = _viewModel;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动失败: {ex.Message}\n\n详细信息: {ex.StackTrace}", 
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _viewModel?.Dispose();
            base.OnClosed(e);
        }
    }
}
