using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using PEPAR.Modules.Debug;

namespace PEPAR.Modules.Text
{
    internal sealed class DebugTextOverlay
    {
        private const int MaxMessages = 8;
        private readonly object _syncRoot = new();
        private readonly List<string> _messages = [];
        private readonly TextDisplayer _textDisplayer = new();
        private readonly TextDisplayStyle _style = new()
        {
            Color = Color.Cyan,
            ShadowEnabled = true,
            ShadowColor = new Color(0, 0, 32, 220),
            ShadowOffset = new Vector2(1f, 1f)
        };

        internal void Load(ContentManager content)
        {
            _textDisplayer.Load(content);

            lock (_syncRoot)
            {
                _messages.Clear();
                foreach (var entry in Debug_Logger.GetEntries().TakeLast(MaxMessages))
                {
                    _messages.Add(FormatEntry(entry));
                }
            }

            Debug_Logger.EntryLogged += HandleEntryLogged;
        }

        internal void Unload()
        {
            Debug_Logger.EntryLogged -= HandleEntryLogged;
        }

        internal void Draw(SpriteBatch spriteBatch)
        {
            List<string> messages;
            lock (_syncRoot)
            {
                messages = _messages.ToList();
            }

            if (messages.Count == 0)
            {
                return;
            }

            var position = new Vector2(16f, 16f);
            var lineHeight = _textDisplayer.GetLineHeight(_style.Scale) + 4f;

            foreach (var message in messages)
            {
                _textDisplayer.Draw(spriteBatch, message, position, _style);
                position.Y += lineHeight;
            }
        }

        private void HandleEntryLogged(DebugLogEntry entry)
        {
            lock (_syncRoot)
            {
                _messages.Add(FormatEntry(entry));
                if (_messages.Count > MaxMessages)
                {
                    _messages.RemoveAt(0);
                }
            }
        }

        private static string FormatEntry(DebugLogEntry entry)
        {
            return $"[{entry.Timestamp:HH:mm:ss}] [{entry.Level}] {entry.Message}";
        }
    }
}
