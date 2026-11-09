using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using VoltDesk.Contracts;
using VoltDesk.Desktop.ViewModels;
using VoltDesk.Desktop.Views;

namespace VoltDesk.Desktop.Services;

/// <summary>Opens modal windows, keeping view models free of window types.</summary>
public sealed class DialogService(IServiceProvider services)
{
    public async Task<ProjectDto?> ShowNewProjectAsync()
    {
        var viewModel = services.GetRequiredService<NewProjectViewModel>();
        ProjectDto? created = null;
        var window = new NewProjectWindow { DataContext = viewModel, Owner = Application.Current.MainWindow };
        viewModel.Created += (_, project) =>
        {
            created = project;
            window.DialogResult = true;
        };

        await viewModel.LoadAsync();
        window.ShowDialog();
        return created;
    }
}
