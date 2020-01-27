using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection.Emit;
using System.Reflection;
using System.Linq;

namespace Centaurus.Xdr
{
    internal class XdrSerializerContractBuilder
    {
        public XdrSerializerContractBuilder(XdrContractDescriptor contractDescriptor)
        {
            ContractDescriptor = contractDescriptor;
        }

        public readonly XdrContractDescriptor ContractDescriptor;

        public TypeInfo CreateDynamicSerializer()
        {
            var typeBuilder = DynamicModule.DefineDynamicSerializer(ContractDescriptor.XdrContractType);
            typeBuilder.AddInterfaceImplementation(typeof(IXdrRuntimeContractSerializer));
            typeBuilder.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);

            BuildSerializeMethod(typeBuilder);
            BuildDeserializeMethod(typeBuilder);

            return typeBuilder.CreateTypeInfo();
        }

        private void BuildSerializeMethod(TypeBuilder typeBuilder)
        {
            var method = typeBuilder.DefineMethod("Serialize",
                MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual,
                null, new Type[] { typeof(object), typeof(XdrWriter) });

            BuildMethodBody(method, il =>
            {
                //serialize union discriminators if any
                foreach (var discriminator in ContractDescriptor.UnionVector)
                {
                    il.Emit(OpCodes.Ldarg_2);
                    il.Emit(OpCodes.Ldc_I4, discriminator);
                    il.Emit(OpCodes.Callvirt, xdrWriterMethods[typeof(int)]);
                }

                //LocalBuilder optionalFlag = null;

                //serialize properties
                foreach (var propDescriptor in ContractDescriptor.Properties)
                {
                    //handle optional serialization
                    if (propDescriptor.IsOptional)
                    {
                        //Label failed = adderIL.DefineLabel();

                        if (propDescriptor.IsNullable)
                        {
                            throw new NotImplementedException();
                        }
                        else
                        {
                            var noValueBranch = il.DefineLabel();
                            var endBranch = il.DefineLabel();

                            EmitSerializeGetValue(il, propDescriptor);

                            il.Emit(OpCodes.Brfalse_S, noValueBranch);

                            EmitSerializeOptionalFlag(il, propDescriptor, true); //write "value is not null" flag
                            EmitSerializeLoadWriter(il);
                            EmitSerializeGetValue(il, propDescriptor); //retrieve value from property
                            EmitSerializeWriteValue(il, propDescriptor); //write to buffer

                            il.Emit(OpCodes.Br_S, endBranch); //exit

                            //no value branch started
                            il.MarkLabel(noValueBranch);
                            il.Emit(OpCodes.Nop);
                            EmitSerializeOptionalFlag(il, propDescriptor, false); //write "value is null" flag

                            il.MarkLabel(endBranch);
                            il.Emit(OpCodes.Nop);
                        }
                    }
                    else
                    {
                        EmitSerializeLoadWriter(il);
                        EmitSerializeGetValue(il, propDescriptor); //retrieve value from property
                        EmitSerializeWriteValue(il, propDescriptor); //write to buffer
                    }
                }
            });
        }

        private void EmitSerializeLoadWriter(ILGenerator il)
        {
            il.Emit(OpCodes.Ldarg_2); //load writer onto stack
        }

        private void EmitSerializeGetValue(ILGenerator il, IXdrPropertySerializationDescriptor propDescriptor)
        {
            il.Emit(OpCodes.Ldloc_0); //obj
            il.Emit(OpCodes.Callvirt, propDescriptor.Property.GetMethod); //retrieve value
        }

        private void EmitSerializeOptionalFlag(ILGenerator il, IXdrPropertySerializationDescriptor propDescriptor, bool hasValue)
        {
            il.Emit(OpCodes.Ldarg_2); //writer
            il.Emit(hasValue ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0); // 1 or 0
            il.Emit(OpCodes.Callvirt, xdrWriterMethods[typeof(int)]); // write flag
        }

