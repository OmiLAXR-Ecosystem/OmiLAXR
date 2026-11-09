/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using OmiLAXR.Editor.Wizard;

namespace OmiLAXR.Tests.Editor
{
    public class McpConfig_Tests
    {
        [Test]
        public void Apply_Adds_Server_For_Claude_Code_With_Relative_Paths()
        {
            // Arrange
            var config = new JObject();

            // Act
            McpConfig.Apply(config, McpClient.ClaudeCode, McpRunner.Dnx, "Jane Doe <jane@example.org>", "/projects/MyGame");

            // Assert
            var server = (JObject)config["mcpServers"][McpConfig.ServerName];
            Assert.AreEqual("dnx", (string)server["command"]);
            Assert.AreEqual(new[] { "OmiLAXR.MCP", "--yes" }, server["args"].ToObject<string[]>());
            Assert.AreEqual(".", (string)server["env"]["OMILAXR_UNITY_PROJECT"]);
            Assert.AreEqual("Jane Doe <jane@example.org>", (string)server["env"]["OMILAXR_AUTHOR"]);
            Assert.IsNull(server["type"]);
        }

        [Test]
        public void Apply_Uses_Servers_Key_And_Workspace_Folder_For_VSCode()
        {
            // Arrange
            var config = new JObject();

            // Act
            McpConfig.Apply(config, McpClient.VSCode, McpRunner.InstalledTool, null, "/projects/MyGame");

            // Assert
            Assert.IsNull(config["mcpServers"]);
            var server = (JObject)config["servers"][McpConfig.ServerName];
            Assert.AreEqual("stdio", (string)server["type"]);
            Assert.AreEqual("omilaxr-mcp", (string)server["command"]);
            Assert.AreEqual("${workspaceFolder}", (string)server["env"]["OMILAXR_WORKSPACE"]);
            Assert.IsNull(server["env"]["OMILAXR_AUTHOR"]);
        }

        [Test]
        public void Apply_Uses_Absolute_Project_Path_For_Cursor()
        {
            // Arrange
            var config = new JObject();

            // Act
            McpConfig.Apply(config, McpClient.Cursor, McpRunner.Dnx, "", @"C:\projects\MyGame");

            // Assert
            var server = (JObject)config["mcpServers"][McpConfig.ServerName];
            Assert.AreEqual("C:/projects/MyGame", (string)server["env"]["OMILAXR_UNITY_PROJECT"]);
        }

        [Test]
        public void Apply_Keeps_Other_Servers_And_Replaces_Own_Entry()
        {
            // Arrange
            var config = JObject.Parse(
                "{\"mcpServers\":{\"other\":{\"command\":\"other-mcp\"},\"omilaxr\":{\"command\":\"old\"}}}");

            // Act
            McpConfig.Apply(config, McpClient.ClaudeCode, McpRunner.InstalledTool, null, "/projects/MyGame");

            // Assert
            var servers = (JObject)config["mcpServers"];
            Assert.AreEqual("other-mcp", (string)servers["other"]["command"]);
            Assert.AreEqual("omilaxr-mcp", (string)servers[McpConfig.ServerName]["command"]);
        }
    }
}
