﻿using Centaurus.Xdr;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Models
{
    public class AuditorResultsBatch : Message
    {
        [XdrField(0)]
        public List<AuditorResultMessage> AuditorResultMessages { get; set; }
    }
}