using NUnit.Framework;

namespace Centaurus.Test
{
    public class AuditorQuantumHandlerTests : BaseQuantumHandlerTests
    {
        [SetUp]
        public void Setup()
        {
            EnvironmentHelper.SetTestEnvironmentVariable();
            context = GlobalInitHelper.DefaultAuditorSetup().Result;
        }

        static object[] AccountRequestRateLimitsCases =
        {
            new object[] { TestEnvironment.Client1KeyPair, null },
            new object[] { TestEnvironment.Client2KeyPair, 10 }
        };
    }
}
