﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Media;
using GlowWindowControl.Extensions;
using GlowWindowControl.Import;
using Color = System.Drawing.Color;
using Pen = System.Drawing.Pen;

namespace GlowWindowControl.Glow
{
    /// <summary>
    /// A SideGlow window is a layered window that
    /// renders the "glowing effect" on one of the sides.
    /// </summary>
    internal class SideGlow : IDisposable
    {
        #region private

        private const int CornerArea = 20;

        private const int ErrorClassAlreadyExists = 1410;
        private bool _disposed;
        private IntPtr _handle;
        private readonly IntPtr _parentHandle;
        private WndProcHandler _wndProcDelegate;

        const int AcSrcOver = 0x00;
        const int AcSrcAlpha = 0x01;

        private const int Size = 9;
        private readonly Dock _side;
        private const SetWindowPosFlags NoSizeNoMove = SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOMOVE;

        private bool _parentWindowIsFocused;
        private readonly List<System.Windows.Media.Color> _activeColors = new List<System.Windows.Media.Color>();
        private readonly List<System.Windows.Media.Color> _inactiveColors = new List<System.Windows.Media.Color>();
        private readonly List<byte> _alphas = new List<byte>();
        private System.Windows.Media.Color _activeColor = Colors.Yellow;
        private System.Windows.Media.Color _inactiveColor = Colors.LightGray;
        private BLENDFUNCTION _blend;
        private POINT _ptZero = new POINT(0, 0);
        private readonly Color _transparent = Color.FromArgb(0);

        private readonly IntPtr _noTopMost = new IntPtr(-2);
        private readonly IntPtr _yesTopMost = new IntPtr(-1);

        #endregion

        #region constuctor

        internal SideGlow(Dock side, IntPtr parent)
        {
            _side = side;
            _parentHandle = parent;

            _blend = new BLENDFUNCTION
            {
                BlendOp = AcSrcOver,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = AcSrcAlpha
            };

            InitializeAlphas();
            InitializeColors();

            CreateWindow("GlowSide_" + side + "_" + parent);
        }

        #endregion

        #region internal

        internal bool ExternalResizeEnable { get; set; }

        internal event SideGlowResizeEventHandler MouseDown;

        internal void SetSize(int width, int height)
        {
            if (_side == Dock.Top || _side == Dock.Bottom)
            {
                height = Size;
                width = width + Size * 2;
            }
            else
            {
                width = Size;
                height = height + Size * 2;
            }

            const SetWindowPosFlags flags = (SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOACTIVATE);
            User32.SetWindowPos(_handle, new IntPtr(-2), 0, 0, width, height, flags);
            Render();
        }

        internal void SetLocation(WINDOWPOS pos)
        {
            int left = 0;
            int top = 0;
            switch (_side)
            {
                case Dock.Top:
                    left = pos.x - Size;
                    top = pos.y - Size;
                    break;
                case Dock.Bottom:
                    left = pos.x - Size;
                    top = pos.y + pos.cy;
                    break;
                case Dock.Left:
                    left = pos.x - Size;
                    top = pos.y - Size;
                    break;
                case Dock.Right:
                    left = pos.x + pos.cx;
                    top = pos.y - Size;
                    break;
            }

            UpdateZOrder(left, top, SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);
        }

        internal void UpdateZOrder(int left, int top, SetWindowPosFlags flags)
        {
            User32.SetWindowPos(_handle, !IsTopMost ? _noTopMost : _yesTopMost, left, top, 0, Size, flags);
            User32.SetWindowPos(_handle, _parentHandle, 0, 0, 0, Size, NoSizeNoMove | SetWindowPosFlags.SWP_NOACTIVATE);
        }

        internal void UpdateZOrder()
        {
            User32.SetWindowPos(_handle, !IsTopMost ? _noTopMost : _yesTopMost, 0, 0, 0, Size, NoSizeNoMove | SetWindowPosFlags.SWP_NOACTIVATE);
            User32.SetWindowPos(_handle, _parentHandle, 0, 0, 0, Size, NoSizeNoMove | SetWindowPosFlags.SWP_NOACTIVATE);
        }

