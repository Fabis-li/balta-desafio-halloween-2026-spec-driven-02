using LinkShortener.Api.Application;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LinkShortener.Api.Data;

public class LinkRepository(AppDbContext dbContext) : ILinkRepository
{
    private const int SqliteConstraintUnique = 2067;

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken) =>
        dbContext.Links.AnyAsync(link => link.Code == code, cancellationToken);

    public Task<Link?> FindByCodeAsync(string code, CancellationToken cancellationToken) =>
        dbContext.Links.AsNoTracking().SingleOrDefaultAsync(link => link.Code == code, cancellationToken);

    // UPDATE Links SET AccessCount = AccessCount + 1 WHERE Code = @code
    public Task IncrementAccessCountAsync(string code, CancellationToken cancellationToken) =>
        dbContext.Links
            .Where(link => link.Code == code)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(link => link.AccessCount, link => link.AccessCount + 1),
                cancellationToken);

    public async Task<bool> TryAddAsync(Link link, CancellationToken cancellationToken)
    {
        dbContext.Links.Add(link);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqliteException { SqliteExtendedErrorCode: SqliteConstraintUnique })
        {
            dbContext.Entry(link).State = EntityState.Detached;
            return false;
        }
    }
}
