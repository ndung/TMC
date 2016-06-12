using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Threading;
using TMC.Util;

namespace TMC.ModemPool
{
    class PortHandler
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private SerialPort serialPort;

        public AutoResetEvent receiveNow;

        private string portName;
        private string baudRate;

        //Open Port
        public bool OpenPort(string strPortName, string strBaudRate)
        {
            try
            {
                this.portName = strPortName;
                this.baudRate = strBaudRate;
                serialPort = null;
                receiveNow = new AutoResetEvent(false);
                serialPort = new SerialPort();
                serialPort.PortName = strPortName;
                serialPort.BaudRate = Convert.ToInt32(strBaudRate);               //updated by Anila (9600)
                serialPort.DataBits = 8;
                serialPort.StopBits = StopBits.One;
                serialPort.Parity = Parity.None;
                serialPort.ReadTimeout = 300;
                serialPort.WriteTimeout = 300;
                serialPort.Encoding = Encoding.GetEncoding("iso-8859-1");
                serialPort.DataReceived += new SerialDataReceivedEventHandler(port_DataReceived);
                serialPort.Open();
                serialPort.DtrEnable = true;
                serialPort.RtsEnable = true;

                log.Info("Connected at Port " + strPortName);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("opening port exception: " + ex.StackTrace);
                log.Error("opening port exception: " + ex.Message);
            }
            return false;
        }

        //Close Port
        public void ClosePort()
        {
            try
            {
                serialPort.Close();
                serialPort.DataReceived -= new SerialDataReceivedEventHandler(port_DataReceived);
                serialPort = null;
                Thread.Sleep(3000); //3 seconds
            }
            catch (Exception ex)
            {
                Console.WriteLine("closing port exception: " + ex.StackTrace);
                log.Error("closing port exception: " + ex.Message);
            }
        }

        private readonly object syncLock = new object();

        //Execute AT Command
        private string ExecCommand(string command, int responseTimeout, string cmdType)
        {
            try
            {
                lock (syncLock)
                {
                    if (!serialPort.IsOpen)
                    {
                        Console.WriteLine("serial port does not open");

                        ClosePort();
                        OpenPort(portName, baudRate);
                    }

                    // receiveNow = new AutoResetEvent();
                    serialPort.DiscardOutBuffer();
                    serialPort.DiscardInBuffer();
                    receiveNow.Reset();
                    serialPort.Write(command + "\r");

                    //Thread.Sleep(3000); //3 seconds
                    string input = ReadResponse(command, responseTimeout, cmdType);

                    if ((input.Length == 0))// || ((!input.EndsWith("\r\n> ")) && (!input.EndsWith("\r\nOK\r\n"))))
                        return "No success message was received.";
                    return input;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("command "+command+": exec cmd exception " + ex.StackTrace);
                log.Error("command " + command + ": exec cmd exception " + ex.Message);
                ClosePort();
                OpenPort(portName, baudRate);
                return ex.Message;
            }
        }

        //Receive data from port
        private void port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (e.EventType == SerialData.Chars)
                receiveNow.Set();
        }

