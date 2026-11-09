/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using OmiLAXR.Composers;
using OmiLAXR.Utils;
using UnityEngine;

namespace OmiLAXR.Endpoints
{
    /// <summary>
    /// CSV file endpoint that stores statements as comma-separated values on the local filesystem.
    /// Provides advanced filtering, header management, and data flattening capabilities for CSV output.
    /// Handles dynamic header expansion and efficient batch writing operations.
    /// </summary>
    [AddComponentMenu("OmiLAXR / 6) Endpoints / (CSV) File Endpoint"),
     Description("Stores statements as CSV on local path.")]
    public class CsvFileEndpoint : LocalFileEndpoint
    {
        /// <summary>
        /// Configuration options for filtering CSV headers using regular expression patterns.
        /// Allows selective inclusion and exclusion of specific data fields in the output.
        /// </summary>
        [Serializable]
        public struct FilterOptions
        {
            /// <summary>
            /// Regex pattern for including headers. Empty or ".*" includes all headers.
            /// Only headers matching this pattern will be included in the CSV output.
            /// </summary>
            [Header("Headers include Regex pattern. Keep empty or write '.*' to include all headers.")]
            public string includePattern;

            /// <summary>
            /// Regex pattern for excluding headers. Empty keeps all headers that pass the include filter.
            /// Headers matching this pattern will be removed from the CSV output.
            /// </summary>
            [Header("Headers exclude Regex pattern. Keep empty to keep all headers.")]
            public string excludePattern;
        }
        
        /// <summary>
        /// When enabled, complex nested data structures are flattened into single CSV rows.
        /// This may impact performance but provides comprehensive data export capabilities.
        /// Disabled flattening preserves object references but may lose nested detail.
        /// </summary>
        [Header("If true, deep structures will be flattened into a single row. May cost more performance.")]
        public bool flatten = true;
        
        /// <summary>
        /// User-configurable options for filtering which headers appear in the CSV output.
        /// Allows fine-grained control over data export scope and format.
        /// </summary>
        [Header("Options for filtering headers.")]
        public FilterOptions filterOptions;
        
        /// <summary>
        /// Specifies the file extension for CSV files.
        /// Overrides the base class to ensure proper CSV file identification.
        /// </summary>
        /// <returns>The CSV file extension "csv"</returns>
        protected override string GetExtension() => "csv";
        
        // Compiled regex patterns for efficient header filtering
        // Cached to avoid recompilation on each use
        private Regex _includeRegex;
        private Regex _excludeRegex;
        
        /// <summary>
        /// Optimized batch size for CSV processing.
        /// Higher than default to improve CSV writing efficiency while managing memory usage.
        /// </summary>
        protected override int MaxBatchSize => 100;
        
        /// <summary>
        /// Internal buffer structure for managing CSV data during batch processing.
        /// Maintains header state, file paths, and writing streams for efficient CSV generation.
        /// </summary>
        private class CsvBuffer
        {
            /// <summary>
            /// CSV formatting helper that manages headers, rows, and output formatting.
            /// </summary>
            public CsvFormat CsvFormat;

            /// <summary>
            /// Stream writer dedicated to writing CSV headers.
            /// Separated from data writing to allow dynamic header expansion.
            /// </summary>
            public StreamWriter HeaderWriter;

            /// <summary>
            /// Tracks the number of headers written to detect when header updates are needed.
            /// Used to determine if the header row needs to be rewritten with new columns.
            /// </summary>
            public int LastHeaderCount;

            /// <summary>
            /// File path for the main CSV file (headers + data).
            /// Used for final file assembly and cleanup operations.
            /// </summary>
            public string FilePath;
        }

