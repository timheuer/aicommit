using System.Text.RegularExpressions;

namespace aicommits;
static class Helpers
{
    public static int EstimateTokenSize(this string text)
    {
        // Calculate the word count by splitting the text by spaces
        int wordCount = text.Split(" ").Length;

        // Calculate the character count by getting the length of the text
        int charCount = text.Length;

        // Estimate tokens count by word and char methods
        int tokensCountWordEst = (int)Math.Ceiling(wordCount / 0.75);
        int tokensCountCharEst = (int)Math.Ceiling(charCount / 4.0);

        // Return the maximum of word and char estimates
        return Math.Max(tokensCountWordEst, tokensCountCharEst);
    }

    public static int TokenCount(this string text) => AI.Dev.OpenAI.GPT.GPT3Tokenizer.Encode(text).Count;

    public static string CleanMessage(this string text) => Regex.Replace(text, "(\r\n|\n|\r)+", string.Empty);
}
