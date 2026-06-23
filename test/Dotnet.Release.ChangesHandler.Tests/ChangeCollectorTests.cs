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

    [Fact]
    public async Task GetMergedPullRequestsAsync_UsesTrailingPrNumberWhenSubjectHasMultiple()
    {
        // GitHub squash subjects can carry an inline issue reference plus the trailing
        // PR number, e.g. "Add FULL OUTER JOIN support (#37633) (#38340)" where #37633 is
        // the closed issue and #38340 is the actual PR. The trailing (#N) must win, and
        // the cleaned title must keep the inline reference.
        var handler = new StubGitHubHandler
        {
            ["https://api.github.com/repos/dotnet/efcore/compare/base...head?per_page=100&page=1"] = """
                {
                  "commits": [
                    {
                      "sha": "cccccccccccccccccccccccccccccccccccccccc",
                      "commit": {
                        "message": "Add FULL OUTER JOIN support (#37633) (#38340)\n\nCloses #37633"
                      }
                    }
                  ]
                }
                """
        };
        using var httpClient = new HttpClient(handler);
        var collector = new ChangeCollector(httpClient);

        var result = await collector.GetMergedPullRequestsAsync("dotnet", "efcore", "base", "head");

        Assert.Collection(
            result.PullRequests,
            pr =>
            {
                Assert.Equal(38340, pr.Number);
                Assert.Equal("Add FULL OUTER JOIN support (#37633)", pr.Title);
                Assert.Equal("https://github.com/dotnet/efcore/pull/38340", pr.Url);
                Assert.Equal("cccccccccccccccccccccccccccccccccccccccc", pr.MergeCommitSha);
            });
        // The subject already yields a PR number, so no commits-to-PRs fallback call is made.
        Assert.DoesNotContain(
            "https://api.github.com/repos/dotnet/efcore/commits/cccccccccccccccccccccccccccccccccccccccc/pulls",
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
