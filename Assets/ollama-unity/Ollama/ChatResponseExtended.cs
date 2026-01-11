using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace ollama
{
    /// <summary>Simple request structure for chat endpoint</summary>
    [Serializable]
    public class SimpleChatRequest
    {
        public string model;
        public List<SimpleChatMessage> messages;
        public bool stream;
        public int keep_alive;
    }

    [Serializable]
    public class SimpleChatMessage
    {
        public string role;
        public string content;
        public string[] images;
    }

    /// <summary>Extended response with full metadata</summary>
    [Serializable]
    public class ChatResponseExtended
    {
        public string model;
        public SimpleChatMessage message;
        
        [JsonProperty("created_at")]
        public string createdAt;
        
        [JsonProperty("done_reason")]
        public string doneReason;
        
        public bool done;
        
        [JsonProperty("prompt_eval_count")]
        public int prompt_eval_count;
        
        [JsonProperty("eval_count")]
        public int eval_count;
        
        [JsonProperty("prompt_eval_duration")]
        public long prompt_eval_duration;
        
        [JsonProperty("eval_duration")]
        public long eval_duration;
        
        [JsonProperty("total_duration")]
        public long total_duration;
        
        [JsonProperty("load_duration")]
        public long load_duration;
    }

    public static class OllamaExtensions
    {
        private const string SERVER = "http://localhost:11434/";
        private const string CHAT_ENDPOINT = "api/chat";

        /// <summary>Chat with full metadata response</summary>
        public static async Task<ChatResponse> ChatWithMetadataExt(
            string model, 
            string prompt, 
            int keep_alive = 300, 
            Texture2D image = null)
        {
            try
            {
                // Instead of using Ollama's chat, we'll manage our own simple history
                // This is a workaround since Ollama.Chat's internal classes are private
                var message = new SimpleChatMessage
                {
                    role = "user",
                    content = prompt,
                    images = image != null ? new[] { Texture2Base64(image) } : null
                };

                var request = new SimpleChatRequest
                {
                    model = model,
                    messages = new List<SimpleChatMessage> { message },
                    stream = false,
                    keep_alive = keep_alive
                };

                string payload = JsonConvert.SerializeObject(request);

                HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create($"{SERVER}{CHAT_ENDPOINT}");
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Method = "POST";

                using (var streamWriter = new StreamWriter(await httpWebRequest.GetRequestStreamAsync()))
                    await streamWriter.WriteAsync(payload);

                string result;
                using (var httpResponse = await httpWebRequest.GetResponseAsync())
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                    result = await streamReader.ReadToEndAsync();

                var fullResponse = JsonConvert.DeserializeObject<ChatResponseExtended>(result);

                return new ChatResponse
                {
                    content = fullResponse.message.content,
                    model = fullResponse.model,
                    promptEvalCount = fullResponse.prompt_eval_count,
                    promptEvalDuration = fullResponse.prompt_eval_duration,
                    evalCount = fullResponse.eval_count,
                    evalDuration = fullResponse.eval_duration,
                    totalDuration = fullResponse.total_duration,
                    loadDuration = fullResponse.load_duration
                };
            }
            catch (WebException webEx)
            {
                string errorResponse = "";
                if (webEx.Response != null)
                {
                    using (var errorStream = webEx.Response.GetResponseStream())
                    using (var reader = new StreamReader(errorStream))
                        errorResponse = await reader.ReadToEndAsync();
                }
                
                Debug.LogError($"[OllamaExt] HTTP Error: {webEx.Message}\n{errorResponse}");
                return CreateErrorResponse(webEx.Message);
            }
            catch (Exception e)
            {
                Debug.LogError($"[OllamaExt] Error: {e.Message}\n{e.StackTrace}");
                return CreateErrorResponse(e.Message);
            }
        }

        private static string Texture2Base64(Texture2D texture, bool fullQuality = true)
        {
            if (texture == null) return null;
            return Convert.ToBase64String(fullQuality ? texture.EncodeToPNG() : texture.EncodeToJPG());
        }

        private static ChatResponse CreateErrorResponse(string error)
        {
            return new ChatResponse
            {
                content = $"Error: {error}",
                model = "error",
                promptEvalCount = 0,
                evalCount = 0,
                promptEvalDuration = 0,
                evalDuration = 0,
                totalDuration = 0,
                loadDuration = 0
            };
        }
    }

    /// <summary>Response from Chat with metadata</summary>
    [Serializable]
    public class ChatResponse
    {
        public string content;
        public string model;
        
        public int promptEvalCount;
        public int evalCount;
        
        public long promptEvalDuration;
        public long evalDuration;
        public long totalDuration;
        public long loadDuration;

        public double PromptEvalSeconds => promptEvalDuration / 1_000_000_000.0;
        public double EvalSeconds => evalDuration / 1_000_000_000.0;
        public double TotalSeconds => totalDuration / 1_000_000_000.0;
        public double LoadSeconds => loadDuration / 1_000_000_000.0;

        public int TotalTokens => promptEvalCount + evalCount;
    }
}