#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    public static class MCPUIToolkitUxmlAuditCommands
    {
        public static object AuditUxmlLayout(Dictionary<string, object> args)
        {
            args = args ?? new Dictionary<string, object>();
            var options = MCPUIToolkitAuditOptions.FromArguments(args);
            var report = MCPUxmlLayoutAuditor.Audit(
                MCPUIToolkitAuditUtility.GetStringList(args, "paths"),
                MCPUIToolkitAuditUtility.GetBool(args, "includeSuppressed"),
                Mathf.Clamp(MCPUIToolkitAuditUtility.GetInt(args, "maxIssues", 200), 1, 5000),
                options);

            if (MCPUIToolkitAuditUtility.GetBool(args, "logWarnings"))
                MCPUxmlLayoutAuditConsoleReporter.Log(report, false);

            var result = report.ToDictionary();
            result["scope"] = options.ToDictionary();
            result["automaticAudit"] =
                MCPUIToolkitAutomaticAuditCoordinator.GetStatus(".uxml");
            if (MCPUIToolkitAuditUtility.GetBool(args, "runSelfTests"))
            {
                var selfTests = MCPUxmlLayoutAuditor.RunSelfTests();
                result["selfTests"] = selfTests;
                object passed;
                if (selfTests.TryGetValue("passed", out passed) &&
                    passed is bool && !(bool)passed)
                    result["success"] = false;
            }

            return result;
        }

        [MenuItem("Tools/UI Toolkit/Audit UXML Layout Contracts")]
        private static void AuditAllFromMenu()
        {
            var options = MCPUIToolkitAuditOptions.FromProjectSettings(
                MCPUIToolkitAuditProjectSettings.Load());
            var report = MCPUxmlLayoutAuditor.Audit(
                Array.Empty<string>(), true, 5000, options);
            MCPUxmlLayoutAuditConsoleReporter.Log(report, false);
        }
    }

    internal static class MCPUxmlLayoutAuditConsoleReporter
    {
        internal static void Log(MCPUxmlLayoutAuditReport report, bool automatic)
        {
            foreach (var error in report.Errors)
            {
                Debug.LogError($"[UXML Layout Audit] {error}");
            }

            foreach (var issue in report.Issues.Where(issue => issue.Suppressed == false))
            {
                var context = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(issue.AssetPath);
                Debug.LogWarning(
                    $"[UXML Layout Audit] {issue.AssetPath}:{issue.Line} {issue.Element}: {issue.Message}",
                    context);
            }

            if (automatic == false || report.Errors.Count > 0 || report.WarningCount > 0)
            {
                var mode = automatic ? "automatic import audit" : "requested audit";
                Debug.Log(
                    $"[UXML Layout Audit] {mode}: scanned={report.ScannedUxmlCount}, " +
                    $"warnings={report.WarningCount}, suppressed={report.SuppressedCount}, " +
                    $"errors={report.Errors.Count}, passed={report.Passed}.");
            }
        }
    }

    internal static class MCPUxmlLayoutAuditor
    {
        internal const string SUPPRESSION_MARKER = "uxml-layout-audit: allow-manual-center";

        private const float CENTER_EPSILON = 0.01f;

        private static readonly Regex styleDeclarationRegex =
            new Regex(@"(?:^|;)\s*(?<name>[-A-Za-z0-9]+)\s*:\s*(?<value>[^;]+)",
                RegexOptions.Compiled);

        private static readonly Regex pixelValueRegex =
            new Regex(@"^(?<value>-?(?:\d+(?:\.\d+)?|\.\d+))px$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex suppressionRegex =
            new Regex(@"^\s*uxml-layout-audit:\s*allow-manual-center\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex ussCommentRegex =
            new Regex(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex ussRuleRegex =
            new Regex(@"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}",
                RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex classTokenRegex =
            new Regex(@"(?<![A-Za-z0-9_-])\.(?<token>[A-Za-z_][A-Za-z0-9_-]*)",
                RegexOptions.Compiled);

        private static readonly Regex idTokenRegex =
            new Regex(@"(?<![A-Za-z0-9_-])#(?<token>[A-Za-z_][A-Za-z0-9_-]*)",
                RegexOptions.Compiled);

        internal static MCPUxmlLayoutAuditReport Audit(IEnumerable<string> requestedPaths,
            bool includeSuppressed, int maxIssues, MCPUIToolkitAuditOptions options)
        {
            options = options ?? MCPUIToolkitAuditOptions.FromArguments(
                new Dictionary<string, object>());
            var report = new MCPUxmlLayoutAuditReport(maxIssues);
            var requestedPathList = (requestedPaths ?? Array.Empty<string>())
                .Where(path => string.IsNullOrWhiteSpace(path) == false).ToList();
            var requested = NormalizeRequestedPaths(requestedPathList, report.Errors);
            var allUxmlPaths = MCPUIToolkitAuditUtility.FindAssetFiles(".uxml", options)
                .Concat(requested)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
            var targetPaths = requestedPathList.Count == 0 ? allUxmlPaths : requested;
            var boxContracts = BuildBoxContractIndex(report, options);

            report.ScannedUxmlCount = targetPaths.Count;
            report.IndexedUxmlCount = allUxmlPaths.Count;

            foreach (var path in targetPaths)
            {
                try
                {
                    AuditText(path,
                        File.ReadAllText(MCPUIToolkitAuditUtility.ToFullPath(path)),
                        boxContracts, report,
                        includeSuppressed);
                }
                catch (Exception exception)
                {
                    report.Errors.Add($"Failed to audit '{path}': {exception.Message}");
                }
            }

            report.SortIssues();
            return report;
        }

        internal static Dictionary<string, object> RunSelfTests()
        {
            const string suspiciousElement =
                "<ui:VisualElement name=\"Navigation\" style=\"position: absolute; left: 309px; " +
                "bottom: 18px; width: 189px; height: 36px; flex-direction: row; align-items: center; " +
                "justify-content: center;\"><ui:Button style=\"width: 24px; height: 33px;\"/></ui:VisualElement>";

            var cases = new List<Dictionary<string, object>>();
            var suspicious = AuditFixture(suspiciousElement);
            AddSelfTestCase(cases, "manual fixed centering box warns",
                suspicious.WarningCount == 1 &&
                suspicious.Issues.Single().Kind == "manual-centered-layout-box");
            AddSelfTestCase(cases, "warning includes redundant fixed height",
                suspicious.Issues.Single().FixedProperties.SequenceEqual(
                    new[] { "left", "width", "height" }));

            var anchored = AuditFixture(
                "<ui:VisualElement name=\"Navigation\" style=\"position: absolute; left: 0; right: 0; " +
                "bottom: 18px; flex-direction: row; align-items: center; justify-content: center;\">" +
                "<ui:Button style=\"width: 24px; height: 33px;\"/></ui:VisualElement>");
            AddSelfTestCase(cases, "owner-edge anchors pass", anchored.WarningCount == 0);

            var offCenter = AuditFixture(
                suspiciousElement.Replace("left: 309px", "left: 300px"));
            AddSelfTestCase(cases, "non-centering fixed region passes", offCenter.WarningCount == 0);

            var visualInline = AuditFixture(
                suspiciousElement.Replace("position: absolute;",
                    "position: absolute; background-color: rgb(1, 2, 3);"));
            AddSelfTestCase(cases, "inline visual region passes", visualInline.WarningCount == 0);

            var visualClassIndex = new UxmlBoxContractIndex();
            IndexStyleSheetText(".intentional-region { background-image: url(\"Panel.png\"); }",
                visualClassIndex);
            var visualClass = AuditFixture(
                suspiciousElement.Replace("name=\"Navigation\"",
                    "name=\"Navigation\" class=\"intentional-region\""),
                boxContracts: visualClassIndex);
            AddSelfTestCase(cases, "USS visual contract passes", visualClass.WarningCount == 0);

            var noParentWidth = AuditFixture(suspiciousElement, "height: 492px;");
            AddSelfTestCase(cases, "unknown owner width passes", noParentWidth.WarningCount == 0);

            var control = AuditFixture(
                "<ui:Button name=\"Navigation\" style=\"position: absolute; left: 309px; width: 189px; " +
                "height: 36px; flex-direction: row; justify-content: center;\"><ui:Label/></ui:Button>");
            AddSelfTestCase(cases, "interactive control passes", control.WarningCount == 0);

            var suppressed = AuditFixture(
                $"<!-- {SUPPRESSION_MARKER} fixture owns an intentional interaction region -->" +
                suspiciousElement, includeSuppressed: true);
            AddSelfTestCase(cases, "reasoned suppression is retained",
                suppressed.WarningCount == 0 &&
                suppressed.SuppressedCount == 1 &&
                suppressed.Issues.Single().Suppressed);

            return new Dictionary<string, object>
            {
                { "passed", cases.All(testCase => (bool)testCase["passed"]) },
                { "cases", cases }
            };
        }

        private static MCPUxmlLayoutAuditReport AuditFixture(string element,
            string parentStyle = "width: 807px; height: 492px;", bool includeSuppressed = false,
            UxmlBoxContractIndex boxContracts = null)
        {
            var text =
                "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" +
                $"<ui:VisualElement style=\"{parentStyle}\">{element}</ui:VisualElement>" +
                "</ui:UXML>";
            var report = new MCPUxmlLayoutAuditReport(100)
            {
                ScannedUxmlCount = 1,
                IndexedUxmlCount = 1
            };
            AuditText("Assets/__UxmlLayoutAuditSelfTest.uxml", text,
                boxContracts ?? new UxmlBoxContractIndex(), report, includeSuppressed);
            report.SortIssues();
            return report;
        }

        private static void AuditText(string assetPath, string text, UxmlBoxContractIndex boxContracts,
            MCPUxmlLayoutAuditReport report, bool includeSuppressed)
        {
            var document = XDocument.Parse(text, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            foreach (var element in document.Descendants())
            {
                AuditElement(assetPath, element, boxContracts, report, includeSuppressed);
            }
        }

        private static void AuditElement(string assetPath, XElement element,
            UxmlBoxContractIndex boxContracts, MCPUxmlLayoutAuditReport report,
            bool includeSuppressed)
        {
            var parent = element.Parent as XElement;
            if (element.Name.LocalName != "VisualElement" ||
                parent == null ||
                HasVisualChildren(element) == false)
            {
                return;
            }

            var style = ParseStyle(AttributeValue(element, "style"));
            var parentStyle = ParseStyle(AttributeValue(parent, "style"));
            if (StyleValue(style, "position") != "absolute" ||
                StyleValue(style, "flex-direction") != "row" ||
                StyleValue(style, "justify-content") != "center" ||
                style.ContainsKey("right") ||
                TryGetPixels(style, "left", out var left) == false ||
                TryGetPixels(style, "width", out var width) == false ||
                TryGetPixels(parentStyle, "width", out var parentWidth) == false ||
                left < 0 ||
                width <= 0 ||
                parentWidth <= 0 ||
                Math.Abs(left * 2 + width - parentWidth) > CENTER_EPSILON ||
                HasBoxContract(element, style, boxContracts))
            {
                return;
            }

            var name = AttributeValue(element, "name");
            var fixedProperties = new List<string> { "left", "width" };
            var heightClause = "";
            if (TryGetPixels(style, "height", out var height))
            {
                fixedProperties.Add("height");
                heightClause =
                    $" The fixed height ({FormatPixels(height)}) also has no authored visual, clipping, " +
                    "constrained-region, or explicit interaction contract; let in-flow children determine it.";
            }

            var suppressionReason = GetSuppressionReason(element);
            var elementLabel = string.IsNullOrWhiteSpace(name)
                ? "<VisualElement>"
                : $"#{name}";
            var issue = new MCPUxmlLayoutAuditIssue
            {
                AssetPath = assetPath,
                Line = GetLineNumber(element),
                Element = elementLabel,
                ElementName = name,
                Kind = "manual-centered-layout-box",
                Axis = "horizontal",
                FixedProperties = fixedProperties,
                ParentSize = parentWidth,
                Offset = left,
                Size = width,
                Suppressed = string.IsNullOrWhiteSpace(suppressionReason) == false,
                SuppressionReason = suppressionReason,
                Message =
                    $"Layout-only {elementLabel} is manually centered with left {FormatPixels(left)} plus " +
                    $"width {FormatPixels(width)} inside a {FormatPixels(parentWidth)} owner while also " +
                    "centering its children. These values only create an empty centering box; anchor both " +
                    "horizontal owner edges and keep justify-content: center." + heightClause
            };
            report.Record(issue, includeSuppressed);
        }

        private static bool HasVisualChildren(XElement element)
        {
            return element.Elements().Any(child =>
                child.Name.LocalName != "Bindings" &&
                child.Name.LocalName != "Style" &&
                child.Name.LocalName != "Template" &&
                child.Name.LocalName != "AttributeOverrides");
        }

        private static bool HasBoxContract(XElement element, IReadOnlyDictionary<string, string> style,
            UxmlBoxContractIndex boxContracts)
        {
            if (style.Any(property => IsBoxContractProperty(property.Key, property.Value)))
            {
                return true;
            }

            var name = AttributeValue(element, "name");
            if (string.IsNullOrWhiteSpace(name) == false && boxContracts.Ids.Contains(name))
            {
                return true;
            }

            foreach (var className in SplitWhitespace(AttributeValue(element, "class")))
            {
                if (boxContracts.Classes.Contains(className))
                {
                    return true;
                }
            }

            if (element.Elements().Any(child => child.Name.LocalName == "Bindings"))
            {
                return true;
            }

            if (string.Equals(AttributeValue(element, "focusable"), "true",
                    StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(AttributeValue(element, "tabindex")) == false ||
                string.IsNullOrWhiteSpace(AttributeValue(element, "tooltip")) == false ||
                string.Equals(AttributeValue(element, "picking-mode"), "Position",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static UxmlBoxContractIndex BuildBoxContractIndex(
            MCPUxmlLayoutAuditReport report, MCPUIToolkitAuditOptions options)
        {
            var index = new UxmlBoxContractIndex();
            foreach (var path in MCPUIToolkitAuditUtility.FindAssetFiles(".uss", options))
            {
                try
                {
                    IndexStyleSheetText(
                        File.ReadAllText(MCPUIToolkitAuditUtility.ToFullPath(path)), index);
                    report.IndexedStyleSheetCount++;
                }
                catch (Exception exception)
                {
                    report.Errors.Add($"Failed to index USS box contracts in '{path}': {exception.Message}");
                }
            }

            return index;
        }

        private static void IndexStyleSheetText(string text, UxmlBoxContractIndex index)
        {
            var sanitized = ussCommentRegex.Replace(text ?? "", "");
            foreach (Match rule in ussRuleRegex.Matches(sanitized))
            {
                var declarations = ParseStyle(rule.Groups["body"].Value);
                if (declarations.Any(property => IsBoxContractProperty(property.Key, property.Value)) == false)
                {
                    continue;
                }

                var selector = rule.Groups["selector"].Value;
                foreach (Match match in classTokenRegex.Matches(selector))
                {
                    index.Classes.Add(match.Groups["token"].Value);
                }

                foreach (Match match in idTokenRegex.Matches(selector))
                {
                    index.Ids.Add(match.Groups["token"].Value);
                }
            }
        }

        private static bool IsBoxContractProperty(string property, string value)
        {
            property = (property ?? "").Trim().ToLowerInvariant();
            value = (value ?? "").Trim();
            if (property.StartsWith("background-", StringComparison.Ordinal) ||
                property.StartsWith("border-", StringComparison.Ordinal) ||
                property.StartsWith("-unity-background", StringComparison.Ordinal) ||
                property.StartsWith("padding-", StringComparison.Ordinal) ||
                property == "padding" ||
                property == "opacity" ||
                property == "visibility" ||
                property == "scale" ||
                property == "rotate" ||
                property == "translate" ||
                property == "transform-origin" ||
                property == "min-width" ||
                property == "max-width" ||
                property == "min-height" ||
                property == "max-height")
            {
                return true;
            }

            return property == "overflow" &&
                   string.Equals(value, "visible", StringComparison.OrdinalIgnoreCase) == false;
        }

        private static Dictionary<string, string> ParseStyle(string style)
        {
            var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match declaration in styleDeclarationRegex.Matches(style ?? ""))
            {
                properties[declaration.Groups["name"].Value.Trim()] =
                    declaration.Groups["value"].Value.Trim();
            }

            return properties;
        }

        private static string StyleValue(IReadOnlyDictionary<string, string> style, string property)
        {
            return style.TryGetValue(property, out var value)
                ? value.Trim().ToLowerInvariant()
                : "";
        }

        private static bool TryGetPixels(IReadOnlyDictionary<string, string> style, string property,
            out float value)
        {
            value = 0;
            if (style.TryGetValue(property, out var rawValue) == false)
            {
                return false;
            }

            var match = pixelValueRegex.Match(rawValue.Trim());
            return match.Success &&
                   float.TryParse(match.Groups["value"].Value, NumberStyles.Float,
                       CultureInfo.InvariantCulture, out value);
        }

        private static string FormatPixels(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture) + "px";
        }

        private static string GetSuppressionReason(XElement element)
        {
            var previous = element.NodesBeforeSelf().Reverse().FirstOrDefault(node =>
                !(node is XText) || string.IsNullOrWhiteSpace(((XText)node).Value) == false);
            var comment = previous as XComment;
            if (comment == null)
            {
                return "";
            }

            var match = suppressionRegex.Match(comment.Value);
            return match.Success ? match.Groups["reason"].Value.Trim() : "";
        }

        private static string AttributeValue(XElement element, string attributeName)
        {
            return element.Attributes().FirstOrDefault(attribute =>
                    string.Equals(attribute.Name.LocalName, attributeName,
                        StringComparison.OrdinalIgnoreCase))
                ?.Value ?? "";
        }

        private static int GetLineNumber(XObject value)
        {
            return value is IXmlLineInfo lineInfo && lineInfo.HasLineInfo()
                ? lineInfo.LineNumber
                : 1;
        }

        private static List<string> NormalizeRequestedPaths(IEnumerable<string> requestedPaths,
            ICollection<string> errors)
        {
            var requested = (requestedPaths ?? Array.Empty<string>())
                .Where(path => string.IsNullOrWhiteSpace(path) == false)
                .Select(MCPUIToolkitAuditUtility.NormalizeAssetPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
            foreach (var path in requested)
            {
                if (path.StartsWith("Assets/", StringComparison.Ordinal) == false ||
                    path.EndsWith(".uxml", StringComparison.OrdinalIgnoreCase) == false)
                {
                    errors.Add($"UXML layout audit path must be an Assets-relative .uxml path: {path}");
                }
                else if (File.Exists(MCPUIToolkitAuditUtility.ToFullPath(path)) == false)
                {
                    errors.Add($"UXML asset does not exist: {path}");
                }
            }

            return requested
                .Where(path => File.Exists(MCPUIToolkitAuditUtility.ToFullPath(path)))
                .ToList();
        }

        private static IEnumerable<string> SplitWhitespace(string value)
        {
            return (value ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        }

        private static void AddSelfTestCase(ICollection<Dictionary<string, object>> cases, string name,
            bool passed)
        {
            cases.Add(new Dictionary<string, object>
            {
                { "name", name },
                { "passed", passed }
            });
        }

        private sealed class UxmlBoxContractIndex
        {
            public readonly HashSet<string> Classes =
                new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> Ids =
                new HashSet<string>(StringComparer.Ordinal);
        }
    }

    internal sealed class MCPUxmlLayoutAuditReport
    {
        private readonly int maxIssues;
        private int activeIssueCount;
        private int suppressedIssueCount;

        public readonly List<MCPUxmlLayoutAuditIssue> Issues =
            new List<MCPUxmlLayoutAuditIssue>();
        public readonly List<string> Errors = new List<string>();
        public int ScannedUxmlCount;
        public int IndexedUxmlCount;
        public int IndexedStyleSheetCount;

        public MCPUxmlLayoutAuditReport(int maxIssues)
        {
            this.maxIssues = maxIssues;
        }

        public int WarningCount => activeIssueCount;
        public int SuppressedCount => suppressedIssueCount;
        public bool Passed => Errors.Count == 0 && WarningCount == 0;

        public void Record(MCPUxmlLayoutAuditIssue issue, bool includeSuppressed)
        {
            if (issue.Suppressed)
            {
                suppressedIssueCount++;
                if (includeSuppressed == false)
                {
                    return;
                }
            }
            else
            {
                activeIssueCount++;
            }

            if (Issues.Count < maxIssues)
            {
                Issues.Add(issue);
            }
        }

        public void SortIssues()
        {
            Issues.Sort((left, right) =>
            {
                var pathComparison = string.Compare(left.AssetPath, right.AssetPath,
                    StringComparison.Ordinal);
                if (pathComparison != 0)
                {
                    return pathComparison;
                }

                return left.Line.CompareTo(right.Line);
            });
        }

        public Dictionary<string, object> ToDictionary()
        {
            var returnedActive = Issues.Count(issue => issue.Suppressed == false);
            var returnedSuppressed = Issues.Count(issue => issue.Suppressed);
            return new Dictionary<string, object>
            {
                { "success", Errors.Count == 0 },
                { "passed", Passed },
                { "scannedUxmlFiles", ScannedUxmlCount },
                { "indexedUxmlFiles", IndexedUxmlCount },
                { "indexedStyleSheets", IndexedStyleSheetCount },
                { "warningCount", WarningCount },
                { "suppressedCount", SuppressedCount },
                { "truncated", WarningCount > returnedActive ||
                               returnedSuppressed > 0 && SuppressedCount > returnedSuppressed },
                { "issues", Issues.Select(issue => issue.ToDictionary()).ToList() },
                { "errors", Errors.ToList() },
                { "suppressionSyntax",
                    $"<!-- {MCPUxmlLayoutAuditor.SUPPRESSION_MARKER} <reason> -->" }
            };
        }
    }

    internal sealed class MCPUxmlLayoutAuditIssue
    {
        public string AssetPath;
        public int Line;
        public string Element;
        public string ElementName;
        public string Kind;
        public string Axis;
        public List<string> FixedProperties = new List<string>();
        public float ParentSize;
        public float Offset;
        public float Size;
        public bool Suppressed;
        public string SuppressionReason;
        public string Message;

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "assetPath", AssetPath },
                { "line", Line },
                { "element", Element },
                { "elementName", ElementName ?? "" },
                { "kind", Kind },
                { "axis", Axis },
                { "fixedProperties", FixedProperties.ToList() },
                { "parentSize", ParentSize },
                { "offset", Offset },
                { "size", Size },
                { "suppressed", Suppressed },
                { "suppressionReason", SuppressionReason ?? "" },
                { "message", Message }
            };
        }
    }
}
#endif
