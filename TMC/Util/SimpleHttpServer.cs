﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using TMC.ModemPool;

// offered to the public domain for any use with no restriction
// and also with no warranty of any kind, please enjoy. - David Jeske. 

// simple HTTP explanation
// http://www.jmarshall.com/easy/http/

namespace TMC.Util
{

    public class HttpProcessor {
        
        public TcpClient socket;        
        public HttpServer srv;

        private Stream inputStream;
        public StreamWriter outputStream;

        public String http_method;
        public String http_url;
        public String http_protocol_versionstring;
        public Hashtable httpHeaders = new Hashtable();


        private static int MAX_POST_SIZE = 10 * 1024 * 1024; // 10MB

        public HttpProcessor(TcpClient s, HttpServer srv) {
            this.socket = s;
            this.srv = srv;                   
        }
        
        private string streamReadLine(Stream inputStream) {
            int next_char;
            string data = "";
            while (true) {
                next_char = inputStream.ReadByte();
                if (next_char == '\n') { break; }
                if (next_char == '\r') { continue; }
                if (next_char == -1) { Thread.Sleep(1); continue; };
                data += Convert.ToChar(next_char);
            }            
            return data;
        }
        public void process() {                        
            // we can't use a StreamReader for input, because it buffers up extra data on us inside it's
            // "processed" view of the world, and we want the data raw after the headers
            inputStream = new BufferedStream(socket.GetStream());

            // we probably shouldn't be using a streamwriter for all output from handlers either
            outputStream = new StreamWriter(new BufferedStream(socket.GetStream()));
            try {
                parseRequest();
                readHeaders();
                if (http_method.Equals("GET")) {
                    handleGETRequest();
                } else if (http_method.Equals("POST")) {
                    handlePOSTRequest();
                }
            } catch (Exception e) {
                //Console.WriteLine("Exception: " + e.ToString());
                writeFailure();
            }
            outputStream.Flush();
            // bs.Flush(); // flush any remaining output
            inputStream = null; outputStream = null; // bs = null;            
            socket.Close();             
        }

        public void parseRequest() {
            String request = streamReadLine(inputStream);
            string[] tokens = request.Split(' ');
            if (tokens.Length != 3) {
                throw new Exception("invalid http request line");
            }
            http_method = tokens[0].ToUpper();
            http_url = tokens[1];
            http_protocol_versionstring = tokens[2];

            //Console.WriteLine("starting: " + request);
        }

        public void readHeaders() {
            //Console.WriteLine("readHeaders()");
            String line;
            while ((line = streamReadLine(inputStream)) != null) {
                if (line.Equals("")) {
                    //Console.WriteLine("got headers");
                    return;
                }
                
                int separator = line.IndexOf(':');
                if (separator == -1) {
                    throw new Exception("invalid http header line: " + line);
                }
                String name = line.Substring(0, separator);
                int pos = separator + 1;
                while ((pos < line.Length) && (line[pos] == ' ')) {
                    pos++; // strip any spaces
                }
                    
                string value = line.Substring(pos, line.Length - pos);
                //Console.WriteLine("header: {0}:{1}",name,value);
                httpHeaders[name] = value;
            }
        }

        public void handleGETRequest() {
            srv.handleGETRequest(this);
        }

        private const int BUF_SIZE = 4096;
        public void handlePOSTRequest() {
            // this post data processing just reads everything into a memory stream.
            // this is fine for smallish things, but for large stuff we should really
            // hand an input stream to the request processor. However, the input stream 
            // we hand him needs to let him see the "end of the stream" at this content 
            // length, because otherwise he won't know when he's seen it all! 

            //Console.WriteLine("get post data start");
            int content_len = 0;
            MemoryStream ms = new MemoryStream();
            if (this.httpHeaders.ContainsKey("Content-Length")) {
                 content_len = Convert.ToInt32(this.httpHeaders["Content-Length"]);
                 if (content_len > MAX_POST_SIZE) {
                     throw new Exception(
                         String.Format("POST Content-Length({0}) too big for this simple server",
                           content_len));
                 }
                 byte[] buf = new byte[BUF_SIZE];              
                 int to_read = content_len;
                 while (to_read > 0) {  
                     //Console.WriteLine("starting Read, to_read={0}",to_read);

                     int numread = this.inputStream.Read(buf, 0, Math.Min(BUF_SIZE, to_read));
                     //Console.WriteLine("read finished, numread={0}", numread);
                     if (numread == 0) {
                         if (to_read == 0) {
                             break;
                         } else {
                             throw new Exception("client disconnected during post");
                         }
                     }
                     to_read -= numread;
                     ms.Write(buf, 0, numread);
                 }
                 ms.Seek(0, SeekOrigin.Begin);
            }
            //Console.WriteLine("get post data end");
            srv.handlePOSTRequest(this, new StreamReader(ms));

        }

