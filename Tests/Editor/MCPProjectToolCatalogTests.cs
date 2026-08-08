using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace UnityMCP.Editor.Tests
{
    [Category(MCPPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class MCPProjectToolCatalogTests
    {
        private const string RUNTIME_MUTATION_TOOL_NAME = "unity-mcp-tests/set-runtime-state";
        private const string PROJECT_FILE_MUTATION_TOOL_NAME = "unity-mcp-tests/write-project-report";
        private const string READ_ONLY_PROJECT_FILE_WRITE_TOOL_NAME =
            "unity-mcp-tests/read-only-project-file-write";
        private const string READ_STATE_TOOL_NAME = "unity-mcp-tests/read-state";
        private const string NESTED_SCHEMA_TOOL_NAME = "unity-mcp-tests/validate-nested-schema";
        private const string STRICT_COMBINATOR_SCHEMA_TOOL_NAME =
            "unity-mcp-tests/validate-strict-combinator-schema";
        private const string MISSING_OPERATION_KIND_TOOL_NAME =
            "unity-mcp-tests/missing-operation-kind";
        private const string MINIMAL_SCHEMA_TOOL_NAME = "unity-mcp-tests/minimal-schema";
        private const string PERSISTENT_PROJECT_TOOL_NAME = "unity-mcp-tests/persistent-step";
        private const string PERSISTENT_PROJECT_TOOL_CLEANUP_NAME =
            "unity-mcp-tests/persistent-step-cleanup";
        private static int runtimeMutationInvocationCount;
        [MCPProjectTool(RUNTIME_MUTATION_TOOL_NAME,
            Description = "Regression fixture for explicit runtime mutation metadata.",
            InputSchemaJson = "{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}",
            MutatesRuntime = true,
            RequiresPlayMode = true)]
        private static object SetRuntimeStateFixture(Dictionary<string, object> args)
        {
            runtimeMutationInvocationCount++;
            return new Dictionary<string, object>
            {
                { "success", true },
                { "receivedKeys", args.Keys.OrderBy(key => key).ToArray() }
            };
        }

        [MCPProjectTool(PROJECT_FILE_MUTATION_TOOL_NAME,
            Description = "Regression fixture for explicit project-file mutation metadata.",
            InputSchemaJson = "{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}",
            MutatesProjectFiles = true)]
        private static object WriteProjectReportFixture(Dictionary<string, object> args)
        {
            return new Dictionary<string, object>
            {
                { "success", true },
                { "receivedKeys", args.Keys.OrderBy(key => key).ToArray() }
            };
        }

        [MCPProjectTool(READ_ONLY_PROJECT_FILE_WRITE_TOOL_NAME,
            Description = "Regression fixture for rejecting read-only file writes.",
            InputSchemaJson = "{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}",
            SideEffects = MCPProjectToolSideEffect.WritesProjectFiles,
            ReadOnly = true)]
        private static object ReadOnlyProjectFileWriteFixture(Dictionary<string, object> args)
        {
            return new Dictionary<string, object> { { "success", true } };
        }

        [MCPProjectTool(READ_STATE_TOOL_NAME,
            Description = "Regression fixture for a canonical read-only project tool.",
            InputSchemaJson = "{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}",
            ReadOnly = true)]
        private static object ReadStateFixture(Dictionary<string, object> args)
        {
            return new Dictionary<string, object>
            {
                { "success", true },
                { "receivedKeys", args.Keys.OrderBy(key => key).ToArray() }
            };
        }

        [MCPProjectTool(NESTED_SCHEMA_TOOL_NAME,
            Description = "Regression fixture for recursive project-tool schema validation.",
            InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"config\":{\"type\":\"object\",\"description\":\"Nested validation configuration.\",\"properties\":{\"mode\":{\"type\":\"string\",\"description\":\"Validation mode.\",\"enum\":[\"safe\",\"fast\"]},\"values\":{\"type\":\"array\",\"description\":\"One or two integer values.\",\"minItems\":1,\"maxItems\":2,\"items\":{\"type\":\"integer\"}},\"choice\":{\"description\":\"String or integer choice.\",\"anyOf\":[{\"type\":\"string\"},{\"type\":\"integer\"}]}},\"required\":[\"mode\",\"values\"],\"additionalProperties\":false}},\"required\":[\"config\"],\"additionalProperties\":false}",
            ReadOnly = true)]
        private static object ValidateNestedSchemaFixture(Dictionary<string, object> args)
        {
            return new Dictionary<string, object>
            {
                { "success", true },
                { "received", args }
            };
        }

        [MCPProjectTool(STRICT_COMBINATOR_SCHEMA_TOOL_NAME,
            Description = "Regression fixture for project-tool not and const schema validation.",
            InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"enabled\":{\"type\":\"boolean\",\"description\":\"Required enabled state.\",\"const\":true},\"blocked\":{\"type\":\"string\",\"description\":\"Forbidden marker.\"}},\"required\":[\"enabled\"],\"not\":{\"required\":[\"blocked\"]},\"additionalProperties\":false}",
            ReadOnly = true)]
        private static object ValidateStrictCombinatorSchemaFixture(Dictionary<string, object> args)
        {
            return new Dictionary<string, object>
            {
                { "success", true },
                { "received", args }
            };
        }

        [MCPProjectTool(MISSING_OPERATION_KIND_TOOL_NAME,
            Description = "Regression fixture for mandatory operation-kind metadata.",
            InputSchemaJson = "{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}")]
        private static object MissingOperationKindFixture(Dictionary<string, object> args)
        {
            return new Dictionary<string, object> { { "success", true } };
        }

        [MCPProjectTool(MINIMAL_SCHEMA_TOOL_NAME,
            Description = "Regression fixture for a valid minimal project-tool schema.",
            InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"values\":{\"type\":\"array\",\"description\":\"Integer values supplied to the fixture.\",\"items\":{\"type\":\"integer\"}}},\"additionalProperties\":false}",
            ReadOnly = true)]
        private static object MinimalSchemaFixture(Dictionary<string, object> args)
        {
            return new Dictionary<string, object> { { "success", true } };
        }

        [MCPProjectTool(PERSISTENT_PROJECT_TOOL_NAME,
            Description = "Regression fixture for resumable persistent project-tool jobs.",
            InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"requiredSteps\":{\"type\":\"integer\",\"description\":\"Number of yielded steps before completion.\"},\"value\":{\"type\":\"integer\",\"description\":\"Value returned when the job completes.\"}},\"required\":[\"requiredSteps\",\"value\"],\"additionalProperties\":false}",
            OutputSchemaJson = "{\"type\":\"object\",\"properties\":{\"value\":{\"type\":\"integer\"},\"stepCount\":{\"type\":\"integer\"}},\"required\":[\"value\",\"stepCount\"],\"additionalProperties\":false}",
            CleanupToolName = PERSISTENT_PROJECT_TOOL_CLEANUP_NAME,
            SideEffects = MCPProjectToolSideEffect.ChangesRuntimeState |
                          MCPProjectToolSideEffect.CreatesTemporaryObjects,
            ErrorCodes = new[] { "fixture_failed" },
            MutatesRuntime = true,
            LongRunning = true)]
        public sealed class PersistentProjectToolFixture : IMCPPersistentProjectTool
        {
            public object Execute(Dictionary<string, object> args)
            {
                return new Dictionary<string, object>
                {
                    { "value", Convert.ToInt32(args["value"]) },
                    { "stepCount", 0 },
                };
            }

            public MCPProjectToolJobStep ExecuteJobStep(Dictionary<string, object> args,
                Dictionary<string, object> state)
            {
                int stepCount = state.TryGetValue("stepCount", out object stepValue)
                    ? Convert.ToInt32(stepValue)
                    : 0;
                int requiredSteps = Convert.ToInt32(args["requiredSteps"]);
                if (stepCount < requiredSteps)
                {
                    stepCount++;
                    return MCPProjectToolJobStep.Pending(
                        new Dictionary<string, object> { { "stepCount", stepCount } },
                        requiredSteps == 0 ? 1d : (double)stepCount / requiredSteps,
                        $"Completed step {stepCount} of {requiredSteps}.",
                        delayMilliseconds: 1,
                        cleanupToken: "fixture-cleanup-token");
                }

                return MCPProjectToolJobStep.Complete(
                    new Dictionary<string, object>
                    {
                        { "value", Convert.ToInt32(args["value"]) },
                        { "stepCount", stepCount },
                    },
                    "fixture-cleanup-token");
            }
        }

        [MCPProjectTool(PERSISTENT_PROJECT_TOOL_CLEANUP_NAME,
            Description = "Cleanup regression fixture for persistent project-tool jobs.",
            InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"action\":{\"type\":\"string\",\"description\":\"Cleanup action.\",\"const\":\"cleanup\"},\"cleanupToken\":{\"type\":\"string\",\"description\":\"Token produced by the persistent fixture.\"}},\"required\":[\"action\",\"cleanupToken\"],\"additionalProperties\":false}",
            OutputSchemaJson = "{\"type\":\"object\",\"properties\":{\"cleaned\":{\"type\":\"boolean\"}},\"required\":[\"cleaned\"],\"additionalProperties\":false}",
            MutatesRuntime = true)]
        private static object CleanupPersistentProjectToolFixture(Dictionary<string, object> args)
        {
            return new Dictionary<string, object>
            {
                { "cleaned", args["cleanupToken"].ToString() == "fixture-cleanup-token" },
            };
        }

        [SetUp]
        public void SetUp()
        {
            runtimeMutationInvocationCount = 0;
        }
        [Test]
        public void ProjectToolNamesStayReadableAndBelowClientLimit()
        {
            var method = typeof(MCPToolMetadata).GetMethod("ProjectToolNameToToolName",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);

            string runtimeName = method.Invoke(null,
                new object[] { "battleidle/get-runtime-ready-state", "" }).ToString();
            string validationName = method.Invoke(null,
                new object[] { "vmframework/validate-visual-element-paths", "" }).ToString();
            Assert.That(runtimeName, Is.EqualTo("unity_pt_battle_get_runtime_ready_state"));
            Assert.That(validationName, Is.EqualTo("unity_pt_vmf_validate_ui_el_paths"));
            Assert.That(runtimeName.Length, Is.LessThanOrEqualTo(48));
            Assert.That(validationName.Length, Is.LessThanOrEqualTo(48));
        }

        [Test]
        public void RuntimeMutatingProjectTool_PublishesCanonicalRouteMetadata()
        {
            var descriptor = MCPProjectToolCommands.GetToolDetails(validOnly: true)
                .Single(tool => tool["toolName"].ToString() == RUNTIME_MUTATION_TOOL_NAME);
            Assert.That(HasTag(descriptor, "readOnly"), Is.False);
            Assert.That(HasSideEffect(descriptor, "writesAssets"), Is.False);
            Assert.That(HasSideEffect(descriptor, "changesRuntimeState"), Is.True);
            Assert.That(HasTag(descriptor, "requiresPlayMode"), Is.True);
            Assert.That(descriptor["moduleId"], Is.EqualTo("unity-mcp-tests"));
            Assert.That(descriptor["capability"], Is.EqualTo("runtime-state"));
            CollectionAssert.Contains((ICollection)descriptor["errorCodes"],
                MCPRuntimePreconditions.PlayModeRequiredErrorCode);

            var toolsResult = RequireDictionary(MCPToolMetadata.GetRegisteredTools(
                compact: false, includeSchema: true, limit: 200,
                category: "project-tools"));
            var tools = (List<Dictionary<string, object>>)toolsResult["tools"];
            var tool = tools.Single(item =>
                item["route"].ToString() == "project-tools/call/" + RUNTIME_MUTATION_TOOL_NAME);
            Assert.That(tool["moduleId"], Is.EqualTo("unity-mcp-tests"));
            Assert.That(tool["capability"], Is.EqualTo("runtime-state"));
            Assert.That(tool["operationKind"], Is.EqualTo("mutate"));
            Assert.That(HasSideEffect(tool, "changesRuntimeState"), Is.True);
        }

        [Test]
        [Category(MCPPackageTestCommands.DefaultPackageSmokeCategory)]
        public void ProjectFileMutatingProjectTool_IsExplicitAndNotMisclassified()
        {
            var descriptor = MCPProjectToolCommands.GetToolDetails(validOnly: true)
                .Single(tool => tool["toolName"].ToString() == PROJECT_FILE_MUTATION_TOOL_NAME);
            Assert.That(HasTag(descriptor, "readOnly"), Is.False);
            Assert.That(HasSideEffect(descriptor, "writesProjectFiles"), Is.True);
            Assert.That(HasSideEffect(descriptor, "writesAssets"), Is.False);
            Assert.That(HasSideEffect(descriptor, "changesRuntimeState"), Is.False);

            var toolsResult = RequireDictionary(MCPToolMetadata.GetRegisteredTools(
                compact: false, includeSchema: true, limit: 200,
                category: "project-tools"));
            var tools = (List<Dictionary<string, object>>)toolsResult["tools"];
            var tool = tools.Single(item =>
                item["route"].ToString() == "project-tools/call/" + PROJECT_FILE_MUTATION_TOOL_NAME);
            Assert.That(tool["operationKind"], Is.EqualTo("mutate"));
            Assert.That(HasSideEffect(tool, "writesProjectFiles"), Is.True);
            Assert.That(HasSideEffect(tool, "writesAssets"), Is.False);
            Assert.That(HasSideEffect(tool, "changesRuntimeState"), Is.False);
        }

        [Test]
        public void ProjectTool_ReadOnlyCannotDeclareProjectFileWrites()
        {
            var descriptor = MCPProjectToolCommands.GetToolDetails(validOnly: false)
                .Single(tool => tool["toolName"].ToString() == READ_ONLY_PROJECT_FILE_WRITE_TOOL_NAME);
            Assert.That(HasTag(descriptor, "invalid"), Is.True);
            Assert.That(descriptor["validationError"].ToString(),
                Does.Contain("declares mutating side effects"));
        }

        [Test]
        public void ProjectTool_AllValidDescriptorsReceiveCanonicalRoutes()
        {
            var descriptor = MCPProjectToolCommands.GetToolDetails(validOnly: true)
                .Single(tool => tool["toolName"].ToString() == READ_STATE_TOOL_NAME);
            Assert.That(HasTag(descriptor, "readOnly"), Is.True);
            Assert.That(descriptor["executeRoute"],
                Is.EqualTo(MCPProjectToolCommands.GetDirectRoute(READ_STATE_TOOL_NAME)));

            var toolsResult = RequireDictionary(MCPToolMetadata.GetRegisteredTools(
                compact: false, includeSchema: true, limit: 200,
                category: "project-tools"));
            var tools = (List<Dictionary<string, object>>)toolsResult["tools"];
            Assert.That(tools.Any(item =>
                item["route"].ToString() == "project-tools/call/" + READ_STATE_TOOL_NAME), Is.True);
        }

        [Test]
        [Category(MCPPackageTestCommands.DefaultPackageSmokeCategory)]
        public void ProjectTool_MissingOperationKindIsRejectedFromEveryCatalogTier()
        {
            var descriptor = MCPProjectToolCommands.GetToolDetails(validOnly: false)
                .Single(tool => tool["toolName"].ToString() == MISSING_OPERATION_KIND_TOOL_NAME);
            Assert.That(HasTag(descriptor, "invalid"), Is.True);
            Assert.That(descriptor["validationError"].ToString(),
                Does.Contain("must explicitly declare ReadOnly, MutatesAssets, MutatesRuntime, or MutatesProjectFiles"));

            Assert.That(MCPProjectToolCommands.GetToolDetails(validOnly: true)
                .Any(tool => tool["toolName"].ToString() == MISSING_OPERATION_KIND_TOOL_NAME), Is.False);

            var toolsResult = RequireDictionary(MCPToolMetadata.GetRegisteredTools(
                compact: false, includeSchema: true, limit: 200,
                category: "project-tools"));
            var tools = (List<Dictionary<string, object>>)toolsResult["tools"];
            Assert.That(tools.Any(item =>
                item["route"].ToString() == "project-tools/call/" + MISSING_OPERATION_KIND_TOOL_NAME),
                Is.False);
        }

        [Test]
        public void ProjectTool_MinimalValidSchemaRemainsCanonicalAndExecutable()
        {
            var descriptor = MCPProjectToolCommands.GetToolDetails(validOnly: true)
                .Single(tool => tool["toolName"].ToString() == MINIMAL_SCHEMA_TOOL_NAME);
            Assert.That(HasTag(descriptor, "invalid"), Is.False);
            Assert.That(descriptor.ContainsKey("exposureWarning"), Is.False);

            var toolsResult = RequireDictionary(MCPToolMetadata.GetRegisteredTools(
                compact: false, includeSchema: true, limit: 200,
                category: "project-tools"));
            var tools = (List<Dictionary<string, object>>)toolsResult["tools"];
            Assert.That(tools.Any(item =>
                item["route"].ToString() == "project-tools/call/" + MINIMAL_SCHEMA_TOOL_NAME),
                Is.True);

            var execute = RequireDictionary(ExecuteProjectTool(
                new Dictionary<string, object>
                {
                    { "toolName", MINIMAL_SCHEMA_TOOL_NAME },
                    { "args", new Dictionary<string, object>
                        {
                            { "values", new List<object> { 1L } }
                        }
                    }
                }));
            Assert.That(execute["success"], Is.EqualTo(true));
        }

        [Test]
        [Category(MCPPackageTestCommands.DefaultPackageSmokeCategory)]
        public void ProjectTool_RecursivelyValidatesNestedSchemasAndCombinators()
        {
            Dictionary<string, object> Execute(Dictionary<string, object> toolArgs)
            {
                return RequireDictionary(ExecuteProjectTool(
                    new Dictionary<string, object>
                    {
                        { "toolName", NESTED_SCHEMA_TOOL_NAME },
                        { "args", toolArgs }
                    }));
            }

            var valid = Execute(new Dictionary<string, object>
            {
                { "config", new Dictionary<string, object>
                    {
                        { "mode", "safe" },
                        { "values", new List<object> { 1L, 2L } },
                        { "choice", "value" },
                    }
                }
            });
            Assert.That(valid["success"], Is.EqualTo(true));

            var invalid = Execute(new Dictionary<string, object>
            {
                { "config", new Dictionary<string, object>
                    {
                        { "mode", "unsupported" },
                        { "values", new List<object> { "not-an-integer", 2L, 3L } },
                        { "choice", true },
                        { "unknown", true },
                    }
                }
            });
            Assert.That(invalid["success"], Is.EqualTo(false));
            Assert.That(invalid["errorCode"], Is.EqualTo("invalid_arguments"));
            Assert.That(invalid["error"].ToString(),
                Does.Contain("$.config.mode")
                    .And.Contain("$.config.values[0]")
                    .And.Contain("at most 2 items")
                    .And.Contain("$.config.choice")
                    .And.Contain("$.config.unknown"));
        }

        [Test]
        public void ProjectTool_EnforcesNotAndConstSchemasBeforeExecution()
        {
            Dictionary<string, object> Execute(Dictionary<string, object> toolArgs)
            {
                return RequireDictionary(ExecuteProjectTool(
                    new Dictionary<string, object>
                    {
                        { "toolName", STRICT_COMBINATOR_SCHEMA_TOOL_NAME },
                        { "args", toolArgs }
                    }));
            }

            var valid = Execute(new Dictionary<string, object>
            {
                { "enabled", true }
            });
            Assert.That(valid["success"], Is.EqualTo(true));

            var invalidConst = Execute(new Dictionary<string, object>
            {
                { "enabled", false }
            });
            Assert.That(invalidConst["success"], Is.EqualTo(false));
            Assert.That(invalidConst["errorCode"], Is.EqualTo("invalid_arguments"));
            Assert.That(invalidConst["error"].ToString(),
                Does.Contain("$.enabled").And.Contain("const"));

            var invalidNot = Execute(new Dictionary<string, object>
            {
                { "enabled", true },
                { "blocked", "present" }
            });
            Assert.That(invalidNot["success"], Is.EqualTo(false));
            Assert.That(invalidNot["errorCode"], Is.EqualTo("invalid_arguments"));
            Assert.That(invalidNot["error"].ToString(), Does.Contain("not"));
        }

        [Test]
        [Category(MCPPackageTestCommands.DefaultPackageSmokeCategory)]
        public void ProjectToolCatalog_UsesSummaryDetailAndCanonicalRouteContracts()
        {
            var summaries = MCPProjectToolCommands.GetToolSummaries(validOnly: true);
            var summary = summaries.Single(tool => tool["toolName"].ToString() == READ_STATE_TOOL_NAME);

            Assert.That(summary["description"], Is.EqualTo("Regression fixture for a canonical read-only project tool."));
            Assert.That(summary.ContainsKey("inputSchema"), Is.False);
            Assert.That(summary.ContainsKey("source"), Is.False);
            Assert.That(summary.ContainsKey("route"), Is.False);
            Assert.That(summary.ContainsKey("validationError"), Is.False);

            var detail = MCPProjectToolCommands.GetToolDetails(validOnly: true)
                .Single(tool => tool["toolName"].ToString() == READ_STATE_TOOL_NAME);
            Assert.That(detail["toolName"], Is.EqualTo(READ_STATE_TOOL_NAME));
            Assert.That(detail.ContainsKey("inputSchema"), Is.True);
            Assert.That(detail.ContainsKey("source"), Is.True);
            Assert.That(detail.ContainsKey("route"), Is.False);
            Assert.That(detail.ContainsKey("directRoute"), Is.False);
            Assert.That(detail["executeRoute"],
                Is.EqualTo(MCPProjectToolCommands.GetDirectRoute(READ_STATE_TOOL_NAME)));
            Assert.That(detail.ContainsKey("enforcesInputSchema"), Is.False);

            var metadata = RequireDictionary(MCPToolMetadata.GetRegisteredTools(
                compact: true, includeSchema: true, limit: 200,
                category: "project-tools"));
            var catalogTools = (List<Dictionary<string, object>>)metadata["tools"];
            var canonicalTool = catalogTools.Single(tool =>
                tool["route"].ToString() == MCPProjectToolCommands.GetDirectRoute(READ_STATE_TOOL_NAME));
            Assert.That(canonicalTool["projectToolName"], Is.EqualTo(READ_STATE_TOOL_NAME));
            Assert.That(canonicalTool.ContainsKey("inputSchema"), Is.True);
            Assert.That(catalogTools.Any(tool => new[]
            {
                "project-tools/list", "project-tools/get", "project-tools/execute"
            }.Contains(tool["route"].ToString())), Is.False);
        }

        [Test]
        public void ProjectToolExecute_StripsProjectBindingArgumentsBeforeStrictSchemaValidation()
        {
            var response = RequireDictionary(ExecuteProjectTool(new Dictionary<string, object>
            {
                { "toolName", READ_STATE_TOOL_NAME },
                { "args", new Dictionary<string, object>
                    {
                        { "expectedProjectPath", "D:/UnityProjects/BattleIdle" },
                        { "expectedProjectName", "BattleIdle" },
                        { "targetProjectPath", "D:/UnityProjects/BattleIdle" },
                        { "targetProjectName", "BattleIdle" },
                        { "unityProjectPath", "D:/UnityProjects/BattleIdle" },
                        { "unityProjectName", "BattleIdle" },
                    }
                },
                { "expectedProjectPath", "D:/UnityProjects/BattleIdle" },
                { "expectedProjectName", "BattleIdle" },
            }));

            Assert.That(response["success"], Is.EqualTo(true));
            var result = RequireDictionary(response["result"]);
            CollectionAssert.IsEmpty((string[])result["receivedKeys"]);
        }

        [Test]
        [Category(MCPPackageTestCommands.DefaultPackageSmokeCategory)]
        public void ProjectToolExecution_EnforcesPlayModeBeforeEveryInvocationBoundary()
        {
            Assert.That(MCPRuntimePreconditions.IsStablePlayMode, Is.False,
                "This regression must execute in Edit Mode.");

            void AssertRejected(object rawResponse)
            {
                var response = RequireDictionary(rawResponse);
                Assert.That(response["success"], Is.EqualTo(false));
                Assert.That(response["errorCode"],
                    Is.EqualTo(MCPRuntimePreconditions.PlayModeRequiredErrorCode));
                Assert.That(response["toolName"], Is.EqualTo(RUNTIME_MUTATION_TOOL_NAME));
                Assert.That(response["requiresPlayMode"], Is.EqualTo(true));
                Assert.That(response["isPlaying"], Is.EqualTo(false));
            }

            object Execute(bool runAsJob = false)
            {
                var arguments = new Dictionary<string, object>
                {
                    { "toolName", RUNTIME_MUTATION_TOOL_NAME },
                    { "args", new Dictionary<string, object>() },
                };
                if (runAsJob)
                    arguments["runAsJob"] = true;
                return ExecuteProjectTool(arguments);
            }

            AssertRejected(Execute());
            AssertRejected(Execute(runAsJob: true));

            bool handled = MCPProjectToolCommands.TryExecuteDirectRoute(
                MCPProjectToolCommands.GetDirectRoute(RUNTIME_MUTATION_TOOL_NAME),
                new Dictionary<string, object>(),
                out object directResponse);
            Assert.That(handled, Is.True);
            AssertRejected(directResponse);

            AssertRejected(MCPProjectToolCommands.ExecuteJobInline(
                RUNTIME_MUTATION_TOOL_NAME,
                new Dictionary<string, object>()));

            MCPProjectToolException stepError = Assert.Throws<MCPProjectToolException>(() =>
                MCPProjectToolCommands.ExecuteJobStepInline(
                    RUNTIME_MUTATION_TOOL_NAME,
                    new Dictionary<string, object>(),
                    new Dictionary<string, object>()));
            Assert.That(stepError.ErrorCode,
                Is.EqualTo(MCPRuntimePreconditions.PlayModeRequiredErrorCode));
            Assert.That(stepError.Details["toolName"], Is.EqualTo(RUNTIME_MUTATION_TOOL_NAME));
            Assert.That(runtimeMutationInvocationCount, Is.Zero);

            var unaffected = RequireDictionary(ExecuteProjectTool(
                new Dictionary<string, object>
                {
                    { "toolName", READ_STATE_TOOL_NAME },
                    { "args", new Dictionary<string, object>() },
                }));
            Assert.That(unaffected["success"], Is.EqualTo(true));
        }

        [Test]
        public void ProjectToolDirectRoute_StripsProjectBindingArgumentsBeforeStrictSchemaValidation()
        {
            bool handled = MCPProjectToolCommands.TryExecuteDirectRoute(
                MCPProjectToolCommands.GetDirectRoute(PROJECT_FILE_MUTATION_TOOL_NAME),
                new Dictionary<string, object>
                {
                    { "expectedProjectPath", "D:/UnityProjects/BattleIdle" },
                    { "expectedProjectName", "BattleIdle" },
                },
                out object rawResponse);

            Assert.That(handled, Is.True);
            var response = RequireDictionary(rawResponse);
            Assert.That(response["success"], Is.EqualTo(true));
            var result = RequireDictionary(response["result"]);
            CollectionAssert.IsEmpty((string[])result["receivedKeys"]);
        }

        [UnityTest]
        public IEnumerator PersistentProjectToolJob_SupportsSchemaMetadataIdempotencyStepsAndCleanup()
        {
            const string agentId = "persistent-project-tool-test-agent";
            string idempotencyKey = Guid.NewGuid().ToString("N");
            Dictionary<string, object> Start(
                int value, string callerAgentId = agentId)
            {
                return RequireDictionary(ExecuteProjectTool(
                    new Dictionary<string, object>
                    {
                        { "toolName", PERSISTENT_PROJECT_TOOL_NAME },
                        { "_agentId", callerAgentId },
                        { "idempotencyKey", idempotencyKey },
                        { "args", new Dictionary<string, object>
                            {
                                { "requiredSteps", 2 },
                                { "value", value },
                            }
                        },
                    }));
            }

            Dictionary<string, object> started = Start(7);
            Assert.That(started["status"], Is.EqualTo("queued"));
            Assert.That(HasTag(started, "incrementalJob"), Is.True);
            Assert.That(HasTag(started, "cleanupDeclared"), Is.True);
            string jobId = started["jobId"].ToString();
            string accessToken = started["jobAccessToken"].ToString();

            Dictionary<string, object> reused =
                Start(7, "reconnected-project-tool-test-agent");
            Assert.That(reused["jobId"], Is.EqualTo(jobId));
            Assert.That(reused["jobAccessToken"],
                Is.EqualTo(accessToken));
            Assert.That(HasTag(reused, "reused"), Is.True);

            Dictionary<string, object> conflict =
                Start(8, "different-project-tool-test-agent");
            Assert.That(conflict["success"], Is.EqualTo(false));
            Assert.That(conflict["errorCode"], Is.EqualTo("idempotency_conflict"));

            Dictionary<string, object> descriptor = MCPProjectToolCommands
                .GetToolDetails(validOnly: true)
                .Single(tool => tool["toolName"].ToString() == PERSISTENT_PROJECT_TOOL_NAME);
            Assert.That(HasTag(descriptor, "incrementalJob"), Is.True);
            Assert.That(HasTag(descriptor, "outputSchema"), Is.True);
            CollectionAssert.Contains((ICollection)descriptor["sideEffects"], "changesRuntimeState");
            CollectionAssert.Contains((ICollection)descriptor["sideEffects"], "createsTemporaryObjects");
            CollectionAssert.Contains((ICollection)descriptor["errorCodes"], "fixture_failed");

            Dictionary<string, object> snapshot = null;
            for (int frame = 0; frame < 120; frame++)
            {
                yield return null;
                snapshot = RequireDictionary(MCPJobCommands.Get(
                    new Dictionary<string, object>
                    {
                        { "jobId", jobId },
                        { "_agentId", agentId },
                    }));
                if (snapshot["status"].ToString() == "succeeded")
                    break;
            }

            Assert.That(snapshot, Is.Not.Null);
            Assert.That(snapshot["status"], Is.EqualTo("succeeded"));
            Assert.That(Convert.ToDouble(snapshot["progress"]), Is.EqualTo(1d));
            Assert.That(Convert.ToInt32(snapshot["stepCount"]), Is.EqualTo(2));
            Assert.That(snapshot["cleanupStatus"], Is.EqualTo("available"));
            Dictionary<string, object> successEnvelope = RequireDictionary(snapshot["result"]);
            Dictionary<string, object> jobResult = RequireDictionary(successEnvelope["result"]);
            Assert.That(Convert.ToInt32(jobResult["value"]), Is.EqualTo(7));
            Assert.That(Convert.ToInt32(jobResult["stepCount"]), Is.EqualTo(2));

            Dictionary<string, object> cleanupQueued = RequireDictionary(MCPJobCommands.Cleanup(
                new Dictionary<string, object>
                {
                    { "jobId", jobId },
                    { "jobAccessToken", accessToken },
                    { "_agentId", "different-agent" },
                }));
            Assert.That(cleanupQueued["cleanupStatus"], Is.EqualTo("queued"));

            for (int frame = 0; frame < 120; frame++)
            {
                yield return null;
                snapshot = RequireDictionary(MCPJobCommands.Get(
                    new Dictionary<string, object>
                    {
                        { "jobId", jobId },
                        { "jobAccessToken", accessToken },
                        { "_agentId", "different-agent" },
                    }));
                if (snapshot["cleanupStatus"].ToString() == "succeeded")
                    break;
            }

            Assert.That(snapshot["cleanupStatus"], Is.EqualTo("succeeded"));
            Dictionary<string, object> cleanupEnvelope =
                RequireDictionary(snapshot["cleanupResult"]);
            Dictionary<string, object> cleanupResult =
                RequireDictionary(cleanupEnvelope["result"]);
            Assert.That(cleanupResult["cleaned"], Is.EqualTo(true));
        }

        [UnityTest]
        public IEnumerator PersistentProjectToolJob_CancelsBetweenSteps()
        {
            const string agentId = "persistent-project-tool-cancel-test-agent";
            Dictionary<string, object> started = RequireDictionary(
                ExecuteProjectTool(new Dictionary<string, object>
                {
                    { "toolName", PERSISTENT_PROJECT_TOOL_NAME },
                    { "_agentId", agentId },
                    { "idempotencyKey", Guid.NewGuid().ToString("N") },
                    { "args", new Dictionary<string, object>
                        {
                            { "requiredSteps", 100 },
                            { "value", 5 },
                        }
                    },
                }));
            string jobId = started["jobId"].ToString();

            Dictionary<string, object> snapshot = null;
            for (int frame = 0; frame < 120; frame++)
            {
                yield return null;
                snapshot = RequireDictionary(MCPJobCommands.Get(
                    new Dictionary<string, object>
                    {
                        { "jobId", jobId },
                        { "_agentId", agentId },
                    }));
                if (snapshot["status"].ToString() == "running" &&
                    Convert.ToInt32(snapshot["stepCount"]) > 0)
                {
                    break;
                }
            }
            Assert.That(snapshot["status"], Is.EqualTo("running"));

            Dictionary<string, object> canceled = RequireDictionary(MCPJobCommands.Cancel(
                new Dictionary<string, object>
                {
                    { "jobId", jobId },
                    { "_agentId", agentId },
                }));
            Assert.That(HasTag(canceled, "cancellationRequested"), Is.True);

            for (int frame = 0; frame < 120; frame++)
            {
                yield return null;
                snapshot = RequireDictionary(MCPJobCommands.Get(
                    new Dictionary<string, object>
                    {
                        { "jobId", jobId },
                        { "_agentId", agentId },
                    }));
                if (snapshot["status"].ToString() == "canceled")
                    break;
            }
            Assert.That(snapshot["status"], Is.EqualTo("canceled"));
            Assert.That(snapshot["cleanupStatus"], Is.EqualTo("available"));
        }
        private static Dictionary<string, object> RequireDictionary(object value)
        {
            Assert.That(value, Is.TypeOf<Dictionary<string, object>>());
            return (Dictionary<string, object>)value;
        }

        private static object ExecuteProjectTool(Dictionary<string, object> request)
        {
            string toolName = request["toolName"].ToString();
            var arguments = request.TryGetValue("args", out object rawArguments) &&
                            rawArguments is Dictionary<string, object> dictionary
                ? new Dictionary<string, object>(dictionary)
                : new Dictionary<string, object>();
            foreach (var pair in request)
            {
                if (pair.Key == "toolName" || pair.Key == "args")
                    continue;
                arguments[pair.Key] = pair.Value;
            }

            bool handled = MCPProjectToolCommands.TryExecuteDirectRoute(
                MCPProjectToolCommands.GetDirectRoute(toolName), arguments, out object response);
            Assert.That(handled, Is.True, $"Canonical project-tool route was not registered for '{toolName}'.");
            return response;
        }

        private static bool HasTag(Dictionary<string, object> metadata, string tag)
        {
            return metadata.TryGetValue("tags", out object value) &&
                   value is IEnumerable values &&
                   values.Cast<object>().Any(item =>
                       string.Equals(item?.ToString(), tag, StringComparison.Ordinal));
        }

        private static bool HasSideEffect(Dictionary<string, object> metadata, string sideEffect)
        {
            return metadata.TryGetValue("sideEffects", out object value) &&
                   value is IEnumerable values &&
                   values.Cast<object>().Any(item =>
                       string.Equals(item?.ToString(), sideEffect, StringComparison.Ordinal));
        }
    }
}
