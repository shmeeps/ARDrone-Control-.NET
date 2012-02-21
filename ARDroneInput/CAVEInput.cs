using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;

namespace ARDrone.Input
{
    public class CAVEInput : ButtonBasedInput
    {
        public enum Commands
        {
            Hover = 0,
            Forward = 1,
            Backward = 2,
            Left = 3,
            Right = 4,
            Up = 5,
            Down = 6,
            FlatTrim = 7,
            TakeOffLand = 8,
            Emergency = 9
        }

        // SOCKET STUFF
        const int MAX_CLIENTS = 10;

        public AsyncCallback pfnWorkerCallBack;
        private Socket m_mainSocket;
        private Socket[] m_workerSocket = new Socket[10];
        private int m_clientCount = 0;

        // Connected flag
        public bool connected = false;

        // IP Settings
        public String m_IPAddress = "127.0.0.1";
        public String m_Port = "8000";

        Commands CurrentCommand = Commands.Hover;

        Dictionary<String, float> currentAxisValues = new Dictionary<String, float>();
        List<String> currentButtonsPressed = new List<String>();        

        public static List<GenericInput> GetNewInputDevices(IntPtr windowHandle, List<GenericInput> currentDevices)
        {
            List<GenericInput> newDevices = new List<GenericInput>();

            CAVEInput input = new CAVEInput();

            if (input.connected == true)
                newDevices.Add(input);

            return newDevices;
        }

        public CAVEInput() : base()
        {
            InitCAVEInput();

            DetermineMapping();
        }

        private void InitCAVEInput()
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
                // TODO: Need any client connect notification?

                // Since the main Socket is now free, it can go back and wait for
                // other clients who are attempting to connect
                m_mainSocket.BeginAccept(new AsyncCallback(OnClientConnect), null);
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

        private List<String> GetValidButtons()
        {
            List<String> validButtons = new List<String>();
            foreach (Button button in Enum.GetValues(typeof(Button)))
            {
                validButtons.Add(button.ToString());
            }

            return validButtons;
        }

        private List<String> GetValidAxes()
        {
            List<String> validAxes = new List<String>();
            foreach (Axis axis in Enum.GetValues(typeof(Axis)))
            {
                validAxes.Add(axis.ToString());
            }

            return validAxes;
        }

        protected override InputMapping GetStandardMapping()
        {
            ButtonBasedInputMapping mapping = new ButtonBasedInputMapping(GetValidButtons(), GetValidAxes());

            mapping.SetAxisMappings(Axis.Axis_BalanceBoard_X, Axis.Axis_BalanceBoard_Y, "Button_Left-Button_Right", "Button_B-Button_A");
            mapping.SetButtonMappings("", Button.Button_Plus, Button.Button_Plus, Button.Button_Minus, Button.Button_Home, "", "");

            return mapping;
        }

        public override void Dispose()
        {
            // Close all sockets
            if (m_mainSocket != null)
            {
                m_mainSocket.Close();
            }
            for (int i = 0; i < m_clientCount; i++)
            {
                if (m_workerSocket[i] != null)
                {
                    m_workerSocket[i].Close();
                    m_workerSocket[i] = null;
                }
            }
        }

        public override List<String> GetPressedButtons()
        {
            return currentButtonsPressed;
        }

        public override Dictionary<String, float> GetAxisValues()
        {
            return currentAxisValues;
        }

