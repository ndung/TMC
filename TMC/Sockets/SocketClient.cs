using System;
using System.Configuration;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TMC.Sockets
{
    public delegate void IncomingSocketMessageHandler(object sender, EventArgs args);

    public class SocketClient
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        
        private string serverID;

        private const int INTERVAL = 50000;
        public SocketClient(string serverID)
        {
            this.serverID = serverID;
            timer = new System.Timers.Timer { Interval = INTERVAL };
            timer.Elapsed += TimerTick;
            timer.Start();
        }

        private readonly System.Timers.Timer timer;

        private void TimerTick(object sender, EventArgs e)
        {
            if (connected)
            {
                Send("UPUPUP" + serverID);
            }
        }

        public event IncomingSocketMessageHandler IncomingSocketMessageHandler;

        private CustomSocket socket;

        public void Connect()
        {
            try
            {
                log.Info("Connecting to server...");

                // Close the socket if it is still open
                if (socket != null && socket.Connected)
                {
                    socket.Shutdown(SocketShutdown.Both);
                    System.Threading.Thread.Sleep(10);
                    socket.Close();
                }

                // Create the socket object
                socket = new CustomSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // Define the Server address and port
                IPEndPoint epServer = new IPEndPoint(IPAddress.Parse(ConfigurationManager.AppSettings["ServerIP"]), 
                    Convert.ToInt32(ConfigurationManager.AppSettings["ServerPort"]));

                // Connect to the server blocking method and setup callback for recieved data
                // m_sock.Connect( epServer );
                // SetupRecieveCallback( m_sock );

                // Connect to server non-Blocking method
                // socket.Blocking = false;
                AsyncCallback onconnect = new AsyncCallback(OnConnect);
                socket.BeginConnect(epServer, onconnect, socket);
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
            }

        }

        public void OnConnect(IAsyncResult ar)
        {
            // Socket was the passed in object
            CustomSocket sock = (CustomSocket)ar.AsyncState;

            // Check if we were sucessfull
            try
            {
                //sock.EndConnect( ar );
                if (sock.Connected)
                {
                    log.Info("Connected to server...");
                    connected = true;
                    socket.SocketClosed += socket_SocketClosed;
                    socket.EventsEnabled = true;
                    SetupReceiveCallback(sock);
                    Send("UPUPUP"+serverID);
                }
                else
                {
                    connected = false;
                    Connect();
                }
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
            }
        }

        bool connected = false;

        void socket_SocketClosed(Socket socket)
        {
            connected = false;
            Connect();
        }

        private byte[] m_byBuff = new byte[1024];
        
        public void OnReceivedData(IAsyncResult ar)
        {
            // Socket was the passed in object
            Socket sock = (Socket)ar.AsyncState;

            // Check if we got any data
            try
            {
                int nBytesRec = sock.EndReceive(ar);
                if (nBytesRec > 0)
                {
                    // Wrote the data to the List
                    log.Debug("total msg Length : " + nBytesRec);
                    string sReceived = Encoding.ASCII.GetString(m_byBuff, 0, nBytesRec);
                    log.Debug("sReceived:" + sReceived);
                    //string sReceived = Encoding.ASCII.GetString(m_byBuff, 4, Convert.ToInt32(length));
                    int total = 0;
                    while (nBytesRec>total)
                    {
                        int length = Convert.ToInt32(sReceived.Substring(0,4));
                        string message = sReceived.Substring(4, length);
                        OnIncomingMessage(message);
                        total = total + 4 + length;
                        sReceived = sReceived.Substring(4+length);
                        log.Debug("total proceed msg Length : " + total);
                    }
                    
                    // If the connection is still usable restablish the callback
                    SetupReceiveCallback(sock);
                }
                else
                {
                    // If no data was recieved then the connection is probably dead
                    log.Info("server disconnected");
                    sock.Shutdown(SocketShutdown.Both);
                    sock.Close();
                }
            }
            catch (OutOfMemoryException ex)
            {
                log.Error("OutOfMemoryException thrown, exit application...", ex);
                System.Windows.Forms.Application.Exit();
            }
            catch (Exception ex)
            {
                SetupReceiveCallback(sock);
                log.Error(ex.Message);

            }
        }

        private void OnIncomingMessage(string message)
        {
            if (IncomingSocketMessageHandler != null)
            {
                EventArgs args = new IncomingSocketMessageEventArgs(message);
                IncomingSocketMessageHandler(this, args);
            }
        }

        public void SetupReceiveCallback(Socket sock)
        {
            try
            {
                AsyncCallback receiveData = new AsyncCallback(OnReceivedData);
                sock.BeginReceive(m_byBuff, 0, m_byBuff.Length, SocketFlags.None, receiveData, sock);
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
            }
        }

        public bool Send(string message)
        {
            if (connected)
            {
                // Read the message from the text box and send it
                try
                {
                    if (!message.StartsWith("STATUS"))
                    {
                        log.Debug("sending message to server: [" + message + "]");
                    }
                    // Convert to byte array and send.
                    string length = (""+message.Length).PadLeft(4,'0');
                    message = length + message;
                    Byte[] byteDateLine = Encoding.ASCII.GetBytes(message);
                    int x = socket.Send(byteDateLine, byteDateLine.Length, 0);
                    if (x > 0)
                    {
                        return true;
                    }
                    //socket.BeginSend(byteData, 0, byteData.Length, SocketFlags.None, new AsyncCallback(OnSend), null);
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message);
                }
            }
            return false;
        }
        
        public bool IsConnected()
        {
            return connected;
        }
    }

}
