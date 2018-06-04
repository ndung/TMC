using System;
using System.Configuration;
using System.Threading;
using TMC.ModemPool;
using TMC.Sockets;
using TMC.Util;

namespace TMC
{
    class Program
    {
        static void Main(string[] args)
        {
            //log4net.Config.XmlConfigurator.Configure();
            try
            {
                string serverID = ConfigurationManager.AppSettings["ServerID"];
                //Console.WriteLine("serverID:" + serverID);

                Processor processor = new Processor();

                SocketClient socketClient = new SocketClient(serverID);
                socketClient.IncomingSocketMessageHandler += new IncomingSocketMessageHandler(processor.processIncomingSocketMessage);
                Thread thread = new Thread(new ThreadStart(socketClient.Connect));
                thread.Start();

                string readSMSTimer = ConfigurationManager.AppSettings["ReadSMSTimer"];
                string cdmaPorts = ConfigurationManager.AppSettings["CDMAPorts"];
                string interval = ConfigurationManager.AppSettings["Interval"];
                string localPort = ConfigurationManager.AppSettings["LocalPort"];
                string baudRate = ConfigurationManager.AppSettings["BaudRate"];
                if (cdmaPorts == null)
                {
                    cdmaPorts = "";
                }
                string ignoredPorts = ConfigurationManager.AppSettings["IgnoredPorts"];
                if (ignoredPorts == null)
                {
                    ignoredPorts = "";
                }
                if (interval == null)
                {
                    interval = "10000";
                }
                Communicator communicator = new Communicator(readSMSTimer, interval, cdmaPorts, ignoredPorts, baudRate);
                if (readSMSTimer.Equals("Y"))
                {
                    communicator.IncomingSMSHandler += new IncomingSMSHandler(processor.processIncomingSMS);
                }
                communicator.SIMModemStatusHandler += new SIMModemStatusHandler(processor.processSIMModemStatusUpdate);
                communicator.OpenAllPorts();
                communicator.CheckSIMModem();
                communicator.ActivateIncomingSMSIndicator();

                processor.Communicator = communicator;
                processor.SocketClient = socketClient;

                HttpServer httpServer;
                if (args.GetLength(0) > 0)
                {
                    httpServer = new MyHttpServer(Convert.ToInt16(args[0]));
                }
                else
                {
                    httpServer = new MyHttpServer(Int32.Parse(localPort));
                }
                httpServer.Communicator = communicator;
                Thread server = new Thread(new ThreadStart(httpServer.listen));
                server.Start();
            }
            catch (OutOfMemoryException ex)
            {
                // Do whatever you need.
            }
        }
    }
}
