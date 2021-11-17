using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Centaurus.Domain
{
    public static class ExecutionContextExtensions
    {
        public static List<RawPubKey> GetAuditors(this ExecutionContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            return (context.Constellation == null
                    ? context.Settings.GenesisAuditors.Select(a => (RawPubKey)a.PubKey)
                    : context.Constellation.Auditors.Select(a => a.PubKey))
                    .ToList();
        }
    }
}
