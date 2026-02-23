using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using SimpleRawEditor.Services;
using SimpleRawEditor.Services.Interfaces;
using SimpleRawEditor.Services.Processing;
using SimpleRawEditor.ViewModels;

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
            var viewModel = Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel
            };
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
        
        services.AddSingleton<MainWindowViewModel>();
        
        return services.BuildServiceProvider();
    }
}
