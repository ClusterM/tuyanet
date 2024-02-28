using Newtonsoft.Json;
using System.Collections.Generic;

namespace com.clusterrr.TuyaNet
{
    /// <summary>
    /// Device info received from Tuya API.
    /// </summary>
    public class TuyaDeviceApiInfo
    {
        [JsonProperty("active_time")]
        public int ActiveTime { get; set; }

        [JsonProperty("biz_type")]
        public int BizType { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("create_time")]
        public int CreateTime { get; set; }

        [JsonProperty("icon")]
        public string Icon { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("ip")]
        public string Ip { get; set; }

        [JsonProperty("lat")]
        public string Lat { get; set; }

        [JsonProperty("local_key")]
        public string LocalKey { get; set; }

        [JsonProperty("lon")]
        public string Lon { get; set; }

        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("online")]
        public bool Online { get; set; }

        [JsonProperty("owner_id")]
        public string OwnerId { get; set; }

        [JsonProperty("product_id")]
        public string ProductId { get; set; }

        [JsonProperty("product_name")]
        public string ProductName { get; set; }

        [JsonProperty("status")]
        public List<TuyaDeviceStatus> Status { get; set; }

        [JsonProperty("sub")]
        public bool Sub { get; set; }

        [JsonProperty("time_zone")]
        public string TimeZone { get; set; }

        [JsonProperty("uid")]
        public string UserId { get; set; }

        [JsonProperty("update_time")]
        public int UpdateTime { get; set; }

        [JsonProperty("uuid")]
        public string Uuid { get; set; }

        public override string ToString() => Name;
    }
}
