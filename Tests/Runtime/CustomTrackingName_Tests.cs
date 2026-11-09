using NUnit.Framework;
using UnityEngine;
using OmiLAXR.Components;

namespace OmiLAXR.Tests
{
    public class CustomTrackingName_Tests
    {
        [Test]
        public void CustomTrackingName_Tests_Returns_GameObject_Name_When_No_CustomTrackingName_Component()
        {
            // Arrange
            var go = new GameObject("TestObject");

            // Act
            var trackingName = go.GetTrackingName();

            // Assert
            Assert.AreEqual("TestObject", trackingName);
        }

        [Test]
        public void CustomTrackingName_Tests_Returns_CustomTrackingName_When_Component_Is_Present()
        {
            // Arrange
            var go = new GameObject("TestObject");
            var customTrackingName = go.AddComponent<CustomTrackingName>();
            customTrackingName.customTrackingName = "CustomName";

            // Act
            var trackingName = go.GetTrackingName();

            // Assert
            Assert.AreEqual("CustomName", trackingName);
        }

        [Test]
        public void CustomTrackingName_Tests_Returns_Component_GameObject_Name_When_No_CustomTrackingName_Component()
        {
            // Arrange
            var go = new GameObject("TestObject");
            var component = go.AddComponent<BoxCollider>();

            // Act
            var trackingName = component.GetTrackingName();

            // Assert
            Assert.AreEqual("TestObject", trackingName);
        }

        [Test]
        public void CustomTrackingName_Tests_Returns_CustomTrackingName_When_Component_Is_Present_On_GameObject()
        {
            // Arrange
            var go = new GameObject("TestObject");
            var component = go.AddComponent<BoxCollider>();
            var customTrackingName = go.AddComponent<CustomTrackingName>();
            customTrackingName.customTrackingName = "CustomName";

            // Act
            var trackingName = component.GetTrackingName();

            // Assert
            Assert.AreEqual("CustomName", trackingName);
        }

        [Test]
        public void CustomTrackingName_Tests_Returns_Component_Name_If_Component_Has_CustomTrackingName()
        {
            // Arrange
            var go = new GameObject("TestObject");
            var component = go.AddComponent<BoxCollider>();
            var customTrackingName = go.AddComponent<CustomTrackingName>();
            customTrackingName.customTrackingName = "CustomName";

            // Act
            var trackingName = component.GetTrackingName();

            // Assert
            Assert.AreEqual("CustomName", trackingName);
        }

        [Test]
        public void CustomTrackingName_Tests_Returns_Object_Name_For_Non_GameObject_Or_Component()
        {
            // Arrange
            var material = new Material(Shader.Find("Standard"))
            {
                name = "TestMaterial"
            };

            // Act
            var trackingName = material.GetTrackingName();

            // Assert
            Assert.AreEqual("TestMaterial", trackingName);
        }
    }
}