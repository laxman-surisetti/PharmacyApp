using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Pharmacy.Api.Configuration;
using Pharmacy.Api.Domain;
using Pharmacy.Api.Infrastructure;
using Pharmacy.Api.Services;
using Pharmacy.Api.Storage;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------- configuration
builder.Services
    .AddOptions<PharmacyOptions>()
    .Bind(builder.Configuration.GetSection(PharmacyOptions.SectionName))
    .Validate(o => o.ExpiryWarningDays > 0, "Pharmacy:ExpiryWarningDays must be greater than zero.")
    .Validate(o => o.LowStockThreshold > 0, "Pharmacy:LowStockThreshold must be greater than zero.")
    .ValidateOnStart();

// ------------------------------------------------------------------------- MVC
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Enums travel as strings ("ExpiringSoon", not 1) so the contract stays readable
        // and a reordered enum cannot silently change the meaning of stored or sent data.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ABC Pharmacy API",
        Version = "v1",
        Description = "Medicine catalogue and sale records, persisted as JSON documents on the server."
    });

    // Swashbuckle does not know DateOnly out of the box.
    options.MapType<DateOnly>(() => new OpenApiSchema { Type = "string", Format = "date" });
    options.MapType<DateOnly?>(() => new OpenApiSchema { Type = "string", Format = "date", Nullable = true });

    var xmlPath = Path.Combine(AppContext.BaseDirectory, "Pharmacy.Api.xml");
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// ------------------------------------------------------------------------- CORS
const string SpaCorsPolicy = "spa";
builder.Services.AddCors(options => options.AddPolicy(SpaCorsPolicy, policy =>
{
    var origins = builder.Configuration
        .GetSection($"{PharmacyOptions.SectionName}:AllowedOrigins")
        .Get<string[]>() ?? ["http://localhost:4200"];

    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));

// -------------------------------------------------------------- application DI
// Everything is a singleton on purpose: the JSON stores hold the collection in memory
// behind a write lock, and that lock only means anything if there is exactly one of it.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPharmacyClock, PharmacyClock>();
builder.Services.AddSingleton<MedicineStatusEvaluator>();

builder.Services.AddSingleton<IJsonStore<Medicine>>(sp => new JsonFileStore<Medicine>(
    ResolveDataFile(sp, builder.Environment.ContentRootPath, "medicines.json"),
    () => SeedData.Medicines(),
    sp.GetRequiredService<ILogger<JsonFileStore<Medicine>>>()));

builder.Services.AddSingleton<IJsonStore<Sale>>(sp => new JsonFileStore<Sale>(
    ResolveDataFile(sp, builder.Environment.ContentRootPath, "sales.json"),
    () => SeedData.Sales(),
    sp.GetRequiredService<ILogger<JsonFileStore<Sale>>>()));

builder.Services.AddSingleton<IMedicineService, MedicineService>();
builder.Services.AddSingleton<ISaleService, SaleService>();

var app = builder.Build();

// ------------------------------------------------------------------- pipeline
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "ABC Pharmacy API v1");
        options.DocumentTitle = "ABC Pharmacy API";
    });
}

app.UseCors(SpaCorsPolicy);

// If the Angular app has been built into wwwroot (see README), serve it from here too so
// that one URL runs the whole application. In development the SPA runs on its own dev
// server and proxies /api back to this host instead.
if (Directory.Exists(Path.Combine(app.Environment.ContentRootPath, "wwwroot")))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).ExcludeFromDescription();

// Deep links such as /medicines/new belong to the Angular router, not to MVC.
if (Directory.Exists(Path.Combine(app.Environment.ContentRootPath, "wwwroot")))
{
    app.MapFallbackToFile("index.html");
}
else if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

app.Run();

static string ResolveDataFile(IServiceProvider services, string contentRoot, string fileName)
{
    var options = services.GetRequiredService<IOptions<PharmacyOptions>>().Value;
    var directory = Path.IsPathRooted(options.DataDirectory)
        ? options.DataDirectory
        : Path.Combine(contentRoot, options.DataDirectory);

    return Path.Combine(directory, fileName);
}

/// <summary>Exposed so tests can reference the entry-point assembly explicitly.</summary>
public partial class Program
{
}
