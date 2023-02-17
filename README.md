# AICommit
This is not my idea, but I just created one to use Azure OpenAI instead of the base OpenAI resources.

## Requirements
You will need an [Azure OpenAI](https://azure.microsoft.com/products/cognitive-services/openai-service/) resource.
From the resource grab the following values and set as Environment variables on your machine.
- `AZURE_OPENAI_KEY` - the key from your provisioned resource
- `AZURE_OPENAI_ENDPOINT` - the endpoint URI from the provisioned resource
- `AZURE_MODEL_DEPLOYMENT` - the name you gave to one of your Azure OpenAI model deployments

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