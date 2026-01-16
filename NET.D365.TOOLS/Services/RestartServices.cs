using Microsoft.VisualBasic.ApplicationServices;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;


namespace NET.D365.TOOLS.Services
{
    public class RestartServices
    {
        private string[] servers;
        private string adminUser;
        private string adminPass;

        public RestartServices(bool isTest) 
        {
            servers = isTest ? ConfigurationManager.AppSettings["TestServerList"].Split(','): ConfigurationManager.AppSettings["ServerList"].Split(',');
            adminUser = isTest ? ConfigurationManager.AppSettings["TestAdminUser"] : ConfigurationManager.AppSettings["AdminUser"];
            adminPass = isTest ? ConfigurationManager.AppSettings["TestAdminPassword"] : ConfigurationManager.AppSettings["AdminPassword"];
        }

        public string RestartAll()
        {
            string message = "";
            foreach (var ip in servers)
            {
                try
                {
                    ConnectionOptions options = new ConnectionOptions
                    {
                        Username = adminUser,
                        Password = adminPass,
                        EnablePrivileges = true, // 必须开启特权才能执行重启
                        Impersonation = ImpersonationLevel.Impersonate
                    };

                    ManagementScope scope = new ManagementScope($@"\\{ip}\root\cimv2", options);
                    scope.Connect();

                    // 查询操作系统实例
                    ObjectQuery query = new ObjectQuery("SELECT * FROM Win32_OperatingSystem");
                    using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, query))
                    {
                        foreach (ManagementObject os in searcher.Get())
                        {
                            // 2 = Reboot, 1 = Shutdown, 0 = Log Off, 8 = Power Off
                            // 执行 Reboot 方法
                            ManagementBaseObject outParams = os.InvokeMethod("Reboot", null, null);
                            uint returnValue = (uint)outParams["ReturnValue"];
                            if (returnValue == 0)
                            {
                                message = $"{ip} 指令发送成功。";
                            }
                            else
                            {
                                message = $"{ip} 返回错误代码: {returnValue}";
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    message = $"{ip} 连接或执行失败: {ex.Message}";
                }
                Thread.Sleep(5000);
            }
            return message;
        }
    }
}
