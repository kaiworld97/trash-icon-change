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

            // Ʈ���� �޴� ����
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("����", null, OnExit);

            // Ʈ���� ������ ����
            trayIcon = new NotifyIcon()
            {
                ContextMenuStrip = trayMenu,
                Icon = new Icon("C:\\Users\\hcy19\\source\\repos\\IconChangeApp\\IconChangeApp\\Resources\\icon.ico"),  // ������ ���� ��� ����
                Visible = true,
                Text = "������ ��Ʈ��"
            };

            trayIcon.DoubleClick += TrayIcon_DoubleClick;

            // �� �ε� �̺�Ʈ �߰�
            Load += MainForm_Load;
            Shown += MainForm_Shown;
            this.FormClosing += new FormClosingEventHandler(Form1_FormClosing); // FormClosing �̺�Ʈ �ڵ鷯 ����
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            Hide(); // ���� �ε��� �� ����ϴ�.
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            Hide(); // ���� �ε��� �� ����ϴ�.
        }

        private void TrayIcon_DoubleClick(object sender, EventArgs e)
        {
            this.Show(); // ���� �����ݴϴ�.
            this.WindowState = FormWindowState.Normal; // ���� ���� ���·� �����մϴ�.
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
            trayIcon.Visible = false; // Ʈ���� ������ �����
            Application.Exit(); // ���ø����̼� ����
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // ����ڰ� ���� ������ �� �� �߻��ϴ� �̺�Ʈ�� ����Ͽ� ���� ������� �ʵ��� ����
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true; // �� ���� �̺�Ʈ�� ���
                this.Hide(); // ���� ����ϴ�
                trayIcon.ShowBalloonTip(1000, "���ø����̼� ���� ��", "���ø����̼��� ������ Ʈ���̿��� ���� ���Դϴ�.", ToolTipIcon.Info);
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
