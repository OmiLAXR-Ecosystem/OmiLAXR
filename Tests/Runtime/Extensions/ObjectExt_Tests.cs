using NUnit.Framework;
using OmiLAXR.Extensions;
using UnityEngine;

namespace OmiLAXR.Tests.Extensions
{
    public class ObjectExt_Tests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Root");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void ObjectExt_Tests_GetFullHierarchyPath_For_GameObject()
        {
            // Arrange
            var child = new GameObject("Child");
            child.transform.SetParent(_root.transform);
            var grandChild = new GameObject("GrandChild");
            grandChild.transform.SetParent(child.transform);

            // Assert
            Assert.AreEqual("Root", _root.GetFullHierarchyPath());
            Assert.AreEqual("Root/Child/GrandChild", grandChild.GetFullHierarchyPath());
        }

        [Test]
        public void ObjectExt_Tests_GetFullHierarchyPath_For_Component_Appends_Type()
        {
            // Arrange
            var collider = _root.AddComponent<BoxCollider>();

            // Assert
            Assert.AreEqual("Root/BoxCollider", collider.GetFullHierarchyPath());
        }

        [Test]
        public void ObjectExt_Tests_GetFullHierarchyPath_For_Non_Scene_Object_Is_Empty()
        {
            // Arrange
            var material = new Material(Shader.Find("Hidden/InternalErrorShader"));

            // Assert
            Assert.AreEqual(string.Empty, material.GetFullHierarchyPath());
            Object.DestroyImmediate(material);
        }

        [Test]
        public void ObjectExt_Tests_Exclude_Removes_Matching_Components_And_Owners()
        {
            // Arrange
            var withRigidbody = new GameObject("WithRigidbody");
            withRigidbody.transform.SetParent(_root.transform);
            var rigidbody = withRigidbody.AddComponent<Rigidbody>();
            var plain = new GameObject("Plain");
            plain.transform.SetParent(_root.transform);
            var colliderNextToRigidbody = withRigidbody.AddComponent<BoxCollider>();
            var plainCollider = plain.AddComponent<BoxCollider>();

            // Act
            var result = new Object[] { withRigidbody, rigidbody, plain, colliderNextToRigidbody, plainCollider }
                .Exclude<Rigidbody>();

            // Assert
            CollectionAssert.AreEqual(new Object[] { plain, plainCollider }, result);
        }
    }
}
