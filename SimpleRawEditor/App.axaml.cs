using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SimpleRawEditor.Services.Core;
using SimpleRawEditor.Services.Parsing;
using SimpleRawEditor.Services.Processing;
using SimpleRawEditor.Services.Processing.Denoising;
using SimpleRawEditor.ViewModels.Main;

namespace SimpleRawEditor;

public partial class App : Application
{
    public new static App? Current => Application.Current as App;

    public IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Services = ConfigureServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = Services.GetService<MainViewModel>();
            desktop.MainWindow = new MainWindow();
            desktop.MainWindow.DataContext = viewModel;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IRawImageService, RawImageService>();
        services.AddSingleton<ILutService, LutService>();
        services.AddSingleton<DenoisingHandler>();
        services.AddSingleton<ImageProcessingService>();
        services.AddSingleton<IImageProcessor, DebouncedImageProcessor>();

        services.AddSingleton<MainViewModel>();

        return services.BuildServiceProvider();
    }
}
