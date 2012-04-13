using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using ARDrone.Input.InputMappings;
using Microsoft.DirectX.DirectInput;
using System.Net;
using System.Windows;
using System.Diagnostics;
using System.Collections;

namespace ARDrone.Input
{
    public class CheckInController : DirectInputInput
    {
        public enum Axis
        {
            // "Normal" axes
            Axis_X, Axis_Y, Axis_Z,
        }

        // SOCKET STUFF
        const int MAX_CLIENTS = 10;

        public AsyncCallback pfnWorkerCallBack;
        private Socket m_mainSocket;
        private Socket[] m_workerSocket = new Socket[10];
        private int m_clientCount = 0;

        // Stopwatch
        public Stopwatch Stopwatch = new Stopwatch();

        // Connected flag
        public bool connected = false;

        // IP Settings
        public String m_IPAddress = "127.0.0.1";
        public String m_Port = "8007";

        public ArrayList<CheckInEvent> ActiveCheckIns = new ArrayList<CheckInEvent>();

        public static List<GenericInput> GetNewInputDevices(IntPtr windowHandle, List<GenericInput> currentDevices)
        {
            List<GenericInput> newDevices = new List<GenericInput>();

            CheckInController input = new CheckInController();

            if (input.connected == true)
                newDevices.Add(input);

            return newDevices;
        }

        public CheckInController()
            : base()
        {
            InitCheckInController();

            DetermineMapping();
        }

        protected override InputMappings.InputMapping GetStandardMapping()
        {
            ButtonBasedInputMapping mapping = new ButtonBasedInputMapping(GetValidButtons(), GetValidAxes());

            //mapping.SetAxisMappings("A-D", "W-S", "LeftArrow-Right", "DownArrow-Up");
            //mapping.SetButtonMappings("C", "Return", "Return", "NumPad0", "Space", "F", "X");

            mapping.SetAxisMappings("StrafeL-StrafeR", "Forward-Backward", "Left-Right", "Down-Up");
            mapping.SetButtonMappings(0, 0, 0, 0, 0, 0, 0);

            return mapping;
        }

        private List<String> GetValidButtons()
        {
            return new List<String>();
        }

        private List<String> GetValidAxes()
        {
            return new List<String>();
        }

        public override List<string> GetPressedButtons()
        {
            return new List<String>();
        }

        public override Dictionary<string, float> GetAxisValues()
        {
            return new Dictionary<String, float>();
        }

        public override bool IsDevicePresent
        {
            get
            {
                try
                {
                    return true;
                }
                catch (InputLostException)
                {
                    return false;
                }
            }
        }

        public override String DeviceName
        {
            get
            {
                if (connected == false) { return string.Empty; }
                else { return "Check-in Controller"; }
            }
        }

        public override String FilePrefix
        {
            get
            {
                if (connected == false) { return string.Empty; }
                else { return "CiC"; }
            }
        }

        private void InitCheckInController()
        {
            try
            {
                int port = System.Convert.ToInt32(m_Port);
                // Create the listening socket...
                m_mainSocket = new Socket(AddressFamily.InterNetwork,
                                          SocketType.Stream,
                                          ProtocolType.Tcp);
                IPEndPoint ipLocal = new IPEndPoint(IPAddress.Any, port);
                // Bind to local IP Address...
                m_mainSocket.Bind(ipLocal);
                // Start listening...
                m_mainSocket.Listen(4);
                // Create the call back for any client connections...
                m_mainSocket.BeginAccept(new AsyncCallback(OnClientConnect), null);

                this.connected = true;
            }
            catch (SocketException se)
            {
                this.connected = false;
            }
        }

        // This is the call back function, which will be invoked when a client is connected
        public void OnClientConnect(IAsyncResult asyn)
        {
            try
            {
                // Here we complete/end the BeginAccept() asynchronous call
                // by calling EndAccept() - which returns the reference to
                // a new Socket object
                m_workerSocket[m_clientCount] = m_mainSocket.EndAccept(asyn);
                // Let the worker Socket do the further processing for the 
                // just connected client
                WaitForData(m_workerSocket[m_clientCount]);
                // Now increment the client count
                ++m_clientCount;
                // Display this client connection as a status message on the GUI	
                //String str = String.Format("Client # {0} connected", m_clientCount);
                //textBoxMsg.Text = str;
                // TODO: Need any client connect notification?0
                //MainWindow.UpdateUIAsync("");

                // Since the main Socket is now free, it can go back and wait for
                // other clients who are attempting to connect
                m_mainSocket.BeginAccept(new AsyncCallback(OnClientConnect), null);

                MessageBox.Show("Client Connected");
            }
            catch (ObjectDisposedException)
            {
                System.Diagnostics.Debugger.Log(0, "1", "\n OnClientConnection: Socket has been closed\n");
            }
            catch (SocketException se)
            {
                System.Diagnostics.Debugger.Log(0, "1", "\n OnClientConnection: Socket could not be created\n");
                //MessageBox.Show(se.Message);
            }
        }

