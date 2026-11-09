using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using VoltDesk.Desktop.Services;

namespace VoltDesk.Desktop.ViewModels;

/// <summary>Base for pages: busy flag, error and info banners, and uniform API error handling.</summary>
public abstract partial class PageViewModel : ObservableObject
{
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string? InfoMessage { get; set; }

    /// <summary>Sidebar section this page belongs to.</summary>
    public abstract string Section { get; }

    public virtual Task LoadAsync() => Task.CompletedTask;

    /// <summary>
    /// Runs an API operation and turns failures into a banner. An expired session sends the user
    /// to sign-in; a version conflict runs <paramref name="onConflict"/>, usually a reload.
    /// </summary>
    protected async Task RunAsync(Func<Task> operation, Func<Task>? onConflict = null)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        InfoMessage = null;
        try
        {
            await operation();
        }
        catch (ApiException ex) when (ex.IsUnauthorized)
        {
            WeakReferenceMessenger.Default.Send(new SessionExpiredMessage(ex.Message));
        }
        catch (ApiException ex) when (ex.IsConflict && onConflict is not null)
        {
            ErrorMessage = ex.Message + " The latest version is shown now.";
            await TryAsync(onConflict);
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task TryAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
