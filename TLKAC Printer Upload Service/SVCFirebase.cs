using System;
using System.IO;
using System.Threading.Tasks;
using Firebase.Storage;
using Firebase.Auth;
using System.Threading;

namespace TLKAC_Printer_Upload_Service
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

        public async Task<bool> UploadAsync(FileStream file, FileInfo info, string rename = null, bool reauthRetry = false)
        {
            if (IsBusy())
            {
                return false;   //Don't do anything while busy
            }
            if (fbRef == null)
            {
                return false;   //If not first-time authenticated yet, don't do anything
            }
            try
            {
                var fileName = info.Name;
                if (rename != null)
                {
                    fileName = rename;
                }
                var uploadTask = fbRef
                    .Child("printerOutput")
                    .Child(DateTime.Now.ToString("yyyy/MM/dd"))
                    .Child(fileName)
                    .PutAsync(file);
                var result = await uploadTask;
                return true;
            }
            catch (Exception e)
            {
                if (e.Message.Contains("Permission denied") && !reauthRetry)
                {
                    Service1.LogEvent("Reauthenticating");
                    await AuthenticateAsync();
                    return await UploadAsync(file, info, rename, true);
                }
                else
                {
                    Service1.LogEvent("Upload failed: " + e.Message);
                }
                return false;
            }
        }

        private async Task AuthenticateAsync()
        {
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
