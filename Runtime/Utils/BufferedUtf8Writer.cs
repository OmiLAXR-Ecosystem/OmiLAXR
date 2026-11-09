/*
* SPDX-License-Identifier: AGPL-3.0-or-later
* Copyright (C) 2025 Sergej Görzen <sergej.goerzen@gmail.com>
* This file is part of OmiLAXR.
*/
using System;
using System.IO;
using System.Text;
using System.Threading; // Für Thread.Sleep im Retry-Block

namespace OmiLAXR.Utils
{
    /// <summary>
    /// High-performance UTF-8 file writer with internal buffering for efficient text output.
    /// Provides thread-safe writing operations with automatic UTF-8 encoding.
    /// Optimized for scenarios with frequent small writes like logging and analytics data.
    /// </summary>
    public class BufferedUtf8Writer : IDisposable
    {
        /// <summary>
        /// Buffered stream wrapper providing write buffering for improved performance.
        /// Reduces the number of actual disk I/O operations.
        /// </summary>
        private readonly BufferedStream _bufferedStream;
        
        /// <summary>
        /// Underlying file stream for direct file system access.
        /// Managed by the BufferedStream for actual I/O operations.
        /// </summary>
        private readonly Stream _fileStream;
        
        /// <summary>
        /// Reusable byte buffer for UTF-8 encoding operations.
        /// Prevents repeated memory allocations during string encoding.
        /// </summary>
        private readonly byte[] _writeBuffer;
        
        /// <summary>
        /// File path this writer is associated with.
        /// Stored for reference and debugging purposes.
        /// </summary>
        public string FilePath { get; private set; }

        /// <summary>
        /// Thread synchronization object for ensuring thread-safe operations.
        /// All public methods acquire this lock before performing I/O operations.
        /// </summary>
        private readonly object _lock = new object();
        
        private bool _isDisposed;
        
        /// <summary>
        /// Initializes a new BufferedUtf8Writer for the specified file path.
        /// Creates or opens the file and sets up buffering for optimal performance.
        /// </summary>
        /// <param name="path">File path to write to</param>
        /// <param name="bufferSize">Size of the internal buffer in bytes (default: 8192)</param>
        public BufferedUtf8Writer(string path, int bufferSize = 8192)
            : this(OpenWithRetry(path), path, bufferSize)
        {
        }

        /// <summary>
        /// Writes to an already opened stream, e.g. one provided by an <see cref="IFileSystem"/> or a test.
        /// The writer takes ownership and disposes the stream.
        /// </summary>
        public BufferedUtf8Writer(Stream stream, string path, int bufferSize = 8192)
        {
            _fileStream = stream ?? throw new ArgumentNullException(nameof(stream));
            _bufferedStream = new BufferedStream(_fileStream, bufferSize);
            _writeBuffer = new byte[bufferSize];
            FilePath = path;
            _isDisposed = false;
        }

        /// <summary>
        /// Opens a FileStream with retry handling for sharing violations.
        /// </summary>
        private static Stream OpenWithRetry(string path)
        {
            const int maxAttempts = 5;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    // Append to existing content instead of overwriting the beginning of the file
                    return FileSystem.Current.OpenWrite(path, append: true);
                }
                catch (IOException ex) when (IsSharingViolation(ex) && attempt < maxAttempts)
                {
                    Thread.Sleep(30 * attempt); // exponential backoff
                }
            }

            // Let the final attempt throw if still failing
            return FileSystem.Current.OpenWrite(path, append: true);
        }

        private static bool IsSharingViolation(IOException ex)
        {
            const int ERROR_SHARING_VIOLATION = 32;
            return (ex.HResult & 0xFFFF) == ERROR_SHARING_VIOLATION;
        }

        public void WriteLine(string line)
        {
            lock (_lock)
            {
                // Append platform-specific newline
                var full = line + Environment.NewLine;
                
                // Encode to UTF-8 using reusable buffer to avoid allocations
                // Lines that don't fit into the reusable buffer get a temporary one
                var buffer = _writeBuffer;
                if (Encoding.UTF8.GetMaxByteCount(full.Length) > buffer.Length)
                {
                    var requiredBytes = Encoding.UTF8.GetByteCount(full);
                    if (requiredBytes > buffer.Length)
                        buffer = new byte[requiredBytes];
                }
                var byteCount = Encoding.UTF8.GetBytes(full, 0, full.Length, buffer, 0);

                // Write encoded bytes to buffered stream
                _bufferedStream.Write(buffer, 0, byteCount);
            }
        }

        /// <summary>
        /// Writes multiple lines of text efficiently in a single thread-safe operation.
        /// More efficient than multiple WriteLine calls for bulk operations.
        /// </summary>
        /// <param name="lines">Array of text lines to write</param>
        public void WriteLines(string[] lines)
        {
            lock (_lock)
            {
                // Write each line within the same lock for better performance
                foreach (var line in lines)
                    WriteLine(line);
            }
        }

        /// <summary>
        /// Forces all buffered data to be written to the underlying file system.
        /// Thread-safe operation that ensures data persistence.
        /// </summary>
        public void Flush()
        {
            lock (_lock)
            {
                _bufferedStream.Flush();
            }
        }
        
        /// <summary>
        /// Gets the current position in the buffered stream.
        /// Thread-safe access to the stream position for debugging or state tracking.
        /// </summary>
        /// <returns>Current byte position in the stream</returns>
        public int GetPosition()
        {
            lock (_lock)
            {
                return (int)_bufferedStream.Position;
            }
        }

        /// <summary>
        /// Sets the position in the buffered stream for random access scenarios.
        /// Thread-safe positioning operation with bounds checking by the underlying stream.
        /// </summary>
        /// <param name="position">Byte position to seek to</param>
        public void SetPosition(int position)
        {
            lock (_lock)
            {
                _bufferedStream.Position = position;
            }
        }

        /// <summary>
        /// Disposes all resources including streams and buffers.
        /// Ensures final flush before disposal to prevent data loss.
        /// Thread-safe cleanup operation.
        /// </summary>
        public void Dispose()
        {
            lock (_lock)
            {
                if (!_isDisposed)
                {
                    // Ensure all data is written before disposing
                    _bufferedStream.Flush();
                    _bufferedStream.Dispose();
                    _isDisposed = true;
                }
                _fileStream.Dispose();
            }
        }
    }
}
