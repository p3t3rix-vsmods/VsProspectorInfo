﻿using ProtoBuf;
using System;
using System.IO.Compression;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using ProspectorInfo.Map;
using System.Linq;

namespace ProspectorInfo.Utils
{
    internal class ChatDataSharing
    {
        private static readonly string CHAT_PREFIX = "ProspectingData;";

        private readonly ICoreClientAPI api;
        private readonly ProspectorOverlayLayer prospectorOverlayLayer;
        public ChatDataSharing(ICoreClientAPI api, ProspectorOverlayLayer prospectorOverlayLayer)
        {
            this.api = api;
            this.prospectorOverlayLayer = prospectorOverlayLayer;
            api.Event.ChatMessage += OnChatMessage;
        }

        private void OnChatMessage(int groupId, string message, EnumChatType chattype, string data) {
            if(chattype != EnumChatType.OthersMessage) 
                return;

            int startIdx = message.IndexOf(CHAT_PREFIX);
            if (startIdx == -1)
                return;

            var result = DeserializeFromBase64<ProspectorMessages>(message.Substring(startIdx + CHAT_PREFIX.Length));
            if (result != null)
                prospectorOverlayLayer.AddOrUpdateProspectingData(result.Values.ToArray());
        }

        public void ShareData(ProspectorMessages messages)
        {
            string data = SerializeToBase64<ProspectorMessages>(messages);
            if (data == null) 
            {
                return;
            }
            api.SendChatMessage(CHAT_PREFIX + data);
        }

        private string SerializeToBase64<T>(T data) {
            byte[] result;
            try
            {
                using (var resultStream = new MemoryStream())
                {
                    using (var compressionStream = new DeflateStream(resultStream, CompressionLevel.Optimal))
                    {
                        Serializer.Serialize(compressionStream, data);
                    }
                    result = resultStream.ToArray();
                    return Convert.ToBase64String(result);
                }
            }
            catch (Exception ex)
            {
                api.Logger.Error("Failed to serialize prospecting data", ex);
                return null;
            }
        }

        private T DeserializeFromBase64<T>(string message) where T : class 
        {
            try
            {
                using (var resultStream = new MemoryStream(Convert.FromBase64String(message)))
                {
                    using (var decompressionStream = new DeflateStream(resultStream, CompressionMode.Decompress))
                    {
                        return Serializer.Deserialize<T>(decompressionStream);
                    }
                }
            } 
            catch (Exception ex)
            {
                api.Logger.Error("Failed to deserialize shared prospecting data from chat", ex);
                return null;
            }
        }
    }
}
