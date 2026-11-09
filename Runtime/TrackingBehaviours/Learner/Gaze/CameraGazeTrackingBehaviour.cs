using System.ComponentModel;
using System.Linq;
using OmiLAXR.Components.Gaze;
using OmiLAXR.Types;
using UnityEngine;

namespace OmiLAXR.TrackingBehaviours.Learner.Gaze
{
    [AddComponentMenu("OmiLAXR / 3) Tracking Behaviours / <Camera> Gaze Tracking Behaviour")]
    [Description("Uses camera gaze to detect what object the user is gazing at.")]
    public class CameraGazeTrackingBehaviour : GazeTrackingBehaviour<CameraData>
    {
        [Tooltip("Camera the gaze ray starts from. Defaults to the main camera.")]
        public Camera originCamera;
        private GazeDetector _gazeDetector;
        protected override void AfterFilteredObjects(GazeDetector[] gazeDetectors)
        {
            base.AfterFilteredObjects(gazeDetectors);

            if (!originCamera)
            {
                var mainCamera = Camera.main;
                var mainCameraGd = mainCamera?.GetComponent<GazeDetector>();
                if (gazeDetectors.Contains(mainCameraGd))
                {
                    _gazeDetector = mainCameraGd;
                    originCamera = mainCamera;
                }
            }
            
            if (!originCamera)
            {
                Destroy(_gazeDetector);
                enabled = false;
            }
        }

        protected override void Run()
        {
            if (!_gazeDetector)
                return;
            _gazeDetector.PerformRaycast(true);
        }

        protected override CameraData GenerateGazeData(GazeHit gazeHit)
        {
            if (!originCamera)
            {
                DebugLog.OmiLAXR.Error("No target camera found.");
                return null;
            }
            var frustum = Frustum.FromCamera(originCamera);
            return new CameraData(gazeHit, frustum, 
                HmdTransform.position, 
                gazeHit.RayHit.point);
        }
    }
}