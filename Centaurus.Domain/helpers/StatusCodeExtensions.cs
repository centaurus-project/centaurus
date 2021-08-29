using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public static class StatusCodeExtensions
    {
        public static string GetDescription(this ResultStatusCode status)
        {
            switch (status)
            {
                case ResultStatusCode.Success: return "Success";
                case ResultStatusCode.Unauthorized: return "Unauthorized";
                case ResultStatusCode.InternalError: return "InternalError";
            }
            throw new NotImplementedException($"Description for status code {status.ToString()} was not specified.");
        }
    }
}
