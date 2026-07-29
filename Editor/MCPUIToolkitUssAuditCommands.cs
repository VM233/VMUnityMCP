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
    public static class MCPUIToolkitUssAuditCommands
    {
        public static object AuditUssStyles(Dictionary<string, object> args)
        {
            args = args ?? new Dictionary<string, object>();
            var options = MCPUIToolkitAuditOptions.FromArguments(args);
            var report = MCPUssStyleAuditor.Audit(
                MCPUIToolkitAuditUtility.GetStringList(args, "paths"),
                MCPUIToolkitAuditUtility.GetBool(args, "includeSuppressed"),
                Mathf.Clamp(MCPUIToolkitAuditUtility.GetInt(args, "maxIssues", 200), 1, 5000),
                options);

            if (MCPUIToolkitAuditUtility.GetBool(args, "logWarnings"))
                MCPUssStyleAuditConsoleReporter.Log(report, false);

            var result = report.ToDictionary();
            result["scope"] = options.ToDictionary();
            result["automaticAudit"] =
                MCPUIToolkitAutomaticAuditCoordinator.GetStatus(".uss");
            if (MCPUIToolkitAuditUtility.GetBool(args, "runSelfTests"))
            {
                var selfTests = MCPUssStyleAuditor.RunSelfTests();
                result["selfTests"] = selfTests;
                object passed;
                if (selfTests.TryGetValue("passed", out passed) &&
                    passed is bool && !(bool)passed)
                    result["success"] = false;
            }

            return result;
        }

        [MenuItem("Tools/UI Toolkit/Audit USS Styles")]
        private static void AuditAllFromMenu()
        {
            var options = MCPUIToolkitAuditOptions.FromProjectSettings(
                MCPUIToolkitAuditProjectSettings.Load());
            var report = MCPUssStyleAuditor.Audit(
                Array.Empty<string>(), true, 5000, options);
            MCPUssStyleAuditConsoleReporter.Log(report, false);
        }
    }

    internal static class MCPUssStyleAuditConsoleReporter
    {
        internal static void Log(MCPUssStyleAuditReport report, bool automatic)
        {
            foreach (var error in report.Errors)
            {
                Debug.LogError($"[USS Style Audit] {error}");
            }

            foreach (var issue in report.Issues.Where(issue => issue.Suppressed == false))
            {
                var context = AssetDatabase.LoadAssetAtPath<StyleSheet>(issue.AssetPath);
                Debug.LogWarning(
                    $"[USS Style Audit] {issue.AssetPath}:{issue.Line} {issue.Selector}: {issue.Message}",
                    context);
            }

            if (automatic == false || report.Errors.Count > 0 || report.WarningCount > 0)
            {
                var mode = automatic ? "automatic import audit" : "requested audit";
                Debug.Log(
                    $"[USS Style Audit] {mode}: scanned={report.ScannedStyleSheetCount}, " +
                    $"warnings={report.WarningCount}, suppressed={report.SuppressedCount}, " +
                    $"errors={report.Errors.Count}, passed={report.Passed}.");
            }
        }
    }

    internal static class MCPUssStyleAuditor
    {
        internal const string SUPPRESSION_MARKER = "uss-audit: allow-single-use";
        internal const string REDUNDANT_DECLARATION_SUPPRESSION_MARKER =
            "uss-audit: allow-redundant-declaration";
        internal const string PIXEL_GRID_SUPPRESSION_MARKER =
            "uss-audit: allow-off-grid-pixels";
        internal const string TEXT_STYLE_CONTRACT_SUPPRESSION_MARKER =
            "uss-audit: allow-text-style-contract";

        private static readonly Regex commentRegex =
            new Regex(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex suppressionRegex =
            new Regex(@"/\*\s*uss-audit:\s*allow-single-use\s+(?<reason>.+?)\*/\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex redundantDeclarationSuppressionRegex =
            new Regex(@"/\*\s*uss-audit:\s*allow-redundant-declaration\s+(?<reason>.+?)\*/\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex pixelGridSuppressionRegex =
            new Regex(@"/\*\s*uss-audit:\s*allow-off-grid-pixels\s+(?<reason>.+?)\*/\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex textStyleContractSuppressionRegex =
            new Regex(@"/\*\s*uss-audit:\s*allow-text-style-contract\s+(?<reason>.+?)\*/\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex importRegex =
            new Regex(@"@import\s+url\(\s*(?:[""'](?<quoted>[^""']+)[""']|(?<plain>[^)\s]+))\s*\)\s*;",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex panelThemeGuidRegex =
            new Regex(@"^\s*themeUss:\s*\{[^}\r\n]*\bguid:\s*(?<guid>[0-9a-fA-F]{32})\b[^}\r\n]*\}",
                RegexOptions.Compiled | RegexOptions.Multiline);

        private static readonly Regex simpleClassSelectorRegex =
            new Regex(@"^\.(?<token>[A-Za-z_][A-Za-z0-9_-]*)$", RegexOptions.Compiled);

        private static readonly Regex simpleIdSelectorRegex =
            new Regex(@"^#(?<token>[A-Za-z_][A-Za-z0-9_-]*)$", RegexOptions.Compiled);

        private static readonly Regex classTokenRegex =
            new Regex(@"(?<![A-Za-z0-9_-])\.(?<token>[A-Za-z_][A-Za-z0-9_-]*)",
                RegexOptions.Compiled);

        private static readonly Regex idTokenRegex =
            new Regex(@"(?<![A-Za-z0-9_-])#(?<token>[A-Za-z_][A-Za-z0-9_-]*)",
                RegexOptions.Compiled);

        private static readonly Regex relationalAnchorClassTokenRegex =
            new Regex(@"(?<![A-Za-z0-9_-])\.(?<token>[A-Za-z_][A-Za-z0-9_-]*)(?=\s|[>+~])",
                RegexOptions.Compiled);

        private static readonly Regex relationalTargetClassTokenRegex =
            new Regex(@"(?<![A-Za-z0-9_-])\.(?<token>[A-Za-z_][A-Za-z0-9_-]*)\s*$",
                RegexOptions.Compiled);

        private static readonly Regex relationalTargetIdTokenRegex =
            new Regex(@"(?<![A-Za-z0-9_-])#(?<token>[A-Za-z_][A-Za-z0-9_-]*)\s*$",
                RegexOptions.Compiled);

        private static readonly Regex quotedTokenRegex =
            new Regex(@"[""'](?<token>[A-Za-z_][A-Za-z0-9_-]*)[""']",
                RegexOptions.Compiled);

        private static readonly Regex yamlListTokenRegex =
            new Regex(@"^\s*-\s*(?<token>[A-Za-z_][A-Za-z0-9_-]*)\s*$",
                RegexOptions.Compiled | RegexOptions.Multiline);

        private static readonly string[] runtimeClassApiMarkers =
        {
            "AddToClassList",
            "RemoveFromClassList",
            "EnableInClassList",
            "ClassListContains",
            "classList",
            "ussClassName",
            "UssClassName"
        };

        private static readonly string[] inheritedTextStyleProperties =
        {
            "color",
            "font-size",
            "-unity-font",
            "-unity-font-definition",
            "-unity-font-style",
            "white-space",
            "letter-spacing",
            "word-spacing",
            "-unity-paragraph-spacing",
            "-unity-text-outline-color",
            "-unity-text-outline-width"
        };

        internal static MCPUssStyleAuditReport Audit(IEnumerable<string> requestedPaths,
            bool includeSuppressed, int maxIssues, MCPUIToolkitAuditOptions options)
        {
            options = options ?? MCPUIToolkitAuditOptions.FromArguments(
                new Dictionary<string, object>());
            var report = new MCPUssStyleAuditReport(maxIssues);
            var requestedPathList = (requestedPaths ?? Array.Empty<string>())
                .Where(path => string.IsNullOrWhiteSpace(path) == false).ToList();
            var requested = NormalizeRequestedPaths(requestedPathList, report.Errors);
            var commonThemePath = FindCommonPanelThemePath(options);
            var commonThemeStylePaths = EnumerateImportedStylePaths(commonThemePath,
                report.Errors);
            var allStyleSheetPaths = MCPUIToolkitAuditUtility.FindAssetFiles(".uss", options)
                .Concat(requested)
                .Concat(commonThemeStylePaths)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
            var targetPaths = requestedPathList.Count == 0 ? allStyleSheetPaths : requested;
            report.ScannedStyleSheetCount = targetPaths.Count;
            report.IndexedStyleSheetCount = allStyleSheetPaths.Count;

            var rulesByPath = new Dictionary<string, List<UssRule>>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in allStyleSheetPaths)
            {
                try
                {
                    rulesByPath[path] = ParseStyleSheet(path,
                        File.ReadAllText(MCPUIToolkitAuditUtility.ToFullPath(path)));
                }
                catch (Exception exception)
                {
                    report.Errors.Add($"Failed to read '{path}': {exception.Message}");
                }
            }

            var usageIndex = BuildUsageIndex(rulesByPath.Values.SelectMany(rules => rules), options);
            var cascadeIndex = BuildCascadeIndex(commonThemePath, rulesByPath, usageIndex,
                report.Errors);
            report.IndexedUxmlCount = usageIndex.IndexedUxmlCount;
            report.IndexedRuntimeSourceCount = usageIndex.IndexedRuntimeSourceCount;

            foreach (var path in targetPaths)
            {
                if (rulesByPath.TryGetValue(path, out var rules) == false)
                {
                    if (File.Exists(MCPUIToolkitAuditUtility.ToFullPath(path)) == false)
                    {
                        report.Errors.Add($"USS asset does not exist: {path}");
                    }

                    continue;
                }

                AuditPixelGridDeclarations(rules, options, report, includeSuppressed);
                AuditTextStyleContracts(rules, usageIndex, cascadeIndex, report,
                    includeSuppressed);
                AuditRules(rules, usageIndex, report, includeSuppressed);
                AuditRedundantDeclarations(rules, usageIndex, cascadeIndex, report,
                    includeSuppressed);
            }

            report.SortIssues();
            return report;
        }

        internal static Dictionary<string, object> RunSelfTests()
        {
            const string path = "Assets/__UssAuditSelfTest.uss";
            const string text =
                ".single { width: 10px; }\n" +
                "#Unique { height: 10px; }\n" +
                "#IdContainer { width: 20px; }\n" +
                "#IdContainer .generated { height: 10px; }\n" +
                "#Parent #UniqueChild { margin-left: 10px; }\n" +
                "#interactive-id { color: white; }\n" +
                "#interactive-id:hover { color: red; }\n" +
                ".shared { color: white; }\n" +
                ".interactive { color: white; }\n" +
                ".interactive:hover { color: red; }\n" +
                ".runtime-state { display: none; }\n" +
                ".container { width: 20px; }\n" +
                ".container .child { width: 10px; }\n" +
                "/* uss-audit: allow-single-use fixture requires authored semantic state */\n" +
                ".suppressed { opacity: 0.5; }\n" +
                ".unused { width: 1px; }\n";

            var rules = ParseStyleSheet(path, text);
            var index = new UssUsageIndex();
            CollectSelectorContracts(rules, index);
            index.AddClassUsage("single", "Assets/Single.uxml", 1);
            index.AddIdUsage("Unique", "Assets/Single.uxml", 2);
            index.AddIdUsage("IdContainer", "Assets/IdContainer.uxml", 1);
            index.AddIdUsage("Parent", "Assets/Parent.uxml", 1);
            index.AddIdUsage("UniqueChild", "Assets/Parent.uxml", 2);
            index.AddIdUsage("interactive-id", "Assets/InteractiveId.uxml", 1);
            index.AddClassUsage("shared", "Assets/SharedA.uxml", 1);
            index.AddClassUsage("shared", "Assets/SharedB.uxml", 1);
            index.AddClassUsage("interactive", "Assets/Interactive.uxml", 1);
            index.AddClassUsage("runtime-state", "Assets/Runtime.uxml", 1);
            index.AddRuntimeClassReference("runtime-state", "Assets/Scripts/Runtime.cs", 4);
            index.AddClassUsage("container", "Assets/Container.uxml", 1, "Container");
            index.AddClassUsage("child", "Assets/Container.uxml", 2);
            index.AddClassUsage("suppressed", "Assets/Suppressed.uxml", 1);

            var report = new MCPUssStyleAuditReport(100)
            {
                ScannedStyleSheetCount = 1,
                IndexedStyleSheetCount = 1
            };
            AuditRules(rules, index, report, true);
            report.SortIssues();

            const string themePath = "Assets/__UssAuditSelfTestTheme.uss";
            const string duplicatePath = "Assets/__UssAuditSelfTestDuplicate.uss";
            var themeRules = ParseStyleSheet(themePath,
                "* { -unity-slice-scale: 3px; }\n");
            var duplicateRules = ParseStyleSheet(duplicatePath,
                ".duplicate { -unity-slice-scale: 3px; }\n" +
                ".different { -unity-slice-scale: 2px; }\n" +
                "/* uss-audit: allow-redundant-declaration fixture documents ownership */\n" +
                ".suppressed-duplicate { -unity-slice-scale: 3px; }\n");
            var duplicateUsageIndex = new UssUsageIndex();
            CollectSelectorContracts(duplicateRules, duplicateUsageIndex);
            var duplicateDocument = new UssAuthoredDocument("Assets/Duplicate.uxml",
                XDocument.Parse(
                    "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" +
                    "<ui:VisualElement class=\"duplicate\"/>" +
                    "<ui:VisualElement class=\"different\"/>" +
                    "<ui:VisualElement class=\"suppressed-duplicate\"/>" +
                    "</ui:UXML>", LoadOptions.SetLineInfo));
            duplicateUsageIndex.Documents.Add(duplicateDocument);
            var duplicateCascade = new UssCascadeIndex();
            var duplicateCascadeDocument = new UssCascadeDocument(duplicateDocument);
            AppendSelfTestRules(duplicateCascadeDocument, themeRules, 0);
            AppendSelfTestRules(duplicateCascadeDocument, duplicateRules, 1);
            duplicateCascade.Documents.Add(duplicateCascadeDocument);
            var duplicateReport = new MCPUssStyleAuditReport(100)
            {
                ScannedStyleSheetCount = 1,
                IndexedStyleSheetCount = 2,
                IndexedUxmlCount = 1
            };
            AuditRedundantDeclarations(duplicateRules, duplicateUsageIndex, duplicateCascade,
                duplicateReport, true);
            duplicateReport.SortIssues();

            var pixelGridRules = ParseStyleSheet(path,
                ".grid-pass { left: -6px; margin-right: 9px; padding: 3px 6px; }\n" +
                ".grid-fail { top: 4px; padding: 3px 7px; }\n" +
                ".grid-non-pixel { left: 50%; font-size: 7px; }\n" +
                "/* uss-audit: allow-off-grid-pixels fixture documents optical alignment */\n" +
                ".grid-suppressed { margin-left: 1px; }\n");
            var pixelGridOptions = MCPUIToolkitAuditOptions.FromArguments(
                new Dictionary<string, object>
                {
                    { "useProjectSettings", false },
                    { "pixelGridEnabled", true },
                    { "pixelGridStep", 3 }
                });
            var pixelGridReport = new MCPUssStyleAuditReport(100);
            AuditPixelGridDeclarations(pixelGridRules, pixelGridOptions, pixelGridReport, true);
            pixelGridReport.SortIssues();

            const string textContractPath = "Assets/__UssAuditSelfTestTextContracts.uss";
            var textContractRules = ParseStyleSheet(textContractPath,
                ".centered-text-owner { align-items: center; justify-content: center; }\n" +
                ".problem-text { color: white; font-size: 18px; -unity-font-style: bold; " +
                "-unity-text-generator: advanced; -unity-text-align: middle-center; }\n" +
                ".auto-sized-text { -unity-text-generator: advanced; " +
                "-unity-text-auto-size: best-fit 8px 18px; }\n" +
                ".boxed-text { width: 30px; -unity-text-align: middle-center; }\n" +
                ".sibling-text { color: white; }\n" +
                "/* uss-audit: allow-text-style-contract fixture documents advanced shaping */\n" +
                ".suppressed-text { -unity-text-generator: advanced; }\n");
            var textContractUsageIndex = new UssUsageIndex();
            CollectSelectorContracts(textContractRules, textContractUsageIndex);
            var textContractDocument = new UssAuthoredDocument(
                "Assets/TextContracts.uxml",
                XDocument.Parse(
                    "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" +
                    "<ui:VisualElement class=\"centered-text-owner\">" +
                    "<ui:Label class=\"problem-text\" text=\"1\"/>" +
                    "</ui:VisualElement>" +
                    "<ui:VisualElement><ui:Label class=\"auto-sized-text\" text=\"Auto\"/>" +
                    "</ui:VisualElement>" +
                    "<ui:VisualElement class=\"centered-text-owner\">" +
                    "<ui:Label class=\"boxed-text\" text=\"Box\"/>" +
                    "</ui:VisualElement>" +
                    "<ui:VisualElement><ui:Label class=\"sibling-text\" text=\"Sibling\"/>" +
                    "<ui:VisualElement/></ui:VisualElement>" +
                    "<ui:VisualElement class=\"centered-text-owner\">" +
                    "<ui:Label class=\"suppressed-text\" text=\"Suppressed\"/>" +
                    "</ui:VisualElement>" +
                    "</ui:UXML>", LoadOptions.SetLineInfo));
            textContractUsageIndex.Documents.Add(textContractDocument);
            var textContractCascade = new UssCascadeIndex();
            var textContractCascadeDocument =
                new UssCascadeDocument(textContractDocument);
            AppendSelfTestRules(textContractCascadeDocument, textContractRules, 1);
            textContractCascade.Documents.Add(textContractCascadeDocument);
            var textContractReport = new MCPUssStyleAuditReport(100);
            AuditTextStyleContracts(textContractRules, textContractUsageIndex,
                textContractCascade, textContractReport, true);
            textContractReport.SortIssues();

            var activeTokens = report.Issues.Where(issue => issue.Suppressed == false)
                .Select(issue => issue.Token).OrderBy(token => token, StringComparer.Ordinal).ToArray();
            var suppressedTokens = report.Issues.Where(issue => issue.Suppressed)
                .Select(issue => issue.Token).OrderBy(token => token, StringComparer.Ordinal).ToArray();
            var activeRedundantSelectors = duplicateReport.Issues
                .Where(issue => issue.Suppressed == false)
                .Select(issue => issue.Selector)
                .OrderBy(selector => selector, StringComparer.Ordinal)
                .ToArray();
            var suppressedRedundantSelectors = duplicateReport.Issues
                .Where(issue => issue.Suppressed)
                .Select(issue => issue.Selector)
                .OrderBy(selector => selector, StringComparer.Ordinal)
                .ToArray();
            var activePixelGridSelectors = pixelGridReport.Issues
                .Where(issue => issue.Suppressed == false)
                .Select(issue => issue.Selector)
                .OrderBy(selector => selector, StringComparer.Ordinal)
                .ToArray();
            var suppressedPixelGridSelectors = pixelGridReport.Issues
                .Where(issue => issue.Suppressed)
                .Select(issue => issue.Selector)
                .OrderBy(selector => selector, StringComparer.Ordinal)
                .ToArray();
            var activeTextContractKinds = textContractReport.Issues
                .Where(issue => issue.Suppressed == false)
                .Select(issue => issue.Kind)
                .OrderBy(kind => kind, StringComparer.Ordinal)
                .ToArray();
            var suppressedTextContractKinds = textContractReport.Issues
                .Where(issue => issue.Suppressed)
                .Select(issue => issue.Kind)
                .OrderBy(kind => kind, StringComparer.Ordinal)
                .ToArray();
            var cases = new List<Dictionary<string, object>>();

            AddSelfTestCase(cases, "single class warns", activeTokens.Contains("single"));
            AddSelfTestCase(cases, "single ID warns", activeTokens.Contains("Unique"));
            AddSelfTestCase(cases, "simple ID with relational contract still warns",
                activeTokens.Contains("IdContainer"));
            AddSelfTestCase(cases, "single relational ID target warns",
                activeTokens.Contains("UniqueChild"));
            AddSelfTestCase(cases, "pseudo ID contract passes",
                activeTokens.Contains("interactive-id") == false);
            AddSelfTestCase(cases, "zero-consumer selector is outside the single-use gate",
                activeTokens.Contains("unused") == false);
            AddSelfTestCase(cases, "shared class passes", activeTokens.Contains("shared") == false);
            AddSelfTestCase(cases, "pseudo contract passes", activeTokens.Contains("interactive") == false);
            AddSelfTestCase(cases, "runtime class passes", activeTokens.Contains("runtime-state") == false);
            AddSelfTestCase(cases, "single named relational anchor warns", activeTokens.Contains("container"));
            AddSelfTestCase(cases, "single relational target warns", activeTokens.Contains("child"));
            AddSelfTestCase(cases, "reasoned suppression is reported as suppressed",
                suppressedTokens.SequenceEqual(new[] { "suppressed" }));
            AddSelfTestCase(cases, "active finding set is exact",
                activeTokens.SequenceEqual(
                    new[] { "IdContainer", "Unique", "UniqueChild", "child", "container", "single" }));
            AddSelfTestCase(cases, "same winning theme value warns",
                activeRedundantSelectors.SequenceEqual(new[] { ".duplicate" }));
            AddSelfTestCase(cases, "different-value override passes",
                activeRedundantSelectors.Contains(".different") == false);
            AddSelfTestCase(cases, "reasoned redundant-declaration suppression is retained",
                suppressedRedundantSelectors.SequenceEqual(new[] { ".suppressed-duplicate" }));
            AddSelfTestCase(cases, "only off-grid structural declarations warn",
                activePixelGridSelectors.SequenceEqual(new[] { ".grid-fail" }));
            AddSelfTestCase(cases, "off-grid shorthand values are retained",
                pixelGridReport.Issues.Single(issue => issue.Suppressed == false)
                    .OffGridDeclarations.Keys.OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(new[] { "padding", "top" }));
            AddSelfTestCase(cases, "reasoned pixel-grid suppression is retained",
                suppressedPixelGridSelectors.SequenceEqual(new[] { ".grid-suppressed" }));
            AddSelfTestCase(cases,
                "advanced generator without auto size warns independently",
                activeTextContractKinds.Contains(
                    "advanced-text-generator-without-auto-size"));
            AddSelfTestCase(cases,
                "text align on shrink-wrapped centered label warns independently",
                activeTextContractKinds.Contains(
                    "ineffective-text-align-on-shrink-wrapped-label"));
            AddSelfTestCase(cases,
                "inheritable only-child text styles warn independently",
                activeTextContractKinds.Contains(
                    "inheritable-text-style-on-only-child-label"));
            AddSelfTestCase(cases, "text contract active finding set is exact",
                activeTextContractKinds.SequenceEqual(new[]
                {
                    "advanced-text-generator-without-auto-size",
                    "ineffective-text-align-on-shrink-wrapped-label",
                    "inheritable-text-style-on-only-child-label"
                }));
            AddSelfTestCase(cases,
                "advanced generator with auto size passes",
                textContractReport.Issues.All(issue =>
                    issue.Selector != ".auto-sized-text"));
            AddSelfTestCase(cases,
                "text align with an explicit box passes",
                textContractReport.Issues.All(issue =>
                    issue.Selector != ".boxed-text"));
            AddSelfTestCase(cases,
                "inheritable text style with a sibling passes",
                textContractReport.Issues.All(issue =>
                    issue.Selector != ".sibling-text"));
            AddSelfTestCase(cases,
                "reasoned text style contract suppression is retained",
                suppressedTextContractKinds.SequenceEqual(new[]
                {
                    "advanced-text-generator-without-auto-size"
                }));

            return new Dictionary<string, object>
            {
                { "passed", cases.All(testCase => (bool)testCase["passed"]) },
                { "cases", cases },
                { "activeTokens", activeTokens },
                { "suppressedTokens", suppressedTokens },
                { "activeRedundantSelectors", activeRedundantSelectors },
                { "suppressedRedundantSelectors", suppressedRedundantSelectors },
                { "activePixelGridSelectors", activePixelGridSelectors },
                { "suppressedPixelGridSelectors", suppressedPixelGridSelectors },
                { "activeTextContractKinds", activeTextContractKinds },
                { "suppressedTextContractKinds", suppressedTextContractKinds }
            };
        }

        private static void AppendSelfTestRules(UssCascadeDocument document,
            IEnumerable<UssRule> rules, int origin)
        {
            foreach (var rule in rules)
            {
                document.LoadedAssetPaths.Add(rule.AssetPath);
                foreach (var selectorText in rule.Selectors)
                {
                    TryParseSimpleSelector(selectorText, out var selector);
                    document.Rules.Add(new UssCascadeRule
                    {
                        Rule = rule,
                        SelectorText = selectorText,
                        Selector = selector,
                        Origin = origin,
                        SourceOrder = document.NextSourceOrder()
                    });
                }
            }
        }

        private static UssUsageIndex BuildUsageIndex(IEnumerable<UssRule> allRules,
            MCPUIToolkitAuditOptions options)
        {
            var index = new UssUsageIndex();
            var rules = allRules.ToList();
            CollectSelectorContracts(rules, index);
            IndexUxmlUsage(index, options);
            IndexRuntimeClassReferences(index, options);
            return index;
        }

        private static void CollectSelectorContracts(IEnumerable<UssRule> rules, UssUsageIndex index)
        {
            foreach (var rule in rules)
            {
                foreach (var selector in rule.Selectors)
                {
                    var simpleClass = simpleClassSelectorRegex.Match(selector);
                    var simpleId = simpleIdSelectorRegex.Match(selector);
                    foreach (Match match in classTokenRegex.Matches(selector))
                    {
                        var token = match.Groups["token"].Value;
                        index.AllClassTokens.Add(token);
                        if (simpleClass.Success == false)
                        {
                            index.ComplexClassTokens.Add(token);
                        }
                    }

                    foreach (Match match in idTokenRegex.Matches(selector))
                    {
                        var token = match.Groups["token"].Value;
                        index.AllIdTokens.Add(token);
                        var tokenEnd = match.Index + match.Length;
                        if (tokenEnd < selector.Length && selector[tokenEnd] == ':')
                        {
                            index.PseudoIdTokens.Add(token);
                        }

                        if (simpleId.Success == false)
                        {
                            index.ComplexIdTokens.Add(token);
                        }
                    }
                }
            }
        }

        private static void IndexUxmlUsage(UssUsageIndex index, MCPUIToolkitAuditOptions options)
        {
            foreach (var path in MCPUIToolkitAuditUtility.FindAssetFiles(".uxml", options))
            {
                try
                {
                    string text = File.ReadAllText(MCPUIToolkitAuditUtility.ToFullPath(path));
                    var document = XDocument.Parse(text,
                        LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
                    index.IndexedUxmlCount++;
                    index.AddDocument(path, document);
                    foreach (XElement element in document.Descendants())
                    {
                        XAttribute classAttribute = GetAttribute(element, "class");
                        if (classAttribute != null)
                        {
                            string elementName = GetAttributeValue(element, "name");
                            foreach (string token in SplitWhitespace(classAttribute.Value))
                            {
                                if (index.AllClassTokens.Contains(token))
                                {
                                    index.AddClassUsage(token, path,
                                        GetLineNumber(classAttribute),
                                        GetColumnNumber(classAttribute), elementName);
                                }
                            }
                        }

                        XAttribute nameAttribute = GetAttribute(element, "name");
                        if (nameAttribute == null)
                            continue;

                        string name = nameAttribute.Value.Trim();
                        if (index.AllIdTokens.Contains(name))
                        {
                            index.AddIdUsage(name, path,
                                GetLineNumber(nameAttribute),
                                GetColumnNumber(nameAttribute));
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }
        }

        private static string ResolveStyleReference(string rawPath, string ownerAssetPath)
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

            if (path.StartsWith("unity-theme://", StringComparison.OrdinalIgnoreCase))
            {
                return "";
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

            var ownerDirectory = Path.GetDirectoryName(ownerAssetPath) ?? "";
            var combined = Path.Combine(ownerDirectory,
                path.Replace('/', Path.DirectorySeparatorChar));
            return MCPUIToolkitAuditUtility.ToAssetPath(
                MCPUIToolkitAuditUtility.ToFullPath(combined));
        }

        private static void IndexRuntimeClassReferences(UssUsageIndex index,
            MCPUIToolkitAuditOptions options)
        {
            foreach (var path in MCPUIToolkitAuditUtility.FindRuntimeSourceFiles(options))
            {
                string text;
                try
                {
                    text = File.ReadAllText(MCPUIToolkitAuditUtility.ToFullPath(path));
                }
                catch
                {
                    continue;
                }

                if (runtimeClassApiMarkers.Any(marker =>
                        text.Contains(marker, StringComparison.Ordinal)) == false)
                {
                    continue;
                }

                index.IndexedRuntimeSourceCount++;
                foreach (Match match in quotedTokenRegex.Matches(text).Cast<Match>()
                             .Concat(yamlListTokenRegex.Matches(text).Cast<Match>()))
                {
                    var token = match.Groups["token"].Value;
                    if (index.AllClassTokens.Contains(token))
                    {
                        index.AddRuntimeClassReference(token, path,
                            GetLineNumber(text, match.Index),
                            GetColumnNumber(text, match.Index));
                    }
                }
            }
        }

        private static string FindCommonPanelThemePath(MCPUIToolkitAuditOptions options)
        {
            var themePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var assetPath in MCPUIToolkitAuditUtility.FindAssetFiles(".asset", options))
            {
                string text;
                try
                {
                    text = File.ReadAllText(MCPUIToolkitAuditUtility.ToFullPath(assetPath));
                }
                catch
                {
                    continue;
                }

                if (text.Contains("UnityEngine.UIElements.PanelSettings",
                        StringComparison.Ordinal) == false)
                {
                    continue;
                }

                var match = panelThemeGuidRegex.Match(text);
                if (match.Success == false)
                {
                    continue;
                }

                var themePath = AssetDatabase.GUIDToAssetPath(match.Groups["guid"].Value);
                if (string.IsNullOrWhiteSpace(themePath) == false)
                {
                    themePaths.Add(MCPUIToolkitAuditUtility.NormalizeAssetPath(themePath));
                }
            }

            return themePaths.Count == 1 ? themePaths.Single() : "";
        }

        private static IReadOnlyCollection<string> EnumerateImportedStylePaths(string rootPath,
            ICollection<string> errors)
        {
            var stylePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectImportedStylePaths(rootPath, stylePaths, visited, errors);
            return stylePaths;
        }

        private static void CollectImportedStylePaths(string assetPath,
            ISet<string> stylePaths, ISet<string> visited, ICollection<string> errors)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || visited.Add(assetPath) == false)
            {
                return;
            }

            string text;
            try
            {
                text = File.ReadAllText(MCPUIToolkitAuditUtility.ToFullPath(assetPath));
            }
            catch (Exception exception)
            {
                errors.Add($"Failed to read theme stylesheet '{assetPath}': {exception.Message}");
                return;
            }

            foreach (var importPath in GetImportedStylePaths(assetPath, text))
            {
                if (importPath.EndsWith(".uss", StringComparison.OrdinalIgnoreCase))
                {
                    stylePaths.Add(importPath);
                }

                CollectImportedStylePaths(importPath, stylePaths, visited, errors);
            }
        }

        private static IEnumerable<string> GetImportedStylePaths(string ownerPath, string text)
        {
            foreach (Match match in importRegex.Matches(commentRegex.Replace(text ?? "", "")))
            {
                var rawPath = match.Groups["quoted"].Success
                    ? match.Groups["quoted"].Value
                    : match.Groups["plain"].Value;
                var resolved = ResolveStyleReference(rawPath, ownerPath);
                if (string.IsNullOrWhiteSpace(resolved) == false)
                {
                    yield return resolved;
                }
            }
        }

        private static UssCascadeIndex BuildCascadeIndex(string commonThemePath,
            IReadOnlyDictionary<string, List<UssRule>> rulesByPath, UssUsageIndex usageIndex,
            ICollection<string> errors)
        {
            var index = new UssCascadeIndex();
            var additionalRules =
                new Dictionary<string, List<UssRule>>(StringComparer.OrdinalIgnoreCase);
            var reportedErrors = new HashSet<string>(StringComparer.Ordinal);

            foreach (var authoredDocument in usageIndex.Documents)
            {
                var cascadeDocument = new UssCascadeDocument(authoredDocument);
                if (string.IsNullOrWhiteSpace(commonThemePath) == false)
                {
                    AppendStyleSheetCascade(commonThemePath, 0, cascadeDocument, rulesByPath,
                        additionalRules, new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                        errors, reportedErrors);
                }

                foreach (var stylePath in authoredDocument.StylePaths)
                {
                    AppendStyleSheetCascade(stylePath, 1, cascadeDocument, rulesByPath,
                        additionalRules, new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                        errors, reportedErrors);
                }

                index.Documents.Add(cascadeDocument);
            }

            return index;
        }

        private static void AppendStyleSheetCascade(string assetPath, int origin,
            UssCascadeDocument document,
            IReadOnlyDictionary<string, List<UssRule>> rulesByPath,
            IDictionary<string, List<UssRule>> additionalRules,
            ISet<string> importStack, ICollection<string> errors, ISet<string> reportedErrors)
        {
            assetPath = MCPUIToolkitAuditUtility.NormalizeAssetPath(assetPath);
            if (string.IsNullOrWhiteSpace(assetPath) || importStack.Add(assetPath) == false)
            {
                return;
            }

            try
            {
                var fullPath = MCPUIToolkitAuditUtility.ToFullPath(assetPath);
                if (File.Exists(fullPath) == false)
                {
                    var message = $"Referenced stylesheet does not exist: {assetPath}";
                    if (reportedErrors.Add(message))
                    {
                        errors.Add(message);
                    }

                    return;
                }

                var text = File.ReadAllText(fullPath);
                foreach (var importPath in GetImportedStylePaths(assetPath, text))
                {
                    AppendStyleSheetCascade(importPath, origin, document, rulesByPath,
                        additionalRules, importStack, errors, reportedErrors);
                }

                if (rulesByPath.TryGetValue(assetPath, out var rules) == false &&
                    additionalRules.TryGetValue(assetPath, out rules) == false)
                {
                    rules = ParseStyleSheet(assetPath, text);
                    additionalRules[assetPath] = rules;
                }

                document.LoadedAssetPaths.Add(assetPath);
                foreach (var rule in rules)
                {
                    foreach (var selectorText in rule.Selectors)
                    {
                        TryParseSimpleSelector(selectorText, out var selector);
                        document.Rules.Add(new UssCascadeRule
                        {
                            Rule = rule,
                            SelectorText = selectorText,
                            Selector = selector,
                            Origin = origin,
                            SourceOrder = document.NextSourceOrder()
                        });
                    }
                }
            }
            catch (Exception exception)
            {
                var message = $"Failed to index stylesheet cascade '{assetPath}': " +
                              exception.Message;
                if (reportedErrors.Add(message))
                {
                    errors.Add(message);
                }
            }
            finally
            {
                importStack.Remove(assetPath);
            }
        }

        private static void AuditRedundantDeclarations(IReadOnlyList<UssRule> rules,
            UssUsageIndex usageIndex, UssCascadeIndex cascadeIndex,
            MCPUssStyleAuditReport report, bool includeSuppressed)
        {
            foreach (var rule in rules)
            {
                var selectors = new List<UssSimpleSelector>();
                var fullySupported = true;
                foreach (var selectorText in rule.Selectors)
                {
                    if (TryParseSimpleSelector(selectorText, out var selector) == false)
                    {
                        fullySupported = false;
                        break;
                    }

                    selectors.Add(selector);
                }

                if (fullySupported == false || selectors.Count == 0 ||
                    selectors.All(selector => selector.Specificity == 0) ||
                    rule.Selectors.Any(selector =>
                        SelectorHasRuntimeClassContract(selector, usageIndex)))
                {
                    continue;
                }

                foreach (var declaration in rule.Declarations)
                {
                    var authoredUsages = new List<UssUsageLocation>();
                    var fallbackRules = new List<UssResolvedDeclaration>();
                    var targetWon = false;
                    var uncertain = false;

                    foreach (var document in cascadeIndex.Documents.Where(document =>
                                 document.LoadedAssetPaths.Contains(rule.AssetPath)))
                    {
                        if (document.HasUnsupportedCompetingDeclaration(
                                declaration.Key, declaration.Value))
                        {
                            uncertain = true;
                            break;
                        }

                        foreach (var element in document.AuthoredDocument.Elements.Where(element =>
                                     selectors.Any(selector => selector.Matches(element))))
                        {
                            var current = document.Resolve(element, declaration.Key, null);
                            if (current == null || ReferenceEquals(current.Rule, rule) == false)
                            {
                                continue;
                            }

                            targetWon = true;
                            var fallback = document.Resolve(element, declaration.Key, rule);
                            if (fallback == null ||
                                StyleValuesEqual(fallback.Value, declaration.Value) == false)
                            {
                                uncertain = true;
                                break;
                            }

                            authoredUsages.Add(new UssUsageLocation(
                                document.AuthoredDocument.AssetPath, element.Line, element.Column));
                            fallbackRules.Add(fallback);
                        }

                        if (uncertain)
                        {
                            break;
                        }
                    }

                    if (uncertain || targetWon == false || fallbackRules.Count == 0)
                    {
                        continue;
                    }

                    AddRedundantDeclarationIssue(report, rule, declaration.Key,
                        declaration.Value, authoredUsages, fallbackRules, includeSuppressed);
                }
            }
        }

        private static void AddRedundantDeclarationIssue(MCPUssStyleAuditReport report,
            UssRule rule, string property, string value,
            IEnumerable<UssUsageLocation> authoredUsages,
            IEnumerable<UssResolvedDeclaration> fallbackDeclarations,
            bool includeSuppressed)
        {
            var usages = authoredUsages
                .GroupBy(usage => $"{usage.Path}:{usage.Line}:{usage.Column}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            var fallbackRules = fallbackDeclarations
                .GroupBy(fallback =>
                        $"{fallback.Rule.AssetPath}\n{fallback.SelectorText}\n{property}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(fallback => fallback.Rule.AssetPath, StringComparer.Ordinal)
                .ThenBy(fallback => fallback.Rule.Line)
                .ThenBy(fallback => fallback.SelectorText, StringComparer.Ordinal)
                .ToList();
            var sourceLabels = fallbackRules
                .Select(fallback =>
                    $"'{fallback.SelectorText}' in {fallback.Rule.AssetPath}")
                .ToList();
            var selectorLabel = string.Join(", ", rule.Selectors);
            var issue = new MCPUssStyleAuditIssue
            {
                AssetPath = rule.AssetPath,
                Line = rule.Line,
                Selector = selectorLabel,
                Token = property,
                Kind = "redundant-declaration",
                Property = property,
                Value = value,
                AuthoredUsageCount = usages.Count,
                UsageLocations = usages.Take(20)
                    .Select(location => location.ToDictionary()).ToList(),
                StylesheetRules = fallbackRules.Select(fallback =>
                    new Dictionary<string, object>
                    {
                        { "property", property },
                        { "value", fallback.Value },
                        { "selector", fallback.SelectorText },
                        { "sourcePath", fallback.Rule.AssetPath },
                        { "line", fallback.Rule.Line }
                    }).ToList(),
                Suppressed = string.IsNullOrWhiteSpace(
                    rule.RedundantDeclarationSuppressionReason) == false,
                SuppressionReason = rule.RedundantDeclarationSuppressionReason,
                Message =
                    $"Declaration '{property}: {value}' in selector '{selectorLabel}' repeats " +
                    $"the same winning value already supplied by {string.Join(", ", sourceLabels)} " +
                    $"for {usages.Count} authored UXML element(s). Remove the duplicate declaration " +
                    "so the broader loaded style remains the single owner."
            };
            report.Record(issue, includeSuppressed);
        }

        private static bool TryParseSimpleSelector(string rawSelector,
            out UssSimpleSelector selector)
        {
            selector = null;
            var value = (rawSelector ?? "").Trim();
            var match = Regex.Match(value,
                @"^(?<type>\*|[A-Za-z_][A-Za-z0-9_-]*)?" +
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
            if (typeName == "*")
            {
                typeName = "";
            }

            selector = new UssSimpleSelector
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

        private static bool StyleValuesEqual(string left, string right)
        {
            return string.Equals(
                Regex.Replace((left ?? "").Trim(), @"\s+", " "),
                Regex.Replace((right ?? "").Trim(), @"\s+", " "),
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDynamicStateSelector(string selector)
        {
            var dynamicStates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "active",
                "checked",
                "disabled",
                "enabled",
                "focus",
                "focus-visible",
                "focus-within",
                "hover",
                "inactive",
                "selected"
            };
            return Regex.Matches(selector ?? "",
                    @":{1,2}(?<state>[A-Za-z_][A-Za-z0-9_-]*)")
                .Cast<Match>()
                .Any(match => dynamicStates.Contains(match.Groups["state"].Value));
        }

        private static void AuditRules(IEnumerable<UssRule> rules, UssUsageIndex usageIndex,
            MCPUssStyleAuditReport report, bool includeSuppressed)
        {
            var ruleList = rules.ToList();
            AuditRelationalSelectorContracts(ruleList, usageIndex, report, includeSuppressed);

            foreach (var rule in ruleList)
            {
                foreach (var selector in rule.Selectors)
                {
                    var classMatch = simpleClassSelectorRegex.Match(selector);
                    if (classMatch.Success)
                    {
                        var token = classMatch.Groups["token"].Value;
                        if (usageIndex.ComplexClassTokens.Contains(token))
                        {
                            continue;
                        }

                        var authored = usageIndex.GetClassUsages(token);
                        var runtime = usageIndex.GetRuntimeClassReferences(token);
                        if (authored.Count == 1 && runtime.Count == 0)
                        {
                            AddIssue(report, rule, selector, token, "single-use-class", authored, runtime,
                                $"Class selector '{selector}' serves one authored UXML element and has no pseudo, " +
                                "relational, or runtime class contract. Move its declarations to that element's inline style.",
                                includeSuppressed);
                        }

                        continue;
                    }

                    var idMatch = simpleIdSelectorRegex.Match(selector);
                    if (idMatch.Success == false)
                    {
                        continue;
                    }

                    var idToken = idMatch.Groups["token"].Value;
                    if (usageIndex.PseudoIdTokens.Contains(idToken))
                    {
                        continue;
                    }

                    var idUsages = usageIndex.GetIdUsages(idToken);
                    if (idUsages.Count == 1)
                    {
                        AddIssue(report, rule, selector, idToken, "single-use-id", idUsages,
                            Array.Empty<UssUsageLocation>(),
                            $"ID selector '{selector}' serves one authored UXML element and has no direct " +
                            "pseudo-state contract. Move its ordinary declarations to that element's inline style; " +
                            "relational use of the same ID does not justify a separate simple selector block.",
                            includeSuppressed);
                    }
                }
            }
        }

        private static void AuditPixelGridDeclarations(IEnumerable<UssRule> rules,
            MCPUIToolkitAuditOptions options, MCPUssStyleAuditReport report,
            bool includeSuppressed)
        {
            if (options.PixelGridEnabled == false)
                return;

            foreach (var rule in rules)
            {
                var offGridDeclarations =
                    MCPUIToolkitPixelGridAuditUtility.FindOffGridDeclarations(
                        rule.Declarations, options.PixelGridStep);
                if (offGridDeclarations.Count == 0)
                    continue;

                var orderedProperties = offGridDeclarations.Keys
                    .OrderBy(property => property, StringComparer.Ordinal)
                    .ToList();
                var selector = string.Join(", ", rule.Selectors);
                var suppressionReason = rule.PixelGridSuppressionReason;
                var issue = new MCPUssStyleAuditIssue
                {
                    AssetPath = rule.AssetPath,
                    Line = rule.Line,
                    Selector = selector,
                    Token = string.Join(", ", orderedProperties),
                    Kind = "off-grid-pixel-declarations",
                    GridStep = options.PixelGridStep,
                    OffGridDeclarations = offGridDeclarations,
                    Suppressed = string.IsNullOrWhiteSpace(suppressionReason) == false,
                    SuppressionReason = suppressionReason,
                    Message =
                        $"Selector '{selector}' has structural offset, spacing, or padding " +
                        $"declarations outside the configured {options.PixelGridStep}px grid: " +
                        $"{string.Join(", ", orderedProperties)}. Align them to the project grid " +
                        "or add a reasoned suppression for a measured optical or seam correction."
                };
                report.Record(issue, includeSuppressed);
            }
        }

        private static void AuditTextStyleContracts(IReadOnlyList<UssRule> rules,
            UssUsageIndex usageIndex, UssCascadeIndex cascadeIndex,
            MCPUssStyleAuditReport report, bool includeSuppressed)
        {
            foreach (var rule in rules)
            {
                if (TryGetSupportedTextContractSelectors(rule, out var selectors) == false)
                {
                    continue;
                }

                AuditAdvancedTextGenerator(rule, selectors, usageIndex, cascadeIndex,
                    report, includeSuppressed);
                AuditShrinkWrappedTextAlignment(rule, selectors, usageIndex, cascadeIndex,
                    report, includeSuppressed);
                AuditInheritableOnlyChildTextStyles(rule, selectors, usageIndex,
                    cascadeIndex, report, includeSuppressed);
            }
        }

        private static void AuditAdvancedTextGenerator(UssRule rule,
            IReadOnlyCollection<UssSimpleSelector> selectors, UssUsageIndex usageIndex,
            UssCascadeIndex cascadeIndex, MCPUssStyleAuditReport report,
            bool includeSuppressed)
        {
            const string property = "-unity-text-generator";
            if (rule.Declarations.TryGetValue(property, out var value) == false ||
                StyleValuesEqual(value, "advanced") == false)
            {
                return;
            }

            var usages = FindWinningElementUsages(rule, property, selectors, cascadeIndex)
                .Where(usage => IsAuthoredTextElement(usage.Element))
                .Where(usage => IsTextAutoSizeEnabled(
                    ResolveEffectiveTextStyle(usage.Document, usage.Element,
                        "-unity-text-auto-size")) == false)
                .ToList();
            if (usages.Count == 0)
            {
                return;
            }

            RecordTextStyleContractIssue(rule, usageIndex, report, includeSuppressed,
                usages, property, value, "advanced-text-generator-without-auto-size",
                $"Selector '{string.Join(", ", rule.Selectors)}' enables the advanced text " +
                $"generator for {usages.Count} authored Label element(s) without effective " +
                "-unity-text-auto-size. Keep the default generator unless auto sizing or another " +
                "documented advanced-text requirement owns this setting.");
        }

        private static void AuditShrinkWrappedTextAlignment(UssRule rule,
            IReadOnlyCollection<UssSimpleSelector> selectors, UssUsageIndex usageIndex,
            UssCascadeIndex cascadeIndex, MCPUssStyleAuditReport report,
            bool includeSuppressed)
        {
            const string property = "-unity-text-align";
            if (rule.Declarations.TryGetValue(property, out var value) == false)
            {
                return;
            }

            var usages = FindWinningElementUsages(rule, property, selectors, cascadeIndex)
                .Where(usage => IsAuthoredTextElement(usage.Element))
                .Where(IsShrinkWrappedBySoleCenteredParent)
                .ToList();
            if (usages.Count == 0)
            {
                return;
            }

            RecordTextStyleContractIssue(rule, usageIndex, report, includeSuppressed,
                usages, property, value, "ineffective-text-align-on-shrink-wrapped-label",
                $"Selector '{string.Join(", ", rule.Selectors)}' sets text alignment on " +
                $"{usages.Count} shrink-wrapped Label element(s). Each Label is the sole child of " +
                "a parent that already centers it on both flex axes, and the Label has no authored " +
                "box expansion for text alignment to act within. Remove the ineffective text-align " +
                "declaration.");
        }

        private static void AuditInheritableOnlyChildTextStyles(UssRule rule,
            IReadOnlyCollection<UssSimpleSelector> selectors, UssUsageIndex usageIndex,
            UssCascadeIndex cascadeIndex, MCPUssStyleAuditReport report,
            bool includeSuppressed)
        {
            var declarations = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            var relatedUsages = new List<UssAuthoredElementUsage>();
            foreach (var property in inheritedTextStyleProperties)
            {
                if (rule.Declarations.TryGetValue(property, out var value) == false ||
                    IsConcreteStyleValue(value) == false)
                {
                    continue;
                }

                var usages = FindWinningElementUsages(rule, property, selectors, cascadeIndex);
                if (usages.Count == 0 || usages.All(IsOnlyAuthoredChildLabel) == false)
                {
                    continue;
                }

                declarations[property] = value;
                relatedUsages.AddRange(usages);
            }

            if (declarations.Count == 0)
            {
                return;
            }

            var usagesForIssue = DistinctElementUsages(relatedUsages);
            var selectorLabel = string.Join(", ", rule.Selectors);
            var runtimeReferences = GetRuntimeReferences(rule, usageIndex);
            var suppressionReason = rule.TextStyleContractSuppressionReason;
            var issue = new MCPUssStyleAuditIssue
            {
                AssetPath = rule.AssetPath,
                Line = rule.Line,
                Selector = selectorLabel,
                Token = string.Join(", ", declarations.Keys
                    .OrderBy(property => property, StringComparer.Ordinal)),
                Kind = "inheritable-text-style-on-only-child-label",
                RelatedDeclarations = declarations,
                AuthoredUsageCount = usagesForIssue.Count,
                RuntimeReferenceCount = runtimeReferences.Count,
                UsageLocations = ToUsageLocations(usagesForIssue)
                    .Concat(runtimeReferences.Select(location => location.ToDictionary()))
                    .Take(20).ToList(),
                Suppressed = string.IsNullOrWhiteSpace(suppressionReason) == false,
                SuppressionReason = suppressionReason,
                Message =
                    $"Selector '{selectorLabel}' owns inheritable text declarations on " +
                    $"{usagesForIssue.Count} Label element(s), each the sole authored child of its " +
                    $"parent: {string.Join(", ", declarations.Keys.OrderBy(property => property, StringComparer.Ordinal))}. " +
                    "Move those declarations to the parent so the Label inherits them, then remove " +
                    "the child-only class if it has no remaining contract."
            };
            report.Record(issue, includeSuppressed);
        }

        private static bool TryGetSupportedTextContractSelectors(UssRule rule,
            out IReadOnlyCollection<UssSimpleSelector> selectors)
        {
            var parsed = new List<UssSimpleSelector>();
            foreach (var selectorText in rule.Selectors)
            {
                if (TryParseSimpleSelector(selectorText, out var selector) == false ||
                    selector.ClassNames.Count == 0 &&
                    string.IsNullOrWhiteSpace(selector.Id))
                {
                    selectors = Array.Empty<UssSimpleSelector>();
                    return false;
                }

                parsed.Add(selector);
            }

            selectors = parsed;
            return parsed.Count > 0;
        }

        private static List<UssAuthoredElementUsage> FindWinningElementUsages(
            UssRule rule, string property,
            IReadOnlyCollection<UssSimpleSelector> selectors, UssCascadeIndex cascadeIndex)
        {
            var usages = new List<UssAuthoredElementUsage>();
            foreach (var document in cascadeIndex.Documents.Where(document =>
                         document.LoadedAssetPaths.Contains(rule.AssetPath)))
            {
                foreach (var element in document.AuthoredDocument.Elements.Where(element =>
                             selectors.Any(selector => selector.Matches(element))))
                {
                    if (element.InlineDeclarations.ContainsKey(property))
                    {
                        continue;
                    }

                    var winner = document.Resolve(element, property, null);
                    if (winner != null && ReferenceEquals(winner.Rule, rule))
                    {
                        usages.Add(new UssAuthoredElementUsage(document, element));
                    }
                }
            }

            return DistinctElementUsages(usages);
        }

        private static List<UssAuthoredElementUsage> DistinctElementUsages(
            IEnumerable<UssAuthoredElementUsage> usages)
        {
            return usages.GroupBy(usage =>
                    $"{usage.Document.AuthoredDocument.AssetPath}:{usage.Element.Line}:{usage.Element.Column}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(usage => usage.Document.AuthoredDocument.AssetPath,
                    StringComparer.Ordinal)
                .ThenBy(usage => usage.Element.Line)
                .ThenBy(usage => usage.Element.Column)
                .ToList();
        }

        private static bool IsShrinkWrappedBySoleCenteredParent(
            UssAuthoredElementUsage usage)
        {
            var element = usage.Element;
            var parent = element.Parent;
            if (IsOnlyAuthoredChildLabel(usage) == false ||
                StyleValuesEqual(ResolveOwnStyle(usage.Document, parent, "align-items"),
                    "center") == false ||
                StyleValuesEqual(ResolveOwnStyle(usage.Document, parent, "justify-content"),
                    "center") == false)
            {
                return false;
            }

            var whiteSpace = ResolveEffectiveTextStyle(usage.Document, element,
                "white-space");
            if (StyleValuesEqual(whiteSpace, "normal") ||
                string.IsNullOrEmpty(element.Text) == false &&
                (element.Text.Contains('\n') || element.Text.Contains('\r')))
            {
                return false;
            }

            if (HasConcreteOwnStyle(usage.Document, element, "width") ||
                HasConcreteOwnStyle(usage.Document, element, "height") ||
                HasConcreteOwnStyle(usage.Document, element, "min-width") ||
                HasConcreteOwnStyle(usage.Document, element, "min-height") ||
                HasConcreteOwnStyle(usage.Document, element, "max-width") ||
                HasConcreteOwnStyle(usage.Document, element, "max-height") ||
                HasConcreteOwnStyle(usage.Document, element, "flex-basis"))
            {
                return false;
            }

            var alignSelf = ResolveOwnStyle(usage.Document, element, "align-self");
            if (StyleValuesEqual(alignSelf, "stretch"))
            {
                return false;
            }

            var flexGrow = ResolveOwnStyle(usage.Document, element, "flex-grow");
            if (HasPositiveNumber(flexGrow))
            {
                return false;
            }

            return new[]
                {
                    "padding", "padding-left", "padding-right", "padding-top",
                    "padding-bottom"
                }
                .All(property => HasNonZeroLength(
                    ResolveOwnStyle(usage.Document, element, property)) == false);
        }

        private static bool IsOnlyAuthoredChildLabel(UssAuthoredElementUsage usage)
        {
            var element = usage.Element;
            return IsAuthoredTextElement(element) &&
                   element.Parent != null &&
                   IsAuthoredTextElement(element.Parent) == false &&
                   element.Parent.Children.Count == 1 &&
                   ReferenceEquals(element.Parent.Children[0], element);
        }

        private static bool IsAuthoredTextElement(UssAuthoredElement element)
        {
            if (element == null)
            {
                return false;
            }

            return string.Equals(element.TypeName, "Label", StringComparison.Ordinal) ||
                   string.Equals(element.TypeName, "TextElement", StringComparison.Ordinal) ||
                   element.TypeName.EndsWith(".Label", StringComparison.Ordinal) ||
                   element.TypeName.EndsWith(".TextElement", StringComparison.Ordinal);
        }

        private static string ResolveOwnStyle(UssCascadeDocument document,
            UssAuthoredElement element, string property)
        {
            if (element == null)
            {
                return "";
            }

            if (element.InlineDeclarations.TryGetValue(property, out var inlineValue))
            {
                return inlineValue;
            }

            return document.Resolve(element, property, null)?.Value ?? "";
        }

        private static string ResolveEffectiveTextStyle(UssCascadeDocument document,
            UssAuthoredElement element, string property)
        {
            for (var current = element; current != null; current = current.Parent)
            {
                var value = ResolveOwnStyle(document, current, property);
                if (string.IsNullOrWhiteSpace(value) == false)
                {
                    return value;
                }
            }

            return "";
        }

        private static bool IsTextAutoSizeEnabled(string value)
        {
            return (value ?? "").Trim().StartsWith("best-fit",
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasConcreteOwnStyle(UssCascadeDocument document,
            UssAuthoredElement element, string property)
        {
            return IsConcreteStyleValue(ResolveOwnStyle(document, element, property));
        }

        private static bool IsConcreteStyleValue(string value)
        {
            var normalized = (value ?? "").Trim();
            return normalized.Length > 0 &&
                   string.Equals(normalized, "auto", StringComparison.OrdinalIgnoreCase) == false &&
                   string.Equals(normalized, "none", StringComparison.OrdinalIgnoreCase) == false &&
                   string.Equals(normalized, "initial", StringComparison.OrdinalIgnoreCase) == false &&
                   string.Equals(normalized, "inherit", StringComparison.OrdinalIgnoreCase) == false &&
                   string.Equals(normalized, "unset", StringComparison.OrdinalIgnoreCase) == false;
        }

        private static bool HasPositiveNumber(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return float.TryParse(value.Trim(), NumberStyles.Float,
                       CultureInfo.InvariantCulture, out var parsed)
                ? parsed > 0
                : IsConcreteStyleValue(value);
        }

        private static bool HasNonZeroLength(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var matches = Regex.Matches(value,
                @"[-+]?(?:\d+(?:\.\d*)?|\.\d+)");
            if (matches.Count == 0)
            {
                return IsConcreteStyleValue(value);
            }

            foreach (Match match in matches)
            {
                if (float.TryParse(match.Value, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var parsed) == false ||
                    Math.Abs(parsed) > 0.0001f)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<UssUsageLocation> GetRuntimeReferences(UssRule rule,
            UssUsageIndex usageIndex)
        {
            return rule.Selectors
                .SelectMany(selector => classTokenRegex.Matches(selector).Cast<Match>())
                .Select(match => match.Groups["token"].Value)
                .Distinct(StringComparer.Ordinal)
                .SelectMany(usageIndex.GetRuntimeClassReferences)
                .GroupBy(location =>
                        $"{location.Path}:{location.Line}:{location.Column}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static IEnumerable<Dictionary<string, object>> ToUsageLocations(
            IEnumerable<UssAuthoredElementUsage> usages)
        {
            return usages.Select(usage => new UssUsageLocation(
                    usage.Document.AuthoredDocument.AssetPath,
                    usage.Element.Line, usage.Element.Column))
                .Select(location => location.ToDictionary());
        }

        private static void RecordTextStyleContractIssue(UssRule rule,
            UssUsageIndex usageIndex, MCPUssStyleAuditReport report,
            bool includeSuppressed, IEnumerable<UssAuthoredElementUsage> usages,
            string property, string value, string kind, string message)
        {
            var authoredUsages = DistinctElementUsages(usages);
            var runtimeReferences = GetRuntimeReferences(rule, usageIndex);
            var suppressionReason = rule.TextStyleContractSuppressionReason;
            var issue = new MCPUssStyleAuditIssue
            {
                AssetPath = rule.AssetPath,
                Line = rule.Line,
                Selector = string.Join(", ", rule.Selectors),
                Token = property,
                Kind = kind,
                Property = property,
                Value = value,
                AuthoredUsageCount = authoredUsages.Count,
                RuntimeReferenceCount = runtimeReferences.Count,
                UsageLocations = ToUsageLocations(authoredUsages)
                    .Concat(runtimeReferences.Select(location => location.ToDictionary()))
                    .Take(20).ToList(),
                Suppressed = string.IsNullOrWhiteSpace(suppressionReason) == false,
                SuppressionReason = suppressionReason,
                Message = message
            };
            report.Record(issue, includeSuppressed);
        }

        private static void AuditRelationalSelectorContracts(IReadOnlyList<UssRule> rules,
            UssUsageIndex usageIndex, MCPUssStyleAuditReport report, bool includeSuppressed)
        {
            var reportedAnchors = new HashSet<string>(StringComparer.Ordinal);
            var reportedClassTargets = new HashSet<string>(StringComparer.Ordinal);
            var reportedIdTargets = new HashSet<string>(StringComparer.Ordinal);

            foreach (var rule in rules)
            {
                foreach (var selector in rule.Selectors)
                {
                    if (simpleClassSelectorRegex.IsMatch(selector) ||
                        simpleIdSelectorRegex.IsMatch(selector) ||
                        selector.Contains(':') ||
                        SelectorHasRuntimeClassContract(selector, usageIndex))
                    {
                        continue;
                    }

                    foreach (Match anchorMatch in relationalAnchorClassTokenRegex.Matches(selector))
                    {
                        var token = anchorMatch.Groups["token"].Value;
                        if (reportedAnchors.Contains(token))
                        {
                            continue;
                        }

                        var authored = usageIndex.GetClassUsages(token);
                        var runtime = usageIndex.GetRuntimeClassReferences(token);
                        var elementName = usageIndex.GetSingleClassUsageName(token);
                        if (authored.Count != 1 || runtime.Count != 0 ||
                            string.IsNullOrWhiteSpace(elementName))
                        {
                            continue;
                        }

                        var issueRule = rule;
                        var issueSelector = selector;
                        foreach (var candidateRule in rules)
                        {
                            var candidateSelector = candidateRule.Selectors.FirstOrDefault(candidate =>
                            {
                                var match = simpleClassSelectorRegex.Match(candidate);
                                return match.Success && match.Groups["token"].Value == token;
                            });
                            if (candidateSelector == null)
                            {
                                continue;
                            }

                            issueRule = candidateRule;
                            issueSelector = candidateSelector;
                            break;
                        }

                        AddIssue(report, issueRule, issueSelector, token,
                            "single-use-relational-class-anchor", authored, runtime,
                            $"Class anchor '.{token}' identifies one named authored UXML element " +
                            $"'{elementName}'. Move its ordinary declarations inline and replace the class " +
                            $"anchor in relational selectors with '#{elementName}'.",
                            includeSuppressed);
                        reportedAnchors.Add(token);
                    }

                    var targetMatch = relationalTargetClassTokenRegex.Match(selector);
                    if (targetMatch.Success)
                    {
                        var targetToken = targetMatch.Groups["token"].Value;
                        if (reportedClassTargets.Contains(targetToken) == false)
                        {
                            var targetAuthored = usageIndex.GetClassUsages(targetToken);
                            var targetRuntime = usageIndex.GetRuntimeClassReferences(targetToken);
                            if (targetAuthored.Count == 1 && targetRuntime.Count == 0)
                            {
                                AddIssue(report, rule, selector, targetToken,
                                    "single-use-relational-class-target", targetAuthored, targetRuntime,
                                    $"Class target '.{targetToken}' in relational selector '{selector}' serves one " +
                                    "authored UXML element and has no pseudo or runtime contract. Move the declarations " +
                                    "to that element's inline style and remove the class token.",
                                    includeSuppressed);
                                reportedClassTargets.Add(targetToken);
                            }
                        }
                    }

                    var idTargetMatch = relationalTargetIdTokenRegex.Match(selector);
                    if (idTargetMatch.Success == false)
                    {
                        continue;
                    }

                    var idTargetToken = idTargetMatch.Groups["token"].Value;
                    if (reportedIdTargets.Contains(idTargetToken))
                    {
                        continue;
                    }

                    var idTargetUsages = usageIndex.GetIdUsages(idTargetToken);
                    if (idTargetUsages.Count != 1)
                    {
                        continue;
                    }

                    AddIssue(report, rule, selector, idTargetToken,
                        "single-use-relational-id-target", idTargetUsages,
                        Array.Empty<UssUsageLocation>(),
                        $"ID target '#{idTargetToken}' in relational selector '{selector}' identifies one " +
                        "authored UXML element. Move the declarations to that element's inline style; keep its " +
                        "name only when binding, lookup, or another real consumer still requires it.",
                        includeSuppressed);
                    reportedIdTargets.Add(idTargetToken);
                }
            }
        }

        private static bool SelectorHasRuntimeClassContract(string selector, UssUsageIndex usageIndex)
        {
            return classTokenRegex.Matches(selector).Cast<Match>().Any(match =>
                usageIndex.GetRuntimeClassReferences(match.Groups["token"].Value).Count > 0);
        }

        private static void AddIssue(MCPUssStyleAuditReport report, UssRule rule, string selector,
            string token, string kind, IReadOnlyCollection<UssUsageLocation> authored,
            IReadOnlyCollection<UssUsageLocation> runtime, string message, bool includeSuppressed)
        {
            var issue = new MCPUssStyleAuditIssue
            {
                AssetPath = rule.AssetPath,
                Line = rule.Line,
                Selector = selector,
                Token = token,
                Kind = kind,
                AuthoredUsageCount = authored.Count,
                RuntimeReferenceCount = runtime.Count,
                UsageLocations = authored.Concat(runtime).Take(20).Select(location => location.ToDictionary()).ToList(),
                Suppressed = string.IsNullOrEmpty(rule.SuppressionReason) == false,
                SuppressionReason = rule.SuppressionReason,
                Message = message
            };
            report.Record(issue, includeSuppressed);
        }

        private static List<UssRule> ParseStyleSheet(string assetPath, string text)
        {
            var sanitized = commentRegex.Replace(text, match =>
                new string(match.Value.Select(character => character == '\n' || character == '\r'
                    ? character
                    : ' ').ToArray()));
            var rules = new List<UssRule>();
            var cursor = 0;

            while (cursor < sanitized.Length)
            {
                var openBrace = sanitized.IndexOf('{', cursor);
                if (openBrace < 0)
                {
                    break;
                }

                var closeBrace = sanitized.IndexOf('}', openBrace + 1);
                if (closeBrace < 0)
                {
                    break;
                }

                var sanitizedHeader = sanitized.Substring(cursor, openBrace - cursor);
                var originalHeader = text.Substring(cursor, openBrace - cursor);
                var lastSemicolon = sanitizedHeader.LastIndexOf(';');
                var selectorOffset = lastSemicolon >= 0 ? lastSemicolon + 1 : 0;
                var selectorGroup = sanitizedHeader.Substring(selectorOffset).Trim();
                if (string.IsNullOrEmpty(selectorGroup) == false && selectorGroup.StartsWith("@") == false)
                {
                    var leadingLength = sanitizedHeader.Substring(selectorOffset)
                        .TakeWhile(char.IsWhiteSpace).Count();
                    var selectorIndex = cursor + selectorOffset + leadingLength;
                    var suppressionContext = originalHeader.Substring(0, selectorOffset + leadingLength);
                    var suppression = suppressionRegex.Match(suppressionContext);
                    var redundantSuppression =
                        redundantDeclarationSuppressionRegex.Match(suppressionContext);
                    var pixelGridSuppression =
                        pixelGridSuppressionRegex.Match(suppressionContext);
                    var textStyleContractSuppression =
                        textStyleContractSuppressionRegex.Match(suppressionContext);
                    rules.Add(new UssRule
                    {
                        AssetPath = assetPath,
                        Line = GetLineNumber(text, selectorIndex),
                        Selectors = SplitSelectors(selectorGroup),
                        Declarations = ParseDeclarations(
                            text.Substring(openBrace + 1, closeBrace - openBrace - 1)),
                        SuppressionReason = suppression.Success
                            ? suppression.Groups["reason"].Value.Trim()
                            : "",
                        RedundantDeclarationSuppressionReason =
                            redundantSuppression.Success
                                ? redundantSuppression.Groups["reason"].Value.Trim()
                                : "",
                        PixelGridSuppressionReason =
                            pixelGridSuppression.Success
                                ? pixelGridSuppression.Groups["reason"].Value.Trim()
                                : "",
                        TextStyleContractSuppressionReason =
                            textStyleContractSuppression.Success
                                ? textStyleContractSuppression.Groups["reason"].Value.Trim()
                                : ""
                    });
                }

                cursor = closeBrace + 1;
            }

            return rules;
        }

        private static Dictionary<string, string> ParseDeclarations(string body)
        {
            var declarations =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            body = commentRegex.Replace(body ?? "", "");
            var start = 0;
            var parentheses = 0;
            var quote = '\0';

            for (var index = 0; index <= body.Length; index++)
            {
                var character = index < body.Length ? body[index] : ';';
                if (quote != '\0')
                {
                    if (character == quote && (index == 0 || body[index - 1] != '\\'))
                    {
                        quote = '\0';
                    }

                    continue;
                }

                if (character == '"' || character == '\'')
                {
                    quote = character;
                    continue;
                }

                if (character == '(')
                {
                    parentheses++;
                    continue;
                }

                if (character == ')')
                {
                    parentheses = Math.Max(0, parentheses - 1);
                    continue;
                }

                if (character != ';' || parentheses != 0)
                {
                    continue;
                }

                var declaration = body.Substring(start, index - start).Trim();
                start = index + 1;
                var colon = declaration.IndexOf(':');
                if (colon <= 0)
                {
                    continue;
                }

                var property = declaration.Substring(0, colon).Trim();
                var value = declaration.Substring(colon + 1).Trim();
                if (property.Length > 0 && value.Length > 0)
                {
                    declarations[property] = value;
                }
            }

            return declarations;
        }

        private static List<string> SplitSelectors(string selectorGroup)
        {
            var selectors = new List<string>();
            var start = 0;
            var parentheses = 0;
            var brackets = 0;
            for (var index = 0; index < selectorGroup.Length; index++)
            {
                switch (selectorGroup[index])
                {
                    case '(':
                        parentheses++;
                        break;
                    case ')':
                        parentheses = Math.Max(0, parentheses - 1);
                        break;
                    case '[':
                        brackets++;
                        break;
                    case ']':
                        brackets = Math.Max(0, brackets - 1);
                        break;
                    case ',' when parentheses == 0 && brackets == 0:
                        AddSelector(selectors, selectorGroup.Substring(start, index - start));
                        start = index + 1;
                        break;
                }
            }

            AddSelector(selectors, selectorGroup.Substring(start));
            return selectors;
        }

        private static void AddSelector(ICollection<string> selectors, string selector)
        {
            selector = Regex.Replace(selector ?? "", @"\s+", " ").Trim();
            if (string.IsNullOrEmpty(selector) == false)
            {
                selectors.Add(selector);
            }
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
                    path.EndsWith(".uss", StringComparison.OrdinalIgnoreCase) == false)
                {
                    errors.Add($"USS audit path must be an Assets-relative .uss path: {path}");
                }
                else if (File.Exists(MCPUIToolkitAuditUtility.ToFullPath(path)) == false)
                {
                    errors.Add($"USS asset does not exist: {path}");
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

        private static XAttribute GetAttribute(XElement element, string name)
        {
            return element.Attributes().FirstOrDefault(attribute =>
                string.Equals(attribute.Name.LocalName, name,
                    StringComparison.OrdinalIgnoreCase));
        }

        private static string GetAttributeValue(XElement element, string name)
        {
            XAttribute attribute = GetAttribute(element, name);
            return attribute != null ? attribute.Value.Trim() : "";
        }

        private static int GetLineNumber(string text, int characterIndex)
        {
            var line = 1;
            var length = Math.Min(Math.Max(characterIndex, 0), text?.Length ?? 0);
            for (var index = 0; index < length; index++)
            {
                if (text[index] == '\n')
                {
                    line++;
                }
            }

            return line;
        }

        private static int GetColumnNumber(string text, int characterIndex)
        {
            int index = Math.Min(Math.Max(characterIndex, 0), text?.Length ?? 0);
            int previousLineBreak = index > 0 ? text.LastIndexOf('\n', index - 1) : -1;
            return index - previousLineBreak;
        }

        private static int GetLineNumber(XObject value)
        {
            var lineInfo = value as IXmlLineInfo;
            return lineInfo != null && lineInfo.HasLineInfo() ? lineInfo.LineNumber : 1;
        }

        private static int GetColumnNumber(XObject value)
        {
            var lineInfo = value as IXmlLineInfo;
            return lineInfo != null && lineInfo.HasLineInfo() ? lineInfo.LinePosition : 1;
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

        private sealed class UssRule
        {
            public string AssetPath;
            public int Line;
            public List<string> Selectors = new List<string>();
            public Dictionary<string, string> Declarations =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public string SuppressionReason;
            public string RedundantDeclarationSuppressionReason;
            public string PixelGridSuppressionReason;
            public string TextStyleContractSuppressionReason;
        }

        private sealed class UssSimpleSelector
        {
            public string Text;
            public string TypeName;
            public string Id;
            public int Specificity;
            public readonly List<string> ClassNames = new List<string>();

            public bool Matches(UssAuthoredElement element)
            {
                if (string.IsNullOrWhiteSpace(TypeName) == false &&
                    string.Equals(TypeName, element.TypeName,
                        StringComparison.Ordinal) == false)
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(Id) == false &&
                    string.Equals(Id, element.Name, StringComparison.Ordinal) == false)
                {
                    return false;
                }

                return ClassNames.All(element.Classes.Contains);
            }
        }

        private sealed class UssAuthoredElement
        {
            public string TypeName;
            public string Name;
            public string Text;
            public int Line;
            public int Column;
            public UssAuthoredElement Parent;
            public readonly HashSet<string> Classes =
                new HashSet<string>(StringComparer.Ordinal);
            public readonly Dictionary<string, string> InlineDeclarations =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public readonly List<UssAuthoredElement> Children =
                new List<UssAuthoredElement>();
        }

        private sealed class UssAuthoredElementUsage
        {
            public readonly UssCascadeDocument Document;
            public readonly UssAuthoredElement Element;

            public UssAuthoredElementUsage(UssCascadeDocument document,
                UssAuthoredElement element)
            {
                Document = document;
                Element = element;
            }
        }

        private sealed class UssAuthoredDocument
        {
            public readonly string AssetPath;
            public readonly List<string> StylePaths = new List<string>();
            public readonly List<UssAuthoredElement> Elements =
                new List<UssAuthoredElement>();

            public UssAuthoredDocument(string assetPath, XDocument document)
            {
                AssetPath = assetPath;
                foreach (var styleElement in document.Descendants()
                             .Where(element => string.Equals(element.Name.LocalName, "Style",
                                 StringComparison.OrdinalIgnoreCase)))
                {
                    var stylePath = ResolveStyleReference(
                        GetAttributeValue(styleElement, "src"), assetPath);
                    if (string.IsNullOrWhiteSpace(stylePath) == false)
                    {
                        StylePaths.Add(stylePath);
                    }
                }

                var authoredByElement = new Dictionary<XElement, UssAuthoredElement>();
                foreach (var element in document.Descendants().Where(element =>
                             IsAuthoredVisualElement(element)))
                {
                    var authored = new UssAuthoredElement
                    {
                        TypeName = element.Name.LocalName,
                        Name = GetAttributeValue(element, "name"),
                        Text = GetAttributeValue(element, "text"),
                        Line = GetLineNumber(element),
                        Column = GetColumnNumber(element)
                    };
                    foreach (var className in SplitWhitespace(
                                 GetAttributeValue(element, "class")))
                    {
                        authored.Classes.Add(className);
                    }

                    foreach (var declaration in ParseDeclarations(
                                 GetAttributeValue(element, "style")))
                    {
                        authored.InlineDeclarations[declaration.Key] = declaration.Value;
                    }

                    Elements.Add(authored);
                    authoredByElement[element] = authored;
                }

                foreach (var pair in authoredByElement)
                {
                    var parentElement = pair.Key.Parent;
                    while (parentElement != null)
                    {
                        if (authoredByElement.TryGetValue(parentElement,
                                out var authoredParent))
                        {
                            pair.Value.Parent = authoredParent;
                            authoredParent.Children.Add(pair.Value);
                            break;
                        }

                        parentElement = parentElement.Parent;
                    }
                }
            }

            private static bool IsAuthoredVisualElement(XElement element)
            {
                switch (element.Name.LocalName)
                {
                    case "UXML":
                    case "Style":
                    case "Template":
                    case "AttributeOverrides":
                        return false;
                    default:
                        return true;
                }
            }
        }

        private sealed class UssCascadeRule
        {
            public UssRule Rule;
            public string SelectorText;
            public UssSimpleSelector Selector;
            public int Origin;
            public int SourceOrder;
        }

        private sealed class UssResolvedDeclaration
        {
            public UssRule Rule;
            public string SelectorText;
            public string Value;
            public int Origin;
            public int Specificity;
            public int SourceOrder;
        }

        private sealed class UssCascadeDocument
        {
            private int sourceOrder;

            public readonly UssAuthoredDocument AuthoredDocument;
            public readonly List<UssCascadeRule> Rules = new List<UssCascadeRule>();
            public readonly HashSet<string> LoadedAssetPaths =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public UssCascadeDocument(UssAuthoredDocument authoredDocument)
            {
                AuthoredDocument = authoredDocument;
            }

            public int NextSourceOrder()
            {
                return sourceOrder++;
            }

            public UssResolvedDeclaration Resolve(UssAuthoredElement element,
                string property, UssRule excludedRule)
            {
                UssResolvedDeclaration winner = null;
                foreach (var contextualRule in Rules)
                {
                    if (contextualRule.Selector == null ||
                        ReferenceEquals(contextualRule.Rule, excludedRule) ||
                        contextualRule.Selector.Matches(element) == false ||
                        contextualRule.Rule.Declarations.TryGetValue(property,
                            out var value) == false)
                    {
                        continue;
                    }

                    if (winner != null &&
                        (winner.Origin > contextualRule.Origin ||
                         winner.Origin == contextualRule.Origin &&
                         winner.Specificity > contextualRule.Selector.Specificity ||
                         winner.Origin == contextualRule.Origin &&
                         winner.Specificity == contextualRule.Selector.Specificity &&
                         winner.SourceOrder > contextualRule.SourceOrder))
                    {
                        continue;
                    }

                    winner = new UssResolvedDeclaration
                    {
                        Rule = contextualRule.Rule,
                        SelectorText = contextualRule.SelectorText,
                        Value = value,
                        Origin = contextualRule.Origin,
                        Specificity = contextualRule.Selector.Specificity,
                        SourceOrder = contextualRule.SourceOrder
                    };
                }

                return winner;
            }

            public bool HasUnsupportedCompetingDeclaration(string property,
                string targetValue)
            {
                return Rules.Any(contextualRule =>
                    contextualRule.Selector == null &&
                    IsDynamicStateSelector(contextualRule.SelectorText) == false &&
                    contextualRule.Rule.Declarations.TryGetValue(property, out var value) &&
                    StyleValuesEqual(value, targetValue) == false);
            }
        }

        private sealed class UssCascadeIndex
        {
            public readonly List<UssCascadeDocument> Documents =
                new List<UssCascadeDocument>();
        }

        private sealed class UssUsageIndex
        {
            private readonly Dictionary<string, List<UssUsageLocation>> classUsages =
                new Dictionary<string, List<UssUsageLocation>>(StringComparer.Ordinal);

            private readonly Dictionary<string, List<UssUsageLocation>> idUsages =
                new Dictionary<string, List<UssUsageLocation>>(StringComparer.Ordinal);

            private readonly Dictionary<string, List<UssUsageLocation>> runtimeClassReferences =
                new Dictionary<string, List<UssUsageLocation>>(StringComparer.Ordinal);

            private readonly Dictionary<string, HashSet<string>> classUsageNames =
                new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            public readonly HashSet<string> AllClassTokens =
                new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> AllIdTokens =
                new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> ComplexClassTokens =
                new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> ComplexIdTokens =
                new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> PseudoIdTokens =
                new HashSet<string>(StringComparer.Ordinal);
            public readonly List<UssAuthoredDocument> Documents =
                new List<UssAuthoredDocument>();
            public int IndexedUxmlCount;
            public int IndexedRuntimeSourceCount;

            public void AddDocument(string assetPath, XDocument document)
            {
                Documents.Add(new UssAuthoredDocument(assetPath, document));
            }

            public void AddClassUsage(string token, string path, int line, string elementName = "")
            {
                AddClassUsage(token, path, line, 0, elementName);
            }

            public void AddClassUsage(string token, string path, int line, int column,
                string elementName = "")
            {
                AddLocation(classUsages, token, path, line, column);
                if (string.IsNullOrWhiteSpace(elementName))
                {
                    return;
                }

                if (classUsageNames.TryGetValue(token, out var names) == false)
                {
                    names = new HashSet<string>(StringComparer.Ordinal);
                    classUsageNames[token] = names;
                }

                names.Add(elementName);
            }

            public void AddIdUsage(string token, string path, int line, int column = 0)
            {
                AddLocation(idUsages, token, path, line, column);
            }

            public void AddRuntimeClassReference(string token, string path, int line,
                int column = 0)
            {
                AddLocation(runtimeClassReferences, token, path, line, column);
            }

            public IReadOnlyList<UssUsageLocation> GetClassUsages(string token)
            {
                return GetLocations(classUsages, token);
            }

            public IReadOnlyList<UssUsageLocation> GetIdUsages(string token)
            {
                return GetLocations(idUsages, token);
            }

            public IReadOnlyList<UssUsageLocation> GetRuntimeClassReferences(string token)
            {
                return GetLocations(runtimeClassReferences, token);
            }

            public string GetSingleClassUsageName(string token)
            {
                if (GetClassUsages(token).Count != 1 ||
                    classUsageNames.TryGetValue(token, out var names) == false ||
                    names.Count != 1)
                {
                    return "";
                }

                return names.First();
            }

            private static void AddLocation(IDictionary<string, List<UssUsageLocation>> locations, string token,
                string path, int line, int column)
            {
                if (locations.TryGetValue(token, out var values) == false)
                {
                    values = new List<UssUsageLocation>();
                    locations[token] = values;
                }

                if (values.Any(value => value.Path == path && value.Line == line &&
                                        value.Column == column) == false)
                {
                    values.Add(new UssUsageLocation(path, line, column));
                }
            }

            private static IReadOnlyList<UssUsageLocation> GetLocations(
                IReadOnlyDictionary<string, List<UssUsageLocation>> locations, string token)
            {
                return locations.TryGetValue(token, out var values)
                    ? values
                    : Array.Empty<UssUsageLocation>();
            }
        }
    }

    internal sealed class MCPUssStyleAuditReport
    {
        private readonly int maxIssues;
        private int activeIssueCount;
        private int suppressedIssueCount;
        private bool truncated;

        public readonly List<MCPUssStyleAuditIssue> Issues =
            new List<MCPUssStyleAuditIssue>();
        public readonly List<string> Errors = new List<string>();
        public int ScannedStyleSheetCount;
        public int IndexedStyleSheetCount;
        public int IndexedUxmlCount;
        public int IndexedRuntimeSourceCount;

        public MCPUssStyleAuditReport(int maxIssues)
        {
            this.maxIssues = maxIssues;
        }

        public int WarningCount => activeIssueCount;
        public int SuppressedCount => suppressedIssueCount;
        public bool Passed => Errors.Count == 0 && WarningCount == 0;

        public void Record(MCPUssStyleAuditIssue issue, bool includeSuppressed)
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

                var lineComparison = left.Line.CompareTo(right.Line);
                if (lineComparison != 0)
                {
                    return lineComparison;
                }

                var selectorComparison = string.Compare(left.Selector, right.Selector,
                    StringComparison.Ordinal);
                return selectorComparison != 0
                    ? selectorComparison
                    : string.Compare(left.Kind, right.Kind, StringComparison.Ordinal);
            });
        }

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "success", Errors.Count == 0 },
                { "passed", Passed },
                { "scannedStyleSheets", ScannedStyleSheetCount },
                { "indexedStyleSheets", IndexedStyleSheetCount },
                { "indexedUxmlFiles", IndexedUxmlCount },
                { "indexedRuntimeSources", IndexedRuntimeSourceCount },
                { "warningCount", WarningCount },
                { "suppressedCount", SuppressedCount },
                { "truncated", truncated },
                { "issues", Issues.Select(issue => issue.ToDictionary()).ToList() },
                { "errors", Errors.ToList() },
                { "suppressionSyntax",
                    $"/* {MCPUssStyleAuditor.SUPPRESSION_MARKER} <reason> */" },
                { "redundantDeclarationSuppressionSyntax",
                    $"/* {MCPUssStyleAuditor.REDUNDANT_DECLARATION_SUPPRESSION_MARKER} " +
                    "<reason> */" },
                { "pixelGridSuppressionSyntax",
                    $"/* {MCPUssStyleAuditor.PIXEL_GRID_SUPPRESSION_MARKER} <reason> */" },
                { "textStyleContractSuppressionSyntax",
                    $"/* {MCPUssStyleAuditor.TEXT_STYLE_CONTRACT_SUPPRESSION_MARKER} " +
                    "<reason> */" }
            };
        }
    }

    internal sealed class MCPUssStyleAuditIssue
    {
        public string AssetPath;
        public int Line;
        public string Selector;
        public string Token;
        public string Kind;
        public string Property;
        public string Value;
        public int GridStep;
        public Dictionary<string, string> OffGridDeclarations =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> RelatedDeclarations =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public int AuthoredUsageCount;
        public int RuntimeReferenceCount;
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
                { "selector", Selector },
                { "token", Token },
                { "kind", Kind },
                { "authoredUsageCount", AuthoredUsageCount },
                { "runtimeReferenceCount", RuntimeReferenceCount },
                { "usageLocations", UsageLocations },
                { "suppressed", Suppressed },
                { "suppressionReason", SuppressionReason ?? "" },
                { "message", Message }
            };
            if (string.IsNullOrWhiteSpace(Property) == false)
            {
                result["property"] = Property;
                result["value"] = Value ?? "";
                result["stylesheetRules"] = StylesheetRules;
            }
            else if (RelatedDeclarations.Count > 0)
            {
                result["declarations"] =
                    new Dictionary<string, string>(RelatedDeclarations,
                        StringComparer.OrdinalIgnoreCase);
            }
            else if (string.Equals(Kind, "off-grid-pixel-declarations",
                         StringComparison.Ordinal))
            {
                result["gridStep"] = GridStep;
                result["offGridDeclarations"] =
                    new Dictionary<string, string>(OffGridDeclarations,
                        StringComparer.OrdinalIgnoreCase);
            }

            return result;
        }
    }

    internal readonly struct UssUsageLocation
    {
        public readonly string Path;
        public readonly int Line;
        public readonly int Column;

        public UssUsageLocation(string path, int line, int column)
        {
            Path = path;
            Line = line;
            Column = column;
        }

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "path", Path },
                { "line", Line },
                { "column", Column }
            };
        }
    }
}
#endif
