using System.Net;
using System.Text.Json;
using AdminService.Infrastructure.ExternalClients;
using Shouldly;

namespace AdminService.UnitTests;

public class JobServiceClientTests
{
    [Fact]
    public async Task CreateTenantProvisioningJobAsync_SchedulesOneTimeJobThirtySecondsInFuture()
    {
        var handler = new CapturingHandler();
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://backend-job-service.test"),
        };
        var sut = new JobServiceClient(client);

        var before = DateTime.UtcNow;
        await sut.CreateTenantProvisioningJobAsync(
            databaseInstanceId: 9,
            dbName: "tenant_abcd1234wxyz",
            dbUsername: "tenant_abcd1234wxyz",
            dbPassword: "secret",
            userId: 42,
            reviewedBy: 1,
            tenantId: Guid.NewGuid().ToString(),
            CancellationToken.None);
        var after = DateTime.UtcNow;

        handler.RequestBodies.ShouldNotBeEmpty();
        using var body = JsonDocument.Parse(handler.RequestBodies[0]);
        var runAt = body.RootElement.GetProperty("runAt").GetDateTime();

        runAt.ShouldBeGreaterThanOrEqualTo(before.AddSeconds(30).AddSeconds(-1));
        runAt.ShouldBeLessThanOrEqualTo(after.AddSeconds(30).AddSeconds(1));
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                RequestBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }

            if (request.RequestUri?.AbsolutePath == "/backend-job-service/api/v1/jobs")
            {
                return JsonResponse("""{"id":123}""", HttpStatusCode.Created);
            }

            return JsonResponse("""{"id":456,"jobId":123,"name":"task","order":1,"handlerType":"handler","pluginAssembly":"plugin","timeoutSeconds":60,"maxRetryCount":2}""", HttpStatusCode.Created);
        }

        private static HttpResponseMessage JsonResponse(string json, HttpStatusCode statusCode) => new(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };
    }
}
