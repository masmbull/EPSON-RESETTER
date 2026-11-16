using System.Windows;

namespace EpsonResetter.App.Services;

public interface IDialogService
{
    bool Confirm(string message, string title, string? details = null);
}

/// <summary>WPF message box implementation of <see cref="IDialogService"/>.</summary>
public sealed class MessageBoxDialogService : IDialogService
{
    public bool Confirm(string message, string title, string? details = null)
    {
        var text = string.IsNullOrWhiteSpace(details) ? message : $"{message}\n\n{details}";

        return MessageBox.Show(text, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }
}