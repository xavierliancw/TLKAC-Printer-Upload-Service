using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace TLKAC_CIRRUS_Upload_Service
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : System.Configuration.Install.Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();
        }
    }
}
