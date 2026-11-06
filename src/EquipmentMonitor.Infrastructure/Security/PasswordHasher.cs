using System.Security.Cryptography;

namespace EquipmentMonitor.Infrastructure.Security;

/// <summary>
/// 口令哈希：PBKDF2-HMAC-SHA256，自描述格式 <c>pbkdf2.sha256.{iterations}.{salt}.{hash}</c>。
/// </summary>
/// <remarks>
/// <para><b>为什么自描述</b>：迭代次数写在哈希串里，将来提高强度时旧口令仍能验证，
/// 不需要改存储结构、也不需要一次性重算所有账号。</para>
/// <para><b>为什么只有 BCL</b>：<see cref="Rfc2898DeriveBytes"/> 是 .NET 内置实现，
/// 不引入第三方密码库；比较用 <see cref="CryptographicOperations.FixedTimeEquals"/> 防时序攻击。</para>
/// <para>本类型只在用户目录内部使用，不暴露给界面或其他模块。</para>
/// </remarks>
internal sealed class PasswordHasher
{
    /// <summary>默认迭代次数（OWASP 对 PBKDF2-SHA256 的推荐下限量级）。</summary>
    public const int DefaultIterations = 210_000;

    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const string AlgorithmPrefix = "pbkdf2.sha256";

    private readonly int _iterations;

    /// <summary>创建哈希器。</summary>
    public PasswordHasher(int iterations = DefaultIterations)
    {
        if (iterations < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(iterations), "迭代次数必须为正数。");
        }

        _iterations = iterations;
    }

    /// <summary>生成 `pbkdf2.sha256.{iterations}.{salt}.{hash}` 形式的哈希串。</summary>
    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            _iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return $"{AlgorithmPrefix}.{_iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// 校验口令。哈希串损坏或格式未知时返回 <c>false</c>，不抛异常 ——
    /// 被手工改坏的库里不应该让登录流程崩掉。
    /// </summary>
    public bool Verify(string? storedHash, string? password)
    {
        if (string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(password))
        {
            return false;
        }

        var parts = storedHash.Split('.');

        if (parts.Length != 5 ||
            !string.Equals(parts[0], "pbkdf2", StringComparison.Ordinal) ||
            !string.Equals(parts[1], "sha256", StringComparison.Ordinal) ||
            !int.TryParse(parts[2], out var iterations) ||
            iterations < 1)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;

        try
        {
            salt = Convert.FromBase64String(parts[3]);
            expected = Convert.FromBase64String(parts[4]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length == 0 || expected.Length == 0)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
