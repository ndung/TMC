﻿using System;
using System.Collections.Generic;
using System.Threading;
using TMC.ModemPool;
using TMC.Sockets;

namespace TMC
{
    class Processor
    {
        Dictionary<string, string> lastSMS = new Dictionary<string, string>();

        public SocketClient SocketClient
        {
            get; set;
        }

        public Communicator Communicator
        {
            get; set;
        }

        private const int INTERVAL = 30000;
        public Processor()
        {
            timer = new System.Timers.Timer { Interval = INTERVAL };
            timer.Elapsed += TimerTick;
            timer.Start();
        }

        private static readonly DateTime Jan1st1970 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long CurrentTimeMillis()
        {
            return (long)(DateTime.UtcNow - Jan1st1970).TotalMilliseconds;
        }

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private void processIncomingSocketMessageThread(object obj, EventArgs args)
        {
            IncomingSocketMessageEventArgs arg = (IncomingSocketMessageEventArgs)args;
            log.Info("received message from server : [" + arg.Message + "]");

            if (arg.Message.StartsWith("USSD"))
            {
                string id = arg.Message.Substring(4, 6);
                string comPort = arg.Message.Substring(10, 5);
                string command = arg.Message.Substring(15);
                string response = Communicator.SendUSSD(comPort.Trim(), command);
                if (response != null)
                {
                    response = response.Trim();
                    response = response.Replace("OK", "");
                    response = response.Replace("\r", "");
                    response = response.Replace("\n", "");
                    if (response.Contains("+CUSD:"))
                    {
                        response = response.Substring(response.IndexOf("+CUSD:"));
                        response = response.Replace("+CUSD:", "");

                        if (response.Contains("\""))
                            response = response.Substring(response.IndexOf("\"") + 1);

                        if (response.Contains("\""))
                            response = response.Substring(0, response.IndexOf("\""));
                    }
                    response = response.Trim();
                    SocketClient.Send(id + response);
                }
            }
            else if (arg.Message.StartsWith("SEND"))
            {
                string id = arg.Message.Substring(4, 6);
                string comPort = arg.Message.Substring(10, 5);
                string msisdn = arg.Message.Substring(15, 15).Trim();
                string message = arg.Message.Substring(30);
                string response = Communicator.SendSMS(comPort.Trim(), msisdn, message);
                Console.WriteLine("response:" + response);
                if (response != null)
                {
                    Console.WriteLine("response.Contains:" + response.Contains("+CMGS:"));
                    if (response.Contains("+CMGS:"))
                    {
                        response = response.Substring(response.IndexOf("+CMGS:"));
                        Console.WriteLine("0:" + response);
                        response = response.Replace("+CMGS:", "");
                        Console.WriteLine("1:" + response);
                        response = response.Replace("OK", "");
                        Console.WriteLine("2:" + response);
                        response = response.Replace("\r", "");
                        Console.WriteLine("3:" + response);
                        response = response.Replace("\n", "");
                        Console.WriteLine("4:" + response);
                    }
                    response = response.Trim();
                    Console.WriteLine("5:" + id + response);
                    int n;
                    bool isNumeric = int.TryParse(response, out n);
                    if (isNumeric)
                    {
                        SocketClient.Send(id + "SUKSES. " + response);
                    }
                }
            }
            else if (arg.Message.StartsWith("CDMS"))
            {
                string id = arg.Message.Substring(4, 6);
                string comPort = arg.Message.Substring(10, 5);
                string msisdn = arg.Message.Substring(15, 15).Trim();
                string message = arg.Message.Substring(30);
                string response = Communicator.SendSMSCDMA(comPort.Trim(), msisdn, message);
                if (response != null)
                {
                    if (response.Contains("+CMGS:"))
                    {
                        response = response.Substring(response.IndexOf("+CMGS:"));
                        response = response.Replace("+CMGS:", "");
                        response = response.Replace("OK", "");
                        response = response.Replace("\r", "");
                        response = response.Replace("\n", "");
                    }
                    response = response.Trim();
                    SocketClient.Send(id + "SUKSES. " + response);
                }
            }
            else if (arg.Message.StartsWith("READ"))
            {
                string id = arg.Message.Substring(4, 6);
                string comPort = arg.Message.Substring(10, 5);
                long timeOutTime = CurrentTimeMillis() + 30000;
                lastSMS.Add(comPort, "");
                while (lastSMS[comPort].Equals(""))
                {
                    Thread.Sleep(100);
                    if (CurrentTimeMillis() > timeOutTime)
                    {
                        break;
                    }
                }
                SocketClient.Send(id + lastSMS[comPort]);
                lastSMS.Remove(comPort);
            }
            else if (arg.Message.StartsWith("DELS"))
            {
                string comPort = arg.Message.Substring(4, 5).Trim();
                Communicator.DeleteSMS(comPort, "4");
            }
            else if (arg.Message.StartsWith("CDMD"))
            {
                string comPort = arg.Message.Substring(4, 5).Trim();
                Communicator.DeleteCDMASMS(comPort, "4");
            }
            else if (arg.Message.StartsWith("PORT"))
            {
                string id = arg.Message.Substring(4, 6);
                string ports = Communicator.GetActivePorts();
                if (ports != null)
                {
                    SocketClient.Send(id + ports);
                }
            }
            else if (arg.Message.StartsWith("ACTV"))
            {
                string id = arg.Message.Substring(4, 6);
                string comPort = arg.Message.Substring(10, 5).Trim();
                string response = Communicator.ActivateSIM(comPort);
                if (response != null)
                {
                    response = response.Trim();
                    response = response.Replace("OK", "");
                    response = response.Replace("\r", "");
                    response = response.Replace("\n", "");
                    response = response.Replace("\"", "");

                    SocketClient.Send(id + response);
                }
            }
            else if (arg.Message.StartsWith("CALL"))
            {
                string id = arg.Message.Substring(4, 6);
                string comPort = arg.Message.Substring(10, 5).Trim();
                string dest = arg.Message.Substring(15).Trim();
                string response = Communicator.VoiceCall(comPort, dest);
                if (response != null)
                {
                    response = response.Trim();
                    response = response.Replace("OK", "");
                    response = response.Replace("\r", "");
                    response = response.Replace("\n", "");

                    SocketClient.Send(id + response);
                }
            }
            else if (arg.Message.StartsWith("ISEV"))
            {
                string id = arg.Message.Substring(4, 6);
                string comPort = arg.Message.Substring(10, 5).Trim();
                string msisdn = arg.Message.Substring(15,15).Trim();
                string denom = arg.Message.Substring(30).Trim();
                string response = Communicator.MTronik(comPort, denom, msisdn);
                if (response != null)
                {
                    response = response.Trim();
                    response = response.Replace("OK", "");
                    response = response.Replace("\r", "");
                    response = response.Replace("\n", "");

                    SocketClient.Send(id + response);
                }
            }
            else if (arg.Message.StartsWith("SMTR"))
            {
                string id = arg.Message.Substring(4, 6);
                string comPort = arg.Message.Substring(10, 5).Trim();
                string pin = arg.Message.Substring(15).Trim();
                string response = Communicator.StokMTronik(comPort, pin);
                if (response != null)
                {
                    response = response.Trim();
                    response = response.Replace("OK", "");
                    response = response.Replace("\r", "");
                    response = response.Replace("\n", "");

                    SocketClient.Send(id + response);
                }
            }
        }

