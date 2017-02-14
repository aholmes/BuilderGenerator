using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;
using System.Threading.Tasks;

namespace Builder
{
    public static class Creator
    {
        public static async Task<string> GetBuilderClassContent(FileInfo fileinfo)
        {
            var className = fileinfo.Name.Replace(fileinfo.Extension, string.Empty);

            var classContent = string.Empty;
            using (var fileStream = fileinfo.OpenRead())
            using (var streamReader = new StreamReader(fileStream))
            {
                classContent = await streamReader.ReadToEndAsync();
            }

            return GetBuilderClassCompilationUnit(classContent, className).NormalizeWhitespace().ToString();
        }

        public static string GetBuilderClassContent(string classContent, string className)
        {
            return GetBuilderClassCompilationUnit(classContent, className).NormalizeWhitespace().ToString();
        }

        public static CompilationUnitSyntax GetBuilderClassCompilationUnit(string classContent, string className)
        {
            var semanticModelCompilation = _getSemanticModel(classContent);

            return _processClass(semanticModelCompilation, className);
        }

        private static Tuple<SemanticModel, Compilation> _getSemanticModel(string classContent)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(classContent);

            var mscorlib = MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location);
            var codeAnalysis = MetadataReference.CreateFromFile(typeof(SyntaxTree).GetTypeInfo().Assembly.Location);
            var csharpCodeAnalysis = MetadataReference.CreateFromFile(typeof(CSharpSyntaxTree).GetTypeInfo().Assembly.Location);

            var references = new MetadataReference[] { mscorlib, codeAnalysis, csharpCodeAnalysis };

