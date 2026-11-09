using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OmiLAXR.Endpoints;
using OmiLAXR.Tests.Mocks;
using OmiLAXR.Utils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OmiLAXR.Tests.Endpoints
{
    public class FileEndpoint_Tests
    {
        private GameObject _dataProviderObject;
        private GameObject _endpointObject;
        private string _statementsFolder;

        [SetUp]
        public void SetUp()
        {
            _statementsFolder = "omilaxr_tests_" + Guid.NewGuid().ToString("N");
            _dataProviderObject = new GameObject("Data Provider");
            _dataProviderObject.AddComponent<DataProvider>();
            _endpointObject = new GameObject("Endpoint");
            _endpointObject.SetActive(false);
            _endpointObject.transform.SetParent(_dataProviderObject.transform);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_dataProviderObject);
            var folder = Path.Combine(Path.GetTempPath(), _statementsFolder);
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }

        private T CreateEndpoint<T>(string identifier = null) where T : LocalFileEndpoint
        {
            var endpoint = _endpointObject.AddComponent<T>();
            endpoint.ConsumeDataMap(new DataMap
            {
                ["statementsFolder"] = _statementsFolder,
                ["identifier"] = identifier,
                ["customLocation"] = null,
                ["defaultFolder"] = nameof(LocalFileEndpoint.DefaultFolderPaths.TempFolder),
                ["oneFilePerComposer"] = false,
                // only read by CsvFileEndpoint
                ["flatten"] = true,
                ["includePattern"] = "",
                ["excludePattern"] = "",
            });
            return endpoint;
        }

        [Test]
        public void LocalFileEndpoint_Writes_One_Json_Line_Per_Statement()
        {
            // Arrange
            var endpoint = CreateEndpoint<LocalFileEndpoint>();
            _endpointObject.SetActive(true);
            var path = (string)endpoint.ProvideDataMap()["resolvedSingleFilePath"];

            // Act
            endpoint.SendStatement(new MockStatement("a"));
            endpoint.SendStatement(new MockStatement("b"));
            endpoint.StopSending();
            _endpointObject.SetActive(false);

            // Assert
            StringAssert.EndsWith(".jsonl", path);
            StringAssert.Contains(_statementsFolder, path);
            CollectionAssert.AreEquivalent(new[] { "{\"text\":\"a\"}", "{\"text\":\"b\"}" }, File.ReadAllLines(path));
        }

        [Test]
        public void LocalFileEndpoint_Identifier_Defaults_To_Timestamp()
        {
            // Arrange
            var endpoint = CreateEndpoint<LocalFileEndpoint>("{yyyyMMdd}");

            // Assert
            Assert.AreEqual(DateTime.Now.ToString("yyyyMMdd"), endpoint.ResolvedIdentifier);
        }

        [Test]
        public void LocalFileEndpoint_Uses_Plain_Identifier_As_File_Name()
        {
            // Arrange
            var endpoint = CreateEndpoint<LocalFileEndpoint>("session-1");

            // Assert
            Assert.AreEqual("session-1", endpoint.ResolvedIdentifier);
        }

        [Test]
        public void LocalFileEndpoint_DataMap_Round_Trip()
        {
            // Arrange
            var endpoint = CreateEndpoint<LocalFileEndpoint>("{yyyy}");

            // Act
            var map = endpoint.ProvideDataMap();

            // Assert
            Assert.AreEqual(_statementsFolder, map["statementsFolder"]);
            Assert.AreEqual("{yyyy}", map["identifier"]);
            Assert.AreEqual("TempFolder", map["defaultFolder"]);
            Assert.AreEqual(false, map["oneFilePerComposer"]);
            Assert.AreEqual(Path.GetTempPath(), map["resolvedFolder"]);
            Assert.AreEqual(Path.Combine(Path.GetTempPath(), _statementsFolder), map["resolvedStatementsFolderPath"]);
        }

        [Test]
        public void LocalFileEndpoint_Resolves_Default_Folders()
        {
            // Arrange
            var endpoint = CreateEndpoint<LocalFileEndpoint>();

            // Act & Assert
            endpoint.defaultFolder = LocalFileEndpoint.DefaultFolderPaths.PersistantDataPath;
            StringAssert.StartsWith(Application.persistentDataPath, endpoint.ResolvedStatementsFolderPath);
            endpoint.defaultFolder = LocalFileEndpoint.DefaultFolderPaths.HomeFolder;
            StringAssert.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), endpoint.ResolvedStatementsFolderPath);
            endpoint.defaultFolder = LocalFileEndpoint.DefaultFolderPaths.Custom;
            endpoint.customLocation = "Logs";
            Assert.AreEqual(LocalFileEndpoint.ResolveRelativeToProject("Logs"), Path.GetDirectoryName(endpoint.ResolvedStatementsFolderPath));
        }

        [Test]
        public void CsvFileEndpoint_Writes_Header_And_Rows()
        {
            // Arrange
            var endpoint = CreateEndpoint<CsvFileEndpoint>();
            _endpointObject.SetActive(true);
            var path = (string)endpoint.ProvideDataMap()["resolvedSingleFilePath"];

            // Act
            endpoint.SendStatement(new MockStatement("a"));
            endpoint.SendStatement(new MockStatement("b"));
            endpoint.StopSending();
            _endpointObject.SetActive(false);

            // Assert
            var lines = File.ReadAllLines(path);
            StringAssert.EndsWith(".csv", path);
            Assert.AreEqual("id,text", lines[0]);
            CollectionAssert.AreEquivalent(new[] { "a", "b" }, lines.Skip(1).Select(l => l.Split(',')[1]));
            Assert.IsFalse(File.Exists(path + ".data"));
        }

        [Test]
        public void CsvFileEndpoint_Disable_Then_Destroy_Does_Not_Throw()
        {
            // Arrange
            var endpoint = CreateEndpoint<CsvFileEndpoint>();
            _endpointObject.SetActive(true);
            endpoint.SendStatement(new MockStatement("a"));
            endpoint.StopSending();

            // Act & Assert
            _endpointObject.SetActive(false);
            Assert.DoesNotThrow(() => Object.DestroyImmediate(_endpointObject));
        }

        [Test]
        public void CsvFileEndpoint_DataMap_Includes_Csv_Options()
        {
            // Arrange
            var endpoint = CreateEndpoint<CsvFileEndpoint>();
            var map = endpoint.ProvideDataMap();
            map["flatten"] = false;
            map["includePattern"] = "^id$";
            map["excludePattern"] = "";

            // Act
            endpoint.ConsumeDataMap(map);

            // Assert
            Assert.IsFalse(endpoint.flatten);
            Assert.AreEqual("^id$", endpoint.filterOptions.includePattern);
            Assert.AreEqual("^id$", endpoint.ProvideDataMap()["includePattern"]);
        }
    }
}
