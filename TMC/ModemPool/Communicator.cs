using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using TMC.Util;

namespace TMC.ModemPool
{
    public delegate void IncomingSMSHandler(object sender, EventArgs args);

    public delegate void SIMModemStatusHandler(object sender, EventArgs args);

    public class Communicator
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        Dictionary<string, PortHandler> modemMap;
        Dictionary<string, PortHandler> simMap;

        public event IncomingSMSHandler IncomingSMSHandler;
        public event SIMModemStatusHandler SIMModemStatusHandler;

        private readonly System.Timers.Timer timer;
        private const int INTERVAL = 10000;
        private string[] cdmaPorts;

        public Communicator(string readSMSTimer, string cdmaPort)
        {
            modemMap = new Dictionary<string, PortHandler>();
            simMap = new Dictionary<string, PortHandler>();

            if (readSMSTimer.Equals("Y"))
            {
                timer = new System.Timers.Timer { Interval = INTERVAL };
                timer.Elapsed += TimerTick;
                timer.Start();
            }
            cdmaPorts = cdmaPort.Split(',');
        }

        public string[] GetAllPorts()
        {
            string[] result = SerialPort.GetPortNames();
            //ThreadPool.SetMinThreads(result.Length, 1);
            return result;
        }

        public string GetActivePorts()
        {
            string rs = "";
            foreach (string portName in simMap.Keys)
            {
                rs = rs + portName + ",";
            }
            return rs;
        }

        public void OpenAllPorts()
        {
            foreach (string portName in GetAllPorts())
            {
                OpenPort(portName);
            }
        }

        public void OpenPort(string portName)
        {
            PortHandler portHandler = new PortHandler();
            portHandler.OpenPort(portName, "115200");
            modemMap.Add(portName, portHandler);
        }

        public void CloseAllPorts()
        {
            foreach (string portName in GetAllPorts())
            {
                ClosePort(portName);
            }
        }

        public void ClosePort(string portName)
        {
            PortHandler handler = modemMap[portName];
            handler.ClosePort();
        }

        public void CheckSIMModem()
        {
            foreach (string portName in modemMap.Keys)
            {
                
                Thread thread = new Thread(() => CheckSIMModem(portName));
                thread.Start();
                //ThreadPool.QueueUserWorkItem(new WaitCallback(ThreadCheckSIMModem), portName);
            }
        }

        private void ThreadCheckSIMModem(Object stateInfo)
        {
            CheckSIMModem((string)stateInfo);
        }

        private string CheckSIMModem(string portName)
        {
            PortHandler handler = modemMap[portName];
            string response = handler.ProcessCommand(CommandType.CHECK_SIM_MODEM, new object[] { });
            if (response != null)
            {
                if (response.Equals(Constants.SIM_READY))
                {
                    if (!simMap.ContainsKey(portName))
                    {
                        log.Info(portName + " IS READY");
                        simMap.Add(portName, handler);
                    }
                }
                else
                {
                    if (simMap.ContainsKey(portName))
                    {
                        log.Info(portName + " IS NOT READY");
                        simMap.Remove(portName);
                    }
                }
                OnStatusUpdate(portName, response);
            }
            return response;
        }

        public string SendUSSD(string strPortName, string command)
        {
            if (!simMap.ContainsKey(strPortName))
            {
                return "Modem port or SIM is not ready";
            }
            else
            {
                PortHandler handler = simMap[strPortName];
                return handler.ProcessCommand(CommandType.SEND_USSD, new object[] { command }); ;
            }
        }

        private void OnIncomingSMS(string comPort, string message)
        {
            if (IncomingSMSHandler != null)
            {
                EventArgs args = new IncomingSMSEventArgs(comPort, message);
                IncomingSMSHandler(this, args);
            }
        }

