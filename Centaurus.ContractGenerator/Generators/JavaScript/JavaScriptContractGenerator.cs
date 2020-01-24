using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Centaurus.ContractGenerator
{
    public class JavaScriptContractGenerator : ContractGenerator
    {
        public JavaScriptContractGenerator()
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

        protected override IEnumerable<GeneratedContractFile> GenerateContract(XdrContractDescriptor contractDescriptor)
        {
            var contractName = contractDescriptor.XdrContractType.Name;
            yield return new GeneratedContractFile($"{CaseConversionUtils.ConvertPascalCaseToKebabCase(contractName)}.js", GenerateContractFile(contractDescriptor, contractName));
            yield return new GeneratedContractFile($"{CaseConversionUtils.ConvertPascalCaseToKebabCase(contractName + "Serializer")}.js", GenerateSerializerFile(contractDescriptor, contractName));
        }

        private string ProcessFieldName(string fieldName)
        {
            return CaseConversionUtils.ConvertToCamelCase(fieldName);
        }

        private string ResolveReflectedPropTypeName(IXdrPropertySerializationDescriptor prop)
        {
            var typeDescriptor = GetTypeDescriptor(prop);
            if (typeDescriptor.Subtype == null) return typeDescriptor.TargetType;
            return $"{typeDescriptor.TargetType}<{typeDescriptor.Subtype.TargetType}>";
        }

        public override GeneratedContractsBundle Generate()
        {
            var bundle = base.Generate();
            var builder = new StringBuilder("import xdrConverter from '../serialization/xdr-converter'");
            var exports = new List<string>();
            foreach (var file in bundle.Files)
            {
                var fileName = file.FileName;
                var className = CaseConversionUtils.ConvertKebabCaseToPascalCase(fileName.Split('.')[0]);
                exports.Add(className);
                builder.Append($"\nimport {className} from './{fileName}'");
            }
            builder.Append("\n");
            foreach (var export in exports)
                if (export.EndsWith("Serializer"))
                {
                    builder.Append($"\nxdrConverter.registerSerializer({export.Replace("Serializer", string.Empty)}.name, {export})");
                }
            builder.Append($"\n\nexport {{\n{string.Join(",\n", exports)}\n}}");
            bundle.Add(new GeneratedContractFile("index.js", builder.ToString()));
            return bundle;
        }

        private string GenerateContractFile(XdrContractDescriptor сontractDescriptor, string contractName)
        {

            var builder = new StringBuilder();
            //write header
            builder.Append($@"/**
* {contractName} XDR data contract.
*/
class {contractName} {{");
            //write contract fields
            foreach (var prop in сontractDescriptor.Properties)
            {
                builder.Append(@$"
    /**
    * @type {{{ResolveReflectedPropTypeName(prop)}}}
    */
    {ProcessFieldName(prop.FieldName)}");
            }
            //write tail
            builder.Append($@"
}}

export default {contractName}");
            return builder.ToString();
        }

        private string GenerateSerializerFile(XdrContractDescriptor contractDescriptor, string contractName)
        {
            var builder = new StringBuilder();
            //write header
            var converClassName = contractName + "Serializer";
            builder.Append($@"/**
* Converter for {contractName} XDR data contract.
*/
class {converClassName} {{");
            //write union info
            if (contractDescriptor.UnionSwitch.Count > 0)
            {
                builder.Append(@"
    isUnion = true
    unionSwitch = {");
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
    ancestorUnionsCounts = {contractDescriptor.AncestorUnionsCounts}");
            }
            //write serialize method
            builder.Append($@"
    /**
    * @param {{{contractName}}} value - Value to serialize.
    * @param {{XdrWriter}} writer - XdrWriter stream instance.
    */
    serialize(value, writer) {{");
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
        writer.write{propTypeDescriptor.PrimitiveTypeName}(value.{ProcessFieldName(prop.FieldName)}{typeArg})");
            }
            builder.Append(@"
    }
");
            //write deserialize method
            builder.Append($@"
   /**
   * @param {{{contractName}}} value - Instance of object to deserialize into.
   * @param {{XdrReader}} reader - XdrReader stream instance.
   */
   deserialize(value, reader) {{");
            //write property deserialization instructions
            foreach (var prop in contractDescriptor.Properties)
            {
                var propTypeDescriptor = GetTypeDescriptor(prop);
                var typeArg = propTypeDescriptor.Subtype != null ? $"'{propTypeDescriptor.Subtype.PrimitiveTypeName}'" : null;
                builder.Append($@"
        value.{ProcessFieldName(prop.FieldName)} = reader.read{propTypeDescriptor.PrimitiveTypeName}({typeArg})");
            }
            builder.Append(@"
    }
");
            //write converter tail
            builder.Append($@"}}

export default {converClassName}");
            return builder.ToString();
        }
    }
}
