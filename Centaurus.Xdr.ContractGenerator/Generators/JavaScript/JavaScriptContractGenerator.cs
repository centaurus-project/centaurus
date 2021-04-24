using Centaurus.Xdr;
using System;
using System.Collections;
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
            MapType(new PrimitiveTypeDescriptor(typeof(byte), "Byte", "Int32"));
            MapType(new PrimitiveTypeDescriptor(typeof(int), "Int32", "Int32"));
            MapType(new PrimitiveTypeDescriptor(typeof(uint), "UInt32", "UInt32"));
            MapType(new PrimitiveTypeDescriptor(typeof(long), "Int64", "Int64"));
            MapType(new PrimitiveTypeDescriptor(typeof(ulong), "UInt64", "UInt64"));
            MapType(new PrimitiveTypeDescriptor(typeof(float), "Float", "Float"));
            MapType(new PrimitiveTypeDescriptor(typeof(double), "Double", "Double"));
            MapType(new PrimitiveTypeDescriptor(typeof(string), "String", "String"));
            MapType(new PrimitiveTypeDescriptor(typeof(List<>), "Array", "Array"));
            MapType(new PrimitiveTypeDescriptor(typeof(Array), "Array", "Array"));
            MapType(new PrimitiveTypeDescriptor(typeof(byte[]), "Buffer", "Variable"));
            MapType(new PrimitiveTypeDescriptor(typeof(object), "Object", "Object"));
        }

        private const string Padding = "    ";

        private HashSet<Type> EnumDefinitions;

        private string ProcessFieldName(string fieldName)
        {
            return CaseConversionUtils.ConvertToCamelCase(fieldName);
        }

        private string ResolveReflectedPropTypeName(XdrPropertySerializationDescriptor prop)
        {
            var typeDescriptor = GetTypeDescriptor(prop);
            if (typeDescriptor.IsEnum)
            {
                EnumDefinitions.Add(typeDescriptor.EnumType);
                return typeDescriptor.EnumType.Name;
            }
            if (typeDescriptor.SubType == null) return typeDescriptor.TargetType;
            return $"{typeDescriptor.TargetType}<{typeDescriptor.SubType.TargetType}>";
        }

        private string GetContractFileName(Type contractType, string suffix = "")
        {
            return CaseConversionUtils.ConvertPascalCaseToKebabCase(contractType.Name + suffix);
        }

        public override GeneratedContractsBundle Generate()
        {
            var bundle = new GeneratedContractsBundle();
            EnumDefinitions = new HashSet<Type>();
            foreach (var contractDescriptor in ContractsMap.Values)
            {
                var contractName = contractDescriptor.XdrContractType.Name;
                bundle.Add(new GeneratedContractFile($"{GetContractFileName(contractDescriptor.XdrContractType)}.js", GenerateContractFile(contractDescriptor, contractName)));
                bundle.Add(new GeneratedContractFile($"{GetContractFileName(contractDescriptor.XdrContractType, "Serializer")}.js", GenerateSerializerFile(contractDescriptor, contractName)));
            }
            foreach (var enumDefinition in EnumDefinitions)
            {
                bundle.Add(new GeneratedContractFile($"{GetContractFileName(enumDefinition)}.js", GenerateEnumFile(enumDefinition)));
            }

            bundle.Add(new GeneratedContractFile("index.js", GenerateIndexFile(bundle)));
            return bundle;
        }

        private string GenerateIndexFile(GeneratedContractsBundle bundle)
        {
            var builder = new StringBuilder();
            var exports = new List<string>();
            var initializers = new List<string>();
            foreach (var file in bundle.Files)
            {
                var fileName = file.FileName;
                var className = CaseConversionUtils.ConvertKebabCaseToPascalCase(fileName.Split('.')[0]);
                if (!className.EndsWith("Serializer"))
                {
                    exports.Add(className);
                }
                else
                {
                    initializers.Add(className);
                }
                builder.Append($"const {className} = require('./{fileName}')\n");
            }
            builder.Append("\nfunction registerSerializers(xdrConverter) {");
            foreach (var initializer in initializers)
            {
                builder.Append($"\n{Padding}xdrConverter.registerSerializer({initializer.Replace("Serializer", string.Empty)}, {initializer})");
            }
            builder.Append("\n}");
            builder.Append($"\n\nmodule.exports = {{\n{Padding}registerSerializers,\n{string.Join(",\n", exports.Select(s => Padding + s))}\n}}");
            return builder.ToString();
        }

        private string GenerateContractFile(XdrContractSerializationDescriptor contractDescriptor, string contractName)
        {
            var extendsContract = contractDescriptor.BaseContractType != null;
            var builder = new StringBuilder();
            if (extendsContract)
            {
                builder.Append($"const {contractDescriptor.BaseContractType.Name} = require('./{GetContractFileName(contractDescriptor.BaseContractType)}')\n\n");
            }
            //write header
            builder.Append($@"/**
* {contractName} XDR data contract.
*/
module.exports = class {contractName}{(extendsContract ? $" extends {contractDescriptor.BaseContractType.Name}" : "")} {{");
            //write contract fields
            foreach (var prop in contractDescriptor.Properties.Where(p => !p.Inherited))
            {
                builder.Append(@$"
    /**
    * @type {{{ResolveReflectedPropTypeName(prop)}}}
    */
    {ProcessFieldName(prop.PropertyName)}");
            }
            //write tail
            builder.Append($@"
}}");
            return builder.ToString();
        }

        private string GenerateEnumFile(Type enumType)
        {
            var builder = new StringBuilder();
            var contractName = enumType.Name;

            //write header
            builder.Append($@"/**
* {contractName} enum.
* @readonly
* @enum {{Number}}
*/
const {contractName} = {{
");
            //write enum values
            var values = Enum.GetValues(enumType).Cast<int>().Select(value => $"    {Enum.GetName(enumType, value)}: {value}");
            builder.Append(String.Join(",\n", values));
            //write tail
            builder.Append($@"
}}
Object.freeze({contractName})
module.exports = {contractName}");
            return builder.ToString();
        }

        private string GenerateSerializerFile(XdrContractSerializationDescriptor contractDescriptor, string contractName)
        {
            var builder = new StringBuilder();
            //write header
            var converterClassName = contractName + "Serializer";
            builder.Append($@"/**
* Converter for {contractName} XDR data contract.
*/
class {converterClassName} {{");
            //write union info
            if (contractDescriptor.UnionSwitch.Count > 0)
            {
                builder.Append(@"
    isUnion = true
    unionSwitch = {");
                foreach (var kv in contractDescriptor.UnionSwitch)
                {
                    builder.Append($@"
         '{kv.Key}': '{kv.Value.Name}',");
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
                var typeArg = propTypeDescriptor.SubType != null ? $", '{propTypeDescriptor.SubType.TargetType}'" : null;
                builder.Append($@"
        writer.write{propTypeDescriptor.PrimitiveTypeName}(value.{ProcessFieldName(prop.PropertyName)}{typeArg})");
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
                var typeArg = propTypeDescriptor.SubType != null ? $"'{propTypeDescriptor.SubType.TargetType}'" : null;
                builder.Append($@"
        value.{ProcessFieldName(prop.PropertyName)} = reader.read{propTypeDescriptor.PrimitiveTypeName}({typeArg})");
            }
            builder.Append(@"
    }
");
            //write converter tail
            builder.Append($@"}}

module.exports = new {converterClassName}()");
            return builder.ToString();
        }

        private string GetSubTypeArg(PrimitiveTypeDescriptor propTypeDescriptor, string format)
        {
            if (propTypeDescriptor.SubType == null) return null;
            var subtype = propTypeDescriptor.SubType.PrimitiveTypeName == "Object" ? propTypeDescriptor.SubType.TargetType : propTypeDescriptor.SubType.PrimitiveTypeName;
            return format.Replace("value", subtype);
        }
    }
}
