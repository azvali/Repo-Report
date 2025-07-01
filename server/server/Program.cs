using System.Text.Json;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddCors(options => {
    options.AddDefaultPolicy(policy => {
        policy.WithOrigins("*").AllowAnyHeader().AllowAnyMethod();
    });
});
builder.Services.AddHttpClient();

var app = builder.Build();

app.UseCors();


app.MapPost("/api/getSummaries", async (Request request, IHttpClientFactory httpClientFactory) => {
    
    if(!request.Num || !request.Url){
        return Results.BadRequest(new {message = "Input is invalid."});
    }

    if(request.Num > 30){
        return Results.BadRequest(new {message = "int > 30"});
    }

    var client = httpClientFactory.CreateClient();
    var (owner, repo) = ParseGitHubUrl(request.Url);
    if(owner == null || repo == null){
        return Results.BadRequest(new {message = "Invalid Github URL format."});
    }

    
});

static (string? owner, string? repo) ParseGitHubUrl(string url){

    var pattern = @"https://github\.com/([^/]+)/([^/]+)";
    var match = Regex.Match(url, pattern);

    if(match.Success){
        return(match.Groups[1].Value, match.Groups[2].Value);
    }

    return (null, null);
}
public class Request{
    public required int Num {get; set;}
    public required string Url {get; set;}
}

app.Run();
