using Centaurus.Domain.Models;
using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.PaymentProvider
{
    public interface ICursorComparer
    {
        int CompareCursors(string left, string right);
    }
}
