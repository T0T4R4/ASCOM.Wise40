﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ASCOM.Wise40.Common;
using ASCOM.Wise40.Telescope;
using ASCOM.DeviceInterface;

namespace ASCOM.Wise40.Hardware
{
    /// <summary>
    /// A WiseVirtualMotor implements the three moving rates available for each axis at the Wise40 telescope
    ///  by turning on (and off) the relevant hardware pins, as follows:
    ///   - rateSlew:  motorPin + slewPin
    ///   - rateSet:   motorPin
    ///   - rateGuide: guidePin
    ///   
    /// When simulated the WiseVirtualMotor will also increase/decrease the value of the relevant (simulated) encoder.
    /// </summary>
    public class WiseVirtualMotor : WiseObject, IConnectable, IDisposable //, ISimulated
    {
        private WisePin motorPin, guideMotorPin, slewPin;
        private List<WisePin> activePins = new List<WisePin>();
        private List<object> encoders;
        private System.Threading.Timer simulationTimer;
        public double currentRate;
        private int simulationTimerFrequency;
        private int timer_counts;
        private DateTime prevTick;
        private TelescopeAxes _axis, _otherAxis;
        private Const.AxisDirection _direction;   // There are separate WiseMotor instances for North, South, East, West
        //private bool _simulated = false;
        private bool _connected = false;
        private Debugger debugger = Debugger.Instance;
        private WiseTele wisetele = WiseTele.Instance;
        private WiseSite wisesite = WiseSite.Instance;

        public WiseVirtualMotor(
            string name,
            WisePin motorPin,
            WisePin guideMotorPin,
            WisePin slewPin,
            TelescopeAxes axis,
            Const.AxisDirection direction,
            List<object> encoders = null)
        {
            this.Name = name;

            this.motorPin = motorPin;
            this.guideMotorPin = guideMotorPin;
            this.slewPin = slewPin;

            this.encoders = encoders;
            this._axis = axis;
            this._otherAxis = (_axis == TelescopeAxes.axisPrimary) ?
                TelescopeAxes.axisSecondary : TelescopeAxes.axisPrimary;
            this._direction = direction;
            //Simulated = motorPin.Simulated || (guideMotorPin != null && guideMotorPin.Simulated);

            if (Simulated && (encoders == null || encoders.Count() == 0))
                throw new WiseException(Name + ": A simulated WiseVirtualMotor must have at least one encoder reference");

            if (Simulated)
            {
                simulationTimerFrequency = 30; // 15;
                TimerCallback TimerCallback = new TimerCallback(bumpEncoders);
                simulationTimer = new System.Threading.Timer(TimerCallback, null, Timeout.Infinite, Timeout.Infinite);
            } 
        }

        public void SetOn(double rate)
        {
            rate = Math.Abs(rate);
            debugger.WriteLine(Debugger.DebugLevel.DebugMotors, "{0}: On at {1}", Name, WiseTele.RateName(rate));

            if (rate == Const.rateSlew)
                activePins = new List<WisePin>() { slewPin, motorPin };
            else if (rate == Const.rateSet || rate == Const.rateTrack)
                activePins = new List<WisePin>() { motorPin };
            else if (rate == Const.rateGuide)
                activePins = new List<WisePin>() { guideMotorPin };
            currentRate = rate;

            foreach (WisePin pin in activePins)
                pin.SetOn();

            if (Simulated)
            {
                timer_counts = 0;
                prevTick = DateTime.Now;
                simulationTimer.Change(0, 1000 / simulationTimerFrequency);
            }
        }

        public void SetOff()
        {
            debugger.WriteLine(Debugger.DebugLevel.DebugMotors,
                "{0}: Off (was at {1})", Name, WiseTele.RateName(currentRate));

            if (Simulated)
                simulationTimer.Change(Timeout.Infinite, Timeout.Infinite);

            if (activePins != null && activePins.Count > 0) {
                foreach (WisePin pin in activePins)
                {
                    bool inUseByOtherAxis;

                    if (pin == slewPin)
                    {
                        inUseByOtherAxis = false;
                        foreach (WiseVirtualMotor m in wisetele.axisMotors[_otherAxis])
                            if (m.isOn)
                            {
                                inUseByOtherAxis = true;
                                break;
                            }
                        if (!inUseByOtherAxis)
                            pin.SetOff();
                    } else
                        pin.SetOff();
                }
                activePins.Clear();
            }
            currentRate = Const.rateStopped;
        }

