using CoreDesign.Identity.Server;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});
builder.Services.AddOpenApi();
builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

builder.Services.AddIdentityServer(builder.Configuration, sectionName: "CoreDesign:IdentityWebHost");
builder.Services.AddJsonFileIdentityStore("identities.json");
builder.Services.AddJsonFileClientStore("clients.json");

var app = builder.Build();

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Servers = [];
        options.WithTheme(ScalarTheme.BluePlanet)
            .WithDefaultHttpClient(ScalarTarget.JavaScript, ScalarClient.HttpClient);
    });
}

app.MapIdentityEndpoints();

app.Run();
