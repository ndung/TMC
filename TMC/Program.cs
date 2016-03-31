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

            string serverID = ConfigurationManager.AppSettings["ServerID"];
            //Console.WriteLine("serverID:" + serverID);

            Processor processor = new Processor();

            SocketClient socketClient = new SocketClient(serverID);
            socketClient.IncomingSocketMessageHandler += new IncomingSocketMessageHandler(processor.processIncomingSocketMessage);
            Thread thread = new Thread(new ThreadStart(socketClient.Connect));
            thread.Start();

            string readSMSTimer = ConfigurationManager.AppSettings["ReadSMSTimer"];
            string cdmaPorts = ConfigurationManager.AppSettings["CDMAPorts"];

            Communicator communicator = new Communicator(readSMSTimer, cdmaPorts);
            if (readSMSTimer.Equals("Y"))
            {
                communicator.IncomingSMSHandler += new IncomingSMSHandler(processor.processIncomingSMS);
            }
            communicator.SIMModemStatusHandler += new SIMModemStatusHandler(processor.processSIMModemStatusUpdate);
            communicator.OpenAllPorts();
            communicator.CheckSIMModem();

            processor.Communicator = communicator;
            processor.SocketClient = socketClient;

            HttpServer httpServer;
            if (args.GetLength(0) > 0)
            {
                httpServer = new MyHttpServer(Convert.ToInt16(args[0]));
            }
            else
            {
                httpServer = new MyHttpServer(8080);
            }
            httpServer.Communicator = communicator;
            Thread server = new Thread(new ThreadStart(httpServer.listen));
            server.Start();
        }
    }
}