        private void EmitSerializeWriteValue(ILGenerator il, IXdrPropertySerializationDescriptor propDescriptor)
        {
            if (!xdrWriterMethods.TryGetValue(propDescriptor.PrimitiveType, out MethodInfo write))
                throw new InvalidOperationException($"Failed to locate primitive value serializer for type {propDescriptor.GenericArgument.FullName}. Check {propDescriptor.ToString()}.");

            //byte array is a special case
            if (propDescriptor.PrimitiveType == typeof(byte[]))
            {
                var pointerType = typeof(int?);
                var local = il.DeclareLocal(pointerType);
                il.Emit(OpCodes.Ldloca_S, local.LocalIndex);
                il.Emit(OpCodes.Initobj, pointerType);
                il.Emit(OpCodes.Ldloc, local.LocalIndex);
            }

            il.Emit(OpCodes.Callvirt, write); //invoke XdrWriter primitive method
        }

        private void BuildDeserializeMethod(TypeBuilder typeBuilder)
        {
            var method = typeBuilder.DefineMethod("Deserialize",
                MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual,
                null, new Type[] { typeof(object), typeof(XdrReader) });

            BuildMethodBody(method, il =>
            {
                foreach (var propDescriptor in ContractDescriptor.Properties)
                {
                    //handle optional serialization
                    if (propDescriptor.IsOptional)
                    {
                        //Label failed = adderIL.DefineLabel();

                        if (propDescriptor.IsNullable)
                        {
                            throw new NotImplementedException();
                        }
                        else
                        {
                            var endBranch = il.DefineLabel();

                            il.Emit(OpCodes.Ldarg_2);
                            il.Emit(OpCodes.Callvirt, xdrReaderMethods[typeof(int)]); //invoke XdrReader primitive method

                            il.Emit(OpCodes.Brfalse_S, endBranch);

                            EmitDeserializeReadValue(il);
                            EmitDeserializeWriteValue(il, propDescriptor);

                            il.MarkLabel(endBranch);
                            il.Emit(OpCodes.Nop);
                        }
                    }
                    else
                    {
                        EmitDeserializeReadValue(il);
                        EmitDeserializeWriteValue(il, propDescriptor);
                    }
                }
            });
        }

        private void EmitDeserializeReadValue(ILGenerator il)
        {
            il.Emit(OpCodes.Ldloc_0); //obj
            il.Emit(OpCodes.Ldarg_2); //reader
        }

        private void EmitDeserializeWriteValue(ILGenerator il, IXdrPropertySerializationDescriptor propDescriptor)
        {
            var setter = propDescriptor.Property.SetMethod;

            if (!xdrReaderMethods.TryGetValue(propDescriptor.PrimitiveType, out MethodInfo read))
                throw new InvalidOperationException($"Failed to locate primitive value serializer for type {propDescriptor.PrimitiveType.FullName}. Check {propDescriptor.ToString()}.");

            if (propDescriptor.GenericArgument != null)
            {
                read = read.MakeGenericMethod(propDescriptor.GenericArgument);
            }

            il.Emit(OpCodes.Callvirt, read); //invoke XdrReader primitive method
            il.Emit(OpCodes.Callvirt, setter); //set property value
        }

        private void BuildMethodBody(MethodBuilder method, Action<ILGenerator> emitOperations)
        {
            //init method generator
            var il = method.GetILGenerator();

            //cast type from object
            var local = il.DeclareLocal(ContractDescriptor.XdrContractType);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Isinst, ContractDescriptor.XdrContractType);
            il.Emit(OpCodes.Stloc, local.LocalIndex);

            //emit required operations
            emitOperations(il);

