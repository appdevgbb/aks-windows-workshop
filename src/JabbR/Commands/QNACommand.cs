using JabbR.Models;
using Microsoft.AspNet.SignalR;
using System.Net;
using System.IO;
using Nancy.Json;
using Newtonsoft.Json;
using System.Configuration;
using System;

namespace JabbR.Commands
{
    [Command("qna", "QNA_CommandInfo", "question", "user")]
    public class QNACommand : UserCommand
    {

        string uri = ConfigurationManager.AppSettings["qnaendpoint"];
        string qnakey = ConfigurationManager.AppSettings["qnakey"];
        public override void Execute(CommandContext context, CallerContext callerContext, ChatUser callingUser, string[] args)
        {
            if (String.IsNullOrEmpty(callerContext.RoomName))
            {
                throw new HubException(LanguageResources.InvokeFromRoomRequired);
            }

            if (args == null || args.Length < 1)
            {
                throw new HubException("Please enter a question");
            }

            if (args.Length != 1)
            {
                throw new HubException("Please just enter one question");
            }

            var httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";
            httpWebRequest.Headers.Add("Ocp-Apim-Subscription-Key", qnakey);
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            string result = "";
            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                string json = new JavaScriptSerializer().Serialize(new
                {
                    question = args[0]
                });
                streamWriter.Write(json);
                streamWriter.Flush();
                streamWriter.Close();
            }

            var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                result = streamReader.ReadToEnd();
            }
            string message = "";
            try
            {
                dynamic converted = JsonConvert.DeserializeObject<dynamic>(result);
                message = converted["answers"][0].answer.ToString();

            }
            catch (Exception ex)
            {
                message = ex.Message;
            }
            ChatRoom callingRoom = context.Repository.GetRoomByName(callerContext.RoomName);



            context.NotificationService.GenerateMeme(callingUser, callingRoom, message);
        }
    }
}