#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
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
        internal const string REPEATED_INLINE_SUPPRESSION_MARKER =
            "uxml-layout-audit: allow-repeated-inline";
        internal const string REDUNDANT_INLINE_SUPPRESSION_MARKER =
            "uxml-layout-audit: allow-redundant-inline";
        internal const string INERT_TEXT_STRETCH_SUPPRESSION_MARKER =
            "uxml-layout-audit: allow-inert-text-stretch";

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

        private static readonly Regex repeatedInlineSuppressionRegex =
            new Regex(@"^\s*uxml-layout-audit:\s*allow-repeated-inline\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex redundantInlineSuppressionRegex =
            new Regex(@"^\s*uxml-layout-audit:\s*allow-redundant-inline\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex inertTextStretchSuppressionRegex =
            new Regex(@"^\s*uxml-layout-audit:\s*allow-inert-text-stretch\s+(?<reason>.+?)\s*$",
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

        private static readonly Dictionary<string, IReadOnlyList<string>>
            implicitElementClassesByType =
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        private static readonly HashSet<string> variantLayoutProperties =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "position",
                "left",
                "right",
                "top",
                "bottom",
                "width",
                "height",
                "min-width",
                "max-width",
                "min-height",
                "max-height",
                "flex",
                "flex-basis",
                "flex-grow",
                "flex-shrink",
                "flex-direction",
                "flex-wrap",
                "align-content",
                "align-items",
                "align-self",
                "justify-content",
                "margin",
                "margin-left",
                "margin-right",
                "margin-top",
                "margin-bottom",
                "padding",
                "padding-left",
                "padding-right",
                "padding-top",
                "padding-bottom",
                "row-gap",
                "column-gap"
            };

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
            var layoutContracts = BuildLayoutContractIndex(report, options, allUxmlPaths);

            report.ScannedUxmlCount = targetPaths.Count;
            report.IndexedUxmlCount = allUxmlPaths.Count;

            foreach (var path in targetPaths)
            {
                try
                {
                    AuditText(path,
                        File.ReadAllText(MCPUIToolkitAuditUtility.ToFullPath(path)),
                        layoutContracts, report,
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

            var visualClassIndex = new UxmlLayoutContractIndex();
            IndexStyleSheetText(".intentional-region { background-image: url(\"Panel.png\"); }",
                visualClassIndex);
            var visualClass = AuditFixture(
                suspiciousElement.Replace("name=\"Navigation\"",
                    "name=\"Navigation\" class=\"intentional-region\""),
                layoutContracts: visualClassIndex);
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

            var variantIndex = new UxmlLayoutContractIndex();
            IndexStyleSheetText(
                ".stage-label { position: absolute; } " +
                ".stage-label-above { top: -18px; }",
                variantIndex);
            const string repeatedInlineVariantElements =
                "<ui:VisualElement class=\"stage-label stage-label-above\"/>" +
                "<ui:VisualElement name=\"Stage2Label\" class=\"stage-label\" " +
                "style=\"top: 57px; background-image: url(&quot;Stage2.png&quot;);\"/>" +
                "<ui:VisualElement name=\"Stage3Label\" class=\"stage-label\" " +
                "style=\"background-image: url(&quot;Stage3.png&quot;); top: 57px;\"/>";
            var repeatedInlineVariant = AuditFixture(repeatedInlineVariantElements,
                layoutContracts: variantIndex);
            AddSelfTestCase(cases, "repeated inline authored variant warns",
                repeatedInlineVariant.WarningCount == 1 &&
                repeatedInlineVariant.Issues.Single().Kind ==
                "repeated-inline-layout-variant" &&
                repeatedInlineVariant.Issues.Single().AuthoredUsageCount == 2);

            var distinctInlineVariants = AuditFixture(
                repeatedInlineVariantElements.Replace(
                    "background-image: url(&quot;Stage3.png&quot;); top: 57px;",
                    "background-image: url(&quot;Stage3.png&quot;); top: 60px;"),
                layoutContracts: variantIndex);
            AddSelfTestCase(cases, "distinct inline variants pass",
                distinctInlineVariants.WarningCount == 0);

            var prefixOnlyIndex = new UxmlLayoutContractIndex();
            IndexStyleSheetText(".stage-label-glyph { top: -18px; }", prefixOnlyIndex);
            var unprovenVariant = AuditFixture(
                "<ui:VisualElement class=\"stage-label-glyph\"/>" +
                "<ui:VisualElement class=\"stage-label\" style=\"top: 57px;\"/>" +
                "<ui:VisualElement class=\"stage-label\" style=\"top: 57px;\"/>",
                layoutContracts: prefixOnlyIndex);
            AddSelfTestCase(cases, "class-name prefix without co-usage passes",
                unprovenVariant.WarningCount == 0);

            var suppressedInlineVariant = AuditFixture(
                "<ui:VisualElement class=\"stage-label stage-label-above\"/>" +
                $"<!-- {REPEATED_INLINE_SUPPRESSION_MARKER} fixture mirrors runtime layout -->" +
                "<ui:VisualElement class=\"stage-label\" style=\"top: 57px;\"/>" +
                "<ui:VisualElement class=\"stage-label\" style=\"top: 57px;\"/>",
                includeSuppressed: true, layoutContracts: variantIndex);
            AddSelfTestCase(cases, "reasoned repeated-inline suppression is retained",
                suppressedInlineVariant.WarningCount == 0 &&
                suppressedInlineVariant.SuppressedCount == 1 &&
                suppressedInlineVariant.Issues.Single().Suppressed);

            var inlineStyleIndex = new UxmlInlineStyleContractIndex();
            IndexInlineStyleSheetText("Assets/Basics.uss",
                ".unity-base-field { overflow: visible; margin-left: 0; }",
                inlineStyleIndex);
            var redundantImplicitInline = AuditFixture(
                "<ui:TextField name=\"Field\" style=\"overflow: visible;\"/>",
                inlineStyleContracts: inlineStyleIndex);
            AddSelfTestCase(cases, "inline declaration duplicating implicit USS class warns",
                redundantImplicitInline.WarningCount == 1 &&
                redundantImplicitInline.Issues.Single().Kind ==
                "redundant-inline-declaration" &&
                redundantImplicitInline.Issues.Single().FixedProperties
                    .SequenceEqual(new[] { "overflow" }));

            var intentionalInlineOverride = AuditFixture(
                "<ui:TextField name=\"Field\" style=\"overflow: hidden;\"/>",
                inlineStyleContracts: inlineStyleIndex);
            AddSelfTestCase(cases, "inline declaration overriding USS default passes",
                intentionalInlineOverride.WarningCount == 0);

            var suppressedRedundantInline = AuditFixture(
                $"<!-- {REDUNDANT_INLINE_SUPPRESSION_MARKER} fixture documents generated output -->" +
                "<ui:TextField name=\"Field\" style=\"overflow: visible;\"/>",
                includeSuppressed: true, inlineStyleContracts: inlineStyleIndex);
            AddSelfTestCase(cases, "reasoned redundant-inline suppression is retained",
                suppressedRedundantInline.WarningCount == 0 &&
                suppressedRedundantInline.SuppressedCount == 1 &&
                suppressedRedundantInline.Issues.Single().Suppressed);

            const string inertStretch =
                "<ui:VisualElement style=\"align-items: center;\">" +
                "<ui:Label name=\"Title\" style=\"align-self: stretch; margin-left: 18px; " +
                "margin-right: 18px; -unity-text-align: middle-center;\"/>" +
                "</ui:VisualElement>";
            var inertTextStretch = AuditFixture(inertStretch);
            AddSelfTestCase(cases, "centered intrinsic label stretch warns",
                inertTextStretch.WarningCount == 1 &&
                inertTextStretch.Issues.Single().Kind ==
                "visually-inert-text-stretch" &&
                inertTextStretch.Issues.Single().Axis == "horizontal");

            var asymmetricStretch = AuditFixture(
                inertStretch.Replace("margin-right: 18px", "margin-right: 21px"));
            AddSelfTestCase(cases, "asymmetric label stretch passes",
                asymmetricStretch.WarningCount == 0);

            var nonCenteredTextStretch = AuditFixture(
                inertStretch.Replace("middle-center", "middle-left"));
            AddSelfTestCase(cases, "non-centered text stretch passes",
                nonCenteredTextStretch.WarningCount == 0);

            var visualBoxStretch = AuditFixture(
                inertStretch.Replace("align-self: stretch;",
                    "align-self: stretch; background-color: rgb(1, 2, 3);"));
            AddSelfTestCase(cases, "visually owned label stretch passes",
                visualBoxStretch.WarningCount == 0);

            var fixedWidthStretch = AuditFixture(
                inertStretch.Replace("align-self: stretch;",
                    "align-self: stretch; width: 120px;"));
            AddSelfTestCase(cases, "fixed cross-size label stretch passes",
                fixedWidthStretch.WarningCount == 0);

            var labelDefaultStyleIndex = new UxmlInlineStyleContractIndex();
            IndexInlineStyleSheetText("Assets/Basics.uss",
                ".unity-label { padding-top: 0; padding-right: 0; " +
                "padding-bottom: 0; padding-left: 0; }",
                labelDefaultStyleIndex);
            var zeroDefaultPaddingStretch = AuditFixture(inertStretch,
                inlineStyleContracts: labelDefaultStyleIndex);
            AddSelfTestCase(cases, "neutral label defaults do not hide inert stretch",
                zeroDefaultPaddingStretch.WarningCount == 1 &&
                zeroDefaultPaddingStretch.Issues.Single().Kind ==
                "visually-inert-text-stretch");

            var labelBoxIndex = new UxmlLayoutContractIndex();
            IndexStyleSheetText(
                ".intentional-label-box { background-image: url(\"Title.png\"); }",
                labelBoxIndex);
            var styledBoxStretch = AuditFixture(
                inertStretch.Replace("name=\"Title\"",
                    "name=\"Title\" class=\"intentional-label-box\""),
                layoutContracts: labelBoxIndex);
            AddSelfTestCase(cases, "explicit USS label box passes",
                styledBoxStretch.WarningCount == 0);

            var suppressedInertStretch = AuditFixture(
                inertStretch.Replace("<ui:Label",
                    $"<!-- {INERT_TEXT_STRETCH_SUPPRESSION_MARKER} " +
                    "fixture reserves a hit region --><ui:Label"),
                includeSuppressed: true);
            AddSelfTestCase(cases, "reasoned inert-stretch suppression is retained",
                suppressedInertStretch.WarningCount == 0 &&
                suppressedInertStretch.SuppressedCount == 1 &&
                suppressedInertStretch.Issues.Single().Suppressed);

            return new Dictionary<string, object>
            {
                { "passed", cases.All(testCase => (bool)testCase["passed"]) },
                { "cases", cases }
            };
        }

        private static MCPUxmlLayoutAuditReport AuditFixture(string element,
            string parentStyle = "width: 807px; height: 492px;", bool includeSuppressed = false,
            UxmlLayoutContractIndex layoutContracts = null,
            UxmlInlineStyleContractIndex inlineStyleContracts = null)
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
                layoutContracts ?? new UxmlLayoutContractIndex(), report, includeSuppressed,
                inlineStyleContracts);
            report.SortIssues();
            return report;
        }

        private static void AuditText(string assetPath, string text,
            UxmlLayoutContractIndex layoutContracts,
            MCPUxmlLayoutAuditReport report, bool includeSuppressed,
            UxmlInlineStyleContractIndex inlineStyleContracts = null)
        {
            var document = XDocument.Parse(text, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            IndexUxmlDocument(document, layoutContracts);
            inlineStyleContracts = inlineStyleContracts ??
                                   BuildInlineStyleContractIndex(assetPath, document, report);
            foreach (var element in document.Descendants())
            {
                AuditElement(assetPath, element, layoutContracts, report, includeSuppressed);
            }

            AuditRedundantInlineDeclarations(assetPath, document, inlineStyleContracts, report,
                includeSuppressed);
            AuditVisuallyInertTextStretch(assetPath, document, inlineStyleContracts,
                layoutContracts, report, includeSuppressed);
            AuditRepeatedInlineLayoutVariants(assetPath, document, layoutContracts, report,
                includeSuppressed);
        }

        private static void AuditElement(string assetPath, XElement element,
            UxmlLayoutContractIndex layoutContracts, MCPUxmlLayoutAuditReport report,
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
                HasBoxContract(element, style, layoutContracts))
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
            UxmlLayoutContractIndex layoutContracts)
        {
            if (style.Any(property => IsBoxContractProperty(property.Key, property.Value)))
            {
                return true;
            }

            var name = AttributeValue(element, "name");
            if (string.IsNullOrWhiteSpace(name) == false && layoutContracts.BoxIds.Contains(name))
            {
                return true;
            }

            foreach (var className in SplitWhitespace(AttributeValue(element, "class")))
            {
                if (layoutContracts.BoxClasses.Contains(className))
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

        private static UxmlLayoutContractIndex BuildLayoutContractIndex(
            MCPUxmlLayoutAuditReport report, MCPUIToolkitAuditOptions options,
            IEnumerable<string> uxmlPaths)
        {
            var index = new UxmlLayoutContractIndex();
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

            foreach (var path in uxmlPaths ?? Enumerable.Empty<string>())
            {
                try
                {
                    var document = XDocument.Parse(
                        File.ReadAllText(MCPUIToolkitAuditUtility.ToFullPath(path)),
                        LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
                    IndexUxmlDocument(document, index);
                }
                catch (Exception exception)
                {
                    report.Errors.Add($"Failed to index UXML layout variants in '{path}': " +
                                      exception.Message);
                }
            }

            return index;
        }

        private static void IndexStyleSheetText(string text, UxmlLayoutContractIndex index)
        {
            var sanitized = ussCommentRegex.Replace(text ?? "", "");
            foreach (Match rule in ussRuleRegex.Matches(sanitized))
            {
                var declarations = ParseStyle(rule.Groups["body"].Value);
                var selector = rule.Groups["selector"].Value;
                var classNames = classTokenRegex.Matches(selector)
                    .Cast<Match>()
                    .Select(match => match.Groups["token"].Value)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                foreach (var className in classNames)
                {
                    foreach (var declaration in declarations.Where(declaration =>
                                 IsVariantLayoutProperty(declaration.Key)))
                    {
                        index.AddClassLayoutProperty(className, declaration.Key);
                    }
                }

                if (declarations.Any(property =>
                        IsBoxContractProperty(property.Key, property.Value)))
                {
                    foreach (var className in classNames)
                    {
                        index.BoxClasses.Add(className);
                    }

                    foreach (Match match in idTokenRegex.Matches(selector))
                    {
                        index.BoxIds.Add(match.Groups["token"].Value);
                    }
                }
            }
        }

        private static void IndexUxmlDocument(XDocument document,
            UxmlLayoutContractIndex index)
        {
            foreach (var element in document.Descendants())
            {
                var classNames = SplitWhitespace(AttributeValue(element, "class"))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                foreach (var baseClass in classNames)
                {
                    foreach (var variantClass in classNames.Where(candidate =>
                                 candidate.StartsWith(baseClass + "-",
                                     StringComparison.Ordinal)))
                    {
                        index.AddAuthoredVariant(baseClass, variantClass);
                    }
                }
            }
        }

        private static UxmlInlineStyleContractIndex BuildInlineStyleContractIndex(
            string uxmlAssetPath, XDocument document, MCPUxmlLayoutAuditReport report)
        {
            var index = new UxmlInlineStyleContractIndex();
            var indexedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var styleElement in document.Descendants()
                         .Where(element => element.Name.LocalName == "Style"))
            {
                var stylePath = ResolveStyleReference(
                    AttributeValue(styleElement, "src"), uxmlAssetPath);
                if (string.IsNullOrWhiteSpace(stylePath) ||
                    indexedPaths.Add(stylePath) == false)
                {
                    continue;
                }

                try
                {
                    var fullPath = MCPUIToolkitAuditUtility.ToFullPath(stylePath);
                    if (File.Exists(fullPath) == false)
                    {
                        report.Errors.Add(
                            $"Referenced USS asset does not exist: {stylePath} " +
                            $"(from {uxmlAssetPath}).");
                        continue;
                    }

                    IndexInlineStyleSheetText(stylePath, File.ReadAllText(fullPath), index);
                }
                catch (Exception exception)
                {
                    report.Errors.Add(
                        $"Failed to index referenced USS defaults in '{stylePath}': " +
                        exception.Message);
                }
            }

            return index;
        }

        private static string ResolveStyleReference(string rawPath, string uxmlAssetPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                return "";
            }

            var path = rawPath.Trim().Replace('\\', '/');
            var queryIndex = path.IndexOf('?');
            if (queryIndex >= 0)
            {
                path = path.Substring(0, queryIndex);
            }

            var fragmentIndex = path.IndexOf('#');
            if (fragmentIndex >= 0)
            {
                path = path.Substring(0, fragmentIndex);
            }

            const string projectPrefix = "project://database/";
            if (path.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(projectPrefix.Length);
            }

            path = Uri.UnescapeDataString(path);
            if (Path.IsPathRooted(path))
            {
                return MCPUIToolkitAuditUtility.ToAssetPath(path);
            }

            if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                return MCPUIToolkitAuditUtility.NormalizeAssetPath(path);
            }

            var ownerDirectory = Path.GetDirectoryName(uxmlAssetPath) ?? "";
            var combined = Path.Combine(ownerDirectory,
                path.Replace('/', Path.DirectorySeparatorChar));
            return MCPUIToolkitAuditUtility.ToAssetPath(
                MCPUIToolkitAuditUtility.ToFullPath(combined));
        }

        private static void IndexInlineStyleSheetText(string sourcePath, string text,
            UxmlInlineStyleContractIndex index)
        {
            var sanitized = ussCommentRegex.Replace(text ?? "", "");
            foreach (Match rule in ussRuleRegex.Matches(sanitized))
            {
                var declarations = ParseStyle(rule.Groups["body"].Value);
                if (declarations.Count == 0)
                {
                    continue;
                }

                foreach (var rawSelector in rule.Groups["selector"].Value.Split(','))
                {
                    if (TryParseSimpleSelector(rawSelector, out var selector) == false)
                    {
                        continue;
                    }

                    index.AddRule(sourcePath, selector, declarations);
                }
            }
        }

        private static bool TryParseSimpleSelector(string rawSelector,
            out UxmlSimpleSelector selector)
        {
            selector = null;
            var value = (rawSelector ?? "").Trim();
            var match = Regex.Match(value,
                @"^(?<type>[A-Za-z_][A-Za-z0-9_-]*)?" +
                @"(?<tokens>(?:[.#][A-Za-z_][A-Za-z0-9_-]*)*)$");
            if (match.Success == false || value.Length == 0)
            {
                return false;
            }

            var classNames = classTokenRegex.Matches(value)
                .Cast<Match>()
                .Select(item => item.Groups["token"].Value)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var ids = idTokenRegex.Matches(value)
                .Cast<Match>()
                .Select(item => item.Groups["token"].Value)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (ids.Count > 1)
            {
                return false;
            }

            var typeName = match.Groups["type"].Value;
            if (string.IsNullOrWhiteSpace(typeName) &&
                classNames.Count == 0 &&
                ids.Count == 0)
            {
                return false;
            }

            selector = new UxmlSimpleSelector
            {
                Text = value,
                TypeName = typeName,
                Id = ids.SingleOrDefault() ?? "",
                Specificity = ids.Count * 100 + classNames.Count * 10 +
                              (string.IsNullOrWhiteSpace(typeName) ? 0 : 1)
            };
            selector.ClassNames.AddRange(classNames);
            return true;
        }

        private static void AuditRedundantInlineDeclarations(string assetPath,
            XDocument document, UxmlInlineStyleContractIndex inlineStyleContracts,
            MCPUxmlLayoutAuditReport report, bool includeSuppressed)
        {
            foreach (var element in document.Descendants())
            {
                var inlineDeclarations = ParseStyle(AttributeValue(element, "style"));
                if (inlineDeclarations.Count == 0)
                {
                    continue;
                }

                var stylesheetDeclarations = inlineStyleContracts.Resolve(element);
                var redundant = inlineDeclarations
                    .Where(declaration =>
                        stylesheetDeclarations.TryGetValue(declaration.Key,
                            out var stylesheetDeclaration) &&
                        StyleValuesEqual(declaration.Value, stylesheetDeclaration.Value))
                    .OrderBy(declaration => declaration.Key,
                        StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(declaration => declaration.Key,
                        declaration => declaration.Value,
                        StringComparer.OrdinalIgnoreCase);
                if (redundant.Count == 0)
                {
                    continue;
                }

                var name = AttributeValue(element, "name");
                var elementLabel = string.IsNullOrWhiteSpace(name)
                    ? $"<{element.Name.LocalName}>"
                    : $"#{name}";
                var stylesheetRules = redundant.Keys.Select(property =>
                {
                    var source = stylesheetDeclarations[property];
                    return new Dictionary<string, object>
                    {
                        { "property", property },
                        { "selector", source.Selector },
                        { "sourcePath", source.SourcePath }
                    };
                }).ToList();
                var sourceLabels = stylesheetRules
                    .Select(source =>
                        $"{source["selector"]} in {source["sourcePath"]}")
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                var suppressionReason = GetSuppressionReason(element,
                    redundantInlineSuppressionRegex);
                var issue = new MCPUxmlLayoutAuditIssue
                {
                    AssetPath = assetPath,
                    Line = GetLineNumber(element),
                    Element = elementLabel,
                    ElementName = name,
                    Kind = "redundant-inline-declaration",
                    Axis = GetLayoutAxis(redundant.Keys),
                    FixedProperties = redundant.Keys.ToList(),
                    InlineDeclarations = redundant,
                    StylesheetRules = stylesheetRules,
                    Suppressed = string.IsNullOrWhiteSpace(suppressionReason) == false,
                    SuppressionReason = suppressionReason,
                    Message =
                        $"Inline style {FormatDeclarations(redundant)} on {elementLabel} repeats " +
                        $"the same default value supplied by {string.Join(", ", sourceLabels)}. " +
                        "Remove the redundant inline declaration so the loaded USS remains the " +
                        "single style owner."
                };
                report.Record(issue, includeSuppressed);
            }
        }

        private static void AuditVisuallyInertTextStretch(string assetPath,
            XDocument document, UxmlInlineStyleContractIndex inlineStyleContracts,
            UxmlLayoutContractIndex layoutContracts, MCPUxmlLayoutAuditReport report,
            bool includeSuppressed)
        {
            foreach (var element in document.Descendants())
            {
                var parent = element.Parent as XElement;
                if (parent == null)
                {
                    continue;
                }

                var elementType = ResolveVisualElementType(element);
                if (elementType == null ||
                    typeof(Label).IsAssignableFrom(elementType) == false ||
                    ResolveVisualElementType(parent) != typeof(VisualElement))
                {
                    continue;
                }

                var inlineStyle = ParseStyle(AttributeValue(element, "style"));
                if (StyleValue(inlineStyle, "align-self") != "stretch")
                {
                    continue;
                }

                var stylesheetStyle = inlineStyleContracts.Resolve(element);
                if (stylesheetStyle.TryGetValue("align-self",
                        out var stylesheetAlignment) &&
                    StyleValuesEqual(stylesheetAlignment.Value, "stretch"))
                {
                    continue;
                }

                var parentStyle = ResolveAuthoredStyle(parent, inlineStyleContracts);
                if (StyleValue(parentStyle, "align-items") != "center")
                {
                    continue;
                }

                var flexDirection = StyleValue(parentStyle, "flex-direction");
                if (string.IsNullOrWhiteSpace(flexDirection))
                {
                    flexDirection = "column";
                }

                var horizontal = flexDirection == "column" ||
                                 flexDirection == "column-reverse";
                var vertical = flexDirection == "row" ||
                               flexDirection == "row-reverse";
                if (horizontal == false && vertical == false)
                {
                    continue;
                }

                var elementStyle = ResolveAuthoredStyle(element, inlineStyleContracts);
                var textAlignment = StyleValue(elementStyle, "-unity-text-align");
                if ((horizontal && textAlignment.EndsWith("-center",
                         StringComparison.Ordinal) == false) ||
                    (vertical && textAlignment.StartsWith("middle-",
                         StringComparison.Ordinal) == false) ||
                    HasCrossAxisSizeContract(elementStyle, horizontal) ||
                    HasVisualBoxContract(element, elementStyle, layoutContracts) ||
                    HasSymmetricCrossAxisMargins(elementStyle, horizontal) == false)
                {
                    continue;
                }

                var name = AttributeValue(element, "name");
                var elementLabel = string.IsNullOrWhiteSpace(name)
                    ? $"<{element.Name.LocalName}>"
                    : $"#{name}";
                var axis = horizontal ? "horizontal" : "vertical";
                var suppressionReason = GetSuppressionReason(element,
                    inertTextStretchSuppressionRegex);
                var issue = new MCPUxmlLayoutAuditIssue
                {
                    AssetPath = assetPath,
                    Line = GetLineNumber(element),
                    Element = elementLabel,
                    ElementName = name,
                    Kind = "visually-inert-text-stretch",
                    Axis = axis,
                    FixedProperties = new List<string> { "align-self" },
                    InlineDeclarations = new Dictionary<string, string>(
                        StringComparer.OrdinalIgnoreCase)
                    {
                        { "align-self", inlineStyle["align-self"] }
                    },
                    Suppressed = string.IsNullOrWhiteSpace(suppressionReason) == false,
                    SuppressionReason = suppressionReason,
                    Message =
                        $"Inline align-self: stretch expands the transparent {elementLabel} " +
                        $"{axis} layout box, but its plain VisualElement parent already centers " +
                        $"the cross axis, the text alignment is {textAlignment}, and the opposing " +
                        "margins are equal. The glyph center is unchanged at the element's natural " +
                        "size; remove the inert stretch declaration."
                };
                report.Record(issue, includeSuppressed);
            }
        }

        private static Dictionary<string, string> ResolveAuthoredStyle(XElement element,
            UxmlInlineStyleContractIndex inlineStyleContracts)
        {
            var result = inlineStyleContracts.Resolve(element)
                .ToDictionary(declaration => declaration.Key,
                    declaration => declaration.Value.Value,
                    StringComparer.OrdinalIgnoreCase);
            foreach (var declaration in ParseStyle(AttributeValue(element, "style")))
            {
                result[declaration.Key] = declaration.Value;
            }

            return result;
        }

        private static bool HasCrossAxisSizeContract(
            IReadOnlyDictionary<string, string> style, bool horizontal)
        {
            var properties = horizontal
                ? new[] { "width", "min-width", "max-width" }
                : new[] { "height", "min-height", "max-height" };
            return properties.Any(property =>
                style.TryGetValue(property, out var value) &&
                string.IsNullOrWhiteSpace(value) == false &&
                string.Equals(value.Trim(), "auto", StringComparison.OrdinalIgnoreCase) == false &&
                string.Equals(value.Trim(), "none", StringComparison.OrdinalIgnoreCase) == false);
        }

        private static bool HasVisualBoxContract(XElement element,
            IReadOnlyDictionary<string, string> style,
            UxmlLayoutContractIndex layoutContracts)
        {
            if (style.Any(property =>
                    IsMeaningfulVisualBoxProperty(property.Key, property.Value)))
            {
                return true;
            }

            var name = AttributeValue(element, "name");
            if (string.IsNullOrWhiteSpace(name) == false &&
                layoutContracts.BoxIds.Contains(name))
            {
                return true;
            }

            return SplitWhitespace(AttributeValue(element, "class"))
                .Any(className => layoutContracts.BoxClasses.Contains(className));
        }

        private static bool IsMeaningfulVisualBoxProperty(string property, string value)
        {
            if (IsBoxContractProperty(property, value) == false)
            {
                return false;
            }

            var normalizedProperty = (property ?? "").Trim().ToLowerInvariant();
            var normalizedValue = Regex.Replace((value ?? "").Trim().ToLowerInvariant(),
                @"\s+", " ");
            if ((normalizedProperty == "padding" ||
                 normalizedProperty.StartsWith("padding-", StringComparison.Ordinal) ||
                 normalizedProperty.EndsWith("-width", StringComparison.Ordinal)) &&
                IsZeroBoxValue(normalizedValue))
            {
                return false;
            }

            if (normalizedProperty == "background-image" &&
                (normalizedValue == "none" || normalizedValue == "initial"))
            {
                return false;
            }

            if (normalizedProperty == "background-color" &&
                (normalizedValue == "transparent" ||
                 Regex.IsMatch(normalizedValue,
                     @"^rgba\([^,]+,[^,]+,[^,]+,\s*0(?:\.0+)?\)$")))
            {
                return false;
            }

            if (normalizedProperty == "opacity" && normalizedValue == "1" ||
                normalizedProperty == "visibility" && normalizedValue == "visible" ||
                normalizedProperty == "scale" &&
                (normalizedValue == "1" || normalizedValue == "1 1") ||
                normalizedProperty == "rotate" &&
                (normalizedValue == "0" || normalizedValue == "0deg") ||
                normalizedProperty == "translate" && IsZeroBoxValue(normalizedValue))
            {
                return false;
            }

            return true;
        }

        private static bool IsZeroBoxValue(string value)
        {
            var parts = SplitWhitespace(value).ToList();
            return parts.Count > 0 && parts.All(part =>
                Regex.IsMatch(part, @"^[+-]?0(?:\.0+)?(?:px|%|em|rem)?$",
                    RegexOptions.IgnoreCase));
        }

        private static bool HasSymmetricCrossAxisMargins(
            IReadOnlyDictionary<string, string> style, bool horizontal)
        {
            var firstSide = horizontal ? "left" : "top";
            var secondSide = horizontal ? "right" : "bottom";
            if (style.ContainsKey("margin") &&
                (style.ContainsKey("margin-" + firstSide) ||
                 style.ContainsKey("margin-" + secondSide)))
            {
                return false;
            }

            return TryGetBoxSideValue(style, "margin", firstSide, out var first) &&
                   TryGetBoxSideValue(style, "margin", secondSide, out var second) &&
                   StyleValuesEqual(first, second);
        }

        private static bool TryGetBoxSideValue(
            IReadOnlyDictionary<string, string> style, string shorthandProperty,
            string side, out string value)
        {
            var sideProperty = shorthandProperty + "-" + side;
            if (style.TryGetValue(sideProperty, out value))
            {
                return true;
            }

            if (style.TryGetValue(shorthandProperty, out var shorthand) == false)
            {
                value = "0";
                return true;
            }

            var values = SplitWhitespace(shorthand).ToList();
            if (values.Count < 1 || values.Count > 4)
            {
                value = "";
                return false;
            }

            switch (side)
            {
                case "top":
                    value = values[0];
                    return true;
                case "right":
                    value = values.Count == 1 ? values[0] : values[1];
                    return true;
                case "bottom":
                    value = values.Count < 3 ? values[0] : values[2];
                    return true;
                case "left":
                    value = values.Count == 1
                        ? values[0]
                        : values.Count < 4 ? values[1] : values[3];
                    return true;
                default:
                    value = "";
                    return false;
            }
        }

        private static bool StyleValuesEqual(string left, string right)
        {
            return string.Equals(
                Regex.Replace((left ?? "").Trim(), @"\s+", " "),
                Regex.Replace((right ?? "").Trim(), @"\s+", " "),
                StringComparison.OrdinalIgnoreCase);
        }

        private static IReadOnlyCollection<string> GetElementClasses(XElement element)
        {
            var classNames = new HashSet<string>(
                SplitWhitespace(AttributeValue(element, "class")),
                StringComparer.Ordinal);
            foreach (var implicitClass in GetImplicitElementClasses(element))
            {
                classNames.Add(implicitClass);
            }

            return classNames;
        }

        private static IReadOnlyList<string> GetImplicitElementClasses(XElement element)
        {
            var namespaceName = element.Name.NamespaceName;
            if (string.IsNullOrWhiteSpace(namespaceName))
            {
                return Array.Empty<string>();
            }

            var fullTypeName = namespaceName + "." + element.Name.LocalName;
            if (implicitElementClassesByType.TryGetValue(fullTypeName, out var cached))
            {
                return cached;
            }

            var classes = new HashSet<string>(StringComparer.Ordinal);
            var elementType = ResolveVisualElementType(fullTypeName);
            for (var current = elementType;
                 current != null && typeof(VisualElement).IsAssignableFrom(current);
                 current = current.BaseType)
            {
                try
                {
                    var field = current.GetField("ussClassName",
                        BindingFlags.Public | BindingFlags.NonPublic |
                        BindingFlags.Static | BindingFlags.DeclaredOnly);
                    if (field != null && field.FieldType == typeof(string) &&
                        field.GetValue(null) is string className &&
                        string.IsNullOrWhiteSpace(className) == false)
                    {
                        classes.Add(className);
                    }
                }
                catch
                {
                    // A third-party VisualElement can expose an unsafe static accessor.
                    // Static auditing only consumes safe, readable class-name constants.
                }
            }

            var result = classes.OrderBy(value => value, StringComparer.Ordinal).ToList();
            implicitElementClassesByType[fullTypeName] = result;
            return result;
        }

        private static Type ResolveVisualElementType(XElement element)
        {
            var namespaceName = element?.Name.NamespaceName;
            return string.IsNullOrWhiteSpace(namespaceName)
                ? null
                : ResolveVisualElementType(namespaceName + "." + element.Name.LocalName);
        }

        private static Type ResolveVisualElementType(string fullTypeName)
        {
            var engineType = typeof(VisualElement).Assembly.GetType(fullTypeName, false);
            if (engineType != null)
            {
                return engineType;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var candidate = assembly.GetType(fullTypeName, false);
                    if (candidate != null &&
                        typeof(VisualElement).IsAssignableFrom(candidate))
                    {
                        return candidate;
                    }
                }
                catch
                {
                    // Ignore assemblies that cannot serve reflected UI Toolkit types.
                }
            }

            return null;
        }

        private static void AuditRepeatedInlineLayoutVariants(string assetPath,
            XDocument document, UxmlLayoutContractIndex layoutContracts,
            MCPUxmlLayoutAuditReport report, bool includeSuppressed)
        {
            var candidates = new List<RepeatedInlineLayoutCandidate>();
            foreach (var element in document.Descendants())
            {
                var inlineLayout = ParseStyle(AttributeValue(element, "style"))
                    .Where(declaration => IsVariantLayoutProperty(declaration.Key))
                    .ToDictionary(declaration => declaration.Key,
                        declaration => declaration.Value,
                        StringComparer.OrdinalIgnoreCase);
                if (inlineLayout.Count == 0)
                {
                    continue;
                }

                foreach (var baseClass in SplitWhitespace(AttributeValue(element, "class"))
                             .Distinct(StringComparer.Ordinal))
                {
                    var relatedVariants = layoutContracts
                        .GetRelatedVariants(baseClass, inlineLayout.Keys)
                        .ToList();
                    if (relatedVariants.Count == 0)
                    {
                        continue;
                    }

                    var variantProperties = new HashSet<string>(
                        relatedVariants.SelectMany(layoutContracts.GetClassLayoutProperties),
                        StringComparer.OrdinalIgnoreCase);
                    var relevantDeclarations = inlineLayout
                        .Where(declaration => variantProperties.Contains(declaration.Key))
                        .OrderBy(declaration => declaration.Key,
                            StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(declaration => declaration.Key,
                            declaration => declaration.Value,
                            StringComparer.OrdinalIgnoreCase);
                    if (relevantDeclarations.Count == 0)
                    {
                        continue;
                    }

                    candidates.Add(new RepeatedInlineLayoutCandidate
                    {
                        Element = element,
                        BaseClass = baseClass,
                        Declarations = relevantDeclarations,
                        Signature = BuildDeclarationSignature(relevantDeclarations),
                        RelatedVariantClasses = relatedVariants
                    });
                }
            }

            foreach (var group in candidates
                         .GroupBy(candidate => candidate.BaseClass + "\n" + candidate.Signature,
                             StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                var ordered = group.OrderBy(candidate => GetLineNumber(candidate.Element)).ToList();
                var first = ordered[0];
                var elementName = AttributeValue(first.Element, "name");
                var elementLabel = string.IsNullOrWhiteSpace(elementName)
                    ? $".{first.BaseClass}"
                    : $"#{elementName}";
                var relatedVariants = ordered
                    .SelectMany(candidate => candidate.RelatedVariantClasses)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(className => className, StringComparer.Ordinal)
                    .ToList();
                var suppressionReason = GetSuppressionReason(first.Element,
                    repeatedInlineSuppressionRegex);
                var issue = new MCPUxmlLayoutAuditIssue
                {
                    AssetPath = assetPath,
                    Line = GetLineNumber(first.Element),
                    Element = elementLabel,
                    ElementName = elementName,
                    Kind = "repeated-inline-layout-variant",
                    Axis = GetLayoutAxis(first.Declarations.Keys),
                    FixedProperties = first.Declarations.Keys
                        .OrderBy(property => property, StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    BaseClass = first.BaseClass,
                    AuthoredUsageCount = ordered.Count,
                    InlineDeclarations =
                        new Dictionary<string, string>(first.Declarations,
                            StringComparer.OrdinalIgnoreCase),
                    RelatedVariantClasses = relatedVariants,
                    UsageLocations = ordered.Select(candidate =>
                        new Dictionary<string, object>
                        {
                            { "path", assetPath },
                            { "line", GetLineNumber(candidate.Element) },
                            {
                                "element",
                                FormatElementLabel(candidate.Element, candidate.BaseClass)
                            }
                        }).ToList(),
                    Suppressed = string.IsNullOrWhiteSpace(suppressionReason) == false,
                    SuppressionReason = suppressionReason,
                    Message =
                        $"Inline layout {FormatDeclarations(first.Declarations)} is repeated on " +
                        $"{ordered.Count} authored elements using .{first.BaseClass}, while " +
                        $"{string.Join(", ", relatedVariants.Select(value => "." + value))} already " +
                        "expresses a shared authored variant for the same layout properties. " +
                        "Move the repeated declarations into a semantic shared class and apply that " +
                        "class to these elements."
                };
                report.Record(issue, includeSuppressed);
            }
        }

        private static string BuildDeclarationSignature(
            IReadOnlyDictionary<string, string> declarations)
        {
            return string.Join(";", declarations
                .OrderBy(declaration => declaration.Key, StringComparer.OrdinalIgnoreCase)
                .Select(declaration =>
                    declaration.Key.Trim().ToLowerInvariant() + ":" +
                    Regex.Replace(declaration.Value.Trim(), @"\s+", " ")
                        .ToLowerInvariant()));
        }

        private static string FormatDeclarations(
            IReadOnlyDictionary<string, string> declarations)
        {
            return "{" + string.Join("; ", declarations
                .OrderBy(declaration => declaration.Key, StringComparer.OrdinalIgnoreCase)
                .Select(declaration => declaration.Key + ": " + declaration.Value)) + "}";
        }

        private static string FormatElementLabel(XElement element, string fallbackClass)
        {
            var name = AttributeValue(element, "name");
            return string.IsNullOrWhiteSpace(name) ? "." + fallbackClass : "#" + name;
        }

        private static string GetLayoutAxis(IEnumerable<string> properties)
        {
            var propertyList = (properties ?? Enumerable.Empty<string>())
                .Select(property => (property ?? "").Trim().ToLowerInvariant())
                .ToList();
            var horizontal = propertyList.Any(property =>
                property == "left" ||
                property == "right" ||
                property.Contains("width") ||
                property.EndsWith("-left", StringComparison.Ordinal) ||
                property.EndsWith("-right", StringComparison.Ordinal) ||
                property == "column-gap");
            var vertical = propertyList.Any(property =>
                property == "top" ||
                property == "bottom" ||
                property.Contains("height") ||
                property.EndsWith("-top", StringComparison.Ordinal) ||
                property.EndsWith("-bottom", StringComparison.Ordinal) ||
                property == "row-gap");
            if (horizontal && vertical)
            {
                return "mixed";
            }

            if (horizontal)
            {
                return "horizontal";
            }

            return vertical ? "vertical" : "layout";
        }

        private static bool IsVariantLayoutProperty(string property)
        {
            return variantLayoutProperties.Contains((property ?? "").Trim());
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
            return GetSuppressionReason(element, suppressionRegex);
        }

        private static string GetSuppressionReason(XElement element, Regex markerRegex)
        {
            var previous = element.NodesBeforeSelf().Reverse().FirstOrDefault(node =>
                !(node is XText) || string.IsNullOrWhiteSpace(((XText)node).Value) == false);
            var comment = previous as XComment;
            if (comment == null)
            {
                return "";
            }

            var match = markerRegex.Match(comment.Value);
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

        private sealed class RepeatedInlineLayoutCandidate
        {
            public XElement Element;
            public string BaseClass;
            public string Signature;
            public Dictionary<string, string> Declarations;
            public List<string> RelatedVariantClasses;
        }

        private sealed class UxmlSimpleSelector
        {
            public string Text;
            public string TypeName;
            public string Id;
            public int Specificity;
            public readonly List<string> ClassNames = new List<string>();

            public bool Matches(XElement element, IReadOnlyCollection<string> elementClasses)
            {
                if (string.IsNullOrWhiteSpace(TypeName) == false &&
                    string.Equals(TypeName, element.Name.LocalName,
                        StringComparison.Ordinal) == false)
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(Id) == false &&
                    string.Equals(Id, AttributeValue(element, "name"),
                        StringComparison.Ordinal) == false)
                {
                    return false;
                }

                return ClassNames.All(elementClasses.Contains);
            }
        }

        private sealed class UxmlInlineStyleRule
        {
            public string SourcePath;
            public UxmlSimpleSelector Selector;
            public int SourceOrder;
            public readonly Dictionary<string, string> Declarations =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class UxmlStylesheetDeclaration
        {
            public string Value;
            public string Selector;
            public string SourcePath;
            public int Specificity;
            public int SourceOrder;
        }

        private sealed class UxmlInlineStyleContractIndex
        {
            private readonly List<UxmlInlineStyleRule> rules =
                new List<UxmlInlineStyleRule>();
            private int sourceOrder;

            public void AddRule(string sourcePath, UxmlSimpleSelector selector,
                IReadOnlyDictionary<string, string> declarations)
            {
                var rule = new UxmlInlineStyleRule
                {
                    SourcePath = sourcePath,
                    Selector = selector,
                    SourceOrder = sourceOrder++
                };
                foreach (var declaration in declarations)
                {
                    rule.Declarations[declaration.Key] = declaration.Value;
                }

                rules.Add(rule);
            }

            public Dictionary<string, UxmlStylesheetDeclaration> Resolve(XElement element)
            {
                var result = new Dictionary<string, UxmlStylesheetDeclaration>(
                    StringComparer.OrdinalIgnoreCase);
                var elementClasses = GetElementClasses(element);
                foreach (var rule in rules)
                {
                    if (rule.Selector.Matches(element, elementClasses) == false)
                    {
                        continue;
                    }

                    foreach (var declaration in rule.Declarations)
                    {
                        if (result.TryGetValue(declaration.Key, out var current) &&
                            (current.Specificity > rule.Selector.Specificity ||
                             current.Specificity == rule.Selector.Specificity &&
                             current.SourceOrder > rule.SourceOrder))
                        {
                            continue;
                        }

                        result[declaration.Key] = new UxmlStylesheetDeclaration
                        {
                            Value = declaration.Value,
                            Selector = rule.Selector.Text,
                            SourcePath = rule.SourcePath,
                            Specificity = rule.Selector.Specificity,
                            SourceOrder = rule.SourceOrder
                        };
                    }
                }

                return result;
            }
        }

        private sealed class UxmlLayoutContractIndex
        {
            public readonly HashSet<string> BoxClasses =
                new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> BoxIds =
                new HashSet<string>(StringComparer.Ordinal);

            private readonly Dictionary<string, HashSet<string>> classLayoutProperties =
                new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            private readonly Dictionary<string, HashSet<string>> authoredVariants =
                new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            public void AddClassLayoutProperty(string className, string property)
            {
                if (!classLayoutProperties.TryGetValue(className, out var properties))
                {
                    properties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    classLayoutProperties[className] = properties;
                }

                properties.Add(property);
            }

            public void AddAuthoredVariant(string baseClass, string variantClass)
            {
                if (!authoredVariants.TryGetValue(baseClass, out var variants))
                {
                    variants = new HashSet<string>(StringComparer.Ordinal);
                    authoredVariants[baseClass] = variants;
                }

                variants.Add(variantClass);
            }

            public IEnumerable<string> GetRelatedVariants(string baseClass,
                IEnumerable<string> inlineProperties)
            {
                if (!authoredVariants.TryGetValue(baseClass, out var variants))
                {
                    return Enumerable.Empty<string>();
                }

                var propertySet = new HashSet<string>(
                    inlineProperties ?? Enumerable.Empty<string>(),
                    StringComparer.OrdinalIgnoreCase);
                return variants.Where(variant =>
                        classLayoutProperties.TryGetValue(variant, out var properties) &&
                        properties.Overlaps(propertySet))
                    .OrderBy(variant => variant, StringComparer.Ordinal);
            }

            public IEnumerable<string> GetClassLayoutProperties(string className)
            {
                return classLayoutProperties.TryGetValue(className, out var properties)
                    ? properties
                    : Enumerable.Empty<string>();
            }
        }
    }

    internal sealed class MCPUxmlLayoutAuditReport
    {
        private readonly int maxIssues;
        private int activeIssueCount;
        private int suppressedIssueCount;
        private bool truncated;

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
            else
            {
                truncated = true;
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
            return new Dictionary<string, object>
            {
                { "success", Errors.Count == 0 },
                { "passed", Passed },
                { "scannedUxmlFiles", ScannedUxmlCount },
                { "indexedUxmlFiles", IndexedUxmlCount },
                { "indexedStyleSheets", IndexedStyleSheetCount },
                { "warningCount", WarningCount },
                { "suppressedCount", SuppressedCount },
                { "truncated", truncated },
                { "issues", Issues.Select(issue => issue.ToDictionary()).ToList() },
                { "errors", Errors.ToList() },
                {
                    "suppressionSyntax",
                    new[]
                    {
                        $"<!-- {MCPUxmlLayoutAuditor.SUPPRESSION_MARKER} <reason> -->",
                        $"<!-- {MCPUxmlLayoutAuditor.REPEATED_INLINE_SUPPRESSION_MARKER} <reason> -->",
                        $"<!-- {MCPUxmlLayoutAuditor.REDUNDANT_INLINE_SUPPRESSION_MARKER} <reason> -->",
                        $"<!-- {MCPUxmlLayoutAuditor.INERT_TEXT_STRETCH_SUPPRESSION_MARKER} <reason> -->"
                    }
                }
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
        public string BaseClass;
        public int AuthoredUsageCount;
        public Dictionary<string, string> InlineDeclarations =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public List<string> RelatedVariantClasses = new List<string>();
        public List<Dictionary<string, object>> UsageLocations =
            new List<Dictionary<string, object>>();
        public List<Dictionary<string, object>> StylesheetRules =
            new List<Dictionary<string, object>>();
        public bool Suppressed;
        public string SuppressionReason;
        public string Message;

        public Dictionary<string, object> ToDictionary()
        {
            var result = new Dictionary<string, object>
            {
                { "assetPath", AssetPath },
                { "line", Line },
                { "element", Element },
                { "elementName", ElementName ?? "" },
                { "kind", Kind },
                { "axis", Axis },
                { "fixedProperties", FixedProperties.ToList() },
                { "suppressed", Suppressed },
                { "suppressionReason", SuppressionReason ?? "" },
                { "message", Message }
            };
            if (string.Equals(Kind, "manual-centered-layout-box",
                    StringComparison.Ordinal))
            {
                result["parentSize"] = ParentSize;
                result["offset"] = Offset;
                result["size"] = Size;
            }
            else if (string.Equals(Kind, "repeated-inline-layout-variant",
                    StringComparison.Ordinal))
            {
                result["baseClass"] = BaseClass ?? "";
                result["authoredUsageCount"] = AuthoredUsageCount;
                result["inlineDeclarations"] =
                    new Dictionary<string, string>(InlineDeclarations,
                        StringComparer.OrdinalIgnoreCase);
                result["relatedVariantClasses"] = RelatedVariantClasses.ToList();
                result["usageLocations"] = UsageLocations.ToList();
            }
            else if (string.Equals(Kind, "redundant-inline-declaration",
                         StringComparison.Ordinal))
            {
                result["inlineDeclarations"] =
                    new Dictionary<string, string>(InlineDeclarations,
                        StringComparer.OrdinalIgnoreCase);
                result["stylesheetRules"] = StylesheetRules.ToList();
            }
            else if (string.Equals(Kind, "visually-inert-text-stretch",
                         StringComparison.Ordinal))
            {
                result["inlineDeclarations"] =
                    new Dictionary<string, string>(InlineDeclarations,
                        StringComparer.OrdinalIgnoreCase);
            }

            return result;
        }
    }
}
#endif