            var compilation = CSharpCompilation.Create("ConsoleApplication",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.ConsoleApplication)
            );

            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            return Tuple.Create(semanticModel, (Compilation)compilation);
        }

        private static CompilationUnitSyntax _processClass(Tuple<SemanticModel, Compilation> model, string className)
        {
            var semanticModel = model.Item1;

            var root = semanticModel.SyntaxTree.GetRoot();
            
            var descendantNodes = root.DescendantNodes().ToArray();

            var @namespace = descendantNodes
                .OfType<NamespaceDeclarationSyntax>()
                .First();

            var @class = semanticModel.GetDeclaredSymbol(descendantNodes.OfType<ClassDeclarationSyntax>().First()); // completely ignoring className ...

            var usings = descendantNodes
                .OfType<UsingDirectiveSyntax>()
                .ToArray();

            var newClassName = string.Concat(@class.Name, "Builder");

            var props = _getClassProperties(semanticModel, descendantNodes);

            var newNamespace = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.IdentifierName(@namespace.Name.ToString()));

            var propDeclarations = _generatePropertyDeclarations(props);

            var fieldDeclarations = _generateFieldDeclarations(props);

            var methodDeclarations = _generateClassMethodDeclarations(newClassName, props); 

            var classCtorBodyStatements = _generateClassCtorBodyStatements(props);

            var buildMethodDeclaration = _generateBuildMethodDeclaration(className, props);

            var newClassCtor = SyntaxFactory.ConstructorDeclaration(newClassName)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                    .WithBody(SyntaxFactory.Block(classCtorBodyStatements));

                var newClass = SyntaxFactory.ClassDeclaration(newClassName)
                    .AddBaseListTypes(SyntaxFactory.SimpleBaseType(
                        SyntaxFactory.GenericName("EntityBuilder")
                            .AddTypeArgumentListArguments(
                                SyntaxFactory.ParseTypeName(newClassName),
                                SyntaxFactory.ParseTypeName(@class.Name)
                            )
                    ))
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.SealedKeyword))
                    .AddMembers(propDeclarations)
                    .AddMembers(fieldDeclarations)
                    .AddMembers(newClassCtor)
                    .AddMembers(methodDeclarations)
                    .AddMembers(buildMethodDeclaration);



            return SyntaxFactory.CompilationUnit()
                .AddMembers(newNamespace.AddMembers(
                    newClass
                ))
                .WithUsings(SyntaxFactory.List(
                    usings
                ));
        }

        private const string BuilderPropertyTypeName = "BuilderProperty";

        private static IPropertySymbol[] _getClassProperties(SemanticModel model, IEnumerable<SyntaxNode> nodes)
        {
            // TODO support classes with no properties
            return nodes.OfType<PropertyDeclarationSyntax>()
                .Select(node => model.GetDeclaredSymbol(node))
                .Where(prop => 
                    prop.Kind == SymbolKind.Property
                    && prop.CanBeReferencedByName == true
                    && prop.DeclaredAccessibility == Accessibility.Public
                    && prop.IsAbstract == false
                    && prop.IsStatic == false
                    && prop.IsImplicitlyDeclared == false
                )
                .ToArray();
        }

        private static PropertyDeclarationSyntax[] _generatePropertyDeclarations(IEnumerable<IPropertySymbol> properties)
        {
            return properties
                    .Select(prop => SyntaxFactory.PropertyDeclaration(
                        SyntaxFactory.GenericName(BuilderPropertyTypeName)
                            .AddTypeArgumentListArguments(SyntaxFactory.ParseTypeName(prop.Type.ToString()))
                        , prop.Name
                    )
                    .WithModifiers(SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.ProtectedKeyword)
                    ))
                    .AddAccessorListAccessors(
                        SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                            .WithBody(SyntaxFactory.Block(
                                SyntaxFactory.ReturnStatement(
                                    SyntaxFactory.GetStandaloneExpression(
                                            SyntaxFactory.IdentifierName(_getPropertyBackingFieldName(prop))
                                        )
                                    )
                                )
                            )
                    )
                )
                .ToArray();
        }

        private static FieldDeclarationSyntax[] _generateFieldDeclarations(IEnumerable<IPropertySymbol> properties)
        {
            return properties
                .Select(prop =>
                    SyntaxFactory.FieldDeclaration(
                        // TODO this should be able to use the same objects from propDeclarations
                        SyntaxFactory.VariableDeclaration(
                            SyntaxFactory.GenericName(BuilderPropertyTypeName)
                                .AddTypeArgumentListArguments(SyntaxFactory.ParseTypeName(prop.Type.ToString()))
                        )
                        .AddVariables(
                            SyntaxFactory.VariableDeclarator(
                                identifier: SyntaxFactory.Identifier(_getPropertyBackingFieldName(prop))
                            )
                        )
                    )
                    .WithModifiers(SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                        SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)
                    ))
                )
                .ToArray();
        }

        private static ExpressionStatementSyntax[] _generateClassCtorBodyStatements(IEnumerable<IPropertySymbol> properties)
        {
            return properties.Select(prop => SyntaxFactory.ExpressionStatement(
                SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    SyntaxFactory.GetStandaloneExpression(
                        SyntaxFactory.IdentifierName(_getPropertyBackingFieldName(prop))
                    ),
                    SyntaxFactory.ObjectCreationExpression(
                        SyntaxFactory.GenericName("BuilderProperty")
                            .AddTypeArgumentListArguments(SyntaxFactory.ParseTypeName(prop.Type.ToString())),
                        SyntaxFactory.ArgumentList(
                            SyntaxFactory.SeparatedList(new[]
                            {
                                SyntaxFactory.Argument(
                                    // FIXME this keeps failing at runtime for me
                                    //SyntaxFactory.IdentifierName(SyntaxFactory.Token(SyntaxKind.ThisKeyword))
                                    SyntaxFactory.IdentifierName("this")
                                )
                            })
                        ),
                        null
                    )
                )
            )).ToArray();
        }

        private static MethodDeclarationSyntax[] _generateClassMethodDeclarations(string className, IEnumerable<IPropertySymbol> properties)
        {
            return properties.Select(prop => SyntaxFactory.MethodDeclaration(
                        returnType: SyntaxFactory.ParseTypeName(className),
                        identifier: SyntaxFactory.Identifier(_getWithMethodName(prop))
                    )
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                    .WithParameterList(
                        SyntaxFactory.ParameterList(
                            SyntaxFactory.SeparatedList<ParameterSyntax>(new[]
                            {
                                SyntaxFactory.Parameter(
                                    identifier: SyntaxFactory.Identifier(_getWithMethodParameterName(prop))
                                )
                                .WithType(SyntaxFactory.ParseTypeName(prop.Type.ToString()))
                            })
                        )
                    )
                    .WithBody(
                        SyntaxFactory.Block(new StatementSyntax[]
                        {
                            SyntaxFactory.ExpressionStatement(
                                SyntaxFactory.InvocationExpression(
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.IdentifierName(prop.Name),
                                        SyntaxFactory.IdentifierName("SetValue")
                                    ),
                                    SyntaxFactory.ArgumentList(
                                        SyntaxFactory.SeparatedList<ArgumentSyntax>(new[]
                                        {
                                            SyntaxFactory.Argument(
                                                SyntaxFactory.GetStandaloneExpression(
                                                    SyntaxFactory.IdentifierName(_getWithMethodParameterName(prop))
                                                )
                                            )
                                        })
                                    )
                                )
                            ),
                            SyntaxFactory.ReturnStatement(
                                SyntaxFactory.GetStandaloneExpression(
                                    SyntaxFactory.IdentifierName("ThisBuilder")
                                )
                            )
                        })
                    )
                ).ToArray();
        }

        private static StatementSyntax[] _generateBuildMethodStatements(string className, IEnumerable<IPropertySymbol> properties)
        {
            var buildBodyStatements = new List<StatementSyntax>();

            // declare new var of the original class type
            var localCreationStatement = SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(
                    SyntaxFactory.IdentifierName("var"),
                    SyntaxFactory.SeparatedList(new[]
                    {
                        SyntaxFactory.VariableDeclarator(
                            SyntaxFactory.Identifier(className.ToLowerInvariant()),
                            null,
                            SyntaxFactory.EqualsValueClause(
                                    SyntaxFactory.ObjectCreationExpression(
                                    SyntaxFactory.ParseTypeName(className),
                                    SyntaxFactory.ArgumentList(),
                                    null
                                )
                            )
                        )
                    })
                )
            );

            // each member of the classes gets an if block within which assignment occurs
            var ifBlockStatments = properties.Select(prop =>
                SyntaxFactory.IfStatement(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(prop.Name),
                        SyntaxFactory.IdentifierName("HasValueOrAutoData")
                    ),
                    SyntaxFactory.Block(
                        SyntaxFactory.ExpressionStatement(
                            SyntaxFactory.AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    //SyntaxFactory.IdentifierName(prop.Name),
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.IdentifierName(className.ToLowerInvariant()),
                                        SyntaxFactory.IdentifierName(prop.Name)
                                    ),
                                    SyntaxFactory.IdentifierName("Value")
                                ),
                                SyntaxFactory.IdentifierName(prop.Name)
                                // this code will use the GetValue method on the class member, but it is likely not needed
                                //SyntaxFactory.InvocationExpression(
                                //    SyntaxFactory.MemberAccessExpression(
                                //        SyntaxKind.SimpleMemberAccessExpression,
                                //        SyntaxFactory.IdentifierName(prop.Name),
                                //        SyntaxFactory.IdentifierName("GetValue")
                                //    ),
                                //    SyntaxFactory.ArgumentList()
                                //)
                            )
                        )
                    )
                )
            );


            buildBodyStatements.Add(localCreationStatement);
            buildBodyStatements.AddRange(ifBlockStatments);
            buildBodyStatements.Add(
                SyntaxFactory.ReturnStatement(
                    SyntaxFactory.IdentifierName(className.ToLowerInvariant())
                )
            );

            return buildBodyStatements.ToArray();
        }

        private static MethodDeclarationSyntax _generateBuildMethodDeclaration(string className, IEnumerable<IPropertySymbol> properties)
        {
            return SyntaxFactory.MethodDeclaration(
                      returnType: SyntaxFactory.ParseTypeName(className),
                      identifier: SyntaxFactory.Identifier("Build")
                  )
                  .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                  .AddModifiers(SyntaxFactory.Token(SyntaxKind.OverrideKeyword))
                  // TODO add body here
                  .WithBody(SyntaxFactory.Block(
                      _generateBuildMethodStatements(className, properties)
                  ));
        }

        private static string _getPropertyBackingFieldName(IPropertySymbol prop)
        {
            return string.Concat('_', Char.ToLowerInvariant(prop.Name[0]), prop.Name.Substring(1));
        }

        private static string _getWithMethodName(IPropertySymbol prop)
        {
            return string.Concat("With", prop.Name);
        }

        private static string _getWithMethodParameterName(IPropertySymbol prop)
        {
            return string.Concat(Char.ToLowerInvariant(prop.Name[0]), prop.Name.Substring(1));
        }
    }
}
