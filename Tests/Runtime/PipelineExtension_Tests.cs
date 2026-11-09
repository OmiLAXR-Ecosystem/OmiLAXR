using NUnit.Framework;
using UnityEngine;

namespace OmiLAXR.Tests
{
    public class PipelineExtension_Tests
    {
        public class MockPipelineExtension : PipelineExtension<Pipeline_Tests.MockPipeline>
        {
        }

        private GameObject _pipelineObject;
        private GameObject _extensionObject;
        private Pipeline_Tests.MockPipeline _pipeline;
        private MockPipelineExtension _extension;
        private Pipeline_Tests.MockListener _listener;
        private Pipeline_Tests.MockFilter _filter;
        private Pipeline_Tests.MockTrackingBehaviour _trackingBehaviour;

        [SetUp]
        public void SetUp()
        {
            _pipelineObject = new GameObject("Pipeline");
            _pipeline = _pipelineObject.AddComponent<Pipeline_Tests.MockPipeline>();
            _pipelineObject.AddComponent<Actor>();

            _extensionObject = new GameObject("Pipeline Extension");
            _extension = _extensionObject.AddComponent<MockPipelineExtension>();
            var child = new GameObject("Child");
            child.transform.SetParent(_extensionObject.transform);
            _listener = child.AddComponent<Pipeline_Tests.MockListener>();
            _filter = child.AddComponent<Pipeline_Tests.MockFilter>();
            _trackingBehaviour = child.AddComponent<Pipeline_Tests.MockTrackingBehaviour>();
        }

        [TearDown]
        public void TearDown()
        {
            _pipeline.StopPipeline();
            Object.DestroyImmediate(_extensionObject);
            Object.DestroyImmediate(_pipelineObject);
        }

        [Test]
        public void PipelineExtension_Tests_Extend_Registers_Child_Components_In_Pipeline()
        {
            // Act
            _extension.Extend(_pipeline);

            // Assert
            Assert.AreEqual(_pipeline, _extension.Pipeline);
            Assert.AreEqual(_pipeline, _extension.GetPipeline());
            Assert.Contains(_listener, _extension.Listeners);
            Assert.Contains(_filter, _extension.Filters);
            Assert.Contains(_trackingBehaviour, _extension.TrackingBehaviours);
            Assert.Contains(_listener, _pipeline.Listeners);
            Assert.Contains(_filter, _pipeline.Filters);
            Assert.Contains(_trackingBehaviour, _pipeline.TrackingBehaviours);
            Assert.Contains(_extension, _pipeline.Extensions);
        }

        [Test]
        public void PipelineExtension_Tests_Components_Resolve_Pipeline_Through_Extension()
        {
            // Act
            _extension.Extend(_pipeline);

            // Assert
            Assert.AreEqual(_pipeline, _listener.Pipeline);
        }

        [Test]
        public void PipelineExtension_Tests_Pipeline_Add_Extends_Pipeline()
        {
            // Act
            _pipeline.Add(_extension);

            // Assert
            Assert.AreEqual(_pipeline, _extension.GetPipeline());
            Assert.Contains(_extension, _pipeline.Extensions);
        }
    }
}
