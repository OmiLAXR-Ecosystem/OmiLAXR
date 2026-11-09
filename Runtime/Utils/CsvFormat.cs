/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using OmiLAXR.Extensions;

namespace OmiLAXR.Utils
{
    /// <summary>
    /// Delegate for custom header name formatting during CSV processing.
    /// Allows transformation of header names for consistency or compliance requirements.
    /// </summary>
    /// <param name="header">Original header name</param>
    /// <returns>Formatted header name</returns>
    public delegate string HeaderFormatter(string header);

    /// <summary>
    /// Comprehensive CSV data manipulation class with advanced features for analytics data processing.
    /// Supports dynamic headers, regex filtering, JSON conversion, and performance-optimized operations.
    /// Designed for handling large datasets common in learning analytics and data export scenarios.
    /// </summary>
    public class CsvFormat
    {
        /// <summary>
        /// Initializes a new CsvFormat instance with specified headers.
        /// Creates empty row collection ready for data population.
        /// </summary>
        /// <param name="headers">Initial column headers for the CSV</param>
        public CsvFormat(params string[] headers)
        {
            Headers = headers.ToList();
            Rows = new List<List<object>>();
            RebuildHeaderIndexCache();
        }

        /// <summary>
        /// Initializes a new CsvFormat instance with pre-allocated row capacity.
        /// Optimizes memory usage for scenarios with known data size.
        /// </summary>
        /// <param name="rowsCapacity">Expected number of rows for memory pre-allocation</param>
        /// <param name="headers">Initial column headers for the CSV</param>
        public CsvFormat(int rowsCapacity, params string[] headers)
        {
            Headers = headers.ToList();
            Rows = new List<List<object>>(rowsCapacity); // Pre-allocate for better performance
            RebuildHeaderIndexCache();
        }

        /// <summary>
        /// Dynamic list of column headers that can be modified during data processing.
        /// Automatically indexed for fast lookup operations.
        /// </summary>
        public List<string> Headers { get; private set; }
        
        /// <summary>
        /// Collection of data rows, each containing objects corresponding to the headers.
        /// Supports heterogeneous data types with automatic string conversion during output.
        /// </summary>
        public List<List<object>> Rows { get; }
        
        /// <summary>
        /// Column separator character used in CSV output formatting.
        /// Defaults to comma but can be changed for other delimiter formats.
        /// </summary>
        public string Separator { get; set; } = ",";

        /// <summary>
        /// Regular expression pattern for including specific headers in output.
        /// When set, only headers matching this pattern will be included in generated CSV.
        /// </summary>
        public Regex IncludedHeaderPattern { get; set; }
        
        /// <summary>
        /// Regular expression pattern for excluding specific headers from output.
        /// When set, headers matching this pattern will be excluded from generated CSV.
        /// </summary>
        public Regex ExcludedHeaderPattern { get; set; }

        /// <summary>
        /// Performance optimization cache mapping header names to their column indices.
        /// Rebuilt automatically when headers are modified.
        /// </summary>
        private Dictionary<string, int> _headerIndexCache;
        
        /// <summary>
        /// Rebuilds the internal header index cache for fast column lookups.
        /// Called automatically when headers are modified to maintain performance.
        /// </summary>
        private void RebuildHeaderIndexCache()
        {
            _headerIndexCache = new Dictionary<string, int>(Headers.Count);
            for (var i = 0; i < Headers.Count; i++)
                _headerIndexCache[Headers[i]] = i;
        }

        /// <summary>
        /// Gets the header index cache, rebuilding it if necessary.
        /// Lazy evaluation ensures cache is always current with minimal overhead.
        /// </summary>
        private Dictionary<string, int> HeaderIndexCache
        {
            get
            {
                // Rebuild cache if it's null or out of sync with headers
                if (_headerIndexCache == null || _headerIndexCache.Count != Headers.Count)
                    RebuildHeaderIndexCache();
                return _headerIndexCache;
            }
        }

        /// <summary>
        /// Sets new headers for the CSV, replacing any existing ones.
        /// Automatically rebuilds the index cache for optimal performance.
        /// </summary>
        /// <param name="headers">New array of header names</param>
        public void SetHeaders(params string[] headers)
        {
            Headers = headers?.ToList() ?? new List<string>(50);
            RebuildHeaderIndexCache();
        }

        /// <summary>
        /// Applies custom formatting to all headers using the provided formatter function.
        /// Useful for standardizing header names or applying naming conventions.
        /// </summary>
        /// <param name="formatter">Function to transform each header name</param>
        public void FormatHeaders(HeaderFormatter formatter)
        {
            Headers = Headers.Select(h => formatter(h)).ToList();
            RebuildHeaderIndexCache(); // header names changed, so the cache keys are stale
        }

