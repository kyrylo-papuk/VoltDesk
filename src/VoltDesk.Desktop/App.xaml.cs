using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VoltDesk.Desktop.Services;
using VoltDesk.Desktop.ViewModels;

namespace VoltDesk.Desktop;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
        });

        var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
            ?? throw new InvalidOperationException("Api:BaseUrl is not configured.");

        builder.Services.AddSingleton<Session>();
        builder.Services.AddTransient<BearerTokenHandler>();
        builder.Services.AddHttpClient<ApiClient>(client =>
            {
                client.BaseAddress = new Uri(apiBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(15);
            })
            .AddHttpMessageHandler<BearerTokenHandler>();

        builder.Services.Configure<DemoOptions>(builder.Configuration.GetSection(DemoOptions.Section));
        builder.Services.AddSingleton<Navigator>();
        builder.Services.AddSingleton<DialogService>();
        builder.Services.AddSingleton<ShellViewModel>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<ProjectsViewModel>();
        builder.Services.AddTransient<ProjectDetailViewModel>();
        builder.Services.AddTransient<ClientsViewModel>();
        builder.Services.AddTransient<NewProjectViewModel>();
        builder.Services.AddTransient<TaskBoardViewModel>();
        builder.Services.AddTransient<PanelScheduleViewModel>();
        builder.Services.AddTransient<NewTaskViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        // Escape hatch for virtual machines and remote sessions with broken GPU rendering.
        if (Environment.GetEnvironmentVariable("VOLTDESK_SOFTWARE_RENDERING") == "1")
        {
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        }

        base.OnStartup(e);
        await _host.StartAsync();

        MainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}
