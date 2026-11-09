using System;

namespace OmiLAXR.TrackingBehaviours
{
    /// <summary>
    /// Debug helper that logs a line on every scheduler tick. Use it to check that a scheduler runs at the expected rate.
    /// It raises no tracking events.
    /// </summary>
    public class ScheduledTrackingBehaviourTester : ScheduledTrackingBehaviour
    {
        protected override void OnStartedPipeline(Pipeline pipeline)
        {
            base.OnStartedPipeline(pipeline);
        }

        protected override void Run()
        {
            DebugLog.OmiLAXR.Print(DateTime.Now + " RUN SCHEDULE");
        }
    }
}