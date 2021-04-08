using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace Centaurus.Domain
{
    public class AlphaStateManager : StateManager
    {
        public AlphaStateManager(CentaurusContext context)
            : base(context)
        {
        }

        private Dictionary<RawPubKey, ConnectionState> ConnectedAuditors = new Dictionary<RawPubKey, ConnectionState>();

        public bool HasMajority => ConnectedAuditors.Count(a => a.Value == ConnectionState.Ready) >= context.GetMajorityCount();

        public int ConnectedAuditorsCount => ConnectedAuditors.Count;

        public void RegisterAuditorState(RawPubKey rawPubKey, ConnectionState connectionState)
        {
            lock (this)
            {
                ConnectedAuditors[rawPubKey] = connectionState;
                if (HasMajority && State == ApplicationState.Running)
                    State = ApplicationState.Ready;
            }
        }

        public void AuditorConnectionClosed(RawPubKey rawPubKey)
        {
            lock (this)
            {
                ConnectedAuditors.Remove(rawPubKey);
                if (!HasMajority && State == ApplicationState.Ready)
                    State = ApplicationState.Running;
            }
        }

        public void AlphaRised()
        {
            lock (this)
            {
                if (HasMajority)
                    State = ApplicationState.Ready;
                else
                    State = ApplicationState.Running;
            }
        }
    }
}