        private string ReadResponse(string command, int timeout, string cmdType)
        {
            string buffer = string.Empty;
            if (cmdType.Equals("CUSD"))
            {
                bool stop = false;
                bool firstResponse = false;
                try
                {
                    do
                    {
                        if (receiveNow.WaitOne(timeout, false))
                        {
                            string t = serialPort.ReadExisting();
                            buffer += t;

                            if (buffer.Contains("CUSD:") && buffer.EndsWith("\r\n"))
                            {
                                stop = true;
                            }
                            else if (buffer.Contains("CUSD:") && !buffer.EndsWith("\r\n"))
                            {
                                firstResponse = true;
                            }
                            else if (firstResponse && buffer.EndsWith("\r\n"))
                            {
                                stop = true;
                            }
                            else if (buffer.Contains("ERROR"))
                            {
                                stop = true;
                            }
                        }
                        else
                        {
                            return buffer;
                            /**if (buffer.Length > 0)
                                return "Response received is incomplete.";
                            else
                                return "No data received from phone.";*/
                        }
                    }
                    while (!stop);
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message);
                    return ex.StackTrace;
                }
            }
            else if (cmdType.Equals("CPIN"))
            {
                try
                {
                    do
                    {
                        if (receiveNow.WaitOne(timeout, false))
                        {
                            string t = serialPort.ReadExisting();
                            buffer += t;
                        }
                        else
                        {
                            if (buffer.Length > 0)
                                return "Response received is incomplete.";
                            else
                                return "No data received from phone.";
                        }
                    }
                    while (!(buffer.Contains("CPIN:") && buffer.EndsWith("\r\n")) && !buffer.EndsWith("\r\nERROR\r\n"));
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message);
                    return ex.StackTrace;
                }
            }
            else if (cmdType.Equals("ATD"))
            {
                try
                {
                    do
                    {
                        if (receiveNow.WaitOne(timeout, false))
                        {
                            string t = serialPort.ReadExisting();
                            buffer += t;
                        }
                        else
                        {
                            if (buffer.Length > 0)
                                return "Response received is incomplete.";
                            else
                                return "No data received from phone.";
                        }
                    }
                    while (!buffer.Contains("WIND") && !buffer.Contains("OK") && !buffer.EndsWith("\r\nERROR\r\n"));
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message);
                    return ex.StackTrace;
                }
            }
            else if (cmdType.Equals("STGR"))
            {
                try
                {
                    do
                    {
                        if (receiveNow.WaitOne(timeout, false))
                        {
                            string t = serialPort.ReadExisting();
                            buffer += t;
                        }
                        else
                        {
                            if (buffer.Length > 0)
                                return "Response received is incomplete.";
                            else
                                return "No data received from phone.";
                        }
                    }
                    while (!buffer.Contains(">") && !buffer.Contains("STIN:") && !buffer.EndsWith("\r\nERROR\r\n"));
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message);
                    return ex.StackTrace;
                }
            }
            else if (cmdType.Equals("STG0"))
            {
                try
                {
                    if (receiveNow.WaitOne(timeout, false))
                    {
                        string t = serialPort.ReadExisting();
                        buffer += t;
                    }
                    else
                    {
                        if (buffer.Length > 0)
                            return "Response received is incomplete.";
                        else
                            return "No data received from phone.";
                    }

                }
                catch (Exception ex)
                {
                    log.Error(ex.Message);
                    return ex.StackTrace;
                }
            }
            else
            {
                try
                {
                    do
                    {
                        if (receiveNow.WaitOne(timeout, false))
                        {
                            string t = serialPort.ReadExisting();
                            buffer += t;
                        }
                        else
                        {
                            if (buffer.Length > 0)
                                return "Response received is incomplete.";
                            else
                                return "No data received from phone.";
                        }
                    }
                    while (!buffer.EndsWith("\r\nOK\r\n") && !buffer.EndsWith("\r\n> ") && !buffer.EndsWith("\r\nERROR\r\n"));
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message);
                    return ex.StackTrace;
                }
            }
            return buffer;
        }


        private string CheckSIMModem()
        {
            string response = ExecCommand("AT", 300, "AT");
            if (response.Contains("OK"))
            {
                response = ExecCommand("AT+CPIN?", 300, "CPIN");
                if (response.Contains("READY"))
                {
                    return Constants.SIM_READY;
                }
                else
                {
                    return Constants.NO_SIM;
                }
            }
            else
            {
                return Constants.NO_MODEM;
            }
        }

        private string ProcessUSSD(string command)
        {
            string response = ExecCommand("AT+CMGF=0", 300, "CMGF");

            if (response.Contains("OK"))
            {
                string cmd = "AT+CUSD=1," + command.Trim() + ",15";
                if (command.StartsWith("*"))
                {
                    cmd = "AT+CUSD=1,\"" + command.Trim() + "#\",15";
                }
                response = ExecCommand(cmd, 30000, "CUSD");
            }
            return response;
        }

        private string SelectRegistrationMenu(string response)
        {
            string menu = "1";
            string[] str = response.Split('\n');
            foreach (string s in str)
            {
                if (s.Contains("Registrasi"))
                {
                    string temp = s.Substring(s.IndexOf("STGI") + 6).Trim();
                    string[] submenu = temp.Split(',');
                    menu = submenu[0];
                }
            }
            Thread.Sleep(500);
            response = ExecCommand("AT+STGR=0,1," + menu, 5000, "STGR");
            log.Debug(portName + ":1:[" + response + "]");

            string stin = GetSTIN(response);
            log.Debug(portName + ":1stin:[" + stin + "]");
            if (!stin.Equals("") && !stin.Equals("99"))
            {
                Thread.Sleep(500);
                response = ExecCommand("AT+STGI=" + stin, 5000, "STGI");
                log.Debug(portName + ":2:[" + response + "]");
                Thread.Sleep(500);
                response = ExecCommand("AT+STGR=" + stin + ",1,1", 5000, "STGR");
                log.Debug(portName + ":3:[" + response + "]");

                stin = GetSTIN(response);

                if (stin.Equals("0"))
                {
                    Thread.Sleep(500);
                    response = ExecCommand("AT+STGI=" + stin, 5000, "STGI");
                    log.Debug(portName + ":4:[" + response + "]");

                    return SelectRegistrationMenu(response);
                }
                return stin;

            }
            return response;
        }

        private string GetSTIN(string response)
        {
            if (response.Contains("+STIN"))
            {
                string temp = response.Substring(response.IndexOf("+STIN:") + 6).Trim();
                if (temp.Length > 0)
                {
                    return temp;
                }
            }
            return "";
        }

        private string MTronik(string denom, string msisdn)
        {
            /**string response = ExecCommand("AT+STSF=2,\"5FFFFFFF7F\"", 30000, "STSF");
            response = ExecCommand("AT+STSF=1", 3000, "STSF");
            
            if (!response.Contains("OK"))
            {
                return response;
            }*/

            string response = ExecCommand("AT+STGI=0", 500, "STGI");
            log.Debug(response);
            
            response = ExecCommand("AT+STGR=0,1,1", 5000, "STGR");
            string stin = GetSTIN(response);
            if (stin.Equals("") || stin.Equals("99"))
            {
                return "99";
            }
            response = ExecCommand("AT+STGI=" + stin, 5000, "STGI");
            log.Debug(response);
            response = ExecCommand("AT+STGR=" + stin + ",1", 5000, "STGR");
            response = ExecCommand(msisdn + (char)26, 5000, "STGR");
            stin = GetSTIN(response);
            if (stin.Equals("") || stin.Equals("99"))
            {
                return "99";
            }
            response = ExecCommand("AT+STGI=" + stin, 5000, "STGI");
            log.Debug(response);

            if (denom.Equals("5000"))
            {
                response = ExecCommand("AT+STGR=" + stin + ",1,1", 5000, "STGR");
                stin = GetSTIN(response);
            }
            else if (denom.Equals("10000"))
            {
                response = ExecCommand("AT+STGR=" + stin + ",1,3", 5000, "STGR");
                stin = GetSTIN(response);
            }
            else if (denom.Equals("25000"))
            {
                response = ExecCommand("AT+STGR=" + stin + ",1,5", 5000, "STGR");
                stin = GetSTIN(response);
            }
            else 
            {
                response = ExecCommand("AT+STGR=" + stin + ",1,6", 5000, "STGR");
                stin = GetSTIN(response);

                if (stin.Equals("") || stin.Equals("99"))
                {
                    return "99";
                }

                response = ExecCommand("AT+STGI=" + stin, 5000, "STGI");
                log.Debug("response1:"+response);
                /**
                response = ExecCommand("AT+STGR=" + stin + ",1,1", 5000, "STGR");
                log.Debug("response2:" + response);
                stin = GetSTIN(response);
                log.Debug("response3:" + stin);

                if (stin.Equals("") || stin.Equals("99"))
                {
                    return "99";
                }

                response = ExecCommand("AT+STGI=" + stin, 5000, "STGI");
                log.Debug("response4:"+response);*/
                response = ExecCommand("AT+STGR=" + stin + ",1", 5000, "STGR");
                log.Debug("response5:" + response);
                
                response = ExecCommand(denom + (char)26, 5000, "STGR");
                log.Debug("response6:" + response);

                stin = GetSTIN(response);
            }

            if (stin.Equals("") || stin.Equals("99"))
            {
                return "99";
            }

            response = ExecCommand("AT+STGI=" + stin, 5000, "STGI");
            log.Debug(response);
            response = ExecCommand("AT+STGR=" + stin + ",1,1", 5000, "STGR");
            stin = GetSTIN(response);

            if (stin.Equals("") || stin.Equals("99"))
            {
                return "99";
            }

            response = ExecCommand("AT+STGI=" + stin, 5000, "STGI");
            log.Debug(response);
            response = ExecCommand("AT+STGR=" + stin + ",1", 5000, "STGR");
            response = ExecCommand("123456", 5000, "STGR");
            stin = GetSTIN(response);

            if (stin.Equals("") || stin.Equals("99"))
            {
                return "99";
            }
            response = ExecCommand("AT+STGI=" + stin, 5000, "STGI");
            log.Debug(response);
            response = ExecCommand("AT+STGR=1,1,1", 5000, "STGR");
            log.Debug(response);

            stin = GetSTIN(response);

            if (stin.Equals("") || stin.Equals("99"))
            {
                return "99";
            }
            response = ExecCommand("AT+STGI=" + stin, 5000, "STGI");
            log.Debug(response);
            //response = ExecCommand("AT+STGR=1,1,1", 5000, "STGR");
            //log.Debug(response);
            return response;
        }

        private string StokMTronik(string pin)
        {
            string response = ExecCommand("AT+STGI=0", 500, "STGI");
            log.Debug("0:"+response);

            response = ExecCommand("AT+STGR=0,1,3", 5000, "STGR");
            string stin = GetSTIN(response);
            if (stin.Equals("") || stin.Equals("99"))
            {
                return "99";
            }
            response = ExecCommand("AT+STGI=" + stin, 5000, "STGI");
            log.Debug("1:" + response);

            response = ExecCommand("AT+STGR=" + stin + ",1,1", 5000, "STGR");
            stin = GetSTIN(response);
            if (stin.Equals("") || stin.Equals("99"))
            {
                return "99";
            }
            response = ExecCommand("AT+STGI=" + stin, 5000, "STGI");
            log.Debug("2:"+response);

            response = ExecCommand("AT+STGR=" + stin + ",1,1", 5000, "STGR");
            stin = GetSTIN(response);
            if (stin.Equals("") || stin.Equals("99"))
            {
                return "99";
            }
            response = ExecCommand("AT+STGI=" + stin, 5000, "STGI");
            log.Debug("3:" + response);

            response = ExecCommand("AT+STGR=" + stin + ",1", 5000, "STGR");
            response = ExecCommand(pin, 5000, "STGR");
            stin = GetSTIN(response);
            if (stin.Equals("") || stin.Equals("99"))
            {
                return "99";
            }
            response = ExecCommand("AT+STGI=" + stin, 5000, "STGI");
            log.Debug(response);
            
            return response;
        }

        private string ActivateSIM()
        {
            Dictionary<string, string> map = new Dictionary<string, string>();
            map.Add("Nama", "nama");
            map.Add("Tempat", "tempat");
            map.Add("Tanggal", "01011990");
            map.Add("Alamat", "Jln. Kol. Amir Hamzah");
            map.Add("Nomor", "123456789012345678");
            map.Add("Kota", "Jakarta");
            map.Add("Kode", "15321");

            //string response = ExecCommand("AT+CMGF=1", 30000, "CMGF");
            string response = ExecCommand("AT+STSF=2,\"5FFFFFFF7F\"", 30000, "STSF");
            //response = ExecCommand("AT+CMEE=0", 3000, "CMEE");
            //Console.WriteLine("0:" + response);
            //response = ExecCommand("AT+WIND=15", 30000, "WIND");
            //response = ExecCommand("AT+STSF=0", 3000, "STSF");
            response = ExecCommand("AT+STSF=1", 3000, "STSF");
            //Console.WriteLine("1:" + response);
            //string response = "OK";
            //response = ExecCommand("AT+STIN?", 30000, "STIN");

            if (response.Contains("OK"))
            {
                Thread.Sleep(500);
                response = ExecCommand("AT+STGI=0", 500, "STGI");
                log.Debug(portName + ":0:[" + response + "]");
                int retry = 0;
                while (response.Contains("incomplete") && retry < 5)
                {
                    //response = ExecCommand("AT+CFUN=1", 30000, "CFUN");
                    //ClosePort();
                    //OpenPort(portName, baudRate);
                    //if (response.Contains("OK"))
                    //{
                    Thread.Sleep(500);
                    response = ExecCommand("AT+STGI=0", 500, "STGI");
                    //}
                    log.Debug(portName + ":0:retry:[" + response + "]");
                    retry = retry + 1;
                }

                if (response.Contains("Menu"))
                {
                    string stin = SelectRegistrationMenu(response);

                    if (stin.Equals("3") || stin.Equals("6"))
                    {
                        while (stin.Equals("3") || stin.Equals("6"))
                        {
                            stin = InputData(map, stin);
                        }

                        if (stin.Equals("1"))
                        {
                            Thread.Sleep(500);
                            response = ExecCommand("AT+STGI=" + stin, 5000, "STGI");

                            log.Debug(portName + ":9:[" + response + "]");
                            Thread.Sleep(500);
                            response = ExecCommand("AT+STGR=" + stin + ",1", 5000, "STGR");

                            stin = GetSTIN(response);
                            if (!stin.Equals(""))
                            {
                                Thread.Sleep(500);
                                response = ExecCommand("AT+STGI=" + stin, 5000, "STGI");
                                log.Debug(portName + ":10:[" + response + "]");
                                Thread.Sleep(500);
                                response = ExecCommand("AT+STGR=" + stin + ",1", 5000, "STGR");
                                log.Debug(portName + ":11:[" + response + "]");

                                stin = GetSTIN(response);
                                if (!stin.Equals(""))
                                {
                                    Thread.Sleep(500);
                                    response = ExecCommand("AT+STGI=" + stin, 5000, "STGI");
                                    log.Debug(portName + ":12:[" + response + "]");
                                    Thread.Sleep(500);
                                    response = ExecCommand("AT+STGR=1,1", 5000, "STGR");
                                    log.Debug(portName + ":13:[" + response + "]");

                                    stin = GetSTIN(response);
                                    if (!stin.Equals(""))
                                    {
                                        Thread.Sleep(500);
                                        response = ExecCommand("AT+STGI=" + stin, 5000, "STGI");
                                        log.Debug(portName + ":14:[" + response + "]");
                                        if (response.Contains("+STGI:"))
                                        {
                                            Thread.Sleep(500);
                                            response = response.Substring(response.IndexOf("+STGI:"));
                                            Thread.Sleep(500);
                                            response = response.Replace("+STGI:", "");
                                        }

                                        if (response.Contains("\""))
                                            response = response.Replace("\"", "");

                                        if (response.Contains("\""))
                                            response = response.Replace("\"", "");

                                        response = "SUKSES. " + response.Trim();

                                        string cfun = ExecCommand("AT+CFUN=0", 5000, "CFUN");
                                        log.Debug("cfun0:" + cfun);
                                        Thread.Sleep(3000);
                                        cfun = ExecCommand("AT+CFUN=1", 5000, "CFUN");
                                        log.Debug("cfun1:" + cfun);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return response;
        }

        private string InputData(Dictionary<string, string> map, string stin)
        {
            Thread.Sleep(500);
            string response = ExecCommand("AT+STGI=" + stin, 5000, "STGI");
            log.Debug(portName + ":5:[" + response + "]");

            if (stin.Equals("3"))
            {
                Thread.Sleep(500);
                string temp = response.Substring(response.IndexOf("+STGI:") + 6).Trim();
                string type = temp.Split(',')[0];
                int length = Convert.ToInt32(temp.Split(',')[3]);
                int start = temp.IndexOf('"') + 1;
                temp = temp.Substring(start);
                string input = temp.Substring(0, temp.IndexOf(' '));

                if (map.ContainsKey(input))
                {
                    string value = map[input];
                    Thread.Sleep(500);
                    response = ExecCommand("AT+STGR=" + stin + ",1", 5000, "STGR");
                    log.Debug(portName + ":6:[" + temp + "]");
                    Thread.Sleep(500);
                    if (type.Equals("1"))
                    {
                        response = ExecCommand(value.PadRight(length, ' '), 5000, "STGR");
                    }
                    if (type.Equals("0"))
                    {
                        response = ExecCommand(value.PadLeft(length, '0'), 5000, "STGR");
                    }
                    log.Debug(portName + ":7:[" + response + "]");

                    return GetSTIN(response);
                }
            }
            else if (stin.Equals("6"))
            {

                Thread.Sleep(500);
                response = ExecCommand("AT+STGR=" + stin + ",1,1", 30000, "STGR");
                log.Debug(portName + ":8:[" + response + "]");
                return GetSTIN(response);
            }
            return "";
        }

        private string ReadSMS()
        {
            string response = ExecCommand("AT+CMGF=1", 300, "CMGF");
            if (response.Contains("OK"))
            {
                response = ExecCommand("AT+CMGL=\"ALL\"", 5000, "CMGL");
            }
            return response;
        }

        private string ReadSMSCDMA()
        {
            string r = ExecCommand("AT+CMGL=0", 5000, "CMGL");
            string response = r.Replace("AT+CMGL=0", "");
            int i = 0;
            while (r.Contains("OK"))
            {
                i = i + 1;
                Thread.Sleep(500);
                r = ExecCommand("AT+CMGL="+i, 5000, "CMGL");
                if (!r.Contains("ERROR"))
                {
                    response = response + r.Replace("AT+CMGL="+i, "");
                }
            }
            log.Info("response:[" + response + "]");
            return response;
        }

        private string SendSMS(string msisdn, string message)
        {
            string response = ExecCommand("AT+CMEE=1", 300, "CMEE");
            if (response.Contains("OK"))
            {
                response = ExecCommand("AT+CMGF=1", 300, "CMGF");
                if (response.Contains("OK"))
                {
                    if (msisdn.StartsWith("0"))
                    { //Add +/00 on number
                        msisdn = "+62" + msisdn.Substring(1);
                    }
                    message = message + char.ConvertFromUtf32(26);
                    response = ExecCommand("AT+CMGS=\"" + msisdn + "\"", 300, "CMGS");
                    if (response.Contains(">"))
                    {
                        response = ExecCommand(message, 5000, "CMGS");
                    }
                }
            }
            return response;
        }

        private string SendSMSCDMA(string msisdn, string message)
        {
            /**string response = ExecCommand("AT+CMEE=1", 300, "CMEE");
            if (response.Contains("OK"))
            {
                response = ExecCommand("AT+CMGF=1", 300, "CMGF");
                if (response.Contains("OK"))
                {*/
            var hexString0 = msisdn.Length.ToString("X").PadLeft(2,'0');
            
            byte[] ba = Encoding.Default.GetBytes(msisdn);
            var hexString1 = BitConverter.ToString(ba);
            hexString1 = hexString1.Replace("-", "");

            var hexString3 = message.Length.ToString("X").PadLeft(2, '0');
            
            ba = Encoding.Default.GetBytes(message);
            var hexString2 = BitConverter.ToString(ba);
            hexString2 = hexString2.Replace("-", "");

            string response = ExecCommand("AT+CMGS="+hexString0 + hexString1+ "02" + hexString3 + hexString2, 30000, "CMGS");
            /** }
            }*/
            return response;
        }

        private string DeleteSMS(string deleteFlag)
        {
            Thread.Sleep(500);
            string response = ExecCommand("AT+CMEE=1", 5000, "CMEE");
            if (response.Contains("OK"))
            {
                Thread.Sleep(500);
                response = ExecCommand("AT + CMGD = 1, " + deleteFlag, 5000, "CMGD");
            }
            return response;
        }

        private string DeleteSMSCDMA(string deleteFlag)
        {
            string r = ExecCommand("AT+CMGD=0", 5000, "CMGD");
            string response = r;
            int i = 0;
            while (r.Contains("OK"))
            {
                i = i + 1;
                Thread.Sleep(500);
                r = ExecCommand("AT + CMGD = " + i, 5000, "CMGD");
                if (!r.Contains("ERROR"))
                {
                    response = response + ";" + r;
                }
            }
            return response;
        }

        private string VoiceCall(string msisdn)
        {
            string response = ExecCommand("AT+CREG?", 300, "CREG");
            log.Debug(portName + ":call:1:" + response);
            if (response.Contains("OK"))
            {
                response = ExecCommand("ATD" + msisdn + ";", 10000, "ATD");
                log.Debug(portName + ":call:2" + response);
                if (response.Contains("OK") || response.Contains("WIND"))
                {
                    Thread.Sleep(3000);
                    response = "SUKSES. " + response.Trim();

                    string hangup = ExecCommand("ATH", 3000, "ATH");

                    log.Debug(portName + ":call:3" + response);
                }
            }
            return response;
        }

        public string ProcessCommand(CommandType cmdType, object[] parameters)
        {
            try
            {
                if (serialPort != null)
                {
                    lock (serialPort)
                    {
                        Thread.Sleep(500);
                        if (cmdType == CommandType.CHECK_SIM_MODEM)
                        {
                            return CheckSIMModem();
                        }
                        else if (cmdType == CommandType.SEND_USSD)
                        {
                            string[] cmds = ((string)parameters[0]).Split(',');
                            string response = "";
                            string prevResponse = "";
                            foreach (string cmd in cmds)
                            {
                                response = ProcessUSSD(cmd);
                                if (response.Contains("CUSD: 4"))
                                {
                                    return prevResponse;
                                }
                                prevResponse = response;
                            }
                            return response;
                        }
                        else if (cmdType == CommandType.READ_SMS)
                        {
                            return ReadSMS();
                        }
                        else if (cmdType == CommandType.SEND_SMS)
                        {
                            return SendSMS((string)parameters[0], (string)parameters[1]);
                        }
                        else if (cmdType == CommandType.DELETE_SMS)
                        {
                            return DeleteSMS((string)parameters[0]);
                        }
                        else if (cmdType == CommandType.VOICE_CALL)
                        {
                            return VoiceCall((string)parameters[0]);
                        }
                        else if (cmdType == CommandType.SIM_ACTIVATION)
                        {
                            return ActivateSIM();
                        }
                        else if (cmdType == CommandType.MTRONIK)
                        {
                            return MTronik((string)parameters[0], (string)parameters[1]);
                        }
                        else if (cmdType == CommandType.SEND_SMS_CDMA)
                        {
                            return SendSMSCDMA((string)parameters[0], (string)parameters[1]);
                        }
                        else if (cmdType == CommandType.READ_SMS_CDMA)
                        {
                            return ReadSMSCDMA();
                        }
                        else if (cmdType == CommandType.DELETE_SMS_CDMA)
                        {
                            return DeleteSMSCDMA((string)parameters[0]);
                        }
                        else if (cmdType == CommandType.STOK_MTRONIK)
                        {
                            return StokMTronik((string)parameters[0]);
                        }
                        return null;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                return ex.StackTrace;
            }
        }
    }
}
