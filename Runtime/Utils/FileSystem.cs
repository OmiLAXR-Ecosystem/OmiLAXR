/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System.IO;

namespace OmiLAXR.Utils
{
    /// <summary>
    /// File system operations used by file based endpoints and writers.
    /// Replace <see cref="FileSystem.Current"/> to redirect file access, e.g. to memory in tests.
    /// </summary>
    public interface IFileSystem
    {
        bool FileExists(string path);
        bool DirectoryExists(string path);
        void CreateDirectory(string path);
        void DeleteFile(string path);

        /// <summary>Opens a file for writing; <paramref name="append"/> keeps existing content, otherwise it is truncated.</summary>
        Stream OpenWrite(string path, bool append);

        Stream OpenRead(string path);
        string ReadAllText(string path);
    }

    /// <summary>
    /// <see cref="IFileSystem"/> operating on the real disk.
    /// </summary>
    public sealed class PhysicalFileSystem : IFileSystem
    {
        public static readonly PhysicalFileSystem Instance = new PhysicalFileSystem();

        public bool FileExists(string path) => File.Exists(path);
        public bool DirectoryExists(string path) => Directory.Exists(path);
        public void CreateDirectory(string path) => Directory.CreateDirectory(path);
        public void DeleteFile(string path) => File.Delete(path);

        public Stream OpenWrite(string path, bool append)
        {
            // OpenOrCreate + seek instead of FileMode.Append, so callers can still reposition the stream
            var stream = new FileStream(path, append ? FileMode.OpenOrCreate : FileMode.Create, FileAccess.Write, FileShare.ReadWrite, 8192);
            if (append)
                stream.Seek(0, SeekOrigin.End);
            return stream;
        }

        public Stream OpenRead(string path)
            => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        public string ReadAllText(string path) => File.ReadAllText(path);
    }

    /// <summary>
    /// Ambient file system used by OmiLAXR.
    /// </summary>
    public static class FileSystem
    {
        private static IFileSystem _current = PhysicalFileSystem.Instance;

        /// <summary>
        /// File system in use. Setting null restores the <see cref="PhysicalFileSystem"/>.
        /// </summary>
        public static IFileSystem Current
        {
            get => _current;
            set => _current = value ?? PhysicalFileSystem.Instance;
        }
    }
}
