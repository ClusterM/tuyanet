# Tuya.Net

.NET Standard 2.0 library to interface with Tuya WiFi smart devices.

## Description

This library controls and monitors [Tuya](https://en.tuya.com/) compatible WiFi Smart Devices (Plugs, Switches, Lights, Window Covers, etc.) using the local area network (LAN). [Tuya](https://en.tuya.com/) devices are designed to communicate with the TuyaCloud but most also expose a local area network API, allowing us to directly control the devices without using the cloud.

## How communication with Tuya devices works at all

Every Tuya device broadcasts to local network UDP packets with short info about itself. This data is encrypted with AES but the encryption key is the same for every device and it can be easily decrypted. This packet is sent every 5 seconds and contains data with a unique **device ID**, device type ID and protocol version.

Also, every device can accept TCP connection and proceed requests. Every request contains command code and JSON string. JSON string is encrypted with AES using **local key**. This key is assigned by the Tuya cloud server and is unique for each device. So you need to create a Tuya developer account and request this key from the server.

Most requests must contain base JSON values:
```JSON
{
    "gwId": "DEVICE_ID",
    "devId": "DEVICE_ID",
    "uid":  "DEVICE_ID",
    "t": "CURRENT_TIME"
}
```
Where `DEVICE_ID` is a **device ID** and CURRENT_TIME is a Unix timestamp as a string.

Every response contains command code, JSON and return code (0 = success).

Most devices are controlled via data points (DPs). Every data point has a number and a value. Usually you can send `DP_QUERY` (0x0A) command and receive ra esponse like
```JSON
{
    "devId": "DEVICE_ID",
    "dps":{
        "1": true,
        "2": false,
        "9": 0,
        "10": 0
    }
}
```

`dps` is a dictionary with data points and current values. This is the response from the "2 in 1" smart switch. `1` is a state of switch #1, `2` is a state of switch #2, `9` and `10` are timer values. Please note that DPs numbers are strings. DPs values can be of any type.

Usually you can change DPs values using `CONTROL` (0x07) command and JSON like:
```JSON
{
    "gwId": "DEVICE_ID",
    "devId": "DEVICE_ID",
    "uid":  "DEVICE_ID",
    "t": "CURRENT_TIME",
    "dps":{
        "1": false
    }    
}
```
This request will turn off switch #1.

Don't worry, this library will help you to create requests automatically.

## How to obtain local key

* Download Smart Life mobile app: [for Android](https://play.google.com/store/apps/details?id=com.tuya.smartlife) or [for iOS](https://apps.apple.com/us/app/smart-life-smart-living/id1115101477)). 
* Register your device using this app.
* Open [iot.tuya.com](https://iot.tuya.com/), create developer account and log in.
* Click on `Cloud`

  ![image](https://user-images.githubusercontent.com/4236181/139099858-ad859219-ae39-411d-8b6f-7edd39684c90.png)

* Click on `the Create Clout Project` button

  ![image](https://user-images.githubusercontent.com/4236181/139100737-7d8f5784-9e2f-492e-a867-b8f6765b3397.png)

* Enter any name for your project, select "Smart Home" for industry and development method. You can select any data center but you **must** remember which one you chose.

  ![image](https://user-images.githubusercontent.com/4236181/139101390-2fb4e88f-235c-4872-91a1-3e78ee6217f8.png)

* Skip Configuration Wizard.

  ![image](https://user-images.githubusercontent.com/4236181/139154750-690cf86a-98ac-4428-8aa8-467ef8b96d32.png)

* Copy and save your **Access ID** and **Access Secret**.

  ![image](https://user-images.githubusercontent.com/4236181/139103527-0a048527-ddc2-40c3-aa99-29db0d3cb94c.png)

* Select `Devices`.

  ![image](https://user-images.githubusercontent.com/4236181/139103834-927c6c02-5860-40d6-829d-5a5dfc9091b6.png)

* Select `Liny Tuya App Account`.

  ![image](https://user-images.githubusercontent.com/4236181/139103967-45cf78f0-375b-49db-a111-7c8509abc5c0.png)

* Click on `Add App Account` and it will display a QR code.

  ![image](https://user-images.githubusercontent.com/4236181/139104100-e9b25366-2feb-489b-9044-322ca1dad9c6.png)

* Scan the QR code using your mobile phone and Smart Life app by going to the "Me" tab and clicking on the QR code button [..] in the upper right hand corner of the app. Your account should appear on the list.

  ![image](https://user-images.githubusercontent.com/4236181/139104842-b93b5285-bf76-4eb2-b01b-8f6aa54fdcd9.png)

* Now open the `Devices` tab.

  ![image](https://user-images.githubusercontent.com/4236181/139104946-2e4279a5-028f-4f9e-beb0-9cfb5bae5285.png)

* You should see list of your devices. Copy and save at least one device ID.

  ![image](https://user-images.githubusercontent.com/4236181/139105306-5d37de66-a64a-4d5d-88e4-bf3a43f08f0e.png)

* Click on the `Service API` tab.

  ![image](https://user-images.githubusercontent.com/4236181/139105534-0b20a651-b72a-44c3-9531-8165d0be5f3e.png)

* Click on `Go to Authorize`

  ![image](https://user-images.githubusercontent.com/4236181/139105727-fcd3f3d0-349a-40ce-a5c3-c534556762ae.png)

* Add `IoT Core` API (subscribe to it first).

  ![image](https://user-images.githubusercontent.com/4236181/139105956-573be361-95ae-4a9d-bf5b-2e848b54547f.png)

* Now you can retrieve **local keys** of your devices using `TuyaApi` class:

  ```C#
  var api = new TuyaApi(region: TuyaApi.Region.CentralEurope, accessId: ACCESS_ID, apiSecret: API_SECRET);
  var devices = await api.GetAllDevicesInfoAsync(anyDeviceId: DEVICE_ID);
  foreach(var device in devices)
  {
      Console.WriteLine($"Device: {device.Name}, device ID: {device.Id}, local key: {device.LocalKey}");
  }
  ```
  `region` - the region of the data center that you have selected on [iot.tuya.com](https://iot.tuya.com/)
  `accessID` and `apiSecret` - `Access ID` and `Access Secret` from [iot.tuya.com](https://iot.tuya.com/)
  `anyDeviceId` - ID of any of your smart devices (to fetch user ID).

## Network scanner for Tuya devices
You can use `TuyaScanner` class to catch and decode broadcast UDP packets from devices:
```C#
static void Main(string[] args)
{
    var scanner = new TuyaScanner();
    scanner.OnNewDeviceInfoReceived += Scanner_OnNewDeviceInfoReceived;
    Console.WriteLine("Scanning local network for Tuya devices, press any key to stop.");
    scanner.Start();
    Console.ReadKey();
    scanner.Stop();
}

private static void Scanner_OnNewDeviceInfoReceived(object sender, TuyaDeviceScanInfo e)
{
    Console.WriteLine($"New device found! IP: {e.IP}, ID: {e.GwId}, version: {e.Version}");
}
```
You can use it to retrieve the device's **IP address**, **ID**, and **protocol version**. Remember that your computer must be on the same network as your devices to receive broadcasts.

## How to communicate with devices

You should now have:
* Device IP address - from scanner or from your router
* Device local encryption key - retrieved via `TuyaApi`
* Device ID - from scanner or from [iot.tuya.com](https://iot.tuya.com/)

There is `TuyaDevice` class, you need create instance of it:
```C#
var dev = new TuyaDevice(ip: DEVICE_IP, localKey: DEVICE_KEY, deviceId: DEVICE_ID);
```
It uses protocol version 3.3 by default but you can specify version 3.1 as well:
```C#
TuyaDevice dev = new TuyaDevice(ip: DEVICE_IP, localKey: DEVICE_KEY, deviceId: DEVICE_ID, protocolVersion: TuyaProtocolVersion.V31);
```

Now you can encode requests:
```C#
byte[] request = device.EncodeRequest(TuyaCommand.DP_QUERY, "{\"gwId\":\"DEVICE_ID\",\"devId\":\"DEVICE_ID\",\"uid\":\"DEVICE_ID\",\"t\":\"CURRENT_TIME\"}");
```
Send it:
```C#
byte[] encryptedResponse = await device.SendAsync(request);
```
And decode response:
```C#
TuyaLocalResponse response = device.DecodeResponse(encryptedResponse);
Console.WriteLine($"Response JSON: {response.JSON}");
```

How to set DPs:
```C#
byte[] request = device.EncodeRequest(TuyaCommand.CONTROL, "{\"gwId\":\"DEVICE_ID\",\"devId\":\"DEVICE_ID\",\"uid\":\"DEVICE_ID\",\"t\":\"CURRENT_TIME\"},\"dps\":{\"1\":false}}");
byte[] encryptedResponse = await device.SendAsync(request);
TuyaLocalResponse response = device.DecodeResponse(encryptedResponse);
Console.WriteLine($"Response JSON: {response.JSON}");
```

Too complicated, isn't it? There is more simple way. You can use `FillJson()` method to fill standard fields automatically:
```C#
byte[] request = device.EncodeRequest(TuyaCommand.CONTROL, device.FillJson("{\"dps\":{\"1\":false}}"));
byte[] encryptedResponse = await device.SendAsync(request);
TuyaLocalResponse response = device.DecodeResponse(encryptedResponse);
Console.WriteLine($"Response JSON: {response.JSON}");
```

Also, there is `SendAsync()` overload that accepts command ID with JSON, encodes it, and returns decoded data:
```C#
TuyaLocalResponse response = await device.SendAsync(TuyaCommand.CONTROL, device.FillJson("{\"dps\":{\"1\":false}}"));
```

Finally, there are `GetDps()` and `SetDps()` methods:
```C#
Dictionary<int, object> dps = await device.GetDps();
// Change multiple values at once
Dictionary<int, object> newDps = await device.SetDps(new Dictionary<int, object> { { 1, false }, { 2, true } });
// Change single value
newDps = await device.SetDps(1, true);
```

## Credits
  * TinyTuya https://github.com/jasonacox/tinytuya by Jason Cox
    For Python version of the library inspired me
  * Tuya Smart Plug API https://github.com/Marcus-L/m4rcus.TuyaCore by Marcus Lum
    For some ideas and algorithms
  * TuyAPI https://github.com/codetheweb/tuyapi by codetheweb and blackrozes
    For protocol reverse engineering, additional protocol reverse engineering from jepsonrob and clach04

## Contacts
* My site (Russian): https://clusterrr.com
* Email and PayPal: clusterr@clusterr.com
* Telegram: https://t.me/Cluster_M
