namespace AdminService.Application.Exceptions;

public class SettingNotFoundException(string key) : Exception($"system setting '{key}' not found");
