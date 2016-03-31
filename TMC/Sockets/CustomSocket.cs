using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Timers;

namespace TMC.Sockets
{
    public delegate void SocketEventHandler(Socket socket);

    class CustomSocket : Socket
    {
        private readonly Timer timer;
        private const int INTERVAL = 1000;

        public CustomSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
            : base(addressFamily, socketType, protocolType)
        {
            timer = new Timer { Interval = INTERVAL };
            timer.Elapsed += TimerTick;
        }

        public CustomSocket(SocketInformation socketInformation)
            : base(socketInformation)
        {
            timer = new Timer { Interval = INTERVAL };
            timer.Elapsed += TimerTick;
        }

        private readonly List<SocketEventHandler> onCloseHandlers = new List<SocketEventHandler>();
        public event SocketEventHandler SocketClosed
        {
            add { onCloseHandlers.Add(value); }
            remove { onCloseHandlers.Remove(value); }
        }

        public bool EventsEnabled
        {
            set
            {
                if (value)
                    timer.Start();
                else
                    timer.Stop();
            }
        }

        private void TimerTick(object sender, EventArgs e)
        {
            if (!Connected)
            {
                foreach (var socketEventHandler in onCloseHandlers)
                    socketEventHandler.Invoke(this);
                EventsEnabled = false;
            }
        }

        // Hiding base connected property
        public bool IsConnected
        {
            get
            {
                if (Connected)
                {
                    if ((Poll(0, SelectMode.SelectWrite)) && (!Poll(0, SelectMode.SelectError)))
                    {
                        byte[] buffer = new byte[1];
                        if (Receive(buffer, SocketFlags.Peek) == 0)
                        {
                            return false;
                        }
                        else
                        {
                            return true;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
        }
    }
}
