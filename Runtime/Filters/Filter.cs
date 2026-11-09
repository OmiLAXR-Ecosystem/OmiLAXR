/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using Object = UnityEngine.Object;

namespace OmiLAXR.Filters
{
    /// <summary>
    /// Abstract base class for pipeline filter components that process arrays of Unity Objects.
    /// Filters selectively include or exclude objects from tracking based on custom criteria.
    /// </summary>
    public abstract class Filter : ActorPipelineComponent, IObjectFilter
    {
        /// <summary>
        /// Processes an array of Unity Objects and returns a filtered subset.
        /// Implementations should evaluate each object against filter criteria
        /// and return only objects that pass the filter.
        /// </summary>
        /// <param name="objects">Array of Unity Objects to filter</param>
        /// <returns>Filtered array containing only objects that meet the criteria</returns>
        public abstract Object[] Pass(Object[] objects);
    }
}