using System.Globalization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using Mingle.Client;
using Mingle.Client.Services;
using Mingle.Engine;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddLocalization();
builder.Services.AddScoped<Text>();
builder.Services.AddScoped<DeviceDeckStore>();
builder.Services.AddScoped<ContentService>();
builder.Services.AddScoped<GameSession>();
builder.Services.AddScoped<BlitzSession>();
builder.Services.AddScoped<StorySession>();
builder.Services.AddScoped<RoomClient>();

var host = builder.Build();

// The UI language is chosen before the app renders; switching language reloads the page.
var js = host.Services.GetRequiredService<IJSRuntime>();
var code = await js.InvokeAsync<string?>("mingle.getLang");
var lang = Langs.TryParse(code, out var parsed) ? parsed : Lang.En;
var culture = CultureInfo.GetCultureInfo(lang.Code());
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

await host.RunAsync();
