using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;

namespace VLCTestCode
{
    public partial class Form1 : Form
    {
        public enum VLC_State { Playing, Paused, Stopped };


        private Process VLCProcess = null;
        
        private const String VLCPath = "c:\\Program Files\\VideoLAN\\VLC\\vlc.exe";
        private const String TestFile = "C:\\Users\\pprice\\Dropbox\\JoCo Karaoke\\MP4\\karaoke_mr_fancy_pants.mp4";
        private const String VLCparams = "--fullscreen --control=rc --rc-quiet --rc-host=localhost:9876 --intf=qt"; //none

        private TcpClient ControlSocket = new TcpClient();

        public VLC_State PlayerState = VLC_State.Stopped;
        

        public Form1()
        {
            InitializeComponent();
        }

        public void LogMessage(String message, Exception e)
        {
            // Dummy method to ensure code compatibility with Intel Unite SDK
            Console.WriteLine(message);
        }

        private async void connect_to_vlcAsync()
        {
            while (ControlSocket.Connected == false)
            {
                try
                {
                    await ControlSocket.ConnectAsync("localhost", 9876);
                }
                catch
                {
                    LogMessage("Connect failed. Retrying.", null);
                }
            }

            LogMessage("Connected", null);

        }

        private async void read_vlcAsync(TextBox textBox) {
            byte[] buffer = new byte[2048];
            String message;
            NetworkStream stream = null;
            while (true){
                if (ControlSocket.Connected)
                {
                    if (stream == null)
                    {
                        stream = ControlSocket.GetStream();
                    }
                    int datalen = await stream.ReadAsync(buffer, 0, 2048);
                    if (datalen == 0)
                    {
                        // vlc has quit. die
                        return;
                    }
                    message = Encoding.ASCII.GetString(buffer, 0, datalen);
                    //LogMessage(message, null);
                    textBox.Text += message;
                    ParseVLC_Message(message);
                }
                else
                {
                    stream = null;
                }
            }
            

        }

        private void ParseVLC_Message(String message)
        {
            if (message.Length > 19 & message.Substring(0,13) == "status change")
            {
                // handle status change message
                String inner_message = message.Substring(17);
                inner_message = inner_message.Substring(0, inner_message.IndexOf(" )"));
                String[] state = inner_message.Split(":".ToCharArray(), 2);
                state[1] = state[1].Trim();
                switch (state[0])
                {
                    case "play state":
                        if (state[1] == "3")
                        {
                            LogMessage("Playing", null);
                            PlayerState = VLC_State.Playing;
                        }
                        break;
                    case "stop state":
                        if (state[1] == "0")
                        {
                            LogMessage("Stopped", null);
                            PlayerState = VLC_State.Stopped;
                        }
                        break;
                    case "pause state":
                        if (state[1] == "3")
                        {
                            LogMessage("Paused", null);
                            PlayerState = VLC_State.Paused;
                        }
                        break;
                    default:
                        LogMessage(state[0], null);
                        LogMessage(state[1], null);
                        break;
                }
            }
        }

        private void ShowDriveInfo()
        {
            DriveInfo[] allDrives = DriveInfo.GetDrives();

            foreach (DriveInfo d in allDrives)
            {
                Console.WriteLine("Drive {0}", d.Name);
                Console.WriteLine("  Drive type: {0}", d.DriveType);
                if (d.IsReady == true)
                {
                    Console.WriteLine("  Volume root: {0}", d.RootDirectory.Name);
                    Console.WriteLine("  Volume label: {0}", d.VolumeLabel);
                    Console.WriteLine("  File system: {0}", d.DriveFormat);
                    Console.WriteLine(
                        "  Available space to current user:{0, 15} bytes",
                        d.AvailableFreeSpace);

                    Console.WriteLine(
                        "  Total available space:          {0, 15} bytes",
                        d.TotalFreeSpace);

                    Console.WriteLine(
                        "  Total size of drive:            {0, 15} bytes ",
                        d.TotalSize);
                }
            }
        }


        private void Form1_Load(object sendery, EventArgs e)
        {
            

        }

        private void SendCommand(String Command)
        {
            if (ControlSocket.Connected)
            {
                NetworkStream stream = ControlSocket.GetStream();
                stream.Write(Encoding.ASCII.GetBytes(Command + "\r\n"), 0, Command.Length + 2);
            }
            else
            {
                LogMessage("Control Socket is not connected!", null);
            }
            
        }

        private async void poke_vlcAsync()
        {
            while (true)
            {
                if (ControlSocket.Connected)
                {
                    SendCommand("");
                }
                await Task.Run(() => Thread.Sleep(500));
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            SendCommand("clear");
            SendCommand("add \"" + TestFile + "\"");
            SendCommand("play");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            SendCommand("stop");
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            SendCommand("quit");
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                // enter!
                SendCommand(textBox2.Text);
                textBox2.Text = "";
            }

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
