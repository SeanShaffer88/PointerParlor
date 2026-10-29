using System.Xml.Linq;
using Microsoft.UI.Xaml.Markup;

namespace PointerParlor.Services;

/// <summary>
/// Smoke-test aid: compiled XAML reports only "XAML parsing failed". This re-parses a page's markup with
/// XamlReader, element by element, whose errors name the line and the problem, and logs what fails.
/// </summary>
public static class XamlDiagnostics
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    // Event handlers and x:Class only exist in compiled XAML; XamlReader rejects them.
    private static readonly HashSet<string> EventNames =
    [
        "Click", "BackRequested", "ItemInvoked", "Navigated", "DragOver", "Drop", "TextChanged",
        "ItemClick", "ValueChanged", "SelectionChanged",
    ];

    private static int _loaded;
    private static int _failed;

    public static void Diagnose(string xamlResourceName)
    {
        try
        {
            AppLog.Info($"XAML diagnostics: checking {xamlResourceName}");
            using var stream = typeof(XamlDiagnostics).Assembly.GetManifestResourceStream("Xaml." + xamlResourceName);
            if (stream is null)
            {
                AppLog.Error($"XAML diagnostics: {xamlResourceName} is not embedded");
                return;
            }
            var doc = XDocument.Load(stream, LoadOptions.SetLineInfo);
            foreach (var element in doc.Root!.Elements())
                Probe(element, depth: 0);
            AppLog.Info($"XAML diagnostics: {_loaded} elements loaded, {_failed} failed");
            ProbeWindow();
        }
        catch (Exception ex)
        {
            AppLog.Error("XAML diagnostics could not run", ex);
        }
    }

    /// <summary>Window-level settings can't go through XamlReader; build a bare window in code with the same ones.</summary>
    private static void ProbeWindow()
    {
        Step("new Window()", () => _ = new Microsoft.UI.Xaml.Window());
        Step("Window.Title", () => _ = new Microsoft.UI.Xaml.Window { Title = "PointerParlor" });
        Step("Window.SystemBackdrop = MicaBackdrop", () => _ = new Microsoft.UI.Xaml.Window { SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop() });
    }

    private static void Step(string name, Action action)
    {
        try
        {
            action();
            AppLog.Info($"XAML diagnostics: {name} OK");
        }
        catch (Exception ex)
        {
            AppLog.Error($"XAML diagnostics: {name} failed: {ex.GetType().Name}: {ex.Message.Replace(Environment.NewLine, " ")}");
        }
    }

    private static bool Probe(XElement element, int depth)
    {
        var line = ((System.Xml.IXmlLineInfo)element).LineNumber;
        try
        {
            var copy = new XElement(element);
            foreach (var e in copy.DescendantsAndSelf())
            {
                foreach (var attr in e.Attributes().Where(a => (a.Name.Namespace == X && a.Name.LocalName == "Class") || EventNames.Contains(a.Name.LocalName)).ToList())
                    attr.Remove();
            }
            if (copy.Name.LocalName.Contains('.'))
                return true; // property elements (Window.SystemBackdrop) can't load alone; their children are probed below
            copy.SetAttributeValue(XNamespace.Xmlns + "x", X.NamespaceName);
            XamlReader.Load(copy.ToString());
            _loaded++;
            return true;
        }
        catch (Exception ex)
        {
            _failed++;
            AppLog.Error($"XAML diagnostics: <{element.Name.LocalName}> at line {line} failed: {ex.Message.Replace(Environment.NewLine, " ")}");
        }
        finally
        {
            if (element.Name.LocalName.Contains('.') && depth < 8)
            {
                foreach (var child in element.Elements())
                    Probe(child, depth + 1);
            }
        }
        // Narrow down: report the innermost failing elements.
        if (depth < 8)
        {
            foreach (var child in element.Elements())
                Probe(child, depth + 1);
        }
        return false;
    }
}
