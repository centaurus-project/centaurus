using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Test.Contracts
{
    public class Fish: Vertebrate
    {
        [XdrField(1)]
        public bool HasScales { get; set; }

        [XdrField(0)]
        public FishBoneStructure Bones { get; set; }
    }

    public enum FishBoneStructure
    {
        Cartilaginous,
        RayFinned,
        LobeFinned
    }
}
