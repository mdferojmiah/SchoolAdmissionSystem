namespace SchoolAdmission.Application.Exceptions;

public class AppException : Exception
{
    public AppException(int statusCode, string code, string message) : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public int StatusCode { get; }

    public string Code { get; }
}
