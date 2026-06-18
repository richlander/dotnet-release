using System.Net;
using Dotnet.Release.ChangesHandler;
using Xunit;

namespace Dotnet.Release.ChangesHandler.Tests;

public class ChangeCollectorTests
{
    [Fact]
    public async Task GetMergedPullRequestsAsync_UsesCommitPullsFallbackWhenSubjectHasNoPrNumber()
    {
        var handler = new StubGitHubHandler
        {
            ["https://api.github.com/repos/dotnet/runtime/compare/base...head?per_page=100&page=1"] = """
                {
                  "commits": [
                    {
                      "sha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                      "commit": {
                        "message": "Add a fluent Validate overload (#123)"
                      }
                    },
                    {
                      "sha": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                      "commit": {
                        "message": "Implement X25519DiffieHellman"
                      }
                    }
                  ]
                }
                """,
            ["https://api.github.com/repos/dotnet/runtime/commits/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb/pulls"] = """
                [
                  {
                    "number": 456,
                    "title": "Implement X25519DiffieHellman",
                    "html_url": "https://github.com/dotnet/runtime/pull/456",
                    "merge_commit_sha": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                  }
                ]
                """
        };
        using var httpClient = new HttpClient(handler);
        var collector = new ChangeCollector(httpClient);

        var result = await collector.GetMergedPullRequestsAsync("dotnet", "runtime", "base", "head");

        Assert.Equal(["aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"], result.CommitShas);
        Assert.Collection(
            result.PullRequests,
            pr =>
            {
                Assert.Equal(123, pr.Number);
                Assert.Equal("Add a fluent Validate overload", pr.Title);
                Assert.Equal("https://github.com/dotnet/runtime/pull/123", pr.Url);
                Assert.Equal("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", pr.MergeCommitSha);
            },
            pr =>
            {
                Assert.Equal(456, pr.Number);
                Assert.Equal("Implement X25519DiffieHellman", pr.Title);
                Assert.Equal("https://github.com/dotnet/runtime/pull/456", pr.Url);
                Assert.Equal("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", pr.MergeCommitSha);
            });
        Assert.DoesNotContain(
            "https://api.github.com/repos/dotnet/runtime/commits/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/pulls",
            handler.RequestedUrls);
        Assert.Contains(
            "https://api.github.com/repos/dotnet/runtime/commits/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb/pulls",
            handler.RequestedUrls);
    }

    private sealed class StubGitHubHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _responses = new(StringComparer.Ordinal);

        public List<string> RequestedUrls { get; } = [];

        public string this[string url]
        {
            set => _responses[url] = value;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? "";
            RequestedUrls.Add(url);

            if (!_responses.TryGetValue(url, out var response))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("{}")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response)
            });
        }
    }
}
