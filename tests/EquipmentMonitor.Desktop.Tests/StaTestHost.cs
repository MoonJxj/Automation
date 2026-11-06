using System.Runtime.ExceptionServices;

namespace EquipmentMonitor.Desktop.Tests;

/// <summary>
/// 在 STA 线程上执行窗体代码，并把异常原样带回测试线程。
/// </summary>
/// <remarks>
/// WinForms 控件有线程亲和性，OLE 相关功能要求 STA。xunit 默认在线程池的 MTA 线程上跑，
/// 大多数控件在 MTA 下也能建出来，但那是「碰巧可以」而不是保证。
/// 用 <see cref="ExceptionDispatchInfo"/> 保留原始堆栈，失败时能看到异常真正从哪个构造函数抛出。
/// </remarks>
internal static class StaTestHost
{
    public static void RunSta(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
