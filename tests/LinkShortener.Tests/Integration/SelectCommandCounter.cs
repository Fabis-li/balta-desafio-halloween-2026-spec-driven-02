using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LinkShortener.Tests.Integration;

// Conta as consultas enviadas ao banco, para provar que o redirect saiu do cache (D-06)
public sealed class SelectCommandCounter : DbCommandInterceptor
{
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Track(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        Track(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    private void Track(DbCommand command)
    {
        if (command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            Interlocked.Increment(ref _count);
    }
}
