﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace ASCOM.Wise40.Common
{
    public class Angle
    {
        internal static Astrometry.AstroUtils.AstroUtils astroutils = new Astrometry.AstroUtils.AstroUtils();
        internal static ASCOM.Utilities.Util ascomutils = new ASCOM.Utilities.Util();
        public enum Format { Deg, RA, Dec, HA, Alt, Az, Double, Rad, HAhms, RAhms };

        private double _degrees;

        private int Sign
        {
            get
            {
                return _degrees < 0 ? -1 : 1;
            }
        }

        public int D
        {
            get
            {
                return (int) Math.Floor(_degrees);
            }
        }

        public int M
        {
            get
            {
                return (int) Math.Floor((_degrees - D) * 60);
            }
        }

        public double S
        {
            get
            {
                return (((_degrees - D) * 60) - M) * 60;
            }
        }

        public int H
        {
            get
            {
                return D / 15;
            }
        }

        public static Angle FromRad(double rad)
        {
            return new Angle(rad * 180.0 / Math.PI);
        }

        public static Angle FromDeg(double deg)
        {
            return new Angle(deg);
        }

        public double Degrees
        {
            get
            {
                return _degrees;
            }

            set
            {
                _degrees = value;
            }
        }

        public double Radians
        {
            get
            {
                return _degrees * Math.PI / 180.0;
            }

            set
            {
                _degrees = value * 180.0 * Math.PI;
            }
        }

        public double ToRA
        {
            get
            {
                return astroutils.ConditionRA(_degrees);
            }
        }

        public double ToHA
        {
            get
            {
                return astroutils.ConditionHA(_degrees);
            }
        }

        public Angle(double deg)
        {
            _degrees = deg;
        }

        public Angle(int d, int m, double s, int sign = 1)
        {
            _degrees = sign * ascomutils.DMSToDegrees(string.Format("{0}:{1}:{2}", d, m, s));
        }

        public Angle(string s)
        {
            _degrees = ascomutils.DMSToDegrees(s);
        }

        public static bool TryParse(string coordinates, out Angle value)
        {
            value = new Angle(-1000);
            double val;

            var c = coordinates.Split(new[] { ' ', '°', '\'', ':' });

            try
            {
                switch (c.Length)
                {
                    case 1:
                        value = new Angle(int.Parse(c[0]), 0, 0);
                        return true;
                    case 2:
                        value = new Angle(int.Parse(c[0]), int.Parse(c[1]), 0);
                        return true;
                    case 3:
                        value = new Angle(int.Parse(c[0]), int.Parse(c[1]), double.Parse(c[2]));
                        Console.WriteLine("TryParse: {0}, {1}, {2} => {3}", int.Parse(c[0]), int.Parse(c[1]), double.Parse(c[2]), value.ToString(Format.Deg));
                        return true;
                }
            }
            catch
            {
                return false;
            }

            if (double.TryParse(coordinates, out val))
            {
                value.Degrees = val;
                return true;
            }
            return false;
        }

        public string ToString(Format format = Format.Deg)
        {
            switch (format)
            {
                case Format.Deg:
                    return ascomutils.DegreesToDMS(_degrees, "°", "'", "\"", 1);
                case Format.RA:
                    return ascomutils.DegreesToHMS(_degrees, "h", "m", "s", 1);
                case Format.Dec:
                    return ascomutils.DegreesToDMS(_degrees, "°", "'", "\"", 1);
                case Format.HA:
                    return ascomutils.DegreesToHMS(_degrees, "h", "m", "s", 1);
                case Format.Az:
                    return ascomutils.DegreesToDMS(_degrees, "°", "'", "\"", 1);
                case Format.Alt:
                    return ascomutils.DegreesToDMS(_degrees, "°", "'", "\"", 1);
                case Format.Double:
                    return string.Format("{0:0.000000}", Degrees);
                case Format.Rad:
                    return string.Format("{0:0.000000}", Radians);
                case Format.RAhms:
                    return ascomutils.DegreesToHMS(_degrees, "h", "m", "s", 1);
                case Format.HAhms:
                    return ascomutils.DegreesToHMS(_degrees, "h", "m", "s", 1);
            }
            return "";
        }
    }
}