        internal System.Windows.Media.Color InactiveColor
        {
            get { return _inactiveColor; }
            set
            {
                _inactiveColor = value;
                InitializeColors();
                Render();
            }
        }

        internal System.Windows.Media.Color ActiveColor
        {
            get
            {
                return _activeColor;
            }
            set
            {
                _activeColor = value;
                InitializeColors();
                Render();
            }
        }

        internal IntPtr Handle
        {
            get { return _handle; }
        }

        internal bool ParentWindowIsFocused
        {
            set
            {
                _parentWindowIsFocused = value;
                Render();
            }
        }

        internal bool IsTopMost { get; set; }

        internal void Show(bool show)
        {
            const int swShowNoActivate = 4;
            //const int swShowActivate = 5;
            User32.ShowWindow(_handle, show ? swShowNoActivate : 0);
        }

        internal void Close()
        {
            User32.CloseWindow(_handle);
            User32.SetParent(_handle, IntPtr.Zero);
            User32.DestroyWindow(_handle);
        }

        #endregion

        #region private

        private void CreateWindow(string className)
        {
            if (className == null) throw new Exception("class_name is null");
            if (className == String.Empty) throw new Exception("class_name is empty");

            _wndProcDelegate = CustomWndProc;

            // Create WNDCLASS
            WNDCLASS windClass = new WNDCLASS
            {
                lpszClassName = className,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate)
            };

            ushort classAtom = User32.RegisterClassW(ref windClass);

            int lastError = Marshal.GetLastWin32Error();

            if (classAtom == 0 && lastError != ErrorClassAlreadyExists)
            {
                throw new Exception("Could not register window class");
            }

            const UInt32 extendedStyle = (UInt32)(
                WindowExStyles.WS_EX_LEFT |
                WindowExStyles.WS_EX_LTRREADING |
                WindowExStyles.WS_EX_RIGHTSCROLLBAR |
                WindowExStyles.WS_EX_TOOLWINDOW);

            const UInt32 style = (UInt32)(
                WindowStyles.WS_CLIPSIBLINGS |
                WindowStyles.WS_CLIPCHILDREN |
                WindowStyles.WS_POPUP);

            // Create window
            _handle = User32.CreateWindowExW(
                extendedStyle,
                className,
                className,
                style,
                0,
                0,
                0,
                0,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero
            );

            if (_handle == IntPtr.Zero)
            {
                return;
            }

            uint styles = User32.GetWindowLong(_handle, GetWindowLongFlags.GWL_EXSTYLE);
            styles = styles | (int)WindowExStyles.WS_EX_LAYERED;
            User32.SetWindowLong(_handle, GetWindowLongFlags.GWL_EXSTYLE, styles);
        }

