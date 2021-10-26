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
    public partial class TuyaDevice
    {
        public enum Version
        {
            V31,
            V33
        }

        private static byte[] PROTOCOL_VERSION_BYTES_31 = Encoding.ASCII.GetBytes("3.1");
        private static byte[] PROTOCOL_VERSION_BYTES_33 = Encoding.ASCII.GetBytes("3.3");
        private static byte[] PROTOCOL_33_HEADER = Enumerable.Concat(PROTOCOL_VERSION_BYTES_33, new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }).ToArray();
        private static byte[] PREFIX = new byte[] { 0, 0, 0x55, 0xAA };
        private static byte[] SUFFIX = { 0, 0, 0xAA, 0x55 };
        private static uint SeqNo = 0;

        public TuyaDevice(string ip, string localKey, Version protocolVersion = Version.V33, int port = 6668, int receiveTimeout = 250) 
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
        public Version ProtocolVersion { get; set; }
        public int ReceiveTimeout { get; set; }
        public bool PermanentConnection { get; set; } = false;

        private TcpClient client = null;

        private static IEnumerable<byte> BinEndian(IEnumerable<byte> seq) => BitConverter.IsLittleEndian ? seq.Reverse() : seq;
        
#if DEBUG
        static void Dump(byte[] data, string comment = "")
        {
            Console.WriteLine(comment);
            foreach (var c in data)
            {
                if (c < 0x80 && c >= 0x20)
                    Console.Write($"{(char)c}");
                else
                    Console.Write($"\\x{c:x2}");
                //Console.Write($"{c:X2}-'{(char)c}' ");
            }
            Console.WriteLine();
        }
