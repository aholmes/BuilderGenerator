using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Builder.VSIX
{
    internal static class GuidList
    {
        public const string guidBuilderPackageString = "28b786c0-53d0-4178-9e10-d6eefaa6ece9";
        public const string guidBuilderCmdSetString = "0a622461-69d9-46c3-b0fe-fb1420066fab";

        public static readonly Guid guidBuilderPackage = new Guid(guidBuilderPackageString);
        public static readonly Guid guidBuilderCmdSet = new Guid(guidBuilderCmdSetString);
    }
}
