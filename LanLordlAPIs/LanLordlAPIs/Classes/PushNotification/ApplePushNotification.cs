using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Script.Serialization;
using Newtonsoft.Json;

namespace LanLordlAPIs.Classes.PushNotification
{
    public static class ApplePushNotification
    {
        public static string SendNotificationMessage(string alertText, int badge, string sound, string devicetokens, string username, string password)
        {
            // Sample JSON Input...
            // string json = "{\"aps\":{\"badge\":356,\"alert\":\"this 4 rd post\"},\"device_tokens\":[\"DC59F629CBAF8D88418C9FCD813F240B72311C6EDF27FAED0F5CB4ADB9F4D3C9\"]}";

            string json = new JavaScriptSerializer().Serialize(new
            {
                app_id = username,
                isIos = true,
                include_ios_tokens = new string[] { devicetokens },
                contents = new GameThriveMsgContent() { en = alertText }
            });

            var cli = new WebClient();
            cli.Headers[HttpRequestHeader.ContentType] = "application/json";

            string response = cli.UploadString("https://gamethrive.com/api/v1/notifications", json);
            GameThriveResponseClass gamethriveresponse = JsonConvert.DeserializeObject<GameThriveResponseClass>(response);

            return "1";
        }
    }
    public class GameThriveMsgContent
    {
        public string en;
    }

    public class GameThriveResponseClass
    {
        public string id;
        public int recipients;
    }
}