#endif

        public byte[] CreatePayload(TuyaCommand command, string json)
        {
            byte[] payload = Encoding.UTF8.GetBytes(json);

            if (ProtocolVersion == Version.V33)
            {
                payload = Encrypt(payload); // Encrypt
                                            // Add protocol 3.3 header
                if ((command != TuyaCommand.DP_QUERY) && (command != TuyaCommand.UPDATED_PS))
                    payload = Enumerable.Concat(PROTOCOL_33_HEADER, payload).ToArray();
            }
            else if (command == TuyaCommand.CONTROL)
            {
                // Encrypt
                payload = Encrypt(payload);
                // Encode to base64
                string data64 = Convert.ToBase64String(payload);
                // Make string
                payload = Encoding.UTF8.GetBytes($"data={data64}||lpv=3.1||");
                using (var md5 = MD5.Create())
                using (var ms = new MemoryStream())
                {
                    // Calculate MD5 of data
                    ms.Write(payload, 0, payload.Length);
                    // ...and encryption key
                    var binaryKey = Encoding.UTF8.GetBytes(LocalKey);
                    ms.Write(binaryKey, 0, binaryKey.Length);

                    string md5s =
                        BitConverter.ToString( // Make string from MD5                            
                            md5.ComputeHash(ms.ToArray()) // Calculate MD5
                        )
                        .Replace("-", string.Empty) // Remove '-'                        
                        .Substring(8, 16)   // Get part of it                                          
                        .ToLower();         // Lowercase
                                            // Data with version & MD5 hash/signature
                    payload = Encoding.UTF8.GetBytes($"3.1{md5s}{data64}");
                }
            }

            using (var ms = new MemoryStream())
            {
                byte[] seqNo = BitConverter.GetBytes(SeqNo++);
                if (BitConverter.IsLittleEndian) Array.Reverse(seqNo); // Make big-endian
                byte[] dataLength = BitConverter.GetBytes(payload.Length + 8);
                if (BitConverter.IsLittleEndian) Array.Reverse(dataLength); // Make big-endian

                ms.Write(PREFIX, 0, 4);         // Prefix
                ms.Write(seqNo, 0, 4);          // Packet number
                ms.Write(new byte[] { 0, 0, 0, (byte)command }, 0, 4); // Command number
                ms.Write(dataLength, 0, 4);     // Length of data + length of suffix
                ms.Write(payload, 0, payload.Length);   // Data
                var crc32 = new Crc32();
                var crc = crc32.Get(ms.ToArray());
                byte[] crcBin = BitConverter.GetBytes(crc);
                if (BitConverter.IsLittleEndian) Array.Reverse(crcBin); // Make big-endian
                ms.Write(crcBin, 0, 4);         // CRC32 checksum
                ms.Write(SUFFIX, 0, 4);         // Suffix
                payload = ms.ToArray();
            }

            return payload;
        }

        public TuyaResponse DecodeResponse(byte[] data)
        {
            Dump(data, "Received");

            // Check length and prefix
            if (data.Length < 20 || !data.Take(PREFIX.Length).SequenceEqual(PREFIX))
            {
                throw new InvalidDataException("Invalid header/prefix");
            }
            // Check length
            int length = BitConverter.ToInt32(BinEndian(data.Skip(12).Take(4)).ToArray(), 0);
            if (data.Length != 16 + length)
            {
                throw new InvalidDataException("Invalid length");
            }
            // skip bytes 17-20 (unknown?)
            // Check suffix
            if (!data.Skip(16 + length - SUFFIX.Length).Take(SUFFIX.Length).SequenceEqual(SUFFIX))
            {
                throw new InvalidDataException("Invalid suffix");
            }

            // Packet number
            uint seq = BitConverter.ToUInt32(BinEndian(data.Skip(4).Take(4)).ToArray(), 0);
            // Command
            var command = (TuyaCommand)BitConverter.ToUInt32(BinEndian(data.Skip(8).Take(4)).ToArray(), 0);
            // Return code
            int returnCode = BitConverter.ToInt32(BinEndian(data.Skip(16).Take(4)).ToArray(), 0);
            // Data
            data = data.Skip(20).Take(length - 12).ToArray();

            var realVersion = ProtocolVersion;
            // Remove version 3.1 header
            if (data.Take(PROTOCOL_VERSION_BYTES_31.Length).SequenceEqual(PROTOCOL_VERSION_BYTES_31))
            {
                data = data.Skip(PROTOCOL_VERSION_BYTES_31.Length).ToArray();
                realVersion = Version.V31;
            }
            // Remove version 3.3 header
            if (data.Take(PROTOCOL_VERSION_BYTES_33.Length).SequenceEqual(PROTOCOL_VERSION_BYTES_33))
            {
                data = data.Skip(PROTOCOL_33_HEADER.Length).ToArray();
                realVersion = Version.V33;
            }

            if (realVersion == Version.V33)
            {
                Dump(data, "Decrypting");
                data = Decrypt(data);
            }

            if (data.Length == 0) 
                return new TuyaResponse(command, returnCode, null);

            var json = Encoding.UTF8.GetString(data);
            if (!json.StartsWith("{") || !json.EndsWith("}"))
                throw new InvalidDataException($"Response is not JSON: {json}");
            return new TuyaResponse(command, returnCode, json);
        }      

        private byte[] Encrypt(byte[] data)
        {
            var aes = new AesManaged()
            {
                Mode = CipherMode.ECB,
                Key = Encoding.UTF8.GetBytes(LocalKey)
            };
            using (var ms = new MemoryStream())
            using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                cs.Write(data, 0, data.Length);
                cs.Close();
                data = ms.ToArray(); // encrypt the data
            }
            return data;
        }

        private byte[] Decrypt(byte[] data)
        {
            var aes = new AesManaged()
            {
                Mode = CipherMode.ECB,
                Key = Encoding.UTF8.GetBytes(LocalKey)
            };
            using (var ms = new MemoryStream())
            using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
            {
                cs.Write(data, 0, data.Length);
                cs.Close();
                data = ms.ToArray(); // dencrypt the data
            }
            return data;
        }

        public async Task<TuyaResponse> Send(TuyaCommand command, string json, int tries = 2, int nullRetries = 1)
            => DecodeResponse(await Send(CreatePayload(command, json), tries, nullRetries));

        public async Task<byte[]> Send(byte[] data, int tries = 2, int nullRetries = 1)
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
                while ((ms.Length < 16) || ((length = BitConverter.ToInt32(BinEndian(ms.ToArray().Skip(12).Take(4)).ToArray(), 0) + 16) < ms.Length))
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
                    } else if (t == readTask)
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
