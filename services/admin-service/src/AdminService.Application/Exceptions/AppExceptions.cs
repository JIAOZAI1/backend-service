namespace AdminService.Application.Exceptions;

public class NotFoundException(string message) : Exception(message);

public class ValidationException(string message) : Exception(message);

/// <summary>
/// 审核编排流程中某一步失败时抛出，携带失败的步骤名，供 controller 返回给调用方，
/// 便于管理员判断失败在哪一步后用同一个接口重试（编排不做自动回滚，见 ReviewService）。
/// </summary>
public class ReviewStepFailedException(string step, string message)
    : Exception(message)
{
    public string Step { get; } = step;
}
