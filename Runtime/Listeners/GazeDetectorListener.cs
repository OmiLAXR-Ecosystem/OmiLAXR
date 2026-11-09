using System.Collections.Generic;
using OmiLAXR.Components;
using OmiLAXR.Components.Gaze;
using OmiLAXR.Components.Gaze.Fixation;
using OmiLAXR.Extensions;
using OmiLAXR.TrackingBehaviours.Learner.Gaze;
using UnityEngine;

namespace OmiLAXR.Listeners
{
    /// <summary>
    /// Base class for listeners that turn gaze sources (a camera, an eye, an XR gaze interactor) into trackable objects.
    /// For every component of type <typeparamref name="T"/> that passes <c>IsCorrectComponent</c>, it adds a
    /// <see cref="GazeDetector"/>, and optionally a <see cref="FixationDetector"/>, a <see cref="TransformWatcher"/> and a debug ray.
    /// </summary>
    public abstract class GazeDetectorListener<T> : Listener
    where T : Component
    {
        [Tooltip("Add <TransformWatcher> to gaze game objects.")]
        public bool addTransformWatcher = true;

        [Tooltip("Add a <FixationDetector> to gaze game objects, so fixations are reported.")]
        public bool enableFixation = true;
        
        [Tooltip("Draws the gaze ray and hit point in the scene for debugging. Off by default.")]
        public GazeDetectorDebug.GazeDetectorDebugSettings
            debugConfig;
        
        protected virtual void Reset()
        {
            debugConfig = GazeDetectorDebug.GazeDetectorDebugSettings.Default;
        }

        protected abstract bool IsCorrectComponent(T component);

        protected virtual void Map(GazeDetector gazeDetector)
        {
            // Do nothing
        }
        
        public override void StartListening()
        {
            var gazeComponents = FindObjects<T>();
            var transformWatchers = new List<TransformWatcher>();
            var gazeDetectors = new List<GazeDetector>();
            foreach (var gazeComponent in gazeComponents)
            {
                if (!IsCorrectComponent(gazeComponent)) 
                    continue;

                if (addTransformWatcher)
                {
                    var tw = gazeComponent.gameObject.EnsureComponent<TransformWatcher>();
                    transformWatchers.Add(tw);
                }
                
                var gd = gazeComponent.gameObject.EnsureComponent<GazeDetector>();
                gazeDetectors.Add(gd);

                if (enableFixation)
                    gazeComponent.gameObject.EnsureComponent<FixationDetector>();
                
                Map(gd);
                
                if (debugConfig.enabled)
                {
                    var debug = gazeComponent.gameObject
                        .EnsureComponent<GazeDetectorDebug>();
                    debug.Setup(debugConfig, gd.rayDistance);
                }
            }
            Found(transformWatchers.ToArray());
            Found(gazeDetectors.ToArray());
        }
    }
}