namespace TMC.Util
{
    public enum CommandType
    {
        CHECK_SIM_MODEM,
        ACTIVATE_INCOMING_SMS_INDICATOR,
        SEND_USSD,
        SEND_SMS,
        SEND_SMS_CDMA,
        READ_SMS,
        READ_NEW_SMS,
        READ_SMS_CDMA,
        DELETE_SMS,
        DELETE_SMS_CDMA,
        VOICE_CALL,
        SIM_ACTIVATION,
        CHECK_SIGNAL,
        CHECK_IMEI,
        RESTART_MODEM
    }

    public class Constants
    {
        public static readonly string NO_MODEM = "0";
        public static readonly string NO_SIM = "1";
        public static readonly string SIM_READY = "2";
    }
}
