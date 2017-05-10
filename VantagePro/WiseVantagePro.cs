﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;

using System.IO;
using ASCOM.Wise40.Common;
using ASCOM.Utilities;


namespace ASCOM.Wise40.VantagePro
{
    public class WiseVantagePro
    {
        private string _dataFile;
        public static string driverID = "ASCOM.Wise40.VantagePro.ObservingConditions";
        internal static string dataFileProfileName = "DataFile";
        private static WiseVantagePro _instance = new WiseVantagePro();
        private static Version version = new Version("0.2");
        public static string driverDescription = string.Format("ASCOM Wise40.VantagePro v{0}", version.ToString());
        private Util util = new Util();
        private TraceLogger tl;

        Common.Debugger debugger = Debugger.Instance;
        private bool _connected = false;
        private bool _initialized = false;

        public WiseVantagePro() { }
        static WiseVantagePro() { }

        private Dictionary<string, string> sensorData;

        public static WiseVantagePro Instance
        {
            get
            {
                return _instance;
            }
        }

        /// <summary>
        /// Forces the driver to immediatley query its attached hardware to refresh sensor
        /// values
        /// </summary>
        public void Refresh()
        {
            tl.LogMessage("Refresh", "dataFile: " + _dataFile);

            if (_dataFile == null || _dataFile == string.Empty)
            {
                if (_connected)
                    throw new InvalidValueException("Null or empty dataFile name");
                else
                    return;
            }

            sensorData = new Dictionary<string, string>();
            using (StreamReader sr = new StreamReader(_dataFile))
            {
                string[] words;
                string line;

                if (sr == null)
                    throw new InvalidValueException(string.Format("Refresh: cannot open \"{0}\" for read.", _dataFile));

                while ((line = sr.ReadLine()) != null)
                {
                    words = line.Split('=');
                    if (words.Length != 3)
                        continue;
                    sensorData[words[0]] = words[1];
                }
            }
        }

        public void init()
        {
            if (_initialized)
                return;
            
            debugger.init();
            tl = new TraceLogger("", "Wise40.VantagePro");
            tl.Enabled = debugger.Tracing;
            tl.LogMessage("ObservingConditions", "initialized");

            ReadProfile();
            Refresh();

            _initialized = true;
        }

        public bool Connected
        {
            get
            {
                //tl.LogMessage("Connected Get", IsConnected.ToString());
                //return IsConnected;
                return _connected;
            }
            set
            {
                tl.LogMessage("Connected Set", value.ToString());
                if (value == _connected)
                    return;

                _connected = value;
            }
        }

        public string Description
        {
            // TODO customise this device description
            get
            {
                tl.LogMessage("Description Get", driverDescription);
                return driverDescription;
            }
        }

        public static string DriverDescription
        {
            get
            {
                return driverDescription;
            }
        }

        public string DriverInfo
        {
            get
            {
                string driverInfo = "Wrapper for VantagePro Report file. Version: " + String.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor);
                tl.LogMessage("DriverInfo Get", driverInfo);
                return driverInfo;
            }
        }

