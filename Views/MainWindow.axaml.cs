using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Eve_Settings_Management.ViewModels;

namespace Eve_Settings_Management.Views
{
    public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
    {
        public static MainWindow? Instance { get; private set; }

        public MainWindow()
        {
            Instance = this;
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}