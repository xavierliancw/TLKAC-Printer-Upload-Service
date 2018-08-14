using System.ServiceProcess;

namespace TLKAC_Printer_Upload_Service
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
#if DEBUG
            //Lol I hope I never push the actual values to source control
            var args = new string[]
            {
                "",
                "",
                ""
            };

            Service1 debugSvcInstance = new Service1();
            debugSvcInstance.OnDebug(args);
            System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
#else
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new Service1()
            };
            ServiceBase.Run(ServicesToRun);
#endif
        }
    }
}
