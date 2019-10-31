using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Test.Contracts
{
    [XdrContract]
    public class Feature
    {
        [XdrField(0)]
        public string FeatureDescription { get; set; }

        [XdrField(1)]
        public bool Unique { get; set; }
    }
}
