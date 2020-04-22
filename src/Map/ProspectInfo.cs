using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace ProspectorInfo.Map
{
    internal class ProspectInfo
    {
        public int X;
        public int Z;
        public string Message;

        public ProspectInfo(int x, int z, string message)
        {
            X = x;
            Z = z;
            Message = message;
        }

        public bool Equals(ProspectInfo other)
        {
            return X == other.X && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is ProspectInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Z;
            }
        }
    }

}