using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class StatusCodeExtensions
    {
        public static string GetDescription(this ResultStatusCodes status)
        {
            switch (status)
            {
                case ResultStatusCodes.Success: return "Success";
                case ResultStatusCodes.Unauthorized: return "Unauthorized";
                case ResultStatusCodes.InternalError: return "InternalError";
            }
            throw new NotImplementedException($"Description for status code {status.ToString()} was not specified.");
        }
    }
}
