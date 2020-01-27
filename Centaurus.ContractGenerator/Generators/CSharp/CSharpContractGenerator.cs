using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.ContractGenerator
{
    public class CSharpContractGenerator : ContractGenerator
    {
        public CSharpContractGenerator()
        {
            MapType(new PrimitiveTypeDescriptor(typeof(bool), "Boolean", "Boolean"));
            MapType(new PrimitiveTypeDescriptor(typeof(byte), "Number", "Int32"));
            MapType(new PrimitiveTypeDescriptor(typeof(int), "Number", "Int32"));
            MapType(new PrimitiveTypeDescriptor(typeof(uint), "Number", "UInt32"));
            MapType(new PrimitiveTypeDescriptor(typeof(long), "Int64", "Int64"));
            MapType(new PrimitiveTypeDescriptor(typeof(ulong), "Int64", "UInt64"));
            MapType(new PrimitiveTypeDescriptor(typeof(float), "Number", "Float"));
            MapType(new PrimitiveTypeDescriptor(typeof(double), "Number", "Double"));
            MapType(new PrimitiveTypeDescriptor(typeof(byte[]), "Buffer", "Variable"));
            MapType(new PrimitiveTypeDescriptor(typeof(string), "String", "String"));
            MapType(new PrimitiveTypeDescriptor(typeof(List<>), "Array", "Array"));
            MapType(new PrimitiveTypeDescriptor(typeof(object), "Object", "Object"));
        }

        public override GeneratedContractsBundle Generate()
        {
            var bundle = new GeneratedContractsBundle();
            foreach (var contractDescriptor in ContractsMap.Values)
            {
                var contractName = contractDescriptor.XdrContractType.Name;
                bundle.Add(new GeneratedContractFile(contractName+"Serializer.cs", GenerateSerializerFile(contractDescriptor, contractName)));
            }
            return bundle;
        }

        private string GenerateSerializerFile(XdrContractDescriptor contractDescriptor, string contractName)
        {
            var builder = new StringBuilder();
            //write header
            var converClassName = contractName + "Serializer";
            builder.Append($@"using System;
using Centaurus.Xdr;

namespace Centaurus.Models
{{
/**
* Converter for {contractName} XDR data contract.
*/
class {converClassName} : IXdrRuntimeContractSerializer
{{");
            //write union info
            if (contractDescriptor.UnionSwitch.Count > 0)
            {
                builder.Append(@"
    private bool IsUnion = true;
    private unionSwitch = {");
                foreach (var kv in contractDescriptor.UnionSwitch)
                {
                    builder.Append($@"
         '{kv.Key}': {kv.Value.ToString()},");
                }
                builder.Append(@"
    }");
            }
            if (contractDescriptor.AncestorUnionsCounts > 0)
            {
                builder.Append($@"
    private int AncestorUnionsCounts = {contractDescriptor.AncestorUnionsCounts};");
            }
            //write serialize method
            builder.Append($@"
    Serialize(object value, XdrWriter writer) {{");
            //write union discriminators serialization if any
            foreach (var discriminator in contractDescriptor.UnionVector)
            {
                builder.Append($@"
        writer.write{GetMethodPrimitiveTypeName(typeof(int))}({discriminator})");
            }
            //write property serialization instructions
            foreach (var prop in contractDescriptor.Properties)
            {
                var propTypeDescriptor = GetTypeDescriptor(prop);
                var typeArg = propTypeDescriptor.Subtype != null ? $", '{propTypeDescriptor.Subtype.PrimitiveTypeName}'" : null;
                builder.Append($@"
        writer.write{propTypeDescriptor.PrimitiveTypeName}(value.{prop.FieldName}{typeArg});");
            }
            builder.Append(@"
    }
");
            //write deserialize method
            builder.Append($@"
    Deserialize(object value, XdrReader reader) {{");
            //write property deserialization instructions
            foreach (var prop in contractDescriptor.Properties)
            {
                var propTypeDescriptor = GetTypeDescriptor(prop);
                var typeArg = propTypeDescriptor.Subtype != null ? $"'{propTypeDescriptor.Subtype.PrimitiveTypeName}'" : null;
                builder.Append($@"
        value.{prop.FieldName} = reader.read{propTypeDescriptor.PrimitiveTypeName}({typeArg});");
            }
            builder.Append(@"
    }
");
            //write converter tail
            builder.Append($@"}}

}}");
            return builder.ToString();
        }
    }
}
