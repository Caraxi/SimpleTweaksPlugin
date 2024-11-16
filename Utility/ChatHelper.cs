using System;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;

// Original:
//      https://git.anna.lgbt/ascclemens/XivCommon/src/branch/main/XivCommon/Functions/Chat.cs

namespace SimpleTweaksPlugin.Utility;

/// <summary>
/// A class containing chat functionality
/// </summary>
public static class ChatHelper {
    /// <summary>
    /// <para>
    /// Send a given message to the chat box. <b>This can send chat to the server.</b>
    /// </para>
    /// <para>
    /// This method will throw exceptions for certain inputs that the client can't
    /// normally send, but it is still possible to make mistakes. Use with caution.
    /// </para>
    /// </summary>
    /// <param name="message">message to send</param>
    /// <exception cref="ArgumentException">If <paramref name="message"/> is empty, longer than 500 bytes in UTF-8, or contains invalid characters.</exception>
    /// <exception cref="InvalidOperationException">If the signature for this function could not be found -or- The UiModule is currently unavailable</exception>
    public static unsafe void SendMessage(string message) {
        var utf8 = Utf8String.FromString(message);

        try {
            if (utf8->Length == 0) {
                throw new ArgumentException("message is empty", nameof(message));
            }

            if (utf8->Length > 500) {
                throw new ArgumentException("message is longer than 500 bytes", nameof(message));
            }

            var oldLength = utf8->Length;

            utf8->SanitizeString(0x27F, null);

            if (utf8->Length != oldLength) {
                throw new ArgumentException($"message contained invalid characters", nameof(message));
            }

            var uiModule = UIModule.Instance();
            if (uiModule == null) {
                throw new InvalidOperationException("The UiModule is currently unavailable");
            }

            uiModule->ProcessChatBoxEntry(utf8);
        } finally {
            if (utf8 != null) {
                utf8->Dtor(true);
            }
        }
    }
}