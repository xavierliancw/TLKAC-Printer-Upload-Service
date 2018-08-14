using System;
using System.IO;
using Newtonsoft.Json;

namespace TLKAC_Printer_Upload_Service
{
    class CredentialsManager
    {
        public static Credentials GetCreds()
        {
            var path = GetCredDocPath();

            //Check if credentials file exists
            if (!File.Exists(path))
            {
                //If the file doesn't exist, try to create it
                try
                {
                    //Create the JSON template for someone to fill out later
                    var template = new Credentials { Key = "", Email = "", Password = "" };

                    //Create the file, write the JSON in, then close the file
                    File.WriteAllText(path, JsonConvert.SerializeObject(template));
                }
                catch (Exception e)
                {
                    Service1.LogEvent("Couldn't create cred file because: " + e.Message);
                    return null;
                }
            }
            //Read the cred file and extract creds
            try
            {
                return JsonConvert.DeserializeObject<Credentials>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Service1.LogEvent("Couldn't read cred file because: " + e.Message);
                return null;
            }
        }

        private static string GetCredDocPath()
        {
            return AppDomain.CurrentDomain.BaseDirectory + "credentials.json";
        }
    }
}
