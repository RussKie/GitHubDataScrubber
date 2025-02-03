using System.CommandLine;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

var inputFileOption = new Option<string?>(["-i", "--input"], () => null, "GitHub data file") { IsRequired = true };

inputFileOption.AddValidator(r =>
{
    var inputFile = r.GetValueOrDefault<string?>();

    if (string.IsNullOrWhiteSpace(inputFile))
    {
        r.ErrorMessage = "GitHub data file is required. The file is produced by ghdump tool (see https://github.com/davidfowl/feedbackflow).\r\nPlease specify it via the command line argument -i/--input.";
    }
    else if (!File.Exists(inputFile))
    {
        r.ErrorMessage = $"GitHub data file '{inputFile}' not found!";
    }
});

var outputPath = new Option<string?>(["-o", "--output"], () => null, "The directory where the results will be written. Defaults to the current working directory");

var rootCommand = new RootCommand("CLI tool for extracting GitHub issues and discussions in a JSON format.")
{
    inputFileOption,
    outputPath
};

rootCommand.SetHandler(Run, inputFileOption, outputPath);

return await rootCommand.InvokeAsync(args);

Task<int> Run(string inputFile, string? outputDirectory)
{
    try
    {
        outputDirectory ??= Environment.CurrentDirectory;
        string outputFile = Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(inputFile)}.csv");

        if (!File.Exists(inputFile))
        {
            Console.Error.WriteLine("Input file not found!");
            return Task.FromResult(1);
        }

        var excludedAuthors = new[] { "??", "maestro-bot", "dotnet-policy-service", "dotnet-issue-labeler", "dotnet-maestro", "azure-pipelines", "ryujit-bot" };

        int issuesScrubbed = 0;
        int commentsScrubbed = 0;

        List<string> authors = [];

        using (StreamReader sr = new(inputFile))
        using (JsonTextReader reader = new(sr))
        using (StreamWriter sentenceWriter = new(outputFile))
        {
            JsonSerializer serializer = new();

            reader.Read(); // Read start of array
            while (reader.Read() && reader.TokenType != JsonToken.EndArray)
            {
                Issue issue = serializer.Deserialize<Issue>(reader);

                List<string> allSentences = [];
                if (!excludedAuthors.Contains(issue.Author))
                {
                    authors.Add(issue.Author);
                    issue.Body = ScrubContent(issue.Body);
                    allSentences = [.. SplitIntoSentences(issue.Body)];

                    issuesScrubbed++;
                }

                issue.Comments.RemoveAll(comment => excludedAuthors.Contains(comment.Author));

                foreach (Comment comment in issue.Comments)
                {
                    authors.Add(comment.Author);

                    comment.Content = ScrubContent(comment.Content);
                    allSentences.AddRange(SplitIntoSentences(comment.Content));
                }

                commentsScrubbed += issue.Comments.Count;

                WriteSentencesToFile(sentenceWriter, allSentences);
            }
        }

        Console.WriteLine($"Issues scrubbed: {issuesScrubbed}, comments scrubbed: {commentsScrubbed}");

        //File.WriteAllLines("authors.txt", authors.Distinct().OrderBy(a => a));
        //Console.WriteLine($"Authors extracted and saved as authors.txt");

        SortAndDeduplicateFile(outputFile);
        Console.WriteLine($"Sentences extracted and saved as {outputFile}");

        return Task.FromResult(0);
    }
    catch
    {
        Console.Error.WriteLine("The operation failed");
        return Task.FromResult(-1);
    }
}

static void SortAndDeduplicateFile(string sentencesFile)
{
    var uniqueSentences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var sortedSentences = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

    using (StreamReader reader = new(sentencesFile))
    {
        string line;
        while ((line = reader.ReadLine()) != null)
        {
            // Validate the line (you can add your own validation logic here)
            line = IsValidSentence(line);
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            // Add to HashSet to ensure uniqueness
            if (uniqueSentences.Add(line))
            {
                // Add to SortedSet to maintain sorted order
                sortedSentences.Add(line);
            }
        }
    }

    File.WriteAllLines(sentencesFile, sortedSentences);
}

