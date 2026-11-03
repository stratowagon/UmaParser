using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using UmaParser;
using UmaParser.MasterData;
using UmaParser.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

GameMasterBootstrap.UseEmbeddedCatalog();
builder.Services.AddSingleton(new WebSession(GameMasterBootstrap.StatusLine()));

await builder.Build().RunAsync();
