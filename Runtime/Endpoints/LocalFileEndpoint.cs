/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using OmiLAXR.Composers;
using OmiLAXR.Utils;
using UnityEngine;
using UnityEngine.Serialization;

namespace OmiLAXR.Endpoints
{
    /// <summary>
    /// Local file endpoint that stores statements as JSONL (JSON Lines) format on the local filesystem.
    /// Provides flexible file organization with support for single-file or per-composer file storage.
    /// Handles path resolution, file management, and buffered writing for optimal performance.
    /// </summary>
    [AddComponentMenu("OmiLAXR / 6) Endpoints / (JSONL) File Endpoint")]
    [Description("Stores statements as JSONL (JSON per Line) on local path.")]
    public class LocalFileEndpoint : Endpoint
    {
#if UNITY_2020_1_OR_NEWER && !UNITY_2021_1_OR_NEWER
        protected override bool useThreads => false;
#endif
        
        /// <summary>
        /// Predefined folder locations for storing statement files.
        /// Provides convenient access to common system folders and custom paths.
        /// </summary>
        public enum DefaultFolderPaths
        {
            PersistantDataPath,     // Unity's persistent data folder (platform-specific)
            TempFolder,             // System temporary folder
            DesktopFolder,          // User's desktop folder
            DocumentsFolder,        // User's documents folder
            HomeFolder,             // User's home directory
            Custom                  // User-defined custom path
        }

        // protected override bool useThreads => false;

        /// <summary>
        /// Selected default folder for file storage.
        /// Determines the base directory where statement files will be created.
        /// </summary>
        public DefaultFolderPaths defaultFolder = DefaultFolderPaths.DesktopFolder;

        /// <summary>
        /// Name of the subfolder within the selected default folder where statements will be stored.
        /// Creates organized storage structure for statement files.
        /// </summary>
        public string statementsFolder = "omilaxr_statements";
        
        /// <summary>
        /// Custom path relative to the project root directory.
        /// Only used when DefaultFolderPaths.Custom is selected.
        /// Hidden in inspector unless Custom folder option is chosen.
        /// </summary>
        [Header("Relative to project root (only used when 'Custom' is selected)")] 
        [HideInInspector]
        public string customLocation;

        /// <summary>
        /// Virtual method to specify the file extension for output files.
        /// Default implementation returns "jsonl" but can be overridden by derived classes.
        /// </summary>
        /// <returns>File extension without the dot (e.g., "jsonl")</returns>
        protected virtual string GetExtension() => "jsonl";

        /// <summary>
        /// When enabled, creates separate files for each composer instead of one unified file.
        /// Provides better organization for complex scenarios with multiple data sources.
        /// </summary>
        [Header("If true, the statements will be split into multiple files named by composers.")]
        public bool oneFilePerComposer;
        
        /// <summary>
        /// Session identifier used in file naming.
        /// Supports timestamp formatting using curly brace notation (e.g., {yyyyMMddHHmmss}).
        /// Provides unique identification for different tracking sessions.
        /// </summary>
        [SerializeField, FormerlySerializedAs("fileName")] 
        [Header("Identification of the tracking session. You can use '{yyyyMMddHHmmss}' (default) to format current timestamp format.")]
        private string identifier = "{yyyyMMddHHmmss}";
        
        /// <summary>
        /// Cached resolved identifier to avoid repeated processing.
        /// Stores the actual string value after timestamp formatting is applied.
        /// </summary>
        private string _resolvedIdentifier;

        /// <summary>
        /// Property that returns the resolved identifier string.
        /// Automatically processes timestamp formatting on first access and caches the result.
        /// Ensures consistent identifier usage throughout the session.
        /// </summary>
        public string ResolvedIdentifier
        {
            get
            {
                // Return cached value if already resolved
                if (!string.IsNullOrWhiteSpace(_resolvedIdentifier))
                    return _resolvedIdentifier;

                // Set default identifier if none provided
                if (string.IsNullOrWhiteSpace(identifier))
                    identifier = "{yyyyMMddHHmmss}";

                // Process timestamp formatting and cache result
                return _resolvedIdentifier = IsInTimeFormat(identifier) ? GenerateIdentifier(DateTime.Now) : identifier;
            }
        }
        
        /// <summary>
        /// File buffer manager for efficient file I/O operations.
        /// Handles multiple file buffers simultaneously and manages their lifecycle.
        /// </summary>
        protected readonly FileBufferManager FileBufferManager = new FileBufferManager();

        /// <summary>
        /// Cached count of composers for performance optimization.
        /// Used to pre-allocate buffer capacity and monitor system complexity.
        /// </summary>
        protected int ComposersCount { get; private set; }
        
        /// <summary>
        /// Generates a formatted identifier string from the current timestamp.
        /// Extracts the format pattern from the identifier and applies it to the provided DateTime.
        /// </summary>
        /// <param name="now">The DateTime to format</param>
        /// <returns>Formatted timestamp string based on the identifier pattern</returns>
        protected string GenerateIdentifier(DateTime now)
        {
            // Extract format string by removing curly braces
            var format = identifier.Substring(1, identifier.Length - 2);
            return now.ToString(format);
        }

