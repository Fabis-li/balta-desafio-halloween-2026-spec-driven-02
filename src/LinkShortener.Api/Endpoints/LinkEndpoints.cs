using System.Diagnostics;
using LinkShortener.Api.Application;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LinkShortener.Api.Endpoints;

public static class LinkEndpoints
{
    public static IEndpointRouteBuilder MapLinkEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/links", CreateLinkAsync);

        return app;
    }

    private static async Task<Results<Created<CreateLinkResponse>, ValidationProblem>> CreateLinkAsync(
        CreateLinkRequest request,
        LinkService linkService,
        HttpRequest httpRequest,
        CancellationToken cancellationToken)
    {
        var result = await linkService.CreateAsync(request.TargetUrl, cancellationToken);

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
                    ["targetUrl"] = [invalid.Error]
                });

            default:
                throw new UnreachableException();
        }
    }
}

public record CreateLinkRequest(string? TargetUrl);

public record CreateLinkResponse(string ShortUrl, string Code);
