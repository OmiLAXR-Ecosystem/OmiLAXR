/*
 * SPDX-License-Identifier: AGPL-3.0-or-later
 * Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
 * This file is part of OmiLAXR.
 */

using System;
using System.Collections;
using UnityEngine;

namespace OmiLAXR.Schedules
{
    /// <summary>
    /// Scheduler class for scheduled operations in the OmiLAXR system.
    /// Combines tick-based and event-driven scheduling.
    /// Use Init(...) for runtime initialization.
    /// </summary>
    public abstract class Scheduler : ScriptableObject, ITicker, ISerializationCallbackReceiver
    {
        [Header("Scheduler Configuration")]
        [Tooltip("Shows that the Scheduler is active.")]
        public bool isActive = true;

        [Tooltip("Don't wait for first tick.")]
        public bool tickImmediate = true;

        [Tooltip("Autostart Scheduler after creation.")]
        public bool runImmediate = false;

        /// <summary>
        /// The MonoBehaviour that owns and executes the coroutine.
        /// Must be set at runtime via Init(), because ScriptableObjects cannot store scene references.
        /// </summary>
        [NonSerialized]
        public MonoBehaviour owner;

        [NonSerialized]
        private Coroutine _coroutine;

        [NonSerialized]
        private bool _isRunning;

        /// <summary>
        /// True if the scheduler is currently running.
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Raised when the scheduler is stopped.
        /// </summary>
        private Action _onStop;
        private Action _onTick;
        private Action _onTickStart;
        private Action _onTickEnd;

        public event Action OnStop { add => _onStop += value; remove => _onStop -= value; }
        public event Action OnTick { add => _onTick += value; remove => _onTick -= value; }
        public event Action OnTickStart { add => _onTickStart += value; remove => _onTickStart -= value; }
        public event Action OnTickEnd { add => _onTickEnd += value; remove => _onTickEnd -= value; }

        /// <summary>
        /// Runtime initialization for this scheduler.
        /// Call this after CreateInstance() or Clone().
        /// </summary>
        public virtual void Init(
            MonoBehaviour owner,
            Action onTick = null,
            Action onTickStart = null,
            Action onTickEnd = null,
            bool runImmediate = false,
            bool resetHandlers = true)
        {
            this.owner = owner;

            if (resetHandlers)
                ResetHandlers();

            if (onTick != null) _onTick += onTick;
            if (onTickStart != null) _onTickStart += onTickStart;
            if (onTickEnd != null) _onTickEnd += onTickEnd;

            this.runImmediate = runImmediate;

            if (runImmediate)
                Start();
        }

        public virtual void Start()
        {
            if (_isRunning || owner == null)
                return;

            isActive = true;
            _isRunning = true;

            TriggerOnTickStart();

            _coroutine = owner.StartCoroutine(RunInternal(tickImmediate));
        }

        public virtual void Stop()
        {
            if (!_isRunning || owner == null)
                return;

            isActive = false;

            if (_coroutine != null)
            {
                owner.StopCoroutine(_coroutine);
                _coroutine = null;
            }

            TriggerOnTickEnd();

            _isRunning = false;
            TriggerOnStop();
        }

        private IEnumerator RunInternal(bool immediate)
        {
            if (immediate)
                TriggerOnTick();

            yield return Run();
        }

        /// <summary>
        /// Override this to implement custom tick logic.
        /// </summary>
        protected abstract IEnumerator Run();

        protected virtual void TriggerOnTick() => _onTick?.Invoke();
        protected virtual void TriggerOnTickStart() => _onTickStart?.Invoke();
        protected virtual void TriggerOnTickEnd() => _onTickEnd?.Invoke();
        protected virtual void TriggerOnStop() => _onStop?.Invoke();

        protected void ResetHandlers()
        {
            _onStop = null;
            _onTick = null;
            _onTickStart = null;
            _onTickEnd = null;
        }

        public virtual void OnBeforeSerialize() { }

        public virtual void OnAfterDeserialize()
        {
            owner = null;
            _coroutine = null;
            _isRunning = false;

            ResetHandlers();
        }

        /// <summary>
        /// Creates a clone of this scheduler with a new owner assigned.
        /// Event handlers and runtime state are not copied.
        /// </summary>
        /// <param name="newOwner">The MonoBehaviour to assign to the clone.</param>
        /// <returns>A new instance of the scheduler with the same config and new owner.</returns>
        public Scheduler Clone(MonoBehaviour newOwner)
        {
            var clone = Instantiate(this);

            clone.owner = newOwner;
            clone._isRunning = false;
            clone._coroutine = null;

            clone.ResetHandlers();

            return clone;
        }
    }
}