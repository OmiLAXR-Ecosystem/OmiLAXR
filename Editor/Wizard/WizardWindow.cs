/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace OmiLAXR.Editor.Wizard
{
    /// <summary>
    /// Step-by-step wizard for setting up OmiLAXR in a project: required steps, optional tracking, tools and AI support (MCP).
    /// Modules add their own checks and actions to a step by implementing <see cref="IWizardSection"/>.
    /// <see cref="WizardStartup"/> opens it the first time the package is loaded in a project.
    /// </summary>
    public sealed class WizardWindow : EditorWindow
    {
        private const string PackageRoot = "Packages/com.rwth.unity.omilaxr/Editor/Wizard/";
        private const string LogoPath = PackageRoot + "Images/OmiLAXR_Logo.png";
        private const string StyleSheetPath = PackageRoot + "WizardWindow.uss";
        private const string StepKey = "OmiLAXR.Wizard.Step";
        private const string McpAuthorKey = "OmiLAXR.Wizard.McpAuthor";
        private const string McpRunnerKey = "OmiLAXR.Wizard.McpRunner";

        private static readonly (WizardStep Step, string Label)[] Steps =
        {
            (WizardStep.Welcome, "Welcome"),
            (WizardStep.Required, "Required"),
            (WizardStep.Tracking, "Tracking"),
            (WizardStep.Tools, "Tools"),
            (WizardStep.AI, "AI (MCP)"),
            (WizardStep.Done, "Done"),
        };

        private struct CheckResult
        {
            public IWizardSection Section;
            public WizardCheck Check;
            public bool Passed;
        }

        private readonly List<IWizardSection> _sections = new List<IWizardSection>();
        private readonly List<CheckResult> _results = new List<CheckResult>();
        private ModuleCatalog _catalog;
        private bool _registryConfigured;
        private string _signature;

        private VisualElement _stepper;
        private ScrollView _content;
        private Button _back;
        private Button _next;
        private string _mcpMessage;

        [MenuItem("OmiLAXR / Open Wizard", priority = -100)]
        public static void Open()
        {
            var window = GetWindow<WizardWindow>();
            window.titleContent = new GUIContent("OmiLAXR Wizard");
            window.minSize = new Vector2(560f, 520f);
            window.Show();
        }

        private static WizardStep CurrentStep
        {
            get => (WizardStep)SessionState.GetInt(StepKey, (int)WizardStep.Welcome);
            set => SessionState.SetInt(StepKey, (int)value);
        }

        #region Lifecycle

        private void OnEnable()
        {
            CreateSections();

            _catalog = new ModuleCatalog();
            _catalog.Changed += Rebuild;

            BuildLayout();

            EditorApplication.hierarchyChanged += Evaluate;
            EditorApplication.projectChanged += Evaluate;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;

            Evaluate();
            if (_registryConfigured)
                _catalog.Refresh();
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= Evaluate;
            EditorApplication.projectChanged -= Evaluate;
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
            _catalog.Changed -= Rebuild;
            _catalog.Dispose();
        }

        private void OnFocus() => Evaluate();

        private void OnActiveSceneChanged(UnityEngine.SceneManagement.Scene from, UnityEngine.SceneManagement.Scene to) => Evaluate();

        private void CreateSections()
        {
            _sections.Clear();
            foreach (var type in TypeCache.GetTypesDerivedFrom<IWizardSection>())
            {
                if (type.IsAbstract || type.IsInterface || type.GetConstructor(Type.EmptyTypes) == null)
                    continue;
                try
                {
                    _sections.Add((IWizardSection)Activator.CreateInstance(type));
                }
                catch (Exception ex)
                {
                    DebugLog.OmiLAXR.Error($"Could not create wizard section '{type.FullName}': {ex.Message}");
                }
            }
            _sections.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Title, b.Title));
        }

        /// <summary>
        /// Runs all checks and rebuilds the page if a result changed.
        /// </summary>
        private void Evaluate()
        {
            _results.Clear();
            foreach (var section in _sections)
            {
                List<WizardCheck> checks;
                try
                {
                    checks = section.GetChecks()?.ToList() ?? new List<WizardCheck>();
                }
                catch (Exception ex)
                {
                    DebugLog.OmiLAXR.Error($"Wizard section '{section.Title}' failed to list its checks: {ex.Message}");
                    continue;
                }

                foreach (var check in checks)
                {
                    bool passed;
                    try
                    {
                        passed = check.Evaluate();
                    }
                    catch (Exception ex)
                    {
                        DebugLog.OmiLAXR.Warning($"Wizard check '{check.Label}' failed: {ex.Message}");
                        passed = false;
                    }
                    _results.Add(new CheckResult { Section = section, Check = check, Passed = passed });
                }
            }

            try
            {
                _registryConfigured = ScopedRegistry.IsConfigured();
            }
            catch (Exception)
            {
                _registryConfigured = false;
            }

            var signature = new StringBuilder(_registryConfigured ? "R" : "r");
            foreach (var r in _results)
                signature.Append(r.Passed ? '1' : '0');
            if (signature.ToString() == _signature)
                return;
            _signature = signature.ToString();
            Rebuild();
        }

        #endregion

        #region Layout

        // Built in OnEnable instead of CreateGUI, which older Unity versions do not call
        private void BuildLayout()
        {
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList("wizard");
            root.AddToClassList(EditorGUIUtility.isProSkin ? "dark" : "light");

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
            if (styleSheet)
                root.styleSheets.Add(styleSheet);

            root.Add(CreateHeader());

            _stepper = new VisualElement();
            _stepper.AddToClassList("stepper");
            root.Add(_stepper);

            _content = new ScrollView();
            _content.style.flexGrow = 1;
            root.Add(_content);

            root.Add(CreateFooter());
        }

        private static VisualElement CreateHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("header");

            var logo = AssetDatabase.LoadAssetAtPath<Texture2D>(LogoPath);
            if (logo)
            {
                var image = new Image { image = logo, scaleMode = ScaleMode.ScaleToFit };
                image.AddToClassList("logo");
                header.Add(image);
            }

            var text = new VisualElement();
            text.AddToClassList("header-text");
            text.Add(Css(new Label("Wizard"), "header-title"));

            var core = PackageInfo.FindForAssembly(typeof(Pipeline).Assembly);
            text.Add(Css(new Label($"Learning analytics for XR · Core {core?.version ?? "?"} · Unity {Application.unityVersion}"), "header-subtitle"));
            header.Add(text);
            return header;
        }

        private VisualElement CreateFooter()
        {
            var footer = new VisualElement();
            footer.AddToClassList("footer");

            var showOnStartup = new Toggle { text = "Show on startup", value = WizardStartup.ShowOnStartup };
            showOnStartup.RegisterValueChangedCallback(e => WizardStartup.ShowOnStartup = e.newValue);
            footer.Add(showOnStartup);

            footer.Add(Css(new VisualElement(), "spacer"));

            _back = Css(new Button(() => GoTo(CurrentStep - 1)) { text = "Back" }, "btn");
            footer.Add(_back);

            _next = Css(new Button(() =>
            {
                if (CurrentStep == WizardStep.Done)
                    Close();
                else
                    GoTo(CurrentStep + 1);
            }), "btn", "btn--primary");
            footer.Add(_next);
            return footer;
        }

        private void GoTo(WizardStep step)
        {
            if (step < WizardStep.Welcome || step > WizardStep.Done)
                return;
            CurrentStep = step;
            Rebuild();
            _content.scrollOffset = Vector2.zero;
        }

        /// <summary>
        /// Rebuilds the stepper, the current page and the footer buttons.
        /// </summary>
        private void Rebuild()
        {
            // The layout is not built yet
            if (_content == null || _next == null)
                return;

            var scroll = _content.scrollOffset;
            BuildStepper();

            _content.Clear();
            var page = Css(new VisualElement(), "page");
            switch (CurrentStep)
            {
                case WizardStep.Welcome: BuildWelcome(page); break;
                case WizardStep.Required: BuildRequired(page); break;
                case WizardStep.Tracking: BuildTracking(page); break;
                case WizardStep.Tools: BuildTools(page); break;
                case WizardStep.AI: BuildAi(page); break;
                case WizardStep.Done: BuildDone(page); break;
            }
            _content.Add(page);
            _content.scrollOffset = scroll;

            _back.style.visibility = CurrentStep == WizardStep.Welcome ? Visibility.Hidden : Visibility.Visible;
            _next.text = CurrentStep == WizardStep.Done ? "Finish" : CurrentStep == WizardStep.Welcome ? "Start" : "Next";
        }

        private void BuildStepper()
        {
            _stepper.Clear();
            for (var i = 0; i < Steps.Length; i++)
            {
                var (step, label) = Steps[i];
                var item = Css(new VisualElement(), "step");
                var state = StepState(step);
                if (step == CurrentStep)
                    item.AddToClassList("step--current");
                else if (state != null)
                    item.AddToClassList(state.Value ? "step--ok" : "step--warn");

                var symbol = step != CurrentStep && state != null ? (state.Value ? "✓" : "!") : (i + 1).ToString();
                var circle = Css(new Button(() => GoTo(step)) { text = symbol }, "step-circle");
                item.Add(circle);
                item.Add(Css(new Label(label), "step-label"));
                _stepper.Add(item);
            }
        }

        /// <summary>
        /// True if a step is complete, false if it needs attention, null if it has no state.
        /// </summary>
        private bool? StepState(WizardStep step)
        {
            switch (step)
            {
                case WizardStep.Required:
                    return RequiredFailures() == 0;
                case WizardStep.AI:
                    return Enum.GetValues(typeof(McpClient)).Cast<McpClient>().Any(McpConfig.IsConfigured) ? true : (bool?)null;
                default:
                    return null;
            }
        }

        private int RequiredFailures()
            => _results.Count(r => r.Section.Step == WizardStep.Required && !r.Passed && !r.Check.Optional);

        #endregion

        #region Pages

        private void BuildWelcome(VisualElement page)
        {
            page.Add(Css(new Label("Welcome to OmiLAXR"), "h1"));
            page.Add(Text("OmiLAXR tracks what learners do in XR and other Unity applications and turns it into learning analytics data, " +
                          "for example xAPI statements for a Learning Record Store. This wizard sets up your project in a few steps:"));

            var overview = Css(new VisualElement(), "overview");
            overview.Add(OverviewCard("1  Required", "Scoped registry, pipeline and a data provider. Without these, nothing is tracked."));
            overview.Add(OverviewCard("2  Tracking", "Optional: XR input, heart rate and other physiological measures (PPM), pseudonyms."));
            overview.Add(OverviewCard("3  Tools", "Optional: ReCoPa to watch data live, xAPI4Unity to work with xAPI definitions."));
            overview.Add(OverviewCard("4  AI (MCP)", "Optional: connect AI coding agents such as Claude Code, Cursor or VS Code to OmiLAXR."));
            page.Add(overview);

            page.Add(Muted("You can skip any optional step and come back later via 'OmiLAXR > Open Wizard'."));
        }

        private void BuildRequired(VisualElement page)
        {
            page.Add(Css(new Label("Required steps"), "h1"));
            page.Add(Text("These steps are needed so that OmiLAXR can track data and send it somewhere."));

            page.Add(H2("Status"));
            page.Add(ChecksCard(WizardStep.Required));

            AddModuleGroups(page, WizardStep.Required);
            AddSectionActions(page, WizardStep.Required);
        }

        private void BuildTracking(VisualElement page)
        {
            page.Add(Css(new Label("Tracking"), "h1"));
            page.Add(Text("Choose what else to track. Everything on this page is optional."));

            AddModuleGroups(page, WizardStep.Tracking);

            if (_results.Any(r => r.Section.Step == WizardStep.Tracking))
            {
                page.Add(H2("Status"));
                page.Add(ChecksCard(WizardStep.Tracking));
            }
            AddSectionActions(page, WizardStep.Tracking);
        }

        private void BuildTools(VisualElement page)
        {
            page.Add(Css(new Label("Tools"), "h1"));
            page.Add(Text("Tools that work with OmiLAXR. Everything on this page is optional."));

            AddModuleGroups(page, WizardStep.Tools);

            // Packages in the registry the wizard does not know yet
            var known = new HashSet<string>(WizardModules.All.Select(m => m.Name)) { WizardModules.Core };
            var others = _catalog.Modules.Where(m => !known.Contains(m.Name)).ToList();
            if (others.Count > 0)
            {
                page.Add(H2("More packages"));
                foreach (var entry in others)
                    page.Add(ModuleCard(entry.DisplayName ?? entry.Name, entry.Description, entry, null, null));
            }

            if (_results.Any(r => r.Section.Step == WizardStep.Tools))
            {
                page.Add(H2("Status"));
                page.Add(ChecksCard(WizardStep.Tools));
            }
            AddSectionActions(page, WizardStep.Tools);
        }

        private void BuildAi(VisualElement page)
        {
            page.Add(Css(new Label("AI assistance (MCP)"), "h1"));
            page.Add(Text("The OmiLAXR MCP server gives AI coding agents knowledge about OmiLAXR: they can search and propose xAPI definitions, " +
                          "follow the recipes for listeners, tracking behaviours, composers and endpoints, and generate components " +
                          "that match the package conventions. This step writes the server into the agents' configuration files in this project."));

            page.Add(H2("Agents"));
            var agents = Css(new VisualElement(), "card");
            var selected = new Dictionary<McpClient, Toggle>();
            foreach (McpClient client in Enum.GetValues(typeof(McpClient)))
            {
                var configured = McpConfig.IsConfigured(client);
                var row = Css(new VisualElement(), "card-row", "option");
                var toggle = new Toggle { text = ClientName(client), value = configured || client == McpClient.ClaudeCode };
                toggle.style.flexGrow = 1;
                selected[client] = toggle;
                row.Add(toggle);
                row.Add(Muted(McpConfig.RelativePath(client)));
                if (configured)
                    row.Add(Badge("configured", "badge--ok"));
                agents.Add(row);
            }
            page.Add(agents);

            page.Add(H2("Start the server with"));
            var runnerCard = Css(new VisualElement(), "card");
            var runner = (McpRunner)EditorPrefs.GetInt(McpRunnerKey, (int)McpRunner.Dnx);
            var dnx = new Toggle { text = "dnx OmiLAXR.MCP", value = runner == McpRunner.Dnx };
            var tool = new Toggle { text = "omilaxr-mcp", value = runner == McpRunner.InstalledTool };
            // The two toggles act as radio buttons
            dnx.RegisterValueChangedCallback(e => { tool.SetValueWithoutNotify(!e.newValue); SaveRunner(e.newValue ? McpRunner.Dnx : McpRunner.InstalledTool); });
            tool.RegisterValueChangedCallback(e => { dnx.SetValueWithoutNotify(!e.newValue); SaveRunner(e.newValue ? McpRunner.InstalledTool : McpRunner.Dnx); });
            runnerCard.Add(dnx);
            runnerCard.Add(Css(Muted("Runs the server from NuGet without installing it. Needs the .NET 10 SDK."), "option"));
            runnerCard.Add(tool);
            runnerCard.Add(Css(Muted("Uses the installed dotnet tool: dotnet tool install -g OmiLAXR.MCP"), "option"));

            var author = new TextField("Author") { value = EditorPrefs.GetString(McpAuthorKey, "") };
            author.RegisterValueChangedCallback(e => EditorPrefs.SetString(McpAuthorKey, e.newValue));
            runnerCard.Add(Css(author, "field"));
            runnerCard.Add(Muted("'Name <email>' for license headers and package.json of generated files. Empty: taken from git config."));
            runnerCard.Add(Muted("The agent may write generated files into this project (OMILAXR_WRITE_ROOTS). It never overwrites existing files."));
            page.Add(runnerCard);

            var actions = Css(new VisualElement(), "card-row");
            actions.Add(Css(new Button(() =>
            {
                var written = new List<string>();
                foreach (var pair in selected.Where(p => p.Value.value))
                {
                    try
                    {
                        McpConfig.Write(pair.Key, (McpRunner)EditorPrefs.GetInt(McpRunnerKey, (int)McpRunner.Dnx), author.value);
                        written.Add(McpConfig.RelativePath(pair.Key));
                    }
                    catch (Exception ex)
                    {
                        DebugLog.OmiLAXR.Error($"Could not write the MCP configuration for {ClientName(pair.Key)}: {ex.Message}");
                    }
                }
                _mcpMessage = written.Count > 0
                    ? $"Written: {string.Join(", ", written)}. Restart the agent or reload its MCP servers."
                    : "Nothing written. Select at least one agent.";
                Rebuild();
            }) { text = "Write MCP configuration" }, "btn", "btn--primary"));
            actions.Add(Css(new VisualElement(), "spacer"));
            actions.Add(LinkButton("MCP documentation", "https://github.com/OmiLAXR-Ecosystem/OmiLAXR.MCP"));
            page.Add(actions);

            if (_mcpMessage != null)
                page.Add(Css(Muted(_mcpMessage), "field"));
        }

        private void BuildDone(VisualElement page)
        {
            var failures = RequiredFailures();
            page.Add(Css(new Label(failures == 0 ? "All set" : "Almost done"), "h1"));
            page.Add(Text(failures == 0
                ? "The required steps are complete. Press Play to start tracking."
                : $"{failures} required step(s) still need attention. Go back to 'Required' to fix them."));

            page.Add(H2("Summary"));
            var summary = Css(new VisualElement(), "card");
            var required = _results.Where(r => r.Section.Step == WizardStep.Required && !r.Check.Optional).ToList();
            summary.Add(SummaryRow("Required steps", $"{required.Count(r => r.Passed)} of {required.Count} done", failures == 0));

            var installed = _catalog.Modules.Where(m => m.IsInstalled && m.Name != WizardModules.Core).Select(m => m.DisplayName ?? m.Name).ToList();
            summary.Add(SummaryRow("Modules", installed.Count > 0 ? string.Join(", ", installed) : "none", installed.Count > 0));

            var agents = Enum.GetValues(typeof(McpClient)).Cast<McpClient>().Where(McpConfig.IsConfigured).Select(ClientName).ToList();
            summary.Add(SummaryRow("AI (MCP)", agents.Count > 0 ? string.Join(", ", agents) : "not configured", agents.Count > 0));
            page.Add(summary);

            page.Add(H2("Learn more"));
            var links = Css(new VisualElement(), "card");
            links.Add(LinkButton("Documentation (omilaxr.dev)", "https://omilaxr.dev/"));
            links.Add(LinkButton("Get started guide", "https://omilaxr.dev/get-started"));
            links.Add(LinkButton("xAPI Registry", "https://omilaxr-ecosystem.github.io/xapi/"));
            links.Add(LinkButton("Changelog", "https://github.com/SGoerzen/OmiLAXR/blob/main/CHANGELOG.md"));
            page.Add(links);
        }

        #endregion

        #region Building blocks

        private void AddModuleGroups(VisualElement page, WizardStep step)
        {
            foreach (var group in WizardModules.All.Where(m => m.Step == step).GroupBy(m => m.Group))
            {
                page.Add(H2(group.Key));
                foreach (var module in group)
                    page.Add(ModuleCard(module.Title, module.Description, _catalog.Get(module.Name), module.LinkLabel, module.LinkUrl));
            }

            if (!_registryConfigured)
                page.Add(Muted("Packages can be installed once the scoped registry is added (see 'Required')."));
            else if (_catalog.Status != null)
                page.Add(Muted(_catalog.Status));
            if (_catalog.Error != null)
                page.Add(Muted($"Package Manager: {_catalog.Error}"));
        }

        private VisualElement ModuleCard(string title, string description, ModuleEntry entry, string linkLabel, string linkUrl)
        {
            var card = Css(new VisualElement(), "card");
            var row = Css(new VisualElement(), "card-row");

            var body = Css(new VisualElement(), "card-body");
            body.Add(Css(new Label(title), "card-title"));
            body.Add(Muted(entry.Name));
            if (!string.IsNullOrEmpty(description))
                body.Add(Css(Text(description), "field"));
            if (linkLabel != null)
                body.Add(LinkButton(linkLabel, linkUrl));
            row.Add(body);

            var actions = Css(new VisualElement(), "card-actions");
            if (entry.IsInstalled && !entry.IsManagedByRegistry)
                actions.Add(Badge($"{entry.Source.ToString().ToLowerInvariant()} {entry.InstalledVersion}", "badge--local"));
            else if (entry.HasUpdate)
                actions.Add(Badge($"{entry.InstalledVersion} → {entry.LatestVersion}", "badge--update"));
            else if (entry.IsInstalled)
                actions.Add(Badge($"installed {entry.InstalledVersion}", "badge--ok"));
            else
                actions.Add(Badge("not installed", "badge--off"));

            var canChange = _registryConfigured && !_catalog.IsBusy;
            if (!entry.IsInstalled)
                actions.Add(Enabled(Css(new Button(() => _catalog.Install(entry)) { text = "Install" }, "btn", "btn--primary"), canChange));
            else if (entry.HasUpdate)
                actions.Add(Enabled(Css(new Button(() => _catalog.Install(entry)) { text = "Update" }, "btn", "btn--primary"), canChange));
            if (entry.IsInstalled && entry.IsManagedByRegistry)
            {
                actions.Add(Enabled(Css(new Button(() =>
                {
                    if (EditorUtility.DisplayDialog("Remove package", $"Remove '{entry.Name}' from this project?", "Remove", "Cancel"))
                        _catalog.Remove(entry);
                }) { text = "Remove" }, "btn"), canChange));
            }
            row.Add(actions);

            card.Add(row);
            return card;
        }

        private VisualElement ChecksCard(WizardStep step)
        {
            var card = Css(new VisualElement(), "card");
            foreach (var result in _results.Where(r => r.Section.Step == step))
                card.Add(CheckRow(result));
            return card;
        }

        private VisualElement CheckRow(CheckResult result)
        {
            var check = result.Check;
            var row = Css(new VisualElement(), "check");

            var icon = result.Passed ? "TestPassed" : check.Optional ? "console.infoicon.sml" : "console.warnicon.sml";
            row.Add(Css(new Image { image = EditorGUIUtility.IconContent(icon).image, scaleMode = ScaleMode.ScaleToFit }, "check-icon"));

            var body = Css(new VisualElement(), "card-body");
            body.Add(new Label(check.Label));
            if (!result.Passed && !string.IsNullOrEmpty(check.Hint))
                body.Add(Muted(check.Hint));
            row.Add(body);

            if (!result.Passed && check.Fix != null)
                row.Add(Css(new Button(() => EditorApplication.delayCall += () => ApplyFix(check)) { text = check.FixLabel ?? "Fix" }, "btn", "btn--primary"));
            return row;
        }

        private void AddSectionActions(VisualElement page, WizardStep step)
        {
            foreach (var section in _sections.Where(s => s.Step == step))
            {
                page.Add(H2(section.Title));
                var card = Css(new VisualElement(), "card");
                card.Add(new IMGUIContainer(() =>
                {
                    try
                    {
                        section.OnGUI();
                    }
                    catch (ExitGUIException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        EditorGUILayout.HelpBox($"Section failed to draw: {ex.Message}", MessageType.Error);
                    }
                }));
                page.Add(card);
            }
        }

        private void ApplyFix(WizardCheck check)
        {
            try
            {
                check.Fix();
            }
            catch (Exception ex)
            {
                DebugLog.OmiLAXR.Error($"Fix for '{check.Label}' failed: {ex.Message}");
            }

            // The scoped registry decides whether modules can be listed
            var hadRegistry = _registryConfigured;
            Evaluate();
            if (!hadRegistry && _registryConfigured)
                _catalog.Refresh();
        }

        private static void SaveRunner(McpRunner runner) => EditorPrefs.SetInt(McpRunnerKey, (int)runner);

        private static string ClientName(McpClient client)
        {
            switch (client)
            {
                case McpClient.Cursor: return "Cursor";
                case McpClient.VSCode: return "VS Code (GitHub Copilot)";
                default: return "Claude Code";
            }
        }

        private static VisualElement OverviewCard(string title, string text)
        {
            var card = Css(new VisualElement(), "card");
            card.Add(Css(new Label(title), "card-title"));
            card.Add(Css(Text(text), "field"));
            return card;
        }

        private static VisualElement SummaryRow(string label, string value, bool ok)
        {
            var row = Css(new VisualElement(), "check");
            row.Add(Css(new Image { image = EditorGUIUtility.IconContent(ok ? "TestPassed" : "console.infoicon.sml").image, scaleMode = ScaleMode.ScaleToFit }, "check-icon"));
            var body = Css(new VisualElement(), "card-body");
            body.Add(Css(new Label(label), "card-title"));
            body.Add(Muted(value));
            row.Add(body);
            return row;
        }

        private static Label H2(string text) => Css(new Label(text), "h2");
        private static Label Text(string text) => Css(new Label(text), "text");
        private static Label Muted(string text) => Css(new Label(text), "muted");
        private static Label Badge(string text, string variant) => Css(new Label(text), "badge", variant);

        private static Button LinkButton(string text, string url)
            => Css(new Button(() => Application.OpenURL(url)) { text = text }, "btn", "link");

        private static T Enabled<T>(T element, bool enabled) where T : VisualElement
        {
            element.SetEnabled(enabled);
            return element;
        }

        private static T Css<T>(T element, params string[] classes) where T : VisualElement
        {
            foreach (var c in classes)
                element.AddToClassList(c);
            return element;
        }

        #endregion
    }
}
#endif
