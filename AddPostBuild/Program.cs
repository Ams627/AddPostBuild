using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace AddPostBuild;

internal enum ProjectFileErrors
{
    None,
    AlreadyHasPostBuild,
    NoProjectRoot,
    NoSdkAttribute,
    NoPropertyGroup
}

internal class Program
{
    private static void Main(string[] args)
    {
        try
        {
            var files = GetFiles(Directory.GetCurrentDirectory(), "*.csproj");
            if (files.Count > 3)
            {
                Console.Error.WriteLine($"Too many csproj files to process: the limit is three.");
                Environment.Exit(-1);
            }
            foreach (var file in files)
            {
                var error = AddPostBuildCopy(file);
                var message = error switch
                {
                    ProjectFileErrors.None => $"Updated file {file} to have a postbuild step",
                    ProjectFileErrors.AlreadyHasPostBuild => $"File {file} already has a postbuild step",
                    ProjectFileErrors.NoProjectRoot => $"File {file} does not have <Project> as the root element",
                    ProjectFileErrors.NoSdkAttribute => $"File {file} has a <Project> root node but does not have an Sdk Attribute in the root node",
                    ProjectFileErrors.NoPropertyGroup => $"File {file} has a <Project> root node and Sdk attribute but does not have any PropertyGroup nodes",
                };
                Console.WriteLine(message);
            }
        }
        catch (Exception ex)
        {
            var fullname = System.Reflection.Assembly.GetEntryAssembly().Location;
            var progname = Path.GetFileNameWithoutExtension(fullname);
            Console.Error.WriteLine($"{progname} Error: {ex.Message}");
        }
    }
    private static List<string> GetFiles(string startDir, string pattern)
    {
        var result = new List<string>();
        var dirStack = new Stack<string>();
        dirStack.Push(startDir);

        while (dirStack.Count > 0)
        {
            var dir = dirStack.Pop();
            var files = Directory.GetFiles(dir, pattern);
            result.AddRange(files);

            var subDirs = Directory.GetDirectories(dir);
            foreach (var subDir in subDirs)
            {
                dirStack.Push(subDir);
            }
        }
        return result;
    }

    private static ProjectFileErrors AddPostBuildCopy(string csProjFile)
    {
        var doc = XDocument.Load(csProjFile);
        var rootName = doc.Root.Name.LocalName;
        var sdkAttribute = doc.Root.Attribute("Sdk");

        if (rootName != "Project")
        {
            return ProjectFileErrors.NoProjectRoot;
        }

        if (sdkAttribute == null)
        {
            return ProjectFileErrors.NoSdkAttribute;
        }

        var targets = doc.Descendants("Target").Where(x => x.Attribute("Name")?.Value == "PostBuild" && x.Attribute("AfterTargets")?.Value == "PostBuildEvent");

        if (targets.Any())
        {
            return ProjectFileErrors.AlreadyHasPostBuild;
        }

        var propGroup = doc.Descendants("PropertyGroup").FirstOrDefault();
        if (propGroup == default)
        {
            return ProjectFileErrors.NoPropertyGroup;
        }

        if (propGroup.Element("BinDir") == null)
        {
            propGroup.Add(new XElement("BinDir", @"c:\bin"));
        }

        doc.Root.Add(
            new XElement("Target",
                new XAttribute("Name", "PostBuild"),
                new XAttribute("AfterTargets", "PostBuildEvent"),
                new XElement("Exec", new XAttribute("Command", $@""))));
        ;

        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = true,
        };

        using var writer = XmlWriter.Create(csProjFile, settings);
        doc.Save(writer);
        return ProjectFileErrors.None;
    }
}
