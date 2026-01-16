using Microsoft.Data.SqlClient;
using Microsoft.VisualBasic.ApplicationServices;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NET.D365.TOOLS.Services
{
    public class ClearSQLLog
    {
        private string dbIp;
        private string dbName;
        private string dbUser;
        private string dbPass;
        private string connString;
        private int targetSizeMB;

        public ClearSQLLog(bool isTest)
        {
            dbIp = isTest ? ConfigurationManager.AppSettings["TestDbIp"] : ConfigurationManager.AppSettings["DbIp"];
            dbName = ConfigurationManager.AppSettings["DbName"];
            dbUser = isTest ? ConfigurationManager.AppSettings["TestDbUser"] : ConfigurationManager.AppSettings["DbUser"];
            dbPass = isTest ? ConfigurationManager.AppSettings["TestDbPassword"] : ConfigurationManager.AppSettings["DbPassword"];
            targetSizeMB = int.Parse(ConfigurationManager.AppSettings["LogTargetSizeMB"]);
            connString = $"Server={dbIp};Database=master;User Id={dbUser};Password={dbPass};Encrypt=False;Integrated Security=False;TrustServerCertificate=True;MultipleActiveResultSets=True;";
        }

        public double ShrinkDatabaseLog()
        {
            double remainSize = 0;
            using (SqlConnection conn = new SqlConnection(connString))
            {
                conn.Open();
                string sql = $@"
                    USE [{dbName}];
                    ALTER DATABASE [{dbName}] SET RECOVERY SIMPLE;
                    DECLARE @LogFile nvarchar(255);
                    SELECT @LogFile = name FROM sys.database_files WHERE type = 1;
                    DBCC SHRINKFILE (@LogFile, {targetSizeMB});
                    ALTER DATABASE [{dbName}] SET RECOVERY FULL;";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.CommandTimeout = 0; // 收缩大文件可能很慢，设置不超时
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (reader.FieldCount >= 3)
                            {
                                long currentSize = reader.GetInt32(2);
                                remainSize = currentSize * 8.0 / 1024.0;
                            }
                        }
                    }
                }
            }
            return remainSize;
        }
    }
}
