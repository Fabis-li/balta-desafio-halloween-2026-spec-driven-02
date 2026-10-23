namespace LinkShortener.Api.Application;

// Code nulo pede código automático (RF-02); preenchido, é o código próprio (RF-03)
// ExpiresOn nulo cria link sem expiração; preenchido, é a data yyyy-MM-dd em UTC (RF-06, D-09)
public record CreateLinkCommand(string? TargetUrl, string? Code = null, string? ExpiresOn = null);
