using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace com.clusterrr.TuyaNet
{
    /// <summary>
    /// Provides access to Tuya Cloud API.
    /// </summary>
    public class TuyaApi
    {
        private readonly Region region;
        private readonly string accessId;
        private readonly string apiSecret;
        private readonly HttpClient httpClient;
        private TuyaToken token = null;
        private DateTime tokenTime = new DateTime();

        private class TuyaToken
        {
            [JsonProperty("access_token")]
            public string AccessToken { get; set; }

            [JsonProperty("expire_time")]
            public int ExpireTime { get; set; }

            [JsonProperty("refresh_token")]
            public string RefreshToken { get; set; }

            [JsonProperty("uid")]
            public string Uid { get; set; }
        }

        /// <summary>
        /// Creates a new instance of the TuyaApi class.
        /// </summary>
        /// <param name="region">Region of server.</param>
        /// <param name="accessId">Access ID/Client ID from https://iot.tuya.com/ .</param>
        /// <param name="apiSecret">API secret from https://iot.tuya.com/ .</param>
        public TuyaApi(Region region, string accessId, string apiSecret)
        {
            this.region = region;
            this.accessId = accessId;
            this.apiSecret = apiSecret;
            httpClient = new HttpClient();
        }

        /// <summary>
        /// Region of server.
        /// </summary>
        public enum Region
        {
            China,
            WesternAmerica,
            EasternAmerica,
            CentralEurope,
            WesternEurope,
            India
        }

        /// <summary>
        /// Request method.
        /// </summary>
        public enum Method
        {
            GET,
            POST,
            PUT,
            DELETE
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

        /// <summary>
        /// Request to official API.
        /// </summary>
        /// <param name="uri">Method URI.</param>
        /// <param name="body">Body of request if any.</param>
        /// <param name="headers">Additional headers.</param>
        /// <param name="noToken">Execute query without token.</param>
        /// <returns>JSON string with response.</returns>
        public async Task<string> RequestAsync(Method method, string uri, string body = null, Dictionary<string, string> headers = null, bool noToken = false)
        {
            while (uri.StartsWith("/")) uri = uri.Substring(1);
            var urlHost = RegionToHost(region);
            var url = new Uri($"https://{urlHost}/{uri}");
            var now = (DateTime.Now.ToUniversalTime() - new DateTime(1970, 1, 1)).TotalMilliseconds.ToString("0");
            string headersStr = "";
            if (headers == null)
            {
                headers = new Dictionary<string, string>();
            }
            else
            {
                headersStr = string.Concat(headers.Select(kv => $"{kv.Key}:{kv.Value}\n"));
                headers.Add("Signature-Headers", string.Join(":", headers.Keys));
            }

            string payload;
            if (noToken)
            {
                payload = accessId + now;
                headers["secret"] = apiSecret;
            }
            else
            {
                await RefreshAccessTokenAsync();
                payload = accessId + token.AccessToken + now;
            }

            using (var sha256 = SHA256.Create())
            {
                payload += $"{method}\n" +
                     string.Concat(sha256.ComputeHash(Encoding.UTF8.GetBytes(body ?? "")).Select(b => $"{b:x2}")) + '\n' +
                     headersStr + '\n' +
                     url.PathAndQuery;
            }

            string signature;
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiSecret)))
            {
                signature = string.Concat(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)).Select(b => $"{b:X2}"));
            }

            headers["client_id"] = accessId;
            headers["sign"] = signature;
            headers["t"] = now;
            headers["sign_method"] = "HMAC-SHA256";
            if (!noToken)
                headers["access_token"] = token.AccessToken;

            var httpRequestMessage = new HttpRequestMessage
            {
                Method = method switch
                {
                    Method.GET => HttpMethod.Get,
                    Method.POST => HttpMethod.Post,
                    Method.PUT => HttpMethod.Delete,
                    Method.DELETE => HttpMethod.Delete,
                    _ => throw new NotSupportedException($"Unknow method - {method}")
                },
                RequestUri = url,
            };
            foreach (var h in headers)
                httpRequestMessage.Headers.Add(h.Key, h.Value);
            if (body != null)
                httpRequestMessage.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using (var response = await httpClient.SendAsync(httpRequestMessage).ConfigureAwait(false))
            {
                var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var root = JObject.Parse(responseString);
                var success = root.GetValue("success").Value<bool>();
                if (!success) throw new InvalidDataException(root.ContainsKey("msg") ? root.GetValue("msg").Value<string>() : null);
                var result = root.GetValue("result").ToString();
                return result;
            }
        }

        /// <summary>
        /// Request access token if it's expired or not requested yet.
        /// </summary>
        private async Task RefreshAccessTokenAsync()
        {
            if ((token == null) || (tokenTime.AddSeconds(token.ExpireTime) >= DateTime.Now))
            {
                var uri = "v1.0/token?grant_type=1";
                var response = await RequestAsync(Method.GET, uri, noToken: true);
                token = JsonConvert.DeserializeObject<TuyaToken>(response);
                tokenTime = DateTime.Now;
            }
        }

        /// <summary>
        /// Requests info about device by it's ID.
        /// </summary>
        /// <param name="deviceId">Device ID.</param>
        /// <returns>Device info.</returns>
        public async Task<TuyaDeviceApiInfo> GetDeviceInfoAsync(string deviceId)
        {
            var uri = $"v1.0/devices/{deviceId}";
            var response = await RequestAsync(Method.GET, uri);
            var device = JsonConvert.DeserializeObject<TuyaDeviceApiInfo>(response);
            return device;
        }

        /// <summary>
        /// Requests info about all registered devices, requires ID of any registered device.
        /// </summary>
        /// <param name="anyDeviceId">ID of any registered device.</param>
        /// <returns>Array of devices info.</returns>
        public async Task<TuyaDeviceApiInfo[]> GetAllDevicesInfoAsync(string anyDeviceId)
        {
            var userId = (await GetDeviceInfoAsync(anyDeviceId)).UserId;
            var uri = $"v1.0/users/{userId}/devices";
            var response = await RequestAsync(Method.GET, uri);
            var devices = JsonConvert.DeserializeObject<TuyaDeviceApiInfo[]>(response);
            return devices;
        }
    }
}
