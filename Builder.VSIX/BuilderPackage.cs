using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using System.IO;
using EnvDTE;

namespace Builder.VSIX
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideAutoLoad(Microsoft.VisualStudio.Shell.Interop.UIContextGuids80.SolutionExists)]
    [Guid(BuilderPackage.PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly",
         Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    public sealed class BuilderPackage : Package
    {
        /// <summary>
        /// BuilderPackage GUID string.
        /// </summary>
        public const string PackageGuidString = GuidList.guidBuilderPackageString;

        /// <summary>
        /// Initializes a new instance of the <see cref="Builder"/> class.
        /// </summary>
        public BuilderPackage()
        {
            // Inside this method you can place any initialization code that does not require
            // any Visual Studio service because at this point the package object is created but
            // not sited yet inside Visual Studio environment. The place to do all the other
            // initialization is the Initialize method.
        }

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();

            OleMenuCommandService commandService = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService == null) throw new Exception("Could not initialize Builder Creator extension.");

            var menuCommandID = new CommandID(GuidList.guidBuilderCmdSet, (int)PkgCmdIDList.BuilderId);
            var menuCommand = new OleMenuCommand(OnCreateBuilderCommand, menuCommandID);

            menuCommand.BeforeQueryStatus += MenuCommand_BeforeQueryStatus;

            commandService.AddCommand(menuCommand);

            //Builder.Initialize(this);
        }

        #endregion

        #region Builder handler
        private async void OnCreateBuilderCommand(object sender, EventArgs e)
        {
            FileInfo fileinfo;
            if (!TryGetSelectedCsFile(out fileinfo))
            {
                System.Diagnostics.Debug.WriteLine("Failed to get FileInfo for selected file.");
                return;
            }

            try
            {
                var newClassContent = await Builder.Creator.GetBuilderClassContent(fileinfo);
                var newClassFilename = string.Concat(fileinfo.Name.Replace(fileinfo.Extension, ""), "Builder");

                CreateNewFile(newClassFilename, newClassContent);

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Exception thrown when creating builder.\n" + ex.Message);
                throw;
            }
        }

        private void CreateNewFile(string filename, string content)
        {
            DTE dte = (DTE)GetService(typeof(DTE));
            dte.ItemOperations.NewFile(@"General\Visual C# Class", filename, EnvDTE.Constants.vsViewKindTextView);
            TextSelection txtSel = (TextSelection)dte.ActiveDocument.Selection;
            TextDocument txtDoc = (TextDocument)dte.ActiveDocument.Object("");

            txtSel.SelectAll();
            txtSel.Delete();
            txtSel.Insert(content);
            //    //var dte = (EnvDTE.DTE)ServiceProvider.GlobalProvider.GetService(typeof(EnvDTE.DTE));
            //    // https://social.msdn.microsoft.com/Forums/vstudio/en-US/a7da9e48-7282-4e22-a07a-36e66426316e/add-in-trying-to-add-class-fails-with-template-invalid-for-that-project?forum=vsx
            //    EnvDTE80.DTE2 dte = Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE80.DTE2;

            //    if (dte == null)
            //    {
            //        //Alert("Could not create new file.");
            //        System.Diagnostics.Debug.WriteLine("Could not get EnvDTE.DTE service.");
            //        return;
            //    }

            //    var solution = dte.Solution as EnvDTE80.Solution2;

            //    if (solution == null)
            //    {
            //        //Alert("Could not create new file.");
            //        System.Diagnostics.Debug.WriteLine("Could not get DTE solution.");
            //        return;
            //    }

            //    var x = solution.GetProjectItemTemplate(filename, "CSharp");
            //    //dte.ActiveDocument.ProjectItem.ContainingProject;


            //    //dte.ItemOperations.AddNewItem(@"Visual C# Project Items\Class", name);
            //    // http://stackoverflow.com/questions/11049758/selected-project-from-solution-explorer

            //    var txtSel = (EnvDTE.TextSelection)dte.ActiveDocument.Selection;
            //    var txtDoc = (EnvDTE.TextDocument)dte.ActiveDocument.Object();

            //    txtSel.SelectAll();
            //    txtSel.Delete();
            //    txtSel.Insert(content);
        }

        #endregion

        private void MenuCommand_BeforeQueryStatus(object sender, EventArgs e)
        {
            var menuCommand = sender as OleMenuCommand;
            if (menuCommand == null)
            {
                System.Diagnostics.Debug.WriteLine("'sender' in menuCommand_BeforeQueryStatus is not of type OleMenuCommand.");
                return;
            }

            menuCommand.Visible = false;
            menuCommand.Enabled = false;

            FileInfo fileinfo = null;
            if (TryGetSelectedCsFile(out fileinfo))
            {
                menuCommand.Visible = true;
                menuCommand.Enabled = true;
            }
        }

        private static bool TryGetSelectedCsFile(out FileInfo fileinfo)
        {
            fileinfo = GetSelectedFile();
            if (fileinfo == null) return false;

            var isCSharpFile = string.Compare(".cs", fileinfo.Extension, StringComparison.OrdinalIgnoreCase) == 0;
            return isCSharpFile;
        }

        private static FileInfo GetSelectedFile()
        {
            IVsHierarchy hierarchy = null;
            var itemId = VSConstants.VSITEMID_NIL;

            if (!IsSingleProjectItemSelection(out hierarchy, out itemId)) return null;

            // if we got this far then there is a single project item selected
            string itemFullPath;
            ((IVsProject)hierarchy).GetMkDocument(itemId, out itemFullPath);
            return new FileInfo(itemFullPath);
        }

        public static bool IsSingleProjectItemSelection(out IVsHierarchy hierarchy, out uint itemid)
        {
            hierarchy = null;
            itemid = VSConstants.VSITEMID_NIL;
            int hr = VSConstants.S_OK;

            var monitorSelection = Package.GetGlobalService(typeof(SVsShellMonitorSelection)) as IVsMonitorSelection;
            var solution = Package.GetGlobalService(typeof(SVsSolution)) as IVsSolution;
            if (monitorSelection == null || solution == null)
            {
                return false;
            }

            IVsMultiItemSelect multiItemSelect = null;
            IntPtr hierarchyPtr = IntPtr.Zero;
            IntPtr selectionContainerPtr = IntPtr.Zero;

            try
            {
                hr = monitorSelection.GetCurrentSelection(out hierarchyPtr, out itemid, out multiItemSelect, out selectionContainerPtr);

                if (ErrorHandler.Failed(hr) || hierarchyPtr == IntPtr.Zero || itemid == VSConstants.VSITEMID_NIL)
                {
                    // there is no selection
                    return false;
                }

                // multiple items are selected
                if (multiItemSelect != null) return false;

                // there is a hierarchy root node selected, thus it is not a single item inside a project

                if (itemid == VSConstants.VSITEMID_ROOT) return false;

                hierarchy = Marshal.GetObjectForIUnknown(hierarchyPtr) as IVsHierarchy;
                if (hierarchy == null) return false;

                Guid guidProjectID = Guid.Empty;

                if (ErrorHandler.Failed(solution.GetGuidOfProject(hierarchy, out guidProjectID)))
                {
                    return false; // hierarchy is not a project inside the Solution if it does not have a ProjectID Guid
                }

                // if we got this far then there is a single project item selected
                return true;
            }
            finally
            {
                if (selectionContainerPtr != IntPtr.Zero)
                {
                    Marshal.Release(selectionContainerPtr);
                }

                if (hierarchyPtr != IntPtr.Zero)
                {
                    Marshal.Release(hierarchyPtr);
                }
            }
        }
    }
}
