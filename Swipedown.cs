using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;

public partial class TouchDetector : Form
{
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

    public const int VK_LBUTTON = 0x01;
    public const int VK_SHIFT = 0x10;
    public const int VK_TAB = 0x09;

    public const int KEYEVENTF_EXTENDEDKEY = 0x0001;
    public const int KEYEVENTF_KEYUP = 0x0002;

    private static readonly int TopScreenTolerance = 10;
    private static readonly int DragThreshold = 100;
    private static readonly int ProgramOpenDelay = 2000;
    private static readonly string SettingsFile = "settings.txt";
    private static readonly string RegistryPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
    private static readonly string TouchDetectorRegistryKey = "TouchDetector";

    private static bool isProgramOpen = false;
    private static bool isMouseDown = false;
    private static int startY = 0;

    private NotifyIcon trayIcon;
    private ContextMenuStrip trayMenu;
    private TextBox programPathTextBox;
    private CheckBox startupCheckBox;
    private RadioButton programRadioButton;
    private RadioButton keyCombinationRadioButton;
    private TextBox keyCombinationTextBox;

    private bool useProgramPath;

    private static byte[] trayIconBytes;

    public static void Main()
    {
        // Load the tray icon into a byte array
        string trayIconFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tray_icon.ico");
        try
        {
            trayIconBytes = File.ReadAllBytes(trayIconFilePath);
        }
        catch (Exception)
        {
            // Handle the case when the tray icon file is not found
            // You can set a default icon here or handle the error in any other appropriate way
            trayIconBytes = null;
        }

        Application.Run(new TouchDetector());
    }

    public TouchDetector()
    {
        InitializeTrayIcon();
        useProgramPath = GetSelectedOption();

        // Byte array representing the icon
        string iconFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "form_icon.ico");
        byte[] form_icon_bytes;
        try
        {
            form_icon_bytes = File.ReadAllBytes(iconFilePath);
        }
        catch (Exception)
        {
            // Handle the case when the icon file is not found
            // You can set a default icon here or handle the error in any other appropriate way
            form_icon_bytes = null;
        }
        this.Icon = form_icon_bytes != null ? new Icon(new MemoryStream(form_icon_bytes)) : SystemIcons.Application;

