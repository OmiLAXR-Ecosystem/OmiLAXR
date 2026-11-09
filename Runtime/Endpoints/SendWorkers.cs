/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System;
using System.Collections;
using System.Threading;
using UnityEngine;

namespace OmiLAXR.Endpoints
{
    /// <summary>
    /// Runs the send loop of an <see cref="Endpoint"/>: repeatedly executes a step until stopped
    /// or the step reports <see cref="TransferCode.InvalidCredentials"/>.
    /// </summary>
    public interface ISendWorker
    {
        bool IsRunning { get; }

        /// <summary>Starts calling <paramref name="step"/> until <see cref="Stop"/> is called.</summary>
        void Start(Func<TransferCode> step);

        /// <summary>Wakes the worker because new work arrived.</summary>
        void Signal();

        /// <summary>Stops the worker and waits at most <paramref name="timeoutMs"/> for it (-1 = infinite).</summary>
        void Stop(int timeoutMs);
    }

    /// <summary>
    /// Timing rules shared by all send workers.
    /// </summary>
    public static class SendWorkerPolicy
    {
        /// <summary>Whether the loop continues after a step returned <paramref name="result"/>.</summary>
        public static bool ShouldContinue(TransferCode result) => result != TransferCode.InvalidCredentials;

        /// <summary>Idle time after a step; longer when nothing was queued to save CPU.</summary>
        public static int WaitMilliseconds(TransferCode result) => result == TransferCode.NoStatements ? 100 : 10;
    }

#if !OMILAXR_THREADS_DISABLED && !UNITY_WEBGL
    /// <summary>
    /// Runs the send loop on a background thread.
    /// </summary>
    public sealed class ThreadSendWorker : ISendWorker
    {
        private readonly string _name;
        private readonly AutoResetEvent _signal = new AutoResetEvent(false);
        private Thread _thread;
        private volatile bool _stopRequested;

        public ThreadSendWorker(string name)
        {
            _name = name;
        }

        public bool IsRunning => _thread != null && _thread.IsAlive;

        public void Start(Func<TransferCode> step)
        {
            if (_thread != null)
                return;
            _stopRequested = false;
            _thread = new Thread(() => Run(step))
            {
                IsBackground = true, // Allow application to exit even if thread is running
                Name = _name // Descriptive name for debugging
            };
            _thread.Start();
        }

        private void Run(Func<TransferCode> step)
        {
            try
            {
                while (!_stopRequested)
                {
                    var result = step();
                    if (_stopRequested || !SendWorkerPolicy.ShouldContinue(result))
                        break;
                    _signal.WaitOne(SendWorkerPolicy.WaitMilliseconds(result));
                }
            }
            catch (ThreadInterruptedException)
            {
                // Stop() gave up waiting
            }
            catch (Exception ex)
            {
                // Log any unexpected exceptions to help with debugging
                Debug.LogException(ex);
            }
        }

        public void Signal() => _signal.Set();

        public void Stop(int timeoutMs)
        {
            if (_thread == null)
                return;
            _stopRequested = true;
            _signal.Set();
            if (!_thread.Join(timeoutMs))
            {
                // Thread is background; avoid blocking shutdown forever
                try
                {
                    _thread.Interrupt();
                }
                catch
                {
                }
            }
            _thread = null;
        }
    }
#endif

    /// <summary>
    /// Runs the send loop as a coroutine on the main thread (e.g. WebGL).
    /// </summary>
    public sealed class CoroutineSendWorker : ISendWorker
    {
        private readonly MonoBehaviour _host;
        private Coroutine _coroutine;
        private bool _stopRequested;

        public CoroutineSendWorker(MonoBehaviour host)
        {
            _host = host;
        }

        public bool IsRunning => _coroutine != null;

        public void Start(Func<TransferCode> step)
        {
            if (_coroutine != null)
                _host.StopCoroutine(_coroutine);
            _stopRequested = false;
            _coroutine = _host.StartCoroutine(Run(step));
        }

        private IEnumerator Run(Func<TransferCode> step)
        {
            while (!_stopRequested)
            {
                var result = step();
                if (_stopRequested || !SendWorkerPolicy.ShouldContinue(result))
                    break;
                // Yield control back to Unity with adaptive timing
                yield return new WaitForSeconds(SendWorkerPolicy.WaitMilliseconds(result) / 1000f);
            }
            _coroutine = null;
        }

        public void Signal()
        {
        }

        public void Stop(int timeoutMs)
        {
            _stopRequested = true;
            if (_coroutine != null && _host)
                _host.StopCoroutine(_coroutine);
            _coroutine = null;
        }
    }

    /// <summary>
    /// Does not run on its own: the owner calls <see cref="Step"/> to process the queue,
    /// e.g. for deterministic tests or a custom update loop.
    /// </summary>
    public sealed class ManualSendWorker : ISendWorker
    {
        private Func<TransferCode> _step;

        public bool IsRunning => _step != null;

        /// <summary>Number of <see cref="Signal"/> calls, i.e. how often new work was announced.</summary>
        public int Signals { get; private set; }

        public void Start(Func<TransferCode> step) => _step = step;

        public void Signal() => Signals++;

        public void Stop(int timeoutMs) => _step = null;

        /// <summary>Processes one batch; returns <see cref="TransferCode.NoStatements"/> when not running.</summary>
        public TransferCode Step() => _step?.Invoke() ?? TransferCode.NoStatements;
    }
}