            //return null
            il.Emit(OpCodes.Ret);
        }

        #region PRIMITIVE_SERIALIZATION_METHODS_CACHE
        static XdrSerializerContractBuilder()
        {
            var readerType = typeof(XdrReader);
            xdrReaderMethods.Add(typeof(object), readerType.GetMethod(nameof(XdrReader.ReadObject)));
            xdrReaderMethods.Add(typeof(bool), readerType.GetMethod(nameof(XdrReader.ReadBool)));
            xdrReaderMethods.Add(typeof(int), readerType.GetMethod(nameof(XdrReader.ReadInt32)));
            xdrReaderMethods.Add(typeof(uint), readerType.GetMethod(nameof(XdrReader.ReadUInt32)));
            xdrReaderMethods.Add(typeof(long), readerType.GetMethod(nameof(XdrReader.ReadInt64)));
            xdrReaderMethods.Add(typeof(ulong), readerType.GetMethod(nameof(XdrReader.ReadUInt64)));
            xdrReaderMethods.Add(typeof(float), readerType.GetMethod(nameof(XdrReader.ReadFloat)));
            xdrReaderMethods.Add(typeof(double), readerType.GetMethod(nameof(XdrReader.ReadDouble)));
            xdrReaderMethods.Add(typeof(string), readerType.GetMethod(nameof(XdrReader.ReadString)));
            xdrReaderMethods.Add(typeof(byte[]), readerType.GetMethod(nameof(XdrReader.ReadVariable)));
            xdrReaderMethods.Add(typeof(int[]), readerType.GetMethod(nameof(XdrReader.ReadInt32Array)));
            xdrReaderMethods.Add(typeof(long[]), readerType.GetMethod(nameof(XdrReader.ReadInt64Array)));
            xdrReaderMethods.Add(typeof(float[]), readerType.GetMethod(nameof(XdrReader.ReadFloatArray)));
            xdrReaderMethods.Add(typeof(double[]), readerType.GetMethod(nameof(XdrReader.ReadDoubleArray)));
            xdrReaderMethods.Add(typeof(List<int>), readerType.GetMethod(nameof(XdrReader.ReadInt32List)));
            xdrReaderMethods.Add(typeof(List<long>), readerType.GetMethod(nameof(XdrReader.ReadInt32List)));
            xdrReaderMethods.Add(typeof(List<float>), readerType.GetMethod(nameof(XdrReader.ReadFloatList)));
            xdrReaderMethods.Add(typeof(List<double>), readerType.GetMethod(nameof(XdrReader.ReadDoubleList)));
            xdrReaderMethods.Add(typeof(List<>), readerType.GetMethod(nameof(XdrReader.ReadList)));

            var writerType = typeof(XdrWriter);
            xdrWriterMethods.Add(typeof(object), writerType.GetMethod(nameof(XdrWriter.WriteObject)));
            xdrWriterMethods.Add(typeof(bool), writerType.GetMethod(nameof(XdrWriter.WriteBool)));
            xdrWriterMethods.Add(typeof(int), writerType.GetMethod(nameof(XdrWriter.WriteInt32)));
            xdrWriterMethods.Add(typeof(uint), writerType.GetMethod(nameof(XdrWriter.WriteUInt32)));
            xdrWriterMethods.Add(typeof(long), writerType.GetMethod(nameof(XdrWriter.WriteInt64)));
            xdrWriterMethods.Add(typeof(ulong), writerType.GetMethod(nameof(XdrWriter.WriteUInt64)));
            xdrWriterMethods.Add(typeof(float), writerType.GetMethod(nameof(XdrWriter.WriteFloat)));
            xdrWriterMethods.Add(typeof(double), writerType.GetMethod(nameof(XdrWriter.WriteDouble)));
            xdrWriterMethods.Add(typeof(string), writerType.GetMethod(nameof(XdrWriter.WriteString)));
            xdrWriterMethods.Add(typeof(byte[]), writerType.GetMethod(nameof(XdrWriter.WriteVariable)));
            xdrWriterMethods.Add(typeof(int[]), writerType.GetMethod(nameof(XdrWriter.WriteInt32Array)));
            xdrWriterMethods.Add(typeof(long[]), writerType.GetMethod(nameof(XdrWriter.WriteInt64Array)));
            xdrWriterMethods.Add(typeof(float[]), writerType.GetMethod(nameof(XdrWriter.WriteFloatArray)));
            xdrWriterMethods.Add(typeof(double[]), writerType.GetMethod(nameof(XdrWriter.WriteDoubleArray)));
            xdrWriterMethods.Add(typeof(List<int>), writerType.GetMethod(nameof(XdrWriter.WriteInt32List)));
            xdrWriterMethods.Add(typeof(List<long>), writerType.GetMethod(nameof(XdrWriter.WriteInt64List)));
            xdrWriterMethods.Add(typeof(List<float>), writerType.GetMethod(nameof(XdrWriter.WriteFloatList)));
            xdrWriterMethods.Add(typeof(List<double>), writerType.GetMethod(nameof(XdrWriter.WriteDoubleList)));
            xdrWriterMethods.Add(typeof(List<>), writerType.GetMethod(nameof(XdrWriter.WriteArray)));

            //TODO: handle DateTime, char, byte, sbyte, nullables, and arrays of all mentioned above types
        }

        static Dictionary<Type, MethodInfo> xdrReaderMethods = new Dictionary<Type, MethodInfo>();
        static Dictionary<Type, MethodInfo> xdrWriterMethods = new Dictionary<Type, MethodInfo>();
        #endregion
    }
}
