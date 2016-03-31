using System;

namespace TMC.ModemPool
{
    public class IncomingSMSEventArgs : EventArgs
    {
        private string comPort;
        private string message;

        public IncomingSMSEventArgs(string comPort, string message)
        {
            this.comPort = comPort;
            this.message = message;
        }

        public string COMPort
        {
            get
            {
                return comPort;
            }
        }

        public string Message
        {
            get
            {
                return message;
            }
        }

    }
}
