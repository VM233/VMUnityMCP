using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

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
        /// Remove response aliases that can be derived from the remaining payload before it crosses the HTTP
        /// transport. Command results stay unchanged in the queue so reload recovery and internal consumers keep
        /// their authoritative data; only the wire representation is compacted.
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

            Dictionary<string, object> source = ToDictionary(value);
            if (source == null)
                return value;

            if (isRoot && IsProjectToolSuccessEnvelope(source))
                return CompactValue(source["result"], true);

            bool isQueueTicketEnvelope = IsQueueTicketEnvelope(source);
            var compacted = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> pair in source)
                compacted[pair.Key] = CompactValue(
                    pair.Value, isQueueTicketEnvelope && pair.Key == "result");

            RemoveDuplicateSummaryValues(compacted);
            RemoveDuplicateErrorMessage(compacted);
            RemoveDerivedPresenceFlags(compacted);
            CompactSerializedArrayMetadata(compacted);
            CompactPersistenceMetadata(compacted);
            CompactOperationMetadata(compacted);
            CompactCollectionAndPaginationMetadata(compacted);
            CompactNamedResultPagination(compacted);
            RemoveFalseTruncationFlags(compacted);

            if (isRoot)
            {
                if (compacted.TryGetValue("success", out object success) && ToBool(success))
                    compacted.Remove("success");
                if (compacted.TryGetValue("stateConfirmed", out object stateConfirmed) &&
                    ToBool(stateConfirmed))
                    compacted.Remove("stateConfirmed");
            }

            return compacted;
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
}
