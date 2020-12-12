using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class AlphaStateManager : StateManager
    {
        private HashSet<RawPubKey> ConnectedAuditors = new HashSet<RawPubKey>();

        public bool HasMajority => ConnectedAuditors.Count >= MajorityHelper.GetMajorityCount();

        public void AuditorConnected(RawPubKey rawPubKey)
        {
            lock (this)
            {
                ConnectedAuditors.Add(rawPubKey);
                if (HasMajority && State == ApplicationState.Running)
                    State = ApplicationState.Ready;
            }
        }

        public void AuditorConnectionClosed(RawPubKey rawPubKey)
        {
            lock (this)
            {
                if (!ConnectedAuditors.Contains(rawPubKey))
                    return;
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
