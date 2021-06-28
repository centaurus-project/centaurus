using Centaurus.Xdr;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Centaurus.Alpha
{
    public class XdrModelBinderProvider : IModelBinderProvider
    {
        private readonly IModelBinder binder = new XdrModelBinder();

        public IModelBinder GetBinder(ModelBinderProviderContext context)
        {
            return context.Metadata.ModelType.GetCustomAttributes(typeof(XdrContractAttribute), true).Length > 0 ? binder : null;
        }
    }
}