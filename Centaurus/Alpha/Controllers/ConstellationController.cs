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
    public class ConstellationController : Controller
    {
        [HttpGet("[action]")]
        public ConstellationInfo Info()
        {
            ConstellationInfo info;
            if (((int)Global.AppState.State) < (int)ApplicationState.Running)
                info = new ConstellationInfo
                {
                    State = Global.AppState.State
                };
            else
            {
                var network = new ConstellationInfo.Network(
                    Global.StellarNetwork.Network.NetworkPassphrase,
                    Global.StellarNetwork.Horizon
                    );
                var assets = Global.Constellation.Assets.Select(a => ConstellationInfo.Asset.FromAssetSettings(a)).ToArray();
                info = new ConstellationInfo
                {
                    State = Global.AppState.State,
                    Vault = ((KeyPair)Global.Constellation.Vault).AccountId,
                    Auditors = Global.Constellation.Auditors.Select(a => ((KeyPair)a).AccountId).ToArray(),
                    MinAccountBalance = Global.Constellation.MinAccountBalance,
                    MinAllowedLotSize = Global.Constellation.MinAllowedLotSize,
                    StellarNetwork = network,
                    Assets = assets,
                    RequestRateLimits = Global.Constellation.RequestRateLimits
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
                    }
                );

                await constellationInitializer.Init();

                return new JsonResult(new { IsSuccess = true });
            }
            catch (Exception exc)
            {
                return new JsonResult(new { IsSuccess = false, Error = exc.Message });
            }
        }
    }
}