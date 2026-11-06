using EquipmentMonitor.Infrastructure;
using System;

namespace EquipmentMonitor.Infrastructure
{
    public class GoogolTechAxisController : IAxisController
    {
        public bool IsInitialize { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public int ControllerCount => throw new NotImplementedException();

        public int AxisCount => throw new NotImplementedException();

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void GetAxisCount(int cardNo, ref uint axisCount)
        {
            throw new NotImplementedException();
        }

        public double GetPos(int cardNo, int axisNo)
        {
            throw new NotImplementedException();
        }

        public double GetTorque(int cardNo, int axisNo)
        {
            throw new NotImplementedException();
        }

        public double GetVel(int cardNo, int axisNo)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 读取轴状态字。
        /// </summary>
        /// <remarks>
        /// 固高状态字尚未接入厂商库，返回 <c>false</c> 而不是伪造全 0 的“正常”状态。
        /// </remarks>
        public bool ReadStateWord(int cardNo, int axisNo, string type, out int state)
        {
            state = 0;
            return false;
        }

        public bool Home(int cardNo, int axisNo)
        {
            throw new NotImplementedException();
        }

        public bool Initialize()
        {
            throw new NotImplementedException();
        }

        public bool Jog(int cardNo, int axisNo, bool direction)
        {
            throw new NotImplementedException();
        }

        public bool MoveAbs(int cardNo, int axisNo, double position)
        {
            throw new NotImplementedException();
        }

        public bool MoveInPos(int cardNo, int axisNo)
        {
            throw new NotImplementedException();
        }

        public bool MoveRel(int cardNo, int axisNo, double distance)
        {
            throw new NotImplementedException();
        }

        public bool ResetAll()
        {
            throw new NotImplementedException();
        }

        public bool ResetAxis(int cardNo, int axisNo)
        {
            throw new NotImplementedException();
        }

        public bool ServeOff(int cardNo, int axisNo)
        {
            throw new NotImplementedException();
        }

        public bool ServeOn(int cardNo, int axisNo)
        {
            throw new NotImplementedException();
        }

        public bool Stop(int cardNo, int axisNo)
        {
            throw new NotImplementedException();
        }
    }
}