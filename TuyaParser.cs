using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace com.clusterrr.TuyaNet
{
    /// <summary>
    /// Class to encode and decode data sent over local network.
    /// </summary>
    internal static class TuyaParser
    {
        private static byte[] PROTOCOL_VERSION_BYTES_31 = Encoding.ASCII.GetBytes("3.1");
        private static byte[] PROTOCOL_VERSION_BYTES_33 = Encoding.ASCII.GetBytes("3.3");
        private static byte[] PROTOCOL_33_HEADER = Enumerable.Concat(PROTOCOL_VERSION_BYTES_33, new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }).ToArray();
        private static byte[] PREFIX = new byte[] { 0, 0, 0x55, 0xAA };
        private static byte[] SUFFIX = { 0, 0, 0xAA, 0x55 };
        private static uint SeqNo = 0;

        internal static IEnumerable<byte> BigEndian(IEnumerable<byte> seq) => BitConverter.IsLittleEndian ? seq.Reverse() : seq;

        internal static byte[] Encrypt(byte[] data, byte[] key)
        {
            var aes = new AesManaged()
            {
                Mode = CipherMode.ECB,
                Key = key
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

        internal static byte[] Decrypt(byte[] data, byte[] key)
        {
            var aes = new AesManaged()
            {
                Mode = CipherMode.ECB,
                Key = key
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

        internal static byte[] EncodeRequest(TuyaCommand command, string json, byte[] key, TuyaProtocolVersion protocolVersion = TuyaProtocolVersion.V33)
        {
            // Remove spaces and newlines
            var root = JObject.Parse(json);
            json = root.ToString(Newtonsoft.Json.Formatting.None);

            byte[] payload = Encoding.UTF8.GetBytes(json);

            if (protocolVersion == TuyaProtocolVersion.V33)
            {
                // Encrypt
                payload = Encrypt(payload, key);
                // Add protocol 3.3 header
                if ((command != TuyaCommand.DP_QUERY) && (command != TuyaCommand.UPDATED_PS))
                    payload = Enumerable.Concat(PROTOCOL_33_HEADER, payload).ToArray();
            }
            else if (command == TuyaCommand.CONTROL)
            {
                // Encrypt
                payload = Encrypt(payload, key);
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
                    ms.Write(key, 0, key.Length);
                    string md5s =
                        BitConverter.ToString(              // Make string from MD5                            
                            md5.ComputeHash(ms.ToArray())   // Calculate MD5
                        )
                        .Replace("-", string.Empty)         // Remove '-'
                        .Substring(8, 16)                   // Get part of it                                          
                        .ToLower();                         // Lowercase
                    // Data with protocol header, MD5 hash and data
                    payload = Encoding.UTF8.GetBytes($"3.1{md5s}{data64}");
                }
            }

            using (var ms = new MemoryStream())
            {
                byte[] seqNo = BitConverter.GetBytes(SeqNo++);
                if (BitConverter.IsLittleEndian) Array.Reverse(seqNo);  // Make big-endian
                byte[] dataLength = BitConverter.GetBytes(payload.Length + 8);
                if (BitConverter.IsLittleEndian) Array.Reverse(dataLength); // Make big-endian

                ms.Write(PREFIX, 0, 4);                                 // Prefix
                ms.Write(seqNo, 0, 4);                                  // Packet number
                ms.Write(new byte[] { 0, 0, 0, (byte)command }, 0, 4);  // Command number
                ms.Write(dataLength, 0, 4);                             // Length of data + length of suffix
                ms.Write(payload, 0, payload.Length);                   // Data
                var crc32 = new Crc32();
                var crc = crc32.Get(ms.ToArray());
                byte[] crcBin = BitConverter.GetBytes(crc);
                if (BitConverter.IsLittleEndian) Array.Reverse(crcBin); // Make big-endian
                ms.Write(crcBin, 0, 4);                                 // CRC32 checksum
                ms.Write(SUFFIX, 0, 4);                                 // Suffix
                payload = ms.ToArray();
            }

            return payload;
        }

        internal static TuyaLocalResponse DecodeResponse(byte[] data, byte[] key, TuyaProtocolVersion protocolVersion = TuyaProtocolVersion.V33)
        {
            // Check length and prefix
            if (data.Length < 20 || !data.Take(PREFIX.Length).SequenceEqual(PREFIX))
            {
                throw new InvalidDataException("Invalid header/prefix");
            }
            // Check length
            int length = BitConverter.ToInt32(BigEndian(data.Skip(12).Take(4)).ToArray(), 0);
            if (data.Length != 16 + length)
            {
                throw new InvalidDataException("Invalid length");
            }
            // Check suffix
            if (!data.Skip(16 + length - SUFFIX.Length).Take(SUFFIX.Length).SequenceEqual(SUFFIX))
            {
                throw new InvalidDataException("Invalid suffix");
            }

            // Packet number
            // uint seq = BitConverter.ToUInt32(BinEndian(data.Skip(4).Take(4)).ToArray(), 0);
            // Command
            var command = (TuyaCommand)BitConverter.ToUInt32(BigEndian(data.Skip(8).Take(4)).ToArray(), 0);
            // Return code
            int returnCode = BitConverter.ToInt32(BigEndian(data.Skip(16).Take(4)).ToArray(), 0);
            // Data
            data = data.Skip(20).Take(length - 12).ToArray();

            var realVersion = protocolVersion;
            // Remove version 3.1 header
            if (data.Take(PROTOCOL_VERSION_BYTES_31.Length).SequenceEqual(PROTOCOL_VERSION_BYTES_31))
            {
                data = data.Skip(PROTOCOL_VERSION_BYTES_31.Length).ToArray();
                realVersion = TuyaProtocolVersion.V31;
            }
            // Remove version 3.3 header
            if (data.Take(PROTOCOL_VERSION_BYTES_33.Length).SequenceEqual(PROTOCOL_VERSION_BYTES_33))
            {
                data = data.Skip(PROTOCOL_33_HEADER.Length).ToArray();
                realVersion = TuyaProtocolVersion.V33;
            }

            if (realVersion == TuyaProtocolVersion.V33)
            {
                data = Decrypt(data, key);
            }

            if (data.Length == 0)
                return new TuyaLocalResponse(command, returnCode, null);

            var json = Encoding.UTF8.GetString(data);
            if (!json.StartsWith("{") || !json.EndsWith("}"))
                throw new InvalidDataException($"Response is not JSON: {json}");
            return new TuyaLocalResponse(command, returnCode, json);
        }
    }
}
