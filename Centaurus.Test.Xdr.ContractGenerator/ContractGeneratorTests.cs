using Centaurus.ContractGenerator;
using Centaurus.Models;
using Centaurus.Test.Contracts;
using Centaurus.Xdr;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Centaurus.Test
{
    public class ContractGeneratorTests
    {
        [Test]
        public void JSContractGeneratorTest()
        {
            var refType = typeof(Effect);
            var contracts = XdrSerializationTypeMapper.DiscoverXdrContracts(refType.Assembly);
            var generator = new JavaScriptContractGenerator();
            generator.LoadContracts(contracts);
            var files = generator.Generate();
            //TODO: prepare reference contracts and run validations checks once the generator is stable.
        }
    }
}