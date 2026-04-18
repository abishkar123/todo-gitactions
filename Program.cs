using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.Extensions.Logging;
using TodoList.Configuration;
using TodoList.Repositories;
using TodoList.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<MongoDbSettings>()
    .Bind(builder.Configuration.GetSection(MongoDbSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IMongoClient>(serviceProvider =>
{
    var settings = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<MongoDbSettings>>()
        .Value;

    var logger = serviceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("TodoList.MongoDb");

    var client = new MongoClient(settings.ConnectionString);
    EnsureMongoConnectivity(client, settings.DatabaseName, logger, settings.ConnectionString);
    return client;
});

builder.Services.AddScoped<ITodoItemRepository, TodoItemRepository>();
builder.Services.AddScoped<ITodoItemService, TodoItemService>();
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Tasks}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

static void EnsureMongoConnectivity(
    IMongoClient client,
    string databaseName,
    ILogger logger,
    string connectionString)
{
    var preview = BuildConnectionPreview(connectionString);

    try
    {
        var database = client.GetDatabase(databaseName);
        database.RunCommand<BsonDocument>(new BsonDocument("ping", 1));
    }
    catch (MongoAuthenticationException authenticationException)
    {
        logger.LogError(authenticationException, "MongoDB authentication failed during startup (preview: {preview})", preview);
        throw new InvalidOperationException(
            "MongoDB authentication failed. Update MongoDb:ConnectionString so the SCRAM-SHA-1 credentials match your server.",
            authenticationException);
    }
    catch (MongoConfigurationException configurationException)
    {
        logger.LogError(configurationException, "MongoDB configuration failed during startup (preview: {preview})", preview);
        throw new InvalidOperationException(
            "MongoDB configuration is invalid. Ensure MongoDb:ConnectionString is a valid MongoDB URI.",
            configurationException);
    }
    catch (MongoException mongoException)
    {
        logger.LogError(mongoException, "Unable to reach MongoDB during startup (preview: {preview})", preview);
        throw new InvalidOperationException(
            "Unable to connect to MongoDB. Confirm the server is reachable and the connection string is correct.",
            mongoException);
    }
}

static string BuildConnectionPreview(string connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return "<empty>";
    }

    return connectionString.Length <= 32
        ? connectionString
        : $"{connectionString[..32]}...";
}

public partial class Program;
