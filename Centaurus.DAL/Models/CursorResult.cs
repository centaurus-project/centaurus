using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.DAL.Models
{
    public class CursorResult<T>
    {
        public List<T> Items { get; set; }

        public string CurrentToken { get; set; }

        public string PrevToken { get; set; }

        public string NextToken { get; set; }
    }
}
