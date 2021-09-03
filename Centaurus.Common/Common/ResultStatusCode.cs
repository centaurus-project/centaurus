namespace Centaurus
{
    public enum ResultStatusCode
    {
        Success = 200,

        BadRequest = 400,
        Unauthorized = 401,
        Forbidden = 403,
        PayloadTooLarge = 413,
        TooManyRequests = 429,
        InvalidState = 450,

        InternalError = 500,

        UnexpectedMessage = 551
    }
}