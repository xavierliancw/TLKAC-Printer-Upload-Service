using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

namespace TLKAC_Printer_Upload_Service
{
    public partial class Service1 : ServiceBase
    {
        private const string printerOutputFolderPath = "C:\\Windows\\System32\\spool\\PRINTERS\\";
        private FileSystemWatcher folderWatcher = null;
        private bool isBusyProcessingBatch = false;
        private Object thisLock = new Object();
        private LinkedList<FileInfo> files = null;

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
        }

        protected override void OnStop()
        {
            //Clean up
            folderWatcher.EnableRaisingEvents = false;
            folderWatcher.Dispose();
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

                LogEvent("Directory start");
                foreach (var inf in files)
                {
                    LogEvent(inf.FullName);
                }
                LogEvent("Directory end");

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
                            Thread.Sleep(1000); //Wait a sec because if not, then this program'll sometimes delete the data before it can actually make it to the printer
                            currentFile = TryToOpen(files.First.Value.FullName);
                            if (currentFile == null)
                            {
                                Thread.Sleep(500);
                            }
                        }
                        attemptCount = 0;   //Reset this immediately since the while loop is done with it

                        if (currentFile != null)
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
                        OnChangedEvent(null, null);
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
            if (info.Extension == ".SHD")
            {
                LogEvent("Gonna delete " + info.FullName);
            }
            if (info.Extension == ".SPL")
            {
                LogEvent("Gonna upload " + info.FullName);
                try
                {
                    var task = new Firebase.Storage.FirebaseStorage("tlkac-api.appspot.com")
                        .Child("printerOutput")
                        .Child(DateTime.Now.ToString("yyyy/MM/dd"))
                        .Child(info.Name)
                        .PutAsync(file);
                    var whenThisDownloadURLIsFilledInItIsDoneUploading = await task;
                }
                catch (Exception e)
                {
                    LogEvent("Upload failed: " + e.Message);
                }
            }
            file.Close();
            File.Delete(info.FullName);
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

        private void LogEvent(string message)
        {
#if DEBUG
            string eventSource = "File Monitor Service";
            DateTime dt = new DateTime();
            dt = DateTime.UtcNow;
            message = dt.ToLocalTime() + ": " + message;
            EventLog.WriteEntry(eventSource, message);
#endif
        }
    }
}
