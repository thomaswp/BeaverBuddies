using BeaverBuddies.DesyncDetecter;
using BeaverBuddies.IO;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using Timberborn.Autosaving;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSaveRuntimeSystem;
using Timberborn.SettlementNameSystem;
using TimberNet;

namespace BeaverBuddies.Reporting
{
    public class ReportingService
    {
        const string CREATE_URL = "https://api.airtable.com/v0/appdIpScGqlZ5FX3r/Errors";
        const string UPLOAD_URL = "https://content.airtable.com/v0/appdIpScGqlZ5FX3r/{0}/Data/uploadAttachment";

        private string accessToken;

        private DesyncDetecterService _desyncDetecterService;

        public ReportingService(DesyncDetecterService desyncDetecterService)
        {
            _desyncDetecterService = desyncDetecterService;

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

        public async bool PostDesync(string eventID)
        {
            JObject body = new JObject();
            body["SaveID"] = "?";
            body["EventID"] = eventID;
            body["Role"] = EventIO.Get()?.GetType()?.Name;
            body["IsCrash"] = false;
            body["DesyncTrace"] = DesyncDetecterService.GetLastDesyncTrace();
            
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            HttpContent content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PostAsync(CREATE_URL, content);
            if (!response.IsSuccessStatusCode)
            {
                Plugin.LogError($"Report post failed: {response.ReasonPhrase}");
                return false;
            }
            string responseString = await response.Content.ReadAsStringAsync();
            string recordID;
            try
            {
                JObject responseJSON = JObject.Parse(responseString);
                recordID = (String) responseJSON["records"][0]["id"];
            } catch (Exception e)
            {
                Plugin.LogError($"Invalid response: {responseString}");
                return false;
            }

            // TODO
            //_rehostingService.SaveRehostFile();
            HttpContent uploadContent = new StringContent(body.ToString(), Encoding.UTF8, "application/json");
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
