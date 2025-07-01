using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

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




app.MapPost("/api/getSummaries", async ([FromBody] Request request, [FromServices] IHttpClientFactory httpClientFactory) => {
    
    if(request.Num == 0 || string.IsNullOrEmpty(request.Url)){
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

    client.DefaultRequestHeaders.Add("User-Agent", "Repo-Report-App");
    var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/commits?per_page={request.Num}";

    var response = await client.GetAsync(apiUrl);

    if(!response.IsSuccessStatusCode){
        return Results.BadRequest(new { message = $"Github API error: {response.StatusCode}"});
    }

    var content = await response.Content.ReadAsStringAsync();
    var jsonContent = JsonSerializer.Deserialize<JsonElement>(content);

    return Results.Ok(jsonContent);

    
});

app.Run();



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

