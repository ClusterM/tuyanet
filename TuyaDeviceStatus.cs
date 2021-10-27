using System.Text.Json.Serialization;

namespace com.clusterrr.TuyaNet
{
    public class TuyaDeviceStatus
    {
        [JsonPropertyName("code")]
        public string Code { get; set; }

        [JsonPropertyName("value")]
        public object Value { get; set; }
    }
}
