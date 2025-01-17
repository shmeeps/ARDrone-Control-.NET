﻿using System;
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

        public Patient currentPatient = null;
        public Patient savingPatient = null;
        public Patient loadedPatient = null;

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

        public void getPatientByID(int id)
        {
             MySqlDataReader rdr = null;

                string stm = @"SELECT 
                                `ID`, 
                                `FirstName`, 
                                `LastName`, 
                                `Date`, 
                                `CheckInOneTime`, 
                                `CheckInTwoTime`, 
                                `CheckInThreeTime`, 
                                `CheckInFourTime` 
                            FROM 
                            (
                                SELECT
                                    `p`.`ID`, 
                                    `p`.`FirstName`, 
                                    `p`.`LastName`, 
                                    `s`.`Date`, 
                                    `s`.`CheckInOneTime`, 
                                    `s`.`CheckInTwoTime`, 
                                    `s`.`CheckInThreeTime`, 
                                    `s`.`CheckInFourTime` 
                                FROM
                                    `patients` p, 
                                    `sessions` s 
                                WHERE 
                                    `p`.`ID` = @ID 
                                AND 
                                    `p`.`ID` = `s`.`patientID` 
                                ORDER BY 
                                    `s`.`Date` DESC
                            ) s
                            LIMIT 1";

            try
            {
                MySqlCommand cmd = new MySqlCommand();
                cmd.Connection = this.conn;
                cmd.CommandText = stm;
                cmd.Prepare();

                cmd.Parameters.AddWithValue("@ID", id);
                rdr = cmd.ExecuteReader();

                Patient p = null;

                if (rdr.Read())
                {
                    p = new Patient(rdr.GetInt32(0), rdr.GetString(1), rdr.GetString(2));
                }

                // Close the reader
                rdr.Close();

                if(p != null)
                {
                    p.LastSession = this.getLastSession(id);                

                    this.currentPatient = p;
                    this.savingPatient = p;
                }
            }
            catch (MySqlException ex)
            {
                Console.WriteLine("Error: {0}", ex.ToString());
                rdr.Close();
                //return null;
            }
        }

        public Session getLastSession(int id)
        {
            MySqlDataReader rdr = null;

            string stm = @"SELECT 
                                `ID`, 
                                `FirstName`, 
                                `LastName`, 
                                `Date`, 
                                `CheckInOneTime`, 
                                `CheckInTwoTime`, 
                                `CheckInThreeTime`, 
                                `CheckInFourTime` 
                            FROM 
                            (
                                SELECT
                                    `p`.`ID`, 
                                    `p`.`FirstName`, 
                                    `p`.`LastName`, 
                                    `s`.`Date`, 
                                    `s`.`CheckInOneTime`, 
                                    `s`.`CheckInTwoTime`, 
                                    `s`.`CheckInThreeTime`, 
                                    `s`.`CheckInFourTime` 
                                FROM
                                    `patients` p, 
                                    `sessions` s 
                                WHERE 
                                    `p`.`ID` = @ID 
                                AND 
                                    `p`.`ID` = `s`.`patientID` 
                                ORDER BY 
                                    `s`.`Date` DESC
                            ) s
                            LIMIT 1";

            try
            {
                MySqlCommand cmd = new MySqlCommand();
                cmd.Connection = this.conn;
                cmd.CommandText = stm;
                cmd.Prepare();

                cmd.Parameters.AddWithValue("@ID", id);
                rdr = cmd.ExecuteReader();

                if (rdr.Read())
                {
                    Session s = new Session(rdr.GetString(3), rdr.GetString(4), rdr.GetString(5), rdr.GetString(6), rdr.GetString(7));
                    rdr.Close();
                    return s;
                }
                else
                {
                    rdr.Close();
                    return new Session("2012-01-01 00:00:00", "00:00", "00:00", "00:00", "00:00");
                }
            }
            catch (MySqlException ex)
            {
                Console.WriteLine("Error: {0}", ex.ToString());
                rdr.Close();
                return new Session("2012-01-01 00:00:00", "00:00", "00:00", "00:00", "00:00");
            }
        }

        public bool UpdatePatient()
        {
            try
            {
                MySqlDataReader rdr = null;

                string stm = @"INSERT INTO
                                    `sessions` s 
                                (
                                    `patientID`,
                                    `CheckInOneTime`,
                                    `CheckInTwoTime`,
                                    `CheckInThreeTime`,
                                    `CheckInFourTime`
                                )
                                VALUES
                                (
                                    @pid,
                                    @checkinone,
                                    @checkintwo,
                                    @checkinthree,
                                    @checkinfour
                                )";

                MySqlCommand cmd = new MySqlCommand();
                cmd.Connection = this.conn;
                cmd.CommandText = stm;
                cmd.Prepare();

                cmd.Parameters.AddWithValue("@pid", savingPatient.ID);
                cmd.Parameters.AddWithValue("@checkinone", savingPatient.LastSession.Time1);
                cmd.Parameters.AddWithValue("@checkintwo", savingPatient.LastSession.Time2);
                cmd.Parameters.AddWithValue("@checkinthree", savingPatient.LastSession.Time3);
                cmd.Parameters.AddWithValue("@checkinfour", savingPatient.LastSession.Time4);
                rdr = cmd.ExecuteReader();

                // Make the system load the newest test
                this.currentPatient = this.savingPatient;

                // Close the reader
                rdr.Close();

                //rdr.Read();
                //cmd.LastInsertedId;

                return true;
            }
            catch (MySqlException e)
            {
                return false;
            }
        }

        public int reserveVideo()
        {
            try
            {
                MySqlDataReader rdr = null;

                string stm = @"INSERT INTO
                                    `recordings`
                                (
                                    `patientID`
                                )
                                VALUES
                                (
                                    @pid
                                );
                                SELECT LAST_INSERT_ID()";

                MySqlCommand cmd = new MySqlCommand();
                cmd.Connection = this.conn;
                cmd.CommandText = stm;
                cmd.Prepare();

                cmd.Parameters.AddWithValue("@pid", currentPatient.ID);
                rdr = cmd.ExecuteReader();

                if (rdr != null && rdr.Read())
                {
                    int temp = rdr.GetInt32(0);
                    rdr.Close();
                    return temp;
                }
                else
                {
                    rdr.Close();
                    return -1;
                }
            }
            catch (MySqlException e)
            {
                return -1;
            }
        }

        public Queue<SearchResult> SearchPatients(String term)
        {
            Queue<SearchResult> results = new Queue<SearchResult>();
            String rterm = "";

            if (term.Contains(" "))
            {
                // TERRIBLE CODE IS TERRIBLE, I KNOW
                string[] split = term.Split(new Char[] {' '});
                Queue<String> validterms = new Queue<String>();

                foreach (string s in split)
                    if (s.Trim() != "")
                        validterms.Enqueue(s);

                term = validterms.Dequeue();

                if (validterms.Count == 0)
                    rterm = term;
                else
                    rterm = validterms.Dequeue();
            }
            else
            {
                rterm = term;
            }

            // Query!
            try
            {
                MySqlDataReader rdr = null;

                string stm = @"SELECT 
                                    `ID`, 
                                    `FirstName`, 
                                    `LastName`
                                FROM
                                    `patients`
                                WHERE
                                    `FirstName` LIKE @term
                                OR
                                    `LastName` LIKE @rterm
                                ORDER BY
                                    `LastName` ASC";

                MySqlCommand cmd = new MySqlCommand();
                cmd.Connection = this.conn;
                cmd.CommandText = stm;
                cmd.Prepare();

                cmd.Parameters.AddWithValue("@term", "%" + term + "%");
                cmd.Parameters.AddWithValue("@rterm", "%" + rterm + "%");
                rdr = cmd.ExecuteReader();

                while (rdr.Read())
                {
                    results.Enqueue(new SearchResult(rdr.GetInt32(0), rdr.GetString(1) + " " + rdr.GetString(2)));
                }

                // Close the reader
                rdr.Close();

                return results;
            }
            catch (MySqlException ex)
            {
                Console.WriteLine("Error: {0}", ex.ToString());

                return results;
            }
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
            try
            {
                conn = new MySqlConnection(cs);
                conn.Open();

                if (conn.State == System.Data.ConnectionState.Open)
                    connected = true;
                else
                    connected = false;
            }
            catch (MySqlException e)
            {
                connected = false;
            }
        }

        public override void Dispose()
        {
            if (conn.State == System.Data.ConnectionState.Open)
                conn.Close();
        }
    }

    public class Patient
    {
        public int ID;
        public String FirstName;
        public String LastName;
        public Session LastSession;

        public Patient(int id, String firstname, String lastname)
        {
            this.ID = id;
            this.FirstName = firstname;
            this.LastName = lastname;
            this.LastSession = null;
        }

        public Patient(int id, String firstname, String lastname, Session lastsession)
        {
            this.ID = id;
            this.FirstName = firstname;
            this.LastName = lastname;
            this.LastSession = lastsession;
        }
    }

    public class Session
    {
        public String Date;
        public String Time1;
        public String Time2;
        public String Time3;
        public String Time4;

        public Session(String date, String time1, String time2, String time3, String time4)
        {
            this.Date = date;
            this.Time1 = time1;
            this.Time2 = time2;
            this.Time3 = time3;
            this.Time4 = time4;
        }
    }

    public class SearchResult
    {
        public int ID;
        public String Name;

        public SearchResult(int id, String name)
        {
            this.ID = id;
            this.Name = name;
        }
    }
}
