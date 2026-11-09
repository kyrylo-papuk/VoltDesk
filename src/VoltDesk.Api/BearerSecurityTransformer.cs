using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace VoltDesk.Api;

/// <summary>Describes JWT bearer auth in the OpenAPI document, so Swagger UI offers "Authorize".</summary>
internal sealed class BearerSecurityTransformer : IOpenApiDocumentTransformer
{
    private const string SchemeId = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeId] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Token from POST /api/auth/login.",
        };
        document.Security =
        [
            new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(SchemeId, document)] = [] },
        ];
        return Task.CompletedTask;
    }
}
