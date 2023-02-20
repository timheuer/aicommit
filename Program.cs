using aicommits;
using Azure.AI.OpenAI;
using CliWrap;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using System.Text;

const int MAX_TOKENS = 256;
const string prompt = "Write an insightful but concise Git commit message in a complete sentence in imperative present tense for the following diff without prefacing it with anything: {0}";
    
var stdOutBuffer = new StringBuilder();
var stdErrBuffer = new StringBuilder();
var diffCreated = false;
var commitMessage = string.Empty;
Completions? completions = null;

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
        var result = await Cli.Wrap("git")
            .WithArguments($"diff --cached .")
            .WithWorkingDirectory(Environment.CurrentDirectory)
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrBuffer))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync();

        var stdOut = stdOutBuffer.ToString();
        var stdErr = stdErrBuffer.ToString();

        if (result.ExitCode == 0 && stdOut.Length > 1) diffCreated = true;

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
            Prompt = { String.Format(prompt, stdOut) },
            Temperature = 0.7f,
            MaxTokens = MAX_TOKENS,
            FrequencyPenalty = 0,
            PresencePenalty = 0,
            Model = "text-davinci-003",
            NucleusSamplingFactor = 1, SnippetCount = numMessages
        };

#pragma warning disable CS8604 // Possible null reference argument.
        var oai = new Azure.AI.OpenAI.OpenAIClient(new Uri(endpoint), new Azure.AzureKeyCredential(token));
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
        // show commit message
        AnsiConsole.MarkupLine($"[bold white]Commit message: {commitMessage}\n[/]");
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
        stdOutBuffer.Clear();
        stdErrBuffer.Clear();

        var result = await Cli.Wrap("git")
                .WithArguments($"commit -m \"{selectedMessage}\"")
                .WithWorkingDirectory(Environment.CurrentDirectory)
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
                .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrBuffer))
                .WithValidation(CommandResultValidation.None)
                .ExecuteAsync();

        var stdOut = stdOutBuffer.ToString();
        var stdErr = stdErrBuffer.ToString();

        AnsiConsole.Write(stdOut);
    }
}
