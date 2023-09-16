using System;
using System.Linq;

namespace com.clusterrr.TuyaNet.Extensions
{
    public static class EnumExtensions
    {
        public static TuyaProtocolVersion[] GetTuyaVersionsValues()
        {
            var values = (TuyaProtocolVersion[])Enum.GetValues(typeof(TuyaProtocolVersion));
            return values;
        } 
        
        public static string GetNames(this TuyaCommand command)
        {
            var values = (TuyaCommand[])Enum.GetValues(typeof(TuyaCommand));
            return string.Join(", ", values.Where(x => x == command).Select(x=>x.ToString()));
        } 
    }
}