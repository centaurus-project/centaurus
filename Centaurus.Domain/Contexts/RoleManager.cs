namespace Centaurus.Domain
{
    //TODO: add server types (FullNode, Auditor)
    public class RoleManager
    {
        public RoleManager(CentaurusRole initRole)
        {
            SetRole(initRole);
        }

        private object syncRoot = new { };

        private CentaurusRole role;
        public CentaurusRole Role => role;

        public void SetRole(CentaurusRole role)
        {
            if (this.role != role)
                lock (syncRoot)
                {
                    if (this.role != role)
                        this.role = role;
                }
        }
    }

    public enum CentaurusRole
    {
        Undefined = 0,
        Alpha = 1, //point of quantum sync
        Beta = 2, //full node, can accept requests, can 
        Auditor = 3
    }
}