namespace Centaurus
{
    public enum ResultStatusCodes
    {
        Success = 200,

        BadRequest = 400,
        Unauthorized = 401,
        Forbidden = 403,
        PayloadTooLarge = 413,
        TooManyRequests = 429,
        InvalidState = 450,

        InternalError = 500,

        SnapshotValidationFailed = 550,
        UnexpectedMessage = 551
    }
}