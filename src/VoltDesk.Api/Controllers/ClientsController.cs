using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VoltDesk.Api.Auth;
using VoltDesk.Contracts;
using VoltDesk.Core.Clients;
using VoltDesk.Core.Common;
using VoltDesk.Core.Projects;
using VoltDesk.Infrastructure.Persistence;

namespace VoltDesk.Api.Controllers;

[ApiController]
[Route("api/clients")]
public sealed class ClientsController(VoltDeskDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<List<ClientSummaryDto>> List([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var query = db.Clients.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c => c.Name.Contains(term) || c.Contacts.Any(x => x.FullName.Contains(term)));
        }

        return await query
            .OrderBy(c => c.Name)
            .Select(c => new ClientSummaryDto(
                c.Id,
                c.Name,
                c.Contacts.Where(x => x.IsPrimary).Select(x => x.FullName).FirstOrDefault(),
                c.Contacts.Where(x => x.IsPrimary).Select(x => x.Email).FirstOrDefault(),
                db.Projects.Count(p => p.ClientId == c.Id && p.Status == ProjectStatus.Active)))
            .ToListAsync(cancellationToken);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClientDto>> Get(int id, CancellationToken cancellationToken)
    {
        var client = await Load(id, cancellationToken);
        return client is null ? NotFound() : client.ToDto(db);
    }

    [HttpGet("{id:int}/history")]
    public Task<List<AuditEntryDto>> History(int id, CancellationToken cancellationToken) =>
        db.HistoryAsync(nameof(Client), id, cancellationToken);

    [HttpPost]
    [Authorize(Policy = Policies.ManageProjects)]
    public async Task<ActionResult<ClientDto>> Create(CreateClientRequest request, CancellationToken cancellationToken)
    {
        var client = Client.Create(request.Name);
        db.Clients.Add(client);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = client.Id }, client.ToDto(db));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.ManageProjects)]
    public Task<ActionResult<ClientDto>> Rename(int id, RenameClientRequest request, CancellationToken cancellationToken) =>
        Change(id, request.Version, client => client.Rename(request.Name), cancellationToken);

    [HttpPost("{id:int}/contacts")]
    [Authorize(Policy = Policies.ManageProjects)]
    public Task<ActionResult<ClientDto>> AddContact(int id, ContactRequest request, CancellationToken cancellationToken) =>
        Change(
            id,
            request.Version,
            client => client.AddContact(request.FullName, request.Title, request.Email, request.Phone, request.MakePrimary),
            cancellationToken);

    [HttpPut("{id:int}/contacts/{contactId:int}")]
    [Authorize(Policy = Policies.ManageProjects)]
    public Task<ActionResult<ClientDto>> UpdateContact(
        int id,
        int contactId,
        ContactRequest request,
        CancellationToken cancellationToken) =>
        Change(
            id,
            request.Version,
            client =>
            {
                var contact = FindContact(client, contactId);
                contact.Update(request.FullName, request.Title, request.Email, request.Phone);
                if (request.MakePrimary)
                {
                    client.SetPrimaryContact(contact);
                }
            },
            cancellationToken);

    [HttpDelete("{id:int}/contacts/{contactId:int}")]
    [Authorize(Policy = Policies.ManageProjects)]
    public Task<ActionResult<ClientDto>> RemoveContact(
        int id,
        int contactId,
        [FromQuery, Required] string version,
        CancellationToken cancellationToken) =>
        Change(id, version, client => client.RemoveContact(FindContact(client, contactId)), cancellationToken);

    private async Task<ActionResult<ClientDto>> Change(
        int id,
        string version,
        Action<Client> change,
        CancellationToken cancellationToken)
    {
        var client = await Load(id, cancellationToken);
        if (client is null)
        {
            return NotFound();
        }

        db.Entry(client).ExpectVersion(version);
        change(client);
        await db.SaveChangesAsync(cancellationToken);
        return client.ToDto(db);
    }

    private Task<Client?> Load(int id, CancellationToken cancellationToken) =>
        db.Clients.Include(c => c.Contacts).SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    private static Contact FindContact(Client client, int contactId) =>
        client.Contacts.SingleOrDefault(c => c.Id == contactId)
        ?? throw new DomainException($"{client.Name} has no contact {contactId}.");
}
