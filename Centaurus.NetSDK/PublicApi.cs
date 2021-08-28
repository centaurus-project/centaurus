using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Centaurus.NetSDK
{
    public static class PublicApi
    {
        public static async Task<ConstellationInfo> GetConstellationInfo(string alphaAddress, bool useSecureConnection)
        {
            if (!TryCreateUri(alphaAddress, useSecureConnection, out var uri))
                throw new ArgumentException("Invalid address");

            var uriBuilder = new UriBuilder(uri);
            uriBuilder.Scheme = useSecureConnection ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;

            return await GetConstellationInfo<ConstellationInfo>(uriBuilder.Uri);
        }

        private static bool TryCreateUri(string address, bool useSecureConnection, out Uri uri)
        {
            return Uri.TryCreate($"{(useSecureConnection ? Uri.UriSchemeHttps : Uri.UriSchemeHttp)}://{address}", UriKind.Absolute, out uri);
        }

        public static async Task<T> GetConstellationInfo<T>(Uri uri)
        {
            using (var httpClient = new HttpClient())
            {
                var res = await httpClient.GetAsync(new Uri(uri, "/api/constellation/info"));
                if (!res.IsSuccessStatusCode)
                    throw new Exception("Request failed with code: " + res.StatusCode);

                var rawJson = await res.Content.ReadAsStringAsync();

                var obj = JsonSerializer.Deserialize<T>(rawJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return obj;
            }
        }
    }
}
