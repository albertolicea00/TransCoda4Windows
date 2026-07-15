using Microsoft.UI.Xaml;
using TransCoda.Core;

namespace TransCoda;

public partial class App : Application
{
    public static JobQueue Queue { get; } = new();

    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
