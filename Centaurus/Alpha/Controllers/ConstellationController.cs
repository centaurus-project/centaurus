using System;
using System.Linq;
using System.Threading.Tasks;
using Centaurus.Domain;
using Centaurus.Models;
using Microsoft.AspNetCore.Mvc;
using stellar_dotnet_sdk;

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
            if (state < (int)ApplicationState.Running)
                info = new ConstellationInfo
                {
                    State = (ApplicationState)state
                };
            else
            {
                var network = new ConstellationInfo.Network(
                   Context.StellarDataProvider.NetworkPassphrase,
                   Context.StellarDataProvider.Horizon
                    );
                var assets = Context.Constellation.Assets.Select(a => ConstellationInfo.Asset.FromAssetSettings(a)).ToArray();
                info = new ConstellationInfo
                {
                    State = Context.AppState.State,
                    Vaults = Context.Constellation.Vaults.ToDictionary(v => v.Provider.ToString(), v => v.AccountId.ToString()),
                    Auditors = Context.Constellation.Auditors.Select(a => ((KeyPair)a).AccountId).ToArray(),
                    MinAccountBalance = Context.Constellation.MinAccountBalance,
                    MinAllowedLotSize = Context.Constellation.MinAllowedLotSize,
                    StellarNetwork = network,
                    Assets = assets,
                    RequestRateLimits = Context.Constellation.RequestRateLimits
                };
            }

            return info;
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> Init([FromBody] ConstellationInitModel constellationInit)
        {
            try
            {
                if (constellationInit == null)
                    return StatusCode(415);

                if (constellationInit.RequestRateLimits == null)
                    throw new ArgumentNullException(nameof(constellationInit.RequestRateLimits), "RequestRateLimits parameter is required.");
                var requestRateLimits = new RequestRateLimits
                {
                    HourLimit = constellationInit.RequestRateLimits.HourLimit,
                    MinuteLimit = constellationInit.RequestRateLimits.MinuteLimit
                };

                var constellationInitializer = new ConstellationInitializer(
                    new ConstellationInitInfo
                    {
                        Auditors = constellationInit.Auditors.Select(a => KeyPair.FromAccountId(a)).ToArray(),
                        MinAccountBalance = constellationInit.MinAccountBalance,
                        MinAllowedLotSize = constellationInit.MinAllowedLotSize,
                        Assets = constellationInit.Assets.Select(a => AssetSettings.FromCode(a)).ToArray(),
                        RequestRateLimits = requestRateLimits
                    },
                    Context
                );

                await constellationInitializer.Init();

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