namespace ProjectManagement.Application.Common;

public class ApiResponse<T>
{
    public int Code { get; set; }
    public string Message { get; set; } = "success";
    public T? Data { get; set; }

    public static ApiResponse<T> Ok(T data) => new() { Code = 0, Data = data };
    public static ApiResponse<T> Fail(string message) => new() { Code = -1, Message = message };
}
