using CoreMoveHubspotData;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MoveHubspotToOntraport
{
    public class Hubspot
    {
        public static IConfigurationRoot app = AppConfiguration.GetConfig();

        public string HubspotBaseUrl = Convert.ToString(app["MySettings:HubspotUrl"]);
        public string HubspotAppKey = Convert.ToString(app["MySettings:HubspotAppKey"]);
        public string HubspotPortalId = Convert.ToString(app["MySettings:HubspotPortalId"]);

        public void MoveContactAsync()
        {
            Common common = new Common();
            Ontraport op = new Ontraport();
            var hubspotFields = Ontraport.hubspotField;
            var properties = new StringBuilder();
            foreach(var _hubField in hubspotFields)
            {
                properties.Append($"&property={_hubField}");
            }

            if(properties.Length == 0)
            {
                properties.Append($"&property=firstname&property=lastname&property=email");
            }

            //get batchwise contacts from hubspot
            {
                long totalProcessed = 0;
                long batchID = 1;
                int successful = 0;
                int failed = 0;
                var entity = "CONTACT";
                var module = "Contacts";
                try
                {
                    var hasMore = true;
                    var vidOffset = 0;
                    var strUrl = string.Empty;
                    JArray arr = null;

                    while (hasMore)
                    {
                        strUrl = $"{HubspotBaseUrl}/contacts/v1/lists/all/contacts/all?hapikey={HubspotAppKey}&count=100&vidOffset={vidOffset}{properties}";
                        var jRes = Common.GetApiResult(strUrl, entity);
                        
                        var response = GetHBAPIError(jRes, strUrl, entity);
                        if (!response)
                            break;

                        //var jr = JsonReader.ParseObject(jRes);
                        JObject jr = JObject.Parse(jRes);
                        arr = (JArray)jr["contacts"];
                        var vOffset = jr["vid-offset"];
                        var hasrecs = jr["has-more"].ToString();

                        vidOffset = Convert.ToInt32(vOffset.ToString());
                        hasMore = Convert.ToBoolean(hasrecs.ToString());

                        //get contacts of 100 batchsize
                        
                        if (arr.Count > 0)
                        {
                            ErrorLog.InfoMessage($"Total Contact in Hubspot {arr.Count} in Batch : {batchID}.");
                            Console.WriteLine($"Total Contact in Hubspot {arr.Count} in Batch : {batchID}.");
                            int counter = 1;
                            foreach (JObject JRecord in arr)
                            {
                                Console.WriteLine("__________________________________________");
                                Console.WriteLine($"Process started for contact {counter}.");

                                ErrorLog.InfoMessage($"------------------------------------------------------------------");
                                ErrorLog.InfoMessage($"----------- Details on {DateTime.Now.ToString()} -----------------");
                                ErrorLog.InfoMessage($"Process started for contact {counter}.");

                                JObject contactRec = JRecord;
                                var contactId = contactRec["vid"].ToString();
                                var contProp = (JObject)contactRec["properties"];

                                /*
                                //for testing purpose, after testing need to remove initialization of jsonarray
                                //var ja = new JArray();
                                contactId = "999051";
                                var urlContact = HubspotBaseUrl + "/contacts/v1/contact/vid/" + contactId + "/profile?hapikey=" + HubspotAppKey;
                                var jContact = Common.GetApiResult(urlContact, entity);
                                var jContResult = JObject.Parse(jContact);
                                var contProp = (JObject)jContResult["properties"];
                                */

                                //get engagements based on single contact id
                                // if engagement has attachment then get attachment Id
                                var ja = GetEntityEngagements(entity, contactId);
                                ErrorLog.InfoMessage($" {ja.Count} engagements found for contact {counter}.");
                                Console.WriteLine($" {ja.Count} engagements found for contact {counter}.");

                                var googleUploadedJA = new JArray();
                                foreach (JObject _ja in ja)
                                {
                                    var fileDetailJo = _ja["filedetails"];
                                    var _fileDetails = new JObject();
                                    var hasResult = fileDetailJo.IsNullOrEmpty();
                                    if (!hasResult)
                                    {
                                        _fileDetails = (JObject)fileDetailJo;
                                    }
                                    if (_fileDetails != null && _fileDetails.Count > 0)
                                    {
                                        var uploadedResp = common.MoveHubspotDataToGoogleAsync(_fileDetails);
                                        if(uploadedResp != null && uploadedResp["webViewLink"].ToString() == "")
                                        {
                                            if (uploadedResp["message"].ToString() == "")
                                            {
                                                ErrorLog.InfoMessage($"File is not successfully uploaded on Google drive");
                                                Common.Output("File is not successfully uploaded on Google drive");
                                            }
                                            else
                                            {
                                                ErrorLog.InfoMessage($"File is not successfully uploaded on Google drive due to : {uploadedResp["message"].ToString()}");
                                                Common.Output($"File is not successfully uploaded on Google drive due to : {uploadedResp["message"].ToString()}");
                                            }
                                        }
                                        googleUploadedJA.Add(uploadedResp);
                                    }
                                }
                                //for create contact in Ontraport
                                var hasCreated = op.CreateEntity(contProp, contactId, ja, module, googleUploadedJA);
                                if (hasCreated)
                                    successful++;
                                else
                                    failed++;

                                counter++;
                            }
                            batchID++;
                            totalProcessed += counter;
                        }
                        else
                        {
                            ErrorLog.InfoMessage($" Total Contact in Hubspot {arr.Count} in Batch {batchID}");
                            Console.WriteLine($" Total Contact in Hubspot {arr.Count} in Batch {batchID}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    ErrorLog.WriteLogFile(ex.Message, "Catch Exception", entity);
                }

                Console.WriteLine("__________________________________________");
                Console.WriteLine($"Total Contact Processed : {totalProcessed}");
                Console.WriteLine($"Contact Created Successfully Count : {successful}");
                Console.WriteLine($"Contact Creation Failed Count : {failed}");
                Console.WriteLine("__________________________________________");

                ErrorLog.InfoMessage($"Total Contact Processed : {totalProcessed} and Contact Created Successfully Count : {successful} and Contact Creation Failed Count : {failed}");
            }
        }

        public void MoveDealAsync()
        {
            Common common = new Common();
            Ontraport op = new Ontraport();
            op.populateDealField();
            var hubspotDealFields = Ontraport.hubspotDealField;
            var dealProperties = new StringBuilder();
            foreach (var _hubField in hubspotDealFields)
            {
                dealProperties.Append($"&properties={_hubField}");
            }

            if (dealProperties.Length == 0)
            {
                dealProperties.Append($"&properties=dealname&properties=amount");
            }

            var totalProcessed = 0;
            long batchID = 1;
            int successful = 0;
            int failed = 0;
            var errorLogJA = new JArray();
            var entity = "DEAL";
            var module = "Deals";
            try
            {
                var hasMore = true;
                var vidOffset = 0;
                var strUrl = string.Empty;
                JArray arr = null;

                while (hasMore)
                {
                    strUrl = $"{HubspotBaseUrl}/deals/v1/deal/paged?hapikey={HubspotAppKey}&count=100&offset={vidOffset}{dealProperties}";
                    var jRes = Common.GetApiResult(strUrl, entity);
                    var response = GetHBAPIError(jRes, strUrl, entity);
                    if (!response)
                        break;

                    var jr = JObject.Parse(jRes);
                    arr = (JArray)jr["deals"];
                    var vOffset = jr["offset"];
                    var hasrecs = jr["hasMore"].ToString();
                    vidOffset = Convert.ToInt32(vOffset.ToString());
                    hasMore = Convert.ToBoolean(hasrecs.ToString());

                    //get contacts of 100 batchsize
                    if (arr.Count > 0)
                    {
                        ErrorLog.InfoMessage($"Total Deals in Hubspot {arr.Count} in Batch : {batchID}");
                        Console.WriteLine($"Total Deals in Hubspot {arr.Count} in Batch : {batchID}");

                        int counter = 1;
                        foreach (var JRecord in arr)
                        {
                            Console.WriteLine("__________________________________________");
                            Console.WriteLine($"Process started for Deal {counter}");

                            ErrorLog.InfoMessage($"------------------------------------------------------------------");
                            ErrorLog.InfoMessage($"----------- Details on {DateTime.Now.ToString()} -----------------");
                            ErrorLog.InfoMessage($"Process started for Deal {counter}");

                            bool hasCreated = false;
                            JObject dealRec = (JObject)JRecord;
                            var dealId = dealRec["dealId"].ToString();
                            var dealProp = (JObject)dealRec["properties"];

                            //get engagements based on single contact id
                            // if engagement has attachment then get attachment Id

                            //var ja = new JArray();

                            var ja = GetEntityEngagements(entity, dealId);
                            ErrorLog.InfoMessage($"{ja.Count} engagements found for Deal {counter}");
                            Console.WriteLine($"{ja.Count} engagements found for Deal {counter}");

                            var googleUploadedJA = new JArray();
                            foreach (JObject _ja in ja)
                            {
                                var fileDetailJo = _ja["filedetails"];
                                var _fileDetails = new JObject();
                                var hasFileDetails = _fileDetails.IsNullOrEmpty();
                                if (!hasFileDetails)
                                {
                                    _fileDetails = (JObject)fileDetailJo;
                                }
                                if (_fileDetails != null && _fileDetails.Count > 0)
                                {
                                    var uploadedResp = common.MoveHubspotDataToGoogleAsync(_fileDetails);
                                    if (uploadedResp != null && uploadedResp["webViewLink"].ToString() == "")
                                    {
                                        if (uploadedResp["message"].ToString() == "")
                                        {
                                            ErrorLog.InfoMessage($"File is not successfully uploaded on Google drive");
                                            Common.Output("File is not successfully uploaded on Google drive");
                                        }
                                        else
                                        {
                                            ErrorLog.InfoMessage($"File is not successfully uploaded on Google drive due to : {uploadedResp["message"].ToString()}");
                                            Common.Output($"File is not successfully uploaded on Google drive due to : {uploadedResp["message"].ToString()}");
                                        }
                                    }
                                    googleUploadedJA.Add(uploadedResp);
                                }
                            }
                            //for create Deal in Ontraport
                            hasCreated = op.CreateEntity(dealProp, dealId, ja, module, googleUploadedJA);

                            if (hasCreated)
                                successful++;
                            else
                                failed++;
                            counter++;
                        }
                        batchID++;
                        totalProcessed += counter;
                    }
                    else
                    {
                        ErrorLog.InfoMessage($"Total Deals in Hubspot {arr.Count} in Batch {batchID}");
                        Console.WriteLine($"Total Deals in Hubspot {arr.Count} in Batch {batchID}");
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLog.WriteLogFile(ex.Message, "Catch Exception", entity);
            }

            Console.WriteLine("__________________________________________");
            Console.WriteLine($"Total Deals Processed : {totalProcessed}");
            Console.WriteLine($"Deals Created Successfully Count : {successful}");
            Console.WriteLine($"Deals Creation Failed Count : {failed}");
            Console.WriteLine("__________________________________________");

            ErrorLog.InfoMessage($"Total Deals Processed : {totalProcessed} and Deals Created Successfully Count : {successful} and Deals Creation Failed Count : {failed}");
        }

        public JArray GetEntityEngagements(string entity, string entityId)
        {
            var ja = new JArray();
            var previewFileUrl = "https://app.hubspot.com/file-preview";

            var urlEngage = $"{HubspotBaseUrl}/engagements/v1/engagements/associated/{entity}/{entityId}/paged?hapikey={HubspotAppKey}";
            var jEngage = Common.GetApiResult(urlEngage, entity);
            if (!string.IsNullOrEmpty(jEngage) && !jEngage.StartsWith("error"))
            {
                var joEngage = JObject.Parse(jEngage);
                var jaEngage = (JArray)joEngage["results"];
                if (jaEngage.Count > 0)
                {
                    foreach (var _ja in jaEngage)
                    {
                        var attchmentIds = new List<string>();

                        var fileJo = new JObject();
                        var jo = new JObject();
                        var disposition = string.Empty;
                        var engageType = string.Empty;
                        var body = string.Empty;
                        var callRecordingUrl = string.Empty;
                        var emailText = string.Empty;

                        jo["type"] = string.Empty;
                        jo["filedetails"] = fileJo;
                        jo["disposition"] = disposition;
                        jo["body"] = body;
                        jo["emailtext"] = emailText;
                        jo["recordingurl"] = callRecordingUrl;

                        var _jo = (JObject)_ja;
                        var engageJo = _jo["engagement"];
                        var hasResult = engageJo.IsNullOrEmpty();
                        if (!hasResult)
                        {
                            var _engage = (JObject)engageJo;
                            engageType = _engage["type"].ToString();
                            jo["type"] = engageType;

                            var bodyPreview = _engage["bodyPreview"];
                            var _hasResult = bodyPreview.IsNullOrEmpty();
                            if (!_hasResult)
                            {
                                jo["body"] = bodyPreview.ToString();
                            }
                        }

                        //get attchmentId and from attchmentId, get Attchmenturl
                        var attachmentJo = _jo["attachments"];
                        var hasAttachment = attachmentJo.IsNullOrEmpty();
                        if (!hasAttachment)
                        {
                            var attachments = ((JArray)attachmentJo);
                            if (attachments.Count > 0)
                            {
                                foreach (JObject _attchjo in attachments)
                                {
                                    attchmentIds.Add(_attchjo["id"].ToString());
                                }
                                if (attchmentIds.Count > 0)
                                {
                                    fileJo = GetEngagementUrl(attchmentIds[0], entity);
                                    if (fileJo.Count > 0)
                                    {
                                        var isHiddenFile = Convert.ToBoolean(fileJo["ishiddenfile"].ToString());
                                        if (isHiddenFile)
                                        {
                                            var previewUrl = $"{previewFileUrl}/{HubspotPortalId}/file/{attchmentIds[0]}";
                                            fileJo["hubspotfileurl"] = previewUrl;
                                        }
                                    }
                                    jo["filedetails"] = fileJo;
                                }
                            }
                        }

                        //get metadata of call recording url and email Text
                        var metadataJo = _jo["metadata"];
                        var hasMetadata = metadataJo.IsNullOrEmpty();
                        if (!hasMetadata)
                        {
                            var metadata = (JObject)metadataJo;
                            if (metadata != null)
                            {
                                var hasDisposition = metadata["disposition"].IsNullOrEmpty();
                                if (!hasDisposition)
                                {
                                    disposition = metadata["disposition"].ToString();
                                    jo["disposition"] = disposition;
                                }
                                var hasRecordingUrl = metadata["recordingUrl"].IsNullOrEmpty();
                                if (!hasRecordingUrl)
                                {
                                    callRecordingUrl = metadata["recordingUrl"].ToString();
                                    jo["recordingurl"] = callRecordingUrl;
                                }
                                var hasEmailText = metadata["text"].IsNullOrEmpty();
                                if (!hasEmailText)
                                {
                                    emailText = metadata["text"].ToString();
                                    jo["emailtext"] = emailText;
                                }
                            }
                        }
                        ja.Add(jo);
                    }
                }
            }
            else
            {
                ErrorLog.WriteLogFile($"Engagement retrieve failed from Hubspot : {jEngage}", urlEngage, entity);
                Console.WriteLine($"Engagement retrieve failed from Hubspot : {jEngage}");
            }

            return ja;
        }

        public JObject GetEngagementUrl(string attchmentId, string module)
        {
            var attachmentJO = new JObject
            {
                ["hubspotfileurl"] = string.Empty,
                ["ishiddenfile"] = false
            };

            var attachmentUrl = $"{HubspotBaseUrl}/filemanager/api/v2/files/{attchmentId}?hapikey={HubspotAppKey}";
            var response = Common.GetApiResult(attachmentUrl, module);
            if (!string.IsNullOrEmpty(response) && !response.StartsWith("error"))
            {
                var joFile = JObject.Parse(response);
                if (joFile != null)
                {
                    attachmentJO["hubspotfileurl"] = joFile["url"].ToString();
                    attachmentJO["ishiddenfile"] = Convert.ToBoolean(joFile["hidden"].ToString());
                }
            }

            return attachmentJO;
        }

        public bool GetHBAPIError(string jRes, string callUrl, string module)
        {
            var hasResponse = true;
            var statusCheck = JObject.Parse(jRes);
            if (statusCheck != null)
            {
                var hasStatusField = statusCheck["status"].IsNullOrEmpty();
                if (!hasStatusField)
                {
                    var status = statusCheck["status"].ToString();
                    if (status.ToLower() == "error")
                    {
                        hasResponse = false;
                        ErrorLog.WriteLogFile(statusCheck["message"].ToString(), callUrl, module);
                    }
                }
            }
            return hasResponse;
        }
    }
}
