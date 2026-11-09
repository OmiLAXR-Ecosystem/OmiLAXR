/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System;
using Object = UnityEngine.Object;

namespace OmiLAXR
{
    /// <summary>
    /// Lifecycle and object discovery events of a <see cref="Pipeline"/>, i.e. everything tracking behaviours listen to.
    /// </summary>
    public interface IPipelineEvents
    {
        event PipelineStartedHandler BeforeStartedPipeline;
        event PipelineStartedHandler AfterStartedPipeline;
        event PipelineStoppedHandler BeforeStoppedPipeline;
        event PipelineStoppedHandler AfterStoppedPipeline;
        event PipelineStoppedHandler OnQuit;

        /// <summary>Objects reported by listeners, before filtering.</summary>
        event Action<Object[]> AfterFoundObjects;

        /// <summary>Objects that passed all filters.</summary>
        event Action<Object[]> AfterFilteredObjects;
    }

    /// <summary>
    /// Provides the actor (and optional instructor) data is tracked for.
    /// </summary>
    public interface IActorProvider
    {
        /// <summary>
        /// Gets the Actor component associated with this tracking behavior.
        /// The Actor represents the entity being tracked (e.g., user, NPC, system).
        /// </summary>
        /// <returns>The Actor component in the tracking pipeline</returns>
        Actor GetActor();

        /// <summary>
        /// Gets the Instructor component that manages this tracking behavior.
        /// The Instructor coordinates tracking behaviors and handles their lifecycle.
        /// </summary>
        /// <returns>The Instructor component managing this tracking behavior</returns>
        Instructor GetInstructor();
    }
}
