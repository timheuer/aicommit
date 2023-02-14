using CliWrap;
using Spectre.Console;
using System.Text;
using System.Text.RegularExpressions;

var token = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
var model = Environment.GetEnvironmentVariable("AZURE_MODEL_DEPLOYMENT");
var prompt = "I want you to act like a git commit message writer. I will input a git diff and your job is to convert it into a useful commit message. Do not preface the commit with anything, use the present tense, return a complete sentence, and do not repeat yourself: {0}";
var stdOutBuffer = new StringBuilder();
var stdErrBuffer = new StringBuilder();
var diffCreated = true;
var commitMessage = string.Empty;

// check for open ai key data
if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(model))
{
    AnsiConsole.Console.MarkupLine("[bold red]ERROR: No AZURE_OPENAI_KEY variable found[/]");
    return;
}

await AnsiConsole.Status()
    .Start("Analyzing diff...", async ctx =>
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

        if (result.ExitCode != 0 || stdOut.Length < 1) diffCreated = false;

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
            return;
        }

        // generate commit message
        var options = new Azure.AI.OpenAI.CompletionsOptions()
        {
            Prompt = { String.Format(prompt, stdOut) },
            Temperature = 0.7f,
            MaxTokens = 2048,
            FrequencyPenalty = 0,
            PresencePenalty = 0,
            Model = "text-davinci-003",
            NucleusSamplingFactor = 1
        };

#pragma warning disable CS8604 // Possible null reference argument.
        var oai = new Azure.AI.OpenAI.OpenAIClient(new Uri(endpoint), new Azure.AzureKeyCredential(token));
#pragma warning restore CS8604 // Possible null reference argument.
        var completions = await oai.GetCompletionsAsync(model, options, new CancellationToken());
        commitMessage = completions.Value.Choices[0].Text;
        commitMessage = Regex.Replace(commitMessage, "(\r\n|\n|\r)+", string.Empty);

        // show commit message
        AnsiConsole.MarkupLine($"[bold white]Commit message: {commitMessage}\n[/]");
    });

if (diffCreated)
{
    if (!AnsiConsole.Confirm("Use commit messsage?"))
    {
        AnsiConsole.Markup("Ok...cancelling");
        return;
    }
    else
    {
        stdOutBuffer.Clear();
        stdErrBuffer.Clear();

        var result = await Cli.Wrap("git")
                .WithArguments($"commit -m \"{commitMessage}\"")
                .WithWorkingDirectory(Environment.CurrentDirectory)
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
                .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrBuffer))
                .WithValidation(CommandResultValidation.None)
                .ExecuteAsync();

        var stdOut = stdOutBuffer.ToString();
        var stdErr = stdErrBuffer.ToString();
    }
}