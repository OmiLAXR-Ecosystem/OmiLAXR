using System;
using System.Collections.Generic;
using OmiLAXR.Composers;
using OmiLAXR.TrackingBehaviours;
using OmiLAXR.Utils;

namespace OmiLAXR.Tests.Mocks
{
    /// <summary>
    /// Minimal statement used to test endpoints, data providers and hooks without a data standard.
    /// </summary>
    public sealed class MockStatement : IStatement
    {
        private readonly Guid _id = Guid.NewGuid();
        private string _origin = "";
        private IComposer _composer;
        private bool _discarded;
        private PipelineInfo _pipelineInfo;

        public string Text { get; }
        public ITrackingBehaviour Owner { get; private set; }

        public MockStatement(string text = "statement")
        {
            Text = text;
        }

        public string GetOrigin() => _origin;
        public void SetOrigin(string origin) => _origin = origin;
        public Guid GetId() => _id;
        public void SetComposer(IComposer composer) => _composer = composer;
        public bool IsFromComposer<T>() where T : IComposer => _composer is T;
        public IComposer GetComposer() => _composer;

        public void SetOwner(ITrackingBehaviour trackingBehaviour)
        {
            Owner = trackingBehaviour;
            if (trackingBehaviour is ActorPipelineComponent component && component.Pipeline)
                _pipelineInfo = new PipelineInfo(component.Pipeline);
        }

        public void SetPipelineInfo(PipelineInfo info) => _pipelineInfo = info;
        public bool IsDiscarded() => _discarded;
        public void Discard() => _discarded = true;
        public IStatement Clone() => new MockStatement(Text);
        public string ToDataStandardString(string version = null) => ToJsonString();
        public string ToJsonString(bool pretty = false, string version = null) => "{\"text\":\"" + Text + "\"}";
        public string ToShortString() => Text;

        public CsvFormat ToCsvFormat(bool flatten = false, string version = null)
        {
            var csv = new CsvFormat();
            csv.AddRow(new Dictionary<string, object> { ["id"] = _id, ["text"] = Text });
            return csv;
        }

        public string GetTimestampString() => "2025-01-01T00:00:00.000Z";
        public PipelineInfo GetSenderPipelineInfo() => _pipelineInfo;
    }
}
