﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ASCOM.Wise40.Common;

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
    public class WiseVirtualMotor : WiseObject, IConnectable, IDisposable, ISimulated
    {
        private WisePin motorPin, guideMotorPin, slewPin;
        private List<WisePin> activePins;
        private List<object> encoders;
        private System.Threading.Timer simulationTimer;
        private double currentRate;
        private int simulationTimerFrequency;
        private int timer_counts;
        private DateTime prevTick;
        private WiseTele.AxisDirection direction;   // There are separate WiseMotor instances for North, South, East, West
        private bool _simulated = false;
        private bool _connected = false;

        public WiseVirtualMotor(string name, WisePin motorPin, WisePin guideMotorPin, WisePin slewPin, WiseTele.AxisDirection direction, List<object> encoders = null)
        {
            this.name = name;

            this.motorPin = motorPin;
            this.guideMotorPin = guideMotorPin;
            this.slewPin = slewPin;

            this.encoders = encoders;
            this.direction = direction;
            simulated = motorPin.simulated || (guideMotorPin != null && guideMotorPin.simulated);

            if (simulated && (encoders == null || encoders.Count() == 0))
                throw new WiseException(name + ": A simulated WiseVirtualMotor must have at least one encoder reference");

            if (simulated)
            {
                simulationTimerFrequency = 15;
                TimerCallback TimerCallback = new TimerCallback(bumpEncoders);
                simulationTimer = new System.Threading.Timer(TimerCallback, null, Timeout.Infinite, Timeout.Infinite);
            }
        }

        public void SetOn(double rate)
        {
            WiseTele.Instance.log("SetOn: {0}: On at {1}", name, WiseTele.rateName[rate]);

            rate = Math.Abs(rate);
            if (rate == WiseTele.rateSlew)
                activePins = new List<WisePin>() { motorPin, slewPin };
            else if (rate == WiseTele.rateSet || rate == WiseTele.rateTrack)
                activePins = new List<WisePin>() { motorPin };
            else if (rate == WiseTele.rateGuide)
                activePins = new List<WisePin>() { guideMotorPin };
            currentRate = rate;

            foreach (WisePin pin in activePins)
                pin.SetOn();

            if (simulated)
            {
                timer_counts = 0;
                prevTick = DateTime.Now;
                simulationTimer.Change(0, 1000 / simulationTimerFrequency);
            }
        }

        public void SetOff()
        {
            WiseTele.Instance.log("SetOff: {0}: Off", name);

            if (simulated)
                simulationTimer.Change(Timeout.Infinite, Timeout.Infinite);

            if ((activePins != null) && (activePins.Count > 0))
            {
                for (int i = activePins.Count - 1; i >= 0; i--)
                {
                    activePins[i].SetOff();
                    activePins.RemoveAt(i);
                }
            }
            currentRate = WiseTele.rateStopped;
        }

        public bool isOn
        {
            get
            {
                return (activePins != null) && activePins.Count != 0;
            }
        }

        public bool isOff
        {
            get
            {
                return !isOn;
            }
        }

        private void bumpEncoders(object StateObject)
        {
            if (!simulated)
                return;

            double increment = ((direction == WiseTele.AxisDirection.Increasing) ? currentRate : -currentRate) / simulationTimerFrequency;

            foreach (IDegrees encoder in encoders)
            {
                Angle before = new Angle(encoder.Degrees), inc = new Angle(increment);
                encoder.Degrees += increment;
                Angle after = new Angle(encoder.Degrees);

//                if (name != "TrackMotor")
//                {
//                    Angle encoderAngle = (name == "EastMotor" || name == "WestMotor") ? new Angle(WiseTele.Instance.RightAscension) : new Angle(WiseTele.Instance.Declination);
//
//                    WiseTele.Instance.log("{0}: {1}: ({2} + {3} = {4}) ({5} + {6} = {7}) {8}: {9} (#{10}, {11} ms)", name, encoder.name,
//                        before.Degrees, increment, after.Degrees,
//                        before, inc, after,
//                        (name == "EastMotor" || name == "WestMotor") ? "ra" : "dec",
//                        encoderAngle, timer_counts++, DateTime.Now.Subtract(prevTick).Milliseconds);
//                }
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
            return name;
        }

        public bool simulated
        {
            get
            {
                return _simulated;
            }
            set
            {
                _simulated = value;
            }
        }
    }
}