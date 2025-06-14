using System.Net;

namespace FlippingExilesPublicStashAPI.Oauth;

public class ApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string ErrorResponse { get; }

    public ApiException(HttpStatusCode statusCode, string errorResponse)
        : base($"API request failed. Status Code: {statusCode}. Error Response: {errorResponse}")
    {
        StatusCode = statusCode;
        ErrorResponse = errorResponse;
    }
}