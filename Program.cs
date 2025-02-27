﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Timers.Timer;
using Microsoft.Win32.SafeHandles;

namespace HDD_Ping
{
  static class Program
  {
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);
      Application.Run(new HDDPing());
    }
  }

  internal class HDDPing : Form
  {
    public const uint GENERIC_READ = 0x80000000;
    public const uint FILE_SHARE_READ = 1;
    public const uint FILE_SHARE_WRITE = 2;
    public const uint OPEN_EXISTING = 3;
    public const uint FILE_BEGIN = 0;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile
    );

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetFilePointerEx(
        IntPtr hFile,
        long liDistanceToMove,
        IntPtr lpNewFilePointer,
        uint dwMoveMethod);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadFile(
        IntPtr hFile,                   // handle to file
        byte[] lpBuffer,                // data buffer
        uint nNumberOfBytesToRead,      // number of bytes to read
        ref uint lpNumberOfBytesRead,   // number of bytes read
        IntPtr lpOverlapped             // overlapped buffer
    );

    private readonly Random rnd = new Random();
    private NotifyIcon trayIcon;
    private ContextMenu trayMenu;
    private HDDPing_Settings settings;
    private Timer timer;

    public HDDPing()
    {
      trayMenu = new ContextMenu();

      trayIcon = new NotifyIcon();
      trayIcon.Text = "HDD-Ping";
      trayIcon.Icon = Properties.Resources.disabled;

      // Add menu to tray icon and show it.
      trayIcon.ContextMenu = trayMenu;
      trayIcon.Visible = true;

      settings = HDDPing_Settings.GetSettings();
      
      this.CreateTrayOptions();
      trayMenu.MenuItems.Add("-");
      trayMenu.MenuItems.Add("Exit", OnExit);

      this.timer = this.InitializeTimer(settings.Interval);
      this.SetStateIcon();
    }

    protected Timer InitializeTimer(TimeSpan interval)
    {
      var timer = new Timer(interval.TotalMilliseconds);
      timer.Elapsed += (s, e) =>
      {
        this.PingDrives();
      };
      timer.Start();

      return timer;
    }

    private void PingDrives()
    {
      if (settings.DriveSettings.Any(ds => ds.Ping))
      {
        this.SwitchToWorkingIcon();
      }

      foreach (var di in settings.DriveSettings.Where(ds => ds.Ping).Select(ds => ds.DriveInfo))
      {
        try
        {
          using (SafeFileHandle handleValue = new SafeFileHandle(
              CreateFile(di.ID, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero), true))
          {
            if (handleValue.IsInvalid)
              Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());

            byte[] buffer = new byte[sizeof(ulong)];
            rnd.NextBytes(buffer);
            ulong lba = BitConverter.ToUInt64(buffer, 0) % (di.Size / 512UL);

            if (!SetFilePointerEx(handleValue.DangerousGetHandle(), (long)(lba * 512UL), IntPtr.Zero, FILE_BEGIN))
              Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());

            uint bytesRead = 0;
            var lpBuffer = new byte[512];
            if (!ReadFile(handleValue.DangerousGetHandle(), lpBuffer, 512, ref bytesRead, IntPtr.Zero))
              Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());

            handleValue.Close();
          }
        }
        catch { }
      }
    }

    protected void CreateTrayOptions()
    {
      var pingNowItem = new MenuItem("Ping now");
      pingNowItem.Click += (s, e) =>
      {
        this.PingDrives();
      };

      trayMenu.MenuItems.Add(pingNowItem);
      trayMenu.MenuItems.Add("-");

      var drivesMenu = new MenuItem("Ping Drives");
      var drives = DriveInfo.GetDrives();

      foreach (var d in drives)
      {
        try
        {
          var driveSetting = settings.DriveSettings.SingleOrDefault(setting =>
            setting.DriveInfo.Name == d.Name);
          var mi = new MenuItem(d.Name)
          {
            Checked = driveSetting is DriveSetting && driveSetting.Ping
          };
          mi.Click += (s, e) =>
          {
            mi.Checked = !mi.Checked;
            this.AddOrUpdateDriveSetting(d, mi.Checked);
          };

          drivesMenu.MenuItems.Add(mi);
        }
        catch { }
      }

      trayIcon.ContextMenu.MenuItems.Add(drivesMenu);


      var timerMenu = new MenuItem("Interval");
      var timerItems = new Dictionary<MenuItem, int>();
      int[] seconds = { 1, 2, 5, 10, 15, 20, 30, 45, 60, 90, 120, 300 };
      foreach (var i in seconds)
      {
        var tSec = new MenuItem(String.Format("{0} {1}", i < 60 ? i : (float)i / 60.0F, i == 1 ? "Second" : i < 60 ? "Seconds" : i < 120 ? "Minute" : "Minutes"))
        {
          Checked = (int)settings.Interval.TotalSeconds == i
        };
        tSec.Click += (s, e) =>
        {
          var interval = timerItems[s as MenuItem];
          settings.Interval = TimeSpan.FromSeconds(interval);

          this.timer.Interval = settings.Interval.TotalMilliseconds;

          foreach (var ti in timerItems)
          {
            ti.Key.Checked = false;
          }
          (s as MenuItem).Checked = true;
        };

        timerItems.Add(tSec, i);
        timerMenu.MenuItems.Add(tSec);
      }

      trayIcon.ContextMenu.MenuItems.Add(timerMenu);
    }

    protected void AddOrUpdateDriveSetting(DriveInfo di, bool ping)
    {
      if (!settings.DriveSettings.Any(ds =>
      {
        if (ds.DriveInfo.Name == di.Name)
        {
          ds.Ping = ping;
          return true;
        }

        return false;
      }))
      {
        settings.DriveSettings.Add(new DriveSetting(di, ping));
      }

      this.SetStateIcon();
    }

    protected void SwitchToWorkingIcon()
    {
      trayIcon.Icon = Properties.Resources.working;

      Task.Delay(Math.Min((int)settings.Interval.TotalMilliseconds / 3, 5000)).ContinueWith(finishedTask =>
      {
        this.SetStateIcon();
      });
    }

    protected void SetStateIcon()
    {
      try
      {
        trayIcon.Icon = settings.DriveSettings.Any(ds => ds.Ping) ?
          Properties.Resources.enabled : Properties.Resources.disabled;
      } catch { }
    }

    protected override void OnLoad(EventArgs e)
    {
      Visible = false; // Hide form window.
      ShowInTaskbar = false; // Remove from taskbar.

      base.OnLoad(e);
    }

    private void OnExit(object sender, EventArgs e)
    {
      HDDPing_Settings.TryWriteSettings(settings);

      Application.Exit();
    }

    protected override void Dispose(bool isDisposing)
    {
      if (isDisposing)
      {
        trayIcon.Dispose();
        timer.Dispose();

        if (Properties.Resources.disabled is Icon)
        {
          Properties.Resources.disabled.Dispose();
        }
        if (Properties.Resources.enabled is Icon)
        {
          Properties.Resources.enabled.Dispose();
        }
        if (Properties.Resources.working is Icon)
        {
          Properties.Resources.working.Dispose();
        }
      }

      base.Dispose(isDisposing);
    }
  }

  [Serializable]
  internal class HDDPing_Settings
  {
    public const string ConfigFile = ".settings.ser";

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    public TimeSpan Interval { get; set; }

    public IList<DriveSetting> DriveSettings { get; private set; }

    public HDDPing_Settings(TimeSpan interval, IList<DriveSetting> driveSettings)
    {
      this.Interval = interval;
      this.DriveSettings = driveSettings;
    }

    public static HDDPing_Settings GetSettings()
    {
      if (File.Exists(ConfigFile))
      {
        var bf = new BinaryFormatter();
        using (var stream = File.Open(ConfigFile, FileMode.Open))
        {
          try
          {
            var drives = DriveInfo.GetDrives();
            var setting = bf.Deserialize(stream) as HDDPing_Settings;

            setting.DriveSettings = setting.DriveSettings.Where(ds =>
            {
              return drives.Any(di => di.Name == ds.DriveInfo.Name);
              // filter non-existent drives
            }).ToList();

            return setting;
          }
          catch
          {
            return new HDDPing_Settings(
              HDDPing_Settings.DefaultTimeout, Enumerable.Empty<DriveSetting>().ToList());
          }
        }
      }
      else
      {
        return new HDDPing_Settings(
          HDDPing_Settings.DefaultTimeout, Enumerable.Empty<DriveSetting>().ToList());
      }
    }

    public static bool TryWriteSettings(HDDPing_Settings settings)
    {
      var bf = new BinaryFormatter();
      settings.DriveSettings = settings.DriveSettings.Where(ds => ds.Ping).ToList();
      using (var stream = File.Open(ConfigFile, FileMode.Create))
      {
        try
        {
          bf.Serialize(stream, settings);
          return true;
        }
        catch
        {
          return false;
        }
      }
    }
  }

  [Serializable]
  internal class DriveInfo
  {
    public string Name { get; private set; }
    public string ID { get; private set; }
    public ulong Size { get; private set; }

    public DriveInfo(string name, string id, ulong size)
    {
      this.Name = name;
      this.ID = id;
      this.Size = size;
    }

    public static List<DriveInfo> GetDrives()
    {
      using (var searcher = new ManagementObjectSearcher(new WqlObjectQuery(
        "SELECT * FROM Win32_DiskDrive WHERE MediaType = 'Fixed hard disk media'")))
      {
        return searcher
          .Get()
          .OfType<ManagementObject>()
          .Select(o => {
            var caption = o.Properties["Caption"].Value.ToString();
            var deviceID = o.Properties["DeviceID"].Value.ToString();
            var size = ulong.Parse(o.Properties["Size"].Value.ToString());
            return new DriveInfo($"{deviceID.Substring(4)} - {caption} ({Math.Round((double)size / (1024 * 1024 * 1024), 1)} GB)", deviceID, size);
          })
          .ToList();
      }
    }
  }

  [Serializable]
  internal class DriveSetting
  {
    public DriveInfo DriveInfo { get; private set; }
    public bool Ping { get; set; }

    public DriveSetting(DriveInfo di, bool ping)
    {
      this.DriveInfo = di;
      this.Ping = ping;
    }
  }
}
