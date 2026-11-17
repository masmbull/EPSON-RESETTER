using CommunityToolkit.Mvvm.ComponentModel;

namespace EpsonResetter.App.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    protected void SetError(string message) => ErrorMessage = message;

    protected void ClearError() => ErrorMessage = null;
}

public interface INavigationPage
{
    string Title { get; }

    Task ActivateAsync(object? parameter = null, CancellationToken cancellationToken = default);
}

public delegate Task NavigateHandler(Type pageType, object? parameter);

public sealed partial class NavigationItem : ObservableObject
{
    public NavigationItem(string title, Type viewModelType, string iconKey)
    {
        Title = title;
        ViewModelType = viewModelType;
        IconKey = iconKey;
    }

    public string Title { get; }

    public Type ViewModelType { get; }

    public string IconKey { get; }

    [ObservableProperty]
    private bool _isSelected;
}