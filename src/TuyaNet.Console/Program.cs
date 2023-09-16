using com.clusterrr.TuyaNet.Services;
using TuyaNet.Console;

var devicesDatabaseFile = args.FirstOrDefault() ?? "../../Database.example.json";
Database.LoadFromFile(devicesDatabaseFile);

var device = Database.Devices.First(x => x.ApiVer == "3.4");
var service = new TuyaSwitcherService(device);
await service.Connect();

async Task TurnOn()
{
    await service.TurnOn();
    var isEnabled = await service.GetStatus();
    Console.WriteLine($"status get response: {isEnabled}");
}
        
async Task TurnOff()
{
    await service.TurnOff();
    var isEnabled = await service.GetStatus();
    Console.WriteLine($"status get response: {isEnabled}");
}        

async Task<bool> GetIsEnabled()
{
    return await service.GetStatus();
}

if(await GetIsEnabled())
    await TurnOff();
else
    await TurnOn();