using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace com.clusterrr.TuyaNet
{
    /// <summary>
    /// Scanner to discover devices over local network.
    /// </summary>
    public class TuyaScanner
    {
        private const ushort UDP_PORT31 = 6666;      // Tuya 3.1 UDP Port
        private const ushort UDP_PORTS33 = 6667;     // Tuya 3.3 encrypted UDP Port
        private const ushort UDP_PORTS34 = 6667;     // Tuya 3.3 encrypted UDP Port
        private const string UDP_KEY = "yGAdlopoPVldABfn";

        private bool running = false;
        private UdpClient udpServer31 = null;
        private UdpClient udpServer33 = null;
        private UdpClient udpServer34 = null;
        private Thread udpListener31 = null;
        private Thread udpListener33 = null;
        private Thread udpListener34 = null;
        private List<TuyaDeviceScanInfo> devices = new List<TuyaDeviceScanInfo>();
        private readonly TuyaParser parser31;
        private readonly TuyaParser parser33;
        private readonly TuyaParser parser34;

        /// <summary>
        /// Even that will be called on every broadcast message from devices.
        /// </summary>
        public event EventHandler<TuyaDeviceScanInfo> OnDeviceInfoReceived;
        /// <summary>
        /// Even that will be called only once for every device.
        /// </summary>
        public event EventHandler<TuyaDeviceScanInfo> OnNewDeviceInfoReceived;

        /// <summary>
        /// Creates a new instance of the TuyaScanner class.
        /// </summary>
        public TuyaScanner()
        {
            byte[] udpKey;
            using (var md5 = MD5.Create())
            {
                udpKey = md5.ComputeHash(Encoding.ASCII.GetBytes(UDP_KEY));
            }
            parser31 = new TuyaParser(udpKey, TuyaProtocolVersion.V31);
            parser33 = new TuyaParser(udpKey, TuyaProtocolVersion.V33);
            parser34 = new TuyaParser(udpKey, TuyaProtocolVersion.V34);
        }

        /// <summary>
        /// Starts scanner.
        /// </summary>
        public void Start()
        {
            Stop();
            running = true;
            devices.Clear();
            udpServer31 = new UdpClient(UDP_PORT31);
            udpServer33 = new UdpClient(UDP_PORTS33);
            udpServer34 = new UdpClient(UDP_PORTS34);
            udpListener31 = new Thread(UdpListener31Thread);
            udpListener33 = new Thread(UdpListener33Thread);
            udpListener34 = new Thread(UdpListener34Thread);
            udpListener31.Start(udpServer31);
            udpListener33.Start(udpServer33);
            udpListener34.Start(udpServer34);
        }

        /// <summary>
        /// Stops scanner.
        /// </summary>
        public void Stop()
        {
            running = false;
            if (udpServer31 != null)
            {
                udpServer31.Dispose();
                udpServer31 = null;
            }
            if (udpServer33 != null)
            {
                udpServer33.Dispose();
                udpServer33 = null;
            }
            if (udpServer34 != null)
            {
                udpServer34.Dispose();
                udpServer34 = null;
            }
            udpListener31 = null;
            udpListener33 = null;
            udpListener34 = null;
        }

        private void UdpListener31Thread(object o)
        {
            var udpServer = o as UdpClient;
            while (running)
            {
                try
                {
                    IPEndPoint ep = null;
                    var data = udpServer.Receive(ref ep);
                    var response = parser31.DecodeResponse(data);
                    Parse(response.Json);
                }
                catch
                {
                    if (!running) return;
                    throw;
                }
            }
        }

        private void UdpListener33Thread(object o)
        {
            var udpServer = o as UdpClient;

            while (running)
            {
                try
                {
                    IPEndPoint ep = null;
                    var data = udpServer.Receive(ref ep);
                    var response = parser33.DecodeResponse(data);
                    Parse(response.Json);
                }
                catch
                {
                    if (!running) return;
                    throw;
                }
            }
        }
        
        private void UdpListener34Thread(object o)
        {
            var udpServer = o as UdpClient;

            while (running)
            {
                try
                {
                    IPEndPoint ep = null;
                    var data = udpServer.Receive(ref ep);
                    var response = parser34.DecodeResponse(data);
                    Parse(response.Json);
                }
                catch
                {
                    if (!running) return;
                    throw;
                }
            }
        }

        private void Parse(string json)
        {
            var deviceInfo = JsonConvert.DeserializeObject<TuyaDeviceScanInfo>(json);
            OnDeviceInfoReceived?.Invoke(this, deviceInfo);
            if ((OnNewDeviceInfoReceived) != null && !devices.Contains(deviceInfo))
            {
                devices.Add(deviceInfo);
                OnNewDeviceInfoReceived?.Invoke(this, deviceInfo);
            }
        }
    }
}
