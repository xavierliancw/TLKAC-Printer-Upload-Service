using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace TLKAC_CIRRUS_Upload_Service
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
                var credFileContents = File.ReadAllText(path);
                if (DetectsPossibleEncryptionFor(credFileContents))
                {
                    credFileContents = EncryptionBlackBox(credFileContents);    //If possibly encrypted, try decrpyting
                }
                var result = JsonConvert.DeserializeObject<Credentials>(credFileContents);
                File.WriteAllText(path, EncryptionBlackBox(credFileContents));  //If deserialization worked, then re-encrpyt the file
                return result;
            }
            catch (Exception e)
            {
                Service1.LogEvent("Couldn't read cred file because: " + e.Message);
                return null;
            }
        }

        private static bool DetectsPossibleEncryptionFor(string input)
        {
            bool inputIsProbablyCurrentlyEncrypted;

            //Check what state the input is in
            try
            {
                JsonConvert.DeserializeObject<Credentials>(input);
                inputIsProbablyCurrentlyEncrypted = false;
            }
            catch
            {
                inputIsProbablyCurrentlyEncrypted = true;
            }
            return inputIsProbablyCurrentlyEncrypted;
        }

        private static string EncryptionBlackBox(string input)
        {
            var key = "just for obfuscation whatever";
            var result = new StringBuilder();

            for (int x = 0; x < input.Length; x++)
            {
                result.Append((char)((uint)input[x] ^ key[x % key.Length]));
            }
            return result.ToString();
        }

        private static string GetCredDocPath()
        {
            return AppDomain.CurrentDomain.BaseDirectory + "credentials.json";
        }
    }
}
