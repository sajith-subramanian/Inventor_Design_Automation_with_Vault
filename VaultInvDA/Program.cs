using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Forge;
using Autodesk.Forge.DesignAutomation.v3;
using Autodesk.Forge.Model;
using Autodesk.Forge.Model.DesignAutomation.v3;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using RestSharp;
using ActivitiesApi = Autodesk.Forge.DesignAutomation.v3.ActivitiesApi;
using Activity = Autodesk.Forge.Model.DesignAutomation.v3.Activity;
using WorkItem = Autodesk.Forge.Model.DesignAutomation.v3.WorkItem;
using WorkItemsApi = Autodesk.Forge.DesignAutomation.v3.WorkItemsApi;


namespace VaultInventorDA
{
    class Program
    {      
        static string ConsumerKey = Environment.GetEnvironmentVariable("FORGE_CLIENT_ID");
        static string ConsumerSecret = Environment.GetEnvironmentVariable("FORGE_CLIENT_SECRET");
        static string EngineName = "Autodesk.Inventor+23";
        static string LocalAppPackageZip = Path.GetFullPath(@"..\..\..\..\UpdateiProp\UpdateiPropBundle\UpdateiPropBundle.zip");
        static string APPNAME = "VaultInvDA";
        static string ACTIVITY_NAME = "VaultInvDActivity";
        static string ALIAS = "v1";
        static Byte[] filebytes = null;
        static string inputFileName = string.Empty;
        private static dynamic InternalToken { get; set; }

        public class Output
        {
            public StatusEnum Status { get; set; }
            public string Message { get; set; }
            public Output(StatusEnum status, string message)
            {
                Status = status;
                Message = message;
                Console.WriteLine(status + ":" + message);
            }
            public enum StatusEnum
            {
                Error,
                Sucess
            }
        }
        

        static void Main(string[] args)
        {           
            // Get file as stream from Vault
            filebytes = VaultUtil.DownloadFileStream(out inputFileName);
            if (filebytes != null)
            {
                Task t = MainAsync(args);
                t.Wait();
            }
        }

        private static async Task MainAsync(string[] args)
        {
            try
            {
                Console.WriteLine("Fetching internal token...");
                InternalToken = await GetInternalAsync();
                try
                {
                    Console.WriteLine("Creating bucket...");
                    dynamic bucket = await CreateBucket();
                    try
                    {
                        Console.WriteLine("Uploading Ipt file from Vault...");
                        dynamic uploadedobject = await UploadIptFile(bucket.bucketKey);
                        try
                        {
                            try
                            {
                                Console.WriteLine("Creating Activity...");
                                await CreateActivity();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Activity failed: " + ex.Message);
                            }
                            Console.WriteLine("Creating workitem...");
                            await CreateWorkItem(bucket.bucketKey);
                            Console.WriteLine("Press any key to terminate");
                            Console.ReadKey();
                        }
                        catch (Exception ex) { Console.WriteLine("Workitem failed: " + ex.Message); }
                    }
                    catch (Exception ex) { Console.WriteLine("UploadIptFile failed: " + ex.Message); }
                }
                catch (Exception ex) { Console.WriteLine("CreateBucket failed: " + ex.Message); }
            }
            catch (Exception ex) { Console.WriteLine("GetInternalAsync failed: " + ex.Message); }
        }


