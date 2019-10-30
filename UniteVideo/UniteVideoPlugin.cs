using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Intel.CFC.Plugin;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Win32;
using System.IO;
using System.Net;

namespace UniteVideoPlugin
{
    public class UniteVideoPlugin : CFCPlugin
    {
        public enum VLC_State { Playing, Paused, Stopped };

        public VLC_State PlayerState = VLC_State.Stopped;

        const String MAIN_GUID = "30185321-48af-4c85-93e5-000000000000";
        const String STATE = "30185321-48af-4c85-93e5-000000000001";

        const String STOP = "30185321-48af-4c85-93e5-000000000002";
        const String PLAY = "30185321-48af-4c85-93e5-000000000003";
        const String PAUSE = "30185321-48af-4c85-93e5-000000000004";
        const String NAV_UP = "30185321-48af-4c85-93e5-000000000005";
        const String NAV_DOWN = "30185321-48af-4c85-93e5-000000000006";
        const String NAV_LEFT = "30185321-48af-4c85-93e5-000000000007";
        const String NAV_RIGHT = "30185321-48af-4c85-93e5-000000000008";
        const String NAV_OK = "30185321-48af-4c85-93e5-000000000009";
        const String SEEK = "30185321-48af-4c85-93e5-00000000000a";
        const String REWIND = "30185321-48af-4c85-93e5-00000000000b";
        const String SKIP_FORWARD = "30185321-48af-4c85-93e5-00000000000c";
        const String SKIP_BACK = "30185321-48af-4c85-93e5-00000000000d";
        const String MENU = "30185321-48af-4c85-93e5-00000000000e";
        const String INACTIVE = "30185321-48af-4c85-93e5-00000000000f";

        private const String VLCparams = "--fullscreen --control=rc --rc-quiet --rc-host=localhost:9876 --intf=none --no-video-title-show";

        private const int MAX_RECV_BUFFER = 1024;
        private const int LISTEN_PORT = 5050;

        private String VLCPath = null; // need to read this from the registry at startup

        private PluginInfo myInfo = new PluginInfo();
        private PluginUI myUI = new PluginUI();
        private PluginUIElementGroup vidGroup = new PluginUIElementGroup();

        private String HubText = "";
        private bool hasDVD = false;
        private bool currentMediaIsDVD = false;

        private Process VLCProcess = null;
        private TcpClient ControlSocket = new TcpClient();
        private TcpListener URLSocketListener = new TcpListener(IPAddress.Any, LISTEN_PORT);

        // Load HTTP listener
        private HTTPServer HTTP = new HTTPServer();

        private async void ConnectVLC_Async()
        {
            while (ControlSocket.Connected == false)
            {
                try
                {
                    await ControlSocket.ConnectAsync("localhost", 9876);
                }
                catch
                {
                    LogMessage("Connect to VLC failed. Retrying.", null);
                }
            }

            LogMessage("Connected to VLC", null);
            ReadVLC_Async();

        }

