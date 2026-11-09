using OmiLAXR.Components;

namespace OmiLAXR.Listeners.Learner
{
    /// <summary>
    /// Finds every <see cref="Area"/> in the scene (including inactive ones) and hands them to the pipeline,
    /// so that <c>AreaTrackingBehaviour</c> can report when the player enters or leaves them.
    /// </summary>
    public class AreaListener : AutoListener<Area>
    {
        
    }
}