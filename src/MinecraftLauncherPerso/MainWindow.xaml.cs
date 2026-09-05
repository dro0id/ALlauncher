using System.Windows;
using MinecraftLauncherPerso.Models;
using MinecraftLauncherPerso.Services.Configuration;
using MinecraftLauncherPerso.Services.Java;

namespace MinecraftLauncherPerso;

public partial class MainWindow : Window
{
    private readonly IJavaManager _javaManager;
    private readonly SettingsManager _settingsManager;
    private LauncherSettings _settings;

    public MainWindow()
    {
        InitializeComponent();

        _javaManager = new JavaManager();
        _settingsManager = new SettingsManager();
        _settings = _settingsManager.Load();

        MaxRamTextBox.Text = _settings.MaxRamMb.ToString();
    }

    private async void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        PlayButton.IsEnabled = false;

        if (int.TryParse(MaxRamTextBox.Text, out var maxRamMb))
        {
            _settings.MaxRamMb = maxRamMb;
            _settingsManager.Save(_settings);
        }

        var progress = new Progress<JavaSetupProgress>(report =>
        {
            ProgressBar.Value = report.PercentComplete;
            AppendLog(report.Message);
        });

        try
        {
            var javaPath = await _javaManager.EnsureJava8Async(progress);
            AppendLog($"Java 8 prêt : {javaPath}");

            // TODO (étapes suivantes) :
            //   1. ForgeManager.EnsureForgeInstalledAsync(_settings.MinecraftVersion, _settings.ForgeVersion, ...)
            //   2. ModSyncService.SyncAsync(_settings.ModsServerBaseUrl, _settings.GameDirectory, ...)
            //   3. AuthService.GetActiveSession()
            //   4. GameLauncher.LaunchAsync(javaPath, forgeVersionId, _settings.GameDirectory, session, _settings.MinRamMb, _settings.MaxRamMb)
            AppendLog("TODO : installation Forge, synchronisation mods/config, authentification et lancement.");
        }
        catch (Exception ex)
        {
            AppendLog($"Erreur : {ex.Message}");
        }
        finally
        {
            PlayButton.IsEnabled = true;
        }
    }

    private void AppendLog(string message)
    {
        StatusLogTextBox.AppendText(message + Environment.NewLine);
        StatusLogTextBox.ScrollToEnd();
    }
}
