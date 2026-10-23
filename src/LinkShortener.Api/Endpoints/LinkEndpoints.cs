using System.Diagnostics;
using LinkShortener.Api.Application;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LinkShortener.Api.Endpoints;

public static class LinkEndpoints
{
    public static IEndpointRouteBuilder MapLinkEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/links", CreateLinkAsync);
        app.MapGet("/{code}", RedirectAsync);
        app.MapGet("/api/links/{code}/stats", GetStatsAsync);

        return app;
    }

    private static async Task<Results<Ok<LinkStatsResponse>, ProblemHttpResult>> GetStatsAsync(
        string code,
        LinkService linkService,
        CancellationToken cancellationToken)
    {
        var result = await linkService.GetStatsAsync(code, cancellationToken);

        return result switch
        {
            GetLinkStatsResult.Found found => TypedResults.Ok(new LinkStatsResponse(found.Code, found.AccessCount)),

            GetLinkStatsResult.NotFound => TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Link não encontrado",
                detail: GetLinkStatsResult.NotFound.Error),

            _ => throw new UnreachableException()
        };
    }

    private static async Task<Results<RedirectHttpResult, ProblemHttpResult>> RedirectAsync(
        string code,
        LinkService linkService,
        CancellationToken cancellationToken)
    {
        var result = await linkService.ResolveAsync(code, cancellationToken);

        return result switch
        {
            ResolveLinkResult.Found found => TypedResults.Redirect(found.TargetUrl),

            ResolveLinkResult.NotFound => TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Link não encontrado",
                detail: ResolveLinkResult.NotFound.Error),

            ResolveLinkResult.Expired => TypedResults.Problem(
                statusCode: StatusCodes.Status410Gone,
                title: "Link expirado",
                detail: ResolveLinkResult.Expired.Error),

            _ => throw new UnreachableException()
        };
    }

    private static async Task<Results<Created<CreateLinkResponse>, ValidationProblem, ProblemHttpResult>> CreateLinkAsync(
        CreateLinkRequest request,
        LinkService linkService,
        HttpRequest httpRequest,
        CancellationToken cancellationToken)
    {
        var result = await linkService.CreateAsync(
            new CreateLinkCommand(request.TargetUrl, request.Code, request.ExpiresOn), cancellationToken);

        switch (result)
        {
            case CreateLinkResult.Created created:
                var shortUrl = $"{httpRequest.Scheme}://{httpRequest.Host}/{created.Link.Code}";
                return TypedResults.Created(
                    shortUrl,
                    new CreateLinkResponse(shortUrl, created.Link.Code));

            case CreateLinkResult.Invalid invalid:
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    [invalid.Field] = [invalid.Error]
                });

            case CreateLinkResult.CodeTaken:
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Código em uso",
                    detail: CreateLinkResult.CodeTaken.Error);

            default:
                throw new UnreachableException();
        }
    }
}

// ExpiresOn chega como texto para a validação do formato ficar no serviço (D-09)
public record CreateLinkRequest(string? TargetUrl, string? Code, string? ExpiresOn);

public record CreateLinkResponse(string ShortUrl, string Code);

public record LinkStatsResponse(string Code, long AccessCount);
