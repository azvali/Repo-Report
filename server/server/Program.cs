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

    var requestMessage = new HttpRequestMessage(HttpMethod.Get, apiUrl);

    var response = await client.SendAsync(requestMessage);

    if(!response.IsSuccessStatusCode){
        return Results.BadRequest(new { message = $"Github API error: {response.StatusCode}"});
    }

    var data = await response.Content.ReadAsStringAsync();
    var jsonCommits = JsonSerializer.Deserialize<JsonElement>(data);

    var tasks = new List<Task<CleanedItem>>();

    foreach(var commit in jsonCommits.EnumerateArray()){
        tasks.Add(GetDetails(commit, httpClientFactory, owner, repo));
    }

    var res = await Task.WhenAll(tasks);

    return Results.Ok(res);
});

app.Run();

static async Task<CleanedItem> GetDetails(JsonElement commit, IHttpClientFactory httpClientFactory, string owner, string repo){
    var client = httpClientFactory.CreateClient();
    client.DefaultRequestHeaders.Add("User-Agent", "Repo-Report-App");

    var sha = commit.GetProperty("sha").GetString();
    var commitDetailsUrl = $"https://api.github.com/repos/{owner}/{repo}/commits/{sha}";

    var request = new HttpRequestMessage(HttpMethod.Get, commitDetailsUrl);
    request.Headers.Add("Accept", "application/vnd.github.diff");

    var response = await client.SendAsync(request);
    var diff = "error fetching diff.";

    if(response.IsSuccessStatusCode){
        diff = await response.Content.ReadAsStringAsync();
    }

    var commitData = commit.GetProperty("commit");
    var committerData = commitData.GetProperty("committer");


    return new CleanedItem
    {
        Date = committerData.GetProperty("date").GetString(),
        Committer = committerData.GetProperty("name").GetString(),
        Comment = commitData.GetProperty("message").GetString(),
        Hash = sha,
        commitURL = commit.GetProperty("html_url").GetString(),
        Diff = diff
    };

}

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

public class CleanedItem{
    public string Date {get; set;}
    public string Committer {get; set;}
    public string Comment {get; set;}
    public string Hash {get; set;}
    public string commitURL {get; set;}
    public string Diff {get; set;}
}
