namespace com.clusterrr.TuyaNet
{
    /// <summary>
    /// Response from local Tuya device.
    /// </summary>
    public class TuyaLocalResponse
    {
        /// <summary>
        /// Command code.
        /// </summary>
        public TuyaCommand Command { get; }
        
        /// <summary>
        /// Return code.
        /// </summary>
        public int ReturnCode { get; }
        
        /// <summary>
        /// Response as bytes string.
        /// </summary>
        public byte[] Payload { get; }
        
        /// <summary>
        /// Response as JSON string.
        /// </summary>
        public string Json { get; }

        internal TuyaLocalResponse(TuyaCommand command, int returnCode, byte[] payload, string json = null)
        {
            Command = command;
            ReturnCode = returnCode;
            Payload = payload;
            Json = json;
        }

        public override string ToString() => $"{Command}: {Json} (return code = {ReturnCode})";
    }
}
