using Common;

namespace EquipmentMonitor.Infrastructure
{
    [InjectableAttribute(DependencyLifetime.Singleton)]
    public class LoginHistoryRepository : RepositoryBase<LoginHistory>
    {
        public LoginHistoryRepository(DbContext dbContext) : base(dbContext)
        {
        }
    }
}