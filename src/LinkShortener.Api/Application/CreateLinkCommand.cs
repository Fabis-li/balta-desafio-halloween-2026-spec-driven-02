namespace LinkShortener.Api.Application;

// Code nulo pede código automático (RF-02); preenchido, é o código próprio (RF-03)
public record CreateLinkCommand(string? TargetUrl, string? Code = null);
