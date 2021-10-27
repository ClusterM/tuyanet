using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace com.clusterrr.TuyaNet
{
    /// <summary>
    /// Connection with Tuya device.
    /// </summary>
    public class TuyaDevice : IDisposable
    {
        /// <summary>
        /// Creates a new instance of the TuyaDevice class.
        /// </summary>
        /// <param name="ip">IP address of device.</param>
        /// <param name="localKey">Local key of device (obtained via API).</param>
        /// <param name="deviceId">Device ID.</param>
        /// <param name="protocolVersion">Protocol version.</param>
        /// <param name="port">TCP port of device.</param>
        /// <param name="receiveTimeout">Receive timeout.</param>
        public TuyaDevice(string ip, string localKey, string deviceId = null, TuyaProtocolVersion protocolVersion = TuyaProtocolVersion.V33, int port = 6668, int receiveTimeout = 250)
        {
            IP = ip;
            LocalKey = localKey;
            DeviceId = deviceId;
            ProtocolVersion = protocolVersion;
            Port = port;
            ReceiveTimeout = receiveTimeout;
        }

        /// <summary>
        /// IP address of device.
        /// </summary>
        public string IP { get; private set; }
        /// <summary>
        /// Local key of device.
        /// </summary>
        public string LocalKey { get; private set; }
        /// <summary>
        /// Device ID.
        /// </summary>
        public string DeviceId { get; private set; } = null;
        /// <summary>
        /// TCP port of device.
        /// </summary>
        public int Port { get; private set; } = 6668;
        /// <summary>
        /// Protocol version.
        /// </summary>
        public TuyaProtocolVersion ProtocolVersion { get; set; }
        /// <summary>
        /// Receive timeout.
        /// </summary>
        public int ReceiveTimeout { get; set; }
        /// <summary>
        /// Permanent connection (connect and stay connected).
        /// </summary>
        public bool PermanentConnection { get; set; } = false;

        private TcpClient client = null;

        /// <summary>
        /// Creates encoded and encrypted payload data from JSON string.
        /// </summary>
        /// <param name="command">Tuya command ID.</param>
        /// <param name="json">String with JSON to send.</param>
        /// <returns>Raw data.</returns>
        public byte[] CreatePayload(TuyaCommand command, string json)
            => TuyaParser.CreatePayload(command, json, Encoding.UTF8.GetBytes(LocalKey), ProtocolVersion);

        /// <summary>
        /// Parses and decrypts payload data from received bytes.
        /// </summary>
        /// <param name="data">.</param>
        /// <returns>Instance of TuyaLocalResponse.</returns>
        public TuyaLocalResponse DecodeResponse(byte[] data)            
            => TuyaParser.DecodeResponse(data, Encoding.UTF8.GetBytes(LocalKey), ProtocolVersion);

        /// <summary>
        /// Sends JSON string to device and reads response.
        /// </summary>
        /// <param name="command">Tuya command ID.</param>
        /// <param name="json">JSON string.</param>
        /// <param name="command">Tuya command ID.</param>
        /// <param name="json">String with JSON to send.</param>
        /// <returns>Parsed and decrypred received data as instance of TuyaLocalResponse.</returns>
        public async Task<TuyaLocalResponse> SendAsync(TuyaCommand command, string json, int tries = 2, int nullRetries = 1)
            => DecodeResponse(await SendAsync(CreatePayload(command, json), tries, nullRetries));

        /// <summary>
        /// Sends raw data over to device and read response.
        /// </summary>
        /// <param name="data">Raw data to send.</param>
        /// <param name="tries">Number of retries.</param>
        /// <param name="nullRetries">Number of retries in case of null answer.</param>
        /// <returns>Received data (raw).</returns>
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

        /// <summary>
        /// Requests current DPS status.
        /// </summary>
        /// <param name="deviceId">Device ID, required only if constuctor was called without it.</param>
        /// <returns>Dictionary of DPS numbers and values.</returns>
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
            var response = await SendAsync(TuyaCommand.CONTROL, requestJson, tries: 2, nullRetries: 1);
            if (string.IsNullOrEmpty(response.JSON))
                throw new InvalidDataException("Response is empty");
            var responseJson = JsonDocument.Parse(response.JSON);
            var dps = JsonSerializer.Deserialize<Dictionary<string, object>>(responseJson.RootElement.GetProperty("dps").ToString());
            return new Dictionary<int, object>(dps.Select(kv => new KeyValuePair<int, object>(int.Parse(kv.Key), kv.Value)));
        }

        /// <summary>
        /// Sets DPS to specified value.
        /// </summary>
        /// <param name="dpsNumber">DPS number.</param>
        /// <param name="value">Value.</param>
        /// <param name="deviceId">Device ID, required only if constuctor was called without it.</param>
        /// <returns></returns>
        public async Task<Dictionary<int, object>> SetDps(int dpsNumber, object value, string deviceId = null)
            => await SetDps(new Dictionary<int, object> { { dpsNumber, value } }, deviceId);

        /// <summary>
        /// Sets DPS to specified value.
        /// </summary>
        /// <param name="dps">Dictionary of DPS numbers and values to set.</param>
        /// <param name="deviceId">Device ID, required only if constuctor was called without it.</param>
        /// <returns></returns>
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
            var response = await SendAsync(TuyaCommand.CONTROL, requestJson, tries: 2, nullRetries: 1);
            if (string.IsNullOrEmpty(response.JSON))
                throw new InvalidDataException("Response is empty");
            var responseJson = JsonDocument.Parse(response.JSON);
            var newDps = JsonSerializer.Deserialize<Dictionary<string, object>>(responseJson.RootElement.GetProperty("dps").ToString());
            return new Dictionary<int, object>(newDps.Select(kv => new KeyValuePair<int, object>(int.Parse(kv.Key), kv.Value)));
        }

        /// <summary>
        /// Disposes object.
        /// </summary>
        public void Dispose()
        {
            client?.Close();
            client?.Dispose();
            client = null;
        }
    }
}
