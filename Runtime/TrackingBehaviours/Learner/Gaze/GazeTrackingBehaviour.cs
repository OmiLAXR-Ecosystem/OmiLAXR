using OmiLAXR.Components.Gaze;
using OmiLAXR.Components.Gaze.Fixation;
using OmiLAXR.Extensions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OmiLAXR.TrackingBehaviours.Learner.Gaze
{
    /// <summary>
    /// Base class for gaze tracking. Configures the <see cref="GazeDetector"/>s found by the pipeline (layers, ray distance,
    /// fixation logic) and reports when the gaze enters, leaves or fixates an object. Subclasses create the gaze data,
    /// for example <see cref="CameraGazeTrackingBehaviour"/> and <see cref="EyeGazeTrackingBehaviour"/>.
    /// </summary>
    public abstract class GazeTrackingBehaviour<TGazeData> : ScheduledTrackingBehaviour<GazeDetector>
    where TGazeData : GazeData
    {
        /// <summary>
        /// The eye moves on to focus on a particular point (fixation). Log attention on specific Areas of Interest (AOIs), durations, and transitions.
        /// </summary>
        [Gesture("Gaze"), Action("Fixate")]
        public readonly TrackingBehaviourEvent<TGazeData, FixationData> OnFixated = new TrackingBehaviourEvent<TGazeData, FixationData>();
        
        [Gesture("Gaze"), Action("Enter")]
        public readonly TrackingBehaviourEvent<TGazeData> OnGazeEntered = new TrackingBehaviourEvent<TGazeData>();
        
        [Gesture("Gaze"), Action("Leave")]
        public readonly TrackingBehaviourEvent<TGazeData> OnGazeLeft = new TrackingBehaviourEvent<TGazeData>();
        
        public virtual Transform HmdTransform => Camera.main?.transform;
        
        [Tooltip("Layers the gaze ray can hit. Objects on other layers are ignored.")]
        public LayerMask layersToInclude = ~0;
        
        [Tooltip("Maximum length of the gaze ray in meters.")]
        public float rayDistance = 10.0f;

        [Tooltip("Decides when a gaze counts as a fixation. Each gaze detector gets its own copy.")]
        public FixationLogic fixationLogic;
        
        protected abstract TGazeData GenerateGazeData(GazeHit gazeHit);

        protected virtual void Reset()
        {
            if (fixationLogic == null)
                fixationLogic = FixationLogic.GetDefault();
        }

        protected virtual void HandleGazeUpdate(GazeHit gazeHit)
        {
        }

        protected override void AfterFilteredObjects(GazeDetector[] gazeDetectors)
        {
            if (!fixationLogic)
                fixationLogic = FixationLogic.GetDefault();
           
            foreach (var gd in gazeDetectors)
            {
                // Gaze Detector Events
                gd.OnEnter += HandleOnEnter;
                gd.OnLeave += HandleOnLeave;
                gd.OnUpdate += HandleGazeUpdate;
                gd.layersToInclude = layersToInclude;
                gd.rayDistance = rayDistance;
                
                // Fixation Detector Events
                var fixationDetector = gd.gameObject.EnsureComponent<FixationDetector>();
                if (fixationDetector)
                {
                    fixationDetector.fixationLogic = fixationLogic.Clone();
                    fixationDetector.hmdTransform = HmdTransform;
                    fixationDetector.OnFixationStarted += HandleFixationStart;
                    fixationDetector.OnFixationEnded += HandleOnFixated;
                }
            }
        }

        private void HandleFixationStart(GazeHit gazeHit, FixationData data)
        {
            
        }

        protected override void Dispose(Object[] objects)
        {
            var gazeDetectors = Select<GazeDetector>(objects);
            foreach (var gd in gazeDetectors)
            {
                UnbindEvents(gd);
            }
        }
        
        protected virtual void UnbindEvents(GazeDetector gd)
        {
            gd.OnEnter -= HandleOnEnter;
            gd.OnLeave -= HandleOnLeave;
            gd.OnUpdate -= HandleGazeUpdate;
            var fixationDetector = gd.GetComponent<FixationDetector>();
            fixationDetector.OnFixationEnded -= HandleOnFixated;
        }

        protected void HandleOnEnter(GazeHit gazeHit)
            => OnGazeEntered.Invoke(this, GenerateGazeData(gazeHit));

        protected void HandleOnLeave(GazeHit gazeHit)
            => OnGazeLeft.Invoke(this, GenerateGazeData(gazeHit));

        protected void HandleOnFixated(GazeHit gazeHit, FixationData fixationData)
            => OnFixated.Invoke(this, GenerateGazeData(gazeHit), fixationData);
    }
}