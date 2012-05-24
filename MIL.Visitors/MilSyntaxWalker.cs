﻿using System.Collections.Generic;
using System.Linq;
using Roslyn.Compilers.CSharp;

namespace MIL.Visitors
{
    public class MilSyntaxWalker : SyntaxWalker
    {
        protected const string CommandIfx = "ICommand";
        protected const string PublishKeyword = "Send";
        protected const string EventIfx = "IEvent";
        protected const string EventHandlerPlainIfx = "IEventHandler";
        protected const string CommandHandlerPlainIfx = "ICommandHandler";

        private readonly HandlerDeclarationSyntaxVisitor _cmdHandlerDeclarationVisitor = new HandlerDeclarationSyntaxVisitor(CommandHandlerPlainIfx);
        private readonly HandlerDeclarationSyntaxVisitor _eventHandlerDeclarationVisitor = new HandlerDeclarationSyntaxVisitor(EventHandlerPlainIfx);

        public MilSyntaxWalker() : base(visitIntoStructuredTrivia: true)
        {
            Commands = new List<ClassDeclarationSyntax>();
            Events = new List<ClassDeclarationSyntax>();
            PublicationCalls = new List<MemberAccessExpressionSyntax>();
            EventHandlers = new List<ClassDeclarationSyntax>();
            CommandHandlers = new List<ClassDeclarationSyntax>();
            Publications = new List<InvocationExpressionSyntax>();
        }

        public List<InvocationExpressionSyntax> Publications { get; private set; }
        public List<ClassDeclarationSyntax> Commands { get; private set; }
        public List<ClassDeclarationSyntax> Events { get; private set; }
        public List<ClassDeclarationSyntax> EventHandlers { get; private set; } 
        public List<ClassDeclarationSyntax> CommandHandlers { get; private set; }
        public List<MemberAccessExpressionSyntax> PublicationCalls { get; private set; }

        protected override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            if (node.Name.GetText() != PublishKeyword) return;

            PublicationCalls.Add(node);
            Publications.Add(node.Ancestors().OfType<InvocationExpressionSyntax>().Last());
        }
        //protected override void VisitInvocationExpression(InvocationExpressionSyntax node)
        //{
           
        //}
        protected override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            foreach (var type in node.DescendentNodes().OfType<ClassDeclarationSyntax>())
            {
                Visit(type);
            }
        }

        protected override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            foreach (var classNode in node.DescendentNodesAndSelf().OfType<ClassDeclarationSyntax>())
            {
                LookForCommands(classNode);
                LookForCommandHandlers(classNode);
                LookForEvents(classNode);
                LookForEventHandlers(classNode);

                var methods = node.DescendentNodes().OfType<MethodDeclarationSyntax>();
                foreach (var method in methods.Where(x => x.BodyOpt != null))
                {
                    Visit(method);
                }
            }
        }

        private void LookForEvents(ClassDeclarationSyntax node)
        {
            if (node.BaseListOpt != null && node.BaseListOpt.Types.Any(t => t.PlainName == EventIfx))
            {
                Events.Add(node);
            }
        }

        private void LookForCommands(ClassDeclarationSyntax node)
        {
            if (node.BaseListOpt != null && node.BaseListOpt.Types.Any(t => t.PlainName == CommandIfx))
            {
                Commands.Add(node);
            }
        }

        private void LookForEventHandlers(ClassDeclarationSyntax node)
        {
            var handles = _eventHandlerDeclarationVisitor.Visit(node);
            
            if (handles.Any())
            {
                EventHandlers.Add(node);
            }
        }

        private void LookForCommandHandlers(ClassDeclarationSyntax node)
        {
            var handles = _cmdHandlerDeclarationVisitor.Visit(node);

            if (handles.Any())
            {
                CommandHandlers.Add(node);
            }
        }
    }
}