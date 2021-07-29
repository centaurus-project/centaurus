using System;
using System.Linq;
using System.Threading.Tasks;
using Centaurus.Domain;
using Centaurus.Models;
using Microsoft.AspNetCore.Mvc;
using NSec.Cryptography;

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
            ConstellationInfo info;

            var state = (int)(Context.AppState?.State ?? 0);
            if (state < (int)State.Running)
                info = new ConstellationInfo
                {
                    State = (State)state
                };
            else
            {
                info = new ConstellationInfo
                {
                    State = Context.AppState.State,
                    Providers = Context.Constellation.Providers.ToArray(),
                    Auditors = Context.Constellation.Auditors
                        .Select(a => new { PubKey = a.PubKey.GetAccountId(), Address = a.Address })
                        .ToArray(),
                    MinAccountBalance = Context.Constellation.MinAccountBalance,
                    MinAllowedLotSize = Context.Constellation.MinAllowedLotSize,
                    Assets = Context.Constellation.Assets.ToArray(),
                    RequestRateLimits = Context.Constellation.RequestRateLimits
                };
            }

            return info;
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> Init(MessageEnvelope constellationInitEnvelope)
        {
            try
            {
                if (constellationInitEnvelope == null)
                    return StatusCode(415);

                var constellationInitRequest = (ConstellationUpdate)constellationInitEnvelope.Message;

                await Context.QuantumHandler.HandleAsync(constellationInitEnvelope);

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