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




app.MapPost("/api/getSummaries", async ([FromBody] Request request, [FromServices] IHttpClientFactory httpClientFactory, [FromServices] IConfiguration configuration) => {
    
    if(request.Num == 0 || string.IsNullOrEmpty(request.Url)){
        return Results.BadRequest(new {message = "Input is invalid."});
    }

    if(request.Num > 30){
        return Results.BadRequest(new {message = "int > 30"});
    }

  
    var (owner, repo) = ParseGitHubUrl(request.Url);
    if(owner == null || repo == null){
        return Results.BadRequest(new {message = "Invalid Github URL format."});
    }

    var client = httpClientFactory.CreateClient();
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

    var open_api_key = configuration["OPENAI_API_KEY"];
    if(string.IsNullOrEmpty(open_api_key)){
        return Results.Problem(detail: "failed to fetch openai api key.",
        statusCode: 500,
        title: "Server config error."
    );}

    var ai_client = httpClientFactory.CreateClient();
    ai_client.DefaultRequestHeaders.Add("Authorization", $"Bearer {open_api_key}");

    var openAiPayload = new
    {
        model = "gpt-4o-mini", //mebbe use o4 mini 
        messages = new[]
        {
            new { role = "system", content = "You are a helpful assistant that summarizes GitHub commit history and diff logs in a clear, simple, and insightful way. Your goal is to help developers quickly understand the recent changes and evolution of a repository." }, //give gpt a role
            new { role = "user", content = "Please summarize the following GitHub commit messages and diff logs. Focus on the overall changes, trends, and significant modifications made over time. Make it concise, easy to understand, and useful for someone reviewing recent history. Here is the data:\n\n" + JsonSerializer.Serialize(res) } //tell gpt to summarize everything
        },
        max_tokens = 250 
    };

    var ai_jsonContent = JsonSerializer.Serialize(openAiPayload);
    var payload = new StringContent(
        ai_jsonContent,
        System.Text.Encoding.UTF8,
        "application/json"
    );

    var ai_apiUrl = "https://api.openai.com/v1/chat/completions";
    var ai_request = new HttpRequestMessage(HttpMethod.Post, ai_apiUrl);
    ai_request.Content = payload;

    var ai_response = await ai_client.SendAsync(ai_request);


    if (ai_response.IsSuccessStatusCode)
    {
        var ai_data_string = await ai_response.Content.ReadAsStringAsync();

        return Results.Ok(ai_data_string); 
    }
    else
    {

        var err = await ai_response.Content.ReadAsStringAsync();
        return Results.Problem(
            detail: err,
            statusCode: (int)ai_response.StatusCode,
            title: "Error from AI service"
        );
    }
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
