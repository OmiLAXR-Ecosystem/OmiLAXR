using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OmiLAXR.Utils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OmiLAXR.Tests.Mocks
{
    /// <summary>
    /// Clock with a fixed, manually advanced time.
    /// </summary>
    public sealed class FixedClock : IClock
    {
        public DateTime Now { get; set; }
        public DateTime UtcNow => Now.ToUniversalTime();

        public FixedClock(DateTime now)
        {
            Now = now;
        }

        public void Advance(TimeSpan delta) => Now += delta;
    }

    /// <summary>
    /// Records everything written through <see cref="DebugLog"/>.
    /// </summary>
    public sealed class CapturingLogWriter : ILogWriter
    {
        public readonly List<(LogType Type, string Message)> Entries = new List<(LogType, string)>();

        public void Write(LogType type, string message, Object context)
        {
            lock (Entries)
                Entries.Add((type, message));
        }

        public bool Contains(LogType type, string part)
        {
            lock (Entries)
                return Entries.Any(e => e.Type == type && e.Message.Contains(part));
        }
    }

    /// <summary>
    /// File system kept in memory; content becomes visible on Flush/Dispose of a write stream.
    /// </summary>
    public sealed class InMemoryFileSystem : IFileSystem
    {
        private readonly Dictionary<string, byte[]> _files = new Dictionary<string, byte[]>();
        private readonly HashSet<string> _directories = new HashSet<string>();

        public IEnumerable<string> Files
        {
            get { lock (_files) return _files.Keys.ToArray(); }
        }

        public string ReadAllText(string path)
        {
            lock (_files)
                return Encoding.UTF8.GetString(_files[path]);
        }

        public bool FileExists(string path)
        {
            lock (_files) return _files.ContainsKey(path);
        }

        public bool DirectoryExists(string path)
        {
            lock (_files) return _directories.Contains(path);
        }

        public void CreateDirectory(string path)
        {
            lock (_files) _directories.Add(path);
        }

        public void DeleteFile(string path)
        {
            lock (_files) _files.Remove(path);
        }

        public void WriteAllText(string path, string text)
        {
            lock (_files) _files[path] = Encoding.UTF8.GetBytes(text);
        }

        public Stream OpenWrite(string path, bool append)
        {
            byte[] existing;
            lock (_files)
                existing = append && _files.TryGetValue(path, out var bytes) ? bytes : new byte[0];
            var stream = new CommittingStream(this, path);
            stream.Write(existing, 0, existing.Length);
            stream.Flush();
            return stream;
        }

        public Stream OpenRead(string path)
        {
            lock (_files)
                return new MemoryStream(_files[path], writable: false);
        }

        private void Commit(string path, byte[] content)
        {
            lock (_files) _files[path] = content;
        }

        private sealed class CommittingStream : MemoryStream
        {
            private readonly InMemoryFileSystem _owner;
            private readonly string _path;

            public CommittingStream(InMemoryFileSystem owner, string path)
            {
                _owner = owner;
                _path = path;
            }

            public override void Flush() => _owner.Commit(_path, ToArray());

            protected override void Dispose(bool disposing)
            {
                if (disposing && CanRead)
                    Flush();
                base.Dispose(disposing);
            }
        }
    }
}
