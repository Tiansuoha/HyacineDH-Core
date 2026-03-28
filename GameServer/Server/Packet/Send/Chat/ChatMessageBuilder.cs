using HyacineCore.Server.Proto;
using HyacineCore.Server.Util;

namespace HyacineCore.Server.GameServer.Server.Packet.Send.Chat;

public static class ChatMessageBuilder
{
    public static ChatMessageData BuildText(uint senderUid, string text, ulong? createTime = null)
    {
        var chatData = new ChatData { MessageText = text };
        var messageData = new MessageChatData
        {
            MessageType = MsgType.CustomText,
            ChatData = chatData
        };

        var chat = new ChatMessageData
        {
            MessageType = MsgType.CustomText,
            Content = text,
            SenderId = senderUid,
            CreateTime = createTime ?? (ulong)Extensions.GetUnixSec(),
            ChatData = chatData,
            MessageData = messageData,
            SourceData = new SourceData { Uid = senderUid }
        };

        // Some clients read repeated message payloads instead of the singular field.
        chat.MessageChatDataList.Add(messageData);
        return chat;
    }

    public static ChatMessageData BuildEmoji(uint senderUid, uint extraId, ulong? createTime = null)
    {
        var chatData = new ChatData { ExtraId = extraId };
        var messageData = new MessageChatData
        {
            MessageType = MsgType.Emoji,
            ChatData = chatData
        };

        var chat = new ChatMessageData
        {
            MessageType = MsgType.Emoji,
            ExtraId = extraId,
            SenderId = senderUid,
            CreateTime = createTime ?? (ulong)Extensions.GetUnixSec(),
            ChatData = chatData,
            MessageData = messageData,
            SourceData = new SourceData { Uid = senderUid }
        };

        // Some clients read repeated message payloads instead of the singular field.
        chat.MessageChatDataList.Add(messageData);
        return chat;
    }
}
