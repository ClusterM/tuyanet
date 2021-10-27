# Tuya.Net
.NET library to interface with Tuya WiFi smart devices.

## Description

This library controls and monitors [Tuya](https://en.tuya.com/) compatible WiFi Smart Devices (Plugs, Switches, Lights, Window Covers, etc.) using the local area network (LAN). [Tuya](https://en.tuya.com/) devices are designed to communicate with the TuyaCloud but most also expose a local area network API, allowing us to directly control the devices without using the cloud.

What you need to communicate with devices over LAN:
* Device IP address
* Device local encryption key

## How to obtain local enryption key
* Download Smart Life mobile app: [for Android](https://play.google.com/store/apps/details?id=com.tuya.smartlife) or [for iOS](https://apps.apple.com/us/app/smart-life-smart-living/id1115101477)). 
* Register your device using this app.
* Open [iot.tuya.com](https://iot.tuya.com/), create developer account and log in.
* Click on `Cloud`

![image](https://user-images.githubusercontent.com/4236181/139099858-ad859219-ae39-411d-8b6f-7edd39684c90.png)

* Click on `Create Clout Project` button

![image](https://user-images.githubusercontent.com/4236181/139100737-7d8f5784-9e2f-492e-a867-b8f6765b3397.png)

* Enter any name for your project, select "Smart Home" for industry and development method. You can select any data center but you **must** remember which one you chose.

![image](https://user-images.githubusercontent.com/4236181/139101390-2fb4e88f-235c-4872-91a1-3e78ee6217f8.png)

* Skip Configuration Wizard.

![image](https://user-images.githubusercontent.com/4236181/139102680-89a1b982-bb90-4a9a-b997-35baabe6f5e5.png)

* Copy and save your `Access ID` and `Access Secret`

![image](https://user-images.githubusercontent.com/4236181/139103527-0a048527-ddc2-40c3-aa99-29db0d3cb94c.png)

* Select `Devices`.

![image](https://user-images.githubusercontent.com/4236181/139103834-927c6c02-5860-40d6-829d-5a5dfc9091b6.png)

* Select `Liny Tuya App Account`.

![image](https://user-images.githubusercontent.com/4236181/139103967-45cf78f0-375b-49db-a111-7c8509abc5c0.png)

* Click on `Add App Account` and it will display a QR code.

![image](https://user-images.githubusercontent.com/4236181/139104100-e9b25366-2feb-489b-9044-322ca1dad9c6.png)

* Scan the QR code using your mobile phone and Smart Life app by going to the "Me" tab and clicking on the QR code button [..] in the upper right hand corner of the app. Your account should appear on the list.

![image](https://user-images.githubusercontent.com/4236181/139104842-b93b5285-bf76-4eb2-b01b-8f6aa54fdcd9.png)

* Now open `Devices` tab.

![image](https://user-images.githubusercontent.com/4236181/139104946-2e4279a5-028f-4f9e-beb0-9cfb5bae5285.png)

* You should see list of your devices. Copy and save at least one device ID.

![image](https://user-images.githubusercontent.com/4236181/139105306-5d37de66-a64a-4d5d-88e4-bf3a43f08f0e.png)

* Click on `Service API` tab.

![image](https://user-images.githubusercontent.com/4236181/139105534-0b20a651-b72a-44c3-9531-8165d0be5f3e.png)

* Click on `Go to Authorize`

![image](https://user-images.githubusercontent.com/4236181/139105727-fcd3f3d0-349a-40ce-a5c3-c534556762ae.png)

* Add `IoT Core` API (subscribe to it first).

![image](https://user-images.githubusercontent.com/4236181/139105956-573be361-95ae-4a9d-bf5b-2e848b54547f.png)

* Now you can retrieve local keys of your devices using `TuyaApi` class:

```C#
var api = new TuyaApi(TuyaApi.Region.CentralEurope, API_KEY, API_SECRET);
var devices = await api.GetAllDevicesInfoAsync(anyDeviceId: DEVICE_ID);
foreach(var device in devices)
{
    Console.WriteLine($"Device: {device.Name}, device ID: {device.Id}, local key: {device.LocalKey}");
}
```

