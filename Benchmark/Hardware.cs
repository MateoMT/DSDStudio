using System.Management;

namespace Benchmark;

public static class Hardware
{
    public static string GetCpuName()
    {
        if (!OperatingSystem.IsWindows())
            return "Unknown CPU";

        try
        {
            using var searcher = new ManagementObjectSearcher("select Name from Win32_Processor");
            foreach (ManagementObject item in searcher.Get())
                return item["Name"]?.ToString() ?? "Unknown CPU";
        }
        catch
        {
        }

        return "Unknown CPU";
    }

    public static List<string> GetGpuNames()
    {
        var names = new List<string>();

        if (!OperatingSystem.IsWindows())
        {
            names.Add("Unknown GPU");
            return names;
        }

        try
        {
            using var searcher = new ManagementObjectSearcher("select Name from Win32_VideoController");
            foreach (ManagementObject item in searcher.Get())
            {
                string? name = item["Name"]?.ToString();
                if (!string.IsNullOrWhiteSpace(name))
                    names.Add(name);
            }
        }
        catch
        {
        }

        if (names.Count == 0)
            names.Add("Unknown GPU");

        return names;
    }
}