        /// <summary>
        /// Removes all data rows while preserving headers and structure.
        /// Efficient way to reset data content without recreating the entire object.
        /// </summary>
        public void ClearRows()
        {
            Rows.Clear();
        }

        /// <summary>
        /// Renames an existing header to a new name with validation.
        /// Updates the index cache automatically to maintain performance.
        /// </summary>
        /// <param name="oldName">Current header name to replace</param>
        /// <param name="newName">New name for the header</param>
        /// <exception cref="ArgumentException">Thrown if the old header name is not found</exception>
        public void RenameHeader(string oldName, string newName)
        {
            if (!HeaderIndexCache.TryGetValue(oldName, out var index))
                throw new ArgumentException($"Header '{oldName}' not found.");

            Headers[index] = newName;
            RebuildHeaderIndexCache(); // Update cache with new header name
        }

        /// <summary>
        /// Adds a new data row from a dictionary of key-value pairs.
        /// Automatically expands headers if new keys are encountered (subject to regex filters).
        /// Handles missing values gracefully by using null placeholders.
        /// </summary>
        /// <param name="values">Dictionary mapping header names to values</param>
        public void AddRow(Dictionary<string, object> values)
        {
            var headerChanged = false;

            // Check for new headers that should be added
            foreach (var key in values.Keys)
            {
                // Apply include/exclude regex patterns for header filtering
                var include = (IncludedHeaderPattern == null || IncludedHeaderPattern.IsMatch(key)) &&
                              (ExcludedHeaderPattern == null || !ExcludedHeaderPattern.IsMatch(key));

                // Add new header if it passes filtering and doesn't already exist
                if (include && !Headers.Contains(key))
                {
                    Headers.Add(key);
                    headerChanged = true;
                }
            }

            // Rebuild index cache if headers were modified
            if (headerChanged)
                RebuildHeaderIndexCache();
            
            // Create row with values corresponding to current headers
            var row = new List<object>(Headers.Count);
            foreach (var header in Headers)
            {
                // Use Unity version-appropriate dictionary lookup
#if UNITY_2021_2_OR_NEWER
                row.Add(values.GetValueOrDefault(header));
#else
                values.TryGetValue(header, out var value);
                row.Add(value);
#endif
            }

            Rows.Add(row);
        }

        /// <summary>
        /// Removes a header column and all associated data from all rows.
        /// Efficiently updates the structure while maintaining data integrity.
        /// </summary>
        /// <param name="headerName">Name of the header column to remove</param>
        /// <exception cref="ArgumentException">Thrown if the header name is not found</exception>
        public void DropHeader(string headerName)
        {
            if (!HeaderIndexCache.TryGetValue(headerName, out var index))
                throw new ArgumentException($"Header '{headerName}' not found.");

            // Remove header from headers list
            Headers.RemoveAt(index);
            
            // Remove corresponding data from all rows
            foreach (var row in Rows.Where(row => index < row.Count))
            {
                row.RemoveAt(index);
            }

            // Rebuild cache to reflect the removed header
            RebuildHeaderIndexCache();
        }

        /// <summary>
        /// Gets the first data row as a CSV-formatted string with filtering applied.
        /// Returns empty string if no rows exist.
        /// </summary>
        /// <returns>First row as CSV string or empty string if no data</returns>
        public string GetFirstRow() =>
            Rows.Count < 1 ? "" : string.Join(Separator, GetFilteredStringValues(Rows.First()));

        /// <summary>
        /// Converts all data rows to CSV-formatted string array with proper escaping.
        /// Applies header filtering and handles missing data gracefully.
        /// </summary>
        /// <returns>Array of CSV-formatted strings representing all rows</returns>
        public string[] GetRowStrings()
        {
            var result = new string[Rows.Count];
            var filteredHeaders = GetFilteredHeaders().ToList();

            // Process each row individually for memory efficiency
            for (var i = 0; i < Rows.Count; i++)
            {
                var row = Rows[i];
                var formatted = new List<string>(filteredHeaders.Count);

                // Extract values for filtered headers only
                foreach (var header in filteredHeaders)
                {
                    var index = HeaderIndexCache.TryGetValue(header, out var idx) ? idx : -1;
                    var value = (index >= 0 && index < row.Count) ? row[index] : null;
                    formatted.Add(FormatCsvValue(value));
                }

                result[i] = string.Join(Separator, formatted);
            }

            return result;
        }

        /// <summary>
        /// Returns a complete CSV string representation including headers and data.
        /// Applies all filtering rules and proper CSV escaping.
        /// </summary>
        /// <returns>Complete CSV formatted string with headers and data</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();

            // Add headers row with filtering applied
            var filteredHeaders = GetFilteredHeaders();
            sb.AppendLine(string.Join(Separator, filteredHeaders.Select(FormatCsvValue)));

