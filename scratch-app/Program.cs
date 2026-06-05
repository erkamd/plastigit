using System;
using System.Collections.Generic;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Running Commit Graph Generator Test...");
        
        // Initialize Git Executable path
        SourceGit.Native.OS.GitExecutable = "git";
        
        // Initialize default pens to avoid division by zero in lane % s_penCount
        SourceGit.Models.CommitGraph.SetDefaultPens();
        
        string repoPath = @"D:\GithubDesktop\BestGitGUI\test-repo";
        var cmd = new SourceGit.Commands.QueryCommits(repoPath, "--branches --remotes --tags HEAD", true);
        var commits = await cmd.GetResultAsync();
        
        Console.WriteLine($"Total Commits Loaded: {commits.Count}");
        
        var graph = SourceGit.Models.CommitGraph.Generate(
            commits, 
            true, 
            false, 
            SourceGit.Models.CommitGraphHighlighting.All, 
            new HashSet<string>()
        );
        
        Console.WriteLine($"Total Lanes Allocated: {graph.TotalLanes}");
        
        Console.WriteLine("\n--- PATHS ---");
        foreach (var path in graph.Paths)
        {
            Console.WriteLine($"Path Lane: {path.Lane} | BranchName: {path.BranchName} | ColorIndex: {path.Color} | Points Count: {path.Points.Count}");
            foreach (var pt in path.Points)
            {
                Console.WriteLine($"  Pt: ({pt.X:0.0}, {pt.Y:0.0})");
            }
        }

        Console.WriteLine("\n--- LINKS ---");
        foreach (var link in graph.Links)
        {
            Console.WriteLine($"Link Start: ({link.Start.X:0.0}, {link.Start.Y:0.0}) | End: ({link.End.X:0.0}, {link.End.Y:0.0}) | Control: ({link.Control.X:0.0}, {link.Control.Y:0.0})");
        }
        
        Console.WriteLine("\n--- COMMITS ---");
        for (int i = 0; i < commits.Count; i++)
        {
            var c = commits[i];
            var dot = graph.Dots[i];
            double lane = (dot.Center.X - 10) / 12.0;
            Console.WriteLine($"[{i}] SHA: {c.SHA.Substring(0, 7)} | Lane: {lane:0.0} | ColorIndex: {c.Color} | Subject: {c.Subject}");
        }
    }
}
