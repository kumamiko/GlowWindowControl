using System;
using System.Windows.Controls;
using GlowWindowControl.Import;

namespace GlowWindowControl.Glow
{
    internal delegate void SideGlowResizeEventHandler(object sender, SideGlowResizeArgs args);

    internal class SideGlowResizeArgs : EventArgs
    {
        private readonly Dock _side;
        private readonly HitTest _mode;

        public Dock Side
        {
            get { return _side; }
        }

        public HitTest Mode
        {
            get { return _mode; }
        }

        internal SideGlowResizeArgs(Dock side, HitTest mode)
        {
            _side = side;
            _mode = mode;
        }
    }
}
