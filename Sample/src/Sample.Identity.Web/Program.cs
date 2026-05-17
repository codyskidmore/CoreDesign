using CoreDesign.Identity.Server;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Services.AddIdentityServerWebHost(builder.Configuration);

var app = builder.Build();

app.MapIdentityServerWebHost();

app.Run();

