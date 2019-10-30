using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;

namespace HTTPServer
{
    public class HTTPRequest
    {
        private String _method = "GET";
        private String _httpversion = "HTTP/1.1";
        private String _uri = "";
        private byte[] _bodybytes = new byte[0];
        private Dictionary<String, String> _headers = new Dictionary<string, string>();
        public String HTTP_METHOD
        {
            get
            {
                return this._method;
            }
        }
        public String HTTP_VERSION
        {
            get
            {
                return this._httpversion;
            }
        }
        public String HTTP_URI
        {
            get
            {
                return this._uri;
            }
        }
        public Dictionary<String, String> Headers
        {
            get
            {
                return this._headers;
            }
        }
        public byte[] BodyRaw
        {
            get
            {
                return this._bodybytes;
            }
            set
            {
                this._bodybytes = value;
                parseBody();
            }
        }


        public HTTPRequest(byte[] buffer)
        {
            string s = System.Text.Encoding.UTF8.GetString(buffer, 0, buffer.Length);

            int headerEnd = s.IndexOf("\r\n\r\n");
            string s1 = s.Substring(0, headerEnd);

            String[] lines = s1.Split(new String[] { "\r\n" }, 2, StringSplitOptions.None);
            String[] line0 = lines[0].Split(new String[] { " " }, StringSplitOptions.None);
            if (line0.Length != 3)
            {
                return;
            }

            // possibly valid - some basic sanity checks should really be here
            this._httpversion = line0[2].ToUpper();
            this._method = line0[0].ToUpper();
            this._uri = line0[1];
            

            // parse the headers
            this._headers = parseHeaders(s1);

            // body
            int bodyLength = buffer.Length - (headerEnd + 4);
            this._bodybytes = new byte[bodyLength];
            Array.Copy(buffer, headerEnd + 4, this._bodybytes, 0, bodyLength);
            parseBody();

        }

        private Dictionary<String, String> parseHeaders(String Buffer)
        {
            Dictionary<String, String> Headers = new Dictionary<string, string>();

            String[] lines = Buffer.Split(new String[] { "\r\n" }, StringSplitOptions.None);
            foreach (String line in lines)
            {
                if (line == "")
                {
                    // empty line - this should be the end of the headers
                    break;
                }
                String[] keypair = line.Split(new char[] { ':' }, 2);
                if (keypair.Length == 2)
                {
                    keypair[0] = keypair[0].ToLower();
                    keypair[1] = keypair[1].TrimStart();
                    Headers.Add(keypair[0], keypair[1]);
                }
            }

            return Headers;

        }

        private void parseBody()
        {
            // try to handle content types for forms and files
            if (this.HTTP_METHOD == "POST")
            {
                String contentType = "application/x-www-form-urlencoded"; // default content type
                if (this.Headers.Keys.Contains("content-type"))
                {
                    contentType = this.Headers["content-type"].ToLower();
                }

                if (contentType == "application/x-www-form-urlencoded")
                {
                    Console.WriteLine("Attempting URLEndoded decode");
                    // basic key/value pair
                    String s = Encoding.UTF8.GetString(this.BodyRaw);
                    String[] pairs = s.Split(new char[] { '&' });
                    foreach (String pair in pairs)
                    {
                        string[] kv = pair.Split(new char[] { '=' }, 2);
                        String k = kv[0];
                        string v = kv[1];
                        Console.WriteLine("{0}: {1}", k, SimpleHTTP.URLDecode(v));
                    }

                }
                if (contentType.StartsWith("multipart/form-data;"))
                {
                    // not so basic
                    Console.WriteLine("Got form data");
                    int b = this.Headers["content-type"].IndexOf("boundary=");
                    Console.WriteLine("Content-Type: {0}", this.Headers["content-type"]);
                    if (b > 0)
                    {
                        b += 9;
                        String boundary = "--" + this.Headers["content-type"].Substring(b);
                        Console.WriteLine("Boundary:{0}", boundary);
                        byte[] boundaryBytes = Encoding.UTF8.GetBytes(boundary);

                        // save the request for analysis
                        File.WriteAllBytes("C:\\Temp\\req.bin", this.BodyRaw);

                        List<int> indicies = new List<int> { };

                        // loop over the bytes
                        for (int i = 0; i< this._bodybytes.Length - boundaryBytes.Length; i++)
                        {
                            if (this.BodyRaw[i] == boundaryBytes[0])
                            {
                                bool match = true; // so far we match
                                // we've at least matched the first byte of the boundary - check the rest
                                for (int x=1; x<boundaryBytes.Length; x++)
                                {
                                    if (boundaryBytes[x] != this._bodybytes[i + x])
                                    {
                                        match = false;
                                        break; 
                                    }
                                }
                                if (match)
                                {
                                    Console.WriteLine("Got a match at index {0}", i);
                                    indicies.Add(i);
                                }

                            }
                        }

                        // loop over the mime sections
                        for (int i=0; i< indicies.Count-1; i++){
                            Console.WriteLine(indicies[i]);
                            int blockStart = indicies[i] + boundaryBytes.Length;
                            int blockLen = Math.Min(indicies[i + 1], this._bodybytes.Length) - blockStart;

                            Console.WriteLine("Block begins at offset {0} and is {1} bytes long", blockStart, blockLen);

                            // search for \r\n\r\n
                        }
                        
                    }
                    
                }
            }
            
        }


    }
}
