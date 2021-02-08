using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace ParseXMLDocumentation
{
    // This program parses multiple XML documentation files and collates them into a single JSON file. This is used to include the documentation for the core classes in the editor.
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Write("\n\tWrong parameters.\n\n\tRequired parameters:\n\n\t\t\tXML documentation path(s) [...]\t\tOutput prefix\n\n");
                return 64;
            }

            string outputFile = args.Last();

            string[] xmlDocDirectories = args.Take(args.Length - 1).ToArray();

            Dictionary<string, string> xmlDocumentation = new Dictionary<string, string>();

            HashSet<string> dllFilesDocumented = new HashSet<string>();

            foreach (string directory in xmlDocDirectories)
            {
                foreach (string dll in from el in Directory.GetFiles(directory, "*.dll") select Path.GetFileName(el))
                {
                    dllFilesDocumented.Add(dll);
                }

                foreach (string xmlFile in Directory.GetFiles(directory, "*.xml"))
                {
                    XDocument doc = XDocument.Load(xmlFile);

                    foreach (XElement element in doc.Descendants("member"))
                    {
                        string name = element.Attribute("name").Value;
                        using XmlReader reader = element.CreateReader();
                        reader.MoveToContent();
                        string value = reader.ReadInnerXml();

                        xmlDocumentation[name] = value;
                    }
                }
            }

            File.WriteAllLines(outputFile + ".dll.list", dllFilesDocumented);

            using (FileStream fs = new FileStream(outputFile + ".json", FileMode.Create))
            {
                using System.Text.Json.Utf8JsonWriter writer = new System.Text.Json.Utf8JsonWriter(fs);
                System.Text.Json.JsonSerializer.Serialize<Dictionary<string, string>>(writer, xmlDocumentation);
            }

            return 0;
        }
    }
}
