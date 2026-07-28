#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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
            var report = MCPUssSingleUseStyleAuditor.Audit(
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
                var selfTests = MCPUssSingleUseStyleAuditor.RunSelfTests();
                result["selfTests"] = selfTests;
                object passed;
                if (selfTests.TryGetValue("passed", out passed) &&
                    passed is bool && !(bool)passed)
                    result["success"] = false;
            }

            return result;
        }

        [MenuItem("Tools/UI Toolkit/Audit USS Single-Use Styles")]
        private static void AuditAllFromMenu()
        {
            var options = MCPUIToolkitAuditOptions.FromProjectSettings(
                MCPUIToolkitAuditProjectSettings.Load());
            var report = MCPUssSingleUseStyleAuditor.Audit(
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

    internal static class MCPUssSingleUseStyleAuditor
    {
        internal const string SUPPRESSION_MARKER = "uss-audit: allow-single-use";

        private static readonly Regex commentRegex =
            new Regex(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex suppressionRegex =
            new Regex(@"/\*\s*uss-audit:\s*allow-single-use\s+(?<reason>.+?)\*/\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

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

        internal static MCPUssStyleAuditReport Audit(IEnumerable<string> requestedPaths,
            bool includeSuppressed, int maxIssues, MCPUIToolkitAuditOptions options)
        {
            options = options ?? MCPUIToolkitAuditOptions.FromArguments(
                new Dictionary<string, object>());
            var report = new MCPUssStyleAuditReport(maxIssues);
            var requestedPathList = (requestedPaths ?? Array.Empty<string>())
                .Where(path => string.IsNullOrWhiteSpace(path) == false).ToList();
            var requested = NormalizeRequestedPaths(requestedPathList, report.Errors);
            var allStyleSheetPaths = MCPUIToolkitAuditUtility.FindAssetFiles(".uss", options)
                .Concat(requested)
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

                AuditRules(rules, usageIndex, report, includeSuppressed);
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

            var activeTokens = report.Issues.Where(issue => issue.Suppressed == false)
                .Select(issue => issue.Token).OrderBy(token => token, StringComparer.Ordinal).ToArray();
            var suppressedTokens = report.Issues.Where(issue => issue.Suppressed)
                .Select(issue => issue.Token).OrderBy(token => token, StringComparer.Ordinal).ToArray();
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

            return new Dictionary<string, object>
            {
                { "passed", cases.All(testCase => (bool)testCase["passed"]) },
                { "cases", cases },
                { "activeTokens", activeTokens },
                { "suppressedTokens", suppressedTokens }
            };
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
                    rules.Add(new UssRule
                    {
                        AssetPath = assetPath,
                        Line = GetLineNumber(text, selectorIndex),
                        Selectors = SplitSelectors(selectorGroup),
                        SuppressionReason = suppression.Success
                            ? suppression.Groups["reason"].Value.Trim()
                            : ""
                    });
                }

                cursor = closeBrace + 1;
            }

            return rules;
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
            public string SuppressionReason;
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
            public int IndexedUxmlCount;
            public int IndexedRuntimeSourceCount;

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
                return lineComparison != 0
                    ? lineComparison
                    : string.Compare(left.Selector, right.Selector, StringComparison.Ordinal);
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
                { "scannedStyleSheets", ScannedStyleSheetCount },
                { "indexedStyleSheets", IndexedStyleSheetCount },
                { "indexedUxmlFiles", IndexedUxmlCount },
                { "indexedRuntimeSources", IndexedRuntimeSourceCount },
                { "warningCount", WarningCount },
                { "suppressedCount", SuppressedCount },
                { "truncated", WarningCount > returnedActive ||
                               returnedSuppressed > 0 && SuppressedCount > returnedSuppressed },
                { "issues", Issues.Select(issue => issue.ToDictionary()).ToList() },
                { "errors", Errors.ToList() },
                { "suppressionSyntax",
                    $"/* {MCPUssSingleUseStyleAuditor.SUPPRESSION_MARKER} <reason> */" }
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
        public int AuthoredUsageCount;
        public int RuntimeReferenceCount;
        public List<Dictionary<string, object>> UsageLocations =
            new List<Dictionary<string, object>>();
        public bool Suppressed;
        public string SuppressionReason;
        public string Message;

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
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
