using System;

namespace Centaurus.Domain
{
    //TODO: add server types (FullNode, Auditor)
    public class RoleManager
    {
        public RoleManager(CentaurusNodeParticipationLevel participationLevel, CentaurusNodeRole initRole)
        {
            ParticipationLevel = participationLevel;
            SetRole(initRole);
        }

        private object syncRoot = new { };

        private CentaurusNodeRole role;
        public CentaurusNodeRole Role => role;

        public CentaurusNodeParticipationLevel ParticipationLevel { get; }

        public void SetRole(CentaurusNodeRole role)
        {
            if (this.role != role)
            {
                if ((role == CentaurusNodeRole.Alpha || role == CentaurusNodeRole.Beta) && ParticipationLevel != CentaurusNodeParticipationLevel.Prime)
                    throw new Exception($"Only nodes with Prime participation level can be switched to {role}.");
                lock (syncRoot)
                {
                    if (this.role != role)
                        this.role = role;
                }
            }
        }
    }

    public enum CentaurusNodeRole
    {
        Undefined = 0,
        Alpha = 1, //point of quantum sync
        Beta = 2, //full node, can accept requests, can 
        Auditor = 3
    }

    public enum CentaurusNodeParticipationLevel
    {
        Undefined = 0,
        Prime = 1, 
        Auditor = 2 
    }
}