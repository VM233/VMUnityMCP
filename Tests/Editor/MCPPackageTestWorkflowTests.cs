using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.Compilation;

namespace UnityMCP.Editor.Tests
{
    public sealed class MCPPackageTestWorkflowTests
    {
        [Test]
        public void CompilationDiagnostics_PersistAcrossReloadAndErrorFilterStillReturnsDeprecatedWarnings()
        {
            FieldInfo bufferField = typeof(MCPConsoleCommands).GetField("_compilationErrors",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo captureMethod = typeof(MCPConsoleCommands).GetMethod("OnAssemblyCompilationFinished",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo persistMethod = typeof(MCPConsoleCommands).GetMethod("PersistCompilationDiagnostics",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo restoreMethod = typeof(MCPConsoleCommands).GetMethod("RestoreCompilationDiagnostics",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(bufferField, Is.Not.Null);
            Assert.That(captureMethod, Is.Not.Null);
            Assert.That(persistMethod, Is.Not.Null);
            Assert.That(restoreMethod, Is.Not.Null);

            var buffer = (IList)bufferField.GetValue(null);
            object[] previousEntries;
            lock (buffer)
            {
                previousEntries = buffer.Cast<object>().ToArray();
                buffer.Clear();
            }

            try
            {
                captureMethod.Invoke(null, new object[]
                {
                    "Library/ScriptAssemblies/Assembly-CSharp.dll",
                    new[]
                    {
                        new CompilerMessage
                        {
                            file = "Assets/Deprecated.cs",
                            line = 12,
                            column = 8,
                            message = "warning CS0618: 'LegacyApi' is obsolete: 'Use CurrentApi.'",
                            type = CompilerMessageType.Warning,
                        },
                        new CompilerMessage
                        {
                            file = "Assets/Unused.cs",
                            line = 5,
                            column = 17,
                            message = "warning CS0168: The variable 'unused' is declared but never used",
                            type = CompilerMessageType.Warning,
                        },
                        new CompilerMessage
                        {
                            file = "Assets/Broken.cs",
                            line = 3,
                            column = 1,
                            message = "error CS1002: ; expected",
                            type = CompilerMessageType.Error,
                        },
                    },
                });
                lock (buffer)
                    buffer.Clear();
                restoreMethod.Invoke(null, null);

                var result = (Dictionary<string, object>)MCPConsoleCommands.GetCompilationErrors(
                    new Dictionary<string, object>
                    {
                        { "severity", "error" },
                        { "count", 50 },
                    });
                var entries = (List<Dictionary<string, object>>)result["entries"];
                var deprecatedWarnings =
                    (List<Dictionary<string, object>>)result["deprecatedWarnings"];
                var counts = (Dictionary<string, object>)result["counts"];

                Assert.That(entries, Has.Count.EqualTo(1));
                Assert.That(entries[0]["code"], Is.EqualTo("CS1002"));
                Assert.That(Convert.ToInt32(counts["errors"]), Is.EqualTo(1));
                Assert.That(Convert.ToInt32(counts["warnings"]), Is.EqualTo(2));
                Assert.That(deprecatedWarnings, Has.Count.EqualTo(1));
                Assert.That(deprecatedWarnings[0]["code"], Is.EqualTo("CS0618"));
                Assert.That(deprecatedWarnings[0]["isDeprecated"], Is.EqualTo(true));
                Assert.That(result.ContainsKey("totalCount"), Is.False);
                Assert.That(result.ContainsKey("errorCount"), Is.False);
                Assert.That(result.ContainsKey("warningCount"), Is.False);
                Assert.That(result.ContainsKey("deprecatedWarningCount"), Is.False);
                Assert.That(result.ContainsKey("hasErrors"), Is.False);
                Assert.That(result.ContainsKey("hasWarnings"), Is.False);
                Assert.That(result.ContainsKey("hasDeprecatedWarnings"), Is.False);
                Assert.That(result.ContainsKey("count"), Is.False);
                Assert.That(result.ContainsKey("severityFilter"), Is.False);
                Assert.That(result.ContainsKey("deprecatedWarningsTruncated"), Is.False);
            }
            finally
            {
                lock (buffer)
                {
                    buffer.Clear();
                    foreach (object previousEntry in previousEntries)
                        buffer.Add(previousEntry);
                }
                persistMethod.Invoke(null, null);
            }
        }

        [Test]
        public void CompilationErrors_AreReportedWhileWaitingForAssemblies()
        {
            MethodInfo method = typeof(MCPPackageTestCommands).GetMethod("TryBuildCompilationFailure",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            var result = new Dictionary<string, object>
            {
                { "entries", new List<Dictionary<string, object>>
                    {
                        new()
                        {
                            { "severity", "warning" },
                            { "assembly", "Unrelated.Tests" },
                            { "message", "warning" },
                        },
                        new()
                        {
                            { "severity", "error" },
                            { "assembly", "Broken.Package.Tests" },
                            { "file", "Tests/BrokenTest.cs" },
                            { "message", "CS1002: ; expected" },
                        },
                    }
                },
            };
            object[] arguments = { result, null };

            bool failed = (bool)method.Invoke(null, arguments);

            Assert.That(failed, Is.True);
            Assert.That(arguments[1], Does.Contain("Package test assemblies failed to compile"));
            Assert.That(arguments[1], Does.Contain("Broken.Package.Tests"));
            Assert.That(arguments[1], Does.Contain("CS1002"));
            Assert.That(arguments[1], Does.Not.Contain("warning"));
        }

        [Test]
        public void NoCompilationErrors_KeepsWaitingForAssemblies()
        {
            MethodInfo method = typeof(MCPPackageTestCommands).GetMethod("TryBuildCompilationFailure",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            var result = new Dictionary<string, object>
            {
                { "entries", new List<Dictionary<string, object>>() },
            };
            object[] arguments = { result, null };

            bool failed = (bool)method.Invoke(null, arguments);

            Assert.That(failed, Is.False);
            Assert.That(arguments[1], Is.Null);
        }

        [Test]
        public void CompiledTestAssemblyArtifact_IsReadyWithoutLoadingIntoDefaultAppDomain()
        {
            MethodInfo method = typeof(MCPPackageTestCommands).GetMethod(
                "AreRequestedAssembliesAvailable", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            bool available = (bool)method.Invoke(null, new object[]
            {
                new[] { "Example.Package.Editor.Tests" },
                Array.Empty<string>(),
                new[] { "Example.Package.Editor.Tests" },
            });

            Assert.That(available, Is.True,
                "Unity Test Runner assemblies may have a compiled artifact without loading into the default AppDomain.");
        }

        [Test]
        public void MissingCompiledAssemblyArtifact_IsNotReady()
        {
            MethodInfo method = typeof(MCPPackageTestCommands).GetMethod(
                "IsCompiledAssemblyOutputAvailable", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            string missingPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                $"unity-mcp-missing-{Guid.NewGuid():N}.dll");
            bool missingAvailable = (bool)method.Invoke(null, new object[] { missingPath });
            bool loadedTestAssemblyAvailable = (bool)method.Invoke(null,
                new object[] { typeof(MCPPackageTestWorkflowTests).Assembly.Location });

            Assert.That(missingAvailable, Is.False,
                "A compilation-graph entry without an emitted DLL must not start Test Runner early.");
            Assert.That(loadedTestAssemblyAvailable, Is.True);
        }

        [Test]
        public void MissingRequestedTestAssembly_RemainsUnavailable()
        {
            MethodInfo method = typeof(MCPPackageTestCommands).GetMethod(
                "AreRequestedAssembliesAvailable", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            bool available = (bool)method.Invoke(null, new object[]
            {
                new[] { "Missing.Package.Tests" },
                new[] { "Unrelated.Loaded" },
                new[] { "Unrelated.Compiled" },
            });

            Assert.That(available, Is.False);
        }

        [Test]
        public void UndeclaredRequestedAssembly_FailsBeforeWaiting()
        {
            MethodInfo method = typeof(MCPPackageTestCommands).GetMethod(
                "TryValidateRequestedAssemblyNames",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            object[] arguments =
            {
                new[] { "UnityMCP.Editor.Tests" },
                new[] { "VMUnityMCP.Editor.Tests" },
                null,
            };

            bool valid = (bool)method.Invoke(null, arguments);

            Assert.That(valid, Is.False);
            Assert.That(arguments[2], Does.Contain("UnityMCP.Editor.Tests"));
            Assert.That(arguments[2],
                Does.Contain("VMUnityMCP.Editor.Tests"));
            Assert.That(arguments[2], Does.Contain("asmdef"));
        }

        [Test]
        public void DeclaredRequestedAssembly_IsAccepted()
        {
            MethodInfo method = typeof(MCPPackageTestCommands).GetMethod(
                "TryValidateRequestedAssemblyNames",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            object[] arguments =
            {
                new[] { "VMUnityMCP.Editor.Tests" },
                new[]
                {
                    "VMUnityMCP.Editor",
                    "VMUnityMCP.Editor.Tests",
                },
                null,
            };

            bool valid = (bool)method.Invoke(null, arguments);

            Assert.That(valid, Is.True);
            Assert.That(arguments[2], Is.Null);
        }

        [Test]
        public void ActiveWorkflow_BlocksConcurrentManifestMutation()
        {
            FieldInfo workflowField = typeof(MCPPackageTestCommands).GetField("_workflow",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo method = typeof(MCPPackageTestCommands).GetMethod("TryGetActiveWorkflow",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(workflowField, Is.Not.Null);
            Assert.That(method, Is.Not.Null);

            object original = workflowField.GetValue(null);
            object active = Activator.CreateInstance(workflowField.FieldType, true);
            workflowField.FieldType.GetField("WorkflowId")?.SetValue(active, "workflow-123");
            workflowField.FieldType.GetField("PackageName")?.SetValue(active, "com.example.tests");
            workflowField.FieldType.GetField("State")?.SetValue(active, "running");

            try
            {
                workflowField.SetValue(null, active);
                object[] arguments = { null, null, null };

                bool hasActiveWorkflow = (bool)method.Invoke(null, arguments);

                Assert.That(hasActiveWorkflow, Is.True);
                Assert.That(arguments[0], Is.EqualTo("workflow-123"));
                Assert.That(arguments[1], Is.EqualTo("com.example.tests"));
                Assert.That(arguments[2], Is.EqualTo("running"));
            }
            finally
            {
                workflowField.SetValue(null, original);
            }
        }

        [Test]
        public void ExplicitFilters_WithNoMatchedTests_FailInsteadOfReportingSuccess()
        {
            Type jobType = typeof(MCPTestRunnerCommands).GetNestedType("TestJob",
                BindingFlags.NonPublic);
            MethodInfo finalize = typeof(MCPTestRunnerCommands).GetMethod("FinalizeJob",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(jobType, Is.Not.Null);
            Assert.That(finalize, Is.Not.Null);

            object job = Activator.CreateInstance(jobType, true);
            jobType.GetField("JobId")?.SetValue(job, "zero-match-job");
            jobType.GetField("HasExplicitFilters")?.SetValue(job, true);
            jobType.GetField("TotalTests")?.SetValue(job, 0);
            jobType.GetField("FailedCount")?.SetValue(job, 0);

            finalize.Invoke(null, new[] { job, (object)0d, false });

            Assert.That(jobType.GetField("Status")?.GetValue(job)?.ToString(), Is.EqualTo("Failed"));
            Assert.That(jobType.GetField("ErrorCode")?.GetValue(job), Is.EqualTo("no_tests_matched"));
            Assert.That(jobType.GetField("Error")?.GetValue(job)?.ToString(),
                Does.Contain("No tests matched"));
        }

        [Test]
        public void StoredPackageTestResult_OmitsDetailedRowsAndStackTraces()
        {
            MethodInfo compact = typeof(MCPPackageTestCommands).GetMethod(
                "CompactStoredTestResult", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(compact, Is.Not.Null);
            var input = new Dictionary<string, object>
            {
                { "jobId", "job-1" },
                { "status", "failed" },
                { "summary", new Dictionary<string, object> { { "failed", 1 } } },
                { "progress", new Dictionary<string, object>
                    {
                        { "failuresSoFar", new List<object>
                            {
                                new Dictionary<string, object>
                                {
                                    { "name", "BrokenTest" },
                                    { "message", "Expected true" },
                                },
                            }
                        },
                    }
                },
                { "tests", new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            { "name", "BrokenTest" },
                            { "stackTrace", "internal stack" },
                        },
                    }
                },
                { "resultOffset", 0 },
            };

            var result = (Dictionary<string, object>)compact.Invoke(null, new object[] { input });

            Assert.That(result["jobId"], Is.EqualTo("job-1"));
            Assert.That(result, Does.ContainKey("summary"));
            Assert.That(result, Does.ContainKey("progress"));
            Assert.That(result, Does.Not.ContainKey("tests"));
            Assert.That(result, Does.Not.ContainKey("resultOffset"));
            Assert.That(MiniJson.Serialize(result), Does.Not.Contain("stackTrace"));
        }
    }
}
