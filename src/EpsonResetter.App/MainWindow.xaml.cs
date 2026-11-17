using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using EpsonResetter.App.ViewModels;

namespace EpsonResetter.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ShellViewModel previous)
        {
            previous.PropertyChanged -= OnShellPropertyChanged;
            previous.State.PropertyChanged -= OnStatePropertyChanged;
        }

        if (e.NewValue is ShellViewModel shell)
        {
            shell.PropertyChanged += OnShellPropertyChanged;
            shell.State.PropertyChanged += OnStatePropertyChanged;
        }
    }

    private void OnStatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppState.IsScanning) && sender is AppState state)
        {
            OnScanningChanged(state.IsScanning);
        }
    }

    private void OnShellPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.CurrentPage))
        {
            AnimatePageIn();
        }
    }

    private void AnimatePageIn()
    {
        if (PageHost.Content is not FrameworkElement content)
        {
            return;
        }

        var duration = TimeSpan.FromMilliseconds(220);

        var fade = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = duration,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        var slide = new DoubleAnimation
        {
            From = 14,
            To = 0,
            Duration = duration,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        content.BeginAnimation(OpacityProperty, fade);
        PageTransform.BeginAnimation(TranslateTransform.XProperty, slide);
    }

    private void OnScanningChanged(bool isScanning)
    {
        var duration = TimeSpan.FromMilliseconds(isScanning ? 1300 : 180);

        var slide = new DoubleAnimation
        {
            From = -180,
            To = 320,
            Duration = duration,
            RepeatBehavior = isScanning ? RepeatBehavior.Forever : new RepeatBehavior(0)
        };

        var fade = new DoubleAnimation
        {
            From = isScanning ? 0 : 1,
            To = isScanning ? 1 : 0,
            Duration = TimeSpan.FromMilliseconds(180)
        };

        ProgressTrack.BeginAnimation(OpacityProperty, fade);
        ProgressSlide.BeginAnimation(TranslateTransform.XProperty, slide);
    }
}