namespace TMC.Util
{
    public enum CommandType
    {
        CHECK_SIM_MODEM,
        SEND_USSD,
        SEND_SMS,
        SEND_SMS_CDMA,
        READ_SMS,
        READ_SMS_CDMA,
        DELETE_SMS,
        DELETE_SMS_CDMA,
        VOICE_CALL,
        SIM_ACTIVATION,
        MTRONIK,
        STOK_MTRONIK
    }

    public class Constants
    {
        public static readonly string NO_MODEM = "0";
        public static readonly string NO_SIM = "1";
        public static readonly string SIM_READY = "2";
    }
}
