//tabs=4
// --------------------------------------------------------------------------------
//
// ASCOM Dome driver for Wise40
//
// Description:	Lorem ipsum dolor sit amet, consetetur sadipscing elitr, sed diam 
//				nonumy eirmod tempor invidunt ut labore et dolore magna aliquyam 
//				erat, sed diam voluptua. At vero eos et accusam et justo duo 
//				dolores et ea rebum. Stet clita kasd gubergren, no sea takimata 
//				sanctus est Lorem ipsum dolor sit amet.
//
// Implements:	ASCOM Dome interface version: <To be completed by driver developer>
// Author:		(blumzi) Arie Blumenzweig <blumzi@013.net>
//
// Edit Log:
//
// Date			Who	Vers	Description
// -----------	---	-----	-------------------------------------------------------
// dd-mmm-yyyy	XXX	6.0.0	Initial edit, created from ASCOM driver template
// --------------------------------------------------------------------------------
//


// This is used to define code in the template that is specific to one class implementation
// unused code canbe deleted and this definition removed.
#define Dome

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;

using ASCOM;
using ASCOM.Astrometry;
using ASCOM.Astrometry.AstroUtils;
using ASCOM.Utilities;
using ASCOM.DeviceInterface;
using ASCOM.DriverAccess;
using System.Globalization;
using System.Collections;

using ASCOM.Wise40.Hardware;
using ASCOM.Wise40.Common;

namespace ASCOM.Wise40
{
    //
    // Your driver's DeviceID is ASCOM.Wise40.Dome
    //
    // The Guid attribute sets the CLSID for ASCOM.Wise40.Dome
    // The ClassInterface/None addribute prevents an empty interface called
    // _Wise40 from being created and used as the [default] interface
    //

    /// <summary>
    /// ASCOM Dome Driver for Wise40.
    /// </summary>
    [Guid("5cec8f8d-f8be-453d-b80f-9a93a758d08a")]
    [ClassInterface(ClassInterfaceType.None)]
    public class Dome : IDomeV2, IDisposable
    {
        /// <summary>
        /// ASCOM DeviceID (COM ProgID) for this driver.
        /// The DeviceID is used by ASCOM applications to load the driver at runtime.
        /// </summary>
        internal static string driverID = "ASCOM.Wise40.Dome";
        /// <summary>
        /// Dome driver for the Wise 40" telescope.
        /// </summary>
        private static string driverDescription = "Wise40 Dome";

        internal static string traceStateProfileName = "Trace Level";
        internal static string debugLevelProfileName = "Debug Level";

        public bool traceState;
        public Common.Debugger debugger;

        /// <summary>
        /// Private variable to hold the connected state
        /// </summary>
        private bool connectedState;

        /// <summary>
        /// Private variable to hold an ASCOM Utilities object
        /// </summary>
        private Util utilities;

        /// <summary>
        /// Private variable to hold an ASCOM AstroUtilities object to provide the Range method
        /// </summary>
        private AstroUtils astroUtilities;

        /// <summary>
        /// Private variable to hold the trace logger object (creates a diagnostic log file with information that you specify)
        /// </summary>
        private TraceLogger tl;

        private  WiseDome wisedome;

        public AutoResetEvent arrived = new AutoResetEvent(false);

        /// <summary>
        /// Initializes a new instance of the <see cref="Wise40Hardware"/> class.
        /// Must be public for COM registration.
        /// </summary>
        public Dome()
        {
            debugger = new Common.Debugger();
            ReadProfile(); // Read device configuration from the ASCOM Profile store

            tl = new TraceLogger("", "Dome");
            tl.Enabled = traceState;
            tl.LogMessage("Dome", "Starting initialisation");

            connectedState = false; // Initialise connected to false
            utilities = new Util(); //Initialise util object
            astroUtilities = new AstroUtils(); // Initialise astro utilities object

            wisedome = new WiseDome(arrived); // Initialise Wise40 dome

            tl.LogMessage("Dome", "Completed initialisation");
        }


        //
        // PUBLIC COM INTERFACE IDomeV2 IMPLEMENTATION
        //

        #region Common properties and methods.

        /// <summary>
        /// Displays the Setup Dialog form.
        /// If the user clicks the OK button to dismiss the form, then
        /// the new settings are saved, otherwise the old values are reloaded.
        /// THIS IS THE ONLY PLACE WHERE SHOWING USER INTERFACE IS ALLOWED!
        /// </summary>
        public void SetupDialog()
        {
            // consider only showing the setup dialog if not connected
            // or call a different dialog if connected
            if (IsConnected)
                System.Windows.Forms.MessageBox.Show("Already connected, just press OK");

            using (SetupDialogForm F = new SetupDialogForm(this))
            {
                var result = F.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    WriteProfile(); // Persist device configuration values to the ASCOM Profile store
                }
            }
        }

