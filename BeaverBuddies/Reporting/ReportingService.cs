using BeaverBuddies.Connect;
using BeaverBuddies.DesyncDetecter;
using BeaverBuddies.IO;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Timberborn.Autosaving;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSaveRuntimeSystem;
using Timberborn.SettlementNameSystem;
using TimberNet;
using UnityEngine.Bindings;

namespace BeaverBuddies.Reporting
{
    public class ReportingService
    {
        const string CREATE_URL = "https://api.airtable.com/v0/appdIpScGqlZ5FX3r/Errors";
        const string UPLOAD_URL = "https://content.airtable.com/v0/appdIpScGqlZ5FX3r/{0}/Data/uploadAttachment";

        private string accessToken;
        private SettlementNameService _settlementNameService;

        public ReportingService(SettlementNameService settlementNameService)
        {
            _settlementNameService = settlementNameService;
            accessToken = GetEmbeddedResource("BeaverBuddies.pat.properties");
            Plugin.Log(accessToken);
        }

        public static string GetStringHash(string str)
        {
            if (str == null)
            {
                return $"{0:X8}";
            }
            var bytes = Encoding.UTF8.GetBytes(str);
            return $"{TimberNetBase.GetHashCode(bytes):X8}";
        }

        public async Task<bool> PostDesync(string eventID, string role, byte[] mapBytes)
        {
            JObject fields = new JObject();
            fields["SaveID"] = GetStringHash(_settlementNameService.SettlementName);
            fields["EventID"] = eventID;
            fields["Role"] = role;
            fields["IsCrash"] = false;
            fields["DesyncTrace"] = DesyncDetecterService.GetLastDesyncTrace();


            Plugin.Log(EventIO.Get()?.ToString());
            
            JObject record = new JObject
            {
                { "fields", fields }
            };
            JArray records = [record];
            JObject body = new JObject
            {
                { "records", records }
            };
            
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            string bodyContent = body.ToString();
            Plugin.Log(bodyContent);
            HttpContent content = new StringContent(bodyContent, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PostAsync(CREATE_URL, content);
            if (!response.IsSuccessStatusCode)
            {
                Plugin.LogError($"Report post failed: {response.ReasonPhrase}");
                Plugin.LogError(await response.Content.ReadAsStringAsync());
                return false;
            }
            string responseString = await response.Content.ReadAsStringAsync();
            Plugin.Log(responseString);

            // If we didn't have a map, stop here
            if (mapBytes == null) return true;

            string recordID;
            try
            {
                JObject responseJSON = JObject.Parse(responseString);
                recordID = (string) responseJSON["records"][0]["id"];
            } catch
            {
                Plugin.LogError($"Invalid response: {responseString}");
                return false;
            }

            string base64Encoded = Convert.ToBase64String(mapBytes);

            body = new JObject()
            {
                { "contentType", "application/zip" },
                { "file", base64Encoded },
                { "filename", "map.zip" },
            };

            HttpContent uploadContent = new StringContent(body.ToString(), Encoding.UTF8, "application/json");
            response = await client.PostAsync(string.Format(UPLOAD_URL, recordID), uploadContent);
            if (!response.IsSuccessStatusCode)
            {
                Plugin.LogError($"Upload map failed: {response.ReasonPhrase}");
                return false;
            }

            return true;
        }
        

        private static string GetEmbeddedResource(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    Plugin.LogError("Unable to read PAT");
                    return null;
                }
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }


}
