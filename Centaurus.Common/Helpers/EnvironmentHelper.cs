using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus
{
    public static class EnvironmentHelper
    {
        public const string IsTestEnvVarName = "IsTest";

        public static void SetTestEnvironmentVariable(bool value = true)
        {
            Environment.SetEnvironmentVariable(IsTestEnvVarName, value.ToString());
        }

        public static bool IsTest => bool.TryParse(Environment.GetEnvironmentVariable(IsTestEnvVarName), out bool isTest) && isTest;
    }
}
