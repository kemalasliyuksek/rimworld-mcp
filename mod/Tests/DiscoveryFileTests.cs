using System;
using System.IO;
using System.Text.Json;
using RimWorldMcp.Bridge.Core;
using Xunit;

namespace RimWorldMcp.Bridge.Tests
{
    public sealed class DiscoveryFileTests : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "rimworld-mcp-test-" + Guid.NewGuid().ToString("N"));

        private string FilePath => Path.Combine(directory, "nested", "bridge.json");

        [Fact]
        public void WritesTheFieldsTheServerExpects()
        {
            DiscoveryFile.Write(FilePath, 1234, "tok", "0.1.0", 99);

            JsonElement root = JsonDocument.Parse(File.ReadAllText(FilePath)).RootElement;
            Assert.Equal(DiscoveryFile.FormatVersion, root.GetProperty("format_version").GetInt32());
            Assert.Equal(1234, root.GetProperty("port").GetInt32());
            Assert.Equal("tok", root.GetProperty("token").GetString());
            Assert.Equal("0.1.0", root.GetProperty("bridge_version").GetString());
            Assert.Equal(99, root.GetProperty("process_id").GetInt32());
        }

        [Fact]
        public void ReplacesAFileLeftBehindByACrash()
        {
            DiscoveryFile.Write(FilePath, 1, "old", "0.1.0", 1);
            DiscoveryFile.Write(FilePath, 2, "new", "0.1.0", 2);

            Assert.Contains("new", File.ReadAllText(FilePath));
            Assert.False(File.Exists(FilePath + ".tmp"));
        }

        [Fact]
        public void TheTokenIsOnlyReadableByItsOwner()
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }

            DiscoveryFile.Write(FilePath, 1, "tok", "0.1.0", 1);

            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(FilePath));
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                File.GetUnixFileMode(Path.GetDirectoryName(FilePath)));
        }

        [Fact]
        public void DeletingAMissingFileIsNotAnError()
        {
            DiscoveryFile.Delete(FilePath);
        }

        public void Dispose()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
