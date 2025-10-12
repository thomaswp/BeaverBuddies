using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

class UpdateLocalizations
{
    // 🔧 Directory containing localization CSVs
    private const string LocalizationDir = @"../BeaverBuddies/Localizations";

    public static void Go()
    {
        Console.WriteLine("Paste your translation input (end with an empty line):");

        // Read full multiline input
        string input = File.ReadAllText("input.txt");

        // Extract blocks like "### 🇩🇪 German (deDE)" ... ```csv ... ```
        var translations = ParseInput(input);

        if (translations.Count == 0)
        {
            Console.WriteLine("No translations found in input. Exiting.");
            return;
        }

        Console.WriteLine($"\nFound {translations.Count} translation blocks.\n");
        
        string workingDirectory = Environment.CurrentDirectory;
        string projectDirectory = Directory.GetParent(workingDirectory).Parent.Parent.FullName;
        string localizationPath = Path.Combine(projectDirectory, LocalizationDir);

        // Process each localization
        var allCsvFiles = Directory.GetFiles(localizationPath, "*.csv");
        var unmatchedTranslations = new List<string>();
        var missingTranslations = new List<string>();

        foreach (var (locale, csvLines) in translations)
        {
            // Try to find matching csv file by locale prefix (e.g. deDE_*)
            string? csvPath = allCsvFiles.FirstOrDefault(f =>
                Path.GetFileName(f).StartsWith(locale, StringComparison.OrdinalIgnoreCase));

            if (csvPath == null)
            {
                Console.WriteLine($"⚠️ No CSV file found for locale: {locale}");
                unmatchedTranslations.Add(locale);
                continue;
            }

            Console.WriteLine($"📝 Processing {Path.GetFileName(csvPath)}...");

            var existingLines = File.ReadAllLines(csvPath).ToList();
            bool updated = false;

            foreach (var line in csvLines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Compare only the first field (the key)
                string key = line.Split(',')[0].Trim();
                bool alreadyPresent = existingLines.Any(l => l.StartsWith(key + ","));

                if (alreadyPresent)
                {
                    Console.WriteLine($"   ✓ Already present: {key}");
                }
                else
                {
                    existingLines.Add(line);
                    Console.WriteLine($"   ➕ Added: {key}");
                    updated = true;
                }
            }

            if (updated)
            {
                File.WriteAllLines(csvPath, existingLines);
                Console.WriteLine($"✅ Updated {Path.GetFileName(csvPath)}");
            }
            else
            {
                Console.WriteLine($"ℹ️ No new entries for {Path.GetFileName(csvPath)}");
            }

            Console.WriteLine();
        }

        // Check if any CSV files had no corresponding translation block
        var allLocales = allCsvFiles
            .Select(f => Path.GetFileName(f).Split('_')[0])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingCsvLocales = translations.Keys
            .Where(locale => !allLocales.Contains(locale))
            .ToList();

        Console.WriteLine("=== Summary ===");
        if (unmatchedTranslations.Any())
            Console.WriteLine($"⚠️ Missing CSV for locales: {string.Join(", ", unmatchedTranslations)}");
        if (missingCsvLocales.Any())
            Console.WriteLine($"⚠️ Translations without matching CSVs: {string.Join(", ", missingCsvLocales)}");
        if (!unmatchedTranslations.Any() && !missingCsvLocales.Any())
            Console.WriteLine("✅ All translations matched existing CSVs!");
    }

    // Reads multi-line console input until an empty line is entered
    static string ReadMultilineInput()
    {
        var lines = new List<string>();
        string? line;
        while (!string.IsNullOrEmpty(line = Console.ReadLine()))
        {
            lines.Add(line);
        }
        return string.Join("\n", lines);
    }

    // Parses the big input block into (locale, csvLines)
    static Dictionary<string, List<string>> ParseInput(string input)
    {
        var results = new Dictionary<string, List<string>>();
        // Example match: ### 🇩🇪 German (deDE)
        var regex = new Regex(@"### [^\(]*\(([A-Za-z]+,\s+)?(?<locale>[a-z]{2}[A-Z]{2})\)\s+```csv(?<csv>(.|\n)*?)```",
            RegexOptions.Singleline);

        foreach (Match match in regex.Matches(input))
        {
            string locale = match.Groups["locale"].Value.Trim();
            string csvBlock = match.Groups["csv"].Value.Trim();

            var csvLines = csvBlock
                .Split('\n')
                .Select(l => l.TrimEnd('\r'))
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            results[locale] = csvLines;
        }

        return results;
    }
}