        public class SocketPacket
        {
            public System.Net.Sockets.Socket m_currentSocket;
            public byte[] dataBuffer = new byte[1];
        }

        // Start waiting for data from the client
        public void WaitForData(System.Net.Sockets.Socket soc)
        {
            try
            {
                if (pfnWorkerCallBack == null)
                {
                    // Specify the call back function which is to be 
                    // invoked when there is any write activity by the 
                    // connected client
                    pfnWorkerCallBack = new AsyncCallback(OnDataReceived);
                }
                SocketPacket theSocPkt = new SocketPacket();
                theSocPkt.m_currentSocket = soc;
                // Start receiving any data written by the connected client
                // asynchronously
                soc.BeginReceive(theSocPkt.dataBuffer, 0,
                                   theSocPkt.dataBuffer.Length,
                                   SocketFlags.None,
                                   pfnWorkerCallBack,
                                   theSocPkt);
            }
            catch (SocketException se)
            {
                System.Diagnostics.Debugger.Log(0, "1", "\n WaitForData: What happened?\n");
                //MessageBox.Show(se.Message);
            }
        }

        // This the call back function which will be invoked when the socket
        // detects any client writing of data on the stream
        public void OnDataReceived(IAsyncResult asyn)
        {
            try
            {
                SocketPacket socketData = (SocketPacket)asyn.AsyncState;

                int tempCheckIn = 0;

                int iRx = 0;
                // Complete the BeginReceive() asynchronous call by EndReceive() method
                // which will return the number of characters written to the stream 
                // by the client
                iRx = socketData.m_currentSocket.EndReceive(asyn);
                char[] chars = new char[iRx + 1];
                System.Text.Decoder d = System.Text.Encoding.UTF8.GetDecoder();
                int charLen = d.GetChars(socketData.dataBuffer,
                                         0, iRx, chars, 0);
                System.String szData = new System.String(chars);
                //richTextBoxReceivedMsg.AppendText(szData);

                try
                {
                    tempCheckIn = Convert.ToInt32(szData);
                }
                catch (FormatException e)
                {
                    //CurrentCommand = Commands.Hover;

                    // TODO: Try to interpret CAVE Calibration data?
                }
                catch (OverflowException e)
                {
                    //CurrentCommand = Commands.Hover;
                }
                finally
                {
                    if (tempCheckIn > 0 && tempCheckIn <= 4)
                    {
                        this.ActiveCheckIns.Add(tempCheckIn);
                    }
                    else
                    {
                        //CurrentCommand = Commands.Hover;
                    }
                }

                // Continue the waiting for data on the Socket
                WaitForData(socketData.m_currentSocket);
            }
            catch (ObjectDisposedException)
            {
                System.Diagnostics.Debugger.Log(0, "1", "\nOnDataReceived: Socket has been closed\n");
            }
            catch (SocketException se)
            {
                System.Diagnostics.Debugger.Log(0, "1", "\nOnDataReceived: Something really bad happened!!!\n");
            }
        }

        // Sends a command
        public void SendCommand(int cmd)
        {
            try
            {
                byte[] byData = System.Text.Encoding.ASCII.GetBytes(((int)cmd).ToString());
                if (m_workerSocket != null)
                {
                    foreach (Socket s in m_workerSocket)
                        if (s != null)
                            s.Send(byData);
                }
            }
            catch (SocketException se)
            {
                System.Diagnostics.Debugger.Log(0, "1", "\nOnDataReceived: Something really bad happened!!!\n");
            }
        }

        public override void Dispose()
        {
            Stopwatch.Stop();
        }
    }

    public class CheckInEvent
    {
        public int CheckInID = 0;
        public TimeSpan Time;

        public CheckInEvent(int i, TimeSpan t)
        {
            this.CheckInID = i;
            this.Time = t;
        }
    }
}
