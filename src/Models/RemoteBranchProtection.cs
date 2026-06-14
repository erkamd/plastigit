using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.Models
{
    public enum RemoteBranchProtectionStatus
    {
        Unknown,
        Protected,
        Unprotected,
    }

    public static class RemoteBranchProtection
    {
        public static async Task<RemoteBranchProtectionStatus> CheckAsync(Remote remote, string branch)
        {
            if (remote == null ||
                string.IsNullOrWhiteSpace(branch) ||
                !remote.TryGetVisitURL(out var visitURL) ||
                !Uri.TryCreate(visitURL, UriKind.Absolute, out var uri))
                return RemoteBranchProtectionStatus.Unknown;

            using var cancellation = new CancellationTokenSource(RequestTimeout);
            using var client = new HttpClient();
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SourceGit/branch-protection-check");

            try
            {
                if (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
                    return await CheckGitHubAsync(client, uri, branch, cancellation.Token).ConfigureAwait(false);

                if (uri.Host.Contains("gitlab", StringComparison.OrdinalIgnoreCase))
                    return await CheckGitLabAsync(client, uri, branch, cancellation.Token).ConfigureAwait(false);
            }
            catch
            {
                return RemoteBranchProtectionStatus.Unknown;
            }

            return RemoteBranchProtectionStatus.Unknown;
        }

        private static async Task<RemoteBranchProtectionStatus> CheckGitHubAsync(
            HttpClient client,
            Uri repository,
            string branch,
            CancellationToken cancellation)
        {
            var parts = repository.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                return RemoteBranchProtectionStatus.Unknown;

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.github.com/repos/{Uri.EscapeDataString(parts[0])}/{Uri.EscapeDataString(parts[1])}/branches/{Uri.EscapeDataString(branch)}");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            var token = Environment.GetEnvironmentVariable("GH_TOKEN") ??
                Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            return await ReadProtectionStatusAsync(client, request, cancellation).ConfigureAwait(false);
        }

        private static async Task<RemoteBranchProtectionStatus> CheckGitLabAsync(
            HttpClient client,
            Uri repository,
            string branch,
            CancellationToken cancellation)
        {
            var project = repository.AbsolutePath.Trim('/');
            if (string.IsNullOrWhiteSpace(project))
                return RemoteBranchProtectionStatus.Unknown;

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{repository.Scheme}://{repository.Authority}/api/v4/projects/{Uri.EscapeDataString(project)}/repository/branches/{Uri.EscapeDataString(branch)}");

            var token = Environment.GetEnvironmentVariable("GITLAB_TOKEN");
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.TryAddWithoutValidation("PRIVATE-TOKEN", token);

            return await ReadProtectionStatusAsync(client, request, cancellation).ConfigureAwait(false);
        }

        private static async Task<RemoteBranchProtectionStatus> ReadProtectionStatusAsync(
            HttpClient client,
            HttpRequestMessage request,
            CancellationToken cancellation)
        {
            using var response = await client.SendAsync(request, cancellation).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return RemoteBranchProtectionStatus.Unknown;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellation).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellation).ConfigureAwait(false);
            if (!document.RootElement.TryGetProperty("protected", out var property) ||
                property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                return RemoteBranchProtectionStatus.Unknown;

            return property.GetBoolean()
                ? RemoteBranchProtectionStatus.Protected
                : RemoteBranchProtectionStatus.Unprotected;
        }

        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(3);
    }
}
