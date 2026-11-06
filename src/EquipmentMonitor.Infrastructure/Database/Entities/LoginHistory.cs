using SqlSugar;
using System;

namespace EquipmentMonitor.Infrastructure
{
    [SugarTable("LoginHistory")]
    public class LoginHistory
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [SugarColumn(IsNullable = false)]
        public string? UserName { get; set; }

        public DateTime LoginTime { get; set; }

        public DateTime LogoutTime { get; set; }
    }
}