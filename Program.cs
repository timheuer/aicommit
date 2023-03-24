using aicommits;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using System.Diagnostics;

const int MAX_TOKENS = 256;
const string prompt = "Write an insightful but concise Git commit message in a complete sentence in imperative present tense for the following diff without prefacing it with anything: {0}";
    
var diffCreated = false;
var commitMessage = string.Empty;
Completions? completions = null;

AnsiConsole.Write(new FigletText("AICommit").LeftJustified().Color(Color.Green));

var config = new ConfigurationBuilder()
    .SetBasePath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
    .AddJsonFile(Path.Combine(".aicommit","appsettings.json"), optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

var token = config["AZURE_OPENAI_KEY"];
var endpoint = config["AZURE_OPENAI_ENDPOINT"];
var model = config["AZURE_MODEL_DEPLOYMENT"];

// check for open ai key data
if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(model))
{
    AnsiConsole.Console.MarkupLine("[bold red]ERROR: No Azure OpenAI variables found, please add as environment variables[/]");
    return;
}

// get the number of completions to generate form the args
int numMessages = 1;
if (args.Length > 0) numMessages = Convert.ToInt32(args[0]);

await AnsiConsole.Status()
    .StartAsync("Analyzing diff and generating commit message...", async ctx =>
    {
        ctx.Spinner(Spinner.Known.Dots2);
        ctx.SpinnerStyle(Style.Parse("green"));

        // run diff
        ProcessStartInfo diffProcess = new ProcessStartInfo("git", "diff --cached .")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Environment.CurrentDirectory
        };
        var diff = Process.Start(diffProcess);

#pragma warning disable CS8602 // Dereference of a possibly null reference.
        var stdOut = await diff.StandardOutput.ReadToEndAsync();
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        var stdErr = await diff.StandardError.ReadToEndAsync();

        if (diff.ExitCode == 0 && stdOut.Length > 1) diffCreated = true;

        // if no diff, send message nothing there
        if (!diffCreated)
        {
            AnsiConsole.MarkupLine("[bold red]No staged changes found. Make sure there are changes and run `git add .`[/]");
            return;
        }

        // if diff is too big, alert (> 8000)
        if (stdOut.Length > 8000)
        {
            AnsiConsole.MarkupLine("[bold red]Diff is too large to handle for the robots at this time[/]");
            diffCreated = false;
            return;
        }

        var finalPrompt = string.Format(prompt, stdOut);

        int estimatedTokenSize = Math.Max(finalPrompt.EstimateTokenSize(), finalPrompt.TokenCount()) + MAX_TOKENS;

        if (estimatedTokenSize > 4090)
        {
            AnsiConsole.MarkupLine($"[bold red]This exceeds token sizes right now at approximately {estimatedTokenSize} tokens with prompt and result[/]");
            diffCreated = false;
            return;
        }
        
        // generate commit message
        var options = new CompletionsOptions()
        {
            Prompts = { String.Format(prompt, stdOut) },
            Temperature = 0.7f,
            MaxTokens = MAX_TOKENS,
            FrequencyPenalty = 0,
            PresencePenalty = 0,
            NucleusSamplingFactor = 1, ChoicesPerPrompt = numMessages
        };

#pragma warning disable CS8604 // Possible null reference argument.
        var oai = new OpenAIClient(new Uri(endpoint), new Azure.AzureKeyCredential(token));
#pragma warning restore CS8604 // Possible null reference argument.
        try
        {
            var response = await oai.GetCompletionsAsync(model, options, new CancellationToken());
            completions = response.Value;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            diffCreated = false;
            return;
        }
    });

if (completions?.Choices.Count > 0)
{
    string[] choices = new string[completions.Choices.Count + 1];
    int i = 0;
    foreach (var choice in completions.Choices)
    {
        choices[i] = completions.Choices[i].Text.CleanMessage();
        i++;
    }
    choices[i] = "Cancel";

    var selectedMessage = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("Select a commit message or Cancel")
            .PageSize(10)
            .AddChoices(choices));

    if (selectedMessage == "Cancel")
    {
        AnsiConsole.Markup("Ok...cancelling");
        return;
    }
    else
    {
        ProcessStartInfo commitProcess = new ProcessStartInfo("git", $"commit -m \"{selectedMessage}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Environment.CurrentDirectory
        };

        var result = Process.Start(commitProcess);

#pragma warning disable CS8602 // Dereference of a possibly null reference.
        var stdOut = await result.StandardOutput.ReadToEndAsync();
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        var stdErr = await result.StandardError.ReadToEndAsync();

        if (result.ExitCode != 0)
        {
            AnsiConsole.MarkupLine($"[bold red]ERROR: {stdErr}[/]");
        }
        else
        {
            AnsiConsole.Write(stdOut);
        }
    }
}
