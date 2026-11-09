using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VoltDesk.Core.Projects;
using VoltDesk.Infrastructure.Persistence;

namespace VoltDesk.Infrastructure.Integrations.Google;

/// <summary>
/// Finds or creates the Drive folders of a project. Every created folder is saved right away, so a
/// retry after a failure continues where it stopped instead of creating duplicates.
/// </summary>
public sealed class GoogleFolders(VoltDeskDbContext db, IGoogleWorkspace google, IOptions<GoogleOptions> options)
{
    public const string ProjectSubfolder = "ProjectSubfolder";

    public async Task<ExternalLink> EnsureProjectFolderAsync(Project project, CancellationToken cancellationToken)
    {
        var folder = await FindAsync(ExternalLink.ProjectFolder, project.Id, string.Empty, cancellationToken);
        if (folder is null)
        {
            var root = await EnsureRootAsync(cancellationToken);
            var created = await google.CreateFolderAsync($"{project.Number} {project.Name}", root.ExternalId, cancellationToken);
            folder = await SaveAsync(ExternalLink.ProjectFolder, project.Id, string.Empty, created, cancellationToken);
        }

        foreach (var name in options.Value.ProjectSubfolders)
        {
            if (await FindAsync(ProjectSubfolder, project.Id, name, cancellationToken) is null)
            {
                var created = await google.CreateFolderAsync(name, folder.ExternalId, cancellationToken);
                await SaveAsync(ProjectSubfolder, project.Id, name, created, cancellationToken);
            }
        }

        return folder;
    }

    /// <summary>Drive id of a project subfolder such as "Panel schedules", creating folders as needed.</summary>
    public async Task<string> SubfolderIdAsync(Project project, string name, CancellationToken cancellationToken)
    {
        var folder = await EnsureProjectFolderAsync(project, cancellationToken);
        var subfolder = await FindAsync(ProjectSubfolder, project.Id, name, cancellationToken);
        if (subfolder is null)
        {
            var created = await google.CreateFolderAsync(name, folder.ExternalId, cancellationToken);
            subfolder = await SaveAsync(ProjectSubfolder, project.Id, name, created, cancellationToken);
        }

        return subfolder.ExternalId;
    }

    public async Task<ExternalLink> EnsureRootAsync(CancellationToken cancellationToken)
    {
        var root = await FindAsync(ExternalLink.RootFolder, projectId: null, string.Empty, cancellationToken);
        if (root is not null)
        {
            return root;
        }

        var created = await google.CreateFolderAsync(options.Value.RootFolderName, parentId: null, cancellationToken);
        return await SaveAsync(ExternalLink.RootFolder, projectId: null, string.Empty, created, cancellationToken);
    }

    private Task<ExternalLink?> FindAsync(string kind, int? projectId, string key, CancellationToken cancellationToken) =>
        db.ExternalLinks
            .Where(l => l.Kind == kind && l.ProjectId == projectId && l.Key == key)
            .OrderBy(l => l.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<ExternalLink> SaveAsync(string kind, int? projectId, string key, DriveItem item, CancellationToken cancellationToken)
    {
        var link = new ExternalLink { Kind = kind, ProjectId = projectId, Key = key, ExternalId = item.Id, Url = item.Url };
        db.ExternalLinks.Add(link);
        await db.SaveChangesAsync(cancellationToken);
        return link;
    }
}
