using Centaurus.Domain;
using Centaurus.Models;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using stellar_dotnet_sdk;
using System.Linq;
using System.Runtime;
using Centaurus.DAL.Mongo;

namespace Centaurus.Test
{
    [TestFixture]
    public class SnapshotPerformanceTests
    {
        private ExecutionContext context;

        [SetUp]
        public void Setup()
        {
            EnvironmentHelper.SetTestEnvironmentVariable();
            var settings = new Settings 
            { 
                CWD = "AppData"
            };
            var stellarProvider = new MockStellarDataProvider(settings.NetworkPassphrase, settings.HorizonUrl);
            context = new ExecutionContext(settings, new MongoStorage(), stellarProvider);

            context.Init().Wait();
        }

        [TearDown]
        public void TearDown()
        {
            context.Exchange.Clear();
            context.Dispose();
        }
    }
}
