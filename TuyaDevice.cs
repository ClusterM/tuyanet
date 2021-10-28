using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
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
        public TuyaDevice(string ip, string localKey, string deviceId, TuyaProtocolVersion protocolVersion = TuyaProtocolVersion.V33, int port = 6668, int receiveTimeout = 250)
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
        public string DeviceId { get; private set; }
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
        /// Fills JSON string with base fields required by most commands.
        /// </summary>
        /// <param name="json">JSON string</param>
        /// <param name="addGwId">Add "gwId" field with device ID.</param>
        /// <param name="addDevId">Add "devId" field with device ID.</param>
        /// <param name="addUid">Add "uid" field with device ID.</param>
        /// <param name="addTime">Add "time" field with current timestamp.</param>
        /// <returns>JSON string with added fields.</returns>
        public string FillJson(string json, bool addGwId = true, bool addDevId = true, bool addUid = true, bool addTime = true)
        {
            if (string.IsNullOrEmpty(json))
                json = "{}";
            var root = JObject.Parse(json);
            if ((addGwId || addDevId || addUid) && string.IsNullOrWhiteSpace(DeviceId))
                throw new ArgumentNullException("deviceId", "Device ID can't be null.");
            if (addTime && !root.ContainsKey("t"))
                root.AddFirst(new JProperty("t", (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds.ToString("0")));
            if (addUid && !root.ContainsKey("uid"))
                root.AddFirst(new JProperty("uid", DeviceId));
            if (addDevId && !root.ContainsKey("devId"))
                root.AddFirst(new JProperty("devId", DeviceId));
            if (addGwId && !root.ContainsKey("gwId"))
                root.AddFirst(new JProperty("gwId", DeviceId));
            return root.ToString();
        }

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
        /// <param name="data">Raw data to parse and decrypt.</param>
        /// <returns>Instance of TuyaLocalResponse.</returns>
        public TuyaLocalResponse DecodeResponse(byte[] data)
            => TuyaParser.DecodeResponse(data, Encoding.UTF8.GetBytes(LocalKey), ProtocolVersion);

        /// <summary>
        /// Sends JSON string to device and reads response.
        /// </summary>
        /// <param name="command">Tuya command ID.</param>
        /// <param name="json">JSON string.</param>
        /// <param name="retries">Number of retries in case of network error.</param>
        /// <param name="nullRetries">Number of retries in case of empty answer.</param>
        /// <returns>Parsed and decrypred received data as instance of TuyaLocalResponse.</returns>
        public async Task<TuyaLocalResponse> SendAsync(TuyaCommand command, string json, int retries = 2, int nullRetries = 1)
            => DecodeResponse(await SendAsync(CreatePayload(command, json), retries, nullRetries));

        /// <summary>
        /// Sends raw data over to device and read response.
        /// </summary>
        /// <param name="data">Raw data to send.</param>
        /// <param name="retries">Number of retries in case of network error.</param>
        /// <param name="nullRetries">Number of retries in case of empty answer.</param>
        /// <returns>Received data (raw).</returns>
        public async Task<byte[]> SendAsync(byte[] data, int retries = 2, int nullRetries = 1)
        {
            Exception lastException = null;
            while (retries-- > 0)
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
                catch (IOException ex)
                {
                    // sockets sometimes drop the connection unexpectedly, so let's 
                    // retry at least once
                    lastException = ex;
                }
                catch (TimeoutException ex)
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
        /// <returns>Dictionary of DPS numbers and values.</returns>
        public async Task<Dictionary<int, object>> GetDps()
        {
            var requestJson = FillJson(null);
            var response = await SendAsync(TuyaCommand.DP_QUERY, requestJson, retries: 2, nullRetries: 1);
            if (string.IsNullOrEmpty(response.JSON))
                throw new InvalidDataException("Response is empty");
            var root = JObject.Parse(response.JSON);
            var dps = JsonConvert.DeserializeObject<Dictionary<string, object>>(root.GetValue("dps").ToString());
            return dps.ToDictionary(kv => int.Parse(kv.Key), kv => kv.Value);
        }

        /// <summary>
        /// Sets DPS to specified value.
        /// </summary>
        /// <param name="dpsNumber">DPS number.</param>
        /// <param name="value">Value.</param>
        /// <returns></returns>
        public async Task<Dictionary<int, object>> SetDps(int dpsNumber, object value)
            => await SetDps(new Dictionary<int, object> { { dpsNumber, value } });

        /// <summary>
        /// Sets DPS to specified value.
        /// </summary>
        /// <param name="dps">Dictionary of DPS numbers and values to set.</param>
        /// <returns></returns>
        public async Task<Dictionary<int, object>> SetDps(Dictionary<int, object> dps)
        {
            var cmd = new Dictionary<string, object>
            {
                { "dps",  dps }
            };
            string requestJson = JsonConvert.SerializeObject(cmd);
            requestJson = FillJson(requestJson);
            var response = await SendAsync(TuyaCommand.CONTROL, requestJson, retries: 2, nullRetries: 1);
            if (string.IsNullOrEmpty(response.JSON))
                throw new InvalidDataException("Response is empty");
            var root = JObject.Parse(response.JSON);
            var newDps = JsonConvert.DeserializeObject<Dictionary<string, object>>(root.GetValue("dps").ToString());
            return newDps.ToDictionary(kv => int.Parse(kv.Key), kv => kv.Value);
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
