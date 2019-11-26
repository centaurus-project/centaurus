using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Centaurus.Domain
{
    public class AlphaStateManager : StateManager
    {
        private int ConnectedAuditorsCount = 0;

        public bool HasMajority => ConnectedAuditorsCount >= MajorityHelper.GetMajorityCount();

        public void AuditorConnected()
        {
            lock (this)
            {
                ConnectedAuditorsCount++;
                if (HasMajority && State == ApplicationState.Running)
                    State = ApplicationState.Ready;
            }
        }

        public void AuditorConnectionClosed()
        {
            lock (this)
            {
                ConnectedAuditorsCount--;
                if (!HasMajority && State == ApplicationState.Ready)
                    State = ApplicationState.Running;
            }
        }

        public void AlphaRised()
        {
            lock (this)
            {
                if (HasMajority && State == ApplicationState.Running)
                    State = ApplicationState.Ready;
            }
        }

        public async Task<AlphaState> GetCurrentAlphaState()
        {
            return new AlphaState
            {
                State = State,
                LastSnapshot = await Global.SnapshotManager.GetSnapshot(ulong.MaxValue)
            };
        }
    }
}
