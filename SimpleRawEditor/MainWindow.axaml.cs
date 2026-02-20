using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SimpleRawEditor.ViewModels;
using SukiUI.Controls;

namespace SimpleRawEditor;

public partial class MainWindow : SukiWindow
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }

    private void OnThumbnailPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is GlassCard border && border.Tag is LoadedImageViewModel imageViewModel)
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.SelectedImage = imageViewModel;
            }
        }
    }

    private void OnSliderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SliderDragStartedCommand.Execute(null);
        }
    }

    private void OnSliderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SliderDragCompletedCommand.Execute(null);
        }
    }
}
