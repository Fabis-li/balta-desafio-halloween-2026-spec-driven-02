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

        return app;
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
            new CreateLinkCommand(request.TargetUrl, request.Code), cancellationToken);

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

public record CreateLinkRequest(string? TargetUrl, string? Code);

public record CreateLinkResponse(string ShortUrl, string Code);
