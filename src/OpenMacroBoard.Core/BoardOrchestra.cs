using OpenMacroBoard.SDK;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenMacroBoard.Core
{
    public class BoardOrchestra
    {
        private readonly IMacroBoard board;

        public BoardOrchestra(IMacroBoard board)
        {
            this.board = board;
        }

        public void AddKeyController(int keyIndex, IKeyController keyController)
        {
            if (keyIndex < 0 || keyIndex > board.Keys.Count)
            {
                throw new IndexOutOfRangeException();
            }

            if (keyController is null)
            {
                throw new ArgumentNullException(nameof(keyController));
            }

            var host = new KeyControllerHost(board, keyIndex);
            keyController.Attach(host);
        }
    }

    internal class KeyControllerHost : IKeyControllerHost
    {
        private readonly IMacroBoard board;
        private readonly int keyId;

        public KeyControllerHost(IMacroBoard board, int keyId)
        {
            this.board = board;
            this.keyId = keyId;

            KeyWidth = board.Keys[keyId].Width;
            KeyHeight = board.Keys[keyId].Height;
        }

        public int KeyWidth { get; }
        public int KeyHeight { get; }

        public void SetKeyBitmap(KeyBitmap bitmapData)
        {
            board.SetKeyBitmap(keyId, bitmapData);
        }
    }

    public interface IKeyController
    {
        void Attach(IKeyControllerHost host);
        void Detach();
        void KeyEvent(bool pressed);
    }

    public interface IKeyControllerHost
    {
        int KeyWidth { get; }
        int KeyHeight { get; }
        void SetKeyBitmap(KeyBitmap bitmapData);
    }
}
