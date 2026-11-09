/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
#if UNITY_EDITOR
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace OmiLAXR.Editor.Wizard
{
    /// <summary>
    /// AI coding agents that read a per-project MCP configuration file.
    /// </summary>
    public enum McpClient
    {
        ClaudeCode,
        Cursor,
        VSCode
    }

    /// <summary>
    /// How the agent starts the OmiLAXR MCP server.
    /// </summary>
    public enum McpRunner
    {
        /// <summary><c>dnx OmiLAXR.MCP --yes</c>: runs the NuGet tool without installing it. Needs the .NET 10 SDK.</summary>
        Dnx,
        /// <summary><c>omilaxr-mcp</c>: the globally installed dotnet tool (<c>dotnet tool install -g OmiLAXR.MCP</c>).</summary>
        InstalledTool
    }

    /// <summary>
    /// Writes the OmiLAXR MCP server into the MCP configuration files of AI coding agents in the Unity project root.
    /// Other servers in those files are kept.
    /// </summary>
    public static class McpConfig
    {
        public const string ServerName = "omilaxr";

        /// <summary>
        /// Unity project root, the folder that contains <c>Assets</c>.
        /// </summary>
        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        /// <summary>
        /// Path of the client's configuration file relative to the project root.
        /// </summary>
        public static string RelativePath(McpClient client)
        {
            switch (client)
            {
                case McpClient.Cursor: return ".cursor/mcp.json";
                case McpClient.VSCode: return ".vscode/mcp.json";
                default: return ".mcp.json";
            }
        }

        /// <summary>
        /// Returns true if the client's configuration file in this project contains the OmiLAXR server.
        /// </summary>
        public static bool IsConfigured(McpClient client)
        {
            var path = Path.Combine(ProjectRoot, RelativePath(client));
            if (!File.Exists(path))
                return false;
            try
            {
                return Servers(JObject.Parse(File.ReadAllText(path)), client, false)?[ServerName] != null;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        /// <summary>
        /// Adds or replaces the OmiLAXR server in the client's configuration file of this project.
        /// </summary>
        /// <returns>The written file path.</returns>
        public static string Write(McpClient client, McpRunner runner, string author)
        {
            var path = Path.Combine(ProjectRoot, RelativePath(client));
            var config = File.Exists(path) ? JObject.Parse(File.ReadAllText(path)) : new JObject();

            Apply(config, client, runner, author, ProjectRoot);

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, config.ToString(Formatting.Indented) + "\n");
            return path;
        }

        /// <summary>
        /// Sets the OmiLAXR server entry in a client configuration. The server reads its settings from environment
        /// variables, so the same arguments work for every runner.
        /// </summary>
        public static void Apply(JObject config, McpClient client, McpRunner runner, string author, string projectRoot)
        {
            // Claude Code starts servers in the project root. VS Code expands ${workspaceFolder}. Cursor gets the absolute path.
            var root = client == McpClient.ClaudeCode ? "."
                : client == McpClient.VSCode ? "${workspaceFolder}"
                : projectRoot.Replace('\\', '/');

            var env = new JObject
            {
                ["OMILAXR_WORKSPACE"] = root,
                ["OMILAXR_UNITY_PROJECT"] = root,
                ["OMILAXR_WRITE_ROOTS"] = root
            };
            if (!string.IsNullOrWhiteSpace(author))
                env["OMILAXR_AUTHOR"] = author.Trim();

            var server = new JObject();
            if (client == McpClient.VSCode)
                server["type"] = "stdio";
            server["command"] = runner == McpRunner.Dnx ? "dnx" : "omilaxr-mcp";
            server["args"] = runner == McpRunner.Dnx ? new JArray("OmiLAXR.MCP", "--yes") : new JArray();
            server["env"] = env;

            Servers(config, client, true)[ServerName] = server;
        }

        // VS Code calls the list "servers", the other clients "mcpServers"
        private static JObject Servers(JObject config, McpClient client, bool create)
        {
            var key = client == McpClient.VSCode ? "servers" : "mcpServers";
            if (config[key] is JObject servers)
                return servers;
            if (!create)
                return null;
            servers = new JObject();
            config[key] = servers;
            return servers;
        }
    }
}
#endif