        InitializeWindow();
        StartTouchDetection();
    }

    private void InitializeTrayIcon()
    {
        trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Open", null, OpenWindow);
        trayMenu.Items.Add("Exit", null, ApplicationExit);

        trayIcon = new NotifyIcon
        {
            Text = "SwipeDown",
            ContextMenuStrip = trayMenu,
            Visible = true
        };

        if (trayIconBytes != null)
        {
            try
            {
                using (MemoryStream stream = new MemoryStream(trayIconBytes))
                {
                    trayIcon.Icon = new Icon(stream);
                }
            }
            catch (Exception)
            {
                // Handle the exception if the tray icon file is invalid or unsupported
                // You can set a default icon here or handle the error in any other appropriate way
                trayIcon.Icon = SystemIcons.Application;
            }
        }
        else
        {
            // Handle the case when the tray icon file is not found
            // You can set a default icon here or handle the error in any other appropriate way
            trayIcon.Icon = SystemIcons.Application;
        }
    }

    private void InitializeWindow()
    {
        this.Text = "Touch Top Function";
        this.Size = new Size(400, 250);
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.MaximizeBox = false;

        programRadioButton = new RadioButton
        {
            Location = new Point(20, 20),
            Size = new Size(200, 20),
            Text = "Open Program",
            Checked = useProgramPath
        };
        this.Controls.Add(programRadioButton);

        keyCombinationRadioButton = new RadioButton
        {
            Location = new Point(20, 50),
            Size = new Size(200, 20),
            Text = "Execute Key Combination",
            Checked = !useProgramPath
        };
        this.Controls.Add(keyCombinationRadioButton);

        programPathTextBox = new TextBox
        {
            Location = new Point(20, 80),
            Size = new Size(300, 20),
            Text = GetProgramPath()
        };
        this.Controls.Add(programPathTextBox);

        keyCombinationTextBox = new TextBox
        {
            Location = new Point(20, 110),
            Size = new Size(300, 20),
            Text = "Shift+Tab"
        };
        this.Controls.Add(keyCombinationTextBox);

        startupCheckBox = new CheckBox
        {
            Location = new Point(20, 140),
            Size = new Size(200, 20),
            Text = "Run at Startup",
            Checked = IsStartupEnabled()
        };
        this.Controls.Add(startupCheckBox);

        Button saveButton = new Button
        {
            Location = new Point(20, 170),
            Size = new Size(100, 30),
            Text = "Save"
        };
        saveButton.Click += SaveButton_Click;
        this.Controls.Add(saveButton);
    }

    private void SaveButton_Click(object sender, EventArgs e)
    {
        SaveSettings();
        useProgramPath = programRadioButton.Checked;
        SaveProgramPathAndSelectedOption();
        MessageBox.Show("Settings saved successfully.", "Save", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        SaveSettings();
    }

    private void OpenWindow(object sender, EventArgs e)
    {
        this.Show();
    }

    private void ApplicationExit(object sender, EventArgs e)
    {
        SaveSettings();
        trayIcon.Visible = false;
        Application.Exit();
    }

    private bool IsStartupEnabled()
    {
        return Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegistryPath, true).GetValue(TouchDetectorRegistryKey) != null;
    }

    private void SaveSettings()
    {
        SaveProgramPathAndSelectedOption();
        SaveStartupSettings();
    }

    private string GetProgramPath()
    {
        string programPath = string.Empty;
        try
        {
            if (File.Exists(SettingsFile))
            {
                string[] lines = File.ReadAllLines(SettingsFile);
                if (lines.Length > 0)
                {
                    programPath = lines[0];
                }
            }
        }
        catch (Exception)
        {
            // Handle any errors silently - ideally, you should log any errors that occur
        }
        return programPath;
    }

    private void SaveProgramPathAndSelectedOption()
    {
        try
        {
            string[] lines = new string[] { programPathTextBox.Text, useProgramPath ? "ProgramPath" : "KeyCombination" };
            File.WriteAllLines(SettingsFile, lines);
        }
        catch (Exception)
        {
            // Handle any errors silently - ideally, you should log any errors that occur
        }
    }

    private void SaveStartupSettings()
    {
        if (startupCheckBox.Checked)
        {
            Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegistryPath, true).SetValue(TouchDetectorRegistryKey, Application.ExecutablePath);
        }
        else
        {
            Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegistryPath, true).DeleteValue(TouchDetectorRegistryKey, false);
        }
    }

    private bool GetSelectedOption()
    {
        if (File.Exists(SettingsFile))
        {
            string[] lines = File.ReadAllLines(SettingsFile);
            if (lines.Length > 1)
            {
                return lines[1] == "ProgramPath";
            }
        }
        return true; // Default option
    }

    private void StartTouchDetection()
    {
        // Start the touch detection in a new thread
        Thread thread = new Thread(() =>
        {
            while (true)
            {
                // Check if the left mouse button is pressed
                if ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0)
                {
                    // Check if the click is at the very top of the screen
                    int y = Cursor.Position.Y;
                    if (y < TopScreenTolerance)
                    {
                        if (!isMouseDown)
                        {
                            // Mouse down event
                            isMouseDown = true;
                            startY = y;
                        }
                    }
                    else
                    {
                        if (isMouseDown && y - startY > DragThreshold) // Mouse drag down past threshold pixels
                        {
                            if (!isProgramOpen)
                            {
                                isProgramOpen = true;
                                if (programRadioButton.Checked)
                                {
                                    OpenProgram();
                                }
                                else
                                {
                                    ExecuteKeyCombination();
                                }
                                Thread.Sleep(ProgramOpenDelay); // Wait for delay time before allowing another action
                                isProgramOpen = false;
                            }
                        }
                    }
                }
                else
                {
                    // Mouse up event
                    isMouseDown = false;
                }
            }
        });

        thread.IsBackground = true;
        thread.Start();
    }

    private void OpenProgram()
    {
        string programPath = GetProgramPath();
        if (!string.IsNullOrEmpty(programPath))
        {
            Process.Start(programPath);
        }
    }

    private void ExecuteKeyCombination()
    {
        keybd_event(VK_SHIFT, 0, KEYEVENTF_EXTENDEDKEY | 0, 0); // Key down
        keybd_event(VK_TAB, 0, KEYEVENTF_EXTENDEDKEY | 0, 0); // Key down
        Thread.Sleep(100); // Delay to ensure Shift+Tab is pressed
        keybd_event(VK_TAB, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0); // Key up
        keybd_event(VK_SHIFT, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, 0); // Key up
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        this.Hide();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        this.Hide();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (this.WindowState == FormWindowState.Minimized)
        {
            this.Hide();
        }
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_SYSCOMMAND = 0x112;
        const int SC_MINIMIZE = 0xF020;
        const int SC_CLOSE = 0xF060;

        if (m.Msg == WM_SYSCOMMAND && (int)m.WParam == SC_MINIMIZE)
        {
            this.Hide();
            return;
        }
        else if (m.Msg == WM_SYSCOMMAND && (int)m.WParam == SC_CLOSE)
        {
            SaveSettings();
            trayIcon.Visible = false;
            Application.Exit();
            return;
        }

        base.WndProc(ref m);
    }
}