        public void processIncomingSocketMessage(object obj, EventArgs args)
        {
            Thread thread = new Thread(() => processIncomingSocketMessageThread(obj, args));
            thread.Start();
        }

        public void processIncomingSMS(object obj, EventArgs args)
        {
            IncomingSMSEventArgs arg = (IncomingSMSEventArgs)args;
            if (arg != null && arg.Message != null)
            {
                string data = arg.Message.Trim();
                data = data.Replace("OK", "");
                data = data.Replace("\r", "");
                data = data.Replace("\n", "");
                data = data.Replace("=", ":");

                if (data.Contains("+CMGL:"))
                {
                    data = data.Substring(data.IndexOf("+CMGL:"));
                    data = data.Replace("+CMGL:", "").Trim();
                    log.Info("1:" + data);
                    data = data.Replace("OK", "");
                    log.Info("2:" + data);
                    data = data.Replace("\r", "");
                    log.Info("3:" + data);
                    data = data.Replace("\n", "");
                    log.Info("4:" + data);
                    data = data.Replace("\"", "");
                    log.Info("5:" + data);
                    data = data.Trim();

                    if (!data.Equals("AT CMGL") && !data.Equals("\"ALL\""))
                    {
                        data = data.Substring(3);
                        if (!data.Equals("ERROR")&&!data.Equals(""))
                        {
                            if (lastSMS.ContainsKey(arg.COMPort))
                            {
                                lastSMS[arg.COMPort]=data;
                            }
                            else
                            {
                                SocketClient.Send("READSM" + arg.COMPort.PadRight(5, ' ') + data);
                            }
                        }
                    }
                }
            }
        }

        public void processSIMModemStatusUpdate(object obj, EventArgs args)
        {
            if (args != null)
            {
                SIMModemStatusEventArgs arg = (SIMModemStatusEventArgs)args;

                if (SocketClient != null && arg.COMPort != null)
                {
                    SocketClient.Send("STATUS" + arg.COMPort.PadRight(5, ' ') + arg.Status);
                }
            }
        }

        private readonly System.Timers.Timer timer;

        private void TimerTick(object sender, EventArgs e)
        {
            if (SocketClient.IsConnected())
            {
                SocketClient.Send("000000"+Communicator.GetActivePorts());
            }
        }
    }
}