        /// <summary>
        /// Creates and configures a default CSV format with current filter settings.
        /// Initializes regex patterns for header filtering and sets up batch processing parameters.
        /// </summary>
        /// <returns>Configured CsvFormat instance ready for statement processing</returns>
        private CsvFormat GetDefaultCsvFormat()
        {
            // Initialize include regex pattern if specified, otherwise allow all headers
            _includeRegex ??= string.IsNullOrEmpty(filterOptions.includePattern) 
                ? null 
                : new Regex(filterOptions.includePattern, RegexOptions.Compiled);

            // Initialize exclude regex pattern if specified, otherwise exclude nothing
            _excludeRegex ??= string.IsNullOrEmpty(filterOptions.excludePattern) 
                ? null 
                : new Regex(filterOptions.excludePattern, RegexOptions.Compiled);

            // Create CSV format with current batch size and filtering rules
            return new CsvFormat(MaxBatchSize)
            {
                IncludedHeaderPattern = _includeRegex,
                ExcludedHeaderPattern = _excludeRegex
            };
        }

        /// <summary>
        /// Creates a new CSV buffer for managing file-specific CSV operations.
        /// Sets up dual-file system (headers and data) for efficient dynamic header handling.
        /// </summary>
        /// <param name="filePath">Path to the main CSV file (without .data extension)</param>
        /// <returns>Initialized CsvBuffer ready for statement processing</returns>
        private CsvBuffer GetDefaultCsvBuffer(string filePath)
        {
            return new CsvBuffer
            {
                CsvFormat = GetDefaultCsvFormat(),
                HeaderWriter = new StreamWriter(filePath) { AutoFlush = false },    // Header file stream
                LastHeaderCount = 0,                                                // No headers written initially
                FilePath = filePath                                                 // Main file path for assembly
            };
        }

        /// <summary>
        /// Post-batch processing hook that writes accumulated CSV data to files.
        /// Called after each batch is processed to ensure data persistence and buffer management.
        /// </summary>
        /// <param name="batch">The batch of statements that was just processed</param>
        protected override void AfterHandleSendingBatch(List<IStatement> batch)
        {
            // Write all accumulated buffer data to their respective files
            foreach (var pair in FileBufferManager.FileBuffers)
            {
                WriteBufferToFile(pair.Value);
            }
        }

        /// <summary>
        /// Not implemented for CSV endpoint as CSV formatting is handled by specialized methods.
        /// CSV statements require complex formatting that cannot be handled by simple line formatting.
        /// Use ToCsvFormat extension method instead for proper CSV statement conversion.
        /// </summary>
        /// <param name="statement">The statement to format</param>
        /// <returns>Throws NotImplementedException</returns>
        /// <exception cref="NotImplementedException">Always thrown as this method should not be used</exception>
        protected override string FormatLine(IStatement statement)
        {
            throw new NotImplementedException("Don't use this method. Use 'ToCsvFormat' instead.");
        }

        /// <summary>
        /// Unity lifecycle method called when the component is destroyed.
        /// Ensures proper cleanup and final CSV file assembly before destruction.
        /// </summary>
        protected override void OnDestroy()
        {
            StopSending();              // Flush queued statements into the data file first
            MergeCsvHeaderAndData();    // Complete final file assembly
            base.OnDestroy();           // Call base cleanup
        }

        /// <summary>
        /// Unity lifecycle method called when the component is disabled.
        /// Ensures proper cleanup and final CSV file assembly before deactivation.
        /// </summary>
        protected override void OnDisable()
        {
            StopSending();              // Flush queued statements into the data file first
            MergeCsvHeaderAndData();    // Complete final file assembly
            base.OnDisable();           // Call base cleanup
        }

