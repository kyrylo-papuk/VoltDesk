using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;
using VoltDesk.Infrastructure.Integrations.Google;

// One-time setup: signs in to the studio Google account in the browser and prints a refresh
// token for the API's Google:RefreshToken setting. See "Google Workspace" in the README.
if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/VoltDesk.GoogleAuth -- <client-id> <client-secret>");
    return 1;
}

var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
    new ClientSecrets { ClientId = args[0], ClientSecret = args[1] },
    GoogleOptions.Scopes,
    "voltdesk",
    CancellationToken.None,
    new NullDataStore());

Console.WriteLine("Refresh token (store it as Google:RefreshToken, never commit it):");
Console.WriteLine(credential.Token.RefreshToken);
return 0;
