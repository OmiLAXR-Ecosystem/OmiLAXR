/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System.Collections.Generic;
using System.Linq;
using OmiLAXR.Composers;
using OmiLAXR.Composers.HigherComposers;
using OmiLAXR.Endpoints;
using OmiLAXR.Hooks;
using UnityEngine;

namespace OmiLAXR
{
    /// <summary>
    /// Core component that manages data flow through the OmiLAXR pipeline system.
    /// Serves as a central hub for composers, hooks, and endpoints.
    /// Executes early in Unity's order to ensure data is available for other components.
    /// </summary>
    [AddComponentMenu("OmiLAXR / Core / Data Provider")]
    [DefaultExecutionOrder(-1)]
    public class DataProvider : PipelineComponent
    {
        /// <summary>
        /// Collection of composers that generate statements/data for the pipeline.
        /// </summary>
        public readonly List<IComposer> Composers = new List<IComposer>();

        /// <summary>
        /// Collection of higher-level composers that process and aggregate statements
        /// from regular composers to create more complex data structures.
        /// </summary>
        public readonly List<HigherComposer<IStatement>> HigherComposers =
            new List<HigherComposer<IStatement>>();

        /// <summary>
        /// Collection of hooks that can intercept and modify statements as they flow through the pipeline.
        /// </summary>
        public readonly List<Hook> Hooks = new List<Hook>();
        
        /// <summary>
        /// Collection of endpoints that receive and process the final statements,
        /// often responsible for data persistence, transmission, or visualization.
        /// </summary>
        public readonly List<Endpoint> Endpoints = new List<Endpoint>();   
        
        /// <summary>
        /// Extensions that can add functionality to the DataProvider without modifying its core implementation.
        /// </summary>
        public readonly List<IDataProviderExtension> Extensions = new List<IDataProviderExtension>();

        /// <summary>
        /// All hooks statements pass through: <see cref="Hook"/> components in the hierarchy plus hooks added via <see cref="AddHook"/>.
        /// </summary>
        public readonly List<IStatementHook> StatementHooks = new List<IStatementHook>();

        /// <summary>
        /// All receivers of processed statements: endpoints in the hierarchy plus sinks added via <see cref="AddSink"/>.
        /// </summary>
        public readonly List<IStatementSink> StatementSinks = new List<IStatementSink>();

        private readonly List<IStatementHook> _registeredHooks = new List<IStatementHook>();
        private readonly List<IStatementSink> _registeredSinks = new List<IStatementSink>();

        /// <summary>
        /// Registers a hook that is not a component in this hierarchy (kept across re-enabling).
        /// </summary>
        public void AddHook(IStatementHook hook) => Register(_registeredHooks, StatementHooks, hook);

        /// <summary>
        /// Registers a statement receiver that is not a component in this hierarchy (kept across re-enabling).
        /// </summary>
        public void AddSink(IStatementSink sink) => Register(_registeredSinks, StatementSinks, sink);

        private void Register<T>(List<T> registered, List<T> active, T item)
        {
            if (item == null || registered.Contains(item))
                return;
            registered.Add(item);
            if (_isInit && !active.Contains(item))
                active.Add(item);
        }

        private static void AddDistinct<T>(List<T> target, IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                if (!target.Contains(item))
                    target.Add(item);
            }
        }

        // Disabled components are skipped; plain C# hooks and sinks are always active
        private static bool IsActive(object item) => !(item is Behaviour behaviour) || behaviour.enabled;
        
        /// <summary>
        /// Retrieves the first composer of the specified type.
        /// </summary>
        /// <typeparam name="T">Type of composer to retrieve</typeparam>
        /// <returns>The first composer of the specified type, or null if none exists</returns>
        public T GetComposer<T>() where T : IComposer
            => Composers.OfType<T>().FirstOrDefault();
        
        private bool _isInit;
        
        /// <summary>
        /// Initializes the DataProvider by discovering and registering all available
        /// composers, higher composers, hooks, and endpoints in its children.
        /// Sets up event subscriptions for processing statements.
        /// </summary>
        protected override void OnEnable()
        {
            if (_isInit)
                return;
            
            // Find available composers
            var composers = GetComponentsInChildren<IComposer>(true);
            Composers.AddRange(composers);
            
            // Find available higher composers
            HigherComposers.AddRange(Composers.Where(c => c.IsHigherComposer)
                .Select(c => c as HigherComposer<IStatement>));
            
            // Find available hooks
            Hooks.AddRange(GetComponentsInChildren<Hook>(true));
            
            // Find available data endpoints
            Endpoints.AddRange(GetComponentsInChildren<Endpoint>(true));

            // Hooks and sinks used for routing, including registered non-component ones
            AddDistinct(StatementHooks, GetComponentsInChildren<IStatementHook>(true));
            AddDistinct(StatementHooks, _registeredHooks);
            AddDistinct(StatementSinks, GetComponentsInChildren<IStatementSink>(true));
            AddDistinct(StatementSinks, _registeredSinks);
            
            // Subscribe to each composer's AfterComposed event to process statements
            foreach (var composer in Composers)
            {
                composer.AfterComposed += HandleStatement;
            }

            _isInit = true;
        }

        private void OnDisable()
        {
            Cleanup();
        }
        
        private void Cleanup()
        {
            if (!_isInit)
                return;
            
            foreach (var composer in Composers)
            {
                composer.AfterComposed -= HandleStatement;
            }
            
            Composers.Clear();
            Endpoints.Clear();
            HigherComposers.Clear();
            Hooks.Clear();
            StatementHooks.Clear();
            StatementSinks.Clear();
            _isInit = false;
        }

        /// <summary>
        /// Processes statements from composers through the pipeline flow:
        /// 1. Provides statements to higher composers for potential aggregation
        /// 2. Passes statements through all active hooks for modification/filtering
        /// 3. Distributes statements to all registered endpoints
        /// </summary>
        /// <param name="sender">The composer that generated the statement</param>
        /// <param name="statement">The data/statement to be processed</param>
        private void HandleStatement(IComposer sender, IStatement statement)
        {
            // First, allow higher composers to examine and potentially aggregate the statement
            foreach (var composer in HigherComposers)
            {
                if (!composer.enabled)
                    continue;
                composer.LookFor(statement);   
            }
            
            // Then, pass through hooks for potential modification or filtering
            foreach (var hook in StatementHooks)
            {
                if (!IsActive(hook))
                    continue;
                statement = hook.AfterCompose(statement);
                if (statement.IsDiscarded())
                    return; // Statement was marked to be discarded by a hook
            }
            
            // Finally, distribute to all endpoints
            foreach (var sink in StatementSinks)
            {
                if (!IsActive(sink))
                    continue;
                sink.SendStatement(statement);
            }
        }

        /// <summary>
        /// Static helper to find the DataProvider instance in the scene.
        /// </summary>
        /// <returns>The first available DataProvider</returns>
        public static DataProvider GetAll() => FindObject<DataProvider>();
    }
}