static string IsValidSentence(string sentence)
{
    if (string.IsNullOrWhiteSpace(sentence))
    {
        return string.Empty;
    }

    // NOTE: all sentences start and end with double quotes
    sentence = sentence.TrimStart('"').TrimEnd('"');

    // Remove branch, build, and metadata lines
    sentence = Regex.Replace(sentence, "^- \\*\\*(Branch|Build|Coherency Updates|Commit|Date Produced)\\*\\*: .*", "", RegexOptions.Singleline);

    // Remove lines starting with "/azp run"
    sentence = Regex.Replace(sentence, "^/azp.*", "", RegexOptions.Singleline);

    // Remove GitHub issue-closing keywords followed by issue numbers
    sentence = Regex.Replace(sentence, "^(Fix|Fixes|Close|Closes|Resolves)(:?)\\s+#\\d+.*", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
    sentence = Regex.Replace(sentence, "^(Fix|Fixes|Close|Closes|Resolves)(:?)\\s+[a-zA-Z0-9_-]+/[a-zA-Z0-9_-]+#\\d+.*", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);

    sentence = sentence.Replace("<details>", "").Replace("</details>", "");
    sentence = sentence.Replace("<summary>", "").Replace("</summary>", "");
    sentence = sentence.Replace("<div>", "").Replace("</div>", "");
    sentence = sentence.Replace("<span>", "").Replace("</span>", "");

    sentence = sentence.Trim();

    if (string.IsNullOrWhiteSpace(sentence))
    {
        return string.Empty;
    }

    // Reinstate double quotes
    return $"\"{sentence}\"";
}

static string ScrubContent(string content)
{
    if (string.IsNullOrEmpty(content))
        return content;

    // Replace GitHub aliases (@alias, @alias-suffix, @alias_suffix) with "@github"
    content = Regex.Replace(content, "@\\w+(-\\w+|_\\w+)?", "@github");

    // Replace markdown-formatted links entirely with "#url"
    content = Regex.Replace(content, "\\[.*?\\]\\(.*?\\)", "#url");

    // Replace explicit URLs (http:// or https://) with "#url"
    content = Regex.Replace(content, "https?://\\S+", "#url");

    // Remove markdown quotations (lines starting with '> ')
    content = Regex.Replace(content, "^>.*", "", RegexOptions.Multiline);


    // Remove markdown tables, including those with inconsistent spacing
    content = Regex.Replace(content, "^\\s*\\|.*\\|\\s*$", "", RegexOptions.Multiline);
    content = Regex.Replace(content, "^\\s*-+:?-+\\s*$", "", RegexOptions.Multiline);

    // Replace code blocks (triple or quadruple backticks) with "#code"
    content = Regex.Replace(content, "```.*?```|````.*?````", "#code", RegexOptions.Singleline);

    // Replace inline code wrapped in backticks with "#code"
    content = Regex.Replace(content, "`(.*?)`", "#code");

    return content.Trim();
}

static List<string> SplitIntoSentences(string content)
{
    if (string.IsNullOrEmpty(content))
        return new List<string>();

    List<string> allSentences = new();
    string[] lines = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);

    foreach (var line in lines)
    {
        string cleanedLine = line.Trim();
        if (!string.IsNullOrEmpty(cleanedLine))
        {
            string[] sentences = Regex.Split(cleanedLine, "(?<=[.!?])\\s+");
            allSentences.AddRange(sentences);
        }
    }

    return allSentences;
}
static void WriteSentencesToFile(StreamWriter writer, List<string> sentences)
{
    foreach (var sentence in sentences)
    {
        string escapedSentence = sentence.Replace("\"", "\"\""); // Escape double quotes
        writer.WriteLine($"\"{escapedSentence}\""); // Wrap in double quotes
    }
    writer.Flush(); // Ensure data is written immediately
}
