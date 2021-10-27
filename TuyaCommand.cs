namespace com.clusterrr.TuyaNet
{
    /// <summary>
    /// Tuya command type
    /// </summary>
    public enum TuyaCommand
    {
        UDP = 0,
        AP_CONFIG = 1,
        ACTIVE = 2,
        BIND = 3,
        RENAME_GW = 4,
        RENAME_DEVICE = 5,
        UNBIND = 6,
        CONTROL = 7,
        STATUS = 8,
        HEART_BEAT = 9,
        DP_QUERY = 10,
        QUERY_WIFI = 11,
        TOKEN_BIND = 12,
        CONTROL_NEW = 13,
        ENABLE_WIFI = 14,
        DP_QUERY_NEW = 16,
        SCENE_EXECUTE = 17,
        UPDATED_PS = 18,
        UDP_NEW = 19,
        AP_CONFIG_NEW = 20,
        GET_LOCAL_TIME_CMD = 28,
        WEATHER_OPEN_CMD = 32,
        WEATHER_DATA_CMD = 33,
        STATE_UPLOAD_SYN_CMD = 34,
        STATE_UPLOAD_SYN_RECV_CMD = 35,
        HEAT_BEAT_STOP = 37,
        STREAM_TRANS_CMD = 38,
        GET_WIFI_STATUS_CMD = 43,
        WIFI_CONNECT_TEST_CMD = 44,
        GET_MAC_CMD = 45,
        GET_IR_STATUS_CMD = 46,
        IR_TX_RX_TEST_CMD = 47,
        LAN_GW_ACTIVE = 240,
        LAN_SUB_DEV_REQUEST = 241,
        LAN_DELETE_SUB_DEV = 242,
        LAN_REPORT_SUB_DEV = 243,
        LAN_SCENE = 244,
        LAN_PUBLISH_CLOUD_CONFIG = 245,
        LAN_PUBLISH_APP_CONFIG = 246,
        LAN_EXPORT_APP_CONFIG = 247,
        LAN_PUBLISH_SCENE_PANEL = 248,
        LAN_REMOVE_GW = 249,
        LAN_CHECK_GW_UPDATE = 250,
        LAN_GW_UPDATE = 251,
        LAN_SET_GW_CHANNEL = 252
    }
}
