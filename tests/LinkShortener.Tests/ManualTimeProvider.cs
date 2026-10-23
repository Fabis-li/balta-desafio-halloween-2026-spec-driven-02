namespace LinkShortener.Tests;

// Relógio controlado pelo teste, para simular a passagem do tempo até a expiração
public sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
