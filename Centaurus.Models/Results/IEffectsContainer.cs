using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public interface IEffectsContainer
    {
        public List<Effect> ClientEffects { get; set; }

        public EffectsProof Effects { get; set; }

        public ulong Apex { get; }
    }
}
