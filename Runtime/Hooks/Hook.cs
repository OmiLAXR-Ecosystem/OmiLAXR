/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System.Linq;
using OmiLAXR.Composers;

namespace OmiLAXR.Hooks
{
    /// <summary>
    /// Abstract base class for statement processing hooks in the OmiLAXR pipeline.
    /// Hooks provide extension points for modifying, enriching, or validating statements
    /// after they have been composed but before final processing by endpoints.
    /// Used for adding contextual information, applying business rules, or transforming statement data.
    /// </summary>
    public abstract class Hook : PipelineComponent, IHook
    {
        /// <summary>
        /// Abstract method called after a statement has been composed.
        /// Implementing classes should define their statement processing logic here.
        /// Can modify the statement, enrich it with additional data, or return a completely new statement.
        /// </summary>
        /// <param name="statement">The statement to process after composition</param>
        /// <returns>The processed statement (can be the same instance or a new one)</returns>
        public abstract IStatement AfterCompose(IStatement statement);
        
        /// <summary>
        /// Utility method to retrieve a specific type of ActorDataProvider from a statement's pipeline information.
        /// Searches through the statement's associated pipeline for a provider of the specified type.
        /// Useful for accessing actor information, authentication data, or other provider-specific context.
        /// </summary>
        /// <typeparam name="TS">The type of ActorDataProvider to retrieve</typeparam>
        /// <param name="statement">The statement containing pipeline information to search</param>
        /// <param name="includeInactive">Whether to include disabled providers in the search</param>
        /// <returns>The first matching provider of the specified type, or null if not found</returns>
        protected TS GetProvider<TS>(IStatement statement, bool includeInactive = false) where TS : ActorDataProvider
            => statement.GetSenderPipelineInfo().ActorDataProviders?
                .FirstOrDefault(o => (includeInactive || o.enabled) && o is TS) as TS;
    }
}