using CoreMoveHubspotData;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace MoveHubspotToOntraport
{
    public class Ontraport
    {
        public static IConfigurationRoot app = AppConfiguration.GetConfig();


        public string OntraportBaseUrl = Convert.ToString(app["MySettings:OntraportUrl"]);
        public string OntraportAppKey = Convert.ToString(app["MySettings:OntraportAppKey"]);
        public string OntraportAppId = Convert.ToString(app["MySettings:OntraportAppId"]);
        public string HubspotPortalId = Convert.ToString(app["MySettings:HubspotPortalId"]);

        public JObject entityFieldsJo = new JObject();

        public static List<string> hubspotField = new List<string>();
        public List<string> ontraportField = new List<string>();

        public static List<string> hubspotDealField = new List<string>();
        public List<string> ontraportDealField = new List<string>();
        public string dealName = string.Empty;
        public string dealId = string.Empty;

        //for contacts Mapping Field
        public Ontraport()
        {
            hubspotField.Add("firstname");
            hubspotField.Add("lastname");
            hubspotField.Add("company");
            hubspotField.Add("email");
            hubspotField.Add("phone");
            hubspotField.Add("mobilephone");
            hubspotField.Add("fax");
            hubspotField.Add("date_of_birth");
            hubspotField.Add("jobtitle");
            hubspotField.Add("address");
            hubspotField.Add("city");
            hubspotField.Add("state");
            hubspotField.Add("zip");
            hubspotField.Add("country");
            hubspotField.Add("website");


            ontraportField.Add("firstname");
            ontraportField.Add("lastname");
            ontraportField.Add("company");
            ontraportField.Add("email");
            ontraportField.Add("sms_number");
            ontraportField.Add("office_phone");
            ontraportField.Add("fax");
            ontraportField.Add("birthday");
            ontraportField.Add("title");
            ontraportField.Add("address");
            ontraportField.Add("city");
            ontraportField.Add("state");
            ontraportField.Add("zip");
            ontraportField.Add("country");
            ontraportField.Add("website");
        }

        public void populateDealField()
        {
            hubspotDealField.Add("dealname");
            hubspotDealField.Add("amount");
            //hubspotDealField.Add("dealstage");
            //hubspotDealField.Add("email");
            //hubspotDealField.Add("phone");
            //hubspotDealField.Add("mobilephone");
            //hubspotDealField.Add("jobtitle");
            //hubspotDealField.Add("address");
            //hubspotDealField.Add("city");


            ontraportDealField.Add("name");
            ontraportDealField.Add("value");
            //ontraportDealField.Add("sales_stage");
            //ontraportDealField.Add("email");
            //ontraportDealField.Add("sms_number");
            //ontraportDealField.Add("office_phone");
            //ontraportDealField.Add("title");
            //ontraportDealField.Add("address");
            //ontraportDealField.Add("city");

        }

        public bool CreateEntity(JObject hubEntityProp, string hubspotEntityId, JArray ja, string entity, JArray googleUploadedJA)
        {
            var common = new Common();

            bool hasCreated = false;
            var method = "POST";
            var entityUrl = string.Empty;
            var module = "Contact";
            if (entity == "Contacts")
                entityUrl = $"{OntraportBaseUrl}/1/{entity}/saveorupdate";
            else
            {
                entityUrl = $"{OntraportBaseUrl}/1/{entity}";
                module = "Deal";
            }
            var body = CreateMappingField(hubEntityProp, hubspotEntityId, ja, entity, googleUploadedJA);

            //deal existing check before creation.
            if (entity == "Deals")
            {
                var arrRecord = new JArray();
                if (!string.IsNullOrEmpty(dealName))
                {
                    var condition = "[{ \"field\":{\"field\":\"name\"}, \"op\":\"=\", \"value\":{\"value\": \"" + dealName + "\"} }]";
                    var dealurl = $"{OntraportBaseUrl}/1/Deals?range=50&condition={condition}";
                    var _response = GetApiResult(dealurl);
                    if (!string.IsNullOrEmpty(_response) && !_response.StartsWith("error"))
                    {
                        var resp = JObject.Parse(_response);
                        var arr = resp["data"];
                        var hasData = arr.IsNullOrEmpty();
                        if (!hasData)
                        {
                            arrRecord = (JArray)arr;
                        }
                        if (arrRecord.Count > 0)
                        {
                            foreach (JObject _deal in arrRecord)
                            {
                                if (_deal != null)
                                {
                                    dealId = _deal["id"].ToString();
                                    method = "PUT";
                                    body.Append($"&id={dealId}");

                                    dealId = ""; dealName = "";
                                    continue;
                                }
                            }
                        }
                    }
                }
            }
            var response = common.PostApi(entityUrl, body.ToString(), out hasCreated, method, module);
            if (hasCreated)
            {
                var resp = JObject.Parse(response);
                var entityRespData = resp["data"];
                var Id = string.Empty;
                var hasResponseData = entityRespData.IsNullOrEmpty();
                if (!hasResponseData)
                {
                    if (entityRespData != null)
                    {
                        var _contData = (JObject)entityRespData;
                        var attrsData = _contData["attrs"];
                        var hasAttrData = attrsData.IsNullOrEmpty();
                        if (!hasAttrData)
                        {
                            Id = ((JObject)attrsData)["id"].ToString();
                            Console.WriteLine($"{module} {Id} updated Successfully.");
                            ErrorLog.InfoMessage($"{module} {Id} updated Successfully.");
                        }
                        else
                        {
                            Id = _contData["id"].ToString();
                            Console.WriteLine($"{module} {Id} created Successfully.");
                            ErrorLog.InfoMessage($"{module} {Id} created Successfully.");
                        }
                    }
                }
                if (!string.IsNullOrEmpty(Id))
                {
                    //Notes creation For contacts
                    if (entity == "Contacts")
                    {
                        var NotesJA = new JArray();
                        foreach (JObject _ja in ja)
                        {
                            var type = _ja["type"].ToString().ToLower();
                            var attachmentUrl = string.Empty;
                            var fileDetails = _ja["filedetails"];
                            var hasFileDetails = fileDetails.IsNullOrEmpty();
                            if (!hasFileDetails)
                            {
                                var _fileDetails = (JObject)fileDetails;
                                if (_fileDetails != null && _fileDetails.Count > 0)
                                    attachmentUrl = _fileDetails["hubspotfileurl"].ToString();
                            }
                            var data = _ja["body"].ToString();
                            var googleAttachmentUrl = string.Empty;
                            var bodyData = new StringBuilder();
                            if (type == "note" && entity == "Contacts")
                            {
                                if (!string.IsNullOrEmpty(data))
                                    bodyData.Append($"data={data}&contact_id={Id}&object_type_id=0");
                                else if (!string.IsNullOrEmpty(attachmentUrl))
                                {
                                    googleAttachmentUrl = googleUploadedJA.Where(x => ((JObject)x)["hubspotfileurl"].ToString() == attachmentUrl).Select(x => ((JObject)x)["webViewLink"].ToString()).FirstOrDefault();
                                    if (!string.IsNullOrEmpty(googleAttachmentUrl))
                                        bodyData.Append($"data={googleAttachmentUrl}&contact_id={Id}&object_type_id=0");
                                    else if (!string.IsNullOrEmpty(attachmentUrl))
                                        bodyData.Append($"data={attachmentUrl}&contact_id={Id}&object_type_id=0");
                                }
                                if (bodyData != null && bodyData.Length > 0)
                                {
                                    if (NotesJA.Count == 0)
                                        NotesJA = CheckExistingNotes(Id);

                                    if (!string.IsNullOrEmpty(googleAttachmentUrl))
                                        CreateNote(Id, bodyData, data, googleAttachmentUrl, NotesJA);
                                    else
                                        CreateNote(Id, bodyData, data, attachmentUrl, NotesJA);
                                }
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"{module} is not created Successfully.");
                    ErrorLog.InfoMessage($"{module} is not created Successfully.");
                }
            }
            return hasCreated;
        }

        public void CreateNote(string entityId, StringBuilder body, string data, string attachmentUrl, JArray NotesJA)
        {
            bool hasCreated = false;
            var notesId = string.Empty;
            var common = new Common();
            var method = "POST";
            var NoteUrl = $"{OntraportBaseUrl}/1/Notes";

            if (NotesJA.Count > 0)
            {
                foreach (JObject _note in NotesJA)
                {
                    if (_note != null)
                    {
                        notesId = _note["id"].ToString();
                        var notesDataRecord = _note["data"].ToString();
                        if (notesDataRecord.ToLower() == data.ToLower())
                        {
                            method = "PUT";
                            body.Append($"&id={notesId}");
                            break;
                        }
                        else if (HttpUtility.UrlDecode(notesDataRecord.ToLower()) == HttpUtility.UrlDecode(attachmentUrl.ToLower()))
                        {
                            method = "PUT";
                            body.Append($"&id={notesId}");
                            break;
                        }
                    }
                }
            }

            var response = common.PostApi(NoteUrl, body.ToString(), out hasCreated, method, "Note");
            if (hasCreated)
            {
                var resp = JObject.Parse(response);
                var notesData = resp["data"];
                var Id = string.Empty;
                var hasNotesData = notesData.IsNullOrEmpty();
                if (!hasNotesData)
                {
                    if (notesData != null)
                    {
                        var _notesData = (JObject)notesData;
                        var attrsData = _notesData["attrs"];
                        var hasNotesAttrData = attrsData.IsNullOrEmpty();
                        if (!hasNotesAttrData)
                        {
                            Id = ((JObject)attrsData)["id"].ToString();
                            Console.WriteLine($"Notes {Id} for Contact {entityId} updated Successfully.");
                            ErrorLog.InfoMessage($"Notes {Id} for Contact {entityId} updated Successfully.");
                        }
                        else
                        {
                            Id = _notesData["id"].ToString();
                            Console.WriteLine($"Notes {Id} for Contact {entityId} created Successfully.");
                            ErrorLog.InfoMessage($"Notes {Id} for Contact {entityId} created Successfully.");
                        }
                    }
                }
            }
            else
            {
                ErrorLog.InfoMessage($"Notes not created Successfully for contact {entityId}");
                Console.WriteLine($"Notes not created Successfully.");
            }
        }

        public StringBuilder CreateMappingField(JObject hubContactProp, string hubspotEntityId, JArray ja, string entity, JArray googleUploadedJA)
        {
            var bodyData = new StringBuilder();
            var counter = 0;

            var module = entity.ToLower() == "contacts" ? "Contact" : "Deal";
            var _module = module.ToLower();

            if (_module == "contact")
            {
                foreach (var field in ontraportField)
                {
                    var hubField = hubspotField[counter];
                    var hasHubField = hubContactProp[hubField].IsNullOrEmpty();
                    if (!hasHubField)
                    {
                        var fieldProp = hubContactProp[hubField];
                        var hasFieldProp = fieldProp.IsNullOrEmpty();
                        if (!hasFieldProp)
                        {
                            var _prop = (JObject)fieldProp;
                            if (_prop != null)
                            {
                                var fieldValue = _prop["value"].ToString();
                                if (!string.IsNullOrEmpty(fieldValue))
                                {
                                    if (bodyData.Length > 0)
                                        bodyData.Append($"&{field}={fieldValue}");
                                    else
                                        bodyData.Append($"{field}={fieldValue}");
                                }
                            }
                        }
                    }
                    counter++;
                }
            }
            else if (_module == "deal")
            {
                if (hubspotDealField.Count == 0 || ontraportDealField.Count == 0)
                    populateDealField();

                foreach (var field in ontraportDealField)
                {
                    var _field = field;
                    var hubField = hubspotDealField[counter];
                    bool hasHubField = hubContactProp[hubField].IsNullOrEmpty();
                    if (!hasHubField)
                    {
                        var fieldProp = hubContactProp[hubField];
                        bool hasFieldProp = fieldProp.IsNullOrEmpty();
                        if (!hasFieldProp)
                        {
                            var _prop = (JObject)fieldProp;
                            if (_prop != null)
                            {
                                var fieldValue = _prop["value"].ToString();
                                if (_field == "name")
                                {
                                    dealName = fieldValue;
                                }
                                if (!string.IsNullOrEmpty(fieldValue))
                                {
                                    if (bodyData.Length > 0)
                                        bodyData.Append($"&{field}={fieldValue}");
                                    else
                                        bodyData.Append($"{field}={fieldValue}");
                                }
                            }
                        }
                    }
                    counter++;
                }
            }

            if (entityFieldsJo != null && entityFieldsJo.Count == 0)
            {
                entityFieldsJo = GetEntityMeta(entity);
            }

            string hubspotContactUrlLinkField = string.Empty;
            foreach (var _field in entityFieldsJo)
            {
                var field = ((JObject)_field.Value)["alias"].ToString().ToLower();
                if (field.StartsWith($"hubspot direct"))
                {
                    hubspotContactUrlLinkField = _field.Key.ToString();
                    break;
                }
            }

            if (!string.IsNullOrEmpty(hubspotContactUrlLinkField))
            {
                var hubspotContactUrl = $"https://app.hubspot.com/contacts/{HubspotPortalId}/{_module}/{hubspotEntityId}";
                bodyData.Append($"&{hubspotContactUrlLinkField}={hubspotContactUrl}");
            }

            var totalNotesFieldList = new List<string>();
            var detailsNotesFieldList = new List<string>();
            var attachmentNotesFieldList = new List<string>();

            var totalCallsFieldList = new List<string>();
            var detailsCallsFieldList = new List<string>();
            var attachmentCallsFieldList = new List<string>();

            var totalEmailsFieldList = new List<string>();
            var detailsEmailsFieldList = new List<string>();
            var attachmentEmailsFieldList = new List<string>();

            var notesAttachmentCounter = 0;
            var callsDetailsCounter = 0;
            var callsAttacmentCounter = 0;
            var emailsDetailsCounter = 0;
            var emailsAttacmentCounter = 0;

            foreach (JObject _ja in ja)
            {
                var isAttachField = true;
                var field = string.Empty;
                var type = _ja["type"].ToString().ToLower();
                var attachmentUrl = string.Empty;
                var fileDetails = _ja["filedetails"];
                var hasFileDetails = fileDetails.IsNullOrEmpty();
                if (!hasFileDetails)
                {
                    var _fileDetails = (JObject)fileDetails;
                    if (_fileDetails != null && _fileDetails.Count > 0)
                        attachmentUrl = _fileDetails["hubspotfileurl"].ToString();
                }
                var bodyText = _ja["body"].ToString();
                var emailText = _ja["emailtext"].ToString();
                var recordingUrl = _ja["recordingurl"].ToString();
                var callrecordingField = string.Empty;

                if (type == "note")
                {
                    if (totalNotesFieldList.Count == 0)
                    {
                        totalNotesFieldList = GetFieldList(entityFieldsJo, type);
                        detailsNotesFieldList = GetFieldList(entityFieldsJo, type, false);
                        attachmentNotesFieldList = totalNotesFieldList.Except(detailsNotesFieldList).ToList();
                    }

                    if (!string.IsNullOrEmpty(attachmentUrl))
                    {
                        if (attachmentNotesFieldList.Count > 0)
                        {
                            field = attachmentNotesFieldList[0];
                            attachmentNotesFieldList.RemoveAt(0);
                        }
                        else if (totalNotesFieldList.Count > 0 && attachmentNotesFieldList.Count == 0)
                        {
                            notesAttachmentCounter++;
                        }
                    }
                    else
                    {
                        if (detailsNotesFieldList.Count > 0)
                        {
                            field = detailsNotesFieldList[0];
                            isAttachField = false;
                            detailsNotesFieldList.RemoveAt(0);
                        }
                    }
                }

                else if (type == "call" && _module == "contact")
                {
                    if (totalCallsFieldList.Count == 0)
                    {
                        GetFieldList(entityFieldsJo, type);
                        totalCallsFieldList = GetFieldList(entityFieldsJo, type);
                        detailsCallsFieldList = GetFieldList(entityFieldsJo, type, false);
                        attachmentCallsFieldList = totalCallsFieldList.Except(detailsCallsFieldList).ToList();
                    }
                    if (!string.IsNullOrEmpty(attachmentUrl) || !string.IsNullOrEmpty(recordingUrl))
                    {
                        if (attachmentCallsFieldList.Count > 0)
                        {
                            field = attachmentCallsFieldList[0];
                            if (!string.IsNullOrEmpty(recordingUrl))
                            {
                                isAttachField = false;
                                callrecordingField = field;
                            }
                            attachmentCallsFieldList.RemoveAt(0);
                        }
                        else if (totalCallsFieldList.Count > 0 && attachmentCallsFieldList.Count == 0)
                        {
                            callsAttacmentCounter++;
                        }

                    }
                    if (string.IsNullOrEmpty(attachmentUrl))
                    {
                        if (detailsCallsFieldList.Count > 0)
                        {
                            field = detailsCallsFieldList[0];
                            isAttachField = false;
                            detailsCallsFieldList.RemoveAt(0);
                        }
                        else if (totalCallsFieldList.Count > 0 && detailsCallsFieldList.Count == 0)
                        {
                            callsDetailsCounter++;
                        }
                    }
                }

                else if (type == "email" && _module == "contact")
                {
                    if (totalEmailsFieldList.Count == 0)
                    {
                        totalEmailsFieldList = GetFieldList(entityFieldsJo, type);
                        detailsEmailsFieldList = GetFieldList(entityFieldsJo, type, false);
                        attachmentEmailsFieldList = totalEmailsFieldList.Except(detailsEmailsFieldList).ToList();
                    }
                    if (!string.IsNullOrEmpty(attachmentUrl))
                    {
                        if (attachmentEmailsFieldList.Count > 0)
                        {
                            field = attachmentEmailsFieldList[0];
                            attachmentEmailsFieldList.RemoveAt(0);
                        }
                        else if (totalEmailsFieldList.Count > 0 && attachmentEmailsFieldList.Count == 0)
                        {
                            emailsAttacmentCounter++;
                        }
                    }
                    else
                    {
                        if (detailsEmailsFieldList.Count > 0)
                        {
                            field = detailsEmailsFieldList[0];
                            isAttachField = false;
                            detailsEmailsFieldList.RemoveAt(0);
                        }
                        else if (totalEmailsFieldList.Count > 0 && detailsEmailsFieldList.Count == 0)
                        {
                            emailsDetailsCounter++;
                        }
                    }
                }

                var googleAttachmentUrl = googleUploadedJA?.Where(x => ((JObject)x)["hubspotfileurl"].ToString() == attachmentUrl).Select(x => ((JObject)x)["webViewLink"].ToString()).FirstOrDefault();

                if ((type == "call" || type == "email") && !isAttachField && _module == "contact")
                {
                    if (type == "call")
                    {
                        if (!string.IsNullOrEmpty(bodyText) && !string.IsNullOrEmpty(field))
                            bodyData.Append($"&{field}={bodyText}");
                        if (!string.IsNullOrEmpty(recordingUrl) && !string.IsNullOrEmpty(callrecordingField))
                            bodyData.Append($"&{callrecordingField}={recordingUrl}");
                    }
                    else if (type == "email")
                    {
                        //if (!string.IsNullOrEmpty(emailText) && !string.IsNullOrEmpty(field))
                        //    bodyData.Append($"&{field}={emailText}");
                        if (!string.IsNullOrEmpty(bodyText) && !string.IsNullOrEmpty(field))
                            bodyData.Append($"&{field}={bodyText}");
                    }
                }
                else if (type == "note" && !isAttachField && _module == "deal")
                {
                    if (!string.IsNullOrEmpty(bodyText) && !string.IsNullOrEmpty(field))
                        bodyData.Append($"&{field}={bodyText}");
                }
                else
                {
                    //for attachment link we are storing in cutom field
                    if (!string.IsNullOrEmpty(googleAttachmentUrl) && !string.IsNullOrEmpty(field))
                    {
                        bodyData.Append($"&{field}={googleAttachmentUrl}");
                    }
                    else if (!string.IsNullOrEmpty(attachmentUrl) && !string.IsNullOrEmpty(field))
                    {
                        bodyData.Append($"&{field}={attachmentUrl}");
                    }
                }
            }

            if (notesAttachmentCounter > 0 || callsDetailsCounter > 0 || callsAttacmentCounter > 0 || emailsDetailsCounter > 0 || emailsAttacmentCounter > 0)
            {
                if (notesAttachmentCounter > 0)
                    ErrorLog.InfoMessage($"More Notes Attachments Field want for this {module} and count : {notesAttachmentCounter}");

                if (callsDetailsCounter > 0)
                    ErrorLog.InfoMessage($"More Call Details Field want for this {module} and count : {callsDetailsCounter}");

                if (callsAttacmentCounter > 0)
                    ErrorLog.InfoMessage($"More Calls Attachments Field want for this {module} and count : {callsAttacmentCounter}");

                if (emailsDetailsCounter > 0)
                    ErrorLog.InfoMessage($"More Email Details Field want for this {module} and count : {emailsDetailsCounter}");

                if (emailsAttacmentCounter > 0)
                    ErrorLog.InfoMessage($"More Email Attachments Field want for this {module} and count : {emailsAttacmentCounter}");
            }

            return bodyData;
        }

        //Common functions for getting Custom Fields
        public JObject GetEntityMeta(string entityName)
        {
            var common = new Common();
            var module = entityName.ToLower() == "contacts" ? "Contact" : "Deal";
            var entityFieldsJo = new JObject();
            try
            {
                var contactUrl = $"{OntraportBaseUrl}/1/{entityName}/meta?format=byName";
                var response = GetApiResult(contactUrl);
                if (!string.IsNullOrEmpty(response) && !response.StartsWith("error"))
                {
                    var responseJo = JObject.Parse(response);
                    if (responseJo != null)
                    {
                        var entityMetaResponse = responseJo["data"];
                        bool hasEntityFieldResp = entityMetaResponse.IsNullOrEmpty();
                        if (!hasEntityFieldResp)
                        {
                            var dataJo = (JObject)entityMetaResponse;
                            if (dataJo != null)
                            {
                                var entityJo = dataJo[module];
                                bool entityJoData = entityJo.IsNullOrEmpty();
                                if (!entityJoData)
                                {
                                    var fieldsJo = (JObject)entityJo;
                                    if (fieldsJo != null)
                                    {
                                        var _fieldsJo = fieldsJo["fields"];
                                        bool hasField = _fieldsJo.IsNullOrEmpty();
                                        if (!hasField)
                                        {
                                            var allFields = (JObject)_fieldsJo;
                                            if (allFields != null)
                                            {
                                                foreach (var _field in allFields)
                                                {
                                                    var fieldName = _field.Key;
                                                    var value = _field.Value;
                                                    var deletable = ((JObject)value)["deletable"].ToString();
                                                    if (fieldName.StartsWith("f") && deletable == "1")
                                                        entityFieldsJo.Add(_field.Key, _field.Value);
                                                }
                                            }
                                        }
                                    }
                                }

                            }
                        }
                    }
                }
                else
                {
                    Common.Output($"Entity Meta not Found for {module}");
                    entityFieldsJo = null;
                }
            }
            catch
            {
                Common.Output($"Entity Meta not Found for {module}");
                entityFieldsJo = null;
            }
            return entityFieldsJo;
        }

        public string GetApiResult(string strUrl)
        {
            var searchresult = string.Empty;
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(strUrl);
                req.Method = "GET";
                req.ContentType = "application/json";
                req.Headers.Add("Api-Appid", OntraportAppId);
                req.Headers.Add("Api-Key", OntraportAppKey);
                req.Accept = "application/json";

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
                    {
                        searchresult = sr.ReadToEnd();
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
                    searchresult = "error:" + respo;
                }
            }
            catch (Exception ex)
            {
                searchresult = "error:" + ex.Message;
            }
            return searchresult;
        }

        public JArray CheckExistingNotes(string entityId)
        {
            var notesJA = new JArray();

            var NoteUrl = $"{OntraportBaseUrl}/1/Notes";
            var condition = "[{ \"field\":{\"field\":\"contact_id\"}, \"op\":\"=\", \"value\":{\"value\": \"" + entityId + "\"} }]";
            var notesExisturl = $"{NoteUrl}?range=50&condition={condition}";
            var _response = GetApiResult(notesExisturl);
            if (!string.IsNullOrEmpty(_response) && !_response.StartsWith("error"))
            {
                var respObj = JObject.Parse(_response);
                var notesRespData = respObj["data"];
                var hasNotesResp = notesRespData.IsNullOrEmpty();
                if (!hasNotesResp)
                {
                    notesJA = (JArray)notesRespData;
                }
            }

            return notesJA;
        }

        public List<string> GetFieldList(JObject entityFieldsJo, string type, bool? isAttachField = null)
        {
            var fieldList = new List<string>();
            foreach (var _field in entityFieldsJo)
            {
                var field = ((JObject)_field.Value)["alias"].ToString().ToLower();
                if (isAttachField == null)
                {
                    if (field.StartsWith($"{type}s"))
                    {
                        fieldList.Add(_field.Key.ToString());
                    }
                }
                else if(isAttachField.HasValue && !isAttachField.Value)
                {
                    if (field.StartsWith($"{type}s") && !field.Contains("attachment"))
                    {
                        fieldList.Add(_field.Key.ToString());
                    }
                }
            }
            return fieldList;
        }
    }
}
