using System.Net;
using System.Net.Http.Json;
using System.Web;
using AdminService.Application.Common;
using AdminService.Application.DTOs;
using AdminService.Application.Exceptions;
using AdminService.Application.Interfaces;

namespace AdminService.Infrastructure.ExternalClients;

public class SsoServiceClient(HttpClient httpClient) : ISsoServiceClient
{
    private sealed record UserResponse(ulong Id, string Username, string Email, string ReviewStatus);

    private sealed record ApproveReviewRequest(ulong ReviewedBy);

    private sealed record RejectReviewRequest(ulong ReviewedBy);

    private sealed record PagedUserResponse(List<UserResponse> Items, int Page, int PageSize, long Total);

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

    public async Task<bool> RejectReviewAsync(ulong userId, ulong reviewedBy, CancellationToken cancellationToken)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"/internal/users/{userId}/reject",
            new RejectReviewRequest(reviewedBy),
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<PagedResult<SsoUserInfo>> ListUsersAsync(
        string? reviewStatus, int page, int pageSize, string? sortBy, SortOrder sortOrder, CancellationToken cancellationToken)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        if (!string.IsNullOrWhiteSpace(reviewStatus))
        {
            query["reviewStatus"] = reviewStatus;
        }
        query["page"] = page.ToString();
        query["pageSize"] = pageSize.ToString();
        if (!string.IsNullOrWhiteSpace(sortBy))
        {
            query["sortBy"] = sortBy;
        }
        query["sortOrder"] = sortOrder.ToString();

        var response = await httpClient.GetAsync($"/internal/users?{query}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ValidationException($"sso-service rejected list query: {errorBody}");
        }
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<PagedUserResponse>(cancellationToken)
            ?? throw new InvalidOperationException("sso-service returned an empty response body");

        return new PagedResult<SsoUserInfo>
        {
            Items = body.Items.Select(u => new SsoUserInfo(u.Id, u.Username, u.Email, u.ReviewStatus)).ToList(),
            Page = body.Page,
            PageSize = body.PageSize,
            Total = body.Total,
        };
    }
}