        private void OnStatusUpdate(string comPort, string status)
        {
            if (SIMModemStatusHandler != null)
            {
                try {
                    EventArgs args = new SIMModemStatusEventArgs(comPort, status);
                    SIMModemStatusHandler(this, args);
                }catch(Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }
        }

        private void TimerTick(object sender, EventArgs e)
        {
            CheckSIMModem();
            foreach (string portName in simMap.Keys)
            {
                //ThreadPool.QueueUserWorkItem(new WaitCallback(ThreadReadSMS), portName);
                Thread thread = new Thread(() => ReadSMS(portName));
                thread.Start();
            }
            foreach (string portName in cdmaPorts)
            {
                Thread thread = new Thread(() => ReadSMSCDMA(portName));
                thread.Start();
            }
        }
        
        private void ThreadReadSMS(Object stateInfo)
        {
            string portName = (String)stateInfo;
            ReadSMS(portName.Trim());
        }

        private void ThreadReadSMSCDMA(Object stateInfo)
        {
            string portName = (String)stateInfo;
            ReadSMSCDMA(portName.Trim());
        }

        public string ReadSMS(string strPortName)
        {
            if (simMap.ContainsKey(strPortName)){
                PortHandler handler = simMap[strPortName];
                string response = handler.ProcessCommand(CommandType.READ_SMS, new object[] { });
                OnIncomingSMS(strPortName, response);
                return response;
            }
            return null;
        }

        public string SendSMS(string comPort, string msisdn, string message)
        {
            if (!simMap.ContainsKey(comPort))
            {
                return "Modem port or SIM is not ready";
            }
            else
            {
                PortHandler handler = simMap[comPort];
                return handler.ProcessCommand(CommandType.SEND_SMS, new object[] { msisdn, message });
            }
        }

        public string SendSMSCDMA(string comPort, string msisdn, string message)
        {
            if (!modemMap.ContainsKey(comPort))
            {
                return "Modem port or SIM is not ready";
            }
            else
            {
                PortHandler handler = modemMap[comPort];
                return handler.ProcessCommand(CommandType.SEND_SMS_CDMA, new object[] { msisdn, message });
            }
        }

        public string ReadSMSCDMA(string strPortName)
        {
            if (modemMap.ContainsKey(strPortName))
            {
                PortHandler handler = modemMap[strPortName];
                string response = handler.ProcessCommand(CommandType.READ_SMS_CDMA, new object[] { });
                OnIncomingSMS(strPortName, response);
                return response;
            }
            return null;
        }

        public string DeleteSMS(string comPort, string deleteFlag)
        {
            if (!simMap.ContainsKey(comPort))
            {
                return "Modem port or SIM is not ready";
            }
            else
            {
                PortHandler handler = simMap[comPort];
                return handler.ProcessCommand(CommandType.DELETE_SMS, new object[] { deleteFlag });
            }
        }

        public string DeleteCDMASMS(string comPort, string deleteFlag)
        {
            if (!modemMap.ContainsKey(comPort))
            {
                return "Modem port or SIM is not ready";
            }
            else
            {
                PortHandler handler = modemMap[comPort];
                return handler.ProcessCommand(CommandType.DELETE_SMS_CDMA, new object[] { deleteFlag });
            }
        }

        public string VoiceCall(string comPort, string msisdn)
        {
            if (!simMap.ContainsKey(comPort))
            {
                return "Modem port or SIM is not ready";
            }
            else
            {
                PortHandler handler = simMap[comPort];
                return handler.ProcessCommand(CommandType.VOICE_CALL, new object[] { msisdn });
            }
        }

        public string ActivateSIM(string comPort)
        {
            if (!simMap.ContainsKey(comPort))
            {
                return "Modem port or SIM is not ready";
            }
            else
            {
                PortHandler handler = simMap[comPort];
                return handler.ProcessCommand(CommandType.SIM_ACTIVATION, new object[] { });
            }
        }

        public string MTronik(string comPort, string denom, string msisdn)
        {
            if (!simMap.ContainsKey(comPort))
            {
                return "Modem port or SIM is not ready";
            }
            else
            {
                PortHandler handler = simMap[comPort];
                return handler.ProcessCommand(CommandType.MTRONIK, new object[] {denom,msisdn});
            }
        }

        public string StokMTronik(string comPort, string pin)
        {
            if (!simMap.ContainsKey(comPort))
            {
                return "Modem port or SIM is not ready";
            }
            else
            {
                PortHandler handler = simMap[comPort];
                return handler.ProcessCommand(CommandType.STOK_MTRONIK, new object[] { pin });
            }
        }
    }
}
