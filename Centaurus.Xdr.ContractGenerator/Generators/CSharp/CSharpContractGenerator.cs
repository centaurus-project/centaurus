using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.ContractGenerator
{
    public class CSharpContractGenerator : ContractGenerator
    {
        public CSharpContractGenerator(string contractsNamespace)
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
            MapType(new PrimitiveTypeDescriptor(typeof(Array), "Array", "Array"));
            MapType(new PrimitiveTypeDescriptor(typeof(List<>), "List", "List"));
            MapType(new PrimitiveTypeDescriptor(typeof(object), "Object", "Object"));
            this.ContractsNamespace = contractsNamespace;
        }

        private readonly string ContractsNamespace;

        public override GeneratedContractsBundle Generate()
        {
            var bundle = new GeneratedContractsBundle();
            foreach (var contractDescriptor in ContractsMap.Values)
            {
                bundle.Add(new GeneratedContractFile(GetSerializerClassName(contractDescriptor) + ".cs", GenerateSerializerFile(contractDescriptor)));
            }
            return bundle;
        }

        private string GenerateSerializerFile(XdrContractSerializationDescriptor contractDescriptor)
        {
            var builder = new StringBuilder();
            //write header
            builder.Append($@"using System;
using Centaurus.Xdr;
using System.Collections.Generic;

namespace {ContractsNamespace}
{{
    public class {GetSerializerClassName(contractDescriptor)}: IXdrRuntimeContractSerializer<{GetContractName(contractDescriptor)}>
    {{");
            //write union info
            if (contractDescriptor.UnionSwitch.Count > 0)
            {
                builder.Append(@"
        public readonly bool IsUnion = true;
");
            }
            if (contractDescriptor.AncestorUnionsCounts > 0)
            {
                builder.Append($@"
        public readonly int AncestorUnionsCounts = {contractDescriptor.AncestorUnionsCounts};
");
            }
            //write serialize method
            builder.Append($@"
        public static void Serialize({contractDescriptor.XdrContractType.Name} value, XdrWriter writer) {{");
            //write union discriminators serialization if any
            foreach (var discriminator in contractDescriptor.UnionVector)
            {
                builder.Append($@"
            writer.Write{GetMethodPrimitiveTypeName(typeof(int))}({discriminator});");
            }
            //write property serialization instructions
            foreach (var prop in contractDescriptor.Properties)
            {
                builder.Append($@"
            {GeneratePropertySerializeInstructions(prop)}");
            }
            builder.Append(@"
        }
");

            //write deserialize method
            builder.Append($@"
        public static void Deserialize({contractDescriptor.XdrContractType.Name} value, XdrReader reader) {{");
            //write property deserialization instructions
            foreach (var prop in contractDescriptor.Properties)
            {
                builder.Append($@"
            {GeneratePropertyDeserializeInstructions(prop)};");
            }
            builder.Append(@"
        }
");
            if (contractDescriptor.UnionSwitch.Count > 0)
            {
                //write deserialize method
                builder.Append(@"
        public static Type ResolveActualUnionType(XdrReader reader) 
        {
            var typeId = reader.ReadInt32();
            switch (typeId)
            {");

                //write union discriminators serialization if any
                foreach (var kv in contractDescriptor.UnionSwitch)
                {
                    builder.Append($@"
                case {kv.Key}: return typeof({kv.Value.Name});");
                }
                builder.Append(@"
                default: throw new InvalidOperationException($""Failed to resolve type for union type id { typeId}."");
            }
        }
");
            }

            //write converter tail
            builder.Append(@"    }
}");
            return builder.ToString();
        }

        private string GeneratePropertySerializeInstructions(XdrPropertySerializationDescriptor prop)
        {
            var propTypeDescriptor = GetTypeDescriptor(prop);
            var fieldAccessor = $"value.{prop.PropertyName}";
            if (!prop.IsOptional) return GeneratePlainPropertySerializeInstructions(propTypeDescriptor, fieldAccessor);
            return $@"if ({fieldAccessor} == null) 
                {{
                    writer.WriteInt32(0);
                }}
                else
                {{
                    writer.WriteInt32(1);
                    {GeneratePlainPropertySerializeInstructions(propTypeDescriptor, fieldAccessor)}
                }}
";
        }

        private string GeneratePlainPropertySerializeInstructions(PrimitiveTypeDescriptor propTypeDescriptor, string fieldAccessor)
        {
            var primitive = propTypeDescriptor.PrimitiveTypeName;
            if (propTypeDescriptor.IsSubTypeContainer)
            {
                var subType = propTypeDescriptor.SubType.OriginalType;
                if (primitive == "Object") return $"XdrConverter.Serialize({fieldAccessor}, writer);";
                if (primitive == "Array" || primitive == "List")
                {
                    if (subType == typeof(byte)) return $"writer.WriteVariable({fieldAccessor});";
                    return GenerateArrayPropertySerializeInstructions(propTypeDescriptor, fieldAccessor);
                }
            }
            if (propTypeDescriptor.IsEnum) return $"writer.Write{propTypeDescriptor.PrimitiveTypeName}((int){fieldAccessor});";
            return $"writer.Write{propTypeDescriptor.PrimitiveTypeName}({fieldAccessor});";
        }

        private string GenerateArrayPropertySerializeInstructions(PrimitiveTypeDescriptor propTypeDescriptor, string fieldAccessor)
        {
            var containerTypeName = propTypeDescriptor.PrimitiveTypeName;
            var elementType = propTypeDescriptor.SubType.OriginalType;
            var lengthAccessor = containerTypeName == "Array" ? "Length" : "Count";
            var writeInstructions = $"XdrConverter.Serialize(array[i], writer)";

            if (elementType.IsValueType)
            {
                var valueTypeDescriptor = ResolvePrimitiveTypeTypeDescriptor(elementType);
                if (valueTypeDescriptor == null) throw new InvalidProgramException($"Primitive type {elementType.FullName} serialization in arrays is not supported.");
                writeInstructions = $"writer.Write{valueTypeDescriptor.PrimitiveTypeName}(array[i]);";
            }
            return $@"{{
                var array = {fieldAccessor};
                var total = array.{lengthAccessor};
                writer.WriteInt32(total);
                for (var i = 0; i < total; i++)
                {{
                    {writeInstructions};
                }}
                }}";
        }

        private string GeneratePropertyDeserializeInstructions(XdrPropertySerializationDescriptor prop)
        {
            var propTypeDescriptor = GetTypeDescriptor(prop);
            var fieldAccessor = $"value.{prop.PropertyName}";
            if (!prop.IsOptional) return GeneratePlainPropertyDeserializeInstructions(propTypeDescriptor, fieldAccessor);
            return $@"if (reader.ReadInt32() > 0) 
                {{
                    {GeneratePlainPropertyDeserializeInstructions(propTypeDescriptor, fieldAccessor)}
                }}
";
        }

        private string GeneratePlainPropertyDeserializeInstructions(PrimitiveTypeDescriptor propTypeDescriptor, string fieldAccessor)
        {
            var primitive = propTypeDescriptor.PrimitiveTypeName;

            if (propTypeDescriptor.IsSubTypeContainer)
            {
                var subType = propTypeDescriptor.SubType.OriginalType;
                if (primitive == "Object") return $"{fieldAccessor} = XdrConverter.Deserialize<{subType.Name}>(reader);";
                if (primitive == "Array" || primitive == "List")
                {
                    if (subType == typeof(byte)) return $"{fieldAccessor} = reader.ReadVariable();";
                    return GenerateArrayPropertyDeserializeInstructions(propTypeDescriptor, fieldAccessor);
                }
            }
            if (propTypeDescriptor.IsEnum) return $"{fieldAccessor} = ({propTypeDescriptor.EnumType.Name})reader.Read{propTypeDescriptor.PrimitiveTypeName}();";
            return $"{fieldAccessor} = reader.Read{primitive}();";
        }

        private string GenerateArrayPropertyDeserializeInstructions(PrimitiveTypeDescriptor propTypeDescriptor, string fieldAccessor)
        {
            var isArray = propTypeDescriptor.PrimitiveTypeName == "Array";
            var elementType = propTypeDescriptor.SubType.OriginalType;
            var initializer = isArray ? $"{elementType.Name}[length]" : $"List<{elementType.Name}>(length)";
            var readInstructions = $"XdrConverter.Deserialize<{elementType.Name}>(reader)";
            if (elementType.IsValueType)
            {
                var valueTypeDescriptor = ResolvePrimitiveTypeTypeDescriptor(elementType);
                if (valueTypeDescriptor == null) throw new InvalidProgramException($"Primitive type {elementType.FullName} serialization in arrays is not supported.");
                readInstructions = $"reader.Read{valueTypeDescriptor.PrimitiveTypeName}()";
            }
            return $@"{{
                var length = reader.Read{GetMethodPrimitiveTypeName(typeof(int))}();
                var res = new {initializer};
                for (var i = 0; i < length; i++)
                {{
                    {(isArray ? $"res[i] = {readInstructions}" : $"res.Add({readInstructions})")};
                }}
                {fieldAccessor} = res;
            }}";
        }
    }
}
