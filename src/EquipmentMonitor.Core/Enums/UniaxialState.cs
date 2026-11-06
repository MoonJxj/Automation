using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EquipmentMonitor.Core
{
    public enum UniaxialState
    {
        /// <summary>
        /// 未知。尚未采集到有效状态。
        /// </summary>
        Unknown = -1,

        /// <summary>
        /// 同步运动
        /// </summary>
        Synchronized = 0,

        /// <summary>
        /// 连续运动
        /// </summary>
        Continuous = 1,

        /// <summary>
        /// 离散运动
        /// </summary>
        Discrete = 2,

        /// <summary>
        /// 减速停止
        /// </summary>
        Stopping = 3,

        /// <summary>
        /// 故障停止
        /// </summary> 
        Errorstop = 4,

        /// <summary>
        /// 回原点
        /// </summary>
        Homing = 5,

        /// <summary>
        /// 保持静止
        /// </summary>
        Standstill = 6,

        /// <summary>
        /// 关闭
        /// </summary>
        Disabled = 7,
    }
}
