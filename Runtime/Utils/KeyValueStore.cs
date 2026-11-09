/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using UnityEngine;

namespace OmiLAXR.Utils
{
    /// <summary>
    /// Persistent string storage, by default backed by <see cref="PlayerPrefs"/>.
    /// </summary>
    public interface IKeyValueStore
    {
        bool HasKey(string key);
        string GetString(string key);
        void SetString(string key, string value);
        void DeleteKey(string key);
        void Save();
    }

    /// <summary>
    /// <see cref="IKeyValueStore"/> backed by Unity's <see cref="PlayerPrefs"/>.
    /// </summary>
    public sealed class PlayerPrefsStore : IKeyValueStore
    {
        public static readonly PlayerPrefsStore Instance = new PlayerPrefsStore();

        public bool HasKey(string key) => PlayerPrefs.HasKey(key);
        public string GetString(string key) => PlayerPrefs.GetString(key);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);
        public void Save() => PlayerPrefs.Save();
    }
}
