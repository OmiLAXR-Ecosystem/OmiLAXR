/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using OmiLAXR.Composers;

namespace OmiLAXR.Hooks
{
    /// <summary>
    /// Marker interface for hook components in the OmiLAXR pipeline system.
    /// Hooks provide extension points for modifying or enriching statements
    /// after they have been composed but before they are processed by endpoints.
    /// Implementing classes can intercept and transform statement data
    /// to add contextual information, apply business rules, or perform validation.
    /// </summary>
    public interface IHook
    {
        // Marker interface - implementation details are provided by concrete Hook classes
    }

    /// <summary>
    /// A hook that processes statements after composing. Return the (possibly modified) statement;
    /// call <see cref="IDiscardable.Discard"/> on it to stop it from reaching the endpoints.
    /// </summary>
    public interface IStatementHook : IHook
    {
        IStatement AfterCompose(IStatement statement);
    }
}
