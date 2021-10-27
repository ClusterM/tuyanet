using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace com.clusterrr.TuyaNet
{
    public class TuyaScanner
    {
        private const ushort UDP_PORT31 = 6666;      // Tuya 3.1 UDP Port
        private const ushort UDP_PORTS33 = 6667;     // Tuya 3.3 encrypted UDP Port
        private const string UDP_KEY = "yGAdlopoPVldABfn";

        private bool running = false;
        private UdpClient udpServer31 = null;
        private UdpClient udpServer33 = null;
        private Thread udpListener31 = null;
        private Thread udpListener33 = null;
        private List<TuyaDeviceScanInfo> devices = new List<TuyaDeviceScanInfo>();

        public event EventHandler<TuyaDeviceScanInfo> OnDeviceInfoReceived;
        public event EventHandler<TuyaDeviceScanInfo> OnNewDeviceInfoReceived;

        public void Start()
        {
            Stop();
            running = true;
            devices.Clear();
            udpServer31 = new UdpClient(UDP_PORT31);
            udpServer33 = new UdpClient(UDP_PORTS33);
            udpListener31 = new Thread(UdpListener31Thread);
            udpListener33 = new Thread(UdpListener33Thread);
            udpListener31.Start(udpServer31);
            udpListener33.Start(udpServer33);
        }

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
            udpListener31 = null;
            udpListener33 = null;
        }

        private void UdpListener31Thread(object o)
        {
            var udpServer = o as UdpClient;
            byte[] udp_key;
            using (var md5 = MD5.Create())
            {
                udp_key = md5.ComputeHash(Encoding.ASCII.GetBytes(UDP_KEY));
            }

            while (running)
            {
                try
                {
                    IPEndPoint ep = null;
                    var data = udpServer.Receive(ref ep);
                    var response = TuyaParser.DecodeResponse(data, udp_key, TuyaProtocolVersion.V31);
                    Parse(response.JSON);
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
            byte[] udp_key;
            using (var md5 = MD5.Create())
            {
                udp_key = md5.ComputeHash(Encoding.ASCII.GetBytes(UDP_KEY));
            }

            while (running)
            {
                try
                {
                    IPEndPoint ep = null;
                    var data = udpServer.Receive(ref ep);
                    var response = TuyaParser.DecodeResponse(data, udp_key, TuyaProtocolVersion.V33);
                    Parse(response.JSON);
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
            var deviceInfo = JsonSerializer.Deserialize<TuyaDeviceScanInfo>(json);
            OnDeviceInfoReceived?.Invoke(this, deviceInfo);
            if ((OnNewDeviceInfoReceived) != null && !devices.Contains(deviceInfo))
            {
                devices.Add(deviceInfo);
                OnNewDeviceInfoReceived?.Invoke(this, deviceInfo);
            }
        }
    }
}
