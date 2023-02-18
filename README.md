# AICommit
This is not my idea (original [Nutlope/aicommits](https://github.com/Nutlope/aicommits)), but I just created one to use Azure OpenAI instead of the base OpenAI resources and make it a .NET global tool.

## Requirements
You will need an [Azure OpenAI](https://azure.microsoft.com/products/cognitive-services/openai-service/) resource.
From the resource grab the following values and set as configuration variables on your machine.
- `AZURE_OPENAI_KEY` - the key from your provisioned resource
- `AZURE_OPENAI_ENDPOINT` - the endpoint URI from the provisioned resource
- `AZURE_MODEL_DEPLOYMENT` - the name you gave to one of your Azure OpenAI model deployments

These can be set as either Environment variables or in the user app local data directory under `.aicommit/appsettings.json` in the following format:
```
{
  "AZURE_OPENAI_KEY": "your api key",
  "AZURE_OPENAI_ENDPOINT": "your endpoint",
  "AZURE_MODEL_DEPLOYMENT": "your name of your model deployment"
}
```
The location for this would be in:
- Windows - `%localappdata%` which is usually `C:\Users\[username]\AppData\Local`
- macOS = `/Users/[username]/.local/share`

## Installation
To install use the `dotnet` CLI command:

```
dotnet tool install -g TimHeuer.Git.AICommit
```

## Usage
From a `git` repository if there are **staged** changes, just run `aicommit` from the repository.
It will attempt to generate a message for you and offer you to see it before using for the commit message.

## Data Sharing
Indeed this does take the text of your `git diff` output and send it to Azure OpenAI for completion.
**You should not use this if you have sensitive information.**