        private async void ReadVLC_Async()
        {
            byte[] buffer = new byte[2048];
            String message;
            NetworkStream stream = null;
            while (true)
            {
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
                        LogMessage("!!!!!! VLC has quit !!!!!!", null);
                        return;
                    }
                    message = Encoding.ASCII.GetString(buffer, 0, datalen);
                    LogMessage(message.Substring(0,message.Length-2), null); // log message without CRLF
                    ParseVLC_Message(message);
                }
                else
                {
                    stream = null;
                }
            }
        }

        private async void PokeVLC_Async()
        {
            while (true){
                if (ControlSocket.Connected)
                {
                    SendCommand("");
                }
                await Task.Run(() => Thread.Sleep(500));
            }
        }

        
        private void ParseVLC_Message(String message)
        {
            if (message.Length > 19 & message.Substring(0, 13) == "status change")
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
                            ShowHubToast("Playback started", ResourceToBytes(new Uri("/UniteVideoPlugin;component/disc.png", System.UriKind.Relative)), 1);
                            FireIsHubBackgroundTransparent(true);
                            FireUIUpdated();
                            HubText = "";
                            FireHubTextUpdated();
                        }
                        break;
                    case "stop state":
                        if (state[1] == "0")
                        {
                            LogMessage("Stopped", null);
                            PlayerState = VLC_State.Stopped;
                            //ShowHubToast("Playback stopped", ResourceToBytes(new Uri("/UniteVideoPlugin;component/disc.png", System.UriKind.Relative)), 1);
                            HubText = "";
                            FireHubTextUpdated();
                            FireIsHubBackgroundTransparent(false);
                            FireUIUpdated();
                        }
                        break;
                    case "pause state":
                        if (state[1] == "3")
                        {
                            LogMessage("Paused", null);
                            PlayerState = VLC_State.Paused;
                            ShowHubToast("Playback paused", ResourceToBytes(new Uri("/UniteVideoPlugin;component/disc.png", System.UriKind.Relative)), 1);
                            FireUIUpdated();
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        private void SendCommand(String Command)
        {
            if (ControlSocket.Connected)
            {
                NetworkStream theStream = ControlSocket.GetStream();
                theStream.Write(Encoding.ASCII.GetBytes(Command + "\r\n"), 0, Command.Length + 2);
            }
            else
            {
                LogMessage("Control Socket is not connected!", null);
            }
        }

        private string[] GetDrivePath()
        {
            DriveInfo[] allDrives = DriveInfo.GetDrives();
            try
            {
                foreach (DriveInfo d in allDrives)
                {
                    if (d.DriveType == DriveType.CDRom)
                    {
                        string[] ret = { d.RootDirectory.Name, d.IsReady.ToString() };
                        return ret;
                    }
                }
            }
            catch
            {
                LogMessage("Error enumerating drives. Perhaps one went away?", null);
            }

            // no optical drive found
            return null;
        }
        
        private async void MonitorDVD_Async()
        {
            FireUIUpdated();
            String DiscReady = "";
            while (true)
            {
                String[] r = GetDrivePath();
                bool hasDVDnow = (r != null);
                if ((r!=null) && (DiscReady != r[1]))
                {
                    DiscReady = r[1];
                    FireUIUpdated();
                    // if somebody just ejected the disc while we were playing or paused, we need to tell vlc to stop
                    if ((DiscReady == "False") && (PlayerState == VLC_State.Playing))
                    {
                        SendCommand("stop");
                    }
                    if ((DiscReady == "False") && (PlayerState == VLC_State.Paused))
                    {
                        SendCommand("pause");
                        SendCommand("stop");
                    }
                }

                if (hasDVD != hasDVDnow)
                {
                    hasDVD = hasDVDnow;
                    FireUIUpdated();
                }
                await Task.Run(() => Thread.Sleep(3000));
            }
        }

        private void URLSocket_OnConnect(IAsyncResult ar)
        {
            // get ready for a new connection
            URLSocketListener.BeginAcceptTcpClient(new AsyncCallback(URLSocket_OnConnect), null);

            byte[] recv_buffer = new byte[MAX_RECV_BUFFER];

            TcpClient clientSocket = URLSocketListener.EndAcceptTcpClient(ar);

            LogMessage("Got connection", null);
            // do something here
            NetworkStream stream = clientSocket.GetStream();
            stream.ReadTimeout = 10000; // 10 second timeout before we boot the client
            try
            {
                int bytesRead = stream.Read(recv_buffer, 0, MAX_RECV_BUFFER);
                LogMessage(String.Format("Read {0} bytes from network stream", bytesRead), null);
                String url = Encoding.ASCII.GetString(recv_buffer,0,bytesRead);
                // some sanity checking would be nice

                LogMessage(url, null);
                HubText = "Video Loading. Please wait...";
                currentMediaIsDVD = false;
                FireHubTextUpdated();
                SendCommand("clear");
                SendCommand("add " + url);

            }
            catch (Exception)
            {
                LogMessage("Error reading from socket", null);
            }

            // close the socket
            LogMessage("Closing connection", null);
            clientSocket.Close();

        }

        public void UniteVideo()
        {
            // set up the info object
            myInfo.Id = new Guid(MAIN_GUID);
            myInfo.Company = "P.A. Price";
            myInfo.Description = "Native Video Playback Controls";
            myInfo.Name = "Native Video Playback";

            myUI.pluginInfo = myInfo;

            // Find VLC's install path
            VLCPath = (string) Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\VideoLAN\VLC", "", null);
            if (VLCPath == null)
            {
                throw new Exception("VLC Not Found!");
            }

            LogMessage("VLC executable found at " + VLCPath, null);

            // Run VLC Process
            VLCProcess = new Process();
            VLCProcess.StartInfo.FileName = VLCPath;
            VLCProcess.StartInfo.Arguments = VLCparams;
            VLCProcess.Start();
            LogMessage("Process started", null);

            ConnectVLC_Async(); // connect to the VLC remote control interface
            ReadVLC_Async(); // prepare to read VLC output
            PokeVLC_Async(); // stimulate VLC to send us output
            MonitorDVD_Async(); // be on the lookout for DVD drives appearing and disappearing

            // listen for incoming URLs
            URLSocketListener.Start();
            URLSocketListener.BeginAcceptTcpClient(new AsyncCallback(URLSocket_OnConnect), null);

        }


        public override string GetHubText()
        {
            return HubText;
        }

        public override PluginInfo GetPluginInfo()
        {
            return myInfo;
        }

        public override PluginUI GetUI(UserEventArgs e)
        {
            // set up the UI
            myUI.Groups = new List<PluginUIElementGroup>();
            vidGroup.GroupName = "Native Video";
            vidGroup.ImageBytes = ResourceToBytes(new Uri("/UniteVideoPlugin;component/disc.png", System.UriKind.Relative));
            vidGroup.UIElements = new List<PluginUIElement>();

            // controls must be added in the sequence they are to appear in the client from left-> right. icons appear as a 4x4 grid

            // is the drive ready to play?
            String[] dvd = GetDrivePath();
            bool dvdReady = ((dvd != null) && dvd[1] == "True");
            LogMessage("Call to GetUI, dvd readystate is: " + dvdReady.ToString(), null);

            // -------------------- ROW 1 --------------------
            // play or pause button
            if (PlayerState == VLC_State.Playing)
            {
                vidGroup.UIElements.Add(new PluginUIElement(new Guid(PAUSE), UIElementType.Button, "", "", ResourceToBytes(new Uri("/UniteVideoPlugin;component/pause.png", System.UriKind.Relative))));
            }
            else if (PlayerState == VLC_State.Paused || dvdReady)
            {
                vidGroup.UIElements.Add(new PluginUIElement(new Guid(PLAY), UIElementType.Button, "", "", ResourceToBytes(new Uri("/UniteVideoPlugin;component/play.png", System.UriKind.Relative))));
            }
            else
            {
                // dvd drive is not yet ready and we don't have a URL
                vidGroup.UIElements.Add(new PluginUIElement(new Guid(INACTIVE), UIElementType.Button, "No Disc", "", ResourceToBytes(new Uri("/UniteVideoPlugin;component/play_grey.png", System.UriKind.Relative))));
            }

            if (PlayerState != VLC_State.Stopped)
            {
                // -------------------- ROW 1 --------------------
                // play or pause button handled already
                
                // all other buttons should be disabled if we are not currently playing

                // dynamically enabled / disabled
                vidGroup.UIElements.Add(new PluginUIElement(new Guid(REWIND), UIElementType.Button, "", "", ResourceToBytes(new Uri("/UniteVideoPlugin;component/rewind.png", System.UriKind.Relative))));
                if (currentMediaIsDVD)
                {
                    vidGroup.UIElements.Add(new PluginUIElement(new Guid(NAV_UP), UIElementType.Button, "", "", ResourceToBytes(new Uri("/UniteVideoPlugin;component/nav_up.png", System.UriKind.Relative))));
                }
                else
                {
                    // placeholder
                    vidGroup.UIElements.Add(new PluginUIElement(new Guid(INACTIVE), UIElementType.Button, "", "", null));
                }
                vidGroup.UIElements.Add(new PluginUIElement(new Guid(SEEK), UIElementType.Button, "", "", ResourceToBytes(new Uri("/UniteVideoPlugin;component/seek.png", System.UriKind.Relative))));

                // -------------------- ROW 2 --------------------
                vidGroup.UIElements.Add(new PluginUIElement(new Guid(STOP), UIElementType.Button, "", "", ResourceToBytes(new Uri("/UniteVideoPlugin;component/stop.png", System.UriKind.Relative))));
                if (currentMediaIsDVD)
                {
                    vidGroup.UIElements.Add(new PluginUIElement(new Guid(NAV_LEFT), UIElementType.Button, "", "", ResourceToBytes(new Uri("/UniteVideoPlugin;component/nav_left.png", System.UriKind.Relative))));
                    vidGroup.UIElements.Add(new PluginUIElement(new Guid(NAV_OK), UIElementType.Button, "", "", ResourceToBytes(new Uri("/UniteVideoPlugin;component/nav_ok.png", System.UriKind.Relative))));
                    vidGroup.UIElements.Add(new PluginUIElement(new Guid(NAV_RIGHT), UIElementType.Button, "", "", ResourceToBytes(new Uri("/UniteVideoPlugin;component/nav_right.png", System.UriKind.Relative))));

                    // -------------------- ROW 3 --------------------
                    vidGroup.UIElements.Add(new PluginUIElement(new Guid(MENU), UIElementType.Button, "", "", ResourceToBytes(new Uri("/UniteVideoPlugin;component/menu.png", System.UriKind.Relative))));
                    vidGroup.UIElements.Add(new PluginUIElement(new Guid(SKIP_BACK), UIElementType.Button, "", "", ResourceToBytes(new Uri("/UniteVideoPlugin;component/skip_back.png", System.UriKind.Relative))));
                    vidGroup.UIElements.Add(new PluginUIElement(new Guid(NAV_DOWN), UIElementType.Button, "", "", ResourceToBytes(new Uri("/UniteVideoPlugin;component/nav_down.png", System.UriKind.Relative))));
                    vidGroup.UIElements.Add(new PluginUIElement(new Guid(SKIP_FORWARD), UIElementType.Button, "", "", ResourceToBytes(new Uri("/UniteVideoPlugin;component/skip_forward.png", System.UriKind.Relative))));
                }
                

            }
            // add the playback control to the main UI
            myUI.Groups.Add(vidGroup);
            

            return myUI;
        }

        public override void Load()
        {
            LogMessage("!!!!!! Video Playback Plugin Loading !!!!!!", null);
            UniteVideo();
            LogMessage("!!!!!! Video Playback Plugin Loaded Successfully !!!!!!", null);

        }

        public override void UIElementEvent(UIEventArgs e)
        {   
            switch (e.ElementId.ToString())
            {
                case PLAY:
                    if (PlayerState == VLC_State.Playing || PlayerState == VLC_State.Paused){
                        SendCommand("pause");
                    }
                    else
                    {
                        String[] DriveState = GetDrivePath();
                        if (DriveState == null || DriveState[1] == "False")
                        {
                            ShowHubToast("Drive not ready", ResourceToBytes(new Uri("/UniteVideoPlugin;component/disc.png", System.UriKind.Relative)), 2);
                            return;
                        }
                        HubText = "Disc Loading. Please wait...";
                        FireHubTextUpdated();
                        SendCommand("clear");
                        SendCommand("add \"dvd:///" + DriveState[0] + "\"");
                        currentMediaIsDVD = true;
                        //SendCommand("play");
                        break;
                    }
                    break;
                case STOP:
                    if (PlayerState == VLC_State.Paused)
                    {
                        // have to unpause before we can stop
                        SendCommand("pause");
                    }
                    SendCommand("stop");
                    break;
                case PAUSE:
                    SendCommand("pause");
                    break;
                case NAV_UP:
                    SendCommand("key key-nav-up");
                    break;
                case NAV_DOWN:
                    SendCommand("key key-nav-down");
                    break;
                case NAV_LEFT:
                    SendCommand("key key-nav-left");
                    break;
                case NAV_RIGHT:
                    SendCommand("key key-nav-right");
                    break;
                case NAV_OK:
                    SendCommand("key key-nav-activate");
                    break;
                case SEEK:
                    SendCommand("key key-jump+short");
                    break;
                case REWIND:
                    SendCommand("key key-jump-short");
                    break;
                case SKIP_FORWARD:
                    SendCommand("key key-chapter-next");
                    break;
                case SKIP_BACK:
                    SendCommand("key key-chapter-prev");
                    break;
                case MENU:
                    SendCommand("key key-disc-menu");
                    break;
            }
        }

        public override void UnLoad()
        {
            // shut down VLC
            SendCommand("quit");
        }

        public override void UserConnected(UserEventArgs e)
        {
            //throw new NotImplementedException();
        }

        public override void UserDisconnected(UserEventArgs e)
        {
            // was it the last user? do we want to stop playback?
            //throw new NotImplementedException();
        }

        public override void UserPresentationEnd(UserEventArgs e)
        {
            //throw new NotImplementedException();
        }

        public override void UserPresentationStart(UserEventArgs e)
        {
            //throw new NotImplementedException();
        }
    }
}
