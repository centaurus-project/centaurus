using Centaurus.Domain;
using Centaurus.Domain.Models;
using Centaurus.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Centaurus.Controllers
{
    [Route("api/[controller]")]
    public class ConstellationController : Controller, IContextual
    {
        public ConstellationController(ExecutionContext centaurusContext)
        {
            Context = centaurusContext;
        }

        public ExecutionContext Context { get; }

        [HttpGet("[action]")]
        public ConstellationInfo Info()
        {
            return Context.GetInfo();
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> Init(ConstellationMessageEnvelope constellationInitEnvelope)
        {
            return await Update(constellationInitEnvelope);
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> Update(ConstellationMessageEnvelope constellationInitEnvelope)
        {
            try
            {
                if (constellationInitEnvelope == null)
                    return StatusCode(415);

                await Context.HandleConstellationQuantum(constellationInitEnvelope);

                return new JsonResult(new InitResult { IsSuccess = true });
            }
            catch (Exception exc)
            {
                return new JsonResult(new InitResult { IsSuccess = false, Error = exc.Message });
            }
        }

        public class InitResult
        {
            public bool IsSuccess { get; set; }

            public string Error { get; set; }
        }
    }
}