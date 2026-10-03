using System.Net;
using System.Diagnostics;
using RPG_Harness.Components;
using RPG_Harness.Services;

var builder = WebApplication.CreateBuilder(args);

// Static Web Assets manifest грузится по умолчанию только в Development и из publish-вывода.
// Без него в обычном прод-запуске маршруты ассетов известны, а физические файлы не резолвятся:
// blazor.web.js отдаёт 500, а CSS при gzip-переговорах — пустое тело (страница без стилей).
// Включаем манифест явно во всех окружениях.
Microsoft.AspNetCore.Hosting.StaticWebAssets.StaticWebAssetsLoader.UseStaticWebAssets(
    builder.Environment, builder.Configuration);

// Razor Components + интерактивность (Blazor Server, один circuit на вкладку).
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// HTTP-клиент для вызовов LLM API (длинный таймаут — на потоковую генерацию).
builder.Services.AddHttpClient(LlmClient.HttpClientName, client =>
{
    client.Timeout = TimeSpan.FromMinutes(10);
});

// Локальные сервисы харнеса.
builder.Services.AddSingleton(_ => new AppPaths(builder.Environment.ContentRootPath));
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddSingleton<ChatStore>();
builder.Services.AddSingleton<RpgStateStore>();
builder.Services.AddSingleton<FileTools>();
builder.Services.AddSingleton<ItemCatalog>();
builder.Services.AddSingleton<ItemEconomy>();
builder.Services.AddScoped<TradeFlow>();
builder.Services.AddScoped<GuildFlow>();
builder.Services.AddScoped<BattleFlow>();
builder.Services.AddScoped<BattleLog>();
builder.Services.AddScoped<PartyFlow>();
builder.Services.AddScoped<TalkFlow>();
builder.Services.AddSingleton<ChatCreationFlow>();
builder.Services.AddSingleton<LlmClient>();

// Каталог портретов (враги, спутники, герой) — wwwroot/sprites/portraits.json.
PortraitCatalog.Load(builder.Environment.WebRootPath ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot"));

// Английские названия предметов и портретов (оверлей каталогов для английского интерфейса).
CatalogEn.Load(builder.Environment.WebRootPath ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot"));

var app = builder.Build();

// Харнес хранит API-ключ и даёт доступ к локальной файловой системе, поэтому по умолчанию
// принимает запросы только с этого компьютера. Осознанное сетевое развёртывание требует
// внешней аутентификации и явного Harness:AllowRemoteAccess=true.
var allowRemoteAccess = app.Configuration.GetValue<bool>("Harness:AllowRemoteAccess");
if (!allowRemoteAccess)
{
    app.Use(async (context, next) =>
    {
        var remote = context.Connection.RemoteIpAddress;
        if (remote is not null && !IPAddress.IsLoopback(remote))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("RPG Harness accepts local connections only.");
            return;
        }

        await next();
    });
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
// HTTP-профиль намеренно слушает только localhost. Не подключаем middleware без
// настроенного HTTPS endpoint: иначе каждый запрос пишет бесполезное предупреждение
// «Failed to determine the https port». За TLS внешнего reverse proxy отвечает proxy.
var configuredUrls = app.Configuration["urls"] ?? app.Configuration["ASPNETCORE_URLS"] ?? "";
if (configuredUrls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Any(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps))
{
    app.UseHttpsRedirection();
}

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Опубликованная настольная сборка сама открывает локальную страницу. В Development
// это отключено через appsettings.Development.json: браузером управляет launchSettings.
if (app.Configuration.GetValue<bool>("Harness:OpenBrowserOnStart"))
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var localUrl = app.Urls
            .Select(url => url.Replace("0.0.0.0", "127.0.0.1", StringComparison.Ordinal)
                              .Replace("[::]", "127.0.0.1", StringComparison.Ordinal))
            .FirstOrDefault(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttp);

        if (localUrl is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(localUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Не удалось автоматически открыть браузер. Откройте {Url} вручную.", localUrl);
        }
    });
}

app.Run();
