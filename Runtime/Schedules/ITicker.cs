/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System;

namespace OmiLAXR.Schedules
{
    /// <summary>
    /// Something that ticks periodically or once (interval, timeout, realtime), independent of how it is scheduled.
    /// </summary>
    public interface ITicker
    {
        bool IsRunning { get; }
        void Start();
        void Stop();
        event Action OnTick;
        event Action OnTickStart;
        event Action OnTickEnd;
        event Action OnStop;
    }
}
