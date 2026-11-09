/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using OmiLAXR.Utils;

namespace OmiLAXR.Endpoints
{
    /// <summary>
    /// Delegate representing a single configuration loading attempt.
    /// Returns true if the strategy successfully loaded and applied a configuration; false otherwise.
    /// </summary>
    public delegate bool AuthLoaderStrategy();
    /// <summary>
    /// Generic authentication loader base class.
    /// 
    /// Provides a unified mechanism for configuring an endpoint (`TEndpoint`) 
    /// from an authentication config (`TConfig`).
    /// 
    /// Loading priority:
    /// - WebGL builds:
    ///   1. If URL contains "?{filename}=https://..." (where {filename} is the authFileName),
    ///      load the JSON config from that remote URL.
    ///   2. Otherwise, parse endpoint/token (or subclass-specific keys) from URL query.
    ///   3. JSON file in Application.streamingAssetsPath.
    /// 
    /// - Non-WebGL builds:
    ///   1. Command line arguments (-key=value...).
    ///   2. JSON file next to the executable.
    ///   3. JSON file in Application.persistentDataPath.
    ///   4. JSON file in Application.streamingAssetsPath.
    /// 
    /// Subclasses implement:
    /// - DefaultAuthFileName (string)
    /// - TryBuildConfig(IDictionary&lt;string,string&gt;, out TConfig)
    /// - ApplyConfig(TEndpoint, TConfig)
    /// </summary>
    [DefaultExecutionOrder(-1001)] // Ensure early initialization in the component lifecycle
    public abstract class AuthLoader<TEndpoint, TConfig> : PipelineComponent
        where TEndpoint : Component
        where TConfig : struct
    {
        /// <summary>
        /// Gets the default filename with extension for authentication configuration files.
        /// Override in derived classes to specify authentication scheme-specific file names.
        /// Used when no explicit filename is provided during file-based loading.
        /// </summary>
        public string authFileName = "";

        /// <summary>
        /// Gets the default authentication configuration filename to use when none is provided.
        /// Should include the file extension (e.g., "bearer.json", "credentials.json").
        /// </summary>
        protected abstract string DefaultAuthFileName { get; }

        /// <summary>
        /// URL query key derived from the auth file name, without extension.
        /// Example: "bearer.json" -> "bearer". Used to look up a remote JSON URL in WebGL builds.
        /// </summary>
        [ReadOnly]
        public string urlQuery = "";

        /// <summary>
        /// The endpoint component instance that will be configured with the loaded authentication settings.
        /// If not assigned, the component will attempt to resolve TEndpoint on the same GameObject in Awake().
        /// </summary>
        [FormerlySerializedAs("targetAuthEndpoint")]
        public TEndpoint targetEndpoint;
        
        /// <summary>
        /// Ordered list of configuration loading strategies. The first one that returns true wins.
        /// Strategies are added in LoadStrategies() and can be extended by derived classes.
        /// </summary>
        private List<AuthLoaderStrategy> _strategies = new List<AuthLoaderStrategy>();

        /// <summary>
        /// Unity lifecycle callback executed when the component becomes active.
        /// Automatically triggers the authentication configuration loading process
        /// using multiple fallback strategies to ensure robust configuration discovery.
        /// </summary>
        protected override void OnEnable() { }

        /// <summary>
        /// Unity inspector callback that resets component settings to default values.
        /// Called when the component is first added or when "Reset" is selected in the inspector.
        /// Override in derived classes to provide authentication scheme-specific default values.
        /// </summary>
        protected virtual void Reset()
        {
            if (string.IsNullOrEmpty(authFileName))
                authFileName = DefaultAuthFileName;
        }

        /// <summary>
        /// Ensures serialized fields are valid after changes in the Inspector.
        /// Backfills authFileName with DefaultAuthFileName if empty and computes urlQuery from it.
        /// </summary>
        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(authFileName))
                authFileName = DefaultAuthFileName;
            urlQuery = Path.GetFileNameWithoutExtension(authFileName);
        }

        /// <summary>
        /// Attempts to construct a typed configuration object from a key-value dictionary.
        /// Parses authentication parameters and validates required fields for the specific
        /// authentication scheme. Must be implemented by derived classes to handle
        /// authentication scheme-specific parameter parsing and validation.
        /// </summary>
        /// <param name="values">Dictionary containing configuration key-value pairs loaded from various sources</param>
        /// <param name="config">Output parameter containing the constructed configuration object if successful</param>
        /// <returns>True if configuration was successfully built and validated, false otherwise</returns>
        protected abstract bool TryBuildConfig(IDictionary<string, string> values, out TConfig config);

        /// <summary>
        /// Applies the loaded authentication configuration to the specified endpoint.
        /// Configures endpoint-specific authentication settings and validates the
        /// configuration compatibility. Must be implemented by derived classes to handle
        /// authentication scheme-specific endpoint configuration.
        /// </summary>
        /// <param name="endpoint">Target endpoint to configure with authentication settings</param>
        /// <param name="config">Configuration object containing validated authentication parameters</param>
        protected abstract void ApplyConfig(TEndpoint endpoint, TConfig config);

        /// <summary>
        /// Unity lifecycle callback executed when the GameObject is instantiated.
        /// Performs early initialization and component discovery for authentication loading.
        /// Ensures proper component setup before configuration loading begins.
        /// </summary>
        protected virtual void Awake()
        {
            if (!enabled) return;

            if (targetEndpoint == null)
                targetEndpoint = GetComponent<TEndpoint>();

            LoadStrategies();

            if (_strategies.Any(strategy => strategy()))
            {
                return;
            }
        }

        /// Registers a loading strategy to be considered during initialization.
        /// Strategies are evaluated in the order they are added.
        /// </summary>
        /// <param name="strategy">A function that attempts to load and apply configuration.</param>
        protected void AddLoaderStrategy(AuthLoaderStrategy strategy)
            => _strategies.Add(strategy);

        /// <summary>
        /// Populates the list of loading strategies based on the current platform.
        /// WebGL favors URL-based loading, while other platforms include CLI and file-based paths.
        /// Derived classes may override to alter the order or add/remove strategies.
        /// </summary>
        protected virtual void LoadStrategies()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            AddLoaderStrategy(LoadFromUrl);
            AddLoaderStrategy(LoadFromStreamingAssetsPath);
