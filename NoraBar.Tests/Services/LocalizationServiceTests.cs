using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NoraBar.Models;
using NoraBar.Services;
using Xunit;

namespace NoraBar.Tests.Services;

public class LocalizationServiceTests
{
    [Theory]
    [InlineData(AppLanguage.Japanese)]
    [InlineData(AppLanguage.English)]
    public void AllLocalizationKeys_HaveValidTranslationsInAllLanguages(AppLanguage language)
    {
        foreach (LocalizationKey key in Enum.GetValues<LocalizationKey>())
        {
            string text = LocalizationService.GetText(language, key);
            Assert.False(string.IsNullOrWhiteSpace(text), $"LocalizationKey.{key} is missing or empty for language '{language}'.");
        }
    }

    [Fact]
    public void AllXamlFiles_HaveNoHardcodedUserVisibleStrings()
    {
        string baseDir = AppContext.BaseDirectory;
        string noraBarDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "NoraBar"));
        if (!Directory.Exists(noraBarDir))
        {
            noraBarDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "NoraBar"));
        }

        Assert.True(Directory.Exists(noraBarDir), $"NoraBar directory not found at '{noraBarDir}'.");

        string[] xamlFiles = Directory.GetFiles(noraBarDir, "*.xaml", SearchOption.AllDirectories);
        Assert.True(xamlFiles.Length > 0, "No XAML files found to test.");

        List<string> violations = [];

        foreach (string file in xamlFiles)
        {
            string relativePath = Path.GetRelativePath(noraBarDir, file);

            // bin, obj, Resources, App.xaml などビルド生成物やリソース定義は対象外
            if (relativePath.StartsWith("bin", StringComparison.OrdinalIgnoreCase) ||
                relativePath.StartsWith("obj", StringComparison.OrdinalIgnoreCase) ||
                relativePath.StartsWith("Resources", StringComparison.OrdinalIgnoreCase) ||
                relativePath.Equals("App.xaml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                XDocument doc = XDocument.Load(file);
                foreach (XElement element in doc.Descendants())
                {
                    foreach (XAttribute attr in element.Attributes())
                    {
                        string attrName = attr.Name.LocalName;
                        if (attrName is "Text" or "Content" or "Title" or "Header" or "ToolTip")
                        {
                            string val = attr.Value.Trim();
                            if (IsWhitelistedText(val)) continue;

                            violations.Add($"[{relativePath}] <{element.Name.LocalName}> has hardcoded {attrName}=\"{val}\"");
                        }
                    }

                    if (!element.HasElements)
                    {
                        string val = element.Value.Trim();
                        if (!IsWhitelistedText(val))
                        {
                            violations.Add($"[{relativePath}] <{element.Name.LocalName}> has hardcoded text \"{val}\"");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                violations.Add($"[{relativePath}] Failed to parse XAML: {ex.Message}");
            }
        }

        Assert.True(violations.Count == 0,
            $"Found {violations.Count} hardcoded user-visible text occurrences across XAML files:\n" +
            string.Join("\n", violations));
    }

    [Fact]
    public void AllCSharpFiles_HaveNoHardcodedUserVisibleStrings()
    {
        string baseDir = AppContext.BaseDirectory;
        string noraBarDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "NoraBar"));
        if (!Directory.Exists(noraBarDir))
        {
            noraBarDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "NoraBar"));
        }

        Assert.True(Directory.Exists(noraBarDir), $"NoraBar directory not found at '{noraBarDir}'.");

        string[] csFiles = Directory.GetFiles(noraBarDir, "*.cs", SearchOption.AllDirectories);
        Assert.True(csFiles.Length > 0, "No C# files found to test.");

        List<string> violations = [];
        var propertyAssignRegex = new Regex(@"\b(ToolTip|Text|Content|Header|Title)\s*=\s*""([^""]+)""", RegexOptions.Compiled);
        var japaneseTextRegex = new Regex(@"[\u3040-\u309F\u30A0-\u30FF\u4E00-\u9FFF]", RegexOptions.Compiled);

        foreach (string file in csFiles)
        {
            string relativePath = Path.GetRelativePath(noraBarDir, file);

            if (relativePath.StartsWith("bin", StringComparison.OrdinalIgnoreCase) ||
                relativePath.StartsWith("obj", StringComparison.OrdinalIgnoreCase) ||
                relativePath.StartsWith("Resources", StringComparison.OrdinalIgnoreCase) ||
                relativePath.EndsWith("LocalizationService.cs", StringComparison.OrdinalIgnoreCase) ||
                relativePath.Contains("AssemblyInfo", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                string trimmed = line.TrimStart();
                bool isExceptionLine = line.Contains("throw ") || line.Contains("Exception(") ||
                    (i > 0 && (lines[i - 1].Contains("Exception") || lines[i - 1].Contains("throw"))) ||
                    (i > 1 && (lines[i - 2].Contains("Exception") || lines[i - 2].Contains("throw"))) ||
                    (i > 2 && (lines[i - 3].Contains("Exception") || lines[i - 3].Contains("throw")));

                if (trimmed.StartsWith("//") || trimmed.StartsWith("/*") || trimmed.StartsWith("*") ||
                    isExceptionLine || trimmed.StartsWith("Trace.") || trimmed.StartsWith("Debug."))
                {
                    continue;
                }

                MatchCollection matches = propertyAssignRegex.Matches(line);
                foreach (Match match in matches)
                {
                    string propName = match.Groups[1].Value;
                    string val = match.Groups[2].Value.Trim();
                    if (!IsWhitelistedText(val))
                    {
                        violations.Add($"[{relativePath}:L{i + 1}] Assigning hardcoded {propName}=\"{val}\"");
                    }
                }

                if ((relativePath.StartsWith("Views", StringComparison.OrdinalIgnoreCase) ||
                     relativePath.StartsWith("ViewModels", StringComparison.OrdinalIgnoreCase) ||
                     relativePath.StartsWith("Hud", StringComparison.OrdinalIgnoreCase)) &&
                    japaneseTextRegex.IsMatch(line))
                {
                    MatchCollection stringMatches = Regex.Matches(line, @"""([^""]+)""");
                    foreach (Match m in stringMatches)
                    {
                        string strVal = m.Groups[1].Value.Trim();
                        if (japaneseTextRegex.IsMatch(strVal) && !IsWhitelistedText(strVal))
                        {
                            violations.Add($"[{relativePath}:L{i + 1}] Contains hardcoded Japanese string \"{strVal}\"");
                        }
                    }
                }
            }
        }

        Assert.True(violations.Count == 0,
            $"Found {violations.Count} hardcoded user-visible text occurrences across C# files:\n" +
            string.Join("\n", violations));
    }

    [Theory]
    [InlineData("Remove Widget", false)]
    [InlineData("ウィジェットを削除", false)]
    [InlineData("Delete Item", false)]
    [InlineData("NoraBar", true)]
    [InlineData("{Binding Title}", true)]
    [InlineData("\uE711", true)]
    [InlineData("12:34", true)]
    public void IsWhitelistedText_DetectsHardcodedEnglishAndJapaneseText(string text, bool expectedWhitelisted)
    {
        bool isWhitelisted = IsWhitelistedText(text);
        Assert.Equal(expectedWhitelisted, isWhitelisted);
    }

    [Theory]
    [InlineData("yyyy/MM/dd")]
    [InlineData("HH:mm:ss")]
    [InlineData("ddd, MMM dd")]
    [InlineData("MM-dd HH:mm")]
    [InlineData("yyyy年MM月dd日")]
    [InlineData("HH時mm分ss秒")]
    [InlineData(" yyyy / MM / dd ")]
    public void IsWhitelistedText_AcceptsCompleteDateTimeFormats(string text)
    {
        Assert.True(IsWhitelistedText(text));
    }

    [Theory]
    [InlineData("ddisabled")]
    [InlineData("MModern View")]
    [InlineData("MMMModern View")]
    [InlineData("yyyySettings")]
    [InlineData("HHHello")]
    [InlineData("yyyy-MM-dd Settings")]
    [InlineData("Date: yyyy/MM/dd")]
    [InlineData("HH:mm enabled")]
    [InlineData("prefix yyyy/MM/dd")]
    [InlineData("yyyy/MM/dd suffix")]
    [InlineData("yyyy/MM/dd!")]
    public void IsWhitelistedText_RejectsDateTokensEmbeddedInLocalizedText(string text)
    {
        Assert.False(IsWhitelistedText(text));
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("-", true)]
    [InlineData("...", true)]
    [InlineData("!", true)]
    [InlineData("Settings", false)]
    public void IsWhitelistedText_HandlesEmptyAndPunctuationValues(
        string text,
        bool expectedWhitelisted)
    {
        Assert.Equal(expectedWhitelisted, IsWhitelistedText(text));
    }

    private static bool IsWhitelistedText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;

        // マークアップ拡張 (Binding, StaticResource 等)
        if (text.StartsWith('{') && text.EndsWith('}')) return true;

        // アイコン文字・1文字記号（プライベート領域または記号/数値）
        if (text.Length == 1)
        {
            UnicodeCategory cat = char.GetUnicodeCategory(text[0]);
            if (cat is UnicodeCategory.PrivateUse or UnicodeCategory.MathSymbol or
                       UnicodeCategory.OtherSymbol or UnicodeCategory.OtherPunctuation or
                       UnicodeCategory.OpenPunctuation or UnicodeCategory.ClosePunctuation or
                       UnicodeCategory.DecimalDigitNumber or UnicodeCategory.Control)
            {
                return true;
            }
        }

        // アイコンフォントコード (e.g. \uE711)
        if (Regex.IsMatch(text, @"^\\u[0-9a-fA-F]{4}$")) return true;

        // 数値、単なる記号、タイムコード、パーセンテージ、日付/時刻フォーマット等
        if (Regex.IsMatch(text, @"^[\d\s:\.,\+\-\*\/\%\(\)\<\>\#\$]+$")) return true;
        if (Regex.IsMatch(text, @"\A(?:yyyy|yy|MMMM|MMM|MM|M|dddd|ddd|dd|d|HH|H|hh|h|mm|m|ss|s|tt|t|[\s\-:/.,年月日時分秒])+\z")) return true;

        // 固有名詞・ブランド名
        if (text is "NoraBar" or "LRCLIB" or "CSCore" or "MIT" or "MS-PL" or "Segoe Fluent Icons" or "Segoe UI") return true;

        return false;
    }
}

