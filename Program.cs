using MudBlazor.Services;
using WorldCupBuddy.Components;
using WorldCupBuddy.Services;

var builder = WebApplication.CreateBuilder(args);

// Blazor Server (interactive server components).
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// MudBlazor component library.
builder.Services.AddMudServices();

// Feature services.
builder.Services.AddHttpClient<OddsService>();
builder.Services.AddHttpClient<ProfileService>();
builder.Services.AddHttpClient<SocialService>();
builder.Services.AddScoped<SimulationService>();
builder.Services.AddScoped<ProfileState>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
