using Microsoft.Extensions.Configuration;
using System.Collections;
using System.Reflection;
using System.Text;

namespace Common
{
    public class IniConfigSerializer : IConfigSerializer
    {
        // 用 IConfigurationRoot 替换原来的 IniData 字段
        private IConfigurationRoot? _configuration;

        public string Serialize<T>(T config)
        {
            // 如果传入 null，直接返回空字符串（或者抛出 ArgumentNullException）
            if (config == null)
            {
                return string.Empty;
                // 或者 throw new ArgumentNullException(nameof(config));
            }
            // Microsoft.Extensions.Configuration.Ini 没有写入功能，
            // 这里手工生成 INI 文本
            var sb = new StringBuilder();
            try
            {
                if (config is IEnumerable enumerable && !(config is string))
                {
                    foreach (var item in enumerable)
                    {
                        WriteObjectToSection(sb, item);
                    }
                }
                else
                {
                    WriteObjectToSection(sb, config);
                }
            }
            catch (Exception)
            {
                throw;
            }
            return sb.ToString();
        }

        public T Deserialize<T>(string content)
        {
            // 1. 对传入的 content 进行空检测（如果传入 null，按空字符串处理或抛异常）
            if (content == null)
            {
                // 视业务需求选择：返回默认值、抛异常、或按空字符串处理
                // 这里按原逻辑容错处理为空字符串
                content = string.Empty;
                // 或者: throw new ArgumentNullException(nameof(content));
            }

            try
            {
                // 把字符串转成 IConfigurationRoot
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
                {
                    _configuration = new ConfigurationBuilder()
                        .AddIniStream(stream)
                        .Build();
                }

                if (typeof(IEnumerable).IsAssignableFrom(typeof(T)) && typeof(T).IsGenericType)
                {
                    // 获取集合的泛型类型
                    var itemtype = typeof(T).GenericTypeArguments[0];

                    // 创建 List<T> 类型
                    var listtype = typeof(List<>).MakeGenericType(itemtype);

                    // 3. 空检测：Activator.CreateInstance 返回可能为 null，使用 ?? throw 抛异常
                    var list = Activator.CreateInstance(listtype) as IList
                               ?? throw new InvalidOperationException($"无法创建集合类型 {listtype.Name} 的实例。");

                    // 顶层节点就是各个 section
                    foreach (var section in _configuration.GetChildren())
                    {
                        // 4. 空检测：反射创建实例可能失败（例如没有无参构造函数）
                        var item = Activator.CreateInstance(itemtype)
                                   ?? throw new InvalidOperationException($"无法创建类型 {itemtype.Name} 的实例，请确保该类具有无参构造函数。");

                        IniDataToObject(item, section);
                        list.Add(item);
                    }

                    // 5. 安全转型
                    return (T)list;
                }
                else
                {
                    // 6. 空检测：反射创建实例可能失败
                    var instance = (T)(Activator.CreateInstance(typeof(T))
                                   ?? throw new InvalidOperationException($"无法创建类型 {typeof(T).Name} 的实例，请确保该类具有无参构造函数。"));

                    var section = _configuration.GetChildren().FirstOrDefault();
                    if (section != null)
                    {
                        IniDataToObject(instance, section);
                    }
                    return instance;
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 把 IConfigurationSection 写入到对象
        /// </summary>
        private void IniDataToObject(object obj, IConfigurationSection sectionData)
        {
            if (sectionData == null)
            {
                return;
            }
            var props = obj.GetType().GetProperties();
            foreach (var prop in props)
            {
                if (prop.CanWrite)
                {
                    // 用索引器直接取 key，等价于 sectionData.GetSection(prop.Name).Value
                    var raw = sectionData[prop.Name];
                    if (raw != null)
                    {
                        var value = ConvertValue(raw, prop.PropertyType);
                        prop.SetValue(obj, value);
                    }
                }
            }
        }

        /// <summary>
        /// 类型转换
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <returns></returns>
        private object? ConvertValue(string value, Type targetType)
        {
            if (targetType == typeof(string)) return value;
            if (targetType.IsEnum) return Enum.Parse(targetType, value);
            if (targetType == typeof(bool)) return value.Equals("true", StringComparison.OrdinalIgnoreCase);

            var parseMethod = targetType.GetMethod("Parse", new[] { typeof(string) });
            if (parseMethod != null)
            {
                return parseMethod.Invoke(null, new object[] { value });
            }

            return Convert.ChangeType(value, targetType);
        }

        /// <summary>
        /// 把对象写成 [Section] Key=Value 形式的 INI 文本
        /// </summary>
        /// <param name="obj"></param>
        private void WriteObjectToSection(StringBuilder sb, object obj)
        {
            string? sectionName;
            try
            {
                sectionName = GetInstanceName(obj);
            }
            catch (Exception)
            {
                throw;
            }

            var properties = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            sb.Append('[').Append(sectionName).Append(']').AppendLine();
            foreach (var prop in properties)
            {
                if (prop.CanRead)
                {
                    var value = prop.GetValue(obj)?.ToString() ?? "";
                    sb.Append(prop.Name).Append('=').Append(value).AppendLine();
                }
            }
            sb.AppendLine(); // section 之间留空行
        }

        /// <summary>
        /// 通过特性获取实例名称
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        /// <exception cref="IniConfigException"></exception>
        private string? GetInstanceName(object obj)
        {
            var props = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var p = props.FirstOrDefault(x => x.GetCustomAttribute<IniConfigInstanceNameAttribute>() != null);
            if (p == null)
            {
                throw new IniConfigException($"{obj.GetType().Name} 无法使用INI配置管理");
            }
            return p.GetValue(obj)?.ToString();
        }
    }
}