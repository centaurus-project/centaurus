using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

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

        public AlphaState GetCurrentAlphaState()
        {
            return new AlphaState
            {
                State = State,
                LastSnapshot = Global.SnapshotManager.LastSnapshot
            };
        }
    }
}
