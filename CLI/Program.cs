using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;
using System.Linq;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using System.Text;
using System.Diagnostics;

namespace BuilderCreator.CLI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                // TODO replace args with an options parser
                var classPath = args[0];
                var className = args[1];


                if (!File.Exists(classPath))
                {
                    throw new Exception(string.Format("Cannot transform \"{0}\". Path does not exist."));
                }

                var fileText = File.ReadAllText(classPath);

                var newClassContent = Builder.Creator.GetBuilderClassContent(fileText, className);

                Console.WriteLine(newClassContent);

                //var model = GetSemanticModel(fileToParse);

                //var cu = ProcessClass(model, className);

                //var formattedNode = Formatter.Format(cu, new Microsoft.CodeAnalysis.AdhocWorkspace());
                //var sb = new StringBuilder();

                /*using(var writer = new StringWriter(sb))
                using(var file = File.CreateText("out.cs"))
                {
                    //formattedNode.WriteTo(writer);
                    //file.Write(formattedNode.ToFullString());
                    file.Write(cu.ToFullString());
                    file.Flush();
                }*/
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);

                Console.Read();
                return;
                //throw;
            }
            finally
            {
                Console.Read();
            }


/*
            foreach (SyntaxTree sourceTree in test.SyntaxTrees)
            {
                // creation of the semantic model
                SemanticModel model = test.GetSemanticModel(sourceTree);

                // initialization of our rewriter class
                InitializerRewriter rewriter = new InitializerRewriter(model);

                // analysis of the tree
                SyntaxNode newSource = rewriter.Visit(sourceTree.GetRoot());

                if(!Directory.Exists(@"../new_src"))
                    Directory.CreateDirectory(@"../new_src");

                // if we changed the tree we save a new file
                if (newSource != sourceTree.GetRoot())
                {
                    File.WriteAllText(Path.Combine(@"../new_src", Path.GetFileName(sourceTree.FilePath)), newSource.ToFullString());
                }
            }
*/
        }
    }
}
