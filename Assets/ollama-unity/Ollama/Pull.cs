using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace ollama
{
    public static partial class Ollama
    {
        /// <summary>
        /// Pull (download) a model from the Ollama registry.
        /// Streams progress updates via the onProgress callback.
        /// </summary>
        /// <param name="modelName">Name of the model to pull, e.g. "llama3:8b"</param>
        /// <param name="onProgress">Optional callback receiving status and progress (0-100)</param>
        /// <returns>True if the pull completed successfully</returns>
        public static async Task<bool> Pull(string modelName, Action<string, float> onProgress = null)
        {
            var request = new Request.Pull(modelName);
            var payload = JsonConvert.SerializeObject(request);
            bool success = false;

            await PostRequestStream<Response.Pull>(payload, Endpoints.PULL, chunk =>
            {
                float progress = chunk.total > 0
                    ? (float)chunk.completed / chunk.total * 100f
                    : 0f;

                onProgress?.Invoke(chunk.status, progress);

                if (chunk.done || chunk.status == "success")
                    success = true;
            });

            return success;
        }
    }
}
