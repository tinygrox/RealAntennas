﻿using System;
using UnityEngine;

namespace RealAntennas
{
    public enum AntennaShape
    {
        Auto, Omni, Dish
    }
    public class RealAntenna : IComparable
    {
        public virtual double Gain { get; set; }         // Physical directionality, measured in dBi
        public virtual double TxPower { get; set; }       // Transmit Power in dBm (milliwatts)
        public virtual int TechLevel { get; set; }
        public virtual double Frequency { get; set; }
        public virtual double PowerEfficiency => Math.Min(1, 0.5 + (TechLevel * 0.05));
        public virtual double AntennaEfficiency => Math.Min(0.7, 0.5 + (TechLevel * 0.025));
        public virtual double SpectralEfficiency => 1.01 - (1 / Math.Pow(2, TechLevel));
        public virtual double DataRate { get; }
        public virtual double NoiseFigure => 2 + ((10 - TechLevel) * 0.8);
        public virtual double Bandwidth => DataRate / SpectralEfficiency;          // RF bandwidth required.
        public virtual double RequiredCI() => 1;
        public virtual double MaxPointingLoss => 200;

        public CommNet.CommNode ParentNode { get; set; }
        protected static readonly string ModTag = "[RealAntenna] ";
        private readonly double minimumSpotRadius = 1e3;
        private readonly double maxOmniGain = 8;

        public virtual AntennaShape Shape => Gain <= maxOmniGain ? AntennaShape.Omni : AntennaShape.Dish;
        public virtual bool CanTarget => Shape != AntennaShape.Omni && (ParentNode == null || !ParentNode.isHome);
        public Vector3 Position => ParentNode.position;
        public Vector3 ToTarget {
            get {
                if (!(CanTarget && Target != null)) return Vector3.zero;
                return (Target is Vessel v) ? v.transform.position - Position : (Vector3)(Target as CelestialBody).position - Position;
            }
        }

        public string TargetID { get; set; }
        private ITargetable _findTargetFromID(string id)
        {
            if (FlightGlobals.fetch && CanTarget)
            {
                if (string.IsNullOrEmpty(id)) return FlightGlobals.GetHomeBody();
                if (FlightGlobals.GetBodyByName(id) is CelestialBody body) return body;
                try
                {
                    if (FlightGlobals.FindVessel(new Guid(id)) is Vessel v) return v;
                }
                catch (FormatException) { }
            }
            return null;
        }
        private void _internalSet(ITargetable tgt, string dispString, string tgtId)
        {
            _target = tgt; TargetID = tgtId;
            if (Parent is ModuleRealAntenna) { Parent.AntennaTargetString = dispString; Parent.TargetID = tgtId; }
        }
        private ITargetable _target = null;
        public ITargetable Target
        {
            get => _target;
            set
            {
                if (!CanTarget) _internalSet(null, string.Empty, string.Empty);
                else if (value is Vessel v) _internalSet(v, v.name, v.id.ToString());
                else if (value is CelestialBody body) _internalSet(body, body.name, body.name);
                else Debug.LogWarningFormat("Tried to set antenna target to {0} and failed", value);
            }
        }

        public double PowerDraw => RATools.LogScale(PowerDrawLinear);
        public virtual double PowerDrawLinear => RATools.LinearScale(TxPower) / PowerEfficiency;
        public double Beamwidth => Math.Sqrt(52525 * AntennaEfficiency / RATools.LinearScale(Gain));
        // Beamwidth is the 3dB full beamwidth contour, ~= the offset angle to the 10dB contour.
        // 10dBi: Beamwidth = 72 = 4dB full beamwidth contour
        // 10dBi @ .6 efficiency: 57 = 3dB full beamwidth contour
        // 20dBi: Beamwidth = 23 = 4dB full beamwidth countour
        // 20dBi @ .6 efficiency: Beamwidth = 17.75 = 3dB full beamwidth contour
        public virtual double MinimumDistance => (Shape == AntennaShape.Omni || Beamwidth >= 90 ? 0 : minimumSpotRadius / Math.Tan(Beamwidth));
        public virtual double PointingLoss(RealAntenna peer)
        {
            double loss = 0;
            if (CanTarget && ToTarget != Vector3.zero)
            {
                float fError = Vector3.Angle(peer.Position - this.Position, ToTarget);
                float angle3dB = Convert.ToSingle(Beamwidth / 2);
                if (fError > Beamwidth) loss = MaxPointingLoss;
                else
                {
                    loss = fError < (Beamwidth / 2) ? Mathf.Lerp(0, 3, fError / angle3dB) :
                                                      Mathf.Lerp(3, 10, (fError - angle3dB) / angle3dB);
                }
            }
//            Debug.LogFormat("PointingLoss from {0} on {1} to Target {2}[{3}] to {4} results {5}",
//                this, this.ParentNode, this.Target, this.ToTarget, peer.ParentNode, loss);
            return loss;
        }

        public string Name { get; set; }
        public ModuleRealAntenna Parent { get; internal set; }
        public override string ToString() => $"[+RA] {Name} [{Gain}dB]{(CanTarget ? $" ->{Target}" : null)}";

        public int CompareTo(object obj)
        {
            if (obj is RealAntenna ra) return DataRate.CompareTo(ra.DataRate);
            else throw new System.ArgumentException();
        }
        public virtual double BestDataRateToPeer(RealAntenna rx, double noiseTemp)
        {
            RealAntenna tx = this;
            Vector3 toSource = rx.Position - tx.Position;
            double distance = toSource.magnitude;
            if ((tx.Parent is ModuleRealAntenna) && !tx.Parent.CanComm()) return 0;
            if ((rx.Parent is ModuleRealAntenna) && !rx.Parent.CanComm()) return 0;
            if ((distance < tx.MinimumDistance) || (distance < rx.MinimumDistance)) return 0;

            double RSSI = RACommNetScenario.RangeModel.RSSI(tx, rx, distance, Frequency);
            double Noise = RACommNetScenario.RangeModel.NoiseFloor(rx, noiseTemp);
            double CI = RSSI - Noise;

            return (CI > RequiredCI()) ? DataRate : 0;
        }
        public RealAntenna() : this("New RealAntennaDigital") { }
        public RealAntenna(string name, double dataRate = 1000)
        {
            Name = name;
            DataRate = dataRate;
        }
        public virtual void LoadFromConfigNode(ConfigNode config)
        {
            Gain = double.Parse(config.GetValue("Gain"));
            TxPower = double.Parse(config.GetValue("TxPower"));
            TechLevel = int.Parse(config.GetValue("TechLevel"));
            Frequency = double.Parse(config.GetValue("Frequency"));
            if (config.HasValue("targetID"))
            {
                TargetID = config.GetValue("targetID");
                if (CanTarget && (_target == null))
                {
                    Target = _findTargetFromID(TargetID);
                }
            }
        }
    }

}