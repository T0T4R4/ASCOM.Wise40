﻿using System;
using System.Threading;
using System.Collections.Generic;
using System.Collections;
using System.Globalization;
using System.Net.Http;

using MccDaq;
using ASCOM.Utilities;
using ASCOM.DeviceInterface;
using ASCOM.Wise40.Common;
using ASCOM.Wise40SafeToOperate;
using ASCOM.Wise40.Hardware;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ASCOM.Wise40
{
    public class WiseDome : WiseObject, IConnectable, IDisposable {

        private Version version = new Version(0, 2);

        private static WiseSafeToOperate wiseSafeToOperate = WiseSafeToOperate.Instance;
        private static bool _initialized = false;

        private WisePin leftPin, rightPin;
        private WisePin[] caliPins = new WisePin[3];
        private WisePin ventPin;
        private WisePin projectorPin;
        private static WiseDomeEncoder domeEncoder = WiseDomeEncoder.Instance;
        private List<IConnectable> connectables;
        private List<IDisposable> disposables;
        private bool _connected = false;
        private bool _calibrating = false;
        public bool _autoCalibrate = false;
        private bool _isStuck;
        private static Object _caliWriteLock = new object();

        public WiseDomeShutter wisedomeshutter = WiseDomeShutter.Instance;
        private static ActivityMonitor activityMonitor = ActivityMonitor.Instance;

        [FlagsAttribute] public enum DomeState {
            Idle = 0,
            MovingCW = (1 << 0),
            MovingCCW = (1 << 1),
            Calibrating = (1 << 2),
            Parking = (1 << 3),
            Stopping = (1 << 4),
        };
        private DomeState _state;

        private enum StuckPhase { NotStuck, FirstStop, GoBackward, SecondStop, ResumeForward };
        private StuckPhase _stuckPhase;

        public enum Direction { None, CW, CCW };

        private uint _prevTicks;      // for Stuck checks
        private DateTime nextStuckEvent;

        public class CalibrationPoint {
            public uint simulatedEncoderValue;
            public Angle simulatedAz;
            public WisePin pin;
            public Angle az;

            public CalibrationPoint(WisePin _pin, Angle _az, uint _simEnc)
            {
                pin = _pin;
                az = _az;
                simulatedEncoderValue = _simEnc;
            }
        };
        private List<CalibrationPoint> calibrationPoints = new List<CalibrationPoint>();
        public const int TicksPerDomeRevolution = 1018;

        public const double DegreesPerTick = 360.0 / TicksPerDomeRevolution;
        public const double ticksPerDegree = TicksPerDomeRevolution / 360;
        private const int simulatedEncoderTicksPerSecond = 6;   // As per Yftach's measurement

        public const double _parkAzimuth = 90.0;
        private Angle _simulatedStuckAz = new Angle(333.0);      // If targeted to this Az, we simulate dome-stuck (must be a valid az)

        private Angle _targetAz = null;

        private System.Threading.Timer _domeTimer;
        private System.Threading.Timer _movementTimer;
        private System.Threading.Timer _stuckTimer;

        private readonly int _movementTimeout = 2000;
        private readonly int _domeTimeout = 50;

        private bool _slaved = false;
        private bool _atPark = false;

        private Debugger debugger = Debugger.Instance;

        private AutoResetEvent internalArrivedAtAzEvent = new AutoResetEvent(false);
        private List<AutoResetEvent> externalArrivedAtAzEvents = new List<AutoResetEvent>();
        private static AutoResetEvent _foundCalibration = new AutoResetEvent(false);
        private static Hardware.Hardware hw = Hardware.Hardware.Instance;

        public static bool _adjustingForTracking = false;

        static WiseDome() { }
        public WiseDome() { }

        private static readonly Lazy<WiseDome> lazy = new Lazy<WiseDome>(() => new WiseDome()); // Singleton

        public static WiseDome Instance
        {
            get
            {
                if (lazy.IsValueCreated)
                    return lazy.Value;

                lazy.Value.init();
                return lazy.Value;
            }
        }

        public void init()
        {
            if (_initialized)
                return;

            WiseName = "Wise40 Dome";
            ReadProfile();

            try {
                uint caliPointsSpacing = domeEncoder.Ticks / 3;

                connectables = new List<IConnectable>();
                disposables = new List<IDisposable>();

                leftPin = new WisePin("DomeLeft", hw.domeboard, DigitalPortType.FirstPortA, 2, DigitalPortDirection.DigitalOut, controlled: true);
                rightPin = new WisePin("DomeRight", hw.domeboard, DigitalPortType.FirstPortA, 3, DigitalPortDirection.DigitalOut, controlled: true);

                caliPins[0] = new WisePin(Const.notsign + "DomeCali0", hw.domeboard, DigitalPortType.FirstPortCL, 0, DigitalPortDirection.DigitalIn);
                caliPins[1] = new WisePin(Const.notsign + "DomeCali1", hw.domeboard, DigitalPortType.FirstPortCL, 1, DigitalPortDirection.DigitalIn);
                caliPins[2] = new WisePin(Const.notsign + "DomeCali2", hw.domeboard, DigitalPortType.FirstPortCL, 2, DigitalPortDirection.DigitalIn);

                calibrationPoints.Add(new CalibrationPoint(caliPins[0], new Angle(254.6, Angle.Type.Az), 10 + 2 * caliPointsSpacing));
                calibrationPoints.Add(new CalibrationPoint(caliPins[1], new Angle(133.0, Angle.Type.Az), 10 + 1 * caliPointsSpacing));
                calibrationPoints.Add(new CalibrationPoint(caliPins[2], new Angle(18.0, Angle.Type.Az), 10 + 0 * caliPointsSpacing));

                ventPin = new WisePin("DomeVent", hw.domeboard, DigitalPortType.FirstPortA, 5, DigitalPortDirection.DigitalOut);
                projectorPin = new WisePin("DomeProjector", hw.domeboard, DigitalPortType.FirstPortA, 4, DigitalPortDirection.DigitalOut);

                List<WisePin> domePins = new List<WisePin> { leftPin, rightPin, ventPin, projectorPin };
                domePins.AddRange(caliPins);
                domePins.AddRange(wisedomeshutter.pins());

                connectables.AddRange(domePins);
                disposables.AddRange(domePins);
            }
            catch (WiseException e)
            {
                debugger.WriteLine(Debugger.DebugLevel.DebugDome, "WiseDome: constructor caught: {0}.", e.Message);
            }

            try
            {
                leftPin.SetOff();
                rightPin.SetOff();
            }
            catch (Hardware.Hardware.MaintenanceModeException) { }

            _calibrating = false;
            _state = DomeState.Idle;

            _domeTimer = new System.Threading.Timer(new TimerCallback(onDomeTimer));
            _domeTimer.Change(_domeTimeout, _domeTimeout);

            _movementTimer = new System.Threading.Timer(new TimerCallback(onMovementTimer));

            _stuckTimer = new System.Threading.Timer(new TimerCallback(onStuckTimer));

            _initialized = true;

            debugger.WriteLine(Debugger.DebugLevel.DebugDome, "WiseDome: init() done.");
        }

        private bool StateIsOn(DomeState flag)
        {
            return (_state & flag) != 0;
        }

        private bool StateIsOff(DomeState flag)
        {
            return !StateIsOn(flag);
        }

        private void SetDomeState(DomeState flags)
        {
            _state |= flags;
        }

        private void UnsetDomeState(DomeState flags)
        {
            _state &= ~flags;
        }

        public void SetArrivedAtAzEvent(AutoResetEvent e)
        {
            //_arrivedAtAzEvent = e;
            //#region debug
            //debugger.WriteLine(Debugger.DebugLevel.DebugDome,
            //    "WiseDome: SetArrivedAtAzEvent(#{0})", _arrivedAtAzEvent.GetHashCode());
            //#endregion
            externalArrivedAtAzEvents.Add(e);
            #region debug
            debugger.WriteLine(Debugger.DebugLevel.DebugDome,
                "WiseDome:SetArrivedAtAzEvent Added: (#{0})", e.GetHashCode());
            #endregion
        }

        public void Connect(bool connected)
        {
            foreach (var connectable in connectables)
            {
                connectable.Connect(connected);
            }
            _connected = connected;

            ActivityMonitor.Instance.Event(new Event.GlobalEvent(
                string.Format("{0} {1}", Const.wiseDomeDriverID, connected ? "Connected" : "Disconnected")));
        }

        public bool Connected
        {
            get
            {
                return _connected;
            }

            set
            {
                if (value == true)
                {
                    RestoreCalibrationData();

                    if (!Calibrated && _autoCalibrate)
                        Task.Run(() => StartFindingHome());
                }

                if (value == _connected)
                    return;

                _connected = value;
            }
        }

        public bool Calibrated
        {
            get
            {
                return domeEncoder.Calibrated;
            }
        }

        /// <summary>
        /// Calculates how many degrees it will take the dome to stop.
        /// Right now it's about 0.7 degrees (two ticks).  In the future it may be derived
        ///  from the Azimuth and maybe from the direction of travel.
        /// </summary>
        /// <param name="az"></param>
        /// <returns></returns>
        private Angle inertiaAngle(Angle az)
        {
            return new Angle(2 * (360.0 / TicksPerDomeRevolution));
        }

        /// <summary>
        /// Checks if we're close enough to a given Azimuth
        /// </summary>
        /// <param name="there"></param>
        /// <returns></returns>
        private bool arriving(Angle there)
        {
            string message = string.Format("WiseDome:arriving: at {0} target {1}: ", Azimuth, there);

            if (!DomeIsMoving)
            {
                #region debug
                debugger.WriteLine(Debugger.DebugLevel.DebugDome, message + "Dome is not moving => false");
                #endregion
                return false;
            }

            Angle az = Azimuth;
            ShortestDistanceResult shortest = Azimuth.ShortestDistance(there);
            Angle inertial = inertiaAngle(there);

            if (StateIsOn(DomeState.MovingCW) && (shortest.direction == Const.AxisDirection.Decreasing))
            {
                #region debug
                debugger.WriteLine(Debugger.DebugLevel.DebugDome, message + "direction changed CW to CCW => true", az, there);
                #endregion
                return true;
            }
            else if (StateIsOn(DomeState.MovingCCW) && (shortest.direction == Const.AxisDirection.Increasing))
            {
                #region debug
                debugger.WriteLine(Debugger.DebugLevel.DebugDome, message + "direction changed CCW to CW => true");
                #endregion
                return true;
            }
            else if (shortest.angle <= inertial)
            {
                #region debug
                debugger.WriteLine(Debugger.DebugLevel.DebugDome, message +
                    string.Format("shortest.Angle {0} <= inertiaAngle({1}) => true", shortest.angle, inertial));
                #endregion
                return true;
            }
            else
            {
                #region debug
                debugger.WriteLine(Debugger.DebugLevel.DebugDome, message + "=> false");
                #endregion
                return false;
            }
        }

        private bool DomeIsMoving
        {
            get
            {
                var ret = StateIsOn(DomeState.MovingCCW) | StateIsOn(DomeState.MovingCW);

                #region debug
                debugger.WriteLine(Debugger.DebugLevel.DebugDome, "DomeIsMoving: {0}", ret);
                #endregion
                return ret;
            }
        }

        /// <summary>
        /// The dome timer is always enabled, at 100 millisec intervals.
        /// </summary>
        /// <param name="state"></param>
        private void onDomeTimer(object state)
        {
            CalibrationPoint cp;

            if ((cp = AtCaliPoint) != null)
            {
                domeEncoder.Calibrate(cp.az);
                if (_calibrating)
                {
                    _calibrating = false;
                    #region debug
                    debugger.WriteLine(Debugger.DebugLevel.DebugDome,
                        "WiseDome: Setting _foundCalibration[{0}] == {1} ...", calibrationPoints.IndexOf(cp), cp.az.ToNiceString());
                    #endregion
                    Stop(string.Format("Arrived at calibration point {0}", cp.az.ToNiceString()));
                    Thread.Sleep(2000);     // settle down
                    _foundCalibration.Set();
                }
            }

            if (_targetAz != null && arriving(_targetAz) && !StateIsOn(DomeState.Stopping))
            {
                SetDomeState(DomeState.Stopping);   // prevent onTimer from re-stopping
                Stop("Reached target");
                UnsetDomeState(DomeState.Stopping);

                _targetAz = null;

                if (StateIsOn(DomeState.Parking))
                {
                    UnsetDomeState(DomeState.Parking);
                    AtPark = true;
                }

                var waiters = new List<AutoResetEvent>() { internalArrivedAtAzEvent };
                waiters.AddRange(externalArrivedAtAzEvents);
                foreach (var e in waiters)
                {
                    #region debug
                    debugger.WriteLine(Debugger.DebugLevel.DebugDome,
                        "WiseDome: Setting arrivedAtAzEvent (#{0})", e.GetHashCode());
                    #endregion
                    e.Set();
                }
            }
        }

        private bool ShutterIsMoving
        {
            get
            {
                return wisedomeshutter.IsMoving;
            }
        }

        /// <summary>
        /// The movement timer is activated when the dome starts moving either CW or CCW, and 
        ///   gets disabled by Stop().
        ///   
        /// The handler decides whether the dome encoder has changed, or the dome seems to be stuck.
        /// </summary>
        /// <param name="state"></param>
        private void onMovementTimer(object state)
        {
            uint currTicks, deltaTicks;
            const int leastExpectedTicks = 2;  // least number of Ticks expected to change in two seconds

            SaveCalibrationData();

            // the movementTimer should not be Enabled unless the dome is moving
            if (_isStuck || !DomeIsMoving)
                return;

            deltaTicks = 0;
            currTicks = domeEncoder.Value;

            if (currTicks == _prevTicks)
                _isStuck = true;
            else
            {
                if (StateIsOn(DomeState.MovingCW))
                {
                    if (_prevTicks > currTicks)
                        deltaTicks = _prevTicks - currTicks;
                    else
                        deltaTicks = domeEncoder.Ticks - currTicks + _prevTicks;

                    if (deltaTicks < leastExpectedTicks)
                        _isStuck = true;
                }
                else if (StateIsOn(DomeState.MovingCCW))
                {
                    if (_prevTicks > currTicks)
                        deltaTicks = _prevTicks - currTicks;
                    else
                        deltaTicks = domeEncoder.Ticks - _prevTicks + currTicks;

                    if (deltaTicks < leastExpectedTicks)
                        _isStuck = true;
                }
            }

            //if (_isStuck)
            //{
            //    _stuckPhase = StuckPhase.NotStuck;
            //    nextStuckEvent = DateTime.Now;
            //    _stuckTimer.Change(0, 1000);
            //}

            _prevTicks = currTicks;
        }


        private void onStuckTimer(object state)
        {
            DateTime rightNow;
            WisePin backwardPin, forwardPin;

            rightNow = DateTime.Now;

            if (DateTime.Compare(rightNow, nextStuckEvent) < 0)
                return;

            forwardPin = StateIsOn(DomeState.MovingCCW) ? leftPin : rightPin;
            backwardPin = StateIsOn(DomeState.MovingCCW) ? rightPin : leftPin;

            switch (_stuckPhase) {
                case StuckPhase.NotStuck:              // Stop, let the wheels cool down
                    forwardPin.SetOff();
                    backwardPin.SetOff();
                    _stuckPhase = StuckPhase.FirstStop;
                    nextStuckEvent = rightNow.AddMilliseconds(10000);
                    #region debug
                    debugger.WriteLine(Debugger.DebugLevel.DebugDome, "WiseDome: stuck: {0}, phase1: stopped moving, letting wheels cool for 10 seconds", Azimuth);
                    #endregion

                    break;

                case StuckPhase.FirstStop:             // Go backward for two seconds
                    backwardPin.SetOn();
                    _stuckPhase = StuckPhase.GoBackward;
                    nextStuckEvent = rightNow.AddMilliseconds(2000);
                    #region debug
                    debugger.WriteLine(Debugger.DebugLevel.DebugDome, "WiseDome: stuck: {0}, phase2: going backwards for 2 seconds", Azimuth);
                    #endregion
                    break;

                case StuckPhase.GoBackward:            // Stop again for two seconds
                    backwardPin.SetOff();
                    _stuckPhase = StuckPhase.SecondStop;
                    nextStuckEvent = rightNow.AddMilliseconds(2000);
                    #region debug
                    debugger.WriteLine(Debugger.DebugLevel.DebugDome, "WiseDome: stuck: {0}, phase3: stopping for 2 seconds", Azimuth);
                    #endregion
                    break;

                case StuckPhase.SecondStop:            // Done, resume original movement
                    forwardPin.SetOn();
                    _stuckPhase = StuckPhase.NotStuck;
                    _isStuck = false;
                    _stuckTimer.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
                    nextStuckEvent = rightNow.AddYears(100);
                    #region debug
                    debugger.WriteLine(Debugger.DebugLevel.DebugDome, "WiseDome: stuck: {0}, phase4: resumed original motion", Azimuth);
                    #endregion
                    break;
            }
        }


        public void StartMovingCW()
        {
            AtPark = false;

            try
            {
                leftPin.SetOff();
                rightPin.SetOn();
            } catch (Hardware.Hardware.MaintenanceModeException)
            {
                return;
            }

            SetDomeState(DomeState.MovingCW);
            domeEncoder.setMovement(Direction.CW);
            _movementTimer.Change(0, _movementTimeout);
            #region debug
            debugger.WriteLine(Debugger.DebugLevel.DebugDome, "WiseDome: Started moving CW");
            #endregion
        }

        public void StartMovingCCW()
        {
            AtPark = false;
            try
            {
                rightPin.SetOff();
                leftPin.SetOn();
            } catch (Hardware.Hardware.MaintenanceModeException)
            {
                return;
            }

            SetDomeState(DomeState.MovingCCW);
            domeEncoder.setMovement(Direction.CCW);
            _movementTimer.Change(0, _movementTimeout);
            #region debug
            debugger.WriteLine(Debugger.DebugLevel.DebugDome, "WiseDome: Started moving CCW");
            #endregion
        }

        public void Stop(string reason)
        {
            int tries;

            #region debug
            string dbg = string.Format("WiseDome:Stop({0}) Starting to stop (encoder: {1}) ", reason, domeEncoder.Value);
            if (Calibrated)
                dbg += string.Format(", az: {0}", Azimuth);
            else
                dbg += ", not calibrated";
            debugger.WriteLine(Debugger.DebugLevel.DebugDome, dbg);
            #endregion
            _movementTimer.Change(Timeout.Infinite, Timeout.Infinite);
            rightPin.SetOff();
            leftPin.SetOff();
            UnsetDomeState(DomeState.MovingCCW | DomeState.MovingCW);
            domeEncoder.setMovement(Direction.None);

            for (tries = 0; tries < 10; tries++)
            {
                uint prev = domeEncoder.Value;
                Thread.Sleep(500);
                uint curr = domeEncoder.Value;
                if (prev == curr)
                    break;
            }

            if (Calibrated)
                SaveCalibrationData();
            #region debug
            if (Calibrated)
                debugger.WriteLine(Debugger.DebugLevel.DebugDome, "WiseDome:Stop({0}) Fully stopped at az: {1} (encoder: {2}) after {3} tries",
                    reason, Azimuth, domeEncoder.Value, tries + 1);
            else
                debugger.WriteLine(Debugger.DebugLevel.DebugDome, "WiseDome:Stop({0}) Fully stopped (not calibrated) (encoder: {1}) after {2} tries",
                    reason, domeEncoder.Value, tries + 1);
            #endregion

            if (_adjustingForTracking)
                _adjustingForTracking = false;
            else
            {
                activityMonitor.EndActivity(ActivityMonitor.ActivityType.DomeSlew, new Activity.DomeSlewActivity.EndParams()
                    {
                        endState = Activity.State.Succeeded,
                        endReason = reason,
                        endAz = Azimuth.Degrees,
                    });
            }
        }

        public CalibrationPoint AtCaliPoint
        {
            get
            {
                foreach (var cp in calibrationPoints)
                {
                    if (Simulated)
                    {
                        if (domeEncoder.Value == cp.simulatedEncoderValue)
                            return cp;
                    }
                    else
                    {
                        if (cp.pin.isOff)
                            return cp;
                    }
                }
                return null;
            }
        }

        public Angle Azimuth
        {
            get
            {
                Angle ret;

                if (!Calibrated)
                {
                    if (_autoCalibrate)
                        StartFindingHome();
                    else
                        return Angle.FromDegrees(double.NaN, Angle.Type.Az);
                }

                ret = domeEncoder.Azimuth;
                #region debug
                debugger.WriteLine(Debugger.DebugLevel.DebugDome, "WiseDome: Azimuth: get => {0}", ret.ToNiceString());
                #endregion
                return ret;
            }

            set
            {
                #region debug
                debugger.WriteLine(Debugger.DebugLevel.DebugDome, "WiseDome: Azimuth: set({0})", value);
                #endregion
                domeEncoder.Calibrate(value);
                SaveCalibrationData();
            }
        }

        public bool Vent
        {
            get
            {
                return ventPin.isOn;
            }

            set
            {
                if (value)
                    ventPin.SetOn();
                else
                    ventPin.SetOff();
            }
        }

        public bool Projector
        {
            get
            {
                return projectorPin.isOn;
            }

            set
            {
                if (value == projectorPin.isOn)
                    return;

                if (value)
                {
                    projectorPin.SetOn();
                    activityMonitor.NewActivity(new Activity.ProjectorActivity());
                }
                else
                {
                    projectorPin.SetOff();
                    activityMonitor.EndActivity(ActivityMonitor.ActivityType.Projector, new Activity.EndParams()
                    {
                        endReason = "projector was turned OFF",
                        endState = Activity.State.Succeeded,
                    });
                }
            }
        }

        public void StartFindingHome()
        {
            if (Calibrated)
            {
                //
                // Consider safety only AFTER calibration, otherwise we ca never produce
                //  an Azimuth reading
                //
                if (ShutterIsMoving)
                {
                    throw new ASCOM.InvalidOperationException("Cannot move, shutter is active!");
                }

                if (!wiseSafeToOperate.IsSafe && !activityMonitor.InProgress(ActivityMonitor.ActivityType.ShuttingDown))
                    throw new ASCOM.InvalidOperationException(wiseSafeToOperate.Action("unsafereasons", ""));
            }

            AtPark = false;

            #region debug
            debugger.WriteLine(Debugger.DebugLevel.DebugDome, "WiseDome:StartFindingHome: started");
            #endregion
            _calibrating = true;

            activityMonitor.NewActivity(new Activity.DomeSlewActivity(new Activity.DomeSlewActivity.StartParams()
            {
                type = Activity.DomeSlewActivity.DomeEventType.FindHome,
            }));

            if (Calibrated)
            {
                List<ShortestDistanceResult> distanceToCaliPoints = new List<ShortestDistanceResult>();
                foreach (var cp in calibrationPoints)
                    distanceToCaliPoints.Add(Azimuth.ShortestDistance(cp.az));

                ShortestDistanceResult closest = new ShortestDistanceResult(new Angle(360.0, Angle.Type.Az), Const.AxisDirection.None);
                foreach (var res in distanceToCaliPoints)
                    if (res.angle < closest.angle)
                        closest = res;

                switch (closest.direction)
                {
                    case Const.AxisDirection.Decreasing: StartMovingCCW(); break;
                    case Const.AxisDirection.Increasing: StartMovingCW(); break;
                }
            }
            else
            {
                SetDomeState(DomeState.Calibrating);
                StartMovingCCW();
            }

            #region debug
            debugger.WriteLine(Debugger.DebugLevel.DebugDome, "WiseDome:StartFindingHome: waiting for _foundCalibration ...");
            #endregion
            _foundCalibration.WaitOne();
            UnsetDomeState(DomeState.Calibrating);
            activityMonitor.EndActivity(ActivityMonitor.ActivityType.DomeSlew, new Activity.DomeSlewActivity.EndParams()
            {
                endState = Activity.State.Succeeded,
                endReason = string.Format("Found calibration point at {0}", Azimuth.ToNiceString()),
                endAz = Azimuth.Degrees,
            });
            #region debug
            debugger.WriteLine(Debugger.DebugLevel.DebugDome, "WiseDome: FindHomePoint: _foundCalibration was Set()");
            #endregion
        }

        public void SlewToAzimuth(double degrees, string reason)
        {
            if (Slaved)
                throw new InvalidOperationException("Cannot SlewToAzimuth, dome is Slaved");

            if (degrees < 0 || degrees >= 360)
                throw new InvalidValueException(string.Format("Invalid azimuth: {0}, must be >= 0 and < 360", degrees));

            if (ShutterIsMoving)
                throw new ASCOM.InvalidOperationException("Cannot move, shutter is active!");

            if ((!activityMonitor.InProgress(ActivityMonitor.ActivityType.ShuttingDown) && !StateIsOn(DomeState.Parking)) && !wiseSafeToOperate.IsSafe)
                throw new ASCOM.InvalidOperationException("Unsafe: " + wiseSafeToOperate.Action("unsafereasons", ""));

            Angle toAng = new Angle(degrees, Angle.Type.Az);

            if (!_adjustingForTracking)
                activityMonitor.NewActivity(new Activity.DomeSlewActivity(new Activity.DomeSlewActivity.StartParams
                {
                    type = Activity.DomeSlewActivity.DomeEventType.Slew,
                    startAz = Azimuth.Degrees,
                    targetAz = degrees,
                    reason = reason,
                }));

            if (!Calibrated)
            {
                if (_autoCalibrate)
                {
                    #region debug
                    debugger.WriteLine(Debugger.DebugLevel.DebugDome, "WiseDome: SlewToAzimuth: {0}, not calibrated, _autoCalibrate == true, calling FindHomePoint", toAng);
                    #endregion
                    StartFindingHome();
                } else
                {
                    #region debug
                    debugger.WriteLine(Debugger.DebugLevel.DebugDome, "WiseDome: SlewToAzimuth: {0}, not calibrated, _autoCalibrate == false, throwing InvalidOperationException", toAng.ToNiceString());
                    #endregion
                    throw new ASCOM.InvalidOperationException("Not calibrated!");
                }
            }

            _targetAz = toAng;
            AtPark = false;

            ShortestDistanceResult shortest = domeEncoder.Azimuth.ShortestDistance(_targetAz);
            switch (shortest.direction)
            {
                case Const.AxisDirection.Decreasing:
                    StartMovingCCW();
                    break;
                case Const.AxisDirection.Increasing:
                    StartMovingCW();
                    break;
            }
            #region debug
            debugger.WriteLine(Debugger.DebugLevel.DebugDome, "WiseDome: SlewToAzimuth: {0} => {1} (dist: {2}), moving {3}",
                Azimuth, toAng, shortest.angle, shortest.direction);
            #endregion

            if (Simulated && _targetAz == _simulatedStuckAz)
            {
                Angle epsilon = new Angle(5.0);
                Angle stuckAtAz = (shortest.direction == Const.AxisDirection.Increasing) ? _targetAz - epsilon : _targetAz + epsilon;

                domeEncoder.SimulateStuckAt(stuckAtAz);
                #region debug
                debugger.WriteLine(Debugger.DebugLevel.DebugEncoders, "WiseDome: Dome encoder will simulate stuck at {0}", stuckAtAz);
                #endregion
            }
        }

        public void Unpark()
        {
            AtPark = false;
        }

        public bool AtPark
        {
            get
            {
                return _atPark;
            }

            set
            {
                _atPark = value;
            }
        }

        public string Digest
        {
            get
            {
                return JsonConvert.SerializeObject(new DomeDigest()
                {
                    Azimuth = Azimuth.Degrees,
                    TargetAzimuth = _targetAz == null ? Const.noTarget : _targetAz.Degrees,
                    Calibrated = Calibrated,
                    Status = Status,
                    Vent = Vent,
                    Projector = Projector,
                    AtPark = AtPark,
                    Slewing = Slewing,
                    DirectionMotorsAreActive = DirectionMotorsAreActive,
                    Shutter = new ShutterDigest()
                    {
                        Status = ShutterStatusString,
                        State = ShutterState,
                        RangeCm = wisedomeshutter.RangeCm,
                        PercentOpen = wisedomeshutter.PercentOpen,
                        TimeSinceLastReading = wisedomeshutter.webClient.TimeSinceLastReading,
                        WiFiIsWorking = wisedomeshutter.webClient.WiFiIsWorking,
                    }
                });
            }
        }

        public string Status
        {
            get
            {
                string ret = string.Empty;

                if (!DomeIsMoving)
                    return ret;

                if (StateIsOn(DomeState.MovingCW))
                    ret = "Moving CW";
                else if (StateIsOn(DomeState.MovingCCW))
                    ret = "Moving CCW";

                if (_targetAz != null)
                    ret += string.Format(" to {0}", _targetAz.ToNiceString());

                if (StateIsOn(DomeState.Calibrating))
                    ret += " (calibrating)";
                if (StateIsOn(DomeState.Parking))
                    ret += " (parking)";

                return ret;
            }
        }

        public void Dispose()
        {
            wisedomeshutter.Dispose();
            leftPin.SetOff();
            rightPin.SetOff();

            Vent = false;
        }

        public bool Slewing
        {
            get
            {
                if (Slaved)
                    throw new InvalidOperationException("Cannot get Slewing while dome is Slaved");

                return DomeIsMoving || ShutterIsMoving;
            }
        }

        public bool Slaved
        {
            get
            {
                return _slaved;
            }

            set
            {
                _slaved = value;
            }
        }

        public void Park()
        {
            if (Slaved)
                throw new InvalidOperationException("Cannot Park, dome is Slaved");

            if (ShutterIsMoving)
            {
                throw new ASCOM.InvalidOperationException("Cannot Park, shutter is active!");
            }

            if (!Calibrated && !_autoCalibrate)
            {
                throw new ASCOM.InvalidOperationException("Cannot Park, not calibrated!");
            }

            SetDomeState(DomeState.Parking);

            AtPark = false;
            SlewToAzimuth(_parkAzimuth, "Park");
        }

        public void OpenShutter(bool bypassSafety = false)
        {
            if (DirectionMotorsAreActive)
                throw new InvalidOperationException("Cannot open shutter while dome is slewing!");

            if (!bypassSafety && (!wiseSafeToOperate.IsSafe && !activityMonitor.InProgress(ActivityMonitor.ActivityType.ShuttingDown)))
                throw new InvalidOperationException(wiseSafeToOperate.CommandString("unsafeReasons", false));

            if (activityMonitor.InProgress(ActivityMonitor.ActivityType.ShuttingDown))
                throw new InvalidOperationException("Observatory is shutting down!");

            int percentOpen = wisedomeshutter.PercentOpen;
            if (percentOpen != -1 && percentOpen > 98)
                return;

            if (wisedomeshutter.IsMoving && !(wisedomeshutter.State == ShutterState.shutterOpening))
                wisedomeshutter.Stop("Stopped before StartOpening");
            wisedomeshutter.StartOpening();
            if (wisedomeshutter._syncVentWithShutter)
                Vent = true;
        }

        public void CloseShutter()
        {
            if (DirectionMotorsAreActive)
                throw new InvalidOperationException("Cannot close shutter while dome is slewing!");

            int percentOpen = wisedomeshutter.PercentOpen;
            if (percentOpen != -1 && percentOpen < 1)
                return;

            if (wisedomeshutter.IsMoving && !(wisedomeshutter.State == ShutterState.shutterClosing))
                wisedomeshutter.Stop("Stopped before StartClosing");
            wisedomeshutter.StartClosing();
            if (wisedomeshutter._syncVentWithShutter)
                Vent = false;
        }

        public void AbortSlew()
        {
            Stop("Slew Aborted");
        }

        public double Altitude
        {
            get
            {
                throw new ASCOM.PropertyNotImplementedException("Altitude", false);
            }
        }

        public bool AtHome
        {
            get
            {
                return AtCaliPoint == calibrationPoints[0];
            }
        }

        public bool CanFindHome
        {
            get
            {
                return true;
            }
        }

        public bool CanPark
        {
            get
            {
                return true;
            }
        }

        public bool CanSetAltitude
        {
            get
            {
                return false;
            }
        }

        public bool CanSetAzimuth
        {
            get
            {
                return true;
            }
        }

        public bool CanSetPark
        {
            get
            {
                return false;
            }
        }

        public bool CanSetShutter
        {
            get
            {
                return true;
            }
        }

        public bool CanSlave
        {
            get
            {
                return true;
            }
        }

        public bool CanSyncAzimuth
        {
            get
            {
                return true;
            }
        }

        public void SyncToAzimuth(double degrees)
        {
            Angle ang = new Angle(degrees, Angle.Type.Az);

            if (degrees < 0.0 || degrees >= 360.0)
                throw new InvalidValueException(string.Format("Cannot SyncToAzimuth({0}), must be >= 0 and < 360", ang));
            Azimuth = ang;
        }

        public void SlewToAltitude(double Altitude)
        {
            throw new ASCOM.MethodNotImplementedException("SlewToAltitude");
        }

        public void SetPark()
        {
            throw new ASCOM.MethodNotImplementedException("SetPark");
        }

        public ShutterState ShutterState
        {
            get
            {
                return wisedomeshutter.State;
            }
        }

        public string ShutterStatusString
        {
            get
            {
                string ret = ShutterState.ToString().ToLower().Remove(0, "shutter".Length);

                if (wisedomeshutter.webClient.WiFiIsWorking)
                {
                    int percent = wisedomeshutter.PercentOpen;

                    if (percent != -1)
                        ret += string.Format(" ({0}% open)", percent);
                } else
                    ret += " (error:No WiFi connection!)";

                return ret;
            }
        }


        private static ArrayList supportedActions = new ArrayList() {
            "projector",
            "vent",
            "digest",
        };

        public ArrayList SupportedActions
        {
            get
            {
                return supportedActions;
            }
        }

        public string Action(string actionName, string actionParameters)
        {
            string param = actionParameters.ToLower();
            string ret = "ok";

            switch (actionName)
            {
                case "projector":
                    if (param != string.Empty)
                        Projector = Convert.ToBoolean(param);

                    return JsonConvert.SerializeObject(Projector);

                case "vent":
                    if (param != string.Empty)
                        Vent = Convert.ToBoolean(param);

                    return JsonConvert.SerializeObject(Vent);

                case "status":
                    return Digest;

                case "halt":
                case "stop":
                    Stop(string.Format("Action: \"{0}\"", actionName));
                    return "ok";

                case "unpark":
                    Unpark();
                    return "ok";

                case "set-zimuth":
                    Azimuth = Angle.FromDegrees(Convert.ToDouble(actionParameters));
                    return "ok";

                case "start-moving":
                    if (!wiseSafeToOperate.IsSafe && !activityMonitor.InProgress(ActivityMonitor.ActivityType.ShuttingDown))
                        throw new ASCOM.InvalidOperationException(wiseSafeToOperate.Action("unsafereasons", ""));

                    switch (param)
                    {
                        case "cw":
                            StartMovingCW();
                            break;
                        case "ccw":
                            StartMovingCCW();
                            break;
                        default:
                            ret = string.Format("Bad parameter \"{0}\" for \"start-moving\".  Can be either \"cw\" or \"ccw\"", param);
                            break;
                    }
                    return ret;

                case "shutter":
                    switch(param)
                    {
                        case "halt":
                            wisedomeshutter.Stop(string.Format("Action \"{0}\".", actionName));
                            break;
                    }
                    return ret;

                case "sync-vent-with-shutter":
                    switch (param)
                    {
                        case "":
                            ret = SyncVentWithShutter.ToString();
                            break;

                        default:
                            bool onOff;

                            if (bool.TryParse(param, out onOff))
                            {
                                SyncVentWithShutter = onOff;
                            }
                            else
                                ret = string.Format("Bad parameter \"{0}\" to \"sync-vent-with-shutter\"", param);
                            break;
                    }
                    return ret;

                case "auto-calibrate":
                    switch (param)
                    {
                        case "":
                            ret = _autoCalibrate.ToString();
                            break;

                        default:
                            bool calibrate;

                            if (bool.TryParse(param, out calibrate))
                            {
                                _autoCalibrate = calibrate;
                            }
                            else
                                ret = string.Format("Bad parameter \"{0}\" to \"auto-calibrate\"", param);
                            break;
                    }
                    return ret;

                case "calibrate":
                    if (! Calibrated)
                        StartFindingHome();
                    return ret;

                default:
                    throw new ASCOM.ActionNotImplementedException(
                        "Action " + actionName + " is not implemented by this driver");
            }
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

        private void CheckConnected(string message)
        {
            if (!Connected)
            {
                throw new ASCOM.NotConnectedException(message);
            }
        }


        public string Description
        {
            get
            {
                return "Wise40 Dome";
            }
        }

        public string DriverInfo
        {
            get
            {
                return "First draft, Version: " + DriverVersion;
            }
        }

        public string DriverVersion
        {
            get
            {
                return String.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor);
            }
        }

        public short InterfaceVersion
        {
            // set by the driver wizard
            get
            {
                return Convert.ToInt16("2");
            }
        }

        #region SaveRestoreCalibrationData

        private readonly string calibrationDataFilePath = Const.topWise40Directory + "Dome/CalibrationData.txt";

        private void SaveCalibrationData()
        {
            if (!Calibrated)
                return;

            List<string> lines = new List<string>();
            DateTime now = DateTime.Now;

            lines.Add("#");
            lines.Add(string.Format("# WiseDome calibration data, generated automatically, please don't change!"));
            lines.Add("#");
            lines.Add(string.Format("# Saved: {0}", now.ToLocalTime()));
            lines.Add("#");
            lines.Add(string.Format("Encoder: {0}", domeEncoder.Value));
            lines.Add(string.Format("Azimuth: {0}", Azimuth.Degrees.ToString()));

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(calibrationDataFilePath));

            lock (_caliWriteLock)
            {
                System.IO.File.WriteAllLines(calibrationDataFilePath, lines);
            }
        }

        private void RestoreCalibrationData()
        {
            uint savedEncoderValue = uint.MaxValue;
            double savedAzimuth = double.NaN;
            
            if (!System.IO.File.Exists(calibrationDataFilePath))
                return;

            bool valid = true;
            foreach (string line in System.IO.File.ReadLines(calibrationDataFilePath))
            {
                if (line.StartsWith("Encoder: "))
                {
                    if (!UInt32.TryParse(line.Substring("Encoder: ".Length), out savedEncoderValue))
                        valid = false;
                }
                else if (line.StartsWith("Azimuth: "))
                {
                    string val = line.Substring("Azimuth: ".Length);
                    if (val == "NaN" || !Double.TryParse(val, out savedAzimuth))
                        valid = false;
                }
            }

            if (valid) {
                if (Simulated)
                {
                    domeEncoder.Value = savedEncoderValue;
                    domeEncoder.Calibrate(Angle.FromDegrees(savedAzimuth, Angle.Type.Az));
                }
                else if (savedEncoderValue == domeEncoder.Value)
                {
                    domeEncoder.Calibrate(Angle.FromDegrees(savedAzimuth, Angle.Type.Az));
                }
                #region debug
                debugger.WriteLine(Debugger.DebugLevel.DebugDome, "Restored calibration data from \"{0}\", Azimuth: {1}",
                    calibrationDataFilePath, savedAzimuth);
                #endregion
            }
        }
        #endregion

        public double ParkAzimuth
        {
            get
            {
                return _parkAzimuth;
            }
        }

        public bool DirectionMotorsAreActive
        {
            get
            {
                return leftPin.isOn || rightPin.isOn;
            }
        }

        public bool SyncVentWithShutter
        {
            get
            {
                return wisedomeshutter._syncVentWithShutter;
            }

            set
            {
                wisedomeshutter._syncVentWithShutter = value;
            }
        }

        #region Profile

        /// <summary>
        /// Read the device configuration from the ASCOM Profile store
        /// </summary>
        public void ReadProfile()
        {
            bool defaultSyncVentWithShutter = (WiseSite.OperationalMode == WiseSite.OpMode.WISE) ? false : true;

            using (Profile driverProfile = new Profile() { DeviceType = "Dome" })
            {
                _autoCalibrate = Convert.ToBoolean(driverProfile.GetValue(Const.wiseDomeDriverID, Const.ProfileName.Dome_AutoCalibrate, string.Empty, true.ToString()));
            }
            wisedomeshutter.ReadProfile();
        }

        /// <summary>
        /// Write the device configuration to the  ASCOM  Profile store
        /// </summary>
        public void WriteProfile()
        {
            using (Profile driverProfile = new Profile() { DeviceType = "Dome" })
            {
                driverProfile.WriteValue(Const.wiseDomeDriverID, Const.ProfileName.Dome_AutoCalibrate, _autoCalibrate.ToString());
            }
            wisedomeshutter.WriteProfile();
        }
        #endregion
    }

    public class ConnectionDigest
    {
        public enum ConnectionState { Working, Dead };

        private ConnectionState _state;
        public string Reason;

        public bool Working
        {
            get
            {
                return _state == ConnectionState.Working;
            }

            set
            {
                _state = value == true ? ConnectionState.Working : ConnectionState.Dead;
            }
        }
    }

    public class ShutterDigest
    {

        public ShutterState State;
        public string Status;
        public int PercentOpen;
        public int RangeCm;
        public TimeSpan TimeSinceLastReading;
        public bool WiFiIsWorking;
        //public ConnectionDigest Connection;
    }

    public class DomeDigest
    {
        public double Azimuth;
        public double TargetAzimuth;
        public bool Calibrated;
        public string Status;
        public bool Vent;
        public bool Projector;
        public bool AtPark;
        public bool Slewing;
        public bool DirectionMotorsAreActive;
        public ShutterDigest Shutter;
    }
}
