using System;
using System.IO;
using System.Threading.Tasks;
using Firebase.Storage;
using Firebase.Auth;
using System.Threading;
using System.Net;

namespace TLKAC_CIRRUS_Upload_Service
{
    class SVCFirebase
    {
        private const string storageURLStr = "tlkac-api.appspot.com";

        private readonly string apiKey;
        private readonly string credEmail;
        private readonly string credPass;

        private FirebaseAuthProvider auth;
        private FirebaseAuthLink token;
        private FirebaseStorageOptions fbRefOptions;
        private FirebaseStorage fbRef;
        private bool busyDoingAsyncStuff = false;
        private readonly Object asyncLock = new Object();

        public SVCFirebase(string key, string email, string pass)
        {
            apiKey = key;
            credEmail = email;
            credPass = pass;

            auth = new FirebaseAuthProvider(new FirebaseConfig(apiKey));
            new Thread(async () =>
            {
                await AuthenticateAsync();
            }).Start();
        }

        public static bool ThereIsInternet()
        {
            try
            {
                using (var client = new System.Net.WebClient())
                using (client.OpenRead("http://clients3.google.com/generate_204"))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public void LogEvent(string message)
        {
            WebRequest req = WebRequest.Create("https://us-central1-tlkac-api.cloudfunctions.net/log");
            req.Method = "POST";
            req.ContentType = "application/json";
            using (var streamWriter = new StreamWriter(req.GetRequestStream()))
            {
                string json = "{\"date\":\"" + DateTime.Now.ToString() + "\"," +
                              "\"log\":\"" + credEmail + " logged: " + message + "\"}";
                streamWriter.Write(json);
                streamWriter.Flush();
                streamWriter.Close();
            }
            //Fire and forget
            ThreadPool.QueueUserWorkItem(o => {
                try
                {
                    req.GetResponse();
                }
                catch
                {
                    return;  //Whatever don't do anything (fire and forget)
                }
            });
        }

        public void SendHeartBeat()
        {
            WebRequest req = WebRequest.Create("https://us-central1-tlkac-api.cloudfunctions.net/hearbeat");
            req.Method = "POST";
            req.ContentType = "application/json";

            //Can't log ".com" because the database can't have periods in the key, so only take first part of email
            var splitEmail = credEmail.Split('@');
            var userFromEmail = "UNKNOWN_USER";
            if (splitEmail.Length > 0)
            {
                userFromEmail = splitEmail[0];
            }
            using (var streamWriter = new StreamWriter(req.GetRequestStream()))
            {
                string json = "{\"service\":\"" + "uploadService" + "\"," +
                                "\"localTime\":\"" + DateTime.Now.ToString() + "\"," +
                                "\"account\":\"" + userFromEmail + "\"}";
                streamWriter.Write(json);
                streamWriter.Flush();
                streamWriter.Close();
            }
            //Fire and forget 
            ThreadPool.QueueUserWorkItem(o =>
            {
                try
                {
                    req.GetResponse();
                }
                catch
                {
                    return; //Forget
                }
            });
        }

        public async Task<bool> UploadAsync(FileStream file, FileInfo info, string rename = null,
            string directory = "", bool reauthRetry = false)
        {
            if (IsBusy())
            {
                return false;   //Don't do anything while busy
            }
            if (fbRef == null)
            {
                await AuthenticateAsync();
                if (fbRef == null)
                {
                    return false;   //If still not authenticated yet, don't do anything
                }
            }
            if (!ThereIsInternet())
            {
                return false;   //Don't even try if there's no internet
            }
            try
            {
                var fileName = info.Name;
                if (rename != null)
                {
                    fileName = rename;
                }
                //Use the ticket's date if possible
                var dirLocation = directory != ""
                    ? directory
                    : DateTime.Now.ToString("yyyy/MM/dd");
                var uploadTask = fbRef
                    .Child("printerOutput")
                    .Child(dirLocation)
                    .Child(fileName)
                    .PutAsync(file);
                var result = await uploadTask;
                return true;
            }
            catch (Exception e)
            {
                if (e.Message.Contains("Permission denied") && !reauthRetry)
                {
                    LogEvent("Reauthenticating");
                    await AuthenticateAsync();
                    return await UploadAsync(file, info, rename, directory, true);
                }
                else
                {
                    LogEvent("Upload failed: " + e.Message);
                }
                return false;
            }
        }

        private async Task AuthenticateAsync()
        {
            //Can't auth if there's no internet
            if (!ThereIsInternet())
            {
                return;
            }
            SetBusy(true);
            token = await auth.SignInWithEmailAndPasswordAsync(credEmail, credPass);
            fbRefOptions = new FirebaseStorageOptions
            {
                AuthTokenAsyncFactory = () => Task.FromResult(token.FirebaseToken)
            };
            fbRef = new FirebaseStorage(storageURLStr, fbRefOptions);
            SetBusy(false);
        }
        
        private void SetBusy(bool busy)
        {
            lock (asyncLock)
            {
                busyDoingAsyncStuff = busy;
            }
        }

        private bool IsBusy()
        {
            lock (asyncLock)
            {
                return busyDoingAsyncStuff;
            }
        }
    }
}
