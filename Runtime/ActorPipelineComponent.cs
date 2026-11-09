/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/

namespace OmiLAXR
{
    /// <summary>
    /// Abstract base class for pipeline components that need access to Actor and Pipeline functionality.
    /// Provides automatic pipeline discovery through component hierarchy and extension system.
    /// Includes logging utilities with pipeline context information.
    /// </summary>
    public abstract class ActorPipelineComponent : PipelineComponent, IActorProvider
    {
        /// <summary>
        /// Cached reference to the associated Pipeline component.
        /// Automatically discovered through component hierarchy or extension system.
        /// </summary>
        private Pipeline _pipeline;

        /// <summary>
        /// Gets the Pipeline component associated with this component.
        /// Uses intelligent discovery to find pipelines through:
        /// 1. Direct component attachment
        /// 2. Parent hierarchy search
        /// 3. Pipeline extension system
        /// </summary>
        public Pipeline Pipeline
        {
            get
            {
                // Return cached pipeline if available
                if (!_pipeline)
                {
                    // Try to find pipeline on same GameObject or in parent hierarchy
#if UNITY_2020_1_OR_NEWER
                    _pipeline = GetComponent<Pipeline>() ?? GetComponentInParent<Pipeline>(true);
#else 
                    // Unity 2019 and earlier don't support includeInactive parameter
                    _pipeline = GetComponent<Pipeline>() ?? GetComponentInParent<Pipeline>(true);
#endif
                }

                // Return pipeline if found through standard hierarchy
                if (_pipeline) 
                    return _pipeline;
                
                // Fallback: Look for Pipeline through extension system
                // This handles cases where pipelines are extended or wrapped
                var pipelineExt = GetComponentInParent<IPipelineExtension>();
                _pipeline = pipelineExt.GetPipeline();

                return _pipeline;
            }
        }
        
        /// <summary>
        /// Gets the Actor component associated with this pipeline.
        /// Provides access to the learner/user being tracked by this pipeline.
        /// </summary>
        /// <returns>The Actor component representing the tracked entity</returns>
        public Actor GetActor() => Pipeline.actor;
        
        /// <summary>
        /// Gets the Instructor component that manages this pipeline.
        /// Provides access to the learning management and analytics coordination.
        /// </summary>
        /// <returns>The Instructor component managing this pipeline</returns>
        public Instructor GetInstructor() => Pipeline.instructor;
        
        /// <summary>
        /// Logs a formatted message with pipeline context information.
        /// Automatically prefixes messages with the pipeline name for easier debugging.
        /// </summary>
        /// <param name="message">Format string for the log message</param>
        /// <param name="ps">Parameters for string formatting</param>
        protected void Log(string message, params object[] ps)
            => DebugLog.OmiLAXR.Print($"(Pipeline '{Pipeline.name}') " + message);
    }
}