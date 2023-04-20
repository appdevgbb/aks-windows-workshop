# Text Sentiment Analysis 

The following example walks through adding the ability to analyze messages written to a chat room and if the message has positive sentiment then a smiley emoji will be automatically appended to the message.

## Hands on Lab

To get started follow these steps:

1. In the Azure Portal Create a new instance of "Text Analytics API".

2. Once providioning is complete retrieve the Endpoint URL and one of the Access Keys from the portal. You'll use these in the code below.

3. Open the ChatService class at Jabbr\Services\ChatService.cs

4. Add the following method:
```cs
        private async Task<string> AnalyzeMessageAndAddEmoji(string message)
        {
            try
            {
                HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "<Insert API Key>");

                var uriBase = "<Insert Text Analysis API URL>";

                string uri = uriBase + "/sentiment";

                HttpResponseMessage response;
                string body = "{\"documents\": [{\"language\": \"en\",\"id\": \"test\",\"text\": \"" + message + "\"}]}";

                response = await client.PostAsync(uri, new StringContent(body, System.Text.Encoding.UTF8, "application/json")).ConfigureAwait(continueOnCapturedContext: false); ;

                string contentString = await response.Content.ReadAsStringAsync().ConfigureAwait(continueOnCapturedContext: false);
                
                //In real life you'd probably use something like Newtonsoft Json here rather than substring
                int score = Convert.ToInt32(contentString.Substring(25, 2));

                if (score>75)
                {
                    message = message + " :-)";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex);
                throw;
            }
            return message;
        }
```

5. Modify the 'AddMessage' method in the same class to call the AnalyzeMessageAndAddEmoji method

```cs
        public ChatMessage AddMessage(ChatUser user, ChatRoom room, string id, string content)
        {
            //Analyze message content and append emoji
            content = AnalyzeMessageAndAddEmoji(content).Result;
                                   
            var chatMessage = new ChatMessage
            {
                Id = id,
                User = user,
                Content = content,
                When = DateTimeOffset.UtcNow,
                Room = room,
                HtmlEncoded = false
            };
```

6. Build and Run, or build and push to your Azure App Service. 

7. Open a chat room and test by typing a positive message.