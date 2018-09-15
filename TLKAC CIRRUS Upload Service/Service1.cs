using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

namespace TLKAC_CIRRUS_Upload_Service
{
    public partial class Service1 : ServiceBase
    {
        private const string printerOutputFolderPath = "C:\\Windows\\System32\\spool\\PRINTERS\\";
        private FileSystemWatcher folderWatcher = null;
        private bool isBusyProcessingBatch = false;
        private object thisLock = new object();
        private LinkedList<FileInfo> files = null;
        private SVCFirebase svc = null;
        private string currentAccount = null;
        private System.Timers.Timer timer = null;

        public Service1()
        {
            InitializeComponent();
        }

        public void OnDebug()
        {
            OnStart(null);
        }

        protected override void OnStart(string[] args)
        {
            //Grab those creds
            var creds = CredentialsManager.GetCreds(loggingSVC: svc);
            currentAccount = creds.Email;

            //Start up Firebase service
            svc = new SVCFirebase(creds.Key, creds.Email, creds.Password);

            //Initialize the folder watcher
            folderWatcher = new FileSystemWatcher(printerOutputFolderPath)
            {
                NotifyFilter = NotifyFilters.LastWrite   //Only observe writes to the directory
            };
            //Add event handlers
            folderWatcher.Changed += new FileSystemEventHandler(OnChangedEvent);
            folderWatcher.Created += new FileSystemEventHandler(OnChangedEvent);

            //Start watching
            folderWatcher.EnableRaisingEvents = true;

            //Start the heartbeat
            timer = new System.Timers.Timer
            {
                Interval = 1000 * 60 * 60   //Every hour
            };
            timer.Elapsed += new System.Timers.ElapsedEventHandler(SendHeartbeat);
            timer.Start();
        }

        protected override void OnStop()
        {
            //Clean up
            if (folderWatcher != null)
            {
                folderWatcher.EnableRaisingEvents = false;
                folderWatcher.Dispose();
            }
        }

        private void OnChangedEvent(object sender, FileSystemEventArgs e)
        {
            //FileSystemWatcher will fire the event multiple times, so this bool limits it
            if (!IsBusy())
            {
                SetBusy(true);

                var dirInfo = new DirectoryInfo(printerOutputFolderPath);
                var fileAr = dirInfo.GetFiles().ToArray();
                files = new LinkedList<FileInfo>(fileAr);
                
                //If there's nothing in the directory, just stop
                if (files.Count == 0)
                {
                    SetBusy(false);
                    return;
                }
                new Thread(async () =>
                {
                    FileStream currentFile = null;
                    var attemptCount = 0;
                    var attemptLimit = 10;

                    //Do this stuff until the batch is empty
                    while (files.Count != 0)
                    {
                        //Try to open a file, but give up if the file can't be opened after a certain amount of attempts
                        while (currentFile == null && attemptCount < attemptLimit)
                        {
                            attemptCount += 1;
                            Thread.Sleep(3000); //Wait a bit because if not, then this program'll sometimes delete the data before it can actually make it to the printer
                            currentFile = TryToOpen(files.First.Value.FullName);
                            if (currentFile == null)
                            {
                                Thread.Sleep(500);
                            }
                        }
                        attemptCount = 0;   //Reset this immediately since the while loop is done with it

                        if (currentFile != null && files.First.Value != null)
                        {
                            await ProcessAsync(currentFile, files.First.Value);
                        }
                        //Clean up for next loop
                        currentFile = null;
                        files.RemoveFirst();
                    }
                    SetBusy(false); //Now that all the files in the batch have been processed and removed from the linked list, the program isn't busy anymore

                    //One more thing, if the directory still contains SHD or SPL files, it's not done, so call again
                    var leftoverFiles = GetFiles(printerOutputFolderPath, ".SHD", ".SPL");
                    if (leftoverFiles.Count != 0)
                    {
                        Thread.Sleep(1000);
                        if (SVCFirebase.ThereIsInternet())  //Oh, but don't keep retrying if there's no internet
                        {
                            OnChangedEvent(null, null);
                        }
                    }
                }).Start();
            }
        }

        /// <summary>
        /// https://stackoverflow.com/a/8132800
        /// </summary>
        /// <param name="path"></param>
        /// <param name="extensions"></param>
        /// <returns></returns>
        private List<FileInfo> GetFiles(string path, params string[] extensions)
        {
            List<FileInfo> list = new List<FileInfo>();
            foreach (string ext in extensions)
            {
                list.AddRange(new DirectoryInfo(path).GetFiles("*" + ext).Where(
                    p => p.Extension.Equals(ext, StringComparison.CurrentCultureIgnoreCase)
                    ).ToArray());
            }
            return list;
        }

        private async Task ProcessAsync(FileStream file, FileInfo info)
        {
            //Only process .SHD and .SPL files
            if (info.Extension != ".SHD" && info.Extension != ".SPL")
            {
                file.Close();
                return;
            }
            var shouldDelete = false;

            if (info.Length < 3)
            {
                shouldDelete = true;    //Don't even bother with tiny files. Sometimes random 1 byte files make it to the spool file, so just throw them away
            }
            else if (info.Extension == ".SPL")
            {
                Ticket ticketInfo = null;
                try
                {
                    file.Close();
                    ticketInfo = TicketParser.Parse(file, info, loggingSvc: svc);
                    file = File.Open(info.FullName, FileMode.Open);
                }
                catch (Exception e)
                {
                    svc.LogEvent("ProcessAsync could not reopen file because: " + e.Message);
                }
                if (ticketInfo != null)
                {
                    //Try to get the date to tell Firebase exactly where to upload the file
                    var uploadDirectory = "";
                    if (ticketInfo.Timestamp.HasValue)
                    {
                        var ticketTime = (DateTime)ticketInfo.Timestamp;
                        uploadDirectory = ticketTime.ToString("yyyy/MM/dd");
                    }
                    //Delete if upload is successful
                    shouldDelete = await svc.UploadAsync(file, info,
                        rename: ticketInfo.OrderNumber + "_" + ticketInfo.OrderKind + ".txt",   //Change extension to .txt to make life easier
                        directory: uploadDirectory);
                }
                else
                {
                    //This is an error case, but I still want to see the file get uploaded
                    var guidTitle = Guid.NewGuid();
                    shouldDelete = await svc.UploadAsync(file, info, rename: guidTitle + ".txt");  //At this point it had to be either a .SHD or .SPL file... Sometimes .SHDs get converted to .SPLs and I don't know why. (That kills the ticket parser)
                    svc.LogEvent("Null ticket info for " + info.Name + ", uploaded as " + guidTitle + ".txt" + " with size (bytes): " + info.Length.ToString());
                }
            }
            else if (info.Extension == ".SHD")
            {
                shouldDelete = true;    //Delete all .SHD no matter so the computer's spool folder doesn't get gunked up
            }
            file.Close();

            if (shouldDelete)
            {
                File.Delete(info.FullName);
            }
        }

        private bool IsBusy()
        {
            lock (thisLock)
            {
                return isBusyProcessingBatch;
            }
        }

        private void SetBusy(bool busy)
        {
            lock(thisLock)
            {
                isBusyProcessingBatch = busy;
            }
        }

        private FileStream TryToOpen(string filePath)
        {
            try
            {
                return File.Open(filePath, FileMode.Open);
            }
            catch
            {
                return null;
            }
        }

        private void SendHeartbeat(object sender, System.Timers.ElapsedEventArgs e)
        {
            svc.SendHeartBeat();
        }
    }
}