#else
            AddLoaderStrategy(LoadFromCommandLine);
            AddLoaderStrategy(LoadFromBaseDirectory);
            AddLoaderStrategy(LoadFromPersistentDataPath);
            AddLoaderStrategy(LoadFromStreamingAssetsPath);
#endif
        }

        protected bool LoadFromBaseDirectory()
            => LoadFromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, authFileName));
        
        protected bool LoadFromPersistentDataPath()
            => LoadFromFile(Path.Combine(Application.persistentDataPath, authFileName));

        protected bool LoadFromStreamingAssetsPath()
            => LoadFromFile(Path.Combine(Application.streamingAssetsPath, authFileName));

        /// <summary>
        /// Attempts to load authentication configuration from a local file path.
        /// Supports JSON format configuration files with key-value parameter structure.
        /// Handles file system access, JSON parsing, and configuration object construction
        /// with comprehensive error handling and logging.
        /// </summary>
        /// <param name="filePath">Absolute or relative path to the authentication configuration file</param>
        /// <returns>True if file was successfully loaded and configuration applied, false otherwise</returns>
        protected bool LoadFromFile(string filePath)
        {
            // Validate file existence before attempting to load
            if (!FileSystem.Current.FileExists(filePath))
            {
                DebugLog.OmiLAXR.Warning($"({GetType().Name}) No config at '{filePath}'.");
                return false;
            }

            try
            {
                // Read and parse JSON configuration file
                var content = FileSystem.Current.ReadAllText(filePath).Trim();
                if (string.IsNullOrEmpty(content))
                    return false;
                
                // Attempt to build typed configuration from parsed values
                var config = JsonUtility.FromJson<TConfig>(content);

                ApplyConfig(targetEndpoint, config);
                DebugLog.OmiLAXR.Print($"({GetType().Name}) ⚙️ Loaded from file '{filePath}'.");
                return true;
            }
            catch (Exception ex)
            {
                DebugLog.OmiLAXR.Error($"({GetType().Name}) ❌ Error reading '{filePath}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Asynchronously loads authentication configuration from a web URL endpoint.
        /// Supports remote configuration management with HTTP/HTTPS protocols.
        /// Implements Unity coroutine pattern for non-blocking web requests with
        /// timeout handling and error recovery mechanisms.
        /// </summary>
        /// <param name="url">HTTP or HTTPS URL pointing to authentication configuration endpoint</param>
        /// <returns>Coroutine enumerator for Unity's coroutine system execution</returns>
        protected System.Collections.IEnumerator LoadFromWeb(string url)
        {
            using (var www = UnityEngine.Networking.UnityWebRequest.Get(url))
            {
                yield return www.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
                if (www.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
#else
                if (www.isNetworkError || www.isHttpError)
#endif
                {
                    DebugLog.OmiLAXR.Error($"({GetType().Name}) ❌ Failed to load JSON from '{url}': {www.error}");
                    yield break;
                }

                try
                {
                    var content = www.downloadHandler.text.Trim();
                    if (string.IsNullOrEmpty(content))
                        yield break;

                    var config = JsonUtility.FromJson<TConfig>(content);

                    ApplyConfig(targetEndpoint, config);
                    DebugLog.OmiLAXR.Print($"({GetType().Name}) ⚙️ Loaded config from '{url}'.");
                }
                catch (Exception ex)
                {
                    DebugLog.OmiLAXR.Error($"({GetType().Name}) ❌ JSON parse error from '{url}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Attempts to load authentication configuration from command-line arguments.
        /// Parses application startup arguments for authentication parameters with
        /// standardized prefix patterns. Enables runtime configuration without file dependencies
        /// for containerized and automated deployment scenarios.
        /// </summary>
        /// <returns>True if command-line arguments contained valid authentication configuration, false otherwise</returns>
        protected bool LoadFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var arg in args)
            {
                if (arg.StartsWith("-") && arg.Contains("="))
                {
                    var kv = arg.Substring(1).Split(new[] { '=' }, 2);
                    if (kv.Length == 2)
                        dict[kv[0]] = kv[1];
                }
            }

            if (!TryBuildConfig(dict, out var config))
                return false;

            ApplyConfig(targetEndpoint, config);
            DebugLog.OmiLAXR.Print($"({GetType().Name}) Loaded from CLI args.");
            return true;
        }

        /// <summary>
        /// Attempts to load authentication configuration from a URL parameter or environment variable.
        /// Supports dynamic configuration loading based on runtime environment setup.
        /// Useful for cloud deployments and dynamic configuration scenarios where
        /// authentication endpoints are determined at runtime.
        /// </summary>
        /// <returns>True if URL-based configuration was successfully loaded and applied, false otherwise</returns>
        protected bool LoadFromUrl()
        {
            var url = Application.absoluteURL;
            if (string.IsNullOrEmpty(url))
                return false;

            var queries = new WebQueries(url);

            // Case 1: remote JSON if query has key == authFileName
            var remote = queries.Get(urlQuery, true);
            if (!string.IsNullOrEmpty(remote))
            {
                StartCoroutine(LoadFromWeb(remote));
                return true;
            }

            DebugLog.OmiLAXR.Warning($"({GetType().Name}) '{urlQuery}' query is not set in URL.");

            // Case 2: parse query parameters
            if (!TryBuildConfig(queries.Queries, out var config))
            {
                DebugLog.OmiLAXR.Error($"({GetType().Name}) Cannot find any '${GetType().Name}' queries in the URL.");
                return false;
            }

            ApplyConfig(targetEndpoint, config);
            DebugLog.OmiLAXR.Print($"({GetType().Name}) Loaded from URL query.");
            return true;
        }
    }
}