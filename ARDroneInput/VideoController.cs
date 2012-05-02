using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using ARDrone.Input.InputMappings;
using System.Net;
using System.Windows;
using ARDrone.Input.Utils;
using System.Drawing;
using System.IO;

namespace ARDrone.Input
{
    public class VideoController : DirectInputInput
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
        static int m_bufferSize = 16;

        // Connected flag
        public bool connected = false;

        // IP Settings
        public String m_IPAddress = "127.0.0.1";

        // Ports
        public String m_Port = "8006";

        public static bool sending = false;
        public static string file = "front";

        public static List<GenericInput> GetNewInputDevices(IntPtr windowHandle, List<GenericInput> currentDevices)
        {
            List<GenericInput> newDevices = new List<GenericInput>();

            List<GenericInput> inputDevices = InputManager.GetInputDevices();

            foreach (GenericInput g in inputDevices)
            {
                if (g.DeviceInstanceId == "PATIENT")
                    return newDevices;
            }

            VideoController input = new VideoController();

            if (input.connected == true)
                newDevices.Add(input);

            return newDevices;
        }

        public VideoController()
            : base()
        {
            InitPatientDisplay();

            DetermineMapping();
        }

        protected override InputMapping GetStandardMapping()
        {
            ButtonBasedInputMapping mapping = new ButtonBasedInputMapping(GetValidButtons(), GetValidAxes());

            //mapping.SetAxisMappings("A-D", "W-S", "LeftArrow-Right", "DownArrow-Up");
            //mapping.SetButtonMappings("C", "Return", "Return", "NumPad0", "Space", "F", "X");

            //mapping.SetAxisMappings("StrafeL-StrafeR", "Forward-Backward", "Left-Right", "Down-Up");
            //mapping.SetButtonMappings(Commands.Camera, Commands.TakeOff, Commands.Land, Commands.Hover, Commands.Emergency, Commands.FlatTrim, Commands.Special);

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

        public override InputState GetCurrentControlInput()
        {
            List<String> buttonsPressed = GetPressedButtons();
            Dictionary<String, float> axisValues = GetAxisValues();

            //if (buttonsPressed.Contains("")) { buttonsPressed.Remove(""); }
            //if (axisValues.ContainsKey("")) { axisValues.Remove(""); }

            float roll = 0;
            float pitch = 0;
            float yaw = 0;
            float gaz = 0;

            bool cameraSwap = false;
            bool takeOff = false;
            bool land = false;
            bool hover = false;
            bool emergency = false;

            bool flatTrim = false;

            bool specialAction = false;

            // TODO: test
            //SetButtonsPressedBefore(buttonsPressed);

            if (roll != lastInputState.Roll || pitch != lastInputState.Pitch || yaw != lastInputState.Yaw || gaz != lastInputState.Gaz || cameraSwap != lastInputState.CameraSwap || takeOff != lastInputState.TakeOff ||
                land != lastInputState.Land || hover != lastInputState.Hover || emergency != lastInputState.Emergency || flatTrim != lastInputState.FlatTrim)
            {
                InputState newInputState = new InputState(roll, pitch, yaw, gaz, cameraSwap, takeOff, land, hover, emergency, flatTrim, specialAction);
                lastInputState = newInputState;
                return newInputState;
            }
            else
            {
                return null;
            }
        }

        public override bool IsDevicePresent
        {
            get
            {
                //return m_mainSocket.Connected;
                // TODO: Make sure socket is still open
                return true;
            }
        }

        public override String DeviceName
        {
            get
            {
                if (connected == false) { return string.Empty; }
                else { return "Patient Display"; }
            }
        }

        public override String FilePrefix
        {
            get
            {
                if (connected == false) { return string.Empty; }
                else { return "PD"; }
            }
        }

        public override string DeviceInstanceId
        {
            get
            {
                return "PATIENT";
            }
        }

        private void InitPatientDisplay()
        {
            try
            {
                int port = System.Convert.ToInt32(m_Port);
                // Create the listening socket...
                m_mainSocket = new Socket(AddressFamily.InterNetwork,
                                          SocketType.Stream,
                                          ProtocolType.Tcp);
                IPEndPoint ipLocal = new IPEndPoint(IPAddress.Parse(m_IPAddress), port);
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

                MessageBox.Show("Patient Display Connected");
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
            public byte[] dataBuffer = new byte[VideoController.m_bufferSize];
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

                /*int tempCMD = 0;

                int iRx = 0;
                // Complete the BeginReceive() asynchronous call by EndReceive() method
                // which will return the number of characters written to the stream 
                // by the client
                iRx = socketData.m_currentSocket.EndReceive(asyn);
                char[] chars = new char[iRx + 1];
                System.Text.Decoder d = System.Text.Encoding.UTF8.GetDecoder();
                int charLen = d.GetChars(socketData.dataBuffer,
                                         0, iRx, chars, 0);
                System.String szData = new System.String(chars);*/
                //richTextBoxReceivedMsg.AppendText(szData);

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
        public void SendImage(System.Drawing.Image image)
        {
            // Make sure we aren't trying to send an image right now
            if (VideoController.sending)
                return;
            else
                VideoController.sending = true;

            try
            {
                image.Save(System.Windows.Forms.Application.StartupPath + "\\VideoStream\\" + VideoController.file + ".bmp");
            }
            catch (System.ArgumentException e)
            {
                VideoController.sending = false;
            }
            
            // Byte data to store the image in
            /*byte[] imageBytes = new byte[0];

            using (MemoryStream stream = new MemoryStream())
            {
                image.Save(stream, System.Drawing.Imaging.ImageFormat.Bmp);
                stream.Close();

                imageBytes = stream.ToArray();
            }*/

            image.Dispose();

            try
            {
                if (m_workerSocket != null)
                {
                    byte[] imageBytes = System.Text.Encoding.ASCII.GetBytes(VideoController.file);
                    foreach(Socket s in m_workerSocket)
                        if (s != null)
                        {
                            s.Send(imageBytes);
                            //System.Diagnostics.Debugger.Log(0, "1", "\nData Sent!\n");
                        }
                }

                //System.Diagnostics.Debugger.Log(0, "1", "\nData Transmission Complete!\n");
                VideoController.sending = false;
                
                if(VideoController.file == "front")
                    VideoController.file = "back";
                else
                    VideoController.file = "front";
            }
            catch (SocketException se)
            {
                System.Diagnostics.Debugger.Log(0, "1", "\nOnDataSend: Something really bad happened!!!\n");
                VideoController.sending = false;
            }
            


        }

        public override void Dispose()
        {
            //m_mainSocket.Close();
        }
    }
}
