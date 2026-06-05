using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ranil_Uchebka.Services;
using Ranil_Uchebka.ViewModels;

namespace Ranil_Uchebka;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton(sp =>
            {
                var conn = sp.GetRequiredService<IConfiguration>()["Database:ConnectionString"]
                           ?? throw new InvalidOperationException("Database connection string is missing.");
                return new DatabaseService(conn);
            });
            services.AddSingleton<RememberMeService>();
            services.AddSingleton<PasswordPolicyService>();
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MainWindow>();

            _serviceProvider = services.BuildServiceProvider();

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            var viewModel = _serviceProvider.GetRequiredService<MainViewModel>();
            var logoPathConfig = configuration["Assets:LogoPath"] ?? string.Empty;
            var logoPath = Path.IsPathRooted(logoPathConfig)
                ? logoPathConfig
                : Path.Combine(AppContext.BaseDirectory, logoPathConfig);
            _ = viewModel.InitializeAsync(logoPath);
            mainWindow.DataContext = viewModel;

            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}