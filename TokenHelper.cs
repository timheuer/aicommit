using Microsoft.ML.Tokenizers;
using System.Text.RegularExpressions;

namespace aicommits;
static class Helpers
{
    public static int EstimateTokenSize(this string text)
    {
        // Calculate the word count by splitting the text by spaces
        int wordCount = Regex.Matches(text, @"\b\S+\b").Count;

        // Calculate the character count by getting the length of the text
        int charCount = text.Length;

        // Estimate tokens count by word and char methods
        int tokensCountWordEst = (int)Math.Ceiling(wordCount / 0.75);
        int tokensCountCharEst = (int)Math.Ceiling(charCount / 4.0);

        // Return the maximum of word and char estimates
        return Math.Max(tokensCountWordEst, tokensCountCharEst);
    }

    public static int TokenCount(this string text)
    {
        var tokenizer = new Tokenizer(new Bpe());
        var encodedResult = tokenizer.Encode(text);
        return encodedResult.Tokens.Count();
    }
    public static string CleanMessage(this string text) => Regex.Replace(text, "(\r\n|\n|\r)+", string.Empty);
}
