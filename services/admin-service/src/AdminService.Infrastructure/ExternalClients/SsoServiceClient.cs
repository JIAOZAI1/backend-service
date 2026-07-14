using System.Net;
using System.Net.Http.Json;
using AdminService.Application.Interfaces;

namespace AdminService.Infrastructure.ExternalClients;

public class SsoServiceClient(HttpClient httpClient) : ISsoServiceClient
{
    private sealed record UserResponse(ulong Id, string Username, string Email, string ReviewStatus);

    private sealed record ApproveReviewRequest(ulong ReviewedBy);

    public async Task<SsoUserInfo?> GetUserAsync(ulong userId, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync($"/internal/users/{userId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<UserResponse>(cancellationToken)
            ?? throw new InvalidOperationException("sso-service returned an empty response body");
        return new SsoUserInfo(body.Id, body.Username, body.Email, body.ReviewStatus);
    }

    public async Task ApproveReviewAsync(ulong userId, ulong reviewedBy, CancellationToken cancellationToken)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"/internal/users/{userId}/review",
            new ApproveReviewRequest(reviewedBy),
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