        /// <summary>
        /// Merges separate header and data files into final CSV files.
        /// Handles the dual-file system cleanup by combining headers with data and removing temporary files.
        /// Critical for proper CSV file integrity and cleanup of intermediate files.
        /// </summary>
        private void MergeCsvHeaderAndData()
        {
            foreach (var pair in FileBufferManager.FileBuffers)
            {
                var csvBuffer = (CsvBuffer)pair.Value.Data;
                var mainFilePath = csvBuffer.FilePath;          // Final CSV file path
                var dataFilePath = mainFilePath + ".data";      // Temporary data file path
                
                // Dispose and flush all streams to ensure data integrity
                if (pair.Value.Writer != null)
                {
                    pair.Value.Writer.Flush();
                    pair.Value.Writer.Dispose();
                    pair.Value.Writer = null;
                }

                if (csvBuffer.HeaderWriter != null)
                {
                    csvBuffer.HeaderWriter.Flush();
                    csvBuffer.HeaderWriter.Dispose();
                    csvBuffer.HeaderWriter = null;
                }
                
                // Merge header file and data file into final CSV
                // Headers are written first, followed by all data rows
                using (var targetStream = new FileStream(mainFilePath, FileMode.Append, FileAccess.Write, FileShare.None))
                using (var sourceStream = new FileStream(dataFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    sourceStream.CopyTo(targetStream);  // Efficiently copy data file content
                }
                
                // Clean up temporary data file
                File.Delete(dataFilePath);
            }
        }

        /// <summary>
        /// Writes the current buffer contents to the appropriate CSV files.
        /// Handles dynamic header expansion and efficient row writing with proper buffering.
        /// Updates headers only when new columns are detected to minimize file operations.
        /// </summary>
        /// <param name="buffer">The file buffer containing CSV data to write</param>
        private void WriteBufferToFile(FileBufferManager.FileBuffer buffer)
        {
            var csvBuffer = (CsvBuffer)buffer.Data;
            var csv = csvBuffer.CsvFormat;

            // Skip processing if no new data has been accumulated
            if (csv.Rows.Count == 0)
                return;

            // Check if new headers have been discovered and need to be written
            if (csv.Headers.Count > csvBuffer.LastHeaderCount)
            {
                var headerWriter = csvBuffer.HeaderWriter;
                
                // Rewrite the entire header row to include new columns
                headerWriter.BaseStream.Seek(0, SeekOrigin.Begin);     // Go to beginning of header file
                headerWriter.WriteLine(csv.GetHeadersString());        // Write updated header row
                csvBuffer.LastHeaderCount = csv.Headers.Count;         // Update header count tracking
                headerWriter.Flush();                                  // Ensure headers are written immediately
            }
            
            // Write all accumulated data rows to the data file
            buffer.Writer.WriteLines(csv.GetRowStrings());
            buffer.Writer.Flush();                                     // Ensure data is written to disk

            // Clear processed rows while preserving headers for next batch
            // This maintains header state while freeing memory from processed data
            csv.ClearRows();
        }
        
        /// <summary>
        /// Handles the processing of individual statements for CSV output.
        /// Manages file buffering, CSV formatting, and error handling for statement conversion.
        /// Implements the core CSV processing logic with proper exception handling.
        /// </summary>
        /// <param name="statement">The statement to process and add to CSV output</param>
        /// <returns>TransferCode indicating success or failure of the operation</returns>
        protected override TransferCode HandleSending(IStatement statement)
        {
            try
            {
                // Generate unique file identifier and path based on statement composer
                var (id, filePath) = GetIdAndPath(statement.GetComposer());
                
                // Get or create file buffer for this specific CSV file
                // Uses .data extension for temporary data file during processing
                var fb = FileBufferManager.EnsureBuffer(id, filePath + ".data", () => GetDefaultCsvBuffer(filePath));
                
                // Convert statement to CSV format and append to buffer
                // The flatten parameter determines whether complex objects are expanded
                fb.GetValue<CsvBuffer>().CsvFormat.Append(statement.ToCsvFormat(flatten));

                return TransferCode.Success;
            }
            catch (IOException ex)
            {
                // Handle file I/O errors gracefully
                Debug.LogException(ex);

                // The base endpoint reports the failure (OnFailedSendingStatement)
                return TransferCode.Error;
            }
        }

        public override void ConsumeDataMap(DataMap map)
        {
            base.ConsumeDataMap(map);
            flatten = GetMapValue(map, "flatten", flatten);
            filterOptions.includePattern = GetMapValue(map, "includePattern", filterOptions.includePattern);
            filterOptions.excludePattern = GetMapValue(map, "excludePattern", filterOptions.excludePattern);
        }

        public override DataMap ProvideDataMap()
        {
            var map = base.ProvideDataMap();
            map["flatten"] = flatten;
            map["includePattern"] = filterOptions.includePattern;
            map["excludePattern"] = filterOptions.excludePattern;
            return map;
        }
    }
}