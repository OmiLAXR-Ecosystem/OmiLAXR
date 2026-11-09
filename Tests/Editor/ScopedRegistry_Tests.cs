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
    public class ScopedRegistry_Tests
    {
        [Test]
        public void EnsureScope_Adds_Registry_To_Empty_Manifest()
        {
            // Arrange
            var manifest = JObject.Parse("{\"dependencies\":{}}");

            // Act
            var changed = ScopedRegistry.EnsureScope(manifest);

            // Assert
            Assert.IsTrue(changed);
            var registry = (JObject)manifest["scopedRegistries"][0];
            Assert.AreEqual(ScopedRegistry.RegistryUrl, (string)registry["url"]);
            Assert.AreEqual(ScopedRegistry.Scope, (string)registry["scopes"][0]);
            Assert.IsTrue(ScopedRegistry.HasScope(manifest));
        }

        [Test]
        public void EnsureScope_Does_Nothing_When_Scope_Exists()
        {
            // Arrange
            var manifest = JObject.Parse(
                "{\"scopedRegistries\":[{\"name\":\"npmjs\",\"url\":\"https://registry.npmjs.com\",\"scopes\":[\"com.rwth.unity.omilaxr\"]}]}");
            var before = manifest.ToString();

            // Act
            var changed = ScopedRegistry.EnsureScope(manifest);

            // Assert
            Assert.IsFalse(changed);
            Assert.AreEqual(before, manifest.ToString());
        }

        [Test]
        public void HasScope_Accepts_Parent_Scope()
        {
            // Arrange
            var manifest = JObject.Parse(
                "{\"scopedRegistries\":[{\"name\":\"rwth\",\"url\":\"https://example.org\",\"scopes\":[\"com.rwth.unity\"]}]}");

            // Act
            var hasScope = ScopedRegistry.HasScope(manifest);

            // Assert
            Assert.IsTrue(hasScope);
        }

        [Test]
        public void HasScope_Rejects_Scope_That_Only_Shares_A_Prefix()
        {
            // Arrange
            var manifest = JObject.Parse(
                "{\"scopedRegistries\":[{\"name\":\"x\",\"url\":\"https://example.org\",\"scopes\":[\"com.rwth.unity.omi\"]}]}");

            // Act
            var hasScope = ScopedRegistry.HasScope(manifest);

            // Assert
            Assert.IsFalse(hasScope);
        }

        [Test]
        public void EnsureScope_Extends_Existing_Npmjs_Registry_Instead_Of_Adding_A_Second_One()
        {
            // Arrange
            var manifest = JObject.Parse(
                "{\"scopedRegistries\":[{\"name\":\"npm\",\"url\":\"http://registry.npmjs.com/\",\"scopes\":[\"com.example\"]}]}");

            // Act
            var changed = ScopedRegistry.EnsureScope(manifest);

            // Assert
            Assert.IsTrue(changed);
            var registries = (JArray)manifest["scopedRegistries"];
            Assert.AreEqual(1, registries.Count);
            var scopes = (JArray)registries[0]["scopes"];
            Assert.AreEqual(2, scopes.Count);
            Assert.AreEqual(ScopedRegistry.Scope, (string)scopes[1]);
        }
    }
}