        /// <summary>
        /// Creates WorkItem
        /// </summary>
        /// <returns>True if successful</returns>
        private static async Task<dynamic> CreateWorkItem(String bucketkey)
        {
            string nickName = ConsumerKey;
            Bearer bearer = (await Get2LeggedTokenAsync(new Scope[] { Scope.CodeAll })).ToObject<Bearer>();
            string downloadUrl = string.Format("https://developer.api.autodesk.com/oss/v2/buckets/{0}/objects/{1}", bucketkey, inputFileName);
            string uploadUrl = string.Format("https://developer.api.autodesk.com/oss/v2/buckets/{0}/objects/{1}", bucketkey, inputFileName);
            JObject iptFile = new JObject
                {
                  new JProperty("url", downloadUrl),
                  new JProperty("headers",
                  new JObject{
                    new JProperty("Authorization", "Bearer " + InternalToken.access_token)
                  })
                };
            JObject resultIpt = new JObject
                {
                  new JProperty("verb", "put"),
                  new JProperty("url", uploadUrl),
                  new JProperty("headers",
                  new JObject{
                    new JProperty("Authorization", "Bearer " + InternalToken.access_token)
                  })
                };
            WorkItem workItemSpec = new WorkItem(
              null, string.Format("{0}.{1}+{2}", nickName, ACTIVITY_NAME, ALIAS),
              new Dictionary<string, JObject>()
              {{ "InputIPT",  iptFile },{ "ResultIPT", resultIpt  }}, null);
            WorkItemsApi workItemApi = new WorkItemsApi();
            workItemApi.Configuration.AccessToken = bearer.AccessToken;
            WorkItemStatus newWorkItem = await workItemApi.WorkItemsCreateWorkItemsAsync(null, null, workItemSpec);

            for (int i = 0; i < 1000; i++)
            {
                System.Threading.Thread.Sleep(1000);
                WorkItemStatus workItemStatus = await workItemApi.WorkItemsGetWorkitemsStatusAsync(newWorkItem.Id);
                if (workItemStatus.Status == WorkItemStatus.StatusEnum.Pending || workItemStatus.Status == WorkItemStatus.StatusEnum.Inprogress) continue;
                break;
            }
            await CheckintoVault(uploadUrl);
            return new Output(Output.StatusEnum.Sucess, "Activity created");
        }


        /// <summary>
        /// Creates Activity
        /// </summary>
        /// <returns>True if successful</returns>
        private static async Task<dynamic> CreateActivity()
        {
            Bearer bearer = (await Get2LeggedTokenAsync(new Scope[] { Scope.CodeAll })).ToObject<Bearer>();
            string nickName = ConsumerKey;

            AppBundlesApi appBundlesApi = new AppBundlesApi();
            appBundlesApi.Configuration.AccessToken = bearer.AccessToken;
            PageString appBundles = await appBundlesApi.AppBundlesGetItemsAsync();
            string appBundleID = string.Format("{0}.{1}+{2}", nickName, APPNAME, ALIAS);

            if (!appBundles.Data.Contains(appBundleID))
            {
                if (!System.IO.File.Exists(LocalAppPackageZip)) return new Output(Output.StatusEnum.Error, "Bundle not found at " + LocalAppPackageZip);
                // create new bundle
                AppBundle appBundleSpec = new AppBundle(APPNAME, null, EngineName, null, null, APPNAME, null, APPNAME);
                AppBundle newApp = await appBundlesApi.AppBundlesCreateItemAsync(appBundleSpec);
                if (newApp == null) return new Output(Output.StatusEnum.Error, "Cannot create new app");
                // create alias
                Alias aliasSpec = new Alias(1, null, ALIAS);
                Alias newAlias = await appBundlesApi.AppBundlesCreateAliasAsync(APPNAME, aliasSpec);
                // upload the zip bundle
                RestClient uploadClient = new RestClient(newApp.UploadParameters.EndpointURL);
                RestRequest request = new RestRequest(string.Empty, Method.POST);
                request.AlwaysMultipartFormData = true;
                foreach (KeyValuePair<string, object> x in newApp.UploadParameters.FormData)
                    request.AddParameter(x.Key, x.Value);
                request.AddFile("file", LocalAppPackageZip);
                request.AddHeader("Cache-Control", "no-cache");
                var res = await uploadClient.ExecuteTaskAsync(request);
            }
            ActivitiesApi activitiesApi = new ActivitiesApi();
            activitiesApi.Configuration.AccessToken = bearer.AccessToken;
            PageString activities = await activitiesApi.ActivitiesGetItemsAsync();

            string activityID = string.Format("{0}.{1}+{2}", nickName, ACTIVITY_NAME, ALIAS);
            if (!activities.Data.Contains(activityID))
            {
                // create activity
                string commandLine = string.Format(@"$(engine.path)\\inventorcoreconsole.exe /i $(args[InputIPT].path) /al $(appbundles[{0}].path)", APPNAME);
                ModelParameter iptFile = new ModelParameter(false, false, ModelParameter.VerbEnum.Get, "Input Ipt File", true, inputFileName);
                ModelParameter result = new ModelParameter(false, false, ModelParameter.VerbEnum.Put, "Resulting Ipt File", true, inputFileName);

                Activity activitySpec = new Activity(
                 new List<string> { commandLine },
                  new Dictionary<string, ModelParameter>()
                  {
                         { "InputIPT", iptFile },
                         { "ResultIPT",result},
                  },
                  EngineName,
                  new List<string>() { string.Format("{0}.{1}+{2}", nickName, APPNAME, ALIAS) },
                  null,
                  ACTIVITY_NAME,
                  null,
                  ACTIVITY_NAME
                );
                Activity newActivity = await activitiesApi.ActivitiesCreateItemAsync(activitySpec);
                Alias aliasSpec = new Alias(1, null, ALIAS);
                Alias newAlias = await activitiesApi.ActivitiesCreateAliasAsync(ACTIVITY_NAME, aliasSpec);
            }
            return new Output(Output.StatusEnum.Sucess, "Activity created");
        }

