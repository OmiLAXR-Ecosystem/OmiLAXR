using System.Collections.Generic;
using NUnit.Framework;
using OmiLAXR.Components;
using OmiLAXR.Filters;
using UnityEngine;

namespace OmiLAXR.Tests.Filters
{
    public class NameFilters_Tests
    {
        private GameObject _filterObject;
        private readonly List<Object> _created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _filterObject = new GameObject("Filters");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created)
                Object.DestroyImmediate(o);
            _created.Clear();
            Object.DestroyImmediate(_filterObject);
        }

        private GameObject CreateObject(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        [Test]
        public void BlacklistFilter_Removes_Blacklisted_Tracking_Names()
        {
            // Arrange
            var filter = _filterObject.AddComponent<BlacklistFilter>();
            filter.blacklist = new List<string> { "Blocked", "Renamed" };
            var allowed = CreateObject("Allowed");
            var blocked = CreateObject("Blocked");
            var renamed = CreateObject("Original");
            renamed.AddComponent<CustomTrackingName>().customTrackingName = "Renamed";

            // Act
            var result = filter.Pass(new Object[] { allowed, blocked, renamed });

            // Assert
            CollectionAssert.AreEqual(new Object[] { allowed }, result);
        }

        [Test]
        public void ExcludeFromTrackingFilter_Removes_Marked_Objects()
        {
            // Arrange
            var filter = _filterObject.AddComponent<ExcludeFromTrackingFilter>();
            var tracked = CreateObject("Tracked");
            var excluded = CreateObject("Excluded");
            excluded.AddComponent<ExcludeFromTracking>();
            var componentOfExcluded = excluded.AddComponent<BoxCollider>();

            // Act
            var result = filter.Pass(new Object[] { tracked, excluded, componentOfExcluded });

            // Assert
            CollectionAssert.AreEqual(new Object[] { tracked }, result);
        }
    }
}
