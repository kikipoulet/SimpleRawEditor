using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SimpleRawEditor.ViewModels.Main.Thumbnails;

public partial class ThumbnailListViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<ViewModels.LoadedImageViewModel> _thumbnails = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasThumbnails))]
    private ViewModels.LoadedImageViewModel? _selectedThumbnail;

    public bool HasThumbnails => Thumbnails.Count > 0;

    public event EventHandler<ViewModels.LoadedImageViewModel?>? ThumbnailSelected;

    partial void OnSelectedThumbnailChanged(ViewModels.LoadedImageViewModel? value)
    {
        foreach (var thumb in Thumbnails)
        {
            thumb.IsSelected = (thumb == value);
        }

        ThumbnailSelected?.Invoke(this, value);
    }

    public void AddThumbnail(ViewModels.LoadedImageViewModel thumbnail)
    {
        thumbnail.Selected += (_, _) => SelectedThumbnail = thumbnail;
        Thumbnails.Add(thumbnail);
    }

    public void Clear()
    {
        Thumbnails.Clear();
        SelectedThumbnail = null;
    }
}
