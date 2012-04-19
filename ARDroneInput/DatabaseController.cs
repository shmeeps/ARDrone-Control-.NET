using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using ARDrone.Input.Utils;
using ARDrone.Input.InputMappings;
using Microsoft.DirectX.DirectInput;
using System.Net;
using System.Windows;
using System.Diagnostics;
using System.Collections;
using MySql.Data.MySqlClient;

namespace ARDrone.Input
{
    public class DatabaseController : DirectInputInput
    {
        public enum Axis
        {
            // "Normal" axes
            Axis_X, Axis_Y, Axis_Z,
        }

        // Connected flag
        public bool connected = false;

        // MySQL Connection
        String cs = @"server=localhost;userid=root;password=;database=crh";
        MySqlConnection conn = null;

        public static List<GenericInput> GetNewInputDevices(IntPtr windowHandle, List<GenericInput> currentDevices)
        {
            List<GenericInput> newDevices = new List<GenericInput>();

            List<GenericInput> inputDevices = InputManager.GetInputDevices();

            foreach (GenericInput g in inputDevices)
            {
                if (g.DeviceInstanceId == "DBC")
                    return newDevices;
            }

            DatabaseController input = new DatabaseController();

            if (input.connected == true)
                newDevices.Add(input);

            return newDevices;
        }

        public DatabaseController()
            : base()
        {
            InitDatabaseController();

            DetermineMapping();
        }

        protected override InputMappings.InputMapping GetStandardMapping()
        {
            ButtonBasedInputMapping mapping = new ButtonBasedInputMapping(GetValidButtons(), GetValidAxes());

            //mapping.SetAxisMappings("A-D", "W-S", "LeftArrow-Right", "DownArrow-Up");
            //mapping.SetButtonMappings("C", "Return", "Return", "NumPad0", "Space", "F", "X");

            //mapping.SetAxisMappings("StrafeL-StrafeR", "Forward-Backward", "Left-Right", "Down-Up");
            //mapping.SetButtonMappings(0, 0, 0, 0, 0, 0, 0);

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
                return true;
            }
        }

        public override String DeviceName
        {
            get
            {
                if (connected == false) { return string.Empty; }
                else { return "Database Controller"; }
            }
        }

        public override String FilePrefix
        {
            get
            {
                if (connected == false) { return string.Empty; }
                else { return "DBC"; }
            }
        }

        public override string DeviceInstanceId
        {
            get
            {
                return "DBC";
            }
        }

        private void InitDatabaseController()
        {
            conn = new MySqlConnection(cs);
            conn.Open();

            if (conn.State == System.Data.ConnectionState.Open)
                connected = true;
            else
                connected = false;
        }

        public override void Dispose()
        {
            if (conn.State == System.Data.ConnectionState.Open)
                conn.Close();
        }
    }
}
