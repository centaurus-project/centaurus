using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace Centaurus.Xdr
{
    internal static class DynamicModule
    {
        static DynamicModule()
        {
            DynamicAssemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(Namespace + "DynamicContractSerializer"), AssemblyBuilderAccess.Run);
            Builder = DynamicAssemblyBuilder.DefineDynamicModule(Namespace);
        }

        public const string Namespace = "Centaurus.XdrSerialization.";

        private static readonly AssemblyBuilder DynamicAssemblyBuilder;

        public static readonly ModuleBuilder Builder;

        public static TypeBuilder DefineDynamicSerializer(Type serializableType)
        {
            var typeName = Namespace + serializableType.FullName.Replace(".", "_") + "DynamicSerializer";
            return Builder.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class, null, null);
        }
    }
}
