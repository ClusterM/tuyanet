using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace com.clusterrr.TuyaNet
{
    public class TuyaDevice
    {
        public TuyaDevice(string ip, string localKey, TuyaProtocolVersion protocolVersion = TuyaProtocolVersion.V33, int port = 6668, int receiveTimeout = 250)
        {
            IP = ip;
            LocalKey = localKey;
            ProtocolVersion = protocolVersion;
            Port = port;
            ReceiveTimeout = receiveTimeout;
        }

        public string IP { get; set; }
        public string LocalKey { get; set; }
        public int Port { get; set; } = 6668;
        public TuyaProtocolVersion ProtocolVersion { get; set; }
        public int ReceiveTimeout { get; set; }
        public bool PermanentConnection { get; set; } = false;

        private TcpClient client = null;

        public byte[] CreatePayload(TuyaCommand command, string json)
            => TuyaParser.CreatePayload(command, json, Encoding.UTF8.GetBytes(LocalKey), ProtocolVersion);
        public TuyaLocalResponse DecodeResponse(byte[] data)
            => TuyaParser.DecodeResponse(data, Encoding.UTF8.GetBytes(LocalKey), ProtocolVersion);     
        public async Task<TuyaLocalResponse> Send(TuyaCommand command, string json, int tries = 2, int nullRetries = 1)
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
                catch (IOException ex)
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
    }
}
