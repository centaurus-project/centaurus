using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus
{
    public static class EnvironmentHelper
    {
        public static string IsTestEnvVarName => "IsTest";

        public static void SetTestEnvironmentVariable(bool value = true)
        {
            Environment.SetEnvironmentVariable(EnvironmentHelper.IsTestEnvVarName, value.ToString());
        }

        public static bool IsTest => bool.TryParse(Environment.GetEnvironmentVariable(IsTestEnvVarName), out bool isTest) && isTest;
    }
}
