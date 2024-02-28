using System.Text.Json;
using com.clusterrr.TuyaNet.Models;

namespace TuyaNet.Console
{
    public static class Database
    {
        public static TuyaDeviceInfo[] Devices { get; set; }

        public static void LoadFromFile(string path)
        {
            var fileBytes = File.ReadAllBytes(path);
            var readOnlySpan = new ReadOnlySpan<byte>(fileBytes);
            Devices = JsonSerializer.Deserialize<TuyaDeviceInfo[]>(readOnlySpan)!;
        }
    }
}