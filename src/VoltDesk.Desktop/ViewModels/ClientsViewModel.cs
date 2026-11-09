using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoltDesk.Contracts;
using VoltDesk.Desktop.Services;

namespace VoltDesk.Desktop.ViewModels;

/// <summary>Client CRM: list on the left, the selected client with contacts on the right.</summary>
public sealed partial class ClientsViewModel(ApiClient api, Session session) : PageViewModel
{
    private bool _rebuildingList;

    public override string Section => "Clients";

    public bool CanManage => session.CanManageProjects;

    public ObservableCollection<ClientSummaryDto> Clients { get; } = [];

    [ObservableProperty]
    public partial string Search { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ClientSummaryDto? SelectedClient { get; set; }

    [ObservableProperty]
    public partial ClientDto? Client { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddClientCommand))]
    public partial string NewClientName { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddContactCommand))]
    public partial string ContactName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ContactTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ContactEmail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ContactPhone { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ContactIsPrimary { get; set; }

    public override Task LoadAsync() => RunAsync(() => ReloadListAsync(selectId: SelectedClient?.Id));

    partial void OnSelectedClientChanged(ClientSummaryDto? value)
    {
        if (!_rebuildingList && value is not null)
        {
            _ = RunAsync(async () => Client = await api.GetClientAsync(value.Id));
        }
    }

    [RelayCommand]
    private Task SearchAsync() => LoadAsync();

    [RelayCommand(CanExecute = nameof(CanAddClient))]
    private Task AddClientAsync() => RunAsync(async () =>
    {
        var created = await api.CreateClientAsync(NewClientName.Trim());
        NewClientName = string.Empty;
        Search = string.Empty;
        await ReloadListAsync(created.Id);
        InfoMessage = $"{created.Name} added.";
    });

    private bool CanAddClient() => CanManage && !string.IsNullOrWhiteSpace(NewClientName);

    [RelayCommand(CanExecute = nameof(CanAddContact))]
    private Task AddContactAsync() => RunAsync(
        async () =>
        {
            var request = new ContactRequest(
                ContactName.Trim(), Blank(ContactTitle), Blank(ContactEmail), Blank(ContactPhone), ContactIsPrimary, Client!.Version);
            Client = await api.AddContactAsync(Client.Id, request);
            InfoMessage = $"{request.FullName} added to {Client.Name}.";
            ContactName = ContactTitle = ContactEmail = ContactPhone = string.Empty;
            ContactIsPrimary = false;
            await ReloadListAsync(Client.Id);
        },
        onConflict: ReloadClientAsync);

    private bool CanAddContact() => CanManage && !string.IsNullOrWhiteSpace(ContactName);

    [RelayCommand]
    private Task RemoveContactAsync(ContactDto contact) => RunAsync(
        async () =>
        {
            Client = await api.RemoveContactAsync(Client!.Id, contact.Id, Client.Version);
            InfoMessage = $"{contact.FullName} removed.";
            await ReloadListAsync(Client.Id);
        },
        onConflict: ReloadClientAsync);

    private async Task ReloadClientAsync() => Client = await api.GetClientAsync(Client!.Id);

    private async Task ReloadListAsync(int? selectId)
    {
        var clients = await api.GetClientsAsync(Search);

        // Rebuilding the list changes the selection; load the details once, below.
        _rebuildingList = true;
        try
        {
            Clients.Clear();
            foreach (var client in clients)
            {
                Clients.Add(client);
            }

            SelectedClient = Clients.FirstOrDefault(c => c.Id == selectId) ?? Clients.FirstOrDefault();
        }
        finally
        {
            _rebuildingList = false;
        }

        Client = SelectedClient is null ? null : await api.GetClientAsync(SelectedClient.Id);
    }

    private static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
