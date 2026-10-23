using LinkShortener.Api.Application;
using LinkShortener.Api.Data;
using LinkShortener.Api.Endpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ICodeGenerator, RandomCodeGenerator>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<LinkRepository>();
builder.Services.AddScoped<ILinkRepository>(services => new CachedLinkRepository(
    services.GetRequiredService<LinkRepository>(),
    services.GetRequiredService<IMemoryCache>(),
    services.GetRequiredService<TimeProvider>()));
builder.Services.AddScoped<LinkService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapLinkEndpoints();

app.Run();

public partial class Program;
