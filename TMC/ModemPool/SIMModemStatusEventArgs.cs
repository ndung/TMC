using System;

namespace TMC.ModemPool
{
    class SIMModemStatusEventArgs : EventArgs
    {
        private string comPort;
        private string status;

        public SIMModemStatusEventArgs(string comPort, string status)
        {
            this.comPort = comPort;
            this.status = status;
        }

        public string COMPort
        {
            get
            {
                return comPort;
            }
        }

        public string Status
        {
            get
            {
                return status;
            }
        }
    }
}