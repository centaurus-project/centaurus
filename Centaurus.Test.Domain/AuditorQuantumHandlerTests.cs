using NUnit.Framework;

namespace Centaurus.Test
{
    public class AuditorQuantumHandlerTests : BaseQuantumHandlerTests
    {
        static object[] AccountRequestRateLimitsCases =
        {
            new object[] { TestEnvironment.Client1KeyPair, null },
            new object[] { TestEnvironment.Client2KeyPair, 10 }
        };
    }
}
