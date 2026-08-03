using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.Compilation;

namespace UnityMCP.Editor.Tests
{
    [Category(MCPPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class MCPPackageTestWorkflowTests
    {
        [Test]
        [Category(MCPPackageTestCommands.DefaultPackageSmokeCategory)]
        public void DefaultPackageSelection_UsesSmokeUnlessCallerSelectsARegressionScope()
        {
            CollectionAssert.AreEqual(
                new[] { MCPPackageTestCommands.DefaultPackageSmokeCategory },
                MCPPackageTestCommands.ResolvePackageTestCategories(
                    "com.vm233.unity-mcp", null, null, null));

            var fullRegression = new[]
            {
                MCPPackageTestCommands.FullPackageRegressionCategory,
            };
            Assert.That(MCPPackageTestCommands.ResolvePackageTestCategories(
                    "com.vm233.unity-mcp", null, fullRegression, null),
                Is.SameAs(fullRegression));
            Assert.That(MCPPackageTestCommands.ResolvePackageTestCategories(
                    "com.vm233.unity-mcp", new[] { "Exact.Test" }, null, null),
                Is.Null);
            Assert.That(MCPPackageTestCommands.ResolvePackageTestCategories(
                    "com.vm233.unity-mcp", null, null, new[] { "Fixture" }),
                Is.Null);
            Assert.That(MCPPackageTestCommands.ResolvePackageTestCategories(
                    "com.example.package", null, null, null),
                Is.Null);
        }

        [Test]
        [Category(MCPPackageTestCommands.DefaultPackageSmokeCategory)]
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
        [Category(MCPPackageTestCommands.DefaultPackageSmokeCategory)]
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
        public void TestRunStart_RequiresAssemblyPublicationAndLaterEditorAdoption()
        {
            Assert.That(MCPPackageTestCommands.CanStartTestRunFromAssemblyState(
                "waiting-for-assembly", assembliesAvailable: false), Is.False);
            Assert.That(MCPPackageTestCommands.CanStartTestRunFromAssemblyState(
                "waiting-for-assembly", assembliesAvailable: true), Is.False);
            Assert.That(MCPPackageTestCommands.CanStartTestRunFromAssemblyState(
                "waiting-for-editor-adoption", assembliesAvailable: false), Is.False);
            Assert.That(MCPPackageTestCommands.CanStartTestRunFromAssemblyState(
                "waiting-for-editor-adoption", assembliesAvailable: true), Is.True);
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
        [Category(MCPPackageTestCommands.DefaultPackageSmokeCategory)]
        public void AcceptedPackageCancellation_IsASuccessfulCancelOperation()
        {
            Type workflowType = typeof(MCPPackageTestCommands).GetNestedType(
                "PackageTestWorkflow", BindingFlags.NonPublic);
            MethodInfo method = typeof(MCPPackageTestCommands).GetMethod(
                "BuildAcceptedCancellationResponse",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(workflowType, Is.Not.Null);
            Assert.That(method, Is.Not.Null);

            object workflow = Activator.CreateInstance(workflowType, true);
            workflowType.GetField("WorkflowId")?.SetValue(workflow, "cancel-contract-test");
            workflowType.GetField("State")?.SetValue(workflow, "canceled");
            workflowType.GetField("PackageName")?.SetValue(workflow, "com.example.tests");
            workflowType.GetField("Mode")?.SetValue(workflow, "EditMode");
            workflowType.GetField("Assemblies")?.SetValue(workflow, Array.Empty<string>());
            workflowType.GetField("TestJobId")?.SetValue(workflow, "test-job");
            workflowType.GetField("CancelRequested")?.SetValue(workflow, true);
            workflowType.GetField("Error")?.SetValue(workflow, "Canceled by request.");
            workflowType.GetField("StartedAt")?.SetValue(workflow, DateTime.UtcNow);
            workflowType.GetField("UpdatedAt")?.SetValue(workflow, DateTime.UtcNow);

            var underlyingJob = new Dictionary<string, object>
            {
                { "success", true },
                { "status", "canceling" },
            };
            var response = (Dictionary<string, object>)method.Invoke(
                null, new object[] { workflow, underlyingJob });

            Assert.That(response["success"], Is.EqualTo(true));
            Assert.That(response["status"], Is.EqualTo("canceled"));
            Assert.That(response["cancelRequested"], Is.EqualTo(true));
            Assert.That(response["cancelMode"], Is.EqualTo("unity-test-runner"));
            Assert.That(response["underlyingJob"], Is.SameAs(underlyingJob));
            Assert.That(response, Does.Not.ContainKey("error"));
        }

        [Test]
        public void PackageWorkflowPoll_PreservesCanceledOutcomeWithoutFailingTheRead()
        {
            Type workflowType = typeof(MCPPackageTestCommands).GetNestedType(
                "PackageTestWorkflow", BindingFlags.NonPublic);
            MethodInfo method = typeof(MCPPackageTestCommands).GetMethod(
                "BuildResponse", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(workflowType, Is.Not.Null);
            Assert.That(method, Is.Not.Null);

            object workflow = Activator.CreateInstance(workflowType, true);
            workflowType.GetField("WorkflowId")?.SetValue(workflow, "canceled-workflow");
            workflowType.GetField("State")?.SetValue(workflow, "canceled");
            workflowType.GetField("PackageName")?.SetValue(workflow, "com.example.tests");
            workflowType.GetField("Mode")?.SetValue(workflow, "EditMode");
            workflowType.GetField("Assemblies")?.SetValue(workflow, Array.Empty<string>());
            workflowType.GetField("CancelRequested")?.SetValue(workflow, true);
            workflowType.GetField("Error")?.SetValue(workflow, "Canceled by request.");
            workflowType.GetField("StartedAt")?.SetValue(workflow, DateTime.UtcNow);
            workflowType.GetField("UpdatedAt")?.SetValue(workflow, DateTime.UtcNow);

            var response = (Dictionary<string, object>)method.Invoke(
                null, new[] { workflow });

            Assert.That(response["success"], Is.EqualTo(true));
            Assert.That(response["jobId"], Is.EqualTo("canceled-workflow"));
            Assert.That(response["jobType"], Is.EqualTo("package-test"));
            Assert.That(response, Does.Not.ContainKey("workflowId"));
            Assert.That(response["status"], Is.EqualTo("canceled"));
            Assert.That(response["error"], Is.EqualTo("Canceled by request."));
            Assert.That(MCPResponse.TryGetError(response, out _, out _, out _), Is.False);
        }

        [Test]
        [Category(MCPPackageTestCommands.DefaultPackageSmokeCategory)]
        public void PackageWorkflowManifestPublicationState_PersistsAndOwnsTags()
        {
            MethodInfo buildResponse = typeof(MCPPackageTestCommands).GetMethod(
                "BuildResponse", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(buildResponse, Is.Not.Null);
            var cases = new[]
            {
                new KeyValuePair<string, string>("Original", null),
                new KeyValuePair<string, string>("Modified", "manifestModified"),
                new KeyValuePair<string, string>("Restoring", "manifestModified"),
                new KeyValuePair<string, string>("Restored", "manifestRestored"),
                new KeyValuePair<string, string>("RestoreFailed", "manifestRestoreFailed"),
            };

            foreach (var pair in cases)
            {
                object workflow = CreatePackageTestWorkflow(
                    "manifest-state-contract-" + pair.Key);
                Type workflowType = workflow.GetType();
                PropertyInfo publicationProperty = workflowType.GetProperty(
                    "ManifestPublication", BindingFlags.Instance | BindingFlags.Public |
                                           BindingFlags.NonPublic);
                MethodInfo toDictionary = workflowType.GetMethod("ToDictionary");
                MethodInfo fromDictionary = workflowType.GetMethod("FromDictionary",
                    BindingFlags.Static | BindingFlags.Public);
                Assert.That(publicationProperty, Is.Not.Null);
                Assert.That(toDictionary, Is.Not.Null);
                Assert.That(fromDictionary, Is.Not.Null);
                AdvanceManifestPublication(workflow, pair.Key);

                var serialized = (Dictionary<string, object>)toDictionary.Invoke(workflow, null);
                Assert.That(serialized["manifestPublicationState"], Is.EqualTo(pair.Key));
                Assert.That(serialized, Does.Not.ContainKey("manifestChanged"));
                object reloaded = fromDictionary.Invoke(null, new object[] { serialized });
                Assert.That(publicationProperty.GetValue(reloaded).ToString(), Is.EqualTo(pair.Key));

                var response = (Dictionary<string, object>)buildResponse.Invoke(
                    null, new[] { reloaded });
                var tags = response.TryGetValue("tags", out object value)
                    ? ((IEnumerable)value).Cast<object>().Select(item => item.ToString()).ToArray()
                    : Array.Empty<string>();

                if (pair.Value == null)
                    Assert.That(tags, Is.Empty, pair.Key);
                else
                    CollectionAssert.Contains(tags, pair.Value, pair.Key);
                Assert.That(tags.Count(tag =>
                        tag.StartsWith("manifest", StringComparison.Ordinal)),
                    Is.EqualTo(pair.Value == null ? 0 : 1), pair.Key);
            }

            object restoredWorkflow = CreatePackageTestWorkflow("immutable-manifest-contract");
            Type restoredWorkflowType = restoredWorkflow.GetType();
            FieldInfo manifestPathField = restoredWorkflowType.GetField("ManifestPath");
            Assert.That(manifestPathField, Is.Not.Null);

            AdvanceManifestPublication(restoredWorkflow, "Restored");
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                $"unity-mcp-manifest-state-{Guid.NewGuid():N}.json");
            manifestPathField.SetValue(restoredWorkflow, path);

            try
            {
                var missingFileResponse = (Dictionary<string, object>)buildResponse.Invoke(
                    null, new[] { restoredWorkflow });
                System.IO.File.WriteAllText(path, "later unrelated manifest bytes");
                var changedFileResponse = (Dictionary<string, object>)buildResponse.Invoke(
                    null, new[] { restoredWorkflow });

                foreach (var response in new[] { missingFileResponse, changedFileResponse })
                {
                    var tags = ((IEnumerable)response["tags"]).Cast<object>()
                        .Select(item => item.ToString()).ToArray();
                    CollectionAssert.Contains(tags, "manifestRestored");
                    CollectionAssert.DoesNotContain(tags, "manifestModified");
                }
            }
            finally
            {
                if (System.IO.File.Exists(path))
                    System.IO.File.Delete(path);
            }

            object invalidWorkflow = CreatePackageTestWorkflow("invalid-manifest-state");
            Type invalidWorkflowType = invalidWorkflow.GetType();
            MethodInfo invalidToDictionary = invalidWorkflowType.GetMethod("ToDictionary");
            MethodInfo invalidFromDictionary = invalidWorkflowType.GetMethod("FromDictionary",
                BindingFlags.Static | BindingFlags.Public);
            Assert.That(invalidToDictionary, Is.Not.Null);
            Assert.That(invalidFromDictionary, Is.Not.Null);
            var invalidSerialized =
                (Dictionary<string, object>)invalidToDictionary.Invoke(invalidWorkflow, null);
            invalidSerialized["manifestPublicationState"] = "Unknown";
            var exception = Assert.Throws<TargetInvocationException>(() =>
                invalidFromDictionary.Invoke(null, new object[] { invalidSerialized }));
            Assert.That(exception.InnerException, Is.TypeOf<System.IO.InvalidDataException>());
        }

        [Test]
        [Category(MCPPackageTestCommands.DefaultPackageSmokeCategory)]
        public void UnityTestPoll_PreservesFailedOutcomeWithoutFailingTheRead()
        {
            Type jobType = typeof(MCPTestRunnerCommands).GetNestedType(
                "TestJob", BindingFlags.NonPublic);
            MethodInfo method = typeof(MCPTestRunnerCommands).GetMethod(
                "SerializeJob", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(jobType, Is.Not.Null);
            Assert.That(method, Is.Not.Null);

            object job = Activator.CreateInstance(jobType, true);
            jobType.GetField("JobId")?.SetValue(job, "failed-test-job");
            jobType.GetField("Status")?.SetValue(
                job, Enum.Parse(jobType.GetField("Status").FieldType, "Failed"));
            jobType.GetField("StartedAt")?.SetValue(job, DateTime.UtcNow);
            jobType.GetField("CompletedAt")?.SetValue(job, DateTime.UtcNow);
            jobType.GetField("Error")?.SetValue(job, "One test failed.");
            jobType.GetField("ErrorCode")?.SetValue(job, "test_failures");
            jobType.GetField("TotalTests")?.SetValue(job, 1);
            jobType.GetField("CompletedTests")?.SetValue(job, 1);
            jobType.GetField("FailedCount")?.SetValue(job, 1);

            var response = (Dictionary<string, object>)method.Invoke(
                null, new object[] { job, false, false, false, 0, 100, 20 });

            Assert.That(response["success"], Is.EqualTo(true));
            Assert.That(response["status"], Is.EqualTo("failed"));
            Assert.That(response["error"], Is.EqualTo("One test failed."));
            Assert.That(MCPResponse.TryGetError(response, out _, out _, out _), Is.False);
        }

        [Test]
        [Category(MCPPackageTestCommands.DefaultPackageSmokeCategory)]
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

        private static object CreatePackageTestWorkflow(string jobId)
        {
            Type workflowType = typeof(MCPPackageTestCommands).GetNestedType(
                "PackageTestWorkflow", BindingFlags.NonPublic);
            Assert.That(workflowType, Is.Not.Null);
            object workflow = Activator.CreateInstance(workflowType, true);
            workflowType.GetField("WorkflowId")?.SetValue(workflow, jobId);
            workflowType.GetField("State")?.SetValue(workflow, "succeeded");
            workflowType.GetField("PackageName")?.SetValue(workflow, "com.example.tests");
            workflowType.GetField("Mode")?.SetValue(workflow, "EditMode");
            workflowType.GetField("Assemblies")?.SetValue(workflow, Array.Empty<string>());
            workflowType.GetField("OriginalManifestBase64")?.SetValue(workflow,
                Convert.ToBase64String(Array.Empty<byte>()));
            workflowType.GetField("StartedAt")?.SetValue(workflow, DateTime.UtcNow);
            workflowType.GetField("UpdatedAt")?.SetValue(workflow, DateTime.UtcNow);
            return workflow;
        }

        private static void AdvanceManifestPublication(object workflow, string target)
        {
            switch (target)
            {
                case "Original":
                    return;
                case "Modified":
                    InvokeManifestTransition(workflow, "BeginManifestModification");
                    return;
                case "Restoring":
                    InvokeManifestTransition(workflow, "BeginManifestModification");
                    InvokeManifestTransition(workflow, "BeginManifestRestore");
                    return;
                case "Restored":
                    InvokeManifestTransition(workflow, "BeginManifestModification");
                    InvokeManifestTransition(workflow, "BeginManifestRestore");
                    InvokeManifestTransition(workflow, "MarkManifestRestored");
                    return;
                case "RestoreFailed":
                    InvokeManifestTransition(workflow, "BeginManifestModification");
                    InvokeManifestTransition(workflow, "BeginManifestRestore");
                    InvokeManifestTransition(workflow, "MarkManifestRestoreFailed");
                    return;
                default:
                    Assert.Fail($"Unknown manifest publication test target '{target}'.");
                    return;
            }
        }

        private static void InvokeManifestTransition(object workflow, string methodName)
        {
            MethodInfo method = workflow.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(workflow, null);
        }

    }
}
