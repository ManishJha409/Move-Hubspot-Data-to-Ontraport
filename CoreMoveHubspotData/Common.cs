using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Net;
using System.IO;
using Microsoft.Extensions.Configuration;
using CoreMoveHubspotData;
using Newtonsoft.Json.Linq;

namespace MoveHubspotToOntraport
{
    public class Common
    {
        public static IConfigurationRoot app = AppConfiguration.GetConfig();
        
        public string OntraportAppKey = Convert.ToString(app["MySettings:OntraportAppKey"]);
        public string OntraportAppId = Convert.ToString(app["MySettings:OntraportAppId"]);
        public string HubspotAppKey = Convert.ToString(app["MySettings:HubspotAppKey"]);

        public static string GetApiResult(string strUrl, string module)
        {
            var searchResult = string.Empty;
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(strUrl);
                req.Method = "GET";
                req.ContentType = "application/json";

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
                    {
                        searchResult = sr.ReadToEnd();
                    }
                }
            }
            catch (WebException wex)
            {
                using (WebResponse response = wex.Response)
                {
                    if (response == null)
                        return null;
                    var sr = new StreamReader(response.GetResponseStream());
                    string respo = sr.ReadToEnd().Trim();
                    //searchresult = "error:" + respo;
                    searchResult = $"error: {module} failed due to {respo}";
                    ErrorLog.WriteLogFile(searchResult, strUrl, module);
                }
            }
            catch (Exception ex)
            {
                searchResult = $"error: {module} failed due to {ex.Message}";
                ErrorLog.WriteLogFile(searchResult, strUrl, module);
            }
            return searchResult;
        }

        public string PostApi(string strUrl, string body, out bool hasCreated, string method, string module)
        {
            hasCreated = false;
            var searchResult = string.Empty;
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(strUrl);
                if (string.IsNullOrEmpty(method))
                {
                    req.Method = "POST";
                }
                else
                {
                    req.Method = method;
                }
                req.ContentType = "application/x-www-form-urlencoded";
                req.Headers.Add("Api-Appid", OntraportAppId);
                req.Headers.Add("Api-Key", OntraportAppKey);
                req.Accept = "application/json";

                byte[] reqBody = System.Text.Encoding.ASCII.GetBytes(body);
                req.ContentLength = reqBody.Length;
                req.GetRequestStream().Write(reqBody, 0, reqBody.Length);
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
                    {
                        searchResult = sr.ReadToEnd();
                        hasCreated = true;
                    }
                }
            }
            catch (WebException wex)
            {
                using (WebResponse response = wex.Response)
                {
                    if (response == null)
                        return null;
                    var sr = new StreamReader(response.GetResponseStream());
                    string respo = sr.ReadToEnd().Trim();
                    searchResult = $"error: {module} failed due to {respo}";
                    ErrorLog.WriteLogFile(searchResult, strUrl, module);
                }
            }
            catch (Exception ex)
            {
                searchResult = $"error: {module} failed due to {ex.Message}";
                ErrorLog.WriteLogFile(searchResult, strUrl, module);
            }
            return searchResult;
        }

        public JObject MoveHubspotDataToGoogleAsync(JObject fileDetails)
        {
            var jo = new JObject();
            try
            {
                var url = string.Empty;
                bool isFileHidden = false;
                jo["hubspotfileurl"] = string.Empty;

                if (fileDetails != null && fileDetails.Count > 0)
                {
                    url = fileDetails["hubspotfileurl"].ToString();
                    isFileHidden = Convert.ToBoolean(fileDetails["ishiddenfile"].ToString());
                    jo["hubspotfileurl"] = url;
                }
                jo["fileUploaded"] = false;
                jo["webViewLink"] = string.Empty;
                jo["message"] = string.Empty;

                byte[] bytes = null;
                var mimeType = string.Empty;
                var name = string.Empty;
                try
                {
                    // file accessible hidden false for the file
                    if (!isFileHidden)
                    {
                        using (var webClient = new WebClient())
                        {
                            name = Path.GetFileName(url);
                            bytes = webClient.DownloadData(url);
                            var length = bytes.Length;
                            mimeType = webClient.ResponseHeaders["content-type"];
                        }
                    }
                    else
                    {
                        jo["message"] = "File is not accessible from Hubspot";
                    }
                }
                catch (Exception ex)
                {
                    jo["message"] = $" Error occurred : {ex.Message}";
                }

                if (bytes != null && bytes.Length > 0)
                {
                    bool status = false;
                    var fileLink = string.Empty;
                    var response = GoogleDrive.CheckFiles(name, mimeType);
                    if (response.Count > 0)
                    {
                        fileLink = response["webViewLink"].ToString();
                        jo["message"] = "File already Exist";
                        ErrorLog.InfoMessage($"File already exist on google drive {fileLink}");
                        Console.WriteLine($"File already exist on google drive {fileLink}");
                    }
                    else
                    {
                        fileLink = GoogleDrive.UploadByGoogleDrive(bytes, name, mimeType, out status);
                        jo["message"] = "success";
                    }
                    jo["fileUploaded"] = status;
                    jo["webViewLink"] = fileLink;
                }
                else
                {
                    jo["message"] = $"File is not accessible from Hubspot, so get preview file {url}";
                }
            }
            catch (Exception ex)
            {
                jo["message"] = $" Error occurred : {ex.Message}";
            }
            return jo;
        }
       
        public static void Output(string output)
        {
            Console.WriteLine(output);
        }

    }

    public static class JsonExtensions
    {
        public static bool IsNullOrEmpty(this JToken token)
        {
            return (token == null) ||
                   (token.Type == JTokenType.Array && !token.HasValues) ||
                   (token.Type == JTokenType.Object && !token.HasValues) ||
                   (token.Type == JTokenType.String && token.ToString() == String.Empty) ||
                   (token.Type == JTokenType.Null);
        }
    }
}