        public ArrayList SupportedActions
        {
            get
            {
                tl.LogMessage("SupportedActions Get", "Returning empty arraylist");
                return new ArrayList();
            }
        }

        public string Action(string actionName, string actionParameters)
        {
            throw new ASCOM.ActionNotImplementedException("Action " + actionName + " is not implemented by this driver");
        }

        public void CommandBlind(string command, bool raw)
        {
            CheckConnected("CommandBlind");
            // Call CommandString and return as soon as it finishes
            this.CommandString(command, raw);
            // or
            throw new ASCOM.MethodNotImplementedException("CommandBlind");
        }

        public bool CommandBool(string command, bool raw)
        {
            CheckConnected("CommandBool");
            string ret = CommandString(command, raw);
            // TODO decode the return string and return true or false
            // or
            throw new ASCOM.MethodNotImplementedException("CommandBool");
        }

        public string CommandString(string command, bool raw)
        {
            CheckConnected("CommandString");
            // it's a good idea to put all the low level communication with the device here,
            // then all communication calls this function
            // you need something to ensure that only one command is in progress at a time

            throw new ASCOM.MethodNotImplementedException("CommandString");
        }

        public void Dispose()
        {
            // Clean up the tracelogger and util objects
            tl.Enabled = false;
            tl.Dispose();
            tl = null;
            utilities.Dispose();
            utilities = null;
            astroUtilities.Dispose();
            astroUtilities = null;
            wisedome.Dispose();
            wisedome = null;
            //Dispose(true);
        }

        public bool Connected
        {
            get
            {
                tl.LogMessage("Connected Get", IsConnected.ToString());
                return IsConnected;
            }
            set
            {
                tl.LogMessage("Connected Set", value.ToString());
                if (value == IsConnected)
                    return;

                if (value)
                {
                    connectedState = true;
                    tl.LogMessage("Connected Set", "Connected");
                }
                else
                {
                    connectedState = false;
                    tl.LogMessage("Connected Set", "Disconnected");
                }
                wisedome.Connect(connectedState);
            }
        }

        public string Description
        {
            get
            {
                tl.LogMessage("Description Get", driverDescription);
                return driverDescription;
            }
        }

