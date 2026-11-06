using EquipmentMonitor.Infrastructure;
using System;

namespace EquipmentMonitor.Infrastructure
{
    public class LeadShineAxisController : IAxisController
    {
        public bool IsInitialize { get; set; }

        public int ControllerCount => _cardCount;

        public int AxisCount => _axisCount;

        private int _cardCount;
        private int _axisCount;

        public LeadShineAxisController()
        {
            if (Initialize())
            {
                //todo
            }
            else
            {
                throw new Exception("板卡初始化失败");
            }
        }

        public bool Initialize()
        {
            _cardCount = LTDMC.dmc_board_init();
            IsInitialize = _cardCount > 0;
            return IsInitialize;
        }

        public void GetAxisCount(int cardNo, ref uint axisCount)
        {
            if (LTDMC.dmc_get_total_axes((ushort)cardNo, ref axisCount) == 0)
            {
                _axisCount = (int)axisCount;
            }
            else
            {
                _axisCount = -1;
            }
        }

        public bool ServeOn(int ControllerNo, int axisNo)
        {
            return false;
        }

        public bool ServeOff(int ControllerNo, int axisNo)
        {
            return false;
        }

        public bool Home(int ControllerNo, int axisNo)
        {
            return LTDMC.dmc_home_move((ushort)ControllerNo, (ushort)axisNo) == 0;
        }

        public bool MoveAbs(int ControllerNo, int axisNo, double position)
        {
            return false;
        }

        public bool MoveRel(int ControllerNo, int axisNo, double distance)
        {
            return false;
        }

        public bool MoveInPos(int ControllerNo, int axisNo)
        {
            return false;
        }

        public bool Jog(int ControllerNo, int axisNo, bool direction)
        {
            return false;
        }

        public bool Stop(int ControllerNo, int axisNo)
        {
            return false;
        }

        public bool ResetAxis(int ControllerNo, int axisNo)
        {
            return false;
        }

        public bool ResetAll()
        {
            return false;
        }

        public double GetPos(int ControllerNo, int axisNo)
        {
            return 0;
        }

        public double GetVel(int ControllerNo, int axisNo)
        {
            return 0;
        }

        public double GetTorque(int ControllerNo, int axisNo)
        {
            return 0;
        }

        /// <summary>
        /// 读取轴状态字。
        /// </summary>
        /// <remarks>
        /// 雷赛的轴状态需要由厂商状态机 / 状态位组合映射到 <see cref="AxisStatusWord"/>，
        /// 目前未实现映射，返回 <c>false</c> 而不是伪造一个全 0 的“正常”状态。
        /// </remarks>
        public bool ReadStateWord(int ControllerNo, int axisNo, string type, out int state)
        {
            state = 0;
            return false;
        }

        public void Dispose()
        {
            LTDMC.dmc_board_close();
        }
    }
}