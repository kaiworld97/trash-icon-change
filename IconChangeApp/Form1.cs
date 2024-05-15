using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace IconChangeApp
{
    public partial class Form1 : Form
    {
        private static IntPtr _hookID = IntPtr.Zero;
        const string RecycleBinIconRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\CLSID\{645FF040-5081-101B-9F08-00AA002F954E}\DefaultIcon";

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SystemParametersInfo(uint uiAction, uint uiParam, string pvParam, uint fWinIni);

        private const uint SPI_SETICONS = 0x0058;
        private const uint SPIF_UPDATEINIFILE = 0x01;
        private const uint SPIF_SENDCHANGE = 0x02;

        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;


        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern void SHUpdateRecycleBinIcon();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;

        public Form1()
        {
            InitializeComponent();
            _hookID = SetHook(Proc);

            // 트레이 메뉴 설정
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("종료", null, OnExit);

            // 트레이 아이콘 설정
            trayIcon = new NotifyIcon()
            {
                ContextMenuStrip = trayMenu,
                Icon = new Icon("C:\\Users\\hcy19\\source\\repos\\IconChangeApp\\IconChangeApp\\Resources\\icon.ico"),  // 아이콘 파일 경로 지정
                Visible = true,
                Text = "아이콘 컨트롤"
            };

            trayIcon.DoubleClick += TrayIcon_DoubleClick;

            // 폼 로드 이벤트 추가
            Load += MainForm_Load;
            Shown += MainForm_Shown;
            this.FormClosing += new FormClosingEventHandler(Form1_FormClosing); // FormClosing 이벤트 핸들러 연결
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            Hide(); // 폼을 로드할 때 숨깁니다.
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            Hide(); // 폼을 로드할 때 숨깁니다.
        }

        private void TrayIcon_DoubleClick(object sender, EventArgs e)
        {
            this.Show(); // 폼을 보여줍니다.
            this.WindowState = FormWindowState.Normal; // 폼을 정상 상태로 복원합니다.
        }

        private void OnExit(object sender, EventArgs e)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RecycleBinIconRegistryPath, true))
            {
                if (key != null)
                {
                    key.SetValue("empty", $"%SystemRoot%\\trash-icons\\empty.ico,0", RegistryValueKind.ExpandString);
                    key.SetValue("full", $"%SystemRoot%\\trash-icons\\full.ico,0", RegistryValueKind.ExpandString);
                    RefreshIcons();
                }
            }
            trayIcon.Visible = false; // 트레이 아이콘 숨기기
            Application.Exit(); // 애플리케이션 종료
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 사용자가 폼을 닫으려 할 때 발생하는 이벤트를 취소하여 폼이 종료되지 않도록 설정
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true; // 폼 종료 이벤트를 취소
                this.Hide(); // 폼을 숨깁니다
                trayIcon.ShowBalloonTip(1000, "애플리케이션 실행 중", "애플리케이션이 여전히 트레이에서 실행 중입니다.", ToolTipIcon.Info);
            }
        }

        private static IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr Proc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                if (wParam == (IntPtr)WM_LBUTTONDOWN)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        SetRecycleBinIcon("drag");
                    });
                }
                else if (wParam == (IntPtr)WM_LBUTTONUP)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        RestoreRecycleBinIcon();
                    });
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private static void SetRecycleBinIcon(string state)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RecycleBinIconRegistryPath, true))
            {
                if (key != null)
                {
                    key.SetValue("empty", $"%SystemRoot%\\trash-icons\\{state}.ico,0", RegistryValueKind.ExpandString);
                    key.SetValue("full", $"%SystemRoot%\\trash-icons\\{state}.ico,0", RegistryValueKind.ExpandString);
                    RefreshIcons();
                }
            }
        }

        private static void RestoreRecycleBinIcon()
        {
            SetRecycleBinIcon("full");
        }



        private static void RefreshIcons()
        {
            SystemParametersInfo(SPI_SETICONS, 0, null, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
            SHUpdateRecycleBinIcon();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct SHQUERYRBINFO
        {
            public int cbSize;
            public long i64Size;
            public long i64NumItems;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        static extern uint SHQueryRecycleBin(string pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

        private void button1_Click(object sender, EventArgs e)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RecycleBinIconRegistryPath, true))
            {
                if (key != null)
                {
                    key.SetValue("empty", $"%SystemRoot%\\trash-icons\\empty.ico,0", RegistryValueKind.ExpandString);
                    key.SetValue("full", $"%SystemRoot%\\trash-icons\\full.ico,0", RegistryValueKind.ExpandString);
                    RefreshIcons();
                }
            }
        }

    }
}
