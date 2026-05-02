using System.Windows;

namespace VagImmoEditor.Pro
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Initialize logging
            Serilog.Log.Logger = new Serilog.LoggerConfiguration()
                .WriteTo.File("logs/vag-editor-.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();
                
            Serilog.Log.Information("VAG IMMO Editor Pro started");
        }
    }
}
