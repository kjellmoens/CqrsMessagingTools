<Query Kind="Program">
  <Reference Relative="..\MIL.Visitors\bin\Debug\MIL.Visitors.dll">D:\source\CqrsMessagingTools\MIL.Visitors\bin\Debug\MIL.Visitors.dll</Reference>
  <GACReference>Roslyn.Compilers, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35</GACReference>
  <GACReference>Roslyn.Compilers.CSharp, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35</GACReference>
  <GACReference>Roslyn.Services, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35</GACReference>
  <GACReference>Roslyn.Services.CSharp, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35</GACReference>
  <Namespace>Roslyn.Compilers.CSharp</Namespace>
  <Namespace>Roslyn.Services</Namespace>
  <Namespace>Roslyn.Compilers</Namespace>
  <Namespace>MIL.Visitors</Namespace>
  <Namespace>Roslyn.Compilers.Common</Namespace>
</Query>

void Main()
{


var cancel = new CancellationToken(false);
	const string Code = @"
namespace TestCode 
{
	using System;  
	
	public class Program
	{
		public static void Main()
		{
			var cmd = new Foo();
			
			Program.Send(cmd);
		}
		
		public static void Send(ICommand command) 
		{ 
			Console.WriteLine(""Sent!""); 
		}
		
	}
	public interface ICommand {}
	public class Foo : ICommand { }
	public interface ICommandHandler<T> where T : ICommand
	{
		void Handles(T command);
	}	                           
	public class FoomandHandler : ICommandHandler<Foo>
	{
		public bool WasCalled { get; private set; }
		public void Handles(Foo command)
		{
			WasCalled = true;
			Console.Write(""Foomand handled {0}"", command.Name);
		}
	}   
	public class BadFooHandler : ICommandHandler<Foo> 
	{ 
		public void Handles(Foo command) { throw new NotImplementedException(); }
	}                                        
}";
var compilation = Compilation.Create("test.dll")
		.AddSyntaxTrees(SyntaxTree.ParseCompilationUnit(Code))
		.UpdateOptions(new CompilationOptions("TestCode.Program", "Program", AssemblyKind.ConsoleApplication))
		.AddReferences(new AssemblyFileReference(typeof(object).Assembly.Location));
 
	
	var d = compilation.GetDeclarationDiagnostics().Dump();
	var milA = new MilSyntaxAnalysis();
	milA.AnalyzeInitiatingSequence(compilation);
		
}
public class MilSyntaxAnalysis
{
	Func<NamespaceOrTypeSymbol, IEnumerable<TypeSymbol>> nameExtractor; 
	
	public MilSyntaxAnalysis()
	{
		nameExtractor = name => 
		{	
			var members = name.GetMembers().ToList();
			return members.Concat(members.OfType<NamespaceSymbol>()
				.SelectMany(x => nameExtractor(x)))
				.OfType<TypeSymbol>();
		};
	}
	
	public void AnalyzeInitiatingSequence(Compilation compilation)
	{
		var walker = new MIL.Visitors.MilSyntaxWalker();
		var types = compilation.SourceModule.GlobalNamespace
		.GetMembers().OfType<NamespaceOrTypeSymbol>()
		.SelectMany(nameExtractor);
		 
		var cmd =  types.Where(x => x.BaseType != null && x.Interfaces.Select(y => y.Name).Contains("ICommand"));
	 	
		foreach (var t in compilation.SyntaxTrees.Select(x => x.GetReference(x.Root)))
		{
			var methodBodies = t.GetSyntax().DescendentNodesAndSelf().OfType<MethodDeclarationSyntax>();
			foreach (var stmt in methodBodies
				.Select(mb => mb.BodyOpt)
				.Where(body => body != null)
				.SelectMany(st => st.Statements, (b, s) => s)
				.SelectMany(sx => sx.DescendentNodesAndSelf().OfType<MemberAccessExpressionSyntax>())
				.Where(ma => ((MemberAccessExpressionSyntax)ma).Name.GetText() == "Send"))
			{
				stmt.GetText().Dump();
 			 
				var model = compilation.GetSemanticModel(t.SyntaxTree);
				var dataFlow = model.AnalyzeRegionDataFlow(stmt.FullSpan);
				var send = (LocalSymbol)dataFlow.ReadOutside.First();
				if (cmd.Contains(send.Type))
				{
					Console.WriteLine("Cmd has this type {0}", send.Type.Name);
				}
			}
		}
	}
	
}
// Define other methods and classes here