        /// <summary>
        /// Fetches the internal token
        /// </summary>
        /// <returns>Internal token</returns>
        private async static Task<dynamic> GetInternalAsync()
        {
            if (InternalToken == null || InternalToken.ExpiresAt < DateTime.UtcNow)
            {
                InternalToken = await Get2LeggedTokenAsync(new Scope[] { Scope.BucketCreate, Scope.BucketRead, Scope.DataRead, Scope.DataCreate, Scope.DataWrite });
                InternalToken.ExpiresAt = DateTime.UtcNow.AddSeconds(InternalToken.expires_in);
            }
            return InternalToken;
        }

        /// <summary>
        /// Fetches the token based on the scope argument
        /// </summary>
        /// <returns>token</returns>
        private async static Task<dynamic> Get2LeggedTokenAsync(Scope[] scopes)
        {
            TwoLeggedApi oauth = new TwoLeggedApi();
            string grantType = "client_credentials";
            dynamic bearer = await oauth.AuthenticateAsync(
             ConsumerKey,
             ConsumerSecret,
              grantType,
              scopes);
            return bearer;
        }

        /// <summary>
        /// Creates Bucket
        /// </summary>
        /// <returns>Newly created bucket</returns>
        private async static Task<dynamic> CreateBucket()
        {
            string bucketKey = "inventorio" + Guid.NewGuid().ToString("N").ToLower();
            PostBucketsPayload postBucket = new PostBucketsPayload(bucketKey, null, PostBucketsPayload.PolicyKeyEnum.Transient);
            BucketsApi bucketsApi = new BucketsApi();
            bucketsApi.Configuration.AccessToken = InternalToken.access_token;
            dynamic newBucket = await bucketsApi.CreateBucketAsync(postBucket);
            return newBucket;
        }

        /// <summary>
        /// Uploads Ipt file from Vault to bucket
        /// </summary>
        /// <returns>uploaded file</returns>
        private async static Task<dynamic> UploadIptFile(string bucketKey)
        {
            ObjectsApi objects = new ObjectsApi();
            objects.Configuration.AccessToken = InternalToken.access_token;
            dynamic uploadedObj = null;

            using (StreamReader streamReader = new StreamReader(new MemoryStream(filebytes)))
            {
                uploadedObj = await objects.UploadObjectAsync(bucketKey,
                      inputFileName, (int)streamReader.BaseStream.Length, streamReader.BaseStream,
                      "application/octet-stream");
            }
            return uploadedObj;
        }

        /// <summary>
        /// Check in updated file back into Vault
        /// </summary>
        /// <returns>Success or error </returns>
        public static async Task<dynamic> CheckintoVault(string url)
        {
            IRestClient client = new RestClient("https://developer.api.autodesk.com/");
            RestRequest request = new RestRequest(url, Method.GET);
            request.AddHeader("Authorization", "Bearer " + InternalToken.access_token);
            request.AddHeader("Accept-Encoding", "gzip, deflate");
            IRestResponse response = await client.ExecuteTaskAsync(request);
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                return new Output(Output.StatusEnum.Error, "Not able to download to local drive");
            }
            else
            {
                VaultUtil.CheckinFileStream(response.RawBytes);
                return new Output(Output.StatusEnum.Sucess, "Checked into Vault successfully");
            }
        }
    }
}





