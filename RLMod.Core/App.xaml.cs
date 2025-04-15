using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;

namespace RLMod.Core;

public partial class App : Application
{
    public IServiceProvider Services => _host.Services;
    public static new App Current => (App)Application.Current;

    private readonly IHost _host;
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public App()
    {
        InitializeComponent();

        _host = CreateHost();
    }

    private static IHost CreateHost()
    {
        var settings = new HostApplicationBuilderSettings
        {
            Args = Environment.GetCommandLineArgs(),
            ApplicationName = "VModer",
            ContentRootPath = AppContext.BaseDirectory
        };
#if DEBUG
        settings.EnvironmentName = "Development";
#else
        settings.EnvironmentName = "Production";
#endif
        var builder = Host.CreateApplicationBuilder(settings);
        builder.Services.AddSingleton<MainWindow>();

        // 添加 NLog 日志
        builder.Logging.ClearProviders();
        builder.Logging.AddNLog();
        LogManager.Configuration = new NLogLoggingConfiguration(builder.Configuration.GetSection("NLog"));
        return builder.Build();
    }

    private async void App_OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
            await _host.StartAsync();
            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "An error occurred while starting the application");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
        _host.StopAsync();
    }
}
