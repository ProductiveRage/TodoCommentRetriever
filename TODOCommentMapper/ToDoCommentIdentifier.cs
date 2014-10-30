using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TODOCommentMapper
{
	public class ToDoCommentIdentifier
	{
		private readonly Predicate<string> _toDoCommentMatcher;
		public ToDoCommentIdentifier(Predicate<string> toDoCommentMatcher)
		{
			if (toDoCommentMatcher == null)
				throw new ArgumentNullException("toDoCommentMatcher");

			_toDoCommentMatcher = toDoCommentMatcher;
		}
		public ToDoCommentIdentifier() : this(DefaultToDoCommentFilter) { }

		public static Predicate<string> DefaultToDoCommentFilter
		{
			get
			{
				// TODO: Tidy this up with a reg ex?
				return commentContent =>
					commentContent.Contains("//TODO") ||
					commentContent.Contains("// TODO") ||
					commentContent.Contains("TODO:") ||
					commentContent.Contains("TODO[") ||
					commentContent.Contains("TODO [") ||
					commentContent.Contains("TODO\r") ||
					commentContent.Contains("TODO\n") ||
					commentContent.EndsWith("TODO");
			}
		}

		public IEnumerable<Comment> GetToDoComments(string content)
		{
			if (content == null)
				throw new ArgumentException("content");

			var todoComments = new List<Comment>();
			var commentLocatingVisitor = new CommentLocatingVisitor(
				comment =>
				{
					if (_toDoCommentMatcher(comment.Content))
						todoComments.Add(comment);
				}
			);
			commentLocatingVisitor.Visit(
				CSharpSyntaxTree.ParseText(content).GetRoot()
			);
			return todoComments;
		}

		private class CommentLocatingVisitor : SyntaxWalker
		{
			private readonly Action<Comment> _commentLocated;
			public CommentLocatingVisitor(Action<Comment> commentLocated) : base(SyntaxWalkerDepth.StructuredTrivia)
			{
				if (commentLocated == null)
					throw new ArgumentNullException("commentLocated");

				_commentLocated = commentLocated;
			}

			private static HashSet<SyntaxKind> _commentTypes = new HashSet<SyntaxKind>(new[] {
				SyntaxKind.SingleLineCommentTrivia,
				SyntaxKind.MultiLineCommentTrivia,
				SyntaxKind.DocumentationCommentExteriorTrivia,
				SyntaxKind.SingleLineDocumentationCommentTrivia,
				SyntaxKind.MultiLineDocumentationCommentTrivia
			});

			public override void Visit(SyntaxNode node)
			{
				var nodeType = node.GetType();
				base.Visit(node);
			}

			protected override void VisitToken(SyntaxToken token)
			{
				var tokenType = token.GetType();
				base.VisitToken(token);
			}

			protected override void VisitTrivia(SyntaxTrivia trivia)
			{
				if (_commentTypes.Contains(trivia.CSharpKind()))
				{
					string triviaContent;
					using (var writer = new StringWriter())
					{
						trivia.WriteTo(writer);
						triviaContent = writer.ToString();
					}

					// Note: When looking for the containingMethodOrPropertyIfAny, we want MemberDeclarationSyntax types such as ConstructorDeclarationSyntax, MethodDeclarationSyntax,
					// IndexerDeclarationSyntax, PropertyDeclarationSyntax but NamespaceDeclarationSyntax and TypeDeclarationSyntax also inherit from MemberDeclarationSyntax and we
					// don't want those
					var containingNode = trivia.Token.Parent;
					var containingMethodOrPropertyIfAny = TryToGetContainingNode<MemberDeclarationSyntax>(
						containingNode,
						n => !(n is NamespaceDeclarationSyntax) && !(n is TypeDeclarationSyntax)
					);
					var containingTypeIfAny = TryToGetContainingNode<TypeDeclarationSyntax>(containingNode);
					var containingNameSpaceIfAny = TryToGetContainingNode<NamespaceDeclarationSyntax>(containingNode);
					_commentLocated(new Comment(
						triviaContent,
						trivia.SyntaxTree.GetLineSpan(trivia.Span).StartLinePosition.Line,
						containingMethodOrPropertyIfAny,
						containingTypeIfAny,
						containingNameSpaceIfAny
					));
				}
				base.VisitTrivia(trivia);
			}

			private T TryToGetContainingNode<T>(SyntaxNode node, Predicate<T> optionalFilter = null) where T : SyntaxNode
			{
				if (node == null)
					throw new ArgumentNullException("node");

				var currentNode = node;
				while (true)
				{
					var nodeOfType = currentNode as T;
					if (nodeOfType != null)
					{
						if ((optionalFilter == null) || optionalFilter(nodeOfType))
							return nodeOfType;
					}
					if (currentNode.Parent == null)
						break;
					currentNode = currentNode.Parent;
				}
				return null;
			}
		}
	}
}
