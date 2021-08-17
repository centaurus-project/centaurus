﻿using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    [XdrContract]
    public class ConstellationUpdateEffect : Effect
    {
        [XdrField(0)]
        public ConstellationSettings Settings { get; set; }

        [XdrField(1, Optional = true)]
        public ConstellationSettings PrevSettings { get; set; }
    }
}