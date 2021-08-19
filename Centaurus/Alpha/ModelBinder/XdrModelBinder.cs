using Centaurus.Models;
using Centaurus.Xdr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Centaurus.Alpha
{
    public class XdrModelBinder : IModelBinder
    {
        public async Task BindModelAsync(ModelBindingContext bindingContext)
        {
            if (bindingContext == null)
                throw new ArgumentNullException(nameof(bindingContext));

            using var memoryStream = new MemoryStream();
            await bindingContext.HttpContext.Request.Body.CopyToAsync(memoryStream);
            bindingContext.Result = ModelBindingResult.Success(XdrConverter.Deserialize<MessageEnvelopeBase>(memoryStream.ToArray()));
        }
    }
}
