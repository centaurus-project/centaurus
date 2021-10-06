using Centaurus.Domain;
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
            ConstellationInfo info;

            var state = -1;
            if (Context.StateManager != null)
                state = (int)Context.StateManager.State;
            if (state < (int)State.Running)
                info = new ConstellationInfo
                {
                    State = (State)state
                };
            else
            {
                info = new ConstellationInfo
                {
                    State = Context.StateManager.State,
                    Providers = Context.Constellation.Providers.ToArray(),
                    Auditors = Context.Constellation.Auditors
                        .Select(a => new ConstellationInfo.Auditor { PubKey = a.PubKey.GetAccountId(), Address = a.Address })
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

                var constellationQuantum = new ConstellationQuantum { RequestEnvelope = constellationInitEnvelope };

                constellationQuantum.Validate(Context);

                var quantumProcessingItem = Context.QuantumHandler.HandleAsync(constellationQuantum, Task.FromResult(true));

                await quantumProcessingItem.OnAcknowledge;

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