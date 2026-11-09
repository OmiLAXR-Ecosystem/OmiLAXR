/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace OmiLAXR.Filters
{
    /// <summary>
    /// Decides which found objects get tracked.
    /// </summary>
    public interface IObjectFilter
    {
        /// <summary>Returns the subset of <paramref name="objects"/> that passes this filter.</summary>
        Object[] Pass(Object[] objects);
    }

    public static class ObjectFilters
    {
        /// <summary>
        /// Passes <paramref name="objects"/> through all filters in order. Disabled filter components are skipped.
        /// </summary>
        public static Object[] Apply(IEnumerable<IObjectFilter> filters, Object[] objects)
        {
            var result = objects ?? Array.Empty<Object>();
            foreach (var filter in filters)
            {
                if (filter is UnityEngine.Behaviour behaviour && !behaviour.enabled)
                    continue;
                result = filter.Pass(result) ?? Array.Empty<Object>();
            }
            return result;
        }
    }
}