        public bool isOn
        {
            get
            {
                List<WisePin> pins = new List<WisePin>() { motorPin, guideMotorPin };

                foreach (var pin in pins)
                    if (pin != null && pin.isOn)
                        return true;
                return false;
            }
        }

        public bool isOff
        {
            get
            {
                return !isOn;
            }
        }

        /// <summary>
        /// This is used only by simulated motors.
        /// It is called at a timer interval (simulationTimerFrequency) and increases/decreases
        ///  the attached encoder(s) according to the motor's currentRate.
        ///  
        /// The TrackMotor is a special case.
        /// </summary>
        /// <param name="StateObject"></param>
        private void bumpEncoders(object StateObject)
        {
            if (!Simulated)
                return;

            bool primary = (_axis == TelescopeAxes.axisPrimary) ? true : false;
            Angle delta = Angle.zero;

            //
            // Calculate the delta to be added/subtracted from the attached encoder(s)
            //
            if (Name == "TrackMotor")
            {
                //
                // To better simulate the tracking-motor, we use the actual LocalSiderealTime
                //  that passed since the last time we read it.  This neutralizes
                //  inaccuracies of the timer intervals.
                //
                // The wisetele._lastTrackingLST variable gets initialized each time
                //  Tracking is enabled.
                //
                double lstHoursNow = wisesite.LocalSiderealTime.Hours;
                delta = Angle.FromHours(lstHoursNow - wisetele._lastTrackingLST);
                wisetele._lastTrackingLST = lstHoursNow;
            }
            else
            {
                double degrees = currentRate / simulationTimerFrequency;
                double hours = Angle.Deg2Hours(currentRate) / simulationTimerFrequency;
                delta = primary ? Angle.FromHours(hours, Angle.Type.HA) : Angle.FromDegrees(degrees, Angle.Type.Dec);
            }

            foreach (IEncoder encoder in encoders)
            {
                Angle before, after;
                string op;

                lock (primary ? wisetele._primaryEncoderLock : wisetele._secondaryEncoderLock)
                {
                    before = primary ?
                        Angle.FromHours(wisetele.HourAngle, Angle.Type.HA) :
                        Angle.FromDegrees(wisetele.Declination);

                    if (_direction == Const.AxisDirection.Increasing)
                    {
                        if (primary)
                        {
                            op = "-";
                            encoder.Angle -= delta;
                        }
                        else
                        {
                            op = "+";
                            encoder.Angle += delta;
                        }
                    }
                    else
                    {
                        if (primary)
                        {
                            op = "+";
                            encoder.Angle += delta;
                        }
                        else
                        {
                            op = "-";
                            encoder.Angle -= delta;
                        }
                    }

                    after = primary ? 
                        Angle.FromHours(wisetele.HourAngle, Angle.Type.HA) : 
                        Angle.FromDegrees(wisetele.Declination);
                }

                debugger.WriteLine(Debugger.DebugLevel.DebugMotors,
                        "bumpEncoders: {0}: {1}: {13} {2}:  {3} {4} {5} = {6} ({7} {8} {9} = {10}) (#{11}, {12} ms)",
                        Name,                   // 0
                        encoder.Name,           // 1
                        primary ? "ha" : "dec", // 2
                        before,                 // 3
                        op,                     // 4
                        delta,                  // 5
                        after,                  // 6
                        before.Degrees,         // 7
                        op,                     // 8
                        delta.Degrees,          // 9
                        after.Degrees,          // 10
                        timer_counts++,         // 11
                        DateTime.Now.Subtract(prevTick).Milliseconds,   // 12
                        WiseTele.RateName(currentRate));                // 13
                prevTick = DateTime.Now;
            }
        }

        public void Connect(bool connected)
        {
            foreach (WisePin pin in new List<WisePin>() { motorPin, guideMotorPin })
                if (pin != null)
                    pin.Connect(connected);
            if (connected && slewPin != null && !slewPin.Connected)
                slewPin.Connect(connected);
            _connected = connected;
        }

        public bool Connected
        {
            get
            {
                return _connected;
            }
        }

        public void Dispose()
        {
            foreach (WisePin pin in new List<WisePin>() { motorPin, guideMotorPin })
                if (pin != null)
                    pin.Dispose();
        }

        public override string ToString()
        {
            return Name;
        }

        //public bool Simulated
        //{
        //    get
        //    {
        //        return _simulated;
        //    }
        //    set
        //    {
        //        _simulated = value;
        //    }
        //}
    }
}