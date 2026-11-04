using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace RimWorldMcp.Bridge.Core
{
    /// <summary>
    /// Tells the MCP server where the running game listens and which token to send. The port and
    /// token change on every launch, so this file is the only link between the two processes.
    /// </summary>
    public static class DiscoveryFile
    {
        public const int FormatVersion = 1;

        public static string DefaultPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".rimworld-mcp", "bridge.json");

        public static void Write(string path, int port, string token, string bridgeVersion, int processId)
        {
            var json = new JsonWriter();
            json.BeginObject();
            json.Prop("format_version", FormatVersion);
            json.Prop("port", port);
            json.Prop("token", token);
            json.Prop("bridge_version", bridgeVersion);
            json.Prop("process_id", processId);
            json.EndObject();

            string directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            RestrictToOwner(directory, DirectoryMode);

            // The temp file is restricted before the token goes in, so it is never world-readable.
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, "", new UTF8Encoding(false));
            RestrictToOwner(temporary, FileMode);
            File.WriteAllText(temporary, json.ToString(), new UTF8Encoding(false));

            if (File.Exists(path))
            {
                File.Delete(path);
            }
            File.Move(temporary, path);
        }

        public static void Delete(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private const uint DirectoryMode = 0x1C0; // 0700

        private const uint FileMode = 0x180; // 0600

        [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
        private static extern int NativeChmod(string path, uint mode);

        private static void RestrictToOwner(string path, uint mode)
        {
            // On Windows the user profile is already private and libc does not exist.
            if (Path.DirectorySeparatorChar == '\\')
            {
                return;
            }
            if (NativeChmod(path, mode) != 0)
            {
                throw new IOException($"Could not restrict permissions on '{path}' (errno {Marshal.GetLastWin32Error()}).");
            }
        }
    }
}
