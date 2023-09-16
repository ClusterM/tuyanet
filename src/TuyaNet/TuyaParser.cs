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
    internal class TuyaParser
    {
        private static byte[] PROTOCOL_VERSION_BYTES_31 = Encoding.ASCII.GetBytes("3.1");
        private static byte[] PROTOCOL_VERSION_BYTES_33 = Encoding.ASCII.GetBytes("3.3");
        private static byte[] PROTOCOL_VERSION_BYTES_34 = Encoding.ASCII.GetBytes("3.4");
        private static byte[] PROTOCOL_33_HEADER = Enumerable.Concat(PROTOCOL_VERSION_BYTES_33, new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }).ToArray();
        private static byte[] PROTOCOL_34_HEADER = Enumerable.Concat(PROTOCOL_VERSION_BYTES_34, new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }).ToArray();
        private static byte[] PREFIX = new byte[] { 0, 0, 0x55, 0xAA };
        internal static byte[] SUFFIX = { 0, 0, 0xAA, 0x55 };
        private uint SeqNo = 0;
        private byte[] sessionKey;
        private byte[] localKey;
        private TuyaProtocolVersion version;

        public TuyaParser(string localKey, TuyaProtocolVersion tuyaProtocolVersion) 
            : this(Encoding.UTF8.GetBytes(localKey), tuyaProtocolVersion)
        {
        }
        
        public TuyaParser(byte[] localKey, TuyaProtocolVersion tuyaProtocolVersion)
        {
            this.sessionKey = null;
            this.localKey = localKey;
            this.version = tuyaProtocolVersion;
        }

        internal IEnumerable<byte> BigEndian(IEnumerable<byte> seq) => BitConverter.IsLittleEndian ? seq.Reverse() : seq;

        internal byte[] Encrypt(byte[] data, byte[] key)
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
        
        internal byte[] Encrypt34(byte[] data)
        {
            var key = GetKey();
            var chiper = Aes.Create();
            chiper.Key = key;
            chiper.Mode = CipherMode.ECB;
            chiper.Padding = PaddingMode.None;
            var encryptor = chiper.CreateEncryptor();
            var dest = encryptor.TransformFinalBlock(data, 0, data.Length);
            return dest;
        }
        
        internal byte[] Decrypt(byte[] data, byte[] key)
        {
            if (data is null || data.Length == 0) return data;
            
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

        internal byte[] EncodeRequest(TuyaCommand command, string json, byte[] key, TuyaProtocolVersion protocolVersion = TuyaProtocolVersion.V33)
        {
            if (protocolVersion == TuyaProtocolVersion.V34)
                return EncodeRequest34(command, json, key);
            
            // Remove spaces and newlines
            var root = JObject.Parse(json);
            json = root.ToString(Newtonsoft.Json.Formatting.None);

            byte[] payload = Encoding.UTF8.GetBytes(json);

            if (protocolVersion == TuyaProtocolVersion.V33)
            {
                // Encrypt
                payload = Encrypt(payload, key);
                // Add protocol 3.3 header
                if ((command != TuyaCommand.DP_QUERY) && (command != TuyaCommand.UPDATE_DPS))
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
                byte[] seqNo = BitConverter.GetBytes(++SeqNo);
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

        internal void SetupSessionKey(byte[] sessionKey)
        {
            this.sessionKey = sessionKey;
        }

        internal byte[] EncodeRequest34(TuyaCommand command, string json, byte[] key)
        {
            // Remove spaces and newlines
            //"{\"data\":{\"ctype\":0,\"devId\":\"bf1d446bf5f3fbfc57fu5u\",\"gwId\":\"bf1d446bf5f3fbfc57fu5u\",\"uid\":\"\",\"dps\":{\"1\":true}},\"protocol\":5,\"t\":1694800339}"
            var root = JObject.Parse(json);
            json = root.ToString(Newtonsoft.Json.Formatting.None);
            byte[] payload = Encoding.UTF8.GetBytes(json);
            return EncodeRequest34(command, payload, key);
        }
        
        internal byte[] EncodeRequest34(TuyaCommand command, byte[] payload, byte[] key)
        {
            // Add protocol 3.4 header
            if (
                (command != TuyaCommand.DP_QUERY) && 
                (command != TuyaCommand.HEART_BEAT) && 
                (command != TuyaCommand.DP_QUERY_NEW) && 
                (command != TuyaCommand.SESS_KEY_NEG_START) && 
                (command != TuyaCommand.SESS_KEY_NEG_FINISH) && 
                (command != TuyaCommand.UPDATE_DPS)
                )
                payload = Enumerable.Concat(PROTOCOL_34_HEADER, payload).ToArray();

            var paddingSize = 0x10 - (payload.Length & 0xF);
            payload = payload.Concat(Enumerable.Range(0, paddingSize).Select(x => (byte)paddingSize)).ToArray();

            // Encrypt
            payload = Encrypt34(payload);
            
            using (var ms = new MemoryStream())
            {
                byte[] seqNo = BitConverter.GetBytes(++SeqNo);
                if (BitConverter.IsLittleEndian) 
                    Array.Reverse(seqNo);  // Make big-endian
                byte[] dataLength = BitConverter.GetBytes(payload.Length + 36);
                if (BitConverter.IsLittleEndian) 
                    Array.Reverse(dataLength); // Make big-endian

                var commandBytes = BitConverter.GetBytes((uint)command);
                if (BitConverter.IsLittleEndian) 
                    Array.Reverse(commandBytes); // Make big-endian
                
                ms.Write(PREFIX, 0, PREFIX.Length);              // Prefix
                ms.Write(seqNo, 0, seqNo.Length);                // Packet number
                ms.Write(commandBytes, 0, commandBytes.Length);  // Command number
                ms.Write(dataLength, 0, dataLength.Length);      // Length of data + length of suffix
                ms.Write(payload, 0, payload.Length);            // Data
                var hashHmacSha256 = GetHashSha256(ms.ToArray());
                ms.Write(hashHmacSha256, 0, hashHmacSha256.Length);// hashHmacSha256 checksum
                ms.Write(SUFFIX, 0, SUFFIX.Length);                // Suffix
                payload = ms.ToArray();
            }
            return payload;
        }

        internal byte[] GetHashSha256(byte[] byteArray)
        {
            var encryptKey = GetKey();
            using (var hasher = new HMACSHA256(encryptKey))
            {
                var hashValue = hasher.ComputeHash(byteArray);
                return hashValue;
            }
        }

        internal byte[] GetKey()
        {
            return sessionKey is null ? localKey : sessionKey;
        }

        private TuyaLocalResponse ParseResponse(byte[] data)
        {
            var defaultUintSize = 4;
            var headerSize = 16;
            var returnCodeSize = defaultUintSize;
            var suffixSize = defaultUintSize;
            var responseHeaderSize = headerSize + returnCodeSize; 
            var hashSize = 32;
            var crcSize = 4;
            var endingHashWithSuffix = hashSize + defaultUintSize;
            var endingCrcWithSuffix = crcSize + defaultUintSize;
            
            // Check length and prefix
            if (data.Length < 20 || !data.Take(PREFIX.Length).SequenceEqual(PREFIX))
            {
                throw new InvalidDataException("Invalid header/prefix");
            }
            // Check length
            var payloadSize = BitConverter.ToInt32(BigEndian(data.Skip(12).Take(4)).ToArray(), 0);
            if (data.Length != headerSize + payloadSize)
            {
                throw new InvalidDataException("Invalid length");
            }
            // Check suffix
            if (!data.Skip(headerSize + payloadSize - SUFFIX.Length).Take(SUFFIX.Length).SequenceEqual(SUFFIX))
            {
                throw new InvalidDataException("Invalid suffix");
            }
            
            // Packet number
            var seq = BitConverter.ToUInt32(BigEndian(data.Skip(4).Take(4)).ToArray(), 0);
            
            // Command
            var command = (TuyaCommand)BitConverter.ToUInt32(BigEndian(data.Skip(8).Take(4)).ToArray(), 0);
            var isDiscoveryPackage = 
                command == TuyaCommand.UDP ||
                command == TuyaCommand.UDP_NEW ||
                command == TuyaCommand.BOARDCAST_LPV34;
            
            // Return code
            var returnCode = BitConverter.ToUInt32(BigEndian(data.Skip(headerSize).Take(4)).ToArray(), 0);
            
            // Data parse
            byte[] payload;
            if ((returnCode & 0xFFFFFF00) > 0) {
                if (this.version == TuyaProtocolVersion.V34 && !isDiscoveryPackage) {
                    payload = data.Skip(headerSize).Take(payloadSize - endingHashWithSuffix).ToArray();
                } else {
                    payload = data.Skip(headerSize).Take(payloadSize - returnCodeSize - endingCrcWithSuffix).ToArray();
                }
            } else if (this.version == TuyaProtocolVersion.V34 && !isDiscoveryPackage) {
                payload = data.Skip(responseHeaderSize).Take(payloadSize - returnCodeSize - endingHashWithSuffix).ToArray();
            } else {
                payload = data.Skip(responseHeaderSize).Take(payloadSize - returnCodeSize - endingCrcWithSuffix).ToArray();
            }
            
            if (this.version == TuyaProtocolVersion.V34 && !isDiscoveryPackage) {
                var expected = data.Skip(responseHeaderSize + payload.Length).Take(hashSize).ToArray();
                var computed = GetHashSha256(data.Take(responseHeaderSize + payload.Length).ToArray());
                if (!expected.SequenceEqual(computed)) {
                    throw new Exception("HMAC mismatch.");
                }
            } else
            {
                var expected = data.Skip(responseHeaderSize + payload.Length).Take(crcSize).ToArray();
                var crcComputed = new Crc32().Get(data.Take(responseHeaderSize + payload.Length).ToArray());
                byte[] computed = BitConverter.GetBytes(crcComputed);
                if (BitConverter.IsLittleEndian) Array.Reverse(computed);
                if (!expected.SequenceEqual(computed)) 
                {
                    throw new Exception("CRC mismatch.");
                }
            }
            
            if (payload.Length == 0)
                return new TuyaLocalResponse(command, (int)returnCode, null);

            return new TuyaLocalResponse(command, (int)returnCode, payload);
        }

        internal TuyaLocalResponse DecodeResponse34(byte[] data)
        {
            var byteResponse = ParseResponse(data);
            var decodedBytes = Decrypt(byteResponse.Payload, GetKey());

            string json = null;
            try
            {
                json = Encoding.UTF8.GetString(decodedBytes);
                if (!json.StartsWith("{") || !json.EndsWith("}"))
                {
                    json = null;
                    throw new InvalidDataException($"Response is not JSON: {json}");
                }
            }
            catch (Exception e)
            {
                // ignored
            }

            return new TuyaLocalResponse(byteResponse.Command, byteResponse.ReturnCode, decodedBytes, json);
        }
        
        internal TuyaLocalResponse DecodeResponse(byte[] data)
        {
            if (version == TuyaProtocolVersion.V34)
                return DecodeResponse34(data);
            
            //todo rm next code and use decode34
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

            // Remove version 3.1 header
            if (data.Take(PROTOCOL_VERSION_BYTES_31.Length).SequenceEqual(PROTOCOL_VERSION_BYTES_31))
            {
                data = data.Skip(PROTOCOL_VERSION_BYTES_31.Length).ToArray();
                this.version = TuyaProtocolVersion.V31;
            }
            // Remove version 3.3 header
            if (data.Take(PROTOCOL_VERSION_BYTES_33.Length).SequenceEqual(PROTOCOL_VERSION_BYTES_33))
            {
                data = data.Skip(PROTOCOL_33_HEADER.Length).ToArray();
                this.version = TuyaProtocolVersion.V33;
            }

            if (this.version == TuyaProtocolVersion.V33)
            {
                data = Decrypt(data, GetKey());
            }

            if (data.Length == 0)
                return new TuyaLocalResponse(command, returnCode, null, null);

            var json = Encoding.UTF8.GetString(data);
            if (!json.StartsWith("{") || !json.EndsWith("}"))
                throw new InvalidDataException($"Response is not JSON: {json}");
            
            return new TuyaLocalResponse(command, returnCode, data, json);
        }
    }
}