        private IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg.Is(WindowsMessages.WM_LBUTTONDOWN))
            {
                CastMouseDown();
            }
            if (msg.Is(WindowsMessages.WM_SETCURSOR))
            {
                SetCursor();
                return new IntPtr(1);
            }

            return User32.DefWindowProcW(hWnd, msg, wParam, lParam);
        }

        private Bitmap GetBitmap(int width, int height)
        {
            Bitmap bmp;
            switch (_side)
            {
                case Dock.Top:
                case Dock.Bottom:
                    bmp = new Bitmap(width, Size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    break;
                case Dock.Left:
                case Dock.Right:
                    bmp = new Bitmap(Size, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            Graphics g = Graphics.FromImage(bmp);
            List<System.Windows.Media.Color> colorMap = _parentWindowIsFocused ? _activeColors : _inactiveColors;

            if (_side == Dock.Top || _side == Dock.Bottom)
            {
                for (int i = 0; i < _alphas.Count; i++)
                {
                    Color color = Color.FromArgb(_alphas[i], colorMap[i].R, colorMap[i].G, colorMap[i].B);
                    Pen pen = new Pen(color);
                    int y = (_side == Dock.Top) ? Size - 1 - i : i;
                    const int xLeft = Size * 2 - 1;
                    int xRight = width - Size * 2;
                    g.DrawLine(pen, new Point(xLeft, y), new Point(xRight, y));
                    double a = _alphas[i] / (Size + i);
                    for (int j = 0; j < Size - 1; j++)
                    {
                        double al = Math.Max(0, _alphas[i] - a * j);
                        color = Color.FromArgb((int)al, colorMap[i].R, colorMap[i].G, colorMap[i].B);
                        System.Drawing.Brush b = new SolidBrush(color);
                        g.FillRectangle(b, xLeft - 1 - j, y, 1, 1);
                        g.FillRectangle(b, xRight + 1 + j, y, 1, 1);
                    }
                    for (int j = Size - 1; j < Size + 1 + i; j++)
                    {
                        double al = Math.Max(0, _alphas[i] - a * j) / 2;
                        color = Color.FromArgb((int)al, colorMap[i].R, colorMap[i].G, colorMap[i].B);
                        System.Drawing.Brush b = new SolidBrush(color);
                        g.FillRectangle(b, xLeft - 1 - j, y, 1, 1);
                        g.FillRectangle(b, xRight + 1 + j, y, 1, 1);
                    }
                }
            }
            else
            {
                for (int i = 0; i < _alphas.Count; i++)
                {
                    Color color = Color.FromArgb(_alphas[i], colorMap[i].R, colorMap[i].G, colorMap[i].B);
                    Pen pen = new Pen(color);
                    int x = (_side == Dock.Right) ? i : Size - i - 1;
                    const int yTop = Size * 2;
                    int yBottom = height - Size * 2 - 1;
                    g.DrawLine(pen, new Point(x, yTop), new Point(x, yBottom));

                    double a = _alphas[i] / (Size + i);
                    for (int j = 0; j < Size; j++)
                    {
                        double al = Math.Max(0, _alphas[i] - a * j);
                        color = Color.FromArgb((int)al, colorMap[i].R, colorMap[i].G, colorMap[i].B);
                        System.Drawing.Brush b = new SolidBrush(color);
                        g.FillRectangle(b, x, yTop - 1 - j, 1, 1);
                        g.FillRectangle(b, x, yBottom + 1 + j, 1, 1);
                    }
                    for (int j = Size; j < Size + i; j++)
                    {
                        double al = Math.Max(0, _alphas[i] - a * j) / 2;
                        color = Color.FromArgb((int)al, colorMap[i].R, colorMap[i].G, colorMap[i].B);
                        System.Drawing.Brush b = new SolidBrush(color);
                        g.FillRectangle(b, x, yTop - 1 - j, 1, 1);
                        g.FillRectangle(b, x, yBottom + 1 + j, 1, 1);
                    }
                }
            }

            g.Flush();
            return bmp;
        }

        private void Render()
        {
            RECT rect = new RECT();
            User32.GetWindowRect(_handle, ref rect);

            int width = rect.right - rect.left;
            int height = rect.bottom - rect.top;
            if (width == 0 || height == 0) return;

            POINT newLocation = new POINT(rect.left, rect.top);
            SIZE newSize = new SIZE(width, height);
            IntPtr screenDc = User32.GetDC(IntPtr.Zero);
            IntPtr memDc = Gdi32.CreateCompatibleDC(screenDc);
            using (Bitmap bmp = GetBitmap(width, height))
            {
                IntPtr hBitmap = bmp.GetHbitmap(_transparent);
                IntPtr hOldBitmap = Gdi32.SelectObject(memDc, hBitmap);

                User32.UpdateLayeredWindow(_handle, screenDc, ref newLocation, ref newSize, memDc, ref _ptZero, 0, ref _blend, 0x02);

                User32.ReleaseDC(IntPtr.Zero, screenDc);
                if (hBitmap != IntPtr.Zero)
                {
                    Gdi32.SelectObject(memDc, hOldBitmap);
                    Gdi32.DeleteObject(hBitmap);
                }
            }
            Gdi32.DeleteDC(memDc);
        }

        private void InitializeAlphas()
        {
            _alphas.Clear();
            _alphas.Add(64);
            _alphas.Add(46);
            _alphas.Add(25);
            _alphas.Add(19);
            _alphas.Add(10);
            _alphas.Add(07);
            _alphas.Add(02);
            _alphas.Add(01);
            _alphas.Add(00); // transparent
        }

        private void InitializeColors()
        {
            _activeColors.Clear();
            _inactiveColors.Clear();

            for (int i = 0; i < Size; i++)
            {
                _activeColors.Add(_activeColor);
                _inactiveColors.Add(_inactiveColor);
            }
        }

        private void SetCursor()
        {
            if (!ExternalResizeEnable)
            {
                return;
            }

            IntPtr handle = User32.LoadCursor(IntPtr.Zero, (int)IdcStandardCursors.IDC_HAND);
            HitTest mode = GetResizeMode();
            switch (mode)
            {
                case HitTest.HTTOP:
                case HitTest.HTBOTTOM:
                    handle = User32.LoadCursor(IntPtr.Zero, (int)IdcStandardCursors.IDC_SIZENS);
                    break;
                case HitTest.HTLEFT:
                case HitTest.HTRIGHT:
                    handle = User32.LoadCursor(IntPtr.Zero, (int)IdcStandardCursors.IDC_SIZEWE);
                    break;
                case HitTest.HTTOPLEFT:
                case HitTest.HTBOTTOMRIGHT:
                    handle = User32.LoadCursor(IntPtr.Zero, (int)IdcStandardCursors.IDC_SIZENWSE);
                    break;
                case HitTest.HTTOPRIGHT:
                case HitTest.HTBOTTOMLEFT:
                    handle = User32.LoadCursor(IntPtr.Zero, (int)IdcStandardCursors.IDC_SIZENESW);
                    break;
            }

            if (handle != IntPtr.Zero)
            {
                User32.SetCursor(handle);
            }
        }

        private void CastMouseDown()
        {
            if (!ExternalResizeEnable)
            {
                return;
            }

            HitTest mode = GetResizeMode();
            if (MouseDown != null)
            {
                SideGlowResizeArgs args = new SideGlowResizeArgs(_side, mode);
                MouseDown(this, args);
            }
        }

        private POINT GetRelativeMousePosition()
        {
            POINT point = new POINT();
            User32.GetCursorPos(ref point);
            User32.ScreenToClient(_handle, ref point);
            return point;
        }

        private HitTest GetResizeMode()
        {
            HitTest mode = HitTest.HTNOWHERE;

            RECT rect = new RECT();
            POINT point = GetRelativeMousePosition();
            User32.GetWindowRect(_handle, ref rect);
            switch (_side)
            {
                case Dock.Top:
                    int width = rect.right - rect.left;
                    if (point.x < CornerArea) mode = HitTest.HTTOPLEFT;
                    else if (point.x > width - CornerArea) mode = HitTest.HTTOPRIGHT;
                    else mode = HitTest.HTTOP;
                    break;
                case Dock.Bottom:
                    width = rect.right - rect.left;
                    if (point.x < CornerArea) mode = HitTest.HTBOTTOMLEFT;
                    else if (point.x > width - CornerArea) mode = HitTest.HTBOTTOMRIGHT;
                    else mode = HitTest.HTBOTTOM;
                    break;
                case Dock.Left:
                    int height = rect.bottom - rect.top;
                    if (point.y < CornerArea) mode = HitTest.HTTOPLEFT;
                    else if (point.y > height - CornerArea) mode = HitTest.HTBOTTOMLEFT;
                    else mode = HitTest.HTLEFT;
                    break;
                case Dock.Right:
                    height = rect.bottom - rect.top;
                    if (point.y < CornerArea) mode = HitTest.HTTOPRIGHT;
                    else if (point.y > height - CornerArea) mode = HitTest.HTBOTTOMRIGHT;
                    else mode = HitTest.HTRIGHT;
                    break;
            }

            return mode;
        }

        #endregion

        #region Dispose

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;
            if (_handle == IntPtr.Zero) return;

            User32.DestroyWindow(_handle);
            _handle = IntPtr.Zero;
        }

        #endregion
    }
}
