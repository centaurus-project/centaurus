using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Centaurus.NetSDK
{
    public static class PublicApi
    {
        public static async Task<ConstellationInfo> GetConstellationInfo(ConstellationConfig constellationConfig)
        {
            return await GetAlphaEndpoint<ConstellationInfo>(constellationConfig);
        }

        private static async Task<T> GetAlphaEndpoint<T>(ConstellationConfig constellationConfig)
        {
            var origin = (constellationConfig.UseSecureConnection ? "https://" : "http://") +
                         constellationConfig.AlphaServerAddress;
            using (var httpClient = new HttpClient())
            {
                var res = await httpClient.GetAsync($"{origin}/api/constellation/info");
                if (!res.IsSuccessStatusCode)
                    throw new Exception("Request failed with code: " + res.StatusCode);

                var rawJson = await res.Content.ReadAsStringAsync();

                return JsonConvert.DeserializeObject<T>(rawJson);
            }
        }
    }
}
