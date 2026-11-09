/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System.Reflection;

namespace OmiLAXR.TrackingBehaviours
{
    /// <summary>
    /// Core interface for tracking behavior components that monitor user interactions and system events.
    /// Provides methods for accessing event fields and associated actors/instructors in the tracking pipeline.
    /// </summary>
    public interface ITrackingBehaviour : IActorProvider
    {
        /// <summary>
        /// Retrieves all tracking behavior event fields through reflection.
        /// Used for dynamically discovering and binding to available events within the tracking behavior.
        /// </summary>
        /// <returns>Array of FieldInfo objects representing all TrackingBehaviourEvent fields</returns>
        FieldInfo[] GetTrackingBehaviourEvents();
    }
}