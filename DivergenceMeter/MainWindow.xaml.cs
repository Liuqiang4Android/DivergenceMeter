﻿using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Interop;
using System.IO;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Diagnostics;
using System.Threading;

namespace DivergenceMeter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region 私有变量
        private String _settingPath;
        private DateTime _nowTime;
        private DispatcherTimer _meterClock;
        private DispatcherTimer _worldLineChangeClock;
        private DispatcherTimer _loadTheApp;
        private DispatcherTimer _exitTheApp;
        private const int _minChangeCount = 50;
        private int _currentChangeCount = 0;
        private int _lastHeaderStatus = int.MaxValue;
        private bool _changeWroldStatus = false;
        private double _workAreaWidth = SystemParameters.WorkArea.Width;
        private double _workAreaHeight = SystemParameters.WorkArea.Height;
        private double _desktopWidth = SystemParameters.PrimaryScreenWidth;
        private double _desktopHeight = SystemParameters.PrimaryScreenHeight;
        private Setting MainSetting { get; set; }
        // 存放所有图片
        private BitmapImage[] AllImages { get; set; } = new BitmapImage[13];
        #endregion
        #region Window styles
        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hwnd, int index);

        [Flags]
        public enum ExtendedWindowStyles
        {
            WS_EX_TOOLWINDOW = 0x00000080,
            WS_EX_LAYERED = 0x80000,
            WS_EX_TRANSPARENT = 0x00000020
        }
        [Flags]
        public enum GetWindowLongFields
        {
            GWL_EXSTYLE = (-20),
        }
        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);


        public static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            int error = 0;
            IntPtr result = IntPtr.Zero;
            // Win32 SetWindowLong doesn't clear error on success
            SetLastError(0);

            if (IntPtr.Size == 4)
            {
                // use SetWindowLong
                Int32 tempResult = IntSetWindowLong(hWnd, nIndex, IntPtrToInt32(dwNewLong));
                error = Marshal.GetLastWin32Error();
                result = new IntPtr(tempResult);
            }
            else
            {
                // use SetWindowLongPtr
                result = IntSetWindowLongPtr(hWnd, nIndex, dwNewLong);
                error = Marshal.GetLastWin32Error();
            }

            if ((result == IntPtr.Zero) && (error != 0))
            {
                throw new System.ComponentModel.Win32Exception(error);
            }

            return result;
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr IntSetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern Int32 IntSetWindowLong(IntPtr hWnd, int nIndex, Int32 dwNewLong);

        private static int IntPtrToInt32(IntPtr intPtr)
        {
            return unchecked((int)intPtr.ToInt64());
        }

        [DllImport("kernel32.dll", EntryPoint = "SetLastError")]
        public static extern void SetLastError(int dwErrorCode);
        #endregion

        #region 主函数
        public MainWindow()
        {
            InitializeComponent();
        }
        #endregion
        #region 重写 OnSourceInitialized 函数
        /// <summary>
        /// 只有在 OnSourceInitialized 函数触发之后，才能获取到 Windows 句柄
        /// </summary>
        protected override void OnSourceInitialized(EventArgs e)
        {
            _settingPath = AppDomain.CurrentDomain.BaseDirectory + "setting.json";
            MainSetting = InitSetting();
            SettingWindowStatus(MainSetting);
            AllImages = LoadAllImage();

            StartWhenSystemStart(MainSetting.StartUpStatus);
            ClickWindowAndNotMouseEvent(MainSetting.ClickThroughStatus);
            WindowAwaysTopOrDesktop(MainSetting.AlwaysInTopStatus);
            (bool f, double left, double top) values =CheckPositonInEdge(MainSetting.EdgeAttachStatus, MainSetting.Left, MainSetting.Top, MainSetting.Width, MainSetting.Height);
            if (values.f)
            {
                this.Left = values.left;
                this.Top = values.top;
                MainSetting.Left = this.Left;
                MainSetting.Top = this.Top;
                Setting.SaveSetting($@"{_settingPath}", MainSetting);
            }
           
            _meterClock = new DispatcherTimer();
            _meterClock.Tick += new EventHandler(TheClock);
            _meterClock.Interval = new TimeSpan(0, 0, 0, 0, 100);

            _worldLineChangeClock = new DispatcherTimer();
            _worldLineChangeClock.Tick += new EventHandler(TheWorldLineChange);
            _worldLineChangeClock.Interval = new TimeSpan(0, 0, 0, 0, 30);
            _worldLineChangeClock.Start();

            _exitTheApp = new DispatcherTimer();
            _exitTheApp.Tick += new EventHandler(ExitTHeApp);
            _exitTheApp.Interval = new TimeSpan(0, 0, 0, 0, 60);


            _loadTheApp = new DispatcherTimer();
            _loadTheApp.Tick += new EventHandler(StartTheApp);
            _loadTheApp.Interval = new TimeSpan(0, 0, 0, 0, 60);
            _loadTheApp.Start();

            WindowInteropHelper wndHelper = new WindowInteropHelper(this);
            int exStyle = (int)GetWindowLong(wndHelper.Handle, (int)GetWindowLongFields.GWL_EXSTYLE);
            exStyle = exStyle | (int)ExtendedWindowStyles.WS_EX_TOOLWINDOW;
            SetWindowLong(wndHelper.Handle, (int)GetWindowLongFields.GWL_EXSTYLE, (IntPtr)exStyle);


        }
        #endregion
        #region 从配置文件中获取以保存的设置
        private Setting InitSetting()
        {
            
            Setting setting;
            if (File.Exists($@"{_settingPath}"))
            {
                Setting.LoadSetting($@"{_settingPath}", out setting);
            }
            else
            {
                Setting t = new Setting();
                setting = new Setting() {Left=(_desktopWidth - t.Width)/2, Top=(_desktopHeight - t.Height) /2};
            }
            return setting;
        }
        #endregion
        #region 初始化设置 UI 界面的状态
        private void SettingWindowStatus(Setting setting)
        {
            TheWindowXaml.Width = setting.Width;
            TheWindowXaml.Height = setting.Height;
            TheWindowXaml.Left = setting.Left;
            TheWindowXaml.Top = setting.Top;
            TheGridXaml.Opacity = setting.Opacity;

            item_a_xaml.IsChecked = setting.StartUpStatus;
            item_b_xaml.IsChecked = setting.ClickThroughStatus;
            item_c_xaml.IsChecked = setting.EdgeAttachStatus;
            item_d_xaml.IsChecked = setting.DragMoveStatus;
            item_e_xaml.IsChecked = setting.AlwaysInTopStatus;

            foreach (MenuItem i in item_f_xaml.Items)
            {
                var ItemOpacity = double.Parse(i.Opacity.ToString());
                if (Math.Abs(setting.Opacity - ItemOpacity) < 0.000001)
                {
                    i.IsChecked = true;
                }

            }

        }
        #endregion
        #region 时钟效果
        private void TheClock(object sender, EventArgs e)
        {
            _nowTime = DateTime.Now;
            var hour = _nowTime.Hour;
            var minute = _nowTime.Minute;
            var second = _nowTime.Second;
            var millisecond = _nowTime.Millisecond;
            var aa = hour < 10 ? 0 : hour / 10;
            var bb = hour < 10 ? hour : hour % 10;
            var cc = millisecond < 500 ? 11 : 12;
            var dd = minute < 10 ? 0 : minute / 10;
            var ee = minute < 10 ? minute : minute % 10;
            var ff = millisecond < 500 ? 11 : 12;
            var gg = second < 10 ? 0 : second / 10;
            var hh = second < 10 ? second : second % 10;
            A.Source = AllImages[aa];
            B.Source = AllImages[bb];
            C.Source = AllImages[cc];
            D.Source = AllImages[dd];
            E.Source = AllImages[ee];
            F.Source = AllImages[ff];
            G.Source = AllImages[gg];
            H.Source = AllImages[hh];
        }
        #endregion
        #region 世界线变动效果
        private void TheWorldLineChange(object sender, EventArgs e)
        {
            if (_currentChangeCount > _minChangeCount && (_lastHeaderStatus == 1 || _lastHeaderStatus == 0))
            {
                _currentChangeCount = 0;
                _lastHeaderStatus = int.MaxValue;
                _changeWroldStatus = false;
                _worldLineChangeClock.Stop();
                Thread.Sleep(2000);
                _meterClock.Start();
                return;
            }
            _currentChangeCount += 1;
            Random rnd = new Random();
            var header = rnd.Next(0, 9);
            _lastHeaderStatus = header;
            A.Source = AllImages[header];
            B.Source = AllImages[11];
            C.Source = AllImages[rnd.Next(0, 9)];
            D.Source = AllImages[rnd.Next(0, 9)];
            E.Source = AllImages[rnd.Next(0, 9)];
            F.Source = AllImages[rnd.Next(0, 9)];
            G.Source = AllImages[rnd.Next(0, 9)];
            H.Source = AllImages[rnd.Next(0, 9)];
        }
        #endregion
        #region 加载所有图片
        private BitmapImage[] LoadAllImage()
        {
            BitmapImage[] images = new BitmapImage[13];
            int[] num = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

            for (int i = 0; i < images.Length; i++)
            {
                String imagePath = $@"/images/{i}.png";
                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(imagePath, UriKind.RelativeOrAbsolute);
                bi.EndInit();
                images[i] = bi;
            }
            return images;
        }
        #endregion
        #region 开机自启
        private void StartWhenSystemStart(bool flag)
        {
            String programName = "DivergenceMeter";
            String programPath = Process.GetCurrentProcess().MainModule.FileName;
            RegistryKey reg = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            
            if (flag)
            {
                reg.SetValue(programName, programPath);
            }
            else
            {
                reg.DeleteValue(programName, false);
            }
        }

        private void RefreshSystem()
        {
            throw new NotImplementedException();
        }
        #endregion
        #region 窗体拖动和边缘吸附
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_changeWroldStatus == true) return;
            if (MainSetting.DragMoveStatus)
            {
                DragMove();
                (bool f, double left, double top) values = CheckPositonInEdge(MainSetting.EdgeAttachStatus, this.Left, this.Top, this.Width, this.Height);
                if (values.f)
                {
                    this.Left = values.left;
                    this.Top = values.top;
                }
                MainSetting.Left = this.Left;
                MainSetting.Top = this.Top;

            }
        }
        #endregion
        #region 判断是否能边缘吸附并设置其坐标
        public (bool,double,double) CheckPositonInEdge(bool flag, double left, double top,double width, double height)
        {
            bool f = false;
            if (flag)
            {
                if (left < 0)
                {
                    left = 0;
                    f = true;
                }
                if (top < 0)
                {
                    top = 0;
                    f = true;
                }
                if (left + width > _workAreaWidth)
                {
                    left = _workAreaWidth - width;
                    f = true;
                }
                if (top + height > _workAreaHeight)
                {
                    top = _workAreaHeight - height;
                    f = true;
                }
            }
            return (f, left, top);

        }
        #endregion
        #region 点击穿透功能
        public void ClickWindowAndNotMouseEvent(bool flag)
        {
            // Get this window's handle
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (flag)
            {
               SetWindowLong(hwnd, (int)GetWindowLongFields.GWL_EXSTYLE, (int)ExtendedWindowStyles.WS_EX_TRANSPARENT);
            } else
            {
               SetWindowLong(hwnd, (int)GetWindowLongFields.GWL_EXSTYLE, (int)ExtendedWindowStyles.WS_EX_LAYERED);
            }

        }
        #endregion
        #region 窗口始终在桌面或在顶层
        public void WindowAwaysTopOrDesktop(bool flag)
        {
            TheWindowXaml.Topmost = flag == true;
        }
        #endregion
        #region 小托盘各项功能
        private void StartUpInWindow(object sender, RoutedEventArgs e)
        {
            MainSetting.StartUpStatus = !MainSetting.StartUpStatus;
            StartWhenSystemStart(MainSetting.StartUpStatus);
            MenuItem item = sender as MenuItem;
            item.IsChecked = MainSetting.StartUpStatus;

            Setting.SaveSetting($@"{_settingPath}", MainSetting);
        }
        private void ClickThrough(object sender, RoutedEventArgs e)
        {
            MainSetting.ClickThroughStatus = !MainSetting.ClickThroughStatus;
            ClickWindowAndNotMouseEvent(MainSetting.ClickThroughStatus);

            MenuItem item = sender as MenuItem;
            item.IsChecked = MainSetting.ClickThroughStatus;
            // 点击穿透 和 允许拖动互斥
            if (MainSetting.ClickThroughStatus == true)
            {
                MainSetting.DragMoveStatus = false;
                item_d_xaml.IsChecked = false;
            }

            Setting.SaveSetting($@"{_settingPath}", MainSetting);
        }
        private void EdgeAttach(object sender, RoutedEventArgs e)
        {
            MainSetting.EdgeAttachStatus = !MainSetting.EdgeAttachStatus;
            MenuItem item = sender as MenuItem;
            item.IsChecked = MainSetting.EdgeAttachStatus;

            (bool f, double left, double top) values = CheckPositonInEdge(MainSetting.EdgeAttachStatus, this.Left, this.Top, this.Width, this.Height);
            if (values.f)
            {
                this.Left = values.left;
                this.Top = values.top;
                MainSetting.Left = this.Left;
                MainSetting.Top = this.Top;
            }

            Setting.SaveSetting($@"{_settingPath}", MainSetting);
        }
        private void AllowDragMove(object sender, RoutedEventArgs e)
        {
            MainSetting.DragMoveStatus = !MainSetting.DragMoveStatus;
            MenuItem item = sender as MenuItem;
            item.IsChecked = MainSetting.DragMoveStatus;
            if (MainSetting.DragMoveStatus == true)
            {
                MainSetting.ClickThroughStatus = false;
                item_b_xaml.IsChecked = false;
                ClickWindowAndNotMouseEvent(MainSetting.ClickThroughStatus);
            }

            Setting.SaveSetting($@"{_settingPath}", MainSetting);
        }
        private void AlwaysInTop(object sender, RoutedEventArgs e)
        {
            MainSetting.AlwaysInTopStatus = !MainSetting.AlwaysInTopStatus;
            WindowAwaysTopOrDesktop(MainSetting.AlwaysInTopStatus);
            MenuItem item = sender as MenuItem;
            item.IsChecked = MainSetting.AlwaysInTopStatus;

            Setting.SaveSetting($@"{_settingPath}", MainSetting);
        }
        private void ChangeOpacity(object sender, RoutedEventArgs e)
        {
            MenuItem pItem = sender as MenuItem;
            foreach(MenuItem i in pItem.Items)
            {
                i.IsChecked = false;
            }
            MenuItem selectedItem = e.OriginalSource as MenuItem;
            selectedItem.IsChecked = !selectedItem.IsChecked;
            MainSetting.Opacity = double.Parse(selectedItem.Opacity.ToString());
            TheGridXaml.Opacity = MainSetting.Opacity;

            Setting.SaveSetting($@"{_settingPath}", MainSetting);
        }
        // 退出应用
        // 配合 app.xaml 中 ShutdownMode 属性使用
        private void ClickToExit(object sender, RoutedEventArgs e)
        {
            _exitTheApp.Start();
        }
        #endregion
        #region 激发世界线效果
        private void TheWroldChange(object sender, MouseButtonEventArgs e)
        {
            _meterClock.Stop();
            _worldLineChangeClock.Start();
            _changeWroldStatus = true;
        }
        #endregion

        private void StartTheApp(object sender, EventArgs e)
        {
            if (TheWindowXaml.Opacity < 1)
            {

                TheWindowXaml.Opacity += 0.1;
            }
            else
            {
                TheWindowXaml.Opacity = 1;
                _loadTheApp.Stop();
                
            }
        }
        private void ExitTHeApp(object sender, EventArgs e)
        {
            if (TheWindowXaml.Opacity > 0)
            {

                TheWindowXaml.Opacity -= 0.1;
            }
            else
            {
                _exitTheApp.Stop();
                Setting.SaveSetting($@"{_settingPath}", MainSetting);
                Application.Current.Shutdown();
            }
        }
    }
}