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
                        if (attrName is "Text" or "Content" or "Title" or "Header")
                        {
                            string val = attr.Value.Trim();
                            if (IsWhitelistedText(val)) continue;

                            violations.Add($"[{relativePath}] <{element.Name.LocalName}> has hardcoded {attrName}=\"{val}\"");
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
                       UnicodeCategory.DecimalDigitNumber)
            {
                return true;
            }
        }

        // 数値、単なる記号、タイムコード、パーセンテージ等
        if (Regex.IsMatch(text, @"^[\d\s:\.,\+\-\*\/\%\(\)\<\>\#\$]+$")) return true;

        // 固有名詞・ブランド名
        if (text is "NoraBar" or "LRCLIB" or "CSCore" or "MIT" or "MS-PL") return true;

        return false;
    }
}