        public string DriverInfo
        {
            get
            {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                string driverInfo = "First draft, Version: " + string.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor);
                tl.LogMessage("DriverInfo Get", driverInfo);
                return driverInfo;
            }
        }

        public string DriverVersion
        {
            get
            {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                string driverVersion = String.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor);
                tl.LogMessage("DriverVersion Get", driverVersion);
                return driverVersion;
            }
        }

        public short InterfaceVersion
        {
            // set by the driver wizard
            get
            {
                tl.LogMessage("InterfaceVersion Get", "2");
                return Convert.ToInt16("2");
            }
        }

        public string Name
        {
            get
            {
                string name = "Wise40 Dome";
                tl.LogMessage("Name Get", name);
                return name;
            }
        }

        #endregion

        #region IDome Implementation

        public void AbortSlew()
        {
            wisedome.AbortSlew();
            tl.LogMessage("AbortSlew", "");
        }

        public double Altitude
        {
            get
            {
                tl.LogMessage("Altitude Get", "Not implemented");
                throw new ASCOM.PropertyNotImplementedException("Altitude", false);
            }
        }

        public bool AtHome
        {
            get
            {
                bool atHome = wisedome.AtCaliPoint;

                tl.LogMessage("AtHome Get", atHome.ToString());
                return atHome;
            }
        }

        public bool AtPark
        {
            get
            {
                bool atPark = wisedome.AtPark;

                tl.LogMessage("AtPark Get", atPark.ToString());
                return atPark;
            }
        }

        public double Azimuth
        {
            get
            {
                Angle az = wisedome.Azimuth;

                tl.LogMessage("Azimuth Get", az.ToString());
                return az.Degrees;
            }
        }

        public bool CanFindHome
        {
            get
            {
                tl.LogMessage("CanFindHome Get", true.ToString());
                return true;
            }
        }

        public bool CanPark
        {
            get
            {
                tl.LogMessage("CanPark Get", true.ToString());
                return true;
            }
        }

        public bool CanSetAltitude
        {
            get
            {
                tl.LogMessage("CanSetAltitude Get", false.ToString());
                return false;
            }
        }

        public bool CanSetAzimuth
        {
            get
            {
                tl.LogMessage("CanSetAzimuth Get", true.ToString());
                return true;
            }
        }

        public bool CanSetPark
        {
            get
            {
                tl.LogMessage("CanSetPark Get", false.ToString());
                return false;
            }
        }

        public bool CanSetShutter
        {
            get
            {
                tl.LogMessage("CanSetShutter Get", true.ToString());
                return true;
            }
        }

        public bool CanSlave
        {
            get
            {
                tl.LogMessage("CanSlave Get", true.ToString());
                return true;
            }
        }

        public bool CanSyncAzimuth
        {
            get
            {
                tl.LogMessage("CanSyncAzimuth Get", true.ToString());
                return true;
            }
        }

        public void CloseShutter()
        {
            if (wisedome.Slewing)
                throw new ASCOM.InvalidOperationException("Denied, dome is slewing!");

            wisedome.CloseShutter();
            tl.LogMessage("CloseShutter", "");
        }

        public void FindHome()
        {
            if (wisedome.ShutterIsActive())
            {
                tl.LogMessage("FindHome", "Cannot FindHome, shutter is active.");
                throw new ASCOM.InvalidOperationException("Cannot FindHome, shutter is active!");
            }

            tl.LogMessage("FindHome", "Calling wisedome.FindHome");
            wisedome.FindHome();
        }

        public void OpenShutter()
        {
            if (wisedome.Slewing)
                throw new ASCOM.InvalidOperationException("Cannot OpenShutter, dome is slewing!");

            wisedome.OpenShutter();
            tl.LogMessage("OpenShutter", "");
        }

        public void Park()
        {
            if (Slaved)
                throw new InvalidOperationException("Cannot Park, dome is Slaved");

            if (!wisedome.Calibrated)
                throw new InvalidOperationException("Cannot Park, dome is NOT calibrated");

            if (wisedome.ShutterIsActive())
            {
                tl.LogMessage("Park", "Cannot Park, shutter is active.");
                throw new ASCOM.InvalidOperationException("Cannot Park, shutter is active!");
            }

            wisedome.Park();
            tl.LogMessage("Park", "");
        }

        public void SetPark()
        {
            tl.LogMessage("SetPark", "Not implemented");
            throw new ASCOM.MethodNotImplementedException("SetPark");
        }

        public ShutterState ShutterStatus
        {
            get
            {
                WiseDome.ShutterState state = wisedome.shutterState;
                ShutterState ret = ShutterState.shutterError;

                switch (state)
                {
                    case WiseDome.ShutterState.Closed:
                        ret = ShutterState.shutterClosed;
                        break;
                    case WiseDome.ShutterState.Closing:
                        ret = ShutterState.shutterClosing;
                        break;
                    case WiseDome.ShutterState.Open:
                        ret = ShutterState.shutterOpen;
                        break;
                    case WiseDome.ShutterState.Opening:
                        ret = ShutterState.shutterOpening;
                        break;
                }
                tl.LogMessage("ShutterState get", ret.ToString());
                return ret;
            }
        }

        public bool Slaved
        {
            get
            {
                bool slaved = wisedome.Slaved;

                tl.LogMessage("Slaved Get", slaved.ToString());
                return slaved;
            }

            set
            {
                tl.LogMessage("Slaved Set", value.ToString());
                wisedome.Slaved = value;
            }
        }

        public void SlewToAltitude(double Altitude)
        {
            tl.LogMessage("SlewToAltitude", "Not implemented");
            throw new ASCOM.MethodNotImplementedException("SlewToAltitude");
        }

        public void SlewToAzimuth(double Azimuth)
        {
            if (wisedome.Slaved)
                throw new InvalidOperationException("Cannot SlewToAzimuth, dome is Slaved");

            if (Azimuth < 0 || Azimuth >= 360)
                throw new InvalidValueException(string.Format("Invalid azimuth: {0}, must be >= 0 and < 360", Azimuth));

            if (wisedome.ShutterIsActive())
            {
                tl.LogMessage("SlewToAzimuth", "Denied, shutter is active.");
                throw new ASCOM.InvalidOperationException("Cannot move, shutter is active!");
            }

            wisedome.SlewToAzimuth(Azimuth);
            tl.LogMessage("SlewToAzimuth", Azimuth.ToString("#.##"));
        }

        public bool Slewing
        {
            get
            {
                if (Slaved)
                    throw new InvalidOperationException("Cannot get Slewing while dome is Slaved");

                bool slewing = wisedome.Slewing;

                tl.LogMessage("Slewing Get", slewing.ToString());
                return slewing;
            }
        }

        public void SyncToAzimuth(double degrees)
        {
            Angle ang = new Angle(degrees);

            if (ang.Degrees < 0 || ang.Degrees >= 360)
                throw new InvalidValueException(string.Format("Cannot SyncToAzimuth({0}), must be >- 0 and < 360", ang));

            tl.LogMessage("SyncToAzimuth", ang.ToString());
            wisedome.Azimuth = ang;
        }

        public uint debugLevel
        {
            get
            {
                return debugger.Level;
            }

            set
            {
                debugger.Level = value;
            }
        }

        #endregion

        #region Private properties and methods
        // here are some useful properties and methods that can be used as required
        // to help with driver development

        #region ASCOM Registration

        // Register or unregister driver for ASCOM. This is harmless if already
        // registered or unregistered. 
        //
        /// <summary>
        /// Register or unregister the driver with the ASCOM Platform.
        /// This is harmless if the driver is already registered/unregistered.
        /// </summary>
        /// <param name="bRegister">If <c>true</c>, registers the driver, otherwise unregisters it.</param>
        private static void RegUnregASCOM(bool bRegister)
        {
            using (var P = new ASCOM.Utilities.Profile())
            {
                P.DeviceType = "Dome";
                if (bRegister)
                {
                    P.Register(driverID, driverDescription);
                }
                else
                {
                    P.Unregister(driverID);
                }
            }
        }

        /// <summary>
        /// This function registers the driver with the ASCOM Chooser and
        /// is called automatically whenever this class is registered for COM Interop.
        /// </summary>
        /// <param name="t">Type of the class being registered, not used.</param>
        /// <remarks>
        /// This method typically runs in two distinct situations:
        /// <list type="numbered">
        /// <item>
        /// In Visual Studio, when the project is successfully built.
        /// For this to work correctly, the option <c>Register for COM Interop</c>
        /// must be enabled in the project settings.
        /// </item>
        /// <item>During setup, when the installer registers the assembly for COM Interop.</item>
        /// </list>
        /// This technique should mean that it is never necessary to manually register a driver with ASCOM.
        /// </remarks>
        [ComRegisterFunction]
        public static void RegisterASCOM(Type t)
        {
            RegUnregASCOM(true);
        }

        /// <summary>
        /// This function unregisters the driver from the ASCOM Chooser and
        /// is called automatically whenever this class is unregistered from COM Interop.
        /// </summary>
        /// <param name="t">Type of the class being registered, not used.</param>
        /// <remarks>
        /// This method typically runs in two distinct situations:
        /// <list type="numbered">
        /// <item>
        /// In Visual Studio, when the project is cleaned or prior to rebuilding.
        /// For this to work correctly, the option <c>Register for COM Interop</c>
        /// must be enabled in the project settings.
        /// </item>
        /// <item>During uninstall, when the installer unregisters the assembly from COM Interop.</item>
        /// </list>
        /// This technique should mean that it is never necessary to manually unregister a driver from ASCOM.
        /// </remarks>
        [ComUnregisterFunction]
        public static void UnregisterASCOM(Type t)
        {
            RegUnregASCOM(false);
        }

        #endregion

        /// <summary>
        /// Returns true if there is a valid connection to the driver hardware
        /// </summary>
        private bool IsConnected
        {
            get
            {
                // TODO check that the driver hardware connection exists and is connected to the hardware
                return connectedState;
            }
        }

        /// <summary>
        /// Use this function to throw an exception if we aren't connected to the hardware
        /// </summary>
        /// <param name="message"></param>
        private void CheckConnected(string message)
        {
            if (!IsConnected)
            {
                throw new ASCOM.NotConnectedException(message);
            }
        }

        /// <summary>
        /// Read the device configuration from the ASCOM Profile store
        /// </summary>
        internal void ReadProfile()
        {
            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "Dome";
                traceState = Convert.ToBoolean(driverProfile.GetValue(driverID, traceStateProfileName, string.Empty, "false"));
                debugger.Level = Convert.ToUInt32(driverProfile.GetValue(driverID, debugLevelProfileName, string.Empty, "0"));
            }
        }

        /// <summary>
        /// Write the device configuration to the  ASCOM  Profile store
        /// </summary>
        internal void WriteProfile()
        {
            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "Dome";
                driverProfile.WriteValue(driverID, traceStateProfileName, traceState.ToString());
                driverProfile.WriteValue(driverID, debugLevelProfileName, debugger.Level.ToString());
            }
        }

        #endregion

    }
}