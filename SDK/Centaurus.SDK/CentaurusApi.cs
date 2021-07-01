using Centaurus.SDK.Models;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Centaurus.SDK
{
    public static class CentaurusApi
    {
        public static async Task<ConstellationInfo> GetConstellationInfo(Uri alphaUri)
        {
            using (var httpClient = new HttpClient())
            {
                var res = await httpClient.GetAsync($"{alphaUri.AbsoluteUri.TrimEnd('/')}/api/constellation/info");
                if (!res.IsSuccessStatusCode)
                    throw new Exception("Request failed with code: " + res.StatusCode);

                var rawJson = await res.Content.ReadAsStringAsync();

                return JsonSerializer.Deserialize<ConstellationInfo>(rawJson);
            }
        }
    }
}