            // Add data rows with filtering applied
            foreach (var row in Rows)
                sb.AppendLine(string.Join(Separator, GetFilteredStringValues(row)));

            return sb.ToString();
        }

        /// <summary>
        /// Returns only the header row as a CSV-formatted string.
        /// Useful for creating header-only files or for validation.
        /// </summary>
        /// <returns>Header row as CSV string</returns>
        public string GetHeadersString()
            => string.Join(Separator, GetFilteredHeaders());

        /// <summary>
        /// Returns only the data rows as CSV-formatted strings without headers.
        /// Applies all filtering and formatting rules to the data.
        /// </summary>
        /// <returns>Data rows as multi-line CSV string</returns>
        public string GetRowsString()
        {
            var sb = new StringBuilder();

            foreach (var row in Rows)
                sb.AppendLine(string.Join(Separator, GetFilteredStringValues(row))); // values are already escaped

            return sb.ToString();
        }

        /// <summary>
        /// Filters the headers list based on include/exclude regex patterns.
        /// Returns only headers that pass the filtering criteria.
        /// </summary>
        /// <returns>List of headers that pass filtering rules</returns>
        private List<string> GetFilteredHeaders()
        {
            var result = new List<string>(Headers.Count);
            for (var i = 0; i < Headers.Count; i++)
            {
                var h = Headers[i];
                // Apply both include and exclude patterns
                if ((IncludedHeaderPattern == null || IncludedHeaderPattern.IsMatch(h)) &&
                    (ExcludedHeaderPattern == null || !ExcludedHeaderPattern.IsMatch(h)))
                {
                    result.Add(h);
                }
            }

            return result;
        }

        /// <summary>
        /// Extracts and formats values from a row based on filtered headers.
        /// Handles missing columns and applies proper CSV formatting.
        /// </summary>
        /// <param name="row">Data row to process</param>
        /// <returns>List of formatted string values for filtered headers</returns>
        private List<string> GetFilteredStringValues(List<object> row)
        {
            if (Headers == null || row == null)
                return new List<string>();

            var result = new List<string>(Headers.Count); // Conservative capacity estimate

            foreach (var header in Headers)
            {
                // Skip headers that don't pass filtering
                if ((IncludedHeaderPattern != null && !IncludedHeaderPattern.IsMatch(header)) ||
                    (ExcludedHeaderPattern != null && ExcludedHeaderPattern.IsMatch(header)))
                    continue;

                // Handle missing columns gracefully
                if (!HeaderIndexCache.TryGetValue(header, out var idx) || idx >= row.Count)
                {
                    result.Add(""); // Empty string for missing data
                    continue;
                }

                var value = row[idx];
                result.Add(FormatCsvValue(value));
            }

            return result;
        }

        /// <summary>
        /// Formats a single value for CSV output with proper escaping.
        /// Handles null values, quotes, commas, and newlines according to CSV standards.
        /// </summary>
        /// <param name="value">Value to format</param>
        /// <returns>CSV-safe formatted string</returns>
        public static string FormatCsvValue(object value)
        {
            if (value == null) return "";
            var str = value.ToString();

            // Escape values containing special CSV characters
            if (str.Contains(',') || str.Contains('"') || str.Contains('\n'))
                str = "\"" + str.Replace("\"", "\"\"") + "\""; // Double quotes for escaping

            return str;
        }

        /// <summary>
        /// Merges another CsvFormat into this one by combining headers and rows.
        /// Handles header conflicts by creating a union of all headers.
        /// </summary>
        /// <param name="other">CsvFormat to merge into this one</param>
        public void Append(CsvFormat other)
        {
            if (other == null || other.Rows.Count == 0)
                return;

            // Create combined header set preserving order
            var combinedHeaders = Headers.Union(other.Headers).ToList();
            var combinedIndexMap = combinedHeaders
                .Select((h, i) => new { h, i })
                .ToDictionary(x => x.h, x => x.i);

            // Create mapping arrays for efficient row processing
            var thisIndexMap = Headers.Select(h => combinedIndexMap[h]).ToList();
            var otherIndexMap = other.Headers.Select(h => combinedIndexMap[h]).ToList();

            var newRows = new List<List<object>>();

            // Reformat existing rows to match combined headers
            foreach (var row in Rows)
            {
                var newRow = Enumerable.Repeat<object>(null, combinedHeaders.Count).ToList();
                for (var i = 0; i < row.Count; i++)
                    newRow[thisIndexMap[i]] = row[i];
                newRows.Add(newRow);
            }

            // Add other CSV's rows with proper header alignment
            foreach (var row in other.Rows)
            {
                var newRow = Enumerable.Repeat<object>(null, combinedHeaders.Count).ToList();
                for (var i = 0; i < row.Count; i++)
                    newRow[otherIndexMap[i]] = row[i];
                newRows.Add(newRow);
            }

            // Update this instance with merged data
            Headers = combinedHeaders;
            Rows.Clear();
            Rows.AddRange(newRows);
            RebuildHeaderIndexCache();
        }

