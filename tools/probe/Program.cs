using System;
using System.IO;
using System.Runtime.InteropServices;
class P {
  static void Main() {
    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WhatSprintIsItWidget");
    Directory.CreateDirectory(dir);
    string logPath = Path.Combine(dir, "probe.log");
    string result;
    var clsid = new Guid("6E3E1B58-1D8C-4F2A-9C4E-6A5B2F0E9D11");
    try {
      Type t = Type.GetTypeFromCLSID(clsid, true);
      object o = Activator.CreateInstance(t);
      result = o != null ? "ACTIVATION_OK: " + o.GetType().FullName : "NULL";
    } catch (Exception ex) {
      result = "ACTIVATION_FAIL: " + ex.GetType().Name + ": " + ex.Message;
    }
    File.AppendAllText(logPath, DateTime.Now.ToString("O") + " " + result + Environment.NewLine);
    Console.WriteLine(result);
  }
}
