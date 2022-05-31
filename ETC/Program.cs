using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ETC
{
    internal static class Program
    {
        private const string ArgumentsError =
            "ETC needs 2 arguments : \n - Path to a DBContext file \n - Namespace of your Configurations files";

        private static readonly string ResultPath = "./Configurations";
        private static readonly string TemplateFilePath = "./EntityTypeConfigurationTemplate.txt";
        private static readonly List<(string entityName, string code)> Entities = new();

        private static async Task Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine(ArgumentsError);
                return;
            }

            var filePath = args.First();
            var namespaceToUse = args[1];

            try
            {
                var lines = await File.ReadAllLinesAsync(filePath);

                if (Directory.Exists(ResultPath))
                    Directory.Delete(ResultPath, true);

                Directory.CreateDirectory(ResultPath);

                var templateFileContent = await File.ReadAllTextAsync(TemplateFilePath);


                var shouldIncludeCode = false;
                var codeToInclude = new StringBuilder();
                string entityName = string.Empty;
                var isInsideModelBuilder = false;
                foreach (var line in lines)
                {
                    var lineWithoutSpace = line.Trim();
                    if (line.Contains("modelBuilder.Entity"))
                    {
                        var match = Regex.Match(line, @"\<(.*?)\>");
                        entityName = match.Groups[1].Value;
                        isInsideModelBuilder = true;
                    }

                    if (lineWithoutSpace == "});" && isInsideModelBuilder)
                    {
                        shouldIncludeCode = false;
                        Entities.Add(new(entityName, codeToInclude.ToString()));
                        codeToInclude.Clear();
                    }

                    if (shouldIncludeCode)
                        codeToInclude.AppendFormat("{0}{1}", line.Replace("entity", "builder"), Environment.NewLine);

                    if (lineWithoutSpace == "{" && isInsideModelBuilder)
                        shouldIncludeCode = true;
                }

                foreach (var (entity, code) in Entities)
                {
                    await using var fileStream =
                        File.Create(Path.Combine(ResultPath, $"{entity}Configuration.cs"));
                    var result = new StringBuilder(templateFileContent);

                    result.Replace("001_MODEL_NAMESPACE", $"{namespaceToUse}.Models");
                    result.Replace("002_CONFIGURATION_NAMESPACE", $"{namespaceToUse}.Configurations");
                    result.Replace("003_ENTITY_NAME_CONFIGURATION", $"{entity}Configuration");
                    result.Replace("004_ENTITY_NAME", $"{entity}");
                    result.Replace("005_CONFIGURE_CONTENT", code);

                    var info = new UTF8Encoding(true).GetBytes(result.ToString());
                    fileStream.Write(info, 0, result.Length);
                }

                await using var dbContextFileStream =
                    File.Create(Path.Combine(ResultPath, Path.GetFileName(filePath)));
                var dbcontextfilecontent = new StringBuilder();
                foreach (var (entity, code) in Entities)
                {
                    dbcontextfilecontent.Append($"modelBuilder.ApplyConfiguration(new {entity}Configuration());\n");
                }

                dbContextFileStream.Write(new UTF8Encoding(true).GetBytes(dbcontextfilecontent.ToString()), 0,
                    dbcontextfilecontent.Length);
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine(ex.Message);
            }

            catch (Exception ex)
            {
                Console.WriteLine($"Something went wrong : {ex.Message}");
            }
        }
    }
}