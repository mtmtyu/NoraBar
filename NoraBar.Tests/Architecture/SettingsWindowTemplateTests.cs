using System.IO;
using System.Xml.Linq;
using Xunit;

namespace NoraBar.Tests.Architecture;

public sealed class SettingsWindowTemplateTests
{
    private static readonly XNamespace PresentationNamespace =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Fact]
    public void TextBoxTemplate_ForwardsConfiguredScrollbarVisibility()
    {
        XDocument document = XDocument.Load(GetSettingsWindowXamlPath());
        XElement window = Assert.IsType<XElement>(document.Root);
        XElement textBoxStyle = Assert.Single(
            window.Descendants(PresentationNamespace + "Style"),
            element => string.Equals(
                    (string?)element.Attribute("TargetType"),
                    "TextBox",
                    StringComparison.Ordinal)
                && element.Descendants(PresentationNamespace + "ControlTemplate").Any());
        XElement contentHost = Assert.Single(
            textBoxStyle.Descendants(PresentationNamespace + "ScrollViewer"),
            element => string.Equals(
                (string?)element.Attribute(XName.Get("Name", XamlNamespaceName)),
                "PART_ContentHost",
                StringComparison.Ordinal));

        Assert.Equal(
            "{TemplateBinding ScrollViewer.HorizontalScrollBarVisibility}",
            (string?)contentHost.Attribute("HorizontalScrollBarVisibility"));
        Assert.Equal(
            "{TemplateBinding ScrollViewer.VerticalScrollBarVisibility}",
            (string?)contentHost.Attribute("VerticalScrollBarVisibility"));
    }

    [Fact]
    public void WorldClockEditor_UsesLocalizedTooltipsAndAutomationNames()
    {
        XDocument document = XDocument.Load(GetSettingsWindowXamlPath());
        XElement window = Assert.IsType<XElement>(document.Root);
        XElement editor = Assert.Single(
            window.Descendants(PresentationNamespace + "ItemsControl"),
            element => string.Equals(
                (string?)element.Attribute(XName.Get("Name", XamlNamespaceName)),
                "WorldClocksItemsControl",
                StringComparison.Ordinal));
        XElement label = Assert.Single(editor.Descendants(PresentationNamespace + "TextBox"));
        Assert.Equal(
            "{Binding DataContext.WorldClockLabelHintText, RelativeSource={RelativeSource AncestorType=Window}}",
            (string?)label.Attribute("ToolTip"));

        AssertLocalizedButton("↑", "MoveUpText");
        AssertLocalizedButton("↓", "MoveDownText");
        AssertLocalizedButton("✕", "RemoveWorldClockText");

        void AssertLocalizedButton(string content, string propertyName)
        {
            XElement button = Assert.Single(
                editor.Descendants(PresentationNamespace + "Button"),
                element => string.Equals(
                    (string?)element.Attribute("Content"),
                    content,
                    StringComparison.Ordinal));
            string expected =
                $"{{Binding DataContext.{propertyName}, RelativeSource={{RelativeSource AncestorType=Window}}}}";
            Assert.Equal(expected, (string?)button.Attribute("ToolTip"));
            Assert.Equal(
                expected,
                (string?)button.Attributes().Single(attribute =>
                    string.Equals(
                        attribute.Name.LocalName,
                        "AutomationProperties.Name",
                        StringComparison.Ordinal)));
        }
    }

    private const string XamlNamespaceName =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static string GetSettingsWindowXamlPath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "NoraBar.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, "NoraBar", "Views", "SettingsWindow.xaml");
    }
}
