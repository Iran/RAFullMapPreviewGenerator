﻿#region Copyright & License Information
/*
 * Copyright 2007-2012 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Diagnostics;
using System.IO;

namespace RAFullMapPreviewGenerator
{
    public enum PlatformType { Unknown, Windows, OSX, Linux }

    public static class Platform
    {
        public static PlatformType CurrentPlatform { get { return currentPlatform; } }

        static PlatformType currentPlatform = GetCurrentPlatform();

        static PlatformType GetCurrentPlatform()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                return PlatformType.Windows;

            try
            {
                var psi = new ProcessStartInfo("uname", "-s");
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                var p = Process.Start(psi);
                var kernelName = p.StandardOutput.ReadToEnd();
                if (kernelName.Contains("Linux") || kernelName.Contains("BSD"))
                    return PlatformType.Linux;
                if (kernelName.Contains("Darwin"))
                    return PlatformType.OSX;
            }
            catch { }

            return PlatformType.Unknown;
        }

        public static string SupportDir
        {
            get
            {
                var dir = Environment.GetFolderPath(Environment.SpecialFolder.Personal);

                switch (CurrentPlatform)
                {
                    case PlatformType.Windows:
                        dir += Path.DirectorySeparatorChar + "OpenRA";
                        break;
                    case PlatformType.OSX:
                        dir += "/Library/Application Support/OpenRA";
                        break;
                    case PlatformType.Linux:
                    default:
                        dir += "/.openra";
                        break;
                }

                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                return dir + Path.DirectorySeparatorChar;
            }
        }
    }
}
