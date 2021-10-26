using System;
using System.Text.Json.Serialization;

namespace com.clusterrr.TuyaNet
{
    public class TuyaDeviceInfo : IEquatable<TuyaDeviceInfo>
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

        public string LocalKey { get; set; } = null;

        public bool Equals(TuyaDeviceInfo other)
            => (IP == other.IP) && (GwId == other.GwId);

        public override string ToString()
            => $"IP: {IP}, gwId: {GwId}, product key: {ProductKey}, encryption: {Encryption}, version: {Version}";
    }

}
