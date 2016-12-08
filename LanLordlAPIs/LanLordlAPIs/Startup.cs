using System;
using System.Threading.Tasks;
using Hangfire;
using LanLordlAPIs.Classes.Utility;
using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(LanLordlAPIs.Startup))]

namespace LanLordlAPIs
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            bool isRunningOnSandbox = Convert.ToBoolean(CommonHelper.GetValueFromConfig("IsRunningOnSandBox"));
            string connString = "";
            if (isRunningOnSandbox)
            {
                connString = CommonHelper.GetValueFromConfig("HangFireSandboxConnectionString");

            }
            else
            {
                connString = CommonHelper.GetValueFromConfig("HangFireProductionConnectionString");

            }
            GlobalConfiguration.Configuration.UseSqlServerStorage(connString);
            app.UseHangfireDashboard();
            app.UseHangfireServer();

            RecurringJob.AddOrUpdate("SendRoutingNumAddReminder",() => methodInvoker(), Cron.Minutely);
        }

        public void methodInvoker()
        {
            if (DateTime.Now.Minute % 2 == 0)
            {
                logTest("1");
            }
            else
            {
                logTest("2");
                
            }
        }
        

        public void logTest(string toliSting)
        {
            Logger.Info("logTest ran for this input -> " + toliSting);
        }
    }
}
