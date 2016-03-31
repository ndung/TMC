using System;

namespace TMC.Sockets
{
    public class IncomingSocketMessageEventArgs : EventArgs
    {
        private string message;

        public IncomingSocketMessageEventArgs(string message)
        {
            this.message = message;
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