        /// <summary>
        /// Merges multiple CsvFormat instances into a single unified format.
        /// Efficiently combines headers and maintains data integrity across all sources.
        /// </summary>
        /// <param name="csvs">Collection of CsvFormat instances to merge</param>
        /// <returns>New CsvFormat containing all merged data</returns>
        public static CsvFormat MergeMany(IEnumerable<CsvFormat> csvs)
        {
            var result = new CsvFormat();
            foreach (var csv in csvs)
            {
                // Initialize with first CSV's structure
                if (result.Headers.Count == 0 && result.Rows.Count == 0)
                {
                    result.SetHeaders(csv.Headers.ToArray());
                    foreach (var row in csv.Rows)
                        result.Rows.Add(new List<object>(row));
                }
                else
                {
                    // Merge subsequent CSVs using Append logic
                    result.Append(csv);
                }
            }

            return result;
        }

        /// <summary>
        /// Creates a CsvFormat from JSON string with optional object flattening.
        /// Supports both single objects and arrays of objects with flexible structure handling.
        /// </summary>
        /// <param name="json">JSON string to convert</param>
        /// <param name="flatten">Whether to flatten nested objects into dot-notation columns</param>
        /// <returns>CsvFormat representation of the JSON data</returns>
        public static CsvFormat FromJson(string json, bool flatten = false) => FromJson(JToken.Parse(json), flatten);

        /// <summary>
        /// Creates a CsvFormat from JToken with sophisticated array expansion and flattening support.
        /// Handles complex JSON structures including nested arrays and objects.
        /// </summary>
        /// <param name="token">JToken representing the JSON data</param>
        /// <param name="flatten">Whether to flatten nested objects into dot-notation columns</param>
        /// <returns>CsvFormat representation of the JSON data</returns>
        /// <exception cref="ArgumentException">Thrown if root token is not an object or array</exception>
        public static CsvFormat FromJson(JToken token, bool flatten = false)
        {
            var result = new CsvFormat();
            var headers = new HashSet<string>(); // Track unique headers across all objects

            if (token is JArray array)
            {
                // Process JSON array - each element becomes a row
                foreach (var item in array)
                {
                    var obj = item as JObject;
                    if (obj == null)
                        continue;

                    // Handle objects with expandable arrays (special case for complex structures)
                    var expandable = obj.Properties().FirstOrDefault(prop => prop.Value is JArray);

                    if (expandable != null)
                    {
                        // Extract shared context from non-array properties
                        var sharedContext = new Dictionary<string, object>();
                        foreach (var prop in obj.Properties())
                        {
                            if (prop != expandable)
                                sharedContext[prop.Name] = prop.Value.ToObject<object>();
                        }

                        // Expand array elements as separate rows with shared context
                        foreach (var subItem in (JArray)expandable.Value)
                        {
                            if (subItem is JObject subObj)
                                AddObject(subObj, sharedContext);
                        }
                    }
                    else
                    {
                        // Standard object processing
                        AddObject(obj);
                    }
                }
            }
            else if (token is JObject single)
            {
                // Process single JSON object
                AddObject(single);
            }
            else
            {
                throw new ArgumentException("Root must be a JSON object or array of objects.");
            }

            // Set headers after processing all data to ensure completeness
            result.SetHeaders(headers.ToArray());
            return result;

            // Local function for adding JSON objects with optional shared context
            void AddObject(JObject obj, Dictionary<string, object> sharedContext = null)
            {
                Dictionary<string, object> dict;

                if (flatten)
                {
                    // Use extension method to flatten nested objects
                    dict = obj.Flatten();
                }
                else
                {
                    // Standard processing - preserve object structure as JSON strings
                    dict = new Dictionary<string, object>();
                    foreach (var prop in obj.Properties())
                    {
                        if (prop.Value is JObject || prop.Value is JArray)
                            dict[prop.Name] = prop.Value.ToString(Newtonsoft.Json.Formatting.None);
                        else
                            dict[prop.Name] = prop.Value.ToObject<object>();
                    }
                }

                // Merge shared context if provided
                if (sharedContext != null)
                {
                    foreach (var kvp in sharedContext)
                        dict[kvp.Key] = kvp.Value;
                }

                // Track all unique headers
                foreach (var key in dict.Keys)
                    headers.Add(key);

                // Add row to result
                result.AddRow(dict);
            }
        }
    }
}