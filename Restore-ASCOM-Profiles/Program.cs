﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ASCOM.Utilities;
using ASCOM.Wise40.Common;
using System.Windows.Forms;

namespace Restore_ASCOM_Profiles
{
    public class Program
    {
        private static bool realMachine = Environment.MachineName.ToUpper() == "dome-ctlr";
        public enum Mode { LCOGT, ACP, WISE };
        public static Mode mode = Mode.WISE;

        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                Mode m;
                if (Enum.TryParse<Mode>(args[0].ToUpper(), out m))
                    mode = m;
            }
            WriteCloudSensorProfile();
            WriteVantageProProfile();
            WriteSafeToOpenProfile();
            WriteSafeToImageProfile();
            WriteDomeProfile();
            WriteTelescopeProfile();
            WriteOCHProfile();
            WriteFilterWheelProfile();

            string message = string.Format("ASCOM Profiles for Wise40 drivers have been restored to mode \"{0}\".", mode.ToString());
            Console.WriteLine(message);
            MessageBox.Show(message);

            Environment.Exit(0);
        }

        internal static void WriteCloudSensorProfile()
        {
            string driverID = "ASCOM.CloudSensor.ObservingConditions";
            string dataFileProfileName = "Data File";
            string dataFile = realMachine ? 
                "z:/clarityII-data.txt" :
                "c:/temp/clarityII-data.txt";

            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "ObservingConditions";
                driverProfile.WriteValue(driverID, dataFileProfileName, dataFile);
            }
        }

        internal static void WriteVantageProProfile()
        {
            string driverID = "ASCOM.Vantage.ObservingConditions";
            string reportFileProfileName = "Report File";
            string reportFile = realMachine ?
                "y:/Weather_Wise40_Vantage_Pro.htm" :
                "c:/temp/Weather_Wise40_Vantage_Pro.htm";

            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "ObservingConditions";
                driverProfile.WriteValue(driverID, reportFileProfileName, reportFile);
            }
        }

        internal static void WriteSafeToOpenProfile()
        {
            string driverID = "ASCOM.Wise40.SafeToOpen.SafetyMonitor";
            string cloudsMaxProfileName = "Clouds Max";
            string windMaxProfileName = "Wind Max";
            string rainMaxProfileName = "Rain Max";
            string lightMaxProfileName = "Light Max";
            string humidityMaxProfileName = "Humidity Max";
            string ageMaxSecondsProfileName = "Age Max";

            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "SafetyMonitor";
                driverProfile.WriteValue(driverID, cloudsMaxProfileName, "cloudClear");
                driverProfile.WriteValue(driverID, windMaxProfileName, 40.ToString());
                driverProfile.WriteValue(driverID, rainMaxProfileName, 0.ToString());
                driverProfile.WriteValue(driverID, lightMaxProfileName, "dayDark");
                driverProfile.WriteValue(driverID, humidityMaxProfileName, 90.ToString());
                driverProfile.WriteValue(driverID, ageMaxSecondsProfileName, 0.ToString());
            }
        }

        internal static void WriteSafeToImageProfile()
        {
            string driverID = "ASCOM.Wise40.SafeToImage.SafetyMonitor";
            string cloudsMaxProfileName = "Clouds Max";
            string windMaxProfileName = "Wind Max";
            string rainMaxProfileName = "Rain Max";
            string lightMaxProfileName = "Light Max";
            string humidityMaxProfileName = "Humidity Max";
            string ageMaxSecondsProfileName = "Age Max";

            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "SafetyMonitor";
                driverProfile.WriteValue(driverID, cloudsMaxProfileName, "cloudClear");
                driverProfile.WriteValue(driverID, windMaxProfileName, 40.ToString());
                driverProfile.WriteValue(driverID, rainMaxProfileName, 0.ToString());
                driverProfile.WriteValue(driverID, lightMaxProfileName, "dayDark");
                driverProfile.WriteValue(driverID, humidityMaxProfileName, 90.ToString());
                driverProfile.WriteValue(driverID, ageMaxSecondsProfileName, 0.ToString());
            }
        }

        internal static void WriteDomeProfile()
        {
            string driverID = "ASCOM.Wise40.Dome";
            string autoCalibrateProfileName = "AutoCalibrate";
            string bypassSafetyProfileName = "Bypass Safety";
            string syncVentWithShutterProfileName = "Sync Vent With Shutter";

            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "Dome";
                driverProfile.WriteValue(driverID, autoCalibrateProfileName, true.ToString());
                driverProfile.WriteValue(driverID, bypassSafetyProfileName, true.ToString());
                driverProfile.WriteValue(driverID, syncVentWithShutterProfileName, mode == Mode.WISE ? false.ToString() : true.ToString());
            }
        }

        internal static void WriteTelescopeProfile()
        {
            string driverID = "ASCOM.Wise40.Telescope";
            string enslaveDomeProfileName = "Enslave Dome";
            string traceStateProfileName = "Tracing";
            string debugLevelProfileName = "DebugLevel";
            string studyMotionProfileName = "StudyMotion";

            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "Telescope";
                driverProfile.WriteValue(driverID, traceStateProfileName, true.ToString());
                driverProfile.WriteValue(driverID, studyMotionProfileName, true.ToString());
                driverProfile.WriteValue(driverID, enslaveDomeProfileName, mode == Mode.ACP ? false.ToString() : true.ToString());
                driverProfile.WriteValue(driverID, debugLevelProfileName, "DebugAxes|DebugMotors|DebugExceptions|DebugASCOM|DebugLogic");
            }
        }

        internal static void WriteOCHProfile()
        {
            string driverID = "ASCOM.OCH.ObservingConditions";

            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "ObservingConditions";

                driverProfile.WriteValue(driverID, "Device Mode", "Real", "CloudCover");
                driverProfile.WriteValue(driverID, "ProgID", "ASCOM.CloudSensor.ObservingConditions", "CloudCover");
                driverProfile.WriteValue(driverID, "Switch Number", "0", "CloudCover");

                driverProfile.WriteValue(driverID, "Device Mode", "Real", "DewPoint");
                driverProfile.WriteValue(driverID, "ProgID", "ASCOM.Vantage.ObservingConditions", "DewPoint");
                driverProfile.WriteValue(driverID, "Switch Number", "0", "DewPoint");

                driverProfile.WriteValue(driverID, "Device Mode", "Real", "Humidity");
                driverProfile.WriteValue(driverID, "ProgID", "ASCOM.Vantage.ObservingConditions", "Humidity");
                driverProfile.WriteValue(driverID, "Switch Number", "0", "Humidity");

                driverProfile.WriteValue(driverID, "Device Mode", "Real", "Pressure");
                driverProfile.WriteValue(driverID, "ProgID", "ASCOM.Vantage.ObservingConditions", "Pressure");
                driverProfile.WriteValue(driverID, "Switch Number", "0", "Pressure");

                driverProfile.WriteValue(driverID, "Device Mode", "Real", "RainRate");
                driverProfile.WriteValue(driverID, "ProgID", "ASCOM.Vantage.ObservingConditions", "RainRate");
                driverProfile.WriteValue(driverID, "Switch Number", "0", "RainRate");

                driverProfile.WriteValue(driverID, "Device Mode", "Real", "SkyTemperature");
                driverProfile.WriteValue(driverID, "ProgID", "ASCOM.CloudSensor.ObservingConditions", "SkyTemperature");
                driverProfile.WriteValue(driverID, "Switch Number", "0", "SkyTemperature");

                driverProfile.WriteValue(driverID, "Device Mode", "Real", "Temperature");
                driverProfile.WriteValue(driverID, "ProgID", "ASCOM.Vantage.ObservingConditions", "Temperature");
                driverProfile.WriteValue(driverID, "Switch Number", "0", "Temperature");

                driverProfile.WriteValue(driverID, "Device Mode", "Real", "WindDirection");
                driverProfile.WriteValue(driverID, "ProgID", "ASCOM.Vantage.ObservingConditions", "WindDirection");
                driverProfile.WriteValue(driverID, "Switch Number", "0", "WindDirection");

                driverProfile.WriteValue(driverID, "Device Mode", "Real", "WindSpeed");
                driverProfile.WriteValue(driverID, "ProgID", "ASCOM.Vantage.ObservingConditions", "WindSpeed");
                driverProfile.WriteValue(driverID, "Switch Number", "0", "WindSpeed");
            }
        }

        internal static void WriteFilterWheelProfile()
        {
            string driverID = "ASCOM.Wise40.FilterWheel";

            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "FilterWheel";

                driverProfile.WriteValue(driverID, "Port", "COM5");

                driverProfile.WriteValue(driverID, "RFID", "7F0007F75E", "Wheel8/Position1");
                driverProfile.WriteValue(driverID, "RFID", "7F000817F7", "Wheel8/Position2");
                driverProfile.WriteValue(driverID, "RFID", "7F000AEFC5", "Wheel8/Position3");
                driverProfile.WriteValue(driverID, "RFID", "7C00563E5A", "Wheel8/Position4");
                driverProfile.WriteValue(driverID, "RFID", "7F001B2B73", "Wheel8/Position5");
                driverProfile.WriteValue(driverID, "RFID", "7F000ACAD5", "Wheel8/Position6");
                driverProfile.WriteValue(driverID, "RFID", "7F001B4A83", "Wheel8/Position7");
                driverProfile.WriteValue(driverID, "RFID", "7F0007BC0E", "Wheel8/Position8");

                driverProfile.WriteValue(driverID, "RFID", "7F001B4C16", "Wheel4/Position1");
                driverProfile.WriteValue(driverID, "RFID", "7C0055F4EB", "Wheel4/Position2");
                driverProfile.WriteValue(driverID, "RFID", "7F0007F75E", "Wheel4/Position3");
                driverProfile.WriteValue(driverID, "RFID", "7F001B0573", "Wheel4/Position4");

                driverProfile.WriteValue(driverID, "Filter Name", "R", "Wheel4/Position1");
                driverProfile.WriteValue(driverID, "Filter Name", "B", "Wheel8/Position1");

                driverProfile.WriteValue(driverID, "Name", "R", "Filter0");
                driverProfile.WriteValue(driverID, "Description", "Red filter", "Filter0");
                driverProfile.WriteValue(driverID, "Offset", "123", "Filter0");

                driverProfile.WriteValue(driverID, "Name", "G", "Filter1");
                driverProfile.WriteValue(driverID, "Description", "Green filter", "Filter1");
                driverProfile.WriteValue(driverID, "Offset", "-123", "Filter1");

                driverProfile.WriteValue(driverID, "Name", "B", "Filter2");
                driverProfile.WriteValue(driverID, "Description", "Blue filter", "Filter2");
                driverProfile.WriteValue(driverID, "Offset", "17", "Filter2");
            }
        }
    }
}