        public void writeSuccess(string content_type="text/html") {
            outputStream.WriteLine("HTTP/1.0 200 OK");            
            outputStream.WriteLine("Content-Type: " + content_type);
            outputStream.WriteLine("Connection: close");
            outputStream.WriteLine("");
        }

        public void writeFailure() {
            outputStream.WriteLine("HTTP/1.0 404 File not found");
            outputStream.WriteLine("Connection: close");
            outputStream.WriteLine("");
        }
    }

    public abstract class HttpServer {

        protected int port;
        TcpListener listener;
        bool is_active = true;
        public Communicator Communicator
        {
            get; set;
        }
       
        public HttpServer(int port) {
            this.port = port;
        }
        
        public void listen() {
            listener = new TcpListener(port);
            listener.Start();
            while (is_active) {                
                TcpClient s = listener.AcceptTcpClient();
                HttpProcessor processor = new HttpProcessor(s, this);
                Thread thread = new Thread(new ThreadStart(processor.process));
                thread.Start();
                Thread.Sleep(1);
            }
        }

        public abstract void handleGETRequest(HttpProcessor p);
        public abstract void handlePOSTRequest(HttpProcessor p, StreamReader inputData);
    }

    public class MyHttpServer : HttpServer {
        public MyHttpServer(int port)
            : base(port) {
        }
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public override void handleGETRequest(HttpProcessor p)
        {

            if (p.http_url.Equals("/Test.png")) {
                Stream fs = File.Open("../../Test.png", FileMode.Open);

                p.writeSuccess("image/png");
                fs.CopyTo(p.outputStream.BaseStream);
                p.outputStream.BaseStream.Flush();
            }
            Dictionary<string, string> pars = new Dictionary<string, string>();

            if (p.http_url.Contains("?")) {
                string parameter = DecodeUrlString(p.http_url.Substring(p.http_url.IndexOf("?") + 1)).Trim();
                string[] parameters = parameter.Split('&');
                Console.WriteLine("parameter:" + parameter);
                
                if (!parameter.Equals("")) { 
                    foreach (string par in parameters)
                    {
                        string key = par.Split('=')[0];
                        string val = par.Split('=')[1];
                        pars.Add(key, val);
                    }
                }
            }
            
            if (p.http_url.StartsWith("/sendUSSD"))
            {
                string port = pars["port"];
                string cmd = pars["cmd"];

                string response = Communicator.SendUSSD( port, cmd );
                p.writeSuccess();
                p.outputStream.WriteLine(response);
            }
            else if (p.http_url.StartsWith("/sendSMS"))
            {
                string port = pars["port"];
                string msisdn = pars["msisdn"];
                string message = pars["message"];

                string response = Communicator.SendSMS(port, msisdn, message);
                p.writeSuccess();
                p.outputStream.WriteLine(response);
            }
            else if (p.http_url.StartsWith("/checkSignal"))
            {
                string port = pars["port"];
                string response = Communicator.CheckSignal(port);
                p.writeSuccess();
                p.outputStream.WriteLine(response);
            }
            else if (p.http_url.StartsWith("/restartModem"))
            {
                string port = pars["port"];
                string response = Communicator.RestartModem(port);
                p.writeSuccess();
                p.outputStream.WriteLine(response);
            }
            else if (p.http_url.StartsWith("/sendCDMASMS"))
            {
                string port = pars["port"];
                string msisdn = pars["msisdn"];
                string message = pars["message"];
                string response = Communicator.SendSMSCDMA(port, msisdn, message);
                p.writeSuccess();
                p.outputStream.WriteLine(response);
            }
            else if (p.http_url.StartsWith("/readSMS"))
            {
                string port = pars["port"];

                string response = Communicator.ReadSMS(port);
                if (response != null)
                {
                    p.writeSuccess();
                    p.outputStream.WriteLine(response);
                }
            }
            else if (p.http_url.StartsWith("/readCDMASMS"))
            {
                string port = pars["port"];

                string response = Communicator.ReadSMSCDMA(port);
                if (response != null)
                {
                    p.writeSuccess();
                    p.outputStream.WriteLine(response);
                }
            }
            else if (p.http_url.StartsWith("/voiceCall"))
            {
                string port = pars["port"];
                string msisdn = pars["msisdn"];
                string response = Communicator.VoiceCall(port, msisdn);
                if (response != null)
                {
                    p.writeSuccess();
                    p.outputStream.WriteLine(response);
                }
            }
            else if (p.http_url.StartsWith("/activateSIM"))
            {
                string port = pars["port"];

                string response = Communicator.ActivateSIM(port);
                if (response != null)
                {
                    p.writeSuccess();
                    p.outputStream.WriteLine(response);
                }
            }
            else if (p.http_url.StartsWith("/deleteSMS"))
            {
                string port = pars["port"];
                string flag = pars["flag"];

                string response = Communicator.DeleteSMS(port,flag);
                p.writeSuccess();
                p.outputStream.WriteLine(response);
            }
            else if (p.http_url.StartsWith("/deleteCDMASMS"))
            {
                string port = pars["port"];
                string flag = pars["flag"];

                string response = Communicator.DeleteCDMASMS(port, flag);
                p.writeSuccess();
                p.outputStream.WriteLine(response);
            }
            else if (p.http_url.StartsWith("/restartPort"))
            {
                string port = pars["port"];
                
                Communicator.ClosePort(port);
                Communicator.OpenPort(port);
                Communicator.CheckSIMModem();

                p.writeSuccess();
                p.outputStream.WriteLine("ok");
            }
            else if (p.http_url.StartsWith("/checkIMEI"))
            {
                string port = pars["port"];

                String resp = Communicator.CheckIMEI(port);

                p.writeSuccess();
                p.outputStream.WriteLine(resp);
            }
            else if (p.http_url.StartsWith("/getActivePorts2"))
            {
                p.writeSuccess();
                p.outputStream.WriteLine(Communicator.GetActivePortsVer2());
            }
            else if (p.http_url.StartsWith("/getActivePorts"))
            {
                p.writeSuccess();
                p.outputStream.WriteLine(Communicator.GetActivePorts());
            }
            
            /**
            else if (p.http_url.StartsWith("/mtronik"))
            {
                string port = pars["port"];
                string denom = pars["denom"];
                string msisdn = pars["msisdn"];
                Communicator.MTronik(port,denom,msisdn);
            }
            else if (p.http_url.StartsWith("/stokMtronik"))
            {
                string port = pars["port"];
                string pin = pars["pin"];

                Communicator.StokMTronik(port, pin);
            }*/
            /**
            Console.WriteLine("request: {0}", p.http_url);
            p.writeSuccess();
            p.outputStream.WriteLine("<html><body><h1>test server</h1>");
            p.outputStream.WriteLine("Current Time: " + DateTime.Now.ToString());
            p.outputStream.WriteLine("url : {0}", p.http_url);

            p.outputStream.WriteLine("<form method=post action=/form>");
            p.outputStream.WriteLine("<input type=text name=foo value=foovalue>");
            p.outputStream.WriteLine("<input type=submit name=bar value=barvalue>");
            p.outputStream.WriteLine("</form>");*/
        }

        public override void handlePOSTRequest(HttpProcessor p, StreamReader inputData) {
            //Console.WriteLine("POST request: {0}", p.http_url);
            string data = inputData.ReadToEnd();

            p.writeSuccess();
            p.outputStream.WriteLine("<html><body><h1>test server</h1>");
            p.outputStream.WriteLine("<a href=/test>return</a><p>");
            p.outputStream.WriteLine("postbody: <pre>{0}</pre>", data);
            

        }
        
        private static string DecodeUrlString(string url)
        {
            string newUrl;
            while ((newUrl = Uri.UnescapeDataString(url)) != url)
                url = newUrl;
            return newUrl;
        }
    }
    

}



