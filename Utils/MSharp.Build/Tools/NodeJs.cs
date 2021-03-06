﻿using Olive;
using System;
using System.IO;

namespace MSharp.Build.Tools
{
    class NodeJs : BuildTool
    {
        protected override string Name => "npm";
        protected override FileInfo Installer => WindowsCommand.Chocolaty;
        protected override string InstallCommand => "install nodejs.install";

        public override FileInfo ExpectedPath
            => Environment.SpecialFolder.ProgramFiles.GetFile("nodejs\\npm.cmd");
    }
}