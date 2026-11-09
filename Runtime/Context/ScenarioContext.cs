/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System;
using OmiLAXR.Composers;

namespace OmiLAXR.Context
{
    /// <summary>Registration (session) UUID statements are grouped by.</summary>
    public interface IRegistrationProvider
    {
        /// <summary>The registration, or null if none (or no valid UUID) is configured.</summary>
        Guid? GetRegistration();
    }

    /// <summary>Language of the learning scenario.</summary>
    public interface ILanguageProvider
    {
        /// <summary>Language code like "en" or "de".</summary>
        string GetLanguageCode();
    }

    /// <summary>Platform description attached to statements.</summary>
    public interface IPlatformInfoProvider
    {
        string GetPlatformString(string module, string version, IComposer composer);
    }

    /// <summary>Current frame rate.</summary>
    public interface IFpsProvider
    {
        /// <summary>Frames per second, or null if not measured.</summary>
        int? GetFps();
    }

    /// <summary>How tracked objects are named.</summary>
    public interface ITrackingNameSettings
    {
        GlobalSettings.TrackingNameBehaviour TrackingNameBehaviour { get; }
    }

    /// <summary>
    /// Access point for the scenario context. By default each provider is the corresponding
    /// component in the scene (or null); assign a provider to override it, e.g. in tests, and
    /// call <see cref="Reset"/> to return to the scene components.
    /// </summary>
    public static class ScenarioContext
    {
        private static IRegistrationProvider _registration;
        private static ILanguageProvider _language;
        private static IPlatformInfoProvider _platform;
        private static IFpsProvider _fps;
        private static ITrackingNameSettings _trackingNames;

        public static IRegistrationProvider Registration
        {
            get => _registration ?? Scene(OmiLAXR.Context.Registration.Instance);
            set => _registration = value;
        }

        public static ILanguageProvider Language
        {
            get => _language ?? Scene(ScenarioLanguage.Instance);
            set => _language = value;
        }

        public static IPlatformInfoProvider Platform
        {
            get => _platform ?? Scene(PlatformInformation.Instance);
            set => _platform = value;
        }

        public static IFpsProvider Fps
        {
            get => _fps ?? Scene(FpsMonitor.Instance);
            set => _fps = value;
        }

        public static ITrackingNameSettings TrackingNames
        {
            get => _trackingNames ?? Scene(GlobalSettings.Instance);
            set => _trackingNames = value;
        }

        /// <summary>Removes all overrides.</summary>
        public static void Reset()
        {
            _registration = null;
            _language = null;
            _platform = null;
            _fps = null;
            _trackingNames = null;
        }

        // Unity's null check, so destroyed components count as missing
        private static T Scene<T>(T component) where T : UnityEngine.Object => component ? component : null;
    }
}
