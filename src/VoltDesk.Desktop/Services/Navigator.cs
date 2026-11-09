using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using VoltDesk.Desktop.ViewModels;

namespace VoltDesk.Desktop.Services;

/// <summary>Page navigation with a back stack. Pages are view models; views come from data templates.</summary>
public sealed partial class Navigator(IServiceProvider services) : ObservableObject
{
    private readonly Stack<PageViewModel> _history = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    public partial PageViewModel? Current { get; private set; }

    public bool CanGoBack => _history.Count > 0;

    /// <summary>Opens a top-level section and forgets the back stack.</summary>
    public Task OpenSectionAsync<TPage>()
        where TPage : PageViewModel
    {
        _history.Clear();
        return ShowAsync(services.GetRequiredService<TPage>(), page => page.LoadAsync());
    }

    /// <summary>Opens a page on top of the current one; <see cref="GoBackAsync"/> returns.</summary>
    public Task OpenAsync<TPage>(Func<TPage, Task> load)
        where TPage : PageViewModel
    {
        if (Current is not null)
        {
            _history.Push(Current);
        }

        return ShowAsync(services.GetRequiredService<TPage>(), load);
    }

    public async Task GoBackAsync()
    {
        if (_history.TryPop(out var previous))
        {
            Current = previous;
            await previous.LoadAsync();
        }
    }

    private async Task ShowAsync<TPage>(TPage page, Func<TPage, Task> load)
        where TPage : PageViewModel
    {
        Current = page;
        OnPropertyChanged(nameof(CanGoBack));
        await load(page);
    }
}
