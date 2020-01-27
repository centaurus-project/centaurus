using Centaurus.ContractGenerator;
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
            //var generator = new JavaScriptContractGenerator();
            var generator = new CSharpContractGenerator();
            generator.LoadContracts(XdrSerializationTypeMapper.DiscoverXdrContracts(typeof(PrimitiveTypes).Assembly));
            var files = generator.Generate();
            //var contract = files.First(f => f.FileName == "primitive-types-converter.js").Contents;
        }
    }
}