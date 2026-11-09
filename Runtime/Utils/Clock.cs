/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System;

namespace OmiLAXR.Utils
{
    /// <summary>
    /// Source of wall clock time. Replace <see cref="Clock.Current"/> to control time, e.g. in tests.
    /// </summary>
    public interface IClock
    {
        /// <summary>Current local time.</summary>
        DateTime Now { get; }

        /// <summary>Current UTC time.</summary>
        DateTime UtcNow { get; }
    }

    /// <summary>
    /// <see cref="IClock"/> based on the system time.
    /// </summary>
    public sealed class SystemClock : IClock
    {
        public static readonly SystemClock Instance = new SystemClock();

        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    /// <summary>
    /// Ambient clock used by OmiLAXR for timestamps and durations.
    /// </summary>
    public static class Clock
    {
        private static IClock _current = SystemClock.Instance;

        /// <summary>
        /// Clock in use. Setting null restores the <see cref="SystemClock"/>.
        /// </summary>
        public static IClock Current
        {
            get => _current;
            set => _current = value ?? SystemClock.Instance;
        }

        public static DateTime Now => _current.Now;
        public static DateTime UtcNow => _current.UtcNow;
    }
}
