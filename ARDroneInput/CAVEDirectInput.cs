/* ARDrone Control .NET - An application for flying the Parrot AR drone in Windows.
 * Copyright (C) 2010, 2011 Thomas Endres, Stephen Hobley, Julien Vinel
 * 
 * This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation; either version 3 of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License along with this program; if not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using Microsoft.DirectX.DirectInput;
using ARDrone.Input.InputMappings;
using System.Net.Sockets;
using System.Net;
using System.Collections.ObjectModel;
using System.Windows.Forms;

namespace ARDrone.Input
{
    public class CAVEDirectInput : DirectInputInput
    {
        public enum Axis
        {
            // "Normal" axes
            Axis_X, Axis_Y, Axis_Z,
        }

        public enum Commands
        {
            None = 0,
            Forward = 1,
            Backward = 2,
            Left = 3,
            Right = 4,
            Up = 5,
            Down = 6,
            FlatTrim = 7,
            TakeOff = 16,
            Land = 17,
            Emergency = 10,
            StrafeL = 11,
            StrafeR = 12,
            Camera = 13,
            Special = 14,
            Hover = 15,
            Calibrate = 8,
            CalibrationComplete = 9,
            ControlToPatient = 18,
            ControlToSupervisor = 19,
            CheckInToggle = 20,
            SelectPatient = 21,
            SavePatient = 22,
            ViewLogs = 23,
            ViewRecordings = 24,
            StartSimulation = 25,
            EndSimulation = 26,
            PauseSimulation = 27,
            Exit = 28
        }

        // SOCKET STUFF
        const int MAX_CLIENTS = 10;

        public AsyncCallback pfnWorkerCallBack;
        private Socket m_mainSocket;
        private Socket[] m_workerSocket = new Socket[10];
        private int m_clientCount = 0;

        // Connected flag
        public bool connected = false;

        // See if the CAVE is calibrated
        public bool CAVECalibrated = false;

        // Calibration data
        public enum Gestures
        {
            Up = 0,
            Down = 1,
            Left = 2,
            Right = 3,
            Forward = 4,
            Back = 5
        }

        public Gesture[] CalibrationData = new Gesture[6];

        // IP Settings
        public String m_IPAddress = "127.0.0.1";
        public String m_Port = "8000";

        Commands CurrentCommand = Commands.Hover;

        protected ArrayList keysPressedBefore = new ArrayList();

        public static List<GenericInput> GetNewInputDevices(IntPtr windowHandle, List<GenericInput> currentDevices)
        {
            List<GenericInput> newDevices = new List<GenericInput>();

            CAVEDirectInput input = new CAVEDirectInput();

            if (input.connected == true)
                newDevices.Add(input);

            return newDevices;
        }

        public CAVEDirectInput()
            : base()
        {
            InitCAVEInput();

            DetermineMapping();
        }

        protected override InputMapping GetStandardMapping()
        {
            ButtonBasedInputMapping mapping = new ButtonBasedInputMapping(GetValidButtons(), GetValidAxes());

            //mapping.SetAxisMappings("A-D", "W-S", "LeftArrow-Right", "DownArrow-Up");
            //mapping.SetButtonMappings("C", "Return", "Return", "NumPad0", "Space", "F", "X");

            mapping.SetAxisMappings("StrafeL-StrafeR", "Forward-Backward", "Left-Right", "Down-Up");
            mapping.SetButtonMappings(Commands.Camera, Commands.TakeOff, Commands.Land, Commands.Hover, Commands.Emergency, Commands.FlatTrim, Commands.Special);

            return mapping;
        }

        private List<String> GetValidButtons()
        {
            List<String> validButtons = new List<String>();
            foreach (Commands key in Enum.GetValues(typeof(Commands)))
            {
                if (!validButtons.Contains(key.ToString()))
                {
                    validButtons.Add(key.ToString());
                }
            }

            return validButtons;
        }

        private List<String> GetValidAxes()
        {
            return new List<String>();
        }

        public override List<String> GetPressedButtons()
        {
            List<String> buttonsPressed = new List<String>();

            if (CurrentCommand == Commands.CalibrationComplete)
                CAVECalibrated = true;

            else if (CurrentCommand != Commands.None)
                if(CAVECalibrated)
                    buttonsPressed.Add(CurrentCommand.ToString());

            return buttonsPressed;
        }

        public override Dictionary<String, float> GetAxisValues()
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
                else { return "CAVE System"; }
            }
        }

        public override String FilePrefix
        {
            get
            {
                if (connected == false) { return string.Empty; }
                else { return "CS"; }
            }
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
                // TODO: Need any client connect notification?0
                //MainWindow.UpdateUIAsync("");

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
            public byte[] dataBuffer = new byte[64];
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

                int tempCMD = 0;

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
                    tempCMD = Convert.ToInt32(szData);
                }
                catch (FormatException e)
                {
                    CurrentCommand = Commands.Hover;

                    // TODO: Try to interpret CAVE Calibration data?
                }
                catch (OverflowException e)
                {
                    CurrentCommand = Commands.Hover;
                }
                finally
                {
                    if (tempCMD < Int32.MaxValue)
                    {
                        if(tempCMD == 9)
                            MessageBox.Show(tempCMD.ToString(), "Derp", MessageBoxButtons.OKCancel);
                        else
                            CurrentCommand = ((Commands)tempCMD);
                    }
                    else
                    {
                        CurrentCommand = Commands.Hover;
                    }
                }

                // TODO
                //updateStatus();

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
        public void SendCommand(Commands cmd)
        {
            try
            {
                byte[] byData = System.Text.Encoding.ASCII.GetBytes(((int)cmd).ToString());
                if (m_workerSocket != null)
                {
                    foreach(Socket s in m_workerSocket)
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

        }
    }

    public class Vec3
    {
        public double X;
        public double Y;
        public double Z;

        public Vec3(double x, double y, double z)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
        }

        public Vec3(Vec3 v)
        {
            this.X = v.X;
            this.Y = v.Y;
            this.Z = v.Z;
        }
    }

    public class Gesture
    {
        public String GestureName;
        public Vec3 LeftHand;
        public Vec3 RightHand;

        public Gesture(String s, Vec3 l, Vec3 r)
        {
            this.GestureName = s;
            this.LeftHand = l;
            this.RightHand = r;
        }
    }
}