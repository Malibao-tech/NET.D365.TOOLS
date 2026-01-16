namespace NET.D365.TOOLS
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 设置全局字体
            Application.SetDefaultFont(new System.Drawing.Font("Microsoft YaHei UI", 10));

            Application.Run(new MenuForm());
        }
    }
}