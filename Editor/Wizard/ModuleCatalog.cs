/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace OmiLAXR.Editor.Wizard
{
    /// <summary>
    /// An OmiLAXR package as seen by the wizard: what the registry offers and what the project has installed.
    /// </summary>
    internal sealed class ModuleEntry
    {
        public string Name;
        public string DisplayName;
        public string Description;
        /// <summary>Installed version, or null if the package is not installed.</summary>
        public string InstalledVersion;
        /// <summary>Latest compatible version in the registry, or null if the registry does not list the package.</summary>
        public string LatestVersion;
        /// <summary>Where the installed package comes from, e.g. registry, local folder or git.</summary>
        public PackageSource Source;

        public bool IsInstalled => InstalledVersion != null;
        /// <summary>Only packages from a registry can be updated or removed from the window. Local and git packages are left alone.</summary>
        public bool IsManagedByRegistry => !IsInstalled || Source == PackageSource.Registry;
        public bool HasUpdate => IsInstalled && IsManagedByRegistry && LatestVersion != null && LatestVersion != InstalledVersion;
    }

    /// <summary>
    /// Lists, installs, updates and removes OmiLAXR packages through the Unity Package Manager client.
    /// Requests run one after another and are polled in <see cref="EditorApplication.update"/>.
    /// </summary>
    internal sealed class ModuleCatalog
    {
        public IReadOnlyList<ModuleEntry> Modules => _modules;
        public bool IsBusy => _request != null;
        public string Status { get; private set; }
        public string Error { get; private set; }

        /// <summary>Raised after a request finished, so the window can repaint.</summary>
        public event Action Changed;

        private List<ModuleEntry> _modules = new List<ModuleEntry>();
        private Request _request;
        private Action<Request> _onDone;
        private PackageInfo[] _installed;

        public void Refresh()
        {
            Run(Client.List(true, true), "Reading installed packages…", r =>
            {
                _installed = ((ListRequest)r).Result.ToArray();
                // Installed packages are known even if the registry search below fails
                _modules = Merge(_installed, Enumerable.Empty<PackageInfo>());
                Run(Client.SearchAll(), "Searching the registry for OmiLAXR packages…", s =>
                    _modules = Merge(_installed, ((SearchRequest)s).Result));
            });
        }

        /// <summary>
        /// Returns the entry of the package, or a placeholder if neither the registry nor the project lists it.
        /// </summary>
        public ModuleEntry Get(string name)
        {
            return _modules.FirstOrDefault(m => m.Name == name) ?? new ModuleEntry { Name = name };
        }

        public void Install(ModuleEntry module)
        {
            var id = module.LatestVersion != null ? $"{module.Name}@{module.LatestVersion}" : module.Name;
            Run(Client.Add(id), $"Installing {id}…", _ => Refresh());
        }

        public void Remove(ModuleEntry module)
        {
            Run(Client.Remove(module.Name), $"Removing {module.Name}…", _ => Refresh());
        }

        private static List<ModuleEntry> Merge(IEnumerable<PackageInfo> installed, IEnumerable<PackageInfo> available)
        {
            var entries = new Dictionary<string, ModuleEntry>();

            foreach (var p in available.Where(p => IsOmiLAXRPackage(p.name)))
            {
                entries[p.name] = new ModuleEntry
                {
                    Name = p.name,
                    DisplayName = p.displayName,
                    Description = p.description,
                    LatestVersion = string.IsNullOrEmpty(p.versions.latestCompatible) ? p.versions.latest : p.versions.latestCompatible
                };
            }

            foreach (var p in installed.Where(p => IsOmiLAXRPackage(p.name)))
            {
                if (!entries.TryGetValue(p.name, out var entry))
                {
                    entry = new ModuleEntry { Name = p.name };
                    entries[p.name] = entry;
                }
                entry.DisplayName = p.displayName;
                entry.Description = p.description;
                entry.InstalledVersion = p.version;
                entry.Source = p.source;
            }

            return entries.Values
                .OrderBy(e => e.Name == ScopedRegistry.Scope ? 0 : 1)
                .ThenBy(e => e.DisplayName ?? e.Name)
                .ToList();
        }

        private static bool IsOmiLAXRPackage(string name)
            => name == ScopedRegistry.Scope || name.StartsWith(ScopedRegistry.Scope + ".", StringComparison.Ordinal);

        private void Run(Request request, string status, Action<Request> onDone)
        {
            _request = request;
            _onDone = onDone;
            Status = status;
            Error = null;
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        private void Poll()
        {
            if (_request == null || !_request.IsCompleted)
                return;

            var request = _request;
            var onDone = _onDone;
            _request = null;
            _onDone = null;
            Status = null;
            EditorApplication.update -= Poll;

            if (request.Status == StatusCode.Failure)
                Error = request.Error?.message ?? "Unknown Package Manager error.";
            else
                onDone(request);

            Changed?.Invoke();
        }

        /// <summary>Stops polling, e.g. when the window closes. A running request still finishes in the Package Manager.</summary>
        public void Dispose()
        {
            EditorApplication.update -= Poll;
        }
    }
}
#endif
