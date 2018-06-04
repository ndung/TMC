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

        //private readonly System.Timers.Timer timer;
        private int interval = 10000;
        private string[] cdmaPorts;
        private List<string> ignoredPorts;
        private string baudRate = "115200";

        public Communicator(string readSMSTimer, string readSMSInterval, string cdmaPort, string ignoredPort, string baudRateStr)
        {
            modemMap = new Dictionary<string, PortHandler>();
            simMap = new Dictionary<string, PortHandler>();

            System.Timers.Timer timer = new System.Timers.Timer { Interval = interval };
            timer.Elapsed += TimerTick;
            timer.Start();

            if (readSMSTimer.Equals("Y"))
            {
                System.Timers.Timer smsReaderTimer = new System.Timers.Timer { Interval = Int32.Parse(readSMSInterval) };
                smsReaderTimer.Elapsed += ReadSMSTimerTick;
                smsReaderTimer.Start();    
            }

            cdmaPorts = cdmaPort.Split(',');
            ignoredPorts = new List<string>(ignoredPort.Split(','));
            baudRate = baudRateStr;
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
            ignoredPorts.ForEach(Console.WriteLine);
            foreach (string portName in simMap.Keys)
            {
                if (!ignoredPorts.Contains(portName))
                {
                    // the array contains the string and the pos variable
                    // will have its position in the array
                    
                    rs = rs + portName + ",";
                } 
            }
            return rs;
        }

        public string GetActivePortsVer2()
        {
            string rs = "";
            ignoredPorts.ForEach(Console.WriteLine);
            foreach (string portName in simMap.Keys)
            {
                if (!ignoredPorts.Contains(portName))
                {
                    string signal = CheckSignal(portName);
                    string imei = CheckIMEI(portName);

                    rs = rs + portName + "|"+ imei + "|" + signal + ";";
                }
            }
            return rs;
        }

        public void OpenAllPorts()
        {
            foreach (string portName in GetAllPorts())
            {
                if (!ignoredPorts.Contains(portName))
                {
                    OpenPort(portName);
                }
            }
        }

        public void OpenPort(string portName)
        {
            PortHandler portHandler = new PortHandler(this);
            bool opened = portHandler.OpenPort(portName, baudRate);
            Console.WriteLine("open "+portName+"="+ opened);
            if (opened)
            {
                modemMap.Add(portName, portHandler);
            }else
            {
                ignoredPorts.Add(portName);
            }
        }

        public void CloseAllPorts()
        {
            foreach (string portName in GetAllPorts())
            {
                if (!ignoredPorts.Contains(portName))
                {
                    ClosePort(portName);
                }
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

        public void ActivateIncomingSMSIndicator()
        {
            foreach (string portName in modemMap.Keys)
            {

                Thread thread = new Thread(() => ActivateIncomingSMSIndicator(portName));
                thread.Start();
                //ThreadPool.QueueUserWorkItem(new WaitCallback(ThreadCheckSIMModem), portName);
            }
        }

        private void ThreadCheckSIMModem(Object stateInfo)
        {
            CheckSIMModem((string)stateInfo);
        }

        Dictionary<string, int> counterMap = new Dictionary<string, int>();

        private string CheckSIMModem(string portName)
        {
            if (!counterMap.ContainsKey(portName))
            {
                counterMap.Add(portName,0);
            }
            PortHandler handler = modemMap[portName];
            string response = handler.ProcessCommand(CommandType.CHECK_SIM_MODEM, new object[] { });
            if (response != null)
            {
                Console.WriteLine("check " + portName + "=" + response);
                if (response.Equals(Constants.SIM_READY))
                {
                    if (!simMap.ContainsKey(portName))
                    {
                        counterMap[portName] = 0;
                        log.Info(portName + " IS READY");
                        simMap.Add(portName, handler);
                    }
                }
                else
                {
                    if (simMap.ContainsKey(portName))
                    {
                        if (counterMap.ContainsKey(portName))
                        {
                            int counter = counterMap[portName];
                            counter = counter + 1;
                            if (counter > 5)
                            {
                                log.Info(portName + " IS NOT READY");
                                counterMap[portName] = counter;
                                simMap.Remove(portName);
                            }
                        }                        
                    }
                }
                OnStatusUpdate(portName, response);
            }
            return response;
        }

        private void ActivateIncomingSMSIndicator(string portName)
        {
            PortHandler handler = modemMap[portName];
            handler.ProcessCommand(CommandType.ACTIVATE_INCOMING_SMS_INDICATOR, new object[] { });            
        }

        public string RestartModem(string strPortName)
        {
            if (!simMap.ContainsKey(strPortName))
            {
                return "Modem port or SIM is not ready";
            }
            else
            {
                PortHandler handler = simMap[strPortName];
                return handler.ProcessCommand(CommandType.RESTART_MODEM, new object[] { }); ;
            }
        }

        public string CheckSignal(string strPortName)
        {
            if (!simMap.ContainsKey(strPortName))
            {
                return "Modem port or SIM is not ready";
            }
            else
            {
                PortHandler handler = simMap[strPortName];
                return handler.ProcessCommand(CommandType.CHECK_SIGNAL, new object[] { }); ;
            }
        }

        public string CheckIMEI(string strPortName)
        {
            if (!simMap.ContainsKey(strPortName))
            {
                return "Modem port or SIM is not ready";
            }
            else
            {
                PortHandler handler = simMap[strPortName];
                return handler.ProcessCommand(CommandType.CHECK_IMEI, new object[] { }); ;
            }
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

        public void OnIncomingSMS(string comPort, string message)
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
        }

        private void ReadSMSTimerTick(object sender, EventArgs e)
        {
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

        public void ReadIncomingSMS(string portName, string location)
        {
            Thread thread = new Thread(() => ReadNewSMS(portName, location));
            thread.Start();
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

        private string ReadNewSMS(string strPortName, string location)
        {
            if (simMap.ContainsKey(strPortName))
            {
                PortHandler handler = simMap[strPortName];
                string response = handler.ProcessCommand(CommandType.READ_NEW_SMS, new object[] { location });
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
       
    }
}
