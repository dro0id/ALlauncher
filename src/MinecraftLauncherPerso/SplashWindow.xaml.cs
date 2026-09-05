using System.Windows;
using System.Windows.Media.Animation;

namespace MinecraftLauncherPerso;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        Loaded += SplashWindow_Loaded;
    }

    private async void SplashWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var entrance = (Storyboard)FindResource("EntranceStoryboard");
        entrance.Begin(this);

        await Task.Delay(1700);

        var exit = (Storyboard)FindResource("ExitStoryboard");
        var exitCompleted = new TaskCompletionSource<bool>();
        exit.Completed += (_, _) => exitCompleted.TrySetResult(true);
        exit.Begin(this);
        await exitCompleted.Task;

        var main = new MainWindow();
        Application.Current.MainWindow = main;
        main.Show();
        Close();
    }
}
