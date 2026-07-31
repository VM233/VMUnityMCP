using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class MCPResponse
    {
        public static Dictionary<string, object> Error(string message, string errorCode = "error",
            bool retryable = false, Dictionary<string, object> extra = null)
        {
            var response = new Dictionary<string, object>
            {
                { "success", false },
                { "error", message ?? "Unknown error" },
                { "message", message ?? "Unknown error" },
                { "errorCode", string.IsNullOrEmpty(errorCode) ? "error" : errorCode },
                { "retryable", retryable },
            };

            if (extra != null)
            {
                foreach (var pair in extra)
                    response[pair.Key] = pair.Value;
            }

            return response;
        }

        public static Dictionary<string, object> Success(object result = null, Dictionary<string, object> extra = null)
        {
            var response = new Dictionary<string, object>
            {
                { "success", true },
            };

            if (result != null)
                response["result"] = result;

            if (extra != null)
            {
                foreach (var pair in extra)
                    response[pair.Key] = pair.Value;
            }

            return response;
        }

        public static bool TryGetError(object data, out string message, out string errorCode, out bool retryable)
        {
            message = null;
            errorCode = null;
            retryable = false;

            var dictionary = ToDictionary(data);
            if (dictionary == null)
                return false;

            if (dictionary.TryGetValue("retryable", out var retryableValue))
                retryable = ToBool(retryableValue);

            if (dictionary.TryGetValue("errorCode", out var codeValue) && codeValue != null)
                errorCode = codeValue.ToString();

            if (dictionary.TryGetValue("error", out var errorValue) && errorValue != null)
            {
                message = errorValue.ToString();
                if (string.IsNullOrEmpty(errorCode))
                    errorCode = "error";
                return !string.IsNullOrEmpty(message);
            }

            if (dictionary.TryGetValue("success", out var successValue) && ToBool(successValue) == false)
            {
                if (dictionary.TryGetValue("message", out var messageValue) && messageValue != null)
                    message = messageValue.ToString();

                if (string.IsNullOrEmpty(message))
                    message = "Operation failed.";

                if (string.IsNullOrEmpty(errorCode))
                    errorCode = "operation_failed";

                return true;
            }

            return false;
        }

        public static Dictionary<string, object> NormalizeError(object data, string fallbackCode = "error",
            bool fallbackRetryable = false)
        {
            if (!TryGetError(data, out var message, out var errorCode, out var retryable))
                return Error("Operation failed.", fallbackCode, fallbackRetryable);

            var dictionary = ToDictionary(data);
            var response = dictionary != null
                ? new Dictionary<string, object>(dictionary)
                : new Dictionary<string, object>();

            response["success"] = false;
            response["error"] = message;
            response["message"] = message;
            response["errorCode"] = string.IsNullOrEmpty(errorCode) ? fallbackCode : errorCode;
            response["retryable"] = retryable || fallbackRetryable;
            return response;
        }

        /// <summary>
        /// Remove response aliases that can be derived from the remaining payload and empty optional containers
        /// before it crosses the HTTP transport. Command results stay unchanged in the queue so reload recovery
        /// and internal consumers keep their authoritative data; only the wire representation is compacted.
        /// </summary>
        public static object CompactForTransport(object data)
        {
            return CompactValue(data, true);
        }

        public static Dictionary<string, object> ToDictionary(object data)
        {
            if (data == null)
                return null;

            if (data is Dictionary<string, object> typed)
                return typed;

            if (data is IDictionary dictionary)
            {
                var result = new Dictionary<string, object>();
                foreach (DictionaryEntry entry in dictionary)
                    result[entry.Key.ToString()] = entry.Value;
                return result;
            }

            var type = data.GetType();
            if (type.IsPrimitive || data is string || data is decimal)
                return null;

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            if (properties.Length == 0)
                return null;

            var reflected = new Dictionary<string, object>();
            foreach (var property in properties)
            {
                if (!property.CanRead)
                    continue;

                try
                {
                    reflected[property.Name] = property.GetValue(data, null);
                }
                catch
                {
                    reflected[property.Name] = null;
                }
            }

            return reflected;
        }

        private static object CompactValue(object value, bool isRoot)
        {
            if (value == null || value is string || value is decimal)
                return value;

            Type valueType = value.GetType();
            if (valueType.IsPrimitive || valueType.IsEnum)
                return value;

            if (value is IList list)
            {
                var compactedList = new List<object>(list.Count);
                foreach (object item in list)
                    compactedList.Add(CompactValue(item, false));
                return compactedList;
            }

            if (MCPCompactValueFormatter.TryFormatUnityValue(value, out string formattedValue))
                return formattedValue;

            Dictionary<string, object> source = ToDictionary(value);
            if (source == null)
                return value;

            bool preserveUnityStructure = source.ContainsKey("$unityStruct");
            if (preserveUnityStructure)
            {
                source = new Dictionary<string, object>(source);
                source.Remove("$unityStruct");
            }
            if (!preserveUnityStructure &&
                MCPCompactValueFormatter.TryFormatDictionary(source, out formattedValue))
                return formattedValue;

            if (IsProjectToolSuccessEnvelope(source))
            {
                return isRoot
                    ? PreserveProjectToolSchemaShape(source["result"])
                    : PreserveProjectToolSchemaShape(source);
            }

            bool isQueueTicketEnvelope = IsQueueTicketEnvelope(source);
            var compacted = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> pair in source)
            {
                compacted[pair.Key] = IsJsonSchemaContractKey(pair.Key)
                    ? PreserveProjectToolSchemaShape(pair.Value)
                    : CompactValue(
                        pair.Value, isQueueTicketEnvelope && pair.Key == "result");
            }
            string emptyPrimaryCollectionKey = isRoot
                ? FindUniqueEmptyCollectionKeyMatchingCount(compacted, "count")
                : null;

            if (!preserveUnityStructure)
            {
                MCPCompactValueFormatter.CompactMembers(compacted);
                RemoveDuplicateSummaryValues(compacted);
                RemoveDuplicateErrorMessage(compacted);
                RemoveDuplicateInstanceDetails(compacted);
                RemoveDerivedPresenceFlags(compacted);
                CompactSerializedArrayMetadata(compacted);
                CompactPersistenceMetadata(compacted);
                CompactOperationMetadata(compacted);
                CompactCollectionAndPaginationMetadata(compacted);
                CompactNamedResultPagination(compacted);
                RemoveFalseTruncationFlags(compacted);
                RemoveDerivedEditorStateAliases(compacted);
                MCPContractMetadata.CompactTransportFlags(compacted);
                CompactJobAliases(compacted);
                RemoveIdleCompilationDiagnostics(compacted);
                CompactSuccessfulWaitMetadata(compacted);
            }

            if (isRoot)
            {
                if (compacted.TryGetValue("success", out object success) && ToBool(success))
                    compacted.Remove("success");
                if (compacted.TryGetValue("stateConfirmed", out object stateConfirmed) &&
                    ToBool(stateConfirmed))
                    compacted.Remove("stateConfirmed");
            }

            RemoveEmptyContainers(compacted, isRoot, emptyPrimaryCollectionKey);
            return compacted;
        }

        /// <summary>
        /// Project tools advertise and validate an explicit output schema before
        /// execution completes. Their wire payload must therefore retain empty
        /// containers, required counts, flags, and nested object members exactly
        /// as validated. Only the internal success/toolName envelope and the
        /// transport-only Unity struct marker are removed.
        /// </summary>
        private static object PreserveProjectToolSchemaShape(object value)
        {
            if (value == null || value is string || value is decimal)
                return value;

            Type valueType = value.GetType();
            if (valueType.IsPrimitive || valueType.IsEnum)
                return value;

            if (value is IList list)
            {
                var transportedList = new List<object>(list.Count);
                foreach (object item in list)
                    transportedList.Add(PreserveProjectToolSchemaShape(item));
                return transportedList;
            }

            if (MCPCompactValueFormatter.TryFormatUnityValue(value,
                    out string formattedValue))
            {
                return formattedValue;
            }

            Dictionary<string, object> source = ToDictionary(value);
            if (source == null)
                return value;

            var transported = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> pair in source)
            {
                if (pair.Key == "$unityStruct")
                    continue;
                transported[pair.Key] =
                    PreserveProjectToolSchemaShape(pair.Value);
            }
            return transported;
        }

        private static bool IsProjectToolSuccessEnvelope(Dictionary<string, object> dictionary)
        {
            if (!dictionary.TryGetValue("success", out object success) || !ToBool(success) ||
                !dictionary.ContainsKey("result") || !dictionary.ContainsKey("toolName"))
                return false;

            foreach (string key in dictionary.Keys)
            {
                if (key != "success" && key != "result" && key != "toolName")
                    return false;
            }

            return true;
        }

        private static bool IsJsonSchemaContractKey(string key)
        {
            return key == "inputSchema" || key == "outputSchema";
        }

        private static bool IsQueueTicketEnvelope(Dictionary<string, object> dictionary)
        {
            return dictionary.ContainsKey("ticketId") &&
                   dictionary.ContainsKey("status") &&
                   (dictionary.ContainsKey("actionName") || dictionary.ContainsKey("queuePosition"));
        }

        private static void RemoveDuplicateSummaryValues(Dictionary<string, object> dictionary)
        {
            if (!dictionary.TryGetValue("summary", out object summaryValue) ||
                !(summaryValue is Dictionary<string, object> summary))
                return;

            Dictionary<string, object> progress = null;
            if (dictionary.TryGetValue("progress", out object progressValue))
                progress = progressValue as Dictionary<string, object>;

            var duplicateKeys = new List<string>();
            foreach (KeyValuePair<string, object> pair in summary)
            {
                if (dictionary.TryGetValue(pair.Key, out object parentValue) &&
                    ValuesEqual(parentValue, pair.Value))
                {
                    duplicateKeys.Add(pair.Key);
                    continue;
                }
                if (progress != null && progress.TryGetValue(pair.Key, out object progressValueForKey) &&
                    ValuesEqual(progressValueForKey, pair.Value))
                    duplicateKeys.Add(pair.Key);
            }

            foreach (string key in duplicateKeys)
                summary.Remove(key);
            if (summary.Count == 0)
                dictionary.Remove("summary");
        }

        private static void RemoveDuplicateErrorMessage(Dictionary<string, object> dictionary)
        {
            if (dictionary.TryGetValue("error", out object error) &&
                dictionary.TryGetValue("message", out object message) &&
                ValuesEqual(error, message))
                dictionary.Remove("message");
        }

        private static void RemoveDuplicateInstanceDetails(Dictionary<string, object> dictionary)
        {
            if (!dictionary.TryGetValue("currentInstance", out object currentValue) ||
                !(currentValue is Dictionary<string, object> current) ||
                !dictionary.TryGetValue("actualProjectPath", out object actualPath) ||
                !dictionary.TryGetValue("actualProjectName", out object actualName) ||
                !dictionary.TryGetValue("actualPort", out object actualPort))
                return;

            if (current.TryGetValue("projectPath", out object currentPath) &&
                current.TryGetValue("projectName", out object currentName) &&
                current.TryGetValue("port", out object currentPort) &&
                ValuesEqual(actualPath, currentPath) &&
                ValuesEqual(actualName, currentName) &&
                ValuesEqual(actualPort, currentPort))
                dictionary.Remove("currentInstance");
        }

        private static void RemoveDerivedPresenceFlags(Dictionary<string, object> dictionary)
        {
            var removableKeys = new List<string>();
            foreach (KeyValuePair<string, object> pair in dictionary)
            {
                if (!pair.Key.StartsWith("has", StringComparison.Ordinal) || pair.Key.Length <= 3 ||
                    pair.Key == "hasMore" || !(pair.Value is bool))
                    continue;

                string suffix = pair.Key.Substring(3);
                string collectionKey = LowerFirst(suffix);
                if (dictionary.TryGetValue(collectionKey, out object collection) && collection is IList list)
                {
                    if ((bool)pair.Value == (list.Count > 0))
                        removableKeys.Add(pair.Key);
                    continue;
                }

                string singular = suffix.EndsWith("s", StringComparison.Ordinal) && suffix.Length > 1
                    ? suffix.Substring(0, suffix.Length - 1)
                    : suffix;
                string countKey = LowerFirst(singular) + "Count";
                if (dictionary.TryGetValue(countKey, out object countValue) &&
                    TryGetInteger(countValue, out long count) &&
                    (bool)pair.Value == (count > 0))
                    removableKeys.Add(pair.Key);
            }

            foreach (string key in removableKeys)
                dictionary.Remove(key);
        }

        private static void CompactSerializedArrayMetadata(Dictionary<string, object> dictionary)
        {
            if (!dictionary.TryGetValue("items", out object itemsValue) || !(itemsValue is IList items) ||
                !dictionary.TryGetValue("arraySize", out object arraySizeValue) ||
                !TryGetInteger(arraySizeValue, out long arraySize))
                return;

            bool truncated = arraySize > items.Count;
            if (dictionary.TryGetValue("truncated", out object truncatedValue) &&
                truncatedValue is bool explicitTruncated)
                truncated |= explicitTruncated;

            dictionary.Remove("maxItems");
            if (!truncated && arraySize == items.Count)
                dictionary.Remove("arraySize");
        }

        private static void CompactPersistenceMetadata(Dictionary<string, object> dictionary)
        {
            if (!dictionary.ContainsKey("persistedState"))
                return;

            dictionary.Remove("saved");
            dictionary.Remove("saveAttempted");
            dictionary.Remove("partialPersisted");
            dictionary.Remove("partialPersistedKnown");
        }

        private static void CompactOperationMetadata(Dictionary<string, object> dictionary)
        {
            if (!dictionary.TryGetValue("operationSummaries", out object summariesValue) ||
                !(summariesValue is IList summaries))
                return;

            RemoveMatchingCount(dictionary, "operationCount", summaries.Count);
            RemoveMatchingCount(dictionary, "appliedOperationCount", summaries.Count);
        }

        private static void CompactCollectionAndPaginationMetadata(Dictionary<string, object> dictionary)
        {
            IList pageCollection = FindUniqueCollectionMatchingCount(dictionary, "count");
            if (pageCollection != null)
                dictionary.Remove("count");

            RemoveNamedCollectionCounts(dictionary);

            long offset = 0;
            bool hadOffset = dictionary.TryGetValue("offset", out object offsetValue) &&
                             TryGetInteger(offsetValue, out offset);

            bool hasNextOffsetKey = dictionary.TryGetValue("nextOffset", out object nextOffsetValue);
            bool hasMore = dictionary.TryGetValue("hasMore", out object hasMoreValue) &&
                           hasMoreValue is bool hasMoreBoolean && hasMoreBoolean;
            long total = 0;
            bool hasTotal = TryGetPaginationTotal(dictionary, out total);

            if (pageCollection != null && !hasMore && hasTotal)
                hasMore = offset + pageCollection.Count < total;

            if (hasNextOffsetKey && nextOffsetValue == null)
                dictionary.Remove("nextOffset");
            else if (hasNextOffsetKey)
                hasMore = true;

            if (pageCollection != null && hasMore && !dictionary.ContainsKey("nextOffset") &&
                pageCollection.Count > 0)
                dictionary["nextOffset"] = offset + pageCollection.Count;

            bool isPagination = pageCollection != null &&
                                (hadOffset || dictionary.ContainsKey("limit") ||
                                 dictionary.ContainsKey("hasMore") || hasNextOffsetKey || hasTotal);
            if (isPagination)
            {
                dictionary.Remove("hasMore");
                dictionary.Remove("limit");
                if (dictionary.ContainsKey("nextOffset"))
                    dictionary.Remove("truncated");
                if (offset == 0)
                    dictionary.Remove("offset");

                if (!dictionary.ContainsKey("nextOffset") && offset == 0 && hasTotal &&
                    total == pageCollection.Count)
                    RemovePaginationTotals(dictionary, total);
            }
            else if (dictionary.TryGetValue("hasMore", out hasMoreValue) &&
                     hasMoreValue is bool falseHasMore && !falseHasMore)
            {
                dictionary.Remove("hasMore");
            }
        }

        private static void CompactNamedResultPagination(Dictionary<string, object> dictionary)
        {
            bool hasPaginationMetadata = dictionary.ContainsKey("resultOffset") ||
                                         dictionary.ContainsKey("resultLimit") ||
                                         dictionary.ContainsKey("totalResults") ||
                                         dictionary.ContainsKey("returnedResults") ||
                                         dictionary.ContainsKey("hasMoreResults") ||
                                         dictionary.ContainsKey("nextResultOffset");
            if (!hasPaginationMetadata)
                return;

            long offset = 0;
            if (dictionary.TryGetValue("resultOffset", out object offsetValue))
                TryGetInteger(offsetValue, out offset);

            long returned = 0;
            bool hasReturned = dictionary.TryGetValue("returnedResults", out object returnedValue) &&
                               TryGetInteger(returnedValue, out returned);
            long total = 0;
            bool hasTotal = dictionary.TryGetValue("totalResults", out object totalValue) &&
                            TryGetInteger(totalValue, out total);
            bool hasNextOffset = dictionary.TryGetValue("nextResultOffset", out object nextOffsetValue) &&
                                 nextOffsetValue != null;
            bool hasMore = dictionary.TryGetValue("hasMoreResults", out object hasMoreValue) &&
                           hasMoreValue is bool hasMoreBoolean && hasMoreBoolean;
            if (!hasMore && hasTotal && hasReturned)
                hasMore = offset + returned < total;
            if (hasNextOffset)
                hasMore = true;

            if (hasMore && !hasNextOffset && hasReturned && returned > 0)
            {
                dictionary["nextResultOffset"] = offset + returned;
                hasNextOffset = true;
            }
            else if (!hasNextOffset)
            {
                dictionary.Remove("nextResultOffset");
            }

            dictionary.Remove("resultOffset");
            dictionary.Remove("resultLimit");
            dictionary.Remove("returnedResults");

            if (!hasMore)
            {
                dictionary.Remove("totalResults");
                dictionary.Remove("hasMoreResults");
            }
            else if (hasNextOffset)
            {
                dictionary.Remove("hasMoreResults");
                dictionary.Remove("resultsTruncated");
            }
        }

        private static IList FindUniqueCollectionMatchingCount(
            Dictionary<string, object> dictionary, string countKey)
        {
            if (!dictionary.TryGetValue(countKey, out object countValue) ||
                !TryGetInteger(countValue, out long count))
                return null;

            IList match = null;
            foreach (object value in dictionary.Values)
            {
                if (!(value is IList list) || list.Count != count)
                    continue;
                if (match != null)
                    return null;
                match = list;
            }

            return match;
        }

        private static void RemoveNamedCollectionCounts(Dictionary<string, object> dictionary)
        {
            var removableKeys = new List<string>();
            foreach (KeyValuePair<string, object> pair in dictionary)
            {
                if (!pair.Key.EndsWith("Count", StringComparison.Ordinal) ||
                    !TryGetInteger(pair.Value, out long count))
                    continue;

                string stem = pair.Key.Substring(0, pair.Key.Length - "Count".Length);
                if (string.IsNullOrEmpty(stem) || stem == "total")
                    continue;

                string collectionKey = stem + "s";
                if (dictionary.TryGetValue(collectionKey, out object collection) &&
                    collection is IList list && list.Count == count)
                {
                    removableKeys.Add(pair.Key);
                    continue;
                }

                const string returnedPrefix = "returned";
                if (!stem.StartsWith(returnedPrefix, StringComparison.Ordinal) ||
                    stem.Length <= returnedPrefix.Length)
                    continue;

                string returnedStem = LowerFirst(stem.Substring(returnedPrefix.Length));
                collectionKey = Pluralize(returnedStem);
                if (dictionary.TryGetValue(collectionKey, out collection) &&
                    collection is IList returnedList && returnedList.Count == count)
                    removableKeys.Add(pair.Key);
            }

            foreach (string key in removableKeys)
                dictionary.Remove(key);
        }

        private static bool TryGetPaginationTotal(Dictionary<string, object> dictionary, out long total)
        {
            foreach (string key in new[]
                     {
                         "total", "totalMatches", "totalTools", "totalAssets", "totalResults",
                         "totalPackages", "totalTests",
                     })
            {
                if (dictionary.TryGetValue(key, out object value) && TryGetInteger(value, out total))
                    return true;
            }

            total = 0;
            return false;
        }

        private static void RemovePaginationTotals(Dictionary<string, object> dictionary, long total)
        {
            foreach (string key in new[]
                     {
                         "total", "totalMatches", "totalTools", "totalAssets", "totalResults",
                         "totalPackages", "totalTests",
                     })
            {
                if (dictionary.TryGetValue(key, out object value) &&
                    TryGetInteger(value, out long candidate) && candidate == total)
                    dictionary.Remove(key);
            }
        }

        private static void RemoveFalseTruncationFlags(Dictionary<string, object> dictionary)
        {
            var removableKeys = new List<string>();
            foreach (KeyValuePair<string, object> pair in dictionary)
            {
                if (!(pair.Value is bool flag) || flag)
                    continue;
                if (pair.Key == "truncated" ||
                    pair.Key.EndsWith("Truncated", StringComparison.Ordinal))
                    removableKeys.Add(pair.Key);
            }

            foreach (string key in removableKeys)
                dictionary.Remove(key);
        }

        private static void CompactJobAliases(Dictionary<string, object> dictionary)
        {
            bool hasJobIdentity = dictionary.ContainsKey("jobId") ||
                                  dictionary.ContainsKey("workflowId");
            if (!hasJobIdentity || !dictionary.ContainsKey("status"))
                return;

            dictionary.Remove("pollRoute");
            dictionary.Remove("statusRoute");
            dictionary.Remove("cancelRoute");
            dictionary.Remove("cleanupRoute");
            dictionary.Remove("pollArgs");

            foreach (string key in new[] { "started", "completed", "canceled", "cancelled" })
            {
                if (dictionary.TryGetValue(key, out object value) && value is bool)
                    dictionary.Remove(key);
            }

            if (dictionary.TryGetValue("startedAt", out object startedAt) &&
                dictionary.TryGetValue("updatedAt", out object updatedAt) &&
                ValuesEqual(startedAt, updatedAt))
            {
                dictionary.Remove("updatedAt");
            }
        }

        private static void RemoveDerivedEditorStateAliases(Dictionary<string, object> dictionary)
        {
            if (dictionary.ContainsKey("isPlaying") &&
                dictionary.ContainsKey("isChangingPlayMode"))
            {
                dictionary.Remove("isPlayingOrWillChangePlaymode");
            }
        }

        private static void RemoveIdleCompilationDiagnostics(Dictionary<string, object> dictionary)
        {
            if (!dictionary.TryGetValue("compilationDiagnostics", out object diagnosticsValue) ||
                !(diagnosticsValue is Dictionary<string, object> diagnostics))
                return;

            bool compiling = MCPContractMetadata.HasTag(
                                 diagnostics, MCPContractMetadata.Tag.Compiling) ||
                             diagnostics.TryGetValue("isCompiling", out object compilingValue) &&
                             ToBool(compilingValue);
            long errors = 0;
            long warnings = 0;
            if (diagnostics.TryGetValue("counts", out object countsValue) &&
                countsValue is Dictionary<string, object> counts)
            {
                if (counts.TryGetValue("errors", out object errorsValue))
                    TryGetInteger(errorsValue, out errors);
                if (counts.TryGetValue("warnings", out object warningsValue))
                    TryGetInteger(warningsValue, out warnings);
            }

            if (!compiling && errors == 0 && warnings == 0)
                dictionary.Remove("compilationDiagnostics");
        }

        private static void CompactSuccessfulWaitMetadata(Dictionary<string, object> dictionary)
        {
            if (!MCPContractMetadata.HasTag(dictionary, MCPContractMetadata.Tag.Idle))
                return;

            dictionary.Remove("timeoutMs");
            dictionary.Remove("stableFrames");
            dictionary.Remove("stableMs");
            dictionary.Remove("currentStableFrames");
        }

        private static void RemoveEmptyContainers(Dictionary<string, object> dictionary,
            bool preserveRootResult, string emptyPrimaryCollectionKey)
        {
            var removableKeys = new List<string>();
            foreach (KeyValuePair<string, object> pair in dictionary)
            {
                if (pair.Key == emptyPrimaryCollectionKey)
                    continue;

                if (pair.Value is IList list && list.Count == 0)
                {
                    removableKeys.Add(pair.Key);
                    continue;
                }

                if (pair.Value is IDictionary nestedDictionary && nestedDictionary.Count == 0)
                    removableKeys.Add(pair.Key);
            }

            if (preserveRootResult &&
                removableKeys.Count == 1 &&
                dictionary.Count == 1)
            {
                // An empty primary collection is still the semantic result of a
                // search/list call. Keep one collection so a completed queue
                // ticket cannot collapse into metadata with no result at all.
                removableKeys.RemoveAt(0);
            }

            foreach (string key in removableKeys)
                dictionary.Remove(key);
        }

        private static string FindUniqueEmptyCollectionKeyMatchingCount(
            Dictionary<string, object> dictionary, string countKey)
        {
            if (!dictionary.TryGetValue(countKey, out object countValue) ||
                !TryGetInteger(countValue, out long count) ||
                count != 0)
                return null;

            string match = null;
            foreach (KeyValuePair<string, object> pair in dictionary)
            {
                if (!(pair.Value is IList list) || list.Count != 0)
                    continue;
                if (match != null)
                    return null;
                match = pair.Key;
            }

            return match;
        }

        private static void RemoveMatchingCount(
            Dictionary<string, object> dictionary, string key, int expectedCount)
        {
            if (dictionary.TryGetValue(key, out object value) &&
                TryGetInteger(value, out long count) && count == expectedCount)
                dictionary.Remove(key);
        }

        private static bool TryGetInteger(object value, out long number)
        {
            try
            {
                if (value == null || value is bool || value is float || value is double || value is decimal)
                {
                    number = 0;
                    return false;
                }

                number = Convert.ToInt64(value);
                return true;
            }
            catch
            {
                number = 0;
                return false;
            }
        }

        private static bool ValuesEqual(object left, object right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null)
                return false;
            if (TryGetInteger(left, out long leftNumber) && TryGetInteger(right, out long rightNumber))
                return leftNumber == rightNumber;
            if (left is string leftText && right is string rightText)
                return string.Equals(leftText, rightText, StringComparison.Ordinal);
            return Equals(left, right);
        }

        private static string LowerFirst(string value)
        {
            if (string.IsNullOrEmpty(value) || char.IsLower(value[0]))
                return value;
            return char.ToLowerInvariant(value[0]) + value.Substring(1);
        }

        private static string Pluralize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;
            if (value.EndsWith("y", StringComparison.Ordinal) && value.Length > 1)
            {
                char beforeY = char.ToLowerInvariant(value[value.Length - 2]);
                if ("aeiou".IndexOf(beforeY) < 0)
                    return value.Substring(0, value.Length - 1) + "ies";
            }
            if (value.EndsWith("s", StringComparison.Ordinal) ||
                value.EndsWith("x", StringComparison.Ordinal) ||
                value.EndsWith("z", StringComparison.Ordinal) ||
                value.EndsWith("ch", StringComparison.Ordinal) ||
                value.EndsWith("sh", StringComparison.Ordinal))
                return value + "es";
            return value + "s";
        }

        private static bool ToBool(object value)
        {
            if (value is bool boolValue)
                return boolValue;

            return value != null && bool.TryParse(value.ToString(), out var parsed) && parsed;
        }
    }

    /// <summary>
    /// Formats small Unity value objects and their dictionary equivalents for the transport wire only.
    /// Command results remain structured internally; compact strings are deliberately optimized for concise,
    /// readable MCP output.
    /// </summary>
    internal static class MCPCompactValueFormatter
    {
        private static readonly HashSet<string> Vector2Keys = new HashSet<string>(StringComparer.Ordinal)
        {
            "x", "y",
        };

        private static readonly HashSet<string> VectorXZKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "x", "z",
        };

        private static readonly HashSet<string> Vector3Keys = new HashSet<string>(StringComparer.Ordinal)
        {
            "x", "y", "z",
        };

        private static readonly HashSet<string> Vector4Keys = new HashSet<string>(StringComparer.Ordinal)
        {
            "x", "y", "z", "w",
        };

        private static readonly HashSet<string> ColorRgbKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "r", "g", "b",
        };

        private static readonly HashSet<string> ColorRgbaKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "r", "g", "b", "a",
        };

        private static readonly HashSet<string> SizeKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "width", "height",
        };

        private static readonly HashSet<string> EdgeKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "left", "top", "right", "bottom",
        };

        private static readonly HashSet<string> RangeKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "min", "max",
        };

        private static readonly HashSet<string> StartEndKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "start", "end",
        };

        private static readonly HashSet<string> RectKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "x", "y", "width", "height", "xMin", "yMin", "xMax", "yMax",
            "position", "center", "min", "max", "size",
            "left", "top", "right", "bottom",
        };

        private static readonly HashSet<string> BoundsKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "center", "size", "extents", "min", "max",
        };

        public static bool TryFormatUnityValue(object value, out string formatted)
        {
            switch (value)
            {
                case Vector2 vector2:
                    formatted = FormatTuple(vector2.x, vector2.y);
                    return true;
                case Vector2Int vector2Int:
                    formatted = FormatTuple(vector2Int.x, vector2Int.y);
                    return true;
                case Vector3 vector3:
                    formatted = FormatTuple(vector3.x, vector3.y, vector3.z);
                    return true;
                case Vector3Int vector3Int:
                    formatted = FormatTuple(vector3Int.x, vector3Int.y, vector3Int.z);
                    return true;
                case Vector4 vector4:
                    formatted = FormatTuple(vector4.x, vector4.y, vector4.z, vector4.w);
                    return true;
                case Quaternion quaternion:
                    formatted = FormatTuple(quaternion.x, quaternion.y, quaternion.z, quaternion.w);
                    return true;
                case Rect rect:
                    formatted = FormatRect(rect.xMin, rect.yMin, rect.xMax, rect.yMax,
                        rect.width, rect.height);
                    return true;
                case RectInt rectInt:
                    formatted = FormatRect(rectInt.xMin, rectInt.yMin, rectInt.xMax, rectInt.yMax,
                        rectInt.width, rectInt.height);
                    return true;
                case Bounds bounds:
                    formatted = FormatBounds(bounds.min, bounds.max, bounds.size);
                    return true;
                case BoundsInt boundsInt:
                    formatted = FormatBounds(boundsInt.min, boundsInt.max, boundsInt.size);
                    return true;
                case Color color:
                    formatted = FormatColor(color.r, color.g, color.b, color.a);
                    return true;
                case Color32 color32:
                    formatted = FormatColor(color32.r, color32.g, color32.b, color32.a);
                    return true;
                case RectOffset offset:
                    formatted = FormatEdges(offset.left, offset.top, offset.right, offset.bottom);
                    return true;
                case Matrix4x4 matrix:
                    formatted = FormatMatrix(matrix);
                    return true;
                case Ray ray:
                    formatted = $"origin:{FormatTuple(ray.origin.x, ray.origin.y, ray.origin.z)}," +
                                $"direction:{FormatTuple(ray.direction.x, ray.direction.y, ray.direction.z)}";
                    return true;
                case Ray2D ray2D:
                    formatted = $"origin:{FormatTuple(ray2D.origin.x, ray2D.origin.y)}," +
                                $"direction:{FormatTuple(ray2D.direction.x, ray2D.direction.y)}";
                    return true;
                case Plane plane:
                    formatted = $"normal:{FormatTuple(plane.normal.x, plane.normal.y, plane.normal.z)}," +
                                $"distance:{FormatNumber(plane.distance)}";
                    return true;
                case Pose pose:
                    formatted = $"position:{FormatTuple(pose.position.x, pose.position.y, pose.position.z)}," +
                                $"rotation:{FormatTuple(pose.rotation.x, pose.rotation.y, pose.rotation.z, pose.rotation.w)}";
                    return true;
            }

            formatted = null;
            return false;
        }

        public static bool TryStructureUnityValue(object value, out object structured)
        {
            switch (value)
            {
                case Vector2 vector2:
                    structured = Structure("Vector2", ("x", vector2.x), ("y", vector2.y));
                    return true;
                case Vector2Int vector2Int:
                    structured = Structure("Vector2Int", ("x", vector2Int.x), ("y", vector2Int.y));
                    return true;
                case Vector3 vector3:
                    structured = Structure("Vector3", ("x", vector3.x), ("y", vector3.y), ("z", vector3.z));
                    return true;
                case Vector3Int vector3Int:
                    structured = Structure("Vector3Int", ("x", vector3Int.x), ("y", vector3Int.y),
                        ("z", vector3Int.z));
                    return true;
                case Vector4 vector4:
                    structured = Structure("Vector4", ("x", vector4.x), ("y", vector4.y), ("z", vector4.z),
                        ("w", vector4.w));
                    return true;
                case Quaternion quaternion:
                    structured = Structure("Quaternion", ("x", quaternion.x), ("y", quaternion.y),
                        ("z", quaternion.z), ("w", quaternion.w));
                    return true;
                case Rect rect:
                    structured = Structure("Rect", ("x", rect.x), ("y", rect.y), ("width", rect.width),
                        ("height", rect.height));
                    return true;
                case RectInt rectInt:
                    structured = Structure("RectInt", ("x", rectInt.x), ("y", rectInt.y),
                        ("width", rectInt.width), ("height", rectInt.height));
                    return true;
                case Bounds bounds:
                    structured = Structure("Bounds",
                        ("center", Structure("Vector3", ("x", bounds.center.x), ("y", bounds.center.y),
                            ("z", bounds.center.z))),
                        ("size", Structure("Vector3", ("x", bounds.size.x), ("y", bounds.size.y),
                            ("z", bounds.size.z))));
                    return true;
                case BoundsInt boundsInt:
                    structured = Structure("BoundsInt",
                        ("position", Structure("Vector3Int", ("x", boundsInt.position.x), ("y", boundsInt.position.y),
                            ("z", boundsInt.position.z))),
                        ("size", Structure("Vector3Int", ("x", boundsInt.size.x), ("y", boundsInt.size.y),
                            ("z", boundsInt.size.z))));
                    return true;
                case Color color:
                    structured = Structure("Color", ("r", color.r), ("g", color.g), ("b", color.b), ("a", color.a));
                    return true;
                case Color32 color32:
                    structured = Structure("Color32", ("r", color32.r), ("g", color32.g), ("b", color32.b),
                        ("a", color32.a));
                    return true;
                case RectOffset offset:
                    structured = Structure("RectOffset", ("left", offset.left), ("top", offset.top),
                        ("right", offset.right), ("bottom", offset.bottom));
                    return true;
                case Matrix4x4 matrix:
                    var values = new List<object>(16);
                    for (int row = 0; row < 4; row++)
                    {
                        for (int column = 0; column < 4; column++)
                            values.Add(matrix[row, column]);
                    }
                    structured = Structure("Matrix4x4", ("rowMajor", values));
                    return true;
                case Ray ray:
                    structured = Structure("Ray",
                        ("origin", Structure("Vector3", ("x", ray.origin.x), ("y", ray.origin.y),
                            ("z", ray.origin.z))),
                        ("direction", Structure("Vector3", ("x", ray.direction.x), ("y", ray.direction.y),
                            ("z", ray.direction.z))));
                    return true;
                case Ray2D ray2D:
                    structured = Structure("Ray2D",
                        ("origin", Structure("Vector2", ("x", ray2D.origin.x), ("y", ray2D.origin.y))),
                        ("direction", Structure("Vector2", ("x", ray2D.direction.x), ("y", ray2D.direction.y))));
                    return true;
                case Plane plane:
                    structured = Structure("Plane",
                        ("normal", Structure("Vector3", ("x", plane.normal.x), ("y", plane.normal.y),
                            ("z", plane.normal.z))),
                        ("distance", plane.distance));
                    return true;
                case Pose pose:
                    structured = Structure("Pose",
                        ("position", Structure("Vector3", ("x", pose.position.x), ("y", pose.position.y),
                            ("z", pose.position.z))),
                        ("rotation", Structure("Quaternion", ("x", pose.rotation.x), ("y", pose.rotation.y),
                            ("z", pose.rotation.z), ("w", pose.rotation.w))));
                    return true;
                default:
                    structured = null;
                    return false;
            }
        }

        private static Dictionary<string, object> Structure(string type,
            params (string key, object value)[] values)
        {
            var result = new Dictionary<string, object>
            {
                { "$unityStruct", true },
                { "type", type },
            };
            foreach ((string key, object value) in values)
                result[key] = value;
            return result;
        }

        public static void CompactMembers(Dictionary<string, object> dictionary)
        {
            if (dictionary == null || dictionary.Count == 0)
                return;

            bool hasRectMembers = dictionary.ContainsKey("x") && dictionary.ContainsKey("y") &&
                                  dictionary.ContainsKey("width") && dictionary.ContainsKey("height");
            if (hasRectMembers && !CompactRectMembers(dictionary))
            {
                RemoveDerivedByteUnits(dictionary);
                RemoveDuplicateBooleanAliases(dictionary);
                return;
            }

            CompactAxisMembers(dictionary);
            CompactDimensionPairs(dictionary);
            CompactEdgeMembers(dictionary);
            RemoveDerivedByteUnits(dictionary);
            RemoveDuplicateBooleanAliases(dictionary);
        }

        public static string FormatInstance(string projectName, int port)
        {
            string name = string.IsNullOrWhiteSpace(projectName) ? "Unity" : projectName.Trim();
            return $"{name}@{port}";
        }

        public static bool TryFormatDictionary(Dictionary<string, object> dictionary, out string formatted)
        {
            formatted = null;
            if (dictionary == null || dictionary.Count == 0)
                return false;

            if (TryFormatBoundsDictionary(dictionary, out formatted) ||
                TryFormatRectDictionary(dictionary, out formatted))
                return true;

            if (KeysEqual(dictionary, ColorRgbaKeys) &&
                TryFormatComponents(dictionary, out string[] rgba, "r", "g", "b", "a"))
            {
                formatted = $"rgba({string.Join(",", rgba)})";
                return true;
            }

            if (KeysEqual(dictionary, ColorRgbKeys) &&
                TryFormatComponents(dictionary, out string[] rgb, "r", "g", "b"))
            {
                formatted = $"rgb({string.Join(",", rgb)})";
                return true;
            }

            if (KeysEqual(dictionary, Vector4Keys) &&
                TryFormatComponents(dictionary, out string[] vector4, "x", "y", "z", "w"))
            {
                formatted = $"({string.Join(",", vector4)})";
                return true;
            }

            if (KeysEqual(dictionary, Vector3Keys) &&
                TryFormatComponents(dictionary, out string[] vector3, "x", "y", "z"))
            {
                formatted = $"({string.Join(",", vector3)})";
                return true;
            }

            if (KeysEqual(dictionary, Vector2Keys) &&
                TryFormatComponents(dictionary, out string[] vector2, "x", "y"))
            {
                formatted = $"({string.Join(",", vector2)})";
                return true;
            }

            if (KeysEqual(dictionary, VectorXZKeys) &&
                TryFormatComponents(dictionary, out string[] vectorXZ, "x", "z"))
            {
                formatted = $"(x:{vectorXZ[0]},z:{vectorXZ[1]})";
                return true;
            }

            if (KeysEqual(dictionary, SizeKeys) &&
                TryFormatComponents(dictionary, out string[] size, "width", "height", true))
            {
                formatted = $"({string.Join(",", size)})";
                return true;
            }

            if (KeysEqual(dictionary, EdgeKeys) &&
                TryFormatComponents(dictionary, out string[] edges, "left", "top", "right", "bottom"))
            {
                formatted = $"LTRB({string.Join(",", edges)})";
                return true;
            }

            if (KeysEqual(dictionary, RangeKeys) &&
                TryFormatComponents(dictionary, out string[] range, "min", "max"))
            {
                formatted = $"{range[0]}..{range[1]}";
                return true;
            }

            if (KeysEqual(dictionary, StartEndKeys) &&
                TryFormatComponents(dictionary, out string[] startEnd, "start", "end"))
            {
                formatted = $"{startEnd[0]}..{startEnd[1]}";
                return true;
            }

            return false;
        }

        private static bool TryFormatRectDictionary(Dictionary<string, object> dictionary, out string formatted)
        {
            formatted = null;
            if (!KeysAreSubset(dictionary, RectKeys) ||
                !TryGetOptionalNumberPair(dictionary, "width", "height",
                    out bool hasDimensions, out double width, out double height) ||
                !TryGetOptionalTuple(dictionary, "size", 2,
                    out bool hasSize, out double[] size))
                return false;

            if (!hasDimensions && !hasSize)
                return false;
            if (!hasDimensions)
            {
                width = size[0];
                height = size[1];
            }
            else if (hasSize &&
                     (!Approximately(width, size[0]) || !Approximately(height, size[1])))
                return false;

            if (!TryGetOptionalNumberPair(dictionary, "x", "y",
                    out bool hasXY, out double x, out double y) ||
                !TryGetOptionalNumberPair(dictionary, "xMin", "yMin",
                    out bool hasMinimumCoordinates, out double xMin, out double yMin) ||
                !TryGetOptionalNumberPair(dictionary, "left", "top",
                    out bool hasLeadingEdges, out double left, out double top) ||
                !TryGetOptionalTuple(dictionary, "position", 2,
                    out bool hasPosition, out double[] position) ||
                !TryGetOptionalTuple(dictionary, "min", 2,
                    out bool hasMinimum, out double[] minimum))
                return false;

            if (!hasXY && !hasMinimumCoordinates && !hasLeadingEdges &&
                !hasPosition && !hasMinimum)
                return false;

            double startX = hasXY ? x :
                hasMinimumCoordinates ? xMin :
                hasLeadingEdges ? left :
                hasPosition ? position[0] : minimum[0];
            double startY = hasXY ? y :
                hasMinimumCoordinates ? yMin :
                hasLeadingEdges ? top :
                hasPosition ? position[1] : minimum[1];
            if ((hasMinimumCoordinates &&
                 (!Approximately(startX, xMin) || !Approximately(startY, yMin))) ||
                (hasLeadingEdges &&
                 (!Approximately(startX, left) || !Approximately(startY, top))) ||
                (hasPosition &&
                 (!Approximately(startX, position[0]) || !Approximately(startY, position[1]))) ||
                (hasMinimum &&
                 (!Approximately(startX, minimum[0]) || !Approximately(startY, minimum[1]))))
                return false;

            double endX = startX + width;
            double endY = startY + height;
            if (!TryGetOptionalNumberPair(dictionary, "xMax", "yMax",
                    out bool hasMaximumCoordinates, out double xMax, out double yMax) ||
                !TryGetOptionalNumberPair(dictionary, "right", "bottom",
                    out bool hasTrailingEdges, out double right, out double bottom) ||
                !TryGetOptionalTuple(dictionary, "max", 2,
                    out bool hasMaximum, out double[] maximum) ||
                !TryGetOptionalTuple(dictionary, "center", 2,
                    out bool hasCenter, out double[] center))
                return false;

            if ((hasMaximumCoordinates &&
                 (!Approximately(endX, xMax) || !Approximately(endY, yMax))) ||
                (hasTrailingEdges &&
                 (!Approximately(endX, right) || !Approximately(endY, bottom))) ||
                (hasMaximum &&
                 (!Approximately(endX, maximum[0]) || !Approximately(endY, maximum[1]))) ||
                (hasCenter &&
                 (!Approximately(startX + width * 0.5d, center[0]) ||
                  !Approximately(startY + height * 0.5d, center[1]))))
                return false;

            formatted = FormatRect(startX, startY, endX, endY, width, height);
            return true;
        }

        private static bool TryGetOptionalNumberPair(
            Dictionary<string, object> dictionary, string firstKey, string secondKey,
            out bool present, out double first, out double second)
        {
            bool hasFirst = TryGetNumber(dictionary, firstKey, out first);
            bool hasSecond = TryGetNumber(dictionary, secondKey, out second);
            present = hasFirst && hasSecond;
            return hasFirst == hasSecond;
        }

        private static bool TryGetOptionalTuple(
            Dictionary<string, object> dictionary, string key, int length,
            out bool present, out double[] tuple)
        {
            present = dictionary.TryGetValue(key, out object value);
            if (!present)
            {
                tuple = null;
                return true;
            }

            return TryReadTuple(value, out tuple) && tuple.Length == length;
        }

        private static bool TryFormatBoundsDictionary(Dictionary<string, object> dictionary, out string formatted)
        {
            formatted = null;
            if (!KeysAreSubset(dictionary, BoundsKeys) ||
                !dictionary.TryGetValue("center", out object centerValue) ||
                !dictionary.TryGetValue("size", out object sizeValue) ||
                !TryReadTuple(centerValue, out double[] center) ||
                !TryReadTuple(sizeValue, out double[] size) ||
                center.Length != size.Length)
                return false;

            double[] min = new double[center.Length];
            double[] max = new double[center.Length];
            for (int index = 0; index < center.Length; index++)
            {
                min[index] = center[index] - size[index] * 0.5d;
                max[index] = center[index] + size[index] * 0.5d;
            }

            bool hasMin = dictionary.TryGetValue("min", out object minValue);
            bool hasMax = dictionary.TryGetValue("max", out object maxValue);
            if (hasMin || hasMax)
            {
                if (minValue == null || maxValue == null ||
                    !TryReadTuple(minValue, out double[] explicitMin) ||
                    !TryReadTuple(maxValue, out double[] explicitMax) ||
                    !TuplesApproximatelyEqual(min, explicitMin) ||
                    !TuplesApproximatelyEqual(max, explicitMax))
                    return false;

                min = explicitMin;
                max = explicitMax;
            }

            if (dictionary.TryGetValue("extents", out object extentsValue))
            {
                if (!TryReadTuple(extentsValue, out double[] extents) || extents.Length != size.Length)
                    return false;
                for (int index = 0; index < extents.Length; index++)
                {
                    if (!Approximately(extents[index] * 2d, size[index]))
                        return false;
                }
            }

            formatted = $"{FormatTuple(min)}-{FormatTuple(max)},size:{FormatTuple(size)}";
            return true;
        }

        private static bool CompactRectMembers(Dictionary<string, object> dictionary)
        {
            if (dictionary.ContainsKey("rect") ||
                !dictionary.ContainsKey("x") || !dictionary.ContainsKey("y") ||
                !dictionary.ContainsKey("width") || !dictionary.ContainsKey("height"))
                return false;

            var rect = new Dictionary<string, object>
            {
                { "x", dictionary["x"] },
                { "y", dictionary["y"] },
                { "width", dictionary["width"] },
                { "height", dictionary["height"] },
            };

            foreach (string key in new[] { "xMin", "yMin", "xMax", "yMax" })
            {
                if (dictionary.TryGetValue(key, out object value))
                    rect[key] = value;
            }

            if (!TryFormatRectDictionary(rect, out string formatted))
                return false;

            foreach (string key in rect.Keys)
                dictionary.Remove(key);
            dictionary["rect"] = formatted;
            return true;
        }

        private static void CompactAxisMembers(Dictionary<string, object> dictionary)
        {
            if (dictionary.ContainsKey("position") || !dictionary.ContainsKey("x"))
                return;

            string[] axes;
            if (dictionary.ContainsKey("y") && dictionary.ContainsKey("z") && dictionary.ContainsKey("w"))
                axes = new[] { "x", "y", "z", "w" };
            else if (dictionary.ContainsKey("y") && dictionary.ContainsKey("z"))
                axes = new[] { "x", "y", "z" };
            else if (dictionary.ContainsKey("y"))
                axes = new[] { "x", "y" };
            else if (dictionary.ContainsKey("z"))
                axes = new[] { "x", "z" };
            else
                return;

            if (!TryFormatComponents(dictionary, out string[] components, axes))
                return;

            foreach (string axis in axes)
                dictionary.Remove(axis);
            dictionary["position"] = axes.Length == 2 && axes[1] == "z"
                ? $"(x:{components[0]},z:{components[1]})"
                : $"({string.Join(",", components)})";
        }

        private static void CompactDimensionPairs(Dictionary<string, object> dictionary)
        {
            var widthKeys = new List<string>();
            foreach (string key in dictionary.Keys)
            {
                if (key == "width" || key.EndsWith("Width", StringComparison.Ordinal))
                    widthKeys.Add(key);
            }

            foreach (string widthKey in widthKeys)
            {
                string prefix = widthKey == "width"
                    ? ""
                    : widthKey.Substring(0, widthKey.Length - "Width".Length);
                string heightKey = string.IsNullOrEmpty(prefix) ? "height" : prefix + "Height";
                string sizeKey = string.IsNullOrEmpty(prefix) ? "size" : prefix + "Size";
                if (!dictionary.TryGetValue(widthKey, out object width) ||
                    !dictionary.TryGetValue(heightKey, out object height) ||
                    dictionary.ContainsKey(sizeKey) ||
                    !TryFormatScalar(width, true, out string formattedWidth) ||
                    !TryFormatScalar(height, true, out string formattedHeight))
                    continue;

                dictionary.Remove(widthKey);
                dictionary.Remove(heightKey);
                dictionary[sizeKey] = $"({formattedWidth},{formattedHeight})";
            }
        }

        private static void CompactEdgeMembers(Dictionary<string, object> dictionary)
        {
            if (!dictionary.ContainsKey("inset") &&
                TryFormatNumericMembers(dictionary, out string rawEdges,
                    "left", "top", "right", "bottom"))
            {
                dictionary.Remove("left");
                dictionary.Remove("top");
                dictionary.Remove("right");
                dictionary.Remove("bottom");
                dictionary["inset"] = $"LTRB({rawEdges})";
            }

            var leftKeys = new List<string>();
            foreach (string key in dictionary.Keys)
            {
                if (key.EndsWith("Left", StringComparison.Ordinal) && key.Length > "Left".Length)
                    leftKeys.Add(key);
            }

            foreach (string leftKey in leftKeys)
            {
                string prefix = leftKey.Substring(0, leftKey.Length - "Left".Length);
                string topKey = prefix + "Top";
                string rightKey = prefix + "Right";
                string bottomKey = prefix + "Bottom";
                if (dictionary.ContainsKey(prefix) ||
                    !TryFormatNumericMembers(dictionary, out string edges,
                        leftKey, topKey, rightKey, bottomKey))
                    continue;

                dictionary.Remove(leftKey);
                dictionary.Remove(topKey);
                dictionary.Remove(rightKey);
                dictionary.Remove(bottomKey);
                dictionary[prefix] = $"LTRB({edges})";
            }
        }

        private static void RemoveDerivedByteUnits(Dictionary<string, object> dictionary)
        {
            var bytesKeys = new List<string>();
            foreach (string key in dictionary.Keys)
            {
                if (key.EndsWith("Bytes", StringComparison.Ordinal))
                    bytesKeys.Add(key);
            }

            foreach (string bytesKey in bytesKeys)
            {
                if (!TryGetNumber(dictionary, bytesKey, out double bytes))
                    continue;

                string stem = bytesKey.Substring(0, bytesKey.Length - "Bytes".Length);
                RemoveMatchingDerivedUnit(dictionary, stem + "KB", bytes / 1024d);
                RemoveMatchingDerivedUnit(dictionary, stem + "MB", bytes / (1024d * 1024d));
                RemoveMatchingDerivedUnit(dictionary, stem + "GB", bytes / (1024d * 1024d * 1024d));
            }
        }

        private static void RemoveMatchingDerivedUnit(
            Dictionary<string, object> dictionary, string key, double expected)
        {
            if (!TryGetNumber(dictionary, key, out double actual))
                return;

            // Existing commands generally round display units to one to three decimal places.
            double tolerance = Math.Max(0.051d, Math.Abs(expected) * 0.00001d);
            if (Math.Abs(actual - expected) <= tolerance)
                dictionary.Remove(key);
        }

        private static void RemoveDuplicateBooleanAliases(Dictionary<string, object> dictionary)
        {
            var aliases = new List<string>();
            foreach (KeyValuePair<string, object> pair in dictionary)
            {
                if (!pair.Key.StartsWith("is", StringComparison.Ordinal) || pair.Key.Length <= 2 ||
                    !(pair.Value is bool aliasValue))
                    continue;

                string canonical = char.ToLowerInvariant(pair.Key[2]) + pair.Key.Substring(3);
                if (dictionary.TryGetValue(canonical, out object canonicalValue) &&
                    canonicalValue is bool canonicalBoolean && canonicalBoolean == aliasValue)
                    aliases.Add(pair.Key);
            }

            foreach (string alias in aliases)
                dictionary.Remove(alias);
        }

        private static bool TryFormatComponents(Dictionary<string, object> dictionary,
            out string[] formatted, params string[] keys)
        {
            return TryFormatComponents(dictionary, out formatted, keys, false);
        }

        private static bool TryFormatComponents(Dictionary<string, object> dictionary,
            out string[] formatted, string[] keys, bool allowText)
        {
            formatted = new string[keys.Length];
            for (int index = 0; index < keys.Length; index++)
            {
                if (!dictionary.TryGetValue(keys[index], out object value) ||
                    !TryFormatScalar(value, allowText, out formatted[index]))
                    return false;
            }

            return true;
        }

        private static bool TryFormatComponents(Dictionary<string, object> dictionary,
            out string[] formatted, string key1, string key2, bool allowText)
        {
            return TryFormatComponents(dictionary, out formatted, new[] { key1, key2 }, allowText);
        }

        private static bool TryFormatNumericMembers(Dictionary<string, object> dictionary,
            out string formatted, params string[] keys)
        {
            formatted = null;
            if (!TryFormatComponents(dictionary, out string[] components, keys))
                return false;
            formatted = string.Join(",", components);
            return true;
        }

        private static bool TryReadTuple(object value, out double[] tuple)
        {
            switch (value)
            {
                case Vector2 vector2:
                    tuple = new[] { (double)vector2.x, vector2.y };
                    return true;
                case Vector2Int vector2Int:
                    tuple = new[] { (double)vector2Int.x, vector2Int.y };
                    return true;
                case Vector3 vector3:
                    tuple = new[] { (double)vector3.x, vector3.y, vector3.z };
                    return true;
                case Vector3Int vector3Int:
                    tuple = new[] { (double)vector3Int.x, vector3Int.y, vector3Int.z };
                    return true;
                case Vector4 vector4:
                    tuple = new[] { (double)vector4.x, vector4.y, vector4.z, vector4.w };
                    return true;
            }

            Dictionary<string, object> dictionary = MCPResponse.ToDictionary(value);
            string[] keys;
            if (dictionary != null && KeysEqual(dictionary, Vector2Keys))
                keys = new[] { "x", "y" };
            else if (dictionary != null && KeysEqual(dictionary, VectorXZKeys))
                keys = new[] { "x", "z" };
            else if (dictionary != null && KeysEqual(dictionary, Vector3Keys))
                keys = new[] { "x", "y", "z" };
            else if (dictionary != null && KeysEqual(dictionary, Vector4Keys))
                keys = new[] { "x", "y", "z", "w" };
            else
            {
                tuple = null;
                return false;
            }

            tuple = new double[keys.Length];
            for (int index = 0; index < keys.Length; index++)
            {
                if (!TryGetNumber(dictionary, keys[index], out tuple[index]))
                {
                    tuple = null;
                    return false;
                }
            }

            return true;
        }

        private static bool TryFormatScalar(object value, bool allowText, out string formatted)
        {
            if (TryGetNumber(value, out _))
            {
                formatted = FormatNumber(value);
                return true;
            }

            if (allowText && value is string text && !string.IsNullOrEmpty(text))
            {
                formatted = text;
                return true;
            }

            formatted = null;
            return false;
        }

        private static bool TryGetNumber(
            Dictionary<string, object> dictionary, string key, out double number)
        {
            if (dictionary.TryGetValue(key, out object value))
                return TryGetNumber(value, out number);
            number = 0d;
            return false;
        }

        private static bool TryGetNumber(object value, out double number)
        {
            if (value == null || value is bool || value is char || value is string)
            {
                number = 0d;
                return false;
            }

            try
            {
                switch (Type.GetTypeCode(value.GetType()))
                {
                    case TypeCode.Byte:
                    case TypeCode.SByte:
                    case TypeCode.Int16:
                    case TypeCode.UInt16:
                    case TypeCode.Int32:
                    case TypeCode.UInt32:
                    case TypeCode.Int64:
                    case TypeCode.UInt64:
                    case TypeCode.Single:
                    case TypeCode.Double:
                    case TypeCode.Decimal:
                        number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                        return !double.IsNaN(number) && !double.IsInfinity(number);
                    default:
                        number = 0d;
                        return false;
                }
            }
            catch
            {
                number = 0d;
                return false;
            }
        }

        private static string FormatNumber(object value)
        {
            switch (Type.GetTypeCode(value.GetType()))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
                case TypeCode.Decimal:
                    return ((decimal)value).ToString("G29", CultureInfo.InvariantCulture);
                default:
                    return FormatNumber(Convert.ToDouble(value, CultureInfo.InvariantCulture));
            }
        }

        private static string FormatNumber(double value)
        {
            if (value == 0d)
                return "0";

            double absolute = Math.Abs(value);
            string format = absolute >= 0.000001d && absolute < 1000000000d
                ? "0.######"
                : "0.######E+0";
            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        private static string FormatTuple(params object[] values)
        {
            var formatted = new string[values.Length];
            for (int index = 0; index < values.Length; index++)
                formatted[index] = FormatNumber(values[index]);
            return $"({string.Join(",", formatted)})";
        }

        private static string FormatTuple(double[] values)
        {
            var formatted = new string[values.Length];
            for (int index = 0; index < values.Length; index++)
                formatted[index] = FormatNumber(values[index]);
            return $"({string.Join(",", formatted)})";
        }

        private static string FormatRect(
            object xMin, object yMin, object xMax, object yMax, object width, object height)
        {
            return $"{FormatTuple(xMin, yMin)}-{FormatTuple(xMax, yMax)}," +
                   $"size:{FormatTuple(width, height)}";
        }

        private static string FormatBounds(Vector3 min, Vector3 max, Vector3 size)
        {
            return $"{FormatTuple(min.x, min.y, min.z)}-{FormatTuple(max.x, max.y, max.z)}," +
                   $"size:{FormatTuple(size.x, size.y, size.z)}";
        }

        private static string FormatBounds(Vector3Int min, Vector3Int max, Vector3Int size)
        {
            return $"{FormatTuple(min.x, min.y, min.z)}-{FormatTuple(max.x, max.y, max.z)}," +
                   $"size:{FormatTuple(size.x, size.y, size.z)}";
        }

        private static string FormatColor(object red, object green, object blue, object alpha)
        {
            return $"rgba({FormatNumber(red)},{FormatNumber(green)}," +
                   $"{FormatNumber(blue)},{FormatNumber(alpha)})";
        }

        private static string FormatEdges(object left, object top, object right, object bottom)
        {
            return $"LTRB({FormatNumber(left)},{FormatNumber(top)}," +
                   $"{FormatNumber(right)},{FormatNumber(bottom)})";
        }

        private static string FormatMatrix(Matrix4x4 matrix)
        {
            return $"[{FormatTuple(matrix.m00, matrix.m01, matrix.m02, matrix.m03)};" +
                   $"{FormatTuple(matrix.m10, matrix.m11, matrix.m12, matrix.m13)};" +
                   $"{FormatTuple(matrix.m20, matrix.m21, matrix.m22, matrix.m23)};" +
                   $"{FormatTuple(matrix.m30, matrix.m31, matrix.m32, matrix.m33)}]";
        }

        private static bool KeysEqual(
            Dictionary<string, object> dictionary, HashSet<string> expected)
        {
            return dictionary.Count == expected.Count && KeysAreSubset(dictionary, expected);
        }

        private static bool KeysAreSubset(
            Dictionary<string, object> dictionary, HashSet<string> allowed)
        {
            foreach (string key in dictionary.Keys)
            {
                if (!allowed.Contains(key))
                    return false;
            }

            return true;
        }

        private static bool TuplesApproximatelyEqual(double[] left, double[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;
            for (int index = 0; index < left.Length; index++)
            {
                if (!Approximately(left[index], right[index]))
                    return false;
            }
            return true;
        }

        private static bool Approximately(double left, double right)
        {
            double scale = Math.Max(1d, Math.Max(Math.Abs(left), Math.Abs(right)));
            return Math.Abs(left - right) <= Math.Max(0.0002d, scale * 0.00001d);
        }
    }
}
