using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using HaversineGenerator;
using HaversineShared;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<IHaversineFormula, HaversineFormula>();
builder.Services.AddSingleton<App>();
using IHost host = builder.Build();

App app = host.Services.GetService<App>();
await app!.RunAsync();

// await host.RunAsync();