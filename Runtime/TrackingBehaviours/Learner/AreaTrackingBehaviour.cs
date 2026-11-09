/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System.Collections.Generic;
using System.ComponentModel;
using OmiLAXR.Components;
using UnityEngine;

namespace OmiLAXR.TrackingBehaviours.Learner
{
    [AddComponentMenu("OmiLAXR / 3) Tracking Behaviours / Area Tracking Behaviour"), 
     Description("Detects area/room enter and left events.")]
    public class AreaTrackingBehaviour : TrackingBehaviour<Area>
    {
        [Gesture("Area"), Action("Enter")]
        public TrackingBehaviourEvent<Area, Vector3> OnEntered = new TrackingBehaviourEvent<Area, Vector3>();
        [Gesture("Area"), Action("Leave")]
        public TrackingBehaviourEvent<Area, Vector3> OnLeft = new TrackingBehaviourEvent<Area, Vector3>();
        private List<Area> _areas = new List<Area>();
        [Tooltip("Transform whose position is checked against the areas. Defaults to the main camera.")]
        public Transform playerTransform;
        protected override void AfterFilteredObjects(Area[] areas)
        {
            if (!playerTransform)
                playerTransform = Camera.main!.transform;
            foreach (var area in areas)
            {
                if (_areas.Contains(area))
                    continue;
                area.OnEnter += (t, a, p) =>
                {
                    OnEntered.Invoke(this, a, p);
                };
                area.OnLeave += (t, a, p) =>
                {
                    OnLeft.Invoke(this, a, p);
                };
                _areas.Add(area);
            }
        }

        private void FixedUpdate()
        {
            if (playerTransform == null)
                return;
            // If you teleported stuff this frame, sync before querying:
            foreach (var t in _areas)
            {
                t.DoCollisionCheck(playerTransform);
            }
        }
    }
}