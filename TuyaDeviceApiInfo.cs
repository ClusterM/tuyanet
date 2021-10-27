using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace com.clusterrr.TuyaNet
{
    /// <summary>
    /// Device info received from Tuya API.
    /// </summary>
    public class TuyaDeviceApiInfo
    {
        [JsonPropertyName("active_time")]
        public int ActiveTime { get; set; }

        [JsonPropertyName("biz_type")]
        public int BizType { get; set; }

        [JsonPropertyName("category")]
        public string Category { get; set; }

        [JsonPropertyName("create_time")]
        public int CreateTime { get; set; }

        [JsonPropertyName("icon")]
        public string Icon { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("ip")]
        public string Ip { get; set; }

        [JsonPropertyName("lat")]
        public string Lat { get; set; }

        [JsonPropertyName("local_key")]
        public string LocalKey { get; set; }

        [JsonPropertyName("lon")]
        public string Lon { get; set; }

        [JsonPropertyName("model")]
        public string Model { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("online")]
        public bool Online { get; set; }

        [JsonPropertyName("owner_id")]
        public string OwnerId { get; set; }

        [JsonPropertyName("product_id")]
        public string ProductId { get; set; }

        [JsonPropertyName("product_name")]
        public string ProductName { get; set; }

        [JsonPropertyName("status")]
        public List<TuyaDeviceStatus> Status { get; set; }

        [JsonPropertyName("sub")]
        public bool Sub { get; set; }

        [JsonPropertyName("time_zone")]
        public string TimeZone { get; set; }

        [JsonPropertyName("uid")]
        public string UserId { get; set; }

        [JsonPropertyName("update_time")]
        public int UpdateTime { get; set; }

        [JsonPropertyName("uuid")]
        public string Uuid { get; set; }

        public override string ToString() => Name;
    }
}
