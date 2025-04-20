using System.Text.Json;
using System.Net;

namespace synk
{
    public static class blobController
    {
        //the dictionary that holds all magic keys and their associated blob filenames
        
        static Dictionary<string, string> blobKeys = new Dictionary<string, string>();

        // save blobKeys to file
        static public void SaveBlobKeys()
        {
            string fn = "SaveBlobKeys";
            DBg.d(LogLevel.Trace, fn);
            
            string blobKeyFile = Path.Combine(GlobalConfig.synkStore!, "blobKeys.json");
            try
            {
                File.WriteAllText(blobKeyFile, JsonSerializer.Serialize(blobKeys, BlobKeysJsonContext.Default.DictionaryStringString));
            }
            catch (Exception ex)
            {
                DBg.d(LogLevel.Error, $"Failed to save blob keys to file {blobKeyFile}. {ex}");
            }
        }
        // load blobKeys from file
        static public void LoadBlobKeys()
        {
            string fn = "LoadBlobKeys";
            DBg.d(LogLevel.Trace, fn);
            
            string blobKeyFile = Path.Combine(GlobalConfig.synkStore!, "blobKeys.json");
            try
            {
                if (File.Exists(blobKeyFile))
                {
                    blobKeys = JsonSerializer.Deserialize(File.ReadAllText(blobKeyFile), BlobKeysJsonContext.Default.DictionaryStringString)
                               ?? new Dictionary<string, string>();
                }
            }
            catch (Exception ex)
            {
                DBg.d(LogLevel.Error, $"Failed to load blob keys from file {blobKeyFile}. {ex}");
            }
        }

        // verify that for each blobKey, the blob file actually exists; return true/false
        static public bool VerifyBlobKeyFiles(bool purgeMissing = false)
        {
            string fn = "VerifyBlobKeyFiles";
            DBg.d(LogLevel.Trace, fn);
            
            foreach (var blobKey in blobKeys)
            {
                string blobFile = Path.Combine(GlobalConfig.synkStore!, blobKey.Value);
                if (!File.Exists(blobFile))
                {
                    if (purgeMissing)
                    {
                        DBg.d(LogLevel.Warning, $"Blob file {blobFile} does not exist for key {blobKey.Key}. Purging key.");
                        blobKeys.Remove(blobKey.Key);
                        SaveBlobKeys();
                    }
                    else
                    DBg.d(LogLevel.Error, $"Blob file {blobFile} does not exist for key {blobKey.Key}");
                    return false;
                }
            }
            return true;
        }

        
        // data written to the blobFile is sanitized; blobFile name is SHA256 hash of the blobKey
        static public void WriteBlobFile(string blobKey, byte[] data)
        {
            string fn = "WriteBlobFile";
            DBg.d(LogLevel.Trace, fn);

            try
            {
                // the blob file name is the SHA256 has of the blobKey
                string blobFileName = Path.Combine(GlobalConfig.synkStore!, GlobalStatic.SHA256(blobKey));
                // check if the blobKey already exists
                if (!blobKeys.ContainsKey(blobKey)) {
                    // add the blobKey and bloFileName to the dictionary
                    blobKeys[blobKey] = blobFileName;
                    SaveBlobKeys();
                    DBg.d(LogLevel.Debug, $"Blob key {blobKey} and {blobFileName} added.");
                    }
                
                File.WriteAllBytes(blobFileName, data);
                // success; return null exception
                DBg.d(LogLevel.Debug, $"Blob file {blobFileName} written for key {blobKey}");
                
            
            }
            catch (Exception ex)
            {
                DBg.d(LogLevel.Error, $"Failed to write blob file for key {blobKey}. {ex}");
                throw new Exception($"Failed to write blob file for key {blobKey}.", ex);
            }
        }

        // read the blob file and return the data
        static public byte[]? ReadBlobFile(string blobKey)
        {
            string fn = "ReadBlobFile";
            DBg.d(LogLevel.Trace, fn);
            
            // check if the blobKey exists
            if (blobKeys.ContainsKey(blobKey))
            {
                string blobFile = Path.Combine(GlobalConfig.synkStore!, blobKeys[blobKey]);
                if (File.Exists(blobFile))
                {
                    return File.ReadAllBytes(blobFile);
                }
                else
                {
                    DBg.d(LogLevel.Error, $"Blob file {blobFile} does not exist for key {blobKey}");
                    return null;
                }
            }
            else
            {
                DBg.d(LogLevel.Error, $"Blob key {blobKey} does not exist");
                return null;
            }
        }

        // method that returns true/false if the provided value is Url encoded
        static public bool IsUrlEncoded(string original)
        {
            string fn = "IsUrlEncoded";
            DBg.d(LogLevel.Trace, fn);
            
            string decoded = WebUtility.UrlDecode(original);
            string reencoded = WebUtility.UrlEncode(decoded);

            bool isUrlEncoded = string.Equals(original, reencoded, StringComparison.Ordinal);
            return isUrlEncoded;
        }
        // method that simply decodes the provided value
        static public string UrlDecode(string original)
        {
            string fn = "UrlDecode";
            DBg.d(LogLevel.Trace, fn);
            
            return WebUtility.UrlDecode(original);
        }

    }
}