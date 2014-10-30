# A C# solution TODO retriever

This will parse C# files to try to identify any "TODO" comments and map them to the containing namespace, type and property or method (where applicable) using Roslyn. The example code (see Program.cs) will parse solution files to identify C# projects and parse those project files to identify ".cs" files to look for comments in.