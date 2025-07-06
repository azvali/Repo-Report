using System.Collections;
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
    
    var overallSummaryTask = GetOverallSummary(res, httpClientFactory, open_api_key);
    var individualSummaryTasks = res.Select(commit => GetIndividualSummary(commit, httpClientFactory, open_api_key)).ToList();

    await Task.WhenAll(individualSummaryTasks);
    var overallSummary = await overallSummaryTask;

    var finalResponse = new ApiResponse
    {
        OverallSummary = overallSummary,
        IndividualSummaries = individualSummaryTasks.Select(task => task.Result).ToList()
    };

    return Results.Ok(finalResponse);
});

app.Run();

static async Task<CompleteSummary> GetIndividualSummary(CleanedItem commit, IHttpClientFactory httpClientFactory, string api_key){
    var client = httpClientFactory.CreateClient();
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {api_key}");

    var payload = new {
        model = "gpt-4o-mini",
        messages = new[]
        {
            new { role = "system", content = "You are an expert at summarizing individual GitHub commits into a single, clear sentence." },
            new { role = "user", content = $"To the best of your ability. Give an insightful summary about this github commit given the content. {JsonSerializer.Serialize(commit)}" }
        },
        max_tokens = 150
    };

    var jsonPayload = JsonSerializer.Serialize(payload);

    var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
    requestMessage.Content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");


    var response = await client.SendAsync(requestMessage);

    var finishedSum = new CompleteSummary
    {
        Date = commit.Date,
        Committer = commit.Committer,
        Comment = commit.Comment,
        Hash = commit.Hash,
        commitURL = commit.commitURL,
        Summary = "Error: Could not generate summary."
    };

    if (response.IsSuccessStatusCode)
    {
        try
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);
            var summary = jsonResponse.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            finishedSum.Summary = summary ?? "Summary could not be extracted from AI response.";
        }
        catch (Exception)
        {
            //leave default msg
        }
    }

    return finishedSum;
}

static async Task<string> GetOverallSummary(IEnumerable<CleanedItem> commits, IHttpClientFactory httpClientFactory, string api_key){
    var ai_client = httpClientFactory.CreateClient();
    ai_client.DefaultRequestHeaders.Add("Authorization", $"Bearer {api_key}");

    var openAiPayload = new
    {
        model = "gpt-4o-mini",
        messages = new[]
        {
            new { role = "system", content = "You are a helpful assistant that summarizes GitHub commit history and diff logs in a clear, simple, and insightful way. Your goal is to help developers quickly understand the recent changes and evolution of a repository." }, //give gpt a role
            new { role = "user", content = "Please summarize the following GitHub commit messages and diff logs. Focus on the overall changes, trends, and significant modifications made over time. Make it concise, easy to understand, and useful for someone reviewing recent history. Here is the data:\n\n" + JsonSerializer.Serialize(commits) } //tell gpt to summarize everything
        },
        max_tokens = 400
    };

    var jsonContent = JsonSerializer.Serialize(openAiPayload);

    var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
    requestMessage.Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");


    var response = await ai_client.SendAsync(requestMessage);

    if (response.IsSuccessStatusCode)
    {
        try
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);
            return jsonResponse.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "Could not extract summary.";
        }
        catch (Exception)
        {
            return "Error: Failed to parse the AI response for the overall summary.";
        }
    }
    else
    {
        return "Error: The AI service failed to provide an overall summary.";
    }
}

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

public class CompleteSummary{
    public string Date {get; set;}
    public string Committer {get; set;}
    public string Comment {get; set;}
    public string Hash {get; set;}
    public string commitURL {get; set;}
    public string Summary {get; set;}
}

public class ApiResponse{
    public required string OverallSummary { get; set; }
    public required List<CompleteSummary> IndividualSummaries { get; set; }
}