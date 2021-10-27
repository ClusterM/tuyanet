using System;
using System.Text.Json.Serialization;

namespace com.clusterrr.TuyaNet
{
    /// <summary>
    /// Device info received from local network.
    /// </summary>
    public class TuyaDeviceScanInfo : IEquatable<TuyaDeviceScanInfo>
    {
        [JsonPropertyName("ip")]
        public string IP { get; set; } = null;

        [JsonPropertyName("gwId")]
        public string GwId { get; set; } = null;

        [JsonPropertyName("active")]
        public int Active { get; set; } = 0;

        [JsonPropertyName("ability")]
        public int Ability { get; set; } = 0;

        [JsonPropertyName("mode")]
        public int Mode { get; set; } = 0;

        [JsonPropertyName("encrypt")]
        public bool Encryption { get; set; } = false;

        [JsonPropertyName("productKey")]
        public string ProductKey { get; set; } = null;

        [JsonPropertyName("version")]
        public string Version { get; set; } = null;

        public bool Equals(TuyaDeviceScanInfo other)
            => (IP == other.IP) && (GwId == other.GwId);

        public override string ToString()
            => $"IP: {IP}, gwId: {GwId}, product key: {ProductKey}, encryption: {Encryption}, version: {Version}";
    }

}
