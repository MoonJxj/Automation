using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Core.Abstractions;

/// <summary>
/// 用户目录：账号的查询与管理。实现方负责持久化（本地文件 / 数据库 / 远端服务）。
/// </summary>
/// <remarks>
/// <para><b>权限</b>：所有<b>写</b>操作在实现内部调用
/// <c>RequirePermission(Permission.UserManage)</c>（失败抛 <see cref="UnauthorizedAccessException"/>），
/// 并写审计。<see cref="ListAsync"/> / <see cref="FindAsync"/> 不校验 —— 登录界面要用它们列出账号，
/// 那时还没有会话。</para>
/// <para><b>口令</b>：本接口不暴露口令哈希，也不提供“校验口令”能力。
/// 校验留在实现内部，由认证 provider 使用；避免任何界面/业务代码能接触到哈希。</para>
/// <para><b>失败语义</b>：重复账号、不合法输入、最后一个管理员保护等属于预期内失败，返回
/// <see cref="Result.Fail"/>（错误码见 <see cref="SecurityErrorCodes"/>）；越权与实现自身的编程错误才抛异常。</para>
/// </remarks>
public interface IUserDirectory
{
    /// <summary>列出全部账号（含停用；按角色降序、账号名升序）。</summary>
    Task<Result<IReadOnlyList<UserAccount>>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>按账号名查找（大小写不敏感）；不存在返回失败而不是 <c>null</c>。</summary>
    Task<Result<UserAccount>> FindAsync(string userName, CancellationToken cancellationToken = default);

    /// <summary>新建账号：默认启用、未设置口令、角色由请求指定。</summary>
    Task<Result<UserAccount>> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>修改显示名与角色（账号名不可改）。</summary>
    Task<Result> UpdateAsync(UpdateUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>启用 / 停用账号。不允许停用当前登录账号，也不允许停用最后一个启用中的管理员。</summary>
    Task<Result> SetEnabledAsync(string userName, bool enabled, CancellationToken cancellationToken = default);

    /// <summary>设置/重置口令（管理员操作）。</summary>
    Task<Result> SetPasswordAsync(string userName, string password, CancellationToken cancellationToken = default);

    /// <summary>清除口令，账号回到“工位信任”模式。</summary>
    Task<Result> ClearPasswordAsync(string userName, CancellationToken cancellationToken = default);

    /// <summary>删除账号。不允许删除当前登录账号或最后一个启用中的管理员。</summary>
    Task<Result> DeleteAsync(string userName, CancellationToken cancellationToken = default);
}
