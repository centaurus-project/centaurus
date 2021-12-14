using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain.Nodes.Common
{
    public class NodeSettings
    {
        public NodeSettings(int id, Uri address)
        {
            Id = id;
            Address = address;
        }

        public int Id { get; }

        public Uri Address { get; }

        public override bool Equals(object obj)
        {
            return obj is NodeSettings settings &&
                   Id == settings.Id &&
                   EqualityComparer<Uri>.Default.Equals(Address, settings.Address);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Address);
        }

        public static bool operator ==(NodeSettings lhs, NodeSettings rhs)
        {
            if (lhs is null)
            {
                if (rhs is null)
                {
                    return true;
                }
                return false;
            }
            return lhs.Equals(rhs);
        }

        public static bool operator !=(NodeSettings lhs, NodeSettings rhs) => !(lhs == rhs);
    }
}
