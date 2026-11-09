/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System.Collections.Generic;
using OmiLAXR.Composers;

namespace OmiLAXR.Endpoints
{
    /// <summary>
    /// Receives statements, e.g. to queue them for sending. This is all a <see cref="DataProvider"/> needs from an endpoint.
    /// </summary>
    public interface IStatementSink
    {
        /// <summary>
        /// Adds a single statement to the sending queue.
        /// </summary>
        /// <param name="statement">The statement to be sent</param>
        void SendStatement(IStatement statement);
    }

    /// <summary>
    /// Start/pause/stop control of a sending process.
    /// </summary>
    public interface ISendingLifecycle
    {
        /// <summary>
        /// Indicates whether the endpoint is currently in the process of sending statements.
        /// </summary>
        bool IsSending { get; }

        /// <summary>
        /// Indicates whether a transfer operation is currently in progress.
        /// </summary>
        bool IsTransferring { get; }

        /// <summary>
        /// Begins the statement sending process.
        /// </summary>
        /// <param name="resetQueue">Optional flag to reset the statement queue before starting</param>
        void StartSending(bool resetQueue = false);

        /// <summary>
        /// Temporarily pauses the statement sending process.
        /// </summary>
        void PauseSending();

        /// <summary>
        /// Completely stops the statement sending process.
        /// </summary>
        void StopSending();
    }

    /// <summary>
    /// Synchronously transfers a batch of statements to its destination (file, LRS, ...).
    /// </summary>
    public interface IStatementTransport
    {
        TransferCode Send(IReadOnlyList<IStatement> batch);
    }

    /// <summary>
    /// Defines the contract for statement transfer endpoints.
    /// Provides methods for managing statement sending lifecycle.
    /// </summary>
    public interface IEndpoint : IStatementSink, ISendingLifecycle
    {
    }
}