        /// <summary>
        /// Gets the absolute path to the project root directory.
        /// Calculated relative to Unity's Application.dataPath for consistent project-relative paths.
        /// </summary>
        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        /// <summary>
        /// Resolves a relative path to an absolute path based on the project root.
        /// Provides consistent path resolution for custom folder locations.
        /// </summary>
        /// <param name="relativePath">Path relative to project root</param>
        /// <returns>Absolute file system path</returns>
        public static string ResolveRelativeToProject(string relativePath)
            => Path.GetFullPath(Path.Combine(ProjectRoot, relativePath ?? ""));

        /// <summary>
        /// Resolves the selected default folder to its absolute file system path.
        /// Maps enum values to appropriate system folders or custom paths.
        /// </summary>
        /// <returns>Absolute path to the resolved folder</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for invalid folder path enum values</exception>
        protected string GetResolvedFolder()
        {
            return defaultFolder switch
            {
                DefaultFolderPaths.PersistantDataPath => Application.persistentDataPath,
                DefaultFolderPaths.TempFolder => Path.GetTempPath(),
                DefaultFolderPaths.DesktopFolder => Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                DefaultFolderPaths.DocumentsFolder => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                DefaultFolderPaths.HomeFolder => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                DefaultFolderPaths.Custom => ResolveRelativeToProject(customLocation),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        
        /// <summary>
        /// Checks if the identifier string contains timestamp formatting syntax.
        /// Identifies curly brace notation used for DateTime formatting patterns.
        /// </summary>
        /// <param name="ident">The identifier string to check</param>
        /// <returns>True if the identifier uses timestamp formatting, false otherwise</returns>
        private bool IsInTimeFormat(string ident)
        #if UNITY_2021_1_OR_NEWER
            => ident.EndsWith('}') && ident.StartsWith("{");
        #else
            => ident.EndsWith("}") && ident.StartsWith("{");
        #endif

        /// <summary>
        /// Generates the complete file path for single-file mode.
        /// Creates the directory structure if it doesn't exist and combines all path components.
        /// </summary>
        /// <returns>Absolute path to the single output file</returns>
        private string GetResolvedSingleFilePath()
        {
            // Build the complete folder path
            var folder = Path.Combine(GetResolvedFolder(), statementsFolder);
            
            // Ensure the directory exists
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            // Create the complete file name with identifier and extension
            var n = $"{ResolvedIdentifier}.{GetExtension()}";
            return Path.Combine(folder, n);
        }

        /// <summary>
        /// Property that returns the complete path to the statements folder.
        /// Used for organizing files and providing path information to other components.
        /// </summary>
        public string ResolvedStatementsFolderPath => Path.Combine(GetResolvedFolder(), statementsFolder);
        
        /// <summary>
        /// Determines the appropriate file ID and path based on the file organization mode.
        /// Handles both single-file and per-composer file generation strategies.
        /// </summary>
        /// <param name="composer">The composer generating the statement</param>
        /// <returns>Tuple containing the file buffer ID and complete file path</returns>
        protected (int, string) GetIdAndPath(IComposer composer)
        {
            // Single file mode: all statements go to one file
            if (!oneFilePerComposer)
                return (0, GetResolvedSingleFilePath());
            
            // Per-composer mode: create separate files for each composer
            var composerHashId = composer.GetHashCode();
            var composerName = composer.GetName();
            
            // Handle unnamed composers gracefully
            if (string.IsNullOrWhiteSpace(composerName))
                composerName = "Unknown";
            
            // Create subfolder for this session's per-composer files
            var folder = Path.Combine(ResolvedStatementsFolderPath, ResolvedIdentifier);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            
            // Generate unique file path for this composer
            var filePath = Path.Combine(folder, composerName + "." + GetExtension());
            return (composerHashId, filePath);
        }

        /// <summary>
        /// Post-batch processing hook that flushes all file buffers.
        /// Ensures data is written to disk after each batch for data integrity.
        /// </summary>
        /// <param name="batch">The batch that was just processed</param>
        protected override void AfterHandleSendingBatch(List<IStatement> batch)
        {
            FileBufferManager.FlushAll();
        }
        
        /// <summary>
        /// Handles the processing of individual statements for file output.
        /// Manages file buffering, formatting, and error handling for statement persistence.
        /// </summary>
        /// <param name="statement">The statement to process and write to file</param>
        /// <returns>TransferCode indicating success or failure of the operation</returns>
        protected override TransferCode HandleSending(IStatement statement)
        {
            if (statement == null)
                return TransferCode.NoStatements;
            try
            {
                // Determine the appropriate file and buffer for this statement
                var (id, filePath) = GetIdAndPath(statement.GetComposer());
                
                // Get or create file buffer for this specific file
                var buffer = FileBufferManager.EnsureBuffer(id, filePath);
                
                // Format and write the statement to the buffer
                buffer.Writer.WriteLine(FormatLine(statement));
                buffer.Writer.Flush();                          // Ensure immediate write
            }
            catch (IOException ex)
            {
                // Handle file I/O errors gracefully
                Debug.LogException(ex);
                return TransferCode.Error;
            }

            return TransferCode.Success;
        }

        /// <summary>
        /// Unity lifecycle method called when the component is destroyed.
        /// Ensures proper cleanup of all file buffers and streams.
        /// </summary>
        protected override void OnDestroy()
        {
            base.OnDestroy(); // stop sending and flush queued statements before closing the files
            FileBufferManager.Dispose();
        }

        /// <summary>
        /// Unity lifecycle method called when the component is disabled.
        /// Ensures proper cleanup of all file buffers and streams.
        /// </summary>
        protected override void OnDisable()
        {
            base.OnDisable(); // stop sending and flush queued statements before closing the files
            FileBufferManager.Dispose();
        }

        /// <summary>
        /// Virtual method for formatting individual statements as text lines.
        /// Default implementation converts statements to JSON strings.
        /// Can be overridden by derived classes for different output formats.
        /// </summary>
        /// <param name="statement">The statement to format</param>
        /// <returns>Formatted string representation of the statement</returns>
        protected virtual string FormatLine(IStatement statement) => statement.ToJsonString();

        /// <summary>
        /// Unity lifecycle method called when the component is enabled.
        /// Performs platform compatibility checks and initializes the file system.
        /// </summary>
        protected override void OnEnable()
        {
            // WebGL platform compatibility check
            // Local file operations are not supported in WebGL builds
#if UNITY_WEBGL
            DebugLog.OmiLAXR.Warning($"{nameof(LocalFileEndpoint)} is not supported on WebGL and will be disabled.");
            enabled = false;
            return;
#endif

            // Initialize system for normal platforms
            var dataProvider = GetDataProvider<DataProvider>();
            var composers = dataProvider ? dataProvider.GetComponentsInChildren<IComposer>() : Array.Empty<IComposer>();

            // Pre-allocate buffer capacity for better performance (Unity 2021.1+)
#if UNITY_2021_1_OR_NEWER
            FileBufferManager.FileBuffers.EnsureCapacity(composers.Length);
#endif

            // Cache composer count for monitoring and optimization
            ComposersCount = composers.Length;
            
            // Call base initialization
            base.OnEnable();
        }

        public override void ConsumeDataMap(DataMap map)
        {
            statementsFolder = GetMapValue(map, "statementsFolder", statementsFolder);
            identifier = GetMapValue(map, "identifier", identifier);
            customLocation = GetMapValue(map, "customLocation", customLocation);
            if (Enum.TryParse(GetMapValue<string>(map, "defaultFolder", null), out DefaultFolderPaths result))
                defaultFolder =  result;
            oneFilePerComposer = GetMapValue(map, "oneFilePerComposer", oneFilePerComposer);
            _resolvedIdentifier = null; // identifier may have changed
        }

        /// <summary>
        /// Reads a value from a data map, keeping the fallback if the key is missing or has another type.
        /// </summary>
        protected static T GetMapValue<T>(DataMap map, string key, T fallback)
            => map != null && map.TryGetValue(key, out var value) && value is T typed ? typed : fallback;

        public override DataMap ProvideDataMap()
        {
            return new DataMap()
            {
                ["statementsFolder"] = statementsFolder,
                ["identifier"] = identifier,
                ["customLocation"] = customLocation,
                ["defaultFolder"] = defaultFolder.ToString(),
                ["oneFilePerComposer"] = oneFilePerComposer,
                ["composersCount"] = ComposersCount,
                ["resolvedStatementsFolderPath"] = ResolvedStatementsFolderPath,
                ["resolvedIdentifier"] = ResolvedIdentifier,
                ["resolvedSingleFilePath"] = GetResolvedSingleFilePath(),
                ["resolvedFolder"] = GetResolvedFolder(),
            };
        }

        // Editor-only functionality for development and debugging
#if UNITY_EDITOR
        /// <summary>
        /// Preview file path for Unity Editor display.
        /// Shows the resolved file path in the inspector for development convenience.
        /// </summary>
        public string EditorPreviewFilePath { get; private set; } = "{yyyyMMddHHmmss}.jsonl";

        /// <summary>
        /// Updates the file location preview for the Unity Editor.
        /// Provides real-time feedback on where files will be created based on current settings.
        /// </summary>
        public void UpdateFileLocationPreview()
        {
            if (oneFilePerComposer)
            {
                // Show example path for per-composer mode
                var folder = Path.Combine(ResolvedStatementsFolderPath, identifier);
                var exampleComposer = "{composer_name}";
                var exampleFile = Path.Combine(folder, exampleComposer + "." + GetExtension());
                EditorPreviewFilePath = Path.GetFullPath(exampleFile);
            }
            else
            {
                // Show actual path for single-file mode
                var n = $"{identifier}.{GetExtension()}";
                EditorPreviewFilePath = Path.GetFullPath(Path.Combine(ResolvedStatementsFolderPath, n));
            }
        }

        /// <summary>
        /// Unity Editor validation callback.
        /// Updates the file location preview whenever inspector values change.
        /// </summary>
        private void OnValidate()
        {
            UpdateFileLocationPreview();
        }
#endif
    }
}