using Avalonia.Controls;

namespace SimpleRawEditor.ViewModels.Editor.Adjustments;

public interface IAdjustmentStep
{
    string Name { get; }
    bool IsEnabled { get; set; }
    bool IsExpanded { get; set; }
    UserControl View { get; }
    
    void Apply(byte[] pixels, int width, int height, int stride);
    void Remove();
}
