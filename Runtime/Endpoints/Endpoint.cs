/*
 * SPDX-License-Identifier: AGPL-3.0-or-later
 * Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
 * This file is part of OmiLAXR.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OmiLAXR.Composers;
using OmiLAXR.Utils;
using UnityEngine;

namespace OmiLAXR.Endpoints
{
    /// <summary>
    /// Abstract base class for managing statement transfers in a data pipeline.
    /// Provides a flexible mechanism for sending statements using either threading or coroutines.
    /// Supports batched statement processing, error handling, and comprehensive state management.
    /// Executes early in Unity's execution order to ensure proper initialization.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public abstract class Endpoint : DataProviderPipelineComponent, IEndpoint, IDataMapConsumer, IDataMapProvider
    {
        /// <summary>
        /// Total count of successfully recorded statements.
        /// Only updated in Unity Editor for debugging purposes.
        /// </summary>
        [field: SerializeField, ReadOnly]
        public ulong RecordedStatements { get; protected set; } = 0;

        private readonly object _queuedStatementsLock = new object();
        private volatile bool _accepting = true;

        // Platform-specific thread usage configuration
        // Threads are disabled on WebGL due to platform limitations
        // Can be disabled globally via OMILAXR_THREADS_DISABLED symbol
#if OMILAXR_THREADS_DISABLED || UNITY_WEBGL
        protected virtual bool useThreads => false;
#else
        protected virtual bool useThreads => true;
#endif

        /// <summary>
        /// Retrieves the parent DataProvider component of the specified type.
        /// Used to access data providers in the component hierarchy.
        /// </summary>
        /// <typeparam name="T">Type of DataProvider to retrieve</typeparam>
        /// <returns>The first found DataProvider of the specified type, or null if not found</returns>
        public T GetDataProvider<T>() where T : DataProvider => GetComponentInParent<T>();

        // Event system for monitoring statement transfer lifecycle
        // Allows external components to hook into various stages of processing
        public event EndpointAction OnStartedSending; // Fired when sending begins
        public event EndpointAction OnStoppedSending; // Fired when sending completely stops
        public event EndpointAction OnPausedSending; // Fired when sending is paused
        public event EndpointAction<IStatement> OnSendingStatement; // Fired before each statement is sent
        public event EndpointAction<IStatement> OnSentStatement; // Fired after successful statement delivery
        public event EndpointAction<List<IStatement>> OnSentBatch; // Fired after successful batch delivery
        public event EndpointAction<IStatement> OnFailedSendingStatement; // Fired when individual statement fails
        public event EndpointAction<List<IStatement>> OnFailedSendingBatch; // Fired when entire batch fails

        // State tracking properties for external monitoring
        public bool IsSending { get; private set; } // True when the endpoint is actively processing statements
        public bool IsTransferring { get; private set; } // True during actual network/file transfer operations

        // Threading and coroutine infrastructure
        private Thread _sendThread; // Background thread for statement processing
        private Coroutine _sendCoroutine; // Unity coroutine alternative to threading

        // Queue management for statement processing
        protected readonly Queue<IStatement>
            QueuedStatements = new Queue<IStatement>(); // Statements waiting to be sent

        private readonly Queue<Action> _executionQueue = new Queue<Action>(); // Actions to execute on main thread

        // Thread synchronization mechanism (only available on thread-supporting platforms)
#if !OMILAXR_THREADS_DISABLED && !UNITY_WEBGL
        private readonly AutoResetEvent _signal = new AutoResetEvent(false);
#endif

        // Shutdown coordination flag
        private bool _shuttingDown;
        private bool _isFlushing;

        /// <summary>
        /// Maximum number of statements to process in a single batch operation.
        /// Can be overridden by derived classes to optimize for specific endpoints.
        /// Higher values reduce overhead but increase memory usage and transfer size.
        /// </summary>
        protected virtual int MaxBatchSize => 50;

        [Header("Shutdown")] [SerializeField] private bool flushOnStop = true;

        [SerializeField,
         Tooltip("Max seconds spent flushing on StopSending (0 = try flush all). For benchmarks use ~0-2s.")]
        private float flushOnStopSecondsBudget = 0f;

        [SerializeField,
         Tooltip("If flushing exceeds budget, drop remaining queued statements (recommended for benchmarks).")]
        private bool dropRemainingAfterBudget = false;

        /// <summary>
        /// Main worker loop for threaded statement processing.
        /// Continuously processes the statement queue until shutdown is requested.
        /// Handles errors gracefully and implements retry logic with exponential backoff.
        /// </summary>
        private void SendWorkerLoop()
        {
            try
            {
                // Continue processing until shutdown is requested
                while (!_shuttingDown)
                {
                    // Process available statements from the queue
                    var result = HandleQueue();

                    // Early exit if shutdown was requested during processing
                    if (_shuttingDown) break;

                    // Log errors for unsuccessful transfers (except normal "no statements" case)
                    if (result != TransferCode.Success && result != TransferCode.NoStatements &&
                        result != TransferCode.Queued)
                    {
                        DebugLog.OmiLAXR?.Error($"Failed to send statements. Error code: {result}");
                    }

                    // Stop processing permanently if credentials are invalid
                    if (result == TransferCode.InvalidCredentials)
                        break;

                    // Implement adaptive waiting based on transfer results
                    // Longer waits when no statements are available to reduce CPU usage
#if !OMILAXR_THREADS_DISABLED && !UNITY_WEBGL
                    if (useThreads)
                        _signal.WaitOne(result == TransferCode.NoStatements ? 100 : 10);
#else
                    Thread.Sleep(result == TransferCode.NoStatements ? 100 : 10);
#endif
                }
            }
            catch (Exception ex)
            {
                // Log any unexpected exceptions to help with debugging
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// Coroutine-based worker for statement processing on platforms without thread support.
        /// Provides the same functionality as SendWorkerLoop but using Unity's coroutine system.
        /// </summary>
        private IEnumerator SendWorkerCoroutine()
        {
            while (!_shuttingDown)
            {
                // Process available statements from the queue
                var result = HandleQueue();

                // Early exit if shutdown was requested during processing
                if (_shuttingDown) break;

                // Log errors for unsuccessful transfers (except normal "no statements" case)
                if (result != TransferCode.Success && result != TransferCode.NoStatements &&
                    result != TransferCode.Queued)
                {
                    DebugLog.OmiLAXR?.Error($"Failed to send statements. Error code: {result}");
                }

                // Stop processing permanently if credentials are invalid
                if (result == TransferCode.InvalidCredentials)
                    break;

                // Yield control back to Unity with adaptive timing
                // Longer delays when no statements are available to improve performance
                yield return new WaitForSeconds(result == TransferCode.NoStatements ? 0.1f : 0.01f);
            }

            // Clean up coroutine reference when finished
            _sendCoroutine = null;
        }

        /// <summary>
        /// Initiates the statement sending process using either threading or coroutines.
        /// Handles worker initialization, event notification, and queue management.
        /// </summary>
        /// <param name="resetQueue">If true, clears existing queued statements before starting</param>
        public virtual void StartSending(bool resetQueue = false)
        {
            // Prevent starting if component is disabled or already sending.
            // A previous Pause/Stop leaves _shuttingDown set, which is reset below so sending can resume.
            if (!enabled || IsSending)
                return;

            // Log the start of sending with endpoint type information
            DebugLog.OmiLAXR.Print($"🚀({GetType().Name}) started writing statements.");

            // Reset shutdown flag for new sending session
            _shuttingDown = false;

            // Allow enqueueing again
            _accepting = true;

            // Handle queue management based on reset parameter
            if (resetQueue)
            {
                lock (_queuedStatementsLock)
                {
                    QueuedStatements.Clear(); // Remove all pending statements
                }
            }

            // Initialize appropriate worker based on platform capabilities
            if (useThreads)
            {
                // Create and start background thread for statement processing
                _sendThread = new Thread(SendWorkerLoop)
                {
                    IsBackground = true, // Allow application to exit even if thread is running
                    Name = $"EndpointThread-{GetType().Name}" // Descriptive name for debugging
                };
                _sendThread.Start();
            }
            else
            {
                // Clean up any existing coroutine before starting new one
                if (_sendCoroutine != null)
                    StopCoroutine(_sendCoroutine);

                // Start coroutine-based processing
                _sendCoroutine = StartCoroutine(SendWorkerCoroutine());
            }

            // Notify external listeners that sending has started
            OnStartedSending?.Invoke(this);

            // Update state to reflect active sending
            IsSending = true;

            // Wake the worker in case there is already work pending
#if !OMILAXR_THREADS_DISABLED && !UNITY_WEBGL
            if (useThreads)
                _signal.Set();
#endif
        }

        /// <summary>
        /// Temporarily pauses statement sending while preserving queued statements.
        /// Can be resumed by calling StartSending() again.
        /// </summary>
        public virtual void PauseSending()
        {
            // Only pause if currently sending
            if (!IsSending)
                return;

            // Initiate shutdown sequence
            _shuttingDown = true;
            IsSending = false;

            // Clean up worker based on current mode
            if (useThreads)
            {
                // Wait for thread to finish processing current batch
                _sendThread?.Join();
                _sendThread = null;
            }
            else
            {
                // Stop coroutine if running
                if (_sendCoroutine != null)
                {
                    StopCoroutine(_sendCoroutine);
                    _sendCoroutine = null;
                }
            }

            // Notify external listeners that sending has been paused
            OnPausedSending?.Invoke(this);
        }

        /// <summary>
        /// Completely stops statement sending and processes any remaining queued statements.
        /// Provides clean shutdown with final statistics logging.
        /// </summary>
        public virtual void StopSending()
        {
            // Robust guard: only skip if nothing is running anymore
            if (!IsSending && _sendThread == null && _sendCoroutine == null)
                return;

            // Stop accepting new statements immediately to prevent enqueue during shutdown
            _accepting = false;

            // Initiate shutdown sequence
            _shuttingDown = true;

            // Wake the worker if it's waiting (thread-enabled platforms only)
#if !OMILAXR_THREADS_DISABLED && !UNITY_WEBGL
            if (useThreads)
                _signal.Set();
#endif

            // Stop the worker first (bounded waiting), so the flush below does not transfer concurrently with it
            if (useThreads)
            {
                if (_sendThread != null)
                {
                    const int joinMs = 2000;
                    if (!_sendThread.Join(joinMs))
                    {
                        // Thread is background; avoid blocking shutdown forever
                        try
                        {
                            _sendThread.Interrupt();
                        }
                        catch
                        {
                        }
                    }

                    _sendThread = null;
                }
            }
            else
            {
                if (_sendCoroutine != null)
                {
                    StopCoroutine(_sendCoroutine);
                    _sendCoroutine = null;
                }
            }

            // Reliability: flush pending statements on stop (thread-safe)
            if (flushOnStop)
            {
                if (flushOnStopSecondsBudget > 0f)
                {
                    FlushQueueBudget(flushOnStopSecondsBudget);

                    if (dropRemainingAfterBudget)
                    {
                        lock (_queuedStatementsLock)
                            QueuedStatements.Clear();
                    }
                }
                else
                {
                    // "Flush all" (can take long, but no intentional loss)
                    FlushQueue();
                }
            }
            else
            {
                // No flush requested: drop remaining to guarantee fast shutdown
                lock (_queuedStatementsLock)
                    QueuedStatements.Clear();
            }

            // Update state to reflect stopped sending
            IsSending = false;

            // Log final statistics
            DebugLog.OmiLAXR.Print(
                $"⛔({GetType().Name}) stopped writing statements. {RecordedStatements} statements were sent.");

            // Notify external listeners that sending has stopped
            OnStoppedSending?.Invoke(this);
        }

        // Unity lifecycle event handlers
        protected override void OnEnable()
        {
            base.OnEnable();
            StartSending(); // Auto-start when component becomes active
        }
        protected virtual void OnDisable() => StopSending(); // Clean stop when component is deactivated
        protected virtual void OnDestroy() => StopSending(); // Ensure cleanup when component is destroyed

        /// <summary>
        /// Processes queued statements for a bounded time budget.
        /// This avoids long shutdowns when many statements are pending.
        /// </summary>
        protected void FlushQueueBudget(float maxSeconds)
        {
            if (_isFlushing) return;
            _isFlushing = true;

            var deadline = Time.realtimeSinceStartup + Mathf.Max(0f, maxSeconds);
            var totalFlushed = 0;

            while (Time.realtimeSinceStartup < deadline)
            {
                var batch = new List<IStatement>(MaxBatchSize);

                lock (_queuedStatementsLock)
                {
                    while (batch.Count < MaxBatchSize && QueuedStatements.Count > 0)
                        batch.Add(QueuedStatements.Dequeue());
                }

                if (batch.Count == 0) break;

                TransferStatements(batch);
                totalFlushed += batch.Count;
            }

            DebugLog.OmiLAXR.Print($"🪄({GetType().Name}) budget-flushed {totalFlushed} statements in <= {maxSeconds:0.###}s.");
            _isFlushing = false;
        }
        
        /// <summary>
        /// Adds a statement to the sending queue for asynchronous processing.
        /// Thread-safe operation that signals workers when new statements are available.
        /// </summary>
        /// <param name="statement">The statement to be queued for sending</param>
        public virtual void SendStatement(IStatement statement)
        {
            if (!_accepting || _shuttingDown) return;

            // Add statement to the processing queue
            lock (_queuedStatementsLock)
            {
                QueuedStatements.Enqueue(statement);
            }

            // Signal thread worker that new work is available (thread-enabled platforms only)
#if !OMILAXR_THREADS_DISABLED && !UNITY_WEBGL
            if (useThreads) _signal.Set();
#endif
        }

        /// <summary>
        /// Processes all queued statements immediately in batches.
        /// Used during shutdown and manual flushing operations.
        /// </summary>
        protected virtual void FlushQueue()
        {
            if (_isFlushing) return;
            _isFlushing = true;

            int count;
            List<IStatement> batch = null;

            lock (_queuedStatementsLock)
            {
                count = QueuedStatements.Count;
                if (count > 0)
                {
                    batch = QueuedStatements.ToList();
                    QueuedStatements.Clear();
                }
            }

            if (batch != null && batch.Count > 0)
                TransferStatements(batch);

            DebugLog.OmiLAXR.Print($"🪄({GetType().Name}) flushed {count} statements.");
            _isFlushing = false;
        }

        /// <summary>
        /// Abstract method that must be implemented by derived classes.
        /// Handles the actual sending of individual statements to the target destination.
        /// </summary>
        /// <param name="statement">The statement to send</param>
        /// <returns>Result code indicating success or failure reason</returns>
        protected abstract TransferCode HandleSending(IStatement statement);

        /// <summary>
        /// Virtual method for batch processing optimization.
        /// Default implementation processes statements individually but can be overridden
        /// for endpoints that support native batch operations.
        /// </summary>
        /// <param name="batch">List of statements to send as a batch</param>
        /// <returns>Result code indicating batch transfer success or failure</returns>
        protected virtual TransferCode HandleSending(List<IStatement> batch)
        {
            // Early return for empty batches
            if (batch == null || batch.Count == 0)
                return TransferCode.Success;

            try
            {
                // Allow derived classes to perform pre-batch operations
                BeforeHandleSendingBatch(batch);

                // Process each statement individually
                foreach (var statement in batch)
                {
                    var result = HandleSending(statement);
                    if (result == TransferCode.Success)
                        TriggerSentStatement(statement);
                    else if (result != TransferCode.NoStatements)
                        TriggerFailedStatement(statement);
                }

                // Allow derived classes to perform post-batch operations
                AfterHandleSendingBatch(batch);

                // Trigger batch success event
                TriggerSentBatch(batch);

                return TransferCode.Success;
            }
            catch (Exception ex)
            {
                // Log the exception for debugging
                Debug.LogException(ex);

                // Handle failure by re-queuing statements and triggering failure events
                lock (_queuedStatementsLock)
                {
                    foreach (var statement in batch)
                    {
                        TriggerFailedStatement(statement);
                        //if (!_shuttingDown) QueuedStatements.Enqueue(statement);
                        QueuedStatements.Enqueue(statement);
                    }
                }

                TriggerFailedBatch(batch);
                return TransferCode.Error;
            }
        }

        /// <summary>
        /// Hook for derived classes to perform operations before batch processing.
        /// Called before each batch is sent, useful for setup operations.
        /// </summary>
        /// <param name="batch">The batch about to be processed</param>
        protected virtual void BeforeHandleSendingBatch(List<IStatement> batch)
        {
        }

        /// <summary>
        /// Hook for derived classes to perform operations after batch processing.
        /// Called after each batch is sent, useful for cleanup operations.
        /// </summary>
        /// <param name="batch">The batch that was just processed</param>
        protected virtual void AfterHandleSendingBatch(List<IStatement> batch)
        {
        }

        /// <summary>
        /// Coordinates the transfer of a batch of statements with proper state management.
        /// Handles the transfer state flag and triggers appropriate events.
        /// </summary>
        /// <param name="batch">List of statements to transfer</param>
        /// <returns>Result code indicating transfer success or failure</returns>
        private TransferCode TransferStatements(List<IStatement> batch)
        {
            try
            {
                // Set transfer state to prevent concurrent operations
                IsTransferring = true;

                // Trigger sending events for each statement in the batch
                foreach (var statement in batch)
                    TriggerSendingStatement(statement);

                // Delegate to the batch handling method
                return HandleSending(batch);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return TransferCode.Error;
            }
            finally
            {
                // Always clear the transfer state, even if an exception occurred
                IsTransferring = false;
            }
        }

        /// <summary>
        /// Triggers the successful statement event and updates statistics.
        /// Only increments counter in Unity Editor for debugging purposes.
        /// </summary>
        /// <param name="statement">The statement that was successfully sent</param>
        protected void TriggerSentStatement(IStatement statement)
        {
            RecordedStatements++;
            // Notify listeners of successful statement delivery
            OnSentStatement?.Invoke(this, statement);
        }

        /// <summary>
        /// Triggers the failed statement event for error handling and monitoring.
        /// </summary>
        /// <param name="statement">The statement that failed to send</param>
        protected void TriggerFailedStatement(IStatement statement)
        {
            OnFailedSendingStatement?.Invoke(this, statement);
        }

        /// <summary>
        /// Triggers the successful batch event for monitoring batch operations.
        /// </summary>
        /// <param name="batch">The batch that was successfully sent</param>
        protected void TriggerSentBatch(List<IStatement> batch)
        {
            OnSentBatch?.Invoke(this, batch);
        }

        /// <summary>
        /// Triggers the failed batch event for error handling and monitoring.
        /// </summary>
        /// <param name="batch">The batch that failed to send</param>
        protected void TriggerFailedBatch(List<IStatement> batch)
            => OnFailedSendingBatch?.Invoke(this, batch);

        /// <summary>
        /// Triggers the sending statement event before actual transfer begins.
        /// </summary>
        /// <param name="statement">The statement about to be sent</param>
        protected void TriggerSendingStatement(IStatement statement)
            => OnSendingStatement?.Invoke(this, statement);

        /// <summary>
        /// Processes a batch of statements from the queue.
        /// Called by worker threads/coroutines to handle pending statements.
        /// </summary>
        /// <returns>Result code indicating processing outcome</returns>
        protected virtual TransferCode HandleQueue()
        {
            var batch = new List<IStatement>(MaxBatchSize);

            // Build batch up to maximum size from available queued statements
            lock (_queuedStatementsLock)
            {
                while (batch.Count < MaxBatchSize && QueuedStatements.Count > 0)
                    batch.Add(QueuedStatements.Dequeue());
            }

            // Transfer the batch if statements are available, otherwise return no-statements code
            return batch.Count > 0 ? TransferStatements(batch) : TransferCode.NoStatements;
        }

        /// <summary>
        /// Unity Update method for processing main thread execution queue.
        /// Handles actions that must be executed on the main thread (like UI updates).
        /// </summary>
        private void Update()
        {
            // Process all queued actions atomically to avoid race conditions
            lock (_executionQueue)
            {
                while (_executionQueue.Count > 0)
                {
                    _executionQueue.Dequeue()?.Invoke();
                }
            }
        }

        /// <summary>
        /// Queues an action for execution on the main thread.
        /// Useful for updating UI or other Unity objects from background threads.
        /// </summary>
        /// <param name="action">The action to execute on the main thread</param>
        protected void Dispatch(Action action)
        {
            lock (_executionQueue)
            {
                _executionQueue.Enqueue(action);
            }
        }

        public virtual void ConsumeDataMap(DataMap map)
        {
            // do nothing
        }

        public virtual DataMap ProvideDataMap()
        {
            // do nothing
            return DataMap.empty;
        }
    }
}