using System;
using System.Collections.Generic;
using UnityEngine;
using OmiLAXR.Utils;

namespace OmiLAXR.Components.Gaze.Fixation
{
    [RequireComponent(typeof(GazeDetector))]
    public sealed class FixationDetector : MonoBehaviour
    {
        [Tooltip("Fixation Logic (ScriptableObject-based logic implementation)")]
        public FixationLogic fixationLogic;

        [Tooltip("Transform of the XR HMD (Camera Rig)")]
        public Transform hmdTransform;
        
        public event FixationStartedHandler OnFixationStarted;
        public event FixationEndedHandler OnFixationEnded;

        private GazeDetector _gazeDetector;
        
        private GameObject _currentTarget;
        private Vector3 _startGazePoint;
        private float _startTime;
        private double? _pupilDiameter;
        private readonly Dictionary<GameObject, int> _fixationCount = new Dictionary<GameObject, int>();

        public DateTime? CurrentFixationStart { get; private set; }
        public DateTime? CurrentFixationEnd { get; private set; }
        
        private bool _hasHadFirstHit = false;
        
        public bool IsFixating => _currentTarget != null;

        private void Awake()
        {
            if (_gazeDetector == null)
                _gazeDetector = GetComponent<GazeDetector>();

            if (fixationLogic == null)
            {
                fixationLogic = FixationLogic.GetDefault();
                if (fixationLogic == null)
                    Debug.LogWarning("No FixationLogic assigned or found as default!");
            }

            if (hmdTransform == null && Camera.main != null)
            {
                hmdTransform = Camera.main.transform;
            }

            fixationLogic?.ResetLogic();

            if (_gazeDetector)
            {
                _gazeDetector.OnEnter += HandleGazePoint;
                _gazeDetector.OnUpdate += HandleGazePoint; 
                _gazeDetector.OnLeave += HandleGazeLeave;
            }
        }

        private FixationData GenerateFixationData(GazeHit hit)
        {
            var target = hit.RayHit.collider.gameObject;
            var targetFixationCount = GetFixationCount(target);
            return new FixationData(hit, hit.RayHit.point, targetFixationCount, CurrentFixationStart, CurrentFixationEnd);
        }

        private int GetFixationCount(GameObject target)
        {
            if (!target)
                return 0;
            if (!_fixationCount.ContainsKey(target))
                _fixationCount[target] = 0;
            return _fixationCount[target]++;
        }
        
        private void HandleGazePoint(GazeHit hit)
        {
            _hasHadFirstHit = true;
            
            if (fixationLogic == null || hmdTransform == null)
            {
                Debug.LogWarning("FixationLogic or HMD transform not set!");
                return;
            }

            if (fixationLogic.TryUpdateFixation(hit.RayHit, hmdTransform, out var isNewFixation))
            {
                if (isNewFixation)
                {
                    CurrentFixationStart = Clock.Now;
                    CurrentFixationEnd = null;
                    _currentTarget = hit.RayHit.collider.gameObject;
                    var fixationData = GenerateFixationData(hit);
                    OnFixationStarted?.Invoke(hit, fixationData);
                }
            }
        }


        private void HandleGazeLeave(GazeHit hit)
        {
            if (!_hasHadFirstHit)
                return; 
            fixationLogic?.ResetLogic();
            if (_currentTarget != null)            // <-- end only if we were fixating
            {
                CurrentFixationEnd = Clock.Now;
                OnFixationEnded?.Invoke(hit, GenerateFixationData(hit));
                _currentTarget = null;             // <-- clear current target
            }
        }

        private void OnDisable()
        {
            if (_gazeDetector)
            {
                _gazeDetector.OnUpdate -= HandleGazePoint; 
                _gazeDetector.OnEnter -= HandleGazePoint;
                _gazeDetector.OnLeave -= HandleGazeLeave;
            }
        }
    }
}
