/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System;
using Object = UnityEngine.Object;

namespace OmiLAXR.Listeners
{
    /// <summary>
    /// Discovers objects that may be tracked and reports them to the pipeline.
    /// </summary>
    public interface IObjectSource
    {
        event Action<Object[]> OnFoundObjects;
        void StartListening();
    }
}
