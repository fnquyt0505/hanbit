using System.Net;
using System.Text;

using Hanbit.ContentImporter.Krdict;

using Shouldly;

namespace Hanbit.Tools.Tests.Krdict;

public sealed class KrdictHttpSearchSourceTests : IDisposable
{
    private const string ApiKey = "0123456789abcdef0123456789abcdef";
    private readonly DirectoryInfo _cache = Directory.CreateTempSubdirectory("hanbit-krdict-cache-");

    public void Dispose() => _cache.Delete(recursive: true);

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Xml(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "text/xml") };

    /// <summary>Không nghỉ giữa request, thử lại 2 lần không chờ — để test chạy nhanh.</summary>
    private KrdictHttpSearchSource Create(FakeHandler handler) =>
        new(new HttpClient(handler), ApiKey, _cache.FullName, TimeSpan.Zero, [TimeSpan.Zero, TimeSpan.Zero]);

    [Fact]
    public async Task GetStartsWithPageAsync_TransientFailures_RetriesThenSucceeds()
    {
        var calls = 0;
        var handler = new FakeHandler(_ => ++calls switch
        {
            1 => throw new HttpRequestException("A connection attempt failed"),
            2 => Xml("busy", HttpStatusCode.ServiceUnavailable),
            _ => Xml("<channel><total>0</total></channel>"),
        });

        var xml = await Create(handler).GetStartsWithPageAsync('가', "level1", 1, CancellationToken.None);

        xml.ShouldContain("<channel>");
        handler.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public async Task GetStartsWithPageAsync_FailuresExceedRetries_Throws()
    {
        var handler = new FakeHandler(_ => throw new HttpRequestException("A connection attempt failed"));

        await Should.ThrowAsync<HttpRequestException>(() => Create(handler).GetStartsWithPageAsync('가', "level1", 1, CancellationToken.None));

        handler.Requests.Count.ShouldBe(3); // 1 lần đầu + 2 lần thử lại
    }

    [Fact]
    public async Task GetStartsWithPageAsync_SendsExpectedQuery()
    {
        var handler = new FakeHandler(_ => Xml("<channel><total>0</total></channel>"));
        var source = Create(handler);

        await source.GetStartsWithPageAsync('학', "level1", 2, CancellationToken.None);

        var query = handler.Requests.ShouldHaveSingleItem().Query;
        query.ShouldContain("q=%ED%95%99");
        query.ShouldContain("method=start");
        query.ShouldContain("level=level1");
        query.ShouldContain("translated=y");
        query.ShouldContain("trans_lang=7");
        query.ShouldContain("num=100");
        query.ShouldContain("start=2");
    }

    [Fact]
    public async Task GetStartsWithPageAsync_SecondCall_ServedFromCacheWithoutKey()
    {
        var handler = new FakeHandler(_ => Xml("<channel><total>0</total></channel>"));
        var source = Create(handler);

        var first = await source.GetStartsWithPageAsync('가', "level1", 1, CancellationToken.None);
        var second = await source.GetStartsWithPageAsync('가', "level1", 1, CancellationToken.None);

        second.ShouldBe(first);
        handler.Requests.Count.ShouldBe(1);
        var cached = _cache.GetFiles("*.xml", SearchOption.AllDirectories).ShouldHaveSingleItem();
        (await File.ReadAllTextAsync(cached.FullName)).ShouldNotContain(ApiKey);
    }

    [Fact]
    public async Task GetStartsWithPageAsync_ErrorResponse_IsNotCached()
    {
        var handler = new FakeHandler(_ => Xml("<error><error_code>010</error_code><message>Daily API Limit Exceeded</message></error>"));
        var source = Create(handler);

        await source.GetStartsWithPageAsync('가', "level1", 1, CancellationToken.None);
        await source.GetStartsWithPageAsync('가', "level1", 1, CancellationToken.None);

        handler.Requests.Count.ShouldBe(2);
        _cache.GetFiles("*.xml", SearchOption.AllDirectories).ShouldBeEmpty();
    }
}
