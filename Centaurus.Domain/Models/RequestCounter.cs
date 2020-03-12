﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class RequestCounter
    {
        public RequestCounter()
        {
            Reset(0);
        }
        public long StartedAt { get; private set; }

        public int Count { get; private set; }

        public void IncRequestsCount()
        {
            Count++;
        }

        public void Reset(long startedAt)
        {
            StartedAt = startedAt;
            Count = 0;
        }
    }
}
