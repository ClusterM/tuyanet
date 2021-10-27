using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace com.clusterrr.TuyaNet
{
    public class TuyaDevice : IDisposable
    {
        public TuyaDevice(string ip, string localKey, string deviceId = null, TuyaProtocolVersion protocolVersion = TuyaProtocolVersion.V33, int port = 6668, int receiveTimeout = 250)
        {
            IP = ip;
            LocalKey = localKey;
            DeviceId = deviceId;
            ProtocolVersion = protocolVersion;
            Port = port;
            ReceiveTimeout = receiveTimeout;
        }

        public string IP { get; private set; }
        public string LocalKey { get; private set; }
        public int Port { get; private set; } = 6668;
        public string DeviceId { get; private set; } = null;
        public TuyaProtocolVersion ProtocolVersion { get; set; }
        public int ReceiveTimeout { get; set; }
        public bool PermanentConnection { get; set; } = false;

        private TcpClient client = null;

        public byte[] CreatePayload(TuyaCommand command, string json)
            => TuyaParser.CreatePayload(command, json, Encoding.UTF8.GetBytes(LocalKey), ProtocolVersion);

        public TuyaLocalResponse DecodeResponse(byte[] data)

            => TuyaParser.DecodeResponse(data, Encoding.UTF8.GetBytes(LocalKey), ProtocolVersion);
        public async Task<TuyaLocalResponse> SendAsync(TuyaCommand command, string json, int tries = 2, int nullRetries = 1)
            => DecodeResponse(await SendAsync(CreatePayload(command, json), tries, nullRetries));

        public async Task<byte[]> SendAsync(byte[] data, int tries = 2, int nullRetries = 1)
        {
            Exception lastException = null;
            while (tries-- > 0)
            {
                if (!PermanentConnection || (client?.Connected == false))
                {
                    client?.Close();
                    client?.Dispose();
                    client = null;
                }
                try
                {
                    if (client == null)
                        client = new TcpClient(IP, Port);
                    var stream = client.GetStream();
                    await stream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
                    return await Receive(stream, nullRetries);
                }
                catch (Exception ex) when (ex is IOException or TimeoutException)
                {
                    // sockets sometimes drop the connection unexpectedly, so let's 
                    // retry at least once
                    lastException = ex;
                }
                finally
                {
                    if (!PermanentConnection || (client?.Connected == false) || (lastException != null))
                    {
                        client?.Close();
                        client?.Dispose();
                        client = null;
                    }
                }
                await Task.Delay(500);
            }
            throw lastException;
        }

        private async Task<byte[]> Receive(NetworkStream stream, int nullRetries = 1)
        {
            byte[] result;
            byte[] buffer = new byte[1024];
            using (var ms = new MemoryStream())
            {
                int length = buffer.Length;
                while ((ms.Length < 16) || ((length = BitConverter.ToInt32(TuyaParser.BigEndian(ms.ToArray().Skip(12).Take(4)).ToArray(), 0) + 16) < ms.Length))
                {
                    var cancellationTokenSource = new CancellationTokenSource();
                    var readTask = stream.ReadAsync(buffer, 0, length, cancellationToken: cancellationTokenSource.Token);
                    var timeoutTask = Task.Delay(ReceiveTimeout, cancellationToken: cancellationTokenSource.Token);
                    var t = await Task.WhenAny(readTask, timeoutTask).ConfigureAwait(false);
                    cancellationTokenSource.Cancel();
                    int bytes = 0;
                    if (t == timeoutTask)
                    {
                        if (stream.DataAvailable)
                            bytes = await stream.ReadAsync(buffer, 0, length);
                        else
                            throw new TimeoutException();
                    }
                    else if (t == readTask)
                    {
                        bytes = await readTask;
                    }
                    ms.Write(buffer, 0, bytes);
                }
                result = ms.ToArray();
            }
            if ((result.Length <= 28) && (nullRetries > 0)) // empty response
            {
                try
                {
                    result = await Receive(stream, nullRetries - 1);
                }
                catch { }
            }
            return result;
        }

        public async Task<Dictionary<int, object>> GetDps(string deviceId = null)
        {
            deviceId = deviceId ?? DeviceId;
            if (string.IsNullOrEmpty(deviceId))
                throw new ArgumentException("deviceId is not specified", "deviceId");
            var cmd = new Dictionary<string, object>
            {
                { "gwId", deviceId },
                { "devId", deviceId },
                { "uid", deviceId },
                { "t", (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds.ToString("0") }
            };
            string requestJson = JsonSerializer.Serialize(cmd);
            var response = await SendAsync(TuyaCommand.CONTROL, requestJson, tries: 2, nullRetries: 2);
            if (string.IsNullOrEmpty(response.JSON))
                throw new InvalidDataException("Response is empty");
            var responseJson = JsonDocument.Parse(response.JSON);
            var dps = JsonSerializer.Deserialize<Dictionary<string, object>>(responseJson.RootElement.GetProperty("dps").ToString());
            return new Dictionary<int, object>(dps.Select(kv => new KeyValuePair<int, object>(int.Parse(kv.Key), kv.Value)));
        }

        public async Task<Dictionary<int, object>> SetDps(int dpsNumber, object value, string deviceId = null)
            => await SetDps(new Dictionary<int, object> { { dpsNumber, value } }, deviceId);

        public async Task<Dictionary<int, object>> SetDps(Dictionary<int, object> dps, string deviceId = null)
        {
            deviceId = deviceId ?? DeviceId;
            if (string.IsNullOrEmpty(deviceId))
                throw new ArgumentException("deviceId is not specified", "deviceId");
            var cmd = new Dictionary<string, object>
            {
                { "gwId", deviceId },
                { "devId", deviceId },
                { "uid", deviceId },
                { "t", (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds.ToString("0") },
                { "dps",  dps }
            };
            string requestJson = JsonSerializer.Serialize(cmd);
            var response = await SendAsync(TuyaCommand.CONTROL, requestJson, tries: 2, nullRetries: 2);
            if (string.IsNullOrEmpty(response.JSON))
                throw new InvalidDataException("Response is empty");
            var responseJson = JsonDocument.Parse(response.JSON);
            var newDps = JsonSerializer.Deserialize<Dictionary<string, object>>(responseJson.RootElement.GetProperty("dps").ToString());
            return new Dictionary<int, object>(newDps.Select(kv => new KeyValuePair<int, object>(int.Parse(kv.Key), kv.Value)));
        }

        public void Dispose()
        {
            client?.Close();
            client?.Dispose();
            client = null;
        }
    }
}
