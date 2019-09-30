using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Centaurus
{
    internal class XdrSerializationVector
    {
        private List<IXdrRuntimeGenericSerializer> serializers = new List<IXdrRuntimeGenericSerializer>();

        private int totalSerializers = 0;

        private bool frozen = false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Serialize(IXdrSerializableModel value, XdrWriter writer)
        {
            //the base class serializers should be always executed first
            for (int i = totalSerializers - 1; i >= 0; i--)
            {
                var serializer = serializers[i];
                serializer.Serialize(value, writer);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IXdrSerializableModel Deserialize(XdrReader reader)
        {
            var topSerializer = serializers[0];
            object res = topSerializer.IsAbstract ? null : topSerializer.CreateInstance();
            //the base class serializers should be always executed first
            for (int i = totalSerializers - 1; i >= 0; i--)
            {
                var serializer = serializers[i];
                serializer.Deserialize(ref res, reader);
            }
            return res as IXdrSerializableModel;
        }

        public void AddSerializer(IXdrRuntimeGenericSerializer serializer)
        {
            CheckNotFrozen();
            serializers.Add(serializer);
            totalSerializers++;
        }

        public void Freeze()
        {
            CheckNotFrozen();
            frozen = true;
        }

        private void CheckNotFrozen()
        {
            if (frozen) throw new InvalidOperationException("Can't modify XdrSerializationVector once it has been built and frozen.");
        }
    }
}
