using System;
using System.Collections.Generic;
using System.Text;

namespace Centaurus.Domain
{
    public class UpdatedObject
    {
        public UpdatedObject(object targer, bool isDeleted = false)
        {
            Target = targer;
            IsDeleted = isDeleted;
        }

        public object Target { get; }

        public bool IsDeleted { get; }
    }
}
