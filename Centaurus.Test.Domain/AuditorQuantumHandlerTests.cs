using Centaurus.Domain;
using Centaurus.Models;
using NUnit.Framework;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Test
{
    public class AuditorQuantumHandlerTests: BaseQuantumHandlerTests
    {
        [SetUp]
        public void Setup()
        {
            EnvironmentHelper.SetTestEnvironmentVariable();
            GlobalInitHelper.DefaultAuditorSetup();
        }

        static object[] AccountRequestRateLimitsCases =
        {
            new object[] { TestEnvironment.Client1KeyPair, null },
            new object[] { TestEnvironment.Client2KeyPair, 10 }
        };
    }
}
