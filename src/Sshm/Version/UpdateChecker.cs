using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Sshm.Version;

public sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; set; }

    [JsonPropertyName("draft")]
    public bool Draft { get; set; }
}

public sealed class UpdateInfo
{
    public bool Available { get; set; }

    public string CurrentVer { get; set; } = string.Empty;

    public string LatestVer { get; set; } = string.Empty;

    public string ReleaseUrl { get; set; } = string.Empty;

    public string ReleaseName { get; set; } = string.Empty;
}

public static class UpdateChecker
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    public static async Task<UpdateInfo> CheckForUpdatesAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        if (currentVersion == "dev")
        {
            return new UpdateInfo
            {
                Available = false,
                CurrentVer = currentVersion,
            };
        }

        using HttpRequestMessage request = new(HttpMethod.Get, "https://api.github.com/repos/emako/sshm/releases/latest");
        request.Headers.TryAddWithoutValidation("User-Agent", "sshm/" + currentVersion);

        using HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        GitHubRelease? release = await response.Content.ReadFromJsonAsync(SshmJsonContext.Default.GitHubRelease, cancellationToken).ConfigureAwait(false);
        if (release == null || release.Prerelease || release.Draft)
        {
            return new UpdateInfo
            {
                Available = false,
                CurrentVer = currentVersion,
            };
        }

        bool updateAvailable = CompareVersions(currentVersion, release.TagName) < 0;
        return new UpdateInfo
        {
            Available = updateAvailable,
            CurrentVer = currentVersion,
            LatestVer = release.TagName,
            ReleaseUrl = release.HtmlUrl,
            ReleaseName = release.Name,
        };
    }

    public static int CompareVersions(string v1, string v2)
    {
        int[] nums1 = ParseVersion(v1);
        int[] nums2 = ParseVersion(v2);
        int maxLen = Math.Max(nums1.Length, nums2.Length);

        for (int i = 0; i < maxLen; i++)
        {
            int a = i < nums1.Length ? nums1[i] : 0;
            int b = i < nums2.Length ? nums2[i] : 0;
            if (a < b)
            {
                return -1;
            }

            if (a > b)
            {
                return 1;
            }
        }

        return 0;
    }

    private static int[] ParseVersion(string version)
    {
        version = version.TrimStart('v');
        string[] parts = version.Split('.');
        int[] nums = new int[parts.Length];

        for (int i = 0; i < parts.Length; i++)
        {
            string numPart = parts[i].Split('-', '+', '_')[0];
            if (int.TryParse(numPart, out int num))
            {
                nums[i] = num;
            }
        }

        return nums;
    }
}
