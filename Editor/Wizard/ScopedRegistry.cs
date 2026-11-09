/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor.PackageManager;

namespace OmiLAXR.Editor.Wizard
{
    /// <summary>
    /// Reads and updates the scoped registry entry for OmiLAXR packages in <c>Packages/manifest.json</c>.
    /// </summary>
    public static class ScopedRegistry
    {
        public const string Scope = "com.rwth.unity.omilaxr";
        public const string RegistryName = "npmjs";
        public const string RegistryUrl = "https://registry.npmjs.com";
        public const string ManifestPath = "Packages/manifest.json";

        /// <summary>
        /// Returns true if the project manifest has a scoped registry that covers OmiLAXR packages.
        /// </summary>
        public static bool IsConfigured()
        {
            return File.Exists(ManifestPath) && HasScope(JObject.Parse(File.ReadAllText(ManifestPath)));
        }

        /// <summary>
        /// Adds the OmiLAXR scope to the project manifest and lets the Package Manager resolve again.
        /// Does nothing if the scope is already configured.
        /// </summary>
        public static void Configure()
        {
            var manifest = JObject.Parse(File.ReadAllText(ManifestPath));
            if (!EnsureScope(manifest))
                return;

            File.WriteAllText(ManifestPath, manifest.ToString(Formatting.Indented));
            Client.Resolve();
            DebugLog.OmiLAXR.Print($"Added scoped registry '{RegistryUrl}' with scope '{Scope}' to '{ManifestPath}'.");
        }

        /// <summary>
        /// Returns true if any scoped registry in the manifest has a scope that matches the OmiLAXR packages.
        /// A scope matches if it equals the package scope or is a parent namespace of it (e.g. <c>com.rwth.unity</c>).
        /// </summary>
        public static bool HasScope(JObject manifest)
        {
            if (!(manifest["scopedRegistries"] is JArray registries))
                return false;

            return registries
                .OfType<JObject>()
                .SelectMany(r => r["scopes"] as JArray ?? new JArray())
                .Select(s => (string)s)
                .Any(s => s == Scope || (s != null && Scope.StartsWith(s + ".", StringComparison.Ordinal)));
        }

        /// <summary>
        /// Adds the OmiLAXR scope to the manifest. If a registry with the npmjs URL already exists, the scope is
        /// added to it, otherwise a new registry entry is created.
        /// </summary>
        /// <returns>True if the manifest was changed.</returns>
        public static bool EnsureScope(JObject manifest)
        {
            if (HasScope(manifest))
                return false;

            if (!(manifest["scopedRegistries"] is JArray registries))
            {
                registries = new JArray();
                manifest["scopedRegistries"] = registries;
            }

            var registry = registries
                .OfType<JObject>()
                .FirstOrDefault(r => NormalizeUrl((string)r["url"]) == NormalizeUrl(RegistryUrl));

            if (registry == null)
            {
                registries.Add(new JObject
                {
                    ["name"] = RegistryName,
                    ["url"] = RegistryUrl,
                    ["scopes"] = new JArray(Scope)
                });
                return true;
            }

            if (!(registry["scopes"] is JArray scopes))
            {
                scopes = new JArray();
                registry["scopes"] = scopes;
            }
            scopes.Add(Scope);
            return true;
        }

        private static string NormalizeUrl(string url)
        {
            if (url == null)
                return null;
            url = url.Trim().TrimEnd('/').ToLowerInvariant();
            return url.StartsWith("http://") ? "https://" + url.Substring("http://".Length) : url;
        }
    }
}
#endif
