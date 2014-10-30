using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Evaluation;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TODOCommentMapper
{
	class Program
	{
		static void Main(string[] args)
		{
			var solutionFilePath = @"..\..\..\TODOCommentMapper.sln";

			var todoCommentIdentifier = new ToDoCommentIdentifier();
			foreach (var csharpCompileFile in GetProjectFilesForSolution(new FileInfo(solutionFilePath)).SelectMany(projectFile => GetCSharpCompileItemFilesForProject(projectFile)))
			{
				foreach (var todoComment in todoCommentIdentifier.GetToDoComments(csharpCompileFile.OpenText().ReadToEnd()))
				{
					Console.WriteLine(todoComment.Content);
					Console.WriteLine();
					if (todoComment.NamespaceIfAny == null)
						Console.WriteLine("Not in any namespace");
					else
					{
						Console.WriteLine("Namespace: " + todoComment.NamespaceIfAny.Name);
						if (todoComment.TypeIfAny != null)
							Console.WriteLine("Type: " + todoComment.TypeIfAny.Identifier);
						if (todoComment.MethodOrPropertyIfAny != null)
						{
							Console.Write("Method/Property: ");
							if (todoComment.MethodOrPropertyIfAny is ConstructorDeclarationSyntax)
								Console.Write(".ctor");
							else if (todoComment.MethodOrPropertyIfAny is MethodDeclarationSyntax)
								Console.Write(((MethodDeclarationSyntax)todoComment.MethodOrPropertyIfAny).Identifier);
							else if (todoComment.MethodOrPropertyIfAny is PropertyDeclarationSyntax)
								Console.Write(((PropertyDeclarationSyntax)todoComment.MethodOrPropertyIfAny).Identifier);
							else
								Console.Write("?");
							Console.WriteLine();
						}
					}
					Console.WriteLine(csharpCompileFile.FullName + ":" + todoComment.LineNumber);
					Console.WriteLine();
				}
			}

			Console.WriteLine("Success! Press [Enter] to continue..");
			Console.ReadLine();
		}

		private static IEnumerable<FileInfo> GetProjectFilesForSolution(FileInfo solutionFile)
		{
			if (solutionFile == null)
				throw new ArgumentNullException("solutionFile");

			var projectFileMatcher = new Regex(
				@"Project\(""\{\w{8}-\w{4}-\w{4}-\w{4}-\w{12}\}""\) = ""(.*?)"", ""(?<projectFile>(.*?\.csproj))"", ""\{\w{8}-\w{4}-\w{4}-\w{4}-\w{12}\}"""
			);
			foreach (Match match in projectFileMatcher.Matches(solutionFile.OpenText().ReadToEnd()))
				yield return new FileInfo(Path.Combine(solutionFile.Directory.FullName, match.Groups["projectFile"].Value));
		}

		private static IEnumerable<FileInfo> GetCSharpCompileItemFilesForProject(FileInfo projectFile)
		{
			if (projectFile == null)
				throw new ArgumentNullException("projectFile");

			return (new ProjectCollection()).LoadProject(projectFile.FullName).AllEvaluatedItems
				.Where(item => item.ItemType == "Compile")
				.Select(item => item.EvaluatedInclude)
				.Where(include => include.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
				.Select(include => new FileInfo(Path.Combine(projectFile.Directory.FullName, include)));
		}
	}
}
