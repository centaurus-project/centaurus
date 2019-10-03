using System;
using System.Linq;
using System.Threading.Tasks;
using Centaurus.Domain;
using Centaurus.Models;
using Microsoft.AspNetCore.Mvc;
using stellar_dotnet_sdk;

namespace Centaurus.Alpha.Controllers
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
                    Assets = assets
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

                var constellationInitializer = new ConstellationInitializer(
                    constellationInit.Auditors.Select(a => KeyPair.FromAccountId(a)),
                    constellationInit.MinAccountBalance,
                    constellationInit.MinAllowedLotSize,
                    constellationInit.Assets.Select(a => AssetSettings.FromCode(a))
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