using System.Text.Json.Serialization;
using Sshm.Config;
using Sshm.History;
using Sshm.Version;

namespace Sshm;

[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(KeyBindings))]
[JsonSerializable(typeof(ConnectionHistory))]
[JsonSerializable(typeof(ConnectionInfo))]
[JsonSerializable(typeof(PortForwardConfig))]
[JsonSerializable(typeof(Dictionary<string, ConnectionInfo>))]
[JsonSerializable(typeof(GitHubRelease))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class SshmJsonContext : JsonSerializerContext;
