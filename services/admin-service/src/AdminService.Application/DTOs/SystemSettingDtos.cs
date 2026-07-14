namespace AdminService.Application.DTOs;

public record SystemSettingResponse(long Id, string Key, string Value, string Description, DateTime UpdatedAt);

public record UpsertSystemSettingRequest(string Value, string Description);