        void wiimote_WiimoteChanged(object sender, WiimoteChangedEventArgs e)
		{
            WiimoteState state = e.WiimoteState;

            Dictionary<String, float> axisValues = new Dictionary<String, float>();

            axisValues[Axis.Axis_X.ToString()] = state.AccelState.Values.X;
            axisValues[Axis.Axis_Y.ToString()] = -state.AccelState.Values.Y;
            axisValues[Axis.Axis_Z.ToString()] = state.AccelState.Values.Z;

            axisValues[Axis.Axis_Nunchuk_X.ToString()] =  -state.NunchukState.Joystick.X * 2.0f;
            axisValues[Axis.Axis_Nunchuk_Y.ToString()] = -state.NunchukState.Joystick.Y * 2.0f;

            axisValues[Axis.Axis_BalanceBoard_X.ToString()] = (state.BalanceBoardState.CenterOfGravity.X + 4.0f) / 20.0f;
            axisValues[Axis.Axis_BalanceBoard_Y.ToString()] = -(state.BalanceBoardState.CenterOfGravity.Y - 4.5f) / 10.0f;

            List<String> buttonsPressed = new List<String>();

            if (state.ButtonState.A) { buttonsPressed.Add(Button.Button_A.ToString()); }
            if (state.ButtonState.B) { buttonsPressed.Add(Button.Button_B.ToString()); }
            if (state.ButtonState.One) { buttonsPressed.Add(Button.Button_One.ToString()); }
            if (state.ButtonState.Two) { buttonsPressed.Add(Button.Button_Two.ToString()); }
            if (state.ButtonState.Minus) { buttonsPressed.Add(Button.Button_Minus.ToString()); }
            if (state.ButtonState.Plus) { buttonsPressed.Add(Button.Button_Plus.ToString()); }
            if (state.ButtonState.Home) { buttonsPressed.Add(Button.Button_Home.ToString()); }
            if (state.ButtonState.Up)  { buttonsPressed.Add(Button.Button_Up.ToString()); }
            if (state.ButtonState.Down)  { buttonsPressed.Add(Button.Button_Down.ToString()); }
            if (state.ButtonState.Left)  { buttonsPressed.Add(Button.Button_Left.ToString()); }
            if (state.ButtonState.Right) { buttonsPressed.Add(Button.Button_Right.ToString()); }

            if (state.NunchukState.C ) { buttonsPressed.Add(Button.Button_Nunchuk_C.ToString()); }

            if (state.ClassicControllerState.ButtonState.A) { buttonsPressed.Add(Button.Button_Classic_A.ToString()); }
            if (state.ClassicControllerState.ButtonState.B) { buttonsPressed.Add(Button.Button_Classic_B.ToString()); }
            if (state.ClassicControllerState.ButtonState.X) { buttonsPressed.Add(Button.Button_Classic_X.ToString()); }
            if (state.ClassicControllerState.ButtonState.Y) { buttonsPressed.Add(Button.Button_Classic_Y.ToString()); }
            if (state.ClassicControllerState.ButtonState.ZL) { buttonsPressed.Add(Button.Button_Classic_L.ToString()); }
            if (state.ClassicControllerState.ButtonState.ZR) { buttonsPressed.Add(Button.Button_Classic_R.ToString()); }
            if (state.ClassicControllerState.ButtonState.Left) { buttonsPressed.Add(Button.Button_Classic_Left.ToString()); }
            if (state.ClassicControllerState.ButtonState.Up) { buttonsPressed.Add(Button.Button_Classic_Up.ToString()); }
            if (state.ClassicControllerState.ButtonState.Right) { buttonsPressed.Add(Button.Button_Classic_Right.ToString()); }
            if (state.ClassicControllerState.ButtonState.Down) { buttonsPressed.Add(Button.Button_Classic_Down.ToString()); }
            if (state.ClassicControllerState.ButtonState.Minus) { buttonsPressed.Add(Button.Button_Classic_Minus.ToString()); }
            if (state.ClassicControllerState.ButtonState.Plus) { buttonsPressed.Add(Button.Button_Classic_Plus.ToString()); }
            if (state.ClassicControllerState.ButtonState.Home) { buttonsPressed.Add(Button.Button_Classic_Home.ToString()); }

            currentAxisValues = axisValues;
            currentButtonsPressed = buttonsPressed;
		}

        void wiimote_WiimoteExtensionChanged(object sender, WiimoteExtensionChangedEventArgs e)
        {
            wiimote.WiimoteExtensionChanged += wiimote_WiimoteExtensionChanged;

            //wiimote.Connect();
            // Nothing to do (for now)
        }

        public override bool IsDevicePresent
        {
            get
            {
                // TODO
                return true;
            }
        }

        public override String DeviceName
        {
            get
            {
                if (connected == false) { return string.Empty; }
                else { return "SOCKETTEST"; }
            }
        }

        public override String FilePrefix
        {
            get
            {
                if (connected == false) { return string.Empty; }
                else { return "ST"; }
            }
        }

        public override String DeviceInstanceId
        {
            get
            {
                if (connected == false) { return string.Empty; }
                else { return m_Port; }
            }
        }

        public List<String> AxisMappingNames
        {
            get
            {
                List<String> axisMappingNames = new List<String>();

                axisMappingNames.Add(Axis.Axis_X.ToString());
                axisMappingNames.Add(Axis.Axis_Y.ToString());
                axisMappingNames.Add(Axis.Axis_Z.ToString());

                axisMappingNames.Add(Axis.Axis_Nunchuk_X.ToString());
                axisMappingNames.Add(Axis.Axis_Nunchuk_Y.ToString());

                axisMappingNames.Add(Axis.Axis_BalanceBoard_X.ToString());
                axisMappingNames.Add(Axis.Axis_BalanceBoard_Y.ToString());

                return axisMappingNames;
            }
        }
    }
}
