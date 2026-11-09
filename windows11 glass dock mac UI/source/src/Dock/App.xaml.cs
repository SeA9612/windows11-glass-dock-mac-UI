using System;
using System.Threading;
using System.Windows;
namespace GlassDock;
public partial class App : Application
{
    private Mutex? instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        StartupDiagnostics.Write("OnStartup entered");
        instance = new Mutex(true, "Local\\GlassDock.Personal", out bool first);
        StartupDiagnostics.Write($"Instance mutex acquired={first}");
        if (!first)
        {
            StartupDiagnostics.Write("Another GlassDock instance owns the mutex");
            MessageBox.Show("GlassDock이 이미 실행 중입니다. 알림 영역의 숨겨진 아이콘을 확인해 주세요.", "GlassDock", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }
        DispatcherUnhandledException += (_, a) => { StartupDiagnostics.Write("Dispatcher exception", a.Exception); SettingsStore.Log(a.Exception); MessageBox.Show("오류가 발생했습니다. 독을 종료합니다. Windows 기본 작업 표시줄은 계속 사용할 수 있습니다."); a.Handled = true; Shutdown(1); };
        AppDomain.CurrentDomain.UnhandledException += (_, a) => StartupDiagnostics.Write("Unhandled exception", a.ExceptionObject as Exception);
        try
        {
            var dock = new DockWindow();
            StartupDiagnostics.Write("DockWindow constructed");
            dock.Show();
#if BORDERLESS_CAPTURE_IDENTITY
            _=CapturePermission.RequestAsync();
#endif
            StartupDiagnostics.Write($"DockWindow.Show returned; visible={dock.IsVisible}, hwnd={new System.Windows.Interop.WindowInteropHelper(dock).Handle}");
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Write("Startup failed", ex);
            SettingsStore.Log(ex);
            MessageBox.Show($"GlassDock 시작 실패\n\n{ex.Message}\n\n진단 기록: {StartupDiagnostics.Path}", "GlassDock", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
    protected override void OnExit(ExitEventArgs e) { instance?.Dispose(); base.OnExit(e); }
}
