/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OmiLAXR.Editor
{
    /// <summary>
    /// Provides custom Unity Editor menu items for OmiLAXR package setup and utilities.
    /// Contains static methods that appear in the Unity Editor menu bar for common development tasks
    /// such as creating configuration files and setting up the project structure.
    /// </summary>
    internal static class CustomMenu
    {
        /// <summary>
        /// Creates a credentials.json file in the StreamingAssets folder for OmiLAXR configuration.
        /// Copies the example credentials file from the package resources to the project's StreamingAssets.
        /// Prevents overwriting existing credentials files and ensures the StreamingAssets folder exists.
        /// </summary>
        [MenuItem("OmiLAXR / Create 'credentials.json' file")]
        internal static void CreateCredentialsFile()
        {
            var destFilePath = Path.Combine(Application.streamingAssetsPath, "credentials.json");
            
            // Prevent overwriting existing credentials file
            if (File.Exists(destFilePath))
            {
                DebugLog.OmiLAXR.Error($"There is already a file '{destFilePath}'.");
                return;
            }
            
            // Ensure the StreamingAssets folder exists before copying files
            if (!Directory.Exists(Application.streamingAssetsPath))
            {
                Directory.CreateDirectory(Application.streamingAssetsPath);
            }
            
            // Copy the example credentials file from package resources to project
            var credentialsPath = Path.GetFullPath("Packages/com.rwth.unity.omilaxr/Resources/StreamingAssets/example.credentials.json");
            File.Copy(credentialsPath, destFilePath, false);
            
            // Refresh asset view
            AssetDatabase.Refresh();
        }
        /// <summary>
        /// Creates a bearer.json file in the StreamingAssets folder for OmiLAXR configuration.
        /// Copies the example credentials file from the package resources to the project's StreamingAssets.
        /// Prevents overwriting existing credentials files and ensures the StreamingAssets folder exists.
        /// </summary>
        [MenuItem("OmiLAXR / Create 'bearer.json' file")]
        internal static void CreateBearerAuthFile()
        {
            var destFilePath = Path.Combine(Application.streamingAssetsPath, "bearer.json");
            
            // Prevent overwriting existing credentials file
            if (File.Exists(destFilePath))
            {
                DebugLog.OmiLAXR.Error($"There is already a file '{destFilePath}'.");
                return;
            }
            
            // Ensure the StreamingAssets folder exists before copying files
            if (!Directory.Exists(Application.streamingAssetsPath))
            {
                Directory.CreateDirectory(Application.streamingAssetsPath);
            }
            
            // Copy the example credentials file from package resources to project
            var credentialsPath = Path.GetFullPath("Packages/com.rwth.unity.omilaxr/Resources/StreamingAssets/example.bearer.json");
            File.Copy(credentialsPath, destFilePath, false);
            
            // Refresh asset view
            AssetDatabase.Refresh();
        }

    }
}
#endif