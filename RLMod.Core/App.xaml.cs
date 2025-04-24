using System.Text;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using RLMod.Core.Services;

namespace RLMod.Core;

public partial class App : Application
{
    public IServiceProvider Services => _host.Services;
    public static new App Current => (App)Application.Current;
    public static string AppConfigPath { get; } = Path.Combine(Environment.CurrentDirectory, "Configs");
    public static string Assets { get; } = Path.Combine(Environment.CurrentDirectory, "Assets");
    public const string ModName = "RLMod";
    public static Encoding Utf8WithoutBom { get; } = new UTF8Encoding(false);

    private readonly IHost _host;
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    public App()
    {
        if (!Directory.Exists(AppConfigPath))
        {
            Directory.CreateDirectory(AppConfigPath);
        }

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
        builder.Services.AddSingleton<MainWindowViewModel>();

        builder.Services.AddSingleton<CountryTagService>();
        builder.Services.AddSingleton<ProvinceService>();
        builder.Services.AddSingleton<StateCategoryService>();
        builder.Services.AddSingleton<BuildingService>();

        builder.Services.AddSingleton(AppSettingService.Load());
        builder.Services.AddSingleton<BuildingGenerateSettingService>();

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
            _host
                .Services.GetRequiredService<IHostApplicationLifetime>()
                .ApplicationStopped.Register(
                    _host.Services.GetRequiredService<AppSettingService>().SaveChanged
                );

            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while starting the application");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
        _host.StopAsync();
        _host.Dispose();
    }
}
