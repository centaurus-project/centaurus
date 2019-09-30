using System.ComponentModel;

namespace Centaurus.Models
{
    public enum ResultStatusCodes
    {
        Success = 200,

        BadRequest = 400,
        Unauthorized = 401,
        InvalidState = 450,

        InternalError = 500,

        SnapshotValidationFailed = 550,
        UnexpectedMessage = 551,
    }
}
