using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace com.clusterrr.TuyaNet
{
    public class TuyaApi
    {
        private readonly Region region;
        private readonly string apiKey;
        private readonly string apiSecret;
        private readonly HttpClient httpClient;
        private TuyaToken token = null;
        private DateTime tokenTime = new DateTime();

        public class TuyaToken
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; }

            [JsonPropertyName("expire_time")]
            public int ExpireTime { get; set; }

            [JsonPropertyName("refresh_token")]
            public string RefreshToken { get; set; }

            [JsonPropertyName("uid")]
            public string Uid { get; set; }
        }

        public TuyaApi(Region region, string apiKey, string apiSecret)
        {
            this.region = region;
            this.apiKey = apiKey;
            this.apiSecret = apiSecret;
            httpClient = new HttpClient();
        }

        public enum Region
        {
            China,
            WesternAmerica,
            EasternAmerica,
            CentralEurope,
            WesternEurope,
            India
        }

        private static string RegionToHost(Region region)
        {
            string urlHost = null;
            switch (region)
            {
                case Region.China:
                    urlHost = "openapi.tuyacn.com";
                    break;
                case Region.WesternAmerica:
                    urlHost = "openapi.tuyaus.com";
                    break;
                case Region.EasternAmerica:
                    urlHost = "openapi-ueaz.tuyaus.com";
                    break;
                case Region.CentralEurope:
                    urlHost = "openapi.tuyaeu.com";
                    break;
                case Region.WesternEurope:
                    urlHost = "openapi-weaz.tuyaeu.com";
                    break;
                case Region.India:
                    urlHost = "openapi.tuyain.com";
                    break;
            }
            return urlHost;
        }

        public async Task<string> RequestAsync(string uri, string body = null, Dictionary<string, string> headers = null, bool noToken = false)
        {
            var urlHost = RegionToHost(region);
            var url = new Uri($"https://{urlHost}/v1.0/{uri}");
            var now = (DateTime.Now.ToUniversalTime() - new DateTime(1970, 1, 1)).TotalMilliseconds.ToString("0");
            string headersStr = "";
            if (headers == null)
            {
                headers = new Dictionary<string, string>();
            }
            else
            {
                headersStr = string.Concat(headers.Select(kv => $"{kv.Key}:{kv.Value}\n"));
                headers.Add("Signature-Headers", string.Join(':', headers.Keys));
            }

            string payload;
            if (noToken)
            {
                payload = apiKey + now;
                headers["secret"] = apiSecret;
            }
            else
            {
                await RefreshAccessToken();
                payload = apiKey + token.AccessToken + now;
            }

            using (var sha256 = SHA256.Create())
            {
                payload += "GET\n" +
                 string.Concat(sha256.ComputeHash(Encoding.UTF8.GetBytes(body ?? "")).Select(b => $"{b:x2}")) + '\n' +
                 headersStr + '\n' +
                 url.PathAndQuery;
            }

            string signature;
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiSecret)))
            {
                signature = string.Concat(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)).Select(b => $"{b:X2}"));
            }

            headers["client_id"] = apiKey;
            headers["sign"] = signature;
            headers["t"] = now;
            headers["sign_method"] = "HMAC-SHA256";
            if (!noToken)
                headers["access_token"] = token.AccessToken;

            var httpRequestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = url
            };
            foreach (var h in headers)
                httpRequestMessage.Headers.Add(h.Key, h.Value);

            using (var response = await httpClient.SendAsync(httpRequestMessage).ConfigureAwait(false))
            {
                var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var root = JsonDocument.Parse(responseString).RootElement;
                var success = root.GetProperty("success").GetBoolean();
                if (!success) throw new InvalidDataException(root.GetProperty("msg").GetString());
                var result = root.GetProperty("result").ToString();
                return result;
            }
        }

        private async Task<TuyaToken> GetAccessTokenAsync()
        {
            var uri = "token?grant_type=1";
            var response = await RequestAsync(uri, noToken: true);
            var token = JsonSerializer.Deserialize<TuyaToken>(response);
            return token;
        }

        private async Task RefreshAccessToken()
        {
            if ((token == null) || tokenTime.AddSeconds(token.ExpireTime) >= DateTime.Now)
            {
                token = await GetAccessTokenAsync();
                tokenTime = DateTime.Now;
            }
        }

        public async Task<TuyaDeviceApiInfo> GetDeviceInfoAsync(string deviceId)
        {
            var uri = $"devices/{deviceId}";
            var response = await RequestAsync(uri);
            var device = JsonSerializer.Deserialize<TuyaDeviceApiInfo>(response);
            return device;
        }

        public async Task<TuyaDeviceApiInfo[]> GetAllDevicesInfoAsync(string anyDeviceId)
        {
            var userId = (await GetDeviceInfoAsync(anyDeviceId)).UserId;
            var uri = $"users/{userId}/devices";
            var response = await RequestAsync(uri);
            var devices = JsonSerializer.Deserialize<TuyaDeviceApiInfo[]>(response);
            return devices;
        }
    }
}
