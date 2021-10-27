using System.Text.Json.Serialization;

namespace com.clusterrr.TuyaNet
{
    /// <summary>
    /// Currect device status.
    /// </summary>
    public class TuyaDeviceStatus
    {
        /// <summary>
        /// DPS number
        /// </summary>
        [JsonPropertyName("code")]
        public string Code { get; set; }

        /// <summary>
        /// DPS value.
        /// </summary>
        [JsonPropertyName("value")]
        public object Value { get; set; }
    }
}
