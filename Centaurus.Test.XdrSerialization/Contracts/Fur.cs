using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Test.Contracts
{
    [XdrContract]
    public class Fur
    {
        [XdrField(1)]
        public int Length { get; set; }

        [XdrField(2)]
        public double Coverage { get; set; }

        [XdrField(0)]
        public string Color { get; set; }
    }
}
