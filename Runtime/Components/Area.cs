using System;
using System.Collections.Generic;
using UnityEngine;

namespace OmiLAXR.Components
{
    [RequireComponent(typeof(Collider))]
    public class Area : MonoBehaviour
    {
#if UNITY_6000_5_OR_NEWER
        private readonly Dictionary<EntityId, bool> _insideObjects = new Dictionary<EntityId, bool>();
#else 
        private Dictionary<int, bool> _insideObjects = new Dictionary<int, bool>();
#endif
        private Collider _collider;
        // public SnapshotCamera snapshotCamera;

        public event Action<Transform, Area, Vector3> OnEnter;
        public event Action<Transform, Area, Vector3> OnLeave;
        
        private void Start()
        {
            _collider = GetComponent<Collider>();
        }

        // Returns true if `point` lies inside `col` (within epsilon).
        public bool IsInside(Vector3 point, float epsilon = 1e-6f)
        {
            var closest = _collider.ClosestPoint(point);
            return (closest - point).sqrMagnitude <= (epsilon * epsilon);
        }

        public void DoCollisionCheck(Transform t)
        {
            if (t == null || !enabled || !gameObject.activeSelf)
                return;
            var point = t.position;
            var isInside = IsInside(point);
#if UNITY_6000_5_OR_NEWER
            var instanceId = t.gameObject.GetEntityId();
#else
            var instanceId = transform.GetInstanceID();
#endif
            var keyExists = _insideObjects.ContainsKey(instanceId);
            var v = keyExists && _insideObjects[instanceId];
            if (isInside)
            {
                if (v)
                    return; // do nothing
                _insideObjects[instanceId] = true;
                OnEnter?.Invoke(t, this, point);
            }
            else
            {
                if (!v)
                    return;
                _insideObjects[instanceId] = false;
                OnLeave?.Invoke(t, this, point);
            }
        }
    }
}