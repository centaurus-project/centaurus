using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Centaurus.Xdr
{
    public class XdrContractSerializationDescriptor
    {
        public XdrContractSerializationDescriptor(Type xdrContractType)
        {
            Name = xdrContractType.Name;
            XdrContractType = xdrContractType;
            if (XdrContractType.BaseType != typeof(object))
            {
                BaseContractType = XdrContractType.BaseType;
            }
            UnionSwitch = GetUnionMarkup(xdrContractType)
                .OrderBy(union => union.Discriminator)
                .ToDictionary(union => union.Discriminator, union => union.ArmType);
            DiscoverMarkup(xdrContractType);
        }

        public readonly String Name;

        public readonly Type XdrContractType;

        public readonly HashSet<XdrPropertySerializationDescriptor> Properties = new HashSet<XdrPropertySerializationDescriptor>();

        public readonly List<int> UnionVector = new List<int>();

        public readonly Dictionary<int, Type> UnionSwitch;

        public readonly Type BaseContractType;

        public int AncestorUnionsCounts { get; private set; }

        private void DiscoverMarkup(Type type)
        {
            if (type == typeof(object)) return;
            //analyze base type before processing current
            DiscoverMarkup(type.BaseType);
            //discover parent union vector
            var unions = GetUnionMarkup(type.BaseType);
            if (unions.Count > 0)
            {
                var currentTypeContract = unions.FirstOrDefault(union => XdrContractType == union.ArmType || XdrContractType.IsSubclassOf(union.ArmType));
                if (currentTypeContract != null)
                {
                    UnionVector.Add(currentTypeContract.Discriminator);
                    AncestorUnionsCounts++;
                    //throw new InvalidOperationException($"Failed to build union vector for {SerializedType.FullName}. Use {nameof(XdrUnionAttribute)} to define the union tree from the base class.");
                }
            }
            //retrieve all properties marked with XdrFieldAttribute and sort them accordingly to the user-defined order
            var properties = type.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance | BindingFlags.SetProperty | BindingFlags.GetProperty)
                .Where(prop => prop.GetCustomAttribute<XdrFieldAttribute>() != null)
                .OrderBy(prop => prop.GetCustomAttribute<XdrFieldAttribute>().Order);

            foreach (var prop in properties)
            {
                var xdrProp = new XdrPropertySerializationDescriptor(prop, prop.DeclaringType != XdrContractType);
                Properties.Add(xdrProp);
            }
        }

        private List<XdrUnionAttribute> GetUnionMarkup(Type type)
        {
            return type.GetCustomAttributes<XdrUnionAttribute>(false).ToList();
        }

        public override string ToString()
        {
            return $"{XdrContractType.Name}{(UnionSwitch.Count > 0 ? ", union " : "")} {Properties.Count} props";
        }
    }
}
