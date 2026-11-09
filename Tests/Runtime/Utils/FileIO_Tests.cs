using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using OmiLAXR.Utils;

namespace OmiLAXR.Tests.Utils
{
    public class FileIO_Tests
    {
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "omilaxr_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, recursive: true);
        }

        private string PathOf(string file) => Path.Combine(_folder, file);

        private static string Lines(params string[] lines)
            => string.Join(Environment.NewLine, lines) + Environment.NewLine;

        [Test]
        public void BufferedUtf8Writer_Writes_Lines_As_Utf8()
        {
            // Arrange
            var path = PathOf("lines.txt");

            // Act
            using (var writer = new BufferedUtf8Writer(path))
            {
                writer.WriteLine("Grüße");
                writer.WriteLines(new[] { "a", "b" });
                writer.Flush();
                Assert.AreEqual(Encoding.UTF8.GetByteCount(Lines("Grüße", "a", "b")), writer.GetPosition());
                Assert.AreEqual(path, writer.FilePath);
            }

            // Assert
            Assert.AreEqual(Lines("Grüße", "a", "b"), File.ReadAllText(path, Encoding.UTF8));
        }

        [Test]
        public void BufferedUtf8Writer_SetPosition_Overwrites()
        {
            // Arrange
            var path = PathOf("pos.txt");

            // Act
            using (var writer = new BufferedUtf8Writer(path))
            {
                writer.WriteLine("xxxx");
                writer.SetPosition(0);
                writer.WriteLine("yy");
            }

            // Assert
            StringAssert.StartsWith("yy", File.ReadAllText(path));
        }

        [Test]
        public void BufferedUtf8Writer_Supports_Lines_Larger_Than_Buffer()
        {
            // Arrange
            var path = PathOf("large.txt");
            var line = new string('x', 10_000);

            // Act
            using (var writer = new BufferedUtf8Writer(path))
                writer.WriteLine(line);

            // Assert
            Assert.AreEqual(Lines(line), File.ReadAllText(path));
        }

        [Test]
        public void BufferedUtf8Writer_Reopening_Does_Not_Corrupt_Existing_File()
        {
            // Arrange
            var path = PathOf("reopen.txt");
            using (var writer = new BufferedUtf8Writer(path))
                writer.WriteLines(new[] { "first line", "second line" });

            // Act
            using (var writer = new BufferedUtf8Writer(path))
                writer.WriteLine("third");

            // Assert
            Assert.AreEqual(Lines("first line", "second line", "third"), File.ReadAllText(path));
        }

        [Test]
        public void BufferedUtf8Writer_Dispose_Twice_Is_Safe()
        {
            // Arrange
            var writer = new BufferedUtf8Writer(PathOf("dispose.txt"));

            // Act & Assert
            writer.Dispose();
            Assert.DoesNotThrow(writer.Dispose);
        }

        [Test]
        public void CsvFile_Writes_Headers_And_Rows()
        {
            // Arrange
            var path = PathOf("file.csv");

            // Act
            using (var csv = new CsvFile(path, new[] { "a", "b" }, autoFlush: true))
            {
                csv.WriteRow(1, "x");
                Assert.AreEqual(new[] { "a", "b" }, csv.Headers);
                Assert.IsTrue(csv.AutoFlush);
            }

            // Assert
            Assert.AreEqual(Lines("a,b", "1,x"), File.ReadAllText(path));
        }

        [Test]
        public void FileBufferManager_Reuses_Buffers_And_Writers()
        {
            // Arrange
            var manager = new FileBufferManager();
            var path = PathOf("shared.txt");

            // Act
            var first = manager.EnsureBuffer(1, path, () => "data");
            var again = manager.EnsureBuffer(1, PathOf("ignored.txt"));
            var second = manager.EnsureBuffer(2, path);

            // Assert
            Assert.AreSame(first, again);
            Assert.AreSame(first.Writer, second.Writer);
            Assert.AreEqual("data", first.GetValue<string>());
            Assert.IsNull(second.Data);
            Assert.AreEqual(2, manager.FileBuffers.Count);
            Assert.IsFalse(File.Exists(PathOf("ignored.txt")));

            // Act
            first.SetValue(42);
            first.Writer.WriteLine("line");
            manager.Flush(1);
            manager.FlushAll();
            manager.Dispose();

            // Assert
            Assert.AreEqual(42, first.GetValue<int>());
            Assert.AreEqual(0, manager.FileBuffers.Count);
            Assert.AreEqual(Lines("line"), File.ReadAllText(path));
        }
    }
}