        public string DriverVersion
        {
            get
            {
                string driverVersion = String.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor);
                tl.LogMessage("DriverVersion Get", driverVersion);
                return driverVersion;
            }
        }

        /// <summary>
        /// Read the device configuration from the ASCOM Profile store
        /// </summary>
        internal void ReadProfile()
        {
            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "ObservingConditions";
                _dataFile = driverProfile.GetValue(driverID, dataFileProfileName, string.Empty, string.Empty);
            }
        }

        /// <summary>
        /// Write the device configuration to the  ASCOM  Profile store
        /// </summary>
        internal void WriteProfile()
        {
            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "ObservingConditions";
                driverProfile.WriteValue(driverID, dataFileProfileName, _dataFile);
            }
        }

        public string DataFile
        {
            get
            {
                return _dataFile;
            }

            set
            {
                _dataFile = value;
            }
        }

        #region IObservingConditions Implementation

        /// <summary>
        /// Gets and sets the time period over which observations wil be averaged
        /// </summary>
        /// <remarks>
        /// Get must be implemented, if it can't be changed it must return 0
        /// Time period (hours) over which the property values will be averaged 0.0 =
        /// current value, 0.5= average for the last 30 minutes, 1.0 = average for the
        /// last hour
        /// </remarks>
        public double AveragePeriod
        {
            get
            {
                tl.LogMessage("AveragePeriod", "get - 0");
                return 0;
            }
            set
            {
                tl.LogMessage("AveragePeriod", string.Format("set - {0}", value));
                if (value != 0)
                    throw new InvalidValueException("Only 0.0 accepted");
            }
        }

        /// <summary>
        /// Amount of sky obscured by cloud
        /// </summary>
        /// <remarks>0%= clear sky, 100% = 100% cloud coverage</remarks>
        public double CloudCover
        {
            get
            {
                tl.LogMessage("CloudCover", "get - not implemented");
                throw new PropertyNotImplementedException("CloudCover", false);
            }
        }

        /// <summary>
        /// Atmospheric dew point at the observatory in deg C
        /// </summary>
        /// <remarks>
        /// Normally optional but mandatory if <see cref=" ASCOM.DeviceInterface.IObservingConditions.Humidity"/>
        /// Is provided
        /// </remarks>
        public double DewPoint
        {
            get
            {
                var dewPoint = Convert.ToDouble(sensorData["insideDewPt"]);

                tl.LogMessage("DewPoint", "get - " + dewPoint.ToString());
                return dewPoint;
            }
        }

        /// <summary>
        /// Atmospheric relative humidity at the observatory in percent
        /// </summary>
        /// <remarks>
        /// Normally optional but mandatory if <see cref="ASCOM.DeviceInterface.IObservingConditions.DewPoint"/> 
        /// Is provided
        /// </remarks>
        public double Humidity
        {
            get
            {
                var humidity = Convert.ToDouble(sensorData["insideHumidity"]);

                tl.LogMessage("Humidity", "get - " + humidity.ToString());
                return humidity;
            }
        }

        /// <summary>
        /// Atmospheric pressure at the observatory in hectoPascals (mB)
        /// </summary>
        /// <remarks>
        /// This must be the pressure at the observatory and not the "reduced" pressure
        /// at sea level. Please check whether your pressure sensor delivers local pressure
        /// or sea level pressure and adjust if required to observatory pressure.
        /// </remarks>
        public double Pressure
        {
            get
            {
                var pressure = Convert.ToDouble(sensorData["barometer"]);

                tl.LogMessage("Pressure", "get - " + pressure.ToString());
                return pressure;
            }
        }

        /// <summary>
        /// Rain rate at the observatory
        /// </summary>
        /// <remarks>
        /// This property can be interpreted as 0.0 = Dry any positive nonzero value
        /// = wet.
        /// </remarks>
        public double RainRate
        {
            get
            {
                var rainRate = Convert.ToDouble(sensorData["rainRate"]);

                tl.LogMessage("RainRate", "get - " + rainRate.ToString());
                return rainRate;
            }
        }
        

        /// <summary>
        /// Provides a description of the sensor providing the requested property
        /// </summary>
        /// <param name="PropertyName">Name of the property whose sensor description is required</param>
        /// <returns>The sensor description string</returns>
        /// <remarks>
        /// PropertyName must be one of the sensor properties, 
        /// properties that are not implemented must throw the MethodNotImplementedException
        /// </remarks>
        public string SensorDescription(string PropertyName)
        {
            switch (PropertyName)
            {
                case "AveragePeriod":
                    return "Average period in hours, immediate values are only available";

                case "DewPoint":
                case "Humidity":
                case "Pressure":
                case "Temperature":
                case "WindDirection":
                case "WindSpeed":
                case "RainRate":
                    return "SensorDescription - " + PropertyName;

                case "SkyBrightness":
                case "SkyQuality":
                case "StarFWHM":
                case "SkyTemperature":
                case "WindGust":
                case "CloudCover":
                    tl.LogMessage("SensorDescription", PropertyName + " - not implemented");
                    throw new MethodNotImplementedException("SensorDescription(" + PropertyName + ")");
                default:
                    tl.LogMessage("SensorDescription", PropertyName + " - unrecognised");
                    throw new ASCOM.InvalidValueException("SensorDescription(" + PropertyName + ")");
            }
        }

        /// <summary>
        /// Sky brightness at the observatory
        /// </summary>
        public double SkyBrightness
        {
            get
            {
                tl.LogMessage("SkyBrightness", "get - not implemented");
                throw new PropertyNotImplementedException("SkyBrightness", false);
            }
        }

        /// <summary>
        /// Sky quality at the observatory
        /// </summary>
        public double SkyQuality
        {
            get
            {
                tl.LogMessage("SkyQuality", "get - not implemented");
                throw new PropertyNotImplementedException("SkyQuality", false);
            }
        }

        /// <summary>
        /// Seeing at the observatory
        /// </summary>
        public double StarFWHM
        {
            get
            {
                tl.LogMessage("StarFWHM", "get - not implemented");
                throw new PropertyNotImplementedException("StarFWHM", false);
            }
        }

        /// <summary>
        /// Sky temperature at the observatory in deg C
        /// </summary>
        public double SkyTemperature
        {
            get
            {
                tl.LogMessage("SkyTemperature", "get - not implemented");
                throw new PropertyNotImplementedException("SkyTemperature", false);
            }
        }

        /// <summary>
        /// Temperature at the observatory in deg C
        /// </summary>
        public double Temperature
        {
            get
            {
                var temperature = Convert.ToDouble(sensorData["insideTemp"]);

                tl.LogMessage("Temperature", "get - " + temperature.ToString());
                return temperature;
            }
        }

        /// <summary>
        /// Provides the time since the sensor value was last updated
        /// </summary>
        /// <param name="PropertyName">Name of the property whose time since last update Is required</param>
        /// <returns>Time in seconds since the last sensor update for this property</returns>
        /// <remarks>
        /// PropertyName should be one of the sensor properties Or empty string to get
        /// the last update of any parameter. A negative value indicates no valid value
        /// ever received.
        /// </remarks>
        public double TimeSinceLastUpdate(string PropertyName)
        {
            switch (PropertyName)
            {
                case "SkyBrightness":
                case "SkyQuality":
                case "StarFWHM":
                case "SkyTemperature":
                case "WindGust":
                case "CloudCover":
                    tl.LogMessage("TimeSinceLastUpdate", PropertyName + " - not implemented");
                    throw new MethodNotImplementedException("SensorDescription(" + PropertyName + ")");
            }
            string dateTime = sensorData["stationDate"] + " " + sensorData["stationTime"] + "m";
            DateTime lastUpdate = Convert.ToDateTime(dateTime);
            double seconds = (DateTime.Now - lastUpdate).TotalSeconds;

            tl.LogMessage("TimeSinceLastUpdate", PropertyName + seconds.ToString());
            return seconds;
        }

        /// <summary>
        /// Wind direction at the observatory in degrees
        /// </summary>
        /// <remarks>
        /// 0..360.0, 360=N, 180=S, 90=E, 270=W. When there Is no wind the driver will
        /// return a value of 0 for wind direction
        /// </remarks>
        public double WindDirection
        {
            get
            {
                var windDir = Convert.ToDouble(sensorData["windDir"]);

                tl.LogMessage("WindDirection", "get - " + windDir.ToString());
                return windDir;
            }
        }

        /// <summary>
        /// Peak 3 second wind gust at the observatory over the last 2 minutes in m/s
        /// </summary>
        public double WindGust
        {
            get
            {
                tl.LogMessage("WindGust", "get - not implemented");
                throw new PropertyNotImplementedException("WindGust", false);
            }
        }

        /// <summary>
        /// Wind speed at the observatory in m/s
        /// </summary>
        public double WindSpeed
        {
            get
            {
                var windSpeed = Convert.ToDouble(sensorData["windSpeed"]);

                tl.LogMessage("WindSpeed", "get - " + windSpeed.ToString());
                return windSpeed;
            }
        }

        #endregion

        public string Name
        {
            get
            {
                return "Wise40.VantagePro";
            }
        }

    }
}