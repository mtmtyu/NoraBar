using System.IO;
using System.Xml.Linq;
using Xunit;

namespace NoraBar.Tests.Architecture;

public sealed class MainWindowNavigationAccessibilityTests
{
    private static readonly XNamespace PresentationNamespace =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    [Fact]
    public void RightRailNavigationButton_ProvidesVisibleAccessibleTooltipStates()
    {
        XDocument document = XDocument.Load(GetMainWindowXamlPath());
        XElement window = Assert.IsType<XElement>(document.Root);
        XElement button = Assert.Single(
            window.Descendants(PresentationNamespace + "Button"),
            element => string.Equals(
                (string?)element.Attribute("Style"),
                "{StaticResource RightRailNavigationButtonStyle}",
                StringComparison.Ordinal));

        Assert.Equal(
            "{Binding DisplayName}",
            (string?)button.Attribute("AutomationProperties.Name"));

        XElement buttonStyle = Assert.Single(
            window.Descendants(PresentationNamespace + "Style"),
            element => string.Equals(
                (string?)element.Attribute(XName.Get("Key", XamlNamespaceName)),
                "RightRailNavigationButtonStyle",
                StringComparison.Ordinal));
        AssertSetter(
            buttonStyle,
            null,
            "Background",
            "{StaticResource RightRailNavigationBackgroundBrush}");
        AssertSetter(
            buttonStyle,
            null,
            "BorderBrush",
            "{StaticResource RightRailNavigationBorderBrush}");
        AssertSetter(
            buttonStyle,
            null,
            "Foreground",
            "{StaticResource RightRailNavigationForegroundBrush}");

        XElement toolTip = Assert.Single(
            button.Descendants(PresentationNamespace + "ToolTip"));
        string toolTipContentBinding = Assert.IsType<XAttribute>(
            toolTip.Attribute("Content")).Value;
        Assert.Contains(
            "PlacementTarget.DataContext.DisplayName",
            toolTipContentBinding,
            StringComparison.Ordinal);
        Assert.Contains(
            "RelativeSource={RelativeSource Self}",
            toolTipContentBinding,
            StringComparison.Ordinal);
        Assert.Equal(
            "{StaticResource HudNavigationToolTipStyle}",
            (string?)toolTip.Attribute("Style"));

        XElement toolTipStyle = Assert.Single(
            window.Descendants(PresentationNamespace + "Style"),
            element => string.Equals(
                (string?)element.Attribute(XName.Get("Key", XamlNamespaceName)),
                "HudNavigationToolTipStyle",
                StringComparison.Ordinal));
        XElement contentPresenter = Assert.Single(
            toolTipStyle.Descendants(PresentationNamespace + "ContentPresenter"));
        Assert.Equal(
            "{TemplateBinding Content}",
            (string?)contentPresenter.Attribute("Content"));

        XElement template = Assert.Single(
            button.Descendants(PresentationNamespace + "ControlTemplate"));
        XElement icon = Assert.Single(
            template.Descendants(PresentationNamespace + "TextBlock"));
        Assert.Equal("16", (string?)icon.Attribute("FontSize"));

        XElement hoverTrigger = Assert.Single(
            template.Descendants(PresentationNamespace + "Trigger"),
            trigger => string.Equals(
                (string?)trigger.Attribute("Property"),
                "IsMouseOver",
                StringComparison.Ordinal));
        AssertSetter(
            hoverTrigger,
            "IconBackground",
            "Background",
            "{StaticResource RightRailNavigationHoverBackgroundBrush}");
        AssertSetter(
            hoverTrigger,
            "IconBackground",
            "BorderBrush",
            "{StaticResource RightRailNavigationHoverBorderBrush}");
        AssertSetter(hoverTrigger, null, "Foreground", "White");

        XElement keyboardFocusTrigger = Assert.Single(
            template.Descendants(PresentationNamespace + "Trigger"),
            trigger => string.Equals(
                (string?)trigger.Attribute("Property"),
                "IsKeyboardFocused",
                StringComparison.Ordinal));
        AssertSetter(keyboardFocusTrigger, "IconBackground", "BorderBrush", "White");

        XElement currentTrigger = Assert.Single(
            template.Descendants(PresentationNamespace + "DataTrigger"),
            trigger => string.Equals(
                (string?)trigger.Attribute("Binding"),
                "{Binding IsCurrent}",
                StringComparison.Ordinal));
        AssertSetter(
            currentTrigger,
            "IconBackground",
            "Background",
            "{StaticResource RightRailNavigationCurrentBackgroundBrush}");
        AssertSetter(
            currentTrigger,
            "IconBackground",
            "BorderBrush",
            "{StaticResource RightRailNavigationCurrentBorderBrush}");
        AssertSetter(currentTrigger, null, "Foreground", "White");
    }

    private const string XamlNamespaceName =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static void AssertSetter(
        XElement setterOwner,
        string? targetName,
        string property,
        string value)
    {
        Assert.Contains(
            setterOwner.Elements(PresentationNamespace + "Setter"),
            setter => string.Equals(
                    (string?)setter.Attribute("TargetName"),
                    targetName,
                    StringComparison.Ordinal)
                && string.Equals(
                    (string?)setter.Attribute("Property"),
                    property,
                    StringComparison.Ordinal)
                && string.Equals(
                    (string?)setter.Attribute("Value"),
                    value,
                    StringComparison.Ordinal));
    }

    private static string GetMainWindowXamlPath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "NoraBar.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, "NoraBar", "MainWindow.xaml");
    }
}
